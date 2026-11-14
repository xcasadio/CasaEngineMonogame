# Plan agent IA — Préférences souris et caméra de l'éditeur

Suite du chantier [unreal-viewport-camera-tasks.md](archive/unreal-viewport-camera-tasks.md) (ADR-0089), demandée par l'auteur le 2026-11-14 : « rajouter un panneau de config dans l'éditeur pour paramétrer la souris de l'éditeur avec un fichier JSON de paramètres pour l'éditeur ».
Les décisions D1 → D5 ci-dessous ont été arbitrées avec l'auteur le 2026-11-14 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Un panneau dockable « Editor Preferences » dans l'éditeur règle la souris et la caméra des viewports (3D et 2D). Les valeurs s'appliquent tout de suite et sont enregistrées dans un fichier JSON propre à l'utilisateur, relu au lancement de l'éditeur. Le runtime (démos) ne lit pas ce fichier.

## État vérifié du dépôt (2026-11-14)

- Fichiers d'état de l'éditeur : seulement **par projet**, dans `<projet>/.casaeditor/` : `layout.editor.json` et `viewport.editor.json` (`CasaEngine.Editor/GameEditor.cs:123-125`, chemins l. 2581 et 2617). Aucune préférence utilisateur globale : `rg -n -i "SpecialFolder" CasaEngine.Editor CasaEngine.EditorServices` ne trouve que `MyDocuments` (lanceur de projets) et `Fonts`.
- Modèle de sérialiseur à suivre : `CasaEngine.Editor/Runtime/EditorViewportViewStateSerializer.cs` (Newtonsoft `JObject`, clés en snake_case, l. 39 et 69) ; échec d'écriture du viewport loggé en avertissement, sans exception (`GameEditor.cs`, `Logs.WriteWarning` « Failed to save the viewport view state »).
- Menu `Windows` : `GameEditor.cs:550-559` (`Audio`, puis la gestion du layout). Panneau dockable de référence : `AudioProfilerPanel` (`CasaEngine.Editor/Controls/AudioProfilerPanel.cs`), id `EditorPanelIds.AudioProfiler` (`CasaEngine.Editor/Workspaces/EditorPanelIds.cs:12`), ouvert par `OpenAudioProfilerPanel` (`GameEditor.cs:2173-2182`).
- Champ numérique existant : `NumericField` (`CasaEngine.Editor/Controls/NumericField.cs:15`). Il n'a qu'un événement `ValueChanged` (l. 53), levé à chaque frappe (`TextChanged`, l. 143-158) et à chaque cran de molette ou de +/- (l. 162-166) : pas d'événement de validation (Entrée, perte de focus).
- `WorldViewportPanel` est créé à quatre endroits : viewport monde (`GameEditor.cs:1419`), viewports des materials (`GameEditor.cs:3586`), aperçu d'`Animation2dAssetInspectorPanel` (`Animation2dAssetInspectorPanel.cs:261`) et d'`EntityAssetEditorPanel` (`EntityAssetEditorPanel.cs:82`) ; ces deux panneaux sont créés par `GameEditor` (l. 5135, 5191). Les quatre reçoivent le même `HostedEditorGameAdapter` (classe interne de l'éditeur, `CasaEngine.Editor/Runtime/HostedEditorGameAdapter.cs:7`). Chaque `WorldViewportPanel` a ses propres contrôleurs 3D et 2D (`WorldViewportPanel.cs:192-193`), donc sa propre `ViewportCameraNavigation` et sa propre vitesse de vol ; il a un `Dispose` (l. 2114).
- ADR : `docs/decisions/0090-demos-carry-a-sheet-and-follow-a-learning-path.md` existe déjà (commit `7eee5c3a`, branche `chantier/demos-onboarding`) : la prochaine ADR libre est **ADR-0091**.
- Caméra 3D : `EditorViewportCameraController` possède un `ViewportCameraNavigation` privé, dont les réglages sont des propriétés publiques : `RotationSensitivity`, `PanSensitivity`, `WheelStepRatio`, `MinWheelStep`, `FlySpeed`, `MinFlySpeed`, `MaxFlySpeed`, `FlySpeedWheelFactor`, `MinDistance`, `MaxDistance`, `DragThresholdPixels`, `FieldOfViewRate` (`CasaEngine/Framework/Scene/Cameras/ViewportCameraNavigation.cs`). Pas d'inversion de l'axe Y.
- Caméra 2D : `EditorViewport2dCameraController` : pan 1:1 en pixels (`Pan`, l. 116), zoom par crans (`WheelDeltaToSteps`, l. 163 ; `WheelDeltaPerStep = 120`, l. 39 ; crans bornés par `MinimumZoomStep = -7` et `MaximumZoomStep = 31`, l. 34-37).
- Branche `chantier/unreal-viewport-camera` non fusionnée dans `main` (dernier commit `e3617e1f`).
- Le dossier du dépôt `D:\development\repo\CasaEngineMonogame` est **occupé par un autre chantier** : branche `chantier/demos-onboarding` (HEAD `88ee4037`, trois commits au-dessus d'`e3617e1f`), avec du travail non commité (`CasaEngine.Demos/DemosGame.cs`, `Demos/DemoUI/MainMenuScreen.cs`, `Content/Screens/main-menu.xaml`, `CasaEngine.Tests/*`, `CasaEngine.Demos/DemoSheetText.cs`, `CasaEngine.Tests/UI/DemoSheetTextTests.cs`), plus les fichiers locaux de l'auteur (`.serena/project.yml`, `MGUI`, `.codegraph/`, `.serena/memories/`, `Projects/SampleProject/.casaeditor/viewport.editor.json`). Ce chantier ne touche ni à ce dossier ni à cette branche (P7).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Fichier **par utilisateur**, valable pour tous les projets (comme les Editor Preferences d'Unreal). |
| D2 | Réglages exposés : sensibilités de la caméra 3D, inversion de l'axe Y, vitesse de vol mémorisée (celle changée au clic droit + molette est retrouvée au lancement suivant), réglages de la caméra 2D. |
| D3 | Panneau **dockable**, ouvert depuis `Windows`, appliqué **en direct** et enregistré, avec un bouton de réinitialisation. |
| D4 | Éditeur seulement : les démos et le runtime ne lisent pas ce fichier (AGENTS §9.7, §9.9). |
| D5 | Le chantier prolonge ADR-0089 ; le fichier de préférences est une décision de format, enregistrée en ADR (numéro prévu ADR-0090, déjà pris : **ADR-0091**, voir O2). |

## Points à valider (proposés par l'agent)

| Réf | Proposition | Raison |
|---|---|---|
| P1 | Chemin : `%APPDATA%/CasaEngine/editor.preferences.json` (`Environment.SpecialFolder.ApplicationData`). | Emplacement standard Windows d'un réglage utilisateur. |
| P2 | Format, clés en snake_case comme `viewport.editor.json` : `{ "version": 1, "camera_3d": { "rotation_sensitivity", "pan_sensitivity", "wheel_step_ratio", "fly_speed", "fly_speed_wheel_factor", "field_of_view_rate_degrees", "drag_threshold_pixels", "invert_y" }, "camera_2d": { "pan_speed", "zoom_steps_per_notch", "invert_y" } }`. Clé absente = valeur par défaut ; valeur hors bornes = bornée et signalée une fois en avertissement avec le nom de la clé ; fichier illisible = valeurs par défaut, avertissement, fichier non écrasé tant que l'utilisateur ne change rien. | Format stable et tolérant (AGENTS §9.7). |
| P3 | Valeurs par défaut = valeurs actuelles du code (ADR-0089, `EditorViewport2dCameraController`) ; `invert_y` faux ; `pan_speed` 1 ; `zoom_steps_per_notch` 1. Bornes : rotation [0.0005, 0.05] rad/px, pan [0.001, 0.1], pas de molette [0.01, 0.5], vol [`MinFlySpeed`, `MaxFlySpeed`], facteur de molette [1.05, 3], champ de vision [5, 180] °/s, seuil [1, 20] px, pan 2D [0.1, 10], crans 2D [1, 5]. | Rien d'arbitraire n'est imposé sans borne. |
| P4 | **Application et écriture séparées.** Chaque `ValueChanged` d'un `NumericField` (et chaque case, `Reset`, ou changement de vitesse de vol au clic droit + molette) met à jour le magasin, qui borne la valeur et l'applique tout de suite ; `NumericField` n'est pas modifié. Le magasin marque alors le fichier « à écrire » et note l'heure ; `GameEditor.Update` appelle `EditorPreferencesStore.FlushIfDue(now)`, qui écrit **une seule fois** quand aucun changement n'est venu depuis 1 seconde (un booléen et une comparaison de temps par image, aucune allocation). Écriture forcée aussi à la fermeture de l'éditeur. | `NumericField` n'a pas d'événement de validation ; une frappe ne doit pas provoquer une écriture (constat du relecteur). |
| P5 | Inversion de Y en 3D : ajout additif `InvertY` à `ViewportCameraNavigation` (regarder, orbite Alt, avance du glisser gauche non concernée). En 2D : inverse le pan vertical. | Unreal inverse la rotation, pas le déplacement. |
| P6 | Modèle et sérialiseur dans `CasaEngine.Editor/Runtime/` (comme `EditorViewportViewStateSerializer`), panneau dans `CasaEngine.Editor/Controls/`. Rien dans `CasaEngine` sauf `InvertY` (P5). | Préférence d'interface de l'éditeur, pas un asset. |
| P7 | **Worktree séparé.** `git worktree add D:\development\repo\CasaEngineMonogame-prefs -b chantier/editor-mouse-preferences e3617e1f` (depuis `chantier/unreal-viewport-camera`, pas encore dans `main`), puis `git submodule update --init` dans ce worktree. Tout le chantier s'exécute là : le dossier principal et la branche `chantier/demos-onboarding` ne sont pas touchés, ni changés de branche. Le fichier de ce plan est déplacé dans le worktree au début de T0.1 et commité avec sa ligne du tableau d'`ai-agent/README.md` ; dans ce worktree, `ai-agent/README.md` n'a pas d'autre modification. | Le dossier principal est occupé par un autre chantier en cours (constat du relecteur). |
| P8 | **Distribution aux viewports.** Un seul `EditorPreferencesStore`, créé par `GameEditor` et exposé par `HostedEditorGameAdapter.Preferences` (objet déjà partagé par les quatre viewports, aucune signature de constructeur changée, pas d'état statique). Chaque `WorldViewportPanel` applique les préférences à sa création, s'abonne à `Changed` et se désabonne dans `Dispose`. La vitesse de vol est une valeur unique : un changement au clic droit + molette dans un viewport passe par le magasin et s'applique à tous les viewports. | Quatre sites de création, dont deux panneaux d'asset à vie courte (constat du relecteur). |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/editor-mouse-preferences`, dans le worktree `D:\development\repo\CasaEngineMonogame-prefs`** (P7). Ne jamais committer sur `main`. Ne jamais changer la branche du dossier principal.
- **Précondition avant T0.1** : worktree créé, sous-modules initialisés, `dotnet build` des deux `.sln` réussi dans le worktree. Sinon ⚠️ Blocked.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché (`dotnet build CasaEngine.MonoGame.sln` ou `dotnet build CasaEngine.Editor.MonoGame.sln` selon le périmètre) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` dès qu'une tâche touche du code testé. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; le runtime ne dépend pas de l'éditeur ; sérialisation additive. Les tests n'écrivent jamais dans le vrai `%APPDATA%` : le chemin du fichier est injectable.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous les tests passent.
- Smoke éditeur (lancé hors de l'app Claude, voir R1) : ouvrir `Windows > Editor Preferences`, changer chaque réglage et vérifier son effet immédiat dans le viewport 3D puis en vue 2D ; changer la vitesse de vol au clic droit + molette ; fermer et relancer l'éditeur : les valeurs sont retrouvées ; `Reset` remet les valeurs par défaut ; supprimer le fichier : l'éditeur démarre avec les valeurs par défaut.

---

## Phase 0 — Décision et modèle

### ✅ T0.1 — ADR-0091 : préférences utilisateur de l'éditeur

- Objectif : enregistrer D1 à D5 et P1 à P8 retenus, et poser le plan dans le worktree.
- Fichiers : `ai-agent/tasks/editor-mouse-preferences-tasks.md` (déplacé depuis le dossier principal), `ai-agent/README.md` (une ligne), `docs/decisions/0091-editor-user-preferences-file.md` (nouveau), `docs/decisions/README.md`.
- Étapes :
  1. Vérifier dans le worktree que `0091` est libre (`fd "^0091" docs/decisions`) ; sinon prendre le suivant et corriger ce plan.
  2. Rédiger l'ADR (skill `adr`), l'ajouter à l'index.
  3. Ajouter la ligne du plan au tableau d'`ai-agent/README.md`.
- Validation : relecture ; liens valides ; aucun préfixe de numéro en double dans `docs/decisions/` ; `git diff --cached ai-agent/README.md` ne montre que la ligne de ce plan.
- Commit : `docs(adr): record ADR-0091, per-user editor preferences file`
- Note de validation (2026-11-14) : worktree créé, sous-modules initialisés (MGUI `a3c42cb0`, NvgSharp `9c0da031`), les deux `.sln` buildent dans le worktree. `0091` libre. ADR-0091 écrite et indexée ; plan déplacé du dossier principal vers le worktree ; ligne ajoutée au tableau.

### ✅ T0.2 — Modèle `EditorPreferences` et son sérialiseur

- Objectif : charger, borner, enregistrer les préférences (P1 à P4), testé sans fichier réel.
- Fichiers : `CasaEngine.Editor/Runtime/EditorPreferences.cs` (nouveau : `EditorPreferences`, `Camera3dPreferences`, `Camera2dPreferences`, valeurs par défaut, `Clamp`), `CasaEngine.Editor/Runtime/EditorPreferencesSerializer.cs` (nouveau : `Read(string json, Action<string> warn)`, `Write(EditorPreferences)`), `CasaEngine.Editor/Runtime/EditorPreferencesStore.cs` (nouveau : chemin et horloge injectables, `Load`, `Update(...)` qui borne, applique et marque « à écrire », `FlushIfDue(now)`, `Flush()`, événement `Changed`), `CasaEngine.Tests/Editor/EditorPreferencesSerializerTests.cs` et `EditorPreferencesStoreTests.cs` (nouveaux, fichiers dans un dossier temporaire du test).
- Étapes :
  1. Modèle et valeurs par défaut (P3).
  2. Lecture tolérante (P2) : clé absente, valeur hors bornes, type faux, JSON illisible, version inconnue (lue au mieux, avertissement).
  3. Écriture déterministe (ordre des clés stable, indentation), dossier créé si absent, échec d'écriture loggé en avertissement sans exception (comme le viewport).
  4. Écriture différée de P4 : `FlushIfDue` n'écrit qu'après 1 seconde sans changement, une seule fois.
  5. Tests : aller-retour, défauts sur fichier absent ou vide, bornes et avertissement par clé, JSON illisible non écrasé au chargement, stabilité de la sortie ; **cinq `Update` successifs (une valeur tapée caractère par caractère) puis `FlushIfDue` après le délai = exactement une écriture** (compteur d'écritures) ; aucune écriture avant le délai.
- Validation : build éditeur ; tests verts.
- Commit : `feat(editor): add the per-user editor preferences model and file`
- Note de validation (2026-11-14) : `EditorPreferences` (records, bornes `EditorPreferenceBounds`), `EditorPreferencesSerializer` (Newtonsoft, snake_case, ordre stable, lecture tolérante), `EditorPreferencesStore` (horloge et chemin injectables, `Update`, `FlushIfDue`, `Flush`, `ChangedSubscriberCount`, `WriteCount`). 14 tests dans `CasaEngine.Tests/Editor/EditorPreferencesTests.cs` (fichiers dans un dossier temporaire), dont « valeur tapée caractère par caractère = une seule écriture ». Build éditeur réussi (via le projet de tests) ; `CasaEngine.Tests` 5125/5125. Corrigé aussi : chemin du worktree dans la ligne d'`ai-agent/README.md` (antislashs perdus en T0.1).

---

## Phase 1 — Application aux caméras

### 🧪 T1.1 — Caméra 3D : réglages, inversion de Y, vitesse de vol mémorisée

- Objectif : D2 pour le viewport 3D.
- Fichiers : `CasaEngine/Framework/Scene/Cameras/ViewportCameraNavigation.cs` (`InvertY`, additif, P5), `CasaEngine.Tests/Scene/ViewportCameraNavigationTests.cs`, `CasaEngine.Editor/Runtime/EditorViewportCameraController.cs` (`ApplyPreferences(Camera3dPreferences)`, vitesse de vol remontée quand elle change), `CasaEngine.Editor/Runtime/HostedEditorGameAdapter.cs` (propriété `Preferences`, P8), `CasaEngine.Editor/Controls/WorldViewportPanel.cs` (application à la création, abonnement à `Changed`, désabonnement dans `Dispose`), `CasaEngine.Editor/GameEditor.cs` (création et chargement du magasin au lancement, `FlushIfDue` dans `Update`, `Flush` à la fermeture). Les trois autres sites de création (`GameEditor.cs:3586`, `Animation2dAssetInspectorPanel.cs:261`, `EntityAssetEditorPanel.cs:82`) passent déjà le même `HostedEditorGameAdapter` : vérifier qu'ils n'ont rien à changer.
- Étapes :
  1. `InvertY` dans la navigation (regarder, orbite Alt) et tests.
  2. Magasin exposé par `HostedEditorGameAdapter.Preferences` ; chaque `WorldViewportPanel` applique les préférences à sa création et à chaque `Changed`, et se désabonne dans `Dispose` (P8).
  3. Vitesse de vol : quand le clic droit + molette la change dans un viewport, le contrôleur la remonte au magasin (`Update`), qui l'applique à tous les viewports ; l'écriture suit P4.
- Validation : `dotnet build` des deux `.sln` ; tests verts ; test du contrôleur : `ApplyPreferences` change la rotation et inverse Y ; test du magasin : deux abonnés reçoivent un changement, un abonné désabonné ne le reçoit plus ; smoke : un réglage changé s'applique au viewport monde **et** à l'aperçu d'un asset entité ouvert ; fermer l'éditeur d'asset ne laisse pas d'abonné (compteur d'abonnés du magasin, exposé pour le test).
- Commit : `feat(editor): apply the user preferences to the 3D viewport camera`
- Note de validation (2026-11-14) : `ViewportCameraNavigation.InvertY` (additif) et 2 tests ; `EditorViewportCameraController.ApplyPreferences` et `FlySpeed`, 2 tests ; `HostedEditorGameAdapter.Preferences` ; chaque `WorldViewportPanel` applique les préférences à sa création, s'abonne à `Changed` par un délégué mis en cache, se désabonne dans `Dispose`, et remonte la vitesse de vol au magasin après la caméra. Les trois autres sites de création passent déjà le même adaptateur : rien à changer. `GameEditor` crée et charge le magasin juste après l'adaptateur, appelle `FlushIfDue` au début d'`Update` et `Flush` dans `OnExiting`. Ajout non prévu par le plan : en mode automatisation, fichier temporaire (`%TEMP%/CasaEngine/editor.preferences.automation.json`) pour ne pas toucher au fichier de l'utilisateur. Le désabonnement est couvert par le test du magasin, pas par un test du panneau (il demande une fenêtre MGUI). Les deux `.sln` buildent ; `CasaEngine.Tests` 5129/5129. **Manque** : smoke (viewport monde et aperçu d'entité), reporté à la recette T3.2.

### 🧪 T1.2 — Caméra 2D : vitesse de pan, crans de zoom, inversion de Y

- Objectif : D2 pour la vue 2D.
- Fichiers : `CasaEngine.Editor/Runtime/EditorViewport2dCameraController.cs`, ses tests s'ils existent (`rg -l EditorViewport2dCameraController CasaEngine.Tests`), `CasaEngine.Editor/Controls/WorldViewportPanel.cs`.
- Étapes : `ApplyPreferences(Camera2dPreferences)` ; pan multiplié par `pan_speed`, vertical inversé si `invert_y` ; crans de zoom multipliés par `zoom_steps_per_notch`, toujours bornés par `MinimumZoomStep`/`MaximumZoomStep`. Valeurs par défaut = comportement actuel inchangé.
- Validation : build éditeur ; tests verts, dont un test « valeurs par défaut = comportement d'avant ».
- Commit : `feat(editor): apply the user preferences to the 2D viewport camera`
- Note de validation (2026-11-14) : `EditorViewport2dCameraController.ApplyPreferences`, `PanSpeed`, `ZoomStepsPerNotch`, `InvertY` ; `Pan` multiplié et inversé, crans de molette multipliés puis bornés comme avant ; le panneau applique aussi les préférences 2D. 3 tests, dont « préférences par défaut = comportement d'avant » ; les tests 2D existants passent sans changement. Build éditeur réussi ; `CasaEngine.Tests` 5132/5132. **Manque** : smoke en vue 2D, reporté à la recette T3.2.

---

## Phase 2 — Panneau

### ⏳ T2.1 — Panneau dockable « Editor Preferences »

- Objectif : D3.
- Fichiers : `CasaEngine.Editor/Controls/EditorPreferencesPanel.cs` (nouveau), `CasaEngine.Editor/Workspaces/EditorPanelIds.cs` (`EditorPreferences`), `CasaEngine.Editor/GameEditor.cs` (`Windows > Editor Preferences`, ouverture comme `OpenAudioProfilerPanel`).
- Étapes :
  1. Deux groupes « 3D viewport » et « 2D viewport » : un `NumericField` par valeur numérique (bornes de P3), une case pour `invert_y`, et la vitesse de vol courante.
  2. Chaque `ValueChanged` d'un champ appelle `EditorPreferencesStore.Update` (application immédiate, écriture différée de P4) ; `Reset to defaults` fait de même avec les valeurs par défaut. `NumericField` n'est pas modifié.
  3. Le panneau se met à jour si la vitesse de vol change au clic droit + molette.
  4. Vérifier que le layout sauvegardé (`layout.editor.json`) accepte le nouveau panneau sans casser un layout existant.
- Validation : build éditeur ; tests verts ; smoke éditeur de la validation globale.
- Commit : `feat(editor): add the Editor Preferences panel`

---

## Phase 3 — Documentation et recette

### ⏳ T3.1 — Documentation

- Objectif : documenter le panneau, le fichier, les bornes.
- Fichiers : `docs/editor/editor-preferences.md` (nouveau), `docs/editor/viewport-camera-controls.md` (renvoi, retirer « user-editable sensitivities » des non-implémentés), `docs/README.md`.
- Validation : relecture ; liens valides.
- Commit : `docs(editor): document the editor preferences panel and file`

### ⏳ T3.2 — Vérification indépendante et recette de l'auteur

- Étapes : validation globale ; vérificateur neuf (`verifier`) sur la branche (format tolérant, bornes, aucun accès disque par image, runtime indépendant du fichier, défauts identiques à l'existant) ; recette de l'auteur.
- Validation : CONFIRMED et accord de l'auteur ; sinon 🧪 avec ce qui manque.
- Commit : `docs(ai-agent): record the check of the editor preferences`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Valider ou modifier P1 à P8 avant l'exécution. | T0.1 |
| O2 | ADR-0090 est déjà prise par le chantier `demos-onboarding` : ce chantier prend ADR-0091. Si les deux branches arrivent dans `main` dans un autre ordre, vérifier les numéros à l'intégration. | T0.1 |
| R1 | Risque : d'après AGENTS §7, les écritures sous AppData faites depuis l'app Claude sont virtualisées. Un éditeur lancé depuis l'app écrirait un fichier invisible ailleurs : la recette se fait avec l'éditeur lancé hors de l'app. | T3.2 |

## Hors périmètre

- Lecture du fichier par le runtime ou les démos (D4).
- Raccourcis clavier personnalisables, préférences autres que souris et caméra (thème, grille, snapping).
- Préférences par projet.
