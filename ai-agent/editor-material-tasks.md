# [ARCHIVE] Édition des propriétés de Material dans l'éditeur — Liste de tâches

## Etat du document

Ce plan n'est plus un backlog actif.

Il decrivait un inspecteur material WPF a construire, alors que le repo utilise maintenant un inspecteur material MGUI genere et un preview runtime deja en place.

Sources de verite a utiliser a la place :

- `ai-agent/material-system-modernization-tasks.md` pour la migration materials deja livree
- `ai-agent/material-inspector-stability-plan.md` pour la validation editeur encore ouverte
- `ai-agent/rendering/materials-workflow.md` pour l'etat final de l'architecture

Consequence : conserver ce fichier uniquement comme trace historique du besoin initial, et ne pas y ajouter de nouvelles taches.

## Contexte

La hiérarchie de mesh (`StaticModel` → `StaticModelNode` → `StaticModelMesh`) et le système de matériaux (`MaterialBase`, `Material`, `UnlitTextureMaterial`, `LitDiffuseMaterial`) sont en place. Les matériaux sont chargés via `MaterialLoader`, sérialisés en JSON (`.material`), et référencés par `Guid` depuis les `StaticModelMesh` et `SubMesh`.

**Ce qui manque** : l'éditeur n'offre aucun contrôle WPF pour visualiser ou modifier les propriétés d'un matériau. Le template `staticModelSubMeshComponentTemplate` dans `EntityComponentControl.xaml` affiche uniquement le nom du mesh, le nombre de vertices et d'indices — aucune information sur le matériau assigné ni ses propriétés.

### État actuel de l'éditeur

| Élément | État |
|---|---|
| `StaticModelSubMeshComponentViewModel` | Expose `MeshName`, `VertexCount`, `IndexCount` — **aucune propriété matériau** |
| `EntityComponentControl.xaml` (template `staticModelSubMeshComponentTemplate`) | Affiche 3 lignes info mesh — **aucun contrôle matériau** |
| `AssetSelectorControl` | Existe et fonctionne pour sélectionner un asset par Guid (utilisé pour textures, modèles) |
| `ColorEditor` (WpfControls) | Existe, utilisé dans d'autres contrôles (sprites, physics debug) |
| `Vector3Editor` (WpfControls) | Existe, utilisé pour Position/Scale |
| Contrôle dédié pour éditer un Material | **N'existe pas** |
| ViewModel pour Material | **N'existe pas** |

### Classes matériau existantes

| Classe | Propriétés éditables |
|---|---|
| `MaterialBase` | `Name`, `IsTransparent`, `Queue` (enum), `CastShadows`, `ReceiveShadows`, `BlendState`, `DepthStencilState`, `RasterizerState`, `SamplerState`, `ShaderAssetId` |
| `UnlitTextureMaterial` | `AlbedoAssetId` (texture), `Tint` (Color), `Alpha` (float) |
| `LitDiffuseMaterial` | `AlbedoAssetId` (texture), `NormalMapAssetId` (texture), `DiffuseColor` (Color), `EmissiveColor` (Vector3), `SpecularColor` (Vector3), `SpecularPower` (float) |
| `Material` | 8 texture slots : BaseColor, Opacity, Normal, Specular, Roughness, Tangent, Height, Reflection (chacun un `Guid`) |

### Conventions à suivre

- ViewModels dans `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/`
- UserControls XAML dans `CasaEngine.EditorUI/Controls/EntityControls/`
- Étendre `NotifyPropertyChangeBase` pour les ViewModels
- Utiliser `AssetSelectorControl` pour les sélecteurs de texture/asset
- Utiliser `ColorEditor` pour les propriétés Color
- Utiliser les `ComboBox` pour les enums (BlendState, RasterizerState, etc.) avec les clés définies dans `MaterialBase` (`BlendStateMap`, `DepthStateMap`, `RasterizerMap`, `SamplerMap`)
- Blocs `#if EDITOR` dans le code engine si nécessaire
- Pattern MVVM : le ViewModel expose les propriétés, le XAML bind dessus

---

## Task 1 — Créer `MaterialViewModel` de base

**Goal:** Créer un ViewModel qui wrape `MaterialBase` et expose ses propriétés communes pour le data binding WPF.

**Steps:**
1. Créer `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/MaterialViewModel.cs`.
2. La classe hérite de `NotifyPropertyChangeBase`.
3. Accepte un `MaterialBase` dans le constructeur et le stocke en champ privé.
4. Expose en lecture/écriture avec notification :
   - `Name` (string)
   - `IsTransparent` (bool)
   - `Queue` (RenderQueue enum) — exposer aussi `AvailableQueues` (liste statique des valeurs de l'enum)
   - `CastShadows` (bool)
   - `ReceiveShadows` (bool)
   - `ShaderAssetId` (Guid)
5. Expose en lecture/écriture les render states sous forme de string (clé du dictionnaire) :
   - `BlendStateName` (string) — valeurs possibles : "Opaque", "AlphaBlend", "Additive", "NonPremultiplied"
   - `DepthStencilStateName` (string) — valeurs possibles : "Default", "None", "Read"
   - `RasterizerStateName` (string) — valeurs possibles : "CullNone", "CullClockwise", "CullCounterClockwise"
   - `SamplerStateName` (string) — valeurs possibles : "LinearClamp", "LinearWrap", "PointClamp", "PointWrap", "AnisotropicClamp", "AnisotropicWrap"
   - Fournir des propriétés statiques `AvailableBlendStates`, `AvailableDepthStencilStates`, `AvailableRasterizerStates`, `AvailableSamplerStates` (listes de strings).
6. Les setters modifient directement les propriétés de `MaterialBase` (par exemple pour les render states, mapper la string vers la valeur XNA via les dictionnaires existants dans `MaterialBase`).
7. Exposer les dictionnaires de `MaterialBase` (`BlendStateMap`, etc.) en `internal` ou ajouter des méthodes statiques publiques pour la conversion string ↔ render state. Si les dictionnaires sont privés, les rendre `internal` dans `MaterialBase`.

**Files to create:**
- `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/MaterialViewModel.cs`

**Files to modify:**
- `CasaEngine/Framework/Materials/MaterialBase.cs` — rendre les dictionnaires `internal static` au lieu de `private static readonly` (ou ajouter des méthodes helper publiques)

**Commit message:** `EditorUI: add MaterialViewModel wrapping MaterialBase properties`

---

## Task 2 — Créer `UnlitTextureMaterialViewModel`

**Goal:** Créer un ViewModel spécialisé pour `UnlitTextureMaterial` qui expose ses propriétés spécifiques.

**Steps:**
1. Créer `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/UnlitTextureMaterialViewModel.cs`.
2. Hérite de `MaterialViewModel`.
3. Accepte un `UnlitTextureMaterial` dans le constructeur.
4. Expose en lecture/écriture avec notification :
   - `AlbedoAssetId` (Guid) — pour le sélecteur de texture via `AssetSelectorControl`
   - `Tint` (Color XNA) — pour le `ColorEditor`
   - `Alpha` (float, 0.0–1.0)
5. Les setters modifient directement les propriétés de `UnlitTextureMaterial`.

**Files to create:**
- `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/UnlitTextureMaterialViewModel.cs`

**Commit message:** `EditorUI: add UnlitTextureMaterialViewModel`

---

## Task 3 — Créer `LitDiffuseMaterialViewModel`

**Goal:** Créer un ViewModel spécialisé pour `LitDiffuseMaterial`.

**Steps:**
1. Créer `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/LitDiffuseMaterialViewModel.cs`.
2. Hérite de `MaterialViewModel`.
3. Accepte un `LitDiffuseMaterial` dans le constructeur.
4. Expose en lecture/écriture avec notification :
   - `AlbedoAssetId` (Guid)
   - `NormalMapAssetId` (Guid)
   - `DiffuseColor` (Color)
   - `EmissiveColor` (Vector3) — utiliser `Vector3Editor`
   - `SpecularColor` (Vector3) — utiliser `Vector3Editor`
   - `SpecularPower` (float)
5. Les setters modifient directement les propriétés de `LitDiffuseMaterial`.

**Files to create:**
- `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/LitDiffuseMaterialViewModel.cs`

**Commit message:** `EditorUI: add LitDiffuseMaterialViewModel`

---

## Task 4 — Créer `PbrMaterialViewModel`

**Goal:** Créer un ViewModel spécialisé pour la classe `Material` (8 texture slots).

**Steps:**
1. Créer `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/PbrMaterialViewModel.cs`.
2. Hérite de `MaterialViewModel`.
3. Accepte un `Material` dans le constructeur.
4. Expose en lecture/écriture avec notification les 8 texture asset IDs :
   - `TextureBaseColorAssetId` (Guid)
   - `TextureOpacityAssetId` (Guid)
   - `TextureNormalAssetId` (Guid)
   - `TextureSpecularAssetId` (Guid)
   - `TextureRoughnessAssetId` (Guid)
   - `TextureTangentAssetId` (Guid)
   - `TextureHeightAssetId` (Guid)
   - `TextureReflectionAssetId` (Guid)
5. Les setters modifient directement les propriétés de `Material`.

**Files to create:**
- `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/PbrMaterialViewModel.cs`

**Commit message:** `EditorUI: add PbrMaterialViewModel for 8-texture-slot Material`

---

## Task 5 — Factory pour créer le bon MaterialViewModel

**Goal:** Créer une factory qui instancie le ViewModel approprié selon le type concret du `MaterialBase`.

**Steps:**
1. Créer `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/MaterialViewModelFactory.cs`.
2. Méthode statique `Create(MaterialBase? material)` → retourne `MaterialViewModel?`.
3. Pattern switch :
   - `UnlitTextureMaterial` → `UnlitTextureMaterialViewModel`
   - `LitDiffuseMaterial` → `LitDiffuseMaterialViewModel`
   - `Material` → `PbrMaterialViewModel`
   - `null` → retourne `null`
   - Autre → `MaterialViewModel` (base, propriétés communes seulement)

**Files to create:**
- `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/MaterialViewModelFactory.cs`

**Commit message:** `EditorUI: add MaterialViewModelFactory`

---

## Task 6 — Enrichir `StaticModelSubMeshComponentViewModel` avec le matériau

**Goal:** Exposer le `MaterialViewModel` depuis le ViewModel du sub-mesh pour que le XAML puisse binder sur les propriétés du matériau.

**Steps:**
1. Modifier `StaticModelSubMeshComponentViewModel.cs` :
   - Ajouter une propriété `MaterialViewModel? MaterialVM { get; private set; }`.
   - Dans le constructeur, après avoir stocké `_subMesh`, résoudre le matériau :
     ```
     var mat = _subMesh.ModelMesh?.Material;
     if (mat != null)
         MaterialVM = MaterialViewModelFactory.Create(mat);
     ```
   - Ajouter une propriété `string MaterialTypeName` exposant le nom du type concret du matériau (ex: "UnlitTextureMaterial", "LitDiffuseMaterial", "Material") ou "None" si null.
   - Ajouter une propriété `Guid MaterialAssetId` pour afficher/modifier l'asset ID du matériau assigné au mesh.
2. Le matériau est lu depuis `_subMesh.ModelMesh?.Material` (déjà résolu au chargement du `StaticModel`).

**Files to modify:**
- `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/StaticModelSubMeshComponentViewModel.cs`

**Commit message:** `EditorUI: expose MaterialViewModel from StaticModelSubMeshComponentViewModel`

---

## Task 7 — Créer le UserControl `MaterialBaseControl.xaml`

**Goal:** Créer un contrôle XAML réutilisable qui affiche et édite les propriétés communes de `MaterialBase` (render states, flags, queue).

**Steps:**
1. Créer `CasaEngine.EditorUI/Controls/EntityControls/MaterialBaseControl.xaml` + `.xaml.cs`.
2. Le DataContext attendu est un `MaterialViewModel`.
3. Layout en grille avec les contrôles suivants :
   - `Name` : TextBox bindé sur `Name`
   - `IsTransparent` : CheckBox bindé sur `IsTransparent`
   - `Queue` : ComboBox bindé sur `Queue`, ItemsSource sur `AvailableQueues`
   - `CastShadows` : CheckBox
   - `ReceiveShadows` : CheckBox
   - `BlendState` : ComboBox, ItemsSource `AvailableBlendStates`, SelectedItem bindé sur `BlendStateName`
   - `DepthStencilState` : ComboBox, ItemsSource `AvailableDepthStencilStates`, SelectedItem bindé sur `DepthStencilStateName`
   - `RasterizerState` : ComboBox, ItemsSource `AvailableRasterizerStates`, SelectedItem bindé sur `RasterizerStateName`
   - `SamplerState` : ComboBox, ItemsSource `AvailableSamplerStates`, SelectedItem bindé sur `SamplerStateName`
4. Le code-behind est minimal (juste `InitializeComponent()`).

**Files to create:**
- `CasaEngine.EditorUI/Controls/EntityControls/MaterialBaseControl.xaml`
- `CasaEngine.EditorUI/Controls/EntityControls/MaterialBaseControl.xaml.cs`

**Commit message:** `EditorUI: add MaterialBaseControl for common material properties`

---

## Task 8 — Créer le UserControl `UnlitTextureMaterialControl.xaml`

**Goal:** Créer un contrôle XAML pour éditer les propriétés spécifiques de `UnlitTextureMaterial`.

**Steps:**
1. Créer `CasaEngine.EditorUI/Controls/EntityControls/UnlitTextureMaterialControl.xaml` + `.xaml.cs`.
2. Le DataContext attendu est un `UnlitTextureMaterialViewModel`.
3. Layout :
   - Inclure `MaterialBaseControl` en haut (bind sur le même DataContext — le ViewModel hérite de `MaterialViewModel`)
   - `Albedo Texture` : `AssetSelectorControl` bindé sur `AlbedoAssetId`, avec validation qui vérifie l'extension `.texture`
   - `Tint` : `ColorEditor` bindé sur `Tint`
   - `Alpha` : Slider (0.0–1.0) + TextBox bindé sur `Alpha`
4. Le code-behind contient la méthode de validation pour l'`AssetSelectorControl`.

**Files to create:**
- `CasaEngine.EditorUI/Controls/EntityControls/UnlitTextureMaterialControl.xaml`
- `CasaEngine.EditorUI/Controls/EntityControls/UnlitTextureMaterialControl.xaml.cs`

**Commit message:** `EditorUI: add UnlitTextureMaterialControl`

---

## Task 9 — Créer le UserControl `LitDiffuseMaterialControl.xaml`

**Goal:** Créer un contrôle XAML pour éditer les propriétés de `LitDiffuseMaterial`.

**Steps:**
1. Créer `CasaEngine.EditorUI/Controls/EntityControls/LitDiffuseMaterialControl.xaml` + `.xaml.cs`.
2. Le DataContext attendu est un `LitDiffuseMaterialViewModel`.
3. Layout :
   - Inclure `MaterialBaseControl` en haut
   - `Albedo Texture` : `AssetSelectorControl` bindé sur `AlbedoAssetId`
   - `Normal Map` : `AssetSelectorControl` bindé sur `NormalMapAssetId`
   - `Diffuse Color` : `ColorEditor` bindé sur `DiffuseColor`
   - `Emissive Color` : `Vector3Editor` bindé sur `EmissiveColor`
   - `Specular Color` : `Vector3Editor` bindé sur `SpecularColor`
   - `Specular Power` : Slider (1–128) + TextBox bindé sur `SpecularPower`

**Files to create:**
- `CasaEngine.EditorUI/Controls/EntityControls/LitDiffuseMaterialControl.xaml`
- `CasaEngine.EditorUI/Controls/EntityControls/LitDiffuseMaterialControl.xaml.cs`

**Commit message:** `EditorUI: add LitDiffuseMaterialControl`

---

## Task 10 — Créer le UserControl `PbrMaterialControl.xaml`

**Goal:** Créer un contrôle XAML pour éditer les 8 texture slots de `Material`.

**Steps:**
1. Créer `CasaEngine.EditorUI/Controls/EntityControls/PbrMaterialControl.xaml` + `.xaml.cs`.
2. Le DataContext attendu est un `PbrMaterialViewModel`.
3. Layout :
   - Inclure `MaterialBaseControl` en haut
   - 8 lignes, chacune avec un label et un `AssetSelectorControl` :
     - Base Color, Opacity, Normal, Specular, Roughness, Tangent, Height, Reflection
   - Chaque `AssetSelectorControl` bindé sur le `Guid` correspondant avec validation `.texture`

**Files to create:**
- `CasaEngine.EditorUI/Controls/EntityControls/PbrMaterialControl.xaml`
- `CasaEngine.EditorUI/Controls/EntityControls/PbrMaterialControl.xaml.cs`

**Commit message:** `EditorUI: add PbrMaterialControl for 8-texture-slot material`

---

## Task 11 — Créer `MaterialControlTemplateSelector`

**Goal:** Créer un `DataTemplateSelector` qui choisit le bon template (et donc le bon UserControl) selon le type du `MaterialViewModel`.

**Steps:**
1. Créer `CasaEngine.EditorUI/Controls/EntityControls/MaterialControlTemplateSelector.cs`.
2. Propriétés `DataTemplate` :
   - `UnlitTextureMaterialTemplate`
   - `LitDiffuseMaterialTemplate`
   - `PbrMaterialTemplate`
   - `DefaultMaterialTemplate` (fallback — affiche seulement `MaterialBaseControl`)
3. Override `SelectTemplate` : switch sur le type du `item` :
   - `UnlitTextureMaterialViewModel` → `UnlitTextureMaterialTemplate`
   - `LitDiffuseMaterialViewModel` → `LitDiffuseMaterialTemplate`
   - `PbrMaterialViewModel` → `PbrMaterialTemplate`
   - Autre `MaterialViewModel` → `DefaultMaterialTemplate`

**Files to create:**
- `CasaEngine.EditorUI/Controls/EntityControls/MaterialControlTemplateSelector.cs`

**Commit message:** `EditorUI: add MaterialControlTemplateSelector`

---

## Task 12 — Intégrer les contrôles matériau dans le template sub-mesh

**Goal:** Modifier le template `staticModelSubMeshComponentTemplate` dans `EntityComponentControl.xaml` pour afficher les propriétés du matériau sous les informations du mesh.

**Steps:**
1. Modifier `EntityComponentControl.xaml` :
   - Dans le template `staticModelSubMeshComponentTemplate`, après les 3 lignes existantes (Mesh, Vertices, Indices), ajouter :
     - Une ligne affichant le type du matériau : `TextBlock` bindé sur `MaterialTypeName`
     - Une ligne affichant l'asset ID du matériau : `TextBlock` bindé sur `MaterialAssetId`
     - Un `ContentControl` dont le `Content` est bindé sur `MaterialVM` et le `ContentTemplateSelector` utilise `MaterialControlTemplateSelector`
   - Déclarer les `DataTemplate` pour chaque type de matériau dans les `Control.Resources` :
     - `unlitTextureMaterialTemplate` → contient `UnlitTextureMaterialControl`
     - `litDiffuseMaterialTemplate` → contient `LitDiffuseMaterialControl`
     - `pbrMaterialTemplate` → contient `PbrMaterialControl`
     - `defaultMaterialTemplate` → contient `MaterialBaseControl`
   - Instancier le `MaterialControlTemplateSelector` avec ces templates
2. Ajouter les `xmlns` nécessaires si pas déjà présents.

**Files to modify:**
- `CasaEngine.EditorUI/Controls/EntityControls/EntityComponentControl.xaml`

**Commit message:** `EditorUI: integrate material controls into sub-mesh template`

---

## Task 13 — Sauvegarde des modifications de matériau

**Goal:** S'assurer que les modifications faites via les contrôles éditeur sont persistées dans le fichier `.material` JSON.

**Steps:**
1. Ajouter une méthode `SaveMaterial()` dans `MaterialViewModel` qui :
   - Crée un `JObject`
   - Appelle `material.Save(jObject)` (déjà implémenté dans chaque sous-classe avec `#if EDITOR`)
   - Utilise `AssetSaver` pour écrire le JSON dans le fichier correspondant (résolu via `AssetCatalog.Get(material.Id)`)
2. Ajouter un bouton "Save Material" dans `MaterialBaseControl.xaml` qui appelle `SaveMaterial()` via un `ICommand` ou un event handler.
3. Vérifier que `AssetSaver` peut écrire un `MaterialBase` — si nécessaire, ajouter le support dans `AssetSaver` (il attend un `ISerializable`, `MaterialBase` implémente déjà `ISerializable`).
4. Tester : modifier une propriété dans l'éditeur → cliquer Save → vérifier que le fichier `.material` est mis à jour.

**Files to modify:**
- `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/MaterialViewModel.cs` — ajouter `SaveMaterial()`
- `CasaEngine.EditorUI/Controls/EntityControls/MaterialBaseControl.xaml` — ajouter bouton Save
- `CasaEngine.EditorUI/Controls/EntityControls/MaterialBaseControl.xaml.cs` — handler du bouton

**Commit message:** `EditorUI: add Save Material button and persistence logic`

---

## Task 14 — Rechargement runtime du matériau après modification

**Goal:** Après la sauvegarde, recharger les textures du matériau pour que les changements soient visibles immédiatement dans le viewport.

**Steps:**
1. Dans `MaterialViewModel.SaveMaterial()`, après l'écriture du fichier, appeler le rechargement des textures :
   - Pour `UnlitTextureMaterial` : si `AlbedoAssetId` a changé, recharger la `Texture2D` via `AssetContentManager` et assigner à `material.Albedo`.
   - Pour `LitDiffuseMaterial` : recharger `Albedo` et `NormalMap` si les asset IDs ont changé.
   - Pour `Material` : appeler `material.LoadTextures(assetContentManager)`.
2. L'`AssetContentManager` est accessible depuis le component (`StaticModelSubMeshComponent.Owner.RootComponent.Owner.World.Game.AssetContentManager`).
3. Ajouter une propriété `AssetContentManager?` dans `MaterialViewModel` (injectée lors de la création depuis `StaticModelSubMeshComponentViewModel`).

**Files to modify:**
- `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/MaterialViewModel.cs` — ajouter `AssetContentManager` + logique de reload
- `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/UnlitTextureMaterialViewModel.cs` — override reload si nécessaire
- `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/LitDiffuseMaterialViewModel.cs` — override reload
- `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/PbrMaterialViewModel.cs` — override reload
- `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/StaticModelSubMeshComponentViewModel.cs` — passer `AssetContentManager` au factory

**Commit message:** `EditorUI: reload material textures after save for live preview`

---

## Task 15 — Changement de matériau sur un sub-mesh

**Goal:** Permettre de changer le matériau assigné à un sub-mesh (par exemple remplacer un `UnlitTextureMaterial` par un `LitDiffuseMaterial`).

**Steps:**
1. Dans `StaticModelSubMeshComponentViewModel`, ajouter un `AssetSelectorControl` bindé sur `MaterialAssetId` qui permet de sélectionner un asset `.material` depuis le content browser.
2. Implémenter la validation : quand un nouvel asset matériau est sélectionné :
   - Charger le `MaterialBase` via `AssetContentManager.Load<MaterialBase>(assetId)`
   - Assigner à `_subMesh.ModelMesh.MaterialAssetId` et `_subMesh.ModelMesh.Material`
   - Recréer le `MaterialVM` via `MaterialViewModelFactory.Create(newMaterial)` et notifier le changement
3. Le template XAML doit se mettre à jour automatiquement grâce au `MaterialControlTemplateSelector` qui réagit au changement de type du `MaterialVM`.

**Files to modify:**
- `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/StaticModelSubMeshComponentViewModel.cs` — ajouter setter `MaterialAssetId` avec rechargement
- `CasaEngine.EditorUI/Controls/EntityControls/EntityComponentControl.xaml` — ajouter `AssetSelectorControl` pour le matériau dans le template sub-mesh

**Commit message:** `EditorUI: allow changing the material assigned to a sub-mesh`

---

## Task 16 — Création d'un nouveau matériau depuis l'éditeur

**Goal:** Permettre de créer un nouveau fichier `.material` directement depuis le content browser.

**Steps:**
1. Dans `ContentBrowserControl`, ajouter une option de menu contextuel "Create Material" (à côté des options existantes de création d'assets).
2. Ouvrir un dialogue simple demandant :
   - Le nom du matériau
   - Le type (`UnlitTextureMaterial`, `LitDiffuseMaterial`, `Material`) via combo box
3. À la confirmation :
   - Créer l'instance du `MaterialBase` avec les valeurs par défaut
   - Générer un `Guid` (déjà fait par le constructeur)
   - Sérialiser en JSON via `Save(JObject)` et écrire le fichier `.material` dans le dossier courant du content browser
   - Ajouter l'entrée dans `AssetCatalog`
   - Rafraîchir le content browser
4. Chercher le pattern existant de création d'asset dans le content browser (ex: création de sprite, animation) et suivre le même pattern.

**Files to modify:**
- `CasaEngine.EditorUI/Controls/ContentBrowser/ContentBrowserControl.xaml` — ajouter menu item
- `CasaEngine.EditorUI/Controls/ContentBrowser/ContentBrowserControl.xaml.cs` — handler de création

**Files to create (si nécessaire):**
- `CasaEngine.EditorUI/Windows/CreateMaterialWindow.xaml` + `.xaml.cs` — dialogue de création

**Commit message:** `EditorUI: add Create Material option in content browser`

---

## Task 17 — Validation et tests manuels

**Goal:** Vérifier que tout le pipeline fonctionne de bout en bout.

**Steps:**
1. Vérifier la compilation du projet `CasaEngine.Editor.MonoGame.sln` sans erreurs.
2. Vérifier que la sélection d'une entité avec un `StaticModelComponent` affiche la hiérarchie des sub-meshes avec leurs matériaux.
3. Vérifier que les contrôles matériau s'affichent correctement selon le type (UnlitTexture, LitDiffuse, PBR).
4. Vérifier que la modification des propriétés (couleur, texture, render states) met à jour le viewport en temps réel après save.
5. Vérifier que le changement de matériau sur un sub-mesh fonctionne (sélection d'un autre `.material`).
6. Vérifier que la création d'un nouveau matériau depuis le content browser produit un fichier `.material` valide.
7. Corriger tout bug ou problème d'affichage rencontré.

**Files to modify:** (aucun fichier spécifique — corrections au besoin)

**Commit message:** `EditorUI: fix issues found during material editing validation`
