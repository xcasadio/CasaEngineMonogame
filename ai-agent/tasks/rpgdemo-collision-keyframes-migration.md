# RPGDemo — migration des volumes de sprite vers les `collision_keyframes` d'animation

Légende : ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

Date : 2026-08-23. Branche : `bepu-physics`. Décision utilisateur : migration par script + test.

## Constat

Le commit `089c2e4c` (phase E de [collision-2d-3d-architecture.md](../../docs/engine/collision-2d-3d-architecture.md))
a supprimé le chemin « corps par sprite » d'`AnimatedSpriteComponent` : un sprite animé ne tire plus ses
volumes que de `collision_keyframes` sur l'asset d'animation. Les assets du RPGDemo n'ont pas été migrés :
les 220 volumes vivent encore dans `Projects/RPGDemo/TileSets/{sword,player,octopus}_*.sprite` (clé
`collisions`) et **aucun** `.anim2d` de `Projects/` ne porte `collision_keyframes`. Résultat : l'épée,
Link et l'octopus n'ont aucun corps — ni hitbox affichée, ni collision avec l'herbe ou l'ennemi.

Constat secondaire : les profils hérités des `collision_type` d'origine sont inversés sémantiquement
(épée = `DamageableVolume`, Link = `AttackVolume`, octopus = `DamageableVolume`). Sans effet sur la
détection (deux capteurs qui overlappent tout), mais couleurs de debug trompeuses.

## Tranche unique — script de migration + test ⏳

**Périmètre (propriété exclusive)** :
- Nouveau script `Tools/migrate-sprite-collisions-to-keyframes.py` (Python 3, stdlib `json` seulement,
  déterministe, idempotent : relancer sur un asset déjà migré remplace `collision_keyframes` par le même
  contenu). Usage : `python Tools/migrate-sprite-collisions-to-keyframes.py Projects/RPGDemo`
  (+ option `--dry-run` qui affiche le résumé sans écrire, `--swap-profiles` décrit plus bas).
- Les 64 fichiers `Projects/RPGDemo/TileSets/*.anim2d` (réécrits par le script) et les `.sprite` de
  `Projects/RPGDemo/TileSets/` dont le `collision_profile` est corrigé (épée → `AttackVolume`,
  `player_*` → `DamageableVolume`, octopus inchangé).
- Nouveau test `CasaEngine.Tests/Physics/RpgDemoCollisionKeyframesTests.cs`.
- Ligne d'index dans `ai-agent/README.md` (tableau des tâches) et ce fichier (suivi).
- Interdit : tout code du moteur (`CasaEngine/`, `CasaEngine.Editor*/`), les entités `.entity`,
  `AssetInfos.json`, les autres projets. Ne pas toucher ni committer les modifications préexistantes de
  l'arbre de travail (`CasaEngine.Launcher/Program.cs`, `Projects/SampleProject/.casaeditor/viewport.editor.json`).

**Règle de conversion** (reproduit exactement l'ancien placement, cf.
[SpriteCollisionHelper.UpdateBodyTransformation](../../CasaEngine/Framework/Scene/Entities/Components/SpriteCollisionHelper.cs)
et le code supprimé par `089c2e4c`, qui passait `spriteData.Origin` = `hotspot`) :

1. Indexer tous les `Projects/RPGDemo/**/*.sprite` par leur `id`.
2. Pour chaque `.anim2d`, pour chaque piste `tracks[]` dont `property == "Sprite"`, pour chaque
   `sprite_keyframes[]` (`time_seconds`, `value` = id de sprite) :
   - charger le sprite ; `hotspot = {x, y}` ; `collisions[]` (clés : `collision_profile`, `shape_type`
     (`Rectangle` ou `Circle`), `location {x, y}`, `orientation`, `w`, `h` ou `radius`) ;
   - émettre un keyframe `{ "time_seconds": t, "fixtures": [...] }` avec, par volume :
     - **tout `shape` est un `Shape3d : ObjectBase`** : son nœud porte obligatoirement `"id"` (GUID)
       et `"name"` (`"Object <id>"`, la forme qu'écrit `EditorJsonSaveHelper.SaveObjectBase`, cf.
       `Projects/RPGDemo/Entities/character_link.entity` lignes 72-77), **avant** `shape_type` ;
       `ObjectBase.Load` lève sans eux. L'id est déterministe : UUID v5 (`uuid.uuid5`) sur la chaîne
       `"<id du sprite>:<index du volume>"` avec un namespace fixe (par ex. `uuid.NAMESPACE_URL`),
       pour que le script soit idempotent (fichier identique à la relance) ;
     - `Rectangle` → `shape = { "id": …, "name": "Object …", "shape_type": "Box", "w": w, "h": h, "l": 1.0 }`,
       `local_position = { "x": location.x - hotspot.x + w/2, "y": -(location.y - hotspot.y + h/2), "z": 0.0 }` ;
     - `Circle` → `shape = { "id": …, "name": "Object …", "shape_type": "Sphere", "radius": r }`,
       `local_position = { "x": location.x - hotspot.x + r, "y": -(location.y - hotspot.y + r), "z": 0.0 }` ;
     - `local_rotation = { "x": 0.0, "y": 0.0, "z": 0.0, "w": 1.0 }` (l'`orientation` des volumes
       est 0 partout : le script **échoue** avec un message si elle ne l'est pas, pas de conversion
       silencieuse) ;
     - `collision_profile` = celui du volume (après correction éventuelle), `tag` = `name` du sprite ;
   - un sprite sans volume produit un keyframe avec `"fixtures": []` (sémantique Step : rien d'actif
     jusqu'au keyframe suivant — c'est voulu, une frame sans hitbox ne touche rien).
3. Les keyframes sont triés par `time_seconds` ; deux keyframes au même temps (deux pistes Sprite) sont
   fusionnés (fixtures concaténées). **Non-objectif explicite** : une animation sans piste `Sprite`
   (sprites fournis par `parts[].default_sprite_id`, offsets `parts[].default_position`) n'est pas
   migrée et ne reçoit pas de clé `collision_keyframes` — c'est le cas de
   `swordman_composed_demo.anim2d` (pistes `Position`/`DrawOrder` seulement) ; le script la liste
   dans son résumé comme « ignorée : pas de piste Sprite ». Donc **63** animations migrées sur 64. Clé `collision_keyframes` insérée **après** `tracks` ; le reste du
   fichier est réécrit à l'identique (indentation 2 espaces, même ordre de clés, même fin de ligne que
   l'original — vérifier avec `git diff` que seules les lignes `collision_keyframes` s'ajoutent).
4. `--swap-profiles` : dans les `.sprite` listés, remplace `collision_profile` `DamageableVolume` →
   `AttackVolume` pour `sword_*.sprite`, `AttackVolume` → `DamageableVolume` pour `player_*.sprite`
   (l'octopus garde `DamageableVolume`). Appliquer **avant** l'émission des keyframes (une seule
   commande : `python ... Projects/RPGDemo --swap-profiles`). Réécriture des `.sprite` à l'identique
   hors cette valeur.

Le schéma cible est celui que lit `Animation2dCollisionKeyframeData.Load` →
`ColliderFixture.Load` → `ShapeLoader.LoadShape3d` (`Box.Load` lit `w`, `h`, `l` ; `Sphere` lit
`radius` ; vecteurs `x,y,z` ; quaternion `x,y,z,w`) et qu'écrit
`EditorAssetJsonSerializer` (`SaveColliderFixture`, `SaveShape3d`). Le relire dans le code avant d'écrire.

**Test `RpgDemoCollisionKeyframesTests`** (xunit, style de `MigratedCollisionAssetTests`) :
- `EveryAnimation_HasOneCollisionKeyframePerSpriteKeyframe` : pour chaque `.anim2d` de
  `Projects/RPGDemo/TileSets` **qui a une piste `Sprite`** (63 ; `Assert.Equal(63, …)` sur le nombre de
  fichiers retenus), charger via `Animation2dData.Load(JObject)` (chemin runtime réel) et
  vérifier `CollisionKeyframes.Count == nombre de sprite_keyframes distincts par temps`, chaque fixture
  ayant une `Shape` non nulle (`Box` ou `Sphere`) et un `ProfileName` connu de
  `GameSettings.PhysicsEngineSettings.CollisionProfiles`.
- `SwordAttackAnimations_CarryAttackVolumes` : pour chaque `baton_attack*.anim2d`, au moins un keyframe
  a une fixture `AttackVolume` ; pour les animations de Link — fichiers **`swordman_*.anim2d`** (les
  sprites s'appellent `player_*`, les animations `swordman_*`), **hors** `swordman_composed_demo`
  (pas de piste Sprite) **et hors `swordman_dead_stand_*` / `swordman_dead_walking_*`** (leurs sprites
  `player_84`…`player_105` n'ont aucun volume : elles produisent légitimement des keyframes à
  `fixtures: []`, un mort ne se fait pas toucher) — au moins une fixture `DamageableVolume` ; pour
  `octopus_*.anim2d`, `DamageableVolume`. Chaque liste doit contenir **au moins un fichier**
  (`Assert.NotEmpty`) : un test qui itère sur un ensemble vide ne prouve rien. Le test vérifie aussi, en
  sens inverse, que chaque `swordman_dead_*` n'a **aucune** fixture (le script n'invente pas de volume).
- `SwordKeyframe_PlacesTheVolumeLikeTheSpriteHelperDid` : cas concret calculé à la main à partir de
  `sword_29.sprite` (hotspot `(-15, 17)`, volume `location (16, 8)`, `w 34`, `h 7`) → la fixture du
  keyframe qui référence ce sprite a `LocalPosition == (16 - (-15) + 17, -(8 - 17 + 3.5), 0) = (48, 5.5, 0)`
  et `Shape` = `Box (34, 7, 1)`.
- `ActivatedSwordKeyframe_CreatesAttackBodies` (intégration) : `PhysicsWorld` + une entité avec un
  `AnimatedSpriteComponent` chargé avec `baton_attack2_right` (réutiliser le montage de
  `AnimatedSpriteCollisionTimelineTests`, qui construit des animations et des sprites en mémoire ; si
  le chargement d'un vrai `.anim2d` du projet exige l'`AssetContentManager` complet, charger l'asset
  JSON en mémoire et alimenter le composant par le même chemin que ce test) → après `Update` jusqu'au
  premier keyframe à fixtures non vides, `GetActiveCollisionBodies` contient un corps dont
  `CollisionProfile.GroupBit` est celui d'`AttackVolume`. Si ce montage s'avère hors de portée
  sans toucher au moteur, le dire et se limiter aux trois premiers tests.

**Budget et arrêt** : cinq passes de réglage ; tout besoin de modifier le moteur arrête la tranche et est
reporté (le moteur n'est pas dans le périmètre) ; jamais de `Skip`, jamais d'assertion supprimée.

**Acceptation** :
- `python Tools/migrate-sprite-collisions-to-keyframes.py Projects/RPGDemo --dry-run` affiche
  63 animations migrées + 1 ignorée (`swordman_composed_demo`), nombre de keyframes émis, nombre de
  fixtures, 0 erreur ; une seconde exécution réelle ne change aucun fichier (`git status` propre
  après la première) ;
- `git diff --stat` : 63 `.anim2d` + les `.sprite` épée/player modifiés, rien d'autre ;
- `rg -l "collision_keyframes" Projects/RPGDemo/TileSets/*.anim2d | wc -l` → 63 ;
- `jq -e .` valide sur chaque fichier réécrit ;
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter "FullyQualifiedName~RpgDemoCollisionKeyframes"` vert ;
- suite physique (`--filter FullyQualifiedName~CasaEngine.Tests.Physics`) : 178 + nouveaux, 0 échec ;
- `dotnet build Projects/CasaEngine.RPGDemo` (ou via la solution) inchangé/vert.
- Validation visuelle (utilisateur, 🧪) : dans le RPGDemo, attaquer à l'épée affiche les volumes rouges
  (épée) et verts (Link/octopus) dans l'overlay physique, l'herbe casse, l'octopus est touché.

## Suivi

| Étape | Statut | Commit(s) | Vérification |
| --- | --- | --- | --- |
| Script + assets migrés | ⏳ | | |
| Test | ⏳ | | |
| Validation visuelle | 🧪 | | utilisateur |
