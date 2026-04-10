# Plan IA - Migration vers RacingGameCasaEngine

## Objectif

Créer un projet `RacingGameCasaEngine` qui porte `RacingGame` sur `CasaEngine`, avec les contraintes suivantes :

- un seul projet MonoGame pour le jeu ;
- références directes vers `CasaEngine` et `MGUI` ;
- pas de `ContentPipeline` MonoGame pour le jeu ;
- chargement des assets via `CasaEngine` ;
- conversion de la logique existante vers `World`, `Entity`, `Component`, `GameMode`, `PlayerController` et écrans MGUI.

## Légende de statut

- `⬜` à faire
- `🟨` en cours
- `✅` terminé
- `⛔` bloqué

## Contrat de travail de l'agent

1. L'agent doit faire un commit à la fin de chaque étape terminée.
2. Une étape ne passe à `✅` que si le code compile au minimum sur le périmètre touché.
3. Si une étape révèle un manque fonctionnel ou technique bloquant, l'agent doit :
   - mettre l'étape en `⛔` ou la scinder,
  - créer une sous-étape dédiée dans ce plan,
  - implémenter la correction dans `RacingGameCasaEngine`,
  - committer séparément cette correction avant de reprendre l'étape principale.
4. Les commits doivent rester petits, lisibles et réversibles.
5. Après chaque commit, l'agent met à jour ce fichier : icône, notes, date si utile.

## Format de commit recommandé

- `feat(racing-casa): create bootstrap project`
- `feat(racing-casa): add initial world and game mode`
- `feat(racing-casa): migrate main menu to mgui`
- `feat(racing-casa): port arcade car controller`
- `feat(racing-casa): add lap and checkpoint race flow`

## Granularité attendue

- Une étape principale peut contenir plusieurs sous-étapes.
- Chaque sous-étape doit rester committable seule.
- Si une étape devient trop grosse pour un seul commit lisible, l'agent doit exécuter ses sous-étapes l'une après l'autre.
- Le statut de l'étape principale devient `🟨` dès qu'une sous-étape a commencé.
- Le statut de l'étape principale devient `✅` seulement quand toutes ses sous-étapes sont terminées.

## Validation minimale transversale

- Build borné obligatoire : `dotnet build RacingGameCasaEngine/RacingGameCasaEngine.csproj`
- Si une étape touche le front-end, le `ScreenManager` ou la navigation de course : lancer aussi `dotnet run --project RacingGameCasaEngine/RacingGameCasaEngine.csproj -- --smoke-frontend`
- Ne pas utiliser la tâche VS Code `Build RacingGame.Shared` pour valider ce plan : elle est mal configurée en PowerShell dans ce workspace.

## Plan committable

## ✅ Étape 1 - Cadrer la migration et geler l'architecture cible

**But**

Éviter un portage opportuniste en définissant la cible runtime avant d'écrire du code.

**Travail**

- Inventorier les points d'entrée de `RacingGame` et les dépendances à `RacingGame.Shared`.
- Lister les concepts à porter : bootstrap, écrans, monde, voiture joueur, ghost car, piste, checkpoints, HUD, audio, highscores, replay.
- Définir la cible CasaEngine minimale :
  - `RacingGameCasaEngineGame : CasaEngineGame`
  - `RaceGameMode`
  - `RacingPlayerController`
  - `RacingCarPawn` ou `RacingCarEntity`
  - `RaceWorldFactory`
  - `MainMenuScreen`, `TrackSelectionScreen`, `CarSelectionScreen`, `OptionsScreen`, `HighscoresScreen`, `PauseScreen`, `RaceHudScreen`, `GameOverScreen`
- Confirmer que le projet reste mono-assembly applicatif malgré la séparation en dossiers.

**Validation**

- Le plan de structure est écrit dans le code ou dans un commentaire de bootstrap minimal.
- Aucun choix structurant ambigu ne reste ouvert.

**Commit**

- `docs(racing-casa): define migration target architecture`

**Sous-étapes**

- `✅ 1.1` Cartographier les entrées runtime et les dépendances `RacingGame.Shared`
- `✅ 1.2` Définir les concepts métier à porter vers CasaEngine
- `✅ 1.3` Figer la structure cible du projet `RacingGameCasaEngine`
- `✅ 1.4` Figer les conventions de nommage et d'emplacement des classes principales

**Notes**

- Architecture cible retenue : bootstrap CasaEngine minimal, monde de course piloté côté projet, écrans et gameplay migrés dans `RacingGameCasaEngine`.

## 🟨 Étape 2 - Créer le projet RacingGameCasaEngine dans la solution

**But**

Obtenir un exécutable autonome qui démarre avec CasaEngine.

**Travail**

- Créer le projet `RacingGameCasaEngine` et l'ajouter à `RacingGame.slnx`.
- Référencer au minimum :
  - `CasaEngine/CasaEngine/CasaEngine.csproj`
  - `MGUI/MGUI.Core/MGUI.Core.csproj`
  - `MGUI/MGUI.Shared/MGUI.Shared.csproj`
- Reprendre le pattern de bootstrap utilisé par `CasaEngine.Launcher` ou `CasaEngine.Demos` :
  - `EngineEnvironment.ProjectPath`
  - `GameSettings.CreateRuntimeContext()`
  - `runtimeContext.UIViewRuntimeFactory = new MguiViewRuntimeFactory()`
  - `using var game = new RacingGameCasaEngineGame(...); game.Run();`
- Configurer la fenêtre, le titre, le redimensionnement et les logs.

**Validation**

- Le projet compile.
- La fenêtre s'ouvre et la boucle CasaEngine tourne sans charger de gameplay.

**Commit**

- `feat(racing-casa): create bootstrap project`

**Sous-étapes**

- `✅ 2.1` Créer le `.csproj` `RacingGameCasaEngine`
- `✅ 2.2` Ajouter le projet à `RacingGame.slnx`
- `✅ 2.3` Ajouter les références vers `CasaEngine`, `MGUI.Core` et `MGUI.Shared`
- `✅ 2.4` Créer `Program.cs` et le bootstrap minimum du jeu
- `✅ 2.5` Configurer la fenêtre, les logs et le contexte runtime MGUI
- `✅ 2.6` Vérifier qu'une fenêtre CasaEngine vide démarre proprement

## 🟨 Étape 3 - Supprimer toute dépendance au ContentPipeline MonoGame côté jeu

**But**

Faire du nouveau jeu un consommateur pur du système d'assets CasaEngine.

**Travail**

- Vérifier qu'aucun `.mgcb` du nouveau projet n'est requis pour démarrer.
- Charger les assets via `AssetCatalog.Load(...)` et `AssetContentManager`.
- Définir un emplacement projet clair pour les assets runtime du jeu.
- Préparer ou générer un premier `AssetInfos.json` minimal pour le jeu.
- Documenter les conventions de chemin pour textures, modèles, sons, shaders, UI et données métier.

**Validation**

- Le projet démarre sans `Content.mgcb` applicatif.
- Un asset simple se charge via CasaEngine depuis le nouveau projet.

**Commit**

- `feat(racing-casa): use casaengine asset loading only`

**Sous-étapes**

- `✅ 3.1` Retirer toute hypothèse de `.mgcb` applicatif du nouveau projet
- `✅ 3.2` Définir l'arborescence runtime des assets du jeu
- `✅ 3.3` Introduire un chargement initial de `AssetCatalog`
- `✅ 3.4` Créer ou préparer un premier `AssetInfos.json` minimal
- `✅ 3.5` Charger au moins un asset simple via `AssetContentManager`
- `✅ 3.6` Documenter les conventions de chemin et de catégorie d'assets

## 🟨 Étape 4 - Mettre en place le squelette runtime du jeu

**But**

Créer la colonne vertébrale du jeu avant de porter les écrans et la course.

**Travail**

- Créer les dossiers du projet :
  - `Bootstrap/`
  - `GameFramework/`
  - `Worlds/`
  - `Entities/`
  - `Components/`
  - `Gameplay/`
  - `Screens/`
  - `UI/`
  - `Assets/`
  - `Persistence/`
- Ajouter un `RaceGameMode`.
- Ajouter un `RacingPlayerController`.
- Ajouter une première factory de monde ou bootstrap de world.
- Ajouter un point d'entrée qui charge un monde vide et au moins une vue active.

**Validation**

- Le jeu charge un `World` vide.
- Une caméra et une vue actives existent.
- La structure du projet est stabilisée.

**Commit**

- `feat(racing-casa): add runtime game skeleton`

**Sous-étapes**

- `✅ 4.1` Créer les dossiers source du projet
- `✅ 4.2` Ajouter `RacingGameCasaEngineGame : CasaEngineGame`
- `✅ 4.3` Ajouter `RaceGameMode`
- `✅ 4.4` Ajouter `RacingPlayerController`
- `✅ 4.5` Ajouter un bootstrap de monde vide
- `✅ 4.6` Vérifier qu'une vue et une caméra actives existent au runtime

## ✅ Étape 5 - Migrer les écrans non gameplay vers MGUI

**But**

Recréer le flux front-end du jeu avec les primitives UI de CasaEngine/MGUI.

**Travail**

- Porter ou recréer les écrans suivants :
  - splash
  - menu principal
  - sélection de piste
  - sélection de voiture
  - options
  - aide
  - highscores
- Utiliser `UIRoot`, `ScreenStack` et `GameScreenManager` plutôt qu'une pile locale de `IGameScreen` comme dans l'ancien jeu.
- Isoler la navigation écran dans une orchestration claire.
- Prévoir les données nécessaires aux bindings MGUI.

**Validation**

- Le joueur peut naviguer de l'écran titre vers la préparation d'une course.
- Aucun rendu legacy `RacingGame.Shared` n'est requis pour ces écrans.

**Commit**

- `feat(racing-casa): migrate front-end screens to mgui`

**Sous-étapes**

- `✅ 5.1` Mettre en place l'orchestrateur de navigation écran
- `✅ 5.2` Recréer l'écran splash
- `✅ 5.3` Recréer le menu principal
- `✅ 5.4` Recréer la sélection de piste
- `✅ 5.5` Recréer la sélection de voiture
- `✅ 5.6` Recréer l'écran options
- `✅ 5.7` Recréer l'écran aide
- `✅ 5.8` Recréer l'écran highscores
- `✅ 5.9` Valider la navigation complète jusqu'au lancement d'une course

**Notes**

- Un mode de validation automatisée `--smoke-frontend` exécute désormais le flux splash -> menu -> highscores -> options -> aide -> sélection voiture -> sélection piste -> HUD de course -> retour menu sans interaction manuelle.
- Les six écrans front-end principaux réutilisent maintenant l'atlas original `buttons.png`, le logo legacy et des compositions MGUI alignées sur les vues historiques pour se rapprocher visuellement des captures de `RacingGame`.
- Les options affichées sont redevenues proches de l'original côté interface, mais seules les options déjà supportées par le runtime CasaEngine actuel ont un effet immédiat garanti.

## 🟨 Étape 6 - Créer le modèle de monde de course

**But**

Remplacer le pilotage global par un monde runtime CasaEngine cohérent.

**Travail**

- Définir la composition du monde de course :
  - piste
  - décor
  - voiture joueur
  - ghost car
  - points de départ
  - checkpoints
  - caméras
  - UI world éventuelle si nécessaire
- Introduire des entités dédiées ou des factories d'entités.
- Faire du monde la source de vérité des objets actifs pendant la course.

**Validation**

- Une course charge un monde avec ses entités principales.
- Le code n'utilise plus de singleton global comme centre exclusif du runtime.

**Commit**

- `feat(racing-casa): add race world composition`

**Sous-étapes**

- `✅ 6.1` Définir les entités racines d'une course
- `✅ 6.2` Définir la factory de monde de course
- `✅ 6.3` Ajouter les points de départ et la caméra runtime
- `✅ 6.4` Ajouter les emplacements de checkpoints et d'objets de course
- `✅ 6.5` Brancher le monde de course au `GameMode`

**Notes**

- Le jeu désactive l'initialisation automatique des `PlayerController` CasaEngine pour binder côté projet un `RaceGameMode`, un `RacingPlayerController` et un `RacingCarPawn` runtime par réflexion contrôlée sur le `World`.

## ✅ Étape 7 - Migrer les assets de piste et de décor vers CasaEngine

**But**

Afficher une première version de la piste et de son décor sans le pipeline historique de `RacingGame`.

**Travail**

- Cartographier les assets actuels : modèles, textures, sons, shaders, données de piste.
- Convertir ou enregistrer les assets nécessaires dans le catalogue CasaEngine.
- Créer les entités de décor avec `StaticModelComponent` et composants associés.
- Porter la logique indispensable de chargement de piste si elle ne peut pas être remplacée par un asset plus simple.

**Validation**

- Une piste se charge visuellement avec son décor principal.
- Les assets sont résolus par CasaEngine sans dépendance au `ContentPipeline` du jeu original.

**Commit**

- `feat(racing-casa): migrate track and scenery assets`

**Sous-étapes**

- `✅ 7.1` Cartographier les assets des pistes du jeu original
- `✅ 7.2` Définir la stratégie de conversion ou d'enregistrement dans le catalogue
- `✅ 7.3` Charger les modèles et textures de décor prioritaires
- `✅ 7.4` Instancier le décor principal dans le monde
- `✅ 7.5` Charger les données de piste nécessaires au gameplay
- `✅ 7.6` Vérifier qu'une piste jouable s'affiche sans pipeline MonoGame dédié

**Notes**

- Les fichiers legacy `TrackBeginner.Track`, `TrackAdvanced.Track`, `TrackExpert.Track`, les `CombiModel` et les modèles `.X` sont maintenant copiés dans `RacingGameCasaEngine/Content` via le projet du jeu, sans réintroduire de pipeline MonoGame dédié au nouveau projet.
- `RacingGameCasaEngine` charge les vraies données de piste via un loader projet local, génère la chaussée en `StaticModel` CasaEngine à partir des points du `.Track`, et dérive désormais le `PlayerStart` ainsi que les checkpoints depuis cette géométrie.
- Le décor principal est assemblé à partir des `NeutralsObjects` des pistes legacy, avec expansion des `CombiModel` et import runtime des modèles `.X` en `StaticModelComponent`.
- Les matériaux du circuit utilisent encore principalement des fallbacks unis. L'affichage final du circuit avec textures, matériaux cibles et éclairage dédié est traité par l'étape suivante.
- Validation effectuée via `dotnet build RacingGameCasaEngine/RacingGameCasaEngine.csproj` puis `dotnet run --project RacingGameCasaEngine/RacingGameCasaEngine.csproj -- --smoke-frontend`, le smoke chargeant bien la course avec la piste `Beginner` et son décor.

## ✅ Étape 8 - Finaliser l'affichage du circuit

**But**

Faire du circuit la priorité visuelle immédiate : scène lisible, modèles statiques texturés, matériaux cohérents et éclairage de course.

**Travail**

- Structurer le circuit en entités runtime explicites plutôt qu'en simple liste d'entités générées à plat.
- Mapper les modèles statiques `.X` du circuit et du décor à leurs textures et slots de matériaux utiles.
- Appliquer des matériaux cibles à la route, au sol et aux objets de décor au lieu de couleurs unies de fallback.
- Ajouter un setup d'éclairage dédié à la scène de course.
- Ajouter un skybox ou fond de scène dédié pour fermer visuellement la scène de course.

**Validation**

- Une piste complète s'affiche avec ses modèles statiques principaux, ses textures utiles et une lumière de course cohérente.
- La scène de course dispose d'un skybox ou d'un fond de scène cohérent autour du circuit.
- Le rendu du circuit reste autonome côté `RacingGameCasaEngine`, sans retour vers le renderer legacy.

**Commit**

- `feat(racing-casa): finalize race track visual rendering`

**Sous-étapes**

- `✅ 8.1` Structurer le circuit en entités runtime explicites
- `✅ 8.2` Mapper les modèles statiques du circuit à leurs textures
- `✅ 8.3` Appliquer des matériaux cibles à la route, au sol et au décor
- `✅ 8.4` Ajouter l'éclairage de la scène de course
- `✅ 8.5` Vérifier qu'une piste lisible s'affiche avec textures et lumière
- `✅ 8.6` Implémenter le skybox ou fond de scène de la course

**Notes**

- La priorité immédiate du plan est d'afficher correctement le circuit avant de finaliser l'affichage de la voiture.
- `RacingGameCasaEngine` copie désormais toutes les textures legacy du projet original dans `Content/Textures`, ce qui permet au chargement runtime des `.X` et des matériaux du sol/route d'utiliser les vrais fichiers du jeu.
- `LegacyTrackSceneFactory` sépare maintenant les entités de piste (`Track.Ground.*`, `Track.Road.*`) et de décor (`Track.Scenery.*`), applique les matériaux importés quand ils existent, et complète le reste avec des fallbacks ciblés.
- `RacingGameCasaEngineGame` applique un setup d'éclairage dédié quand un monde de course est chargé.
- La vue de course utilise désormais un fond de ciel dédié piloté par le runtime CasaEngine, et les matériaux réfléchissants du circuit et de la voiture consomment le même cubemap de scène au lieu de recharger directement le `SkyCubeMap.dds` legacy quand il s'agit du ciel partagé.
- Validation automatisée effectuée via `dotnet build RacingGameCasaEngine/RacingGameCasaEngine.csproj` puis `dotnet run --project RacingGameCasaEngine/RacingGameCasaEngine.csproj -- --smoke-frontend`, le smoke chargeant bien la course, le HUD et le retour menu après création des entités de piste et de décor.
- `8.5` est validée : le rendu actuel du circuit est jugé lisible avec textures et éclairage.

## ✅ Étape 9 - Porter la voiture joueur en Entity/Pawn CasaEngine

**But**

Faire de la voiture un objet de jeu moderne, composé et contrôlable, après que le circuit soit lisible visuellement.

**Travail**

- Créer `RacingCarPawn` ou `RacingCarEntity` comme agrégat runtime stable.
- Décomposer la voiture en composants :
  - racine physique
  - pivot visuel
  - rendu voiture
  - gameplay proxy ou contrôleur arcade
  - audio moteur si nécessaire
  - point d'ancrage caméra
- Raccorder le rendu de la voiture à un vrai modèle et à ses matériaux.
- Brancher ensuite la physique gameplay de la voiture sur la physique du circuit.

**Validation**

- La voiture apparaît avec un vrai rendu dans le monde.
- Le code gameplay n'est pas couplé à l'ancien `RacingGameManager`.

**Commit**

- `feat(racing-casa): port player car as entity`

**Sous-étapes**

- `✅ 9.1` Créer l'entité ou le pawn de voiture
- `✅ 9.2` Figer la hiérarchie ECS et les points d'ancrage de la voiture
- `✅ 9.3` Ajouter un premier visuel debug de la voiture
- `✅ 9.4` Remplacer le visuel debug par un vrai rendu de voiture
- `✅ 9.5` Brancher les matériaux et textures de la voiture
- `✅ 9.6` Ajouter le contrôleur arcade de déplacement
- `✅ 9.7` Brancher les inputs joueur
- `✅ 9.8` Exposer l'état runtime utile au HUD et au `GameMode`
- `✅ 9.9` Brancher la physique gameplay de la voiture sur la physique du circuit
- `✅ 9.10` Vérifier que la voiture roule sur une piste simple

**Notes**

- `RacingCarPawn` expose maintenant une hiérarchie runtime explicite `PhysicsRoot -> VisualPivot -> Body / ChaseCamera / CockpitCamera / AudioEmitter`, avec des ancrages stables réutilisables par le rendu, la caméra et l'audio.
- `LegacyCarVisualComponent` charge désormais `Content/Models/Car.x` au runtime, l'attache au `BodyVisual` du pawn et laisse `DebugCarVisualComponent` uniquement en fallback si le modèle legacy ne peut pas être importé.
- `LegacyCarVisualFactory` centralise désormais la présentation de la voiture legacy: sélection de `RacerCar` / `RacerCar2` / `RacerCar3`, conservation du normal map dédié et teinte du matériau de carrosserie selon la couleur choisie en front-end.
- Le rendu runtime de la voiture suit maintenant plus fidèlement les valeurs legacy: teinte de carrosserie pilotée par l'alpha de la texture comme dans `NormalMapping.fx`, réflexion multiplicative `0.85 + cubemap * 0.75` pour les surfaces de carrosserie/chrome, et scale recalé sur les dimensions gameplay de `CarPhysics` (`5.6m x 2.6m x 1.8m`).
- `RaceWorldFactory` propage la sélection front-end (`SelectedCarIndex`, `SelectedCarColorIndex`) jusqu'au pawn, et `LegacyCarVisualComponent` consomme directement ces indices pour construire le vrai rendu runtime de la voiture avec sa variante visuelle complète.
- `CarSelectionScreen` affiche maintenant la voiture choisie dans un aperçu rendu sur `RenderTarget`, réutilisant la même logique de modèle, textures et couleur que la voiture de course.
- `DebugCarVisualComponent` et `ChaseCameraRigComponent` consomment déjà ces ancrages, ce qui stabilise le contrat ECS autour du vrai modèle de voiture.
- `RaceTrackPhysicsProfile` dérive désormais une surface de roulage runtime à partir de la spline de piste déjà utilisée pour générer la route visuelle, et `RaceTrackPhysicsComponent` expose ce profil au monde de course sans recoupler la voiture au legacy runtime.
- `ArcadeCarMovementComponent` n'est plus une simple translation libre sur XZ: il projette maintenant la voiture sur la surface du circuit, aligne le mouvement sur la tangente et la normale de la route, bloque la caisse contre les rembardes latérales en tenant compte du gabarit gameplay de la voiture, et applique un ralentissement au contact dans l'esprit du runtime legacy.
- `9.10` est considéré validé: la voiture roule maintenant sur une piste simple avec ses collisions latérales de rembardes actives, ce qui ferme l'étape 9 côté gameplay voiture.
- Le rendu runtime de la voiture applique de nouveau le blend de réflexion legacy attendu sur la carrosserie au lieu d'additionner uniformément le cubemap de scène, ce qui évite l'effet miroir apparu après le branchement du cubemap partagé de course.
- Validation effectuée via `dotnet build RacingGameCasaEngine/RacingGameCasaEngine.csproj -p:BaseOutputPath=artifacts/verify-build/`, `dotnet run --project RacingGameCasaEngine/RacingGameCasaEngine.csproj -p:BaseOutputPath=artifacts/verify-build/ -- --smoke-frontend` puis `dotnet run --project RacingGameCasaEngine/RacingGameCasaEngine.csproj -p:BaseOutputPath=artifacts/verify-build/ -- --capture-track-audit`, le smoke front-end et l'audit course complétant sans échec ni warning de fallback sur le modèle de voiture.
- Validation complémentaire 9.9/9.10 effectuée via `dotnet build RacingGameCasaEngine/RacingGameCasaEngine.csproj -p:BaseOutputPath=artifacts/verify-build/` puis `dotnet run --project RacingGameCasaEngine/RacingGameCasaEngine.csproj -p:BaseOutputPath=artifacts/verify-build/ -- --smoke-frontend`.

## ✅ Étape 10 - Construire la physique du circuit et les triggers de course

**But**

Donner au circuit une représentation physique exploitable pour le roulage, les collisions de bord et la progression de course.

**Travail**

- Générer ou déduire une surface de roulage physique à partir des données de piste.
- Ajouter les collisions de bord de piste et des obstacles prioritaires.
- Remplacer les checkpoints basés sur la distance par des triggers runtime explicites.
- Préparer un socle physique stable pour la voiture joueur et, plus tard, le ghost car.

**Validation**

- Une voiture peut rouler sur la piste via la physique du circuit et non uniquement par translation libre.
- Un tour n'est validé qu'en franchissant les checkpoints dans l'ordre.

**Commit**

- `feat(racing-casa): build race track physics`

**Sous-étapes**

- `✅ 10.1` Construire la surface de roulage physique du circuit
- `✅ 10.2` Ajouter les collisions de bord de piste et des obstacles prioritaires
- `✅ 10.3` Remplacer les checkpoints par des triggers runtime
- `✅ 10.4` Vérifier qu'une piste jouable combine rendu et physique cohérents

**Notes**

- Cette étape débloque directement `9.9` et `9.10` pour la voiture.
- Les obstacles prioritaires visés par `10.2` sont d'abord les volumes statiques proches de la chaussée et plausibles côté gameplay: glissières et supports de glissière, portique de départ `StartLight3`, panneaux de signalisation (`SignWarning`, `SignCurveLeft`, `SignCurveRight`), puis les objets durs déjà présents dans les pistes legacy au bord de route (`Hydrant`, `Blockade`, `Blockade2`, `SharpRock`, `SharpRock2`, `OilPump`, `OilTanks`, `AlphaTrain` selon la piste). Les palmiers, cactus, bâtiments, hôtels, ruines et décor lointain restent secondaires tant qu'ils n'empiètent pas sur la trajectoire utile.

## ✅ Étape 11 - Porter la caméra de poursuite et le ressenti de conduite

**But**

Retrouver le feel RacingGame sans réintroduire l'architecture ancienne.

**Travail**

- Créer une caméra de poursuite dédiée au véhicule.
- Porter les règles utiles : distance dynamique, inertie, recentrage, zoom, caméra de game over.
- Vérifier la séparation entre logique véhicule et logique caméra.

**Validation**

- La caméra suit correctement la voiture.
- Les transitions course, pause et game over restent lisibles.

**Commit**

- `feat(racing-casa): add chase camera gameplay`

**Sous-étapes**

- `✅ 11.1` Créer la caméra de poursuite de base
- `✅ 11.2` Ajouter le suivi position/orientation du véhicule
- `✅ 11.3` Ajouter le zoom et la distance dynamique
- `✅ 11.4` Ajouter le mode game over ou orbit caméra
- `✅ 11.5` Ajuster le feel sans recoupler caméra et physique

**Notes**

- La caméra de course suit maintenant le `RacingCarPawn` avec un offset lissé, une orientation inertielle, une distance dynamique liée à la vitesse, un zoom runtime (`PageUp` / `PageDown`, `GamePad X/Y`) et un orbit de fin de course, tout en restant portée par un composant dédié séparé de la physique véhicule.

## ✅ Étape 12 - Migrer le flow de course

**But**

Recréer la boucle de jeu complète.

**Travail**

- Porter ou recréer :
  - countdown / start lights
  - checkpoints
  - nombre de tours
  - victoire / défaite
  - pause
  - retour menu
- Brancher `RaceGameMode` et les écrans runtime aux états de course.
- Faire vivre les transitions via `GameScreenManager` et le `GameMode`.

**Validation**

- Une course complète peut commencer, progresser, se terminer et revenir au menu.

**Commit**

- `feat(racing-casa): implement race flow`

**Sous-étapes**

- `✅ 12.1` Créer le countdown et l'état de départ de course
- `✅ 12.2` Ajouter la logique de checkpoints
- `✅ 12.3` Ajouter le comptage de tours
- `✅ 12.4` Ajouter la logique de victoire et défaite
- `✅ 12.5` Ajouter pause et reprise côté logique
- `✅ 12.6` Raccorder les états de course au `GameScreenManager`
- `✅ 12.7` Vérifier la boucle complète début de course -> fin -> retour menu

**Notes**

- Le flow de course gère maintenant aussi la pause clavier/manette via le `ScreenManager`, tandis que la fin de course reste pilotée par le HUD runtime lui-même, avec une validation smoke qui force pause -> reprise -> fin de course -> retour menu pour vérifier la boucle complète.

## ✅ Étape 13 - Migrer le HUD et les overlays en jeu

**But**

Afficher les informations de course via MGUI, sans renderer UI legacy.

**Travail**

- Faire évoluer le HUD actuel de télémétrie vers un HUD orienté joueur.
- Ajouter les overlays de pause et de fin de partie.
- Raccorder le HUD et les overlays aux données runtime du véhicule et de la course.

**Validation**

- Le HUD suit correctement l'état de la course.
- Aucun texte critique n'est encore rendu par l'ancien système `RacingGame.Shared`.

**Commit**

- `feat(racing-casa): add in-game hud and overlays`

**Sous-étapes**

- `✅ 13.1` Créer un premier HUD de course de télémétrie
- `✅ 13.2` Afficher vitesse, tour et chrono sur un HUD orienté joueur
- `✅ 13.3` Afficher meilleur temps et informations de course
- `✅ 13.4` Ajouter l'overlay pause
- `✅ 13.5` Ajouter l'écran ou overlay de game over
- `✅ 13.6` Brancher toutes les données runtime au HUD

**Notes**

- `RaceHudScreen` porte maintenant le HUD joueur en reprenant la structure MGUI legacy (`laps`, `current/best`, `top 5`, tachymètre) et intègre aussi le panneau de fin de course, tandis que `PauseScreen` reste l'unique overlay modal en course.

## ⬜ Étape 14 - Porter le ghost car, les highscores et la persistance

**But**

Conserver les fonctionnalités identitaires du jeu original.

**Travail**

- Introduire une vraie persistance des meilleurs temps par piste.
- Migrer ou réimplémenter le système de replay échantillonné.
- Afficher une ghost car dans le monde.
- Migrer les options utiles : vidéo, audio, affichage, vibration si conservée.

**Validation**

- Les meilleurs temps persistent.
- Le ghost car fonctionne sur au moins une piste.

**Commit**

- `feat(racing-casa): port ghost replay and persistence`

**Sous-étapes**

- `⬜ 14.1` Sauvegarder les meilleurs temps
- `⬜ 14.2` Recharger les meilleurs temps au démarrage
- `⬜ 14.3` Introduire la structure de replay runtime
- `⬜ 14.4` Enregistrer les échantillons de course nécessaires
- `⬜ 14.5` Lire et interpoler un ghost car
- `⬜ 14.6` Brancher les options de persistance utiles

## ⬜ Étape 15 - Finaliser l'intégration audio et les feedbacks

**But**

Retrouver le ressenti du jeu complet.

**Travail**

- Migrer musique, effets sonores et son moteur.
- Ajouter les feedbacks checkpoint, victoire, défaite et collisions.
- Ajuster les réactions visuelles et sonores restantes.

**Validation**

- Une session complète a les feedbacks essentiels attendus.

**Commit**

- `feat(racing-casa): finish audio and gameplay feedback`

**Sous-étapes**

- `⬜ 15.1` Brancher les musiques de menu et de course
- `⬜ 15.2` Ajouter le son moteur et ses variations
- `⬜ 15.3` Ajouter les feedbacks checkpoint, victoire et défaite
- `⬜ 15.4` Ajuster les réactions audio et visuelles des collisions
- `⬜ 15.5` Ajuster les feedbacks visuels restants

## ⬜ Étape 16 - Stabiliser, nettoyer et documenter

**But**

Clore la migration avec un état maintenable.

**Travail**

- Supprimer les dépendances restantes au code legacy non utilisé.
- Vérifier les références projet.
- Documenter la structure du nouveau jeu.
- Documenter les écarts assumés avec `RacingGame` si certains comportements ne sont pas portés à l'identique.
- Ajouter une note finale sur les briques spécifiques créées dans `RacingGameCasaEngine` pendant la migration.

**Validation**

- Build ciblé OK.
- Le projet démarre, on peut faire une course complète, et la dette restante est explicitée.

**Commit**

- `docs(racing-casa): finalize migration notes`

**Sous-étapes**

- `⬜ 16.1` Supprimer les dépendances legacy devenues inutiles
- `⬜ 16.2` Vérifier les références et l'initialisation du projet
- `⬜ 16.3` Documenter la structure finale du jeu
- `⬜ 16.4` Documenter les écarts fonctionnels restants avec `RacingGame`
- `⬜ 16.5` Documenter les extractions moteur réalisées pendant la migration

## Ordre d'exécution recommandé

1. Étapes 1 à 7
2. Étape 8
3. Étape 9 jusqu'à l'obtention d'une vraie voiture visible (`9.2` à `9.5`)
4. Étape 10 puis fin de l'étape 9 (`9.9` et `9.10`)
5. Étapes 11 à 13
6. Étapes 14 et 15
7. Étape 16

## Lot prioritaire conseillé pour un agent autonome

1. `8.1` à `8.5`
2. `9.2` à `9.5`
3. `10.1` à `10.4`
4. `9.9` à `9.10`

Ce lot doit aboutir à un projet `RacingGameCasaEngine` où le circuit s'affiche correctement avec ses textures et sa lumière, puis où la voiture apparaît comme une vraie entité rendue avant le raccord complet à la physique du circuit.

## Règle de blocage

Si une étape dépend d'une brique absente ou encore floue, l'agent doit insérer une sous-étape projet juste avant, l'implémenter directement dans `RacingGameCasaEngine`, la commit, puis reprendre la migration du jeu.

## Écarts à traiter dans RacingGameCasaEngine

Les points suivants ne sont pas traités comme des évolutions du moteur `CasaEngine`, mais comme des briques propres au projet `RacingGameCasaEngine` :

- hiérarchie ECS, rendu, matériaux et physique de la voiture joueur ;
- rendu du circuit : scene graph, modèles statiques, textures, matériaux et éclairage ;
- physique du circuit : surface roulable, collisions de bord et triggers de checkpoints ;
- système de course : checkpoints, tours, départ, fin ;
- caméra de poursuite véhicule ;
- ghost car et replay ;
- workflow d'import et de mapping des assets du jeu ;
- feedbacks audio et visuels utiles à la course.

Ils doivent être implémentés au fil des étapes du plan, dans le projet du jeu, sans créer de backlog séparé côté moteur.