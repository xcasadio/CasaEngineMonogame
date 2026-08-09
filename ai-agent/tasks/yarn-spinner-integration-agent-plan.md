# Plan agent IA - Integration Yarn Spinner

Date de creation : 2026-05-31.

Ce plan est base uniquement sur les elements verifies dans le depot et sur les pages NuGet consultees pour les packages `YarnSpinner` et `YarnSpinner.Compiler`. Ne pas transformer une proposition de ce document en fait etabli sans verification locale.

## Legende des statuts

- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

Chaque tache ci-dessous doit garder une icone de statut devant son titre. L'agent doit mettre l'icone a jour dans ce fichier avant de terminer la tache.

## Regles obligatoires pour l'agent

1. Avant chaque tache, relire les fichiers cites dans la tache et verifier que leur etat n'a pas change.
2. Passer la tache en `🚧 In progress` pendant l'implementation.
3. Ne pas coder contre une API Yarn Spinner supposee. Ajouter le package, restaurer, puis inspecter les types disponibles avant d'ecrire l'adaptateur.
4. Ne pas inventer d'action d'input `Action` ou `Interact`. Verifier d'abord les abstractions d'input existantes. Si aucune action logique n'existe, livrer une touche de test documentee dans la demo et ouvrir une tache separee pour l'abstraction d'action.
5. Ne pas modifier les fichiers binaires ou generes deja modifies dans le workspace, notamment `Projects/RPGDemo/CasaEngine.RPGDemo.dll`, `Projects/RPGDemo/CasaEngine.RPGDemo.pdb` et `Projects/SampleProject/.editor-trash/`.
6. A la fin de chaque tache atomique, lancer la validation indiquee, passer le statut en `✅ Done`, `🧪 Needs testing` ou `⚠️ Blocked`, puis creer un commit dedie.
7. Le commit doit inclure la mise a jour de statut du plan et uniquement les fichiers necessaires a la tache.
8. Ne pas fusionner plusieurs taches dans un seul commit, sauf si une tache precedente est impossible a compiler seule et que le blocage est documente dans ce fichier.

## Faits verifies

- `Directory.Packages.props` active `ManagePackageVersionsCentrally` et contient les versions NuGet centrales.
- `Directory.Packages.props` ne contient pas encore `YarnSpinner` ni `YarnSpinner.Compiler`.
- Les pages NuGet `YarnSpinner` et `YarnSpinner.Compiler` existent et affichent la version `3.2.1`, compatible `.NET Standard 2.0`.
- `Directory.Build.props` definit `BaseTargetFramework` sur `net9.0` et `WindowsTargetFramework` sur `net9.0-windows`.
- `CasaEngine/CasaEngine.csproj` est le projet runtime principal `CasaEngine`, cible `$(WindowsTargetFramework)` et reference deja des packages via `PackageReference` sans version locale.
- `CasaEngine.Compiler/CasaEngine.Compiler.csproj` existe et cible `$(BaseTargetFramework)`, mais aucun role lie aux assets Yarn n'est verifie.
- `CasaEngine.EditorServices/CasaEngine.EditorServices.csproj` reference `CasaEngine`, mais aucun importer Yarn n'existe.
- `CasaEngine/Framework/Assets/AssetContentManager.cs` charge les assets par type via `IAssetLoader` en resolvant un `AssetInfo`.
- `CasaEngine/Framework/Assets/AssetLoaderRegistry.cs` enregistre les loaders centraux, dont `CutsceneAssetLoader`, `MaterialAssetLoader`, `ParticleEffectAssetLoader` et `AssetLoader<UIScreenAsset>`.
- `CasaEngine/Framework/Assets/IAssetLoader.cs` expose `LoadAsset(string, AssetContentManager)` et `IsFileSupported(string)`.
- `CasaEngine/Framework/Configuration/Constants.cs` declare les extensions d'assets existantes, sans extension dialogue ou Yarn.
- `CasaEngine/Framework/UI/UIRoot.cs` cree un `ScreenStack`, expose `PushScreen`, `PopScreen`, `RemoveScreen`, et `HasModalInput`.
- `CasaEngine/Framework/UI/ScreenStack.cs` met a jour uniquement les ecrans au-dessus du dernier ecran bloquant et expose `HasModalInput` via `BlocksViewsBelow`.
- `CasaEngine/Framework/UI/IUIScreen.cs` et `CasaEngine/Framework/UI/UIScreenBase.cs` fournissent l'abstraction d'ecran UI.
- `CasaEngine.Demos/Demos/UIOverlay/PauseMenuScreen.cs` montre un ecran MGUI modal base sur `UIScreenBase`.
- `CasaEngine/Framework/Input/InputRouter.cs` priorise une vue modale via `UIView.InputState.HasModalInput` et route alors l'input avec `InputRoutingReason.Modal`.
- `CasaEngine/Framework/Gameplay/GameplayModeRunner.cs` expose `Pause()` et `Resume()`.
- `CasaEngine/Framework/Cutscenes` contient `CutsceneAsset`, `CutsceneActionData`, `CutsceneActionTypes`, `CutsceneAssetJsonSerializer` et `CutsceneActionCoroutineFactory`.
- Les actions cutscene verifiees sont `Wait`, `MoveTo`, `Sequence` et `Parallel`.
- La recherche locale n'a pas trouve de module `Dialogue`, `DialogueService`, `YarnDialogueAsset`, fichier `.yarn` ou reference Yarn hors du document `docs/engine/yarn_spinner_integration.md`.

## Critique du document actuel

- Le document `docs/engine/yarn_spinner_integration.md` pose une bonne direction generale : ne pas reimplementer un langage de dialogue et separer runtime narratif, UI, gameplay et outils.
- Il melange toutefois faits, recommandations et hypotheses. Les mentions comme `probablement CasaEngine.Framework` ou les noms de classes proposes ne sont pas des faits du code actuel.
- La roadmap est utile mais trop optimiste : elle affiche des coches dans les sections V1/V2/V3 alors que les elements ne sont pas implementes dans le depot.
- Le document propose `Input Action` / `Interact`, mais la recherche effectuee n'a pas confirme une abstraction d'action logique existante. Ce point doit etre une tache d'investigation avant implementation.
- Le document propose un `YarnDialogueRunner` conceptuel, mais l'API exacte Yarn Spinner n'a pas ete inspectee dans le code compile. L'adaptateur doit etre ecrit apres restauration du package, pas depuis un pseudo-code.
- Le document propose une integration cutscene, mais le systeme cutscene actuel ne connait que `Wait`, `MoveTo`, `Sequence` et `Parallel`; ajouter `StartDialogue` implique de modifier les types, la serialization, la factory et les tests.
- Le document conseille une compilation d'asset a l'import, ce qui est coherent avec `AssetContentManager` et `AssetLoaderRegistry`, mais aucun importer editor/pipeline Yarn n'existe encore. Il faut commencer par un asset runtime minimal et une validation de chargement avant d'automatiser l'import.
- Le document cite des sources externes. Dans ce plan, seules les pages NuGet des deux packages ont ete reverifiees.

## Plan de travail atomique

### ✅ Tache 1 - Verifier dependances Yarn et compatibilite build

Objectif : ajouter uniquement les references NuGet necessaires, sans code runtime.

Fichiers a verifier avant modification :

- `Directory.Packages.props`
- `CasaEngine/CasaEngine.csproj`
- `CasaEngine.EditorServices/CasaEngine.EditorServices.csproj`
- `CasaEngine.Compiler/CasaEngine.Compiler.csproj`

Actions :

1. Ajouter `YarnSpinner` et `YarnSpinner.Compiler` dans `Directory.Packages.props` avec la version verifiee au moment de la tache.
2. Ajouter `PackageReference Include="YarnSpinner"` dans `CasaEngine/CasaEngine.csproj` seulement si le runtime dialogue vit dans `CasaEngine`.
3. Ajouter `PackageReference Include="YarnSpinner.Compiler"` uniquement dans le projet qui compile les sources `.yarn`; choisir ce projet apres verification locale, pas par supposition.
4. Restaurer les packages.
5. Inspecter les types publics disponibles dans les packages avant toute tache d'adaptation.

Validation :

- `dotnet restore CasaEngine.MonoGame.sln`
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Resultat 2026-05-31 : restore OK, build OK avec avertissements existants. Assemblies inspectees : `YarnSpinner.dll` expose notamment `Yarn.Dialogue`, `Yarn.Program`, `Yarn.Line`, `Yarn.OptionSet`, `Yarn.MemoryVariableStore`; `YarnSpinner.Compiler.dll` expose notamment `Yarn.Compiler.Compiler`, `Yarn.Compiler.CompilationJob` et `Yarn.Compiler.CompilationResult`.

Commit requis : oui, un commit dedie avec les changements de packages et le statut de cette tache.

### 🧪 Tache 2 - Creer le noyau runtime dialogue sans Yarn

Objectif : creer une API de dialogue testable sans UI et sans dependance Yarn directe.

Fichiers a verifier avant modification :

- `CasaEngine/CasaEngine.csproj`
- `CasaEngine/Framework/Gameplay/GameplayModeRunner.cs`
- `CasaEngine/Framework/Scene/World/World.cs`

Actions :

1. Creer un dossier runtime sous `CasaEngine/Framework/Dialogue/Runtime`.
2. Ajouter un service minimal de dialogue avec etat ouvert/ferme, ligne courante et evenements de changement d'etat.
3. Ne pas referencer MGUI, `SpriteBatch`, `ScreenStack` ou Yarn dans ce service.
4. Ajouter des tests unitaires pour ouverture, fermeture, double ouverture et fermeture idempotente.
5. Si le service doit etre attache au `World`, verifier le cycle de vie de `World` avant d'ajouter la propriete ou le runtime system correspondant.

Validation :

- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore --filter Dialogue`
- Si le filtre ne trouve aucun test, lancer les tests ajoutes par nom complet.
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Resultat 2026-05-31 : noyau runtime ajoute et `dotnet build CasaEngine/CasaEngine.csproj --no-restore` OK. `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore --filter Dialogue` est bloque avant execution par des erreurs de compilation existantes hors dialogue (`DualQuaternion`, `Pool<>`, `LightComponent.Coordinates`).

Commit requis : oui, un commit dedie avec code, tests et statut.

### ✅ Tache 3 - Ajouter une UI dialogue modale minimale

Objectif : afficher une ligne de texte via le systeme UI existant.

Fichiers a verifier avant modification :

- `CasaEngine/Framework/UI/IUIScreen.cs`
- `CasaEngine/Framework/UI/UIScreenBase.cs`
- `CasaEngine/Framework/UI/UIRoot.cs`
- `CasaEngine/Framework/UI/ScreenStack.cs`
- `CasaEngine.Demos/Demos/UIOverlay/PauseMenuScreen.cs`

Actions :

1. Creer un ecran `DialogueScreen` base sur `UIScreenBase`.
2. Utiliser MGUI comme les ecrans existants, avec `IsModal == true` ou `BlocksViewsBelow == true` selon le comportement voulu.
3. Brancher l'ecran sur le service runtime via un presenter ou une interface de presentation simple.
4. Eviter toute reference a Yarn dans cette tache.
5. Ajouter un test ou une verification de pile UI si une couverture automatisable existe.

Validation :

- `dotnet build CasaEngine.MonoGame.sln --no-restore`
- Verification manuelle dans une demo si l'ecran est branche a une demo a cette etape.

Resultat 2026-05-31 : `DialogueScreen` modal ajoute, branche sur `DialogueService` sans Yarn. `dotnet build CasaEngine.MonoGame.sln --no-restore` OK avec avertissements existants. Verification manuelle repoussee a la tache 4, ou l'ecran est branche a une demo.

Commit requis : oui, un commit dedie avec UI minimale et statut.

### 🧪 Tache 4 - Creer une demo manuelle ouvrir/fermer

Objectif : prouver que `ScreenStack`, modalite UI et service dialogue fonctionnent ensemble.

Fichiers a verifier avant modification :

- `CasaEngine.Demos/Demos/UIOverlayDemo.cs`
- `CasaEngine.Demos/DemosGame.cs`
- `CasaEngine/Framework/Input/InputRouter.cs`
- Tout fichier de demo choisi comme point d'integration.

Actions :

1. Ajouter un declencheur de test dans une demo existante ou une nouvelle demo minimale.
2. Ne pas nommer ce declencheur `Action` ou `Interact` tant que l'abstraction correspondante n'est pas verifiee.
3. Afficher le texte `Bonjour depuis CasaEngine.` via le service et le `DialogueScreen`.
4. Fermer la boite avec le meme declencheur ou un controle explicitement documente dans la demo.
5. Verifier que l'ecran modal active `HasModalInput` et evite la double consommation par le gameplay.

Validation :

- `dotnet build CasaEngine.MonoGame.sln --no-restore`
- Lancer la demo si possible et noter le resultat dans ce fichier avant commit.

Resultat 2026-05-31 : `UIOverlayDemo` branche `DialogueService` + `DialogueScreen`. Le dialogue s'ouvre via le bouton HUD `Open Dialogue` ou la touche de test `D`, affiche `Bonjour depuis CasaEngine.`, puis se ferme via `D`, le bouton `Close` ou la fermeture de fenetre. `dotnet build CasaEngine.MonoGame.sln --no-restore` OK avec avertissements existants. Lancement automatise tente avec capture, commande sans sortie console mais aucune capture n'a ete produite; verification interactive de la fenetre reste a faire.

Commit requis : oui, un commit dedie avec demo et statut.

### 🧪 Tache 5 - Ajouter les contrats de presentation dialogue

Objectif : separer durablement runtime et UI avant de brancher Yarn.

Fichiers a verifier avant modification :

- Les fichiers crees aux taches 2 et 3.

Actions :

1. Introduire `IDialoguePresenter` ou un contrat equivalent.
2. Ajouter des DTO de presentation pour ligne et choix seulement si necessaire a l'etape courante.
3. Adapter `DialogueScreen` pour consommer ce contrat.
4. Garder les collections en dehors des hot paths ou les reutiliser si elles sont mises a jour par frame.
5. Tester que la demo de la tache 4 fonctionne encore.

Validation :

- Tests dialogue ajoutes.
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Resultat 2026-05-31 : `IDialoguePresenter` et evenement `PresentationChanged` ajoutes; `DialogueScreen` consomme le contrat au lieu de `DialogueService`. `dotnet build CasaEngine.MonoGame.sln --no-restore` OK avec avertissements existants. Tests dialogue ajoutes, mais `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore --filter Dialogue` reste bloque avant execution par des erreurs de compilation existantes hors dialogue (`Pool<>`, `DualQuaternion`, et autres erreurs deja presentes dans le projet de tests).

Commit requis : oui, un commit dedie avec contrats et statut.

### 🧪 Tache 6 - Creer l'asset dialogue runtime minimal

Objectif : ajouter un type d'asset CasaEngine pour representer un dialogue compile ou serialise, sans compiler Yarn encore.

Fichiers a verifier avant modification :

- `CasaEngine/Framework/Assets/AssetContentManager.cs`
- `CasaEngine/Framework/Assets/AssetLoaderRegistry.cs`
- `CasaEngine/Framework/Assets/IAssetLoader.cs`
- `CasaEngine/Framework/Configuration/Constants.cs`
- `CasaEngine/Framework/Cutscenes/CutsceneAsset.cs`
- `CasaEngine/Framework/Cutscenes/Serialization/CutsceneAssetJsonSerializer.cs`

Actions :

1. Definir le nom exact de l'asset apres verification des conventions existantes.
2. Ajouter une extension dans `Constants.FileNameExtensions` si un nouveau format d'asset est necessaire.
3. Creer le type d'asset avec version/schema si le format est serialise en JSON comme les cutscenes et particles.
4. Creer le loader correspondant et l'enregistrer dans `AssetLoaderRegistry`.
5. Ajouter des tests de serialization/chargement sur le modele des tests cutscene ou particle.

Validation :

- Tests de chargement asset dialogue.
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Resultat 2026-05-31 : asset `DialogueAsset` ajoute avec format JSON `.dialogue`, `start_node`, `program_base64` et `line_texts`; loader enregistre dans `AssetLoaderRegistry`. `dotnet build CasaEngine.MonoGame.sln --no-restore` OK avec avertissements existants. Tests de serialization/loader ajoutes, mais `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore --filter Dialogue` reste bloque avant execution par des erreurs de compilation existantes hors dialogue (`Pool<>`, `LightComponent.Coordinates`, `DualQuaternion`).

Commit requis : oui, un commit dedie avec asset, loader, tests et statut.

### ✅ Tache 7 - Inspecter l'API Yarn Spinner restauree

Objectif : documenter l'API reelle disponible avant d'ecrire l'adaptateur.

Fichiers a verifier avant modification :

- `Directory.Packages.props`
- Les fichiers assets et runtime crees precedemment.

Actions :

1. Apres restauration NuGet, inspecter les assemblies ou la documentation locale du package.
2. Identifier les types reels pour compiler une source Yarn et executer un programme Yarn.
3. Noter dans ce fichier les noms exacts trouves, ou passer la tache en `⚠️ Blocked` si l'API n'est pas accessible.
4. Ne pas ecrire encore `YarnDialogueRunner` si les types exacts ne sont pas verifies.

Validation :

- Note factuelle ajoutee dans ce plan ou dans une courte documentation liee.
- `dotnet build CasaEngine.MonoGame.sln --no-restore` si aucun code n'est modifie, ou validation de compilation si un fichier de notes n'est pas le seul changement.

Resultat 2026-05-31 : inspection par reflection des assemblies `YarnSpinner.dll` et `YarnSpinner.Compiler.dll` restaurees en version `3.2.1`.

Types/verifications utiles :

- Compilation : `Yarn.Compiler.Compiler.Compile(Yarn.Compiler.CompilationJob)` est statique.
- Creation de job : `Yarn.Compiler.CompilationJob.CreateFromString(string fileName, string source, Yarn.Library library, int languageVersion)`, `CreateFromFiles(...)`, `CreateFromInputs(...)`.
- Resultat : `Yarn.Compiler.CompilationResult.Program`, `StringTable`, `Diagnostics`, `ContainsErrors`, `GetStringForKey(string)`, `GetLabelsForNode(string)`.
- Ligne compilee : `Yarn.Compiler.StringInfo` expose des champs publics `text`, `nodeName`, `lineNumber`, `fileName`, `isImplicitTag`, `metadata`, `shadowLineID`.
- Diagnostics : `Yarn.Compiler.Diagnostic` expose `FileName`, `Range`, `Message`, `Severity`, `Code`; les positions utiles sont dans `Range.Start.Line` et `Range.Start.Character`.
- Runtime : `Yarn.Dialogue` se construit avec `Yarn.IVariableStorage`, puis `SetProgram(Yarn.Program)`, `SetNode(string)`, `Continue()`, `SignalContentComplete()`, `SetSelectedOption(int)`, `Stop()`.
- Handlers runtime : `Yarn.LineHandler.Invoke(Yarn.Line)`, `Yarn.OptionsHandler.Invoke(Yarn.OptionSet)`, `Yarn.CommandHandler.Invoke(Yarn.Command)`, `Yarn.NodeStartHandler.Invoke(string)`, `Yarn.NodeCompleteHandler.Invoke(string)`, `Yarn.DialogueCompleteHandler.Invoke()`.
- Programme : `Yarn.Program` expose `Parser`, `Nodes`, `LanguageVersion`, `LineIDsForNode(string)` et implemente les methodes protobuf (`WriteTo`, `MergeFrom`, `CalculateSize`). La serialization en bytes devra passer par Google.Protobuf.

Validation : note factuelle ajoutee; aucun code modifie dans cette tache.

Commit requis : oui, un commit dedie avec la note d'inspection et le statut.

### ✅ Tache 8 - Compiler un fichier `.yarn` de test hors boucle de jeu

Objectif : valider la compilation Yarn sans UI et sans gameplay.

Fichiers a verifier avant modification :

- Projet choisi pour `YarnSpinner.Compiler`.
- Tests existants dans `CasaEngine.Tests`.
- Toute convention d'asset source dans les projets `Content`.

Actions :

1. Ajouter un fichier `.yarn` de test dans un emplacement justifie par les conventions existantes.
2. Compiler ce fichier dans un test ou un utilitaire hors boucle de jeu.
3. Capturer les erreurs de compilation dans une structure CasaEngine simple.
4. Ne pas lancer la compilation depuis `Update` ou `Draw`.
5. Ne pas ajouter d'import editor automatique tant que la compilation de base n'est pas prouvee.

Validation :

- Test de compilation Yarn minimal.
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Resultat 2026-05-31 : wrapper `YarnDialogueCompiler` ajoute dans `CasaEngine.Compiler` avec diagnostics CasaEngine simples, bytes protobuf du programme compile et table de lignes. Fixture `CasaEngine.Tests/Dialogue/Fixtures/greeting.yarn` ajoutee. `dotnet build CasaEngine.Compiler/CasaEngine.Compiler.csproj --no-restore` OK. Validation comportementale hors boucle de jeu par chargement du compiler en processus enfant OK : `Success=True`, `ProgramBytes=60`, `LineCount=1`, `Diagnostics=0`. `dotnet build CasaEngine.MonoGame.sln --no-restore` OK avec avertissements existants. `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore --filter Dialogue` reste bloque avant execution par des erreurs de compilation existantes hors dialogue (`Pool<>`, `DualQuaternion`, `LightComponent.Coordinates`, `PreviewEnvironmentFactory`).

Commit requis : oui, un commit dedie avec source test, compilation et statut.

### ✅ Tache 9 - Charger un asset dialogue issu de Yarn

Objectif : connecter la compilation Yarn au format d'asset runtime defini a la tache 6.

Fichiers a verifier avant modification :

- Loader dialogue cree a la tache 6.
- Resultat d'inspection API de la tache 7.
- Test de compilation de la tache 8.

Actions :

1. Convertir le resultat compile Yarn dans le format d'asset CasaEngine choisi.
2. Charger l'asset via `AssetContentManager` et son loader.
3. Tester le chargement par `Guid` si un `AssetInfo` exploitable existe dans le test; sinon tester le loader directement et documenter la limite.
4. Ne pas ajouter de gameplay ou UI dans cette tache.

Validation :

- Test asset compile chargeable.
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Resultat 2026-05-31 : `DialogueAsset.FromCompiledProgram(...)` ajoute pour convertir bytes protobuf + table de lignes dans l'asset runtime sans faire dependre `CasaEngine` du projet compiler. Test de round-trip ajoute : compilation de `greeting.yarn`, sauvegarde `.dialogue`, chargement direct par `DialogueAssetLoader`. Validation comportementale en processus enfant OK : `CompileSuccess=True`, `SavedProgramBytes=60`, `LoadedProgramBytes=60`, `LoadedLineCount=1`. `dotnet build CasaEngine/CasaEngine.csproj --no-restore` OK avec avertissements existants. `dotnet build CasaEngine.MonoGame.sln --no-restore` OK avec 1 avertissement existant. `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore --filter Dialogue` reste bloque avant execution par des erreurs de compilation existantes hors dialogue (`Pool<>`, `DualQuaternion`, `PreviewEnvironmentFactory`).

Commit requis : oui, un commit dedie avec chargement et statut.

### ✅ Tache 10 - Implementer le runner Yarn adapte CasaEngine

Objectif : executer un dialogue Yarn minimal et emettre des lignes vers le presenter.

Fichiers a verifier avant modification :

- Contrats runtime/presentation crees precedemment.
- Resultat d'inspection API Yarn.
- Asset dialogue runtime.

Actions :

1. Creer un adaptateur Yarn qui depend des types Yarn verifies.
2. Exposer des methodes de demarrage, continuation, choix et arret seulement si l'API Yarn les supporte sous cette forme.
3. Presenter une ligne simple via `IDialoguePresenter`.
4. Ajouter un test sans MGUI qui verifie qu'une ligne Yarn arrive au presenter fake.
5. Documenter toute difference entre l'API Yarn reelle et le pseudo-code du document initial.

Validation :

- Test runner minimal.
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Resultat 2026-05-31 : `YarnDialogueRunner` ajoute sous `CasaEngine/Framework/Dialogue/Yarn`. Il deserialise `DialogueAsset.ProgramBytes` via `Yarn.Program.Parser`, configure les handlers requis par `Yarn.Dialogue`, route les lignes via `IDialoguePresenter` et ferme le presenter a la fin du dialogue. Tests runner ajoutes avec presenter fake. Validation comportementale en processus enfant OK avec `DialogueService` : `Started=True`, `RunningAfterStart=True`, `OpenAfterStart=True`, ligne `Bonjour depuis CasaEngine.`, puis `Continued=True`, `RunningAfterContinue=False`, `OpenAfterContinue=False`. `dotnet build CasaEngine/CasaEngine.csproj --no-restore` OK avec avertissements existants. `dotnet build CasaEngine.MonoGame.sln --no-restore` OK avec avertissements existants. `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore --filter YarnDialogueRunnerTests` reste bloque avant execution par des erreurs de compilation existantes hors dialogue (`LightComponent.Coordinates`, `DualQuaternion`, `PreviewEnvironmentFactory`).

Commit requis : oui, un commit dedie avec runner, tests et statut.

### ✅ Tache 11 - Brancher la demo UI sur le runner Yarn

Objectif : remplacer le texte code en dur de la demo par une ligne issue d'un fichier Yarn compile.

Fichiers a verifier avant modification :

- Demo de la tache 4.
- Runner de la tache 10.
- Asset chargeable de la tache 9.

Actions :

1. Charger l'asset dialogue dans la demo.
2. Demarrer le noeud verifie dans le fichier `.yarn` de test.
3. Continuer/fermer le dialogue via le controle de test existant.
4. Garder la gestion multi-ligne, choix, variables et commandes hors scope sauf si necessaire pour la ligne minimale.

Validation :

- `dotnet build CasaEngine.MonoGame.sln --no-restore`
- Lancer la demo si possible et noter le resultat dans ce fichier.

Resultat 2026-05-31 : `UIOverlayDemo` utilise maintenant un `YarnDialogueRunner` et charge `Content/Dialogues/greeting.dialogue` via `DialogueAssetLoader`; le texte code en dur a ete retire du flux d'ouverture. La touche de test `D` ouvre la ligne Yarn puis continue/ferme le dialogue, et le bouton de fermeture stoppe le runner. L'asset `.dialogue` compile est copie en sortie par `CasaEngine.Demos.csproj`. Validation directe de l'asset de sortie OK : `AssetExists=True`, `AssetLoaded=True`, `Started=True`, ligne `Bonjour depuis CasaEngine.`, `Continued=True`, `OpenAfterContinue=False`. `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj --no-restore` OK. `dotnet build CasaEngine.MonoGame.sln --no-restore` OK avec avertissements existants. Lancement automatise de `UI Overlay` depuis `CasaEngine.Demos/bin/Debug/net9.0-windows` OK; capture enregistree dans `artifacts/ui-overlay-yarn-task11.png` sous ce dossier de sortie.

Commit requis : oui, un commit dedie avec demo Yarn minimale et statut.

### ⏳ Tache 12 - Ajouter les lignes multiples

Objectif : verifier la progression ligne par ligne avec le meme runner.

Fichiers a verifier avant modification :

- Runner Yarn.
- Presenter/UI dialogue.
- Fichier `.yarn` de test.

Actions :

1. Ajouter plusieurs lignes au fichier `.yarn` de test.
2. Implementer la continuation selon l'API Yarn reelle.
3. Tester que chaque continuation affiche la ligne attendue.
4. Fermer l'ecran uniquement quand le dialogue est termine.

Validation :

- Test runner multi-lignes.
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Commit requis : oui, un commit dedie avec multi-lignes et statut.

### ⏳ Tache 13 - Ajouter les choix simples

Objectif : ajouter le premier comportement interactif non lineaire.

Fichiers a verifier avant modification :

- Contrats de presentation.
- UI dialogue.
- Runner Yarn.

Actions :

1. Ajouter un `.yarn` de test avec deux choix.
2. Ajouter un DTO ou contrat de choix si absent.
3. Afficher les choix dans `DialogueScreen`.
4. Router haut/bas/validation uniquement par les APIs d'input verifiees.
5. Tester que le choix selectionne mene a la branche attendue.

Validation :

- Test runner choix.
- `dotnet build CasaEngine.MonoGame.sln --no-restore`
- Verification manuelle de la demo si possible.

Commit requis : oui, un commit dedie avec choix et statut.

### ⏳ Tache 14 - Ajouter le store de variables Yarn

Objectif : connecter les variables Yarn a un stockage CasaEngine testable.

Fichiers a verifier avant modification :

- API Yarn verifiee pour les variables.
- Systeme de sauvegarde existant si une integration sauvegarde est envisagee.

Actions :

1. Implementer le store selon l'interface attendue par Yarn Spinner, apres verification.
2. Limiter les types supportes aux types prouves par les tests.
3. Ajouter un test qui definit une variable dans un dialogue et la relit dans un dialogue suivant.
4. Ne pas integrer la sauvegarde persistante dans cette tache, sauf si le systeme de sauvegarde est deja identifie et teste.

Validation :

- Test variables Yarn.
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Commit requis : oui, un commit dedie avec variables et statut.

### ⏳ Tache 15 - Ajouter les commandes Yarn dispatchables

Objectif : preparer les commandes sans les lier a des services inexistants.

Fichiers a verifier avant modification :

- API Yarn verifiee pour les commandes.
- Services CasaEngine reels disponibles pour les commandes ciblees.

Actions :

1. Creer un dispatcher de commandes avec handlers enregistres explicitement.
2. Ajouter un handler de test sans effet gameplay pour valider le dispatch.
3. Pour chaque commande gameplay reelle, verifier le service cible avant d'ajouter le handler.
4. Logguer ou retourner une erreur pour les commandes inconnues selon les conventions de logging existantes.

Validation :

- Test commande connue et commande inconnue.
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Commit requis : oui, un commit dedie avec dispatcher et statut.

### ⏳ Tache 16 - Integrer dialogue et cutscenes

Objectif : permettre a une cutscene de lancer un dialogue apres que le dialogue runtime soit stable.

Fichiers a verifier avant modification :

- `CasaEngine/Framework/Cutscenes/CutsceneActionTypes.cs`
- `CasaEngine/Framework/Cutscenes/CutsceneActionData.cs`
- `CasaEngine/Framework/Cutscenes/Serialization/CutsceneAssetJsonSerializer.cs`
- `CasaEngine/Framework/Cutscenes/CutsceneActionCoroutineFactory.cs`
- `CasaEngine/Framework/Cutscenes/CutsceneDirector.cs`
- Tests cutscenes existants dans `CasaEngine.Tests/Cutscenes`.

Actions :

1. Ajouter un type d'action `StartDialogue` seulement apres avoir identifie comment le `World` expose le service dialogue.
2. Mettre a jour la serialization et la deserialization.
3. Mettre a jour la factory coroutine pour attendre la fin si l'option est active.
4. Ajouter des tests serialization et execution coroutine.
5. Ne pas ajouter de commandes Yarn de cutscene dans la meme tache.

Validation :

- Tests cutscene ciblant `StartDialogue`.
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Commit requis : oui, un commit dedie avec cutscene et statut.

### ⏳ Tache 17 - Ajouter validation editor/import minimale

Objectif : exposer les erreurs Yarn avant runtime dans les outils existants.

Fichiers a verifier avant modification :

- `CasaEngine.EditorServices/EditorAssetImportService.cs`
- Services editor lies au catalogue d'assets.
- Tests `CasaEngine.Tests/EditorServices`.

Actions :

1. Identifier le point d'import reel des assets source.
2. Ajouter la reconnaissance `.yarn` seulement si ce flux gere bien les sources editables.
3. Retourner erreurs et warnings de compilation dans un format consultable par l'editeur.
4. Ajouter un test sur un fichier Yarn invalide.
5. Ne pas ajouter de preview editor dans cette tache.

Validation :

- Test import/validation Yarn invalide.
- `dotnet build CasaEngine.Editor.MonoGame.sln --no-restore`

Commit requis : oui, un commit dedie avec validation editor et statut.

### ⏳ Tache 18 - Documenter l'etat reel apres implementation

Objectif : remplacer la roadmap theorique par un etat maintenable.

Fichiers a verifier avant modification :

- `docs/engine/yarn_spinner_integration.md`
- Ce plan agent.
- Tout nouveau README ou doc cree pendant les taches precedentes.

Actions :

1. Corriger les coches trompeuses du document initial si elles ne refletent pas l'etat du depot.
2. Lister les fonctionnalites implementees, les limites et les validations executees.
3. Ajouter un exemple minimal d'utilisation base sur le code reel.
4. Conserver les taches non faites avec statut explicite, sans les presenter comme livrees.

Validation :

- Relecture de documentation.
- `dotnet build CasaEngine.MonoGame.sln --no-restore` si aucune modification code n'est faite, pour verifier que le repo reste sain.

Commit requis : oui, un commit dedie avec documentation et statut.

## Taches explicitement hors scope tant que les bases ne sont pas livrees

- Localisation Yarn avancee.
- Voice-over.
- Portraits et animations de texte.
- Graphe visuel de dialogue.
- Integration quetes.
- Edition visuelle de dialogues.
- Commandes gameplay sans service CasaEngine verifie.
- Sauvegarde persistante complete des variables Yarn avant identification du systeme de sauvegarde existant.

## Commandes de validation de reference

Utiliser `rtk` si disponible pour reduire le bruit de sortie.

```powershell
rtk git status
dotnet restore CasaEngine.MonoGame.sln
dotnet build CasaEngine.MonoGame.sln --no-restore
dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore
dotnet build CasaEngine.Editor.MonoGame.sln --no-restore
```

## Format de commit recommande

Chaque commit doit etre atomique et citer la tache.

```text
Dialogue: task 01 add Yarn package references
Dialogue: task 02 add runtime dialogue service
Dialogue: task 03 add modal dialogue screen
```

Avant chaque commit :

1. Verifier `git status`.
2. Verifier que le statut de la tache dans ce fichier est a jour.
3. Verifier que les fichiers modifies correspondent uniquement a la tache.
4. Inclure le resultat de validation dans le message de commit si la convention locale le permet.