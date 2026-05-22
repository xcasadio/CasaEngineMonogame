# Plan agent IA - Profondeur TileMap et integration des entities

Objectif : implementer une gestion moderne et progressive de la profondeur 2D pour que les personnages et autres entities puissent s'integrer facilement dans un monde TileMap, sans les faire appartenir a `TileMapComponent`, sans casser le chunking, et sans s'appuyer uniquement sur `z_offset`.

Regles d'execution pour l'agent IA :

- Faire un commit apres chaque tache terminee.
- Garder une tache petite et compilable.
- Mettre a jour l'icone de statut devant le nom de la tache dans ce fichier avant chaque commit.
- Utiliser les statuts suivants :
  - ⏳ Todo
  - 🚧 In progress
  - ✅ Done
  - 🧪 Needs testing
  - ⚠️ Blocked
- Ne pas committer les binaires generes dans `Projects/RPGDemo`.
- Mettre a jour `RPGDemo` pour montrer ou preparer l'usage de la nouvelle profondeur 2D.
- Apres chaque tache C#, lancer au minimum un build cible ou les tests pertinents.

## Taches

- ✅ Creer le plan agent detaille
  - Creer ce fichier de plan.
  - Reprendre les contraintes du document `docs/tilemaps-gestion-profondeur.md`.
  - Inclure explicitement la mise a jour de `RPGDemo`.
  - Commit attendu : `Add tilemap depth integration agent plan`.

- ✅ Ajouter les fondations de tri 2D
  - Ajouter une representation comparable de la cle de tri 2D.
  - Ajouter les enums minimales pour render pass et mode de tri.
  - Ajouter des helpers sans allocation par frame.
  - Ajouter des tests unitaires sur l'ordre lexicographique et le tie-breaker stable.
  - Commit attendu : `Add 2D depth sort key foundation`.

- ✅ Ajouter la metadata de profondeur TileMap
  - Ajouter un modele de metadata `depth.*` pour les layers et object layers TileMap.
  - Lire les custom properties existantes sans casser `z_offset`.
  - Fournir des valeurs par defaut compatibles avec les assets actuels.
  - Ajouter des tests de parsing des roles, anchors, elevations et valeurs par defaut.
  - Commit attendu : `Add tilemap depth metadata parsing`.

- ✅ Ajouter un composant de profondeur pour entities
  - Ajouter un composant de type `DepthSortable2DComponent` ou equivalent.
  - Exposer `RenderPass`, `SortingLayer`, `OrderInLayer`, `Elevation`, `SortAnchorLocal`, `LocalSortOffset` et `StableId`.
  - Calculer un `RenderSortKey2D` depuis la position monde et le `RenderFrame` quand disponible.
  - Charger les champs depuis JSON avec valeurs par defaut.
  - Ajouter des tests de calcul de SortAnchor et de cle de tri si possible.
  - Commit attendu : `Add entity 2D depth component`.

- ✅ Connecter les sprites a la cle de profondeur optionnelle
  - Etendre `SpriteRendererComponent` pour accepter une cle de tri optionnelle en plus du `z` historique.
  - Conserver les overloads existants pour compatibilite.
  - Trier les sprites par cle moderne quand elle est fournie, sinon conserver le comportement historique base sur `z`.
  - Eviter les closures nouvelles dans les chemins `Draw`.
  - Commit attendu : `Use optional 2D depth keys for sprites`.

- ✅ Brancher les components sprite sur la profondeur entity
  - Faire consommer `DepthSortable2DComponent` par `StaticSpriteComponent`.
  - Faire consommer `DepthSortable2DComponent` par `AnimatedSpriteComponent`.
  - Garantir un fallback identique au comportement actuel si le composant est absent.
  - Commit attendu : `Route sprite components through 2D depth`.

- ✅ Preparar l'integration TileMap avec les roles de profondeur
  - Exposer les metadata de profondeur des layers TileMap depuis `TileMapLayerData`.
  - Documenter ou coder le mapping par defaut des layers existants vers les passes fixes.
  - Conserver le rendu chunk statique pour les layers fixes.
  - Ajouter des tests ou une validation de non-regression TileMap.
  - Commit attendu : `Prepare tilemap depth roles for rendering`.

- ⏳ Mettre a jour RPGDemo
  - Ajouter les proprietes `depth.*` utiles dans les assets RPGDemo ou dans le script de setup.
  - Ajouter au joueur une configuration de profondeur 2D exploitable par le nouveau systeme.
  - Ajouter au moins un exemple documente ou visible de layer/objet compatible avec la profondeur 2D.
  - Verifier que `CasaEngine.RPGDemo` via `CasaEngine.Launcher` continue de charger la map.
  - Commit attendu : `Update RPGDemo for 2D depth integration`.

- ⏳ Validation finale
  - Lancer les tests pertinents.
  - Lancer un build de `CasaEngine.Launcher`.
  - Verifier `git status --short`.
  - Mettre tous les statuts a jour.
  - Commit attendu : `Finalize tilemap depth integration validation` si seul le plan ou la documentation de validation change.
