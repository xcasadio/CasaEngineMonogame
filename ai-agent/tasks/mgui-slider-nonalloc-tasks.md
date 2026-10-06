# Plan agent IA — `MGSlider` sans allocation quand sa valeur change

Plan d'exécution de la décision D20 de [audio-modern-tasks.md](audio-modern-tasks.md) (réponse à O24-6, 2026-10-06) :
l'allocation de `MGSlider` à chaque changement de valeur se corrige dans un chantier séparé, avant le panneau de
mixage éditable (tranche S6b), qui utilisera des sliders. Le 2026-10-06, l'auteur a étendu le chantier aux
allocations qui restaient autour du glissement d'un slider (abonnés existants, entrée souris et clavier, liaison de
données, `MGColorSlider`, update par image de `MGElement`) ; le texte de `MGTextBlock` part dans un plan séparé.
Les décisions D1 → D10 ci-dessous ont été arbitrées avec l'auteur le 2026-10-06 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Un `MGSlider` qu'on fait glisser n'alloue plus rien à chaque image sur son propre chemin, de l'entrée jusqu'à
l'abonné :

- changement de valeur sans `EventArgs` (événement additif `ValueChangedNonAlloc`), abonnés existants migrés ;
- texte de la valeur reconstruit seulement quand le texte affiché change ;
- `DrawSelf` sans liste ni LINQ pendant le survol ou le glissement ;
- `MGColorSlider` traité de la même façon ;
- update par image de chaque `MGElement` sans argument ni itérateur alloué ;
- entrée (trackers et handlers souris, clavier et manette, détection d'activité du bureau) sans allocation par
  image, arguments de déplacement et de glissement réutilisés ;
- notification d'une propriété liée par `DataBinding` sans passer par l'événement faible WPF.

Restent hors de ce plan, chacun avec un plan séparé préparé en phase 3 : le texte affiché qui change vraiment
(pipeline de `MGTextBlock`, T3.1) et le reste d'une image complète du bureau (ordre des fenêtres de
`MGDesktop.Update` et du dessin, update et layout de `MGWindow`, T3.2).

## État vérifié du dépôt (2026-10-06)

- Worktree réparé en début de session : `git rev-parse --show-toplevel` rendait le gitdir ; `core.worktree` posé
  pour ce seul worktree (`config.worktree`), sous-modules `MGUI` et `NvgSharp` initialisés depuis le checkout local.
- Branche moteur `chantier/mgui-slider-nonalloc` créée depuis `main` (`6f06298f`, qui contient D20). Arbre propre.
  Pointeur `MGUI` = `d3e0cd1` = `develop` de MGUI (`git submodule status`).

### Slider

- `MGUI/MGUI.Core/UI/MGSlider.cs` : `SetValue` (`:106-117`) lève `ValueChanged?.Invoke(this, new(Previous, Value))`
  (`:114`), un `EventArgs<float>` par changement ; `EventArgs<TProperty>` est une classe à propriétés en lecture seule
  (`MGUI/MGUI.Shared/Helpers/EventArgs.cs:23-33`). Le constructeur s'abonne à son propre événement (`:723`), donc
  l'événement n'est jamais nul ; cet abonnement est le premier de la liste, le texte de la valeur est à jour avant les
  abonnés extérieurs.
- `UpdateValueLabelText` (`:650-656`) construit `Value.ToString(...)` à chaque changement quand `ShowValueLabel` est
  vrai ; `MGTextBlock.SetTextCore` ignore une chaîne identique (test `_Text != Value`), seule la chaîne est gaspillée.
- `DrawSelf` alloue une `List<Rectangle>` puis `Where(...).ToList()` à chaque image de survol, d'appui ou de
  glissement (`:1050`, `:1133`).
- `NotifyPropertyChanged` n'alloue pas sans liaison : arguments en cache par nom (`MGUI.Shared/Helpers/ViewModelBase.cs:15-23`).
- Précédents MGUI d'événement par type valeur : `EventHandler<float> SplitRatioDragged`
  (`MGUI.Core/UI/Docking/Controls/MGDockSplitterBar.cs:122`), `EventHandler<(MGTheme PreviousTheme, MGTheme Theme)>
  OnDefaultThemeChanged` (`MGUI.Core/UI/MGResources.cs:544`).
- Abonnés de `MGSlider.ValueChanged`, tous lisent `NewValue` pendant l'appel sans garder l'argument : moteur
  `CasaEngine.Editor/Controls/AnimationClipPreviewPanel.cs:504`, `MaterialAssetInspectorPanel.cs:1286`,
  `ParticlePreviewViewport.cs:456`, `CasaEngine.Demos/Demos/DemoUI/BlendingControlsScreen.cs:146` ; MGUI
  `MGUI.Samples/Controls/Slider.xaml.cs:26`, `:31`, `:36`, `MGUI.Samples/Features/AnimationDemo.xaml.cs:121`, `:122`,
  `:132`, `:255`, `:306`. Sous-classe : `CasaEngine.Editor/Controls/Timeline/TimelineHorizontalScrollBar.cs:8`, à
  laquelle son propriétaire s'abonne (`CasaEngine.Editor/Controls/Timeline/TimelineControl.cs:147`, gestionnaire
  `:950` qui ne lit que `e.NewValue`). Les autres `ValueChanged +=` du moteur visent des `NumericField`,
  `ColorEditor` ou `MGColorField`, pas des sliders.
- `MGUI.Core/UI/Color/MGColorSlider.cs` : `ValueChanged` (`:126`, levé `:226`) et `ValueChanging` (`:127`, levé
  `:356` pendant un glissement) allouent un `EventArgs<float>` chacun, seulement s'ils ont un abonné (pas
  d'abonnement à soi-même) ; son dessin n'alloue pas (cache de dégradé reconstruit seulement si taille, canal ou
  couleur changent). Seul abonné : `MGUI.Samples/Controls/ColorPicker.xaml.cs:70-71`. Absent du moteur.

### Update par image de `MGElement`

- `MGElement.Update` crée `ElementUpdateEventArgs` (une classe, `MGUI.Core/UI/MGElement.cs:3807`) à chaque image pour
  chaque élément, même sans abonné (`:3988`), puis le passe à `OnBeginUpdate`, `OnBeginUpdateContents`,
  `OnEndUpdateContents`, `OnEndUpdate` (`:3990`, `:4064`, `:4093`, `:4121`).
- Il parcourt trois itérateurs `yield` à chaque image (`:3993-4009`) : `GetBorderBrushes`, `GetFillBrushes`,
  `GetVisualStateFillBrushes` (`:3561-3590`), surchargés dans 11 contrôles MGUI (dont `MGSlider`, qui les chaîne à
  la base), aucun dans le moteur. Autres usages : `MGUI.Tests/Architecture/FillBrushLifecycleTests.cs:351`, `:361`,
  `:652-653` ; docs `MGUI/Docs/drawing-architecture.md:85`, `:142` ; doc XML `MGUI.Core/UI/Brushes/FillBrushes/IFillBrush.cs:48`.

### Entrée

- `MGUI.Shared/Input/Mouse/MouseTracker.cs` : nouvel argument à chaque image où la souris bouge (`:376`,
  `CurrentMoveEvent`, propriété publique `:189`) et à chaque image de glissement (`:473`, exposé par
  `CurrentDraggedEvents`, `:322`). `UpdateHandlers` (`:521-531`) trie les handlers par LINQ (`Where`,
  `OrderByDescending`, `GroupBy`) à chaque image, même souris immobile ; même code clavier
  (`MGUI.Shared/Input/Keyboard/KeyboardTracker.cs:316`). `MouseHandler.UpdatePriority` ne change jamais après la
  construction (`MouseHandler.cs:65`). `MouseHandler` crée une `List<DragStartCondition>` à chaque image de
  glissement (`MouseHandler.cs:783`).
- Autres allocations par image du chemin d'entrée (MGUI cible .NET 9, `MGUI/Directory.Build.props:5`) :
  - `MouseTracker.Update` : `foreach` sur `MouseButtons`, une `ReadOnlyCollection` (`MouseTracker.cs:77`), et sur
    `MouseHandler.DragStartConditions`, un `IReadOnlyList` (`MouseHandler.cs:60-61`), donc un énumérateur alloué
    à chaque parcours (`MouseTracker.cs:381`, `:438`, `:440`) ; huit `Any(...)` LINQ sur des dictionnaires à chaque
    image, souris immobile comprise (`:491-498`).
  - `MouseHandler` : même `foreach` sur `MouseButtons` à chaque image de glissement (`:796`) et aux images
    d'événement (`:489`, `:561`, `:633`, `:706`).
  - `KeyboardTracker.Update` : `foreach` sur `AllKeys`, une `ReadOnlyCollection` (`KeyboardTracker.cs:44`, `:162`) ;
    `GetPressedKeys().Where(IsTrackedKey).ToList()` deux fois par image (`:169-170`). Version de MonoGame référencée
    par MGUI : `MonoGame.Framework.DesktopGL` 3.8.5.1 (`MGUI/Directory.Packages.props:8`).
  - `KeyboardHandler.InvokeQueuedEvents` : `foreach` sur `AllKeys`, une `ReadOnlyCollection`
    (`MGUI.Shared/Input/Keyboard/KeyboardHandler.cs:23`, `:151`, `:164`), à chaque image, par `AutoUpdate`
    (`UpdateHandlers`) et par `ManualUpdate` (`MGUI.Core/UI/MGElement.cs:4117`) ; le bureau a toujours un handler
    clavier abonné (`MGUI.Core/UI/MGDesktop.cs:1423`).
  - `GamePadTracker.Update`, appelé à chaque image par `InputTracker.Update` (`MGUI.Shared/Input/InputTracker.cs:71`) :
    `foreach` sur `AllButtons`, une `ReadOnlyCollection` (`MGUI.Shared/Input/GamePad/GamePadTracker.cs:35`, `:75`,
    `:88`, `:95`, `:104`) ; `HasActivity` fait un `Values.Any(...)` (`:66`).
  - `MGDesktop.Update` appelle à chaque image `HasKeyboardActivity` (trois `Values.Any(...)`,
    `MGUI.Core/UI/MGDesktop.cs:166-169`) et `GamePad.HasActivity()` (`:1555`).
  - Par action seulement, hors périmètre : `InputRouter.Route` (LINQ, `MGUI.Shared/Input/Semantic/InputRouter.cs:48-52`).
- Parcours systématique de `MGUI.Shared/Input` (`rg "foreach \(|\.Any\(|\.Where\(|\.ToList\(|OrderBy|GroupBy"`) :
  tous les sites par image sont listés ci-dessus ; les autres sont dans des initialisations statiques ou des méthodes
  appelées à l'événement.
- Les arguments portent un état « handled » modifiable (`HandledByEventArgs.SetHandledBy`, `Reset`,
  `MGUI.Shared/Input/InputTracker.cs:20-50`) ; aucun code du moteur ni de MGUI ne garde un argument de déplacement
  ou de glissement au-delà de l'appel (`rg CurrentMoveEvent|CurrentDraggedEvents|BaseMouse(Moved|Dragged)EventArgs`).

### Liaison de données

- Version WPF (celle qui est compilée, `MGUI.Core/MGUI.Core.csproj:8-10`) : `DataBinding` s'abonne à la source et à
  la cible par `PropertyChangedEventManager.AddHandler` (`MGUI.Core/UI/DataBinding/DataBinding.cs:269`, `:408`), et
  aux objets intermédiaires d'un chemin pointé de même (`:194`). Version sans WPF (`#else`) : `+=` direct (`:271`,
  `:410`) et `PropertyNameHandler` (`:198`). Les gestionnaires filtrent déjà le nom de propriété (`:908-918`).
- Environ 192 octets par notification dans la diffusion WPF, mesurés par une sonde isolée et cités en en-tête de
  `MGUI.Tests/Architecture/DataBindingAllocationTests.cs` (aucun test ne les mesure).
- `DataBindingManager` garde chaque liaison par une référence forte (`MGUI.Core/UI/DataBinding/DataBindingManager.cs:6-9`)
  jusqu'à `RemoveBinding`, qui appelle `Dispose` (`:45-63`) ; l'événement faible ne change donc pas la durée de vie
  d'une liaison enregistrée.

### Bureau (pour le plan séparé de T3.2)

- `MGDesktop.Update` trie les fenêtres à chaque image : `Windows.Reverse<MGWindow>().OrderByDescending(...).ToList()`
  (`MGUI.Core/UI/MGDesktop.cs:1588`) ; `foreach (var Window in Windows.OrderBy(...))` (`:1725`). `MGWindow.Update`,
  `MGWindow.Draw` et le layout n'ont pas été parcourus.

### Texte (pour le plan séparé de T3.1)

- Chaque changement de texte reparse tout (`MGUI.Core/UI/MGTextBlock.cs:1167-1189` : `ParseRuns`, `ToList`, LINQ),
  puis invalide le layout du parent (`:973-990`) ; la mesure refait `MGTextLine.ParseLines(...).ToList()` (`:1053`).
  L'API de mesure et de dessin du texte n'accepte que des `string` (`MGUI.Shared/Text/Engines/ITextMeasurementEngine.cs:12`,
  `MGUI.Shared/Rendering/IUIDrawContext.cs:21`).

### Tests et conventions

- `MGUI/MGUI.Tests/AllocationWindow.cs` ; précédents : test d'allocation de `DrawSelf`
  (`MGUI.Tests/Architecture/AnimatedImageTests.cs:410-414`), simulation d'états souris
  (`MGUI.Tests/Input/MouseTrackerDragFastPathTests.cs`), tests de liaison sérialisés (`DataBindingRegistryCollection`, ADR-0019).
- `MGUI.Tests/Architecture/ResolvedPilotWriteSitesTests.cs:90` autorise la ligne 675 de `MGSlider.cs` par son numéro :
  toute ligne ajoutée au-dessus doit y être reportée.
- MGUI : plans dans `MGUI/Docs/Tasks/`, ADR en anglais dans `MGUI/Docs/decisions/` (dernière : ADR-0020), docs
  d'architecture en français sans accents.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D0 | (D20 du plan audio) L'allocation de `MGSlider` se corrige dans ce chantier séparé, dans le sous-module MGUI. |
| D1 | Nouvel événement additif `EventHandler<(float PreviousValue, float NewValue)>`, levé juste après `ValueChanged` avec le même couple de valeurs. Le slider ne s'abonne plus à lui-même : il met son texte à jour directement, au même moment qu'avant (avant tout abonné). `ValueChanged` garde sa signature, n'est pas marqué `[Obsolete]`, et n'alloue que s'il a des abonnés. |
| D2 | Nom de l'événement : `ValueChangedNonAlloc`. |
| D3 | Texte de la valeur : `float.TryFormat` (API .NET de base, aucune dépendance) dans un `char[]` privé du slider, comparé au texte affiché ; une chaîne n'est construite que si le texte affiché change. |
| D4 | L'allocation de `DrawSelf` pendant le survol ou le glissement est corrigée dans une tâche et un commit MGUI séparés. |
| D5 | Les abonnés existants passent à `ValueChangedNonAlloc` : éditeur et démo du moteur, samples MGUI (« inclus dans des tâches », 2026-10-06). |
| D6 | `MGColorSlider` reçoit `ValueChangedNonAlloc` et `ValueChangingNonAlloc`, de même forme que D1 et D2 ; ses événements actuels restent. Découle de D1, D2 et de la demande d'inclure `MGColorSlider` : à confirmer à l'approbation du plan. |
| D7 | Texte affiché qui change vraiment : tout le pipeline de `MGTextBlock` devient sans allocation, dans un chantier séparé avec son propre plan ; ce plan ne fait que l'écrire (T3.1). |
| D8 | Entrée : une instance d'argument par tracker pour le déplacement et le glissement, remise à zéro à chaque image (`Reset` existant). Le tri LINQ de `UpdateHandlers` (souris et clavier) est remplacé par un ordre mis en cache, reconstruit à l'ajout ou au retrait d'un handler. La `List` de `MouseHandler` disparaît. Changement visible accepté : `CurrentMoveEvent` et `CurrentDraggedEvents` rendent le même objet d'une image à l'autre. |
| D8b | Entrée, suite : toutes les autres allocations par image de `MouseTracker.Update`, `KeyboardTracker.Update`, `GamePadTracker.Update` et `HasActivity`, du chemin d'événements de `MouseHandler` et de `KeyboardHandler.InvokeQueuedEvents`, et de `MGDesktop.HasKeyboardActivity` (énumérateurs, `Any`, listes de touches) disparaissent aussi, sans changement visible. Trouvé par la relecture du plan après D8, et nécessaire à l'objectif « glissement sans allocation » : à confirmer à l'approbation du plan. |
| D9 | Liaison : la version WPF utilise aussi l'abonnement direct de la version sans WPF (`+=` et `PropertyNameHandler`) à la place de `PropertyChangedEventManager`. |
| D10 | `MGElement.Update` : `ElementUpdateEventArgs` n'est créé que si l'un des quatre événements d'update a un abonné ; les itérateurs `Get*Brushes` sont remplacés par des méthodes protégées qui remplissent une liste réutilisée ; les 11 surcharges MGUI sont migrées. |

## Règles d'exécution pour l'agent

- **Branches dédiées `chantier/mgui-slider-nonalloc`** : dans le moteur depuis `main`, dans `MGUI` depuis `develop`
  (`d3e0cd1`). Ne jamais committer sur `main` ni sur `develop`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la
  validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche,
  puis **créer un commit dédié** qui inclut la mise à jour de ce fichier. Une tâche MGUI a son commit dans `MGUI` ; la
  mise à jour de ce plan pour cette tâche part dans le commit moteur suivant, en le disant dans sa note.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Aucun merge (ni `develop` de MGUI ni `main` du moteur) sans demande explicite de l'auteur.
- **Ne rien inventer** : sinon passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
  En particulier, une allocation imprévue trouvée par un test d'allocation hors des sites listés ici est un point
  ouvert, pas une extension silencieuse du périmètre.
- **Build obligatoire** avant ✅ dès que du code est touché ; **tests** MGUI et moteur selon la tâche ; chaque test
  d'allocation est contre-éprouvé (rouge sur l'ancien code ou avec la mutation indiquée, arbre remis à l'identique).
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session. `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, commits et ADR en anglais ; docs d'architecture MGUI dans la langue du fichier.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds (input, update, draw).

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- MGUI : `dotnet build MGUI/MGUI.Tests/MGUI.Tests.csproj` et `dotnet build` du projet `MGUI.Samples` sans erreur ;
  `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` vert (ligne de base relevée en T1.1 avant toute modification).
- Moteur : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` sans erreur ;
  `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` vert (ligne de base relevée en T2.1).
- Preuve de bout en bout (T1.11) : une image du chemin du slider glissé (update de l'entrée et des handlers, update
  du slider, qui met à jour ses propres handlers, `MGElement.cs:4114-4117`, changement de valeur, `DrawSelf` du
  slider) n'alloue rien, label de valeur masqué ou à texte inchangé. L'image complète du bureau relève de T3.2.
- Smoke : `MGUI.Samples` lancé une fois. Glissement visuel d'un slider (sample « Slider », sample « ColorPicker »,
  inspecteur de matériau de l'éditeur) : 🧪 laissé à l'auteur si l'agent ne peut pas le piloter.
- Vérificateur frais (`verifier`) sur l'ensemble avant de déclarer le chantier terminé (T4.1).

---

## Phase 0 — Plan

### ✅ T0.1 — Branche moteur et plan

- Objectif : ce plan, sa ligne dans `ai-agent/README.md`, la branche moteur.
- Fichiers : `ai-agent/tasks/mgui-slider-nonalloc-tasks.md`, `ai-agent/README.md`.
- Validation : relecture par l'auteur, approbation avant toute modification de code.
- Commit : `docs(plan): plan the allocation-free MGUI slider value change` ; révision du 2026-10-06 :
  `docs(plan): extend the slider chantier to the remaining per-frame allocations`.

---

## Phase 1 — MGUI (sous-module, branche `chantier/mgui-slider-nonalloc`)

### ✅ T1.1 — `ValueChangedNonAlloc`, plus d'abonnement à soi-même, texte formaté dans un tampon

- Objectif : D1, D2, D3.
- Fichiers : `MGUI/MGUI.Core/UI/MGSlider.cs`, `MGUI/MGUI.Tests/Controls/SliderValueChangedTests.cs` (nouveau),
  `MGUI/MGUI.Tests/Architecture/ResolvedPilotWriteSitesTests.cs` (numéro de ligne reporté).
- Étapes :
  1. Branche MGUI `chantier/mgui-slider-nonalloc` depuis `develop` ; ligne de base `dotnet test` de `MGUI.Tests`.
  2. `SetValue` : après `NotifyPropertyChanged(nameof(Value))`, appeler `UpdateValueLabelText()` directement, puis
     lever `ValueChanged` (inchangé) puis `ValueChangedNonAlloc` avec le même couple de valeurs ; supprimer
     l'abonnement du constructeur (`:723`). Doc XML des deux événements : coût de l'un, absence d'allocation de
     l'autre, ordre de levée.
  3. `UpdateValueLabelText` : `Value.TryFormat` dans un `char[]` privé avec le format courant ; si le résultat est égal
     (`SequenceEqual`) au texte affiché, ne rien faire ; sinon construire la chaîne depuis le tampon ; si le tampon est
     trop petit, repli sur `ToString` (comportement actuel). La comparaison reste sous le test `ShowValueLabel`
     (`:652`) : le constructeur appelle `SetValue` (`:672`) avant de créer `ValueLabelElement` (`:713`).
  4. Tests : `ValueChanged` et `ValueChangedNonAlloc` reçoivent les bons ancien et nouveau couples (valeur bornée et
     valeur discrète comprises), dans cet ordre, une seule fois, et rien pour une valeur identique ; le texte de la
     valeur suit la valeur, le format et `ShowValueLabel`, et il est déjà à jour quand un abonné de `ValueChanged` est
     appelé ; allocation nulle sur des `SetValue` répétés (fenêtre `AllocationWindow.Start()` après chauffe) sans
     abonné, avec un abonné de `ValueChangedNonAlloc`, et avec un texte affiché inchangé (format `F0`, valeurs 3,2 et 3,4).
  5. Contre-épreuve : remettre temporairement l'abonnement à soi-même, puis l'ancien `ToString` ; les tests
     d'allocation doivent échouer.
- Validation : build `MGUI.Tests` et `MGUI.Samples` ; nouveaux tests verts ; suite `MGUI.Tests` complète verte.
- Commit (MGUI) : `feat(slider): raise an allocation-free value change event and format the value label without allocating`
- Note (2026-10-06) : MGUI `chantier/mgui-slider-nonalloc` créée depuis `develop` `d3e0cd1` ; commit `da602c2`.
  Ligne de base `MGUI.Tests` 3108/3108. 9 tests dans `SliderValueChangedTests` (valeurs, ordre, borne, valeur discrète,
  valeur identique, texte de la valeur, texte déjà à jour pour les abonnés, 3 tests d'allocation nulle). Contre-épreuves :
  abonnement à soi-même remis, 3 tests d'allocation rouges (24 000 octets pour 1000 changements) ; ancien `ToString`
  remis, test du texte inchangé rouge (24 000 octets) ; arbre remis à l'identique (empreinte du diff identique).
  `ResolvedPilotWriteSitesTests` : ligne 675 → 701. Build `MGUI.Tests` et `MGUI.Samples` sans erreur ; `MGUI.Tests`
  3117/3117. Mise à jour du plan commitée avec le commit moteur suivant.

### ✅ T1.2 — `DrawSelf` du slider sans liste ni LINQ

- Objectif : D4.
- Fichiers : `MGUI/MGUI.Core/UI/MGSlider.cs`, test voisin de T1.1, `ResolvedPilotWriteSitesTests.cs` si la ligne bouge.
- Étapes :
  1. Remplacer les deux `new List<Rectangle>() { ... }.Where(...).ToList()` (`:1050`, `:1133`) par deux tests sur
     `Rectangle.Empty` qui dessinent les mêmes morceaux, dans le même ordre, avec les mêmes brushes et épaisseurs.
  2. Test d'allocation nulle d'un `DrawSelf` répété dans l'état qui dessine la surcouche (survol ou glissement), en
     horizontal et en vertical, sur le modèle de `AnimatedImageTests.cs:410-414`. Si le runtime de test ne permet pas
     d'atteindre cet état sans modifier MGUI hors tâche, le dire dans la note et passer en 🧪.
- Validation : build ; test vert, rouge avec l'ancien code ; suite `MGUI.Tests` complète verte.
- Commit (MGUI) : `perf(slider): draw the hover overlay without allocating a list each frame`
- Note (2026-10-06) : commit `702f09d`. Les morceaux non vides passent par un helper `DrawNumberLineOverlayChunk`,
  même ordre. `SliderDrawAllocationTests` (4) : en état survolé, la surcouche couvre le morceau avant, le morceau
  après puis le curseur, contigus au curseur, en horizontal et en vertical (vert sur l'ancien code aussi : même
  dessin) ; allocation nulle du code de la surcouche (slider non visible au hit-test, donc `IsHovered` vrai et brosses
  de surcouche nulles, bordures retirées). Contre-épreuve : ancien `DrawSelf`, 296 000 octets pour 1000 dessins ;
  arbre remis à l'identique. Trouvé en écrivant le test : un `DrawSelf` complet alloue encore 64 octets par bordure
  uniforme dessinée, dans `ThicknessUtils.IsEmpty`, hors des sites du plan : point ouvert O3. `MGUI.Tests` 3121/3121,
  `MGUI.Samples` sans erreur.

### ✅ T1.3 — `MGColorSlider` : événements sans allocation

- Objectif : D6.
- Fichiers : `MGUI/MGUI.Core/UI/Color/MGColorSlider.cs`, `MGUI/MGUI.Tests/Color/ColorSliderTests.cs` (ou fichier voisin).
- Étapes : `ValueChangedNonAlloc` levé juste après `ValueChanged` (`:226`), `ValueChangingNonAlloc` juste après
  `ValueChanging` (`:356`), même couple de valeurs ; doc XML ; tests de valeurs, d'ordre et d'allocation nulle sur des
  `SetValue` répétés avec un abonné de chaque nouvel événement ; contre-épreuve avec l'ancien `EventArgs`.
- Validation : build ; tests verts ; suite `MGUI.Tests` complète verte.
- Commit (MGUI) : `feat(color): raise allocation-free value events from the color slider`
- Note (2026-10-06) : commit `70d9544`. `ColorSliderValueEventsTests` (4) : ordre et valeurs de `ValueChanged` puis
  `ValueChangedNonAlloc` (valeur bornée, valeur identique sans événement) ; glissement simulé (appui puis
  déplacement) : `ValueChanging` puis `ValueChangingNonAlloc`, mêmes valeurs ; allocation nulle de `SetValue` répétés
  et de pas de glissement répétés (méthode privée `SetValueFromScreenPosition` appelée par un délégué construit hors
  de la fenêtre, comme `DataBindingAllocationTests`) avec abonnés des nouveaux événements. Contre-épreuve : un
  `EventArgs` alloué dans les nouveaux événements, tests rouges (24 000 et 48 000 octets) ; arbre remis à l'identique.
  `MGUI.Tests` 3125/3125, `MGUI.Samples` sans erreur.

### ✅ T1.4 — Samples MGUI sur les événements sans allocation

- Objectif : D5 côté MGUI ; montre la feature dans un sample (règle MGUI).
- Fichiers : `MGUI/MGUI.Samples/Controls/Slider.xaml.cs` (`:26`, `:31`, `:36`),
  `MGUI/MGUI.Samples/Features/AnimationDemo.xaml.cs` (`:121`, `:122`, `:132`, `:255`, `:306`),
  `MGUI/MGUI.Samples/Controls/ColorPicker.xaml.cs` (`:70-71`).
- Étapes : chaque abonnement passe à l'événement `NonAlloc` correspondant, corps inchangé (`e.NewValue` existe sous le
  même nom dans le tuple).
- Validation : build `MGUI.Samples` ; `MGUI.Samples` lancé une fois.
- Commit (MGUI) : `refactor(samples): subscribe to the allocation-free slider events`
- Note (2026-10-06) : commit `52fba11`, 10 abonnements passés aux événements `NonAlloc`, corps inchangés ; plus aucun
  abonné slider aux anciens événements dans MGUI hors des tests qui les couvrent. Build `MGUI.Samples` sans erreur ;
  `MGUI.Samples.exe` lancé 25 s (arrêté par `timeout`, code 124), aucun plantage ni sortie d'erreur. Glissement
  visuel des sliders du sample non piloté par l'agent : à faire par l'auteur.

### ✅ T1.5 — `MGElement.Update` : argument d'update créé seulement s'il a un abonné

- Objectif : D10, premier volet.
- Fichiers : `MGUI/MGUI.Core/UI/MGElement.cs`, nouveau test d'allocation (dossier `MGUI.Tests/Architecture/`).
- Étapes : ne créer `ElementUpdateEventArgs` (`:3988`) qu'au premier des quatre événements qui a un abonné, puis le
  réutiliser pour les suivants dans la même image ; mêmes arguments, même ordre pour les abonnés. Test : abonnés
  présents, ils reçoivent un argument portant le bon élément et les bons `UA` aux quatre étapes ; abonné ajouté
  pendant l'update reçoit lui aussi l'argument.
- Validation : build ; tests verts ; suite `MGUI.Tests` complète verte (l'allocation nulle de l'update complet est
  prouvée en T1.6, après les itérateurs).
- Commit (MGUI) : `perf(element): create the update event args only when an update event has a subscriber`
- Note (2026-10-06) : commit `f9ff09c`, helper privé `RaiseUpdateEvent` (argument créé au premier événement qui a
  un abonné, partagé par les suivants de la même update). `ElementUpdateEventArgsTests` (3) : les quatre événements
  reçoivent l'élément et ses `UA`, une instance par update ; abonné du seul `OnEndUpdate` ; abonné ajouté pendant
  l'update. `MGUI.Tests` 3128/3128. Allocation nulle prouvée en T1.6.

### ✅ T1.6 — `MGElement.Update` : brushes collectés dans des listes réutilisées

- Objectif : D10, second volet.
- Fichiers : `MGUI/MGUI.Core/UI/MGElement.cs` et les 11 surcharges (`MGSlider`, `MGRectangle`, `MGShapeElementBase`,
  `MGScrollViewer`, `MGProgressButton`, `MGProgressBar`, `MGGridColorPicker`, `MGOverlayHost`, `MGUniformGrid`,
  `MGGrid`, `MGGridSplitter`), `MGUI.Tests/Architecture/FillBrushLifecycleTests.cs`,
  `MGUI.Core/UI/Brushes/FillBrushes/IFillBrush.cs` (doc XML), `MGUI/Docs/drawing-architecture.md` (`:85`, `:142`).
- Étapes :
  1. Remplacer `GetBorderBrushes`, `GetFillBrushes`, `GetVisualStateFillBrushes` par trois méthodes protégées
     virtuelles qui ajoutent leurs brushes à une liste fournie (même contenu, même ordre, entrées nulles permises,
     surcharges qui appellent la base) ; `Update` les remplit dans des listes de travail réutilisées, vidées après
     usage, sans réentrance possible (une liste occupée n'est pas réutilisée par un update imbriqué).
  2. Migrer les 11 surcharges et `FillBrushLifecycleTests` (mêmes assertions de contenu).
  3. Test : une image d'update (`Update` de l'élément) d'un `MGSlider` immobile n'alloue rien ; contre-épreuve avec
     l'ancien itérateur. Toute allocation restante dans `MGElement.Update` hors des sites de D10 est un point ouvert.
- Validation : build `MGUI.Tests` et `MGUI.Samples` ; suite `MGUI.Tests` complète verte.
- Commit (MGUI) : `perf(element): collect the brushes to tick into reused lists instead of iterators`
- Note (2026-10-06) : commit `2a7d10e`. `CollectBorderBrushes`, `CollectVisualStateFillBrushes`, `CollectFillBrushes`
  (protégées virtuelles) ; `TickOwnBrushes` les remplit dans trois listes `[ThreadStatic]` réutilisées, avec un
  drapeau d'usage (une update imbriquée prend des listes temporaires). 16 surcharges dans 11 fichiers converties par
  script (base puis `Add` dans le même ordre) ; aucune dans le moteur. Sonde et noms de `FillBrushLifecycleTests`,
  doc XML de `IFillBrush`, `drawing-architecture.md` (`:85`, `:142`) mis à jour. `ElementUpdateAllocationTests` :
  update d'un `MGSlider` immobile (registre de peinture vidé à chaque image) sans allocation. Contre-épreuves : listes
  neuves à chaque update, 560 000 octets ; argument d'update recréé à chaque image (avant T1.5), 272 000 octets ;
  arbre remis à l'identique. `MGUI.Tests` 3129/3129, `MGUI.Samples` sans erreur.

### ✅ T1.7 — Entrée : ordre des handlers en cache, plus de `List` au glissement

- Objectif : D8, partie sans changement visible.
- Fichiers : `MGUI/MGUI.Shared/Input/Mouse/MouseTracker.cs`, `MGUI/MGUI.Shared/Input/Keyboard/KeyboardTracker.cs`,
  `MGUI/MGUI.Shared/Input/Mouse/MouseHandler.cs`, tests dans `MGUI/MGUI.Tests/Input/`.
- Étapes :
  1. `UpdateHandlers` (souris `:521-531`, clavier `:316`) parcourt un tableau ordonné par priorité décroissante puis
     ordre d'ajout (l'ordre exact du `OrderByDescending` stable suivi du `GroupBy`), reconstruit seulement quand un
     handler est ajouté ou retiré ; un handler ajouté ou retiré pendant le parcours ne change pas le parcours en
     cours (comme l'instantané du `GroupBy` aujourd'hui).
  2. `MouseHandler` (`:783`) parcourt une ou deux conditions sans liste.
  3. Tests : ordre d'appel identique à l'ancien sur un jeu de priorités mêlées ; ajout et retrait pendant le parcours ;
     handlers manuels exclus ; allocation nulle de `UpdateHandlers` souris immobile ; contre-épreuve avec le LINQ.
- Validation : build ; tests verts ; suites `Input`, `Focus`, `KeyboardNav` puis `MGUI.Tests` complète vertes.
- Commit (MGUI) : `perf(input): order input handlers once instead of sorting them every frame`
- Note (2026-10-06) : commit `adc7cd2`. `InputHandlerOrder.Build` (nouveau, interne à `MGUI.Shared`) : tri par insertion
  stable dans un tableau neuf ; `_OrderedHandlers` remis à null à l'ajout et au retrait réel d'un handler, reconstruit
  par le `UpdateHandlers` suivant (souris et clavier). Conditions de glissement de `MouseHandler` lues une fois et
  parcourues sans `List`. `InputHandlerOrderTests` (7) : ordre souris et clavier égal à l'ordre LINQ d'origine sur des
  priorités mêlées, handler ajouté pendant le parcours (parcouru à l'image suivante), handler retiré pendant le
  parcours, handlers manuels exclus, allocation nulle de `UpdateHandlers` souris immobile et clavier. Contre-épreuve :
  les trois fichiers d'origine, 2 tests d'allocation rouges (1 656 000 octets), 5 tests de comportement verts (même
  ordre) ; arbre remis à l'identique. La `List` de glissement est couverte par la mesure d'une image de glissement en
  T1.9. `MGUI.Tests` 3136/3136, `MGUI.Samples` sans erreur.

### ✅ T1.8 — Entrée : boucles des trackers sans énumérateur, sans LINQ ni liste de touches

- Objectif : D8b.
- Fichiers : `MouseTracker.cs`, `MouseHandler.cs`, `KeyboardTracker.cs`, `KeyboardHandler.cs`,
  `MGUI/MGUI.Shared/Input/GamePad/GamePadTracker.cs`, `MGUI/MGUI.Core/UI/MGDesktop.cs` (`HasKeyboardActivity`
  seulement), tests dans `MGUI/MGUI.Tests/Input/`.
- Étapes :
  1. Parcourir boutons, conditions et touches par `for` sur des tableaux statiques en lecture seule, construits une
     fois à côté des collections publiques existantes (`MouseButtons`, `DragStartConditions`, les deux `AllKeys`,
     `AllButtons` gardent leur type et leur contenu) : `MouseTracker.cs:381`, `:438`, `:440`, `MouseHandler.cs:489`,
     `:561`, `:633`, `:706`, `:796`, `KeyboardTracker.cs:162`, `KeyboardHandler.cs:151`, `:164`,
     `GamePadTracker.cs:75`, `:88`, `:95`, `:104`.
  2. Remplacer les huit `Any(...)` (`MouseTracker.cs:491-498`) par des drapeaux calculés dans les boucles qui
     remplissent déjà ces dictionnaires, ou par des boucles sans LINQ ; mêmes valeurs. Même traitement pour
     `GamePadTracker.HasActivity` (`:66`) et `MGDesktop.HasKeyboardActivity` (`MGDesktop.cs:166-169`).
  3. `KeyboardTracker` (`:169-170`) : touches appuyées précédentes et courantes dans deux listes réutilisées,
     vidées à chaque image, remplies sans `Where`/`ToList`. Si la version de MonoGame référencée fournit une
     surcharge sans allocation pour lire les touches appuyées, l'utiliser ; sinon parcourir les touches suivies avec
     `IsKeyDown`. La vérifier dans le paquet référencé avant de l'écrire, ne pas la supposer.
  4. Tests : mêmes événements souris et clavier qu'avant sur des séquences simulées (appui, relâchement, clic,
     début, suite et fin de glissement, touches tenues et relâchées) ; allocation nulle de `InputTracker.Update`
     (souris, clavier, manette) sur une image souris immobile, une image clavier sans touche et une image avec deux
     touches tenues ; allocation nulle de `UpdateHandlers` souris et clavier et du `ManualUpdate` d'un handler
     clavier abonné et apte à recevoir l'entrée, sans touche ; allocation nulle de `HasKeyboardActivity` et
     `HasActivity` ; contre-épreuve sur l'ancien code pour chaque mesure. L'allocation de l'argument de déplacement
     et de glissement reste mesurée à part (T1.9).
- Validation : build ; tests verts ; suites `Input`, `Focus`, `KeyboardNav`, `TextBox` puis `MGUI.Tests` complète vertes.
- Commit (MGUI) : `perf(input): walk buttons, conditions and keys without allocating each frame`
- Note (2026-10-06) : commit `e702cc9`. `foreach` sur des tableaux statiques construits depuis les collections publiques
  (inchangées) ; `Any` remplacés par des parcours de `Dictionary` concrets (énumérateur struct) ; `HasKeyboardActivity`
  par index sur `KeyboardTracker.AllKeys` ; touches appuyées lues par `GetPressedKeyCount` et `GetPressedKeys(Keys[])`
  (présentes dans MonoGame 3.8.5.1, vérifié dans `MonoGame.Framework.xml` du paquet) dans un tampon et deux listes
  réutilisés. `ResolvedPilotWriteSitesTests` : `MGDesktop.cs` 966 → 980. `InputTrackerAllocationTests` (5) : journal
  d'une session souris et clavier (appui, glissement, relâchement, deux touches à la fois, clics) identique ligne à
  ligne à celui de l'ancien code ; allocation nulle de `InputTracker.Update` souris immobile sans touche et avec deux
  touches tenues, des handlers clavier abonnés (`UpdateHandlers` et `ManualUpdate`), de `HasKeyboardActivity` et
  `HasActivity`. Contre-épreuve : les six fichiers d'origine, 4 tests d'allocation rouges (976 000, 1 200 000,
  80 000 et 160 000 octets) ; arbre remis à l'identique. `MGUI.Tests` 3141/3141, `MGUI.Samples` sans erreur.

### ✅ T1.9 — Entrée : arguments de déplacement et de glissement réutilisés

- Objectif : D8, partie visible.
- Fichiers : `MouseTracker.cs`, `MGUI/MGUI.Shared/Input/Mouse/MouseEventArgs.cs`, tests dans `MGUI.Tests/Input/`.
- Étapes : une instance de `BaseMouseMovedEventArgs` par tracker et une de `BaseMouseDraggedEventArgs` par condition
  et par bouton, remplies et remises à zéro (`Reset`) à chaque image où l'événement a lieu (`:376`, `:473`) ;
  `CurrentMoveEvent` reste nul quand la souris ne bouge pas ; les champs des arguments deviennent modifiables pour le
  tracker seulement (setters internes à `MGUI.Shared`, API publique en lecture inchangée). Doc XML : l'instance est
  réutilisée, ne pas la garder au-delà de l'appel. Tests : positions, bouton, condition et instant corrects image après
  image ; état « handled » remis à zéro à chaque image ; glissement d'un `MGSlider` de bout en bout (valeur suivie) ;
  allocation nulle d'une image de déplacement et d'une image de glissement ; contre-épreuve.
- Validation : build ; tests verts ; suite `MGUI.Tests` complète verte.
- Commit (MGUI) : `perf(input): reuse the mouse move and drag event args across frames`
- Note (2026-10-06) : commit `ba19985`. Une instance de déplacement par tracker et une instance de glissement par
  condition et par bouton, remplies par des setters internes ; propriétés publiques toujours en lecture seule ; doc
  XML « lire pendant la frame, ne pas garder ». Écart avec l'étape prévue : ni `BaseMouseMovedEventArgs` ni
  `BaseMouseDraggedEventArgs` ne dérivent de `HandledByEventArgs` (`MouseEventArgs.cs:34`, `:257`, `SetHandled` vide),
  il n'y a donc aucun état « handled » à remettre à zéro. `MouseEventArgsReuseTests` (5) : déplacement rempli à chaque
  mouvement et nul souris immobile ; glissement rempli à chaque frame (position, départ, instant, bouton), une instance
  par condition ; glissement réel du curseur d'un `MGSlider` (valeur croissante, puis 100) ; allocation nulle des
  frames de déplacement et de glissement avec handler abonné (couvre aussi la `List` retirée en T1.7). Contre-épreuve :
  fichiers précédents, tests d'allocation rouges (44 208 et 152 000 octets) et tests « même instance » rouges (le
  changement visible), glissement du slider vert ; arbre remis à l'identique. `MGUI.Tests` 3146/3146, `MGUI.Samples`
  sans erreur.

### ✅ T1.10 — Liaison de données : abonnement direct dans la version WPF

- Objectif : D9.
- Fichiers : `MGUI/MGUI.Core/UI/DataBinding/DataBinding.cs`, `MGUI/MGUI.Tests/Architecture/DataBindingAllocationTests.cs`
  (ou fichier voisin, dans `DataBindingRegistryCollection`).
- Étapes :
  1. Retirer les branches `#if UseWPF` de l'abonnement et du désabonnement (`:194`, `:211`, `:235`, `:269`, `:408` et
     celles de `Dispose`) au profit du code `#else` existant ; retirer le `throw` WPF des gestionnaires (le filtre de
     nom reste) ; mettre à jour le commentaire d'en-tête (`:58-60`) et l'en-tête de `DataBindingAllocationTests`.
  2. Tests : notification d'une source liée (OneWay et TwoWay, chemin pointé avec objet intermédiaire remplacé) :
     la cible suit ; après `RemoveBinding`, plus aucune poussée ; allocation nulle d'une notification de bout en bout
     (setter qui notifie, poussée comprise) sur un type à chemin typé d'ADR-0016 ; contre-épreuve sur l'ancien code
     (environ 192 octets attendus).
- Validation : build ; tests verts ; suites de liaison (`Binding`, `Xaml`, `DataContext`) puis `MGUI.Tests` complète vertes.
- Commit (MGUI) : `perf(binding): subscribe to property changes directly instead of through WPF weak events`
- Note (2026-10-06) : commit `1166a5a`. Toutes les branches `#if UseWPF` d'abonnement et de désabonnement (chemin
  pointé, source, cible, `Dispose`) remplacées par le code `#else` existant ; `throw` WPF des gestionnaires retiré ;
  le bloc `using` conditionnel du haut reste (il ne sert pas qu'aux abonnements) ; commentaire d'en-tête et en-tête de
  `DataBindingAllocationTests` mis à jour. `DataBindingNotificationTests` (5, collection non parallèle) sur un
  `MGSlider` : OneWay, TwoWay, chemin pointé dont l'objet intermédiaire est remplacé, plus aucune poussée après
  `RemoveBinding`, allocation nulle d'une notification de bout en bout (setter puis slider). Contre-épreuve : ancien
  `DataBinding.cs`, 192 000 octets pour 1000 notifications (les 192 octets cités jusqu'ici), 4 tests de comportement
  verts ; arbre remis à l'identique. Écart de comportement à connaître : `PropertyChangedEventManager` transmettait
  aussi une notification au nom nul ou vide (« toutes les propriétés »), qui relançait la résolution d'un chemin
  pointé et levait une exception dans les gestionnaires de source et de cible ; l'abonnement direct l'ignore. Aucun
  code de MGUI ni du moteur n'en émet (`rg` sur `NotifyPropertyChanged(null|""|string.Empty)` et
  `PropertyChangedEventArgs(null|""|string.Empty)`). `MGUI.Tests` 3151/3151, `MGUI.Samples` sans erreur.

### ⚠️ T1.11 — Preuve de bout en bout, ADR et documentation MGUI

- Objectif : prouver l'objectif sur le chemin du slider et enregistrer D1–D10 et D8b côté MGUI.
- Fichiers : test de bout en bout (`MGUI.Tests/Architecture/`), `MGUI/Docs/decisions/0021-allocation-free-slider-drag.md`,
  `MGUI/Docs/decisions/README.md`, `MGUI/Docs/controls-architecture.md` (section Slider : événements, coût, texte en
  tampon, limites), `MGUI/Docs/input-architecture.md` (arguments réutilisés), doc de liaison existante si elle cite
  l'événement faible.
- Étapes : test qui fait glisser un slider placé dans une fenêtre (états souris simulés ; par image :
  `InputTracker.Update`, `UpdateHandlers` souris et clavier, `Update` du slider, qui met à jour ses handlers
  et lève `ValueChangedNonAlloc`, puis `DrawSelf` du slider) et mesure zéro allocation par image après chauffe,
  label masqué puis label à texte inchangé ; contre-épreuve en remettant un seul des anciens sites (par exemple
  l'abonnement à soi-même). Si la valeur ne suit pas sans `MGWindow.Update`, le dire dans la note, passer en ⚠️ et
  poser la question. ADR et docs.
- Validation : test vert ; suite `MGUI.Tests` complète verte ; liens et index relus.
- Commit (MGUI) : `docs(decisions): record the allocation-free slider drag (ADR-0021)` (le test part dans ce commit).
- Note (2026-10-06) : **bloquée par O3**. Test de bout en bout écrit (`SliderDragFrameAllocationTests`, deux variantes :
  label masqué, label au texte inchangé) : la valeur suit bien le glissement et le texte du label garde la même
  instance, mais chaque image alloue 320 octets, soit 5 bordures uniformes × 64 octets dans `ThicknessUtils.IsEmpty`
  (2 bordures du slider, 3 bordures de surcouche pendant le glissement). Sonde non commitée : avec `IsEmpty` réécrit sans
  LINQ, les deux variantes passent à 0 octet ; arbre remis à l'identique. Le test est mis de côté hors de l'arbre
  (scratchpad de la session) en attendant la réponse ; l'ADR et la doc sont écrits avec lui, une fois O3 tranché.

---

## Phase 2 — Moteur

### ✅ T2.1 — Pointeur de sous-module

- Objectif : le moteur pointe sur la tête de `chantier/mgui-slider-nonalloc` de MGUI ; mise à jour de ce plan pour
  T1.1 à T1.11.
- Fichiers : `MGUI` (pointeur), ce plan.
- Étapes : ligne de base `CasaEngine.Tests` sur le pointeur d'origine ; bascule du pointeur ; builds et tests.
- Validation : `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` sans erreur ; `CasaEngine.Tests` vert.
- Commit : `chore(submodules): point MGUI at the allocation-free slider drag`
- Note (2026-10-06) : pointeur `d3e0cd1` → `1166a5a` (tête de `chantier/mgui-slider-nonalloc` après T1.10 ; T1.11 bloquée
  par O3, un second pointeur suivra). Ligne de base sur `d3e0cd1` : `CasaEngine.MonoGame.sln` 0 erreur (218
  avertissements), `CasaEngine.Editor.MonoGame.sln` 0 erreur (193), `CasaEngine.Tests` 3159/3159. Après bascule : 0
  erreur (218 et 188 avertissements), aucun avertissement CS0618 ni sur `ValueChanged`, `CasaEngine.Tests` 3159/3159.
  Ce commit porte aussi les notes de T1.1 à T1.11 de ce plan.

### 🧪 T2.2 — Abonnés du moteur sur `ValueChangedNonAlloc`

- Objectif : D5 côté moteur.
- Fichiers : `CasaEngine.Editor/Controls/AnimationClipPreviewPanel.cs` (`:504`),
  `CasaEngine.Editor/Controls/MaterialAssetInspectorPanel.cs` (`:1286`),
  `CasaEngine.Editor/Controls/ParticlePreviewViewport.cs` (`:456`),
  `CasaEngine.Demos/Demos/DemoUI/BlendingControlsScreen.cs` (`:146`),
  `CasaEngine.Editor/Controls/Timeline/TimelineControl.cs` (`:147`, gestionnaire `:950`).
- Étapes : chaque abonnement passe à `ValueChangedNonAlloc`, corps inchangé ; le gestionnaire de la timeline prend
  `(float PreviousValue, float NewValue) e` à la place de `EventArgs<float> e`, corps inchangé.
- Validation : les deux solutions sans erreur ; `CasaEngine.Tests` vert ;
  `rg "\.ValueChanged\s*\+=" CasaEngine.Editor CasaEngine.Demos` ne montre plus aucun abonnement sur un `MGSlider`
  ou une sous-classe ; ouverture de l'éditeur, glissement d'un slider de l'inspecteur de matériau et défilement
  horizontal de la timeline : 🧪 laissés à l'auteur si l'agent ne peut pas les piloter.
- Commit : `perf(editor): subscribe editor and demo sliders to the allocation-free value event`
- Note (2026-10-06) : 5 abonnements migrés, corps inchangés ; gestionnaire de la timeline sur le tuple ;
  `using MGUI.Shared.Helpers` de `TimelineControl.cs` retiré, devenu inutile (le projet éditeur compile sans lui). La
  recherche d'acceptation ne trouve plus d'abonnement `ValueChanged` sur un slider. Deux solutions 0 erreur (build
  incrémental), `CasaEngine.Tests` 3159/3159. Ouverture de l'éditeur, glissement d'un slider de l'inspecteur de matériau
  et défilement horizontal de la timeline : non pilotés par l'agent, 🧪 pour l'auteur.

---

## Phase 3 — Plans séparés

### ⏳ T3.1 — Plan séparé « `MGTextBlock` sans allocation »

- Objectif : D7. Écrire le plan du chantier séparé, sans code.
- Fichiers : `ai-agent/tasks/mgui-text-nonalloc-tasks.md` (nouveau), `ai-agent/README.md`.
- Étapes : partir de l'état vérifié « Texte » ci-dessus ; inventorier, par lecture et par une mesure d'allocation
  (test temporaire non commité), chaque allocation d'un changement de texte, de la mesure et du dessin
  (`ParseRuns`, tokenizer, `ParseLines`, coupure en mots, invalidation du layout, révélation progressive) ; poser les
  questions de conception à l'auteur en une fois ; rédiger le plan ; le soumettre.
- Validation : plan approuvé ou questions posées.
- Commit : `docs(plan): plan the allocation-free MGUI text pipeline`

### ⏳ T3.2 — Plan séparé « image du bureau sans allocation »

- Objectif : écrire le plan du chantier séparé qui rend une image complète du bureau sans allocation (resserrement
  du 2026-10-06 après la relecture du plan), sans code.
- Fichiers : `ai-agent/tasks/mgui-desktop-frame-nonalloc-tasks.md` (nouveau), `ai-agent/README.md`.
- Étapes : partir de l'état vérifié « Bureau » ci-dessus ; mesurer une image complète du bureau avec un slider
  glissé, une fois ce chantier livré (test temporaire non commité) ; inventorier chaque site restant
  (`MGDesktop.Update`, dessin du bureau, `MGWindow`, layout) ; poser les questions à l'auteur en une fois ; rédiger le
  plan ; le soumettre.
- Validation : plan approuvé ou questions posées.
- Commit : `docs(plan): plan the allocation-free MGUI desktop frame`

---

## Phase 4 — Vérification

### ⏳ T4.1 — Vérificateur frais

- Objectif : un `verifier` frais confirme ou réfute : (a) T1.1 et T1.3 (valeurs, ordre, allocation nulle) ;
  (b) `ValueChanged` inchangé pour un abonné qui y reste ; (c) `DrawSelf` du slider sans allocation ; (d) update
  d'un élément sans allocation et brushes tickés comme avant ; (e) entrée : ordre des handlers inchangé, arguments
  corrects image après image, allocation nulle ; (f) liaison : mêmes poussées, plus de poussée après retrait,
  allocation nulle ; (g) chemin du slider glissé sans allocation de bout en bout (T1.11) ; (h) les deux solutions du moteur compilent
  et les abonnés migrés se comportent comme avant.
- Validation : verdict CONFIRMED ; tout constat P0–P2 traité selon les règles d'`AGENTS.md`.
- Commit : `docs(plan): record the slider chantier verification`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Le plan audio (`audio-modern-tasks.md`, D20 et O24) n'est pas modifié ici pour éviter un conflit avec le chantier audio en cours ; à noter côté audio quand ce chantier sera fusionné. | — |
| O2 | D9 change l'ordre de passage d'une liaison par rapport aux autres abonnés du `PropertyChanged` d'un objet (l'événement faible WPF passait par son propre gestionnaire). Aucun code connu n'en dépend ; un test rouge de la suite en serait le signe, à remonter avant toute correction. | T1.10 |

| O3 | Allocation imprévue trouvée en T1.2 : `ThicknessUtils.IsEmpty` (`MGUI/MGUI.Shared/Helpers/ThicknessUtils.cs:14`) fait `@this.Sides().All(x => x == 0)`, 64 octets par appel. `MGUniformBorderBrush.Draw` l'appelle à chaque bordure dessinée (`MGUI.Core/UI/Brushes/BorderBrushes/MGUniformBorderBrush.cs:69`) : un `DrawSelf` du slider avec ses bordures par défaut alloue 128 octets (mesuré), plus 64 par bordure de surcouche quand il est survolé (déduit du code) ; 10 appelants dans MGUI. Correctif d'une ligne à comportement identique (`Left == 0 && Top == 0 && Right == 0 && Bottom == 0`), mais hors des sites du plan : **à confirmer par l'auteur avant de l'inclure**. Sans lui, la preuve de bout en bout de T1.11 (dessin du slider compris) ne peut pas être verte. **Confirmé en T1.11** : 320 octets par image de glissement, tous dans `IsEmpty` (0 octet avec le correctif d'une ligne, sonde non commitée). | T1.11 |

## Hors périmètre

- Le texte affiché qui change vraiment : plan séparé (D7, T3.1).
- Les arguments de défilement (`MouseTracker.cs:368`, une instance par défilement), d'appui, de relâchement, de clic
  et de touche clavier : créés à l'événement, pas à chaque image de glissement.
- Le travail des abonnés eux-mêmes (application d'une valeur de matériau, d'un paramètre de particules, etc.).
- Le reste d'une image complète du bureau (`MGDesktop.Update` hors détection d'activité, dessin du bureau,
  `MGWindow`, layout) : plan séparé (T3.2). `InputRouter.Route`, appelé par action et non par image.
