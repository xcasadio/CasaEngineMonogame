# Plan agent IA — Écrans liés : les écrans d'un jeu en assets, liés à des view-models

Plan d'exécution du programme décidé avec l'auteur du 2026-09-22 au 2026-09-24, après que le portage Alundra a
montré que ses écrans ne pouvaient pas s'ouvrir dans l'éditeur. Il couvre trois dépôts : MGUI (sous-module),
le moteur (exécution et éditeur), et le dépôt parent, consommateur, dont les tranches sont dans
[`docs/plan-bound-screens.md`](../../../docs/plan-bound-screens.md). Décisions : MGUI ADR-0016, moteur
[ADR-0038](../../docs/decisions/0038-game-screens-are-assets-bound-to-view-models.md), parent ADR-0002.
Les décisions D1 → D12 ci-dessous ont été arbitrées avec l'auteur : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Chaque écran d'un jeu devient un asset du projet (`.uiscreen` et `.xaml`), ouvrable, prévisualisable avec des
données de conception, et enregistrable sans perte dans l'éditeur. Il se lie à un view-model observable. Ses
images portent leur source dans le balisage : un sprite, ou une animation 2D du moteur jouée par l'horloge de
l'interface. Le binding de MGUI n'alloue plus rien par mise à jour. L'écran de dialogue du moteur devient
remplaçable par un asset du projet. Alundra en est le premier consommateur : inventaire, HUD réécrit en XAML,
boîte de dialogue.

## Enveloppe du programme

| Tranche | Dépôt | Résultat | Prérequis |
|---|---|---|---|
| Phase 1 — T1.1, T1.2 | MGUI | Binding typé sans allocation ; `Canvas.Left/Top` liables | — |
| Phase 2 — T2.1 à T2.3 | MGUI, puis référence dans le moteur | Noms d'image résolus par l'hôte ; images animées ; sample et doc MGUI | Phase 1 |
| Phase 3 — T3.1 à T3.3 | Moteur (exécution) | Résolution des sprites et des `.anim2d` ; écrans acquis par handle ; champ `design_time_data_file` | Phase 2 |
| Phase 4 — T4.1 à T4.3 | Moteur (éditeur) | Aller-retour sans perte ; aperçu lié ; choix d'image dans l'inspecteur | Phase 3 (T4.1 n'a pas de prérequis) |
| Phase 5 — B1 à B3 | Parent | Écrans d'Alundra en assets versionnés ; inventaire et HUD liés et animés | Phases 1 à 4 |
| Phase 6 — T6.1, puis B4 | Moteur, puis parent | Écran de dialogue remplaçable ; celui d'Alundra en asset | Phase 5 |
| Phase 7 — T7.1 | Moteur | Documentation et clôture | Tout le reste |

- **Ordre** : les phases dans l'ordre, une tâche à la fois. T4.1 peut être avancée si une autre phase est bloquée.
- **Vérification** : relecture du plan par un `plan-verifier` avant approbation (enveloppe et première tranche).
  Après exécution, un `verifier` frais à chaque frontière d'intégration : fin de la phase 2 (API MGUI), fin de la
  phase 3, fin de la phase 4, fin de chaque tranche du parent, fin de T6.1.
- **Retour arrière** : chaque tâche est un commit sur une branche de chantier ; rien n'est mergé ni poussé sans
  l'auteur. Revenir sur une tranche = revenir sur ses commits.
- **Arrêts** : une information manquante, une contradiction ou un défaut préexistant qui touche la tâche mettent la
  tâche en ⚠️ Blocked, avec la question dans « Points ouverts ».

## État vérifié du dépôt (2026-09-24)

**Git**
- Moteur : `main` à `c07560e6` (merge de `chantier/generated-cache-stale-handle`). Branche du chantier
  `chantier/bound-screens` créée depuis ce commit. Modification préexistante de l'auteur :
  `CasaEngine.Launcher/Program.cs` — ne jamais l'indexer.
- MGUI : `develop` à `6ac7a68`. Branche `chantier/bound-screens` créée depuis ce commit.
- Parent : `main` à `084d3e8`, branche `chantier/bound-screens`.

**MGUI**
- `IUIAssetProvider` n'a que `LoadImage` et `TryLoadImage` (`MGUI.Shared/Assets/IUIAssetProvider.cs:3-6`) ; un test
  d'architecture épingle cette forme (`MGUI.Tests/Architecture/AssetProviderTests.cs:22`).
- `MGUI.Shared` référence MonoGame et `MGUI.Rendering.Abstractions` (`MGUI.Shared/MGUI.Shared.csproj`), donc
  `Rectangle` et `IUIImageResource` y sont visibles ; `MGTextureData` est dans `MGUI.Core` (`MGUI.Core/UI/MGTextureData.cs:11`).
- `MGResources.TryGetTexture` ne consulte jamais le fournisseur (`MGUI.Core/UI/MGResources.cs:203-218`).
- Le setter de `MGImage.SourceName` se désabonne puis se réabonne à `OnTextureAdded`/`OnTextureRemoved` à chaque
  changement (`MGUI.Core/UI/MGImage.cs:53-61`). `MGImage` n'a pas de `UpdateSelf`.
- Chaque élément a un `UpdateSelf(ElementUpdateArgs)` par frame (`MGElement.cs:4052`), avec le temps écoulé en type
  valeur (`RenderLoopArgs.cs:32`) ; un élément replié n'est pas mis à jour (`MGElement.cs:3839-3844, 5930-5936`).
- `Canvas.Left/Top` sont des métadonnées lues par des méthodes statiques (`MGUI.Core/UI/Containers/MGCanvas.cs:43-48`).
- Une mise à jour de binding lit et écrit par réflexion et boxe les types valeur (`MGUI.Core/UI/DataBinding/DataBinding.cs:512, 526`) ;
  le cache des `PropertyInfo` est indexé par instance et jamais vidé (`DataBinding.cs:291-313`) ; `BuildTaggedWriter`
  recrée une fermeture à chaque mise à jour pour les huit propriétés pilotes de l'ADR-0005 (`DataBinding.cs:402-411, 441`).
- Lever `PropertyChanged` n'alloue rien (`MGUI.Shared/Helpers/ViewModelBase.cs:15-25`, arguments mis en cache par nom).
- Un `Rectangle` XAML accepte un `GradientFillBrush` à quatre couleurs de coin (`MGUI.Core/UI/XAML/Controls.cs:2371-2422`).

**Moteur**
- RPGDemo lit ses `.uiscreen` à la main, hors `AssetContentManager` (`Projects/CasaEngine.RPGDemo/Scripts/Screens/RpgDemoScreenAssets.cs:20-34`).
- `UIScreenAsset.Load` lit tous ses champs comme optionnels (`CasaEngine/Framework/UI/MGUI/UIScreenAsset.cs:16-36`) ;
  le sérialiseur est `EditorAssetJsonSerializer.SaveUIScreenAsset` (`CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:471-482`).
- L'analyseur XAML de l'éditeur saute les déclarations d'espace de noms (`CasaEngine.EditorServices/ScreenEditor/Xaml/UIScreenXamlParser.cs:74`),
  ne parcourt que `Elements()` et perd donc les commentaires (`:88`), et tronque le propriétaire d'une propriété
  attachée écrite en élément de propriété (`:98, :115`). Le sérialiseur trie les attributs par nom (`UIScreenXamlSerializer.cs:75, 85`).
- L'aperçu construit la fenêtre sans contexte de données (`ScreenEditor/Preview/UIScreenPreviewBuilder.cs:34, 49`).
- L'éditeur charge la DLL de gameplay du projet à son ouverture (`CasaEngine/Framework/Configuration/Project/ProjectSettingsHelper.cs:38`),
  et `ElementFactory` instancie un type de cette DLL par son nom.
- `Animation2dCompositionSampler.ApplyTracks` parcourt ses pistes en `foreach` à travers une interface
  (`CasaEngine/Framework/Assets/Animations/Animation2dCompositionSampler.cs:163`) ; `Seek` fixe le temps courant sans
  déclencher d'événement (`:38-46`).
- L'écran de dialogue du moteur est embarqué dans `CasaEngine.dll` (`CasaEngine/Framework/Dialogue/UI/DialogueScreen.cs:16-22`).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Une `Image` porte sa source dans le XAML sous forme de chaîne : un GUID d'asset, ou un nom d'asset. L'hôte la résout en texture et rectangle source, à travers le fournisseur d'assets de MGUI (2026-09-22). |
| D2 | L'éditeur conserve les commentaires XML quand il enregistre un écran (2026-09-22). |
| D3 | Le XAML d'un écran de jeu est un asset du projet CasaEngine (`.uiscreen` et `.xaml`), jamais une ressource embarquée dans la DLL du jeu (2026-09-22). |
| D4 | Le HUD d'Alundra est réécrit en XAML (2026-09-22). |
| D5 | Les écrans se lient à des view-models observables, par le binding de MGUI, dans ce programme (2026-09-23). |
| D6 | Les écrans d'interface (HUD, inventaire, dialogues) ne suivent plus le tick logique à 50 Hz ; leurs animations restent proches du timing de l'original (2026-09-23). |
| D7 | MGUI apprend à lier `Canvas.Left/Top` (2026-09-23). |
| D8 | Mises à jour de binding par accesseurs typés compilés une fois par (type, chemin), sans réflexion ni boxing ; les view-models ne notifient que ce qui change (2026-09-24). |
| D9 | L'aperçu de l'éditeur charge un fichier de données de conception déclaré dans le `.uiscreen` (2026-09-23). |
| D10 | `alundra-project/UI/Screens/` est versionné par une exception en cascade dans `.gitignore` ; le convertisseur y enregistre les écrans au catalogue avec des identifiants stables (2026-09-23). |
| D11 | Le curseur de l'inventaire, les pastilles de magie et la pièce du HUD deviennent des animations d'asset `.anim2d`, résolues à la volée par l'hôte derrière une interface de MGUI, jouées par l'horloge de l'interface (2026-09-24). |
| D12 | Le moteur permet à un projet de remplacer l'écran de dialogue par défaut par son propre asset, l'écran embarqué restant le repli ; en fin de programme (2026-09-24). |

## Points à valider (propositions de l'agent)

| Réf | Proposition |
|---|---|
| P1 | Pas de conteneur d'éléments sur un `Canvas` : les 26 cases du HUD et les 24 de l'inventaire sont des emplacements nommés fixes (aucun conteneur MGUI ne pose une collection sur un canevas aujourd'hui). |
| P2 | `SourceName` reste le seul vocabulaire de source, dans le XAML comme dans les view-models ; `MGImage` s'abonne une seule fois aux événements de ses ressources. |
| P3 | Le fournisseur gagne des membres avec implémentation par défaut, placés dans `MGUI.Shared` : les implémentations existantes compilent toujours. Une résolution est mise en cache à la racine des ressources ; l'hôte possède les handles et les rend quand le bureau est libéré. |
| P4 | Une image animée expose aussi un décalage de dessin par frame, pour les animations dont les images ont des pivots différents (le curseur de l'inventaire bouge d'un pixel selon sa phase, `Alundra/Scripts/AlundraInventoryComposer.cs:105-106`). |
| P5 | Trois chemins de mise à jour, et un seul peut allouer ; le chemin est choisi à chaque résolution de la propriété source, puisque le contexte de données est souvent posé après le chargement et peut changer. (a) Cible pilote de l'ADR-0005 : écriture étiquetée typée, sans boxing, qui garde la provenance `LocalBinding`. (b) Autre cible de même type, sans convertisseur ni format : copie typée. (c) Convertisseur, format de chaîne ou conversion de type : l'ancien chemin, qui peut allouer, puisqu'un convertisseur prend et rend des objets et qu'un format construit une chaîne. Les écrans du programme n'utilisent que (a) et (b). |
| P6 | Un écran est acquis par `AssetContentManager` (un chargeur de `.uiscreen`) et tenu par l'écran ; RPGDemo passe par ce chemin. |
| P7 | Les données de conception instancient le vrai type de view-model depuis la DLL chargée (`ElementFactory`), remplies par `JsonConvert.PopulateObject`. Un view-model doit donc avoir un constructeur public sans paramètre. |
| P8 | Le remplacement de l'écran de dialogue se déclare par un champ optionnel des réglages du projet, qui nomme l'asset `.uiscreen` ; les noms d'éléments que l'écran lie forment un contrat documenté. |
| P9 | L'éditeur corrige son aller-retour (T4.1) avant qu'un écran d'Alundra ne soit ouvert dans l'éditeur. |

## Règles d'exécution pour l'agent

- **Branches dédiées `chantier/bound-screens`** : moteur et parent depuis `main`, MGUI depuis `develop`. Ne jamais
  committer sur `main` ni `develop`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la
  validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche,
  puis **créer un commit dédié** qui inclut la mise à jour de ce fichier. Une tâche MGUI se commite dans MGUI ; la
  mise à jour de ce plan se commite dans le moteur avec la référence du sous-module quand elle change.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge reste une décision de l'auteur.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de
  l'auteur, ou d'une doc officielle citée. Sinon : ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build obligatoire** avant ✅ : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln`
  selon le périmètre ; pour MGUI, `dotnet build MGUI/MGUI.Tests/MGUI.Tests.csproj`. **Tests** :
  `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` pour MGUI ; `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj`
  puis `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build` pour le moteur (la solution n'inclut pas le
  projet de tests). Tests en premier plan.
- Code écrit mais vérification visuelle ou manuelle manquante : `🧪 Needs testing`, avec ce qui manque.
- **Ne jamais laisser une tâche en 🚧** en fin de session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ, de closure, de réflexion, de boxing ni d'abonnement d'événement par
  frame dans les chemins chauds (`AGENTS.md` §9.3) ; sérialisation additive (§9.7) ; API publique additive (§9.8).
  MGUI : garder une signature publique qui change sous `[Obsolete]` avec renvoi vers la nouvelle.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `MGUI.Tests` et `CasaEngine.Tests` au moins à la référence (2989 et 1781 au 2026-09-22), plus les tests ajoutés.
- Tests d'allocation : zéro octet mesuré par `GC.GetAllocatedBytesForCurrentThread` autour d'une boucle de mises à
  jour de binding, de changements de `SourceName` et d'avances d'image animée.
- Smoke : les écrans de RPGDemo s'affichent comme avant ; dans l'éditeur, un écran s'ouvre, s'affiche avec ses
  images et ses données de conception, et s'enregistre sans rien perdre (diff vide si rien n'a été modifié).
- Recettes Alundra : dans le plan parent.

---

## Phase 0 — Décisions

### ✅ T0.1 — ADR et plans

- Objectif : enregistrer D1 → D12 et poser le programme.
- Fichiers : `MGUI/Docs/decisions/0016-host-resolved-image-sources-and-allocation-free-binding.md` et son index ;
  `docs/decisions/0038-game-screens-are-assets-bound-to-view-models.md` et son index ; ce plan et le tableau de
  `ai-agent/README.md` ; côté parent, `docs/decisions/0002-…`, l'ADR-0001 passée à « Superseded » et
  `docs/plan-bound-screens.md`.
- Validation : relecture ; plan relu par un `plan-verifier`.
- Commits : `docs(adr): host-resolved and animated image sources, allocation-free binding` (MGUI) ;
  `docs(adr): game screens are assets bound to view models` puis `docs(ai-agent): plan the bound screens program` (moteur).
- **Fait** (2026-09-24) : relu par un `plan-verifier` (enveloppe et T1.1). Deux REVISE, tous deux corrigés :
  - la copie typée aurait court-circuité l'écriture étiquetée des propriétés pilotes (ADR-0005) : trois chemins
    nommés, dont un chemin pilote typé ;
  - le chemin ne peut pas être choisi à la construction, car le contexte de données arrive après le chargement :
    il est choisi à chaque résolution de la propriété source.
  Relecture de clôture : **READY**.

---

## Phase 1 — MGUI : binding sans allocation

### ✅ T1.1 — Accesseurs typés compilés

- Objectif : D8, P5. Une mise à jour de binding ne passe plus par la réflexion et ne boxe plus, sauf le chemin (c)
  de P5 (convertisseur, format de chaîne, conversion de type).
- Fichiers : `MGUI.Core/UI/DataBinding/DataBinding.cs`, un nouveau fichier d'accesseurs dans le même dossier ;
  `MGUI.Core/UI/Styling/UIPilotPropertyResolver.cs` ; `MGUI.Tests` (nouveau fichier de tests d'allocation à côté
  des tests de binding existants).
- Étapes :
  1. Indexer le cache des `PropertyInfo` par type, plus par instance.
  2. Compiler une fois par (type, propriété) un lecteur et un écrivain typés (`Expression.Lambda`), mis en cache par
     type.
  3. Router chaque binding vers l'un des trois chemins de P5. Le côté cible est décidé une fois, à la
     construction : cible pilote ou non (`HasPilotTarget`), écrivain étiqueté et sa source `LocalBinding`. Le chemin
     est choisi à chaque résolution de la propriété source : première résolution, nouvel objet source, nouveau
     contexte de données, ou type déclaré différent (la propriété source est recherchée de nouveau dans ces cas,
     `DataBinding.cs:201-221, 352-375`). Tant qu'aucune source n'est résolue, le chemin (c) s'applique. Les
     accesseurs viennent du cache par type : choisir un chemin ne compile rien de nouveau pour un type déjà vu.
     - (a) cible pilote (`HasPilotTarget`) : `UIPilotPropertyResolver` gagne des surcharges typées de
       `TrySetTagged` pour les trois types valeur de ses cibles, `Thickness` (`Margin`, `Padding`,
       `BorderThickness`), `int?` (`MinHeight`) et `Color?` (emplacements de couleur) ; les cibles de type
       référence (brosses) gardent la surcharge `object`, qui ne boxe pas. L'écrivain étiqueté et sa source de
       résolution `LocalBinding` sont construits une fois par binding ;
     - (b) autre cible, types identiques, sans convertisseur ni format : copie typée source → cible ;
     - (c) sinon : l'ancien chemin, inchangé.
- Validation : tous les tests MGUI verts, dont ceux de l'ADR-0005 ; nouveaux tests :
  - zéro octet alloué sur 1000 mises à jour pour une cible `string`, `bool`, `int`, `float`, une énumération
    (`Visibility`), une structure non pilote, et deux cibles pilotes (`Margin` liée à un `Thickness`, `MinHeight`
    liée à un `int?`) ;
  - une liaison pilote de même type enregistre toujours une contribution `LocalBinding` après le changement ;
  - le même contrôle de zéro allocation avec le contexte de données posé **après** la construction du binding,
    comme le font les samples (`Window.WindowDataContext = this`), sur une cible non pilote et sur `Margin` ;
  - le contexte de données remplacé par un view-model d'un autre type, dont la propriété liée a un autre type : la
    cible reçoit la bonne valeur et rien ne lève ;
  - le cache ne grandit pas quand on lie de nouvelles instances d'un même type ;
  - un binding avec convertisseur ou format donne le même résultat qu'avant (chemin (c)).
- Commit : `perf(binding): push bound values through compiled typed accessors`
- **Fait** (MGUI `5635823`) :
  - `TypedAccessorCache` : cache des `PropertyInfo` par type, lecteurs et écrivains compilés par (type, propriété),
    copie typée par paire ; `DataBinding` choisit son chemin à chaque résolution de la propriété source ;
    l'écrivain étiqueté et sa source `LocalBinding` sont construits une fois, dans le constructeur ;
    `UIPilotPropertyResolver` gagne les surcharges typées `Thickness`, `int?`, `Color?`.
  - Le chemin (b) exige des types déclarés strictement identiques (`int` et `int?` ne le sont pas) ; le sens
    cible → source reste sur l'ancien chemin, hors du périmètre de D8.
  - `MGUI.Tests` 3004/3004 (2989 + 15), reproduit par la session principale ; builds du moteur et de l'éditeur
    sans erreur. Zéro octet mesuré sur 1000 mises à jour pour les huit cibles demandées, contexte de données
    posé avant ou après la construction du binding.
  - **Constat hors de D8 (O3)** : MGUI est compilé avec `UseWPF=true` (`MGUI.Core/MGUI.Core.csproj:9-10`) ; la
    diffusion de `PropertyChanged` passe alors par `PropertyChangedEventManager`, qui alloue environ 192 octets
    par notification (mesure de l'exécuteur, sans binding). Les tests d'allocation mesurent donc la mise à jour
    elle-même en appelant son point d'entrée ; les tests de comportement passent par la vraie diffusion.

### ✅ T1.2 — Coordonnées de canevas liables

- Objectif : D7. `CanvasLeft="{MGBinding Path=X}"` (la syntaxe XAML de MGUI est `CanvasLeft`/`CanvasTop`,
  `MGUI.Samples/Controls/Canvas.xaml:31`) suit le view-model en direct, sans allocation.
- Fichiers : `MGUI.Core/UI/MGElement.cs` (propriétés `CanvasLeft`/`CanvasTop` de type `int?`),
  `MGUI.Core/UI/Containers/MGCanvas.cs` (stockage typé), la correspondance des chemins de binding XAML
  (`MGUI.Core/UI/XAML/Element.cs`) si nécessaire ; tests.
- Étapes :
  1. Stocker les quatre coordonnées de canevas dans des champs typés de l'élément, plus dans
     `Metadata` (`Dictionary<string, object>`, `MGElement.cs:3270`), qui boxe l'entier à chaque écriture ; l'API
     statique `MGCanvas.Get/SetLeft…` garde sa forme et son comportement (seul `MGCanvas` lit ces clés).
  2. Exposer la gauche et le haut comme propriétés notifiantes de type `int?` qui délèguent à ce stockage et
     invalident la mise en page du canevas parent.
  3. Faire aboutir un binding écrit sur `CanvasLeft`/`CanvasTop` à ces propriétés.
- Validation : test : un élément lié se redessine à la nouvelle position quand le view-model change ; zéro octet
  alloué sur 1000 mises à jour d'une source `int?` (chemin (b) de T1.1) ; les écrans et tests qui écrivent
  `CanvasLeft` en littéral ou appellent `MGCanvas.SetLeft` ne changent pas.
- Commit : `feat(canvas): bind canvas coordinates`
- **Fait** (MGUI `bb23edf`) :
  - Les quatre coordonnées sont des champs typés de `MGElement`, exposés par `CanvasLeft`, `CanvasTop`,
    `CanvasRight` et `CanvasBottom` (les deux derniers ajoutés par symétrie : ajout d'API accepté, documenté en
    T2.3). `MGCanvas.Get/Set…` gardent leur forme et renvoient à ces propriétés ; les clés `Metadata` ont disparu.
  - `MGUI.Tests` 3008/3008 (3004 + 4), reproduit par la session principale ; un test existant qui lisait
    `Metadata` par réflexion a été réécrit sur le stockage typé. Zéro octet sur 1000 mises à jour d'une source
    `int?` et sur 1000 écritures directes.
  - **Fait appris** : un binding s'écrit `{dataBinding:MGBinding …}` avec `xmlns:dataBinding` déclaré ; la forme
    sans préfixe lève une `XamlObjectWriterException`. Le commentaire de `MGUI.Core/UI/DataBinding/MGBinding.cs:12-14`,
    qui promet l'inverse, est périmé : la classe est dans `MGUI.Core.UI.DataBinding`. Les écrans du programme
    déclarent donc cet espace de noms, que T4.1 doit préserver.
  - Une fenêtre fraîchement chargée demande deux `Desktop.Update()` pour stabiliser sa mise en page
    (comportement préexistant, déjà suivi par les tests de MGUI).

---

## Phase 2 — MGUI : sources d'image résolues par l'hôte, et animées

### ✅ T2.1 — Noms d'image résolus par l'hôte

- Objectif : D1, P2, P3.
- Fichiers : `MGUI.Shared/Assets/IUIAssetProvider.cs`, `MGUI.Core/UI/MGResources.cs`, `MGUI.Core/UI/MGImage.cs`,
  `MGUI.Tests/Architecture/AssetProviderTests.cs` et un nouveau test.
- Étapes :
  1. Ajouter au fournisseur un membre avec implémentation par défaut qui résout un nom en image et rectangle source.
  2. `TryGetTexture` : si le nom est inconnu dans toute la chaîne, demander au fournisseur et mettre le résultat en
     cache à la racine.
  3. `MGImage` : s'abonner une fois aux événements de texture de ses ressources.
  4. Mettre à jour le test d'architecture sur la nouvelle forme, voulue.
- Validation : tests avec un fournisseur factice : une `Image` qui nomme une source inconnue l'obtient du
  fournisseur, une seule résolution par nom ; zéro octet alloué sur 1000 changements de `SourceName` entre noms déjà
  résolus ; tous les tests MGUI verts.
- Commit : `feat(images): resolve unknown image names through the host`
- **Fait** (MGUI `27550cd`) :
  - `IUIAssetProvider.TryResolveImage(string name, out IUIImageResource image, out Rectangle? sourceRect)`, avec
    une implémentation par défaut qui ne résout rien : `CasaUIAssetProvider` compile sans changement.
  - `MGResources.TryGetTexture` demande au fournisseur à la racine, met le résultat en cache à la racine, et garde
    un cache négatif par racine (vidé si le nom est ajouté explicitement ensuite).
  - `MGImage` s'abonne une seule fois ; deux défauts préexistants corrigés : le gestionnaire comparait le nom de
    l'élément au lieu de celui de la texture ajoutée, et `UpdateActualSource` contournait le setter
    d'`ActualSource` (ni notification ni invalidation de mise en page).
  - `MGUI.Tests` 3012/3012 (3008 + 4), reproduit ; zéro octet sur 1000 changements de `SourceName` entre noms
    résolus ; test d'architecture renommé `IUIAssetProvider_ExposesImageLoadingAndResolutionOnly`.
  - **Limite connue, reportée** : `OnTextureAdded` n'est pas relayé entre portées de ressources ; une image dont la
    résolution a **échoué** n'est pas rafraîchie si la texture est ajoutée plus tard dans une autre portée
    (comportement préexistant). Le cas du programme, une résolution synchrone qui réussit dès l'affectation de
    `SourceName`, n'est pas touché.

### ✅ T2.2 — Images animées

- Objectif : D11, P4.
- Fichiers : `MGUI.Shared/Assets/` (interface d'image animée, membre du fournisseur qui en crée une par nom),
  `MGUI.Core/UI/MGImage.cs` (`UpdateSelf`, propriétés liables de décalage de départ et de lecture), tests.
- Étapes :
  1. Déclarer l'interface : avancer d'un temps écoulé, lire la frame courante (image, rectangle source, décalage de
     dessin), redémarrer à un décalage donné.
  2. `MGImage` : si le nom résout une image animée, en créer une instance propre à l'image ; l'avancer dans
     `UpdateSelf` avec le temps écoulé de la frame ; dessiner la frame courante avec son décalage.
  3. Propriétés liables : décalage de départ ; lecture en cours. L'arrêt ramène à la première frame.
- Validation : tests avec une image animée factice de 4 frames de 200 ms, avancée par pas de 16,67 ms : chaque
  changement de frame tombe à moins d'une frame de k × 200 ms ; deux images au décalage différent restent décalées ;
  une image repliée n'avance pas ; zéro octet alloué par frame.
- Commit : `feat(images): play animated image sources`
- **Fait** (MGUI `a80149e`) :
  - `IUIAnimatedImage : IDisposable` (`Advance`, `Restart(TimeSpan)`, `CurrentImage`, `CurrentSourceRect`,
    `CurrentDrawOffset`) dans `MGUI.Shared/Assets/` ; `IUIAssetProvider.TryCreateAnimatedImage`, avec une
    implémentation par défaut qui ne crée rien.
  - `MGImage` : ordre de résolution `Source` explicite, puis texture (T2.1), puis animation créée par image ;
    l'instance précédente est libérée quand la source change ; `UpdateSelf` avance l'animation ; le dessin applique
    le décalage de la frame ; `AnimationStartOffset` et `IsAnimationPlaying` liables, aussi posables en XAML.
  - `MGUI.Tests` 3020/3020 (3012 + 8), reproduit. Temps piloté par `GraphTestRuntime.ApplyFrame` ; cycle exact sur
    plus de 25 tours de 4 × 200 ms ; deux images décalées de 200 ms restent à une frame d'écart ; zéro octet par
    frame, mesuré sur `UpdateSelf` appelé directement ; décalage de dessin vérifié sur l'enregistreur de dessin.
  - Limite reportée : l'ajout d'une texture statique de même nom relance la résolution et remplace l'animation,
    conformément à l'ordre (le statique gagne).

### 🧪 T2.3 — Sample, documentation MGUI et référence dans le moteur

- Objectif : démontrer et documenter les fonctionnalités ajoutées (règles de MGUI : toute nouvelle fonctionnalité
  est démontrée dans un sample MGUI ; toute API publique ajoutée est documentée), puis les utiliser depuis le moteur.
- Fichiers : un sample dans `MGUI.Samples` (une fenêtre liée à un view-model qui déplace une image par
  `Canvas.Left/Top`, nomme une image résolue par un fournisseur de démonstration et en anime une autre) ; la doc
  MGUI concernée (images, binding) ; dans le moteur, la référence du sous-module `MGUI`.
- Validation : le sample se lance et montre les trois comportements (🧪 si le lancement n'est pas faisable en
  session) ; builds du moteur ; `CasaEngine.Tests` à la référence. **Vérificateur frais sur les phases 1 et 2.**
- Commits : `feat(samples): demonstrate bound canvas coordinates and host-resolved animated images` puis
  `docs(images): document host-resolved and animated image sources` (MGUI) ;
  `chore(submodules): point at MGUI with bound and animated images` (moteur).
- **Fait** (MGUI `d2cc651`, `446f547`, `3ca2c23`) :
  - Sample « Bound Images » (`MGUI.Samples/Features/BoundImages.*`, case à cocher dans le Compendium) : une
    image déplacée par `CanvasLeft`/`CanvasTop`, une image résolue par l'hôte, une image animée avec décalage et
    lecture liables. Son fournisseur décore celui de l'application (et le runtime, pour être le fournisseur racine)
    sans changer le reste des ressources du sample.
  - Doc : `MGUI/Docs/image-sources-and-data-binding.md` (liée depuis le README) ; commentaire périmé de
    `MGBinding.cs` corrigé ; `Docs/controls-architecture.md` mis à jour sur le stockage typé des coordonnées.
  - `MGUI.Tests` 3020/3020 ; moteur : les deux solutions et `CasaEngine.Tests` construits sans erreur,
    `CasaEngine.Tests` 1781/1781 (référence), vérifié par la session principale.
  - **Reste pour ✅** : la vérification visuelle du sample (cocher « Bound Images » dans l'application
    `MGUI.Samples`) : l'application n'offre pas d'accès sans intervention humaine.
- **Vérificateur frais des phases 1 et 2 (2026-09-24) : CONFIRMED**, sans constat P0 à P2, sur MGUI
  `6ac7a68..3ca2c23` et le moteur `80c11406`. Il a reproduit les suites (MGUI 3020/3020, `CasaEngine.Tests`
  1781/1781, deux solutions sans erreur), sondé hors dépôt le chemin pilote `Color?` (zéro octet) et les cas de
  nullabilité, et remesuré O3 : 192 octets par notification, dus en totalité à la diffusion par événements
  faibles de WPF ; les tests d'allocation mesurent honnêtement la mise à jour elle-même. Remarques reportées :
  - A1 (P3) : une exception d'un getter source, sur les chemins typés, est capturée et signalée par
    `HasError`/`LastError` au lieu de se propager : changement de comportement accepté (plus sûr), à connaître ;
  - A2 (P3) : une image reste abonnée aux événements de texture après que son `SourceName` est repassé à `null`
    (durée bornée par la portée de sa fenêtre) ;
  - A3 (P3) : une animation n'est pas libérée quand son image est jetée (MGUI n'a pas de fin de vie d'élément) :
    traité par conception en T3.2, les handles partagés appartiennent au fournisseur, libéré avec l'interface ;
  - A4 (P4) : le cache négatif ne redemande jamais un nom (voulu, documenté) ;
  - A5 (P4) : un chemin pilote typé qui n'écrit rien se déclare réussi (même résultat visible qu'avant) ;
  - A6 (P4) : le texte de l'ADR-0016 décrit encore les coordonnées comme des métadonnées et la syntaxe
    `Canvas.Left` ; la doc `image-sources-and-data-binding.md` fait foi ; le commentaire de
    `MGElement.CanvasRight/Bottom` oublie leurs attributs XAML ;
  - A7 (P4, non reproduit) : le choix du chemin pourrait lever pour des formes de propriété inhabituelles.

---

## Phase 3 — Moteur : résolution et écrans en assets

### ✅ T3.1 — Résolution des sprites

- Objectif : D1, P3 côté moteur.
- Fichiers : `CasaEngine/Framework/UI/Backend/MonoGame/Assets/CasaUIAssetProvider.cs`, la construction du
  fournisseur dans le runtime UI (`CasaEngine/Framework/UI/UIRoot.cs` et le démarrage du backend), tests.
- Étapes :
  1. Le fournisseur reçoit le gestionnaire de ressources du jeu.
  2. Un nom qui est un GUID désigne un asset ; sinon, un nom d'asset du catalogue. Un sprite : acquérir le sprite et
     sa texture par handle, rendre la texture et `PositionInTexture`.
  3. Le fournisseur tient les handles et les rend quand le runtime UI est libéré.
- Validation : tests : un sprite résolu par GUID et par nom ; les handles rendus à la libération, puis la ressource
  libérée au prochain `CollectUnreferenced` ; un nom inconnu ne résout rien et le signale une fois.
- Commit : `feat(ui): resolve sprite assets named by UI images`
- **Fait** (`4d6906ae`) :
  - `CasaUIAssetProvider` reçoit le gestionnaire de ressources par `CasaMonoGameBackendOptions.AssetContentManager`
    (que `UIRoot` renseigne), résout un GUID puis un nom de catalogue, accepte les assets de type `sprite`, et
    passe par `Sprite.Create` : trois handles par sprite (`SpriteData`, `Texture`, son `Texture2D`). Tout échec
    rend `false` et journalise une seule fois par nom.
  - Le fournisseur est `IDisposable` ; `UIRoot.Dispose` le libère, et `CollectUnreferenced` libère ensuite ce que
    plus personne ne tient.
  - `CasaEngine.Tests` 1788/1788 (1781 + 7), reproduit ; deux solutions et `Alundra.csproj` sans erreur. Le
    test de bout en bout construit un `MGDesktop` sans `LoadDefaultResources`, qui exige des icônes de contenu
    absentes du projet de tests.

### ✅ T3.2 — Animations 2D comme images animées

- Objectif : D11 côté moteur.
- Fichiers : le fournisseur de T3.1, une implémentation de l'image animée sur `Animation2dCompositionSampler`,
  `CasaEngine/Framework/Assets/Animations/Animation2dCompositionSampler.cs` (parcours sans allocation), tests.
- Étapes :
  1. `ApplyTracks` et les parcours de l'échantillonneur en boucles `for` sur des listes concrètes.
  2. Un nom qui désigne un `.anim2d` : acquérir l'animation par handle, partager ses données de composition, créer un
     échantillonneur par image ; la frame courante vient du sprite de la partie, résolu comme en T3.1.
  3. Le décalage de départ passe par `Seek`.
- Validation : tests : une animation de 4 frames de 200 ms change de frame à moins d'une frame de k × 200 ms ; un
  décalage de 200 ms décale d'une frame ; zéro octet alloué par avance ; handles rendus à la libération.
- Commit : `feat(ui): play 2D animation assets as animated UI images`
- **Fait** :
  - `Animation2dCompositionSampler` : les deux `foreach` à travers une interface (pistes et événements) sont des
    boucles `for` ; comportement inchangé (tests d'animation et d'`AnimatedSpriteComponent` verts).
  - `CasaUIAssetProvider.TryCreateAnimatedImage` : type d'asset `anim2d` ; le fournisseur tient l'animation (une
    composition par nom) et les sprites de ses frames (un cache par id de sprite, ressource d'image créée une fois
    par sprite) ; chaque appel rend une instance propre qui ne possède qu'un échantillonneur (remarque A3 de la
    phase 2). Frame courante : la première partie dans l'ordre de dessin ; décalage de dessin = sa position
    arrondie au pixel ; `Restart` = remise à zéro puis `Seek`. `Dispose` du fournisseur rend tous les handles et
    marque les instances créées comme libérées.
  - Correction de la session principale : `TryResolveImage` journalisait « n'est pas un sprite » pour chaque nom
    d'animation **valide**, puisque MGUI demande d'abord une image statique ; il ne journalise plus que les types
    qui ne sont ni sprite ni animation. Test de non-régression, vérifié par mutation (échoue avec l'ancien code).
  - `CasaEngine.Tests` 1797/1797 (1788 + 9) ; frames à k × 200 ms sur plus de 25 cycles ; deux instances
    décalées de 200 ms restent à une frame ; zéro octet sur 1000 `Advance` et sur 1000 `Update` de
    l'échantillonneur ; deux solutions et `Alundra.csproj` sans erreur.
  - Limites acceptées : un même asset atteint par GUID et par nom donne deux compositions (aucun chargement en
    double) ; le fournisseur garde les instances créées jusqu'à sa libération, c'est-à-dire au plus un monde.

### ✅ T3.3 — Écrans acquis par handle, données de conception

- Objectif : D3, D9, P6 (format), P7 (champ seulement).
- Fichiers : `CasaEngine/Framework/UI/MGUI/UIScreenAsset.cs`, un chargeur de `.uiscreen` enregistré dans
  `AssetLoaderRegistry`, `CasaEngine/Framework/UI/XamlUIScreenBase.cs`,
  `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`, `Projects/CasaEngine.RPGDemo/Scripts/Screens/RpgDemoScreenAssets.cs`
  et ses écrans, `docs/editor/ui-screen-editor/screen-authoring-conventions.md`, tests.
- Étapes :
  1. Champ optionnel `design_time_data_file` dans `UIScreenAsset` et son sérialiseur.
  2. Chargeur de `.uiscreen` ; un écran de jeu acquiert son asset et le rend quand il est libéré.
  3. RPGDemo passe par ce chemin.
  4. Documenter le format complet de l'enveloppe (champs actuels et nouveau).
- Validation : tests : une enveloppe sans le champ se charge comme avant ; le champ fait l'aller-retour ; un écran
  tient son asset et le rend ; RPGDemo lancé depuis `CasaEngine.Demos/` (mémoire du dépôt : dossier courant
  obligatoire) affiche ses trois écrans comme avant. **Vérificateur frais sur la phase 3.**
- Commit : `feat(ui): acquire screen assets through the asset manager`
- **Fait** :
  - `UIScreenAsset.DesignTimeDataFile`, lu depuis le champ optionnel `design_time_data_file` ;
    `SaveUIScreenAsset` ne l'écrit que s'il est renseigné, comme les autres champs optionnels du sérialiseur.
  - Aucun nouveau chargeur n'était nécessaire : `AssetLoader<UIScreenAsset>` était déjà enregistré, et le
    gestionnaire pose `FileName` sur l'asset chargé. `XamlUIScreenBase` gagne un constructeur
    `(AssetContentManager, string assetIdOrName)` qui acquiert l'enveloppe par handle et retrouve son chemin
    complet par `ResolveAssetFullPath` ; `XamlUIScreenBase` devient `IDisposable` (virtuel, idempotent).
  - RPGDemo : ses trois écrans passent par ce constructeur ; `RpgDemoScreenAssets.cs`, devenu inutile, est
    supprimé ; `ScriptTitleScreenWorld` et `ScriptWorld` libèrent leurs écrans dans `OnEndPlay`.
  - Doc : format complet de l'enveloppe dans `docs/editor/ui-screen-editor/screen-authoring-conventions.md`.
  - `CasaEngine.Tests` 1804/1804 (1797 + 7) ; deux solutions sans erreur.
  - RPGDemo lancé par un harnais hors dépôt (`CasaEngine.Launcher/Program.cs`, fichier de l'auteur, est codé
    en dur sur Alundra) : écran titre puis HUD du monde de jeu affichés par le nouveau chemin, aucune exception ;
    l'écran de fin de partie, qui demande de perdre, est couvert par les tests sans affichage.
  - **Régression introduite, corrigée dans le parent** : `AlundraInventoryScreen.Dispose()` masquait le
    nouveau `Dispose` virtuel de la base (CS0114), si bien qu'une libération par une référence de base aurait
    oublié `font3` et les sprites ; il le redéfinit désormais et appelle la base.
- **Vérificateur frais de la phase 3 (2026-09-24) : CONFIRMED**, sans constat P0 à P2, sur le moteur
  `276d36a1..9701916b` et le parent `3613920`. Suites et builds reproduits ; RPGDemo (titre puis HUD) et la
  recette Alundra (0 / 0 / 31, aucun avertissement) rejoués ; zéro octet sur 5000 avances ; aucune double
  libération, `CollectUnreferenced` passant avant `world.Clear` au changement de monde. Remarques reportées :
  - A1 (P3) : le fournisseur garde toutes les instances d'animation créées jusqu'à sa libération, même celles
    que MGUI a libérées ; une image dont la source alterne entre animations en crée une par changement : voir O4 ;
  - A2 (P3/P4) : un appel au fournisseur après sa libération acquiert des handles qui ne sont jamais rendus ;
    aucun chemin du moteur ne le fait.

---

## Phase 4 — Éditeur

### ✅ T4.1 — Aller-retour sans perte

- Objectif : D2, P9.
- Fichiers : `CasaEngine.EditorServices/ScreenEditor/Xaml/UIScreenXamlParser.cs`, `UIScreenXamlSerializer.cs`, le
  modèle de document (`ScreenEditor/DocumentModel/`), `docs/editor/ui-screen-editor/xaml-support-matrix.md`, tests.
- Étapes :
  1. Le modèle garde les commentaires (et leur place), les déclarations d'espace de noms, le propriétaire d'une
     propriété attachée, l'ordre des attributs et le texte des extensions de balisage.
  2. Le sérialiseur les restitue ; un attribut ajouté se place après les autres.
- Validation : tests golden : un XAML avec commentaires, `xmlns:dataBinding`, `{dataBinding:MGBinding …}`,
  `CanvasLeft` en attribut et un élément de propriété qualifié, ouvert puis enregistré sans modification, ressort
  identique octet pour octet (fins de ligne comprises) et se charge dans le vrai `XAMLParser`. Tests existants de
  l'éditeur verts.
- Précision de contrat (2026-09-24, fait vérifié dans le code) : `XDocument` ne conserve ni les retours à la ligne
  entre attributs (la racine d'`InventoryScreen.xaml` en a), ni les guillemets simples, ni les fins de ligne `\r\n` ;
  le sérialiseur actuel (`UIScreenXamlSerializer.cs:28-29`) ajoute en plus une déclaration XML. Deux garanties :
  - **document non modifié** : l'enregistrement réécrit le texte d'origine tel quel ;
  - **document modifié** : sérialisation sans perte de contenu (commentaires, déclarations d'espace de noms,
    préfixes, propriétaire des propriétés attachées et éléments de propriété à leur place, ordre des attributs,
    texte des extensions de balisage, présence de la déclaration XML, style de fin de ligne) ; seule la mise en
    forme des zones touchées peut changer.
- Commit : `fix(screen-editor): keep comments, namespaces and attribute order when saving a screen`
- **Fait** :
  - Le modèle garde une référence vivante vers l'arbre XML d'origine (`UIScreenNode.SourceElement`,
    `UIScreenPropertyValue.SourceElement`, `UIScreenDocument.SourceXDocument`, champs internes : aucune API des
    panneaux ne change). Le sérialiseur modifie cet arbre en place, seulement là où une valeur a changé :
    commentaires, espaces, déclarations `xmlns`, préfixes et éléments de propriété qualifiés survivent par
    construction. Un document sans source (créé dans l'éditeur) garde l'ancienne synthèse.
  - « Aucun changement sémantique » : `UIScreenSemanticSnapshot` (type, nom, valeurs effectives, enfants dans
    l'ordre, ressources) calculé juste après l'analyse et comparé à l'enregistrement ; s'il est égal, les octets
    d'origine sont réécrits tels quels, BOM compris.
  - Supprimer un nœud supprime les commentaires qui le précèdent ; limites documentées dans la matrice de support :
    réordonner des enfants ne déplace pas leurs commentaires, et modifier une ressource reconstruit tout le bloc
    `Window.Resources`.
  - `CasaEngine.Tests` 1814/1814 (1804 + 10), reproduit : rejeu octet pour octet de la vraie
    `InventoryScreen.xaml` d'Alundra (CRLF, attributs sur plusieurs lignes, commentaires), d'un fichier LF, d'un
    fichier à guillemets simples, d'un fichier avec BOM, et d'un fichier avec `xmlns:dataBinding` ; enregistrement
    après modification chargé par le vrai `XAMLParser`, binding compris ; deux solutions sans erreur.
  - Fait confirmé : MGUI ne connaît pas la syntaxe attachée `Canvas.Left` en XAML (le vrai analyseur la
    refuse) ; ses coordonnées s'écrivent `CanvasLeft` (voir T1.2).

### 🧪 T4.2 — Aperçu lié et images réelles

- Objectif : D9, D1 dans l'éditeur, P7.
- Fichiers : `CasaEngine.EditorServices/ScreenEditor/Preview/UIScreenPreviewBuilder.cs`,
  `CasaEngine.Editor/Controls/UIScreenPreviewPanel.cs`, la construction du backend de l'éditeur
  (`CasaEngine.Editor/GameEditor.cs`), tests.
- Étapes :
  1. Construire l'aperçu avec un contexte de données (`XAMLParser.LoadPreview` accepte déjà un contexte).
  2. Lire le fichier de conception, instancier le type nommé par `ElementFactory`, le remplir par
     `JsonConvert.PopulateObject` ; en cas d'échec, journaliser et l'afficher dans l'aperçu, qui s'affiche sans
     données.
  3. Donner au fournisseur de l'éditeur le gestionnaire de ressources de l'éditeur, pour que l'aperçu résolve les
     images comme le jeu.
- Validation : tests : un aperçu avec données de conception montre les valeurs ; un fichier absent ou invalide
  laisse l'aperçu vide avec un message ; smoke dans l'éditeur sur un écran de RPGDemo doté d'un fichier de
  conception (🧪 si le smoke n'est pas faisable en session).
- Commit : `feat(screen-editor): preview screens with design-time data and real images`
- **Fait** :
  - `UIScreenDesignTimeDataLoader` lit le fichier de conception (résolu comme la source XAML : chemin absolu, sinon relatif au
    `.uiscreen`, sinon au projet), instancie le type
    nommé par `ElementFactory` (nom simple du type, pas de nom qualifié) et le remplit par
    `JsonConvert.PopulateObject` ; il rend un contexte ou un message d'erreur, jamais d'exception.
    `UIScreenPreviewBuilder` pose le contexte comme `WindowDataContext` de la fenêtre d'aperçu ; le panneau
    d'aperçu journalise l'erreur et l'ajoute à sa ligne d'état, l'écran s'affichant sans données.
    `UIScreenEditorSession` expose le même résultat (`DesignTimeDataContext`, `DesignTimeDataError`).
  - Le fournisseur de l'éditeur reçoit l'`AssetContentManager` du runtime de l'éditeur à chaque présentation
    d'un projet (`AttachAssetContentManager`) après avoir rendu ses handles (`ReleaseHeldAssets`) ; MGUI oublie
    les textures résolues par l'hôte et son cache négatif (`MGResources.ForgetHostResolvedTextures`, MGUI
    `23638b0`), pour qu'un changement de projet redemande chaque nom au nouveau catalogue. L'éditeur libère le
    fournisseur à sa fermeture.
  - MGUI.Tests 3021/3021 ; `CasaEngine.Tests` 1822/1822 (1814 + 8 : 5 sur l'aperçu avec données de conception,
    3 sur le fournisseur) ; deux solutions sans erreur.
  - **Reste en 🧪** : le smoke dans l'éditeur n'est pas faisable sur RPGDemo. L'automatisation de l'éditeur
    exige une entité sélectionnable dans le premier monde, et `TitleScreenWorld` n'en a aucune (deux lancements
    arrêtés au délai). Le smoke est reporté en B2, sur le projet Alundra, dont le premier monde a des entités.

### ⏳ T4.3 — Choisir une image dans l'inspecteur

- Objectif : rendre la source d'une `Image` éditable sans taper de GUID.
- Fichiers : `CasaEngine.EditorServices/ScreenEditor/Inspector/UIPropertyRegistry.cs` (descripteur `Source` existant,
  à brancher sur `SourceName`), `CasaEngine.Editor/Controls/MGElementPropertyApplier.cs`, le panneau d'inspecteur, le
  sélecteur `CasaEngine.Editor/Controls/AssetSelector.cs`, tests.
- Validation : test : choisir un sprite ou une animation écrit le GUID dans le document et met l'aperçu à jour ;
  smoke dans l'éditeur (🧪 si non faisable en session). **Vérificateur frais sur la phase 4.**
- Commit : `feat(screen-editor): pick an image source from the asset catalogue`

---

## Phase 5 — Consommateur Alundra (dépôt parent)

### ⏳ T5.1 — Écrans d'Alundra

- Objectif : tranches B1 à B3 du plan parent `docs/plan-bound-screens.md` : écrans en assets versionnés, inventaire
  et HUD liés et animés, recettes en jeu.
- Validation et commits : dans le dépôt parent.

---

## Phase 6 — Écran de dialogue remplaçable

### ⏳ T6.1 — Remplacer l'écran de dialogue par un asset du projet

- Objectif : D12, P8.
- Fichiers : `CasaEngine/Framework/Dialogue/UI/DialogueScreen.cs`, les réglages du projet (champ optionnel), la doc de
  l'écran de dialogue (`docs/engine/dialogue-choices-and-bitmap-fonts.md`), tests.
- Étapes :
  1. Champ optionnel des réglages du projet qui nomme l'asset `.uiscreen` de dialogue.
  2. Si le champ est renseigné, charger cet asset ; sinon, ou en cas d'échec journalisé, le XAML embarqué.
  3. Documenter les noms d'éléments que l'écran lie.
- Validation : tests : sans le champ, comportement inchangé ; avec un asset valide, il est utilisé ; avec un asset
  invalide, repli journalisé. **Vérificateur frais.**
- Commit : `feat(dialogue): let a project replace the dialogue screen markup`
- Suite : tranche B4 du plan parent.

---

## Phase 7 — Documentation et clôture

### ⏳ T7.1 — Documentation et clôture

- Fichiers : `docs/editor/ui-screen-editor/` (auteur d'écrans : sources d'image, données de conception, binding),
  `docs/README.md`, `ai-agent/README.md`, ce plan (archivage).
- Validation : relecture des liens ; validation globale.
- Commit : `docs(ui): document bound screens, image sources and design-time data`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | ~~P1 à P9 attendent la validation de l'auteur avec ce plan.~~ Validés par l'auteur le 2026-09-24, plan approuvé, exécution en mode AUTO. | toutes |
| O2 | Ordre des merges en fin de programme : MGUI `develop`, moteur `main`, parent `main`. Décision de l'auteur. | clôture |
| O3 | **Reporté, à arbitrer par l'auteur.** La diffusion de `PropertyChanged` par les événements faibles de WPF (`UseWPF`) alloue environ 192 octets par notification, avant toute mise à jour de binding. Préexistant, hors de D8 (ni réflexion ni boxing). Pistes : un gestionnaire d'événements faibles sans allocation dans MGUI, ou un abonnement direct avec désabonnement explicite à la libération. Le vérificateur de fin de phase 2 recontrôle la mesure. | suite |
| O4 | **À corriger en B3.** Le fournisseur retire de sa liste une instance d'animation que MGUI libère (remarque A1 de la phase 3) : les cases du HUD changent de source en jeu, entre sprites et animations, et la liste grossirait pendant tout un monde. | B3 |

## Hors périmètre

- Un conteneur d'éléments posé sur un `Canvas` (P1).
- Un générateur de code au build pour les accesseurs de binding (D8 laisse la porte ouverte).
- Les écrans de l'éditeur lui-même et l'éditeur XAML de MGUI (`MGUI.Editor`).
- Les défilements d'argent et de PV et les glissements d'ouverture d'Alundra : ils restent calculés par les
  directeurs et arrivent par binding.
