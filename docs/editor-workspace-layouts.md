# Layouts par workspace dans CasaEngine.Editor

## Résumé

`CasaEngine.Editor` utilise désormais des **workspaces d'édition** pour séparer les panneaux contextuels par mode tout en gardant un shell commun.

Le shell commun conserve :
- la barre de menus
- la status bar
- le `MGDockHost`
- les panneaux communs comme `Content Browser` et `Logs`

Les workspaces gèrent :
- leur layout par défaut
- leurs panneaux contextuels
- leur fichier de persistance de layout

## Workspaces disponibles

### World

Panneaux attendus :
- `World Viewport`
- `Entities`
- `Details`
- `Content Browser`
- `Output / Logs`

Classe :
- `CasaEngine.Editor/Workspaces/WorldEditorWorkspace.cs`

### UIScreen

Panneaux attendus :
- `Screen Hierarchy`
- `Screen Toolbox`
- `Screen Inspector`
- document(s) `UIScreen`
- `Content Browser`
- `Output / Logs`

Classe :
- `CasaEngine.Editor/Workspaces/UIScreenEditorWorkspace.cs`

## Fichiers principaux

- `CasaEngine.Editor/Workspaces/EditorWorkspaceManager.cs`
- `CasaEngine.Editor/Workspaces/EditorPanelRegistry.cs`
- `CasaEngine.Editor/Workspaces/EditorPanelIds.cs`
- `CasaEngine.Editor/Workspaces/UIScreenWorkspaceContext.cs`
- `CasaEngine.Editor/Workspaces/WorldWorkspaceContext.cs`
- `CasaEngine.Editor/Game1.cs`

## Persistance

Les layouts sont persistés par projet dans le dossier :

- `.casaeditor/layout.world.json`
- `.casaeditor/layout.uiscreen.json`

Compatibilité legacy :
- l'ancien fichier global `layout.json` reste lu comme fallback pour le workspace `World`

## Règles de bascule

- Au chargement d'un projet, le workspace `World` est activé explicitement.
- Lorsqu'un document `UIScreen` devient actif, le workspace `UIScreen` est activé.
- Lorsqu'un document `World Viewport` redevient actif, le workspace `World` est réactivé.
- Les onglets document connus sont restaurés lors d'un changement de workspace pour limiter les pertes de contexte.

## Règles de restauration

- Les panneaux inconnus utilisent toujours un fallback visuel de type `Panel unavailable`.
- Après chargement d'un layout persisté, les panneaux outil incompatibles avec le workspace courant sont retirés automatiquement.
- Les documents restent autorisés pour éviter de casser les onglets ouverts lors d'une bascule de workspace.

## Contextes métier

### UIScreenWorkspaceContext

Responsable de :
- la preview UI active
- le document UI actif
- la sélection partagée de noeuds UI

Les panneaux `Hierarchy`, `Inspector` et `Toolbox` sont alimentés à partir de ce contexte.

### WorldWorkspaceContext

Responsable de :
- la sélection d'entité active
- le composant sélectionné
- le viewport monde actif

Les panneaux `Entities`, `Details` et le `World Viewport` sont synchronisés à partir de ce contexte.

## Limites actuelles

- Les documents `UIScreen` restaurés depuis un layout persisté nécessitent que leur preview existe déjà dans la session courante pour retrouver leur contenu complet.
- La validation fonctionnelle a été faite par build ciblé ; aucun scénario UI automatisé de bout en bout n'a encore été ajouté.