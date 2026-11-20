# Plan agent IA — Écrire un monde depuis des entités non initialisées

Plan d'exécution d'une évolution demandée par le chantier RacingGame `ai-agent/tasks/rgce-worlds-as-assets-tasks.md` (tranche S6 du projet éditeur : les mondes du jeu écrits en assets par un outil, sans jeu lancé).
La décision D1 ci-dessous a été arbitrée avec l'auteur le 2026-11-18 : **ce plan l'applique, il ne la rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Donner à `CasaEngine.EditorServices` une voie publique pour écrire un fichier `.world` à partir d'un monde et d'une liste d'entités construites en code, **sans initialiser ces entités et sans les ajouter au monde**. Le fichier écrit a le format que l'éditeur écrit déjà : il se charge par `World.Load` et l'éditeur le réenregistre à l'identique.

Ce plan ne change ni le format des mondes, ni le chargement, ni l'éditeur.

## État vérifié du dépôt (2026-11-18, `da32906e2` = `main` = `origin/main`)

- `EditorWorldWriter.SaveWorld(World)` (`CasaEngine.EditorServices/EditorWorldWriter.cs:8-13`) appelle `EditorEntityJsonSerializer.SaveWorld(world, node)` puis `EditorAssetWriterService.SaveDocument(world.FileName, node)`.
- `EditorEntityJsonSerializer.SaveWorld` (`CasaEngine.EditorServices/EditorEntityJsonSerializer.cs:21-65`) n'écrit que les références du champ privé `World._entityReferences`, lu par réflexion (`:18`, `:179-182`). Une référence d'`AssetId` vide est écrite en ligne : `asset_id` puis `entity` (`SaveEntityReference`, `:184-202`, par `SaveEntity`, `:67-103`).
- Ce champ n'est rempli que par `World.Load` (`CasaEngine/Framework/Scene/World/World.cs:1046-1051`) et par `World.AddEntityReferenceImmediate` (`World.cs:1138-1150`, `internal`), qui appelle aussitôt `Initialize` et `InitializeWithWorld`. Les voies publiques `EditorWorldEditingService.AddEntity` et `AddEntityReference` (`EditorWorldEditingService.cs:28`, `:37`) passent par elle.
- Sans jeu, `StaticModelComponent.InitializeWithWorld` lit `world.Game.AssetContentManager` : un monde qui porte des modèles ne peut donc pas être rempli par ces voies dans un outil. Un `LightComponent` s'initialise sans jeu (`CasaEngine.Tests/EditorServices/EditorWorldPlaySnapshotTests.cs:13-35`).
- `EntityReference.Entity` a un setter `internal` (`CasaEngine/Framework/Scene/Entities/EntityReference.cs:17`) ; `CasaEngine` déclare `InternalsVisibleTo("CasaEngine.EditorServices")` et `("CasaEngine.Tests")`.
- `EditorAssetWriterService.SaveDocument` (`EditorAssetWriterService.cs:59-70`) écrit sous `EngineEnvironment.ProjectPath` ; l'écriture ne lit ni le catalogue ni un service du jeu.
- Tests : 6469, 0 échec (`ai-agent/tasks/shadow-filter-taps-tasks.md`, T2).
- Arbre de travail propre.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Réponse de l'auteur du 2026-11-18 à la question « outil d'écriture des mondes » : **une petite API publique dans le moteur**, avec tests et ADR, plutôt qu'une réflexion sur un champ privé du moteur depuis l'outil du jeu. |

## Points à valider avec ce plan

Arbitrages proposés par l'agent : l'approbation du plan les valide.

| Réf | Proposition |
|---|---|
| P1 | **Forme de l'API** : une surcharge `EditorWorldWriter.SaveWorld(World world, IReadOnlyList<Entity> entities)`. Elle écrit le fichier `world.FileName` avec les clés de niveau monde de `SaveWorld(World)`, dans le même ordre, et une référence en ligne par entité de la liste, dans l'ordre de la liste. |
| P2 | **Garde-fous** : `ArgumentNullException` si le monde, la liste ou une entité est nul ; `InvalidOperationException` si le monde porte déjà des références d'entités (les deux sources ne se mélangent pas). |
| P3 | **Aucun effet de bord** : après l'appel, le monde n'a toujours aucune référence ni aucune entité, et aucune entité de la liste n'est initialisée (`IsInitialized` faux). |
| P4 | **Une seule écriture du format** : `EditorEntityJsonSerializer.SaveWorld` garde son corps ; la liste des références devient un paramètre interne, pour que les deux surcharges partagent le même code et ne puissent pas diverger. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/offline-world-writer`**, créée depuis `main` (`da32906e2`). Ne jamais committer sur `main`.
- **Une seule tâche à la fois**, icône de statut tenue à jour, note de validation, un commit par tâche qui inclut ce fichier.
- **Ne jamais pousser** ; `main` n'avance qu'à la demande de l'auteur.
- **Ne rien inventer** : sinon ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build** : `dotnet build CasaEngine.Editor.MonoGame.sln`, 0 erreur. **Tests** : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, 0 échec, jamais avec `--no-build`.
- Aucune commande git dans `MGUI` ni `NvgSharp`. `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, commits, `docs/` et ADR en anglais.
- Sérialisation additive : aucun fichier `.world` existant ne change d'un octet.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Tâches

### ✅ T0 — Plan

- Ce fichier et sa ligne dans `ai-agent/README.md`.
- Commit : `docs(ai-agent): plan the offline world writer`

> Validation (2026-11-18) : plan relu par un `plan-verifier` ; première relecture **REVISE** (un blocage : isolation des tests qui changent `EngineEnvironment.ProjectPath`, corrigé dans T1), relecture de la version corrigée par un relecteur neuf : **READY**. Soumis à l'auteur ; T1 ne commence qu'après son approbation.

### ✅ T1 — La surcharge, ses tests, sa documentation

- **Objectif** : P1 à P4.
- **Fichiers** :
  - `CasaEngine.EditorServices/EditorWorldWriter.cs` : la surcharge publique, documentée (`///`) ;
  - `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs` : `SaveWorld` reçoit la liste des références à écrire ; la voie existante lui passe celles du monde, la nouvelle des références en ligne construites pour l'écriture (`AssetId` vide, `Entity` = l'entité) ;
  - `CasaEngine.Tests/EditorServices/EditorWorldWriterTests.cs` (nouveau) ;
  - `docs/editor/asset-documents.md` : un court paragraphe « Writing a world from code » (usage, limites).
- **Isolation des tests** : `EngineEnvironment.ProjectPath` est un statique du processus et xUnit lance les classes de test en parallèle. La classe `EditorWorldWriterTests` porte donc `[Collection(ProjectEnvironmentCollection.Name)]` (`CasaEngine.Tests/ProjectEnvironmentCollection.cs:5-8`, collection sans parallélisme, comme `PlayerStartupSettingsWriterTests.cs:14`). Chaque test sauvegarde `ProjectPath`, pose un dossier temporaire, puis le restaure et supprime le dossier dans un `finally`.
- **Tests** :
  1. **forme** : trois entités (une lumière, une entité sans composant racine, une entité portant un composant à `[SerializedProperty]`) ; le fichier a trois `entity_references`, dans l'ordre, chacune avec `asset_id` vide et un nœud `entity` du bon nom ; les clés de niveau monde sont celles de `SaveWorld(World)` dans le même ordre ; un réglage d'environnement non par défaut (`filter_taps` 64, pipeline de couleur `LinearHdr`) est écrit ;
  2. **équivalence avec la voie existante** : pour un monde de lumières (initialisables sans jeu), le document de la surcharge est égal, par `JToken.DeepEquals`, à celui de `SaveWorld(World)` après `EditorWorldEditingService.AddEntityReference` sur un monde et des entités construits de la même façon avec les mêmes ids ; là où un type n'a pas de constructeur à id, les valeurs `id` sont retirées des deux documents avant la comparaison, et la note de validation le dit ;
  3. **équivalence par entité** : chaque nœud `entity` est égal au document qu'écrit `EditorEntityWriter.SaveEntity` pour la même entité ;
  4. **rechargement** : `new World().Load(document)` ne lève pas, et un second enregistrement du même monde et de la même liste donne les mêmes octets ;
  5. **aucun effet de bord** (P3) ;
  6. **garde-fous** (P2) : monde nul, liste nulle, entité nulle, monde qui porte déjà une référence.
- **Validation** : build à 0 erreur ; tests : 6469 + les nouveaux, 0 échec, lancés deux fois de suite (aucun échec intermittent) ; `rg "Collection\(ProjectEnvironmentCollection.Name\)" CasaEngine.Tests/EditorServices/EditorWorldWriterTests.cs` trouve la classe, et chaque affectation de `ProjectPath` de ce fichier est dans un `try` / `finally` qui restaure ; `git status` : seuls les quatre fichiers ; aucun `.world` du dépôt modifié.
- **Arrêts** : un test existant qui change de résultat ; un fichier `.world` existant réécrit différemment par la voie existante (contrôle : `Tools/automation-smokes.sh` n'est pas lancé ici ; le contrôle est le test 2 et l'aller-retour éditeur du plan du jeu).
- **Retour arrière** : `git revert` du commit.
- **Commit** : `feat(editor-services): write a world from entities that are not initialized`

> Validation (2026-11-18) : `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur. `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` (sans `--no-build`) : 6475 tests, 0 échec, deux exécutions de suite sans échec intermittent (6469 + 6 nouveaux). `rg` : la classe `EditorWorldWriterTests` porte `[Collection(ProjectEnvironmentCollection.Name)]` et sa seule affectation de `ProjectPath` est dans le `try` / `finally` de `WithProject`. Cas d'id du test 2 : repli (`LightComponent` n'a pas de constructeur à id) : seules les valeurs `id` sont retirées des deux documents, les noms étant fixés explicitement. Écarts : (a) le test 3 compare les nœuds par texte (`ToString`) et non par `JToken.DeepEquals`, qui répond faux sur deux documents au texte identique ; (b) `EditorEntityJsonSerializer.GetEntityReferences` passe de `private` à `public` (interne à l'assembly) pour que la garde P2 et le test P3 lisent les références du monde. Réalisation de P4 : `SaveWorld(world, node)` délègue à une nouvelle surcharge `SaveWorld(world, entityReferences, node)` qui porte le corps inchangé. Statut 🧪 : la vérification indépendante suit (T2).

### ✅ T2 — ADR, vérification indépendante, clôture

- **Fichiers** : `docs/decisions/0144-…md` (skill `CasaEngine:adr`, `Proposed`), son index, ce plan, `ai-agent/README.md`.
- **Vérification** : un `verifier` frais sur T1 (build, tests relancés, P1 à P4 éprouvés, un monde écrit puis chargé par `World.Load`, la voie existante inchangée). Ses constats sont tranchés avant la clôture.
- **Commit** : `docs(adr): record the offline world writer`

> Validation (2026-11-18). ADR-0144 `Proposed`, index mis à jour.
> Vérification indépendante de T1 (`verifier` frais sur `de9770f10`) : **CONFIRMED** sur les six affirmations ; arbre propre à la fin.
> - Périmètre : six fichiers depuis `da32906e2`, tous sous `CasaEngine.EditorServices/`, `CasaEngine.Tests/`, `docs/`, `ai-agent/` ; aucun `.world` modifié.
> - Voie existante inchangée, **mesurée** : l'ancien sérialiseur (copie de `da32906e2` compilée à part) et le nouveau écrivent un texte identique pour un monde riche construit en code, un monde de lumières initialisé, un monde vide et les 14 `.world` des projets du dépôt.
> - P1 à P4 et cas limites : liste vide ; même entité deux fois ; entité à composant enfant ; `StaticModelComponent` avec un id de modèle et sans jeu, écrit sans être initialisé puis relu ; exception avant toute écriture (un fichier existant reste intact).
> - Build à 0 erreur ; 6475 tests, 0 échec ; la classe de test est dans `ProjectEnvironmentCollection`, `ProjectPath` restauré dans un `finally`.
> - Écart (b) de T1 expliqué : `SaveEntity` ajoute `script_class_name` par la conversion implicite d'une chaîne nulle, qui donne en mémoire un `JValue` de type `String` portant `null` ; après lecture du fichier le même champ est de type `Null`. `JToken.DeepEquals` compare ce type, d'où le faux écart ; les deux s'écrivent `null`. La comparaison par texte est donc un substitut sûr.
> - Constats P4, reportés : un `FileName` dont le dossier n'existe pas lève `DirectoryNotFoundException`, comme la voie existante (l'outil appelant crée le dossier ; noté dans l'ADR) ; une entité dont l'état enregistré change à l'initialisation est écrite dans son état d'avant (noté dans l'ADR) ; le test 4 ne contrôle après `World.Load` que le nom et `FilterTaps` (les tests 2 et 3 couvrent le reste).

## Validation globale

- `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 0 échec.
- Vérification indépendante de T1 : CONFIRMED.

## Hors périmètre

- Écrire un `.entity` depuis du code : `EditorEntityWriter.SaveEntity` le fait déjà sans initialiser l'entité.
- Fixer les ids des entités et des composants : l'appelant s'en charge.
- Toute voie d'exécution (ajouter une entité à un monde vivant sans l'initialiser).
- Push, avance de `main`.

## Points ouverts

- O1 (auteur, après T2) : **répondu le 2026-11-19** : l'ADR-0144 est acceptée par l'auteur ; `main` a été avancé et poussé avec l'intégration d'`origin/main` (`f82f213fc`). Texte d'origine : validation de l'ADR-0144 ; avance de `main` et push.
