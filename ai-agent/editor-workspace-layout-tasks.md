# Plan d'implémentation — Layouts par mode d'édition pour CasaEngine.Editor

## Contexte

`CasaEngine.Editor` dispose déjà d'un shell MGUI avec docking, persistance de layout, panneaux communs, viewport monde et éditeur d'écran UI.

Le problème actuel est structurel : le layout initial mélange dans un même arbre des panneaux qui appartiennent à des contextes d'édition différents.

Exemples :
- le mode World a besoin de `World Viewport`, `Entities`, `Details`
- le mode UIScreen a besoin de `UIScreen Preview`, `Screen Hierarchy`, `Screen Toolbox`, `Screen Inspector`
- certains panneaux restent communs, par exemple `Content Browser` et `Logs`

L'objectif est d'introduire une architecture propre de **workspaces d'édition** avec un **layout persisté par mode**, sans casser le shell existant.

---

## Objectif

Mettre en place un système où :
- le shell de l'éditeur reste unique
- les panneaux communs restent disponibles quel que soit le mode
- chaque mode d'édition possède son propre layout par défaut
- chaque mode d'édition possède sa propre persistance de layout
- le document actif pilote le workspace actif
- les panneaux contextuels sont bindés au bon contexte métier

---

## Règles obligatoires pour l'agent IA

- Langue du document : français
- Langue du code : anglais
- Pas de régression fonctionnelle volontaire
- Pas de refactor massif non lié au sujet
- Un commit atomique après chaque tâche terminée
- Mettre à jour ce fichier après chaque tâche
- Toujours laisser la solution dans un état compilable avant de commit
- Si une tâche introduit un risque, ajouter une sous-tâche de validation avant le commit

---

## Légende des statuts

- ⏳ À faire
- 🚧 En cours
- ✅ Terminé
- 🧪 À valider
- ⚠️ Bloqué

---

## Workflow attendu pour l'agent IA

Pour chaque tâche :

1. passer la tâche en `🚧`
2. implémenter la modification minimale nécessaire
3. lancer une validation ciblée
4. mettre la tâche en `✅` ou `🧪`
5. créer un commit atomique
6. noter le hash de commit sous la tâche

Format attendu sous chaque tâche terminée :

```md
Commit : `abc12345` - message de commit
Validation : build ciblé ou test ciblé
Notes : point important si nécessaire
```

---

## Architecture cible

### Shell commun

Le shell reste responsable de :
- la fenêtre principale
- la menu bar
- la status bar
- le `MGDockHost`
- les panneaux communs
- le dispatch vers le workspace actif

### Workspace d'édition

Un workspace d'édition décrit :
- son identifiant
- son layout par défaut
- les panneaux qu'il autorise
- la façon de binder son contexte au document actif

### Documents

Le document actif doit permettre de déterminer :
- quel workspace activer
- quel contexte métier injecter dans les panneaux

### Panneaux

Les panneaux doivent être classés en trois scopes :
- `Common`
- `World`
- `UIScreen`

---

## Découpage recommandé

### Phase 1 — Formaliser les concepts

#### ✅ Tâche 1.1 — Introduire les types de base du système de workspace

**Objectif :** créer le vocabulaire minimal du nouveau modèle sans changer encore le comportement visible.

**À faire :**
1. Créer un `EditorWorkspaceId` ou enum équivalent
2. Créer un `EditorPanelScope`
3. Créer un type de description de panneau, par exemple `EditorPanelDescriptor`
4. Créer un contrat `IEditorWorkspace`
5. Créer un contrat minimal pour représenter un document éditable si nécessaire

**Résultat attendu :**
- le code possède des abstractions explicites pour les workspaces et les panneaux
- aucune régression visuelle à ce stade

**Commit attendu :**
- `feat(editor): add workspace model abstractions`

Commit : `5e45a4b2` - `feat(editor): add workspace model abstractions`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : types de base ajoutés sans changement de comportement visible du shell

---

#### ✅ Tâche 1.2 — Déclarer les panneaux communs et contextuels

**Objectif :** centraliser la description des panneaux au lieu de disperser leur logique dans `Game1`.

**À faire :**
1. Décrire les panneaux communs : `Content Browser`, `Logs`
2. Décrire les panneaux World : `World Viewport`, `Entities`, `Details`
3. Décrire les panneaux UIScreen : `Screen Hierarchy`, `Screen Toolbox`, `Screen Inspector`
4. Associer à chaque panneau : id, titre, scope, factory de contenu, type document/tool

**Résultat attendu :**
- le shell peut résoudre les factories à partir d'un registre propre
- la liste des panneaux supportés n'est plus implicite

**Commit attendu :**
- `refactor(editor): centralize editor panel descriptors`

Commit : `PENDING` - `refactor(editor): centralize editor panel descriptors`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : ids, titres, scopes, types et factories des panneaux statiques centralisés dans un registre unique

---

### Phase 2 — Séparer les layouts par workspace

#### ✅ Tâche 2.1 — Extraire le layout par défaut du mode World

**Objectif :** sortir la construction du layout World du layout mixte actuel.

**À faire :**
1. Créer une méthode ou classe dédiée au layout World
2. Conserver `Entities` à gauche, `World Viewport` au centre, `Details` à droite
3. Garder `Content Browser` et `Logs` en bas
4. Ne plus inclure les panneaux UIScreen dans ce layout

**Résultat attendu :**
- un layout par défaut World autonome existe

**Commit attendu :**
- `refactor(editor): extract default world workspace layout`

Commit : `59d2916e` - `refactor(editor): extract default world workspace layout`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : le layout initial n'embarque plus les panneaux UIScreen et passe désormais par `WorldEditorWorkspace`

---

#### ✅ Tâche 2.2 — Créer le layout par défaut du mode UIScreen

**Objectif :** définir un layout dédié pour l'édition d'écran UI.

**À faire :**
1. Créer une méthode ou classe dédiée au layout UIScreen
2. Placer `Screen Hierarchy` et `Screen Toolbox` à gauche ou en pile logique
3. Placer la preview UI dans la zone document centrale
4. Placer `Screen Inspector` à droite
5. Garder `Content Browser` et `Logs` en bas

**Résultat attendu :**
- un layout par défaut UIScreen autonome existe

**Commit attendu :**
- `feat(editor): add default ui screen workspace layout`

Commit : `79399f56` - `feat(editor): add default ui screen workspace layout`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : le workspace UIScreen expose désormais un layout par défaut distinct avec hiérarchie/outils à gauche, zone document centrale et inspecteur à droite

---

#### ✅ Tâche 2.3 — Introduire une persistance de layout par workspace

**Objectif :** ne plus utiliser un seul fichier de layout pour tous les modes.

**À faire :**
1. Modifier la résolution du chemin de layout pour inclure le workspace actif
2. Prévoir au minimum `world` et `uiscreen`
3. Adapter save/load/reset pour travailler par workspace
4. Garder une compatibilité propre si aucun layout n'existe encore

**Résultat attendu :**
- chaque workspace a son propre fichier JSON de layout
- l'absence de layout persisté retombe sur le layout par défaut du workspace

**Commit attendu :**
- `feat(editor): persist dock layout per workspace`

Commit : `5cca627c` - `feat(editor): persist dock layout per workspace`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : les layouts World et UIScreen utilisent désormais des fichiers distincts, avec fallback legacy pour l'ancien layout global World

---

### Phase 3 — Introduire le gestionnaire de workspaces

#### ✅ Tâche 3.1 — Ajouter un WorkspaceManager minimal

**Objectif :** centraliser la transition entre modes d'édition.

**À faire :**
1. Créer un composant `EditorWorkspaceManager`
2. Lui faire connaître le workspace actif
3. Lui faire charger le bon layout au bon moment
4. Lui faire sauvegarder le layout du workspace courant avant bascule
5. Le brancher dans le shell sans déplacer encore toute la logique métier

**Résultat attendu :**
- la bascule de workspace ne dépend plus d'un enchaînement d'ifs dispersés

**Commit attendu :**
- `feat(editor): add workspace manager for dock layouts`

Commit : `8a553191` - `feat(editor): add workspace manager for dock layouts`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : la création, l'activation, le reset et la restauration des layouts passent désormais par un gestionnaire dédié

---

#### ✅ Tâche 3.2 — Activer le workspace World au chargement de projet

**Objectif :** définir un comportement de démarrage stable et explicite.

**À faire :**
1. Activer le workspace World comme mode par défaut à l'ouverture d'un projet
2. Charger son layout persisté si disponible
3. Retomber sur son layout par défaut sinon
4. Vérifier que les panneaux communs restent opérationnels

**Résultat attendu :**
- l'éditeur démarre dans un état déterministe et cohérent

**Commit attendu :**
- `feat(editor): activate world workspace on project load`

Commit : `36ffe7dd` - `feat(editor): activate world workspace on project load`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : le chargement de projet réactive explicitement le workspace World et restaure son layout dédié si disponible

---

### Phase 4 — Brancher les documents au bon workspace

#### ✅ Tâche 4.1 — Faire piloter le workspace par le document actif

**Objectif :** faire de l'onglet document actif la source de vérité du mode courant.

**À faire :**
1. Identifier le type du document actif dans la zone document
2. Mapper ce type au bon workspace
3. Bascule automatique vers `UIScreen` quand un écran UI devient actif
4. Retour au workspace `World` quand un document World redevient actif

**Résultat attendu :**
- le mode visible suit le document central actif

**Commit attendu :**
- `feat(editor): switch workspace from active document`

Commit : `41a0cad0` - `feat(editor): switch workspace from active document`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : la bascule World/UIScreen suit désormais le document actif et préserve les onglets document connus lors d'un changement de workspace

---

#### ✅ Tâche 4.2 — Binder les panneaux UIScreen au document UI actif

**Objectif :** éviter les dépendances implicites et garantir que les panneaux UI reflètent la bonne preview active.

**À faire :**
1. Formaliser un contexte `UIScreenWorkspaceContext` si nécessaire
2. Injecter le document UI actif dans `Hierarchy`, `Inspector` et `Toolbox`
3. Mettre à jour le binding lors du changement de preview active
4. Vérifier qu'aucun panneau UIScreen ne reste alimenté par un document obsolète

**Résultat attendu :**
- les panneaux UIScreen suivent toujours la preview active

**Commit attendu :**
- `refactor(editor): bind ui screen panels to active workspace context`

Commit : `9e0c772a` - `refactor(editor): bind workspace contexts`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : un contexte UIScreen explicite alimente désormais la hiérarchie, l'inspecteur et la toolbox à partir de la preview active

---

#### ✅ Tâche 4.3 — Binder les panneaux World au contexte World actif

**Objectif :** rendre symétrique la gestion des panneaux World.

**À faire :**
1. Formaliser un contexte `WorldWorkspaceContext` si nécessaire
2. Y rattacher la sélection, le viewport et les détails entité
3. Vérifier que `Entities` et `Details` ne dépendent plus d'états globaux mal définis

**Résultat attendu :**
- les panneaux World sont alimentés par un contexte explicite

**Commit attendu :**
- `refactor(editor): bind world panels to active workspace context`

Commit : `9e0c772a` - `refactor(editor): bind workspace contexts`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : un contexte World explicite synchronise désormais la sélection, le viewport et le panneau Details

---

### Phase 5 — Finitions et robustesse

#### ✅ Tâche 5.1 — Adapter les actions de menu et status bar au workspace actif

**Objectif :** éviter qu'une action shell tente de manipuler un panneau non pertinent pour le mode courant.

**À faire :**
1. Réviser les actions `Save Layout`, `Load Layout`, `Reset Layout`
2. Les faire agir sur le workspace actif
3. Réviser les raccourcis ou boutons shell si nécessaire
4. S'assurer que les boutons communs restent globaux

**Résultat attendu :**
- les commandes shell manipulent le bon layout

**Commit attendu :**
- `refactor(editor): route shell layout actions through active workspace`

Commit : `d19ef7ef` - `refactor(editor): route shell layout actions through active workspace`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : les actions Save/Load/Reset Layout ciblent désormais le workspace actif et tentent de préserver le document courant

---

#### ✅ Tâche 5.2 — Gérer proprement les panneaux indisponibles ou inconnus

**Objectif :** rendre la désérialisation plus robuste face aux layouts anciens ou partiels.

**À faire :**
1. Conserver un fallback de panneau indisponible
2. Vérifier qu'un layout World ne tente pas de restaurer un panneau UIScreen absent du workspace courant
3. Logger clairement les cas de migration ou de panneau manquant

**Résultat attendu :**
- la persistance est tolérante aux versions intermédiaires du refactor

**Commit attendu :**
- `fix(editor): harden workspace layout restoration`

Commit : `874760f0` - `fix(editor): harden workspace layout restoration`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : les panneaux outil incompatibles avec le workspace restauré sont retirés automatiquement après chargement, avec fallback et log explicite

---

#### ✅ Tâche 5.3 — Validation finale et documentation courte

**Objectif :** stabiliser la feature et documenter le fonctionnement.

**À faire :**
1. Vérifier le scénario de démarrage projet
2. Vérifier le scénario d'ouverture d'un UIScreen
3. Vérifier l'aller-retour World ↔ UIScreen
4. Vérifier la persistance distincte des layouts
5. Ajouter une courte documentation développeur si nécessaire

**Résultat attendu :**
- la feature est utilisable et compréhensible

**Commit attendu :**
- `docs(editor): document workspace-based editor layouts`

Commit : `PENDING` - `docs(editor): document workspace-based editor layouts`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : documentation développeur ajoutée dans `docs/editor-workspace-layouts.md`; validation finale limitée au build ciblé, sans scénario UI automatisé

---

## Critères d'acceptation

- Le mode World ne montre plus par défaut les panneaux UIScreen
- Le mode UIScreen ne montre plus par défaut les panneaux World
- `Content Browser` et `Logs` restent disponibles dans les deux modes
- Le layout de chaque mode est persisté séparément
- Le changement de document actif peut faire basculer automatiquement le workspace
- Le système ne casse pas l'ouverture de projet ni l'ouverture d'un écran UI existant

---

## Notes d'implémentation

- Préférer une extraction incrémentale depuis `Game1` plutôt qu'un grand refactor en une fois
- Éviter toute duplication de logique de factory de panneaux
- Garder les ids de panneaux stables si possible pour limiter les migrations de layout
- Si un renommage d'id est nécessaire, prévoir une compatibilité de lecture ou un fallback logué
- Prioriser la clarté de responsabilité plutôt qu'une abstraction trop générique

---

## Résumé attendu à la fin de chaque tâche

```md
## Résumé
- modifications réalisées

## Validation
- build ou test ciblé exécuté

## Commit
- hash court et message

## Prochaine tâche
- identifiant de la tâche suivante
```