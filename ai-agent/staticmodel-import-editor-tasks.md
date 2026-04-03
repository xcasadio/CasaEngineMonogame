# StaticModel Import + Editor Tasks

## Règles d'exécution
- Faire 1 commit par sous-tâche atomique.
- Mettre à jour le statut avant de démarrer puis à la fin de la sous-tâche.
- Toujours laisser le build compilable.
- Utiliser une validation bornée après chaque sous-tâche.

## Tâches

- ✅ T1 - Afficher les noms réels des composants dans l'éditeur
  Objectif:
  - Afficher le nom d'instance du composant quand il existe, au lieu du seul DisplayName.
  - Exemple cible: `Sub Mesh [WheelFrontRight / chrome]`.

- ✅ T2 - Générer des noms de slot lisibles
  Objectif:
  - Générer des noms stables à partir du noeud et du matériau source.
  - Exemple cible: `WheelFrontRight / chrome`, `Car / paint`, `glass`.

- ✅ T3 - Importer les textures vers des slots runtime
  Objectif:
  - Importer ou résoudre les textures source en vrais assets utilisables via `TextureAssetId`.

- ✅ T4 - Importer les matériaux vers des slots runtime
  Objectif:
  - Générer ou résoudre des assets `.material` puis renseigner `MaterialAssetId`.

- ✅ T5 - Valider build et cas voiture
  Objectif:
  - Vérifier le build ciblé et le pipeline sur `Car.x` / `Car.staticModel`.
  Validation réalisée:
  - Smoke test OK sur `kid_idle.FBX`: génération de `kid_idle.staticModel`, des `.texture` et des `.material`, avec `texture_asset_id` et `material_asset_id` non vides sur les meshes.
  - Smoke test OK sur `Car.x`: génération de `Car.staticModel` avec noms de slots lisibles et `material_asset_id` non vides; `texture_asset_id` reste vide car les textures référencées par `Car.x` ne sont pas présentes dans ce repo.