# Plan agent IA — Suite du zéro allocation MGUI (dessin, contrôles, texte)

Plan d'exécution des suites listées à la clôture des deux chantiers archivés
[mgui-desktop-frame-nonalloc-tasks.md](archive/mgui-desktop-frame-nonalloc-tasks.md) et
[mgui-text-nonalloc-tasks.md](archive/mgui-text-nonalloc-tasks.md).
L'auteur a demandé le 2026-11-15 : « Oui, planifie-les et suis les choix que tu recommandes ». Les décisions D1 → D12
ci-dessous sont donc les recommandations de l'agent, appliquées sur cette demande : **ce plan les applique, il ne les
rediscute pas**. L'auteur peut en refuser une à l'approbation du plan.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Retirer les allocations par image qui restent dans une interface MGUI dessinée par le moteur, et le seul défaut connu
de l'API de dessin :

1. **Dessin** : `SetDrawSettingsTemporary`, `SetTransformTemporary`, `SetEffectTemporary` et le clip à stencil
   n'allouent plus ; une référence périmée de clip ou de changement temporaire libérée une seconde fois ne libère plus
   l'usage suivant ; le `MainRenderer` de MGUI garde sa transaction.
2. **Contrôles** : `MGGrid` avec sélection, `MGDockHost`, fenêtre en `SizeToContent`, et les familles jamais mesurées
   (défilement, listes, arbre, onglets, menus, saisie) à 0 octet par image au repos, et pendant l'interaction nommée
   dans chaque tâche.
3. **Texte** : bloc sans balise souligné ou à fond, révélation par `TextCharactersPerSecond`, valeur de balise refusée,
   et grammaire des balises en ASCII pour que le lecteur sans allocation lise tous les textes.

Ce que le chantier ne livre pas est dans « Hors périmètre ».

## État vérifié du dépôt (2026-11-15)

Branche `chantier/mgui-nonalloc-suite` créée depuis `main` (`6e21e841`) ; MGUI sur `develop` (`a693289f`). Faits relevés
par trois éclaireurs en lecture seule puis revérifiés par `rg` pour ceux qui portent une décision.

Dessin :

- `MGUI/MGUI.Shared/Rendering/IUIRenderContext.cs:14-23` : `SetDrawSettingsTemporary`, `SetRenderTargetTemporary`,
  `SetTransformTemporary` et `SetClipTargetTemporary` rendent `IDisposable` ; `PushClipTemporary` et
  `PushRectangleClip` rendent `ClipScope`.
- `MGUI/MGUI.Shared/Helpers/TemporaryChange.cs:10` : `TemporaryChange<TItem>` est une classe. Elle est créée à chaque
  appel dans `CasaEngine/Framework/UI/Backend/MonoGame/CasaDrawStateController.cs:132` et
  `MGUI/MGUI.MonoGame.Integration/Rendering/DrawTransaction.cs:1046` (réglages), et dans `CasaRenderTargetService.cs:33`
  et `DrawTransaction.cs:1020` (cible de rendu).
- `MGUI/MGUI.Shared/Rendering/DrawSettings.cs:85` : `DrawSettings` est un `sealed record class` ; chaque `with` crée un
  objet. `SetTransformTemporary` fait `CurrentSettings with { Transform = ... }` (`CasaDrawTransaction.cs:708-711`,
  `DrawTransaction.cs:1049-1052`).
- Clip à stencil du moteur : `CasaEngine/Framework/UI/Backend/MonoGame/Clipping/CasaStencilClipExecutor.cs:29-83` fait
  trois `with` (`:48`, `:58`, `:71`), trois changements temporaires, et `new ClipScope(..., () => ...)` (`:67`) avec
  une fermeture. Même forme dans MGUI : `MGUI.MonoGame.Integration/Rendering/Clipping/ClipManager.cs:127` et `:195`.
- `MGUI/MGUI.Shared/Rendering/Clipping/ClipAbstractions.cs:91` : `ClipScope` est une `sealed class` réarmable
  (`Reset`, `ResetRectangle`). `Dispose` est sans effet si `_IsDisposed`, donc une référence périmée libérée après le
  réarmement de l'objet libère l'usage suivant (constat reporté du chantier précédent).
- `MGUI/MGUI.MonoGame.Integration/Rendering/MainRenderer.cs:92` et `:125` : une `DrawTransaction` créée à chaque appel.
  Utilisé par `MGUI.Samples`, `MGUI.Editor.Host` et `MGUI.MiniGame` (`MonoGameBackendBootstrap.Create`).
- Mesures existantes, sans affirmation : `CasaEngine.Tests/UI/Backend/DesktopFrameGpuAllocationTests.cs`
  (`WhatTheFrameStillAllocates`) et `MGUI/MGUI.Tests/Integration/ClipScopeRemainingAllocationMeasurements.cs`. Valeurs
  notées à la clôture du chantier précédent : stencil 1112 octets par paire, `SetDrawSettingsTemporary` 112,
  `SetTransformTemporary` 304.

Contrôles :

- Banc : `MGUI/MGUI.Tests/Architecture/DesktopFrameAllocationMeasurements.cs` (`enum Fixture` `:33-48` : `NoWindow` à
  `FullInDockHost`, `Measure` `:126-142`, 500 images de chauffe puis 500 mesurées, arrondi vers le haut) et
  `DesktopFrameZeroAllocationTests.cs`. Aucune fixture n'existe pour : sélection de grille, séparations ou aperçu de
  dépôt d'un `MGDockHost`, `SizeToContent`, `MGScrollViewer`, `MGListBox`, `MGListView`, `MGTreeView`, `MGTabControl`,
  `MGContextMenu`, `MGMenuBar`, `MGTextBox`.
- `MGUI/MGUI.Core/UI/Containers/Grids/MGGrid.cs:74-108` : `GridSelection` implémente `IEnumerable<GridCell>` par un
  itérateur `yield` ; `EndingDrawInternal` (`:1739`) et `DrawSelf` (`:1759`) l'énumèrent à chaque dessin.
- `MGUI/MGUI.Core/UI/MGScrollViewer.cs:1233` : `$"{ElementType}.Viewport"` construit à chaque dessin.
- `MGDockHost.cs` : `GetAllVisibleTabGroups` (`:3033`) et `GetAllSplitContainers` (`:3112`) sont des itérateurs
  `yield` appelés pendant un glissement ; `GetForbiddenZones` (`:2360`) crée un `HashSet` par appel ;
  `MGDockPreviewOverlay.cs:240` (`GetChildren`) est un itérateur énuméré au dessin.
- `MGWindow.cs:1831-1853` : une fenêtre en `SizeToContent` repasse par `RevalidateSizeToContent` puis
  `ApplySizeToContent` (`:376-414`) à chaque image. La cause des 488 à 1422 octets n'est pas localisée.
- Non localisé par les éclaireurs : les 32 octets par image d'un `MGDockHost` après un changement de texte.

Texte :

- `MGUI/MGUI.Core/UI/MGTextBlock.cs:2017` : un bloc sans balise qui a un fond ou un soulignement ne prend pas la
  boucle sans allocation ; il prend la boucle historique de `DrawSelf` (`:2032` et suivantes), qui construit les
  lignes publiques et fait `Substring` (`:2066`).
- `MGTextBlock.cs:826` : `new TextRevealRun` à chaque resynchronisation de la révélation
  (`SyncTextProgressAnimation`, `:798-838`).
- `MGUI/MGUI.Core/UI/Text/FormattedTextScanner.cs:778` et `:895` : les deux seuls endroits qui rendent un texte
  « ambigu » (chiffre non ASCII, caractère non ASCII dans les dix caractères qui suivent un `[` sans type) et le
  laissent au code historique.
- `FormattedTextScanner.cs:201-210` et `:687-764` : une valeur dont l'interprétation lève n'est pas gardée en cache ;
  l'exception est levée et rattrapée à chaque lecture du texte.
- Un seul plafond de cache dans le lecteur : `CacheLimit = 128` (`FormattedTextScanner.cs:27`), noms compris. Le
  « 64 noms » des notes de clôture n'existe pas dans le code.
- Aucun tampon du texte ne rétrécit (`PlainTextLayout.cs`, `FormattedTextRuns.cs`, `FormattedTextLayout.cs`,
  `FormattedTextScanner.cs`).

Arbre de travail : modifications de l'auteur à ne jamais indexer : `.serena/project.yml`, `.codegraph/`,
`.serena/memories/`, `Projects/SampleProject/.casaeditor/viewport.editor.json`.

## Décisions verrouillées

Recommandations de l'agent, appliquées à la demande de l'auteur du 2026-11-15.

| Réf | Décision |
|---|---|
| D1 | Ordre : mesurer d'abord (phase 0), puis dessin (phase 1), contrôles (phase 2), texte (phase 3), clôture (phase 4). Les phases 1, 2 et 3 sont indépendantes ; un blocage dans l'une n'arrête pas les autres. |
| D2 | Rupture d'API acceptée, sans alias `[Obsolete]` (mémoire `modern-design-over-api-compat`, auteur, 2026-11-15) : les méthodes `Set...Temporary` et `Push...Clip` de `IUIRenderContext` rendent des poignées `struct` au lieu de `IDisposable` et de la classe `ClipScope`. |
| D3 | Une poignée porte son propriétaire, un numéro d'emplacement et une génération. `Dispose` d'une poignée dont la génération n'est plus celle de l'emplacement est sans effet, comme un second `Dispose` aujourd'hui. Pas d'exception : un `Dispose` en double n'était pas une erreur avant. |
| D4 | `DrawSettings` reste une classe immuable. Les réglages dérivés du clip à stencil (trois par profondeur) sont gardés par le backend tant que les réglages de départ sont le même objet. `SetTransformTemporary` et `SetEffectTemporary` dérivent aujourd'hui un `DrawSettings` par appel (`with`) : leur mécanisme sans objet par appel est choisi en T1.1 après lecture du contrôleur d'état (point ouvert O1), écrit dans un ADR, et livré en T1.3. |
| D5 | Les deux backends sont traités : le moteur (`CasaEngine/Framework/UI/Backend/MonoGame/`) et celui de MGUI (`MGUI.MonoGame.Integration`). Le `MainRenderer` de MGUI garde sa transaction comme `CasaDesktopRuntime` (ADR-0097) : une par runtime, une seule ouverte à la fois, une seconde demande lève. |
| D6 | Le chemin à masque (`CasaMaskClipExecutor`, jamais choisi par défaut) reçoit les poignées pour compiler, mais n'est pas amené à zéro. |
| D7 | Un contrôle est « à zéro » quand `MGDesktop.Update` puis `MGDesktop.Draw` allouent 0 octet par image, total exact sur 500 images après 500 de chauffe, sur le banc existant, au repos et pendant l'interaction nommée dans sa tâche. Les opérations ponctuelles (ouvrir un menu, changer d'onglet, ajouter un élément) ne sont pas dans la cible. |
| D8 | Ce que la phase 0 mesure à 0 octet est noté et sa tâche est fermée sans changement de code. Une allocation dont la cause est hors des fichiers d'une tâche est mesurée, écrite dans le plan, et la tâche s'arrête en ⚠️ : pas d'élargissement en silence. |
| D9 | Grammaire des balises en ASCII : un type de balise et un nombre ne sont faits que de lettres et de chiffres ASCII, dans le code historique comme dans le lecteur. `[o=٠.٥]` (chiffres arabes-indiens) devient du texte. Le lecteur ne laisse plus de texte au code historique, sauf une image sans taille dans un bloc sans bureau. Changement de sens du balisage, à noter dans un ADR de MGUI. |
| D10 | Le code historique (`FTTokenizer`, `FTParser`, `MGTextRun.ParseRuns`, `ForceLegacyTextPath`) reste : API publique et référence des tests différentiels. Ses coûts connus ne sont pas corrigés, puisque D9 le sort du chemin d'exécution du bloc. |
| D11 | Une valeur de balise refusée est gardée en cache comme refusée (même plafond de 128), pour ne lever qu'une fois par valeur. |
| D12 | Restent des limites documentées, non traitées : le plafond de 128 entrées des caches, les tampons qui ne rétrécissent pas, le changement de police (opération ponctuelle). |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/mgui-nonalloc-suite`** dans le moteur, créée depuis `main` ; branche du même nom dans
  MGUI, créée depuis `develop` à la première tâche qui touche MGUI. Ne jamais committer sur `main` ni sur `develop`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la
  validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche,
  puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche** et par dépôt : le commit de MGUI d'abord, puis le commit du moteur qui porte la référence
  du sous-module, le code moteur de la tâche et ce plan. Message en anglais `type(area): summary`. Chaque commit du
  moteur compile avec la référence de MGUI qu'il porte.
- **Ne jamais pousser.** La fusion dans `main` et `develop` reste une décision de l'auteur.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de
  l'auteur, ou d'une doc officielle citée (URL). Sinon : ⚠️ Blocked, question dans « Points ouverts », **arrêt** de la
  phase concernée (D1).
- **Build obligatoire** avant ✅ dès que du code est touché : `dotnet build MGUI/MGUI.sln`,
  `dotnet build CasaEngine.MonoGame.sln`, `dotnet build CasaEngine.Editor.MonoGame.sln`. **Tests** :
  `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` et `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, sans
  `--no-build` (binaires de test périmés sinon, constaté le 2026-11-15).
- `git commit` précédé de `cd D:/development/repo/CasaEngineMonogame` (ou `.../MGUI`) : le hook de commit ne résout
  pas les chemins `/d/...`.
- **Mesure avant et après** : chaque tâche qui vise zéro écrit sous elle les octets par image mesurés avant et après.
  Un test de zéro arrondit la moyenne vers le haut et affirme sur le total.
- **Rendu inchangé** : une tâche de dessin ou de contrôle garde les appels de dessin identiques (comparaison des
  appels enregistrés ou des pixels sur GPU réel, comme dans le chantier précédent). Tout changement de rendu voulu est
  nommé dans la tâche.
- **Vérification indépendante** (`verifier`, modèle opus) à la fin de la phase 1 (rupture d'API sur deux dépôts) et de
  la tâche T3.4 (sens du balisage), avant de les déclarer faites. Les tâches de la phase 2 et T3.1 à T3.3 n'en ont
  pas : elles ne changent ni API ni sens, et leur preuve est mécanique (test de zéro et appels de dessin identiques).
- **Sample** : à la fin de T1.6, de la phase 2 et de T3.1, lancer `MGUI.Samples` une fois et noter qu'il démarre et
  dessine sans exception. Le jugement du rendu à l'œil reste à l'auteur (smoke de la validation globale).
- Si le code est écrit mais qu'une vérification manque, utiliser `🧪 Needs testing` et noter ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications de l'auteur : `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais ; les fichiers
  d'architecture de `MGUI/Docs/*.md` en français sans accents, comme ils le sont.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; restaurer tout état GPU modifié.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build MGUI/MGUI.sln`, `dotnet build CasaEngine.MonoGame.sln`, `dotnet build CasaEngine.Editor.MonoGame.sln` :
  0 erreur.
- `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` et `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout
  vert, aucun test sauté (référence au 2026-11-15 : 3684 et 5550).
- Les tests de zéro ajoutés par ce plan affirment 0 octet ; les fichiers de mesure sans affirmation
  (`WhatTheFrameStillAllocates`, `ClipScopeRemainingAllocationMeasurements`) deviennent des affirmations pour ce que la
  phase 1 amène à zéro.
- Smoke manuel, à faire par l'auteur (l'agent ne juge pas un rendu à l'œil) : lancer `MGUI.Samples` et l'éditeur,
  ouvrir un écran avec coins arrondis, un dock, une liste défilante ; rien ne doit changer à l'écran.

---

## Phase 0 — Mesure

### ✅ T0.1 — Fixtures et mesures des cas connus et des familles non mesurées

- Objectif : un chiffre par image, `Update` et `Draw` séparés, pour chaque cas que les phases 1 à 3 visent. Aucun
  code produit n'est modifié.
- Fichiers : `MGUI/MGUI.Tests/Architecture/DesktopFrameAllocationMeasurements.cs` (nouvelles fixtures), un nouveau
  fichier de mesure `MGUI/MGUI.Tests/Architecture/ControlFamilyAllocationMeasurements.cs` si l'`enum Fixture` devient
  illisible, `MGUI/MGUI.Tests/Text/TextBlockAllocationMeasurements.cs`, ce plan.
- Étapes :
  1. Ajouter une fixture par cas, chacune dans une fenêtre du banc existant : `MGGrid` avec une sélection de cellule ;
     `MGDockHost` à deux groupes séparés, avec un panneau masqué, et pendant un glissement d'onglet (aperçu de dépôt) ;
     `MGDockHost` après un changement de texte d'un bloc ; fenêtre en `SizeToContent` ; `MGScrollViewer` au repos et
     pendant un défilement à la molette ; `MGListBox` et `MGListView` de 50 éléments au repos et avec la souris qui
     passe d'un élément à l'autre ; `MGTreeView` de trois niveaux dépliés, au repos et au survol ; `MGTabControl` de
     trois onglets, au repos et au survol des en-têtes ; `MGContextMenu` ouvert et `MGMenuBar`, au repos et au
     survol ; `MGTextBox` avec le focus, au repos (curseur qui clignote) et pendant la frappe d'un caractère par image.
  2. Texte : bloc sans balise souligné, bloc sans balise à fond, révélation par `TextCharactersPerSecond` avec un
     changement de texte toutes les 100 images, texte dont une balise a une valeur refusée changé à chaque image,
     texte avec un caractère non ASCII près d'un `[` changé à chaque image.
  3. Les nouveaux cas sont des mesures sans affirmation (écrites dans la sortie du test, comme
     `WhatTheFrameStillAllocates`) : ils ne rejoignent pas les théories `AnIdleFrame` et `ADragFrame`, qui affirment 0.
     Lancer les mesures, écrire le tableau des octets par image sous cette tâche.
  4. Pour chaque cas à 0 octet : le noter, et marquer ✅ la tâche correspondante des phases 2 et 3 avec « mesuré à
     zéro en T0.1 » (D8). Pour chaque cas qui alloue : confirmer ou corriger la liste de fichiers de sa tâche.
- Validation : les mesures tournent (`dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj --filter Measurements`), le
  tableau est dans le plan, les deux suites complètes restent vertes.
- Commit : `test(ui): measure the frame of the control families and text cases left to bring to zero`
- Validation (2026-11-16) : `MGUI.sln` 0 erreur ; `MGUI.Tests` 3706 verts (3684 + 22 mesures), aucun sauté ; mesures
  relancées par la session principale, chiffres identiques sur trois lancements. Aucun code produit modifié, donc
  ni build ni tests du moteur. Fichiers : `MGUI.Tests/Architecture/ControlFamilyAllocationMeasurements.cs` et
  `MGUI.Tests/Text/TextCaseAllocationMeasurements.cs` (collection xunit non parallèle).

  Octets par image, 500 images après 500 de chauffe :

  | Cas | Update | Draw |
  |---|---|---|
  | `MGGrid` 3x3, une cellule sélectionnée, repos | 0 | 288 |
  | `MGGrid`, sélection déplacée par un clic toutes les 2 images | 2060 | 288 |
  | `MGDockHost` deux groupes et une séparation, repos | 160 | 40 |
  | `MGDockHost`, un panneau en masquage automatique (bandeau), repos | 160 | 208 |
  | `MGDockHost`, un onglet derrière l'onglet actif, repos | 160 | 40 |
  | `MGDockHost`, glissement d'onglet avec aperçu de dépôt | 3248 | 848 |
  | `MGDockHost` (un groupe), après un changement de texte | 0 | 0 |
  | Fenêtre `SizeToContent` (`WidthAndHeight` et `Height`), repos | 0 | 0 |
  | `MGScrollViewer`, repos | 136 | 210 |
  | `MGScrollViewer`, molette | 192 | 331 |
  | `MGListBox` 50 éléments, repos et survol | 272 | 210 |
  | `MGListView` 50 éléments, repos et survol | 136 | 786 |
  | `MGTreeView` 14 nœuds sur trois niveaux, repos et survol | 136 | 786 |
  | `MGTabControl` trois onglets, repos | 0 | 0 |
  | `MGTabControl`, survol des en-têtes | 88 | 0 |
  | `MGContextMenu` ouvert, repos | 272 | 1178 |
  | `MGContextMenu`, survol des entrées | 1280 | 1178 |
  | `MGMenuBar`, aucun menu ouvert, repos | 0 | 56 |
  | `MGMenuBar`, aucun menu ouvert, survol | 672 | 56 |
  | `MGMenuBar`, premier menu ouvert, survol des entrées | 1280 | 874 |
  | `MGTextBox` avec focus, repos (curseur clignotant ou fixe) | 0 | 0 |
  | `MGTextBox`, une touche par image (A puis retour arrière) | 3876 | 0 |

  | Cas de texte | Setter | Update | Draw |
  |---|---|---|---|
  | Bloc sans balise souligné, texte inchangé | — | 0 | 120 |
  | Révélation à 20 caractères par seconde, changement toutes les 100 images | 608 par changement | 0 | 0 |
  | `[color=NotAColor]` changé à chaque image | 2656 | 0 | 0 |
  | Caractère non ASCII après un `[`, changé à chaque image | 10847 | 10104 | 120 |
  | Référence : caractère ASCII après un `[`, changé à chaque image | 0 | 0 | 0 |

  Constats qui changent la suite :
  - O3 : les 32 octets d'un `MGDockHost` après un changement de texte sont pris une fois, à la première image après
    le changement, puis 0 sur 500 images. Ce n'est pas un coût par image : le cas « après un changement de texte »
    sort de T2.2.
  - O4 : une fenêtre en `SizeToContent` lit 0 et 0 sur la fixture `Full`. Les 488 à 1422 octets notés à la clôture du
    chantier précédent ne se reproduisent pas sur ce banc. T2.3 est fermée (D8).
  - Un bloc sans balise ne peut pas avoir de fond de texte : `UpdatePlainRuns` part de `DefaultTextRunSettings`, dont
    le fond est toujours par défaut, et seule une balise `[bg]` en pose un. T3.1 se réduit au soulignement.
  - `MGListView` et `MGTreeView` lisent le même chiffre (136 et 786) et le survol n'ajoute rien aux listes : la cause
    commune probable est le `MGScrollViewer` qu'ils contiennent (hypothèse, à vérifier en T2.4 avant T2.5 et T2.6).
  - La sélection de `MGGrid` ne bouge qu'à l'appui ou au relâchement (`MGGrid.cs:998-1008`) : l'interaction mesurée est
    un clic toutes les 2 images.
  - Le curseur de `MGTextBox` lit `DateTime.Now` (`MGTextCaret.cs:43`), pas l'horloge de l'update.
  - « Panneau masqué » a été mesuré dans ses deux lectures (masquage automatique, onglet derrière l'actif).
  - Aucune tâche de la phase 2 autre que T2.3 n'est à zéro : `MGTabControl` et `MGTextBox` le sont au repos seulement.

---

## Phase 1 — Dessin : poignées et clip à stencil

### ✅ T1.1 — ADR et types des poignées

- Objectif : les poignées de D2 et D3 existent dans `MGUI.Shared`, décrites par un ADR de MGUI, sans appelant encore
  migré ; le mécanisme de `SetTransformTemporary` est choisi (O1).
- Fichiers : `MGUI/MGUI.Shared/Rendering/` (nouveaux types de poignée), `MGUI/Docs/decisions/0031-*.md` et son index,
  `MGUI/MGUI.Tests/Rendering/` (tests des poignées : génération, second `Dispose`, poignée par défaut).
- Étapes :
  1. Lire `CasaDrawStateController.cs`, `CasaDrawTransaction.cs` (`SetDrawSettings`, `Rearm`), `DrawTransaction.cs`
     de MGUI et `ClipAbstractions.cs` ; relever tous les lecteurs de `ClipScope.Resolution` (5 occurrences hors tests
     au 2026-11-15) et tous les appelants qui gardent un `IDisposable` dans un champ ou une variable typée.
  2. Choisir pour O1 (transformation et effet temporaires) entre : une pile de matrices et d'effets dans le
     contrôleur d'état, appliquée sans créer de `DrawSettings` ;
     ou un petit nombre d'objets `DrawSettings` réutilisables possédés par la transaction. Critère : zéro allocation,
     `CurrentSettings` reste lisible par les appelants actuels, égalité de `DrawSettings` (ADR-0029) respectée.
  3. Écrire l'ADR (contexte, décision, conséquences, ruptures) et les types, avec leurs tests.
- Validation : `MGUI.sln` 0 erreur, `MGUI.Tests` vert. Si aucune des deux options de l'étape 2 ne tient le critère :
  le fait est écrit ici, T1.3 passe en ⚠️ (O1) et le reste de la phase 1 continue : T1.2, T1.4, T1.5 et T1.6 ne
  dépendent pas d'O1.
- Commit (MGUI) : `feat(rendering): add generation handles for temporary draw state and clips`
- Validation (2026-11-16) : `MGUI.sln` 0 erreur ; `MGUI.Tests` 3718 verts (12 tests ajoutés, `Rendering/GenerationHandleTests.cs`), aucun sauté. Types : `DrawStateHandle` et `IDrawStateHandleOwner`, `ClipHandle` et `IClipHandleOwner`, `GenerationSlots<TState>` (emplacements partagés par les deux backends). ADR-0031 de MGUI.
  - Relevé de l'étape 1 : aucun lecteur de `ClipScope.Resolution` hors tests dans le code produit ; un seul appelant garde un `IDisposable` dans une variable hors des backends (`MGElement.cs:5395`, `TempTransform`) ; les appelants de `MGUI.Core` écrivent `using (cond ? Push(...) : null)`, remplacé par `default`.
  - O1 tranché : la transaction garde les réglages qu'elle a dérivés, retrouvés par l'identité des réglages de départ et la valeur (matrice ou effet), comme `WithRasterizerType` le fait déjà pour le scissor. Écartés : la pile de matrices (`CurrentSettings` ne serait plus les réglages en vigueur pour ceux qui le lisent ou le dérivent) et les objets réécrits (`DrawSettings` est immuable et des références sont gardées). Limite écrite dans l'ADR : une transformation dont la valeur est nouvelle (animée à chaque image) dérive encore un `DrawSettings` ; une transformation stable n'alloue rien. T1.3 n'est donc pas bloquée, avec cette limite.

### ✅ T1.2 — Changements temporaires de réglages et de cible sans allocation

- Objectif : cette tâche possède tout le changement de signature des changements temporaires. Dans
  `IUIRenderContext`, `SetDrawSettingsTemporary`, `SetRenderTargetTemporary` et `SetTransformTemporary` rendent une
  poignée `struct` au lieu de `IDisposable` ; `SetEffectTemporary`, qui n'existe que sur les deux transactions
  concrètes, aussi. 0 octet par paire appel et `Dispose` pour `SetDrawSettingsTemporary` et
  `SetRenderTargetTemporary`, dans les deux backends. `SetTransformTemporary` et `SetEffectTemporary` rendent la
  poignée mais gardent leur `DrawSettings` dérivé par appel jusqu'à T1.3 : ils ne sont pas à zéro ici, et la
  signature livrée ne dépend pas d'O1. `SetClipTargetTemporary` est un clip : il est traité en T1.4.
- Fichiers : tous ceux qui déclarent, implémentent ou appellent ces quatre méthodes (`rg "Set(DrawSettings|RenderTarget|Transform|Effect)Temporary"`
  et les erreurs du compilateur dans les deux dépôts) : `IUIRenderContext.cs`, `TemporaryChange.cs` (retiré s'il
  n'a plus d'appelant) ; moteur `CasaDrawStateController.cs`, `CasaDrawTransaction.cs`, `CasaRenderTargetService.cs`,
  exécuteurs de clip ; MGUI `DrawTransaction.cs`, `ClipManager.cs` ; appelants de `MGUI.Core`
  (`MGNineSliceFillBrush.cs:241`, `MGTextureFillBrush.cs:269`, `MGElement.cs:5400`, `MGWindow.cs:2409` et `:2431`) ;
  doublures de test qui implémentent l'interface : 15 fichiers de `MGUI.Tests` et 2 de `CasaEngine.Tests`
  (`UI/EditorControlTemplateAssetLoadingTests.cs`, `ContentBrowser/GridViewVirtualizationTests.cs`) au 2026-11-15
  (`rg "IDisposable SetTransformTemporary"` : 20 fichiers avec l'interface et les deux backends).
- Étapes : pile d'états précédents possédée par le contrôleur ; poignée rendue ; appelants migrés ; tests de zéro sur
  GPU réel (moteur) et sur le banc (MGUI) ; test du second `Dispose` et de la poignée périmée après réarmement.
- Validation : mesures avant et après écrites ici, prises par un appel typé `IUIRenderContext` (une poignée qui
  serait rendue en `IDisposable` serait boxée) ; cible 0 octet pour `SetDrawSettingsTemporary` et
  `SetRenderTargetTemporary`, chiffres restants de `SetTransformTemporary` et `SetEffectTemporary` écrits ; plus
  aucun `IDisposable Set...Temporary` dans les deux dépôts hors `SetClipTargetTemporary` ; les trois solutions 0 erreur ; les deux
  suites vertes ; mêmes pixels qu'avant sur les tests GPU existants.
- Commit : `perf(rendering)!: return a handle from temporary draw state changes` (MGUI), puis
  `perf(ui)!: change draw state temporarily without allocating` (moteur)
- Validation (2026-11-16) : les trois solutions 0 erreur ; `MGUI.Tests` 3720 verts, `CasaEngine.Tests` 5552 verts, aucun
  sauté (2 tests GPU ajoutés par dépôt : zéro allocation, poignée d'un changement terminé). Tests de pixels existants
  inchangés et verts. Plus aucun `IDisposable Set...Temporary` hors `SetClipTargetTemporary` (`rg`).

  Octets par paire appel et `Dispose`, GPU réel, 500 paires :

  | Opération | Moteur avant | Moteur après | MGUI avant | MGUI après |
  |---|---|---|---|---|
  | `SetDrawSettingsTemporary` (par `IUIRenderContext`) | 112 | 0 | 112 | 0 |
  | Deux `SetDrawSettingsTemporary` imbriqués | non mesuré | 0 | non mesuré | 0 |
  | `SetRenderTargetTemporary` | non mesuré | 0 | non mesuré | 0 |
  | `SetTransformTemporary` (reste pour T1.3) | 304 | 120 | 304 | 120 |
  | Clip à stencil (reste pour T1.5) | 1112 | 568 | non noté | 568 |

  - `SetRenderTargetTemporary` lisait 120 octets après le passage aux poignées : trois tableaux de
    `GraphicsDevice.GetRenderTargets()`. Les deux backends lisent maintenant la cible liée par
    `RenderTargetCount` et `GetRenderTargets(RenderTargetBinding[])` (MonoGame 3.8.6.1, lus dans le XML du paquet),
    sans tableau quand une seule cible est liée.
  - `SetEffectTemporary` rend la poignée ; son coût est celui de `SetTransformTemporary` (un `DrawSettings` dérivé),
    non mesuré à part ici, traité en T1.3.
  - Ajouté à `MGUI.Shared` : `CallbackDrawStateOwner`, propriétaire de poignées qui restaure par rappel, pour les
    doublures de test (17 fichiers migrés par un `mech-executor`). Retiré : `TemporaryChange<TItem, TParameter>`, sans
    appelant ; `TemporaryChange<TItem>` reste (`MGContentHost.cs:56`).
  - Écart provisoire : la `DrawTransaction` de MGUI, créée à chaque image jusqu'à T1.6, crée maintenant ses deux tables
    d'emplacements à chaque création.

### ✅ T1.3 — `SetTransformTemporary` et `SetEffectTemporary` sans allocation

- Objectif : 0 octet par paire pour les deux, par le mécanisme choisi en T1.1 (O1). Seul l'intérieur des deux
  backends change : les signatures sont déjà celles de T1.2. Tâche en ⚠️ si T1.1 n'a trouvé aucun mécanisme qui
  tient son critère ; les deux méthodes gardent alors la poignée de T1.2 et leur `DrawSettings` par appel, écrit
  comme limite.
- Fichiers : `CasaDrawStateController.cs`, `CasaDrawTransaction.cs` (`:708-711`, `:839-840`), MGUI `DrawTransaction.cs`
  (`:1049-1052`, `:1187-1188`), et leurs tests.
- Validation : comme T1.2, cible 0 octet pour les deux, mesurée par un appel typé `IUIRenderContext` pour
  `SetTransformTemporary` ; en plus un test qui dessine sous une transformation imbriquée et un sous un effet
  temporaire, et compare les pixels.
- Commit : `perf(rendering): set a temporary transform without allocating` (MGUI), puis
  `perf(ui): set a temporary transform without allocating` (moteur)
- Validation (2026-11-16) : les trois solutions 0 erreur ; `MGUI.Tests` 3730 verts, `CasaEngine.Tests` 5554 verts, aucun
  sauté. Mécanisme : `DerivedDrawSettingsCache` (`MGUI.Shared`, 32 entrées, la plus ancienne remplacée), tenu par la
  transaction du moteur et par le `MainRenderer` de MGUI ; une entrée est retrouvée par des réglages de départ égaux
  (pas seulement le même objet), donc une image retrouve ce que l'image d'avant a dérivé.

  | Opération (octets par paire, GPU réel) | Moteur avant | Moteur après | MGUI avant | MGUI après |
  |---|---|---|---|---|
  | `SetTransformTemporary`, même valeur (par `IUIRenderContext`) | 120 | 0 | 120 | 0 |
  | Deux `SetTransformTemporary` imbriqués | non mesuré | 0 | non mesuré | 0 |
  | `SetEffectTemporary`, même effet | non mesuré | 0 | non mesuré | 0 |
  | `SetTransformTemporary` dans une transaction réarmée | non mesuré | 0 | sans objet | sans objet |
  | `SetTransformTemporary`, valeur nouvelle à chaque appel (limite) | 120 | 120 | 120 | 120 |

  - Pixels : `DrawingUnderTemporaryTransformsAndEffect_DrawsThePixelsOfSettingsDerivedAtEachChange` dans les deux
    dépôts (transformations imbriquées et effet temporaire, premier et second passage, comparés à des réglages
    dérivés à chaque changement).
  - Limites écrites dans l'ADR-0031 : une valeur nouvelle à chaque appel dérive un `DrawSettings` (120 octets) ; plus de
    32 transformations ou effets temporaires distincts par image allouent de nouveau ; une entrée garde son effet
    jusqu'à son remplacement.

### ✅ T1.4 — Poignée de clip à génération

- Objectif : `PushClipTemporary`, `PushRectangleClip` et `SetClipTargetTemporary` (qui appelle `PushRectangleClip`
  dans les deux backends) rendent une poignée `struct` ; une poignée périmée libérée après la réutilisation de son
  emplacement ne libère rien ; les clips rectangulaires restent à 0 octet.
- Fichiers : tous ceux qui construisent ou reçoivent un `ClipScope` aujourd'hui (`rg "new ClipScope\("` et les
  erreurs du compilateur dans les deux dépôts) : `ClipAbstractions.cs` (la classe `ClipScope` est remplacée),
  `IUIRenderContext.cs` ; moteur `CasaClipManager.cs`, `CasaDrawTransaction.cs` (`ScissorClipSlot`,
  `SetClipTargetTemporary` `:899-902`), `CasaStencilClipExecutor.cs` et `CasaMaskClipExecutor.cs` ; MGUI
  `ClipManager.cs` (rectangle, stencil, masque), `DrawTransaction.cs` ; appelants dans `MGUI.Core` (`MGElement.cs`,
  `MGShapeElementBase.cs`, `MGGridColorPicker.cs`, `Clipping/ElementClipRequest.cs`) ; doublures de test
  (`MGUI.Tests/Graph/OrderedRecordingDrawTransaction.cs`, `MGUI.Tests/Integration/EngineOwnedRenderingProofTests.cs`,
  `MGUI.Tests/Integration/ClipScopeTests.cs`,
  et celles du moteur). Les exécuteurs à stencil et à masque sont seulement migrés vers la poignée, sans cible de
  zéro : leur fermeture et leurs réglages dérivés restent jusqu'à T1.5 (stencil) ou restent tout court (masque, D6).
- Étapes : emplacements à génération dans les deux backends ; `Resolution` lu par la poignée ; migration des
  appelants ; test « une poignée périmée ne libère pas l'usage suivant » qui échoue sur le code d'avant ;
  `ClipScopeReuseGpuTests` et `DrawTransactionRearmGpuTests` adaptés.
- Validation : comme T1.2 ; les tests de réarmement (clip non libéré, masque non libéré) gardent leur sens.
- Commit : `fix(rendering)!: make a clip handle that cannot release a later use` (MGUI), puis
  `fix(ui)!: use generation handles for clips` (moteur)
- Validation (2026-11-16) : les trois solutions 0 erreur ; `MGUI.Tests` 3732 verts, `CasaEngine.Tests` 5555 verts, aucun
  sauté. La classe `ClipScope` n'existe plus ; `PushClipTemporary`, `PushRectangleClip` et `SetClipTargetTemporary`
  rendent `ClipHandle`. Les clips rectangulaires restent à 0 octet (`ClipScopeReuseGpuTests`, images du moteur à 0).
  - Ajouté à `MGUI.Shared` : `ClipSlots` (emplacements des clips en vigueur d'un backend, un état `ClipSlotState` par
    clip, restauration par `IClipSlotRestorer`), partagé par les deux backends, et `CallbackClipHandleOwner` pour les
    doublures de test (19 fichiers migrés par un `mech-executor`).
  - Poignée périmée : `AHandleKeptAfterItsDispose_DoesNotReleaseTheNextClip` (MGUI, GPU réel),
    `AClipHandleOfAClipThatIsOver_ReleasesNothing` (moteur, avec réarmement), `ClipSlotsTests` (remplace
    `ClipScopeTests`, retiré avec la classe). Sur le code d'avant, la référence gardée était l'objet réarmé du clip
    suivant et sa libération le libérait : ce cas ne peut plus s'écrire, la poignée étant une valeur.
  - Un réarmement de la transaction oublie les clips restés ouverts (`ClipSlots.ReleaseAll`) : les tests
    `Rearm_AfterAnUndisposed...` (rectangle, sans clip, stencil, masque) sont verts sans changement de sens.
  - Constat corrigé en route : `ClipHandle.IsActive` construisait la résolution paresseuse d'un clip rectangulaire
    (96 octets) ; il lit maintenant `IClipHandleOwner.IsClipInForce`, sans allocation.
  - `ClipPipelineStrategyTests.RenderContext_KeepsLegacyRectangleClipShim` attend désormais `ClipHandle` (rupture
    voulue, D2).
  - Stencil et masque : seulement migrés (fermeture gardée dans `ClipSlotState.Restore`), comme prévu.
  - Docs : `MGUI/Docs/rendering-architecture.md`, `custom-render-backend-integration.md`, statut de l'ADR-0027.

### ✅ T1.5 — Clip à stencil sans allocation

- Objectif : 0 octet par paire pousser et libérer un clip arrondi, dans les deux backends (1112 octets mesurés avant
  dans le moteur).
- Fichiers : `CasaStencilClipExecutor.cs`, MGUI `ClipManager.cs` (chemin stencil), déjà migrés vers la poignée par
  T1.4, et le rendu de la géométrie de
  clip (`DrawClipGeometry`, renderer de formes) seulement si la mesure montre qu'il alloue.
- Étapes : réglages dérivés gardés par profondeur (D4) ; plus de fermeture : l'état de restauration vit dans
  l'emplacement de la poignée ; mesure ; si le renderer de formes alloue, le mesurer séparément et l'écrire ici avant
  d'y toucher.
- Validation : `WhatTheFrameStillAllocates` et `ClipScopeRemainingAllocationMeasurements` deviennent des affirmations
  à 0 ; pixels identiques sur un clip arrondi imbriqué à deux niveaux. Les réglages dérivés sont gardés par identité
  des réglages de départ (D4) : un cas mesure le clip arrondi sous une transformation temporaire ; il est à 0 si T1.3
  est ✅, sinon son chiffre est écrit ici comme limite.
- Commit : `perf(rendering): push a stencil clip without allocating` (MGUI), puis
  `perf(ui): push a stencil clip without allocating` (moteur)
- Validation (2026-11-16) : les trois solutions 0 erreur ; `MGUI.Tests` 3733 verts, `CasaEngine.Tests` 5556 verts, aucun
  sauté. Plus de fermeture : l'état du clip (poignée des réglages de lecture, profondeurs) vit dans son emplacement
  (`ClipSlotState.Stencil`) ; les trois réglages dérivés par profondeur sont gardés (`DerivedDrawSettingsCache.WithStencil`,
  un cache par backend, retrouvé par des réglages de départ égaux).

  | Octets par paire, GPU réel | Moteur avant | Moteur après | MGUI avant | MGUI après |
  |---|---|---|---|---|
  | Clip à stencil, pousser et libérer | 568 (1112 avant T1.2) | 0 | 568 | 0 |
  | Deux clips à stencil imbriqués | non mesuré | 0 | non mesuré | 0 |
  | Clip à stencil sous une transformation temporaire | non mesuré | 0 | non mesuré | 0 |

  - O2 : le dessin de la géométrie de clip (`DrawClipGeometry`) n'alloue pas ; rien n'y est touché.
  - Les mesures sans affirmation sont devenues des affirmations : `TheStencilClip_TheTemporaryChanges_AndTheFrame_AllocateNothing`
    (moteur, ex `WhatTheFrameStillAllocates`) et `TheStencilClipAndTheTemporaryChanges_AllocateNothing` (MGUI).
  - Pixels : `TwoNestedStencilClips_CutToTheirIntersection_TheSecondTimeAsTheFirst` dans les deux dépôts (deux niveaux,
    premier et second passage identiques, points à l'intérieur et à l'extérieur de l'intersection vérifiés).
  - Limite : la mesure pousse une définition de clip déjà construite ; la construction d'une `ClipDefinition` et de sa
    géométrie par un contrôle n'est pas dans cette tâche.

### ✅ T1.6 — Le `MainRenderer` de MGUI garde sa transaction

- Objectif : `MainRenderer.BeginDrawTransaction` et `DrawViews` réutilisent une `DrawTransaction` réarmée (D5).
- Fichiers : `MGUI/MGUI.MonoGame.Integration/Rendering/MainRenderer.cs`, `DrawTransaction.cs`, `ClipManager.cs`,
  ADR de MGUI, tests GPU de MGUI.
- Étapes : porter `Rearm` et le contrat d'ADR-0097 (une ouverte à la fois, seconde demande qui lève, état rendu
  utilisable après un dessin qui lève) ; vérifier dans le code qu'aucun appelant de `MGUI.Samples`,
  `MGUI.Editor.Host` et `MGUI.MiniGame` ne garde deux transactions ouvertes ; si l'un le fait, ⚠️ avec le site.
- Validation : image réelle du backend de MGUI à 0 octet au repos sur GPU réel ; mêmes pixels qu'avec une transaction
  neuve ; `MGUI.sln` 0 erreur.
- Commit (MGUI) : `perf(rendering)!: keep one draw transaction per renderer`
- Validation (2026-11-16) : les trois solutions 0 erreur ; `MGUI.Tests` 3743 verts (10 tests GPU ajoutés,
  `Integration/MainRendererDrawTransactionGpuTests.cs`, écrits par un `executor`), `CasaEngine.Tests` 5556 verts, aucun
  sauté. ADR-0032 de MGUI.
  - Contrat porté d'ADR-0097 et testé sur GPU réel : une transaction par `MainRenderer`, réarmée ; seconde demande qui
    lève et laisse la première utilisable ; même objet après `Dispose` ; dessin, `Dispose` ou début de lot qui lève ;
    changement de viewport entre deux images ; changements temporaires et clips laissés ouverts oubliés par l'usage
    suivant. Référence des pixels : un `MainRenderer` neuf par image de référence, sur le même bureau.
  - O6 : aucun appelant de `MGUI.Samples`, `MGUI.Editor.Host` ou `MGUI.MiniGame` ne garde deux transactions ouvertes
    (`rg "BeginDrawTransaction\("` : seuls `MGDesktop.Draw` et `MainRenderer.DrawViews` en ouvrent, sous `using`).
  - Image réelle du backend de MGUI (`MGDesktop.Update` puis `MGDesktop.Draw`, fixture `Full`, GPU réel, 500 images) :

    | | Update | Draw |
    |---|---|---|
    | Avant (transaction gardée, rien d'autre) | 0 | 6624 octets par image |
    | Après | 0 | 0 |

    Deux causes hors de la transaction, corrigées dans les fichiers de la tâche en portant ce que le moteur fait déjà :
    `DrawTransaction.GetFirstValidDrawContext(params ...)` (un tableau de 48 octets par forme dessinée, 33 par image)
    et `MainRenderer.GetOrCreateWhiteCircleTexture` (requête LINQ, 504 octets par point, 10 par image pour la poignée de
    redimensionnement de la fenêtre).
  - Sample : `MGUI.Samples.exe` (net9.0-windows) lancé 20 secondes, encore en vie à l'arrêt, aucune sortie d'erreur.
    Le rendu n'a pas été jugé à l'œil.

### ✅ T1.7 — Vérification indépendante de la phase 1

- Objectif : un `verifier` neuf confirme ou réfute : zéro allocation des quatre opérations dans les deux backends,
  poignée périmée sans effet, rendu inchangé, aucune régression des deux suites.
- Sources : diff de la phase 1, ADR, tests.
- Validation : verdict CONFIRMED, ou constats traités selon leur priorité et revérifiés. Docs mises à jour
  (`MGUI/Docs/drawing-architecture.md`, `docs/decisions/` du moteur si le contrat d'ADR-0097 change).
- Commit : `docs(ui): record the handles of the draw transaction` (les deux dépôts)
- Validation (2026-11-16) : verdict **CONFIRMED** d'un `verifier` neuf (opus) sur les cinq points de la revendication,
  avec ses propres sondes sur GPU réel (retirées ensuite) : 0 octet pour chaque opération dans les deux backends, y
  compris imbriquées et avec un vrai dessin dans chaque portée ; poignées périmées sans effet (20 niveaux, hors ordre,
  après réutilisation, après réarmement) ; sémantique du stencil, du scissor et des caches de réglages identique au
  code d'avant (test de propriété sur 200 000 opérations pour `DerivedDrawSettingsCache`) ; aucun `box` d'une poignée
  dans 133 assemblies produit (analyse de l'IL) ; trois solutions 0 erreur, 3743 et 5556 tests verts, aucun sauté.
  Docs : faites en T1.4 et T1.6 (`MGUI/Docs/rendering-architecture.md`, `custom-render-backend-integration.md`,
  ADR-0031 et ADR-0032 de MGUI, statut de l'ADR-0027) ; le contrat d'ADR-0097 du moteur ne change pas.
  - Constats non bloquants, reportés sans changement de code (P3 et P4) :
    - P3, déjà là avant : des clips à stencil libérés hors ordre laissent la profondeur que le dernier libéré avait
      notée (même comportement que la fermeture d'avant).
    - P4, introduit : `SetDrawSettingsTemporary` et `SetRenderTargetTemporary` prennent leur emplacement avant de
      changer l'état ; si ce changement lève, l'emplacement reste pris jusqu'au réarmement suivant.
    - P4 : une entrée d'un cache de réglages dérivés garde son effet jusqu'à son remplacement (écrit dans l'ADR-0031).
    - P4 : une copie de la transaction de MGUI libérée une seconde fois après un réarmement ferme le nouvel usage ;
      c'est l'usage interdit par l'ADR-0032 (« ne plus utiliser après `Dispose` »), aucun code produit ne le fait.
    - P4 : une génération peut reboucler après 2^32 libérations d'un emplacement (écrit dans l'ADR-0031).

---

## Phase 2 — Contrôles

Chaque tâche : fixture de T0.1, correction, test de zéro ajouté à `DesktopFrameZeroAllocationTests` (ou au fichier
créé en T0.1), appels de dessin inchangés. Une tâche mesurée à 0 en T0.1 est fermée sans code (D8). Chaque tâche
notée « Commit (MGUI) » a aussi son commit du moteur (référence du sous-module et ce plan), comme le disent les
règles d'exécution ; il en va de même en phase 3 et pour T1.1 et T1.6.

### ⚠️ T2.1 — `MGGrid` avec sélection

- Fichiers : `MGUI/MGUI.Core/UI/Containers/Grids/MGGrid.cs`.
- Étapes : énumération de `GridSelection` sans itérateur (énumérateur `struct` ou boucle sur lignes et colonnes) ;
  cellules dont les bornes changent : mesurer le cas, ne garder un dictionnaire que réutilisé.
- Validation : 0 octet au repos avec une sélection et pendant le déplacement de la sélection à la souris.
- Commit (MGUI) : `perf(grid): draw and move a selection without allocating`
- Validation (2026-11-16) : `MGUI.sln` et les deux solutions du moteur 0 erreur ; `MGUI.Tests` 3790 verts (6 tests
  ajoutés à `ControlFamilyZeroAllocationTests.cs`), `CasaEngine.Tests` 5556 verts, aucun sauté. Écrit par un `executor`,
  diff relu par la session principale (équivalence du parcours de focus vérifiée contre `TraverseVisualTree`).

  | Octets par image | Update avant | Update après | Draw avant | Draw après |
  |---|---|---|---|---|
  | `MGGrid` 3x3, une cellule sélectionnée, repos | 0 | 0 | 288 | 0 |
  | Sélection déplacée par un clic toutes les 2 images | 1996 | 232 | 288 | 0 |

  - Causes retirées : itérateur `yield` de `GridSelection` au dessin (énumérateur `struct` interne
    `GridSelection.Cells`, l'énumération publique est inchangée) ; recherche de la cible de focus automatique à chaque
    appui (`UIFocusNavigationService.GetFirstAutoFocusCandidate` : parcours, `Where`, `Distinct`, tris et liste,
    environ 1760 octets par clic), remplacée par un parcours unique qui garde le premier candidat dans le même ordre
    (index de tabulation, haut, gauche, premier rencontré). `GetFocusableElements`, utilisé par la navigation au
    clavier, n'a pas changé.
  - Appels de dessin inchangés : journal figé de 1500 lignes pris sur le code d'avant (repos et déplacements).
  - ⚠️ Reste, cause hors des fichiers de la tâche (D8) : 464 octets par clic, les objets que le suivi de la souris de
    `MGUI.Shared/Input/Mouse` crée pour tout appui et relâchement (événements d'appui, de relâchement, de clic, de début
    et de fin de glissement, séquence de clics et ses métadonnées). Preuve : la même suite de clics sur la même grille
    avec `SelectionMode = None` alloue exactement le même total, ce que le test affirme. Voir O10.

### ⚠️ T2.2 — `MGDockHost`

- Fichiers : `MGUI/MGUI.Core/UI/Docking/` (`MGDockHost.cs`, `MGDockDropIndicators.cs`, `MGDockPreviewOverlay.cs`,
  `DockDropCalculator.cs`, et les classes de séparation que la mesure désigne).
- Étapes : parcours des groupes et des séparations dans des listes réutilisées ; zones interdites et cibles de dépôt
  dans des collections gardées ; localiser les 32 octets après un changement de texte par mesure élément par élément.
- Validation : 0 octet au repos avec séparations et panneau masqué, pendant un glissement d'onglet, et après un
  changement de texte.
- Commit (MGUI) : `perf(docking): update and draw a dock host without allocating`
- Validation (2026-11-16) : `MGUI.sln` et les deux solutions du moteur 0 erreur ; `MGUI.Tests` 3804 verts (14 tests
  ajoutés à `ControlFamilyZeroAllocationTests.cs`), `CasaEngine.Tests` 5556 verts, aucun sauté. Écrit par un `executor`,
  diff relu par la session principale (12 fichiers produit).

  | Octets par image | Update avant | Update après | Draw avant | Draw après |
  |---|---|---|---|---|
  | Deux groupes et une séparation, repos | 160 | 0 | 40 | 0 |
  | Un panneau en masquage automatique, repos | 160 | 0 | 208 | 0 |
  | Un onglet derrière l'onglet actif, repos | 160 | 0 | 40 | 0 |
  | Glissement d'onglet avec aperçu de dépôt | 3248 | 80 | 80 (848 avant T2.6) | 0 |
  | Un groupe (`FullInDockHost`), repos et après un changement de texte | 0 | 0 | 0 | 0 |

  - Causes retirées :
    - `MGDockSplitterBar.SyncVisualParts` créait une brosse à chaque update (160 octets) ; la brosse est gardée avec
      sa couleur. Effet voulu : la partie d'accent ne notifie plus un changement de fond à chaque image ;
    - `DrawContents` par itérateur de `GetChildren()` dans `MGDockSplitContainer`, `MGDockAutoHideStrip`,
      `MGDockAutoHideDrawer` et `MGDockPreviewOverlay` : enfants dessinés directement, dans le même ordre ;
    - pendant le glissement : parcours de l'arbre par itérateurs (`GetAllVisibleTabGroups`, `GetAllSplitContainers`,
      `FindTabGroupsRecursive`), remplacés par des listes gardées remplies à chaque appel (`CollectVisibleTabGroups`,
      `CollectSplitContainers`, nouveau `MGElement.AddChildrenTo` interne) ; ensemble des zones interdites gardé et
      rempli à chaque appel (les appelants le lisent aussitôt, vérifié) ; `GetDocumentArea` par
      `DockLayoutModel.FindVisibleDocumentArea` ; fermetures de `FirstOrDefault` remplacées par des boucles. Les
      énumérations publiques ne changent pas.
  - Appels de dessin inchangés : quatre journaux figés pris sur le code d'avant (632, 688, 672 et 3100 lignes ; le
    glissement couvre l'appui, les déplacements, l'aperçu, le dépôt et la suite). Tests d'équivalence :
    `AddChildrenTo_GivesTheChildrenOfGetChildren_ForTheElementsOfTheDockScenes`,
    `DockHost_CollectedTreeWalks_AreTheEnumerations`.
  - Doublure de test modifiée : `GraphTestTextEngine` garde ses `ResolvedFont` par spécification, comme les deux
    moteurs de texte réels (il en créait un par appel, ce que `MGRotatedTextLabel` demande à chaque dessin).
  - ⚠️ Reste pendant le glissement : 80 octets par image, un `DockDropTarget` par recalcul de la cible (le test
    l'affirme exactement). `MGDockHost.CurrentDropTarget` est une propriété publique qui notifie quand la référence
    change : réutiliser l'instance arrêterait cette notification. Voir O11.
  - Non atteint par la fixture, laissé : LINQ de `DockDropCalculator` (groupe de plus d'un panneau, zones de
    séparation, bords de l'hôte), `RefreshActiveGroupHighlight`.
  - Sample : à relancer en fin de phase 2 (règle d'exécution).

### ✅ T2.3 — Fenêtre en `SizeToContent`

- Fichiers : `MGUI/MGUI.Core/UI/MGWindow.cs` (`BeginUpdateInternal`, `RevalidateSizeToContent`,
  `ApplySizeToContent`).
- Étapes : mesurer chaque appel de `ApplySizeToContent` ; ne refaire le travail que si la taille du contenu ou les
  contraintes ont changé, si le code permet de le savoir sans changer le résultat ; sinon retirer les allocations une
  à une.
- Validation : 0 octet au repos ; la taille de la fenêtre suit toujours un changement de contenu (test existant ou
  ajouté).
- Commit (MGUI) : `perf(window): keep a window sized to its content without allocating`
- Validation (2026-11-16) : mesuré à zéro en T0.1 (Update 0, Draw 0, `WidthAndHeight` et `Height`). Fermée sans code (D8).

### ⚠️ T2.4 — `MGScrollViewer`

- Fichiers : `MGUI/MGUI.Core/UI/MGScrollViewer.cs`.
- Étapes : nom de clip gardé (`:1233`) ; parcours des enfants sans énumérateur d'interface ; ce que la mesure montre
  d'autre.
- Validation : 0 octet au repos et pendant un défilement à la molette.
- Commit (MGUI) : `perf(scroll): scroll and draw a scroll viewer without allocating`
- Validation (2026-11-16) : `MGUI.sln` et les deux solutions du moteur 0 erreur ; `MGUI.Tests` 3748 verts (5 tests
  ajoutés, `Architecture/ControlFamilyZeroAllocationTests.cs`), `CasaEngine.Tests` 5556 verts, aucun sauté. Écrit par un
  `executor`, diff relu par la session principale.

  | Octets par image | Update avant | Update après | Draw avant | Draw après |
  |---|---|---|---|---|
  | Repos | 136 | 0 | 210 | 0 |
  | Molette (le banc repasse par 5 décalages) | 192 | 56 | 331 | 0 |

  - Causes retirées : abonnement à `OnBeginUpdateContents` (un `ElementUpdateEventArgs` par update), remplacé par le
    crochet `BeginUpdateContentsInternal` que `MGWindow` utilise déjà ; itérateur de `GetChildren()` au dessin ; nom et
    `ClipDefinition` du viewport construits à chaque dessin, gardés tant que les bornes à l'écran sont les mêmes ;
    anneau des clips récents d'un élément porté de 8 à 16 (`MGElement.cs`, les deux crochets de clip le partagent).
  - Appels de dessin inchangés : journal figé de 2912 lignes pris sur le code d'avant
    (`ScrollViewerWithTallContent_DrawJournal_AtRestAndAfterWheelNotches`), identique après.
  - ⚠️ Reste, cause hors des fichiers de la tâche (D8) : 56 octets par image où la molette bouge, un
    `BaseMouseScrolledEventArgs` créé par `MGUI.Shared/Input/Mouse/MouseTracker.cs:409`. Le test l'affirme tel quel
    (« un événement de molette par image et rien d'autre »). Voir O7.
  - Limite constatée : un défilement continu sur des décalages tous différents crée encore jusqu'à deux
    `ClipDefinition` par image (environ 120 octets) pour le contenu défilé. Voir O8.
  - Effet sur les autres familles : `MGListView` et `MGTreeView` 136/786 → 0/576, `MGListBox` 272/210 → 136/0,
    `MGContextMenu` 272/1178 → 136/968, menu déroulant de `MGMenuBar` 1280/874 → 1144/664.

### ✅ T2.5 — Listes (`MGListBox`, `MGListView`)

- Fichiers : `MGUI/MGUI.Core/UI/MGListBox.cs`, `MGListView.cs`, et ce que T0.1 désigne.
- Validation : 0 octet au repos et au survol des éléments, liste de 50 éléments dans un défilement.
- Commit (MGUI) : `perf(lists): hover and draw list controls without allocating`
- Validation (2026-11-16) : `MGUI.sln` et les deux solutions du moteur 0 erreur ; `MGUI.Tests` 3758 verts (10 tests
  ajoutés à `ControlFamilyZeroAllocationTests.cs`), `CasaEngine.Tests` 5556 verts, aucun sauté. Écrit par un `executor`,
  diff relu par la session principale.

  | Octets par image, repos et survol | Update avant | Update après | Draw avant | Draw après |
  |---|---|---|---|---|
  | `MGListBox` 50 éléments (après T2.4) | 136 | 0 | 0 | 0 |
  | `MGListView` 50 éléments (après T2.4) | 0 | 0 | 576 | 0 |

  - Causes : abonnement à `OnBeginUpdate` dans `MGListBox` (un `ElementUpdateEventArgs` par update), remplacé par le
    crochet `BeginUpdateInternal`, au même point de l'update ; `Enum.HasFlag` sur `GridLinesVisibility` dans le dessin
    des lignes de `MGGrid.cs` (enum boxé, six appels), remplacé par le test de bit que le fichier utilise déjà.
    `MGListView.cs` n'a pas changé.
  - Appels de dessin inchangés : journaux figés pris sur le code d'avant (5876 et 4430 lignes, repos et survol des
    deux éléments), identiques après.
  - Hors chemin mesuré, laissés : `HasFlag` de `MGGrid.CheckIfOuterPaddingChanged` et de `MGUniformGrid.cs`.
  - Une lecture isolée à 13 octets par image (update au survol de `MGListView`) vue une fois par l'agent pendant son
    travail, non reproduite ensuite (3 passes de la classe et 2 suites complètes vertes).

### ✅ T2.6 — `MGTreeView`

- Fichiers : `MGUI/MGUI.Core/UI/MGTreeView.cs`, `MGTreeViewItem.cs`.
- Validation : 0 octet au repos et au survol, arbre de trois niveaux dépliés.
- Commit (MGUI) : `perf(tree): hover and draw a tree view without allocating`
- Validation (2026-11-16) : `MGUI.sln` et les deux solutions du moteur 0 erreur ; `MGUI.Tests` 3763 verts (5 tests
  ajoutés à `ControlFamilyZeroAllocationTests.cs`), `CasaEngine.Tests` 5556 verts, aucun sauté. Écrit par un `executor`,
  diff relu par la session principale.

  | Octets par image, repos et survol | Update avant | Update après | Draw avant | Draw après |
  |---|---|---|---|---|
  | `MGTreeView` 14 nœuds, trois niveaux (après T2.4) | 0 | 0 | 576 | 0 |

  - Cause : `UISymbolDrawing.DrawFilledTriangleArrow` créait un `Point[3]` et un itérateur `Select` par flèche (96
    octets, 6 flèches par image). Les trois sommets sont maintenant écrits dans un tampon gardé par thread, que le
    contexte de dessin lit pendant l'appel (vérifié dans les deux backends et les doublures : `ToArray` pendant
    l'appel). `MGTreeView.cs` et `MGTreeViewItem.cs` n'ont pas changé. `GetTriangleArrowVertices` garde sa signature.
  - Appels de dessin inchangés : journal figé de 4280 lignes pris sur le code d'avant (sommets des polygones compris).
  - Autres appelants de la flèche, même sortie : `MGComboBox`, `MGContextMenuItem`, `UISymbolElements`,
    `MGGraphControls`, `MGDockDropIndicators`. Effet mesuré : dessin de `MGContextMenu` 968 → 392, aperçu de dépôt de
    `MGDockHost` 848 → 80, menu déroulant de `MGMenuBar` 664 → 280.
  - Limite constatée, hors tâche : sur un backend réel, `FillPolygon` copie encore les sommets (`Any()` puis `ToArray()`
    dans `CasaLegacyShapeRenderer2D.cs:323-329` et `DrawTransaction.cs:792-800` de MGUI). Le banc, qui dessine dans
    une transaction de test, ne le voit pas. Voir O9.

### ✅ T2.7 — `MGTabControl`

- Fichiers : `MGUI/MGUI.Core/UI/MGTabControl.cs`.
- Validation : 0 octet au repos et au survol des en-têtes.
- Commit (MGUI) : `perf(tabs): hover and draw a tab control without allocating`
- Validation (2026-11-16) : `MGUI.sln` et les deux solutions du moteur 0 erreur ; `MGUI.Tests` 3769 verts (6 tests
  ajoutés à `ControlFamilyZeroAllocationTests.cs`), `CasaEngine.Tests` 5556 verts, aucun sauté. Écrit par un `executor`,
  diff relu par la session principale.

  | Octets par image | Update avant | Update après | Draw avant | Draw après |
  |---|---|---|---|---|
  | `MGTabControl` trois onglets, repos | 0 | 0 | 0 | 0 |
  | Survol alterné de deux en-têtes | 88 | 0 | 0 | 0 |

  - Cause : `MGTabControl.GetVisualTreeChildren` créait une liste à chaque appel, et la recherche de l'élément survolé
    l'appelle à chaque mouvement de souris. La liste est gardée par combinaison d'arguments et revérifiée à chaque
    appel contre les onglets et la sélection (aucun drapeau à oublier) ; une liste rendue n'est jamais modifiée, une
    nouvelle la remplace quand le contenu change.
  - Appels de dessin inchangés : journal figé de 1100 lignes pris sur le code d'avant. Test de sens du cache :
    `TabControlWithThreeTabs_VisualTreeChildren_FollowTheSelection`.

### ✅ T2.8 — Menus (`MGContextMenu`, `MGMenuBar`)

- Fichiers : `MGUI/MGUI.Core/UI/MGContextMenu.cs`, `MGContextMenuItem.cs`, `MGMenuBar.cs`.
- Validation : 0 octet menu ouvert au repos et au survol des entrées.
- Commit (MGUI) : `perf(menus): hover and draw menus without allocating`
- Validation (2026-11-16) : `MGUI.sln` et les deux solutions du moteur 0 erreur ; `MGUI.Tests` 3784 verts (15 tests
  ajoutés à `ControlFamilyZeroAllocationTests.cs`), `CasaEngine.Tests` 5556 verts, aucun sauté. Écrit par un `executor`,
  diff relu par la session principale.

  | Octets par image | Update avant | Update après | Draw avant | Draw après |
  |---|---|---|---|---|
  | `MGContextMenu` ouvert, souris dehors | 136 | 0 | 392 | 0 |
  | `MGContextMenu`, survol alterné de deux entrées | 1144 | 0 | 392 | 0 |
  | `MGMenuBar` fermée, repos | 0 | 0 | 56 | 0 |
  | `MGMenuBar` fermée, survol alterné | 672 | 0 | 56 | 0 |
  | `MGMenuBar`, menu ouvert, survol alterné | 1144 | 0 | 280 | 0 |

  - Causes et corrections :
    - abonnements à `OnEndDraw` (fenêtre hôte, menu racine, flèche de sous-menu d'une entrée) : un
      `MGElementDrawEventArgs` par élément abonné et par dessin. Remplacés par un crochet `EndDrawInternal` ajouté à
      `MGElement` (jumeau de `EndingDrawInternal`, appelé juste avant `OnEndDraw`) et par
      `MGWindow.AddEndDrawSubmenuHost` ;
    - abonnement à `OnBeginUpdateContents` du menu, remplacé par le crochet `BeginUpdateContentsInternal` ;
    - liste de l'arbre visuel recréée à chaque changement de sélection : `MGSingleContentHost` garde une liste d'un
      élément, comparée au contenu courant à chaque appel ;
    - `EventArgs<VisualState>` : une instance par paire d'états (144), créée au premier usage et partagée ;
    - `EventArgs<Rectangle>` de `OnLayoutBoundsChanged` : l'élément réutilise les arguments de sa dernière levée quand
      les deux rectangles sont les mêmes.
  - Appels de dessin inchangés : trois journaux figés pris sur le code d'avant (1440, 1111 et 1983 lignes).
  - Changements de contrat à connaître, sans effet trouvé dans les deux dépôts :
    - les arguments de `VisualStateChanged` et de `OnLayoutBoundsChanged` peuvent être la même instance d'une levée à
      l'autre (mêmes valeurs, lecture seule) ;
    - le sous-menu d'un menu hébergé par une autre fenêtre est dessiné par le crochet, donc avant tout abonné à
      `OnEndDraw` de cette fenêtre, y compris un abonné venu avant la création du menu. Aucun abonné de ce genre dans
      MGUI ni dans le moteur (`rg "OnEndDraw \+="` : seul `MGOverlay`, sur son contenu).
  - Le survol alterné change la sélection et refait le layout du menu à chaque image : ce travail reste, il n'alloue plus.
  - `MGMenuBar.cs` n'a pas changé. Effet de bord : sélection de `MGGrid` par clic 2060 → 1996.

### ✅ T2.9 — `MGTextBox`

- Fichiers : `MGUI/MGUI.Core/UI/MGTextBox.cs`, `Text/MGTextCaret.cs`.
- Étapes : repos avec focus (curseur) d'abord. La frappe change le texte : la cible est 0 octet hors de la chaîne du
  texte elle-même, que `string` impose ; mesurer et écrire ce que la chaîne coûte.
- Validation : 0 octet au repos avec focus ; pendant la frappe, le chiffre restant est écrit ici avec sa cause.
- Commit (MGUI) : `perf(textbox): idle and type in a text box without allocating`
- Validation (2026-11-16) : `MGUI.sln` et les deux solutions du moteur 0 erreur ; `MGUI.Tests` 3812 verts (8 tests
  ajoutés à `ControlFamilyZeroAllocationTests.cs`), `CasaEngine.Tests` 5556 verts, aucun sauté. Écrit par un `executor`.

  | Octets par image | Update avant | Update après | Draw avant | Draw après |
  |---|---|---|---|---|
  | `MGTextBox` avec focus, repos (curseur clignotant ou fixe) | 0 | 0 | 0 | 0 |
  | Une touche par image (A puis retour arrière) | 3876 | 3296 | 0 | 0 |

  - Retiré (environ 580 octets par touche) : lectures de `IsControlDown` qui passent par `IsAnyPressed(params Keys[])`
    (tableau, fermeture, délégué), `StringBuilder` de l'insertion et de l'effacement (`string.Concat` de `Span`),
    LINQ et énumérateurs boxés de `TextRenderInfo` (boucles indexées, tampon d'avances gardé).
  - Appels de dessin inchangés : journal figé de 936 lignes pris sur le code d'avant (repos, dix touches, repos).
  - Reste pendant la frappe, 3296 octets par touche, affirmé exactement par le test (`référence + 1 300 000` sur 500
    images) :
    - 696 octets : objets de l'entrée clavier de `MGUI.Shared/Input` créés pour toute touche (appui, relâchement, clic,
      flux d'entrée et ses métadonnées, tableau et délégué de `KeyboardTracker.IsAnyPressed`). Preuve : une zone de
      saisie qui ignore les touches (F1, F2) alloue exactement cela. Hors périmètre. Voir O12 ;
    - environ 2600 octets : à chaque changement de texte, la géométrie du curseur (`TextRenderInfo.UpdateLines`) lit
      `MGTextBlock.Lines`, qui reconstruit les lignes publiques par le code historique (`MGTextLine.ParseLines`,
      environ 1600 octets estimés), alors que le bloc a déjà mis le texte en page. `TextRenderInfo` a besoin de la
      table des index de caractères d'origine, que seul ce code calcule. Le reste est le résultat du changement : la
      nouvelle chaîne, les informations de ligne et de caractères du curseur, les arguments de `TextChanged` et du
      changement de bornes. Voir O13.
  - Non touché, hors du chemin de frappe : lambdas des déplacements verticaux de `MGTextCaret`.

---

## Phase 3 — Texte

### ✅ T3.1 — Bloc sans balise souligné ou à fond

- Fichiers : `MGUI/MGUI.Core/UI/MGTextBlock.cs` (`DrawSelf`, `DrawPlainLines`), `Text/PlainTextLayout.cs` si la
  boucle a besoin des bornes d'une ligne.
- Étapes : dessiner le fond et le soulignement dans la boucle sans allocation, par `DrawUnderline` et la brosse du
  fond, aux mêmes rectangles que la boucle historique.
- Validation : mêmes appels de dessin que `ForceLegacyTextPath` sur le corpus des tests du texte sans balise, avec
  soulignement et fond ; 0 octet par dessin.
- Commit (MGUI) : `perf(text): draw an underlined or backed plain block without allocating`
- Validation (2026-11-16) : `MGUI.sln` et les deux solutions du moteur 0 erreur ; `MGUI.Tests` 3822 verts (10 tests
  ajoutés), `CasaEngine.Tests` 5556 verts, aucun sauté. Écrit par un `executor`. Sample relancé à la fin de la phase 2
  (20 secondes, en vie, aucune sortie d'erreur) ; cette tâche ne change aucun appel de dessin.

  | Octets par dessin, texte inchangé | Avant | Après |
  |---|---|---|
  | Bloc sans balise souligné (`MGDesktop.Draw` et `DrawSelf`) | 120 | 0 |

  - Changement : `DrawPlainLines` dessine le soulignement lui-même par `DrawUnderline`, au même rectangle que la
    boucle historique ; un bloc sans balise souligné ne retombe plus sur celle-ci. `PlainTextLayout.cs` n'a pas changé.
  - Périmètre réduit au soulignement (constat de T0.1) : un bloc sans balise ne peut pas avoir de fond de texte ; la
    garde `HasBackground` reste, un fond irait toujours à la boucle historique.
  - Mêmes appels de dessin que `ForceLegacyTextPath` : 12 672 cas (96 configurations : retour à la ligne, gras, ombre,
    soulignement, largeurs, alignements, couleurs, opacités de dessin et d'élément, interligne, progression du texte ;
    100 textes du corpus), 17 561 rectangles de soulignement comparés. Le test échoue si le soulignement est décalé
    d'un pixel (vérifié puis annulé).
  - Non couvert par un test, par construction seulement : brosse, hauteur et décalage du soulignement, qui ne peuvent
    pas atteindre le chemin sans balise aujourd'hui.

### ✅ T3.2 — Révélation par `TextCharactersPerSecond`

- Fichiers : `MGTextBlock.cs` (`SyncTextProgressAnimation`, `TextRevealRun`).
- Étapes : garder un `TextRevealRun` et le reconfigurer au lieu de le recréer, si le moteur d'animation accepte de
  redémarrer une instance (lire `UIPropertyAnimation` et `Animations.Start`) ; sinon ⚠️ avec le fait.
- Validation : 0 octet par image pendant la révélation, et par redémarrage après un changement de texte ; les tests
  de révélation existants verts.
- Commit (MGUI) : `perf(text): restart a text reveal without allocating`
- Validation (2026-11-16) : `MGUI.sln` et les deux solutions du moteur 0 erreur ; `MGUI.Tests` 3832 verts (10 tests
  ajoutés), `CasaEngine.Tests` 5556 verts, aucun sauté. Écrit par un `executor`.

  | Octets | Avant | Après |
  |---|---|---|
  | Image d'une révélation en cours (Update, Draw) | 0 | 0 |
  | Changement de texte pendant une révélation (setter) | 608 | 0 |
  | Changement de vitesse, écriture de `TextProgress` | non mesuré | 0 |

  - O5 : oui, le moteur d'animation redémarre une instance (`UIAnimation.cs:143-160` et `:184-186`,
    `UIAnimationManager.Start` `:108-187`, `UIAnimation.Begin` `:214-239`).
  - Cause : deux `new TextRevealRun` de 304 octets par changement de texte (`SyncTextProgressAnimation` appelé deux
    fois). Le bloc garde maintenant une instance, l'annule, la reconfigure et la redémarre. Une instance neuve est
    encore créée dans trois cas où la réutilisation changerait l'ordre : la première fois, pendant le tick du moteur
    d'animation, et quand l'instance est encore active dans le moteur d'un autre bureau. Ajout interne :
    `UIAnimationManager.IsTicking`.
  - Comportement : journal figé de 897 lignes pris sur le code d'avant, rejoué image par image (progression, nombre de
    caractères révélés lu dans les appels de dessin, notifications et événements dans l'ordre) : changements de texte
    et de vitesse en cours, écriture de `TextProgress`, fin puis relance, vitesse nulle ou négative, texte vide,
    détachement et rattachement, pause de l'horloge, échelle de temps, changement de texte depuis le rappel d'une
    autre animation. Sans la garde `IsTicking` le journal diffère (vérifié puis annulé).
  - Différence restante, à connaître : un redémarrage lève `Cancelled` puis `Started` sur la même instance ; celui
    qui gardait une référence prise dans `Animations.Active` voit l'animation repartir au lieu d'une animation
    terminée.

### ✅ T3.3 — Valeur de balise refusée gardée en cache (D11)

- Fichiers : `Text/FormattedTextScanner.cs`.
- Validation : un texte avec `[color=NotAColor]` changé à chaque image alloue 0 octet après la première lecture ;
  mêmes runs que le code historique.
- Commit (MGUI) : `perf(text): remember a rejected tag value`
- Validation (2026-11-16) : `MGUI.sln` et les deux solutions du moteur 0 erreur ; `MGUI.Tests` 3865 verts (33 tests
  ajoutés, `Text/FormattedTextRejectedValueTests.cs`), `CasaEngine.Tests` 5556 verts, aucun sauté. Écrit par un
  `executor`.

  | `[color=NotAColor]` changé à chaque image | Avant | Après |
  |---|---|---|
  | Setter, octets par image | 2656 | 0 |
  | Exceptions levées et rattrapées, par 10 images | 27 | 0 |

  - Changement (`FormattedTextScanner.cs` seul) : chaque cache de valeurs (un par sorte : couleur, soulignement, fond,
    ombre) garde aussi les valeurs refusées, dans le même dictionnaire et sous le même plafond de 128. L'interprétation
    ne lève qu'une fois par valeur. Images, noms d'infobulle et d'action, opacité : aucun refus possible, rien à garder.
  - Toutes les valeurs gardées comme refusées ne dépendent que de leur texte ; aucune ne lit le bureau ni une
    ressource. Garde-fou ajouté : une entrée refusée ne vaut que pour la culture courante où elle a été faite (les
    brosses passent par un convertisseur qui peut lire la culture). Aucune valeur lue différemment selon la culture
    n'a pu être produite : ce garde-fou n'est donc pas démontré par un test.
  - Mêmes runs que le code historique : différentiel existant inchangé et vert (609 textes, 21 cas d'ADR-0030), plus
    9600 lectures au-delà du plafond avec des valeurs acceptées voisines.
  - Docs : puce de l'ADR-0030 de MGUI et ligne de `MGUI/Docs/text-architecture.md` réécrites. Trois lignes de ce
    fichier sont périmées par T3.1, T3.2 et le plafond de 128 : corrigées en T4.1.

### ✅ T3.4 — Grammaire des balises en ASCII (D9)

- Fichiers : `Text/FormattedTextTokenizer.cs` (classes de caractères ASCII explicites à la place de `\d`, et casse
  ASCII), `Text/MGTextRun.cs` (`ShadowOffsetParser`) et tout autre motif de `MGUI.Core/UI/Text/` qui contient `\d`
  ou compare des lettres sans casse (`rg` sur le dossier), `Text/FormattedTextScanner.cs` (plus d'ambiguïté :
  `IsAmbiguous` retiré), `MGUI/Docs/decisions/` (ADR),
  `Docs/text-architecture.md`, tests du corpus.
- Étapes : relever chaque motif du dossier `Text/` qui accepte un caractère non ASCII dans un type ou un nombre ; le
  restreindre ; ajouter au corpus un nombre en chiffres non ASCII pour chaque sorte de valeur (opacité,
  soulignement, décalage d'ombre, marge de fond, taille d'image, couleur RGB) ; retirer le refus du lecteur ; adapter les attentes du corpus pour les textes non ASCII ; vérifier
  qu'un nom (infobulle, action, image) et un texte gardent leurs caractères non ASCII.
- Validation : le lecteur lit tous les textes du corpus et d'un différentiel aléatoire sous `en-US` et `tr-TR`
  (`AreFormattedRunsKnown` vrai), mêmes runs que le code historique ; un texte avec un caractère non ASCII près d'un
  `[` changé à chaque image alloue 0 octet. Vérification indépendante (`verifier`) avant ✅.
- Commit (MGUI) : `fix(text)!: read tag types and numbers as ASCII only`
- Validation (2026-11-16) : `MGUI.sln` et les deux solutions du moteur 0 erreur ; `MGUI.Tests` 3896 verts (31 tests
  ajoutés), `CasaEngine.Tests` 5556 verts, aucun sauté. Écrit par un `executor` ; verdict **CONFIRMED** d'un `verifier`
  neuf (opus) avant le commit. ADR-0033 de MGUI.

  | Texte avec un caractère non ASCII après un `[`, changé à chaque image | Avant | Après |
  |---|---|---|
  | Setter, octets par image | 10847 | 0 |
  | Update | 10104 | 0 |
  | Draw | 120 | 0 |

  - Changement : dans `FormattedTextTokenizer.cs` et `MGTextRun.cs`, `\d` devient `[0-9]` (opacité, RGB, facteur de
    couleur, décalage d'ombre, hauteur et décalage du soulignement, marge de fond, taille d'image) et les types de
    balise sont écrits lettre par lettre (`[Bb][Oo][Ll][Dd]`) au lieu de `(?i)` ; dans `FormattedTextScanner.cs`,
    l'ambiguïté (`IsAmbiguous`) est retirée. Le lecteur ne laisse plus un texte au code historique que pour une image
    sans taille dans un bloc sans bureau. Aucun membre public ne change.
  - Ce qui change de sens (ADR-0033), vérifié par le vérificateur sur 81 516 textes dans quatre cultures (0 différence
    sur un texte tout en ASCII, 550 textes différents, tous de ces deux sortes) :
    - `[bacKground=Red]` écrit avec le signe Kelvin (U+212A) n'est plus une balise : il devient du texte. C'était le
      seul caractère non ASCII qu'un type acceptait ;
    - **une taille d'image écrite en chiffres non ASCII** (`[img=Icon ١٦x١٦]`) : c'était du texte littéral, c'est
      maintenant une image dont le nom est toute la valeur, sans taille, comme `[img=Inconnu]` l'était déjà. C'est la
      conséquence des règles inchangées (« un nom est tout texte jusqu'au `]` »), mais elle va au-delà de « devient du
      texte » écrit en D9. Voir O14.
    - Les autres nombres en chiffres non ASCII (`[o=٠.٥]`, soulignement, ombre, marge, RGB, facteur) étaient déjà du
      texte, parce que leur interprétation échouait ; ils le restent.
  - Mêmes runs dans les deux chaînes : corpus porté à 390 textes sous `en-US`, `tr-TR`, `fr-FR` et la culture
    invariante, différentiel aléatoire de 20 000 textes (graine 20261116), plus celui du vérificateur (autre
    générateur, tous les chiffres décimaux Unicode, paires de substitution, lettres qui se replient sur de l'ASCII) :
    0 écart, 0 exception, 0 refus du lecteur avec un bureau.
  - Laissé sans décision, hors du dossier `Text/` : la recherche d'un nom de couleur dans une brosse
    (`ColorStringConverter`, comparaison selon la culture courante, suffixe `F` d'un facteur accepté) et
    `RGBColorRegex` de `IFillBrush.cs` (`\d`, sans effet : `int.Parse` refuse ensuite ces chiffres).
  - Constats non bloquants du vérificateur, reportés : P4, les analyseurs publics `FTTokenizer.ParseUnderlineValue` et
    `ParseBackgroundValue` appelés directement avec des chiffres non ASCII rendent une valeur vide au lieu de lever
    (aucun appelant hors du texte) ; P3, `Update` et `Draw` à 0 ne sont affirmés par un test que pour le setter (le
    vérificateur les a mesurés à 0).
  - Docs : ADR-0033, puce de l'ADR-0030, `MGUI/Docs/text-architecture.md` (règle de grammaire, fin de l'ambiguïté, et
    les trois lignes périmées par T3.1, T3.2 et le plafond de 128).

---

## Phase 4 — Clôture

### ✅ T4.1 — Documentation, validation globale, rapport

- Objectif : docs à jour (`MGUI/Docs/drawing-architecture.md`, `controls-architecture.md`, `text-architecture.md`,
  ADR), tableau final des mesures dans ce plan, limites restantes listées, validation globale, rapport de fin
  (`AGENTS.md` §12), ligne de `ai-agent/README.md` mise à jour. Le plan passe dans `tasks/archive/` après acceptation
  par l'auteur.
- Validation : section « Validation globale ».
- Commit : `docs(ai-agent): close the MGUI zero allocation follow-up`
- Validation (2026-11-16), sur le dernier état des deux branches : `dotnet build MGUI/MGUI.sln`,
  `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` 0 erreur ; `MGUI.Tests` 3896 verts,
  `CasaEngine.Tests` 5556 verts, aucun sauté (3684 et 5550 au départ). `MGUI.Samples` lancé à la fin de T1.6 et de la
  phase 2 : démarre et tient 20 secondes sans sortie d'erreur. Le smoke à l'œil (sample, éditeur : coins arrondis,
  dock, liste défilante) reste à faire par l'auteur. Rien n'est poussé ni fusionné ; le plan reste dans `tasks/`
  jusqu'à l'acceptation de l'auteur.
- Docs : `MGUI/Docs/rendering-architecture.md`, `custom-render-backend-integration.md`, `controls-architecture.md`
  (section « Allocation par image des controles »), `text-architecture.md` ; ADR de MGUI 0031 (poignées), 0032
  (transaction du `MainRenderer`), 0033 (grammaire ASCII), statut de l'ADR-0027, puces de l'ADR-0030.

  Tableau final, octets par image (Update / Draw) ou par opération :

  | Cas | Avant | Après | Reste |
  |---|---|---|---|
  | `SetDrawSettingsTemporary`, `SetRenderTargetTemporary` (paire, deux backends) | 112 / non mesuré | 0 | — |
  | `SetTransformTemporary`, `SetEffectTemporary`, valeur stable | 304 | 0 | 120 pour une valeur nouvelle à chaque appel |
  | Clip à stencil (paire, deux backends) | 1112 | 0 | chemin à masque non traité (D6) |
  | Image du backend de MGUI (`MainRenderer`) | 6624 au dessin, plus la transaction | 0 / 0 | — |
  | `MGScrollViewer` repos | 136 / 210 | 0 / 0 | — |
  | `MGScrollViewer` molette | 192 / 331 | 56 / 0 | O7, O8 |
  | `MGListBox` repos et survol | 272 / 210 | 0 / 0 | — |
  | `MGListView` repos et survol | 136 / 786 | 0 / 0 | — |
  | `MGTreeView` repos et survol | 136 / 786 | 0 / 0 | O9 sur GPU réel |
  | `MGTabControl` survol | 88 / 0 | 0 / 0 | — |
  | `MGContextMenu` ouvert, repos et survol | 1280 / 1178 | 0 / 0 | — |
  | `MGMenuBar` fermée et ouverte, survol | 1280 / 874 | 0 / 0 | — |
  | `MGGrid` avec sélection, repos | 0 / 288 | 0 / 0 | — |
  | `MGGrid`, sélection par clic | 2060 / 288 | 232 / 0 | O10 |
  | `MGDockHost` repos (trois dispositions) | 160 / 208 | 0 / 0 | — |
  | `MGDockHost` glissement d'onglet | 3248 / 848 | 80 / 0 | O11 |
  | `MGTextBox` repos avec focus | 0 / 0 | 0 / 0 | — |
  | `MGTextBox` frappe | 3876 / 0 | 3296 / 0 | O12, O13 |
  | Fenêtre `SizeToContent` | 0 / 0 | 0 / 0 | fermée sans code |
  | Bloc sans balise souligné (dessin) | 120 | 0 | — |
  | Changement de texte pendant une révélation | 608 | 0 | — |
  | `[color=NotAColor]` changé à chaque image | 2656 | 0 | — |
  | Caractère non ASCII après un `[`, changé à chaque image | 10847 + 10104 + 120 | 0 | — |

- Tâches : 23 ✅, 3 ⚠️ (T2.1, T2.2, T2.4 : tout ce qui était dans leurs fichiers est fait et commité ; leur reste attend
  une décision de l'auteur, points ouverts O7, O8, O10, O11).
- Points ouverts pour l'auteur : O7 (événement de molette), O8 (clips d'un contenu défilé), O9 (`FillPolygon` des
  backends), O10 (événements de clic), O11 (`DockDropTarget`), O12 (entrée clavier), O13 (curseur de `MGTextBox` et
  lignes historiques), O14 (taille d'image en chiffres non ASCII, à valider).
- Limites restantes, déjà écrites : D6 (masque), D12 (plafond de 128, tampons qui ne rétrécissent pas, changement de
  police), 32 réglages dérivés gardés par backend, constats P3 et P4 des deux vérifications (T1.7, T3.4).

---

## Phase 5 — Suites décidées par l'auteur (O7 à O14)

Décision de l'auteur du 2026-11-16 : « pour ce qui reste, prends les recommandations et fais tâche par tâche ». Les
recommandations des points ouverts O7 à O14 deviennent les décisions D13 à D20. Aucun code de cette phase avant
l'approbation de ce découpage.

État vérifié pour cette phase (trois éclaireurs en lecture seule le 2026-11-16, faits porteurs revérifiés par `rg`) :

- Entrée souris (`MGUI/MGUI.Shared/Input/Mouse/MouseTracker.cs`, `MouseEventArgs.cs`) : `MouseTracker.Update` crée un
  objet par événement : molette (`:409`), appui (`:443`), relâchement (`:462`), clic (`:473`), début de glissement
  (`:451`, `:511`), fin de glissement (`:542`), séquence de clics (`:472`) avec ses métadonnées
  (`MouseEventArgs.cs:75`) et une `WeakReference` (`:105`). Le déplacement et le glissement en cours sont déjà
  réutilisés (`_ReusedMoveEvent` `:206`, `_ReusedDraggedEvents` `:343`), avec la règle « lire pendant l'événement, ne
  pas garder » dans leur commentaire. Les objets sont liés entre eux : le relâchement tient l'appui, le clic tient le
  relâchement, le clic précédent (`PreviousClickInSequence`) et sa séquence (`FirstClick`, `LatestClick`), la fin de
  glissement tient le début.
- Qui garde un objet d'entrée au-delà de l'image : le suivi lui-même (`_RecentButtonPressedEvents` `:209` jusqu'au
  relâchement, `_RecentButtonClickedEvents` `:249` pour le double clic, `RecentDragStartEvents` `:270` jusqu'à la fin
  du glissement) et quatre contrôles qui gardent l'appui jusqu'au relâchement (`PressedArgs` de `MGButton.cs:309`,
  `MGCheckBox.cs:263`, `MGRadioButton.cs:134`, `MGToggleButton.cs:240`). Lecteurs des liens hors du dossier d'entrée :
  `MGResizeGrip.cs:203` (`e.DragStartArgs.HandledBy`), `MGTextBox.cs:1214` (`e.PressedArgs.HandledBy`) ;
  `PreviousClickInSequence` n'est lu que par des tests (`Focus/InputEnhancedTests.cs`). Les objets portent un état
  modifiable (`SetHandledBy`, 25 appels dans `MouseHandler.cs`, remise à zéro `InputTracker.cs:43-48`).
- Entrée clavier (`Keyboard/KeyboardTracker.cs`, `KeyboardEventArgs.cs`, `KeyboardHandler.cs`) : un flux et un appui
  par touche (`KeyboardTracker.cs:195-196`, `:281-282`), un relâchement (`:212`), un clic (`:218`), une répétition
  (`KeyboardHandler.cs:278`). `RecentKeyPressedEvents` (`:112`) garde l'appui tant que la touche est tenue.
  `IsAnyPressed(params Keys[] Keys) => Keys.Any(x => IsPressed(x))` (`:136`) sert `IsShiftDown`, `IsControlDown`,
  `IsAltDown` (`:142-144`), lus par 8 fichiers produit.
- Deux cas du clavier qui portent la conception de T5.3 (relevés par le relecteur du plan, lus dans le code) :
  la répétition n'est pas créée par le suivi mais par chaque `KeyboardHandler`, qui en fait sa propre instance à
  chaque répétition due (`KeyboardHandler.cs:236-281`) ; la remise dépend de l'état « traité » de cette instance
  (`:204-216`), donc chaque gestionnaire a aujourd'hui un état « traité » qui lui est propre. Et quand une touche est
  relâchée à l'image où arrive une saisie de texte native non consommée pour la même touche,
  `CreateTextInputOnlyPressedEvents` crée dans cette même image un nouvel appui et un nouveau flux pour cette touche
  (`KeyboardTracker.cs:262-285`), alors que le relâchement déjà construit désigne l'appui tenu (`:204-227`) et que le
  relâchement et le clic sont routés par le flux de cet appui (`KeyboardHandler.cs:190-195`, `:219-225`,
  `KeyboardEventArgs.cs:116`, `:149`).
- Identité de routage (relevée par le second relecteur du plan) : un flux de touche porte le gestionnaire qui l'a
  adopté (`KeyboardHandler.AdoptStreamIfHandled`, `KeyboardHandler.cs:56-61`, `OwnerHandlerId`,
  `KeyboardEventArgs.cs:46-55`), et le relâchement, la répétition et le clic sont routés par ce propriétaire avant le
  focus (`KeyboardHandler.cs:191-195`, `:205-209`, `:220-225`). Une séquence de clics porte la cible qui l'a adoptée
  (`MouseClickSequence.TryAdoptTarget`, `MouseEventArgs.cs:94-106`, refuse une seconde cible), et le double clic ne
  va qu'à une cible qui correspond (`MouseHandler.cs:716-718`, `:143-164`). Aujourd'hui un flux neuf et une séquence
  neuve naissent sans propriétaire (`KeyboardTracker.cs:195`, `:281`, `MouseTracker.cs:472`).
- `MGButton` relit l'appui gardé aux images suivantes pour sa répétition (`MGButton.cs:330-333`) et ne l'efface que
  dans ses gestionnaires de relâchement.
- Le moteur lit ces arguments dans 16 fichiers de `CasaEngine.Editor` (`rg "Base(Mouse|Key)\w+EventArgs"`) ; aucun
  champ de ces types n'y a été trouvé par les éclaireurs, recherche à refaire dans la tâche.
- Clips d'élément (`MGUI/MGUI.Core/UI/MGElement.cs:5439-5465`) : les deux clips d'un élément passent par
  `PushClipTemporary(ClipDefinition)`. Un clip rectangulaire résout toujours en scissor
  (`ClipStrategyResolver.cs:19`), comme `PushRectangleClip`. `DebugName` n'est lu par le code produit que pour des
  messages d'exception et comme clé de cache. Des tests en dépendent : 37 empreintes de journal figées dans 3 fichiers
  (`ControlFamilyZeroAllocationTests.cs`, `DesktopFrameCharacterisationTests.cs`, `DesktopFrameLayoutTextTests.cs`),
  `TransformedClipTests.cs:39-47` (cherche le clip nommé `ScrollViewer.Viewport`),
  `GridColorPickerSwatchClipTests.cs:44` et `:74`, et 8 doublures qui font passer `PushRectangleClip` par
  `PushClipTemporary`. `ElementClipRequest.cs` n'a aucun appelant.
- `FillPolygon` : les deux backends font `Any()` puis `ToArray()` puis `PrimitiveDrawing.DrawSolidPolygon(origine,
  tableau, couleur, false)` (`DrawTransaction.cs:790-801` de MGUI, `CasaLegacyShapeRenderer2D.cs:321-330`).
  `DrawSolidPolygon` vient de MonoGame.Extended, son source n'est pas dans le dépôt : on ne sait pas s'il alloue.
  Appelants qui passent déjà un tableau : `MGEllipse`, `MGPolygon`, `UISymbolDrawing` ; qui passent du LINQ :
  `MGRatingControl.cs:648`, `MGHighlightBorderBrush.cs:794-796`, `MGDockedBorderBrush.cs:150-169`.
- Cible de dépôt (`MGUI/MGUI.Core/UI/Docking/DockDropTarget.cs`, `MGDockHost.cs:2867-2897`) : classe publique à
  propriétés modifiables, sans égalité de valeur ; `CurrentDropTarget` a un setter privé qui notifie quand la
  référence change. Créations : `MGDockHost.cs:2927` et `:3007`, `DockDropCalculator.cs` (14 `new`, par zone et par
  bord de l'hôte). Aucun abonné à `PropertyChanged` d'un `MGDockHost` et aucune lecture hors de `MGDockHost` et des
  tests dans les deux dépôts ; le dépôt lit `CurrentDropTarget` une fois (`:890`).
- Champs écrits après coup sur une cible (relevé par le relecteur de clôture du plan) : le calculateur ne remplit que
  `TargetNode`, `Zone`, `HitRect` et `PreviewRect` (`DockDropCalculator.cs:111-198`, `:228-268`) ; l'hôte écrit ensuite
  `IsSplitterDrop` et `SplitterNode` sur l'objet rendu pour une barre de séparation (`MGDockHost.cs:812-821`) et
  `TabIndex` sur une cible de centre ou de réordonnancement (`:2958-2968`, `:2927-2947`, `:3007-3031`). Les autres
  champs ne valent leur défaut (`TabIndex = -1`, `IsHostEdge`, `IsSplitterDrop` faux, `SplitterNode` nul,
  `DockDropTarget.cs:40-60`) que parce que chaque cible est aujourd'hui un objet neuf ; `ExecuteDrop` lit ces champs.
- Curseur de `MGTextBox` (`MGUI/MGUI.Core/UI/Text/TextRenderInfo.cs:86-206`) : il lit `MGTextBlock.Lines`, les runs de
  texte de chaque ligne et `MGTextLine.OriginalCharacterIndices` (`MGTextLine.cs:48`), table que seul
  `MGTextLine.ParseLines` remplit (tiret de césure qui partage l'index du caractère suivant, saut de ligne compté,
  ligne d'espace ignorée). Les mises en page internes (`PlainTextLayout.cs`, `FormattedTextLayout.cs`) gardent des
  morceaux qui pointent dans leur propre tampon, sans index dans le texte d'origine. Le bloc d'un `MGTextBox` est en
  balisage activé avec un texte échappé (`MGTextBox.cs:236`, `EscapeMarkdown`) et retour à la ligne ;
  `MGRichTextBox` et `MGPasswordBox` en dérivent. Aucun test ne vérifie la géométrie du curseur (position après
  frappe, clic, sélection).
- Deux lecteurs que T5.8 doit tenir (relevés par le relecteur du plan) : `MGTextCaret.MoveToApproximateScreenPosition`
  choisit le bord gauche ou droit d'un caractère à partir de `CharInfo.Line.Source`, la `MGTextLine` gardée par
  `LineRenderInfo`, et de ses runs (`MGTextCaret.cs:220-223`, `TextRenderInfo.cs:122`, `:257`) ; et un
  `MGRichTextBox` qui a une sélection, des segments stylés ou un colorateur passe par `SetTextRuns`
  (`MGRichTextBox.cs:376-386`), cas où `MGTextBlock.UpdateLines` prend sa troisième branche et construit les
  `MGTextLine` directement, sans mise en page interne (`MGTextBlock.cs:1590-1618`).
- Dossier de docs de MGUI : `Docs/` dans l'index git, `docs/` sur le disque ; un nouvel ADR s'indexe par le chemin
  que `git status` montre, et `git ls-files` doit le lister sous `Docs/`.

Décisions de cette phase :

| Réf | Décision |
|---|---|
| D13 | (O7, O10, O12) Les arguments d'événement d'entrée de `MGUI.Shared/Input` sont réutilisés. Durée de validité, écrite dans un ADR de MGUI et dans le commentaire de chaque type : molette, pendant l'image ; appui, relâchement, clic, début et fin de glissement d'un bouton, jusqu'à l'événement suivant de la même sorte pour le même bouton (un appui reste donc valide tant que le bouton est tenu et pendant son relâchement) ; appui, relâchement et clic d'une touche, de même par touche. Deux instances restent séparées pour ne rien changer : la répétition d'une touche appartient à chaque gestionnaire (une instance par gestionnaire et par touche, avec son propre état « traité », comme aujourd'hui) ; l'appui créé pour une saisie de texte seule a sa propre instance et son propre flux, distincts de ceux de l'appui tenu, pour que le relâchement et le clic de la même image gardent leur appui, son heure et son flux. À chaque réutilisation, l'état « traité » est remis à zéro, et toute l'identité de l'objet repart comme celle d'un objet neuf : un flux de touche reçoit un nouvel identifiant, son heure de départ, son appui initial, et son propriétaire (`OwnerHandlerId`) est effacé ; une séquence de clics reçoit un nouvel identifiant, son bouton, son heure de départ, et sa cible adoptée (`ValidatedTargetId`, `ValidatedTargetHost`) est effacée. Une séquence de clics d'un bouton est valide jusqu'à la séquence suivante du même bouton. |
| D14 | (O10) Les membres qui désigneraient une instance réécrite sont retirés sans alias : `BaseMouseClickedEventArgs.PreviousClickInSequence`, `MouseClickSequence.FirstClick` et `LatestClick`. `ClickCount` et la séquence restent ; si un appelant des deux dépôts a besoin d'une valeur du clic précédent, elle est exposée comme valeur. Rupture d'API (mémoire `modern-design-over-api-compat`). |
| D15 | (O12) `KeyboardTracker.IsAnyPressed(params Keys[])` reste publique ; `IsShiftDown`, `IsControlDown` et `IsAltDown` ne passent plus par elle. |
| D16 | (O8) Les clips rectangulaires d'un élément passent par `PushRectangleClip` (ADR de MGUI écrit en T5.5). Le journal des appels de dessin change pour ces clips (`PushRectangleClip/2` au lieu de `PushClipTemporary/1`, plus de nom) ; le rendu ne change pas. Les clips arrondis et à géométrie gardent `PushClipTemporary`. |
| D17 | (O9) `FillPolygon` des deux backends ne copie plus ses sommets quand l'appelant passe un tableau ou une liste indexable. Si `PrimitiveDrawing.DrawSolidPolygon` alloue lui-même, le fait est mesuré et écrit, et la tâche s'arrête là : pas de remplacement de MonoGame.Extended dans cette phase. |
| D18 | (O11) `MGDockHost` garde une instance de `DockDropTarget` pour `CurrentDropTarget` : ses valeurs sont réécrites à chaque recalcul et `PropertyChanged` n'est levé que quand une valeur change ou quand la cible apparaît ou disparaît. Chaque remplissage d'une cible gardée (emplacement du calculateur, cible de centre, cible de réordonnancement, instance de `CurrentDropTarget`) écrit les huit champs comme un objet neuf les aurait : `TargetNode`, `Zone`, `HitRect`, `PreviewRect`, `TabIndex` à -1 sauf valeur calculée, `IsHostEdge`, `IsSplitterDrop` faux et `SplitterNode` nul sauf barre de séparation. La valeur rendue par la méthode publique `GetDropTarget` est cette même instance. Changement de comportement observable : ADR de MGUI écrit en T5.7 et section Docking de `MGUI/Docs/controls-architecture.md`. |
| D19 | (O13) Quand le bloc d'un `MGTextBox` a une mise en page interne (texte sans balise ou à balises), la géométrie du curseur ne relit plus `MGTextBlock.Lines` : la mise en page interne donne la table des index d'origine. Quand il n'en a pas (runs explicites d'un `MGRichTextBox` avec sélection, segments stylés ou colorateur), elle continue de lire `MGTextBlock.Lines`. ADR de MGUI écrit en T5.8. La table doit être identique à `MGTextLine.OriginalCharacterIndices` sur le corpus ; sinon la tâche s'arrête en ⚠️ avec le premier texte qui diffère. |
| D20 | (O14) Validé par l'auteur : une balise d'image dont la taille est en chiffres non ASCII reste une image nommée par toute sa valeur, sans taille (ADR-0033 de MGUI). Aucun code. |

Règles propres à cette phase, en plus des règles d'exécution du plan : les tâches sont indépendantes sauf mention (T5.2 et T5.3 s'appuient sur l'ADR écrit en T5.1) ;
une tâche dont la preuve échoue s'arrête en ⚠️ sans bloquer les autres ; les tests d'égalité à un reste mesuré
écrits en phase 2 (molette, clic, frappe, cible de dépôt) deviennent des affirmations à 0 dans la tâche qui retire ce
reste ; vérification indépendante (`verifier`, opus) en T5.4 et en T5.9.

### ✅ T5.1 — ADR de l'entrée, molette et modificateurs

- Objectif : l'ADR de D13 à D15 existe ; l'événement de molette est réutilisé ; `IsShiftDown`, `IsControlDown` et
  `IsAltDown` n'allouent plus.
- Fichiers : `MGUI/Docs/decisions/0034-*.md` et son index, `MGUI/Docs/input-architecture.md` ;
  `MGUI/MGUI.Shared/Input/Mouse/MouseTracker.cs`, `MouseEventArgs.cs`, `MouseHandler.cs` (si la remise à zéro de
  l'état « traité » le demande), `MGUI/MGUI.Shared/Input/InputTracker.cs`, `Keyboard/KeyboardTracker.cs` ;
  `MGUI/MGUI.Core/UI/MGTextBox.cs` (son contournement `IsControlDown` redevient la propriété) ;
  `MGUI/MGUI.Tests/Input/` et `Architecture/ControlFamilyZeroAllocationTests.cs`.
- Étapes : écrire l'ADR ; une instance de `BaseMouseScrolledEventArgs` gardée par le suivi, remplie à chaque image où
  la molette bouge, état « traité » remis à zéro ; chercher tout code des deux dépôts qui garde cet événement
  (`rg` sur les champs, listes et fermetures) et l'écrire dans la note ; un gardien trouvé est corrigé pour copier les
  valeurs, ou la tâche s'arrête en ⚠️ avec le site s'il ne peut pas l'être sans changer son comportement ;
  modificateurs par deux `IsPressed`.
- Validation : `ScrollViewerWithTallContent` à 0 et 0 pendant la molette, et son test de phase 2 affirme 0 ; test
  « l'événement de molette est la même instance d'une image à l'autre, avec ses valeurs et un état non traité » ; test
  de zéro des trois modificateurs ; mêmes journaux figés ; trois solutions 0 erreur, deux suites vertes.
- Commit : `perf(input)!: reuse the scroll event and read modifiers without allocating` (MGUI), puis le commit du moteur.
- Validation (2026-11-16) : les trois solutions 0 erreur ; `MGUI.Tests` 3901 verts (5 tests ajoutés), `CasaEngine.Tests`
  5556 verts, aucun sauté. Écrit par un `executor`. ADR-0034 de MGUI (décisions D13 à D15 entières ; les boutons de
  souris et les touches sont livrés par T5.2 et T5.3).

  | `MGScrollViewer`, molette | Update avant | Update après | Draw |
  |---|---|---|---|
  | Octets par image | 56 | 0 | 0 |

  - Changement : `MouseTracker` garde une instance de `BaseMouseScrolledEventArgs`, remplie à chaque image où la
    molette bouge, état « traité » remis à zéro ; `CurrentScrollEvent` reste nul sans mouvement. `IsShiftDown`,
    `IsControlDown` et `IsAltDown` lisent deux `IsPressed` ; `IsAnyPressed` est inchangée. Le contournement de
    `MGTextBox` est retiré.
  - Relevé des gardiens (deux dépôts) : aucun champ, propriété ni collection de ce type ; 7 gestionnaires de
    `CasaEngine.Editor` et 7 de `MGUI.Core` lisent l'événement pendant l'appel ; `MGOverlay` le capture dans un
    délégué exécuté pendant l'appel. Aucun gardien, aucune correction.
  - Le test de phase 2 de la molette affirme maintenant 0 ; journaux figés inchangés.
  - Reste noté : commentaire périmé de `MGDockHost.cs:542` sur les modificateurs, corrigé en T5.2.

### ✅ T5.2 — Événements de bouton de souris réutilisés

- Objectif : un appui, un relâchement, un clic, un début et une fin de glissement n'allouent plus après le premier
  usage de chaque bouton (464 octets par clic mesurés), selon D13 et D14.
- Fichiers : `MGUI/MGUI.Shared/Input/Mouse/` (`MouseTracker.cs`, `MouseEventArgs.cs`, `MouseHandler.cs`),
  `InputTracker.cs` ; lecteurs et gardiens dans `MGUI.Core` (`MGButton.cs`, `MGCheckBox.cs`, `MGRadioButton.cs`,
  `MGToggleButton.cs`, `MGResizeGrip.cs`, `MGTextBox.cs` et ceux que le compilateur et `rg` désignent) et dans
  `CasaEngine.Editor` s'il en reste ; tests `MGUI.Tests/Input/`, `Focus/InputEnhancedTests.cs`, `Focus/FocusTests.cs`,
  `Architecture/ControlFamilyZeroAllocationTests.cs`.
- Étapes :
  1. Relever, avant tout changement, chaque endroit des deux dépôts qui garde un de ces objets après le retour du
     gestionnaire ou qui lit un lien entre deux objets ; l'écrire dans la note de la tâche. Un gardien dont la durée
     dépasse celle de D13 est corrigé pour copier les valeurs ; s'il ne peut pas l'être sans changer son
     comportement, ⚠️ avec le site. Cas déjà connu à trancher ici : `MGButton` relit son appui gardé pour sa
     répétition (`MGButton.cs:330-333`) et ne l'efface qu'au relâchement ; vérifier ce qu'il lit si le relâchement
     ne lui est pas remis.
  2. Figer le comportement avant le changement : rejouer un scénario scripté (appuis, relâchements, clics simples,
     doubles et triples sur les trois boutons, clic sur un autre bouton entre deux clics, glissements par les deux
     conditions de départ, relâchement hors de l'élément, clic traité par un parent, appui d'un bouton pendant qu'un
     autre est tenu, **double clic du même bouton sur un élément X puis sur un autre élément Y**, qui doit recevoir
     son double clic) et enregistrer, image par image,
     les événements reçus par les gestionnaires avec leurs valeurs (bouton, position, temps, compteur de clics,
     identifiant de séquence, traité par qui).
  3. Instances gardées par bouton (et par condition pour le glissement), séquence et métadonnées réutilisées avec
     leur identité remise à zéro à chaque nouvelle séquence (D13 : identifiant, bouton, heure de départ, cible
     adoptée effacée), `WeakReference` réarmée par `SetTarget`, détection du double clic sur des valeurs gardées et non sur l'objet
     précédent.
  4. Retirer les membres de D14 et adapter leurs tests, ainsi que ceux qui affirment que deux séquences sont deux
     objets (`Focus/InputEnhancedTests.cs:214`, `:239`) : ils affirment désormais deux identifiants différents,
     relevés au moment de chaque clic et non relus sur la référence gardée (de même pour `:274-275`, qui relit
     l'état « traité » du premier clic après le second). `MouseTracker.GetMultiClickCount` (publique, statique,
     `MouseTracker.cs:562-569`) lit l'heure du relâchement du clic précédent : elle lit désormais la valeur gardée ;
     l'ADR dit si sa signature change. La garde `withoutSelection.UpdateTotal > 0` du test de la grille
     (`ControlFamilyZeroAllocationTests.cs:703`) devient fausse et est réécrite.
- Validation : le scénario figé rejoue à l'identique (aux membres retirés près, nommés dans la note), y compris le cas
  en gras ; un test dédié échoue si la cible adoptée d'une séquence survit dans la séquence suivante du même bouton ; la sélection
  de `MGGrid` par clic lit 0 et 0, et son test de phase 2 affirme 0 ; test « un appui gardé reste lisible jusqu'au
  relâchement » pour les quatre contrôles ; mêmes journaux figés ; trois solutions 0 erreur, deux suites vertes.
- Commit : `perf(input)!: reuse the mouse button events` (MGUI), puis `perf(ui)!: take the reused mouse button events of MGUI` (moteur).
- Validation (2026-11-16) : les trois solutions 0 erreur ; `MGUI.Tests` 3927 verts (26 tests ajoutés), `CasaEngine.Tests`
  5556 verts, aucun sauté. Écrit par un `executor`. ADR-0034 de MGUI complété.

  | `MGGrid`, sélection par clic | Update avant | Update après | Draw |
  |---|---|---|---|
  | Octets par image | 232 | 0 | 0 |

  - Changement (`MouseTracker.cs`, `MouseEventArgs.cs`) : un appui, un relâchement, un clic et une séquence gardés par
    bouton, un début et une fin de glissement par condition et par bouton, créés au premier usage et remplis avec
    l'état « traité » remis à zéro ; la séquence repart avec une identité neuve (identifiant, bouton, heure, cible
    adoptée effacée) ; double clic détecté sur des valeurs gardées. `MouseHandler.cs`, `InputTracker.cs` et les
    gardiens de `MGUI.Core` n'ont pas changé.
  - Relevé des gardiens (deux dépôts, sur le code d'avant) : tous couverts par la durée de D13, aucun à corriger ;
    aucun dans le moteur (16 gestionnaires de `CasaEngine.Editor` lisent pendant l'appel).
  - Ruptures d'API : retirés `BaseMouseClickedEventArgs.PreviousClickInSequence` (et son paramètre de constructeur),
    `MouseClickSequence.FirstClick` et `LatestClick` ; `MouseTracker.GetMultiClickCount` change de signature (valeurs
    du clic précédent au lieu de l'objet) ; les membres réécrits par un remplissage ont un setter privé. Aucun usage
    dans le moteur.
  - Comportement : scénario figé sur le code d'avant (2397 lignes, six gestionnaires, valeurs copiées au moment de
    chaque événement), rejoué à l'identique après ; il contient le double clic sur X puis sur Y. Sans l'effacement de
    la cible adoptée, le rejeu et deux tests dédiés échouent (vérifié puis annulé). Tests de
    `Focus/InputEnhancedTests.cs` adaptés (cinq) : ils comparent des identifiants relevés au moment de chaque clic.
  - Le test de phase 2 de la grille affirme maintenant 0, avec et sans mode de sélection ; journaux de dessin figés
    inchangés. Commentaire périmé de `MGDockHost.cs:542` corrigé.
  - Différence restante, écrite dans l'ADR : si le relâchement n'est jamais remis à un `MGButton` à répétition, son
    appui gardé désigne, au prochain appui du même bouton, cet appui plus récent (avant : l'ancien). Seule la
    répétition le lit. Jugé correct par l'agent, laissé à l'avis du vérificateur de T5.4.

### ✅ T5.3 — Événements de touche réutilisés

- Objectif : un appui, un relâchement, un clic et une répétition de touche n'allouent plus après le premier usage de
  chaque touche (696 octets par touche mesurés), selon D13.
- Fichiers : `MGUI/MGUI.Shared/Input/Keyboard/` (`KeyboardTracker.cs`, `KeyboardEventArgs.cs`, `KeyboardHandler.cs`) ;
  lecteurs dans `MGUI.Core` et `CasaEngine.Editor` que `rg` désigne ; tests `MGUI.Tests/Input/`, `Focus/`,
  `Architecture/ControlFamilyZeroAllocationTests.cs`.
- Étapes : même méthode que T5.2. Relevé des gardiens. Scénario figé avant le changement, enregistré image par image
  et par gestionnaire (qui a reçu quoi, heure d'appui, identifiant de flux, traité par qui) : touches seules, tenues
  avec répétition, deux touches à la fois, modificateurs, saisie de texte, focus qui change pendant qu'une touche est
  tenue, **une touche tenue dont la répétition va à deux gestionnaires dont l'un la marque traitée**, **le
  relâchement d'une touche dans l'image où arrive une saisie de texte native de la même touche**, et **une touche
  appuyée, traitée et relâchée par un gestionnaire A, puis le focus passé à B, puis la même touche appuyée, répétée
  et relâchée**, que B doit recevoir. Puis : appui,
  relâchement, clic, flux et métadonnées gardés par touche dans le suivi, créés à leur premier usage, le flux repartant
  à chaque nouvel appui avec une identité neuve (D13 : identifiant, heure, appui initial, propriétaire effacé) ; répétition
  gardée par gestionnaire et par touche ; appui et flux de saisie de texte seule gardés à part (D13).
- Validation : le scénario figé rejoue à l'identique, y compris les trois cas en gras ; un test dédié échoue si le
  propriétaire d'un flux survit dans le flux suivant de la même touche ; un test dédié pousse l'image
  « relâchement et saisie de texte de la même touche » et affirme que le relâchement et le clic portent l'heure
  d'appui, l'identifiant de flux et le propriétaire d'origine, et vont aux mêmes gestionnaires qu'avant ; la zone de
  saisie qui ignore les touches lit 0 et 0, et la garde `ignored.UpdateTotal > 0` de
  `ControlFamilyZeroAllocationTests.cs:1243`, qui devient fausse, est réécrite ; le test de frappe de phase 2 est
  resserré au reste hors entrée (environ 2600 octets, objet de T5.8) ; trois solutions 0 erreur, deux suites vertes.
- Commit : `perf(input)!: reuse the key events` (MGUI), puis le commit du moteur.
- Validation (2026-11-16) : les trois solutions 0 erreur ; `MGUI.Tests` 3938 verts (11 tests ajoutés), `CasaEngine.Tests`
  5556 verts, aucun sauté. Écrit par un `executor`. ADR-0034 de MGUI complété. La vérification indépendante de
  l'entrée (T5.4) n'est pas faite : arrêt demandé par l'auteur après cette tâche.

  | `MGTextBox` avec focus, octets par image (Update) | Avant | Après |
  |---|---|---|
  | Touches ignorées (F1, F2) | environ 312 à 320 | 0 |
  | Frappe (A puis retour arrière) | 2912 (dont environ 312 d'entrée clavier) | 2600 |

  - Le chiffre d'avant est celui relevé au début de cette tâche, après T5.1 ; les 696 octets notés en T2.9
    comprenaient les lectures de modificateurs retirées en T5.1.
  - Changement (`KeyboardTracker.cs`, `KeyboardEventArgs.cs`, `KeyboardHandler.cs`) : appui, flux, relâchement et clic
    gardés par touche dans le suivi ; un appui et un flux séparés par touche pour la saisie de texte seule ; la
    répétition gardée par gestionnaire et par touche ; à chaque nouvel appui le flux repart avec une identité neuve
    (identifiant, heure, appui initial, propriétaire effacé) ; les chaînes des caractères de code inférieur à 256
    d'une saisie de texte sont gardées. Aucun membre public retiré, aucune signature changée.
  - Relevé des gardiens (deux dépôts, sur le code d'avant) : aucun objet d'événement gardé ; `MGTextBox` garde des
    identifiants de flux (des valeurs). Aucune correction.
  - Comportement : scénario figé sur le code d'avant (1038 lignes, quatre gestionnaires, valeurs copiées au moment de
    chaque événement), rejoué à l'identique. Il contient les trois cas du plan : répétition vers deux gestionnaires
    dont l'un la marque traitée ; relâchement d'une touche dans l'image d'une saisie de texte native de la même
    touche ; touche traitée par A, focus passé à B, même touche appuyée, répétée et relâchée. Deux mutations
    vérifiées puis annulées : garder le propriétaire d'un flux au redémarrage, et réutiliser l'appui tenu pour la
    saisie de texte seule, font échouer le rejeu et les tests dédiés.
  - Le test de frappe de phase 2 affirme maintenant le seul reste hors entrée (1 300 000 octets sur 500 images,
    inchangé, objet de T5.8) ; le test des touches ignorées affirme 0. Journaux de dessin figés inchangés.
  - Reste : une saisie de texte native d'un caractère de code 256 ou plus (IME) crée encore sa chaîne, valeur que
    l'événement porte ; non mesuré.

### ✅ T5.4 — Vérification indépendante de l'entrée

- Objectif : un `verifier` neuf confirme ou réfute T5.1 à T5.3 : mêmes événements avec les mêmes valeurs qu'avant,
  durées de validité de D13 tenues (y compris pour un gestionnaire qui garde un appui jusqu'au relâchement), état
  « traité » jamais hérité d'un usage précédent, zéro allocation après chauffe, aucun code des deux dépôts qui garde
  un objet au-delà de sa durée, aucune régression des deux suites.
- Sources : diff de T5.1 à T5.3, ADR, scénarios figés.
- Validation : verdict CONFIRMED, ou constats traités selon leur priorité et revérifiés une fois.
- Commit : `docs(input): record the verification of the reused input events` (moteur, ce plan).
- Validation (2026-11-16, seconde machine, branche `chantier/mgui-nonalloc-phase5`) : `verifier` neuf (opus), verdict
  **CONFIRMED**, aucun constat P0 à P2. Aucun changement de code.
  - Preuves du vérificateur : les deux scénarios figés, posés sur le code d'avant (`9881ff93`, extrait hors du dépôt),
    y passent, donc ils viennent bien de l'ancien code ; leurs valeurs sont copiées au moment de chaque événement ;
    ils contiennent les cas en gras de T5.2 et de T5.3. Une sonde différentielle écrite par le vérificateur (ancien et
    nouveau code, mêmes séquences : clic puis appui long puis clic, doubles clics gauche et droit entrelacés,
    glissement puis clic, deux appuis de saisie de texte seule puis un vrai appui, touche tenue avec focus déplacé,
    modificateurs) rend les mêmes événements, valeurs, états « traité » et identifiants, aux deux avis ci-dessous
    près. Chaque remplissage remet l'état « traité » à zéro ; séquence de clics et flux de touche repartent avec une
    identité neuve. Les tests de zéro (molette, sélection de `MGGrid`, touches ignorées, modificateurs) passent à 0.
    Aucun gardien hors durée de D13 dans les deux dépôts ; `InputTracker.Update` n'a qu'un appelant.
  - Avis P4 reportés, tous deux déjà écrits dans l'ADR-0034 de MGUI : (A1) l'appui gardé d'un `MGButton` dont le
    relâchement n'est jamais remis désigne l'appui suivant du même bouton ; seule la répétition le lit, et seulement
    quand ce nouvel appui est dans le bouton ; jugé correct et sans effet visible atteignable. (A2) Les liens lus par
    `MouseTracker.RecentButtonClickedEvents[bouton].ReleasedArgs` ou `.PressedArgs` après un relâchement ultérieur qui
    n'est pas un clic désignent ce relâchement plus récent ; aucun lecteur de cette forme dans les deux dépôts.
  - Suites sur cette machine : trois solutions 0 erreur ; `MGUI.Tests` 3938 verts, aucun sauté ; `CasaEngine.Tests`
    6119 tests (la référence de 5556 date d'avant les fusions d'`origin/main`), 6111 verts, 0 sauté, **8 échecs
    d'environnement** : six tests de `DesktopFrameGpuAllocationTests` et un de `DrawTransactionRearmGpuTests`
    comparent l'image au repos à une empreinte de pixels prise sur la première machine (`124E0E0F…` attendu,
    `D297B949…` obtenu ; le commentaire du test dit qu'un autre GPU ou pilote peut différer), et
    `IblCubeGeneratorGpuTests.OpenGlChain_IgnoresTheRequestedCubeLevel_SoTheAtlasIsUsed` lit une autre couleur. Le
    vérificateur a rejoué ces trois classes avec les binaires de MGUI d'avant T5.1 : mêmes 8 échecs, mêmes valeurs.
    Ils ne viennent donc pas de T5.1 à T5.3. Voir O15.
  - Non vérifié : l'empreinte absolue de l'image de glissement sur le GPU de la première machine (remplacée par une
    comparaison ancien et nouveau MGUI sur cette machine, empreintes égales) ; aucun lancement de l'éditeur ni des
    samples.

### ✅ T5.5 — Clips rectangulaires d'élément par `PushRectangleClip`

- Objectif : un élément dont le clip est un rectangle ne crée plus de `ClipDefinition`, quelles que soient ses bornes
  (environ 120 octets par image pour un contenu défilé sur des décalages nouveaux), selon D16.
- Fichiers : `MGUI/MGUI.Core/UI/MGElement.cs` (`Draw`, crochets de clip, cache et anneau des définitions
  rectangulaires), les redéfinitions qui rendent un rectangle (`MGScrollViewer.cs`, `MGBorder.cs`, `MGTextBox.cs`,
  `Adorners/MGAdorner.cs`, `MGEllipse.cs`, `MGPathLite.cs`, `MGPolygon.cs`, `MGGridColorPicker.cs`), celles qui rendent
  `null` et que le compilateur fera suivre (`Docking/Controls/MGDockDropIndicators.cs`, `MGDockPreviewOverlay.cs`),
  `Clipping/ElementClipRequest.cs` (retiré ou utilisé) ; tests : les trois fichiers d'empreintes,
  `Animation/TransformedClipTests.cs`, `Architecture/GridColorPickerSwatchClipTests.cs`, les doublures qui notent le
  nom d'un clip ; `MGUI/Docs/rendering-architecture.md`, `MGUI/Docs/decisions/0035-*.md` et son index.
- Étapes :
  1. Avant le changement, écrire un test qui normalise un journal : un `PushClipTemporary/1` d'une définition
     rectangulaire **posée par les crochets de clip d'un élément** (nom en `.Self`, `.Contents` ou `.Viewport`)
     devient la ligne `PushRectangleClip/2` de mêmes bornes. Les autres `PushClipTemporary` restent tels quels, en
     particulier le clip des pastilles que `MGGridColorPicker.cs:883-886` pousse lui-même. Garder les 37 journaux
     normalisés.
  2. Faire rendre aux crochets soit un rectangle, soit une définition, sans objet pour le rectangle ; `Draw` appelle
     `PushRectangleClip` pour le premier. Les crochets sont internes : aucune signature publique ne change, sauf
     `CreateRectangleClipDefinition` (`protected`), gardée si un appelant externe à `MGUI.Core` existe dans les deux
     dépôts, retirée sinon.
  3. Vérifier que les 37 journaux d'après sont égaux aux journaux normalisés d'avant, puis refiger les empreintes.
     Adapter les tests qui cherchent un clip par son nom à ce que le journal porte désormais (bornes), sans perdre
     ce qu'ils prouvent. Écrire l'ADR (D16 : ce qui change dans le journal, ce qui ne change pas à l'écran, sort de
     `CreateRectangleClipDefinition`).
- Validation : journaux normalisés identiques (le test reste) ; mesure et test de zéro d'un défilement continu sur
  500 décalages tous différents (0 au dessin) ; pixels identiques sur les tests GPU existants ; trois solutions
  0 erreur, deux suites vertes.
- Commit : `perf(rendering): push the rectangle clips of an element without a definition` (MGUI), puis le commit du moteur.
- Validation (2026-11-16, seconde machine) : les trois solutions 0 erreur ; `MGUI.Tests` 3945 verts (7 tests ajoutés),
  `CasaEngine.Tests` 6119 verts, aucun sauté. Écrit par un `executor`, relu en session principale. ADR-0035 de MGUI.

  | `MGScrollViewer`, défilement sur 500 décalages tous différents | Draw avant | Draw après |
  |---|---|---|
  | Octets par image | 9888 | 0 |

  - Le chiffre d'avant dépasse les 120 octets estimés : chaque descendant du contenu qui pose un clip (les blocs de
    texte) recevait lui aussi une définition neuve à chaque décalage.
  - Changement : `ElementClipRequest` (interne, sans appelant jusque-là) devient le type rendu par les deux crochets :
    rien, un rectangle, ou une définition ; `MGElement.Draw` pousse le rectangle par `PushRectangleClip(bornes, true)`
    et la définition par `PushClipTemporary`. Les crochets gardent leur nom. Retirés : `CreateRectangleClipDefinition`
    (`protected`, aucun appelant hors de `MGUI.Core` dans les deux dépôts), le cache et l'anneau de seize définitions
    rectangulaires de `MGElement`, le cache du viewport de `MGScrollViewer`. Clips arrondis et à géométrie inchangés.
    `CreateBorderBackedContentsClipDefinition` (`protected`) garde sa signature pour `MGGridColorPicker`, qui pousse
    toujours lui-même son clip `.Swatches` par `PushClipTemporary` ; son cas rectangulaire garde une définition tant
    que bornes et nom ne changent pas (ajouté à la relecture : la première version en créait une par appel).
  - Aucun changement de code dans le moteur : `CasaClipManager` pose déjà le même scissor pour les deux appels.
  - Journaux : les 37 journaux d'après sont égaux aux journaux normalisés d'avant (nombre de lignes et empreinte) ;
    9 n'ont aucun clip d'élément et ne changent pas, 28 sont refigés sur l'empreinte normalisée. Les empreintes figées sont désormais celles des journaux normalisés : un clip rectangulaire d'élément
    qui repasserait par une définition change l'empreinte et fait échouer le journal (corrigé après T5.9 : la
    première note prêtait à `FrozenJournal.Assert` un contrôle dédié qu'il n'a pas). Mutation vérifiée puis
    annulée : pousser le rectangle décalé d'un pixel fait échouer 32 tests, dont les 28 journaux.
  - Pixels : les tests GPU du moteur (`DesktopFrameGpuAllocationTests`, `DrawTransactionRearmGpuTests`) passent sans
    changement des empreintes, sur cette machine seulement.
  - Tests adaptés : `TransformedClipTests` lit le clip en vigueur quand le contenu commence à se dessiner
    (`CurrentClipBounds`) au lieu de chercher le clip nommé `ScrollViewer.Viewport`, mêmes bornes vérifiées ;
    `BorderShapeAdoptionTests` attend le nouveau type des crochets ; deux numéros de ligne de la liste de
    `ResolvedPilotWriteSitesTests` décalés d'une ligne par un `using` ajouté. `GridColorPickerSwatchClipTests` inchangé.
  - Non fait : aucun lancement de `MGUI.Samples` ni de l'éditeur (prévu en T5.10) ; rien rejoué sur la première machine.

### ✅ T5.6 — `FillPolygon` des backends sans copie

- Objectif : dessiner un polygone ou une flèche sur GPU réel n'alloue plus dans les deux backends, selon D17.
- Fichiers : `MGUI/MGUI.MonoGame.Integration/Rendering/DrawTransaction.cs`,
  `CasaEngine/Framework/UI/Backend/MonoGame/Primitives/CasaLegacyShapeRenderer2D.cs` (et `IShapeRenderer2D.cs`,
  `CasaEngine.AposShapes/.../CasaAposShapeRenderer2D.cs` s'ils doivent suivre), appelants de `MGUI.Core` qui passent
  du LINQ à chaque dessin (`MGRatingControl.cs`, `Brushes/BorderBrushes/MGHighlightBorderBrush.cs`,
  `MGDockedBorderBrush.cs`) ; tests GPU des deux dépôts.
- Étapes : mesurer d'abord sur GPU réel un `FillPolygon` d'un tableau de trois sommets dans chaque backend, et
  séparément `PrimitiveDrawing.DrawSolidPolygon` seul ; si ce dernier alloue, l'écrire et ne traiter que ce qui est
  autour (D17). Vérifier au même moment, par un test, que `DrawSolidPolygon` ne modifie pas le tableau reçu et ne le
  garde pas (tableau inchangé après l'appel ; mêmes pixels quand le tableau est réécrit après l'appel et avant la fin
  du lot) : s'il modifie le tableau, un tampon gardé par longueur le remplace ; s'il le garde, ni le tableau de l'appelant ni un
  tampon gardé ne sont sûrs (deux polygones de même longueur dans un lot partageraient le tampon) : la tâche
  s'arrête en ⚠️ avec le fait, comme pour l'allocation (D17). Sinon : test de vide sans `Any()`, tableau passé tel quel quand l'appelant en donne un, tampon gardé
  par longueur quand il donne une liste indexable, copie seulement pour une énumération quelconque ; appelants
  LINQ remplacés par un tampon gardé, mêmes sommets dans le même ordre (`MGRatingControl` passe par
  `StrokeAndFillPolygon` et calcule aussi un minimum et un maximum par lambda, `MGRatingControl.cs:648-658` : le
  même traitement s'y applique). Ces réécritures d'appelants ne sont prouvées que par les journaux figés et les
  pixels ; leur gain n'est mesuré que si une fixture existante les atteint, et la note le dit.
- Validation : 0 octet par `FillPolygon` d'un tableau et d'une liste dans les deux backends (ou le reste dû à
  MonoGame.Extended, écrit) ; pixels identiques sur un triangle, un polygone concave et une flèche ; journaux figés
  inchangés ; trois solutions 0 erreur, deux suites vertes.
- Commit : `perf(rendering): fill a polygon without copying its vertices` (MGUI), puis `perf(ui): fill a polygon without copying its vertices` (moteur).
- Validation (2026-11-16, seconde machine, GPU réel) : les trois solutions 0 erreur ; `MGUI.Tests` 3957 verts (12 tests
  ajoutés), `CasaEngine.Tests` 6124 verts (5 ajoutés), aucun sauté. Écrit par un `executor`, relu en session
  principale. **Le zéro n'est pas atteint** : `PrimitiveDrawing.DrawSolidPolygon` (MonoGame.Extended 6.1.1) alloue
  lui-même à chaque appel ; selon D17 seule la copie autour de lui est retirée.

  Octets par appel, 500 appels après 500 de chauffe, identiques dans les deux backends :

  | Forme | `DrawSolidPolygon` seul | `FillPolygon` avant (tableau ou liste) | `FillPolygon` après |
  |---|---|---|---|
  | Triangle (3 sommets) | 376 | 424 | 376 |
  | Triangle, autre sens | 424 | 472 | 424 |
  | Concave (8 sommets) | 992 | 1080 | 992 |
  | Flèche (7 sommets) | 928 | 1008 | 928 |
  | Flèche, autre sens | 952 | 1032 | 952 |

  - Reste : le triangulateur de MonoGame.Extended crée un tableau d'indices et un nœud par sommet à chaque appel, et
    une copie inversée pour un polygone en sens horaire (lu dans la DLL décompilée par l'exécutant). Le retirer
    demande de remplacer ce dessin de polygone : hors de cette phase (D17). Voir O16.
  - `DrawSolidPolygon` ne modifie pas le tableau reçu et ne le garde pas (test GPU dans les deux dépôts : tableau
    inchangé après l'appel, mêmes pixels quand il est réécrit avant la fin du lot). Un tampon partagé dans un lot est
    donc sûr ; pas d'arrêt.
  - Changement (`DrawTransaction.cs` de MGUI, `CasaLegacyShapeRenderer2D.cs` du moteur) : un tableau est passé tel
    quel ; une `IReadOnlyList<Vector2>` est copiée dans un tampon gardé par longueur (jusqu'à 256 sommets, tableau
    neuf au-delà) ; `Any()` et `ToArray()` seulement pour une autre énumération. Signatures publiques inchangées.
    `IShapeRenderer2D.cs` et `CasaAposShapeRenderer2D.cs` non touchés (Apos délègue au rendu historique ; chemin non
    exécuté).
  - Les tests affirment que `FillPolygon` d'un tableau ou d'une liste n'alloue pas plus que `DrawSolidPolygon` seul,
    sur les cinq formes, dans les deux dépôts.
  - Pixels : comparés dans le test (tableau, liste, liste en lecture seule et énumération contre `DrawSolidPolygon`
    sur un tableau neuf, image différente de la cible vide) pour le triangle, le concave, la flèche et sept polygones
    dans un lot. Empreintes relevées avant et après sur cette machine, égales ; aucune n'est figée dans le code.
  - Appelants réécrits sans LINQ, tampons gardés par thread : `MGRatingControl` (étoile et losange, minimum et maximum
    par boucle ; le triangle passe par `FillTriangle` et garde son LINQ, hors tâche), `MGHighlightBorderBrush`
    (polygone de progression ; le reste de cette branche alloue encore, hors tâche), `MGDockedBorderBrush` (huit
    triangles de coin). Aucun des 37 journaux figés ne les atteint : un test neuf, `FillPolygonCallerJournalTests`,
    fige sur l'ancien code les sommets envoyés par ces trois appelants et les affirme sur le nouveau. Leur gain en
    octets n'est pas mesuré.
  - Mutations vérifiées puis annulées : un sommet décalé dans le tampon gardé fait échouer deux tests GPU ; deux
    sommets échangés dans le triangle de `MGDockedBorderBrush` font échouer son journal.
  - Relecture : le tableau des tampons de `MGRatingControl` est dimensionné par l'énumération des formes et non par le
    nombre de formes à sommets.

### ✅ T5.7 — Cible de dépôt de `MGDockHost` réutilisée

- Objectif : un glissement d'onglet n'alloue plus de `DockDropTarget` à chaque recalcul (80 octets par image), selon D18.
- Fichiers : `MGUI/MGUI.Core/UI/Docking/Controls/MGDockHost.cs`, `Docking/DockDropCalculator.cs`,
  `Docking/DockDropTarget.cs` ; tests `MGUI.Tests/Docking/`, `Architecture/ControlFamilyZeroAllocationTests.cs` ;
  `MGUI/Docs/controls-architecture.md` (section Docking), `MGUI/Docs/decisions/0036-*.md` et son index.
- Étapes : figer avant le changement, pour un scénario de glissement qui passe par chaque sorte de cible (centre,
  quatre zones de séparation, quatre bords de l'hôte, barre de séparation, réordonnancement d'onglet, aucune cible),
  les huit champs de `CurrentDropTarget` image par image et la disposition après le dépôt. Le scénario contient
  aussi, dans cet ordre, les deux suites où un champ écrit après coup pourrait survivre : **barre de séparation à
  côté d'un groupe G du côté S, puis zone S du même groupe G, puis dépôt** ; et **cible de centre ou de
  réordonnancement avec un `TabIndex` calculé, puis cible de centre sans index** (autre groupe, ou `GetDropTarget`
  hors d'un glissement). Ensuite : instance gardée par l'hôte, remplie
  par copie de valeurs ; listes de zones du calculateur gardées et remplies par de nouvelles surcharges internes de
  `DockDropCalculator` (les méthodes publiques statiques `CalculateDropZones` et `CalculateHostEdgeZones`, qu'un test
  appelle, ne changent pas) ; notification selon D18 ; ADR.
- Validation : mêmes huit champs image par image et même disposition après dépôt que le scénario figé ; un test dédié
  survole une barre de séparation, puis la zone S du même groupe, dépose, et affirme `IsSplitterDrop` faux,
  `SplitterNode` nul et la disposition figée avant le changement ; un second affirme que le `TabIndex` d'une cible
  ne se retrouve jamais dans la suivante ; 0 et 0 pendant le
  glissement sur chaque sorte de cible, et le test de phase 2 affirme 0 ; test de la notification (levée au
  changement de valeur, à l'apparition et à la disparition, pas à un recalcul identique) ; journaux figés
  inchangés ; trois solutions 0 erreur, deux suites vertes.
- Commit : `perf(docking)!: keep one drop target during a drag` (MGUI), puis le commit du moteur.
- Validation (2026-11-16, seconde machine) : les trois solutions 0 erreur ; `MGUI.Tests` 3982 verts (25 tests ajoutés),
  `CasaEngine.Tests` 6124 verts, aucun sauté. Écrit par un `executor`, diff relu en session principale. ADR-0036 de MGUI.

  Octets par image pendant un glissement d'onglet, souris alternant entre deux positions de la même cible ; le dessin
  est à 0 avant et après :

  | Cible | Update avant | Update après |
  |---|---|---|
  | Centre d'un autre groupe (cas de la phase 2) | 80 | 0 |
  | Zones de séparation (quatre), bande de proximité, barre de séparation | 576 | 0 |
  | Bords de l'hôte (quatre) | 408 | 0 |
  | Centre du groupe de l'onglet glissé (réordonnancement) | 480 | 0 |
  | Aucune cible | 0 | 0 |

  - Changement : `MGDockHost` garde une instance pour `CurrentDropTarget`, remplie par copie des huit champs ; les
    cibles de centre et de réordonnancement et les listes de zones du calculateur sont gardées et remplies par
    `DockDropTarget.Set` (interne), qui écrit les huit champs comme un objet neuf. Nouvelles surcharges internes
    `CalculateDropZonesInto` et `CalculateHostEdgeZonesInto` ; les méthodes publiques statiques gardent signature et
    résultat. `ExecuteDrop` lit zone, index d'onglet et bord d'hôte une fois, au début.
  - Surface publique : `CurrentDropTarget` n'a plus de setter privé ; elle et la valeur rendue par `GetDropTarget`
    sont une seule instance pour la vie de l'hôte, réécrite à chaque calcul ; `PropertyChanged` est levé à
    l'apparition, à la disparition et au changement d'une valeur, plus à un recalcul identique. Les membres publics de
    `DockDropTarget` ne changent pas.
  - Relevé (deux dépôts, avant le changement) : aucun lecteur hors de `MGDockHost`, du calculateur et des tests de
    MGUI ; aucun gardien ; aucun abonné à `PropertyChanged` pour cette propriété ; le moteur ne touche pas la cible.
  - Élargi dans les fichiers de la tâche, pour tenir « 0 sur chaque sorte de cible » : le cas du réordonnancement
    allouait 400 octets par image par du LINQ dans `CalculateTabIndex` et `CalculateTabReorderPreviewRect`, et non par
    la cible ; ils reçoivent des surcharges internes qui parcourent les onglets dans une liste gardée par l'hôte,
    mêmes résultats (couverts par le rejeu).
  - Comportement : scénario figé sur le code d'avant (12 glissements, 230 lignes, les huit champs copiés en texte à
    chaque image, disposition après chaque dépôt), rejoué à l'identique. Il contient les deux suites du plan : barre
    de séparation puis zone du même groupe puis dépôt (quatre variantes), et index d'onglet calculé puis centre sans
    index. Tests dédiés : champs de barre de séparation jamais hérités, index d'onglet jamais hérité, une seule
    instance, notification (apparition, changement, disparition, rien à l'identique), 14 cas de zéro.
  - Mutations vérifiées puis annulées : ne plus remettre `IsSplitterDrop` et `SplitterNode` fait échouer le rejeu et
    7 tests ; ne plus remettre `TabIndex` fait échouer le rejeu et 2 tests.
  - Le test de phase 2 du glissement affirme maintenant 0 ; journaux de dessin figés inchangés.
  - Restes notés : une notification de changement de valeur crée encore ses arguments d'événement (seulement quand
    une valeur change) ; un glissement dont la souris passe d'un groupe à un autre à chaque image alloue 96 à 128
    octets par image, cause non cherchée par l'exécutant (supposée : réaffichage des indicateurs au changement de
    groupe survolé), notée dans l'ADR-0036. Aucun lancement de `MGUI.Samples` ni de l'éditeur.

### ✅ T5.8 — Géométrie du curseur de `MGTextBox` sans les lignes historiques

- Objectif : un changement de texte dans un `MGTextBox` dont le bloc a une mise en page interne (texte sans balise ou
  à balises) ne refait plus la mise en lignes par `MGTextLine.ParseLines` (environ 1600 octets par touche estimés),
  selon D19. Un `MGRichTextBox` en runs explicites garde le chemin d'aujourd'hui. Dépend de T5.3 seulement pour le chiffre affirmé par le test
  de frappe.
- Fichiers : `MGUI/MGUI.Core/UI/Text/PlainTextLayout.cs`, `FormattedTextLayout.cs`, `TextRenderInfo.cs`,
  `MGUI/MGUI.Core/UI/MGTextBlock.cs` (accès interne aux mises en page), `Text/MGTextCaret.cs` (il lit
  `LineRenderInfo.Source` et ses runs pour choisir le bord d'un caractère : ce qu'il lit à la place est fourni par
  `LineRenderInfo` et écrit dans l'ADR) ; `MGTextBox.cs` seulement s'il lit ce qui change ; tests `MGUI.Tests/Text/` ;
  `MGUI/Docs/text-architecture.md`, `MGUI/Docs/decisions/0037-*.md` et son index.
- Étapes :
  1. Écrire d'abord, sur le code d'avant, les tests de géométrie qui manquent (figés) : pour un corpus de textes de
     zone de saisie (une ligne, plusieurs lignes, retour à la ligne, mot plus long que la ligne avec tiret, lignes
     d'espace, caractères échappés `[` et `\`, texte vide, zone de saisie alignée au centre et à droite, `MGPasswordBox`, `MGRichTextBox` simple, `MGRichTextBox` avec
     une sélection et avec des segments stylés gras et italique), la position, la largeur, l'index d'origine et
     l'index analysé de chaque caractère de `TextRenderInfo` ; et, pour chaque point d'une grille de positions à
     l'écran (moitié gauche et moitié droite de caractères, lignes vides, lignes d'espace), le caractère trouvé et
     la position du curseur après `MGTextCaret.MoveToApproximateScreenPosition` (ligne, index d'origine, index
     analysé, bornes).
  2. Faire porter aux morceaux des mises en page internes l'index d'origine de leur premier caractère et la marque
     d'un caractère inséré ; différentiel : la table reconstruite est égale à `OriginalCharacterIndices` du code
     historique sur le corpus des tests du texte et sur un tirage aléatoire, avec et sans retour à la ligne.
  3. `TextRenderInfo.UpdateLines` lit la mise en page interne quand le bloc en a une, `MGTextBlock.Lines` sinon (runs
     explicites). `MGTextBlock.Lines` reste, pour ce cas et pour ses autres lecteurs. Écrire l'ADR.
- Validation : géométrie et positions du curseur figées identiques, y compris les moitiés droites, les lignes vides et
  le `MGRichTextBox` avec sélection et segments stylés ; différentiel de la table sans écart (sinon ⚠️ avec le premier texte qui
  diffère, D19) ; mesure de la frappe écrite ici, le test de frappe affirme le nouveau reste et le nomme (la nouvelle
  chaîne et ce que le curseur garde) ; journaux figés inchangés ; trois solutions 0 erreur, deux suites vertes.
- Commit : `perf(textbox): compute the caret geometry from the text layouts` (MGUI), puis le commit du moteur.
- Validation (2026-11-16, seconde machine) : les trois solutions 0 erreur ; `MGUI.Tests` 3990 verts (8 tests ajoutés),
  `CasaEngine.Tests` 6124 verts, aucun sauté. Écrit par un `executor`, surface publique relue en session principale ;
  le fond est l'objet de T5.9. ADR-0037 de MGUI. La condition d'arrêt de D19 ne s'est pas déclenchée.

  | `MGTextBox` avec focus, frappe (A puis retour arrière), octets par image (Update) | Avant | Après |
  |---|---|---|
  | Total | 2600 | 356 |

  - Le chiffre d'avant est celui affirmé par le test de frappe après T5.3 ; il n'a pas été remesuré sur l'ancien code.
  - Reste de 356 octets par image, soit 178 000 octets sur 500 images, affirmé par le test de frappe comme une somme
    de parts nommées : géométrie que le curseur garde (200 par changement : une `LineRenderInfo`, une liste, un
    tableau de caractères ; mesurée seule par un test) ; arguments de `TextChanged` (32 par changement) ; arguments du
    changement de bornes de layout (48 par changement) ; `EscapeMarkdown("a")` (128, à la touche A seulement ; mesuré
    seul par un test) ; la chaîne « a » (24, à la touche A). Les parts de 32, 48 et 24 sont attribuées par
    échantillonnage des allocations par type, pas mesurées une à une ; le total exact est le garde-fou.
  - Différentiel de la table contre `MGTextLine.OriginalCharacterIndices`, sans écart, avec et sans retour à la ligne,
    avec et sans lignes d'un espace ignorées : mise en page simple, 301 textes du corpus (28 896 mises en page) et 700
    textes tirés avec la graine 20261116 ; mise en page à balises, 3 780 paires du corpus existant (181 440 mises en
    page).
  - Géométrie figée sur le code d'avant : 188 scénarios sur deux moteurs de texte (376 textes), avec position, largeur,
    index d'origine et index analysé de chaque caractère, grille de points à l'écran et curseur après
    `MoveToApproximateScreenPosition`, recherches par index, flèches. Rejouée à l'identique. Les 88 scénarios de
    mise en page simple ont été figés en remettant `TextRenderInfo.cs` et `MGTextCaret.cs` à leur contenu d'avant,
    puis restaurés à l'octet.
  - Changement : les morceaux des deux mises en page portent l'index d'origine de leur premier caractère et la marque
    d'un caractère inséré ; `TextRenderInfo.UpdateLines` lit la mise en page interne quand le bloc en a une,
    `MGTextBlock.Lines` sinon. `MGTextCaret` ne lit plus `LineRenderInfo.Source` (interne, retiré) mais deux drapeaux
    de `LineRenderInfo` : ligne sans source, ligne sans caractère de texte. Aucun membre public changé.
  - Écart avec le texte du plan, constaté dans le code : un `MGRichTextBox` avec une sélection seule n'est pas en runs
    explicites (la sélection est écrite en balises, comme pour `MGTextBox`) ; seuls les segments stylés et un
    colorateur le sont. Les deux chemins sont figés.
  - Mutations vérifiées puis annulées : index d'origine décalé après un tiret (différentiel et géométrie figée
    échouent, dans chaque mise en page) ; curseur toujours au bord gauche (géométrie figée échoue).
  - Journaux de dessin figés inchangés. Non fait : réutiliser les `LineRenderInfo` d'un changement à l'autre (le
    curseur et d'autres lecteurs en gardent des références), écrit dans l'ADR.

### ✅ T5.9 — Vérification indépendante de T5.5 à T5.8

- Objectif : un `verifier` neuf confirme ou réfute : rendu inchangé pour les clips rectangulaires et les polygones
  (pixels sur GPU réel, journaux normalisés), cible de dépôt aux mêmes valeurs et même résultat de dépôt, géométrie
  du curseur identique, zéros annoncés, aucune régression des deux suites.
- Sources : diff de T5.5 à T5.8, tests et scénarios figés. Une tâche restée en ⚠️ est exclue de la revendication et
  nommée.
- Validation : verdict CONFIRMED, ou constats traités selon leur priorité et revérifiés une fois.
- Commit : `docs(ui): record the verification of the follow-up of the MGUI zero allocation chantier` (moteur, ce plan).
- Validation (2026-11-16, seconde machine) : `verifier` neuf (opus), verdict **CONFIRMED** sur T5.5 à T5.8, aucun
  constat P0 à P2. Aucun changement de code.
  - Méthode du vérificateur : ancien arbre de MGUI (`d0efd9b4`) et nouvel arbre (`d37712ff`) extraits hors du dépôt,
    fichiers de test neufs posés sur l'ancien. Sur l'ancien code, 114 tests copiés sur 114 passent : les scénarios
    figés de T5.6 (sommets des appelants), de T5.7 (230 lignes) et de T5.8 (376 textes) viennent donc bien de
    l'ancien code, et pour T5.5 les 37 journaux de l'ancien code, une fois normalisés, ont le nombre de lignes et
    l'empreinte figés aujourd'hui.
  - T5.5 : même scissor dans les deux backends pour les deux appels (lu dans `ClipStrategyResolver`, `ClipManager`,
    `CasaClipManager`) ; le normaliseur ne retire que le nom de débogage ; plus aucune référence à
    `CreateRectangleClipDefinition` ; `TransformedClipTests` réécrit passe aussi sur l'ancien code.
  - T5.6 : tests GPU des deux dépôts passés sur GPU réel ; tampon gardé propre à chaque transaction ou rendu, rempli
    avant le début du lot ; tampons par thread des appelants lus pendant l'appel seulement.
  - T5.7 : sonde du vérificateur sur sept glissements qui changent de cible ou de groupe : mêmes valeurs de cible
    sur l'ancien et le nouveau code, Update de 80 à 608 octets par image avant et 0 après, sauf un cas (bord haut de
    l'hôte à cheval sur deux groupes : 608 avant, 32 après). Le reste d'un glissement qui change de groupe existait
    donc avant et n'est pas introduit ; le cas de 96 à 128 octets de l'exécutant n'a pas été reproduit à
    l'identique.
  - T5.8 : différentiel du vérificateur sur 1126 textes de plus (563 scénarios sur les deux moteurs : espaces de
    tête et de fin, chaînes de tirets, retours chariot, tabulations, balises, 160 textes aléatoires, alignements,
    avec et sans retour à la ligne) : géométrie du curseur identique à l'octet entre l'ancien et le nouveau code.
    L'arithmétique du reste de frappe tombe juste : (2 × (200 + 32 + 48) + 128 + 24) / 2 = 356.
  - Avis P4 reportés : (A1) `CreateBorderBackedContentsClipDefinition` ne garde qu'une définition rectangulaire,
    l'ancien code en gardait dix-sept : un `MGGridColorPicker` à coins arrondis dont le rayon intérieur est nul et
    qui alterne entre quelques positions crée une définition à chaque changement de bornes ; rendu identique, non
    mesuré. (A2) `TransformedClipTests` ne vérifie plus quel élément a posé le clip, seulement ses bornes. (A3) note
    de T5.5 sur `FrozenJournal.Assert`, corrigée. (A4) chemin Apos de `FillPolygon` lu et non exécuté ; parts de 32
    et 48 octets du reste de frappe non mesurées une à une.
  - Suites rejouées par le vérificateur : trois solutions 0 erreur ; `MGUI.Tests` 3990 verts, `CasaEngine.Tests` 6124
    verts, aucun sauté, tests GPU compris.
  - Non vérifié : `MGUI.Samples` et l'éditeur lancés à la main ; le chiffre de frappe d'avant (2600) non remesuré sur
    l'ancien code ; rien rejoué sur la première machine.

### 🚧 T5.10 — Clôture de la phase 5

- Objectif : tableau final mis à jour (restes de la molette, du clic, de la frappe, du glissement, du défilement, des
  polygones), statut de T2.1, T2.2 et T2.4 revu selon ce que T5.2, T5.5 et T5.7 ont retiré, docs de MGUI à jour
  (`input-architecture.md`, `controls-architecture.md`, `rendering-architecture.md`, `text-architecture.md`), ligne
  de `ai-agent/README.md`, `MGUI.Samples` lancé une fois, validation globale, rapport de fin (`AGENTS.md` §12).
- Validation : section « Validation globale ».
- Commit : `docs(ai-agent): close the follow-up decisions of the MGUI zero allocation chantier`

---

## Reprise sur une autre machine

Écrit le 2026-11-16 à la demande de l'auteur, pour continuer ce plan depuis une autre machine ou une autre session.
Tout le travail est commité et fusionné ; rien d'autre que les branches principales n'est nécessaire.

État au moment de l'arrêt :

- À la demande de l'auteur (2026-11-16), les branches du chantier ont été fusionnées en avance rapide dans les
  branches principales : `chantier/mgui-nonalloc-suite` du moteur dans `main`, et celle de MGUI dans `develop`. Le
  moteur référence la tête de `develop` de MGUI. La fusion a été faite avant la vérification indépendante de
  l'entrée (T5.4) : T5.1 à T5.3 sont donc dans `main` et `develop` sur la foi de leurs propres tests.
- **La suite du plan se fait sur une nouvelle branche**, créée depuis `main` dans le moteur et depuis `develop` dans
  MGUI : `chantier/mgui-nonalloc-phase5` dans les deux dépôts. Cela remplace, pour T5.4 à T5.10, le nom de branche
  écrit dans « Règles d'exécution pour l'agent ». Ne jamais committer sur `main` ni sur `develop`.
- Faites : phases 0 à 4, puis T5.1, T5.2 et T5.3. Prochaine tâche : **T5.4** (vérification indépendante de l'entrée),
  puis T5.5 à T5.10 dans l'ordre du plan. Aucune tâche n'est en 🚧.
- Mise à jour du 2026-11-16, seconde machine : branches `chantier/mgui-nonalloc-phase5` créées dans les deux dépôts,
  T5.4 faite (CONFIRMED). Les 8 tests GPU en échec sur cette machine avant tout changement sont corrigés (O15).
  T5.5 à T5.9 faites (T5.9 : CONFIRMED). Prochaine tâche : **T5.10**.
- Référence des suites à la fusion : `MGUI.Tests` 3938 tests verts, `CasaEngine.Tests` 5556, aucun sauté ; les trois
  solutions à 0 erreur.

Pour pousser (à faire par l'auteur, MGUI d'abord, sinon le moteur référence un commit absent du dépôt distant) :

```bash
git -C MGUI push origin develop
```

```bash
git push origin main
```

Pour récupérer sur l'autre machine, à la racine du dépôt du moteur, puis créer les branches de la suite :

```bash
git checkout main
```

```bash
git pull --ff-only origin main
```

```bash
git submodule update --init
```

```bash
git -C MGUI checkout develop
```

```bash
git -C MGUI pull --ff-only origin develop
```

```bash
git checkout -b chantier/mgui-nonalloc-phase5
```

```bash
git -C MGUI checkout -b chantier/mgui-nonalloc-phase5
```

`git submodule update` laisse MGUI en tête détachée sur le commit que le moteur référence ; les commandes suivantes
le remettent sur `develop`, puis sur la branche de la suite, nécessaire pour y commiter. Vérifier que le commit de
MGUI référencé par le moteur (`git ls-tree HEAD MGUI`) est la tête de `develop` de MGUI et que `git status` est
propre dans les deux dépôts. Si l'autre machine a des modifications locales non commitées, les signaler à l'auteur
avant tout `checkout`, sans les écraser.

Ce qui n'est pas dans le dépôt et que l'autre machine doit avoir :

- Le SDK .NET et une carte graphique : les tests `GpuFact` sont sautés sans GPU, et une suite avec des tests sautés
  ne vaut pas validation (section « Validation globale »).
- Les rôles génériques de sous-agents (`scout`, `executor`, `mech-executor`, `verifier`, `plan-verifier`) et la
  politique d'orchestration : ils sont installés par machine dans `~/.claude/` (pilotfish), pas versionnés ici. Sans
  eux, les vérifications indépendantes de T5.4 et de T5.9 se font avec un sous-agent générique en lecture et
  exécution seules, modèle opus, sur la revendication écrite dans la tâche.
- Les index locaux (`.codegraph/`, `.serena/`) se reconstruisent (`codegraph init`) ; ils ne sont pas requis.

Règles de travail apprises pendant ce chantier, que la mémoire locale de l'agent ne transporte pas :

- Préférence de l'auteur (2026-11-15) : un design moderne et performant ; casser l'API et retirer une
  fonctionnalité obsolète plutôt que garder une compatibilité, sans alias `[Obsolete]`.
- `git commit` doit être précédé de `cd D:/...` vers le dépôt : le hook de commit ne résout pas les chemins `/d/...`.
  Ne jamais lancer `dotnet test --no-build` (binaires de test périmés constatés).
- Dans MGUI, un nouvel ADR s'indexe par le chemin que `git status` affiche (`docs/...` ou `Docs/...` selon le cas) ;
  vérifier ensuite par `git ls-files` qu'il est bien dans le commit.
- `sed -i` du shell Bash retire les fins de ligne CRLF : vérifier par `file <chemin>` après toute édition en ligne de
  commande. Un script contenant des apostrophes ne passe pas en heredoc : l'écrire dans un fichier puis le lancer.
- Ne jamais indexer les fichiers de l'auteur : `.serena/project.yml`, `.codegraph/`, `.serena/memories/`,
  `Projects/SampleProject/.casaeditor/viewport.editor.json`.
- Méthode qui a tenu pour chaque tâche : figer le comportement sur le code d'avant (journal des appels de dessin,
  scénario image par image, différentiel contre le code historique), changer, rejouer à l'identique, puis mesurer
  0 octet sur 500 images après 500 de chauffe ; une mutation volontaire vérifie que le test peut échouer.

Consigne à donner à la session qui reprend :

> Reprends le plan `ai-agent/tasks/mgui-nonalloc-suite-tasks.md` à la tâche T5.4, en suivant ses règles d'exécution et
> sa section « Reprise sur une autre machine » : crée d'abord la branche `chantier/mgui-nonalloc-phase5` depuis `main`
> dans le moteur et depuis `develop` dans MGUI. L'auteur a approuvé la phase 5 le 2026-11-16 et choisi le mode AUTO :
> avance tâche par tâche dans le périmètre approuvé, un commit par tâche dans chaque dépôt, sans pousser ni
> fusionner ; arrête-toi sur tout blocage du plan (⚠️) et à la fin de T5.10.

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Tranché en T1.1 (ADR-0031 de MGUI) : réglages dérivés gardés par la transaction. Limite : une valeur nouvelle à chaque image dérive encore un `DrawSettings`. | T1.3 |
| O2 | Résolu en T1.5 : `DrawClipGeometry` n'alloue pas. | T1.5 |
| O3 | Résolu en T0.1 : les 32 octets sont pris une seule fois, à la première image après le changement de texte, puis 0. | T0.1 |
| O4 | Résolu en T0.1 : 0 octet sur le banc ; le chiffre de la clôture précédente ne se reproduit pas. | T0.1 |
| O5 | Résolu en T3.2 : oui, le moteur d'animation redémarre une instance. | T3.2 |
| O6 | Résolu en T1.6 : aucun appelant ne garde deux transactions ouvertes. | T1.6 |
| O7 | **Décidé le 2026-11-16 (D13), traité en T5.1.** L'événement de molette (`BaseMouseScrolledEventArgs`) est créé à chaque image où la molette bouge (56 octets, `MouseTracker.cs:409`). Le réutiliser, comme le commit `ba19985d` l'a fait pour l'événement de déplacement, change ce qu'un gestionnaire peut garder : l'instance ne vaut que pendant l'événement. Recommandation de l'agent : oui, même contrat que le déplacement. | T2.4 |
| O8 | **Décidé le 2026-11-16 (D16), traité en T5.5.** Un contenu défilé pose deux clips rectangulaires par image à des bornes nouvelles ; chaque borne nouvelle crée une `ClipDefinition` (environ 60 octets). Deux voies : passer ces clips par `PushRectangleClip` (pas de définition, mais le journal des appels de dessin change : `PushRectangleClip` au lieu de `PushClipTemporary`), ou laisser comme limite. Recommandation de l'agent : `PushRectangleClip` pour les clips rectangulaires des éléments. | T2.4 |
| O9 | **Décidé le 2026-11-16 (D17), traité en T5.6.** `FillPolygon` des deux backends réels copie les sommets à chaque appel (`Any()` puis `ToArray()`), donc une flèche ou un polygone dessiné alloue encore sur GPU réel, ce que le banc des contrôles (transaction de test) ne voit pas. Hors des fichiers des tâches de la phase 2. Recommandation de l'agent : une tâche de suite sur les deux backends (tampon gardé, lecture par `IReadOnlyList`). | T2.6 |
| O10 | **Décidé le 2026-11-16 (D13 et D14), traité en T5.2.** Tout clic crée dans `MGUI.Shared/Input/Mouse` les événements d'appui, de relâchement, de clic, de début et de fin de glissement, une séquence de clics et ses métadonnées (464 octets par clic mesurés). Les réutiliser, comme pour le déplacement (`ba19985d`), change ce qu'un gestionnaire peut garder. Recommandation de l'agent : même contrat que le déplacement et la molette (O7), en une tâche de suite sur l'entrée. | T2.1 |
| O11 | **Décidé le 2026-11-16 (D18), traité en T5.7.** Pendant un glissement d'onglet, `MGDockHost` crée un `DockDropTarget` à chaque recalcul de la cible (80 octets par image). `CurrentDropTarget` est publique et notifie quand la référence change. Deux voies : réutiliser une instance et ne notifier que quand une valeur change (changement de comportement observable), ou laisser comme limite. Recommandation de l'agent : réutiliser et notifier sur changement de valeur. | T2.2 |
| O12 | **Décidé le 2026-11-16 (D13 et D15), traité en T5.1 et T5.3.** Toute touche crée dans `MGUI.Shared/Input` les événements d'appui, de relâchement et de clic, un flux d'entrée et ses métadonnées, et `KeyboardTracker.IsAnyPressed(params Keys[])` crée un tableau et un délégué à chaque lecture d'un modificateur (696 octets par touche mesurés). Même question que O7 et O10 pour le clavier. Recommandation de l'agent : une tâche de suite sur l'entrée (souris et clavier). | T2.9 |
| O13 | **Décidé le 2026-11-16 (D19), traité en T5.8.** La géométrie du curseur de `MGTextBox` relit `MGTextBlock.Lines` à chaque changement de texte, donc refait la mise en lignes par le code historique (environ 1600 octets par touche). Le remplacer demande que les mises en page internes du bloc donnent la table des index de caractères d'origine : décision d'architecture du texte. Recommandation de l'agent : une tâche de suite, après la phase 3. | T2.9 |
| O14 | **Validé par l'auteur le 2026-11-16 (D20) : comportement livré gardé.** Conséquence de D9 allant au-delà de « devient du texte » : une balise d'image dont la taille est en chiffres non ASCII (`[img=Icon ١٦x١٦]`) était du texte littéral et devient une image nommée par toute la valeur, sans taille (ADR-0033 de MGUI). Autre voie : refuser une telle balise pour qu'elle reste du texte, ce qui demande une règle de plus dans les deux chaînes. Recommandation de l'agent : garder le comportement livré, cohérent avec la règle des noms. | T3.4 |
| O15 | **Répondu par l'auteur le 2026-11-16 : « corrige les 8 échecs GPU puis enchaîne avec T5.5 ».** Corrigé hors des tâches du plan, dans les tests seulement (commit `fix(tests): make the GPU reference frames and the cube level probe hold on a second GPU`) : les empreintes de l'image de référence du bureau sont dans `CasaEngine.Tests/UI/Backend/ReferenceDesktopFramePixels.cs`, une entrée par machine connue (la première, et l'AMD Radeon RX 9060 XT de la seconde, où le moteur et le `MainRenderer` de MGUI donnent les mêmes empreintes) ; une machine sans entrée échoue avec les empreintes à ajouter. Le test `OpenGlChain_IgnoresTheRequestedCubeLevel_SoTheAtlasIsUsed` fixe les deux comportements mesurés : niveaux gardés et niveau demandé ignoré (première machine), ou niveaux écrits à la main perdus, chaque niveau relu rendant la couleur du niveau 0 (seconde machine) ; le moteur ne crée aucun cube à mips. À rejouer sur la première machine avant la fusion (non vérifiable ici). Question d'origine : Sur la seconde machine, 8 tests GPU de `CasaEngine.Tests` échouent avant tout changement (7 empreintes de pixels prises sur la première machine, 1 couleur d'IBL ; détail sous T5.4). La validation de T5.5 à T5.10 demande « deux suites vertes » et, pour T5.5 et T5.6, « pixels identiques sur les tests GPU existants » : elle ne peut pas être tenue à la lettre ici. Voies : (a) accepter comme référence de cette machine « les mêmes 8 échecs, rien d'autre » et prouver les pixels par comparaison avant et après sur cette machine ; (b) refiger les empreintes pour qu'elles ne dépendent plus de la machine (tâche hors plan) ; (c) faire T5.5 à T5.10 sur la première machine. Recommandation de l'agent : (a), avec les suites rejouées sur la première machine avant la fusion. | T5.5 à T5.10 |
| O16 | **Ouvert le 2026-11-16, suite possible, hors de cette phase (D17).** `PrimitiveDrawing.DrawSolidPolygon` de MonoGame.Extended 6.1.1 alloue à chaque polygone (376 octets pour un triangle, 928 à 992 pour sept ou huit sommets, mesuré sur GPU réel). Amener le dessin d'un polygone à zéro demande de trianguler sans allocation dans les backends, donc de ne plus passer par MonoGame.Extended pour ce dessin. À décider par l'auteur. | T5.6 |

## Hors périmètre

- Amener à zéro le chemin de clip à masque (D6), le changement de police, les caches au-delà de 128 valeurs, le
  rétrécissement des tampons (D12).
- Corriger ou retirer le code historique du texte (D10) : lecture quadratique sur une suite de barres obliques
  inverses, copie du reste du texte par jeton.
- Les opérations ponctuelles des contrôles (ouvrir, fermer, ajouter, trier, changer d'onglet) et le remplissage des
  listes (LINQ de `MGListBox` et `MGListView` hors `Update` et `Draw`).
- `MGUI.Editor`, `CasaEngine.Editor` et les contrôles d'éditeur du moteur, sauf pour compiler avec les nouvelles
  poignées.
- Toute fusion dans `main` ou `develop` et tout `push`.
