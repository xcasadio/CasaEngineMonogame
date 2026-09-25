# Plan agent IA — Boîtes de message MGUI dans l'éditeur

Plan d'exécution né d'une demande de l'auteur (2026-09-25) : la boîte native « Save changes to 'DialogueScreen' before
closing? » (fenêtre Windows, boutons « Oui / Non / Annuler » dans la langue du système) doit devenir une boîte MGUI,
et il doit en aller de même pour toutes les boîtes de dialogue de l'éditeur.
Les décisions D1 → D6 ci-dessous ont été arbitrées avec l'auteur le 2026-09-25 ; D7 → D9 sont proposées avec ce plan et
valent décision une fois le plan approuvé : **ce plan les applique, il ne les rediscute pas**.
**Approuvé par l'auteur le 2026-09-25, mode AUTO**, après deux relectures du vérificateur de plan (REVISE sur la boîte
enchaînée, corrigé dans T1.1, puis READY). Décisions : ADR-0039 (moteur) et MGUI ADR-0018.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Toutes les boîtes de message natives de l'éditeur (WinForms `MessageBox`, 13 appels) deviennent des boîtes MGUI : même
thème que l'éditeur, textes en anglais, boucle de jeu jamais bloquée. Le chantier livre :

- dans MGUI, un contrôle générique `MGMessageBox` affiché dans la surcouche modale du bureau, testé, avec un sample et
  sa documentation ;
- dans MGUI, la cause « fenêtre flottante fermée entière » transmise aux abonnés de `MGDockHost.PanelClosing` ;
- dans l'éditeur, une file de boîtes (une seule ouverte à la fois) et la conversion en asynchrone des trois questions
  d'enregistrement (fermer un écran modifié, quitter l'éditeur, ouvrir un autre monde).

Ce qu'il ne livre pas est dans « Hors périmètre ».

## État vérifié du dépôt (2026-09-25)

- Branche courante `main` ; `MGUI` sur `develop` propre en `c5d5a09`, pointeur du parent identique (`git submodule status`).
- Changement préexistant de l'auteur : `CasaEngine.Launcher/Program.cs` modifié, non indexé. **Ne jamais l'indexer.**
- Appels natifs dans l'éditeur (`rg "MessageBox|OpenFileDialog|SaveFileDialog|FolderBrowserDialog" CasaEngine.Editor`) :
  - messages OK : `GameEditor.cs:2211` (ouverture de projet), `:2231` (création de projet),
    `ContentBrowserPanel.cs:515` (erreur d'opération), `:521` (avertissement d'opération), `:1238` (Properties),
    `ProjectLauncherWindow.cs:141` (projet introuvable), `:210` (validation), `:222` (création du dossier) ;
  - confirmations Oui/Non : `ContentBrowserPanel.cs:1051` (supprimer un élément), `:1863` (supprimer N éléments) ;
  - Oui/Non/Annuler : `GameEditor.cs:2587` (fermeture d'un écran, `OnDockHostPanelClosing`), `:2675` (sortie,
    `OnExiting`), `:4247` (`ConfirmSaveBeforeOpeningWorld`) ;
  - sélecteurs natifs gardés (D2) : `GameEditor.cs:8343` (Export PNG), `ContentBrowserPanel.cs:1121` (Import),
    `ProjectLauncherWindow.cs:123` (Browse), `:182` (dossier du projet).
- Les trois questions d'enregistrement dépendent d'une réponse synchrone : `e.Cancel` de `PanelClosing`
  (`GameEditor.cs:2594-2597`), `args.Cancel` de `OnExiting` (`GameEditor.cs:2694-2697`), et le `bool` de
  `ConfirmSaveBeforeOpeningWorld` qui arrête `TryOpenWorldAsset` (`GameEditor.cs:4208-4212`).
  `ModifiedScreenCloseDecision.Decide(isModified, isAutomationActive, Func<Answer> askUser, Func<bool> trySave)`
  (`CasaEngine.Editor/History/ModifiedScreenCloseDecision.cs:48`), 7 tests (`CasaEngine.Tests/Editor/ModifiedScreenCloseDecisionTests.cs`).
- Sous automatisation, aucune question à la fermeture ni à la sortie (`GameEditor.cs:2586`, `:2659-2667`).
- MGUI : aucune boîte de message réutilisable. `MGDesktop.OverlayHost` (`MGOverlayHost`, `MGUI.Core/UI/MGOverlay.cs:16`)
  est une surcouche du bureau entier : `AddOverlay`, `TryOpen`, `TryClose`, `TryRemoveOverlay`, `IsModal` (défaut vrai),
  fond assombri `DefaultOverlayBackground`. Avec une surcouche modale active :
  `MGDesktop.IsBlockedByModalOrOverlay` bloque tout élément hors de la surcouche (`MGDesktop.cs:324-339`), et
  `MGDesktop.ShouldCaptureGameplayInput()` rend vrai (`MGDesktop.cs:621-624`), donc `IsEditorShellCapturingKeyboard()`
  coupe les raccourcis de l'éditeur (`GameEditor.cs:5907-5908`) et `IsEditorShellBlockingViewportPointer()` coupe le
  pointeur du viewport (`GameEditor.cs:5910-5919`).
- À l'inverse, `MGWindow.PushModalWindow` ne bloque que la fenêtre propriétaire (`MGDesktop.cs:338`,
  `MGUI.Tests/Modal/ModalBlockingTests.cs`) : une fenêtre flottante du docking, ajoutée par `AddNestedWindow`
  (`MGDockHost.cs:1556`), resterait cliquable.
- Docking : « Close Others » et « Close All » demandent panneau par panneau et continuent après un refus
  (`MGDockTabGroup.cs:597-631`) ; la fermeture d'une fenêtre flottante entière s'arrête au premier refus et annule la
  fermeture (`MGDockHost.cs:1559-1580`) ; `RemovePanel` ne lève pas `PanelClosing` mais lève `PanelRemoved`
  (`MGDockHost.cs:1119-1162`), dont `GameEditor.OnDockHostPanelRemoved` fait le ménage de l'écran (historique, état
  modifié, titres). `CancelEventArgs<T>` n'est pas scellée (`MGWindow.cs:19-27`) ; `MGFloatingDockWindow` est publique.
- `TryOpenEditorAsset` : les chemins utilisateur ignorent le retour (`GameEditor.cs:3655`, `:3674`, `:3783`) ; les
  chemins d'automatisation l'utilisent (`GameEditor.cs:7022`, `:7047`, `:7083`).
- `ContentBrowserPanel` et `ProjectLauncherWindow` reçoivent déjà la fenêtre principale `_mainWindow`
  (`GameEditor.cs:1296`, `:2185`).
- Règles MGUI : `MGUI/CLAUDE.md` renvoie à `MGUI/.github/copilot-instructions.md` (outils shell) ; les règles du
  parent s'appliquent (`AGENTS.md` §1). ADR de MGUI dans `MGUI/Docs/decisions/` (dernier : 0017) ; ADR du moteur dans
  `docs/decisions/` (dernier : 0038).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Les 13 boîtes de message natives de l'éditeur deviennent des boîtes MGUI (auteur, 2026-09-25). |
| D2 | Les 4 sélecteurs natifs de fichiers et de dossiers restent (Export PNG, Import, Browse et dossier du lanceur) (auteur, 2026-09-25). |
| D3 | La boîte de message réutilisable est un contrôle générique du sous-module MGUI, avec tests, sample et doc (auteur, 2026-09-25). |
| D4 | Plusieurs écrans modifiés fermés d'un coup (« Close Others », « Close All ») : une boîte par écran, l'une après l'autre ; Cancel garde cet écran et les questions continuent pour les suivants, comme aujourd'hui (auteur, 2026-09-25). |
| D5 | Fermer une fenêtre flottante entière : après Save réussi ou Don't Save, la fenêtre retente d'elle-même sa fermeture, ce qui pose la question de l'écran modifié suivant ; Cancel ou un enregistrement raté arrête. MGUI transmet cette cause dans `PanelClosing` (auteur, 2026-09-25). |
| D6 | Les trois questions d'enregistrement ont les boutons « Save », « Don't Save », « Cancel » (auteur, 2026-09-25). |
| D7 | `MGMessageBox` s'affiche dans la surcouche modale du bureau (`MGDesktop.OverlayHost`), pas en fenêtre modale d'une fenêtre : elle bloque tout le bureau, fenêtres flottantes comprises, et coupe les raccourcis et le pointeur du viewport de l'éditeur sans code de plus (faits ci-dessus). Proposée avec ce plan. |
| D8 | Une seule boîte ouverte à la fois : l'éditeur tient une file, la suivante s'ouvre quand la précédente se ferme. Pas de file dans MGUI. Proposée avec ce plan. |
| D9 | Règle d'automatisation inchangée : pas de question à la fermeture d'un écran ni à la sortie. Les confirmations de suppression gardent « Yes » / « No » ; les messages simples ont un seul bouton « OK ». Pas d'icône dans cette version (voir Hors périmètre). Proposée avec ce plan. |

## Règles d'exécution pour l'agent

- **Branches dédiées `chantier/mgui-message-boxes`** : dans le moteur depuis `main`, dans `MGUI` depuis `develop`. Ne jamais committer sur `main` ni sur `develop`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier. Une tâche MGUI a son commit dans `MGUI` ; la mise à jour de ce plan pour cette tâche part dans le commit moteur suivant (le pointeur de sous-module ou la tâche moteur suivante), en le disant dans sa note.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` et sur `develop` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant ✅ dès que du code est touché ; **tests** dès qu'une tâche touche du code testé. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ni aucune modification préexistante : `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/`, docs de `MGUI/Docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds (la boîte se construit une fois à l'ouverture, rien par frame) ; pas d'état global mutable nouveau (le service de boîtes est une instance passée par constructeur) ; échouer tôt sur un usage invalide.
- **Chantier à risque** (une erreur dans les flux asynchrones fait perdre des modifications non enregistrées) : vérificateur frais sur T1.2 + T3.1 ensemble (frontière MGUI/éditeur de la fermeture) et vérificateur frais de clôture sur l'ensemble avant de déclarer le chantier fait.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` : ligne de base de T0.1 plus les nouveaux tests, tout vert.
- `dotnet build MGUI/MGUI.Samples/MGUI.Samples.csproj` : sans erreur.
- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : sans erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : ligne de base de T0.1 plus les nouveaux tests ; aucun nouvel échec.
- `rg -n "System.Windows.Forms.MessageBox|FormsMessageBox|MessageBoxButtons|MessageBoxIcon|DialogResult" CasaEngine.Editor --glob "*.cs"` : aucun résultat à la fin de T3.3.
- Smoke manuel de l'auteur (éditeur réel, projet Alundra) : voir la liste de T4.1.

---

## Phase 0 — Préparation

### ✅ T0.1 — Branches, plan, ADR, lignes de base

- Objectif : ouvrir le chantier et enregistrer les décisions.
- Fichiers : `ai-agent/tasks/mgui-message-boxes-tasks.md` (ce plan), `ai-agent/README.md` (ligne du tableau),
  `docs/decisions/0039-editor-message-boxes-are-mgui.md` + `docs/decisions/README.md`,
  `MGUI/Docs/decisions/0018-modal-message-box-in-the-desktop-overlay.md` + `MGUI/Docs/decisions/README.md`.
- Étapes :
  1. Créer `chantier/mgui-message-boxes` dans `MGUI` (depuis `develop`) et dans le moteur (depuis `main`).
  2. Mesurer les lignes de base : `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` et
     `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` (nombres réussis/échoués notés ici, échecs préexistants nommés).
  3. ADR moteur (skill `adr`) : D1, D2, D4, D5, D6, D8, D9 et leurs raisons. ADR MGUI : D3, D7 et la cause de fermeture de D5.
  4. Copier ce plan, l'ajouter au tableau de `ai-agent/README.md`.
- Validation : fichiers relus ; `git diff --cached` ne contient que ces fichiers.
- Commit MGUI : `docs(decisions): record the modal message box design` ; commit moteur : `docs(plan): plan the MGUI message boxes`.
- Note (2026-09-25) : branches `chantier/mgui-message-boxes` créées (MGUI depuis `develop` `c5d5a09`, moteur depuis
  `main` `43688074`). Lignes de base : `MGUI.Tests` 3049/3049, `CasaEngine.Tests` 1888/1888, aucun échec préexistant.
  ADR-0039 (`docs/decisions/0039-editor-message-boxes-are-mgui.md`) et MGUI ADR-0018
  (`MGUI/Docs/decisions/0018-modal-message-box-in-the-desktop-overlay.md`), indexées. Le pointeur `MGUI` du moteur
  n'est pas mis à jour ici : il suit en T2.1.

---

## Phase 1 — MGUI

### 🧪 T1.1 — `MGMessageBox` (D3, D7)

- Objectif : un contrôle générique qui affiche un titre, un message et 1 à 3 boutons libellés, dans la surcouche modale
  du bureau, et rend l'indice du bouton choisi par un rappel.
- Fichiers : `MGUI/MGUI.Core/UI/MGMessageBox.cs` (nouveau), `MGUI/MGUI.Tests/MessageBox/MGMessageBoxTests.cs` (nouveau),
  une page de `MGUI/MGUI.Samples` enregistrée dans `Compendium` comme les autres, `MGUI/Docs/controls-architecture.md`.
- Contrat :
  - `public static MGMessageBox Show(MGDesktop desktop, string title, string message, IReadOnlyList<string> buttonLabels, int defaultButtonIndex, int cancelButtonIndex, Action<int> closed)` ;
    arguments invalides (bureau ou rappel nul, aucun bouton, plus de 3, indice hors plage) → exception immédiate ;
    `desktop.OverlayHost.IsModal == false` → `InvalidOperationException` (la boîte ne doit jamais être non bloquante).
  - Contenu construit une fois : titre, message avec retour à la ligne et largeur maximale, rangée de boutons à droite ;
    thème du bureau, aucune couleur codée en dur.
  - Clic sur un bouton, Entrée (bouton par défaut) ou Échap (bouton d'annulation) : ferme et retire la surcouche, puis
    appelle `closed` **exactement une fois** avec l'indice ; aucun abonnement laissé derrière.
  - Pas de bouton de fermeture de la surcouche (`ShowCloseButton = false`) : on ne sort que par un bouton, Entrée ou Échap.
  - Appeler `Show` depuis `closed` est permis (la file de l'éditeur, D8, ouvre la boîte suivante ainsi) : la nouvelle
    boîte devient la surcouche active, et le clic ou la touche qui a fermé la précédente ne lui arrive jamais.
- Étapes :
  1. Lire `MGOverlayHost` / `MGOverlay` (`MGUI.Core/UI/MGOverlay.cs`) : ouverture, fermeture, placement du contenu,
     `ZIndex` ; et les tests qui s'en servent (`MGUI.Tests/Focus/FocusArchitectureTests.cs`,
     `MGUI.Tests/Input/WindowActivationOnClickTests.cs`) pour le harnais.
  2. Trouver le chemin réel d'Entrée et d'Échap dans le bureau (`MGDesktop.TryHandleInputAction` et
     `UINavigationAction.Submit`/`Cancel`, `MGUI.Core/UI/Enums.cs:130-135`, ou le gestionnaire clavier) **et le chemin que
     l'éditeur alimente** ; le test pilote ce chemin-là. Si l'éditeur n'alimente aucun des deux → ⚠️ Blocked (O2).
  3. Écrire le contrôle, puis les tests : chaque bouton rend son indice ; Entrée → défaut ; Échap → annulation ; deux clics
     dans la même frame → un seul rappel ; pendant l'ouverture, `ShouldCaptureGameplayInput()` est vrai et un clic sur un
     bouton d'une **autre fenêtre racine** du bureau n'arrive pas ; après fermeture, `OverlayHost.Overlays` ne contient plus
     la boîte ; arguments invalides → exception ; **boîte enchaînée** : depuis `closed`, rappeler `Show`, une fois après
     un clic, une fois après Entrée, une fois après Échap, et vérifier à chaque fois que la seconde boîte est
     `OverlayHost.ActiveOverlay`, que son rappel n'a pas été appelé pendant cette mise à jour, et que
     `ShouldCaptureGameplayInput()` est toujours vrai après la mise à jour.
  4. Sample : trois boutons qui ouvrent un OK, un Yes/No, un Save/Don't Save/Cancel et affichent la réponse.
  5. Doc : section « Message box » dans `controls-architecture.md` (usage, file à tenir côté hôte, limites).
- Validation : `MGUI.Tests` vert (ligne de base + nouveaux) ; chaque nouveau test échoue si l'on retire la fermeture de
  la surcouche, le rappel ou la gestion d'Entrée/Échap (mutation notée) ; le test de la boîte enchaînée échoue sous au
  moins une mutation notée : appeler `closed` avant que la boîte ne soit retirée de `OpenOverlays`, ou laisser la même
  touche Entrée atteindre la seconde boîte ; `MGUI.Samples` construit. 🧪 pour le coup d'œil
  de l'auteur sur le sample si aucun lancement visuel n'est possible.
- Commit MGUI : `feat(ui): add a modal message box hosted in the desktop overlay`.
- Note (2026-09-25) : MGUI `e061e06`. `MGUI.Core/UI/MGMessageBox.cs` suit le contrat ; les boutons sont une sous-classe
  privée de `MGButton` qui envoie `UINavigationAction.Cancel` au bouton d'annulation.
  - **O2 levé** : l'éditeur ne touche ni à `UseRawNavigationInput` ni à `MGUIInputContext` (`rg` sur `CasaEngine*`), il
    passe donc par le chemin brut du bureau. Ce chemin envoie Entrée/Espace → `Submit` et Échap → `Cancel`
    (`MGDesktop.TryMapNavigationAction`) à l'élément qui a le focus (`UIFocusNavigationService.TryDispatchNavigationAction`).
    Le chemin sémantique (`TryHandleInputAction`) est testé aussi.
  - Précision du contrat : Entrée active le bouton qui a le focus. À l'ouverture, c'est le bouton par défaut ; Tab
    passe d'un bouton à l'autre, comme sous Windows. Le focus est ramené sur le bouton par défaut s'il quitte les boutons.
  - Écart de langue : `MGUI/Docs/controls-architecture.md` est rédigé en français sans accents. La section
    « MessageBox » suit donc la langue du fichier, pas la règle « anglais » de ce plan.
  - `ResolvedPilotWriteSitesTests` (ADR-0005 de MGUI) impose le setter étiqueté `SetPadding(…, UIValueResolutionSource.Default(…))`.
  - Défaut latent de MGUI trouvé, non corrigé ici (O4) : la boîte appelle `TryClose` avant `TryRemoveOverlay`.
  - Tests : 15 dans `MGUI.Tests/MessageBox/MGMessageBoxTests.cs` (dont la boîte enchaînée après un clic, après Entrée
    et après Échap, avec Entrée/Échap tenus 60 frames) et 5 dans `MessageBoxSampleTests.cs` (le XAML du sample se
    charge en mode strict, et les noms que lit le code-behind existent). `MGUI.Tests` 3069/3069 (ligne de base 3049) ;
    `MGUI.Samples` construit sans avertissement.
  - Mutations, chacune rougit au moins un test :
    - M1 : surcouche non retirée.
    - M2 : pas de rappel.
    - M3 : Échap non géré.
    - M4 : ni focus initial ni maintien du focus.
    - M5 : pas de maintien du focus.
    - M6 : rappel **avant** que la boîte quitte l'hôte ; il ne rougit que la boîte enchaînée, aux trois entrées.
    - M7 : pas de `ZIndex` au-dessus des autres surcouches.
    - M8 : pas de garde `IsOpen`. Elle a d'abord survécu ; le test `AButtonOfAnAnsweredBox_DoesNothing` l'attrape maintenant.
    - M9 : `TryRemoveOverlay` sans `TryClose`.
  - La mutation « la même Entrée atteint la seconde boîte » n'a pas de forme naturelle : chaque appui n'est envoyé
    qu'une fois, à un seul élément, et un bouton ne se déclenche qu'au relâchement d'un appui reçu par lui-même. Les
    60 frames d'Entrée tenue de la boîte enchaînée l'épinglent.
  - **Reste 🧪** : coup d'œil de l'auteur sur la page « MessageBox » du Compendium de `MGUI.Samples` (thème, tailles,
    anneau de focus du bouton par défaut) ; aucun lancement visuel n'est possible depuis cette session.
  - Ajout (2026-09-25, MGUI `bb0201d`), suite au vérificateur de T3.1 : `TheClickThatAnswersABox_DoesNotReachWhatLiesUnderIt`.
    La frame qui répond ferme aussi la surcouche, et les autres fenêtres se mettent à jour après elle dans cette même
    frame ; le relâchement ne doit pas atteindre l'élément sous le bouton. Le test rougit quand les boutons répondent par
    `OnLeftClicked` au lieu de `Command` : c'est la marque « handled » de la commande qui protège. `MGUI.Tests` 3083/3083.

### ✅ T1.2 — Cause « fenêtre flottante fermée entière » dans `PanelClosing` (D5)

- Objectif : l'abonné de `PanelClosing` sait qu'un refus a annulé la fermeture d'une fenêtre flottante entière, et
  laquelle, pour la retenter après la réponse.
- Fichiers : `MGUI/MGUI.Core/UI/Docking/Controls/MGDockHost.cs`, un nouveau type
  `DockPanelClosingEventArgs : CancelEventArgs<DockPanelNode>` à côté (même dossier), `MGUI/MGUI.Tests/Docking/PanelClosingVetoTests.cs`,
  `MGUI/Docs/controls-architecture.md`.
- Contrat : le type de l'événement ne change pas (`EventHandler<CancelEventArgs<DockPanelNode>>`, aucune rupture) ;
  `RaisePanelClosingVetoed` crée toujours un `DockPanelClosingEventArgs` ; sa propriété
  `MGFloatingDockWindow ClosingFloatingWindow` n'est renseignée que depuis `OnFloatingWindowClosing`, nulle partout ailleurs.
- Étapes :
  1. Ajouter le type et le paramètre ; renseigner la fenêtre depuis `OnFloatingWindowClosing` seulement.
  2. Tests : fenêtre flottante fermée entière → `ClosingFloatingWindow` est cette fenêtre ; onglet, « Close Others »,
     « Close All », onglet d'une fenêtre flottante, tiroir auto-masqué → nulle ; un abonné qui refuse puis appelle
     `RemovePanel` puis `TryCloseWindow()` sur la même fenêtre voit `PanelClosing` levé pour les panneaux restants et la
     fenêtre se ferme quand plus personne ne refuse ; `RemovePanel` du dernier panneau d'une fenêtre flottante : noter
     par un test si la fenêtre se ferme d'elle-même (fait dont T3.1 a besoin).
  3. Doc : compléter la section `PanelClosing` de `controls-architecture.md`.
- Validation : `MGUI.Tests` vert ; les 10 tests existants de `PanelClosingVetoTests` inchangés et verts ; le test de la
  fenêtre flottante échoue si l'on ne passe pas la fenêtre (mutation notée).
- Commit MGUI : `feat(docking): tell a panel-closing subscriber which floating window is closing`.
- Note (2026-09-25) : MGUI `2e037f1`.
  - `DockPanelClosingEventArgs` (même dossier que `MGDockHost`) : `RaisePanelClosingVetoed(panel, closingFloatingWindow = null)`
    crée toujours ce type. Seul `OnFloatingWindowClosing` passe la fenêtre.
  - **Écart au plan, dans le périmètre** : le test de la chaîne a montré que `DetachToFloating` retire le panneau du
    registre de l'hôte (`MGDockHost.cs`, `_panelRegistry.Remove(panel.Id)` dans `DetachToFloating`). `RemovePanel(id)`
    rend donc `false` pour un panneau flottant, ce qui contredit le fait rapporté par la reconnaissance. L'éditeur
    n'avait aucune voie publique pour fermer un écran flottant après sa réponse (`CloseFloatingPanel` est interne).
    Ajout additif : `MGDockHost.ClosePanel(DockPanelNode)`. Pour un panneau ancré ou auto-masqué, il passe par
    `RemovePanel` ; pour une fenêtre flottante suivie par l'hôte, il appelle `MGFloatingDockWindow.ClosePanelWithoutVeto`,
    la suite de sa fermeture d'onglet après le veto, désormais partagée. Il ne lève pas `PanelClosing`. Consigné dans
    MGUI ADR-0018 et la doc ; T3.1 appelle `ClosePanel` au lieu de `RemovePanel`.
  - Tests ajoutés dans `PanelClosingVetoTests` (13) : la cause sur chaque chemin (onglet, « Close Others », « Close
    All », onglet flottant ancré au modèle, onglet flottant autonome, tiroir auto-masqué, fenêtre entière), la chaîne
    refus → `ClosePanel` → `TryCloseWindow()` jusqu'à la fermeture de la fenêtre (le dernier `ClosePanel` ferme la
    fenêtre elle-même), `RemovePanel` aveugle aux panneaux flottants, et `ClosePanel` ancré, auto-masqué, flottant avec
    d'autres panneaux et inconnu. Les 10 tests existants sont inchangés et verts. `MGUI.Tests` 3082/3082.
  - Mutations, chacune rougit au moins un test : N1 la fenêtre non transmise (la cause de la fenêtre entière et la
    chaîne) ; N2 un `CancelEventArgs` simple (les 8 tests de cause) ; N3 `ClosePanel` sans les fenêtres flottantes ;
    N4 `ClosePanel` sans le registre.

---

## Phase 2 — Éditeur : la file et les messages simples

### ✅ T2.1 — Pointeur de sous-module

- Objectif : le moteur référence le `MGUI` de T1.1 et T1.2.
- Fichiers : `MGUI` (pointeur), ce plan (notes de T1.1 et T1.2).
- Validation : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` sans erreur ;
  `CasaEngine.Tests` sans nouvel échec.
- Commit : `chore(submodules): point MGUI at the message box and the floating-window close cause`.
- Note (2026-09-25) : pointeur `MGUI` sur `2e037f1` (T1.1 `e061e06`, T1.2 `2e037f1`, ADR `9224a2a`). `dotnet build
  CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` : 0 erreur ; `CasaEngine.Tests` 1888/1888 (ligne de base
  1888). Ce commit porte aussi les notes de T1.1 et T1.2.

### ✅ T2.2 — File de boîtes de l'éditeur (D8)

- Objectif : un service d'instance qui ouvre les `MGMessageBox` l'une après l'autre.
- Fichiers : `CasaEngine.Editor/Controls/EditorMessageBoxes.cs` (nouveau), sa partie pure (file) testable sans MGUI,
  `CasaEngine.Tests/Editor/EditorMessageBoxQueueTests.cs` (nouveau), `CasaEngine.Editor/GameEditor.cs` (création du
  service à côté de `_desktop` et `_mainWindow`).
- Contrat : `Show(title, message, labels, defaultIndex, cancelIndex, Action<int>)` et des raccourcis `ShowError`,
  `ShowWarning`, `ShowInfo` (un bouton « OK », rappel optionnel), `AskYesNo` (« Yes » / « No »), `AskSave`
  (« Save » / « Don't Save » / « Cancel », D6) ; une seule boîte ouverte, les suivantes attendent dans l'ordre d'arrivée ;
  `HasPendingOrOpen` pour les appelants qui doivent éviter un doublon. La boîte suivante s'ouvre depuis le rappel
  `closed` de la précédente, cas couvert par le test « boîte enchaînée » de T1.1.
- Validation : tests de la file (ordre, une seule ouverte, la suivante s'ouvre à la fermeture de la précédente, rappel
  une fois) ; build des deux solutions ; `CasaEngine.Tests` sans nouvel échec.
- Commit : `feat(editor): add a queue of MGUI message boxes`.
- Note (2026-09-25) : `EditorMessageBoxQueue` (pure) et `EditorMessageBoxes`, instance créée dans
  `GameEditor.Initialize` juste après `_desktop` (champ `_messageBoxes`). Écart au contrat : `ShowError`, `ShowWarning` et
  `ShowInfo` sont réunis en `ShowMessage`, puisque sans icône (D9) les trois seraient identiques. `AskYesNo` : Entrée →
  Yes, comme le bouton par défaut de la boîte native ; `AskSave` rend `EditorSaveAnswer`. La file valide la demande à
  l'entrée. Un présentateur qui lève ou une réponse qui lève ne bloquent pas la file. Une question posée depuis une
  réponse passe après celles qui attendaient déjà. Tests : 9 dans `CasaEngine.Tests/Editor/EditorMessageBoxQueueTests.cs`.
  Mutations Q1 à Q5 (pas de garde de réponse unique, présentateur qui laisse la file ouverte, suite seulement si la
  réponse réussit, suite toujours montrée, `Enqueue` qui montre même si une boîte est ouverte) : chacune rougit au moins
  un test. Les deux solutions compilent sans erreur ; `CasaEngine.Tests` 1897/1897.

### 🧪 T2.3 — Messages et confirmations de suppression (D1, D9)

- Objectif : les 8 messages OK et les 2 confirmations de suppression passent par la file.
- Fichiers : `CasaEngine.Editor/GameEditor.cs` (`QueueProjectOpen`, `QueueProjectCreate` : le lanceur se rouvre dans le
  rappel de la boîte, pas avant), `CasaEngine.Editor/Controls/ContentBrowserPanel.cs` (`:515`, `:521`, `:1051`, `:1238`,
  `:1863` ; la suppression s'exécute dans le rappel « Yes »), `CasaEngine.Editor/ProjectLauncher/ProjectLauncherWindow.cs`
  (`:141`, `:210`, `:222`) ; le service est passé par constructeur à `ContentBrowserPanel` et `ProjectLauncherWindow`.
- Étapes : remplacer chaque appel ; retirer les alias `FormsMessageBox`, `FormsMessageBoxButtons`, `FormsMessageBoxIcon`
  et `FormsDialogResult` devenus inutiles ; garder `FormsOpenFileDialog`, `FormsClipboard`, et dans le lanceur les
  sélecteurs natifs (D2).
- Validation : build des deux solutions ; `CasaEngine.Tests` sans nouvel échec ;
  `rg -n "MessageBox.Show" CasaEngine.Editor --glob "*.cs"` ne rend plus que `GameEditor.cs` (les trois questions
  d'enregistrement). 🧪 pour l'auteur : supprimer un élément (Yes et No), Properties, un projet récent disparu, un
  nouveau projet sans nom.
- Commit : `feat(editor): show editor messages and delete confirmations in MGUI`.
- Note (2026-09-25) : les 10 appels passent par `EditorMessageBoxes`.
  - Les suppressions (un ou N éléments) s'exécutent dans le rappel « Yes » (`DeleteItem`, `DeleteItems`).
  - Après une erreur d'ouverture ou de création de projet, le lanceur se rouvre dans le rappel de la boîte.
  - Les titres des boîtes natives sont gardés (« Content Browser », « File Not Found », « Validation », « Error »,
    « Properties - <nom> »).
  - API : les constructeurs publics de `ContentBrowserPanel` et `ProjectLauncherWindow` gardent leur signature et
    créent leur propre file. Seuls les constructeurs internes reçoivent la file partagée de `GameEditor`.
  - Alias `FormsMessageBox`, `FormsMessageBoxButtons` et `FormsMessageBoxIcon` retirés. `FormsDialogResult` est gardé
    (sélecteur d'import), tout comme `using System.Windows.Forms` du lanceur (sélecteurs natifs, D2).
  - `rg "MessageBox.Show" CasaEngine.Editor` ne rend plus que les trois questions d'enregistrement de `GameEditor.cs`
    et `MGMessageBox.Show` dans `EditorMessageBoxes.cs`.
  - Les deux solutions compilent sans erreur ; `CasaEngine.Tests` 1897/1897. Aucun test ajouté : ce sont des appels
    d'UI qui demandent le runtime GPU de l'éditeur ; la file est testée en T2.2.
  - **Reste 🧪** : les vrais clics de l'auteur (liste de la tâche).

---

## Phase 3 — Éditeur : les questions d'enregistrement en asynchrone

### 🧪 T3.1 — Fermer un écran modifié (D4, D5, D6)

- Objectif : `OnDockHostPanelClosing` refuse toujours la fermeture d'un écran modifié hors automatisation, pose la
  question, puis ferme par code selon la réponse.
- Fichiers : `CasaEngine.Editor/History/ModifiedScreenCloseDecision.cs`, `CasaEngine.Tests/Editor/ModifiedScreenCloseDecisionTests.cs`,
  `CasaEngine.Editor/GameEditor.cs`.
- Étapes :
  1. `ModifiedScreenCloseDecision` sépare « faut-il demander ? » (modifié, automatisation) de « que faire de la
     réponse ? » (Save et enregistrement réussi → fermer ; Save raté → garder ; Don't Save → fermer ; Cancel → garder),
     sans délégué synchrone. Les 7 cas existants sont repris, plus « Save raté » et « Cancel » sur chacune des deux entrées.
  2. `OnDockHostPanelClosing` : écran modifié hors automatisation → `e.Cancel = true` et `AskSave` pour ce panneau, sauf
     si une question est déjà en attente ou ouverte pour ce panneau. Réponse qui ferme → `ClosePanel(panel)` (corrigé
     en T1.2 : `RemovePanel` ne voit pas un panneau flottant ; le ménage passe par `PanelRemoved`, comme avant) ; puis,
     si `e` était un `DockPanelClosingEventArgs` avec
     `ClosingFloatingWindow` et que cette fenêtre est encore ouverte, `TryCloseWindow()` sur elle (D5). Réponse qui garde
     → rien, pas de nouvel essai. Réponse pour un panneau qui n'existe plus → ignorée.
- Validation : tests de la décision (chaque nouveau cas échoue sous mutation) ; build ; `CasaEngine.Tests` sans nouvel
  échec ; vérificateur frais sur T1.2 + T3.1. 🧪 pour l'auteur : un onglet (Save, Don't Save, Cancel) ; « Close All »
  avec deux écrans modifiés (Cancel sur le premier le garde et la question du second vient) ; une fenêtre flottante avec
  deux écrans modifiés (Save puis Don't Save → la fenêtre se ferme ; Cancel → elle reste) ; le tiroir auto-masqué.
- Commit : `feat(editor): ask to save a closing screen in an MGUI message box`.
- Note (2026-09-25) :
  - `ModifiedScreenCloseDecision` gagne `NeedsAnswer` et `ApplyAnswer`, ajoutés sans casser l'API : `Decide` reste
    public, les réutilise, et ses 7 tests sont inchangés. Il sert encore à `OnExiting` jusqu'à T3.2.
  - Le flux asynchrone vit dans `ModifiedScreenCloseCoordinator`, une classe pure (`CasaEngine.Editor/History`) : refus
    immédiat et question unique par écran ; à la réponse, rien si l'écran a disparu, sinon `ApplyAnswer`, puis
    fermeture par code, puis nouvelle tentative de la fenêtre flottante seulement si l'écran a été fermé.
  - `GameEditor.OnDockHostPanelClosing` ne fait que brancher : `AskSave` « Close Screen », `ClosePanel(panel)`, et
    `TryCloseWindow()` sur `ClosingFloatingWindow` si l'hôte la suit encore.
  - Tests : 9 dans `ModifiedScreenCloseDecisionTests` (`NeedsAnswer` et `ApplyAnswer`) et 15 dans
    `ModifiedScreenCloseCoordinatorTests`. Ceux-ci couvrent Save, Save raté, Don't Save, Cancel, l'écran disparu,
    la double fermeture, la double réponse, « Close All » avec Cancel sur un écran, et la fenêtre flottante entière
    avec ses trois enchaînements.
  - Mutations C1 à C6 : chacune rougit au moins un test (pas de dédoublonnage, pas de contrôle d'écran ouvert, nouvelle
    tentative même si l'écran est gardé, question jamais soldée, fermeture jamais refusée, automatisation ignorée).
  - Les deux solutions compilent sans erreur ; `CasaEngine.Tests` 1921/1921.
  - **Reste 🧪** : les clics de l'auteur (liste ci-dessus).
- **Vérificateur frais sur T1.2 + T3.1 (2026-09-25) : CONFIRMED**, aucun constat P0–P2. Il a vérifié MGUI `2e037f1` et
  le moteur `eaf59110`.
  - Il a reproduit : `MGUI.Tests` 3082/3082, `CasaEngine.Tests` 1921/1921, les deux solutions sans erreur.
  - Chaque chemin utilisateur passe par le veto avec la bonne cause.
  - `ClosePanel` refait la fermeture utilisateur d'après le veto, y compris la fermeture immédiate d'une fenêtre
    flottante vidée.
  - `OnDockHostPanelRemoved` fait le ménage comme avant.
  - Rien n'est réentrant ni périmé quand la réponse arrive dans la mise à jour de MGUI.
  - Aucun chemin ne ferme sans Save ni Don't Save.
  - Trois avis P4, reportés (O5, O6, O7). Un point non vérifiable par lecture, « le clic qui répond atteint-il ce qui est
    dessous ? », est désormais épinglé par un test MGUI (note de T1.1).

### 🧪 T3.2 — Quitter avec des écrans modifiés (D6)

- Objectif : `OnExiting` annule la sortie, pose la question « Quit » qui liste les écrans, puis relance `Exit()`.
- Fichiers : `CasaEngine.Editor/GameEditor.cs`.
- Étapes : champ `_exitConfirmed` ; `OnExiting` avec `_exitConfirmed` → `base.OnExiting` directement ; sinon écrans
  modifiés hors automatisation → `args.Cancel = true` et une seule question à la fois (une seconde demande de sortie
  pendant la question ne l'empile pas). Save → `SaveDirtyScreenDocuments` ; tous enregistrés → `_exitConfirmed = true`
  puis `Exit()` ; sinon avertissement et l'éditeur reste. Don't Save → `_exitConfirmed = true` puis `Exit()`. Cancel → rien.
  Branche d'automatisation inchangée.
- Validation : build ; `CasaEngine.Tests` sans nouvel échec ; une sortie automatisée par une option d'automatisation
  existante qui appelle `Exit()` (`GameEditor.cs:6294` ou `:6501`) sort seule, code 0. 🧪 pour l'auteur : File > Exit et
  la croix de la fenêtre, chacun avec Save, Don't Save, Cancel (seul un lancement réel prouve que `Exit()` relancé après
  un `Cancel` ferme bien l'éditeur sous DesktopGL).
- Commit : `feat(editor): ask to save modified screens before quitting in an MGUI message box`.
- Note (2026-09-25) :
  - Le flux vit dans `ModifiedScreensExitCoordinator`, classe pure de `CasaEngine.Editor/History`. Il annule la
    sortie, pose une seule question « Quit » avec la liste des écrans, et ne la repose pas si on quitte de nouveau
    pendant l'attente. Save enregistre tout, puis ne sort que si plus rien n'est modifié. Don't Save sort. Pour sortir,
    il appelle `Exit()`, et laisse passer la sortie suivante, une seule fois : si elle n'avait pas lieu, la prochaine
    redemanderait. Sous automatisation, rien n'est demandé et le journal liste les écrans abandonnés, comme avant.
  - `GameEditor.OnExiting` ne fait que brancher.
  - `ModifiedScreenCloseDecision.Decide` n'a plus d'appelant : il est marqué `[Obsolete]` mais reste public
    (§9.8), et ses tests tournent sous `#pragma warning disable CS0618`.
  - La surcharge `ToModifiedScreenCloseAnswer(DialogResult)` est retirée.
  - Tests : 10 dans `CasaEngine.Tests/Editor/ModifiedScreensExitCoordinatorTests.cs`. Mutations E1 à E6 : chacune
    rougit au moins un test (pas de passage après la réponse, passage jamais consommé, automatisation ignorée, pas de
    dédoublonnage, sortie jamais demandée, seconde réponse acceptée).
  - Les deux solutions compilent sans erreur ; `CasaEngine.Tests` 1931/1931.
  - **Sortie automatisée avec un écran modifié**, éditeur réel : `CasaEngine.Editor.exe --project
    Projects/SampleProject/SampleProject.json --open-asset Screens/sample-popup.uiscreen --set-screen-property
    lblTitle:Text=ChangedBySmoke --capture-delay 2 --diagnostics-out …`, lancé depuis le scratchpad. Le diagnostic
    montre « Updated screen property 'lblTitle.Text' ». L'éditeur sort seul, code 0, en 3 s, sans question. Les
    empreintes SHA-1 du projet sont identiques avant et après (le `.xaml` n'est pas écrit).
  - Seul artefact : `Projects/SampleProject/.casaeditor/viewport.editor.json`, créé par le lancement et retiré.
    Incident, réparé : en le retirant, j'ai d'abord supprimé tout `.casaeditor/`, qui contient deux fichiers suivis
    (`layout.uiscreen.json`, `layout.world.json`). Ils ont été restaurés depuis Git ; ils étaient propres avant le
    lancement, et les empreintes sont de nouveau identiques au relevé d'avant.
  - Sur `Projects/RPGDemo`, la même commande ne progresse pas : l'automatisation générique doit d'abord
    sélectionner une entité, et le monde de départ de RPGDemo n'en a pas. C'est une limite de l'automatisation, pas
    de ce chantier : `--play-smoke` sort bien, code 0, en 8 s.
  - **Reste 🧪** : File > Exit et la croix de la fenêtre, chacun avec Save, Don't Save et Cancel. Seul un lancement
    réel prouve que `Exit()` relancé après une annulation ferme bien l'éditeur sous DesktopGL.

### 🧪 T3.3 — Ouvrir un monde quand le monde courant est modifié (D6)

- Objectif : `ConfirmSaveBeforeOpeningWorld` devient une question asynchrone ; l'ouverture continue dans le rappel.
- Fichiers : `CasaEngine.Editor/GameEditor.cs`.
- Étapes : `TryOpenWorldAsset` pose la question et rend `false` (monde pas encore ouvert) ; Save → `SaveCurrentProject`,
  puis ouverture seulement si le monde n'est plus modifié ; Don't Save → ouverture sans le contrôle « modifié » ;
  Cancel → rien. L'ouverture reprend toutes les vérifications de `TryOpenWorldAsset` (catalogue, mode Play, même monde).
  Vérifier qu'aucun chemin d'automatisation (`GameEditor.cs:7022`, `:7047`, `:7083`) ne peut arriver avec un monde
  modifié ; sinon → ⚠️ Blocked (O3).
- Validation : build ; `CasaEngine.Tests` sans nouvel échec ; `rg` de la validation globale sans résultat. 🧪 pour
  l'auteur : ouvrir un monde avec le monde courant modifié, avec chaque réponse.
- Commit : `feat(editor): ask to save the world before opening another in an MGUI message box`.
- Note (2026-09-25) :
  - `TryOpenWorldAsset` (la route) appelle `OpenWorldAsset(fullPath, unsavedChangesHandled: false)`. Avec un monde
    modifié, `AskSaveBeforeOpeningWorld` pose « Open World » et rend `false`.
  - La réponse passe par `ModifiedScreenCloseDecision.ApplyAnswer`, déjà testé :
    - Save appelle `TrySaveProjectBeforeOpeningWorld`, l'ancien corps de `ConfirmSaveBeforeOpeningWorld`, inchangé ;
    - si l'enregistrement réussit, et sur Don't Save, le monde s'ouvre avec `OpenWorldAsset(fullPath, true)`, qui
      refait toutes les autres vérifications (catalogue, mode Play, même monde) ;
    - Cancel ou un enregistrement raté ne font rien.
  - Une seule question à la fois (`_worldOpenQuestionPending`).
  - O3 levé (voir « Points ouverts ») : aucun chemin d'automatisation n'arrive ici avec un monde modifié.
  - Aucun test ajouté : le flux reste dans `GameEditor` (runtime GPU), et la décision est celle d'`ApplyAnswer`, déjà
    couverte.
  - Les deux solutions compilent sans erreur ; `CasaEngine.Tests` 1931/1931.
  - `rg "System.Windows.Forms.MessageBox|FormsMessageBox|MessageBoxButtons|MessageBoxIcon" CasaEngine.Editor` ne
    rend plus rien. `DialogResult` ne reste que pour les sélecteurs natifs gardés (D2) : `ProjectLauncherWindow.cs`
    Browse et dossier, `ContentBrowserPanel.cs` Import. La commande de la validation globale l'incluait à tort.
  - **Reste 🧪** : ouvrir un monde quand le monde courant est modifié, avec chaque réponse.

---

## Phase 4 — Clôture

### ✅ T4.1 — Documentation et vérification finale

- Objectif : documenter et faire vérifier l'ensemble.
- Fichiers : `docs/editor/` (page courte sur les boîtes de l'éditeur : file, libellés, automatisation, sélecteurs natifs
  gardés), `docs/README.md`, ce plan, `ai-agent/README.md`.
- Étapes : doc ; validation globale complète ; vérificateur frais de clôture sur tout le chantier.
- Validation : validation globale verte ; vérificateur **CONFIRMED**. Smoke de l'auteur restant, repris des tâches 🧪 :
  T1.1 (sample), T2.3, T3.1, T3.2, T3.3.
- Commit : `docs(editor): document the MGUI message boxes`.
- Note (2026-09-25) :
  - Documentation : `docs/editor/editor-message-boxes.md` (en anglais) et son entrée dans `docs/README.md`.
  - ADR-0039 corrigée sur un point : la fermeture par code passe par `MGDockHost.ClosePanel`, pas par `RemovePanel`
    (voir T1.2). La correction est écrite en toutes lettres dans l'ADR.
  - **Vérificateur de clôture, frais, sur tout le chantier : CONFIRMED**, aucun constat P0–P2. Il a vérifié le moteur
    `724b5da5` et MGUI `bb0201d`.
  - Il a reproduit : `MGUI.Tests` 3083/3083 ; `MGUI.Samples`, `CasaEngine.MonoGame.sln` et
    `CasaEngine.Editor.MonoGame.sln` sans erreur ; `CasaEngine.Tests` 1931/1931 ; le `rg` des boîtes natives vide.
  - Il n'a trouvé aucune autre boîte native, dans l'éditeur, le lanceur et les sous-modules.
  - Il a décompilé MonoGame DesktopGL 3.8.5.1 : `Game.Exit()` ne fait que lever un drapeau, `OnExiting` passe en fin
    de frame et respecte `Cancel`, et la croix de la fenêtre appelle aussi `Exit()`. Relancer `Exit()` depuis un
    rappel n'est donc pas réentrant.
  - Il a relu la doc et la correction de l'ADR : conformes au code.
  - Quatre avis P4, reportés (O5 élargi, O8, O9 ; le troisième, la liste de « Quit » figée, est déjà dans la doc).
  - Restent les clics de l'auteur listés dans les tâches 🧪.

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Icônes (avertissement, erreur, information) : pas de ressource d'icône vérifiée dans MGUI ; hors de cette version, à ajouter si l'auteur le demande. | — |
| O2 | ~~Chemin d'Entrée et d'Échap que l'éditeur alimente vraiment dans le bureau MGUI.~~ Levé en T1.1 : chemin de navigation brut du bureau. | T1.1 |
| O3 | ~~Un chemin d'automatisation peut-il ouvrir un monde alors que le monde courant est modifié ?~~ Non, levé le 2026-09-25 : `RunAutomation` n'ouvre son asset (`TryApplyAutomationAssetOpen`, une fois) qu'au début de la séquence, juste après le chargement du monde et avant toute étape qui le modifie ; l'asset de démarrage ne s'ouvre que hors automatisation (`TryOpenStartupAssetIfRequested`). | T3.3 |
| O4 | ~~Défaut latent de MGUI : `MGOverlayHost.TryRemoveOverlay` sur une surcouche ouverte ne la retire pas de `OpenOverlays` (`MGUI/MGUI.Core/UI/MGOverlay.cs:43-73`), donc elle reste `ActiveOverlay`.~~ **Corrigé le 2026-09-25**, à la demande de l'auteur, dans un chantier séparé : MGUI branche `fix/overlay-remove-open` (depuis `develop`), commit `ac4b6ed`, 5 tests (`MGUI.Tests/Overlay/OverlayHostRemoveOverlayTests.cs`, dont 3 qui échouent sans le correctif) ; `MGUI.Tests` 3054/3054 sur cette branche. Fusion d'essai avec `chantier/mgui-message-boxes` dans un worktree jetable : sans conflit, 3088/3088. **Fusionné dans `develop` de MGUI** le 2026-09-25 à la demande de l'auteur (`c831b53`, `--no-ff`, non poussé) ; `MGUI.Tests` sur `develop` fusionné : 3054/3054 sur 4 des 5 passes, un échec intermittent sans rapport (`HostImageResolutionTests.SourceName_ChangesBetweenAlreadyResolvedNames_AllocateNothing`, mesure d'allocations par thread ; 10/10 en isolation ; signalé en tâche séparée). **Au merge des deux branches** : le commentaire « TryClose first » de `MGMessageBox.Choose` et la puce « Fermeture » de la section MessageBox de `MGUI/Docs/controls-architecture.md` ne seront plus vrais ; les retirer (appeler `TryClose` d'abord reste correct). | T1.1 |
| O5 | Avis P4 du vérificateur de T3.1, reporté : si `MGMessageBox.Show` levait, `ModifiedScreenCloseCoordinator` garderait la question « en attente » et refuserait ensuite toute fermeture de cet écran. Le vérificateur de clôture relève le même cas pour la question « Quit » (`ModifiedScreensExitCoordinator`) et pour `_worldOpenQuestionPending` : la sortie serait toujours annulée, ou l'ouverture d'un monde modifié ignorée. `Show` ne lève que sur de mauvais arguments ou un hôte de surcouche non modal ; l'éditeur passe des libellés fixes et l'hôte modal par défaut. À revoir si ce réglage change. | T3.1 |
| O6 | Avis P4 du vérificateur de T3.1, reporté : un écran rouvert sous le même identifiant pendant que sa question attend serait enregistré par Save, puis `ClosePanel` échouerait avec un avertissement (aucune perte). La boîte modale rend ce cas difficile à atteindre. | T3.1 |
| O7 | Avis P4 du vérificateur de T3.1, reporté : pas de test de `ClosePanel` sur une fenêtre flottante autonome (non suivie par l'hôte, que l'éditeur ne crée pas) ; fermeture sans frame intermédiaire vérifiée à la lecture seulement. | T1.2 |
| O8 | Avis P4 du vérificateur de clôture, reporté : un `ContentBrowserPanel` ou un `ProjectLauncherWindow` construit par son constructeur public (sans la file partagée) tient sa propre file ; les boîtes s'empilent alors (ce que `MGMessageBox` sait faire). L'éditeur passe toujours la file partagée. | T2.3 |
| O9 | Avis P4 du vérificateur de clôture, spéculatif, reporté : si Entrée ouvrait un monde depuis le Content Browser et que la répétition de la touche atteignait la nouvelle boîte, elle répondrait « Save » (non destructif). À observer dans l'éditeur, Entrée tenue. | T3.3 |

## Hors périmètre

- Les sélecteurs natifs de fichiers et de dossiers (D2) ; « Open in Explorer » (`ContentBrowserPanel.cs:1265`) ; le
  presse-papiers WinForms (`UIScreenPreviewPanel.cs:669`, `ContentBrowserPanel.cs`, `LogsPanel.cs`). `UseWindowsForms`
  reste donc dans `CasaEngine.Editor.csproj`.
- L'`OpenFileDialog` du designer XAML de MGUI (`MGUI/MGUI.Core/UI/MGXAMLDesigner.cs:212`), que l'éditeur n'utilise pas.
- Les boîtes du jeu (runtime) et la traduction des textes de l'éditeur.
- Les icônes de boîte (O1) et une file de boîtes dans MGUI (D8).
- Les autres types de documents modifiés (matériaux, entités) : aucune question aujourd'hui, pas de nouvelle question.
