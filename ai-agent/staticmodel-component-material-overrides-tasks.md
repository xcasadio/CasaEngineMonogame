# StaticModelComponent Material Overrides — Plan d'implémentation

## Objectif

Ajouter une gestion moderne des matériaux sur `StaticModelComponent` et composants apparentés, avec la séparation suivante :
- matériaux par défaut stockés dans l'asset (`StaticModel`, `StaticModelMesh`, `SubMesh`),
- overrides de matériaux stockés sur l'instance de composant,
- overrides de paramètres fins conservés dans `PropertyOverrides`.

Le résultat attendu est similaire à Unity / Unreal / Godot :
- l'asset définit les slots de matériau,
- le composant peut override un ou plusieurs slots sans muter l'asset partagé,
- le renderer consomme le matériau résolu par instance,
- les overrides survivent au save/load et restent stables au reimport.

## Contraintes obligatoires

- 1 commit par sous-tâche atomique.
- Mettre à jour le statut avant démarrage puis à la fin.
- Toujours laisser le build compilable.
- Validation bornée après chaque sous-tâche.
- Ne pas ajouter d'allocations évitables dans `Draw()` / `Flush()`.
- Ne jamais muter les matériaux par défaut stockés sur l'asset au moment d'appliquer un override d'instance.

## Critères d'acceptation

- Un `StaticModelComponent` expose une liste de slots avec matériau par défaut, override éventuel et matériau résolu.
- Un override appliqué sur une instance n'affecte pas les autres instances du même `StaticModel`.
- Le save/load d'une entity conserve les overrides.
- Le reimport d'un modèle conserve les overrides si les noms de slot restent stables.
- Le renderer utilise l'override d'instance si présent, sinon le matériau de l'asset, sinon le fallback texture legacy.
- L'éditeur permet d'éditer les overrides depuis le composant racine, pas depuis les sous-composants générés.

## Architecture cible

- `StaticModelMesh` et `SubMesh` gardent les références par défaut de l'asset.
- `StaticModelComponent` porte une collection sérialisée d'overrides par slot.
- `StaticModelSubMeshComponent` reçoit un matériau résolu en runtime, en lecture seule côté auteur.
- La résolution suit l'ordre :
  `Override du composant -> Matériau asset du submesh/mesh -> Texture fallback legacy`.
- Les slots sont adressés par une clé stable :
  `SlotName` en priorité, `SlotIndex` en fallback.

## Tâches

- ⏳ T1 - Définir le modèle de données des overrides d'instance
  Objectif:
  - Créer une structure sérialisable dédiée, par exemple `MaterialSlotOverride`, avec au minimum `SlotName`, `SlotIndex`, `MaterialAssetId`.
  - Ajouter la collection d'overrides à `StaticModelComponent`.
  - Préparer une API de résolution sans casser la compatibilité existante.
  Sortie attendue:
  - Les overrides existent dans le runtime et sont sérialisables.
  Validation:
  - `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug --no-restore`
  Commit conseillé:
  - `feat(materials): add static model material override data`

- ⏳ T2 - Introduire des clés de slot stables pour le matching
  Objectif:
  - Ajouter une notion explicite de slot stable sur `StaticModelMesh` et, si nécessaire, `SubMesh`.
  - Distinguer si besoin `SlotName` et `DisplayName` pour éviter qu'un renommage UI casse le matching.
  - Faire persister cette clé dans le JSON et la remplir à l'import.
  Sortie attendue:
  - Les slots ont une identité stable pour reimport et overrides.
  Validation:
  - Smoke test import sur `kid_idle.FBX` et vérification du JSON généré.
  Commit conseillé:
  - `feat(import): persist stable material slot keys`

- ⏳ T3 - Implémenter le résolveur de matériaux par instance
  Objectif:
  - Ajouter une couche de résolution dédiée, par exemple `StaticModelMaterialResolver` ou méthode équivalente dans `StaticModelComponent`.
  - Résoudre le matériau effectif de chaque mesh/submesh au `InitializeWithWorld()`.
  - Ne jamais écraser `mesh.MaterialAssetId` ou `mesh.Material` pour porter un override d'instance.
  Sortie attendue:
  - Le matériau résolu existe côté instance, indépendamment de l'asset partagé.
  Validation:
  - Deux entities utilisant le même `.staticModel` peuvent afficher des matériaux différents sans se polluer.
  Commit conseillé:
  - `feat(materials): resolve per-instance static model materials`

- ⏳ T4 - Étendre le chemin renderer pour consommer les overrides
  Objectif:
  - Ajouter un `MaterialBase? materialOverride` ou équivalent dans le flux `StaticModelSubMeshComponent -> StaticMeshRendererComponent -> RenderItem`.
  - Conserver le fallback existant : override d'instance, matériau asset, texture asset.
  - Vérifier qu'il n'y a pas d'allocations supplémentaires par frame.
  Sortie attendue:
  - Le renderer dessine le matériau résolu de l'instance.
  Validation:
  - Build + vérification manuelle avec 2 instances dans la même world.
  Commit conseillé:
  - `feat(renderer): support static model material overrides`

- ⏳ T5 - Sauvegarder et recharger les overrides dans les entities
  Objectif:
  - Étendre `EditorEntityJsonSerializer` et le load runtime pour persister la collection d'overrides sur `StaticModelComponent`.
  - Garder la compatibilité avec les entities existantes qui n'ont pas encore ce bloc JSON.
  Sortie attendue:
  - Les overrides survivent au save/load de la scène ou de l'entity.
  Validation:
  - Save d'une entity, reload, vérification que les GUIDs d'override sont restaurés.
  Commit conseillé:
  - `feat(serialization): persist static model material overrides`

- ⏳ T6 - Exposer l'édition des overrides dans l'éditeur
  Objectif:
  - Ajouter dans l'éditeur du `StaticModelComponent` une section `Material Overrides` listant les slots.
  - Pour chaque slot, afficher : nom du slot, matériau par défaut, override, matériau résolu.
  - Permettre de choisir / effacer un override via `AssetSelector`.
  - Laisser `StaticModelSubMeshComponent` en lecture seule pour inspection.
  Sortie attendue:
  - L'auteur édite les overrides depuis le composant racine.
  Validation:
  - Ouvrir une entity avec `StaticModelComponent`, changer un slot, constater l'effet visuel et la persistance.
  Commit conseillé:
  - `feat(editor): edit static model material overrides`

- ⏳ T7 - Gérer proprement la stabilité au reimport
  Objectif:
  - Au reimport, rematcher les overrides par `SlotName` puis fallback sur `SlotIndex`.
  - Signaler proprement les overrides orphelins si un slot a disparu.
  - Éviter de perdre les overrides si seul l'ordre interne des meshes change.
  Sortie attendue:
  - Les overrides restent attachés aux bons slots après reimport raisonnable.
  Validation:
  - Reimport d'un modèle avec même noms de slot, vérifier conservation des overrides.
  Commit conseillé:
  - `feat(import): preserve material overrides on reimport`

- ⏳ T8 - Factoriser la logique pour les composants apparentés
  Objectif:
  - Extraire la logique commune dans une abstraction réutilisable par `SkinnedMeshComponent` et futurs composants mesh.
  - Créer si utile une interface du type `IMaterialOverrideOwner` ou un helper partagé.
  - Adapter le premier composant dérivé utile après `StaticModelComponent`.
  Sortie attendue:
  - La feature n'est pas figée dans un seul component et peut être étendue proprement.
  Validation:
  - Build ciblé et vérification du composant secondaire choisi.
  Commit conseillé:
  - `refactor(materials): share material override pipeline across mesh components`

- ⏳ T9 - Ajouter des tests et une validation bornée de bout en bout
  Objectif:
  - Ajouter au minimum des tests purs pour l'algorithme de matching `SlotName -> SlotIndex`.
  - Conserver une validation manuelle bornée sur `kid_idle.FBX` et un cas à textures manquantes comme `Car.x`.
  - Documenter brièvement le comportement dans un fichier `ai-agent` ou `docs` si nécessaire.
  Sortie attendue:
  - Le comportement est vérifié et reproductible.
  Validation:
  - `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug --no-restore`
  - Test pur ciblé si ajouté.
  Commit conseillé:
  - `test(materials): cover static model override matching`

## Ordre recommandé d'exécution

1. T1
2. T2
3. T3
4. T4
5. T5
6. T6
7. T7
8. T8
9. T9

## Risques à surveiller

- Risque de muter le matériau partagé de l'asset au lieu de porter un override par instance.
- Risque de lier les overrides à l'index seul et de perdre les correspondances au reimport.
- Risque d'introduire de la logique d'édition dans les sous-composants générés au lieu de centraliser sur le composant racine.
- Risque d'ajouter des allocations en hot path si la résolution n'est pas préparée à l'initialisation.

## Notes de design pour l'agent

- Ne pas faire du `StaticModelSubMeshComponent` le point d'autorité pour l'authoring des matériaux.
- Préférer un affichage editor de type tableau des slots sur `StaticModelComponent`.
- Conserver `PropertyOverrides` pour les overrides fins de paramètres shader, pas pour remplacer un matériau entier.
- Si une décision API est ambiguë, privilégier `SlotName + SlotIndex` plutôt que `meshIndex` seul.