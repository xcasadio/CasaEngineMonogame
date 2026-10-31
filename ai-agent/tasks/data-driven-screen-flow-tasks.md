# Plan agent IA — Flux d'écrans piloté par les données (E1)

Plan d'exécution de E1, issu de l'étude du 2026-10-31 (sans analyse dédiée, voir O6).
Les décisions D1, D2, D3 et P1 → P4 ont été arbitrées avec l'auteur (P1 → P4 approuvés le 2026-10-31) : **ce plan les applique, il ne les rediscute pas**. Les points O1 → O7 sont tranchés par l'auteur le 2026-10-31 : recommandations du tableau acceptées, O4 et O5 restant des inconnues à vérifier pendant T1.2 et T2.2.

Ce plan vient d'un projet parent, approuvé par l'auteur le 2026-10-31 (« Go E1, recos O1-O7 »). Cette copie fait foi.

Ce fichier doit être mis à jour pendant le travail : l'icône de chaque tâche indique son statut.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit.
> **Avant d'écrire le plan** : poser toutes les questions en une fois ; ne rien inventer.
> **Après approbation** : exécution autonome ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Un projet sans DLL ouvre un écran XAML, lie un bouton à une action et change de monde, par ses seules données. Cas motivant externe : un écran titre.

## État vérifié du dépôt (2026-10-31)

- `XamlUIScreenBase` est abstraite (`:23`) ; toutes ses sous-classes sont du code.
- `FirstScreenName` n'est que lu et écrit (`ProjectSettingsHelper.cs:23,123`).
- DLL vide : chargement sauté (`ProjectSettingsHelper.cs:57-59`), proxy vide accepté (`World.cs:357-360`).
- Dernier ADR : 0077. Arbre propre, aucun changement préexistant.
- `origin/main` de `xcasadio/CasaEngineMonogame` est à `92d8e847`, le commit lu pour ce plan.

## Vérifications

Lecture à `92d8e847`. Seul le focus a été exécuté, en tests sans affichage sur une copie temporaire (sonde hors dépôt, à reprendre pour T0.3). `UIRoot` exige un device graphique : son ordre d'appel a été reproduit à la main. Manette et modal non testés.

1. **Clic et Submit partagent la commande, en portée fenêtre** : confirmé (`MGButton.cs:239-248,274-280,299`, `MGWindow.cs:1640`).
2. **L'ordre XAML suffit au focus** : réfuté. Un bouton survolé bloque l'auto-focus (`UIFocusNavigationService.cs:300`) et Submit n'a pas de repli : Entrée ne fait rien. Il faut un focus par défaut en donnée (`MGElement.cs:2822`). `MainMenuScreen.cs:99` n'est pas un précédent : cet appel est perdu. Mesuré : `Focus` dans `OnWindowLoaded`, focus nul pendant 5 frames ; après 1 mise à jour, perdu ; après 2, appliqué à la frame suivante ; dès que `CanReceiveKeyboardInput()` est vrai, appliqué à la frame suivante, et Entrée exécute la commande, y compris sous survol.
3. **Le changement de monde atteint l'écran sans propriétaire** : confirmé (`ViewManager.cs:249`, `CasaEngineGame.cs:794-796`, `UIRoot.cs:177`). Seul `Dispose` manque.
4. **Le son meurt avec l'écran** : réfuté par lecture. `PlaySound` ne garde pas l'asset (`CutsceneActionCoroutineFactory.cs:162`), le provider garde le clip (`AssetContentManagerAudioClipProvider.cs:7-9`).
5. **Zéro DLL jusqu'à l'écran** : plausible, jamais exécuté. `FirstScreenName` est vide partout, SampleProject n'a aucun script. Corrections : seul `main-menu.xaml` a une racine `Window`, aucun son catalogué, `IsMouseVisible` faux.

Autres corrections : `MGUI.Samples` appelle `AddCommand` ; `AssetLoader.cs:18-23` avale les exceptions de `Load`, donc valider après acquisition.

Conception retenue : commandes dans le `.uiscreen`. L'asset de flux ajoute type, loader et clé sans consommateur. Greffes : focus par défaut, course de référence, vue factice.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Ne rien inventer ; l'inconnu va en UNKNOWN. |
| D2 | Zéro DLL ; moteur générique, aucun code propre à un jeu ; le moteur évolue d'abord. |
| D3 | Navigation, UI pixel, soustractif, fondu sur l'UI : E2 à E4. |
| P1 | `DataDrivenScreen` scellée ; champs optionnels `.uiscreen` : `ui_commands`, `is_modal`, `default_focus`. Aucun nom de classe (ADR-0075). |
| P2 | Actions `open_screen`, `close_screen`, `change_world`, `play_sound`, `quit` ; liste ordonnée, sans condition ni attente. |
| P3 | Donnée invalide : exception contextuelle au chargement ; bouton sans commande légal. |
| P4 | `FirstScreenName` ouvre une fois après le premier `WorldLoaded`. Clic mémorisé, exécuté par `Update` ; `Hide` appelle `Dispose`. |

## Règles d'exécution pour l'agent

- **Branche `chantier/data-driven-screen-flow`** depuis `main` ; jamais de commit sur `main`, **jamais de push**.
- **Une tâche à la fois, un commit par tâche**, plan et note de validation inclus ; passer en 🚧 avant de commencer ; jamais de 🚧 restant, 🧪 si un contrôle manuel manque.
- **Ne rien inventer** : sinon ⚠️, question en Points ouverts, arrêt.
- **Build** avant ✅, puis `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` ; si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- `git add` fichier par fichier ; plan en français ; code, commits, docs, ADR en anglais.
- Rappel moteur : aucune allocation par frame, runtime sans éditeur, format additif, MGUI inchangé.

## Légende des statuts

⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked.

## Validation globale

- `dotnet build` des deux `.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : vert.
- Smoke : Launcher sur `SampleProject.json` ; focus visible, souris, Entrée, manette, son, sous-écran, monde suivant, Quit.

---

## Phase 0 — Socle

### ✅ T0.1 — Course de référence zéro DLL

- Objectif : prouver le chemin existant avant tout code.
- Sources : `Projects/SampleProject`, `CasaEngine.Launcher`.
- Fichiers : `ai-agent/tasks/data-driven-screen-flow-tasks.md`, `ai-agent/README.md`.
- Étapes : lancer le projet inchangé, noter le résultat.
- Validation : `DefaultWorld` affiché, `log.txt` sans exception ; sinon ⚠️.
- Commit : `docs(plan): record zero-DLL baseline`
- Validation faite le 2026-10-31 à `92d8e847` : `dotnet build CasaEngine.Launcher/CasaEngine.Launcher.csproj` 0 erreur (110 avertissements). `CasaEngine.Launcher.exe Projects/SampleProject/SampleProject.json` tourne encore après 15 s, fenêtre « Sample Project » 1024x768 (`GameplayDllName` vide). Le log charge `DefaultWorld.world` et ses entités (dont `camera`, `light1`, `light2`), aucune exception ni erreur sur 28 lignes. Capture : scène dessinée, fond CornflowerBlue dans le bas de l'image. La scène n'a pas été comparée à une référence.

### ✅ T0.2 — ADR-0078

- Objectif : enregistrer format, actions et sens de `FirstScreenName`.
- Fichiers : `docs/decisions/0078-ui-screens-declare-commands-in-data.md`, `docs/decisions/0035-xaml-authored-ui-screens.md`, `docs/decisions/0038-game-screens-are-assets-bound-to-view-models.md`, `docs/decisions/README.md`.
- Étapes : ADR-0078 amende ADR-0035 (abonnements) et ADR-0038 (view model) : statut des deux fichiers en `Accepted. Amended by ADR-0078 (...)`, corps inchangé, ligne d'index mise à jour ; poser que la donnée remplace la politique de la DLL ; justifier l'écart avec ADR-0042.
- Validation : relecture.
- Commit : `docs(adr): record data-driven UI screen commands`
- Validation faite le 2026-10-31 : relu contre P1 à P4, O1 à O4 et le code cité. ADR-0035 et ADR-0038 : seul le statut change, index mis à jour. Format retenu pour P2 : une action porte `type` et `target`.

### ✅ T0.3 — Figer le comportement MGUI

- Objectif : tester le socle de la conception.
- Fichiers : `CasaEngine.Tests/UI/XamlCommandFocusTests.cs`.
- Étapes : partir de la sonde hors dépôt ; commande au clic et à Entrée ; premier Entrée ; Entrée sous survol ; `Focus` avant la première mise à jour : perdu, file vidée (`MGDesktop.cs:1091,1565`) ; `Focus` après une seule mise à jour : perdu aussi, la première mise à jour de la fenêtre passe des bornes nulles à ses enfants (`MGElement.cs:3971-3995`, `MGWindow.cs:1709,1735`) ; `Focus` dès que `CanReceiveKeyboardInput()` est vrai : appliqué à la frame suivante.
- Validation : tests verts ; comportement différent : ⚠️.
- Commit : `test(ui): pin command and first-focus behaviour`
- Validation faite le 2026-10-31 : 8 tests verts, écrits sur un XAML en ligne avec `CommandName`, le chemin réel d'E1, sans dépendre de `MainMenuScreen`. Les mesures de la section Vérifications tiennent. Fait précisé par le premier passage : sans focus ni survol, le premier Entrée n'est pas perdu, l'auto-focus prend le premier bouton et sa commande s'exécute (`UIFocusNavigationService.cs:298-310`) ; sous un bouton survolé, rien ne s'exécute. Mon attente initiale était fausse, pas le plan, qui ne traite que du survol. `default_focus` reste nécessaire : survol, et cible autre que le premier bouton. ADR-0078 précisé en conséquence.

## Phase 1 — Format et runtime

### ✅ T1.1 — Commandes dans l'enveloppe

- Objectif : charger et sauver les champs de P1.
- Fichiers : `CasaEngine/Framework/UI/MGUI/UIScreenAsset.cs`, `CasaEngine/Framework/UI/MGUI/UIScreenCommandData.cs`, `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`, `CasaEngine.Tests/UI/UIScreenAssetSerializationTests.cs`.
- Étapes : types et enum avec `Unknown` ; lecture optionnelle sans exception ; écriture hors défaut.
- Validation : champs absents non réécrits ; aller-retour ; `Unknown` préservé.
- Commit : `feat(ui): add declarative commands to the UIScreen envelope`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` 4392/4392, dont 20 dans `UIScreenAssetSerializationTests`. Choix de mise en œuvre : `UIScreenAsset.Load` ne lève pas sur ces champs, car `AssetLoader` avalerait l'exception ; une valeur mal formée va dans `CommandFormatErrors`, que `DataDrivenScreen` refusera en T1.2. Limite : une entrée mal formée n'est pas réécrite par une sauvegarde de l'enveloppe. Aucun chemin de l'éditeur n'écrit aujourd'hui un `.uiscreen` existant : `UIScreenEditorSession.Save` n'écrit que le XAML.

### ✅ T1.2 — `DataDrivenScreen`

- Objectif : exécuter les commandes.
- Fichiers : `CasaEngine/Framework/UI/DataDrivenScreen.cs`, `CasaEngine/Framework/UI/UIScreenActionContext.cs`, `CasaEngine.Tests/UI/DataDrivenScreenTests.cs`.
- Étapes : `OnWindowLoaded` enregistre commandes et sons, résout `default_focus`, puis valide ; `Update` appelle `Focus` une seule fois, à la première frame où `IKeyboardHandlerHost.CanReceiveKeyboardInput()` de la cible est vrai ; contexte à délégués, sans interface.
- Validation : headless : même liste au clic et à Entrée, ordre, chaque erreur, bouton non lié ; `default_focus` appliqué après la mise en page ; Entrée sous survol exécute la liste.
- Commit : `feat(ui): add data-driven XAML screen`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` 4425/4425, dont `DataDrivenScreenTests` 33/33 en sans-affichage. Gestes réels renvoyés à T2.2. Fichier hors liste : `XamlUIScreenBase.cs` gagne deux accesseurs protégés additifs, `Asset` et `AssetFilePath`, seul moyen de lire l'enveloppe acquise par le constructeur à gestionnaire d'assets. O4 appliqué : un clic hors sommet est abandonné, pas différé. Ajouts conformes à P3 : une cible sur `close_screen` ou `quit`, et un `default_focus` non focalisable, sont refusés au chargement. `Hide` pendant la liste qui ferme l'écran reporte `Dispose` à la fin de la liste, car `AssetHandle.Asset` lève après `Dispose` et un `play_sound` peut suivre `close_screen`.

### ✅ T1.3 — Consommer `FirstScreenName`

- Objectif : ouvrir le premier écran sans code.
- Fichiers : `CasaEngine/Framework/Application/GameManager.cs`, `CasaEngine.Tests/UI/FirstScreenTests.cs`.
- Étapes : méthode interne, `IUIViewRuntime` factice (`InputRouterTests.cs:291`), politique d'exécution passée en paramètre (`_game` est nul en test, `GameManager.cs:54-57`) ; vide : rien ; sous `UseExternalViewManagement` (`GameplayExecutionPolicy.cs:44,58`, édition et Play-in-Editor) : rien, ni écran ni exception, testé avant la recherche de vue ; sans vue active : exception (Runtime uniquement).
- Validation : tests ; clés vides sans effet ; `EditorPreview` et `EditorSimulation` : aucun écran poussé malgré une vue active et `FirstScreenName` renseigné, aucune exception sans vue active.
- Commit : `feat(app): open the project's first screen from data`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` 4437/4437, dont `FirstScreenTests` 12/12. `OpenFirstScreen` est testé directement, car `UpdateWorld` exige un jeu : l'appel réel dans `UpdateWorld` et le contexte d'actions construit par `GameManager` restent à prouver par le smoke de T2.2. O1 et O3 appliqués : aucun écran à l'entrée d'un autre monde, rien dans l'éditeur. `open_screen` et `change_world` vérifient aussi le type du catalogue (`uiscreen`, `world`).

## Phase 2 — Éditeur, sample, doc

### ✅ T2.1 — `CommandName` dans l'inspecteur

- Objectif : éditer le nom, prouver sa survie.
- Fichiers : `CasaEngine.EditorServices/ScreenEditor/Inspector/UIPropertyRegistry.cs`, `CasaEngine.Tests/ScreenEditor/UIScreenEditorSessionTests.cs`.
- Étapes : ajouter le descripteur.
- Validation : aller-retour XAML.
- Commit : `feat(editor): expose Button CommandName`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` 4439/4439. Aller-retour XAML prouvé par `UIScreenEditorSessionTests` : le nom survit à l'ouverture, à la modification, à la sauvegarde et au rechargement. La ligne de l'inspecteur n'a pas été regardée dans l'éditeur lancé.

### 🧪 T2.2 — SampleProject sans DLL

- Objectif : sample obligatoire.
- Fichiers : `Projects/SampleProject/` : `main-menu.*`, écran secondaire, monde vide, son copié de `CasaEngine.Demos/Content/Audio`, `AssetInfos.json`, `SampleProject.json`.
- Étapes : couvrir les cinq actions.
- Validation : smoke ; 🧪 pour les gestes réels.
- Commit : `feat(sample): add zero-DLL menu`
- Validation faite le 2026-10-31 : les deux `.sln` 0 erreur ; `CasaEngine.Tests` 4446/4446. Smoke sur le vrai Launcher, gestes injectés dans la fenêtre (souris, clavier) et captures relues :
  - `FirstScreenName` ouvre le menu sans DLL, y compris par la commande du README lancée depuis la racine ;
  - clic sur Options : son chargé, popup modal ouvert ; clic sur Back : popup fermé ; clic sur Quit : code de sortie 0 ;
  - Entrée sur Play : `NextWorld` s'affiche et le menu disparaît avec l'ancienne vue ; Bas, Entrée : popup, 3 essais sur 3 ; Entrée : Back ; Bas, Bas, Entrée après fermeture : Quit, code 0 ;
  - après fermeture du popup, Entrée rouvre Options : le focus revient au bouton d'origine, et un second popup se construit ;
  - `log.txt` sans exception. Seul avertissement, antérieur à ce chantier : `DockClose` non résolu.
- Trois défauts trouvés par ce smoke, invisibles en sans-affichage, corrigés dans `DataDrivenScreen` avec leurs tests :
  - O4 : le clic sur l'écran gelé était bien abandonné, mais MGUI remontait sa fenêtre devant le modal. Le modal caché restait au sommet et plus rien ne répondait. Le modal au sommet se remet devant dans `Update`, avant le dessin.
  - Après la fermeture d'un écran, le focus restait sur son élément retiré, ce qui coupait tout le clavier. `Hide` ferme maintenant la fenêtre par MGUI. La cause est plus large : `ScreenStack` retire une fenêtre sans prévenir MGUI, donc les autres écrans y restent exposés (hors périmètre).
  - `CanCloseWindow="False"` laisse le bouton de fermeture affiché. Ce correctif-là est dans le XAML du sample, qui pose aussi `IsCloseButtonVisible="False"`, pas dans `DataDrivenScreen`.
- Faux défauts écartés, dus à l'injection : SDL ignore une touche sans code de scan, et les flèches exigent le drapeau de touche étendue.
- Reste 🧪 pour l'auteur : entendre le son, car seul son chargement est prouvé ; manette, non testée (O5) ; passage humain. Visuel du focus (O5) : visible en mode navigation (bouton plus clair), rien au démarrage tant que la souris reste le mode actif. Si Entrée ne répond pas juste après le lancement, activer d'abord la fenêtre : `MonoGameWindowInputSource` coupe le clavier d'une fenêtre inactive.
- Ajouts hors liste : `Projects/SampleProject/README.md` (règle des samples), test `EverySampleMenuScreen_PassesTheChecksOfADataDrivenScreen`.

### ✅ T2.3 — Documentation

- Objectif : format et limites.
- Fichiers : `docs/engine/data-driven-screens.md`, `docs/README.md`.
- Étapes : usage, limites.
- Validation : relecture.
- Commit : `docs(ui): document data-driven screens`
- Validation faite le 2026-10-31 : relu contre `DataDrivenScreen`, `GameManager` et le smoke de T2.2. Le maintien du son après la fermeture de l'écran vient de la lecture du code, il n'a pas été écouté.

## Phase 3 — Clôture

### 🚧 T3.1 — Vérification et archive

- Objectif : vérification par un sous-agent neuf en lecture seule (rôle `verifier`, `AGENTS.md` §8, modèle opus ; aucune définition n'existe dans `.claude/agents`), plan archivé.
- Fichiers : ce plan, `ai-agent/README.md`.
- Étapes : validation globale, archive, index.
- Validation : verdict du vérificateur.
- Commit : `docs(plan): close data-driven screen flow`
- Premier passage le 2026-10-31 : quatre relecteurs neufs en lecture seule (modèle opus), chaque constat P0 à P2 soumis à un contradicteur. Aucun P0 ni P1, deux P2 confirmés, un P2 ramené à P3, aucun réfuté :
  - P2 : un écran ouvert au clavier sans `default_focus` était inaccessible au clavier et à la manette, le focus restant sur l'écran gelé. Corrigé : le premier bouton prend le focus.
  - P2 : `play_sound` acceptait une cible d'un autre type, ce que l'ADR dit refusé, et pouvait laisser un `SoundAsset` en cache sous l'id d'un écran. Corrigé : `FindSoundAsset` contrôle le type du catalogue.
  - P3 : `IsFocusable = false` ne libérait pas une `TextBox`, et l'historique de focus gardait chaque fenêtre fermée. Corrigés : `Hide` passe par `TryCloseWindow`.
  - P3 : contrôles du catalogue, `Hide` depuis une commande et `Hide` après une commande sans fermeture étaient sans test. Tests ajoutés, dont `ScreenAssetLookupTests`.
  - P3 : phrase de `is_modal` trop large. Doc corrigée. P4 : effet de `FirstScreenName` sur les projets à DLL ajouté à la doc, note de T2.2 corrigée.
- Second passage : verdict REFUTED, deux P2 introduits par ces correctifs, plus un P3 important. Corrigés :
  - P2 : le premier bouton retenu pouvait être désactivé ou masqué, et l'écran redevenait inaccessible au clavier. Les boutons sont maintenant des candidats, testés dans l'ordre à chaque image, sans allocation, jusqu'au premier capable de prendre le focus.
  - P2 : avec un effet de sortie, `TryCloseWindow` redonnait le focus à la fenêtre qui se ferme, et le clavier mourait. La fenêtre passe d'abord en arrière-plan. Test `AScreenWithAnExitEffect_DoesNotKeepTheFocusWhenItCloses`, dont l'échec sans le correctif a été constaté.
  - P3 : sans `default_focus`, tout écran avec un bouton prenait le clavier, y compris un HUD sur un monde jouable. Le repli ne prend plus le focus que s'il est tenu par une autre fenêtre.
  - Infobulles et menus contextuels sont écartés des candidats. L'effet de sortie qui ne s'affiche pas est documenté comme limite.
- Les deux `.sln` 0 erreur ; `CasaEngine.Tests` 4469/4469. Attention : les `.sln` ne reconstruisent pas `CasaEngine.Tests`, donc `dotnet test --no-build` après un `dotnet build` de solution peut tester d'anciens binaires. C'est arrivé ici après la mutation : le test échouait sur le mutant resté dans `CasaEngine.Tests/bin`, le source était juste.
- Smoke rejoué sur le Launcher : popup avec effet de sortie, premier bouton désactivé et sans `default_focus` : Back prend le focus, Entrée ferme, Bas puis Entrée atteint Quit, code 0. Parcours standards clavier et souris rejoués, code 0, `NextWorld` chargé par Entrée, aucune exception.
- O7 vérifié : la PR 7 de `ui-integration.md` prévoit `GameScreenState`, `IGameScreen` et ses écrans ; cette branche n'en crée aucun et ne touche pas `GameScreenManager`.
- Constats laissés en l'état, sans décision à prendre ici : l'appel à `OpenFirstScreen` dans `UpdateWorld` et le contexte réel ne sont prouvés que par le smoke, car `UpdateWorld` exige un jeu ; le gel sous un modal reste non testé, car `ScreenStack` exige un `UIRoot` ; un `DataDrivenScreen` repoussé par du code après `Hide` échoue tard, usage que la classe documente comme interdit ; `open_screen` refuse une enveloppe `.screen`, format hérité qu'aucun code ne lit (ADR-0035).

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Écran à l'entrée d'un autre monde ? Reco : hors E1, champ `.world` additif plus tard. | T1.3 |
| O2 | Le fondu final exige une attente, absente de P2. Reco : E1 s'arrête à `change_world`, E4 ajoute l'action. | T0.2 |
| O3 | L'éditeur (édition et Play-in-Editor) n'ouvre pas l'écran ; actions en JSON manuel. Reco : accepter. | T1.3, T2.1 |
| O4 | Clic sur un écran gelé sous un modal : UNKNOWN. Reco : ignorer hors sommet. | T1.2 |
| O5 | Manette, visuel du focus : UNKNOWN. Reco : 🧪. Si le fond du sample ne doit pas être bleu, son monde vide pose `environment.background_mode` et `background_color`. | T2.2 |
| O6 | Analyse dédiée exigée ? Reco : non. | — |
| O7 | Recouvrement avec `ui-integration.md` PR 7. Reco : ne pas créer ses types. | T3.1 |

## Hors périmètre

- E2 à E4, conditions, sauvegarde, intro, panneau d'actions.
- Tout contenu propre à un jeu dans le moteur.
