# Plan agent IA — Édition des propriétés d'une cutscene dans l'éditeur

Première tranche de l'éditeur de cutscenes. Il n'y a pas de document d'analyse dans `ai-agent/audits/` : les faits viennent de la lecture du code du 2026-11-17, listés dans « État vérifié du dépôt ».
Les décisions D1 → D7 ci-dessous ont été arbitrées avec l'auteur le 2026-11-17 : **ce plan les applique, il ne les rediscute pas**. Les points P1 → P11 sont des arbitrages de l'agent, à confirmer par l'auteur à la recette.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Le document `.cutscene` de l'éditeur, aujourd'hui en lecture seule, devient éditable pour les **propriétés des actions existantes** : on change une valeur, on annule, on rétablit, l'onglet montre l'état modifié, et on sauvegarde dans le fichier `.cutscene`.

L'arbre `RootAction` reste la seule source de vérité. Le format `.cutscene` et le runtime ne changent pas. La structure de l'arbre (ajouter, supprimer, déplacer une action), l'aperçu et la timeline sont hors périmètre.

## État vérifié du dépôt (2026-11-17)

- `main` est à `c365b514` (`git rev-parse --short=8 refs/heads/main`). Arbre de travail : un seul dossier non suivi, `Projects/PixelUiSample/.casaeditor/`, sans rapport, à ne jamais indexer (`git status --short`).
- Le document cutscene de l'éditeur est `CutsceneAssetInspectorPanel`, `internal sealed`, qui n'affiche que du texte (`CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs:17`, `:224-239`). Son statut dit « Read-only V1 cutscene asset » (`:166-168`).
- `TryLoadAsset` remplace `Name`, `AssetId` et `FileName` par ceux du catalogue quand le fichier y figure (`:125-132`), et avale toute exception de chargement (`:140-143`).
- `GameEditor.TryOpenCutsceneAsset` ouvre le document (`CasaEngine.Editor/GameEditor.cs:5067-5119`). Il n'appelle ni `SetHistoryContextId`, ni abonnement à un état modifié, contrairement à Animation2D (`:5136-5141`).
- `RefreshCutsceneRuntimeSnapshot` reconstruit l'affichage à l'activation du document (`GameEditor.cs:3692-3695`, `:3718-3721`).
- Le modèle affiché vient de `CutsceneReadOnlyDocumentBuilder.Build` (`CasaEngine.EditorServices/Cutscenes/CutsceneReadOnlyDocumentBuilder.cs:9`). Il liste les propriétés de la plupart des types d'action (`:32-139`), **mais pas de `PlaySound`, `PlayMusic`, `StopMusic`, `FadeMusic` ni `FadeScreen`** : ces cinq types tombent dans `default` et n'affichent que `runtime_type` (`:136-138`). Ses nombres sont formatés en `0.###`, donc avec perte. `CutsceneReadOnlyDocument.CanEdit` vaut toujours `false` (`CutsceneReadOnlyDocument.cs`, dernière propriété).
- La liste complète des champs de chaque type est dans `SaveAction` (`CasaEngine/Framework/Cutscenes/Serialization/CutsceneAssetJsonSerializer.cs:48-301`). Pour les cinq types absents du builder : `PlaySound` (`sound_asset_id`, `volume`, `bus_name`), `PlayMusic` (`sound_asset_id`, `fade_in_seconds`, `crossfade`), `StopMusic` (`fade_out_seconds`), `FadeMusic` (`target_volume`, `duration_seconds`), `FadeScreen` (`r`, `g`, `b`, `duration_seconds`, `blend_mode`) (`:92-119`). `FadeScreen` figure dans 6 des 14 fichiers `.cutscene` (constat d'une relecture).
- Le chargement perd ou réécrit des données sans erreur de format dans trois cas relevés par une relecture : un `target` qui n'est pas une chaîne devient `null` et `Save` omet alors la clé (`:497-498`, `:157-163`, `:283-297`) ; un `blend_mode` invalide de `FadeScreen` devient `Additive` (`:674-680`) ; `LoadActions` saute une entrée qui n'est pas un objet et ignore un `actions` qui n'est pas un tableau (`:658-671`).
- `MoveTo` et `NavigateTo` sans `entity`, et `PlaySound` sans `bus_name`, se chargent avec une chaîne vide (`:325`, `:332`, `:341`, constat d'une relecture).
- `EditorHistoryContext.FromDocument` ne connaît pas `EditorDocumentKind.Cutscene` et rend `Empty` (`CasaEngine.Editor/History/EditorHistoryContext.cs:20-33`) : tant qu'un onglet cutscene est actif, annuler et rétablir ne font rien. Un test du même motif existe pour les entités (`CasaEngine.Tests/Editor/EntityAssetDocumentTests.cs:11-19`).
- La garde « modifications non enregistrées » à la fermeture n'existe que pour les écrans d'UI (`GameEditor.cs:390-402`, `:2762-2767`, `:2863-2880`). Animation2D n'en a pas.
- `EditorHistoryStack.Undo` retire l'entrée avant d'appeler `Command.Undo()` (`CasaEngine.EditorServices/History/EditorHistoryStack.cs:86-89`) : une commande dont `Undo` lève perd son entrée.
- `CutsceneAssetJsonSerializer.Save(CutsceneAsset, JObject)` existe et est public (`CasaEngine/Framework/Cutscenes/Serialization/CutsceneAssetJsonSerializer.cs:11`). Il écrit `id`, `name`, `type`, `version`, `schema_version` puis `root_action`.
- `EditorAssetJsonSerializer.TrySerialize` n'a pas de cas `CutsceneAsset` (`CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:29-107`) : `EditorAssetWriterService.SaveAsset` lèverait `InvalidOperationException` (`EditorAssetWriterService.cs:45-55`). `SaveDocument(string, JObject, EditorAssetSaveSource)` est public (`:57`).
- `EditorAssetSaveSource` va de `Unknown = 0` à `WorldInspector = 8`, sans valeur pour les cutscenes (`EditorAssetWriterService.cs:9-20`).
- `EditorHistoryContextKind` n'a pas de valeur pour les cutscenes (`CasaEngine.Editor/History/EditorHistoryContextKind.cs:3-17`).
- Dix classes d'action portent un dictionnaire `FormatErrors`, rempli au chargement et indexé par la clé JSON du champ (`rg -n "FormatErrors" CasaEngine/Framework/Cutscenes --glob "*ActionData.cs"` ; `CutsceneAssetJsonSerializer.cs:456-470`). Le validateur transforme chaque entrée en erreur (`CutsceneValidator.cs:296-309`). Rien ne vide ces dictionnaires.
- Une action de type inconnu se charge en `UnknownCutsceneActionData` ; le builder n'en connaît que `UnknownType` (`CutsceneReadOnlyDocumentBuilder.cs:132-134`).
- Animation2D donne le modèle d'historique et de sauvegarde d'un panneau d'asset : `SetHistoryContextId`, `IsDirty`, `DirtyStateChanged`, `TrySaveLoadedAsset`, `EditorHistoryService.Current.Execute(contexte, EditorDelegateCommand)`, `EditorDirtyStateService.Current.MarkSaved` (`CasaEngine.Editor/Controls/Animation2dAssetInspectorPanel.cs:250-252`, `:355-412`, `:3594-3628`).
- `GameEditor` relie un panneau d'asset à l'éditeur en cinq endroits, visibles pour Animation2D : sauvegarde du document actif (`GameEditor.cs:2467-2474`), fermeture (`:3017-3021`), ouverture (`:5136-5141`), « tout sauvegarder » (`:5573-5580`), titre à l'astérisque (`:5702-5708`, `:5804-5807`, `:5845`).
- Le dépôt contient 14 fichiers `.cutscene` : 12 dans `Projects/` et 2 dans `CasaEngine.Demos/Content/Cutscenes/` (`fd -e cutscene .`).
- Des tests de panneaux d'éditeur existent (`CasaEngine.Tests/Editor/AudioMixerPanelTests.cs`, `SoundAssetInspectorPanelTests.cs`), ainsi que des tests du builder (`CasaEngine.Tests/EditorServices/Cutscenes/CutsceneReadOnlyDocumentBuilderTests.cs`) et du sérialiseur (`CasaEngine.Tests/Cutscenes/CutsceneAssetJsonSerializerTests.cs`).
- Le dernier ADR est le 0094 (`ls docs/decisions`). Le prochain numéro libre est 0095.
- Le plan archivé de la timeline exclut un modèle cutscene plat et un `CutsceneTimelineAdapter` (`ai-agent/tasks/archive/timeline-generic-agent-plan.md:23-27`). Ce plan-ci ne touche pas à la timeline.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | L'arbre `RootAction` reste la seule source de vérité. Pas de modèle plat, pas de nouveau format (proposition du 2026-11-17, acceptée par « Oui, rédige le plan »). |
| D2 | Sauvegarder réécrit le fichier sous forme canonique : valeurs par défaut explicites, ordre des clés du sérialiseur, nom du catalogue, clés inconnues perdues. |
| D3 | La sauvegarde est refusée, avec un message, si l'arbre contient une action de type inconnu ou une erreur de format restante. Elle ne détruit jamais de données en silence. |
| D4 | Découpage en tranches. Ce plan est la tranche 1 : propriétés, annuler/rétablir, état modifié, sauvegarde. |
| D5 | L'auteur a demandé l'exécution complète de ce plan en mode autonome le 2026-11-17 (« enchaine pour executer totalement le plan en mode autonome »). Pas de push, pas de fusion dans `main`. |
| D6 | L'aperçu se fera plus tard dans un monde isolé, et les effets globaux y seront neutralisés. Hors de ce plan. |
| D7 | Une timeline viendra plus tard, en projection en lecture seule, après son propre ADR. Hors de ce plan. |

## Points à valider par l'auteur

| Réf | Arbitrage de l'agent |
|---|---|
| P1 | Chaque modification d'une propriété est une commande d'historique qui garde l'ancienne et la nouvelle valeur **typées**, plutôt qu'une copie complète de l'asset comme Animation2D. Raison : une copie par JSON perdrait les champs d'une action inconnue et les erreurs de format. L'annulation remet la valeur typée, sans repasser par l'analyse d'un texte. |
| P2 | Modifier un champ efface son entrée dans `FormatErrors` et sa perte de chargement (P7) ; annuler les remet. Sans cela, la validation et la garde garderaient l'erreur d'un champ corrigé. |
| P3 | Nouvelles valeurs d'énumération, additives : `EditorAssetSaveSource.CutsceneEditorPanel = 9` et `EditorHistoryContextKind.Cutscene` en fin de liste. |
| P4 | Un cas `CutsceneAsset` est ajouté à `EditorAssetJsonSerializer.TrySerialize`, qui délègue à `CutsceneAssetJsonSerializer.Save`. Le contrôle de D3 se fait avant, dans un service dédié. |
| P5 | Les champs sont édités en texte, validés à la saisie, en culture invariante : nombre, entier, booléen `true`/`false`, GUID, couleur `r, g, b, a`, vecteur `x, y, z`, valeur d'énumération, liste séparée par des virgules. Le type exact de chaque champ est celui de la propriété C# de l'action. Les sélecteurs d'entité et d'asset viennent dans une tranche suivante. |
| P6 | `CutsceneReadOnlyDocument` et son builder ne changent pas, `CanEdit` compris : ils restent le modèle d'affichage de l'arbre et de l'état du runtime. Le panneau cesse seulement d'afficher la ligne « Editable » et tire ses champs éditables du service de T1.1, qui couvre aussi les cinq types que le builder ignore. |
| P7 | Pour appliquer D3 aux pertes que le chargeur ne signale pas, l'éditeur compare, à l'ouverture, le JSON du fichier au JSON que produirait une sauvegarde immédiate. Pour chaque clé connue d'une action présente dans le fichier, la sauvegarde doit rendre la même clé, du même genre (nombre, chaîne, booléen, tableau) et de même valeur ; les tableaux `actions` doivent avoir le même nombre d'entrées. Chaque écart est une « perte de chargement » qui bloque la sauvegarde tant que le champ n'a pas été modifié. Les clés inconnues et les valeurs par défaut ajoutées restent acceptées (D2). |
| P8 | Pas de garde « modifications non enregistrées » à la fermeture d'un onglet cutscene dans cette tranche, comme pour Animation2D : fermer un onglet modifié perd les modifications. La limite est écrite dans l'ADR et la documentation. |
| P9 | Une chaîne vide est une valeur acceptée par le champ de saisie pour toute propriété de type chaîne, puisque le chargeur en produit. Le validateur ne la signale pas (`CutsceneValidator.cs:279-285` ne teste que `null`), alors que le chargeur la refuse pour plusieurs clés (`CutsceneAssetJsonSerializer.cs:456-470`, `:630-656`). C'est donc la garde de sauvegarde qui la bloque, par relecture de ce qu'elle va écrire (P10). Une propriété chaîne nullable ne repasse à `null` que par l'annulation. |
| P10 | Avant d'écrire, la garde sérialise l'asset, recharge ce JSON dans un asset neuf et refuse la sauvegarde si ce rechargement porte une erreur de format, une action inconnue ou une perte de chargement (P7). L'éditeur n'écrit donc jamais un fichier que son propre chargeur relirait avec une erreur. |
| P11 | Un nombre saisi doit être fini (`NaN`, `Infinity` et un dépassement comme `1e40` sont refusés). Une valeur d'énumération doit être un nom défini de l'énumération, pas un nombre. Une liste ne contient pas d'entrée vide. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/cutscene-property-editing`**, créée depuis `main` (`c365b514`). Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché (`dotnet build CasaEngine.Editor.MonoGame.sln`) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, sans `--no-build`, dès qu'une tâche touche du code testé. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- **Aucun fichier `.cutscene` du dépôt n'est réécrit** par une tâche ou par un test : les tests travaillent en mémoire ou dans un dossier temporaire.
- **Aucun fichier de `CasaEngine/` (runtime) n'est modifié.** Si une tâche semble l'exiger : ⚠️ Blocked.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; le runtime ne dépend pas de l'éditeur ; sérialisation additive (détail dans `AGENTS.md` et les règles par chemin).

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet build CasaEngine.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous les tests passent, aucun test existant modifié.
- `git status --short` : aucun fichier `.cutscene` modifié, aucun fichier sous `CasaEngine/` modifié.
- Recette manuelle de l'auteur dans l'éditeur (T3.2).

---

## Phase 0 — Décision

### ✅ T0.1 — ADR-0095 : les cutscenes s'éditent dans l'éditeur, l'arbre reste la source de vérité

> Validation (2026-11-17) : plan relu par trois relecteurs neufs (deux REVISE corrigés, puis READY). ADR écrit selon le modèle, ajouté à l'index, sans nom de jeu ni de projet tiers.

- Objectif : enregistrer D1, D2, D3, les ajouts d'API de P3 et P4, la règle de fidélité de P7 et la limite de P8 (fermer un onglet modifié perd les modifications).
- Fichiers : `docs/decisions/0095-<titre>.md` (créé, modèle `docs/decisions/template.md`), `docs/decisions/README.md` (index).
- Étapes :
  1. Créer la branche `chantier/cutscene-property-editing` depuis `main`.
  2. Écrire l'ADR avec le skill `adr` : contexte (document en lecture seule, sauvegarde absente), décision (D1 à D3, P1 à P4), conséquences (réécriture canonique, refus de sauvegarde), limites (pas d'édition de structure, pas d'aperçu, pas de timeline).
  3. Ajouter la ligne à l'index.
- Validation : l'ADR suit le modèle ; aucun nom de jeu ni de projet tiers n'y figure.
- Commit : `docs(decisions): record ADR-0095 on editing cutscene assets in the editor`

## Phase 1 — Services d'édition (`CasaEngine.EditorServices`)

### ✅ T1.1 — Propriétés éditables d'une action

> Validation (2026-11-17) : `dotnet build CasaEngine.Editor.MonoGame.sln` 0 erreur ; `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` 5809 réussis sur 5809 (5674 avant, 135 ajoutés par `CutsceneActionPropertyEditorTests`). Les tests comparent les propriétés aux clés réellement écrites par `Save` pour chaque type d'action. Écrit en plus : `CutsceneActionProperty` (type d'une propriété), `FindProperty` et `GetFormatErrors` (lecture des `FormatErrors` des dix classes, utilisée par T1.2). Un texte de couleur à trois composantes est accepté en entrée (opaque), comme le chargeur ; le texte rendu a toujours quatre composantes.

- Objectif : un service pur qui, pour une action, donne la liste de ses propriétés éditables et sait lire et écrire chacune depuis un texte.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/CutsceneActionPropertyEditor.cs` (créé, plus les petits types qu'il expose), `CasaEngine.Tests/EditorServices/Cutscenes/CutsceneActionPropertyEditorTests.cs` (créé).
- Étapes :
  1. La source des propriétés est `SaveAction` du sérialiseur (`CutsceneAssetJsonSerializer.cs:48-301`), pas le builder : pour chaque type d'action, exposer chaque champ que `SaveAction` écrit, sous sa clé JSON, sauf `type` et `actions`. `count` de `Repeat` est une propriété éditable.
  2. Chaque propriété offre : lire et écrire la valeur typée (`object`), convertir la valeur en texte sans perte en culture invariante (format aller-retour, pas `0.###`), et analyser un texte (`TryParse(texte, out valeur, out erreur)`). Les formats sont ceux de P5.
  2 bis. Une propriété de type liste (`inputs` d'`ExitOnInput`, dont la liste est en lecture seule et vivante, `ExitOnInputCutsceneActionData.cs:16`) rend une **copie** à la lecture, et l'écriture remplace le contenu de la liste depuis une copie. Une valeur capturée ne change donc jamais après coup.
  2 ter. `TryParse` applique P11 : nombres finis, noms d'énumération définis, pas d'entrée vide dans une liste.
  3. Un texte invalide ne change rien et rend un message. Un texte vide remet à `null` une propriété nullable qui n'est pas une chaîne ; pour une chaîne, il donne la chaîne vide (P9) ; il est refusé pour un nombre, un booléen, un GUID, une couleur ou un vecteur non nullable.
  4. Le service ne touche pas à `FormatErrors` : c'est la commande de T1.2 qui le fait.
  5. Les seules exemptions, sans propriété éditable, sont `Sequence`, `Parallel` et `UnknownCutsceneActionData`. Elles sont nommées dans le code et dans le test.
- Validation : tests unitaires. Le test énumère toutes les constantes de `CutsceneActionTypes` par réflexion et échoue si un type n'a ni propriété ni exemption nommée. Pour chaque propriété de chaque type, `FadeScreen` et les quatre actions audio comprises : écriture d'une valeur valide, relue égale ; texte de la valeur relu par `TryParse` égal à la valeur (aller-retour sans perte, avec un nombre comme `0.1` et `1234.5678`) ; refus sans changement d'un texte invalide pour toute propriété qui n'est pas une chaîne. Refus de `NaN`, `Infinity` et `1e40` pour un nombre, de `5` pour une énumération, de `a,,b` pour une liste. La valeur lue d'une liste ne change pas quand on écrit ensuite la propriété.
- Commit : `feat(editor-services): expose editable properties of cutscene actions`

### ⏳ T1.2 — Commande d'historique d'une modification

- Objectif : une session d'édition qui porte l'asset et ses pertes de chargement, et une commande `IEditorCommand` qui applique une modification de propriété et sait l'annuler exactement.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/CutsceneEditSession.cs` (créé), `CasaEngine.EditorServices/Cutscenes/SetCutsceneActionPropertyCommand.cs` (créé), `CasaEngine.Tests/EditorServices/Cutscenes/SetCutsceneActionPropertyCommandTests.cs` (créé).
- Étapes :
  1. `CutsceneEditSession` porte le `CutsceneAsset` et la liste des pertes de chargement, chacune identifiée par l'instance de l'action et la clé JSON. Dans cette tâche la liste est vide à la création ; T1.3 la remplit.
  2. La commande reçoit la session, l'action, la propriété et la nouvelle valeur typée, déjà analysée par T1.1. À la construction, elle capture l'ancienne valeur typée, l'entrée `FormatErrors` de la clé et la perte de chargement de la clé, s'il y en a.
  3. `Execute` écrit la nouvelle valeur, retire l'entrée `FormatErrors` et la perte de chargement de la clé (P2).
  4. `Undo` remet l'ancienne valeur typée directement, sans analyser de texte, puis l'entrée `FormatErrors` et la perte de chargement capturées. `Undo` ne peut pas lever sur une valeur que le chargeur a produite.
- Validation : tests unitaires. `Execute` puis `Undo` rend un JSON de `Save` et un `Validate()` identiques à l'état de départ, dans ces cas au moins : une action chargée avec une erreur de format, un `MoveTo` chargé sans `entity`, un `OpenScreen` chargé avec `"target": ""`, un `ShowSprite` chargé sans `color`, et un `ExitOnInput` chargé avec `"inputs": ["a","b"]` dont on passe `inputs` à `["c"]` (après `Undo` : `["a","b"]` ; après `Redo` : `["c"]`). `Execute`, `Undo`, `Execute` rend l'état modifié. Passage par `EditorHistoryStack` (`Execute`, `Undo`, `Redo`). Une perte de chargement retirée par `Execute` revient par `Undo`.
- Commit : `feat(editor-services): add an undoable command to set a cutscene action property`

### ⏳ T1.3 — Sauvegarde d'une cutscene et garde contre la perte de données

- Objectif : sauvegarder un `CutsceneAsset` depuis l'éditeur, et refuser quand D3 s'applique.
- Fichiers : `CasaEngine.EditorServices/EditorAssetWriterService.cs` (valeur `CutsceneEditorPanel = 9`), `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs` (cas `CutsceneAsset`), `CasaEngine.EditorServices/Cutscenes/CutsceneLoadFidelity.cs` (créé), `CasaEngine.EditorServices/Cutscenes/CutsceneSaveGuard.cs` (créé), `CasaEngine.EditorServices/Cutscenes/CutsceneEditSession.cs` (fabrique depuis un `JObject`), `CasaEngine.Tests/EditorServices/Cutscenes/CutsceneSaveTests.cs` (créé).
- Étapes :
  1. `CutsceneLoadFidelity.FindLosses(JObject source, CutsceneAsset asset)` applique P7 : il sérialise l'asset et parcourt en parallèle `root_action` du fichier et de la sauvegarde. Les clés connues d'un type sont celles de T1.1, plus `count`. Les nombres se comparent en `float`, les chaînes sans tenir compte de la casse, les tableaux élément par élément sur la longueur du fichier, les objets (comme `destination`) clé par clé avec les mêmes règles ; jamais par `JToken.DeepEquals`. Un `actions` du fichier qui n'est pas un tableau, ou dont le nombre d'entrées diffère de la sauvegarde, est une perte. Un `root_action` présent qui n'est pas un objet, et une clé connue dont la valeur est `null` en JSON alors que la sauvegarde écrit autre chose ou rien, sont aussi des pertes. Chaque perte porte l'instance de l'action, la clé et le chemin au format du validateur (`root_action.actions[2]`).
  2. `CutsceneEditSession` gagne une fabrique qui charge un asset depuis un `JObject` et remplit ses pertes de chargement.
  3. `CutsceneSaveGuard.TryGetBlockingReason(session, out raison)` refuse s'il trouve une `UnknownCutsceneActionData`, un `FormatErrors` non vide ou une perte de chargement restante. Il applique ensuite P10 : il sérialise l'asset, recharge ce JSON par la fabrique de l'étape 2 et refuse si le rechargement porte une action inconnue, une erreur de format ou une perte. La raison donne le chemin et la clé du premier cas.
  4. Ajouter le cas `CutsceneAsset` à `TrySerialize`, qui appelle `CutsceneAssetJsonSerializer.Save`, et la valeur d'énumération.
  5. Si un des 14 fichiers du dépôt produit une perte de chargement, ne pas assouplir la règle sans raison : noter le cas sous la tâche. Si l'écart est une forme canonique acceptée par D2 (par exemple une couleur à trois composantes réécrite à quatre), l'écrire comme règle explicite de comparaison, avec son test. Sinon : ⚠️ Blocked.
- Validation : tests.
  - Les 14 fichiers sont les 12 de `Projects/*/Cutscenes/` et les 2 de `CasaEngine.Demos/Content/Cutscenes/`, pris dans les sources ; les copies sous `bin/` et `obj/` sont exclues. Le test échoue s'il en trouve moins de 14.
  - (a) Pour chacun des 14 fichiers `.cutscene` du dépôt, lus en mémoire : charger, sérialiser par `TrySerialize`, écrire en texte indenté, relire par `JObject.Parse`, recharger. Les deux arbres ont les mêmes types et chemins, le même texte sans perte de chaque propriété de T1.1, et les mêmes messages de validation. Une seconde sauvegarde rend exactement le même texte que la première.
  - (b) Un test de contrôle prouve que (a) détecte une perte : modifier après le premier chargement un `blend_mode` de `FadeScreen` et un champ décimal fait échouer la comparaison.
  - (c) Les 14 fichiers n'ont aucune perte de chargement et la garde les accepte.
  - (d) Une fixture malformée en mémoire par cas, et la garde refuse chacune : action de type inconnu, erreur de format, `target` qui n'est pas une chaîne, `blend_mode` invalide de `FadeScreen`, entrée de `actions` qui n'est pas un objet, `actions` qui n'est pas un tableau.
  - (e) Pour les fixtures de (d) dont le champ fautif est éditable (erreur de format, `target`, `blend_mode`) : après modification de ce champ par une commande de T1.2, la garde accepte ; après annulation, elle refuse de nouveau. Les deux fixtures sur `actions` n'ont pas de champ éditable et restent refusées.
  - (e bis) P10, par des commandes de T1.2 sur un asset sain : passer `entity` d'un `ShowSprite` à la chaîne vide fait refuser la garde, avec le chemin et la clé ; l'annulation la fait accepter de nouveau.
  - (f) Aucun fichier du dépôt n'est écrit : le test écrit au plus dans un dossier temporaire.
- Commit : `feat(editor-services): save cutscene assets and refuse a lossy save`

## Phase 2 — Document de l'éditeur (`CasaEngine.Editor`)

### ⏳ T2.1 — Champs éditables, historique et sauvegarde dans le panneau

- Objectif : le panneau cutscene affiche chaque propriété dans un champ éditable et porte l'état modifié.
- Fichiers : `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`, `CasaEngine.Editor/History/EditorHistoryContextKind.cs` (valeur `Cutscene`), `CasaEngine.Editor/History/EditorHistoryContext.cs` (cas `EditorDocumentKind.Cutscene` dans `FromDocument`), `CasaEngine.Editor/GameEditor.cs` (seulement l'appel de chargement, si sa signature change), `CasaEngine.Tests/Editor/CutsceneDocumentHistoryTests.cs` (créé), `CasaEngine.Tests/Editor/CutsceneAssetInspectorPanelTests.cs` (créé si le panneau se construit en test comme `AudioMixerPanelTests` ; sinon dire pourquoi sous la tâche).
- Étapes :
  1. Chercher dans les panneaux d'asset existants le contrôle MGUI et le motif de validation de saisie déjà utilisés pour un champ texte éditable, et les reprendre. Ne pas créer de contrôle. S'il n'existe aucun champ texte éditable réutilisable : ⚠️ Blocked.
  2. Le panneau garde une `CutsceneEditSession`, créée depuis le `JObject` du fichier. Sous chaque action de l'arbre, il affiche un libellé et un champ par propriété de T1.1. La validation d'une saisie analyse le texte par T1.1, crée une `SetCutsceneActionPropertyCommand` et l'exécute par `EditorHistoryService.Current.Execute` avec un contexte `EditorHistoryContextKind.Cutscene`. Une saisie invalide remet l'ancien texte et affiche le message dans la ligne de statut. Une saisie égale à la valeur courante ne crée pas de commande.
  3. Ajouter `SetHistoryContextId`, `IsDirty`, `DirtyStateChanged` et `TrySaveLoadedAsset(out string)`, sur le modèle d'Animation2D. `TrySaveLoadedAsset` consulte `CutsceneSaveGuard`, puis appelle `EditorAssetWriterService.SaveAsset(..., EditorAssetSaveSource.CutsceneEditorPanel)` et `MarkSaved`.
  3 bis. Ajouter le cas `Cutscene` à `EditorHistoryContext.FromDocument`, sur le modèle des autres documents d'asset, pour que l'annulation globale vise l'onglet cutscene actif.
  4. Après chaque commande, annulation ou rétablissement, reconstruire l'affichage et la validation depuis l'asset. Le panneau apprend une annulation ou un rétablissement global par l'événement d'historique existant (`EditorHistoryService.HistoryChanged`), filtré sur son contexte ; il s'en désabonne à sa fermeture. `RefreshRuntimeSnapshot` ne doit perdre aucune modification : il relit le même asset en mémoire.
  5. Remplacer le statut « Read-only V1 » par l'état réel (valide ou nombre de problèmes, modifié ou non) et retirer la ligne « Editable » (P6).
  6. `TryLoadAsset` : journaliser l'exception au lieu de l'avaler (`Logs.WriteException`), sans changer sa valeur de retour.
- Validation : build de l'éditeur à 0 erreur. Test, sur le modèle d'`EntityAssetDocumentTests` : `FromDocument` sur un document `Cutscene` rend `(EditorHistoryContextKind.Cutscene, panelId)`. Test d'historique : une commande exécutée dans ce contexte, actif, est annulée par `EditorHistoryService.Undo()` et la propriété reprend sa valeur. Tests du panneau si constructible : une modification rend `IsDirty` vrai, une annulation le rend faux, `TrySaveLoadedAsset` écrit dans un dossier temporaire un fichier qui se recharge avec la valeur modifiée, et il échoue avec la raison de la garde sur une action inconnue.
- Commit : `feat(editor): edit cutscene action properties with undo and save`

### ⏳ T2.2 — Câblage dans `GameEditor`

- Objectif : le document cutscene se comporte comme les autres documents d'asset.
- Fichiers : `CasaEngine.Editor/GameEditor.cs`.
- Étapes : reprendre, pour le panneau cutscene, les cinq points de câblage relevés pour Animation2D dans « État vérifié » :
  1. à l'ouverture : `SetHistoryContextId(panelId)` et abonnement à `DirtyStateChanged` ;
  2. titre de l'onglet avec l'astérisque ;
  3. sauvegarde du document actif ;
  4. « tout sauvegarder » ;
  5. à la fermeture : désabonnement, retrait de l'historique et de l'état modifié du contexte.
  Aucune garde « modifications non enregistrées » n'est ajoutée (P8).
- Validation : les deux solutions buildent à 0 erreur ; tous les tests passent. Relecture du diff : chacun des cinq points a son pendant cutscene, à côté de celui d'Animation2D. Le comportement à l'écran relève de T3.2.
- Commit : `feat(editor): wire the cutscene document into history, dirty state and save`

## Phase 3 — Documentation et recette

### ⏳ T3.1 — Documentation

- Objectif : décrire l'édition des cutscenes dans l'éditeur.
- Fichiers : `docs/editor/cutscene-editor.md` (créé), `docs/README.md` (index), `ai-agent/README.md` (ligne de ce plan mise à jour).
- Étapes : résumé, mode d'emploi court (ouvrir, modifier, annuler, sauvegarder), format des champs (P5), refus de sauvegarde (D3), réécriture canonique (D2), limites, suites prévues.
- Validation : relecture ; liens valides ; aucun nom de jeu ni de projet tiers.
- Commit : `docs(editor): document cutscene property editing`

### ⏳ T3.2 — Recette de l'auteur dans l'éditeur

- Objectif : validation visuelle, que l'agent ne peut pas faire seul.
- Sources : `Projects/SpriteCutsceneSample/Cutscenes/loop.cutscene`.
- Étapes (auteur) :
  1. Ouvrir le projet `Projects/SpriteCutsceneSample` dans l'éditeur, puis `loop.cutscene`.
  2. Changer la durée d'un `Wait` : l'onglet prend l'astérisque. Annuler, rétablir.
  3. Saisir un texte invalide : le champ revient à l'ancienne valeur, avec un message.
  4. Sauvegarder, fermer, rouvrir : la valeur tient. Lancer le sample : la cutscene joue avec la nouvelle durée.
  5. Remettre le fichier dans son état d'origine (`git checkout -- <fichier>`).
- Validation : réponse de l'auteur. La tâche reste 🧪 jusque-là.
- Commit : `docs(ai-agent): record the acceptance of cutscene property editing`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | P1 à P11 sont des arbitrages de l'agent : à confirmer ou corriger par l'auteur à la recette. | T3.2 |
| O2 | La sauvegarde écrit le nom du catalogue dans `name` (D2). Pour `loop.cutscene` de `SpriteCutsceneSample`, le fichier dit « Sprite Loop » et le catalogue « SpriteLoop » : le fichier changera à la première sauvegarde. Constat d'une relecture, à vérifier en T1.3. | T1.3, T3.2 |

## Hors périmètre

- Ajouter, supprimer, dupliquer, déplacer ou envelopper une action (tranche 2).
- Sélecteurs d'entité, de sprite, de son, d'écran ; vérification qu'une entité existe dans un monde (tranche 3).
- Aperçu, lecture, scrub (tranches suivantes, D6).
- Timeline (D7) et tout `CutsceneTimelineAdapter`.
- Toute modification du runtime (`CasaEngine/`), du format `.cutscene`, de `CutsceneReadOnlyDocument` et de son builder.
- Corriger une action de type inconnu depuis l'éditeur.
