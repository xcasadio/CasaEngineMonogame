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

### ⏳ T3.2 — Révélation par `TextCharactersPerSecond`

- Fichiers : `MGTextBlock.cs` (`SyncTextProgressAnimation`, `TextRevealRun`).
- Étapes : garder un `TextRevealRun` et le reconfigurer au lieu de le recréer, si le moteur d'animation accepte de
  redémarrer une instance (lire `UIPropertyAnimation` et `Animations.Start`) ; sinon ⚠️ avec le fait.
- Validation : 0 octet par image pendant la révélation, et par redémarrage après un changement de texte ; les tests
  de révélation existants verts.
- Commit (MGUI) : `perf(text): restart a text reveal without allocating`

### ⏳ T3.3 — Valeur de balise refusée gardée en cache (D11)

- Fichiers : `Text/FormattedTextScanner.cs`.
- Validation : un texte avec `[color=NotAColor]` changé à chaque image alloue 0 octet après la première lecture ;
  mêmes runs que le code historique.
- Commit (MGUI) : `perf(text): remember a rejected tag value`

### ⏳ T3.4 — Grammaire des balises en ASCII (D9)

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

---

## Phase 4 — Clôture

### ⏳ T4.1 — Documentation, validation globale, rapport

- Objectif : docs à jour (`MGUI/Docs/drawing-architecture.md`, `controls-architecture.md`, `text-architecture.md`,
  ADR), tableau final des mesures dans ce plan, limites restantes listées, validation globale, rapport de fin
  (`AGENTS.md` §12), ligne de `ai-agent/README.md` mise à jour. Le plan passe dans `tasks/archive/` après acceptation
  par l'auteur.
- Validation : section « Validation globale ».
- Commit : `docs(ai-agent): close the MGUI zero allocation follow-up`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Tranché en T1.1 (ADR-0031 de MGUI) : réglages dérivés gardés par la transaction. Limite : une valeur nouvelle à chaque image dérive encore un `DrawSettings`. | T1.3 |
| O2 | Résolu en T1.5 : `DrawClipGeometry` n'alloue pas. | T1.5 |
| O3 | Résolu en T0.1 : les 32 octets sont pris une seule fois, à la première image après le changement de texte, puis 0. | T0.1 |
| O4 | Résolu en T0.1 : 0 octet sur le banc ; le chiffre de la clôture précédente ne se reproduit pas. | T0.1 |
| O5 | Le moteur d'animation accepte-t-il de redémarrer une instance d'animation ? Non lu. | T3.2 |
| O6 | Résolu en T1.6 : aucun appelant ne garde deux transactions ouvertes. | T1.6 |
| O7 | **Décision de l'auteur.** L'événement de molette (`BaseMouseScrolledEventArgs`) est créé à chaque image où la molette bouge (56 octets, `MouseTracker.cs:409`). Le réutiliser, comme le commit `ba19985d` l'a fait pour l'événement de déplacement, change ce qu'un gestionnaire peut garder : l'instance ne vaut que pendant l'événement. Recommandation de l'agent : oui, même contrat que le déplacement. | T2.4 |
| O8 | **Décision de l'auteur.** Un contenu défilé pose deux clips rectangulaires par image à des bornes nouvelles ; chaque borne nouvelle crée une `ClipDefinition` (environ 60 octets). Deux voies : passer ces clips par `PushRectangleClip` (pas de définition, mais le journal des appels de dessin change : `PushRectangleClip` au lieu de `PushClipTemporary`), ou laisser comme limite. Recommandation de l'agent : `PushRectangleClip` pour les clips rectangulaires des éléments. | T2.4 |
| O9 | **Décision de l'auteur.** `FillPolygon` des deux backends réels copie les sommets à chaque appel (`Any()` puis `ToArray()`), donc une flèche ou un polygone dessiné alloue encore sur GPU réel, ce que le banc des contrôles (transaction de test) ne voit pas. Hors des fichiers des tâches de la phase 2. Recommandation de l'agent : une tâche de suite sur les deux backends (tampon gardé, lecture par `IReadOnlyList`). | T2.6 |
| O10 | **Décision de l'auteur.** Tout clic crée dans `MGUI.Shared/Input/Mouse` les événements d'appui, de relâchement, de clic, de début et de fin de glissement, une séquence de clics et ses métadonnées (464 octets par clic mesurés). Les réutiliser, comme pour le déplacement (`ba19985d`), change ce qu'un gestionnaire peut garder. Recommandation de l'agent : même contrat que le déplacement et la molette (O7), en une tâche de suite sur l'entrée. | T2.1 |
| O11 | **Décision de l'auteur.** Pendant un glissement d'onglet, `MGDockHost` crée un `DockDropTarget` à chaque recalcul de la cible (80 octets par image). `CurrentDropTarget` est publique et notifie quand la référence change. Deux voies : réutiliser une instance et ne notifier que quand une valeur change (changement de comportement observable), ou laisser comme limite. Recommandation de l'agent : réutiliser et notifier sur changement de valeur. | T2.2 |
| O12 | **Décision de l'auteur.** Toute touche crée dans `MGUI.Shared/Input` les événements d'appui, de relâchement et de clic, un flux d'entrée et ses métadonnées, et `KeyboardTracker.IsAnyPressed(params Keys[])` crée un tableau et un délégué à chaque lecture d'un modificateur (696 octets par touche mesurés). Même question que O7 et O10 pour le clavier. Recommandation de l'agent : une tâche de suite sur l'entrée (souris et clavier). | T2.9 |
| O13 | **Décision de l'auteur.** La géométrie du curseur de `MGTextBox` relit `MGTextBlock.Lines` à chaque changement de texte, donc refait la mise en lignes par le code historique (environ 1600 octets par touche). Le remplacer demande que les mises en page internes du bloc donnent la table des index de caractères d'origine : décision d'architecture du texte. Recommandation de l'agent : une tâche de suite, après la phase 3. | T2.9 |

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
