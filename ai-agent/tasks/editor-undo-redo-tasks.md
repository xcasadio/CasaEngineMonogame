# Plan d'implementation - Undo/Redo global de l'editeur

## Contexte

L'editeur dispose deja d'un undo/redo local pour le screen editor, mais le reste des surfaces d'edition n'est pas couvert de maniere coherente.

Etat actuel observe dans le code :
- `Game1` route `Edit > Undo/Redo` et `Ctrl+Z` / `Ctrl+Y` vers `_screenCommandStack` uniquement.
- Le screen editor a une vraie base de commandes (`UICommandStack`, `IUIScreenCommand`, `CompositeCommand`).
- `EntitiesPanel`, `EntityDetailsPanel` et `WorldViewportPanel` mutent le monde directement.
- `MaterialAssetInspectorPanel` applique les changements directement et sauvegarde sur disque a chaque modification.
- `ContentBrowserPanel` effectue des operations fichier directes (rename, move, copy, delete, import, new folder, paste) via `FileOperationService`.

L'objectif est d'obtenir un undo/redo digne d'un moteur de jeu moderne : global dans le shell, scope par contexte/document authoring, transactionnel, previsible, et compatible avec toutes les surfaces editables.

---

## Objectif cible

Quand ce plan sera termine, l'editeur devra fournir :
- un undo/redo global route par contexte/document authoring actif
- une pile d'historique distincte pour le monde, chaque UI screen ouvert, chaque material ouvert, et le content browser
- une entree d'historique par intention utilisateur, pas une entree par evenement technique
- des labels explicites du type `Undo Rename Entity`, `Redo Move Material Asset`, `Undo Delete 3 Items`
- une restauration coherente de la selection, du refresh visuel et de l'etat dirty
- une separation claire entre les champs d'authoring et les champs UI ephemeres (recherche, filtres, etc.)

Surfaces devant etre couvertes :
- world hierarchy : `EntitiesPanel`
- world inspector : `EntityDetailsPanel` + `ComponentEditors`
- world viewport : `WorldViewportPanel` + gizmo + drag/drop d'assets
- UI screen hierarchy : `UIScreenHierarchyPanel`
- UI screen inspector : `UIScreenInspectorPanel`
- UI screen toolbox : `UIScreenToolboxPanel`
- UI screen preview : `UIScreenPreviewPanel`
- material inspector : `MaterialAssetInspectorPanel`
- content browser : `ContentBrowserPanel`

Hors scope initial volontaire :
- layout docking du shell
- selection seule comme action historique autonome
- champs de recherche / filtre non lies aux donnees authoring
- logs / output / project launcher

---

## Regles obligatoires pour l'agent IA

- Langue du document : francais
- Langue du code : anglais
- Aucun nouveau code WPF
- Pas de refactor non relie au sujet
- Toujours garder un build compilable
- Toujours faire au minimum un build cible borne apres chaque tache : `dotnet build CasaEngine.Editor.MonoGame.sln`
- 1 commit par tache atomique, jamais un commit pour plusieurs taches de cette liste
- La tache ne peut pas passer en `✅` tant que le commit n'existe pas
- Le statut et le hash du commit doivent etre mis a jour dans ce fichier apres chaque tache
- Si une tache terminee attend encore une validation manuelle ciblée, utiliser `🧪` au lieu de `✅`
- Si un comportement legacy doit etre garde temporairement, ajouter une compat explicite plutot qu'une rupture brutale

---

## Legende des statuts

- ⏳ A faire
- 🚧 En cours
- ✅ Termine
- 🧪 A valider
- ⚠️ Bloque

---

## Protocole de suivi obligatoire

Pour chaque tache :

1. Passer l'icone de la tache de `⏳` a `🚧` avant la premiere modification.
2. Realiser la tache avec un scope strictement limite a son objectif.
3. Lancer la validation ciblee indiquee par la tache.
4. Creer un commit dedie.
5. Remplacer `Commit realise: -` par le hash court et le message reel du commit.
6. Si le hash du commit courant ne peut pas etre renseigne sans creer un commit supplementaire, noter provisoirement `a renseigner au commit suivant` puis le completer lors de la tache suivante.
7. Passer le statut en `✅` si le build et la verification demandee sont OK.
8. Si le code est pret mais qu'une verification reste a faire, utiliser `🧪`.
9. Si la tache bloque, passer en `⚠️` et documenter le blocage juste sous la tache.

Regle de synthese :
- le statut d'une phase doit refleter l'etat le plus avance de ses sous-taches
- ne pas laisser une tache en `🚧` une fois la session terminee

---

## Principes d'architecture a respecter

### 1. Historique par contexte actif

Le shell doit router l'undo/redo selon `EditorContextService.ActiveDocument` ou, pour le content browser, selon un contexte explicite d'outillage. Il ne doit plus exister de pile unique hard-codee pour le screen editor dans `Game1`.

Important : la granularite recommandee est le contexte/document authoring, pas le panel UI. Par exemple, `EntitiesPanel`, `EntityDetailsPanel` et `WorldViewportPanel` doivent partager la meme pile `World`, car ils editent tous le meme etat authoring.

### 2. Commandes generiques, domaines specialises

Le screen editor a deja de bonnes commandes. Il faut capitaliser dessus et generaliser le contrat au lieu d'inventer un second systeme concurrent.

Cible recommandee :
- `IEditorCommand`
- `EditorHistoryStack`
- `EditorHistoryService`
- `EditorTransactionScope` ou `EditorCommandBatch`
- adaptateur temporaire entre `IUIScreenCommand` et `IEditorCommand` si necessaire

### 3. Une interaction utilisateur = une entree d'historique

Comportements attendus :
- taper un nouveau nom complet dans un champ de propriete => 1 entree
- glisser un gizmo translate/rotate/scale => 1 entree
- drag d'un control UI => 1 entree
- reset d'une propriete material => 1 entree
- rename/move/delete dans le content browser => 1 entree par intention utilisateur

### 4. Arbitrage entre texte local et historique global

Les `MGTextBox` ont deja un undo/redo interne. Il faut definir une regle claire :
- si le champ edite une donnee authoring, l'operation doit finir en commande editeur globale
- si le champ est purement UI (search box, filtre, rename overlay non encore valide), l'undo local reste prioritaire

### 5. Dirty state moderne

L'historique ne doit pas etre confondu avec la sauvegarde :
- `Save` vide le dirty state du document/panel
- `Save` ne doit pas effacer l'historique
- charger un autre projet ou fermer un document peut vider l'historique associe
- le material editor ne doit plus sauver sur disque a chaque changement de valeur si on veut un comportement moderne et coherent

### 6. Operations fichier reversibles

Pour le content browser, une suppression undoable ne doit pas etre un `File.Delete` irreverssible au moment de la commande. Il faut une strategie de staging/trash editor pour pouvoir restaurer proprement les fichiers et dossiers.

---

## Definition of Done

La feature sera consideree complete quand :
- `Ctrl+Z`, `Ctrl+Y` et `Ctrl+Shift+Z` agissent sur le bon contexte actif
- le menu `Edit` expose l'etat enabled/disabled et les descriptions dynamiques
- toutes les surfaces editables listees plus haut passent par des commandes reversibles
- le screen editor garde ses capacites actuelles sans regression
- les manipulations viewport/gizmo et les editions multi-proprietes sont groupees proprement
- le material editor fonctionne avec un vrai cycle dirty/save/undo/redo
- le content browser supporte l'undo/redo de ses operations editables sans corruption du catalogue ou de la vue
- un build cible borne passe
- la doc est mise a jour

---

## Decoupage des taches

### ✅ Phase 1 - Socle global de l'historique

#### ✅ Tache 1.1 - Formaliser les abstractions generiques

**Objectif :** sortir du contrat `screen-only` et introduire un socle editeur reutilisable.

**A faire :**
1. Ajouter un contrat generique `IEditorCommand` avec `Execute`, `Undo`, `Description`.
2. Ajouter une stack generique `EditorHistoryStack` avec `CanUndo`, `CanRedo`, descriptions et evenement de changement.
3. Ajouter un `EditorCompositeCommand` ou equivalent batchable.
4. Ajouter un adaptateur temporaire pour reutiliser les commandes du screen editor sans tout casser d'un coup.

**Resultat attendu :**
- le screen editor et les autres domaines peuvent partager la meme infrastructure de base

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`

**Commit attendu :**
- `feat(editor-history): add generic undo redo abstractions`

**Commit realise :**
- `cf44741d` `feat(editor-history): add generic undo redo abstractions`

---

#### ✅ Tache 1.2 - Ajouter un service global route par contexte actif

**Objectif :** disposer d'un point d'entree unique pour l'historique de l'editeur.

**A faire :**
1. Ajouter `EditorHistoryService`.
2. Gerer une stack par contexte/document (`World`, chaque `UIScreen`, chaque `Material`, `ContentBrowser`).
3. Brancher la resolution du contexte actif sur `EditorContextService` et sur un contexte explicite pour le content browser.
4. Ajouter des APIs de clear ciblé lors d'un changement de projet ou fermeture de document.

**Resultat attendu :**
- `Game1` n'a plus besoin de connaitre une pile d'historique concrete par domaine

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`

**Commit attendu :**
- `feat(editor-history): add active-context history service`

**Commit realise :**
- `27262616` `feat(editor-history): add active-context history service`

---

#### ✅ Tache 1.3 - Ajouter transactions, coalescing et arbitrage input

**Objectif :** obtenir un comportement moderne pour typing, slider, drag et gizmo.

**A faire :**
1. Ajouter un mecanisme `BeginTransaction` / `CommitTransaction` / `CancelTransaction`.
2. Definir comment regrouper les changements de texte, numeriques et drag en une seule commande.
3. Definir la precedence entre l'undo local de `MGTextBox` et l'historique authoring global.
4. Documenter explicitement quels champs restent en undo local pur (search, filtre, etc.).

**Resultat attendu :**
- le comportement de l'historique est previsible et non pollue par les evenements bas niveau

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`

**Commit attendu :**
- `feat(editor-history): add transactions and input arbitration`

**Commit realise :**
- `7cbc7422` `feat(editor-history): add transactions and input arbitration`

---

### ✅ Phase 2 - Integrer le shell editeur

#### ✅ Tache 2.1 - Router le menu Edit et les raccourcis vers l'historique actif

**Objectif :** rendre `Undo/Redo` global et contextuel au document/contexte authoring actif.

**A faire :**
1. Remplacer le routage screen-only dans `Game1.ExecuteUndo/Redo`.
2. Supporter `Ctrl+Z`, `Ctrl+Y` et `Ctrl+Shift+Z`.
3. Mettre a jour `Edit > Undo/Redo` avec les descriptions dynamiques.
4. Ajouter ou mettre a jour des boutons toolbar visibles si cela reste leger et coherent avec l'UI actuelle.

**Resultat attendu :**
- le shell pilote le bon historique sans connaitre les details metier de chaque panel

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`
- verification manuelle courte du menu `Edit`

**Commit attendu :**
- `feat(editor-history): route shell undo redo through active context`

**Commit realise :**
- `6a1c217e` `feat(editor-history): route shell undo redo through active context`

---

#### ✅ Tache 2.2 - Ajouter dirty tracking et notifications globales

**Objectif :** garder les panels, tabs et sauvegardes coherents avec l'historique.

**A faire :**
1. Ajouter un service de dirty state par contexte/document.
2. Exposer des notifications apres `Execute`, `Undo`, `Redo`, `Clear`, `Save`.
3. Mettre a jour les titres d'onglets et/ou labels quand un document est dirty.
4. S'assurer que `Save` nettoie le dirty state sans purger l'historique.

**Resultat attendu :**
- le shell sait quels documents sont modifies et quand rafraichir les vues

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`

**Commit attendu :**
- `feat(editor-history): add dirty tracking and change notifications`

**Commit realise :**
- `85b42dcb` `feat(editor-history): add dirty tracking and change notifications`

---

### 🚧 Phase 3 - Migrer le screen editor sans regression

#### ✅ Tache 3.1 - Rebrancher les commandes UIScreen sur le nouveau socle

**Objectif :** conserver l'existant du screen editor tout en l'integrant au service global.

**A faire :**
1. Remplacer l'usage direct de `_screenCommandStack` dans `Game1` par le nouveau service.
2. Faire en sorte que chaque document screen ouvert ait sa propre stack.
3. Garder les commandes existantes (`SetPropertyCommand`, `AddNodeCommand`, `RemoveNodeCommand`, etc.).
4. Verifier que les refresh de preview/hierarchy/inspector restent corrects.

**Resultat attendu :**
- le screen editor devient un client du systeme global, pas une exception legacy

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`
- smoke manuel : add node / delete node / property edit / copy / paste / undo / redo

**Commit attendu :**
- `feat(screen-editor): connect ui screen history to global editor history`

**Commit realise :**
- `d51faedb` `feat(screen-editor): connect ui screen history to global editor history`

---

#### 🧪 Tache 3.2 - Regrouper preview drag et editions multi-proprietes

**Objectif :** corriger le point faible deja documente dans le screen editor (`R-06`).

**A faire :**
1. Faire qu'un drag de node ou resize genere une transaction unique.
2. Regrouper les series d'edits liees a une seule interaction utilisateur.
3. Verifier que toolbox, duplicate, cut/copy/paste restent des operations unitaires naturelles.
4. Mettre a jour la doc screen editor si le comportement utilisateur change legerement.

**Resultat attendu :**
- un `Ctrl+Z` annule une intention complete, pas un axe ou une valeur partielle

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`
- smoke manuel : drag preview, resize, multi-propriete, undo, redo

**Commit attendu :**
- `feat(screen-editor): group drag and multi-property edits`

**Commit realise :**
- `1dbaede9` `feat(screen-editor): group drag and multi-property edits`

---

### 🚧 Phase 4 - Couvrir le world editor

#### ✅ Tache 4.1 - Rendre undoable la hierarchy des entites

**Objectif :** couvrir les operations structurelles du monde.

**A faire :**
1. Introduire des commandes world pour add / delete / duplicate / rename.
2. Faire passer `EntitiesPanel` et `EditorWorldEditingService` par ces commandes.
3. Restaurer selection et tree state apres undo/redo.
4. Couvrir aussi la creation d'entites par drop d'assets dans le viewport si cela cree une entite authoring.

**Resultat attendu :**
- les operations structurelles world ne mutent plus le monde en direct sans historique

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`
- smoke manuel : add entity / duplicate / rename / delete / asset drop / undo / redo

**Commit attendu :**
- `feat(world-editor): add undo redo for entity hierarchy operations`

**Commit realise :**
- `54dc73d4` `feat(world-editor): add undo redo for entity hierarchy operations`

---

#### 🧪 Tache 4.2 - Rendre undoable l'inspector entite/composants

**Objectif :** couvrir les mutations faites depuis `EntityDetailsPanel`.

**A faire :**
1. Introduire des commandes pour rename entity, add component, remove component si expose, et changement de propriete composant.
2. Remplacer les `property.SetValue(...)` directs dans `ComponentEditorBase` par des bindings vers l'historique.
3. Gérer les cas `SceneComponent`, `RootComponent`, `AssetId`, `Vector3`, `Color`, enums et numeriques.
4. Garder le refresh des editors et de la selection stable.

**Resultat attendu :**
- les edits d'inspector world deviennent reversibles et propres

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`
- smoke manuel : rename entity, add component, edit several component fields, undo, redo

**Commit attendu :**
- `feat(world-editor): add undo redo for entity inspector edits`

**Commit realise :**
- `e5a647c0` `feat(world-editor): add undo redo for entity inspector edits` + `ece7a88a` `fix(world-editor): route camera inspector edits through history`

---

#### 🧪 Tache 4.3 - Rendre undoable le gizmo et les edits viewport

**Objectif :** couvrir translate / rotate / scale et autres interactions directes du viewport.

**A faire :**
1. Capturer l'etat initial/final d'une manipulation gizmo.
2. Generer une commande unique par drag gizmo.
3. Couvrir translate, rotate, non-uniform scale et uniform scale.
4. Verifier que la selection, l'overlay et le gizmo restent synchronises apres undo/redo.

**Resultat attendu :**
- les manipulations 3D directes sont annulees proprement comme dans un editeur moderne

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`
- smoke manuel : translate / rotate / scale / undo / redo

**Commit attendu :**
- `feat(world-editor): add undo redo for viewport gizmo edits`

**Commit realise :**
- `bf093e10` `feat(world-editor): add undo redo for viewport gizmo edits`

---

### 🧪 Phase 5 - Couvrir le material editor

#### ✅ Tache 5.1 - Introduire une session d'authoring material avec dirty state

**Objectif :** supprimer la sauvegarde immediate a chaque edit et revenir a un flux moderne.

**A faire :**
1. Decoupler la mutation du `MaterialAsset` en memoire de `EditorAssetWriterService.SaveDocument(...)`.
2. Ajouter un dirty state par material ouvert.
3. Garder le preview refresh en direct sans ecrire sur disque a chaque changement.
4. Clarifier quand le hot reload runtime doit se produire : a l'undo/redo local, a la sauvegarde, ou aux deux selon le design retenu.

**Resultat attendu :**
- l'undo/redo material devient coherent et la sauvegarde retrouve un role explicite

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`

**Commit attendu :**
- `feat(material-editor): add material authoring session and dirty tracking`

**Commit realise :**
- `098d9416` `feat(material-editor): add material authoring session and dirty tracking`

---

#### 🧪 Tache 5.2 - Encapsuler les edits material en commandes

**Objectif :** couvrir toutes les mutations faites dans `MaterialAssetInspectorPanel`.

**A faire :**
1. Ajouter des commandes pour set / reset / restore inherited value des proprietes material.
2. Brancher tous les editors (`bool`, `slider`, `numeric`, `color`, `vector3`, `texture`, `enum`, `text`).
3. S'assurer que chaque material ouvert a sa propre stack d'historique.
4. Garder `MaterialHierarchyPanel`, preview et inspector synchronises.

**Resultat attendu :**
- toutes les actions authoring du material inspector deviennent undoables

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`
- smoke manuel : edit float, color, texture, enum, reset, undo, redo

**Commit attendu :**
- `feat(material-editor): add undo redo for material property edits`

**Commit realise :**
- `a127a653` `feat(material-editor): add undo redo for material property edits`

---

#### 🧪 Tache 5.3 - Rebrancher Save et hot reload sur la nouvelle session

**Objectif :** finaliser le flux material sans regressions de preview ou de persistance.

**A faire :**
1. Faire que `Save` flush uniquement les materials dirty concernes.
2. Verifier l'integration avec `EditorAssetWriterService.AssetSaved` et les reloads d'inspectors ouverts.
3. Eviter les rechargements parasites d'un material en cours d'edition lors d'un undo/redo local.
4. Stabiliser le comportement en cas de save externe ou hot reload.

**Resultat attendu :**
- le material editor a un cycle authoring/save/hot reload robuste

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`
- smoke manuel : edit material, save, undo, redo, reopen panel

**Commit attendu :**
- `feat(material-editor): connect save and hot reload to material history`

**Commit realise :**
- `dd93a324` `feat(material-editor): connect save and hot reload to material history`

---

### 🧪 Phase 6 - Couvrir le content browser

#### 🧪 Tache 6.1 - Ajouter une infrastructure de file operations reversibles

**Objectif :** rendre undoables les operations du content browser sans casser le file watcher ni le catalogue.

**A faire :**
1. Introduire des commandes pour create folder, rename, move, copy/duplicate, paste, import, delete.
2. Ajouter une strategie de staging/trash editor pour les deletes undoables.
3. Ne jamais toucher aux fichiers source externes lors d'un undo d'import ; seul le contenu copie dans le projet doit etre retire/restaure.
4. Reutiliser `FileOperationService` en lui ajoutant les primitives necessaires plutot que dupliquer la logique fichier.

**Resultat attendu :**
- les operations du content browser deviennent reversibles et sures

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`

**Commit attendu :**
- `feat(content-browser): add reversible file operation infrastructure`

**Commit realise :**
- `47cf9525` `feat(content-browser): add reversible file operation infrastructure`

---

#### 🧪 Tache 6.2 - Brancher `ContentBrowserPanel` sur l'historique global

**Objectif :** remplacer les appels directs par des commandes undoables.

**A faire :**
1. Faire passer `new folder`, `rename`, `delete`, `duplicate`, `move`, `paste`, `import` par le service global.
2. Restaurer la selection et le dossier courant apres undo/redo.
3. Coordonner correctement `FileOperationService`, le watcher et le refresh de l'arbre.
4. Donner au content browser un contexte d'historique stable, meme s'il n'est pas un document tab classique.

**Resultat attendu :**
- le content browser se comporte comme un panel editable de premier rang dans le systeme global

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`
- smoke manuel : new folder, rename, duplicate, move, delete, import, undo, redo

**Commit attendu :**
- `feat(content-browser): add undo redo for browser actions`

**Commit realise :**
- `72139213` `feat(content-browser): add undo redo for browser actions`

---

### 🧪 Phase 7 - Stabilisation, validation et doc

#### 🧪 Tache 7.1 - Optimiser les refresh et la restauration de selection

**Objectif :** eviter les refresh trop larges et les regressions UX apres undo/redo.

**A faire :**
1. Identifier les refresh minimums par domaine au lieu de full rebuild partout.
2. Restaurer selection, focus et panel actif quand c'est attendu.
3. Verifier que le shell contextuel (`Hierarchy`, `Inspector`, `Toolbox`) reste coherent apres undo/redo.
4. Eviter de polluer les hot paths Update/Draw avec des allocations ou du travail inutile.

**Resultat attendu :**
- l'undo/redo est rapide, stable et lisible pour l'utilisateur

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`
- smoke manuel croise world / screen / material / content browser

**Commit attendu :**
- `fix(editor-history): stabilize panel refresh after undo redo`

**Commit realise :**
- `96bf0193` `fix(editor-history): stabilize panel refresh after undo redo`

---

#### 🧪 Tache 7.2 - Ajouter une validation smoke ciblee

**Objectif :** prouver que la feature marche sur toutes les surfaces editables.

**A faire :**
1. Ajouter si possible une automation smoke ou un scenario manuel scriptable couvrant les cas principaux.
2. Verifier au minimum :
   - world hierarchy
   - world inspector
   - world viewport gizmo
   - UI screen hierarchy / inspector / toolbox / preview
   - material inspector
   - content browser
3. Verifier aussi le changement de contexte actif entre documents et panels.
4. Verifier `Save`, dirty state, undo, redo, fermeture/reouverture de documents.

**Resultat attendu :**
- la fonctionnalite est testee de bout en bout sur les surfaces critiques

**Validation ciblee :**
- `dotnet build CasaEngine.Editor.MonoGame.sln`
- campagne smoke ciblee et bornee documentee dans le commit ou sous cette tache

**Commit attendu :**
- `test(editor-history): add undo redo smoke coverage`

**Commit realise :**
- `b54de9aa` `test(editor-history): add undo redo smoke coverage`

---

#### ✅ Tache 7.3 - Mettre a jour la documentation

**Objectif :** documenter l'architecture et le workflow utilisateur.

**A faire :**
1. Documenter le nouveau systeme d'historique global.
2. Documenter le cycle dirty/save pour les materials et autres documents.
3. Documenter les conventions de grouping et les zones volontairement hors historique.
4. Ajouter une note pour les futurs panels editables expliquant comment s'enregistrer dans `EditorHistoryService`.

**Resultat attendu :**
- un futur agent peut brancher un nouveau panel editable sans reinventer l'architecture

**Validation ciblee :**
- revue rapide de coherence + `dotnet build CasaEngine.Editor.MonoGame.sln`

**Commit attendu :**
- `docs(editor-history): document global undo redo workflow`

**Commit realise :**
- `a renseigner au commit suivant`

---

## Risques et points d'attention

- Le material editor auto-save actuellement a chaque changement : ne pas essayer d'ajouter l'undo/redo sans traiter ce point a la racine.
- Le content browser fait des operations destructives directes : la suppression undoable doit passer par une strategie reversible.
- Les champs texte authoring et les champs texte utilitaires ne doivent pas partager le meme comportement d'undo.
- Les manipulations gizmo doivent etre transactionnelles, sinon l'historique sera inutilisable.
- Le screen editor a deja une base solide : il faut la migrer, pas la contourner.

---

## Ordre d'execution recommande

1. Phase 1
2. Phase 2
3. Phase 3
4. Phase 4
5. Phase 5
6. Phase 6
7. Phase 7

Ne pas attaquer la Phase 6 avant d'avoir stabilise le socle global et le dirty tracking.