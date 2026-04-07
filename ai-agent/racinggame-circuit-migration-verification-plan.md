# Plan IA - Verification detaillee de la migration du circuit vers CasaEngine

## Objectif

Verifier de facon deterministe que la migration du circuit depuis RacingGame vers RacingGameCasaEngine n'a pas introduit de regressions sur :

- le placement du decor auteur,
- le point de depart de la voiture,
- la geometrie de route,
- les checkpoints,
- les cas speciaux du track (terrain clamp, loops, combis, doublons, textures de route).

## Artifacts existants a reutiliser

- Comparateur CLI : `scripts/TrackPlacementExporter`
- Exports JSON : `artifacts/track-placement/`
- Runtime debug : `F1` camera libre, `F2` screenshot, `F3` route seule

## Definition of done

- Aucun delta non justifie sur les placements auteur.
- Aucun delta non justifie sur le start pose (position + orientation).
- Les ecarts de spline/largeur/checkpoints sont identifies track par track.
- Un rapport final distingue clairement : conforme, regression confirmee, risque residuel.

## Actions pour l'agent

- `✅ 1.0` Regenerer la baseline des exports existants.
  Commandes ciblees :
  - `dotnet run --project scripts/TrackPlacementExporter/TrackPlacementExporter.csproj -- --repo-root .`
  - Conserver les JSON produits dans `artifacts/track-placement/`.
  Resultat : exports regeneres et verifies le 2026-04-06.

- `✅ 2.0` Verifier les placements auteur deja compares.
  Attendu : zero `missingInCasa`, zero `missingInLegacy`, zero `nameMismatch`, zero `transformMismatch`.
  Si un ecart apparait, isoler le track, le `SourcePath`, le modele resolu et la nature du delta.
  Resultat : zero ecart sur `TrackAdvanced`, `TrackBeginner`, `TrackExpert`.

- `✅ 3.0` Verifier explicitement le start pose de chaque track.
  Utiliser l'export du comparateur pour relever, pour chaque track :
  - position de depart,
  - orientation de depart,
  - forward/up du point de depart.
  Sortie attendue : deltas bornes en position et rotation entre legacy et Casa.
  Resultat : `startPosDelta=0` et `startRotDelta=0deg` sur les trois tracks.

- `✅ 4.0` Etendre le comparateur si le start pose ne suffit pas.
  Ameliorations prioritaires :
  - exporter un echantillonnage de la spline de route,
  - exporter les vecteurs `right/up/forward` sur plusieurs segments,
  - exporter la largeur de route interpolee,
  - exporter les positions de checkpoints derivees,
  - exporter les UV de route sur un petit echantillon representatif.
  Resultat : le comparateur exporte maintenant `StartPose`, `RoadSamples`, `CheckpointPositions`, `LoopInsertionsCount`.

- `✅ 5.0` Comparer la geometrie de route, pas seulement le decor.
  Pour chaque track, produire un resume des deltas suivants :
  - centre de route,
  - orientation locale,
  - largeur,
  - checkpoints,
  - eventuels segments de loop inseres.
  Resultat : deltas nuls sur positions, checkpoints, largeur, UV et loops; bruit flottant mineur a `0.034deg` sur les angles `forward/up` dans le resume console.

- `✅ 6.0` Verifier les regles heritagees du pipeline legacy.
  L'agent doit confirmer que CasaEngine reproduit bien :
  - alias de modeles,
  - expansion des combis,
  - clamp terrain,
  - filtre anti-doublons,
  - scale global legacy `1.2`.
  Resultat : confirme par le comparateur et les corrections runtime deja integrees.

- `✅ 7.0` Ajouter une passe de validation visuelle bornee.
  Pour chaque track :
  - lancer la course,
  - capturer une vue route-only depuis la camera de course normale,
  - activer `F3` pour isoler la route,
  - capturer au moins une capture `F2` au depart,
  - capturer une vue d'un secteur dense en decor,
  - capturer un checkpoint ou une zone de loop si applicable.
  Ne pas faire de session ouverte sans borne; definir un parcours et un timeout.
  Resultat : mode automatise `--capture-track-audit` ajoute, 12 captures generees sous `%LOCALAPPDATA%/CasaEngine/RacingGameCasaEngine/Screenshots`, dont une capture `chase-road-only` par track confirmant la visibilite de la route en vue de jeu standard.

- `✅ 8.0` Croiser les constats visuels avec les exports JSON.
  Si un probleme visuel est observe sans delta JSON evident, l'agent doit investiguer en priorite :
  - materiau route,
  - UV,
  - ordre de rendu,
  - orientation de mesh,
  - collisions ou absence de collisions.
  Resultat : l'invisibilite de la route etait cote rendu, pas cote geometrie comparee; correction appliquee sur le culling du materiau route. Risque residuel : de grands polygones occluants restent visibles sur certaines captures plein-scene et ne sont pas expliques par les deltas geometriques de route.

- `✅ 9.0` Produire un rapport final actionnable.
  Le rapport doit contenir, track par track :
  - statut,
  - deltas chiffres,
  - captures associees,
  - hypothese racine,
  - correction recommande si regression.
  Resultat : rapport produit dans `artifacts/track-placement/racinggame-circuit-migration-audit-report.md`.

## Regles de conduite pour l'agent

- `✅` Toujours travailler avec des validations bornees.
- `✅` Preferer les comparaisons deterministes aux impressions visuelles seules.
- `✅` En cas de divergence, remonter au `SourcePath` et a la regle de pipeline correspondante.
- `⚠️` Ne pas conclure a une conformite de la route uniquement a partir des placements de decor.
- `⚠️` Ne pas modifier plusieurs dimensions a la fois sans regenerer les artifacts de comparaison.

## Livrables attendus

- JSON de comparaison mis a jour.
- Resume console des placements et du start pose.
- Captures ciblees des tracks inspectes.
- Rapport final de verification avec corrections ou risques restants.

## Phase corrective complementaire

- `✅ 10.0` Recaler l'unite monde Casa sur l'echelle legacy.
  Actions :
  - supprimer le facteur de reduction `0.04` applique a la scene de track et au comparateur,
  - revalider la largeur de route, le point de depart, la camera et la voiture par rapport au meme referentiel.
  Resultat : l'echelle runtime de la piste utilise maintenant l'unite legacy directe au lieu d'une reduction artificielle a `4%`.

- `✅ 11.0` Reintegrer les objets de piste helper-driven les plus visibles.
  Actions :
  - porter la generation `Palms`, `Laterns`, bannieres de checkpoint, panneaux, `Banner6` et `StartLight3`,
  - conserver un tirage pseudo-aleatoire borne et stable par track pour garder des captures comparables.
  Resultat : la scene Casa regenere maintenant les principaux objets de bord de route et de depart qui etaient absents du runtime migre.

- `✅ 12.0` Aligner la correction d'orientation des meshes legacy.
  Actions :
  - reappliquer cote import Casa la correction de repere que le renderer legacy appliquait aux `.X`,
  - verifier ensuite la coherence visuelle des modeles asymetriques.
  Resultat : les modeles legacy importes recoivent maintenant une correction d'orientation explicite coherente avec le renderer d'origine.

- `✅ 13.0` Etendre la verification au contenu runtime reel.
  Actions :
  - ajouter un exporteur de scene live Casa pour comparer les transforms monde finales,
  - ne plus limiter l'audit aux seuls objets auteur et a la reconstruction theorique de spline.
  Resultat : mode runtime `--export-track-runtime-scene --track-runtime-export-file <path>` ajoute, export live produit dans `artifacts/track-placement/racinggame-casaengine-live-runtime-scene.json`, baseline runtime auteur produite dans `artifacts/track-placement/racinggame-casaengine-authored-runtime-scene.json`, et comparaison bornee disponible via `scripts/TrackPlacementExporter`. Constats actuels : offset constant `Y=0.7` sur `Track.Ground.*`, ecarts importants sur le sous-ensemble `AlphaPalm*` entre reconstruction auteur et scene runtime, et presence confirmee d'entites runtime additionnelles majoritairement `Sign*`/`Banner*` qui ne sont pas couvertes par la baseline auteur.