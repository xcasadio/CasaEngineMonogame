# Plan agent IA — Éditeur de cutscenes : un aperçu vivant (tranche H)

Suite des tranches A à G de l'éditeur de cutscenes, archivées le 2026-11-19 ([preview](archive/cutscene-editor-preview-tasks.md), [transport](archive/cutscene-editor-transport-tasks.md), [fidelity](archive/cutscene-editor-fidelity-tasks.md)).
Les décisions D1 → D4 ci-dessous ont été arbitrées avec l'auteur le 2026-11-19 : **ce plan les applique, il ne les rediscute pas**. Les arbitrages P1 → P19 sont ceux de l'agent : à confirmer par l'auteur avec le plan.
Relectures neuves du 2026-11-19 avant approbation : une relecture de plan (REVISE, trois points P2), une vérification des faits (cinq imprécisions) et une revue d'impact (cinq points P2, six notes) ; tous les points sont repris dans cette version. Deuxième relecture neuve le même jour (REVISE : un point P1 et trois points P2 sur le sample et les lancements de validation ; une phrase fausse et cinq imprécisions de faits) : tous repris ici. Relecture de clôture le même jour : REVISE sur un seul point, de priorité P1 (un test existant compte les fichiers `.cutscene` du dépôt, et le sample en ajoute un) ; il est repris par l'exception nommée P19, **que l'auteur doit confirmer avec le plan**. Aucune relecture n'a été faite après cette dernière correction : le plan n'a donc pas reçu de verdict READY.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Aujourd'hui l'aperçu d'une cutscene montre un monde figé : les sprites animés restent sur leur image, rien ne tombe, et la ligne d'état le dit. La tranche H rend ce monde vivant : pendant la lecture, **les animations de sprites jouent, les particules simulent et la physique avance** ; la pause les fige, un retour en arrière les rejoue. Les scripts de gameplay ne tournent toujours pas.

Le moteur gagne pour cela une règle d'exécution **par monde** : un monde peut porter sa propre règle au lieu de celle du jeu. Seul le monde d'aperçu des cutscenes s'en sert.

Ce que la tranche ne livre pas est dans « Hors périmètre », en particulier la vue du jeu (caméra du monde, résolution virtuelle), qui fait l'objet de la tranche I.

## État vérifié du dépôt (2026-11-19)

Lu dans `main` `ae2a42be`, plus le commit de clôture `adb35431` (documentation seule).

- **La règle d'exécution est une par jeu.** `GameplayExecutionPolicy` est une classe scellée de dix booléens `required init` (`CasaEngine/Framework/Application/GameplayExecutionPolicy.cs:3-23`). Trois instances nommées existent : `Runtime`, `EditorPreview`, `EditorSimulation` (`:27`, `:41`, `:55`), construites par `new()` dans ce fichier. Aucune autre instance n'est construite dans le dépôt (`rg "new GameplayExecutionPolicy"` : aucun résultat) : les tests n'utilisent que les trois instances nommées. Le jeu la porte : `CasaEngineGame.ExecutionPolicy`, valeur par défaut `Runtime` (`CasaEngineGame.cs:83`).
- **L'éditeur n'a qu'un jeu, donc qu'une règle.** Il crée son runtime avec `EditorPreview` (`CasaEngine.Editor/GameEditor.cs:1243`). Le mode Play passe à `EditorSimulation` puis revient (`PlayMode/EditorPlaySessionController.cs:49`, `:73`). Aucun mécanisme ne donne une autre règle à un seul monde (`rg ExecutionPolicy CasaEngine CasaEngine.Editor CasaEngine.EditorServices`) : tous les lecteurs lisent la règle du jeu, par `Game.ExecutionPolicy` pour les mondes et les composants, par `_game.ExecutionPolicy` ou `ExecutionPolicy` dans `GameManager` et `CasaEngineGame`, par `_editorRuntime.ExecutionPolicy` pour les contrôles de fumée de `GameEditor.cs:7947` et `:8005`.
- **Lecteurs de la règle qui portent sur un monde** (les seuls hors `CasaEngineGame.cs` et `GameManager.cs`) :
  - `World.cs:348` (`InitializePlayerControllers`), `:355` (`InitializeGameplayOnLoad`), `:528` (`RunBeginPlay`), `:669` (`UpdateGameplayScripts`), `:869` (`ThrowOnScriptTypeResolution`) ;
  - `Entity.cs:538` (`UpdateGameplayScripts`) ;
  - `AnimatedSpriteComponent.cs:223` et `:228` (`UpdateAnimatedSprites`) ;
  - `PhysicsBaseComponent.cs:96`, `:143`, `:145` (`UseExternalViewManagement`, `UpdatePhysicsComponents`) ;
  - `ParticleSystemComponent.cs:347` (`IsEditorPreview`, avec `SimulateInEditor`) ;
  - `PhysicsSystemComponent.cs:29` (`UseExternalViewManagement`, à la création du contexte physique d'un monde) et `:58` (`UpdatePhysicsEngine`, pour tous les mondes d'un coup).
- **Les règles de chargement sont lues pendant `World.LoadContent`** (`World.cs:309-365`, et `PhysicsBaseComponent.InitializeWithWorld`, `:89-107`) : une règle propre à un monde doit être posée avant ce chargement.
- **Sprites animés.** Au chargement, un composant qui a des animations prend la première (`AnimatedSpriteComponent.cs:205-208`), sans script. `Update` sort tout de suite quand `UpdateAnimatedSprites` est faux (`:228`) ; sinon le temps avance du pas reçu, par composant. Le composant lève trois événements publics, `AnimationFinished`, `AnimationLooped` et `AnimationEventTriggered` (`:22`, `:28`, `:29`), à cinq endroits (`:248`, `:253`, `:352`, `:371`, `:919`). Aucun code du moteur ne s'y abonne ; deux scripts et un état de contrôleur du projet `Projects/CasaEngine.RPGDemo` s'abonnent à `AnimationFinished` (`Scripts/ScriptPlayer.cs:30`, `Scripts/ScriptEnemy.cs:34`, `Controllers/EnemyState/EnemyDyingState.cs:22`) ; personne n'écoute les deux autres. Le temps de l'animation courante se lit par `CurrentAnimationTimeSeconds` (`:69`) ; l'identifiant du sprite courant est privé (`:71`).
- **Particules.** `SimulateInEditor` vaut vrai par défaut (`ParticleSystemComponent.cs:40`) : les particules simulent déjà dans l'aperçu, sauf si la case est décochée. L'aléa est déterministe : `ParticleRandom` part d'une graine fixe par émetteur (`CasaEngine/Framework/Particles/Authoring/ParticleRandom.cs:6-14`, `CasaEngine/Framework/Particles/Runtime/ParticleRuntimeInstance.cs:115-124` ; le composant ne passe pas de graine, `ParticleSystemComponent.cs:245` : la graine de base vaut 1 et chaque émetteur reçoit 1 plus son index multiplié par 747796405). Le nombre de particules vivantes se lit par `ParticleSystemComponent.RuntimeInstance` (`:82`) et `ParticleRuntimeInstance.AliveCount` (`ParticleRuntimeInstance.cs:101`).
- **Physique.** Chaque monde a son contexte physique, créé par `PhysicsSystemComponent.GetOrCreateContext` (`PhysicsSystemComponent.cs:19-33`, `World.cs:316-320`). Le composant du jeu fait avancer les mondes des vues et le monde courant, une fois par image, avec le temps réel multiplié par `TimeScale`, et ne fait rien si le pas est nul ou négatif (`:56-93`). `IPhysicsWorld.Update(float)` existe (`IPhysicsWorld.cs:17`) ; `PhysicsWorld.Update` enchaîne le pas, les contacts et l'envoi des événements (`PhysicsWorld.cs:27-32`). Dans une image du jeu, le monde est mis à jour avant les composants du jeu, dont la physique (`CasaEngineGame.cs:630-648`) : une entité montre donc la pose de son corps avec un pas de retard. Le moteur physique avance par pas fixes de 1/60 s, un seul par appel (`PhysicsEngineSettings.cs:10`, `MaxSubSteps = 1`) : un appel qui porte deux pas ou plus n'en fait qu'un et perd le reste (`BepuPhysicsEngine.cs:745-754`). Un corps dynamique ne déplace son entité que si son composant a un parent (`PhysicsBaseComponent.cs:162-168`) ; le modèle existant est `Projects/SampleProject/Entities/Box.entity` (composant racine, composant de collision dynamique en enfant). `PhysicsSystemComponent.GetOrCreateContext` est appelé par deux tests existants avec un monde sans jeu (`CasaEngine.Tests/Physics/SimulationSpacePolicyTests.cs:131-137`, `:149-152`).
- **Les collisions appellent les scripts.** `BepuPhysicsEngine` appelle `GameplayProxy?.OnHit` et `OnHitEnded` des deux entités (`BepuPhysicsEngine.cs:861-862`, `:903-904`, `:914-915`), sans consulter la règle d'exécution. Des tests existants font avancer la physique sous `EditorPreview`, la valeur par défaut de leur fabrique d'entité (`CasaEngine.Tests/Physics/CollisionComponentTests.cs:242`, `:275`, défaut à `:334`).
- **Ce que les scripts font déjà dans l'aperçu, et qui ne change pas.** L'initialisation d'un script (`Entity.cs:247`, `:268`, `World.cs:359-360`), son `OnEndPlay` au vidage du monde (`World.cs:134-137`) et son `Draw` (`Entity.cs:566`) s'exécutent quelle que soit la règle. Seules la mise à jour et `BeginPlay` dépendent d'elle.
- **Aperçu des cutscenes.** `CutscenePreviewSession` reconstruit une copie du monde par `PreviewWorldDriver.Rebuild(Func<World>)` (`CutscenePreviewSession.cs:583`), qui appelle la fabrique puis charge le monde dans le runtime de l'éditeur (`PreviewWorldDriver.cs:67-83`). Un pas de l'aperçu appelle `_driver.Tick(seconds)`, donc `World.Update(seconds)`, puis l'hôte d'effets (`CutscenePreviewSession.cs:546-557`, `PreviewWorldDriver.cs:98-106`). Le transport fait un pas du temps de l'image en lecture, des pas fixes de 1/60 s en recherche, aucun en pause ; hors lecture il revient en `Running` et le monde avance (`CutscenePreviewTransport.cs:108-130`, ADR-0139). Un retour en arrière reconstruit le monde et rejoue ; une recherche vers l'avant pendant une lecture dont le document n'a pas changé continue depuis l'état courant, sans reconstruire, sauf quand l'état de départ est marqué (la lecture vient d'une recherche vers le premier instant, ADR-0139) : toute autre cible relance alors la lecture (`CutscenePreviewSession.cs:373-405`, `:453`, `:578`). Le bouton `Stop` du document appelle `Rewind`, qui reconstruit le monde (`CutsceneAssetInspectorPanel.cs:1624-1632`, `CutscenePreviewSession.cs:412-437`) ; la méthode `Stop` de la session, elle, laisse le monde dans l'état de la lecture (`:511-516`). Quand une lecture se termine pendant une recherche, le transport revient en `Running` et le monde avance avec les images de l'éditeur (`CutscenePreviewTransport.cs:112-116`, `:186-190`). Le premier pas après une reconstruction est borné à 1/30 s (`:29`, `:531-535`).
- **Le viewport de l'aperçu est une vue du jeu de l'éditeur** (`WorldViewportPanel.cs:1381`, `ViewManager.CreateView`), à laquelle le panneau donne le monde d'aperçu (`_renderWorldOverride` lu à `:1787`, `_renderView.World = desiredWorld` à `:1802`) : en mode Play de l'éditeur, le composant physique du jeu ferait avancer aussi le monde d'aperçu (`PhysicsSystemComponent.cs:74-80`). Rien d'autre ne met à jour ce monde : `GameManager.UpdateWorld` ne met à jour que le monde courant (`GameManager.cs:168`).
- **Diagnostics de l'aperçu.** `GameEditor.AppendCutscenePreviewDiagnostics` recopie les lignes de `CutsceneAssetInspectorPanel.GetAutomationStateSnapshot()` (`GameEditor.cs:9717-9721`, `CutsceneAssetInspectorPanel.cs:1466-1517`), qui écrit déjà le nom et la position de chaque entité du monde d'aperçu (`AppendEntityPositions`) et la ligne `Preview status line`, laquelle contient le texte de limite (`:1476`, `:1804`). `--cutscene-preview-seek` accepte une liste d'instants parcourus l'un après l'autre (`EditorAutomationOptions.cs:50-53`, `:322`).
- **Texte affiché.** `CutsceneAssetInspectorPanel.cs:37` : `"Sprite animations, physics and scripts are frozen in the preview."`. `docs/editor/cutscene-editor.md:318` dit la même chose (« gameplay scripts ») et précise que changer la règle pour le seul monde d'aperçu est « a separate decision ».
- **Choix du monde d'aperçu.** Sans choix mémorisé, l'aperçu prend le monde dont `startup_cutscene_asset_id` est la cutscene ouverte, puis le premier monde chargé du projet, puis les autres (`CutscenePreviewWorldResolver.cs:92-111`). Le choix mémorisé vit dans `<projet>/.casaeditor/cutscene-preview.editor.json` ; aucune option d'automatisation ne choisit un monde. Les diagnostics écrivent `Preview world: <nom>` (`CutsceneAssetInspectorPanel.cs:1498`).
- **Actions de sprite.** `ShowSprite`, `HideSprite`, `MoveSprite` et `TintSprite` échouent si le composant racine de l'entité nommée n'est pas un `StaticSpriteComponent` (`CasaEngine/Framework/Cutscenes/CutsceneActionCoroutineFactory.cs:681-694`).
- **Fichier écrit à la sortie.** En se fermant, l'éditeur écrit `<projet>/.casaeditor/viewport.editor.json` (`GameEditor.cs:535-540`, `:2904-2926`). Ce dossier n'existe pas dans le worktree pour `Projects/SpriteCutsceneSample` et aucun `.gitignore` ne le nomme : chaque lancement automatisé laisse donc un fichier non suivi.
- **Sample.** `Projects/SpriteCutsceneSample` n'a que des sprites fixes (`SpriteWorld.world` : quatre `StaticSpriteComponent`, un `Camera2dComponent`). Aucun monde de ce sample n'a de sprite animé, de particules ni de corps physique. `SpriteWorld.world` nomme sa cutscene de démarrage par `"startup_cutscene_asset_id"` (`:242`). La gravité par défaut est `(0, -9.87, 0)` (`CasaEngine/Engine/Physics/PhysicsEngineSettings.cs:15`). Un effet de particules en rafale existe (`Projects/SampleProject/Particles/SparkBurst_Additive.particle`). `Cutscenes/loop.cutscene` est une boucle sans fin (`Repeat` de compte 0, `:18-21`) : sa lecture libre donne des valeurs qui dépendent des images de l'éditeur.
- **Un test compte les cutscenes du dépôt.** `CutsceneSaveTests.TheRepositoryHoldsTheFourteenCutsceneFiles` exige exactement `RepositoryCutsceneCount = 15` fichiers `.cutscene` sous `Projects/*/Cutscenes` et `CasaEngine.Demos/Content/Cutscenes` (`CasaEngine.Tests/EditorServices/Cutscenes/CutsceneSaveTests.cs:13`, `:30-36`, `:423-445`) ; il y en a 15 aujourd'hui. Le commit `dddd9a4f`, qui a ajouté une cutscene au sample, avait modifié ce fichier de test. Le test voisin `ARepositoryFile_SavesAndReloadsWithoutLoss_AndSavesTheSameTextTwice` s'applique à chaque fichier trouvé.
- **Tests.** Aucun test ne construit `CutscenePreviewSession` (`rg CutscenePreviewSession CasaEngine.Tests` : aucun résultat). Les tests de physique et de sprites construisent un `World` avec un `CasaEngineGame` non initialisé dont ils fixent `ExecutionPolicy` (par exemple `CasaEngine.Tests/Physics/CollisionComponentTests.cs:326-336`, `Video/WorldVideoHarness.cs:21-33`).
- **ADR.** Le dernier numéro de `main` est 0144. La branche `chantier/camera-behaviours`, non fusionnée, porte déjà un ADR-0145.
- **Arbre de travail.** L'arbre principal du moteur est occupé par une autre session (`chantier/camera-behaviours`). Ce chantier travaille dans le worktree `D:\development\repo\CasaEngineMonogame-cutscene`. Deux dossiers non suivis existent dans l'arbre principal (`Projects/PixelUiSample/.casaeditor/`, `Projects/SpriteCutsceneSample/.casaeditor/`) : ne jamais les indexer.
- **Non lu, donc non affirmé.** L'animation squelettique des modèles 3D (son chemin de mise à jour dans l'éditeur) ; le coût d'un pas de physique pendant une recherche sur un grand monde.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | L'aperçu fait tourner **les animations de sprites, les particules et la physique**. Les particules simulent même sans leur case « simuler dans l'éditeur ». Les scripts de gameplay ne sont pas dans la tranche. |
| D2 | Le cadrage du jeu est voulu sous la forme « vue du jeu par défaut, avec un bouton pour revenir à la vue libre ». |
| D3 | La branche part du `main` actuel. Pour le cadrage, « il faudra regarder quand la branche des caméras est finie » : il est donc traité après, dans la tranche I, avec son propre plan. |
| D4 | Les règles habituelles du chantier s'appliquent : rien n'est poussé ni fusionné dans `main` sans demande ; aucune entrée clavier ou souris n'est envoyée à l'éditeur ; un lancement automatisé n'enregistre aucun asset. |

## Arbitrages de l'agent, à confirmer

| Réf | Arbitrage |
|---|---|
| P1 | **Règle par monde.** `World` gagne `ExecutionPolicyOverride` (nulle par défaut) et `ExecutionPolicy`, qui rend la règle propre au monde si elle est posée, sinon celle du jeu (`ExecutionPolicyOverride ?? Game?.ExecutionPolicy`) : elle est donc nulle pour un monde sans règle propre et sans jeu, et chaque lecteur garde la tolérance qu'il a aujourd'hui dans ce cas. La règle propre se pose tant que le monde n'a pas de jeu, donc avant `LoadContent` ; la poser ensuite lève une `InvalidOperationException`. Elle n'est ni enregistrée dans un `.world` ni copiée avec le monde. |
| P2 | Les lecteurs qui portent sur un monde (liste de l'état vérifié) lisent la règle du monde. Les lecteurs de `CasaEngineGame.cs` et `GameManager.cs` continuent de lire celle du jeu : fenêtre, vues, premier écran, résolution virtuelle. Sans règle propre, le comportement de chaque monde est exactement celui d'aujourd'hui. |
| P3 | Une quatrième règle nommée, `GameplayExecutionPolicies.EditorLivePreview` : comme `EditorPreview`, sauf `IsEditorPreview` faux (les particules simulent toujours, D1), `UpdateAnimatedSprites` et `UpdatePhysicsComponents` vrais, et `RaiseGameplayNotifications` faux (P4). `RunBeginPlay`, `UpdateGameplayScripts` et `InitializePlayerControllers` restent faux ; `ThrowOnScriptTypeResolution` reste faux. |
| P4 | **Ni les collisions ni les événements d'animation n'appellent du code de jeu dans l'aperçu.** Un onzième booléen, `RaiseGameplayNotifications`, vrai dans les trois règles existantes (rien ne change pour elles) et faux dans `EditorLivePreview`. Il est lu sur le monde de l'entité à deux endroits : dans `BepuPhysicsEngine` avant `OnHit` et `OnHitEnded`, et dans `AnimatedSpriteComponent` avant ses trois événements. Un monde sans règle (ni règle propre ni jeu) et une entité sans monde notifient comme aujourd'hui. Les listes de collisions des composants restent tenues à jour. Un nouveau booléen plutôt que `UpdateGameplayScripts`, qui est faux sous `EditorPreview` : le réutiliser changerait ce que font aujourd'hui les mondes et les tests qui avancent la physique sous cette règle. |
| P5 | `UpdatePhysicsEngine` devient aussi une règle par monde pour le composant physique du jeu : quand le jeu fait avancer la physique, il saute un monde dont la règle dit non, **avant** de demander son contexte physique. Dans `EditorLivePreview` il vaut **faux**, avec ce sens : « le composant du jeu ne fait pas avancer ce monde, celui qui le pilote le fait ». Ainsi le mode Play de l'éditeur ne fait pas avancer deux fois le monde d'aperçu. |
| P6 | **C'est l'aperçu qui fait avancer sa physique**, au pas de son transport. `PreviewWorldDriverOptions` gagne `StepPhysics` (faux par défaut : les autres aperçus ne changent pas). Avec l'option, `Tick` appelle `World.Update(seconds)` puis `World.PhysicsWorld.Update(seconds)`, dans l'ordre du jeu, et saute la physique quand le pas est nul ou négatif. `TimeScale` du jeu n'est pas appliqué. Comme dans le jeu, une entité montre la pose de son corps avec un pas de retard. |
| P7 | **Hors lecture, le monde vit aussi** : la règle d'ADR-0139 (le monde reste vivant dès qu'aucune lecture ne court) ne change pas. Après le bouton `Stop`, qui reconstruit déjà le monde, l'état de départ est affiché puis vit (une animation boucle, un corps tombe). `Play`, le bouton `Stop`, un retour en arrière et toute recherche lancée hors lecture reconstruisent le monde depuis son asset, comme aujourd'hui : l'état joué ne dépend pas du temps passé à l'arrêt. Autre choix possible, non retenu : figer le monde hors lecture. À juger à la recette (O2). |
| P8 | **Limites acceptées, écrites dans la documentation.** (a) Une lecture avance au temps des images de l'éditeur, une recherche par pas fixes de 1/60 s : l'état d'un sprite animé, d'une particule ou d'un corps à un instant peut différer entre une lecture et une recherche. (b) Seule une recherche qui relance la lecture (retour en arrière, document modifié, aucune lecture en cours) donne un état reproductible : deux telles recherches vers le même instant donnent le même état, et c'est ce que la validation prouve. Une recherche vers l'avant pendant une lecture continue depuis l'état de cette lecture, comme aujourd'hui, sauf depuis l'état de départ marqué, où elle relance (O5). (c) En lecture, une image de l'éditeur qui dure deux pas de physique ou plus n'en fait qu'un, comme dans le jeu : la physique prend alors du retard sur l'horloge de la cutscene. Le premier pas après une reconstruction, borné à 1/30 s, est dans ce cas. Une recherche n'a pas ce défaut : chacun de ses pas vaut un pas de physique. |
| P9 | La ligne d'état de l'aperçu dit désormais : `Gameplay scripts are not updated in the preview.` |
| P10 | L'ADR de la tranche prend le numéro **0146** : 0145 est déjà pris par la branche des caméras, non fusionnée. |
| P11 | Le sample `Projects/SpriteCutsceneSample` gagne un monde `LiveWorld.world` et une cutscene `Cutscenes/live.cutscene` : un sprite animé en boucle (`AnimatedOrb` : deux images, `orb` et `ring`, de 0,35 s chacune, soit une boucle de 0,7 s), un système de particules (`Sparks` : émission continue, en boucle, particules qui vivent au moins 2 s), un corps dynamique qui tombe (`FallingBox`) vers un sol fixe (`Floor`) placé assez bas pour que la boîte tombe encore à 1 s, et un sprite fixe (`Marker`, composant racine `StaticSpriteComponent` avec le sprite `ring`). Le corps dynamique est un composant de collision **enfant** d'un composant racine, à pose locale nulle, comme `Box.entity`. La cutscene dure au moins 2 s et ne nomme que `Marker` : aucune action ne vise `AnimatedOrb`, `Sparks`, `FallingBox` ni `Floor`. `LiveWorld.world` nomme `live.cutscene` comme cutscene de démarrage (`startup_cutscene_asset_id`), pour que l'aperçu de cette cutscene s'ouvre sur ce monde. Les fichiers existants du sample ne changent pas, sauf `AssetInfos.json` et `README.md`. Le monde de démarrage du sample ne change pas. |
| P12 | L'automatisation n'envoie toujours aucune entrée. Les diagnostics de l'aperçu, qui donnent déjà la position de chaque entité (celle de l'entité, pas celle du corps), gagnent par entité : le nom et le temps de l'animation courante d'un sprite animé, et le nombre de particules vivantes d'un système de particules, lus par des membres publics existants. Une valeur qui n'a pas de membre public n'est pas ajoutée, et aucune API du moteur n'est créée pour elle : la tâche le dit. Aucune nouvelle option de ligne de commande : `--cutscene-preview-seek` (liste d'instants) et `--cutscene-preview-step` suffisent aux lancements de T2.4. |
| P13 | En mode Play de l'éditeur, le monde d'aperçu garde sa propre règle : ses scripts ne sont plus mis à jour à ce moment-là (ils le seraient aujourd'hui, point O4 du plan de la tranche A). |
| P14 | Ce qu'un script fait déjà dans l'aperçu ne change pas : son initialisation, son `OnEndPlay` au vidage du monde et son `Draw` s'exécutent toujours. La tranche n'ajoute aucun appel à un script : P4 ferme les deux chemins que la physique et les animations auraient ouverts. |
| P15 | L'animation squelettique des modèles 3D n'est pas dans la tranche : son comportement dans l'aperçu n'a pas été lu et ne change pas. |
| P16 | Le coût d'une recherche augmente, puisque chaque pas rejoué fait aussi un pas de physique. Il est mesuré sur le sample dans la tâche de validation et écrit dans la documentation ; aucun plafond n'est ajouté. |
| P17 | Les tests ajoutés n'utilisent que les règles nommées, jamais `new GameplayExecutionPolicy` : une règle propre `EditorSimulation` sous un jeu en `EditorPreview` pour ce qui doit tourner, une règle propre `EditorPreview` sous un jeu en `Runtime` pour ce qui doit rester arrêté, puis `EditorLivePreview` à partir de T1.2. Ainsi l'ajout d'un booléen obligatoire en T1.2 ne casse aucun test. |
| P18 | La règle « aucun test existant n'est modifié » porte sur les tests présents à la base de la branche. Un test ajouté par une tâche de ce plan peut être complété par une tâche suivante. |
| P19 | **Une exception nommée à cette règle, à confirmer par l'auteur.** T2.2 ajoute `Cutscenes/live.cutscene` : elle fait passer `RepositoryCutsceneCount` de 15 à 16 dans `CasaEngine.Tests/EditorServices/Cutscenes/CutsceneSaveTests.cs:13`. C'est la seule ligne d'un test existant que le plan change ; le nom du test et le reste du fichier ne changent pas. La nouvelle cutscene est ainsi couverte par le test d'aller-retour de sauvegarde, comme les quinze autres. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/cutscene-live-preview`**, créée depuis `main` `ae2a42be` plus le commit de clôture `adb35431`. Ne jamais committer sur `main`.
- **Travailler dans le worktree `D:\development\repo\CasaEngineMonogame-cutscene`**, jamais dans l'arbre principal du moteur, qu'une autre session occupe. Ne jamais arrêter un éditeur que l'auteur a lancé.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API utilisée existe dans le dépôt et a été lue. Sinon : ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Aucun test existant n'est modifié**, à la seule exception nommée par P19. Si un autre test existant échoue à cause d'une tâche : ⚠️ Blocked, avec le nom du test et la raison.
- **Build obligatoire** avant ✅ dès que du code est touché : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, sans `--no-build`.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** ce qui n'est pas de la tâche : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Lancements automatisés de l'éditeur** : uniquement par ses options d'automatisation, sorties (`--diagnostics-out`, `--screenshot-out`) hors du dépôt, jamais `--keep-open`, aucune entrée clavier ou souris, aucun asset enregistré. Chaque lancement crée `Projects/SpriteCutsceneSample/.casaeditor/viewport.editor.json` dans le worktree : après le lancement, supprimer ce fichier et lui seul (tout autre fichier non suivi apparu sous ce `.casaeditor/` est nommé sous la tâche avant d'être supprimé), jamais un fichier suivi, jamais rien dans l'arbre principal du moteur. `git status --short` doit ensuite ne rien afficher d'autre que le travail de la tâche.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds (`World.Update`, `Entity.Update`, les `Update` des composants, l'envoi des collisions) ; le runtime ne dépend pas de l'éditeur ; sérialisation additive (ici : aucun format ne change).

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout vert ; le nombre de tests est celui relevé en T0.1 plus les tests ajoutés ; aucun test existant modifié hors P19 (`git diff --stat <base>..HEAD -- CasaEngine.Tests` ne montre que des fichiers ajoutés, plus une ligne changée dans `CutsceneSaveTests.cs`).
- Lancements automatisés de T2.4 : les valeurs attendues sont dans les diagnostics, la capture est regardée, `git status` est propre.
- Recette de l'auteur dans l'éditeur lancé (T3.2).

---

## Phase 0 — Décision

### ⏳ T0.1 — ADR-0146 et relevé de départ

- Objectif : écrire la décision avant le code, et relever l'état de départ des tests et des diagnostics.
- Fichiers : `docs/decisions/0146-a-world-can-run-under-its-own-execution-policy.md`, `docs/decisions/README.md`, ce plan.
- Étapes :
  1. Lancer `dotnet build CasaEngine.Editor.MonoGame.sln` puis `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` sur la base de la branche ; noter sous cette tâche le nombre de tests et le résultat. Si le build ou un test échoue avant tout changement : ⚠️ Blocked.
  2. Relever la référence des diagnostics, avant tout changement de code : lancer deux fois l'éditeur sur `Projects/SpriteCutsceneSample` avec `--open-asset Cutscenes/loop.cutscene --cutscene-preview-seek 2 --diagnostics-out <fichier hors dépôt>` et les autres options qu'exigent les lancements automatisés (les lire dans `EditorAutomationOptions.cs` et dans les notes de validation des plans archivés). Noter la ligne de commande exacte sous la tâche et garder les deux fichiers hors du dépôt. Les lignes de l'aperçu doivent être identiques d'un lancement à l'autre ; celles qui ne le sont pas sont listées sous la tâche et sorties des comparaisons de T2.3 et de T2.4.
  3. Écrire l'ADR avec le skill `adr`, statut `Proposed`, en anglais : contexte (une règle par jeu, l'aperçu figé, ADR-0135, ADR-0138 et ADR-0139), décision (P1 à P8, P13, P14), conséquences (les trois limites de P8, P15, P16 ; ce que la tranche I reprendra). L'ADR ne dit pas qu'un état est reproductible au-delà de ce que P8 (b) établit. Source : ce plan et les fichiers cités dans l'état vérifié.
  4. Ajouter la ligne à l'index des ADR.
- Validation : les deux lancements de référence sortent avec le code 0 ; après la suppression du fichier que chaque lancement crée (règle des lancements automatisés), `git status --short` est propre, et la note de la tâche le dit ; l'ADR ne contient que des faits de l'état vérifié et les arbitrages du plan ; le numéro 0146 est libre dans `main` et dans `chantier/camera-behaviours` (`git ls-tree --name-only <branche> docs/decisions/`).
- Commit : `docs(adr): let a world run under its own execution policy (ADR-0146)`

---

## Phase 1 — Runtime (`CasaEngine`)

### ⏳ T1.1 — Règle d'exécution par monde

- Objectif : un monde peut porter sa règle ; sans elle, rien ne change.
- Fichiers : `CasaEngine/Framework/Scene/World/World.cs`, `CasaEngine/Framework/Scene/Entities/Entity.cs`, `CasaEngine/Framework/Scene/Entities/Components/AnimatedSpriteComponent.cs`, `PhysicsBaseComponent.cs`, `ParticleSystemComponent.cs`, tests ajoutés dans `CasaEngine.Tests/Scene/`, ce plan.
- Étapes :
  1. `World.ExecutionPolicyOverride` et `World.ExecutionPolicy` selon P1 (nulle sans règle propre et sans jeu ; exception si on la pose quand le monde a un jeu), avec un commentaire XML qui dit quand la règle propre se pose et ce qu'elle ne fait pas (ni enregistrée, ni copiée).
  2. Remplacer la lecture de `Game.ExecutionPolicy` par celle du monde aux sites de `World.cs`, `Entity.cs`, `AnimatedSpriteComponent.cs`, `PhysicsBaseComponent.cs` et `ParticleSystemComponent.cs` listés dans l'état vérifié, en gardant à chaque site la tolérance actuelle à un monde ou un jeu nul (`World.cs:669`, `Entity.cs:538`, `AnimatedSpriteComponent.cs:223` et `ParticleSystemComponent.cs:347` s'en servent).
  3. Relancer `rg "Game\??\.ExecutionPolicy" CasaEngine/Framework/Scene` : il ne doit rester aucun lecteur par monde qui lise la règle du jeu, sauf la propriété `World.ExecutionPolicy` elle-même.
- Validation : build des deux solutions ; `dotnet test` entier vert, aucun test existant modifié. Tests ajoutés, avec le harnais des tests existants (jeu non initialisé dont on fixe la règle ; la règle propre est posée avant le jeu) et les seules règles nommées (P17) : sous un jeu en `EditorPreview`, un monde **sans** règle propre garde le temps de son animation à la même valeur après une mise à jour, et son système de particules dont `SimulateInEditor` est faux n'émet rien ; le même monde **avec** la règle propre `EditorSimulation` voit le temps de son animation avancer du pas donné, et ses particules simulent ; sous un jeu en `Runtime`, un monde avec la règle propre `EditorPreview` ne lance rien à `BeginPlay` et ne met pas à jour le script d'une entité ; un monde sans jeu rend une règle nulle sans lever d'exception ; poser la règle propre quand le monde a un jeu lève l'exception. Si le harnais existant ne permet pas un de ces tests : le dire sous la tâche, ne pas l'inventer.
- Commit : `feat(world): let a world run under its own execution policy`

### ⏳ T1.2 — Règle de l'aperçu vivant et physique par monde

- Objectif : la règle nommée de l'aperçu vivant, les collisions et les événements d'animation qui n'appellent pas de code de jeu quand la règle le dit, et un composant physique du jeu qui respecte la règle de chaque monde.
- Fichiers : `CasaEngine/Framework/Application/GameplayExecutionPolicy.cs`, `CasaEngine/Framework/Application/Components/Physics/PhysicsSystemComponent.cs`, `CasaEngine/Framework/Physics/Bepu/BepuPhysicsEngine.cs`, `CasaEngine/Framework/Scene/Entities/Components/AnimatedSpriteComponent.cs`, tests ajoutés dans `CasaEngine.Tests/Physics/` et `CasaEngine.Tests/Scene/` (ceux de T1.1 peuvent être complétés, P18), ce plan.
- Étapes :
  1. Ajouter `RaiseGameplayNotifications` (P4) à la classe et aux trois règles existantes, à vrai ; ajouter `EditorLivePreview` (P3, P4, P5), avec un commentaire XML qui dit le sens de `UpdatePhysicsEngine` faux pour cette règle.
  2. `PhysicsSystemComponent.GetOrCreateContext` lit `UseExternalViewManagement` sur la règle du monde, en tolérant une règle nulle (`world.ExecutionPolicy?.UseExternalViewManagement == true`) : deux tests existants l'appellent avec un monde sans jeu. `Update` garde son retour immédiat sur la règle du jeu, puis saute tout monde dont la règle dit de ne pas faire avancer la physique, avant d'appeler `GetOrCreateContext` pour lui (P5).
  3. `BepuPhysicsEngine` : avant chacun des six appels à `OnHit` et `OnHitEnded`, lire la règle du monde de l'entité (P4), par une méthode privée commune, sans allocation ; une entité sans monde, ou un monde sans règle, notifie comme aujourd'hui.
  4. `AnimatedSpriteComponent` : la même lecture avant chacune des cinq levées de `AnimationFinished`, `AnimationLooped` et `AnimationEventTriggered` (`:248`, `:253`, `:352`, `:371`, `:919`), par une propriété privée commune.
- Validation : build ; `dotnet test` entier vert, aucun test existant modifié ; `rg "new GameplayExecutionPolicy" CasaEngine.Tests` ne rend rien (P17). Tests ajoutés : sous un jeu en `EditorSimulation`, `PhysicsSystemComponent.Update` fait avancer un monde ordinaire et saute un monde sous `EditorLivePreview` (un corps dynamique y garde sa position) ; ce même monde avance quand on appelle `PhysicsWorld.Update` soi-même, et son composant physique reporte la position du corps sur l'entité à la mise à jour suivante, le corps dynamique étant un composant de collision enfant d'un composant racine, à pose locale nulle (sans parent, le corps ne déplace pas l'entité) ; une collision dans un monde sous `EditorLivePreview` n'appelle ni `OnHit` ni `OnHitEnded` d'un script de test mais remplit la liste des collisions du composant ; la même collision sous `Runtime` appelle le script ; un sprite animé d'un monde sous `EditorLivePreview` qui atteint la fin de son animation ne lève pas `AnimationFinished`, et le lève sous `Runtime`. Si un test existant échoue : ⚠️ Blocked.
- Commit : `feat(physics): add the live preview policy and follow the rule of each world`

---

## Phase 2 — Éditeur et sample

### ⏳ T2.1 — L'aperçu des cutscenes devient vivant

- Objectif : le monde d'aperçu d'une cutscene porte `EditorLivePreview` et fait avancer sa physique à chaque pas.
- Fichiers : `CasaEngine.Editor/Runtime/PreviewWorldDriver.cs`, `CasaEngine.Editor/Runtime/CutscenePreviewSession.cs`, `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`, tests dans `CasaEngine.Tests/Editor/` si un harnais existe, ce plan.
- Étapes :
  1. `PreviewWorldDriverOptions.StepPhysics` et le pas de physique de `Tick(float)` selon P6. Vérifier par `rg "new PreviewWorldDriverOptions"` qu'aucun autre aperçu n'active l'option.
  2. Dans `CutscenePreviewSession`, la fabrique passée à `Rebuild` pose la règle propre sur la copie du monde avant son chargement ; le pilote est créé avec `StepPhysics`.
  3. Remplacer `PreviewLimitText` par le texte de P9, et mettre à jour les commentaires de classe qui disent l'aperçu figé.
  4. Chercher les tests qui citent l'ancien texte (`rg "frozen in the preview" CasaEngine.Tests`). S'il y en a : ⚠️ Blocked avec la liste, puisqu'aucun test existant ne se modifie sans accord.
  5. Le pas de physique de `Tick` est dans le pilote, qu'aucun test ne construit : ne pas créer de harnais pour lui.
- Validation : build ; `dotnet test` entier vert. Le câblage de la session n'a pas de test (aucun test ne construit la session) : il est prouvé par T2.4, et la tâche reste 🧪 jusque-là si rien d'autre ne le prouve.
- Commit : `feat(editor): run sprite animations, particles and physics in the cutscene preview`

### ⏳ T2.2 — Sample vivant

- Objectif : un monde et une cutscene du sample qui montrent les trois choses, et qui servent à la validation.
- Fichiers : `Projects/SpriteCutsceneSample/LiveWorld.world`, `Cutscenes/live.cutscene`, les assets nécessaires (animation 2D, effet de particules), `AssetInfos.json`, `README.md`, `CasaEngine.Tests/EditorServices/Cutscenes/CutsceneSaveTests.cs` (la constante de P19, rien d'autre), un test ajouté, ce plan.
- Étapes :
  1. Lire le format de chaque asset dans des fichiers existants du dépôt (un monde avec `AnimatedSpriteComponent`, un effet de particules, un corps dynamique et un corps statique) et dans leurs sérialiseurs. Ne rien écrire qui ne soit pas lu dans un de ces deux endroits ; si un format ne peut pas être établi : ⚠️ Blocked.
  2. Écrire le monde de P11 avec ses cinq entités nommées (`AnimatedOrb`, `Sparks`, `FallingBox`, `Floor`, `Marker`) et les valeurs que P11 fixe, et une cutscene d'au moins 2 s qui attend, puis montre et déplace `Marker`, et lui seul. Le monde nomme cette cutscene par `startup_cutscene_asset_id`, au format de `SpriteWorld.world:242`. `FallingBox` suit le modèle de `Projects/SampleProject/Entities/Box.entity` : un composant racine et, en enfant à pose locale nulle, le composant de collision dynamique. La gravité par défaut fait tomber un corps d'environ 5 unités en 1 s : `Floor` est placé à plus de 10 unités sous la boîte, pour qu'elle tombe encore à 1 s. Si le projet ou le monde règle une autre gravité ou un autre espace de simulation, le noter sous la tâche et garder la même marge.
  3. Enregistrer les nouveaux assets dans `AssetInfos.json` avec des identifiants neufs ; décrire le monde dans `README.md`.
  4. Vérifier par un test ajouté, sur le modèle des tests de sample existants : le monde et la cutscene se chargent sans erreur de format ; la validation de la cutscene ne signale rien ; l'identifiant de cutscene de démarrage du monde est celui de `live.cutscene` dans le catalogue ; chaque entité que la cutscene nomme existe dans `LiveWorld.world` avec un `StaticSpriteComponent` en racine.
  5. Passer `RepositoryCutsceneCount` de 15 à 16 (P19).
- Validation : build ; `dotnet test` vert, dont `TheRepositoryHoldsTheFourteenCutsceneFiles` et le test d'aller-retour de sauvegarde pour `live.cutscene` ; les fichiers existants du sample sont inchangés hors `AssetInfos.json` et `README.md`, et `CutsceneSaveTests.cs` ne change que d'une ligne (`git diff --stat`).
- Commit : `feat(samples): add a live world to the sprite cutscene sample`

### ⏳ T2.3 — Diagnostics de l'aperçu vivant

- Objectif : pouvoir lire, sans regarder l'écran, ce que le monde d'aperçu fait.
- Fichiers : `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs` (`GetAutomationStateSnapshot`, `AppendEntityPositions`), ce plan. `GameEditor.cs` ne change pas : il recopie les lignes du panneau.
- Étapes :
  1. Ajouter dans `AppendEntityPositions` (ou à côté) les valeurs de P12, pour les entités du monde d'aperçu.
  2. Refaire sur la branche le lancement de référence de T0.1, avec la même ligne de commande.
- Validation : build ; `dotnet test` vert ; les lignes de l'aperçu du lancement de l'étape 2 sont celles de la référence de T0.1, à deux écarts attendus près : la ligne `Preview status line` porte le texte de P9 au lieu de l'ancien, et les lignes d'entité portent les valeurs ajoutées. Tout autre écart est un défaut. La comparaison est notée sous la tâche.
- Commit : `feat(editor): report the live state of the cutscene preview world in the diagnostics`

### ⏳ T2.4 — Lancements de validation

- Objectif : prouver, sans entrée envoyée à l'éditeur, que l'aperçu vit, se fige et se rejoue.
- Sources : `Projects/SpriteCutsceneSample`, `Cutscenes/live.cutscene` et `Cutscenes/loop.cutscene` ; sorties hors du dépôt.
- Étapes, chaque lancement sur `Projects/SpriteCutsceneSample` avec `--open-asset`, `--diagnostics-out` et `--screenshot-out`, sans lecture libre (`--cutscene-preview-play` seul donnerait des valeurs qui dépendent des images de l'éditeur) :
  1. `Cutscenes/live.cutscene`, `--cutscene-preview-seek 1` : la ligne `Preview world:` nomme `LiveWorld` et les lignes d'entité comptent `AnimatedOrb`, `Sparks`, `FallingBox`, `Floor` et `Marker` ; l'état est `Playing` et le transport `Paused` ; le `y` de `FallingBox` est plus petit que son `y` de départ, lu dans `LiveWorld.world` (gravité par défaut ; si T2.2 a noté une autre gravité, le sens attendu est le sien, ici et à l'étape 3) ; le temps d'animation d'`AnimatedOrb` est supérieur à 0 ; `Sparks` a plus de 0 particule vivante.
  2. Le même lancement une seconde fois : mêmes valeurs qu'en 1 (une recherche qui relance se rejoue à l'identique, P8).
  3. `--cutscene-preview-seek 0.5` : y(1 s) < y(0,5 s) < y(départ) pour `FallingBox` ; le temps d'animation d'`AnimatedOrb` est supérieur à 0 et différent de celui de 1 s ; `Sparks` a plus de 0 particule vivante.
  4. `--cutscene-preview-seek 1,0.5` (retour en arrière dans le même lancement) : mêmes valeurs qu'en 3.
  5. `--cutscene-preview-seek 0` : `FallingBox` est à son `y` de départ et le temps d'animation d'`AnimatedOrb` vaut 0 (premier instant, pas de durée nulle) ; le nombre de particules n'est pas comparé.
  6. `Cutscenes/loop.cutscene`, `--cutscene-preview-seek 2` : mêmes lignes d'aperçu que la référence de T0.1, aux deux écarts attendus de T2.3 près (aucune régression du sample fixe).
  7. Relever ce que coûte la recherche vers 1 s (nombre d'images de l'éditeur ou de pas que les diagnostics donnent entre la demande et l'état `Paused`) et le noter pour P16 ; si les diagnostics ne le donnent pas, le dire.
  8. Un lancement avec `--cutscene-preview-play` sur `live.cutscene`, pour la capture seulement : aucune valeur n'en est attendue, sauf que l'éditeur sort avec le code 0 et que rien n'est signalé dans le journal.
- Validation : chaque capture est regardée et décrite honnêtement ; chaque valeur est comparée à l'attente ; `git status` propre après les lancements. Un écart est un défaut à corriger avant ✅, ou à noter en 🧪.
- Limite d'essais : deux séries de corrections au plus dans cette tâche ; au-delà, 🧪 avec la liste exacte de ce qui reste.
- Commit : `docs(plan): record the validation runs of the live cutscene preview`

---

## Phase 3 — Documentation et recette

### ⏳ T3.1 — Documentation

- Objectif : dire ce que l'aperçu fait maintenant, et ce que le moteur permet.
- Fichiers : `docs/editor/cutscene-editor.md`, la page de `docs/engine/` qui décrit la règle d'exécution si elle existe (sinon une courte page nouvelle, ajoutée à `docs/README.md`), `ai-agent/README.md`, ce plan.
- Étapes :
  1. `cutscene-editor.md` : retirer la limite « frozen » ; décrire ce qui vit, ce que les scripts font et ne font pas (P4, P14), ce qui n'est pas couvert (P15), l'état hors lecture (P7), les trois limites de P8, le coût mesuré (P16) ; garder la limite du cadrage, renvoyée à la tranche suivante.
  2. Moteur : `World.ExecutionPolicyOverride`, la règle `EditorLivePreview`, `RaiseGameplayNotifications`, le sens de `UpdatePhysicsEngine` par monde, avec un extrait d'usage.
  3. Mettre à jour la ligne du plan dans `ai-agent/README.md`.
- Validation : chaque phrase de la documentation correspond à du code livré ; aucun fichier ne nomme autre chose que le moteur et ses samples.
- Commit : `docs(editor): describe the live cutscene preview and the per-world execution policy`

### ⏳ T3.2 — Recette de l'auteur

- Objectif : validation dans l'éditeur lancé, que l'agent ne peut pas faire.
- Étapes (auteur) : ouvrir `Projects/SpriteCutsceneSample`, puis `Cutscenes/live.cutscene` avec le monde `LiveWorld` ; `Play` : le sprite s'anime, la boîte tombe, les particules sortent ; `Pause` : tout se fige ; glisser la tête de lecture en arrière : l'état revient ; `Stop` : état de départ, qui vit ensuite (P7). Confirmer ou corriger P1 à P19, puis l'ADR-0146 passe à `Accepted`.
- Validation : réponse de l'auteur. La tâche reste 🧪 jusque-là.
- Commit : `docs(plan): record the acceptance of the live cutscene preview`

---

## Tranche suivante

**Tranche I — la vue du jeu** (D2, D3) : l'aperçu montre par défaut ce que le jeu montrera (caméra du monde, résolution virtuelle du monde, bandes noires), avec un bouton pour revenir à la vue libre. Elle demande son propre plan. Avant de l'écrire : regarder où en est `chantier/camera-behaviours` (D3), qui modifie `WorldViewportPanel` et les caméras, et repartir d'un `main` qui la contient si elle est finie.

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Confirmation de P1 → P19 par l'auteur ; P19 avant le lancement, puisqu'elle touche un test existant. | Toutes |
| O2 | P7 : laisser le monde vivre hors lecture, ou le figer ? À juger à la recette. | T2.1, T3.2 |
| O3 | Le numéro d'ADR 0146 suppose que la branche des caméras garde 0145 ; si elle est abandonnée, le trou reste. | T0.1 |
| O4 | Coût d'une recherche sur un grand monde avec physique : mesuré seulement sur le sample. | T2.4 |
| O5 | Une recherche vers l'avant pendant une lecture garde l'état de cette lecture, qui a avancé au temps des images (P8 b). La faire relancer la lecture rendrait toute recherche reproductible, au prix d'une reconstruction à chaque mouvement vers l'avant : non fait dans cette tranche, à décider après la recette. | T2.1 |
| O6 | La physique perd du temps sur une image longue (P8 c), comme dans le jeu. Faire avancer la physique de l'aperçu par tranches d'un pas fixe l'éviterait, mais l'aperçu ne se comporterait plus comme le jeu : non fait. | T2.1 |

## Hors périmètre

- La mise à jour des scripts de gameplay dans l'aperçu, `BeginPlay`, les contrôleurs de joueur. Ce que les scripts font déjà dans l'aperçu (initialisation, `OnEndPlay`, `Draw`) ne change pas.
- La vue du jeu : caméra du monde, résolution virtuelle, bandes noires (tranche I).
- L'animation squelettique des modèles 3D.
- Les autres aperçus de l'éditeur (matériau, sprite, particules, Animation2D, modèle) et le monde en cours d'édition : aucun ne reçoit de règle propre.
- Le confort de la timeline, les raccourcis du transport, le réglage de vitesse.
- Tout changement de format d'asset.
