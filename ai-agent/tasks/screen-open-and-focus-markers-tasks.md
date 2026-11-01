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
- ADR-0081 liste comme limite « No fade runs when a screen opens » : rien ne lance une commande à l'ouverture. Corrigé en T7.4 : cette ligne disait aussi que le premier écran d'un projet dessine une image avant son premier `Update`, ce qui est faux. Son premier `Update` précède son premier `Draw`.
- `DataDrivenScreen.OnWindowLoaded` tourne avant l'entrée de l'écran sur la pile (`DataDrivenScreen.cs:116`).
- Un élément MGUI n'a pas de propriété qui dise qu'il tient le focus : `HasKeyboardFocus` est une méthode d'interface explicite (`MGElement.cs:2918`). Le bureau signale pourtant chaque changement par `MGDesktop.FocusedKeyboardHandlerChanged` (`MGDesktop.cs:1261`) : corrigé en T4.2, la version d'origine de cette ligne l'ignorait.
- `MGDesktop.FocusedKeyboardHandler` se lit sans allocation, et `DataDrivenScreen.WatchFocusMove` compare déjà sa valeur d'une image à l'autre.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Champ `on_open_command` du `.uiscreen`, qui nomme une entrée de `ui_commands`. Elle démarre à la fin de `OnWindowLoaded`, pas dans `Update`, pour que la première image dessinée montre déjà le fondu. Motif corrigé en T7.6 : un démarrage dans `Update` montrerait lui aussi le fondu sur la première image qui dessine la fenêtre. La décision reste, le démarrage au chargement donne l'overlay sur l'image de l'ouverture. |
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

- Défaut mesuré au Launcher sur un projet à pas fixe : le fondu d'ouverture de 51 pas ne se voyait que sur une image. 30 Updates avant la première image, qui dessinait 110, 26 avant la seconde, qui dessinait 0. Valeurs de T7.3.
- Un seul `ResetElapsedTime` à la fin du bloc `_isNewWorld` ne suffit pas (mesuré en T7.3 : 245, 115, 110, puis de 5 en 5, 26 Updates avant la seconde image). Quatre retards se suivent : le chargement, le premier `Update` des vues, le premier `Draw`, le premier pas de physique. La cause de ce dernier coût n'est pas mesurée.
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
- Validation : 5 méthodes de test neuves, 6 cas. 4 cas échouent sans le correctif et passent avec. Les deux `ASpaceBoundToCancel_Cancels_*` passent aussi sans lui : ils gardent contre un correctif trop large, et échouent tous les deux quand chaque Cancel non consommé est gardé (compté en T7.4, la version d'origine de cette ligne parlait de 4 tests neufs). Le mapping du sample, celui de `RPGDemo` et celui du projet externe n'ont aucun doublon (lu avec `jq`).
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

## Phase 6 — Constats sur MGUI

Deux constats de revue sur la branche MGUI `chantier/navigation-input-suspension`. L'objectif de ce plan écarte tout changement de MGUI : cette phase n'y change aucune ligne de production, seulement un test et un mot d'ADR. T6.1 et T6.2 se committent dans MGUI, qui ne contient pas ce plan : elles sont enregistrées ici dans le commit de T6.3.

### ✅ T6.1 — Un bouton tenu se répète encore quand `false` est écrit à chaque image

- Constat, vrai, mesuré : sans `_IsNavigationInputSuspended &&` dans le setter d'`IsNavigationInputSuspended` (`MGDesktop.cs:727`), `MGUI.Tests` restait à 3225/3225 et `CasaEngine.Tests` à 4736/4736. Or `UIRoot.RunFrame` écrit cette propriété à chaque image.
- Test ajouté : `FalseWrittenEveryFrame_LeavesAHeldButtonRepeating`. Il compte les validations d'un bouton tenu 40 images sans écriture, puis exige le même nombre avec `false` écrit entre deux `Update`, là où `UIRoot.RunFrame` l'écrit.
- Fichiers (MGUI) : `MGUI.Tests/Focus/NavigationInputSuspensionTests.cs`.
- Validation : avec la mutation, ce test échoue seul sur 3226, 4 validations attendues, 1 obtenue : l'appui reste, toutes les répétitions sont perdues. Code restauré, vérifié par `git diff` : 3226/3226. Reconstruit à chaque passage. Les états de manette sont injectés : aucune vraie manette essayée, comme le dit ADR-0025 de MGUI. `CasaEngine.Tests` ne voit toujours pas cette mutation : le test qui la tient est dans MGUI.
- Commit (MGUI) : `64a458b1`, `test(input): keep a held button repeating when false is written every frame`.

### ✅ T6.2 — ADR-0025 de MGUI nomme le bon événement

- Constat, vrai : `KeyboardHandler` n'a pas d'événement `Repeated`, le sien est `KeyRepeat` (`KeyboardHandler.cs:109`). Dans MGUI, `Repeated` n'est qu'une valeur d'`InputActionPhase` et un événement de `UIAnimation`. Le reste de la phrase est vrai : le chemin brut s'abonne à `Pressed` (`MGDesktop.cs:1458`).
- Fichiers (MGUI) : `Docs/decisions/0025-pause-the-raw-navigation-path.md`.
- Validation : code lu, aucune autre citation de cet événement sous ce nom dans `Docs/`.
- Commit (MGUI) : `83bda51f`, `docs(decisions): name the KeyRepeat event in ADR-0025`.

### ✅ T6.3 — Pointeur MGUI

- Le pointeur `MGUI` passe de `62498f75` à `83bda51f`, toujours sur `chantier/navigation-input-suspension`, à fusionner dans `develop`.
- Fichiers : `MGUI` (pointeur), ce plan, `ai-agent/README.md`.
- Validation : voir le résultat de la phase 6 ci-dessous.
- Commit : `chore(mgui): bump MGUI to the held button repeat test`.

## Phase 7 — Constats restants de la dernière revue

Un commit par tâche, en commits neufs. Les commits du dessous ne sont pas réécrits. T7.4 touche un commentaire de MGUI et son ADR, à la demande, malgré l'objectif de ce plan : aucune ligne de production n'y change.

### ✅ T7.1 — `focus_markers` : deux formes refusées au chargement

- Constat, vrai : le refus ne regardait que sa propre paire. `CheckFocusMarker` compare maintenant toutes les paires, une fois résolues : un marqueur nommé par deux paires est refusé, et un marqueur qui est ou contient l'élément de n'importe quelle paire aussi, avec le chemin de l'enveloppe. Le message dit désormais « hiding the marker would hide that element too », vrai dans tous les cas, au lieu de « keep the focus from that element for good ». Un élément avec deux marqueurs reste légal.
- Mesuré sur bureau sans affichage, refus retiré, trace temporaire : deux paires sur `lblFirst`, focus sur le premier élément, marqueur caché, puis visible au second. Cause du second point, que T4.1 laissait ouverte : le focus atteint le second bouton sur l'image de la touche, `ShowFocusMarkers` cache le panneau, l'image suivante vide le focus (`SanitizeKeyboardFocusState`), celle d'après le rend au premier bouton.
- Constat sur `CanHandleInputsWhileHidden`, vrai : le motif d'ADR-0082 ne valait que pour un marqueur égal à son élément. Même mesure : un marqueur égal à son élément avec ce drapeau n'est jamais atteint par la navigation, et `default_focus` le lui donne une fois, perdu au premier déplacement. Un ancêtre avec ce drapeau fonctionne : le focus entre, sort et revient, et le marqueur suit. Il reste refusé, comme règle simple, et ADR-0082 le dit : tant qu'il est caché, l'élément n'est pas dessiné (`MGElement.Draw`, lu dans le code).
- Fichiers : `DataDrivenScreen.cs`, `DataDrivenScreenTests.cs`, ADR-0082, `docs/engine/data-driven-screens.md`, ce plan, `ai-agent/README.md`.
- Validation : 7 mutations, toutes tuées puis restaurées, dont le contrôle limité à sa propre paire (4 cas), aux paires suivantes (2), aux paires précédentes (2), et l'égalité à la place de l'ancêtre (3). Le seul projet du dépôt qui utilise `focus_markers` est `Projects/PixelUiSample`, avec une paire : il charge (test `ThePixelSampleScreens_PassTheChecksOfADataDrivenScreen`, et Launcher). Trace temporaire retirée, vérifié par `git diff`.
- Commit : `fix(ui): refuse a focus marker named twice or covering another pair's element`.

### ✅ T7.2 — Le popup d'options de `Projects/PixelUiSample`

- Constat, vrai, vu sur capture au Launcher : le texte du popup chevauchait `QUIT`. À 960x540, ses pixels jaunes tenaient les lignes 375 à 395, et le bouton `QUIT` tient les lignes 360 à 407, depuis que `bfc4319a` a ajouté `FADE OUT` à la colonne.
- Correctif : `Screens/options.xaml`, `Top="116" Height="60"` devient `Top="132" Height="48"`. Le contenu fait 48 pixels de design, marges comprises : la fenêtre finit à 180, le bas de la résolution de design. Aucun autre fichier du sample ne change.
- Validation : capture du popup ouvert, lue : le texte tient les lignes 423 à 443, les boutons 468 à 515. Mêmes touches au Launcher avant et après le correctif, mêmes résultats : Haut et Bas ne bougent pas le focus, ni avant ni après ; Droite va à `TWO`, `BACK`, puis revient à `ONE` ; Gauche revient à `BACK` ; Z sur `BACK` et X ferment le popup. Les captures après Z et après X sont identiques à l'octet à celle du titre avec le focus sur `OPTIONS`. Aucune exception dans les logs.
- Commit : `fix(sample): move the options popup below the QUIT row`.

### ✅ T7.3 — Un exemple de fondu neutre

- L'exemple chiffré du fondu est remplacé partout à la tête de branche par un fondu neutre : 255 à 0 par 5, 51 images, 0,85 s. Ce commit ne recopie pas les anciens chiffres.
- Lieux : ceux de la demande, plus trois qu'elle ne nommait pas : `screen-effects.md` section 5.2 (la phrase « a frame 4 steps late »), la ligne d'ADR-0082 sur les 30 Updates, et les lignes 85 et 86 de ce plan. Recherche sur `docs/`, `ai-agent/`, les tests, les samples et MGUI : plus rien. `ScreenEffectServiceTests.AFadeInFrames_StepsExactly_WhenTheStepDivides255` reste, c'est un balayage, et le 16 de `AFadeInFrames_MissesItsMultiples_WhenTheStepDoesNotDivide255` aussi.
- 0,85 sur 166667 ticks, test jetable sur `ScreenEffectService` : `0.85f` et `51 / 60f` ont les mêmes bits (`3F59999A`), le fondu finit sur l'image 51, chaque valeur vaut 255 - 5 × image, le temps cumulé en `float` lit 0.850002. `0.8501` finit sur l'image 52, c'est la valeur écrite dans `screen-effects.md`.
- Mesure sur `scratchpad/fade2`, copie du sample avec `"IsFixedTimeStep": true` et 0,85 s, le `VSyncEnabled` du sample conservé. Trace temporaire de la valeur dessinée et des Updates avant chaque image, câblage de `LoadTimeDiscard` changé par une variable d'environnement. Les quatre lignes du tableau d'ADR-0082, deux runs chacune : mêmes valeurs, mêmes Updates d'un run à l'autre. Fondu sortant suivi de `change_world`, deux runs avec le discard et deux sans. Les nouveaux chiffres sont dans ADR-0082.
- Valeurs qui ne reviennent pas à l'identique, écrites comme mesurées : sans discard, le fondu n'est plus « jamais vu », il se voit sur une image, à 110, parce que 30 Updates ne couvrent pas 51 pas ; un seul reset donne 3 Updates avant l'image 1, pas 4 ; une seule image à 10 Updates suit le changement de monde sans discard, aux deux runs, ce qui remplace la fourchette d'avant.
- Délais : même ordre sur les huit runs, avec le chargement à 994-1096 ms, le premier `Update` des vues à 64-71 ms, le premier `Draw` à 252-257 ms et le premier pas de physique à 78-88 ms. La table d'ADR-0082 garde ses chiffres. Sa phrase d'introduction disait « in the same runs », ce qui n'est plus vrai : elle est corrigée et donne les miens.
- Piège vu en T7.1 et évité ici : une sauvegarde restaurée par `Copy-Item` garde sa date, le compilateur croit alors le code à jour et garde la version tracée. Les tests ont échoué 7 fois sur ce binaire périmé, sans défaut dans le code. Le fichier restauré reçoit maintenant la date courante.
- Trace retirée : `git status` vide après le retrait, aucune occurrence de `FadeTrace` ni de `FADE2_`, `FadeTrace.cs` supprimé, puis reconstruction.
- Commits du dessous, non réécrits, qui portent les anciens chiffres : `d50ba581` (`chantier/screen-fade-actions`, `docs/engine/screen-effects.md`, chiffres dans le fichier) et `3ce34f21` (`chantier/screen-open-and-focus-markers`, ADR-0082, `data-driven-screens.md`, `screen-effects.md`, ce plan, et une phrase de son message). Tous les commits entre `d50ba581` et ce commit-ci portent ces fichiers tels quels. `chantier/data-driven-screen-flow`, `chantier/pixel-ui-screens` et `chantier/data-driven-navigation` n'en contiennent aucun.
- Fichiers : ADR-0082, `docs/engine/data-driven-screens.md`, `docs/engine/screen-effects.md`, ce plan, `ai-agent/README.md`.
- Commit : `docs(ui): use a neutral fade as the worked example`.

### ✅ T7.4 — Des documents exacts

Chaque constat a été vérifié contre le code avant toute correction, puis mesuré quand c'était possible. Les numéros sont ceux de la demande. Aucun comportement ne change.

- Constat 1, vrai : « un premier écran dessine une image avant son premier `Update` » était faux. Dans la trace du Launcher, le premier `Update` de l'écran tombe dans le premier `Update` de la boucle, avant le premier `Draw`. Aucune raison d'ordre ne tient pour un premier écran ni pour un `open_screen` ordinaire. La raison écrite est celle que la demande prévoyait : le chargement est le seul endroit par où passe toute ouverture d'écran. Corrigé en T7.6 : la suite de cette ligne disait que la première image dessinée montre le fondu et que cela comptait pour un écran sans `Update` sur l'image de son ouverture, comme si la fenêtre y était dessinée. Elle ne l'est pas. Le message de `2cb48c51` garde la phrase fausse : il n'est pas réécrit.
- Constat 2, vrai : à pas fixe, deux runs, un popup ouvert par `open_screen` avec un fondu de 51 pas dans son `on_open_command` dessine 250 sur l'image de son ouverture et atteint 0 sur la 51e à partir de celle-là, sa fenêtre n'apparaissant que sur la suivante (précisé en T7.6). `from` ne se voit qu'au démarrage d'un monde.
- Constat 3, vrai : IL de `Game.Tick` lu, puis mesuré en pas variable, quatre runs. L'image 2 reçoit 1,7 à 3,0 ms, l'image 3 reçoit 0,2 à 0,4 ms, et le fondu dessine 255, 254, 254, puis 250 ou 251. Trouvé en plus, même confusion : `LoadTimeDiscard.cs` et `data-driven-screens.md` disaient que MonoGame rejoue le temps en Updates sans Draw, ce qui n'est vrai qu'à pas fixe. Corrigés.
- Constat 4, vrai : `ResetElapsedTime` ne touche pas `TotalGameTime` (IL). Ce qui le lit n'est pas retenu : l'horloge de MGUI, la répétition d'`UINavigationInput`, `FrameTime.UnscaledTotalTime`. Mesuré sur l'image 1, le temps du jeu étant à 0 : 16,667 ms à pas fixe, 46 à 63 ms en pas variable. Les 500 ms de la revue sont la borne de `MaxElapsedTime`, pas une mesure. Trouvé en plus et écrit dans ADR-0082 : `Draw` lit un pas sur l'image 1 à pas fixe, et après `change_world` le premier `FrameElapsed` du nouveau `UIRoot` vaut tout `TotalGameTime`, 7967 et 8066 ms mesurés. Ce dernier point ne vient pas du discard et n'est pas corrigé.
- Constat 5, vrai, mesuré avec une touche sur bureau sans affichage, avec et sans mapping : rien n'ayant le focus pendant une attente, la première touche le donne au premier bouton et montre son marqueur, les suivantes ne bougent rien, et confirmer ne lance rien. MGUI : `a3c42cb0`, doc XML d'`IsNavigationInputSuspended` et ADR-0025. `MGDesktop.cs` garde son nombre de lignes, donc `ResolvedPilotWriteSitesTests` n'a pas bougé. Même phrase trouvée en plus et corrigée : `UINavigationInput.IsSuspended` et le résumé de `DataDrivenScreen`.
- Constat 6, vrai, sauf un lieu : `docs/decisions/README.md` disait déjà les deux amendements, il ne change pas. ADR-0078, ADR-0081, `data-driven-screens.md`, `docs/README.md` et le README du sample sont corrigés.
- Constat 7, vrai à moitié : `focus_markers` et ses refus sont déjà dans la liste à la tête, T7.1 les y a mis. Seul `on_open_command` manquait : son nom inconnu et son action refusée y sont.
- Constat 8, vrai : `screen-effects.md` donnait à `fade_screen` une surface éditeur que `data-driven-screens.md` et ADR-0078 lui refusent.
- Constat 9, vrai : le test a été renommé par `19e1a540`.
- Constat 10, vrai, compté : 4 cas sur 6 échouent sans le correctif de T5.1, et les deux autres échouent quand le correctif est trop large. T5.1 et le résultat de la phase 5 sont corrigés.
- Constat 11, vrai : les citations d'ADR-0078, d'ADR-0081 et de `screen-effects.md` deviennent des membres nommés.
- Mesures : Launcher Debug, piloté par le script gardé, traces temporaires dans `CasaEngineGame`, `GameManager`, `ScreenEffectComponent`, `SpriteRendererComponent` et `DataDrivenScreen`. Aucun garde déclenché, aucun processus Launcher restant. Sondes de `DataDrivenScreenTests` et mutations de `UINavigationInput` jetables. Tout est retiré par `git checkout` des fichiers, qui leur donne la date courante : `git status` vide, recherche de la trace vide.
- Fichiers (moteur) : `DataDrivenScreen.cs`, `UINavigationInput.cs`, `LoadTimeDiscard.cs`, ADR-0078, ADR-0081, ADR-0082, `docs/engine/data-driven-screens.md`, `docs/engine/screen-effects.md`, `docs/README.md`, `Projects/PixelUiSample/README.md`, `pixel-ui-screens-tasks.md`, ce plan, `ai-agent/README.md`, `MGUI` (pointeur).
- Fichiers (MGUI) : `MGUI.Core/UI/MGDesktop.cs` (commentaire), `Docs/decisions/0025-pause-the-raw-navigation-path.md`.
- Commit (MGUI) : `a3c42cb0`, `docs(input): say what a pause of the navigation leaves running`.
- Commit : `docs(ui): make the open, time and wait statements exact`.

### ✅ T7.5 — Les limites connues

Aucun comportement ne change. Chaque limite a été vérifiée contre le code, puis mesurée, ce qui va au-delà de la demande : presque toutes sont écrites comme mesurées, et ce qui reste lu est dit tel quel. Les lettres sont celles de la demande.

- a, vraie, mesurée : au Launcher, sur les deux sortes de pas, deux runs chacune, 2 quads sur l'image 1, puis 1. L'effet sur les pixels n'a pas été capturé : il est écrit comme lu dans le code.
- b, vraie, mesurée : l'action tourne 389 et 404 ms avant que l'image 1 soit présentée, et l'IL de `SdlGamePlatform.RunLoop` montre la fenêtre dans la boucle. Le moment où le son s'entend n'est pas mesuré.
- c, vraie, mesurée : `Game.SuppressDraw` sur les 300 premiers `Update`, temps à 0 sur tous, fondu resté à `from`, horloge de MGUI qui avance, temps qui repart à la première image présentée. Le cas de `BeginDraw` n'est que lu dans l'IL de `Game.DoDraw`.
- d, vraie, mesurée avec une touche, avec et sans mapping : confirmer lance le premier bouton sur la même image, une direction le saute. Le conseil `default_focus` est écrit.
- e, vraie, mesurée avec un `TextBox`, sur un écran modal et sur un écran qui ne l'est pas.
- f, vraie, mesurée : après le clic, focus sur le second bouton et premier marqueur toujours affiché. La phrase de `data-driven-screens.md`, le commentaire de `FollowFocusWhileFrozen` et la prémisse d'ADR-0082 sont corrigés.
- g, vraie, mesurée : sous un modal sans élément focalisable, Haut change la sélection d'un `ComboBox` et Entrée ouvre sa liste. Aucune commande.
- h, vraie, mesurée sur les quatre cas : seul le cas sans mapping et avec souris perd la commande.
- i, vraie, mesurée sur un vrai `ScreenStack`, avec des écrans qui comptent leurs `Update`. Trouvé en plus : `open_screen` puis `close_screen` dans une liste donne le même résultat, mesuré aussi, et il n'y faut aucun code. C'est le seul cas que change le démarrage au chargement de T7.4. Corrigé en T7.6 : sur cette image seul l'overlay en profite, aucune fenêtre n'y est dessinée.
- j, vraie à moitié : la pause n'est pas posée sur ce premier `Desktop.Update`, mais la conséquence du constat n'a pas été vue. Une touche sur cette image n'a donné aucun focus, n'a rien bougé et n'a rien lancé, comme avec un mapping. La limite le dit. Cause lue en T7.7.
- Mesures : traces et sondes de T7.4, plus une sonde pour `open_screen` puis `close_screen`, retirée par `git checkout`.
- Fichiers : `DataDrivenScreen.cs` (commentaire), ADR-0078, ADR-0080, ADR-0081, ADR-0082, `docs/engine/data-driven-screens.md`, `docs/engine/screen-effects.md`, ce plan, `ai-agent/README.md`.
- Commit : `docs(ui): write the known limits of the open command and the focus`.

### ✅ T7.6 — Une fenêtre ouverte par commande n'est dessinée qu'à l'image suivante

Aucun comportement ne change : un commentaire et des documents. Le constat vient de la dernière vérification, qui l'avait déduit du code sans l'exécuter. Il a été vérifié contre le code, puis mesuré.

- Constat, vrai. ADR-0082 et `data-driven-screens.md` écrivaient, comme lu dans le code, que la fenêtre d'un écran sans `Update` sur l'image de son ouverture est dessinée sur cette image. Le code dit l'inverse. `UIRoot.RunFrame` appelle `desktop.Update()` avant `screenStack.Update`, et une commande ne tourne que dans `DataDrivenScreen.Update` : la fenêtre arrive sur le bureau après les `Window.Update` de l'image, sans layout, et `MGElement.Draw` s'arrête sur des `LayoutBounds` vides. Les références du constat ont été relues, toutes justes. `MGComboBox` et `MGResizeGrip`, qui appellent aussi `UpdateLayout`, ne touchent pas la fenêtre d'un écran.
- C'est plus large que la limite écrite : c'est vrai de tout `open_screen`, popup ordinaire compris. Avec `close_screen` dans la liste, dans un ordre ou dans l'autre, l'image n'a plus aucune UI.
- Le motif de la limite tombe avec : un démarrage dans le premier `Update`, essayé par un changement jetable, montre lui aussi le fondu sur la première image qui dessine la fenêtre. À pas fixe, mêmes valeurs qu'au chargement sur un premier écran (255, 250) et sur un `open_screen` seul (250 sur l'image F de l'action, 245 sur F + 1). Avec `close_screen` : 0 puis 250, contre 250 puis 245 au chargement.
- Ce qui reste du démarrage au chargement, et ce que disait le constat : l'overlay sur F. Mesuré, ce n'est pas rien : avec un fondu soustractif parti de 255, aucun pixel de F au-dessus de 8, contre les 518400 pixels du monde à découvert avec un démarrage dans `Update`. D1 est gardée, son motif est corrigé.
- Trouvé en plus, même confusion : la conséquence d'ADR-0082 sur un écran ouvert par `open_screen`, sa mesure, et la section des fondus de `data-driven-screens.md` disaient « sa première image » pour l'image de l'ouverture, où la fenêtre n'est pas encore là. Corrigés, comme D1, T7.4 (constats 1 et 2) et T7.5 i dans ce plan. Un second `Update` avant le `Draw` de F poserait la fenêtre plus tôt : c'est lu dans le code, non exécuté, et la limite le dit. Toutes les images F mesurées avaient un seul `Update`.
- Mesures : Launcher Debug, 36 runs sur 12 copies du sample, pilotés par le script gardé, aux deux sortes de pas, avec une trace temporaire de chaque fenêtre après `Desktop.Draw`, du tampon d'affichage et de chaque `Update` d'écran. Détail dans ADR-0082. Sur les 8 runs sans fondu avec les deux actions, l'image F n'a qu'une couleur, celle du monde, sur un pixel sur trois d'une ligne sur trois. Les 3 à 5 `Update` par image vus juste après F sur ces runs viennent de la trace, qui y écrit trois PNG : les runs sans PNG gardent 1 `Update`.
- Trace retirée par `git checkout` des fichiers et suppression de `OpenTrace.cs` : `git status` vide, recherche de la trace vide, `CasaEngine.dll` du Launcher reconstruite sans elle. Aucun garde déclenché, aucune exception dans les logs, aucun processus Launcher restant.
- Pas de test neuf : aucune logique ne change, et le constat porte sur ce que fait une image réelle, que le Launcher prouve.
- Fichiers : `DataDrivenScreen.cs` (commentaire), ADR-0082, `docs/engine/data-driven-screens.md`, ce plan, `ai-agent/README.md`.
- Commit : `docs(ui): say a window opened by a command is drawn from the next frame`.

### ✅ T7.7 — Six phrases de la dernière vérification

Aucun comportement ne change : un commentaire et des documents. Tous les constats ont été relus contre le code, aucun n'est faux.

- Rattrapage : 29 `Update`, pas 30. 500 ms font 5000000 ticks, 30 pas en demanderaient 5000010. Le 30 du tableau d'ADR-0082 compte l'`Update` d'avant la boucle, et 255 - 29 × 5 donne bien le 110 mesuré.
- Image sans fenêtre : « aucune UI » ne vaut que si l'écran fermé était seul sur la pile. `ScreenStack.Remove` ne retire que ses fenêtres. Lu, non exécuté.
- Mesure de cette image : les fenêtres et les valeurs viennent des deux sortes de pas, le comptage de pixels du pas fixe seul.
- « Une seule différence » du démarrage au chargement : vrai pour un fondu. Un `play_sound` de premier écran bouge aussi.
- Limite j : aucun élément n'a eu d'`Update` sur cette image, donc `_CanReceiveKeyboardInput` vaut faux et aucun ne peut prendre le focus. La touche pose quand même le mode d'entrée (`MGDesktop.cs:1591`), donc le focus devrait arriver à l'image suivante sans autre touche. Lu, non exécuté.
- ADR-0082 disait qu'un écran prend le focus d'un autre sans condition, contre la limite d'ADR-0078 : un bouton ou un `default_focus` est nécessaire.
- Les valeurs de T7.4 à T7.6 écrites dans la section Measures d'ADR-0082 et absentes de ce plan n'ont pas d'autre rapport écrit : leurs traces ne sont pas versionnées.
- Fichiers : `DataDrivenScreen.cs` (commentaire), ADR-0081, ADR-0082, `docs/engine/data-driven-screens.md`, `docs/engine/screen-effects.md`, ce plan.
- Validation : voir le résultat de la phase 7.
- Commit : `docs(ui): correct the last statements of the verification`.

## Points ouverts

Vu pendant T7.1, hors de la demande, non corrigé :

- Un marqueur placé dans le marqueur d'une autre paire est accepté. Quand son élément tient le focus, il est `Visible`, mais rien ne le dessine : `MGElement.Draw` s'arrête à l'ancêtre caché. Lu dans le code, non exécuté.

Vu pendant la dernière vérification, à trancher par l'auteur avant une fusion :

- `d50ba581` et `3ce34f21` gardent l'ancien exemple chiffré de fondu, retiré à la tête par `cad4aaa0`, et `3ce34f21` le cite dans son message. Les messages de `2cb48c51` et `69458722` gardent chacun une phrase inexacte. Aucun commit n'a été réécrit.

## Résultat

- Les deux `.sln` : 0 erreur. `CasaEngine.Tests` : 4688/4688, reconstruit.
- Launcher sur `Projects/PixelUiSample` : le curseur n'apparaît que sur `FADE OUT`, et disparaît quand le focus passe à `QUIT`. Aucune exception dans le log.
- Limite ajoutée à ADR-0081 après vérification de `chantier/screen-fade-actions` : sans mapping, un bouton tenu à travers `change_world` compte comme un nouvel appui sur le monde suivant. Il faut un écran poussé par du code pour l'atteindre. Le comportement n'est pas corrigé.
- T3.1 : les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4700/4700, reconstruit. Launcher sur un projet à pas fixe : séquences mesurées de nouveau en T7.3 sur un fondu de 0,85 s : 255, 250, puis de 5 en 5 jusqu'à 0 à l'image 52, une seule image à 2 Updates (la première). Fondu sortant : 5 à 255 sur 51 images, 255 dessiné avant le changement de monde.
- T4.1 : les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4711/4711, reconstruit, `MGUI.Tests` 3225/3225. Launcher sur le sample : focus au troisième `Update`, curseur sur `FADE OUT` puis disparu sur `QUIT`, aucune exception. Sur une copie sans `default_focus` : 930 `Update` sans focus ni marqueur, puis marqueur à la première touche. Sur une copie où le marqueur est son élément : le jeu s'arrête au chargement sur le message qui nomme l'enveloppe. Sur un projet externe qui utilise `focus_markers` : chargement sans erreur.
- Phase 5 : les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4736/4736, reconstruit, `MGUI.Tests` 3225/3225, MGUI inchangé à `62498f75`. 15 mutations, toutes tuées et restaurées (T5.2, T5.3, T5.5). Pour a et b, 4 des 6 cas neufs ont été vus en échec avant le correctif, voir T5.1. Pour f et h, le test neuf a été vu en échec. Launcher sur le sample : voir T5.6. Sur un projet externe, avec mapping et pas fixe : chargement, deux touches et deux redimensionnements, aucune exception. Aucun nom interdit dans le diff ni dans les messages de commit depuis `d9cff5e6`.
- Phase 6 : les deux `.sln` à 0 erreur, recompilés sans incrémental, `CasaEngine.Tests` 4736/4736, reconstruit, `MGUI.Tests` 3226/3226, MGUI à `83bda51f`. 1 mutation, tuée et restaurée. Aucun Launcher : ni le code de production de MGUI ni celui du moteur ne change.
- Phase 7, T7.1 à T7.3 : les deux `.sln` à 0 erreur et `CasaEngine.Tests` 4742/4742, reconstruit, après chaque tâche. MGUI inchangé à `83bda51f`, `MGUI.Tests` non relancé. 7 mutations, toutes tuées et restaurées (T7.1). Launcher sur le sample et sur la copie à pas fixe, aucune exception dans les logs, aucun garde de saisie déclenché, aucun processus Launcher restant.
- Phase 7, T7.4 : les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4742/4742, reconstruit, `MGUI.Tests` 3226/3226, reconstruit, MGUI à `a3c42cb0`. Seuls des commentaires et des documents changent, donc aucun test neuf. Sur les 11 constats, 9 sont vrais tels quels, le 6 l'est sauf un lieu, le 7 à moitié, aucun n'est faux. Mesures de T7.4 : voir cette tâche.
- Phase 7, T7.5 : les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4742/4742, reconstruit, `MGUI.Tests` 3226/3226, reconstruit, MGUI à `a3c42cb0`. Launcher sur `Projects/PixelUiSample`, sur le binaire sans trace : le popup est identique à l'octet aux captures de T7.2 et de T7.3, le retour au titre à celle de T7.3, aucune exception dans le log, aucun garde déclenché, aucun processus Launcher restant. Sur les 10 limites, 9 sont vraies et la j l'est à moitié.
- Phase 7, T7.6 : les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4742/4742, reconstruit, `MGUI.Tests` 3226/3226, reconstruit, MGUI inchangé à `a3c42cb0`. Launcher sur `Projects/PixelUiSample`, sur le binaire sans trace : popup et retour au titre identiques à l'octet aux captures de T7.4, aucune exception dans le log, aucun garde déclenché, aucun processus Launcher restant. Le constat est vrai.
- Phase 7, T7.7 : les deux `.sln` à 0 erreur, `CasaEngine.Tests` 4742/4742, reconstruit. Un commentaire et des documents seulement : aucun test neuf, aucun Launcher.
