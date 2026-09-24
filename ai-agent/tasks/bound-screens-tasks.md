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
| D13 | « Save » enregistre les changements des écrans dans l'éditeur (réponse à O6, 2026-09-24). |
| D14 | Un balisage de dialogue de remplacement incomplet reste affiché, sans la partie manquante ; seul un échec de chargement replie sur la boîte embarquée (option B, conforme à la conséquence écrite de l'ADR-0038 ; remplace le repli complet de T6.1, 2026-09-24). |
| D15 | Pas de fichier de conception pour la boîte de dialogue d'Alundra : rien n'y est lié (écart de B4 validé, 2026-09-24). |
| D16 | Merges MGUI `develop`, puis moteur `main`, puis parent `main`, après les tâches de la phase 8, sur feu vert explicite de l'auteur (2026-09-24). |
| D17 | Fermer un écran modifié, ou quitter l'éditeur avec un écran modifié, demande s'il faut enregistrer les changements (2026-09-24). |
| D18 | Le raccourci Ctrl+S lance le même enregistrement que File > Save (2026-09-24). |

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

### ✅ T2.3 — Sample, documentation MGUI et référence dans le moteur

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
  - **Constat de l'auteur, 2026-09-24 (vérification visuelle, après les merges)** : `MGUI.Samples` plantait au
    démarrage, « Embedded resource was not found: 'MGUI.Samples.Features.BoundImages.xaml' » : le sample avait été
    ajouté sans que `MGUI.Samples.csproj` embarque son XAML (la liste y est explicite), et le compendium construit
    tous les samples au lancement. Corrigé dans MGUI `d47c8e1` (entrée `EmbeddedResource`, et
    `SampleXamlEmbeddingTests`, qui échoue en nommant tout `.xaml` du dossier des samples non embarqué ; vérifié en
    retirant la correction) ; ressource présente dans l'assembly construit ; l'application lancée 25 s sans
    exception ; `MGUI.Tests` 3034/3034. La vérification visuelle du sample reste à l'auteur.
  - **Validé par l'auteur le 2026-09-24** (vérification visuelle du sample « Bound Images »).
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

### ✅ T4.2 — Aperçu lié et images réelles

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
  - Le smoke dans l'éditeur n'était pas faisable sur RPGDemo (l'automatisation exige une entité dans le premier
    monde, que `TitleScreenWorld` n'a pas). **Fait en B2 sur le projet Alundra (2026-09-24)** : `--project
    alundra-project/AlundraGame.json --open-asset UI/Screens/InventoryScreen.uiscreen --entity-index 0` ouvre
    l'écran dans son onglet ; la capture montre les boîtes, les icônes de la pièce, du faucon et de la clé tirées du
    catalogue, les chiffres des données de conception (`x0000`, `00`) et le curseur ; la ligne d'état dit « Loaded
    InventoryScreen.xaml », sans erreur de données de conception ; aucun avertissement dans les diagnostics.
    L'automatisation ne prenait de capture que pendant le smoke de jeu : elle en prend désormais une finale quand
    `--screenshot-out` est donné (`GameEditor`, validation directement liée).

### ✅ T4.3 — Choisir une image dans l'inspecteur

- Objectif : rendre la source d'une `Image` éditable sans taper de GUID.
- Fichiers : `CasaEngine.EditorServices/ScreenEditor/Inspector/UIPropertyRegistry.cs` (descripteur `Source` existant,
  à brancher sur `SourceName`), `CasaEngine.Editor/Controls/MGElementPropertyApplier.cs`, le panneau d'inspecteur, le
  sélecteur `CasaEngine.Editor/Controls/AssetSelector.cs`, tests.
- Validation : test : choisir un sprite ou une animation écrit le GUID dans le document et met l'aperçu à jour ;
  smoke dans l'éditeur (🧪 si non faisable en session). **Vérificateur frais sur la phase 4.**
- Commit : `feat(screen-editor): pick an image source from the asset catalogue`
- **Fait** :
  - Le descripteur d'`Image` édite désormais `SourceName`, le nom que lit le XAML de MGUI (l'ancien descripteur
    `Source` écrivait un attribut qui attend une texture fournie par code ou par binding ; un `Source` présent
    dans un fichier reste conservé tel quel, il n'a simplement plus de ligne dans l'inspecteur).
  - `UIPropertyDescriptor.AssetTypes` (ajout, `init`) marque une propriété comme référence d'asset ; pour
    `SourceName` : `sprite` et `anim2d` (`UIPropertyRegistry.ImageSourceAssetTypes`).
  - L'inspecteur garde la zone de texte (nom, id ou binding) et ajoute dessous un `AssetSelector` filtré sur ces
    types, qui affiche l'asset désigné (lu comme le fournisseur : id, sinon nom). Choisir un asset écrit son id
    (format `D`) en une seule commande annulable, hors transaction de frappe.
  - L'aperçu applique `SourceName` sans reconstruction (`MGElementPropertyApplier`) ; une valeur de binding
    renvoie à la reconstruction, seule à savoir l'installer.
  - `AssetSelector` : la liste du sélecteur et l'action du bouton « Select » passent par deux méthodes internes
    (`GetPickableAssets`, `SelectAsset`), sans changement de comportement, pour les tests.
  - `CasaEngine.Tests` 1828/1828 (1822 + 6), mutation vérifiée (écrire l'id au format `N` fait échouer 3 tests) ;
    deux solutions sans erreur ni avertissement dans les fichiers touchés.
  - **Reste en 🧪** : la ligne « Source » avec son sélecteur n'apparaît qu'une fois un nœud `Image` sélectionné
    dans l'écran, ce que l'automatisation de l'éditeur ne sait pas faire (elle sélectionne des entités du monde). La
    vérification visuelle du sélecteur revient à l'auteur ; sa logique est couverte par les six tests.
  - **Validé par l'auteur le 2026-09-24** (vérification visuelle du sélecteur d'image).
- **Vérificateur frais de la phase 4 (2026-09-24) : REFUTED**, sur le moteur `26e944a7..10ac11cd` et MGUI
  `3ca2c23..23638b0`. Suites et builds reproduits (moteur 1828/1828 sur 28 lancements sur 29, MGUI 3021/3021).
  Constats et décisions :
  - F1 (P2, introduit, **corrigé**) : enregistrer un écran modifié reformatait des éléments non touchés
    (attributs sur plusieurs lignes fusionnés, guillemets simples et références de caractères réécrits), parce que
    `XDocument.ToString()` réécrit chaque balise ouvrante. Contraire au contrat de T4.1. Correctif :
    `UIScreenXamlSourceWriter` relève à l'analyse le texte d'origine de chaque balise ouvrante et la signature
    de ses attributs, et le réécrit tel quel tant que la signature n'a pas changé ; la déclaration XML
    d'origine aussi. Deux tests en égalité exacte (écran réel, et guillemets simples avec `&#x0a;`), qui
    échouent si l'on resynthétise toutes les balises. Sonde du vérificateur rejouée sur 8 fichiers réels : seule
    la balise de l'élément modifié change. Le test lit maintenant une copie de l'inventaire d'Alundra dans
    `CasaEngine.Tests/ScreenEditor/Fixtures/`, et non plus le fichier du dépôt parent : il cassait en B2, qui
    déplace ce fichier, et sur un clone du moteur seul.
  - F2 (P2, introduit, **corrigé**) : le chargeur des données de conception levait sur un `view_model_type` qui
    n'est pas une chaîne, et sur un chemin inutilisable ; l'aperçu tombait alors en « Preview unavailable ».
    Toute erreur de chemin, de lecture ou de forme est désormais rendue en message ; `values` qui n'est pas un
    objet l'est aussi, au lieu d'être ignoré. Cinq cas de test, dont deux échouent si l'on retire la
    vérification de type ; sonde du vérificateur rejouée : aucun cas ne lève.
  - F3 (P3, **reporté**) : un test de T4.2 a échoué une fois sur 29 dans `XAMLParser.LoadRootWindow`, message
    perdu. Non reproduit : 15 lancements complets verts après les correctifs (1835 tests). Cause plausible, non
    prouvée : MGUI range tous les bindings dans des collections statiques non thread-safe
    (`MGUI/MGUI.Core/UI/DataBinding/DataBindingManager.cs:6-9`), et xUnit exécute les classes de test en
    parallèle.
    **Cause prouvée puis corrigée** (deuxième passe) : reproduit une fois sur 30 lancements, avec la pile complète,
    `Dictionary.Add` concurrent dans `DataBindingManager.AddBinding` (`DataBindingManager.cs:35`). Seules deux classes
    de `CasaEngine.Tests` créent des bindings, toutes deux ajoutées par ce programme (T4.1, T4.2) : elles rejoignent
    une collection xUnit qui s'exécute seule (`MguiDataBindingCollection`), comme `ProjectEnvironmentCollection` pour
    le catalogue d'assets global. Le registre de MGUI est mono-thread par conception (l'interface tourne sur un
    thread) : ce n'est pas un manque.
- **Seconde relecture de clôture (2026-09-24) : CONFIRMED** sur `f2bc60bb` (MGUI `09d0462`), sans constat P0 à P2.
  Aller-retour vérifié sur 74 écrans réels et des cas adverses (ajout, suppression et réordonnancement dans le même
  parent, premier et dernier enfant, imbrication, plusieurs enregistrements d'affilée, CRLF avec BOM, CDATA) ;
  chargeur des données de conception sur 37 cas ; test instable absent de 165 lancements ; suites et solutions
  vertes. **Phase 4 close.** Remarques reportées, toutes antérieures aux correctifs :
  - A1 (P3) : déplacer un nœud vers un autre parent le resynthétise (une ligne, commentaires internes perdus),
    `UIScreenXamlSerializer.cs:176` ; la matrice de support ne le dit pas encore : à documenter en T7.1 ;
  - A2 (P3) : un nœud ajouté à un panneau sans enfant n'a pas sa propre ligne indentée (cosmétique) ;
  - A3 (P4) : un enregistrement modifié uniformise des fins de ligne mélangées ;
  - A4 (P4) : un fichier UTF-16 modifié est réécrit en UTF-8 en gardant `encoding="utf-16"`.
  - Test instable sans rapport : `StaticModelMaterialOverrideResolverTests` (1 échec sur 55), déjà connu.
  - F4 (P3, **reporté**, O5) : `ForgetHostResolvedTextures` ne rafraîchit pas une image de fenêtre, et son
    commentaire affirmait le contraire. Commentaire corrigé (MGUI `09d0462`) ; manque consigné en G7 du
    [rapport des manques](../audits/mgui-gaps-from-xaml-screens.md). Le correctif touche une règle
    d'architecture de MGUI.
  - F5 (P4, **reporté**) : un abonné de la racine qui re-résoudrait un nom pendant `RemoveTexture` verrait son
    ajout effacé de la liste des noms résolus. Latent : aucun abonné de ce genre.
  - F6 (P4, préexistant, O6) : aucun chemin de l'éditeur n'enregistre un écran ; la garantie de T4.1 ne passe
    que par `UIScreenEditorSession`, que `GameEditor` n'utilise pas.
- **Relecture de clôture (2026-09-24) : REFUTED** sur `10ac11cd..07171269`. F2 confirmé (33 cas, aucun ne lève) ;
  F1 confirmé pour les attributs (76 fichiers réels) ; suites vertes. Mais l'exploration demandée a trouvé d'autres
  écarts au contrat de T4.1, tous introduits par T4.1 (`482393f3`) et antérieurs aux correctifs. Deuxième passe
  de correction :
  - C1 (P2, **corrigé**) : le BOM était perdu à l'enregistrement d'un écran modifié : `Encoding.GetBytes` n'écrit
    jamais le préambule. Le test `UnmodifiedSave_WithByteOrderMark` de T4.1 ne contenait d'ailleurs pas de BOM,
    pour la même raison : corrigé aussi.
  - C2 (P2, **corrigé**) : enregistrer après l'ajout d'un nœud levait `The parent is missing`.
  - C3 (P2, **corrigé**) : supprimer ou réordonner un enfant reformatait tous ses frères. La réconciliation des
    enfants déplace maintenant des blocs entiers (l'élément avec son indentation et ses commentaires de tête) ;
    un nœud ajouté s'insère après son frère précédent, indenté comme lui.
  - C4 (P3, **corrigé**) : le texte des éléments non touchés était ré-échappé (`=&gt;` réécrit). Le texte d'origine
    de chaque nœud texte est relevé à l'analyse et réécrit tant que sa valeur n'a pas changé.
  - C5 (P4, **reporté**, préexistant) : le contenu reconstruit d'un élément de propriété porte un `xmlns`
    redondant, hérité du XML interne que garde le modèle. Documenté, comme la balise fermante toujours réécrite
    `</nom>`.
  - Preuves : six tests en égalité exacte (suppression avec et sans commentaire, réordonnancement, ajout en fin et
    au milieu, BOM) ; mutations : sans le texte d'origine 6 échecs, sans le BOM 1 échec. Sondes de la relecture
    rejouées : `vf1c` (5 fichiers × 11 opérations) ne montre plus que les lignes de l'élément touché, et `vp4rt`
    sur 74 fichiers réels ne montre aucune sauvegarde non modifiée qui diffère, aucun BOM perdu, aucune
    régression de chargement.

---

## Phase 5 — Consommateur Alundra (dépôt parent)

### ✅ T5.1 — Écrans d'Alundra

- Objectif : tranches B1 à B3 du plan parent `docs/plan-bound-screens.md` : écrans en assets versionnés, inventaire
  et HUD liés et animés, recettes en jeu.
- Validation et commits : dans le dépôt parent.
- Changement moteur fait pendant B2 (2026-09-24) : `XamlUIScreenBase(AssetContentManager, idOrName)` résout un
  identifiant par le gestionnaire d'assets lui-même (son contexte, le catalogue du projet en jeu) et ne consulte
  `AssetCatalog` que pour un nom : les tests de la DLL Alundra donnent à leur gestionnaire un catalogue à eux. Même
  comportement en jeu (`EngineRuntimeContext` résout par `AssetCatalog.Get`). Un test moteur de plus. Manque G8
  consigné : le filtrage à la réduction d'une `Image` n'est pas déclarable en XAML.
- Pendant B3 (2026-09-24) : O4 corrigé (`afbb3cdc`) ; manque G9 consigné : la translation d'une `RenderTransform`
  ne se lie pas en XAML, le HUD la recopie depuis son view-model.
- B1 fait ; B2 (parent `79c0099`) et B3 (parent `b9f3133`, moteur `e78e14a9`) CONFIRMED par un vérificateur frais.
  Reste en 🧪 : l'enregistrement d'un écran depuis l'éditeur, qui attend la réponse à O6.
- **Clos le 2026-09-24** : O6 tranché (D13), enregistrement livré par T4.4 et prouvé sur les écrans d'Alundra par
  B6 (CONFIRMED).

### ✅ T5.2 — Libérer les bindings d'un écran avec lui

- Constat (2026-09-24, session principale, après B3) : rien ne retire les bindings d'une fenêtre d'écran quand l'écran
  est libéré. `XamlUIScreenBase.Dispose` ne rend que le handle de l'enveloppe, et aucun code du moteur n'appelle
  `RemoveDataBindings` (MGUI le recommande avant de retirer un élément, `MGElement.cs:3592-3606`). Les bindings
  restent dans le registre statique de MGUI (`DataBindingManager._Bindings` et `_BindingsByTargetObject`) et
  gardent la fenêtre, ses images et son view-model atteignables. Depuis B2 et B3, l'inventaire (172 chemins) et le
  HUD (122) d'Alundra sont reconstruits à chaque changement de monde : le registre grossit à chaque changement.
  Second défaut, dans MGUI : `DataBindingManager.RemoveBindings(target)` libère les bindings et vide
  `_BindingsByTargetObject`, mais les laisse dans `_Bindings` (`DataBindingManager.cs:70-89`), qui garde donc
  les éléments atteignables même après un `RemoveDataBindings`.
- Classement : P2 introduit par le programme (B2, B3), non couvert par leurs vérifications. Correction dans le
  périmètre approuvé, avant T6.1.
- Fichiers : MGUI `MGUI.Core/UI/DataBinding/DataBindingManager.cs` et un test ; moteur
  `CasaEngine/Framework/UI/XamlUIScreenBase.cs` et un test ; parent : un test par écran d'Alundra ; rapport des
  manques (G10).
- Étapes :
  1. MGUI : `RemoveBindings` retire aussi les bindings de `_Bindings`. Test : après `RemoveBindings`, les bindings
     de l'élément sont libérés et absents de `DataBindingManager.Bindings`.
  2. Moteur : `XamlUIScreenBase.Dispose` retire les bindings de toute la fenêtre (`RemoveDataBindings(true)`).
     Test : un écran chargé depuis un asset qui lie, une fois libéré, ne laisse aucun binding sur sa fenêtre.
  3. Parent : l'inventaire et le HUD libérés ne laissent aucun binding sur leur fenêtre.
- Validation : builds ; `MGUI.Tests`, `CasaEngine.Tests`, `Alundra.Tests` ; chaque nouveau test échoue sans la
  correction. **Vérificateur frais** sur l'ensemble (MGUI, moteur, parent).
- Commits : MGUI `fix(binding): take removed bindings out of the registry` ; moteur
  `fix(ui): release a screen's bindings when it is disposed` ; parent
  `test(ui): the Alundra screens leave no binding behind`.
- Avancement (2026-09-24) :
  - étape 1 : MGUI `0559e6b`, `MGUI.Tests` 3023/3023 ; les deux nouveaux tests échouaient avant la correction
    (« Item found in collection ») ;
  - étape 2 : moteur, `CasaEngine.Tests` 1844/1844 ; le nouveau test échouait avant la correction, après avoir
    constaté le texte lié et les deux bindings ; `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln`
    sans erreur ; manque G10 consigné comme corrigé ;
  - étape 3 : parent `a52a022`, `Alundra.Tests` 1084/1084 ; les deux tests échouent sans la ligne du moteur ;
    recette rejouée (`run-t52`) identique à `run-b3-clock`, hors `inv-389` (écart d'animation du monde entre runs) ;
  - **vérificateur frais (2026-09-24) : CONFIRMED**, sans constat P0 à P2 : défaut reproduit en retirant chaque
    ligne (122 bindings restants pour le HUD, 172 pour l'inventaire) ; suites et builds reproduits ; aucun code de
    production ne lit `DataBindingManager.Bindings` ; les écrans libérés ont déjà quitté l'affichage (le `UIRoot`
    est libéré au même changement de monde) ; recette rejouée identique au pixel à `run-b3-clock`. Remarques P4,
    reportées :
    - un binding dont la cible n'est pas un élément (un pinceau lié) n'est pas parcouru par `RemoveDataBindings`
      et reste dans le registre : limite de MGUI antérieure, les écrans d'Alundra n'en ont pas ;
    - `DataBindingRegistryTests` n'est dans aucune collection, comme les autres tests de binding de MGUI : risque
      d'instabilité hérité d'`AddBinding`, deux passes complètes sans échec ;
    - un second `BuildWindow` sur le même écran remplacerait la fenêtre sans libérer les bindings de la première :
      aucun appelant ne le fait.

---

## Phase 6 — Écran de dialogue remplaçable

### ✅ T6.1 — Remplacer l'écran de dialogue par un asset du projet

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
- Note d'exécution (2026-09-24) :
  - réglage `ProjectSettings.DialogueScreenAsset` (catégorie « UI »), identifiant ou nom ; lu s'il est présent,
    écrit seulement s'il est renseigné : les fichiers de projet existants se chargent sans changement ;
  - `DialogueScreen` gagne le constructeur `(presenter, requestClose, fontFamily, assetContentManager)`, seul à lire
    le réglage (par `RuntimeContext.ProjectSettings` du gestionnaire) ; les trois autres gardent le balisage
    embarqué. L'enveloppe est acquise à la construction et rendue à `Dispose` ;
  - `XamlUIScreenBase` gagne `protected virtual LoadWindow`, que `DialogueScreen` redéfinit : il charge le
    remplacement, vérifie le contrat, et sinon rend les bindings de la fenêtre rejetée (G10) puis charge le
    balisage embarqué ; l'acquisition par identifiant ou nom est partagée (`AcquireScreenAsset`) ;
  - contrat : `pnlContent` (`StackPanel`), `lblLine` (`TextBlock`), `pnlChoices` (`StackPanel`), `btnClose`
    (`Button`, enfant direct de `pnlContent`, obligatoire si `ShowCloseButton`, facultatif sinon), documenté dans
    `docs/engine/dialogue-choices-and-bitmap-fonts.md` (section écrite en français comme le reste du document) ;
  - **interprétation à signaler à l'auteur** : l'ADR-0038 prévoyait qu'un remplacement privé d'un élément
    documenté « perd cette partie de l'affichage » ; l'étape 2 de cette tâche demande le repli journalisé sur le
    balisage embarqué en cas d'échec. Un contrat rompu est traité comme un échec : repli complet, avertissement
    qui dit pourquoi. L'ADR n'est pas réécrite ; **tranché par l'auteur le 2026-09-24 : option B (D14), mise en
    œuvre par T6.2** ;
  - repli journalisé (`Logs.WriteWarning`, comme `CasaUIAssetProvider`) sur : identifiant ou nom inconnu, fichier
    absent, enveloppe illisible, XAML invalide, contrat rompu ; toute autre exception est une erreur de
    programmation et remonte ;
  - tests : 13 de plus (`DialogueScreenReplacementTests` 10, `DialogueScreenReplacementBindingTests` 1,
    `ProjectSettingsDialogueScreenTests` 2) ; trois mutations (sans libération des bindings, sans contrôle du
    contrat, sans lecture du réglage) font chacune échouer les tests qui les visent ; `CasaEngine.Tests`
    1857/1857, `Alundra.Tests` 1084/1084, les deux solutions sans erreur ;
  - démo : `UIOverlayDemo` utilise `DialogueScreen` par le constructeur à deux arguments, inchangé ; elle est
    interactive, sans mode automatique, et n'a pas été lancée ; la tranche B4 exerce le remplacement en jeu.
- **Vérificateur frais (2026-09-24) : CONFIRMED** sur le moteur `6a4aca3a`, sans constat P0 à P2 : les huit
  conditions reproduites, dont un ancien fichier de projet chargé sans le champ, les trois anciens constructeurs
  inchangés, le vrai chargeur d'enveloppes de production, un réglage donné par nom (catalogue global), cinq cas de
  repli (un seul avertissement chacun), une mutation sur la libération des bindings ; `CasaEngine.Tests`
  1857/1857, les deux solutions, `Alundra.Tests` 1084/1084 ; l'interprétation « contrat rompu = échec » jugée
  conforme au texte approuvé et bien signalée. Remarques, reportées :
  - P3 : un identifiant qui désigne un asset d'un autre type déjà en cache fait lever `InvalidCastException`
    (`AssetContentManager.Acquire`, cast `(T)cached`) au lieu d'un repli ; comportement déjà présent dans le
    constructeur de `XamlUIScreenBase` qui prend un gestionnaire ; aucun appelant avant B4 ;
  - P4 : un identifiant qui désigne un autre asset JSON pas encore chargé serait lu comme une enveloppe d'écran et
    resterait en cache sous cet identifiant (déduit du code, non reproduit) ;
  - P4 : avec le vrai chargeur, une enveloppe absente ou malformée écrit aussi une erreur (`AssetLoader.cs:20`) en
    plus de l'avertissement ; les tests livrés passent par un chargeur de test ;
  - P4 : la doc laissait croire que la hauteur déclarée servait de minimum ; `ResizeToFitContent` impose 150 px :
    formulation corrigée.

---

## Phase 7 — Documentation et clôture

### ✅ T7.1 — Documentation et clôture

- Fichiers : `docs/editor/ui-screen-editor/` (auteur d'écrans : sources d'image, données de conception, binding),
  `docs/README.md`, `ai-agent/README.md`, ce plan (archivage).
- Validation : relecture des liens ; validation globale.
- Commit : `docs(ui): document bound screens, image sources and design-time data`
- Note (2026-09-24) :
  - `docs/editor/ui-screen-editor/screen-authoring-conventions.md` : sources d'image (sprites et animations 2D
    nommés par identifiant ou nom, résolus par l'hôte ; `AnimationStartOffset` et `IsAnimationPlaying` ; pause d'une
    image repliée ou hors champ ; décalage de partie en pixels d'écran, Y vers le bas, contre Y vers le haut dans le
    monde ; G8) ; écrans liés (contexte de données, chemins imbriqués, notifier seulement ce qui change, copie typée
    sans allocation, `CanvasLeft`/`CanvasTop`, G9, libération des bindings G10, collection de tests) ; renvoi vers
    le remplacement de la boîte de dialogue. Chaque affirmation cite le code qui la porte.
  - `xaml-support-matrix.md` : remarque A1 de la phase 4 (un nœud déplacé vers un autre parent est ressynthétisé,
    commentaires internes perdus) et état de l'enregistrement dans l'éditeur (O6).
  - Index `docs/README.md` et tableau de `ai-agent/README.md` à jour. Le plan reste dans `tasks/` et non dans
    `tasks/archive/` : T2.3, T4.3 et T5.1 attendent une validation de l'auteur, et O3, O5, O6 une décision.
  - Validation : liens relatifs relus (chaque cible existe).

---

## Phase 8 — Suite après les réponses de l'auteur (2026-09-24)

Réponses de l'auteur aux questions de clôture : D13 à D18 ci-dessus. Plan relu par des vérificateurs de plan frais :
enveloppe READY, T6.2 READY, T4.4 et B6 READY après une révision (le premier passage avait relevé que « Save »
réécrit aussi le monde, `AssetInfos.json` et `AlundraGame.json` : B6 encadre donc chaque lancement de l'éditeur par
un manifeste et une restauration). **Approuvé par l'auteur le 2026-09-24, mode AUTO.** T4.5 et T4.6 (D17, D18) sont
conçues après une reconnaissance, relues avant exécution. Ordre : T6.2, T4.4, T4.5, T4.6, puis B6 (plan parent).

### ✅ T6.2 — Dialogue de remplacement incomplet : affichage partiel (D14)

- Résultat : un balisage de remplacement sans `pnlContent`, `lblLine` ou `pnlChoices`, ou avec l'un d'eux d'un autre
  type, est gardé ; la partie manquante n'est pas affichée ; un seul avertissement nomme chaque problème (type
  attendu et type trouvé). `btnClose` ne compte que si la boîte montre un bouton de fermeture ; sinon il est retiré
  de `pnlContent` s'il en est l'enfant direct, masqué ailleurs. Un échec de chargement replie toujours sur la boîte
  embarquée.
- Fichiers : `CasaEngine/Framework/Dialogue/UI/DialogueScreen.cs`, `CasaEngine.Tests/Dialogue/DialogueScreenReplacementTests.cs`,
  suppression de `DialogueScreenReplacementBindingTests.cs` (plus de fenêtre rejetée), la doc du dialogue.
- Validation : cas (a) à (e) et (d') du plan relu, mutation (repli réintroduit) qui les fait échouer, suites et
  solutions, `Alundra.Tests`. **Vérificateur frais.**
- Commit : `feat(dialogue): keep an incomplete replacement dialogue markup and hide its missing parts`
- Note d'exécution (2026-09-24, exécutant, relu par la session principale) :
  - `LoadWindow` garde le repli sur échec de chargement (`IsMarkupFailure`) ; une fois chargé, le remplacement est
    toujours utilisé ; `FindContractProblems` (remplace `FindContractViolation`) liste chaque problème, par la
    recherche non typée plus un test de type pour nommer le type trouvé ; `ReportContractProblems` écrit un seul
    avertissement ; le rejet et son `RemoveDataBindings` disparaissent.
  - `OnWindowLoaded` : recherches tolérantes (`TryFindElement<T>`), champs nuls sans exception ; avec
    `ShowCloseButton` faux, un `btnClose` de tout type est retiré de `pnlContent` s'il en est l'enfant direct, masqué
    sinon, sans jamais déréférencer un `pnlContent` absent.
  - Tests : `DialogueScreenReplacementTests` réécrit (cas (a) à (e) et (d') du plan relu) ;
    `DialogueScreenReplacementBindingTests` supprimé (plus de fenêtre rejetée ; la libération au `Dispose` reste
    couverte par `XamlUIScreenBaseBindingReleaseTests`, verte). Mutation (repli réintroduit dès qu'il y a un
    problème) : cinq tests échouent, exactement (a), (b), (c), (d') et (e) ; (d) n'a pas de problème de contrat et
    passe, comme attendu ; code restauré puis suite relancée.
  - `CasaEngine.Tests` 1858/1858 ; `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` sans erreur ;
    `Alundra.Tests` 1089/1089 (dont `AlundraDialogueScreenAssetTests` 5/5). Doc du dialogue réécrite (section du
    remplacement et liste des tests).
- **Vérificateur frais (2026-09-24) : CONFIRMED** sur le moteur `b6eacfb6`, sans constat P0 à P2 : cinq conditions
  reproduites ; sondes ajoutées puis retirées (tous les problèmes à la fois -> un seul avertissement qui les liste ;
  trois cycles Show/ShowLine/ShowChoices/SelectChoice/Hide sans exception ; `btnClose` en `TextBlock` avec et sans
  bouton de fermeture ; deux constructions -> deux avertissements) ; mutation reproduite (exactement (a), (b), (c),
  (d') et (e) échouent) ; suites et solutions. Remarque P4 : la doc pouvait se lire « un avertissement par
  élément » et ne disait plus qu'un `btnClose` enfant direct de `pnlContent` en est retiré : corrigée.

### ✅ T4.4 — « Save » enregistre les écrans modifiés (D13)

- Résultat : File > Save, et l'enregistrement proposé avant d'ouvrir un monde, écrivent sans perte le `.xaml` de
  chaque écran ouvert modifié, effacent sa marque et mettent à jour le titre ; le panneau garde la même instance de
  document (sélection et annulation) et ne se recharge pas depuis sa propre écriture. Mêmes gardes que
  `SaveCurrentProject` (mode Play, pas de projet).
- Étapes : `UIScreenDocumentFileWriter` extrait de `UIScreenEditorSession.Save` (commit
  `refactor(editor): share the lossless screen writer`) ; `UIScreenPreviewPanel.TrySaveDocument`, octets de
  l'enveloppe gardés au chargement, décision de rechargement pure `ShouldReload` ; `GameEditor.SaveDirtyScreenDocuments`
  avant `SaveProject` ; automatisation `--set-screen-property <Nœud>:<Propriété>=<Valeur>` et `--save-project`
  (journal : marque et instance de document 60 frames après l'enregistrement ; aucun fichier restauré à la sortie) ;
  doc de la matrice de support (commit `feat(editor): save modified screens with the project`).
- Validation : tests du writer, de `ShouldReload`, et de `TrySaveDocument` si le panneau se construit sans affichage ;
  suites et solutions ; preuve de bout en bout en B6. **Vérificateur frais sur T4.4 et B6 ensemble.**
- Note d'exécution (2026-09-24, exécutant, relu par la session principale) :
  - commit `96ceb976` : `UIScreenDocumentFileWriter.Write(document, chemin, sérialiseur)` extrait de
    `UIScreenEditorSession.Save`, qui délègue ; tests du writer (valeurs remises à l'origine -> octets identiques ;
    BOM et commentaires gardés, `OriginalBytes` et `BaselineSemanticSnapshot` à jour, second `Write` identique) ;
  - `UIScreenPreviewPanel` : `TrySaveDocument`, `LoadedSourceXamlPath`, octets du `.xaml` et du `.uiscreen` gardés au
    chargement et après chaque enregistrement (`OriginalBytes` est interne à EditorServices, invisible de l'éditeur),
    `ShouldReload` pur ; `ReloadFromDisk` s'arrête quand le disque est identique ;
  - `GameEditor.SaveDirtyScreenDocuments`, appelée dans `SaveCurrentProject` avant les autres enregistrements ;
  - automatisation : `--set-screen-property <Nœud>:<Propriété>=<Valeur>` (commande `SetPropertyCommand` par la
    pile du panneau actif ; seul un nœud introuvable est refusé, le modèle de document acceptant tout nom de
    propriété) et `--save-project` (état et instance de document journalisés 60 frames après l'enregistrement,
    aucune restauration de fichier) ;
  - tests : `UIScreenPreviewPanelTests` (`ShouldReload`, et un test sans affichage : chargement, modification,
    `TrySaveDocument`, puis `Update()` pendant que le vrai `FileSystemWatcher` réagit : même instance de document),
    `EditorAutomationOptionsTests` (deux options, cas malformés) ; `CasaEngine.Tests` 1876/1876, deux solutions sans
    erreur ; doc de la matrice de support ;
  - mutations : `ShouldReload` toujours vrai -> deux tests échouent ; sérialisation forcée sans rejeu des octets ->
    tous les tests passent (le sérialiseur fidèle suffit à ces cas, le rejeu est une sécurité en plus) ; sans l'appel
    à `SaveDirtyScreenDocuments`, aucun test unitaire n'échoue (`GameEditor` ne se construit pas en test) : la preuve
    de bout en bout est B6.
- B6 (parent `45de7e4`) : neuf lancements de l'éditeur réel sur le HUD, l'inventaire et le dialogue, chacun protégé
  par manifeste ; sans modification, fichier intact ; une propriété, exactement cette ligne dans `git diff`, même
  instance de document ; valeur remise, `git diff` vide.
- **Vérificateur frais (2026-09-24) : CONFIRMED** sur le moteur `99e66ee0` (avec `96ceb976`) et le parent
  `45de7e4`, sans constat P0 à P2 : writer déplacé tel quel (aucun test existant modifié) ; gardes de
  `SaveCurrentProject` et titre sans astérisque constatés ; sondes ajoutées puis retirées, avec le vrai
  `EditorDirtyStateService`, la vraie pile de commandes et le vrai `FileSystemWatcher` (annuler après un
  enregistrement remarque l'écran, le second enregistrement réécrit l'original à l'octet ; une modification externe
  après un enregistrement recharge ; un fichier en lecture seule donne un message sans exception ; deux panneaux
  modifiés sont écrits, un propre garde sa date) ; mutation de `ShouldReload` reproduite ; `CasaEngine.Tests`
  1876/1876 (trois passes), deux solutions, `Alundra.Tests` 1089/1089 ; **les neuf lancements de B6 rejoués** (mêmes
  résultats, aucune alerte, `git status` de `alundra-project` vide). Remarques :
  - P3 : le manifeste final de B6 avait été pris juste avant le commit qui change le commentaire de
    `DialogueScreen.xaml` (D14) ; le fichier sur disque est bien la version commitée, et les lancements du
    vérificateur ont porté sur elle. Référence désormais : `scratchpad/v-b6-manifest.sha256` (état à `45de7e4`) ;
  - P4 (reportée, sans rapport) : `AudioServiceFadeTests.FadingVoices_DoNotAllocateDuringUpdate` a échoué une fois
    sur une passe complète (7 888 octets au lieu de 0), vert seul et aux deux passes suivantes ;
  - P4 (reportée, non reproduite) : si la relecture du fichier juste après une écriture réussie échouait,
    `TrySaveDocument` signalerait un échec et l'écran resterait modifié ; le prochain enregistrement le réécrirait ;
  - P4 (reportée, non reproduite) : `TrySaveDocument` ne rattrape que `IOException` et
    `UnauthorizedAccessException` ; une autre exception du sérialiseur sortirait de `SaveCurrentProject`, que le
    menu appelle sans `try`.

### ✅ T4.5 — Demander avant de perdre un écran modifié (D17)

Conception relue READY le 2026-09-24 (vérificateur de plan frais), après reconnaissance.

- Faits : fermer un onglet passe par `MGDockTabGroup.PanelCloseRequested` (aussi « Close Others » et « Close All »,
  panneau par panneau, `MGUI/MGUI.Core/UI/Docking/Controls/MGDockTabGroup.cs:552-624`) puis `MGDockHost`
  (`MGDockHost.cs:2422-2444`), qui retire le panneau aussitôt ; `GameEditor.OnDockHostPanelRemoved`
  (`GameEditor.cs:2525-2537`) n'arrive qu'après. Autres chemins : `CloseFloatingPanel` (`MGDockHost.cs:1655`), la
  branche des fenêtres flottantes sans modèle (`MGFloatingDockWindow.cs:269-276`), `CloseAutoHidePanel`
  (`MGDockHost.cs:2029`), une fenêtre flottante fermée entière (`OnFloatingWindowClosed`, `MGDockHost.cs:1505-1535`).
  Aucun n'est annulable. `MGWindow.WindowClosing` l'est (`MGWindow.cs:985-1000`) ; `CancelEventArgs<T>` existe
  (`MGWindow.cs:19-26`). MonoGame 3.8.5.1 : `Game.OnExiting(object, ExitingEventArgs)` avec `Cancel` (documentation
  XML du paquet). Précédent de boîte : `ConfirmSaveBeforeOpeningWorld` (`GameEditor.cs:4067-4097`).
- Étape MGUI : événement public `MGDockHost.PanelClosing` (`EventHandler<CancelEventArgs<DockPanelNode>>`), levé
  avant tout retrait décidé par l'utilisateur : groupes d'onglets (un panneau refusé reste, les autres se ferment),
  `CloseFloatingPanel`, branche des fenêtres sans modèle, `CloseAutoHidePanel`, et fermeture d'une fenêtre flottante
  entière (l'hôte s'abonne à son `WindowClosing` et l'annule si un panneau refuse ; les panneaux déjà traités dans
  ce passage restent, à documenter). Pas pour les retraits programmatiques (`RemovePanel`, `CloseFloatingWindow`).
  Tests `MGUI.Tests/Docking`, sample de docking (case « Unsaved changes »), doc du docking. Commit MGUI :
  `feat(docking): let the host veto a user panel close`.
- Étape éditeur : `GameEditor` s'abonne à `PanelClosing` ; écran modifié -> boîte « Save changes to '<titre>'
  before closing? » Yes (enregistrer puis fermer, ou garder ouvert si l'enregistrement échoue) / No (fermer sans
  enregistrer) / Cancel (garder ouvert). `OnExiting` redéfini : écran modifié -> boîte qui les liste, Yes
  (`SaveDirtyScreenDocuments`, sortie annulée si un écran reste modifié) / No / Cancel ; `base.OnExiting` seulement
  si la sortie continue. Sous automatisation, aucune boîte, et le journal nomme les écrans abandonnés. Décision en
  méthode interne pure, testée dans tous ses cas. Commit moteur : `feat(editor): ask before losing a modified screen`.
- Validation : `MGUI.Tests` (chaque nouveau test échoue sans l'événement ou sans l'annulation), `CasaEngine.Tests`,
  deux solutions ; en B6, une sortie automatisée avec un écran modifié ne bloque pas. **Reste 🧪 pour l'auteur** :
  les vrais clics Yes/No/Cancel à la fermeture d'un onglet, par File > Exit et par la croix de la fenêtre de
  l'éditeur (seul un lancement réel prouve que `Cancel` arrête ce chemin DesktopGL). Vérificateur frais.
- Non-objectifs : les autres types de documents, le changement de projet.
- Note d'exécution (2026-09-24, exécutant, relu par la session principale) :
  - MGUI `6817691` : `MGDockHost.PanelClosing` et `RaisePanelClosingVetoed` ; branché sur les groupes d'onglets
    (onglet, Close Others, Close All), le tiroir auto-masqué (sa demande de fermeture, pas les appels
    programmatiques de `CloseAutoHidePanel`), les deux branches de `MGFloatingDockWindow.OnPanelCloseRequested`, et
    `WindowClosing` des fenêtres flottantes (abonné et désabonné avec `WindowClosed`) ; `PanelClosingVetoTests`
    (10 tests sans affichage) ; panneau « Scratchpad » avec une case « Unsaved changes » dans `DockingDemo` ; doc
    `Docs/controls-architecture.md` ; `MGUI.Tests` 3033/3033 ; `MGUI.Samples` construit.
  - Moteur : `ModifiedScreenCloseDecision.Decide` (pure : modifié, automatisation, réponse demandée par délégué,
    enregistrement par délégué) et 7 tests ; `GameEditor.OnDockHostPanelClosing` (boîte « Close Screen ») et
    `OnExiting` redéfini (boîte « Quit » qui liste les écrans ; sous automatisation, rien n'est demandé et le journal
    nomme les écrans abandonnés) ; `CasaEngine.Tests` 1883/1883, deux solutions sans erreur.
  - Mutations : Cancel ignoré sur le chemin des onglets -> exactement les trois tests de ce chemin échouent ; pas
    d'abonnement à `WindowClosing` -> le test de la fenêtre flottante entière échoue ; un enregistrement raté traité
    comme « continuer » -> le test de ce cas échoue.
  - Sortie automatisée avec un écran modifié (session principale, éditeur réel, projet Alundra protégé par
    `scratchpad/b6_run.py --no-save`, run `t45-exit-modified`) : `WeaponBoxBackground.Opacity` changé, pas
    d'enregistrement, sortie après capture : l'éditeur sort seul (code 0, aucune boîte), le `.xaml` n'est pas écrit,
    rien n'est écrit dans le projet. La ligne « modified screen(s) abandoned » n'est pas observable dans un fichier :
    l'éditeur ne journalise que dans son panneau et la sortie de débogage (`GameEditor.cs:369-370`), et
    `diag.txt` est écrit avant la sortie ; elle reste couverte par la lecture du code et le test du cas
    « automatisation » de `ModifiedScreenCloseDecision`.

### ✅ T4.6 — Ctrl+S (D18)

Conception relue READY le 2026-09-24 (vérificateur de plan frais).

- Faits : raccourcis dans `GameEditor.Update` (`GameEditor.cs:5506-5555`, F5, Ctrl+Z/Shift+Z/Y/D/C/X/V) derrière
  `!IsEditorShellCapturingKeyboard()` ; pas de Ctrl+S. La caméra du viewport recule sur S
  (`EditorViewportCameraController.cs:270-297`) quand `allowClassicKeys` (`_isRightDragCapturing || isKeyboardFocused`,
  `:182`), sans regarder Ctrl : Ctrl+S, comme Ctrl+D aujourd'hui, la déplacerait.
- Étapes : branche Ctrl+S -> `SaveCurrentProject()` dans la même chaîne et derrière la même garde ; la caméra ignore
  WASDQE tant que Ctrl est enfoncé (flèches et Page Haut/Bas inchangées), avec un test si la décision s'isole ; doc
  des raccourcis. Commit : `feat(editor): save with Ctrl+S`.
- Validation : `CasaEngine.Tests`, deux solutions ; l'appui réel sur Ctrl+S (enregistre, astérisque effacée,
  caméra immobile) revient à l'auteur si l'automatisation ne sait pas injecter de touche (🧪 pour ce point).
  Vérificateur frais (avec T4.5).
- Note d'exécution (2026-09-24, session principale) : branche Ctrl+S -> `SaveCurrentProject()` en fin de chaîne des
  raccourcis de `GameEditor.Update`, derrière la même garde ; `EditorViewportCameraController.HandleKeyboardCameraInput`
  ignore WASDQE tant que Ctrl gauche ou droit est tenu ; théorie `Update_LetterKeysMoveTheCamera_ButNotWhileControlIsHeld`
  (S seul déplace ; Ctrl gauche ou droit + S, Ctrl + D ne déplacent pas ; Ctrl + flèche déplace) ; mutation (garde
  retirée) : les trois cas Ctrl échouent ; `CasaEngine.Tests` 1888/1888, deux solutions sans erreur. Doc : étape 7
  du flux de l'éditeur d'écrans (`screen-authoring-conventions.md`) : File > Save ou Ctrl+S, confirmation à la
  fermeture et à la sortie. L'automatisation de l'éditeur ne sait pas injecter une touche (`EditorAutomationOptions`
  n'a pas d'option de clavier) : l'appui réel sur Ctrl+S reste à l'auteur.
- **Vérificateur frais sur T4.5 et T4.6 (2026-09-24) : CONFIRMED** sur MGUI `6817691` et le moteur `aaba7d96`,
  `fdd88804`, sans constat P0 à P2 : tous les appelants de `DockOperation.ClosePanel`, `RemovePanelById`,
  `NotifyFloatingPanelClosed`, `PanelRemoved` et `TryCloseWindow` passés en revue, aucun chemin utilisateur oublié ;
  `OnExiting` sans risque sur un éditeur partiellement initialisé ; sortie automatisée avec un écran modifié rejouée
  (code 0 en 36 s, rien d'écrit), et une sonde temporaire a prouvé que la branche « automatisation » est bien prise
  (`modified=1 automation=True`, puis retirée) ; **MonoGame DesktopGL 3.8.5.1 décompilé** : la croix de la fenêtre,
  Alt+F4 et File > Exit passent par `Game.Exit()`, et `Game.Tick` n'arrête rien quand `ExitingEventArgs.Cancel` est
  vrai ; Ctrl+S sur front de touche, inerte dans une zone de texte ; `CasaEngine.Tests` 1888/1888, `MGUI.Tests`
  3033/3033, `Alundra.Tests` 1089/1089, deux solutions et `MGUI.Samples`. Remarques P4, reportées :
  - « Yes » enregistre l'écran même en mode Play (pas la garde de `SaveCurrentProject`) : choix de conception ;
  - pendant un vol au clic droit, WASDQE ne bouge plus si Ctrl est tenu : effet voulu ;
  - une fenêtre flottante autonome créée par du code applicatif n'est pas suivie par l'hôte, sa fermeture entière
    ne lève pas `PanelClosing` : préexistant, l'éditeur n'en crée pas ;
  - une exception autre qu'`IOException`/`UnauthorizedAccessException` pendant « Yes » à la sortie remonterait hors
    de `Game.Tick` (même remarque qu'en T4.4) ;
  - sur une capture automatisée, l'onglet d'un écran modifié s'affichait sans `*` alors que le contexte était bien
    modifié (confiance basse, relève de T4.4) : à regarder par l'auteur en vrai lancement.
- **Reste 🧪 pour l'auteur** : les vrais clics Yes/No/Cancel (onglet, File > Exit, croix de la fenêtre), un vrai
  Ctrl+S, la case « Unsaved changes » du sample de docking de MGUI, et la présence du `*` après une modification.
- **Validé par l'auteur le 2026-09-24** : les clics Yes/No/Cancel, Ctrl+S et le veto du sample de docking. Le `*`,
  lui, n'apparaît qu'au prochain redessin de l'onglet : défaut de MGUI traité par T4.7.

### 🧪 T4.7 — Le titre d'un onglet suit son panneau

- Constat de l'auteur (2026-09-24, vérification visuelle de T4.5/T4.6) : le `*` d'un écran modifié n'apparaît sur
  son onglet qu'au prochain redessin (survol, activation). Cause : `GameEditor.UpdateDockPanelTitle`
  (`GameEditor.cs:5190-5197`) met bien à jour `DockPanelNode.Title`, qui lève `PropertyChanged`
  (`MGUI/MGUI.Core/UI/Docking/DockLayout/DockPanelNode.cs:12-22`), mais les vues du docking copient le titre une
  fois et ne l'observent pas : `MGDockTabItem` (ne relit `Panel.Title` que dans `UpdateVisuals`, `:540`),
  `MGFloatingDockWindow.UpdateTitle`, les boutons de `MGDockAutoHideStrip`, l'en-tête de `MGDockAutoHideDrawer`.
  Défaut antérieur au programme, qui touche tous les types de documents.
- Correction (MGUI) : chaque vue s'abonne au `PropertyChanged` du panneau dont elle affiche le titre, rafraîchit
  son texte sur « Title », et se désabonne quand elle cesse de l'afficher (panneau remplacé, vue reconstruite ou
  jetée) ; tests sans affichage pour les quatre vues, une mutation par vue.
- Validation : `MGUI.Tests`, `CasaEngine.Tests`, les solutions ; vérificateur frais ; le `*` en vrai lancement
  revient à l'auteur.
- Commits : MGUI `fix(docking): keep tab and window titles in step with their panel`, puis pointeur du sous-module.
- Note d'exécution (2026-09-24, exécutant, relu par la session principale) : MGUI `6e8b999`. `MGDockTabItem`
  (abonnement dans le setter de `Panel` et le constructeur, `OnPanelPropertyChanged`, `Detach`) ;
  `MGDockTabGroup.RebuildTabHeaders` détache les anciens onglets ; `MGFloatingDockWindow.UpdateTitle` déplace
  l'abonnement vers le panneau dont il montre le titre ; `MGDockAutoHideStrip.Refresh` se réabonne, `Detach` appelé
  par `MGDockHost` quand un gabarit remplace la bande ; `MGDockAutoHideDrawer.ActivePanel` déplace l'abonnement.
  `DockTitleFollowTests` (5 tests, une mutation par vue : chaque test échoue sans son abonnement) ; `MGUI.Tests`
  3039/3039, `CasaEngine.Tests` 1888/1888, `MGUI.Samples` et les deux solutions sans erreur.
  - Hypothèse de l'exécutant, **contredite** par l'éditeur réel : selon lui, l'hôte reconstruisait déjà les onglets
    à tout changement d'un panneau. Même scénario dans l'éditeur réel (`scratchpad/b6_run.py --no-save`, HUD
    modifié, capture) : avant la correction (run `t45-exit-modified`) l'onglet affiche « HudScreen » sans `*` ;
    après (run `t47-asterisk-fixed`) « HudScreen * ». Projet intact après les deux runs.
- Vérificateur frais n° 1 (2026-09-24) : **REFUTED** sur un seul point, le reste confirmé (cause, onglets, fenêtre
  flottante, bandes, capture « HudScreen * » refaite en vrai lancement (run `vf-t47-asterisk`), suites et solutions,
  mutations, note exacte).
  - Pourquoi l'hôte ne reconstruisait pas l'onglet : `DockLayoutModel` ne s'abonne qu'aux nœuds présents quand sa
    racine est affectée ou qu'un groupe flottant est ajouté ; l'éditeur ajoute un document plus tard par
    `DockOperation.DockAsTab`, donc un changement de titre ne lève jamais `LayoutChanged`. Les panneaux présents dès
    l'affectation de la racine sont rafraîchis deux fois (texte, puis reconstruction), sans effet visible.
  - F1 (P2, corrigé) : quand un gabarit de l'hôte remplace le tiroir pendant qu'il est ouvert, l'ancien tiroir restait
    abonné à son panneau et suivait encore son titre. MGUI `9e98a4a` : `MGDockHost.AttachControlTemplateStructure`
    remet `ActivePanel` à null sur le tiroir jeté, comme `Detach` pour les bandes. Nouveau test
    `ReplacingTheHostStructure_DetachesTheOpenDrawer_TheNewDrawerFollowsInstead`, qui échoue sans la correction
    (vérifié) ; `MGUI.Tests` 3040/3040, `MGUI.Samples` sans erreur.
  - A1 (P4, corrigé dans le même commit) : le commentaire de `DockTitleFollowTests` disait que le modèle
    reconstruisait l'arbre à tout changement d'un nœud ; il donne maintenant la raison ci-dessus.
  - A2 (P4, reporté) : une fenêtre flottante autonome (constructeur public) fermée d'un bloc garde ses panneaux
    dans son groupe et reste abonnée au panneau dont elle montrait le titre. L'éditeur ne crée pas de telles
    fenêtres ; à reprendre si un usage apparaît.
- Vérificateur frais n° 2 (2026-09-24, état final MGUI `9e98a4a`, moteur `62e91913`, parent `1bac792`) :
  **CONFIRMED**. F1 repris sur le cas d'origine : le tiroir jeté lâche son panneau et son contenu, le nouveau tiroir
  héberge le même contenu et suit le titre, l'hôte reste cohérent (tiroir fermé, panneau toujours masqué, bande
  visible). Vrai lancement (run `vf2-t47-final`, DLL MGUI de l'éditeur identique à celle construite) : « HudScreen * »,
  projet intact. `MGUI.Tests` 3040/3040, `CasaEngine.Tests` 1888/1888, `Alundra.Tests` 1089/1089, `MGUI.Samples` et
  les deux solutions sans erreur ; mutation du nouveau test reproduite.
  - A3 (P4, reporté avec A2) : une fenêtre flottante dont le groupe n'a aucun panneau actif suit le panneau de repli
    (`Panels[0]`) ; si on retire ce panneau directement du groupe, elle le suit encore, `UpdateTitle` n'étant rappelé
    que sur un changement de panneau actif (`MGFloatingDockWindow.cs:140-147`). Aucun code de production ne vide le
    panneau actif ; avant la correction, le titre restait déjà figé dans ce cas.
- **Reste pour ✅** : le coup d'œil de l'auteur sur le `*` après une vraie saisie dans l'inspecteur (les deux captures
  passent par `--set-screen-property`).

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | ~~P1 à P9 attendent la validation de l'auteur avec ce plan.~~ Validés par l'auteur le 2026-09-24, plan approuvé, exécution en mode AUTO. | toutes |
| O2 | Ordre des merges en fin de programme : MGUI `develop`, moteur `main`, parent `main`. Décision de l'auteur. | clôture |
| O3 | **Reporté, à arbitrer par l'auteur.** La diffusion de `PropertyChanged` par les événements faibles de WPF (`UseWPF`) alloue environ 192 octets par notification, avant toute mise à jour de binding. Préexistant, hors de D8 (ni réflexion ni boxing). Pistes : un gestionnaire d'événements faibles sans allocation dans MGUI, ou un abonnement direct avec désabonnement explicite à la libération. Le vérificateur de fin de phase 2 recontrôle la mesure. | suite |
| O4 | ~~À corriger en B3 : le fournisseur garde toutes les instances d'animation.~~ **Corrigé (B3, 2026-09-24)** : une instance que MGUI libère quitte la liste du fournisseur (`CasaUIAnimatedImage.Dispose`), sauf pendant la libération du fournisseur lui-même ; un test (50 instances créées et libérées, une seule vivante), qui échoue si l'instance ne se retire pas. | B3 |
| O5 | **À arbitrer par l'auteur.** Une image de fenêtre ne voit pas les textures ajoutées ou retirées à la racine (G7) : après un changement de projet dans l'éditeur, une image encore affichée garderait une texture dont le handle est rendu. Pistes : relayer les événements de texture vers les portées enfants (un troisième événement dans le lien faible que l'ADR-0001 de MGUI veut étroit), ou abonner l'image à la portée qui a fourni sa texture. | suite |
| O6 | ~~Question posée à l'auteur le 2026-09-24.~~ **Répondue le 2026-09-24 : D13 (T4.4).** L'éditeur n'enregistre aucun écran : « Save » (`GameEditor.SaveCurrentProject`) ne couvre pas les documents d'écran, et le sérialiseur ne sert qu'au presse-papiers (`GameEditor.cs:1881`). Un choix d'image fait dans l'inspecteur (T4.3) ne peut donc pas être enregistré depuis l'éditeur, et la validation « enregistrement sans modification » de B2 n'est pas atteignable. Préexistant, hors des tâches du plan. | B2 |

## Hors périmètre

- Un conteneur d'éléments posé sur un `Canvas` (P1).
- Un générateur de code au build pour les accesseurs de binding (D8 laisse la porte ouverte).
- Les écrans de l'éditeur lui-même et l'éditeur XAML de MGUI (`MGUI.Editor`).
- Les défilements d'argent et de PV et les glissements d'ouverture d'Alundra : ils restent calculés par les
  directeurs et arrivent par binding.
