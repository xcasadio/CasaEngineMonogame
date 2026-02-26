# Plan de refactor — Drag & Drop d'assets vers Entity (Architecture extensible)

## 0) Contexte

### État actuel
- Le drag & drop est géré **directement** dans `GameEditorWorldControl.OnDrop()` avec un gros `if/else` sur l'extension du fichier (`.entity`, `.staticModel`).
- Le **ContentBrowserControl** initie le drag via `DragDrop.DoDragDrop()` en passant un objet `AssetInfo` brut.
- Un second chemin utilise un JSON sérialisé `DragAndDropInfo` (action `Create` + type `Entity`/`PlayerStart`) pour les éléments du toolbox (pas des assets).
- La logique de création d'entité (instanciation + composants + positionnement) est **couplée** au contrôle WPF world editor.
- Il n'y a **aucun mécanisme réutilisable** : un futur contrôle (éditeur de prefabs, éditeur de tilemap, éditeur de screens…) devrait dupliquer toute cette logique.
- Seuls 2 types d'assets sont supportés sur le drop (`.entity` et `.staticModel`). Les autres types (`.sprite`, `.anim2d`, `.material`, `.model`, `.texture`, `.skeletonAnim`, `.tileMap`, `.screen`) ne sont pas gérés.

### Problèmes
1. **Violation du Open/Closed Principle** : ajouter un nouveau type d'asset oblige à modifier `OnDrop()`.
2. **Code dupliqué à venir** : chaque contrôle cible devra ré-écrire la résolution asset → entity.
3. **Pas de validation** : aucun `DragOver` ne filtre les assets acceptés ; l'utilisateur n'a aucun feedback visuel.
4. **Couplage fort** : la logique métier (créer une entity avec les bons composants) vit dans un contrôle UI WPF.

### Objectifs
- Créer une architecture **Strategy / Registry** pour le drag & drop d'assets.
- Chaque type d'asset a un **handler** dédié (`IAssetDropHandler`) qui sait :
  - si le drop est accepté (validation)
  - comment créer l'entity à partir de l'`AssetInfo`
- Un **`AssetDropHandlerRegistry`** centralise les handlers et est utilisable depuis n'importe quel contrôle.
- `GameEditorWorldControl` et tout futur contrôle (prefab editor, etc.) délèguent au registry.
- Le mécanisme doit supporter le feedback visuel `DragOver` (curseur accepté/refusé).

### Non-objectifs
- Pas de refonte du ContentBrowser (le drag source reste inchangé).
- Pas d'undo/redo dans ce ticket (sera ajouté ensuite).
- Pas de preview 3D pendant le drag (ghost entity).

---

## 1) Stratégie de livraison

### Règle
Chaque tâche doit :
- compiler en Debug + DebugEditor
- ne pas casser le drag & drop existant
- être testable manuellement (drag un asset depuis le content browser vers le world editor)

---

## 2) Backlog détaillé (petites tâches)

> Convention : "✅ Done = compile + comportement identique ou démonstration claire"

---

### Tâche 1 — Créer l'interface `IAssetDropHandler`
**Objectif :** définir le contrat pour les handlers de drop.

Tâches :
- [x] Créer le fichier `CasaEngine.EditorUI/DragAndDrop/IAssetDropHandler.cs`
- [x] Définir l'interface :
  ```csharp
  public interface IAssetDropHandler
  {
      /// Extensions supportées (ex: ".staticModel", ".entity")
      IReadOnlyList<string> SupportedExtensions { get; }

      /// Retourne true si ce handler peut traiter cet asset (validation fine au-delà de l'extension)
      bool CanHandle(AssetInfo assetInfo);

      /// Crée et retourne une Entity configurée à partir de l'asset.
      /// Ne l'ajoute PAS au monde (c'est le contrôle appelant qui décide).
      Entity CreateEntity(AssetInfo assetInfo, CasaEngineGame game);
  }
  ```
- [x] S'assurer que le fichier compile

✅ Critère : build OK, interface disponible.

---

### Tâche 2 — Créer la classe `AssetDropHandlerRegistry`
**Objectif :** registre central qui résout le bon handler pour un asset donné.

Tâches :
- [x] Créer le fichier `CasaEngine.EditorUI/DragAndDrop/AssetDropHandlerRegistry.cs`
- [x] Implémenter :
  ```csharp
  public class AssetDropHandlerRegistry
  {
      private readonly List<IAssetDropHandler> _handlers = new();

      public void Register(IAssetDropHandler handler);
      public IAssetDropHandler? FindHandler(AssetInfo assetInfo);
      public bool CanHandle(AssetInfo assetInfo);
  }
  ```
  - `FindHandler` : parcourt les handlers, vérifie l'extension puis `CanHandle()`, retourne le premier match.
  - `CanHandle` : raccourci qui retourne `FindHandler(assetInfo) != null`.
- [x] Rendre le registry accessible (singleton statique ou injection via `EngineHost`). Privilégier une propriété statique `AssetDropHandlerRegistry.Instance` pour commencer.

✅ Critère : build OK, registry utilisable.

---

### Tâche 3 — Implémenter `EntityAssetDropHandler`
**Objectif :** handler pour les fichiers `.entity` (reprend la logique existante).

Tâches :
- [x] Créer `CasaEngine.EditorUI/DragAndDrop/Handlers/EntityAssetDropHandler.cs`
- [x] Supporter l'extension `.entity`
- [x] Dans `CreateEntity()` :
  - Créer un `EntityReference` via `EntityReference.CreateFromAssetInfo(assetInfo, game.AssetContentManager)`
  - Retourner `entityReference.Entity`
- [x] Enregistrer ce handler dans le registry au démarrage de l'éditeur

✅ Critère : build OK, le drag & drop d'un `.entity` fonctionne comme avant.

---

### Tâche 4 — Implémenter `StaticModelAssetDropHandler`
**Objectif :** handler pour les fichiers `.staticModel` (reprend la logique existante).

Tâches :
- [x] Créer `CasaEngine.EditorUI/DragAndDrop/Handlers/StaticModelAssetDropHandler.cs`
- [x] Supporter l'extension `.staticModel`
- [x] Dans `CreateEntity()` :
  - Créer une `Entity` avec `Name = Path.GetFileNameWithoutExtension(assetInfo.FileName)`
  - Créer un `StaticModelComponent` avec `StaticModelAssetId = assetInfo.Id`
  - Assigner comme `entity.RootComponent`
  - Retourner l'entity
- [x] Enregistrer ce handler dans le registry au démarrage de l'éditeur

✅ Critère : build OK, le drag & drop d'un `.staticModel` fonctionne comme avant.

---

### Tâche 5 — Refactorer `GameEditorWorldControl.OnDrop()` pour utiliser le registry
**Objectif :** remplacer le `if/else` par un appel au registry.

Tâches :
- [x] Dans `OnDrop()`, pour le chemin `AssetInfo` :
  - Appeler `AssetDropHandlerRegistry.Instance.FindHandler(assetInfo)`
  - Si un handler est trouvé, appeler `handler.CreateEntity(assetInfo, game)`
  - Appeler `CreateEntity(entity, mousePosition)` comme avant
  - Si aucun handler, logger un warning (comportement existant)
- [x] Supprimer le `if/else` sur les extensions dans `OnDrop()`
- [x] Conserver le chemin `DragAndDropInfo` (JSON) inchangé pour l'instant
- [x] Vérifier que le drag & drop `.entity` et `.staticModel` fonctionnent toujours

✅ Critère : comportement identique, code `OnDrop()` simplifié.

---

### Tâche 6 — Ajouter le feedback `DragOver` avec validation via le registry
**Objectif :** afficher un curseur accepté/refusé selon le type d'asset.

Tâches :
- [x] Dans `GameEditorWorldControl`, s'abonner à l'événement `DragOver`
- [x] Dans le handler `OnDragOver` :
  - Extraire l'`AssetInfo` du `DragEventArgs`
  - Si `AssetDropHandlerRegistry.Instance.CanHandle(assetInfo)` → `e.Effects = DragDropEffects.Copy`
  - Sinon → `e.Effects = DragDropEffects.None`
  - Mettre `e.Handled = true`
- [x] S'assurer que `AllowDrop="True"` est bien défini dans le XAML du contrôle

✅ Critère : curseur adapté lors du survol selon le type d'asset.

---

### Tâche 7 — Créer une classe helper `AssetDropHelper` pour les contrôles cibles
**Objectif :** factoriser la logique commune de drop (extraction AssetInfo, appel registry, positionnement) dans un helper réutilisable.

Tâches :
- [x] Créer `CasaEngine.EditorUI/DragAndDrop/AssetDropHelper.cs`
- [x] Méthodes :
  ```csharp
  public static class AssetDropHelper
  {
      /// Extrait l'AssetInfo d'un DragEventArgs, ou null.
      public static AssetInfo? ExtractAssetInfo(DragEventArgs e);

      /// Gère le DragOver : valide via le registry et met à jour e.Effects.
      public static void HandleDragOver(DragEventArgs e);

      /// Gère le Drop : crée l'entity via le registry et la retourne (ou null).
      public static Entity? HandleDrop(DragEventArgs e, CasaEngineGame game);
  }
  ```
- [x] Refactorer `GameEditorWorldControl` pour utiliser `AssetDropHelper`

✅ Critère : `GameEditorWorldControl.OnDrop()` devient très court, la logique est réutilisable.

---

### Tâche 8 — Implémenter `SpriteAssetDropHandler`
**Objectif :** supporter le drag & drop de `.sprite` → entity avec `StaticSpriteComponent`.

Tâches :
- [x] Créer `CasaEngine.EditorUI/DragAndDrop/Handlers/SpriteAssetDropHandler.cs`
- [x] Supporter l'extension `.sprite`
- [x] Dans `CreateEntity()` :
  - Créer une entity avec un `StaticSpriteComponent`
  - Configurer le sprite asset id
- [x] Enregistrer dans le registry

✅ Critère : drag & drop d'un sprite crée une entity visible.

---

### Tâche 9 — Implémenter `Animation2dAssetDropHandler`
**Objectif :** supporter le drag & drop de `.anim2d` → entity avec `AnimatedSpriteComponent`.

Tâches :
- [x] Créer `CasaEngine.EditorUI/DragAndDrop/Handlers/Animation2dAssetDropHandler.cs`
- [x] Supporter l'extension `.anim2d`
- [x] Dans `CreateEntity()` :
  - Créer une entity avec un `AnimatedSpriteComponent`
  - Configurer l'animation asset
- [x] Enregistrer dans le registry

✅ Critère : drag & drop d'une animation 2D crée une entity avec animation.

---

### Tâche 10 — Refactorer le chemin `DragAndDropInfo` (JSON) en handlers
**Objectif :** unifier le mécanisme toolbox avec le même pattern handler.

Tâches :
- [x] Créer l'interface `IToolboxDropHandler` (ou étendre `IAssetDropHandler` avec un second contrat)
  ```csharp
  public interface IToolboxDropHandler
  {
      string SupportedType { get; } // ex: "Entity", "PlayerStart"
      bool CanHandle(DragAndDropInfo info);
      Entity CreateEntity(DragAndDropInfo info);
  }
  ```
- [x] Créer `ToolboxDropHandlerRegistry`
- [x] Implémenter `EmptyEntityToolboxHandler` (type `Entity`)
- [x] Implémenter `PlayerStartToolboxHandler` (type `PlayerStart`)
- [x] Refactorer `OnDrop()` pour utiliser le registry toolbox
- [x] Le code `OnDrop()` final ne contient plus aucune logique métier de création

✅ Critère : les deux chemins (asset + toolbox) passent par des registries, `OnDrop()` est un pur dispatcher.

---

### Tâche 11 — Initialisation centralisée des handlers
**Objectif :** enregistrer tous les handlers au démarrage de l'éditeur.

Tâches :
- [x] Créer `CasaEngine.EditorUI/DragAndDrop/DragAndDropConfiguration.cs`
- [x] Méthode statique `RegisterAllHandlers()` qui enregistre tous les handlers dans les registries
- [x] Appeler cette méthode au démarrage de l'éditeur (dans `App.xaml.cs` ou `EngineHost`)
- [x] Vérifier que tous les types existants sont couverts

✅ Critère : un seul point d'entrée pour la configuration du D&D.

---

## 3) Architecture cible (résumé)

```
ContentBrowserControl (drag source)
    │
    │ DragDrop.DoDragDrop(AssetInfo)
    ▼
┌─────────────────────────────────────────────┐
│           AssetDropHelper (static)          │
│  ExtractAssetInfo() / HandleDragOver()      │
│  HandleDrop() → AssetDropHandlerRegistry    │
└──────────────────┬──────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────┐
│       AssetDropHandlerRegistry              │
│  FindHandler(AssetInfo) → IAssetDropHandler │
├─────────────────────────────────────────────┤
│  EntityAssetDropHandler      (.entity)      │
│  StaticModelAssetDropHandler (.staticModel) │
│  SpriteAssetDropHandler      (.sprite)      │
│  Animation2dAssetDropHandler (.anim2d)      │
│  ... (extensible)                           │
└──────────────────┬──────────────────────────┘
                   │
                   ▼
              Entity (configured)
                   │
    ┌──────────────┼──────────────┐
    ▼              ▼              ▼
WorldEditor   PrefabEditor   FutureControl
(add to       (add to        (add to
 world)        prefab)        context)
```

## 4) Fichiers impactés

| Fichier | Modification |
|---------|-------------|
| `CasaEngine.EditorUI/DragAndDrop/IAssetDropHandler.cs` | Nouveau — interface |
| `CasaEngine.EditorUI/DragAndDrop/AssetDropHandlerRegistry.cs` | Nouveau — registry |
| `CasaEngine.EditorUI/DragAndDrop/AssetDropHelper.cs` | Nouveau — helper statique |
| `CasaEngine.EditorUI/DragAndDrop/DragAndDropConfiguration.cs` | Nouveau — initialisation |
| `CasaEngine.EditorUI/DragAndDrop/Handlers/EntityAssetDropHandler.cs` | Nouveau — handler .entity |
| `CasaEngine.EditorUI/DragAndDrop/Handlers/StaticModelAssetDropHandler.cs` | Nouveau — handler .staticModel |
| `CasaEngine.EditorUI/DragAndDrop/Handlers/SpriteAssetDropHandler.cs` | Nouveau — handler .sprite |
| `CasaEngine.EditorUI/DragAndDrop/Handlers/Animation2dAssetDropHandler.cs` | Nouveau — handler .anim2d |
| `CasaEngine.EditorUI/Controls/WorldControls/GameEditorWorldControl.xaml.cs` | Refactor — simplifier OnDrop |
| `CasaEngine.EditorUI/DragAndDrop/DragAndDropInfo.cs` | Existant — inchangé |
| `CasaEngine.EditorUI/DragAndDrop/DragAndDropInfoAction.cs` | Existant — inchangé |
| `CasaEngine.EditorUI/DragAndDrop/DragAndDropInfoType.cs` | Existant — inchangé |

## 5) Ordre d'exécution recommandé

1. **Tâches 1-2** : fondation (interface + registry)
2. **Tâches 3-4** : handlers existants (pas de changement de comportement)
3. **Tâche 5** : refactor `OnDrop()` (moment critique — comportement identique)
4. **Tâche 6** : feedback `DragOver`
5. **Tâche 7** : helper réutilisable
6. **Tâches 8-9** : nouveaux handlers (gain fonctionnel)
7. **Tâche 10** : unification toolbox
8. **Tâche 11** : initialisation centralisée
