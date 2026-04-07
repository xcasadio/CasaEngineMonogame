# Retuning materials legacy RacingGame

## Objectif

Corriger la sur-reflexion et le specular trop fort dans `RacingGameCasaEngine` sans reintroduire de logique RacingGame dans le moteur generique.

## Constats issus de l'audit

Artefacts d'audit disponibles :

- `artifacts/track-placement/legacy-material-audit.csv`
- `artifacts/track-placement/legacy-material-profile-delta.csv`

Les problemes observes se repartissent en quatre groupes.

### 1. Facades, briques, ruines, pierres

Inspection :

- `Building.tga`, `building2.tga`, `building3.tga`, `building4.tga`, `building5.tga`
- `Hotel01.tga`
- `ruin.tga`, `Ruin01.tga`
- `Stone04.tga`, `Stone5.tga`

Constat :

- ces materials portent souvent `SkyCubeMap.dds` comme ressource legacy,
- mais l'effet voulu est mat ou tres legerement speculaire,
- pas une vraie reflection de scene.

Action :

- ne jamais activer la reflection runtime pour ces textures,
- reduire fortement `SpecularColor`,
- plafonner `SpecularPower`.

### 2. Panneaux, banners, panneaux de course

Inspection :

- `Schild.tga`, `Schild2.tga`, `Schild_Kurve_links.tga`, `SignWarning.tga`
- `banner.tga`, `banner2.tga`, `banner3.tga`
- `roadsign1.tga`, `roadsign2.tga`
- `Goal.tga`, `plazaschild.tga`, `plazacasino.tga`, `ladyluck.tga`

Constat :

- ces elements sont trop brillants et reflectent comme du metal,
- l'exception `BrightAmbient` doit rester distincte de la reflection.

Action :

- conserver le boost d'ambient quand necessaire,
- supprimer la reflection runtime,
- reduire le specular pour eviter l'effet plastique/metallique.

### 3. Feux rouges, lampadaires, props industriels mats

Inspection :

- `Light.tga`, `TLight.tga`
- `streetlamp.tga`, `streetlamp2.tga`
- `Hydrant.tga`, `garbagecan.tga`, `OilWell.tga`, `Oiltank.tga`, `Leitplanke.tga`, `gelaender.tga`, `Windmill.tga`

Constat :

- le pipeline actuel leur applique une reflection ou un specular trop fort,
- alors qu'on veut au plus un highlight mat, pas une reflection de cubemap.

Action :

- desactiver la reflection runtime,
- baisser le specular,
- limiter `SpecularPower`.

### 4. Survivants legitimes

Inspection :

- surfaces `ReflectionSimpleGlass.fx`
- `Car` (`chrome`, `lack`) et `CarSelectionPlate`

Constat :

- ces surfaces doivent rester reflechissantes.

Action :

- conserver la reflection,
- ne pas ecraser leur parametrage specular legacy.

## Liste d'action pour un agent IA

- [x] Auditer tous les `.X` legacy par modele, material et texture.
- [x] Identifier les deltas entre comportement neutre et profil RacingGame.
- [x] Retirer l'implicite "cubemap present = reflection active" du chemin generique.
- [x] Ne transporter la cubemap que lorsque `UsesReflection` est explicitement actif.
- [x] Restreindre la reflection RacingGame aux survivants legitimes (verre + voiture).
- [x] Ajouter une table de tuning runtime RacingGame par texture/modele pour reduire le specular des surfaces mates.
- [x] Verrouiller la correction avec un verifier borne couvrant au minimum : batiment, panneau, feu rouge, verre, voiture.

## Resultat cible

- les facades, briques, ruines, pierres et props mats ne reflectent plus la scene,
- les panneaux et feux gardent leur lisibilite sans paraitre metalliques,
- le verre et la voiture restent reflectifs,
- le moteur reste neutre et la granularite contenu-specifique reste cote RacingGame.