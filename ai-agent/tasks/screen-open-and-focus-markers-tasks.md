# Plan agent IA — Commande d'ouverture et marqueurs de focus

Plan d'exécution d'un petit chantier sur `DataDrivenScreen`. Les décisions ci-dessous ont été déléguées par l'auteur le 2026-11-01 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Un projet sans DLL peut :

- lancer une liste d'actions à l'ouverture d'un écran, avant sa première image, donc un fondu d'entrée ;
- afficher un élément, un curseur par exemple, seulement quand un autre élément tient le focus.

Hors périmètre : un curseur qui glisse d'une ligne à l'autre, une animation de focus, tout changement de MGUI.

## État vérifié du dépôt (2026-11-01)

- `git log -1` : la branche part de `chantier/screen-fade-actions` `c5ffe054`.
- ADR-0081 liste comme limite « No fade runs when a screen opens » : rien ne lance une commande à l'ouverture, et le premier écran d'un projet dessine une image avant son premier `Update`. Corrigé en T5.7 : la version d'origine disait « un écran », or un écran ouvert par `open_screen` reçoit le sien sur l'image de son ouverture.
- `DataDrivenScreen.OnWindowLoaded` tourne avant l'entrée de l'écran sur la pile (`DataDrivenScreen.cs:116`).
- Un élément MGUI n'a pas de propriété qui dise qu'il tient le focus : `HasKeyboardFocus` est une méthode d'interface explicite (`MGElement.cs:2918`). Le bureau signale pourtant chaque changement par `MGDesktop.FocusedKeyboardHandlerChanged` (`MGDesktop.cs:1261`) : corrigé en T4.2, la version d'origine de cette ligne l'ignorait.
- `MGDesktop.FocusedKeyboardHandler` se lit sans allocation, et `DataDrivenScreen.WatchFocusMove` compare déjà sa valeur d'une image à l'autre.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Champ `on_open_command` du `.uiscreen`, qui nomme une entrée de `ui_commands`. Elle démarre à la fin de `OnWindowLoaded`, pas dans `Update`, pour que la première image dessinée montre déjà le fondu. |
| D2 | Cette commande n'accepte que `play_sound`, `fade_screen` et `wait_fade`. Au chargement l'écran n'est pas sur la pile : changer la pile ou le monde depuis cet endroit n'a pas de sens défini. Le reste est refusé au chargement. |
| D3 | Champ `focus_markers` : liste d'objets `{ "element", "marker" }`, noms d'éléments XAML. Le marqueur est visible seulement quand l'élément tient le focus. Le lien est explicite, parce qu'un marqueur peut être un frère de l'élément sur un `Canvas`. |
| D4 | Un marqueur caché garde sa place (`Visibility.Hidden`) : le layout ne bouge pas. |
| D5 | La visibilité change seulement quand le focus change, dans `Update`, sans abonnement et sans allocation. Un écran gelé sous un modal garde son marqueur. |
| D6 | Une erreur de données (nom inconnu, élément qui ne peut pas prendre le focus) est refusée au chargement avec le chemin de l'enveloppe, comme les autres champs. |

## Règles d'exécution pour l'agent

- **Branche `chantier/screen-open-and-focus-markers`**, empilée sur `chantier/screen-fade-actions`. Jamais de commit sur `main`, jamais de push.
- **Un commit par tâche**, plan mis à jour dans le même commit, `git add` fichier par fichier.
- **Langue** : plan en français ; code, commits, `docs/` et ADR en anglais.
- Lancer `dotnet test` sans `--no-build` : les `.sln` ne reconstruisent pas `CasaEngine.Tests`.

## Légende des statuts

⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout passe.
- Launcher sur `Projects/PixelUiSample` : fondu d'entrée, curseur visible seulement sur son bouton.

---

## Phase 1 — Format et écran

### ✅ T1.1 — ADR et plan

- Fichiers : ce plan, `ai-agent/README.md`, `docs/decisions/0082-screens-run-a-command-on-open-and-mark-the-focus.md`, `docs/decisions/README.md`.
- Commit : `docs(decisions): record the open command and the focus markers (ADR-0082)`.

### ✅ T1.2 — `on_open_command`

- Fichiers : `UIScreenAsset.cs`, `EditorAssetJsonSerializer.cs`, `DataDrivenScreen.cs`, `UIScreenAssetSerializationTests.cs`, `DataDrivenScreenTests.cs`.
- Validation : tests du format et de l'écran, dont une attente qui coupe les commandes dès l'ouverture.
- Commit : `feat(ui): run a command when a data-driven screen opens`.

### ✅ T1.3 — `focus_markers`

- Fichiers : mêmes fichiers.
- Validation : marqueur caché au chargement, visible avec le focus, refus des noms inconnus.
- Commit : `feat(ui): show a marker while an element holds the focus`.

## Phase 2 — Preuve

### ✅ T2.1 — Sample et documentation

- Fichiers : `Projects/PixelUiSample`, `docs/engine/data-driven-screens.md`, ADR-0081 (limite levée), ce plan, `ai-agent/README.md`.
- Validation : Launcher, captures lues.
- Commit : `docs(ui): show the open command and the focus markers in the pixel sample`.

## Phase 3 — Temps de démarrage d'un monde

### ✅ T3.1 — Le démarrage d'un monde ne mange plus le fondu d'ouverture

- Défaut mesuré au Launcher sur un projet à pas fixe : le fondu d'ouverture de 17 pas n'était jamais vu. 30 Updates avant la première image, qui dessinait déjà 0.
- Un seul `ResetElapsedTime` à la fin du bloc `_isNewWorld` ne suffit pas (mesuré : 210 puis 0). Quatre retards se suivent : le chargement, le premier `Update` des vues, le premier `Draw`, le premier pas de physique. La cause de ce dernier coût n'est pas mesurée.
- Décision : le temps d'un monde commence à sa première image. `LoadTimeDiscard` jette le temps sur deux images, en runtime seulement ; l'éditeur fait tourner la boucle et ne lance jamais son jeu hébergé, donc rien n'y change.
- Fichiers : `LoadTimeDiscard.cs`, `GameManager.cs`, `CasaEngineGame.cs`, `LoadTimeDiscardTests.cs`, `LoadTimeDiscardGameLoopTests.cs`, `GpuDeviceHost.cs`, ADR-0082, `docs/engine/data-driven-screens.md`, `docs/engine/screen-effects.md`, ce plan, `ai-agent/README.md`.
- Validation : séquences dessinées dans ADR-0082, trace temporaire retirée (vérifié par `git diff`), cinq mutations qui font toutes échouer un test. Le branchement dans `CasaEngineGame` et `UpdateWorld` n'a pas de test : aucun test ne construit ce jeu, le Launcher le prouve.
- Commit : `fix(runtime): keep the start of a world off the game clock`.

## Phase 4 — Revue d'ADR-0082

### ✅ T4.1 — Fermer les constats de la revue d'ADR-0082

- Constat 1, vrai : un marqueur égal à son élément, ou ancêtre de celui-ci, était accepté. Caché, il retire l'entrée clavier à tout ce qu'il contient (`MGElement.Update`), donc le focus qui le montrerait n'arrive jamais. Mesuré avant le correctif : 120 images et trois touches, élément sans focus, marqueur caché, aucune erreur. Il est refusé au chargement avec le chemin de l'enveloppe. Une fenêtre nommée n'était pas en cause : son propre nom n'est pas indexé, le chargement la refusait déjà (mesuré).
- Constat 2, vrai : « une image ou deux après le chargement » était faux sans `default_focus`. Le mode d'entrée de MGUI démarre en Pointer et aucun focus n'arrive avant la première touche, le premier appui manette ou un clic. Avec `default_focus`, le focus arrive au troisième `Update`. Mêmes résultats sur bureau sans affichage et au Launcher : 930 `Update` sans focus sur une copie du sample sans `default_focus`.
- Constat 3, vrai : le contexte d'ADR-0082 ignorait `MGDesktop.FocusedKeyboardHandlerChanged`. La vraie raison du polling : cet événement touche aussi un écran gelé sous un modal, qui doit garder son marqueur. L'ancienne phrase « aucun lien de données vers le focus » a aussi été retirée : `VisualState` est public et notifié, seulement il ne dit Focused qu'hors du mode Pointer. Un binding dessus n'a pas été essayé.
- Constat 4, vrai : tests ajoutés pour un marqueur gelé sous un modal et pour une action qui lève pendant `on_open_command`. Ce dernier passe par le vrai `GameManager.PushNewScreen`, avec une vue de remplacement : aucun test ne construit un `UIRoot`.
- Fichiers : `DataDrivenScreen.cs`, `DataDrivenScreenTests.cs`, `FirstScreenTests.cs`, ADR-0082, `docs/engine/data-driven-screens.md`, ce plan, `ai-agent/README.md`.
- Validation : 13 mutations, toutes tuées par le test visé, dont 2 dans MGUI, restaurées. La première version du test « aucun focus avant la première touche » survivait à celle qui fait démarrer MGUI en Navigation : son premier pas déplaçait la souris, ce qui rend le mode Pointer de toute façon. Le test garde maintenant la souris immobile. Trace temporaire retirée (vérifié par `git diff`).
- Commit : `fix(ui): refuse a focus marker that hides its element and correct ADR-0082`.

## Phase 5 — Revues d'E2, E3 et E4

Constats restants des revues indépendantes des chantiers E2, E3 et E4, fermés ici en commits neufs, sans réécrire l'historique des branches du dessous. Les lettres sont celles de la demande.

### ✅ T5.1 — Navigation : les touches d'une zone de texte et les doublons de mapping (a, b)

- Constat a, vrai : `TryDispatchNavigationAction` rend faux quand `ShouldPreserveTextEntryKey(key)` est vrai, et `Fire` prenait tout faux sur Cancel pour un Cancel non consommé. Avec Espace lié à Cancel et une zone de texte éditable focalisée, taper un espace lançait `cancel_command`. Espace et Entrée sont dans `InputActionMapper`, donc `IsTextForAFocusedTextBox` ne les arrêtait pas. `Fire` demande maintenant à la zone de texte avant de signaler le Cancel. Seul ce signalement change : `TryHandleInputAction` est toujours appelé, le mode d'entrée de MGUI ne bouge pas.
- Constat b, vrai : le contrôle de doublon lisait les slots construits, et une entrée sans entrée physique n'en construit aucun. `Submit` vide puis `Submit` sur Z passait, alors qu'`InputMappingManager` lit la première entrée. Les actions déclarées ont maintenant leur propre ensemble.
- Fichiers : `UINavigationInput.cs`, `UINavigationInputTests.cs`, ADR-0080, `docs/engine/data-driven-screens.md`, ce plan.
- Validation : les 4 tests neufs échouent sans le correctif et passent avec. Le mapping du sample, celui de `RPGDemo` et celui du projet externe n'ont aucun doublon (lu avec `jq`).
- Commit : `fix(ui): keep a text box's keys out of Cancel and refuse an empty duplicate action`.

### ✅ T5.2 — `move_command` : le retour du focus et l'écran gelé (c, d)

- Constat c, vrai : le test « le focus arrive d'un autre écran » est arrêté par le garde du focus par défaut, pas par `previous?.SelfOrParentWindow == Window`. Test ajouté : le focus revient à un écran non modal quand le popup au-dessus se ferme, sans focus par défaut à l'œuvre.
- Constat d, vrai, mesuré avant le correctif : sous un modal sans élément focalisable, la navigation de MGUI déplace le focus dans l'écran gelé, et son premier `Update` après la fermeture jouait `move_command` sur une image où rien ne bougeait. `ScreenStack.Update` appelle maintenant `FollowFocusWhileFrozen` sur les écrans gelés, sans allocation. Ce suivi consomme aussi `_isDefaultFocusArriving` : un focus par défaut arrivé pendant le gel avalait sinon le premier vrai déplacement, mesuré aussi.
- Fichiers : `DataDrivenScreen.cs`, `ScreenStack.cs`, `DataDrivenScreenTests.cs`, ADR-0080, `docs/engine/data-driven-screens.md`, ce plan.
- Validation : 4 mutations, toutes tuées, dont `previous != null` à la place de la clause de fenêtre, tuée par le test du popup seul.
- Commit : `fix(ui): keep a frozen screen's view of the focus current`.

### ✅ T5.3 — Les tests suivent l'ordre réel de `UIRoot` (l, m)

- Constat m, vrai : `RootStep` recopiait la ligne de `UIRoot` qui pose la suspension, donc la déplacer ne faisait rien échouer. Le corps de `UIRoot.Update` passe dans `UIRoot.RunFrame`, interne et statique, qui prend le bureau, la navigation et la pile : `RootStep` l'appelle. Aucune API publique ne change. Le test du chemin avec mapping, qui recopiait lui aussi `navigation.IsSuspended`, passe par là.
- Constat l, vrai : `AWait_LeavesTheValueItFound` ne pouvait plus échouer, plus rien n'écrivant ce drapeau pendant une attente. Remplacé par `AWait_LeavesTheChoiceOfTheInputPathAlone`, sur les deux valeurs de `UseRawNavigationInput`, pendant l'attente et après, à travers le vrai `RunFrame`.
- Ajouté : `UnderAMapping_ACommandRuns_OnTheFrameOfItsInput`. Rien ne fixait la place de la navigation avant la pile, écrite dans ADR-0080.
- Fichiers : `UIRoot.cs`, `DataDrivenScreenTests.cs`, ce plan.
- Validation : 5 mutations, toutes tuées : ligne de suspension retirée (8 tests), placée avant la pile (3 tests), ligne de suspension du mapping retirée, navigation déplacée après la pile, écriture de `UseRawNavigationInput` pendant une attente. Une première version de la mutation d'ordre laissait le bloc d'origine en place et ne mutait donc rien : refaite. `UIRoot.Update` lui-même garde son seul garde `_disposed` et n'a pas de test : aucun test ne construit un `UIRoot`.
- Commit : `test(ui): run the frame order of UIRoot in the screen tests`.

### ✅ T5.4 — Un nom de police qui tombe sur un autre asset (f)

- Constat f, vrai : `AssetCatalog` indexe les noms par écrasement (`_assetInfosByName[assetInfo.Name] = assetInfo`), la dernière entrée lue gagne. Un PNG de page au nom de son `.fnt` fait résoudre le nom vers le PNG, et le message disait qu'aucune police n'est enregistrée. Le message nomme maintenant le fichier et le type trouvés, et la cause. Un nom inconnu garde le message d'avant, au mot près. `docs/engine/data-driven-screens.md` le dit aussi.
- Fichiers : `GameManager.cs`, `ScreenAssetLookupTests.cs`, `docs/engine/data-driven-screens.md`, ce plan.
- Validation : le test neuf échoue sans le correctif et passe avec.
- Commit : `fix(ui): say which asset a font name landed on`.

### ✅ T5.5 — Le contexte d'un écran se construit sans jeu (g)

- Constat g, vrai : `CreateScreenActionContext` lisait `_game`, et `Quit = _game.Exit` levait dès la construction avec un jeu nul. Les lignes de `DesignResolution`, `AcquireFont`, `StartFade` et `IsFadeRunning` n'avaient aucun test. Le type interne `ScreenGameServices` porte ce que le contexte prend au jeu, et `CreateScreenActionContext` devient interne : aucune API publique ne change. Le service de fondu reste lu à l'usage, pas à la construction.
- Reste hors de portée d'un test : `ReadScreenGameServices`, les cinq lectures de `_game`. Aucun test ne construit ce jeu. Le Launcher les prouve sur `Projects/PixelUiSample`, voir T5.6.
- Fichiers : `ScreenGameServices.cs`, `GameManager.cs`, `ScreenActionContextWiringTests.cs`, `FirstScreenTests.cs` (bouchon de vue devenu `internal`), `ScreenFadeWiringTests.cs` (commentaire), ce plan.
- Validation : 6 mutations, toutes tuées, une par ligne câblée.
- Commit : `test(ui): build the context of a screen without a game`.

### ✅ T5.6 — Une image dessinée sur des bornes périmées quand k change (h)

- Constat h, vrai : le setter `Left` ne déplace rien, seul `ValidateWindowSizeAndPosition` lève `OnWindowPositionChanged`, qui translate les éléments. `UIRoot` notifie les bornes après `Desktop.Update`, donc l'image suivante dessinait les éléments à leur ancienne place sous la nouvelle transformation. Mesuré sur un bouton, de k = 3 à k = 4 : (38, 114) au lieu de (43, 121). `ApplyDesignScale` appelle maintenant `ValidateWindowSizeAndPosition` après `Window.Scale`.
- Fichiers : `DataDrivenScreen.cs`, `DataDrivenScreenTests.cs`, ce plan.
- Validation : le test neuf échoue sans le correctif, et vérifie aussi que l'`Update` suivant ne translate pas une seconde fois.
- Launcher, redimensionnement à chaud du même processus sur `Projects/PixelUiSample` : 960x540, 1280x720, 640x360, 1000x600, puis 960x540. À marge de 12 px des bords, chaque capture compte 5 couleurs et tous les changements tombent en phase avec la grille de `k` : phase 0 à k = 3, 4 et 2, phase 2 en x et 0 en y à 1000x600, dont l'image part de (20, 30). La capture de retour à 960x540 est identique à l'octet à la première. Sans cette marge, 36 pixels de 11 couleurs rares restent hors phase. Ils sont tous à moins de 9 px d'un coin, en même nombre à chaque taille. Leur cause n'est pas prouvée.
- Le même run et un second prouvent les cinq lectures de `_game` de T5.5 : l'échelle, la police bitmap, le fondu `FADE OUT` jusqu'à `BlackWorld` (capture finale entièrement noire), le popup d'options, qui a un `cancel_command` et n'aurait pas chargé sans mapping, fermé par X avec le focus rendu à `OPTIONS`, et `QUIT` avec le code 0. Aucune exception dans les logs. L'image transitoire elle-même n'a pas été observée au Launcher : seul le test la mesure.
- Commit : `fix(ui): move a scaled window's elements on the frame k changes`.

### ✅ T5.7 — Documents exacts (e, i, j, k)

- Constat e, vrai : `CasaEngineGame` lit le fichier à l'initialisation, mais le contenu est contrôlé dans le constructeur de `UINavigationInput`, que `UIRoot` appelle quand sa vue est ajoutée, donc pendant le `UpdateWorld` du premier monde (`BootstrapViews`). ADR-0080 et `docs/engine/data-driven-screens.md` distinguent maintenant les deux moments.
- Constat i, vrai sur le fond, inexact sur le lieu : la phrase n'est ni dans ADR-0081 ni dans `data-driven-screens.md`. Elle est dans ADR-0082, dans le commentaire de `StartOnOpenCommand` et en ligne 19 de ce plan, corrigés tous les trois. La liste d'`open_screen` tourne dans la boucle de `ScreenStack.Update`, dont la borne relit `_screens.Count` : le nouvel écran reçoit son `Update` sur la même image. `data-driven-screens.md` le dit désormais pour `on_open_command`. Lu dans le code, pas mesuré. La seconde partie est vraie : `CheckWaits` lit le type de l'action, donc un `open_screen` non modal est refusé lui aussi. Corrigé dans `data-driven-screens.md`, ADR-0081 et le commentaire de `CheckWaits`.
- Constat j, vrai : `StartFade` et `IsFadeRunning` sont `required` sur un type public. Écrit dans les conséquences d'ADR-0081.
- Constat k, vrai au commit que la section décrit : `fc4359b1` a 150 lignes et `Update` y est en 137-149. La citation `154-165` n'est juste qu'à partir de `c5ffe054`, à une ligne près. Corrigée, avec le commit nommé.
- Fichiers : ADR-0080, ADR-0081, ADR-0082, `docs/engine/data-driven-screens.md`, `DataDrivenScreen.cs` (commentaires seulement), `screen-fade-actions-tasks.md`, ce plan, `ai-agent/README.md`.
- Validation : `docs/engine/data-driven-screens.md` relu contre le code sur ces points. Validation globale ci-dessous.
- Commit : `docs(ui): make the load, open and wait statements exact`.

## Points ouverts

Vus pendant T4.1, hors de la demande, non corrigés :

- Deux paires de `focus_markers` qui nomment le même marqueur : seule la dernière le pilote, parce que `ShowFocusMarkers` écrit la visibilité paire par paire. Mesuré : focus sur le premier élément, marqueur caché.
- Un marqueur qui contient l'élément d'une autre paire est accepté. Mesuré : avec le second bouton dans le panneau marqueur du premier, trois appuis sur Bas laissent le focus sur le premier bouton. La cause n'a pas été analysée.

## Résultat

- Les deux `.sln` : 0 erreur. `CasaEngine.Tests` : 4688/4688, reconstruit.
- Launcher sur `Projects/PixelUiSample` : le curseur n'apparaît que sur `FADE OUT`, et disparaît quand le focus passe à `QUIT`. Aucune exception dans le log.
- Limite ajoutée à ADR-0081 après vérification de `chantier/screen-fade-actions` : sans mapping, un bouton tenu à travers `change_world` compte comme un nouvel appui sur le monde suivant. Il faut un écran poussé par du code pour l'atteindre. Le comportement n'est pas corrigé.
- T3.1 : les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4700/4700, reconstruit. Launcher sur un projet à pas fixe : 255, 240, puis de 15 en 15 jusqu'à 0 à l'image 18, une seule image à 2 Updates (la première). Fondu sortant : 15 à 255 sur 17 images, 255 dessiné avant le changement de monde.
- T4.1 : les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4711/4711, reconstruit, `MGUI.Tests` 3225/3225. Launcher sur le sample : focus au troisième `Update`, curseur sur `FADE OUT` puis disparu sur `QUIT`, aucune exception. Sur une copie sans `default_focus` : 930 `Update` sans focus ni marqueur, puis marqueur à la première touche. Sur une copie où le marqueur est son élément : le jeu s'arrête au chargement sur le message qui nomme l'enveloppe. Sur un projet externe qui utilise `focus_markers` : chargement sans erreur.
- Phase 5 : les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4736/4736, reconstruit, `MGUI.Tests` 3225/3225, MGUI inchangé à `62498f75`. 15 mutations, toutes tuées et restaurées (T5.2, T5.3, T5.5). Pour a, b, f et h, les tests neufs ont été vus en échec avant le correctif. Launcher sur le sample : voir T5.6. Sur un projet externe, avec mapping et pas fixe : chargement, deux touches et deux redimensionnements, aucune exception. Aucun nom interdit dans le diff ni dans les messages de commit depuis `d9cff5e6`.
