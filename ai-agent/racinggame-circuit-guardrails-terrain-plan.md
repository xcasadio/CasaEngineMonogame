# Plan IA - Rambardes et relief du sol du circuit

## Objectif

Traiter les deux manques visuels et structurels encore visibles sur la migration du circuit vers CasaEngine :

- les rambardes de securite sur les cotes de la route,
- le sol du circuit, qui doit retrouver un vrai denivele au lieu d'un simple bloc plat.

Ce document sert a la fois de plan d'analyse et de plan correctif.

## Reponse courte a la question sur le sol

Oui, il y a bien une height map legacy exploitable.

- Le jeu original lit `LandscapeHeights.data` dans `RacingGame.Shared/Landscapes/TerrainRenderer.cs`.
- Le runtime CasaEngine lit deja ce meme fichier dans `RacingGameCasaEngine/Worlds/LegacyTrackSceneFactory.cs`, via `LegacyTerrainHeightSampler`.

Le probleme actuel n'est donc pas l'absence de donnees de relief.

Le probleme actuel est que CasaEngine utilise deja cette height map pour echantillonner des hauteurs, mais pas encore pour generer un vrai mesh de terrain visible.

## Constat confirme dans le code

### 1. Les rambardes existent bien dans le pipeline legacy

Le legacy genere explicitement :

- une rambarde gauche dans `RacingGame.Shared/Tracks/Track.cs` via `leftRail = new GuardRail(...)`,
- une rambarde droite dans `RacingGame.Shared/Tracks/Track.cs` via `rightRail = new GuardRail(...)`,
- des colonnes de support dans `RacingGame.Shared/Tracks/Track.cs` via `columns = new TrackColumns(...)`.

Le detail de generation des rails est dans `RacingGame.Shared/Tracks/GuardRail.cs` :

- mesh procedural de rail,
- placement periodique des objets `GuardRailHolder`,
- offset du rail vers l'interieur de la route,
- hauteur du rail au-dessus du sol,
- UV recalcules selon la distance.

Le detail des colonnes est dans `RacingGame.Shared/Tracks/TrackColumns.cs` :

- generation conditionnelle selon la hauteur au-dessus du terrain,
- pose de `RoadColumnSegment`,
- colonnes visibles uniquement quand la route surplombe suffisamment le paysage.

### 2. Les rambardes ne sont pas encore portees dans CasaEngine

Aujourd'hui, `LegacyTrackSceneFactory` cree surtout :

- la route,
- un sol plat,
- le decor importe,
- les elements helper-driven comme palmiers, lanternes, panneaux et checkpoints.

Les indices actuels sont :

- `CreateGroundEntity(...)` dans `RacingGameCasaEngine/Worlds/LegacyTrackSceneFactory.cs`,
- `AddPalmAndLaternEntities(...)`,
- `AddSignAndCheckpointEntities(...)`.

Il n'existe pas encore de chemin equivalent a `GuardRail` ou `TrackColumns` dans le runtime CasaEngine actuel.

Conclusion : les rails manquants ne sont pas un bug de placement. Ils ne sont simplement pas encore generes.

### 3. Le relief legacy existe, mais le sol runtime actuel est encore plat

Le legacy construit un vrai terrain topologique dans `RacingGame.Shared/Landscapes/TerrainRenderer.cs` a partir de :

- une grille `257 x 257`,
- `LandscapeHeights.data`,
- un facteur XY de `10`,
- une amplitude verticale de `300`.

Le runtime CasaEngine lit deja la meme source dans `LegacyTerrainHeightSampler`, mais le sol visible est encore cree comme un simple `BoxPrimitive` dans `CreateGroundEntity(...)`.

Conclusion :

- la donnee de relief existe,
- la logique d'echantillonnage existe,
- mais la geometrie visible du terrain n'a pas encore ete portee.

### 4. CasaEngine ne semble pas avoir de composant terrain pret a l'emploi pour ce cas

La recherche actuelle ne montre pas de brique `TerrainComponent` ou `HeightField` exploitable directement pour ce besoin dans CasaEngine.

Conclusion pratique :

- soit on porte un mesh de terrain cote jeu,
- soit on cree une petite brique moteur dediee,
- mais il ne faut pas compter sur un composant terrain deja disponible pour finir rapidement cette sous-tache.

## Decision recommandee

### Rambardes

Recommandation : porter d'abord le visuel des rambardes en reprenant la logique legacy, sans attendre une physique complete du circuit.

Ordre recommande :

1. mesh procedural des rails,
2. supports `GuardRailHolder`,
3. colonnes `RoadColumnSegment` pour les zones sur elevees,
4. validation visuelle,
5. seulement ensuite, si necessaire, raccord a la physique du circuit.

Raison : le besoin remonte aujourd'hui comme manque visuel de scene. Il ne faut pas bloquer le rendu sur la future physique complete.

### Sol topologique

Recommandation : porter un vrai mesh de terrain a partir de `LandscapeHeights.data`, avec les memes constantes de grille que le legacy, au lieu d'essayer de bricoler un sol procedural plat deforme localement.

Raison :

- c'est la voie la plus fidele,
- les donnees existent deja,
- la logique legacy de sampling est connue,
- cela servira ensuite aussi aux validations de placement et a la future physique.

## Plan d'analyse

### Phase A - Analyse des rambardes

- ✅ A1. Relire completement `RacingGame.Shared/Tracks/GuardRail.cs` pour relever les invariants a porter :
  - section du rail,
  - `CorrectionScale`,
  - `GuardRailHeight`,
  - `InsideRoadDistance`,
  - logique d'UV,
  - cadence des `GuardRailHolder`.

  Resultat : invariants identifies. Le rail legacy est un mesh procedural extrude sur une section fixe de 17 sommets, avec `CorrectionScale = 0.0019`, `GuardRailHeight = 1.35f * 1.5f * 0.425f`, `InsideRoadDistance = 0.5f`, UV longitudinaux bases sur la distance et `HolderGap = 15.0f`.

- ✅ A2. Relire `RacingGame.Shared/Tracks/TrackColumns.cs` pour relever les regles de generation conditionnelle des colonnes :
  - `ColumnsDistance`,
  - `ColumnGroundHeight`,
  - `MinimumColumnHeight`,
  - orientation top/bottom,
  - dependance a `GetMapHeight`.

  Resultat : invariants identifies. Les colonnes sont un mesh procedural cylindrique avec `ColumnsDistance = 33.0f`, `ColumnGroundHeight = 1.0f`, `MinimumColumnHeight = 2.5f` et un habillage `RoadColumnSegment` pose au sol. Leur generation depend explicitement du terrain via `GetMapHeight`.

- ✅ A3. Verifier quels assets sont deja disponibles et copiables dans le runtime CasaEngine :
  - `GuardRailHolder.X`,
  - `RoadColumnSegment.X`,
  - textures associees.

  Resultat : assets confirms et deja copiables via `RacingGameCasaEngine.csproj`. Les modeles `.X` et textures associees existent dans le contenu legacy, notamment `GuardRailHolder.X`, `RoadColumnSegment.X`, `Leitplanke.tga`, `LeitplankeNormal.tga`, `RoadCement.tga` et `RoadCementNormal.tga`.

- ✅ A4. Comparer l'ecart exact entre legacy et CasaEngine sur une piste de reference :
  - `TrackBeginner`,
  - `TrackAdvanced`,
  - `TrackExpert`.

  Resultat : l'ecart structurel est confirme sur les trois pistes. Le runtime CasaEngine ne cree actuellement ni entite de rail, ni entite de colonnes, ni habillage `GuardRailHolder` ou `RoadColumnSegment`. L'absence est globale et ne depend pas d'une piste en particulier.

- ✅ A5. Determiner si les rails doivent etre portes tout de suite en pur visuel, ou si une partie collision doit etre ajoutee en meme temps.

  Resultat : decision prise. Le premier portage vise le visuel runtime des rails et des colonnes. La collision restera rattachee a l'etape physique du circuit.

### Phase B - Analyse du relief du sol

- ✅ B1. Relever exactement les constantes et conventions du terrain legacy dans `RacingGame.Shared/Landscapes/TerrainRenderer.cs` :
  - dimensions de grille,
  - echelle XY,
  - echelle Z,
  - generation des normales,
  - UV du terrain.

  Resultat : constantes relevees. Le terrain legacy est une grille `257 x 257`, `MapWidthFactor = 10`, `MapHeightFactor = 10`, `MapZScale = 300`, UV lineaires sur toute la grille et normales lissees apres une passe de moyenne locale.

- ✅ B2. Comparer la logique de `LegacyTerrainHeightSampler` de CasaEngine avec celle du legacy pour verifier qu'il n'y a pas de divergence de sampling.

  Resultat : pas de divergence racine detectee. Le sampler CasaEngine reprend la meme logique de grille torique et d'interpolation triangulaire que le legacy pour `GetMapHeight(float x, float y)`.

- ✅ B3. Verifier comment le terrain legacy est texture :
  - `Landscape`,
  - `LandscapeNormal`,
  - `LandscapeDetail`,
  - `CityGround`,
  - `CityGroundNormal`.

  Resultat : set de textures confirme dans le contenu legacy. Le terrain principal utilise `Landscape` + `LandscapeNormal` + `LandscapeDetail`, et le plan de ville utilise `CityGround` + `CityGroundNormal`.

- ✅ B4. Determiner si le premier port doit etre :
  - le terrain complet `257 x 257`,
  - ou un sous-ensemble borne autour de la piste.

  Resultat : decision prise. Le premier port vise le terrain complet `257 x 257` pour rester fidele au legacy et eviter les coutures ou les erreurs de clamp autour de la piste.

- ✅ B5. Verifier si le city plane legacy doit aussi etre reporte dans le premier lot, ou si seul le terrain principal suffit.

  Resultat : le terrain principal est prioritaire. Le city plane sera ajoute seulement s'il reste necessaire apres retour du vrai relief, sinon un fallback visuel simple suffira.

## Plan correctif

### Phase C - Correction des rambardes

- ✅ C1. Creer un builder runtime dedie, par exemple `LegacyTrackGuardRailBuilder`, pour isoler la logique des rails de `LegacyTrackSceneFactory`.

- ✅ C2. Generer les rails gauche et droit a partir des points de route CasaEngine, avec les memes regles de decalage que le legacy.

- ✅ C3. Porter la section procedurale du rail et ses UV.

- ✅ C4. Poser les objets `GuardRailHolder` au bon intervalle et avec la bonne orientation.

- ✅ C5. Porter les colonnes `RoadColumnSegment` quand la route surplombe suffisamment le terrain.

- ✅ C6. Ajouter un materiau cible pour les rails si le simple import du modele `GuardRailHolder` ne suffit pas visuellement.

- ✅ C7. Inserer les entites de rails dans la scene avec un nommage stable, par exemple :
  - `Track.GuardRail.Left.<TrackName>`
  - `Track.GuardRail.Right.<TrackName>`
  - `Track.Columns.<TrackName>`

- ✅ C8. Verifier que le resultat reste compatible avec les futures collisions de bord de piste.

  Resultat : phase C portee dans le runtime. `LegacyTrackGuardRailBuilder` cree maintenant les deux rails proceduraux, leurs supports `GuardRailHolder`, les colonnes procedurales sous route et les entites `RoadColumnSegment`. Le tout est branche dans `LegacyTrackSceneFactory` avec un nommage runtime stable et sans couplage a une collision de bord encore inexistante.

### Phase D - Correction du terrain topologique

- ✅ D1. Creer un builder runtime dedie, par exemple `LegacyTerrainMeshBuilder`, pour sortir la construction du terrain de `LegacyTrackSceneFactory`.

- ✅ D2. Lire `LandscapeHeights.data` avec les memes constantes que le legacy.

- ✅ D3. Generer un vrai mesh de terrain avec :
  - vertices,
  - indices,
  - normales,
  - tangentes,
  - UV.

- ✅ D4. Remplacer `CreateGroundEntity(...)` base sur `BoxPrimitive` par une entite terrain basee sur ce mesh.

- ✅ D5. Reprendre un materiau proche du legacy pour le terrain principal :
  - texture diffuse,
  - normal map,
  - detail si possible dans le shader cible,
  - sampler wrap.

- ✅ D6. Ajouter ensuite, si necessaire, le city plane ou un fallback visuel equivalent.

- 🚧 D7. Verifier que les objets clamps au terrain restent correctement poses apres remplacement du sol plat par le vrai terrain.

  Resultat : phase D portee dans le runtime. `LegacyTerrainMeshBuilder` remplace le sol plat par le mesh complet derive de `LandscapeHeights.data`, avec les constantes legacy de grille, UV, normales et tangentes. Le materiau de terrain utilise maintenant les textures `Landscape` et `LandscapeNormal` avec sampling wrap. Le detail texture n'est pas encore exploite par le shader statique actuel, mais le rendu vise deja une approximation proche et topologiquement correcte. Le city plane n'a pas ete rajoute dans ce premier lot, car le vrai terrain complet sert deja de fallback visuel principal.

## Validation demandee apres correction

### Validation visuelle

- ⏳ V1. Capturer une vue de depart sur les trois pistes.
- ⏳ V2. Capturer au moins une zone de virage avec rails visibles.
- ⏳ V3. Capturer au moins une zone sur elevee avec colonnes si applicable.
- ⏳ V4. Capturer une zone ou le relief du terrain est fortement visible.

### Validation technique

- 🚧 V5. Verifier que le build borne `dotnet build RacingGameCasaEngine/RacingGameCasaEngine.csproj -c Debug --no-restore` reste vert.
- ⏳ V6. Verifier qu'aucune regression de placement du decor n'apparait apres introduction du vrai terrain.
- ⏳ V7. Verifier que la route reste au-dessus du terrain partout et que les objets clamps restent poses proprement.

### Validation de parite

- ⏳ V8. Comparer visuellement CasaEngine au legacy sur au moins `Beginner` et `Advanced` pour les rails.
- ⏳ V9. Comparer visuellement CasaEngine au legacy sur au moins `Beginner` et `Advanced` pour le relief global du paysage.

## Definition of done

Cette sous-tache sera consideree terminee quand les points suivants seront vrais :

- les rambardes gauche et droite sont visibles sur les bords de route,
- les supports de rails sont presents,
- les colonnes sous route sont presentes sur les zones ou elles doivent exister,
- le sol n'est plus un bloc plat mais un vrai terrain derive de `LandscapeHeights.data`,
- le rendu du circuit reste compilable et stable,
- la scene devient visuellement beaucoup plus proche du legacy sur les zones de bord de route et sur le paysage.

## Ordre d'execution recommande

1. Porter d'abord les rails visuels.
2. Porter ensuite le mesh de terrain topologique.
3. Revalider la pose des objets sur le terrain.
4. Decider seulement apres cela si une couche collision/physique de rails doit etre ajoutee tout de suite ou dans l'etape physique du circuit.

## Notes finales

Le point critique a retenir est celui-ci :

- pour les rails, il manque surtout une logique de generation qui existe encore seulement dans le legacy,
- pour le sol, il ne manque pas une source de donnees mais le portage du mesh de terrain lui-meme.

Autrement dit, le denivele n'est pas bloque par l'absence d'height map. Il est bloque par le fait que CasaEngine s'en sert deja pour lire des hauteurs, mais pas encore pour dessiner le terrain correspondant.