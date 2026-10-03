# Plan agent IA — Semi-transparence PSX par texel des sprites (champ de `SpriteData`, deux dessins disjoints, capacité de la file)

Plan d'exécution de la partie moteur de la tranche E19.g G2a du portage Alundra (plan parent `docs/plan-e19-opcodes.md`, §1.2o.2,
dépôt `alundra-casaengine-project-converter`). **Approuvé : plan parent §1.2o.2 READY (relecture de clôture) le 2026-10-03, travail
dans le moteur autorisé par l'auteur le 2026-10-03.** Les décisions ci-dessous viennent du plan parent (D-E19-52, règles G2a-R1 à
G2a-R3, tâche G2a-1) : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Un sprite peut porter un mode de semi-transparence PSX (`None`, `Mode0` à `Mode3`) ; le chemin trié de `AnimatedSpriteComponent`
le dessine en deux entrées de même clé de tri, sur deux fenêtres d'alpha brut disjointes :

- texels opaques (alpha brut dans `(0,75 ; 1]`) avec l'état de mélange opaque ;
- texels STP (alpha brut dans `(0,25 ; 0,75]`) avec l'état du mode (`Mode0` alpha non prémultiplié, `Mode1` additif, `Mode2`
  soustractif, `Mode3` additif avec la couleur (64, 64, 64) qui remplace la couleur du composant).

La file de `SpriteRendererComponent` peut dépasser 10 000 entrées (tampon de sommets et `VertexBuffer` qui grandissent).

## Hors périmètre

- Les quads à quatre sommets libres (G2b), les effets (G1/G3), le chemin par `zOrder` (reste opaque, documenté).
- La conversion des données Alundra (convertisseur du dépôt parent, G2a-2).
- Aucune surcharge existante de `DrawSprite` ne change de signature ni de valeur par défaut.

## État vérifié du dépôt (2026-10-03)

- Branche `chantier/e19g2a-psx-semi` créée depuis `chantier/e19m2-cellular-binary` (`61358ac0`) : le pointeur du sous-module du
  parent ne peut désigner qu'un commit et doit garder E19.k2 et E19.m2. Modification locale de l'auteur dans
  `CasaEngine.Launcher/Program.cs` : ne jamais l'indexer, ne jamais la modifier.
- Shader : `CasaEngine/Content/Shaders/SpriteBatch.fx:40-49` rejette `(tex × Color).a <= 0.01`. Un seul exemplaire compilé par le
  moteur (`Content.mgcb:88-93`) ; `Projects/RPGDemo/Shaders/spritebatch.fx` est une copie de projet qui n'a pas le paramètre
  (`TryReloadBuiltInShader` peut donc recevoir un effet sans `AlphaWindow` : la pose de la fenêtre tolère l'absence du paramètre).
- `SpriteRendererComponent` : `_effect` est partagé par `Draw`, `DrawStaticBatch` et `DrawDirectly` ; ses paramètres persistent d'un
  dessin à l'autre. `UpdateBuffer` écrit `_vertices` (40 000 sommets) pour chaque entrée et lève `IndexOutOfRange` à la 10 001e.
- `AnimatedSpriteComponent.DrawComposedAnimation` : chemin trié (entités à `DepthSortable2DComponent`) sans mélange, chemin par
  `zOrder` sans clé.
- `EditorAssetJsonSerializer.SaveSpriteData` écrit `SpriteData` ; `SpriteData.Load` le lit ; le convertisseur du parent écrit ses
  `.sprite` par ce sérialiseur.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| G2a-R1 | Shader : le rejet sur le produit `(tex × Color).a <= 0.01` reste pour tout dessin ; s'y ajoute une fenêtre sur l'alpha **brut** du texel, `(AlphaMin ; AlphaMax]`, paramètre `AlphaWindow` (float2), neutre (−1 ; 2]. Échantillonnage par point seulement (documenté dans le shader). **Chaque** chemin qui dessine avec `_effect` (`Draw` à chaque entrée, `DrawStaticBatch`, `DrawDirectly`, et après `TryReloadBuiltInShader`) pose la fenêtre qu'il veut. |
| G2a-R2 | `SpriteData.PsxSemiTransparency` (`SpritePsxSemiTransparency` : `None`, `Mode0` à `Mode3`), clé JSON `psx_semi_transparency` (nom du membre), lue par `SpriteData.Load` (absente = `None`), écrite par `SaveSpriteData` **seulement quand elle n'est pas `None`**. `AnimatedSpriteComponent` passe le mode de chaque partie au chemin trié par une **nouvelle surcharge** de `DrawSprite` ; le chemin par `zOrder` reste opaque. |
| G2a-R3 | Une partie de mode non `None` = deux entrées de même clé (opaque `(0,75 ; 1]`, puis STP `(0,25 ; 0,75]`) ; une partie `None` = une entrée à la fenêtre neutre ; le dédoublement ne dépend que du mode PSX, jamais du `SpriteBlendMode`. Capacité : `_vertices` et le `VertexBuffer` grandissent ; la borne de `SetData` suit la capacité réelle ; le tampon d'indices (six indices) ne change pas. |

## Coutures internes (nommées pour les tests sans périphérique)

- **Remplissage des sommets** : `internal int FillVertices()` de `SpriteRendererComponent` (tri compris, croissance de `_vertices`,
  rend le nombre de sommets écrits) ; `UpdateBuffer` l'appelle puis envoie au GPU. Les sommets sont lus par réflexion sur `_vertices`
  (style des tests existants).
- **Fenêtre posée par les chemins de dessin** : `internal Action<float, float> AlphaWindowWriter` de `SpriteRendererComponent`
  (`null` = écrit le paramètre `AlphaWindow` de l'effet) ; `Draw`, `DrawStaticBatch` et `DrawDirectly` posent la fenêtre par la
  méthode `PoseAlphaWindow` **avant** de toucher au périphérique. Le test installe un enregistreur et appelle `DrawDirectly` /
  `DrawStaticBatch` sur un `Effect` non initialisé : la pose est observée, puis le `NullReferenceException` de la partie qui touche
  le périphérique est attendu (elle ne tourne pas sans GPU). La preuve sur périphérique est portée par la démo des modes.

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/e19g2a-psx-semi`**, créée depuis `chantier/e19m2-cellular-binary` (`61358ac0`). Ne jamais committer sur
  `main`.
- **Une seule tâche à la fois**, statut mis à jour dans le même commit que la tâche. Tests d'abord : rouge constaté (valeurs lues),
  puis vert. Le rouge se lit sur le code d'avant avec la surface d'API minimale ajoutée sans comportement.
- **Un commit par tâche**, message en anglais `type(area): summary`. **Ne jamais pousser, ne jamais merger.**
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ; `git add` fichier par fichier.
- Une valeur écrite d'avance que la mesure contredit est un ARRÊT ; un test existant qui change ou rougit aussi.
- `CasaEngine.Tests` n'est pas dans le `.sln` : le construire explicitement.
- Démos : lancées depuis `CasaEngine.Demos/` (des assets manquent du `Content.mgcb`) ; capture du back-buffer en processus
  (`GetBackBufferData`), jamais de capture du bureau.
- Langue : plan en français ; code, commits, docs et ADR en anglais.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug` sans erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-build --blame-hang-timeout 300s` tout vert (un test de
  matériaux est connu pour être instable).
- Démos (modes et capacité) : captures du back-buffer égales aux valeurs ci-dessous à ±1 par canal (RVB).

---

## Phase 1 — Moteur

### ✅ E1 — Champ de `SpriteData`, fenêtre du shader, deux dessins disjoints

- Objectif : G2a-R1, G2a-R2 et G2a-R3 (hors capacité).
- Fichiers : `CasaEngine/Content/Shaders/SpriteBatch.fx`, `CasaEngine/Framework/Assets/Sprites/SpriteData.cs`,
  `CasaEngine/Framework/Assets/Sprites/SpritePsxSemiTransparency.cs` (nouveau), `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`,
  `CasaEngine/Framework/Application/Components/SpriteRendererComponent.cs`,
  `CasaEngine/Framework/Scene/Entities/Components/AnimatedSpriteComponent.cs`, tests dans `CasaEngine.Tests/Rendering/` et
  `CasaEngine.Tests/Assets/`.
- Valeurs de test (écrites d'avance) sur la liste des entrées en file (avant le tri, ordre d'insertion ; aucun ordre relatif entre
  les deux entrées d'une partie n'est affirmé après le tri) :
  - partie `None` : 1 entrée, `BlendMode` opaque, fenêtre (−1 ; 2], couleur d'origine ;
  - partie de chaque mode `Mode0` à `Mode3` : 2 entrées de même clé de tri ; opaque : fenêtre (0,75 ; 1], `BlendMode` opaque, couleur
    d'origine ; STP : fenêtre (0,25 ; 0,75], `BlendMode` `AlphaBlend` (`Mode0`), `Additive` (`Mode1`), `Subtractive` (`Mode2`),
    `Additive` (`Mode3`) avec la couleur (64, 64, 64, 255) à la place de la couleur du composant ;
  - le `SpriteBlendMode` des appelants existants ne dédouble rien (une entrée) ;
  - `SpriteData` : sans le champ `None`, écrit sans clé ; avec `Mode1` la clé `psx_semi_transparency` vaut `"Mode1"` et se relit ;
    un nom inconnu se relit `None` ;
  - `AnimatedSpriteComponent` d'un sprite `Mode2` à `DepthSortable2DComponent` : 2 entrées ; sans `DepthSortable2DComponent` : 1
    entrée (chemin par `zOrder` opaque) ;
  - `Draw`/`DrawStaticBatch`/`DrawDirectly` posent la fenêtre neutre (−1 ; 2) avant de toucher au périphérique.
- Validation faite : rouges d'abord sur le code d'avant avec la surface d'API ajoutée sans comportement (champ, surcharge qui
  ignore le mode, couture `AlphaWindowWriter` muette) : 12 tests rouges sur 20 (le 13e rouge de la série, la capacité, se lit en E2) ; valeurs lues : `SpriteData`
  écrit sans clé là où `"ModeN"` est attendu (4 cas, lu `null`) ; une entrée là où deux sont attendues (4 modes, et
  `AnimatedSpriteComponent` à `DepthSortable2DComponent`) ; aucune pose de fenêtre sur `Draw`, `DrawStaticBatch` et `DrawDirectly`
  (liste vide là où `[(-1 ; 2)]` est attendu). Les cas `None`, chemin par `zOrder`, appelant à `SpriteBlendMode`, entrée recyclée
  du pool et nom inconnu étaient verts d'avance (gardes). Verts après : 20 tests ajoutés (13 dans
  `SpriteRendererComponentPsxSemiTransparencyTests`, 7 dans `SpriteDataPsxSemiTransparencyTests`, théories comptées par cas),
  `CasaEngine.Tests` 2531/2531 (2511 avant, +20 ici), aucun test existant touché. Aucune valeur écrite d'avance contredite.
  L'entrée `AnimatedSpriteComponent` des tests passe par un `IGraphicsDeviceService` factice dont le périphérique est un objet non
  initialisé (`ScissorRectangle` rend un rectangle vide).
- Commit : `feat(rendering): sprites carry a PSX semi-transparency mode drawn as two disjoint passes`

### ✅ E2 — Capacité de la file au-delà de 10 000 entrées

- Objectif : G2a-R3, capacité.
- Fichiers : `CasaEngine/Framework/Application/Components/SpriteRendererComponent.cs`, tests dans `CasaEngine.Tests/Rendering/`.
- Valeurs de test : 10 001 entrées en file (clés croissantes) : `FillVertices` ne lève rien, rend 40 004 et remplit les sommets de la
  10 001e à ses positions (aujourd'hui : `IndexOutOfRangeException`).
- Validation faite : rouge d'abord sur le code d'E1 : `IndexOutOfRangeException` dans `FillVertices` à la 10 001e entrée (valeur
  écrite d'avance : « aujourd'hui `IndexOutOfRange` », lue égale) ; le test des 10 entrées (tableau gardé, 40 sommets) était vert
  d'avance. Verts après : `_vertices` grandit (le plus grand de la taille requise et du double), `UpdateBuffer` recrée le
  `VertexBuffer` quand le tableau le dépasse et envoie `vertexCount` sommets ; `CasaEngine.Tests` 2533/2533 (+2). La croissance du
  `VertexBuffer` lui-même n'est observable que sur périphérique : démo de capacité (E3).
- Commit : `feat(rendering): the sprite queue grows past 10000 entries`

### ✅ E3 — Démos du moteur (modes et capacité)

- Objectif : preuve sur périphérique de G2a-R1 à G2a-R3. Scène « PSX sprite semi-transparency » : fond uni (100, 150, 200), un sprite
  de chaque mode (`AnimatedSpriteComponent` + `DepthSortable2DComponent`), texel opaque (60, 40, 20), texel STP (120, 80, 40, alpha 128),
  texel transparent ; scène « Sprite queue capacity » : 12 000 sprites opaques de 1 × 1 en file à clés croissantes, le dernier dans
  l'ordre de tri un texel vert pur (0, 255, 0) seul au pixel témoin sur le fond (100, 150, 200).
- Capture du back-buffer en processus (variables d'environnement existantes `CASAENGINE_START_DEMO`,
  `CASAENGINE_CAPTURE_SCREENSHOT_PATH`), pixels loin des bords. Valeurs attendues, à ±1 par canal :
  - opaque → (60, 40, 20) ; `Mode0` → (110, 115, 120) ; `Mode1` → (220, 230, 240) ; `Mode2` → (0, 70, 160) ; `Mode3` →
    (130, 170, 210) ; transparent → (100, 150, 200) ;
  - capacité : (0, 255, 0).
- Validation faite : démos `PSX sprite semi-transparency` et `Sprite queue capacity` (`CasaEngine.Demos/Demos/PsxSemiTransparency/`,
  sonde `BackBufferProbe` : `GetBackBufferData` en processus, projection monde vers écran par `Viewport.Project`, comparaison à ±1 par
  canal RVB), lancées depuis `CasaEngine.Demos/` (Debug, fenêtre 1024 × 768). Modes : 15 contrôles sur 15 passent, pixels lus égaux aux
  valeurs écrites d'avance : opaque (60, 40, 20) ; `Mode0` (110, 115, 120) (alpha de back-buffer 191, comme prévu) ; `Mode1` (220, 230,
  240) ; `Mode2` (0, 70, 160) ; `Mode3` (130, 170, 210) ; transparent (100, 150, 200) ; témoin sans mode (STP dessiné opaque) (120, 80,
  40) ; image `scratchpad/e19g2a-exec/demo-out/psx-modes.png`. Capacité : 12 001 entrées en file, pixel témoin (702, 469) lu (0, 255, 0),
  fond voisin (100, 150, 200) ; image `psx-capacity.png`. Rouge de la capacité sur le code d'E1 (sans croissance) : `IndexOutOfRangeException`
  à chaque image dans `log.txt`, ni capture ni lecture (le jeu tourne sans rien dessiner de la file) ; vert avec E2. Écart : le fond est
  une entrée de plus en tête de file (12 001 entrées en tout, 12 000 sprites de texel plus le fond). Doc : `docs/engine/sprite-psx-semi-transparency.md`.
- Commit : `feat(demos): PSX semi-transparency and sprite queue capacity demos`

### ⏳ E4 — ADR-0051

- Objectif : ADR du moteur, `Accepted` (anglais), ligne à l'index `docs/decisions/README.md` (D-E19-52 de l'auteur : champ de
  `SpriteData`, deux dessins disjoints par fenêtres d'alpha, états de mélange, couleur du mode 3, capacité).
- Commit : `docs(adr): ADR-0051 PSX semi-transparency of sprites as two disjoint passes`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun. | — |
