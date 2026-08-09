# Plan agent IA - Visualisateur Sprite et thumbnails Sprite

## Objectif

Mettre en place un visualisateur de sprite qui s'ouvre au double-clic sur un fichier `.sprite`, suit la philosophie des viewers déjà présents (`.particle`, `.entity`, `.anim2d`, `.tileMap`), affiche le sprite tel que le moteur le rend, expose un inspector éditable couvrant tout `SpriteData` dont les changements se répercutent immédiatement sur le visualisateur, laisse `Hierarchy` et `Toolbox` volontairement vides pour ce type de document, et met à jour les thumbnails de sprites quand le sprite change ou quand aucun thumbnail n'est encore disponible.

Décisions validées par le demandeur:
- l'inspector doit éditer tout `SpriteData`;
- il n'existe actuellement pas de thumbnail sprite, seulement l'icône d'asset par défaut, donc le process doit créer un vrai thumbnail rendu par le moteur;
- le thumbnail doit être recadré au plus près du sprite tout en gardant le chemin de rendu moteur;
- la sauvegarde d'un `.sprite` doit rafraîchir les worlds ouverts qui consomment ce sprite.

Le plan ci-dessous est volontairement basé uniquement sur l'état confirmé du code et sur ces décisions validées.

## Règles d'exécution pour l'agent

- Ne pas démarrer une tâche `⚠️ Blocked` tant que la réponse utilisateur n'est pas obtenue.
- Faire un commit à la fin de chaque tâche terminée.
- Ne pas ajouter un workaround local juste pour `.sprite`.
- Ne pas rendre les thumbnails sprite via un crop `System.Drawing` ad hoc si l'objectif est de correspondre au rendu moteur.
- Ne pas ajouter un nouveau maillon monolithique sans traiter la question d'architecture du routage d'ouverture.

## Etat confirmé dans le code

### ✅ SPRITE-ANALYSE-000 - Relever l'existant

Objectif:
Documenter les points d'ancrage réels avant implémentation.

Constats confirmés:
- L'extension `.sprite` existe déjà dans `CasaEngine.Framework.Configuration.Constants.FileNameExtensions.Sprite`.
- Le modèle d'asset existe déjà via `CasaEngine.Framework.Assets.Sprites.SpriteData` avec `SpriteSheetAssetId`, `PositionInTexture`, `Origin`, `Sockets` et `CollisionShapes`.
- La sérialisation éditeur de `SpriteData` existe déjà via `EditorAssetJsonSerializer.SaveSpriteData(...)`.
- Le rendu runtime confirmé passe par `StaticSpriteComponent.Draw(...)` puis `SpriteRendererComponent.DrawSprite(...)`, avec source rectangle et origin issus de `SpriteData`.
- Le double-clic Content Browser suit déjà le flux `ContentBrowserPanel.FileOpened -> GameEditor.OnContentBrowserFileOpened -> GameEditor.TryOpenEditorAsset(...)`.
- Le routage d'ouverture d'asset est aujourd'hui une chaîne monolithique de `TryOpen...` dans `GameEditor` pour `UIScreen`, `AnimationClip`, `Entity`, `Animation2d`, `Particle`, `Cutscene`, `TileMap`, `Material`.
- Les panneaux contextuels `Hierarchy`, `Inspector` et `Toolbox` sont déjà pilotés par `EditorDocumentKind` via `ContextualDockPanelHost`.
- Il n'existe actuellement ni `EditorDocumentKind.Sprite`, ni `EditorHistoryContextKind.Sprite`, ni `EditorPanelIds.SpriteAssetDocumentPrefix`.
- Le Content Browser classe aujourd'hui `.sprite` en `ContentItemType.Animation`, pas en type sprite dédié.
- `ContentItemType` n'a pas encore de valeur `Sprite` et `ContentItemDisplay` n'a ni icône ni label sprite dédiés.
- `ThumbnailCache` ne supporte actuellement que `Texture` et `Particle`.
- Le rendu de thumbnail `Particle` réutilise déjà un pipeline runtime dédié (`ParticleSceneThumbnailRenderer`) et non un simple fallback bitmap.
- `ContentBrowserPanel` invalide les thumbnails lors de certaines opérations de fichier locales comme rename/delete, mais n'écoute pas `EditorAssetWriterService.AssetSaved`.
- Aucun process de thumbnail persistant sur disque n'a été identifié dans l'éditeur; le process existant confirmé est un cache mémoire/runtime.

Commit attendu:
- `docs(ai-agent): capture sprite viewer baseline`

Validation:
- Aucune, tâche d'analyse déjà réalisée.

## Decisions de scope validees

### ✅ SPRITE-SCOPE-001 - Inspector `SpriteData` complet

Decision:
L'inspector doit rendre éditables toutes les propriétés sérialisées de `SpriteData`: `SpriteSheetAssetId`, `PositionInTexture`, `Origin`, `Sockets` et `CollisionShapes`.

Impact:
- le viewer et le hot-reload doivent supporter les changements de géométrie, d'origine, de texture source, de sockets et de collisions;
- le panneau inspector ne peut pas se limiter à un sous-ensemble visuel.

### ✅ SPRITE-SCOPE-002 - Creation du vrai thumbnail sprite

Decision:
Il n'existe pas actuellement de thumbnail sprite; ce qui est affiché est l'icône d'asset par défaut. Le process doit donc créer un vrai thumbnail sprite rendu par le moteur.

Impact:
- il faut étendre le pipeline de thumbnails existant à `Sprite`;
- il ne faut pas ajouter de stockage sur disque tant qu'aucun process persistant n'est identifié comme standard dans le repo.

### ✅ SPRITE-SCOPE-003 - Recadrage serré avec chemin de rendu moteur

Decision:
Le thumbnail doit être recadré au plus près du sprite, tout en gardant un chemin de rendu moteur fidèle.

Impact:
- le rendu doit passer par une vraie scène/preview runtime;
- le cadrage final peut être resserré à partir du résultat rendu, mais pas remplacé par un crop bitmap de texture source.

### ✅ SPRITE-SCOPE-004 - Hot-reload des worlds ouverts

Decision:
La sauvegarde d'un `.sprite` doit rafraîchir les worlds ouverts qui consomment ce sprite.

Impact:
- le hot-reload doit couvrir les composants runtime sprite déjà présents dans les worlds ouverts;
- la feature ne se limite pas au viewer sprite et au Content Browser.

## Avis architecture

### ✅ SPRITE-ARCH-OBS-001 - Evaluation de l'architecture actuelle

Points sains a préserver:
- Le shell editor est déjà structuré par `EditorDocumentKind` et `ContextualDockPanelHost`.
- Les viewers existants ont déjà des cycles d'activation, d'onglets, de dirty state, de diagnostics d'automation et de contextual panels cohérents.
- Le pipeline `Particle` montre déjà le bon principe pour un thumbnail fidèle au runtime: réutiliser une scène/pipeline de preview, pas un rendu hors moteur.

Points faibles a traiter avant ou pendant l'ajout Sprite:
- Le routage d'ouverture d'assets est centralisé mais monolithique dans `GameEditor.TryOpenEditorAsset(...)`.
- Les types d'assets, ids de panel, historique, diagnostics et activation sont dupliqués par famille d'asset.
- `.sprite` est actuellement mal classé côté Content Browser, ce qui masque le besoin structurel derrière un faux type `Animation`.

Cible recommandée:
Avant d'ajouter le viewer sprite, introduire une extension point déclarative ou au minimum un facteur commun centralisé pour l'ouverture des documents d'asset, afin que `Sprite` ne soit pas un `TryOpen...` ad hoc supplémentaire. Le but est d'ajouter un nouveau type de document sans répéter toute la plomberie de routage, d'activation et de titres.

Commit attendu:
- Aucun, cette observation nourrit les tâches suivantes.

## Plan détaillé d'implémentation

### ✅ SPRITE-ARCH-001 - Centraliser le routage d'ouverture des documents d'asset

Objectif:
Remplacer ou encapsuler la chaîne actuelle de `GameEditor.TryOpenEditorAsset(...)` par un mécanisme déclaratif qui permette d'ajouter `Sprite` sans workaround local.

Travail demandé:
1. Recenser les responsabilités répétées dans les `TryOpenMaterialAsset`, `TryOpenParticleAsset`, `TryOpenAnimation2dAsset`, `TryOpenEntityAsset`, `TryOpenTileMapAsset`.
2. Extraire une structure de description de document d'asset: extension prise en charge, type de document, préfixe de panel, chargement d'asset, création du panel, activation, titre, diagnostics.
3. Rebrancher les types déjà supportés sur cette structure sans régression de comportement.
4. Laisser `Sprite` s'ajouter ensuite par configuration ciblée, pas par duplication d'un nouveau bloc monolithique.

Fichiers probables:
- `CasaEngine.Editor/GameEditor.cs`
- `CasaEngine.Editor/EditorDocumentKind.cs`
- `CasaEngine.Editor/Workspaces/EditorPanelIds.cs`
- `CasaEngine.Editor/History/EditorHistoryContext.cs`
- `CasaEngine.Editor/History/EditorHistoryContextKind.cs`
- nouveaux fichiers éventuels dans `CasaEngine.Editor/Workspaces/` ou `CasaEngine.Editor/` si une description de document est extraite

Critères d'acceptation:
- L'ouverture des types déjà supportés continue à fonctionner.
- L'ajout du type `Sprite` ne nécessite pas une nouvelle séquence complète de duplication `TryOpen...`.
- L'activation du document, le titre, le dirty state et le contexte actif restent cohérents.

Validation:
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`
- Smoke automation sur au moins un asset existant par famille déjà supportée via `--open-asset`

Commit attendu:
- `refactor(editor): centralize asset document routing`

### ✅ SPRITE-DOC-002 - Introduire le type de document Sprite dans le shell editor

Objectif:
Ajouter la notion de document `Sprite` dans toutes les primitives de navigation du shell.

Travail demandé:
1. Ajouter `Sprite` aux enums et identifiants de contexte nécessaires.
2. Ajouter le préfixe de panel document sprite.
3. Étendre l'historique, le dirty state et la résolution du document actif pour reconnaître `Sprite`.
4. Préparer l'activation/disposal du document sprite dans `GameEditor` au même niveau de qualité que `Particle`, `Animation2d`, `TileMap`.

Fichiers probables:
- `CasaEngine.Editor/EditorDocumentKind.cs`
- `CasaEngine.Editor/History/EditorHistoryContextKind.cs`
- `CasaEngine.Editor/History/EditorHistoryContext.cs`
- `CasaEngine.Editor/Workspaces/EditorPanelIds.cs`
- `CasaEngine.Editor/GameEditor.cs`

Critères d'acceptation:
- Le shell peut identifier un document sprite actif.
- Les panneaux contextuels peuvent se brancher sur `EditorDocumentKind.Sprite`.
- La fermeture/réouverture du document sprite suit les conventions existantes.

Validation:
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`

Commit attendu:
- `feat(editor): register sprite asset document kind`

### ✅ SPRITE-CB-003 - Corriger la classification Content Browser des fichiers `.sprite`

Objectif:
Faire apparaître les sprites comme un type d'asset dédié et non comme une animation.

Travail demandé:
1. Ajouter `ContentItemType.Sprite`.
2. Mapper `.sprite` vers ce type dédié dans `ContentItem`.
3. Ajouter label et icône dédiés dans `ContentItemDisplay`.
4. Vérifier les impacts sur tooltip, filtres, context menus et diagnostics du Content Browser.

Fichiers probables:
- `CasaEngine.Editor/ContentBrowser/Models/ContentItemType.cs`
- `CasaEngine.Editor/ContentBrowser/Models/ContentItem.cs`
- `CasaEngine.Editor/ContentBrowser/ContentItemDisplay.cs`
- tests Content Browser existants dans `CasaEngine.Tests/ContentBrowser/`

Critères d'acceptation:
- Un `.sprite` apparaît comme `Sprite` dans le Content Browser.
- Le double-clic continue à partir du même événement `FileOpened`.
- Aucun autre type existant n'est reclassé par erreur.

Validation:
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter ContentBrowser`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`

Commit attendu:
- `fix(editor): classify sprite assets explicitly`

### ✅ SPRITE-VIEW-004 - Créer le document de visualisation Sprite fidèle au runtime

Objectif:
Ouvrir un document sprite au double-clic et afficher le sprite tel que le moteur le rend.

Travail demandé:
1. Créer le panel document sprite dans `CasaEngine.Editor/Controls/` en restant cohérent avec la philosophie des viewers existants.
2. Charger un `SpriteData` depuis le fichier `.sprite` ouvert.
3. Reposer le rendu sur le chemin de rendu moteur 2D existant, pas sur un crop bitmap custom.
4. Gérer l'initialisation du viewport, le sizing et l'état vide quand aucun sprite n'est chargé.
5. Brancher l'ouverture de document sprite dans `GameEditor` via l'architecture cible décidée dans `SPRITE-ARCH-001`.

Fichiers probables:
- `CasaEngine.Editor/GameEditor.cs`
- nouveaux fichiers dans `CasaEngine.Editor/Controls/` pour le panel sprite et/ou son viewport
- éventuellement une utilité de preview runtime réutilisable si nécessaire

Critères d'acceptation:
- Un double-clic sur un `.sprite` ouvre un onglet document sprite.
- Un même sprite réouvre le même document logique plutôt que de dupliquer les onglets sans contrôle.
- Le sprite affiché respecte le rectangle source et l'origine du runtime.
- Aucun overlay debug non demandé n'est affiché par défaut.

Validation:
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`
- Smoke automation via `--open-asset <sprite>` et diagnostics du document sprite

Commit attendu:
- `feat(editor): add sprite asset viewer`

### ✅ SPRITE-INSPECT-005 - Ajouter l'inspector éditable du sprite

Objectif:
Exposer les propriétés du sprite dans le panel Inspector et refléter immédiatement les changements dans le visualisateur.

Travail demandé:
1. Construire un inspector sprite basé sur le périmètre validé dans `SPRITE-SCOPE-001`.
2. Appliquer les changements en mémoire sur l'asset chargé.
3. Rafraîchir le viewer sprite après chaque édition significative.
4. Gérer `dirty state`, sauvegarde, rechargement disque et message d'état comme les viewers existants.
5. S'appuyer sur `EditorAssetWriterService.SaveAsset(...)` et ajouter un `EditorAssetSaveSource` dédié si nécessaire.

Fichiers probables:
- nouveaux fichiers dans `CasaEngine.Editor/Controls/` pour l'inspector/view sprite
- `CasaEngine.Editor/GameEditor.cs`
- `CasaEngine.EditorServices/EditorAssetWriterService.cs`

Critères d'acceptation:
- L'inspector affiche les propriétés du sprite validées par le demandeur.
- Une modification se répercute immédiatement dans le visualisateur actif.
- La sauvegarde écrit un `.sprite` valide sans casser la structure JSON existante.

Validation:
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`
- automation `--open-asset <sprite>` puis édition pilotée si un hook est ajouté

Commit attendu:
- `feat(editor): edit sprite assets from inspector`

### ✅ SPRITE-CONTEXT-006 - Rendre `Hierarchy` et `Toolbox` volontairement vides pour Sprite

Objectif:
Faire suivre au viewer sprite la philosophie demandée: `Hierarchy` vide et `Toolbox` vide.

Travail demandé:
1. Enregistrer `EditorDocumentKind.Sprite` dans les hosts contextuels concernés.
2. Fournir un contenu minimal explicite et non ambigu pour `Hierarchy`.
3. Fournir un contenu minimal explicite et non ambigu pour `Toolbox`.
4. Vérifier que l'activation d'un document sprite bascule correctement ces panneaux contextuels.

Fichiers probables:
- `CasaEngine.Editor/GameEditor.cs`

Critères d'acceptation:
- Quand un document sprite est actif, `Hierarchy` ne montre aucun arbre métier.
- Quand un document sprite est actif, `Toolbox` ne montre aucun outil métier.
- Le retour à un autre document restaure les panneaux contextuels attendus.

Validation:
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`
- smoke manuel/automation de changement de document actif

Commit attendu:
- `feat(editor): add sprite contextual empty panels`

### ✅ SPRITE-SAVE-007 - Intégrer la sauvegarde et le rafraîchissement post-save du document sprite

Objectif:
Faire suivre au sprite le même niveau d'intégration que les autres viewers: save source dédiée, refresh des documents ouverts et diagnostics propres.

Travail demandé:
1. Ajouter la source de sauvegarde sprite dans `EditorAssetSaveSource`.
2. Etendre `GameEditor.OnEditorAssetSaved(...)` pour reconnaître `.sprite`.
3. Recharger le document sprite ouvert correspondant si la sauvegarde vient d'ailleurs.
4. Mettre à jour le dirty state et le titre d'onglet du document sprite.
5. Limiter le périmètre au viewer/thumbnails sauf si `SPRITE-SCOPE-004` étend explicitement au hot-reload global des worlds ouverts.

Fichiers probables:
- `CasaEngine.EditorServices/EditorAssetWriterService.cs`
- `CasaEngine.Editor/GameEditor.cs`
- nouveau panel sprite dans `CasaEngine.Editor/Controls/`

Critères d'acceptation:
- Le document sprite sauvegardé revient à l'état non dirty.
- Une sauvegarde externe du même sprite recharge le viewer sprite concerné.
- Aucun autre viewer n'est rafraîchi par erreur.

Validation:
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`
- scénario de save/reload ciblé sur un `.sprite`

Commit attendu:
- `feat(editor): handle sprite asset saves`

### ✅ SPRITE-THUMB-008 - Ajouter le rendu runtime des thumbnails sprite

Objectif:
Générer des thumbnails de sprite via le moteur afin qu'ils correspondent au rendu attendu, en reprenant la philosophie du process particle existant.

Travail demandé:
1. Etendre `ThumbnailCache` pour supporter `ContentItemType.Sprite`.
2. Introduire un renderer dédié de thumbnail sprite dans `CasaEngine.Editor/ContentBrowser/Services/`.
3. Réutiliser un chemin de rendu moteur 2D, pas un simple découpage bitmap de la texture source.
4. Définir explicitement la règle de cadrage retenue après validation de `SPRITE-SCOPE-003`.
5. Conserver le traitement GPU côté thread principal comme pour les thumbnails particle si le pipeline choisi l'impose.

Fichiers probables:
- `CasaEngine.Editor/ContentBrowser/Services/ThumbnailCache.cs`
- nouveau renderer sprite dans `CasaEngine.Editor/ContentBrowser/Services/`
- `CasaEngine.Editor/Controls/ContentBrowserPanel.cs`
- tests `CasaEngine.Tests/ContentBrowser/ThumbnailCacheTests.cs`

Critères d'acceptation:
- Un `.sprite` obtient un thumbnail au premier affichage si aucun thumbnail n'est encore disponible dans le process confirmé.
- Le thumbnail utilise le même rectangle source et la même logique de rendu que le moteur.
- Le rendu ne bascule pas sur une génération bitmap simplifiée incompatible avec le moteur.

Validation:
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter ThumbnailCache`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`

Commit attendu:
- `feat(editor): render sprite thumbnails via engine pipeline`

### ✅ SPRITE-THUMB-009 - Invalider et régénérer les thumbnails sprite sur changement

Objectif:
Faire en sorte qu'un thumbnail sprite se mette à jour après une modification d'asset et se régénère lorsqu'il manque.

Travail demandé:
1. Brancher une invalidation thumbnail sprite sur `EditorAssetWriterService.AssetSaved` via `GameEditor` et/ou `ContentBrowserPanel`.
2. Vérifier que le Content Browser n'a pas besoin d'un refresh complet pour refléter le nouveau thumbnail.
3. Préserver les invalidations déjà en place sur rename/delete.
4. Si `SPRITE-SCOPE-002` confirme un cache mémoire seulement, s'assurer que l'absence de thumbnail déclenche bien une génération lazy au prochain `GetOrRequest(...)`.
5. Si `SPRITE-SCOPE-002` exige un cache persistant, ouvrir une sous-tâche dédiée avant implémentation.

Fichiers probables:
- `CasaEngine.Editor/GameEditor.cs`
- `CasaEngine.Editor/Controls/ContentBrowserPanel.cs`
- `CasaEngine.Editor/ContentBrowser/Services/ThumbnailCache.cs`

Critères d'acceptation:
- Après sauvegarde d'un `.sprite`, le Content Browser affiche le thumbnail mis à jour sans redémarrage de l'éditeur.
- Un sprite sans thumbnail visible auparavant obtient un thumbnail lors de sa première demande d'affichage.
- Les autres types existants ne régressent pas.

Validation:
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter ContentBrowser`
- smoke manuel/automation avec save d'un `.sprite`

Commit attendu:
- `feat(editor): refresh sprite thumbnails after asset changes`

### ✅ SPRITE-AUTO-010 - Ajouter les diagnostics d'automation du viewer sprite

Objectif:
Permettre une validation non interactive du viewer sprite et de son état courant.

Travail demandé:
1. Ajouter une méthode `GetAutomationStateSnapshot()` sur le document sprite.
2. Ajouter `AppendSpriteDiagnostics(...)` dans `GameEditor.CaptureAutomationDiagnostics()` sur le modèle de `Particle` et `TileMap`.
3. S'assurer que `--open-asset <sprite>` permet de capturer un état document lisible.

Fichiers probables:
- `CasaEngine.Editor/GameEditor.cs`
- nouveau panel sprite dans `CasaEngine.Editor/Controls/`

Critères d'acceptation:
- Les diagnostics contiennent un bloc `Sprite document state:` ou équivalent pour un sprite ouvert.
- Le bloc expose au minimum le sprite chargé, l'état du viewport et les informations utiles pour un smoke test.

Validation:
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`
- smoke automation `--open-asset <sprite> --diagnostics-out ...`

Commit attendu:
- `test(editor): add sprite viewer automation diagnostics`

### ✅ SPRITE-TEST-011 - Ajouter les tests ciblés et la validation finale

Objectif:
Terminer avec des validations ciblées, pas seulement un build global.

Travail demandé:
1. Ajouter ou étendre des tests de mapping Content Browser pour `.sprite`.
2. Ajouter des tests `ThumbnailCache` pour le chemin sprite, sur le modèle des tests particle existants.
3. Ajouter, si possible, un smoke automation document sprite dans `artifacts/validation/` ou `ai-agent/`.
4. Exécuter le build editor final.

Fichiers probables:
- `CasaEngine.Tests/ContentBrowser/ContentItemTests.cs`
- `CasaEngine.Tests/ContentBrowser/ThumbnailCacheTests.cs`
- éventuels tests viewer si une surface testable existe

Critères d'acceptation:
- Les nouveaux tests couvrent au minimum la classification `.sprite` et le pipeline thumbnail sprite.
- Le build editor passe.
- Le smoke automation d'ouverture sprite est exploitable pour les régressions futures.

Validation:
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter ContentBrowser`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`

Commit attendu:
- `test(editor): cover sprite viewer and thumbnail flow`

## Ordre d'exécution recommandé

1. Exécuter `SPRITE-ARCH-001` avant toute ouverture `.sprite` pour éviter un ajout non moderne.
2. Enchaîner `SPRITE-DOC-002`, `SPRITE-CB-003`, `SPRITE-VIEW-004`, `SPRITE-INSPECT-005`, `SPRITE-CONTEXT-006`.
3. Traiter ensuite `SPRITE-SAVE-007`, `SPRITE-THUMB-008`, `SPRITE-THUMB-009`, `SPRITE-AUTO-010`.
4. Finir par `SPRITE-TEST-011`.

## Résultat attendu final

Quand toutes les tâches `⏳` sont terminées:
- Un double-clic sur un `.sprite` ouvre un document sprite dédié.
- Le viewer sprite rend le sprite via le moteur et non via un rendu simplifié.
- L'inspector édite le sprite demandé et le viewer se met à jour immédiatement.
- `Hierarchy` et `Toolbox` sont explicitement vides pour ce document.
- Les thumbnails sprite se génèrent dans le process validé, se mettent à jour après changement et restent cohérents avec le rendu moteur.
- L'ouverture d'assets n'est pas dégradée par un nouveau traitement spécial bricolé pour `.sprite`.

## Validation réalisée

- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug --no-restore`
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter ContentBrowser`
- smoke viewer sprite: `--open-asset Spritesheets/ryu_0_0.sprite` avec diagnostics `artifacts/validation/sprite-viewer-smoke.txt`
- smoke thumbnails sprite: `--activate-panel contentbrowser --content-folder Spritesheets` avec diagnostics `artifacts/validation/sprite-thumbnails-smoke.txt`