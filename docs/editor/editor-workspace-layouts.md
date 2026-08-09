# Panels contextuels dans CasaEngine.Editor

## Résumé

`CasaEngine.Editor` n'utilise plus de layout special par type d'editeur.

Le shell MGUI est maintenant stable :
- barre de menus
- status bar
- `MGDockHost`
- un seul layout persiste par projet

Les panneaux dockables principaux sont semantiques :
- `Hierarchy`
- `Inspector`
- `Toolbox`
- `Content Browser`
- `Output / Logs`

Le contenu de `Hierarchy`, `Inspector` et `Toolbox` change selon le document actif, mais leur emplacement dans le shell ne change pas.

## Layout shell par défaut

Le layout par défaut est :
- gauche : `Hierarchy` et `Toolbox`
- centre : zone document
- droite : `Inspector`
- bas : `Content Browser` et `Output / Logs`

Le document `World Viewport` reste le document par défaut ouvert dans la zone centrale.

## Contexte global

Le shell s'appuie sur un `EditorContextService` qui projette :
- le document actif
- le type du document actif
- la sélection active
- le nombre d'elements selectionnes

Le contexte global ne remplace pas les services locaux de chaque editeur.
Il sert de couche de coordination pour les panels contextuels.

## Documents pris en charge

### World

Le document actif `World` pilote :
- `Hierarchy` -> `EntitiesPanel`
- `Inspector` -> `EntityDetailsPanel`
- `Toolbox` -> empty state

### UIScreen

Le document actif `UIScreen` pilote :
- `Hierarchy` -> `UIScreenHierarchyPanel`
- `Inspector` -> `UIScreenInspectorPanel`
- `Toolbox` -> `UIScreenToolboxPanel`

### Material

Le document actif `Material` pilote :
- `Hierarchy` -> `MaterialHierarchyPanel`
- `Inspector` -> `MaterialInspectorView`
- `Toolbox` -> empty state

## Persistance

Le layout est maintenant persiste par projet dans :

- `.casaeditor/layout.editor.json`

Il n'existe plus de fichiers de layout par workspace.

## Points d'extension

Pour ajouter un nouvel editeur demain :

1. Ajouter un nouveau `EditorDocumentKind`
2. Publier le document actif dans `EditorContextService`
3. Publier la selection active dans `EditorContextService`
4. Enregistrer les vues adaptees pour `Hierarchy`, `Inspector` et/ou `Toolbox`
5. Ajouter le document correspondant a la zone document du dock si necessaire

Le shell docking n'a pas besoin d'etre reconfigure pour ce nouveau type d'editeur.

## Limites actuelles

- La multi-selection reste volontairement simple : seul le compteur de selection est expose au shell.
- Les documents dynamiques restaures depuis un layout persiste ne retrouvent leur contenu complet que si leur preview/inspector a ete recree dans la session courante.
- La validation a ete faite par build cible du projet editeur ; aucun scenario UI automatise de bout en bout n'a encore ete ajoute.