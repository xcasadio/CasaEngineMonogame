## Contexte

Objectif : créer un **éditeur de screen MGUI** intégré à **CasaEngine**.

Le screen editor doit permettre :
- d’éditer un écran UI MGUI comme un asset CasaEngine,
- d’avoir une preview temps réel,
- d’éditer la hiérarchie des contrôles,
- d’éditer les propriétés,
- de sauvegarder/recharger proprement,
- de préparer ensuite l’édition visuelle complète (sélection dans la surface, drag & drop, resize, guides, etc.).

Contraintes d’architecture :
- **ne pas** éditer directement les instances runtime MGUI comme source de vérité,
- utiliser un **document model** dédié pour l’édition,
- garder la **sérialisation XAML** comme format principal,
- faire coexister le futur éditeur avec l’éditeur CasaEngine existant,
- livrer en petites tâches, avec un commit après chaque tâche.

Le repo MGUI fournit déjà :
- un framework UI MonoGame proche de WPF,
- un moteur de layout,
- du data binding,
- de nombreux contrôles,
- un contrôle `MGXAMLDesigner`.

Le repo CasaEngineMonogame contient déjà :
- un éditeur WPF/MonoGame,
- plusieurs couches `CasaEngine.Editor`, `CasaEngine.EditorServices`, `CasaEngine.EditorUI`,
- une architecture d’éditeur à faire évoluer vers MGUI.

## Règles de travail de l’agent

- Faire **une seule tâche à la fois**.
- À la fin de **chaque tâche**, faire un **commit git**.
- Mettre à jour ce fichier pour refléter le statut réel des tâches.
- Ne jamais marquer une tâche comme terminée si elle ne l’est pas complètement.
- Si une tâche révèle un refactor nécessaire non prévu, l’ajouter dans la section **Tâches découvertes**.
- Préserver les responsabilités architecturales : **Document / Runtime Preview / Editor UI / Serialization / Commands**.

## Légende des statuts

- ⚪ À faire
- 🟡 En cours
- 🟢 Terminé
- 🔴 Bloqué
- ⏸️ Reporté
- ❓ À clarifier

---

## Phase 0 - Cadrage et fondations

### 0.1 Créer le dossier de documentation de feature
- Statut : 🟢
- Objectif : créer un emplacement stable pour la doc du screen editor.
- Actions :
  - créer un dossier du type `docs/ui-screen-editor/`
  - y placer ce fichier
  - ajouter un `README.md` de feature avec objectif, périmètre, non-objectifs
- Livrable :
  - documentation initiale structurée
- Commit :
  - `docs: initialize screen editor feature documentation`

### 0.2 Cartographier les points d’intégration MGUI et CasaEngine
- Statut : 🟢
- Objectif : identifier où brancher l’éditeur sans casser l’architecture existante.
- Actions :
  - recenser les projets/classes actuels liés à l’éditeur, aux assets, aux vues dockables, au chargement UI
  - recenser côté MGUI les classes de chargement XAML, runtime UI, designer existant
  - produire une note `architecture-entry-points.md`
- Livrable :
  - mapping clair des points d’entrée
- Commit :
  - `docs: map screen editor integration points`

### 0.3 Définir l’architecture cible du screen editor
- Statut : 🟢
- Objectif : verrouiller la séparation des couches avant de coder.
- Actions :
  - créer un document `architecture.md`
  - définir explicitement les couches :
    - Screen Asset
    - Screen Document Model
    - XAML Serializer / Parser
    - Runtime Preview Adapter
    - Editor Services
    - Editor UI
  - définir les dépendances autorisées entre couches
- Livrable :
  - schéma d’architecture textuel validé
- Commit :
  - `docs: define target architecture for screen editor`

---

## Phase 1 - Asset et document model

### 1.1 Introduire le type d’asset UIScreen
- Statut : 🟢
- Objectif : disposer d’un asset officiel côté CasaEngine.
- Actions :
  - créer un type `UIScreenAsset`
  - ajouter identifiant, nom, chemin source XAML, métadonnées minimales
  - prévoir l’extensibilité pour thème / résolution preview / ressources
- Livrable :
  - asset compilable et intégré au système d’assets
- Commit :
  - `feat: add UIScreen asset type`

### 1.2 Ajouter un document model indépendant du runtime
- Statut : 🟢
- Objectif : avoir une source de vérité éditable.
- Actions :
  - créer des types du genre :
    - `UIScreenDocument`
    - `UIScreenNode`
    - `UIScreenPropertyValue`
  - séparer structure, propriétés, enfants, métadonnées d’édition
  - ne mettre aucune dépendance MGUI runtime directe dans ce modèle
- Livrable :
  - modèle de document sérialisable/testable
- Commit :
  - `feat: add UI screen document model`

### 1.3 Définir les métadonnées de design-time
- Statut : 🟢
- Objectif : préparer sélection, toolbox, inspector, noms, ids.
- Actions :
  - ajouter sur le modèle :
    - id stable
    - type du contrôle
    - nom optionnel
    - flags design-time
  - prévoir des annotations non sauvegardées si nécessaire
- Livrable :
  - document model enrichi pour l’édition
- Commit :
  - `feat: add design-time metadata to screen document`

### 1.4 Ajouter des tests unitaires sur le document model
- Statut : 🟢
- Objectif : stabiliser tôt la structure centrale.
- Actions :
  - tester création de nœuds
  - test parent/enfants
  - test propriétés
  - test ids stables
- Livrable :
  - suite de tests de base
- Commit :
  - `test: cover screen document model basics`

---

## Phase 2 - Sérialisation XAML

### 2.1 Créer le parser XAML -> document
- Statut : 🟢
- Objectif : pouvoir ouvrir un screen existant.
- Actions :
  - créer un service du type `UIScreenXamlParser`
  - convertir le XAML MGUI en `UIScreenDocument`
  - gérer les cas minimaux :
    - type racine
    - propriétés simples
    - contenu enfant
    - collections d’enfants
- Livrable :
  - ouverture d’un XAML simple vers document model
- Commit :
  - `feat: add XAML to screen document parser`

### 2.2 Créer le serializer document -> XAML
- Statut : 🟢
- Objectif : pouvoir sauvegarder le document.
- Actions :
  - créer `UIScreenXamlSerializer`
  - convertir le document model vers un XAML MGUI valide
  - garantir un output déterministe
- Livrable :
  - sauvegarde minimale fonctionnelle
- Commit :
  - `feat: add screen document to XAML serializer`

### 2.3 Ajouter des tests de round-trip
- Statut : 🟢
- Objectif : garantir la stabilité ouverture/sauvegarde.
- Actions :
  - parser un XAML
  - sérialiser
  - reparser
  - vérifier équivalence structurelle
- Livrable :
  - tests de round-trip
- Commit :
  - `test: add screen XAML round-trip coverage`

### 2.4 Documenter les limites de la v1 du parser
- Statut : ⚪
- Objectif : rendre explicite ce qui n’est pas encore supporté.
- Actions :
  - lister les éléments non encore gérés :
    - styles complexes
    - resources avancées
    - bindings complexes
    - templates
  - écrire `xaml-support-matrix.md`
- Livrable :
  - matrice de support claire
- Commit :
  - `docs: add screen editor XAML support matrix`

---

## Phase 3 - Runtime preview

### 3.1 Créer un service de preview runtime
- Statut : ⚪
- Objectif : instancier un document en arbre MGUI visualisable.
- Actions :
  - créer `UIScreenPreviewBuilder` ou équivalent
  - convertir `UIScreenDocument` vers contrôles runtime MGUI
  - gérer les contrôles de base en priorité
- Livrable :
  - preview runtime d’un screen simple
- Commit :
  - `feat: add runtime preview builder for UI screens`

### 3.2 Ajouter un host de preview dans CasaEngine
- Statut : ⚪
- Objectif : afficher le screen dans un éditeur dédié.
- Actions :
  - créer un host/editor view pour la preview
  - intégrer le rendu MGUI dans l’éditeur CasaEngine
  - permettre chargement d’un `UIScreenAsset`
- Livrable :
  - onglet d’édition affichant la preview
- Commit :
  - `feat: add UI screen preview host in editor`

### 3.3 Gérer le reload de preview
- Statut : ⚪
- Objectif : rafraîchir proprement la preview à chaque changement.
- Actions :
  - définir une stratégie simple : rebuild total v1
  - éviter les fuites et handlers persistants
  - ajouter logs/diagnostics en cas d’échec
- Livrable :
  - preview rafraîchissable de façon fiable
- Commit :
  - `feat: support preview reload for UI screen editor`

### 3.4 Ajouter gestion d’erreur et fallback visuel
- Statut : ⚪
- Objectif : ne pas casser l’éditeur si le screen est invalide.
- Actions :
  - capturer erreurs parser / build / preview
  - afficher un panneau d’erreur lisible dans l’éditeur
  - conserver le document chargé même si la preview échoue
- Livrable :
  - éditeur robuste face aux erreurs
- Commit :
  - `feat: add resilient error handling to UI screen preview`

---

## Phase 4 - Session d’édition

### 4.1 Introduire une EditorSession dédiée
- Statut : ⚪
- Objectif : centraliser l’état d’édition.
- Actions :
  - créer `UIScreenEditorSession`
  - y stocker :
    - asset courant
    - document courant
    - état dirty
    - sélection
    - preview
  - interdire les accès dispersés à l’état
- Livrable :
  - session d’édition centralisée
- Commit :
  - `feat: add UI screen editor session`

### 4.2 Ajouter le cycle open/save/reload
- Statut : ⚪
- Objectif : rendre l’éditeur réellement utilisable.
- Actions :
  - ouvrir depuis asset
  - sauvegarder vers XAML
  - recharger depuis disque
  - marquer dirty proprement
- Livrable :
  - cycle d’édition complet minimal
- Commit :
  - `feat: add open save reload workflow for UI screens`

### 4.3 Ajouter tests sur la session
- Statut : ⚪
- Objectif : sécuriser les transitions d’état.
- Actions :
  - tests dirty / save / reload
  - tests erreurs de chargement
  - tests conservation de sélection si applicable
- Livrable :
  - couverture de base de la session
- Commit :
  - `test: cover UI screen editor session workflow`

---

## Phase 5 - Hiérarchie et sélection

### 5.1 Créer le service de sélection
- Statut : ⚪
- Objectif : unifier la sélection entre hiérarchie, inspector et preview.
- Actions :
  - créer `UIScreenSelectionService`
  - stocker la sélection par `DocumentNodeId`
  - notifier les vues
- Livrable :
  - service de sélection central
- Commit :
  - `feat: add UI screen selection service`

### 5.2 Créer le panneau hiérarchie
- Statut : ⚪
- Objectif : naviguer dans l’arbre visuel/document.
- Actions :
  - afficher l’arbre du document
  - afficher type + nom/id
  - synchroniser clic hiérarchie -> sélection
- Livrable :
  - hiérarchie consultable et synchronisée
- Commit :
  - `feat: add UI screen hierarchy panel`

### 5.3 Synchroniser preview -> sélection
- Statut : ⚪
- Objectif : sélectionner depuis la surface de preview.
- Actions :
  - mapping runtime control -> document node
  - clic dans la preview = sélection du nœud
  - surbrillance simple du contrôle sélectionné
- Livrable :
  - sélection bidirectionnelle preview/hierarchy
- Commit :
  - `feat: sync preview picking with screen selection`

### 5.4 Ajouter suppression de nœud via hiérarchie
- Statut : ⚪
- Objectif : première édition structurelle utile.
- Actions :
  - suppression sécurisée d’un nœud
  - prise en compte du parent
  - refresh preview
- Livrable :
  - suppression d’éléments fonctionnelle
- Commit :
  - `feat: allow deleting UI nodes from hierarchy`

---

## Phase 6 - Inspector de propriétés

### 6.1 Définir un système de descripteurs de propriétés
- Statut : ⚪
- Objectif : éviter un inspector codé en dur contrôle par contrôle.
- Actions :
  - créer des métadonnées de propriétés :
    - nom
    - catégorie
    - type
    - valeur par défaut
    - éditable ou non
  - permettre extensibilité par type de contrôle
- Livrable :
  - base de réflexion/métadonnées pour l’inspector
- Commit :
  - `feat: add property descriptors for UI screen editor`

### 6.2 Implémenter l’inspector minimal
- Statut : ⚪
- Objectif : modifier les propriétés simples.
- Actions :
  - afficher les propriétés du nœud sélectionné
  - supporter au minimum :
    - Width / Height
    - Margin / Padding
    - HorizontalAlignment / VerticalAlignment
    - Text / Name
  - écrire dans le document model
- Livrable :
  - inspector minimal exploitable
- Commit :
  - `feat: add basic property inspector for UI screens`

### 6.3 Rafraîchir preview après changement de propriété
- Statut : ⚪
- Objectif : boucle d’édition temps réel.
- Actions :
  - brancher propriété modifiée -> dirty -> refresh preview
  - fiabiliser la synchronisation
- Livrable :
  - édition de propriétés en live
- Commit :
  - `feat: refresh preview after property edits`

### 6.4 Ajouter validateurs et messages d’erreur de propriété
- Statut : ⚪
- Objectif : éviter les valeurs invalides silencieuses.
- Actions :
  - validation de types
  - validation de format
  - affichage d’erreur contextualisé
- Livrable :
  - saisie plus robuste dans l’inspector
- Commit :
  - `feat: validate property editing in UI screen inspector`

---

## Phase 7 - Toolbox et création de contrôles

### 7.1 Créer le registre des contrôles éditables
- Statut : ⚪
- Objectif : contrôler quels types sont disponibles dans l’éditeur.
- Actions :
  - créer un catalogue des contrôles autorisés
  - associer :
    - type runtime
    - nom affiché
    - catégorie
    - valeurs par défaut
- Livrable :
  - registre central des contrôles de toolbox
- Commit :
  - `feat: add editable UI control registry`

### 7.2 Implémenter la toolbox
- Statut : ⚪
- Objectif : permettre l’ajout de nouveaux contrôles.
- Actions :
  - panneau toolbox avec groupes
  - sélection d’un contrôle à créer
  - première version via bouton “add child”
- Livrable :
  - toolbox fonctionnelle
- Commit :
  - `feat: add toolbox for UI screen editor`

### 7.3 Ajouter création de nœud dans le document
- Statut : ⚪
- Objectif : insertion structurée dans le screen.
- Actions :
  - créer factory de nœuds
  - insérer dans parent sélectionné
  - gérer contenu unique vs collection d’enfants
- Livrable :
  - ajout de contrôles depuis toolbox
- Commit :
  - `feat: support adding controls to UI document`

### 7.4 Ajouter règles d’insertion par parent
- Statut : ⚪
- Objectif : éviter des arbres invalides.
- Actions :
  - définir contraintes par type de parent
  - gérer parents content-control vs panels
  - bloquer/guider les insertions invalides
- Livrable :
  - insertion cohérente selon le layout
- Commit :
  - `feat: enforce parent child insertion rules in screen editor`

---

## Phase 8 - Commandes d’édition et undo/redo

### 8.1 Introduire une infrastructure de commandes
- Statut : ⚪
- Objectif : unifier les modifications d’édition.
- Actions :
  - créer interface de commande
  - supporter execute / undo / redo
  - stocker stack de commandes dans la session
- Livrable :
  - infrastructure de base des commandes
- Commit :
  - `feat: add command stack for UI screen editor`

### 8.2 Passer les modifications de propriétés par commandes
- Statut : ⚪
- Objectif : rendre l’inspector annulable.
- Actions :
  - encapsuler changements de propriétés en commandes
- Livrable :
  - undo/redo sur propriétés
- Commit :
  - `feat: add undo redo for property edits`

### 8.3 Passer les modifications structurelles par commandes
- Statut : ⚪
- Objectif : rendre add/delete annulables.
- Actions :
  - commandes add node / remove node / reparent v1 si prêt
- Livrable :
  - undo/redo structurel
- Commit :
  - `feat: add undo redo for UI tree changes`

### 8.4 Ajouter UI pour undo/redo
- Statut : ⚪
- Objectif : exposer la fonctionnalité dans l’éditeur.
- Actions :
  - boutons, raccourcis clavier, état enabled/disabled
- Livrable :
  - UX minimale d’historique
- Commit :
  - `feat: expose undo redo actions in screen editor`

---

## Phase 9 - Surface de design

### 9.1 Ajouter overlay de sélection
- Statut : ⚪
- Objectif : rendre la sélection visible sur la preview.
- Actions :
  - dessiner rectangle de sélection
  - afficher bounds du contrôle sélectionné
- Livrable :
  - retour visuel clair dans la surface
- Commit :
  - `feat: add selection overlay on UI preview`

### 9.2 Ajouter hit testing design-time fiable
- Statut : ⚪
- Objectif : cliquer précisément le bon contrôle.
- Actions :
  - fiabiliser picking
  - gérer imbrication des contrôles
- Livrable :
  - sélection précise dans la surface
- Commit :
  - `feat: improve design-time hit testing for UI screens`

### 9.3 Ajouter déplacement simple dans la surface
- Statut : ⚪
- Objectif : première édition visuelle.
- Actions :
  - ne supporter au début que les cas compatibles
  - convertir mouvement en propriétés pertinentes
  - ne pas casser les containers layout-driven
- Livrable :
  - déplacement simple ou contrôlé
- Commit :
  - `feat: add basic visual move support for UI controls`

### 9.4 Ajouter resize simple
- Statut : ⚪
- Objectif : éditer la taille par poignées.
- Actions :
  - handles minimales
  - écriture sur Width/Height quand applicable
- Livrable :
  - resize de base
- Commit :
  - `feat: add basic visual resize support for UI controls`

---

## Phase 10 - Productivité

### 10.1 Ajouter duplication
- Statut : ⚪
- Objectif : accélérer l’édition.
- Actions :
  - dupliquer nœud sélectionné
  - ids régénérés
- Livrable :
  - duplication fonctionnelle
- Commit :
  - `feat: add duplicate action for UI nodes`

### 10.2 Ajouter copier/coller
- Statut : ⚪
- Objectif : améliorer le workflow.
- Actions :
  - sérialiser sous-arbre temporaire
  - coller dans un parent compatible
- Livrable :
  - copier/coller minimal
- Commit :
  - `feat: add copy paste for UI nodes`

### 10.3 Ajouter preview multi-résolutions
- Statut : ⚪
- Objectif : tester l’adaptation du screen.
- Actions :
  - presets de résolution
  - refresh preview selon preset
- Livrable :
  - simulation de tailles d’écran
- Commit :
  - `feat: add screen preview resolution presets`

### 10.4 Ajouter grille/guides optionnels
- Statut : ⚪
- Objectif : préparer une meilleure édition visuelle.
- Actions :
  - grille visuelle optionnelle
  - guides simples
- Livrable :
  - outils visuels de placement
- Commit :
  - `feat: add optional guides and grid to screen editor`

---

## Phase 11 - Design-time avancé

### 11.1 Introduire un mode design-time explicite
- Statut : ⚪
- Objectif : empêcher l’exécution de logique runtime non voulue.
- Actions :
  - flag global design mode
  - branchements conditionnels si nécessaire
- Livrable :
  - environnement d’édition sécurisé
- Commit :
  - `feat: add explicit design-time mode for UI preview`

### 11.2 Ajouter données mockées pour preview
- Statut : ⚪
- Objectif : voir un écran utile même sans gameplay.
- Actions :
  - faux data context
  - valeurs d’exemple pour textes/listes
- Livrable :
  - preview design-time enrichie
- Commit :
  - `feat: add mock data support for UI design-time preview`

### 11.3 Documenter les conventions de screens CasaEngine
- Statut : ⚪
- Objectif : normaliser les futurs écrans.
- Actions :
  - écrire conventions :
    - structure des assets
    - nommage
    - thèmes
    - ressources
    - bindings
- Livrable :
  - guide d’usage
- Commit :
  - `docs: add CasaEngine UI screen authoring conventions`

---

## Phase 12 - Stabilisation

### 12.1 Créer une suite de screens exemples
- Statut : ⚪
- Objectif : tester plusieurs cas réels.
- Actions :
  - créer au moins :
    - menu principal
    - popup simple
    - inventaire mocké
    - panneau d’options
- Livrable :
  - corpus de validation
- Commit :
  - `test: add sample UI screens for editor validation`

### 12.2 Vérifier performance et rebuilds excessifs
- Statut : ⚪
- Objectif : éviter un éditeur trop lent.
- Actions :
  - mesurer temps de reload preview
  - identifier allocations excessives
  - documenter optimisations futures
- Livrable :
  - note de perf initiale
- Commit :
  - `perf: assess UI screen editor preview performance`

### 12.3 Créer la liste de refactors post-v1
- Statut : ⚪
- Objectif : préparer les améliorations suivantes.
- Actions :
  - générer `SCREEN_EDITOR_REFACTOR_TASKS.md`
  - y mettre :
    - incremental preview update
    - support avancé bindings/styles/resources
    - drag & drop avancé
    - templates/components réutilisables
- Livrable :
  - backlog post-v1 priorisé
- Commit :
  - `docs: add post-v1 refactor backlog for screen editor`

---

## Tâches découvertes

Ajouter ici toute nouvelle tâche identifiée pendant l’implémentation.

- Statut : ⚪
- Description : _à compléter_

---

## Journal des commits

Ajouter une ligne après chaque tâche terminée.

- 🟢 0.1 `docs: initialize screen editor feature documentation`
- 🟢 0.2 `docs: map screen editor integration points`
- 🟢 0.3 `docs: define target architecture for screen editor`
- 🟢 1.1 `feat: add UIScreen asset type`
- 🟢 1.2 `feat: add UI screen document model`
- 🟢 1.3 `feat: add design-time metadata to screen document`
- 🟢 1.4 `test: cover screen document model basics`
- 🟢 2.1 `feat: add XAML to screen document parser`
- 🟢 2.2 `feat: add screen document to XAML serializer`
- 🟢 2.3 `test: add screen XAML round-trip coverage`

---

## Notes importantes pour l’agent

- Toujours privilégier une architecture où :
  - le **document model** est la source de vérité,
  - la **preview runtime** est reconstruite depuis le document,
  - l’**inspector** et la **hiérarchie** modifient le document,
  - la **sérialisation XAML** est pilotée par le document.
- Ne pas coupler l’éditeur à un seul screen concret.
- Garder les APIs testables.
- Favoriser des petites PR/commits lisibles.
- Mettre à jour l’icône de statut de la tâche avant de passer à la suivante.
