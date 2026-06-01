# Material Inspector Stability Fix — Plan d'action IA

## Règles obligatoires pour l'agent

1. Traiter une seule tâche à la fois.
2. Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`.
3. À la fin de chaque tâche terminée, remplacer l'icône par `✅`.
4. Si une tâche nécessite une validation complémentaire non faite, utiliser `🧪`.
5. Si une tâche est bloquée, utiliser `⚠️` et ajouter une note courte juste dessous.
6. Mettre à jour ce fichier dans le même commit que le code de la tâche.
7. Faire un commit compilable après chaque tâche atomique.

## Validation minimale par tâche

- `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- Si un workflow visible change: smoke test éditeur ciblé.

## Tâches

- ✅ Stabiliser le cycle de vie des rows de l'inspector
  Objectif: construire les contrôles de propriété une seule fois par asset chargé et supprimer le rebuild complet à chaque changement de valeur.
  Fichiers ciblés:
  - `CasaEngine.Editor/Controls/MaterialAssetInspectorPanel.cs`
  Commit conseillé: `fix(editor): stop rebuilding material inspector rows on every edit`

- ✅ Introduire des bindings d'éditeurs stables
  Objectif: encapsuler les contrôles `MGCheckBox`, `MGSlider`, `NumericField`, `ColorEditor`, `Vector3Editor`, `AssetSelector`, `MGComboBox` et `MGTextBox` pour mettre à jour une valeur sans reboucle ni perte d'état.
  Fichiers ciblés:
  - `CasaEngine.Editor/Controls/MaterialAssetInspectorPanel.cs`
  Commit conseillé: `refactor(editor): add stable material property editor bindings`

- ✅ Rafraîchir seulement la propriété modifiée
  Objectif: mettre à jour badge, bouton Reset, texte de source et preview pour la propriété touchée, sans reconstruire l'arbre UI complet.
  Fichiers ciblés:
  - `CasaEngine.Editor/Controls/MaterialAssetInspectorPanel.cs`
  - `CasaEngine.Editor/Controls/MaterialPreviewViewport.cs`
  Commit conseillé: `fix(editor): refresh only changed material property row`

- ✅ Supprimer le reload global post-save du material
  Objectif: éviter qu'un save initié par l'inspector recharge le panel depuis le disque et remplace aussi le scan global des dépendances materials par un index réutilisable.
  Fichiers ciblés:
  - `CasaEngine.EditorServices/EditorAssetWriterService.cs`
  - `CasaEngine.Editor/Game1.cs`
  - `CasaEngine/Framework/Game/CasaEngineGame.cs`
  - `CasaEngine/Framework/Materials/MaterialDependencyIndex.cs`
  Commit conseillé: `fix(editor): stop reloading material inspector on self save`

- ✅ Valider le flux d'édition material
  Objectif: vérifier build + smoke d'ouverture d'un material dans l'éditeur, sans régression de chargement ni de preview.
  Fichiers ciblés:
  - `CasaEngine.Editor/Controls/MaterialAssetInspectorPanel.cs`
  - `CasaEngine.Editor/Controls/MaterialPreviewViewport.cs`
  - `CasaEngine.EditorServices/EditorAssetWriterService.cs`
  - `CasaEngine.Editor/Game1.cs`
  - `CasaEngine/Framework/Game/CasaEngineGame.cs`
  - `CasaEngine/Framework/Materials/MaterialDependencyIndex.cs`
  Commit conseillé: `test(editor): validate stable material inspector editing workflow`
  Note: `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore` passe. `CasaEngine.Tests` reste bloqué par des erreurs préexistantes dans `CasaEngine.Tests/Editor/MaterialEditorWorkspaceTests.cs`.