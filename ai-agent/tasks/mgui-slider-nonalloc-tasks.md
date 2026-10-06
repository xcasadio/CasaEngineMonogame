# Plan agent IA — `MGSlider` sans allocation quand sa valeur change

Plan d'exécution de la décision D20 de [audio-modern-tasks.md](audio-modern-tasks.md) (réponse à O24-6, 2026-10-06) :
l'allocation de `MGSlider` à chaque changement de valeur se corrige dans un chantier séparé, avant le panneau de
mixage éditable (tranche S6b), qui utilisera des sliders.
Les décisions D1 → D4 ci-dessous ont été arbitrées avec l'auteur le 2026-10-06 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Un `MGSlider` dont la valeur change (glissement, molette, clavier, `SetValue`) n'alloue plus rien de lui-même :
pas d'`EventArgs` quand personne n'écoute l'événement historique, un événement additif sans allocation pour les
nouveaux abonnés (le futur panneau de mixage), pas de chaîne reconstruite quand le texte affiché de la valeur ne
change pas, et plus de liste ni de LINQ dans `DrawSelf` pendant le survol ou le glissement. Les abonnés existants
de `ValueChanged` (éditeur, démo, samples MGUI) continuent de fonctionner sans modification.

## État vérifié du dépôt (2026-10-06)

- Worktree réparé en début de session : `git rev-parse --show-toplevel` rendait le gitdir ; `core.worktree` posé
  pour ce seul worktree (`config.worktree`), sous-modules `MGUI` et `NvgSharp` initialisés depuis le checkout local.
- Branche moteur `chantier/mgui-slider-nonalloc` créée depuis `main` (`6f06298f`, qui contient D20). Arbre propre.
  Pointeur `MGUI` = `d3e0cd1` = `develop` de MGUI (`git submodule status`).
- `MGUI/MGUI.Core/UI/MGSlider.cs` :
  - `SetValue` (`:106-117`) lève `ValueChanged?.Invoke(this, new(Previous, Value))` (`:114`) : un
    `EventArgs<float>` par changement. `EventArgs<TProperty>` est une classe à propriétés en lecture seule
    (`MGUI/MGUI.Shared/Helpers/EventArgs.cs:23-33`).
  - Le constructeur s'abonne à son propre événement (`ValueChanged += (sender, e) => UpdateValueLabelText();`, `:723`),
    donc l'événement n'est jamais nul et l'allocation a lieu même sans abonné extérieur. Cet abonnement est le premier
    de la liste : le texte de la valeur est à jour avant que les abonnés extérieurs soient appelés.
  - `UpdateValueLabelText` (`:650-656`) construit `Value.ToString(ValueLabelFormat ?? "F0")` à chaque changement quand
    `ShowValueLabel` est vrai, même si le texte affiché ne change pas. `MGTextBlock.SetTextCore` ignore une chaîne
    identique (`MGUI/MGUI.Core/UI/MGTextBlock.cs`, test `_Text != Value`), donc seule la chaîne est gaspillée.
  - `DrawSelf` alloue une `List<Rectangle>` puis `Where(...).ToList()` à chaque image pendant le survol, l'appui ou
    le glissement (`:1050` horizontal, `:1133` vertical).
  - `NotifyPropertyChanged(nameof(Value))` n'alloue pas : arguments mis en cache par nom
    (`MGUI/MGUI.Shared/Helpers/ViewModelBase.cs:15-23`).
- Aucun utilitaire de formatage dans un tampon dans MGUI (`rg "TryFormat|stackalloc char" MGUI.Core MGUI.Shared` : rien).
- Précédents MGUI d'événement sans allocation par type valeur : `EventHandler<float> SplitRatioDragged`
  (`MGUI.Core/UI/Docking/Controls/MGDockSplitterBar.cs:122`),
  `EventHandler<(MGTheme PreviousTheme, MGTheme Theme)> OnDefaultThemeChanged` (`MGUI.Core/UI/MGResources.cs:544`).
- Abonnés existants de `MGSlider.ValueChanged`, tous lisent `NewValue` pendant l'appel sans garder l'argument :
  moteur `CasaEngine.Editor/Controls/AnimationClipPreviewPanel.cs:504`, `MaterialAssetInspectorPanel.cs:1286`,
  `ParticlePreviewViewport.cs:456`, `CasaEngine.Demos/Demos/DemoUI/BlendingControlsScreen.cs:146` ;
  MGUI `MGUI.Samples/Controls/Slider.xaml.cs:26`, `:31`, `:36`, `MGUI.Samples/Features/AnimationDemo.xaml.cs:121`,
  `:122`, `:132`, `:255`, `:306`. Sous-classe : `CasaEngine.Editor/Controls/Timeline/TimelineHorizontalScrollBar.cs`
  (aucun abonnement).
- Tests MGUI : `MGUI/MGUI.Tests/AllocationWindow.cs` (fenêtre de mesure à contexte d'allocation vide) ; précédent de
  test d'allocation de `DrawSelf` : `MGUI.Tests/Architecture/AnimatedImageTests.cs:410-414`.
  `MGUI.Tests/Architecture/ResolvedPilotWriteSitesTests.cs:90` autorise la ligne 675 de `MGSlider.cs` par son numéro :
  toute ligne ajoutée au-dessus doit y être reportée.
- MGUI : plans dans `MGUI/Docs/Tasks/`, ADR en anglais dans `MGUI/Docs/decisions/` (dernière : ADR-0020), docs
  d'architecture en français sans accents.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D0 | (D20 du plan audio) L'allocation de `MGSlider` se corrige dans ce chantier séparé, dans le sous-module MGUI. |
| D1 | Nouvel événement additif `EventHandler<(float PreviousValue, float NewValue)>`, levé juste après `ValueChanged` avec le même couple de valeurs. Le slider ne s'abonne plus à lui-même : il met son texte à jour directement, au même moment qu'avant (avant tout abonné). `ValueChanged` garde sa signature, n'est pas marqué `[Obsolete]`, et n'alloue que s'il a des abonnés. Les abonnés existants ne sont pas migrés (auteur, 2026-10-06). |
| D2 | Nom de l'événement : `ValueChangedNonAlloc` (auteur, 2026-10-06). |
| D3 | Texte de la valeur : `float.TryFormat` (API .NET de base, aucune dépendance) dans un `char[]` privé du slider, comparé au texte affiché ; une chaîne n'est construite que si le texte affiché change (auteur, 2026-10-06). |
| D4 | L'allocation de `DrawSelf` pendant le survol ou le glissement est corrigée dans une tâche et un commit MGUI séparés (auteur, 2026-10-06). |

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
- **Build obligatoire** avant ✅ dès que du code est touché ; **tests** MGUI et moteur selon la tâche.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session. `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, commits et ADR en anglais ; docs d'architecture MGUI dans la langue du fichier.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds (input, draw).

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- MGUI : `dotnet build MGUI/MGUI.Tests/MGUI.Tests.csproj`, `dotnet build` du projet `MGUI.Samples` sans erreur ;
  `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` vert (ligne de base relevée en T1.1 avant toute modification).
- Moteur : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` sans erreur ;
  `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` vert (ligne de base relevée en T2.1).
- Smoke : `MGUI.Samples` lancé une fois. Glissement visuel d'un slider (sample « Slider », inspecteur de matériau de
  l'éditeur) : 🧪 laissé à l'auteur si l'agent ne peut pas le piloter.
- Vérificateur frais (`verifier`) sur l'ensemble avant de déclarer le chantier terminé (T3.1).

---

## Phase 0 — Plan

### ✅ T0.1 — Branche moteur et plan

- Objectif : ce plan, sa ligne dans `ai-agent/README.md`, la branche moteur.
- Fichiers : `ai-agent/tasks/mgui-slider-nonalloc-tasks.md`, `ai-agent/README.md`.
- Validation : relecture par l'auteur, approbation avant toute modification de code.
- Commit : `docs(plan): plan the allocation-free MGUI slider value change`

---

## Phase 1 — MGUI (sous-module, branche `chantier/mgui-slider-nonalloc`)

### ⏳ T1.1 — `ValueChangedNonAlloc`, plus d'abonnement à soi-même, texte formaté dans un tampon

- Objectif : D1, D2, D3.
- Fichiers : `MGUI/MGUI.Core/UI/MGSlider.cs`, `MGUI/MGUI.Tests/Controls/SliderValueChangedTests.cs` (nouveau),
  `MGUI/MGUI.Tests/Architecture/ResolvedPilotWriteSitesTests.cs` (numéro de ligne reporté),
  `MGUI/MGUI.Samples/Controls/Slider.xaml.cs` (le premier slider du sample passe à `ValueChangedNonAlloc`, règle MGUI :
  toute nouvelle feature est montrée dans un sample).
- Étapes :
  1. Branche MGUI `chantier/mgui-slider-nonalloc` depuis `develop` ; ligne de base `dotnet test` de `MGUI.Tests`.
  2. `SetValue` : après `NotifyPropertyChanged(nameof(Value))`, appeler `UpdateValueLabelText()` directement, puis
     lever `ValueChanged` (inchangé, n'alloue que s'il a des abonnés) puis `ValueChangedNonAlloc` avec le même couple
     de valeurs ; supprimer l'abonnement du constructeur (`:723`). Doc XML des deux événements : coût de l'un,
     absence d'allocation de l'autre, ordre de levée.
  3. `UpdateValueLabelText` : `Value.TryFormat` dans un `char[]` privé avec le format courant ; si le résultat est égal
     (`SequenceEqual`) au texte affiché, ne rien faire ; sinon construire la chaîne depuis le tampon ; si le tampon est
     trop petit, repli sur `ToString` (comportement actuel).
  4. Tests : `ValueChanged` et `ValueChangedNonAlloc` reçoivent les bons ancien et nouveau couples (y compris valeur
     bornée et valeur discrète), dans cet ordre, une seule fois, et rien pour une valeur identique ; le texte de la
     valeur suit la valeur, le format et `ShowValueLabel`, et il est déjà à jour quand un abonné de `ValueChanged` est
     appelé ; allocation nulle sur des changements répétés (fenêtre ouverte par `AllocationWindow.Start()`, après
     chauffe) sans abonné, avec un abonné de `ValueChangedNonAlloc`, et avec le texte affiché dont la chaîne ne change
     pas (format `F0`, valeurs 3,2 et 3,4).
  5. Contre-épreuve : remettre temporairement l'abonnement à soi-même, puis l'ancien `ToString` ; les tests
     d'allocation doivent échouer ; arbre remis à l'identique.
- Validation : build `MGUI.Tests` et `MGUI.Samples` ; nouveaux tests verts ; suite `MGUI.Tests` complète verte.
- Commit (MGUI) : `feat(slider): raise an allocation-free value change event and format the value label without allocating`

### ⏳ T1.2 — `DrawSelf` sans liste ni LINQ pendant le survol et le glissement

- Objectif : D4.
- Fichiers : `MGUI/MGUI.Core/UI/MGSlider.cs`, `MGUI/MGUI.Tests/Controls/SliderValueChangedTests.cs` (ou fichier de
  test voisin), `ResolvedPilotWriteSitesTests.cs` si le numéro de ligne bouge encore.
- Étapes :
  1. Remplacer les deux `new List<Rectangle>() { ... }.Where(...).ToList()` (`:1050`, `:1133`) par deux tests sur
     `Rectangle.Empty` qui dessinent les mêmes morceaux, dans le même ordre, avec les mêmes brushes et épaisseurs.
  2. Test d'allocation nulle d'un `DrawSelf` répété dans l'état qui dessine la surcouche (survol ou glissement), sur le
     modèle de `AnimatedImageTests.cs:410-414`, en horizontal et en vertical. Si le runtime de test ne permet pas
     d'atteindre cet état sans modifier MGUI hors tâche, le dire dans la note et garder la preuve par lecture du code.
- Validation : build ; test vert, et rouge avec l'ancien code (contre-épreuve) ; suite `MGUI.Tests` complète verte.
- Commit (MGUI) : `perf(slider): draw the hover overlay without allocating a list each frame`

### ⏳ T1.3 — ADR et documentation MGUI

- Objectif : enregistrer D1–D3 dans MGUI (décision d'API publique) et documenter l'événement.
- Fichiers : `MGUI/Docs/decisions/0021-allocation-free-slider-value-change.md`, `MGUI/Docs/decisions/README.md`,
  `MGUI/Docs/controls-architecture.md` (courte section Slider : les deux événements, leur coût, le texte en tampon,
  limites).
- Validation : liens et index relus.
- Commit (MGUI) : `docs(decisions): record the allocation-free slider value change (ADR-0021)`

---

## Phase 2 — Moteur

### ⏳ T2.1 — Pointeur de sous-module

- Objectif : le moteur pointe sur la tête de `chantier/mgui-slider-nonalloc` de MGUI ; mise à jour de ce plan pour
  T1.1 à T1.3.
- Fichiers : `MGUI` (pointeur), ce plan.
- Étapes : ligne de base `CasaEngine.Tests` sur le pointeur d'origine ; bascule du pointeur ; builds et tests.
- Validation : `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` sans erreur ni nouvel avertissement sur
  les abonnés de `ValueChanged` ; `CasaEngine.Tests` vert ; aucun fichier de l'éditeur ni de la démo modifié.
- Commit : `chore(submodules): point MGUI at the allocation-free slider value change`

---

## Phase 3 — Vérification

### ⏳ T3.1 — Vérificateur frais

- Objectif : un `verifier` frais confirme ou réfute : (a) changements de valeur répétés sans allocation dans les trois
  cas de T1.1 ; (b) `ValueChanged` inchangé pour ses abonnés (valeurs, ordre, texte déjà à jour) ; (c) `DrawSelf` sans
  allocation pendant le survol ou le glissement ; (d) les deux solutions du moteur compilent sans toucher les abonnés.
- Validation : verdict CONFIRMED ; tout constat P0–P2 traité selon les règles d'`AGENTS.md`.
- Commit : `docs(plan): record the slider chantier verification`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Le plan audio (`audio-modern-tasks.md`, D20 et O24) n'est pas modifié ici pour éviter un conflit avec le chantier audio en cours ; à noter côté audio quand ce chantier sera fusionné. | — |

## Hors périmètre

- Migrer les abonnés existants (éditeur, démo, samples MGUI autres que le premier slider du sample) vers
  `ValueChangedNonAlloc` : ils gardent 24 octets par changement.
- Allocation quand le texte affiché change vraiment : la nouvelle chaîne et la mise à jour de `MGTextBlock` restent.
- `MouseTracker` alloue des arguments de déplacement et de glissement à chaque image de glissement
  (`MGUI/MGUI.Shared/Input/Mouse/MouseTracker.cs:376`, `:473`) : cadre d'entrée commun à tout MGUI.
- Liaison de données sur `Value` : la diffusion par les événements faibles WPF alloue environ 192 octets par
  notification (en-tête de `MGUI.Tests/Architecture/DataBindingAllocationTests.cs`).
- `MGColorSlider` lève le même genre d'`EventArgs<float>` (`MGUI/MGUI.Core/UI/Color/MGColorSlider.cs:226`).
- Les itérateurs `GetBorderBrushes` / `GetFillBrushes` de `MGSlider` (communs aux éléments MGUI).
