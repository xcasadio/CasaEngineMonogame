# Architecture Entry Points

## 1. Vue d'ensemble

Le screen editor devra s'insérer entre deux ensembles déjà présents dans le dépôt :
- la chaîne d'authoring CasaEngine pour les assets, les sessions de projet et les vues d'éditeur
- la pile runtime MGUI pour le parsing XAML, l'instanciation des contrôles et l'affichage par vue

Le point de vigilance principal est la séparation des responsabilités :
- le document d'édition est la source de vérité
- la preview runtime est reconstruite à partir du document
- la sérialisation XAML reste le format principal de persistance

## 2. Couche Asset et projet côté CasaEngine

### Registre d'assets
- `CasaEngine/Framework/Assets/AssetCatalog.cs`
  - registre global des `AssetInfo`
  - charge `AssetInfos.json`
  - fournit le modèle de référencement déjà utilisé par l'éditeur
- `CasaEngine/Framework/Assets/AssetInfo.cs`
  - métadonnées d'asset : identifiant, nom, fichier, type
  - bon point d'appui pour introduire un futur `UIScreenAsset`
- `CasaEngine.EditorServices/EditorAssetCatalogService.cs`
  - façade d'édition sur le catalogue d'assets
  - expose les événements Add/Remove/Rename/Save utiles pour brancher l'éditeur de screen

### Session de projet
- `CasaEngine.EditorServices/EditorProjectAuthoringService.cs`
  - charge, crée et ferme les projets
  - point d'entrée principal pour brancher l'ouverture d'assets UIScreen
- `CasaEngine.EditorServices/EditorProjectSession.cs`
  - conserve l'état minimal de session, notamment le chemin du projet courant
- `CasaEngine/Framework/Configuration/Project/ProjectSettingsHelper.cs`
  - charge la configuration projet et initialise le catalogue d'assets

## 3. Sérialisation et patterns d'authoring existants

### Écriture d'assets
- `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`
  - dispatcher polymorphe `TrySerialize()` vers les serializers d'assets existants
  - le futur `UIScreenAsset` devra être branché ici si on ajoute une couche JSON autour du XAML
- `CasaEngine.EditorServices/EditorAssetWriterService.cs`
  - pipeline d'écriture d'asset sur disque
  - utile si l'asset UIScreen doit être enregistré via un writer d'éditeur standard
- `CasaEngine.EditorServices/EditorJsonSaveHelper.cs`
  - helpers de sérialisation de valeurs simples et structurées
  - probablement réutilisable pour certaines métadonnées de screen

### Pattern d'arbre éditable
- `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs`
  - exemple existant de sérialisation récursive d'un arbre authoring
  - sert de référence structurelle pour `UIScreenDocument` et `UIScreenNode`

## 4. Hôtes de vues et intégration de preview

### Gestion des vues de rendu
- `CasaEngine/Framework/Rendering/RenderView.cs`
  - représente une vue de rendu isolée avec caméra, surface et mode d'update
- `CasaEngine/Framework/Rendering/ViewManager.cs`
  - registre des vues actives
  - création, suppression, invalidation, resize
- `CasaEngine/Framework/Rendering/IViewHost.cs`
  - contrat minimal pour un hôte de vue côté UI

### Hôtes d'éditeur
- Les hôtes WPF `CasaEngine.EditorUI/Controls/EngineHost.cs` et `ViewportControl.cs` cités ici à l'origine n'existent plus : le projet `CasaEngine.EditorUI` a été remplacé par l'éditeur MGUI `CasaEngine.Editor` (constat du 2026-09-06).
- `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
  - hôte MGUI intégré pour héberger une vue et ses overlays
  - la référence pour héberger une preview screen côté runtime UI
- `CasaEngine.Editor/Runtime/EditorViewContext.cs`
  - agrège l'état par vue : `RenderView`, caméra, surface, overlays
  - extension possible pour stocker le contexte de preview screen

## 5. Panneaux et patterns d'éditeur réutilisables

### Hiérarchie et navigation
- `CasaEngine.Editor/Controls/EntitiesPanel.cs`
  - arbre hiérarchique synchronisé avec la sélection
  - bon modèle pour un futur panneau de hiérarchie des contrôles UIScreen

### Navigation d'assets
- `CasaEngine.Editor/Controls/ContentBrowserPanel.cs`
  - navigation par dossiers et fichiers avec événements d'ouverture/suppression
  - réutilisable pour brancher l'ouverture d'un asset UIScreen depuis le content browser

### Sauvegarde globale d'éditeur
- `CasaEngine.Editor/GameEditor.cs`
  - contient le flux de sauvegarde projet et layout
  - point d'intégration potentiel pour le save global des screens ouverts

## 6. Parsing XAML et runtime MGUI

### Parsing et résolution de types
- `MGUI/MGUI.Core/UI/XAML/XAMLParser.cs`
  - parser principal XML vers objets runtime MGUI
  - résout les types de contrôles et namespaces XAML
  - point central pour la lecture d'un screen existant ou pour valider un XAML produit
- `MGUI/MGUI.Core/UI/XAML/XamlDocumentSource.cs`
  - abstraction de la source de document XAML
  - permet un chargement depuis fichier, flux ou chaîne
- `MGUI/MGUI.Core/Tooling/UIToolingService.cs`
  - expose un `LoadPreview()` autour du parser
  - probablement exploitable pour une première version de preview

### Designer existant
- `MGUI/MGUI.Core/UI/MGXAMLDesigner.cs`
  - designer/runtime preview déjà présent dans MGUI
  - intéressant comme référence ou pour accélérer une preview v1, mais ne remplace pas le besoin d'un document model CasaEngine
- `MGUI/MGUI.Core/UI/XAML/Controls.cs`
  - contient la définition XAML liée au designer

### Binding, thèmes et templates
- `MGUI/MGUI.Core/UI/Data Binding/XamlBindableBase.cs`
  - base pour bindings et références de ressources
  - important pour cadrer les limites de support de la v1
- `MGUI/MGUI.Core/UI/XAML/ThemeDefinitionLoader.cs`
  - chargement des thèmes depuis XAML
- `MGUI/MGUI.Core/UI/XAML/ControlTemplateLoader.cs`
  - chargement des templates de contrôle
- `MGUI/MGUI.Core/UI/MGResources.cs`
  - expose le chargement de thèmes et templates

## 7. Runtime UI CasaEngine déjà branché sur MGUI

### Racine UI par vue
- `CasaEngine/Framework/GUI/MGUI/UIRoot.cs`
  - racine runtime MGUI par vue
  - possède le desktop, le renderer et la gestion des screens
  - point d'ancrage naturel pour afficher la preview screen dans l'éditeur
- `CasaEngine/Framework/GUI/MGUI/ScreenStack.cs`
  - pile de screens par vue avec gestion des couches et du modal
  - utile si le screen editor doit prévisualiser un screen comme un vrai écran runtime
- `CasaEngine/Framework/GUI/MGUI/IUIScreen.cs`
  - contrat runtime des screens CasaEngine
  - sert de référence pour l'adaptateur de preview, pas comme source de vérité authoring
- `CasaEngine/Framework/GUI/MGUI/GameScreenManager.cs`
  - gestionnaire de transitions et de factories de screens
  - potentiellement utile plus tard si les screens édités doivent s'intégrer au flux runtime standard

## 8. Recommandations d'intégration pour la suite

### Emplacement des nouvelles couches
- `CasaEngine.EditorServices`
  - `UIScreenAsset`
  - `UIScreenDocument`, `UIScreenNode`, `UIScreenPropertyValue`
  - parser et serializer XAML dédiés à l'édition
  - session d'édition de screen
- `CasaEngine.Editor`
  - panneau hiérarchie
  - inspector
  - host de preview
- `CasaEngine/Framework/GUI/MGUI`
  - seulement pour les adaptateurs runtime nécessaires à la preview, sans faire de l'instance runtime la source de vérité

### Point d'entrée privilégié pour une v1
1. ouvrir un asset UIScreen via `EditorProjectAuthoringService` et `EditorAssetCatalogService`
2. charger le XAML dans un `UIScreenDocument`
3. reconstruire une preview via `UIRoot` et `XAMLParser` ou un builder dédié
4. afficher la preview dans une `RenderView` enregistrée via `ViewManager` et hébergée comme dans `WorldViewportPanel` (l'ancien `EngineHost` WPF n'existe plus)
5. faire converger hiérarchie et inspector sur le document, pas sur les contrôles runtime

## 9. Résumé des points d'entrée prioritaires

### CasaEngine
- `CasaEngine.EditorServices/EditorProjectAuthoringService.cs`
- `CasaEngine.EditorServices/EditorAssetCatalogService.cs`
- `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
- `CasaEngine/Framework/Rendering/ViewManager.cs`
- `CasaEngine.Editor/Controls/EntitiesPanel.cs`
- `CasaEngine.Editor/Controls/ContentBrowserPanel.cs`

### MGUI
- `MGUI/MGUI.Core/UI/XAML/XAMLParser.cs`
- `MGUI/MGUI.Core/UI/XAML/XamlDocumentSource.cs`
- `MGUI/MGUI.Core/UI/MGXAMLDesigner.cs`
- `MGUI/MGUI.Core/Tooling/UIToolingService.cs`
- `CasaEngine/Framework/GUI/MGUI/UIRoot.cs`
- `CasaEngine/Framework/GUI/MGUI/ScreenStack.cs`
