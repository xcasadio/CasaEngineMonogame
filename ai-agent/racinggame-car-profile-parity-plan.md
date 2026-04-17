# Plan IA - Retablir des profils de voiture distincts dans RacingGameCasaEngine

## Objectif

Faire en sorte que chaque voiture de `RacingGameCasaEngine` ait un comportement runtime reellement distinct, en branchant ses caracteristiques sur la dynamique du vehicule au lieu de n'utiliser ces differences que pour l'UI et le visuel.

Le plan doit couvrir les deux modes de conduite existants, `Arcade` et `Simulation`, sans casser le flow de course, le HUD, la camera, les validations bornees ni l'architecture `RacingCarPawn` + `VehicleDynamicsComponent` deja en place.

## Constat de depart

- `RaceFrontEndCatalog.Cars` expose aujourd'hui surtout des labels et des stats texte, pas une vraie fiche technique exploitee par le runtime.
- `CarSelectionScreen` affiche des barres codees en dur qui ne sont pas derivees des vraies donnees du vehicule.
- `SelectedCarIndex` influence surtout le nom, la couleur et le visuel de la voiture, pas sa physique.
- `VehicleDynamicsComponent` construit actuellement une transmission, une masse de chassis et une definition de roues communes a toutes les voitures.
- Les solveurs `Arcade` et `Simulation` utilisent des constantes partagees qui neutralisent les differences attendues entre voitures.
- Le jeu legacy appliquait au moins `max speed`, `mass` et `max acceleration` par voiture, mais pas encore une vraie transmission distincte ni un vrai grip dedie par voiture.

## Resultat attendu

Une voiture differente doit produire au minimum des differences mesurables en acceleration, vitesse de pointe, inertie et ressenti general. Si le design produit l'exige, elle doit aussi produire des differences de boite, de freinage et d'adherence, avec un seul systeme de donnees partage entre l'UI et le runtime.

## Legende de statut

- `⏳` Todo
- `🚧` In progress
- `✅` Done
- `🧪` Needs testing
- `⚠️` Blocked

## Contrat de travail de l'agent

1. Chaque sous-etape de ce plan est committable seule.
2. L'agent doit faire un commit a la fin de chaque sous-etape terminee.
3. Apres chaque commit, l'agent met a jour ce fichier avec l'icone de statut, une note concise et la date si utile.
4. Une sous-etape ne passe a `✅` que si le code compile au minimum sur le perimetre touche.
5. Une sous-etape implementee mais pas encore verifiee doit passer par `🧪` tant que la validation n'est pas faite.
6. Si un blocage apparait, l'agent doit passer la sous-etape a `⚠️`, creer une sous-etape corrective dans ce plan, traiter ce blocage, committer, puis reprendre l'etape initiale.
7. L'agent ne doit pas casser les contrats deja consommes par `RaceHudScreen`, `ChaseCameraRigComponent`, `RuntimeRaceSession`, `RaceWorldFactory`, `RacingPlayerController` et `RaceGameMode` sans couche de compatibilite.
8. L'agent doit d'abord retablir la parite minimale avec le legacy, puis etendre la differenciation des voitures au-dela du legacy si c'est necessaire pour honorer les stats affichees.
9. Le hot path `Update` et `Draw` doit rester sans allocations evitables et sans LINQ introduit dans les nouvelles boucles runtime.

## Validation minimale transversale

- Build borne obligatoire : `dotnet build RacingGameCasaEngine/RacingGameCasaEngine.csproj -p:BaseOutputPath=artifacts/verify-build/`
- Si une etape touche le front-end, les options, les ecrans ou la selection de voiture : `dotnet run --project RacingGameCasaEngine/RacingGameCasaEngine.csproj -p:BaseOutputPath=artifacts/verify-build/ -- --smoke-frontend`
- Si une etape touche la conduite, la physique, la transmission, l'adherence ou le roulage sur piste : `dotnet run --project RacingGameCasaEngine/RacingGameCasaEngine.csproj -p:BaseOutputPath=artifacts/verify-build/ -- --capture-track-audit`
- Si aucune validation bornee ne permet de comparer proprement les voitures, l'agent doit ajouter un audit runtime minimal dedie avant de clore le chantier.
- Ne pas utiliser la tache VS Code `Build RacingGame.Shared` pour valider : elle est mal configuree dans ce workspace.

## Principes d'architecture a respecter

- Une seule source de verite pour les profils de voiture.
- Le front-end doit afficher des stats derivees des vraies donnees runtime, pas des tableaux decoratifs de secours.
- `RacingCarPawn` reste l'agregat stable de la voiture.
- `VehicleDynamicsComponent` reste le point d'entree unique de la dynamique vehicule.
- Les deux solveurs `Arcade` et `Simulation` doivent consommer un profil commun, pas chacun une serie de constantes locales incoherentes.
- Le chantier doit distinguer clairement :
  - `parite legacy` : vitesse max, masse, acceleration max par voiture ;
  - `differenciation etendue` : transmission, grip, freinage, braquage, geometrie de roues, repartition des forces, si le design le demande.

## Format de commit recommande

- `docs(racing-casa): add car profile parity implementation plan`
- `feat(racing-casa): add runtime car performance profiles`
- `refactor(racing-casa): derive car selection stats from runtime profiles`
- `feat(racing-casa): propagate selected car profile to vehicle dynamics`
- `feat(racing-casa): apply per-car tuning to arcade solver`
- `feat(racing-casa): apply per-car tuning to simulation solver`
- `test(racing-casa): add bounded audit for car profile differences`

## Plan committable

## ✅ Etape 1 - Geler la source de verite des profils voiture

**But**

Remplacer les donnees purement decoratives par une fiche technique exploitable par le runtime et par l'UI.

**Travail**

- Introduire un type de donnees dedie du style `CarPerformanceProfile` ou `VehicleProfileDefinition`.
- Y separer explicitement les champs `parite legacy` et les champs `differenciation etendue`.
- Mappper les trois voitures existantes vers des valeurs numeriques concretes.
- Garder une compatibilite avec les textes et l'affichage front-end existants tant que l'UI n'a pas ete migree sur ces donnees.

**Validation**

- Le projet compile.
- Les trois voitures ont une fiche technique runtime lisible et centralisee.

**Commits recommandes**

- `feat(racing-casa): add runtime car performance profiles`
- `docs(racing-casa): freeze car profile source of truth`

**Sous-etapes**

- `✅ 1.1` Introduire le type `CarPerformanceProfile` avec les champs numeriques utiles au runtime
- `✅ 1.2` Definir les trois profils de voiture du jeu dans une seule source de verite
- `✅ 1.3` Distinguer noir sur blanc les valeurs `parite legacy` et les valeurs `gameplay etendu`

**Notes**

- 2026-04-17 : `CarPerformanceProfile` a ete introduit avec separation explicite entre parite legacy et tuning etendu. Les trois voitures sont centralisees dans une source unique, et le catalogue front-end est deja derive de cette source tout en restant compatible avec l'UI actuelle.

## ✅ Etape 2 - Rebrancher le front-end sur les vraies donnees

**But**

Faire de l'ecran de selection et du catalogue une projection fidele des profils runtime au lieu d'une UI decoratrice.

**Travail**

- Faire deriver `CarDefinition` ou son remplacant du vrai profil de voiture.
- Remplacer les listes de stats texte et les barres codees en dur par des valeurs derivees du profil.
- Verifier la coherence entre le resume affiche et les valeurs gameplay reelles.
- Eviter toute duplication silencieuse entre `RaceFrontEndCatalog` et `CarSelectionScreen`.

**Validation**

- Build borne du projet.
- Smoke front-end reussi.
- Les stats affichees dans la selection voiture proviennent de la vraie fiche technique runtime.

**Commits recommandes**

- `refactor(racing-casa): derive car catalog from runtime profiles`
- `refactor(racing-casa): replace hardcoded car stat bars`

**Sous-etapes**

- `✅ 2.1` Refaire le catalogue front-end a partir des profils runtime
- `✅ 2.2` Supprimer les barres de stats codees en dur de `CarSelectionScreen`
- `✅ 2.3` Deriver textes et pourcentages affiches depuis les vraies valeurs du profil

**Notes**

- 2026-04-17 : `CarDefinition` est maintenant derivee des profils runtime, et `CarSelectionScreen` consomme des `SelectionStats` derives au lieu d'un tableau local code en dur. La stat `Handling` est actuellement calculee depuis une formule stable basee sur le grip simule et la vitesse de braquage arcade en attendant l'audit runtime dedie.

## ⏳ Etape 3 - Propager le profil choisi jusqu'au pawn et au runtime

**But**

Faire en sorte que la voiture instanciee en course connaisse autre chose qu'un `SelectedCarIndex`.

**Travail**

- Resoudre le profil complet a partir de `SelectedCarIndex` lors du chargement de course.
- Le propager via `RaceWorldFactory`, `RuntimeRaceWorldBinder`, `RacingCarPawn` et toute couche necessaire.
- Ajouter un contrat clair entre le pawn et `VehicleDynamicsComponent` pour recevoir ce profil.
- Preserver l'usage actuel de `SelectedCarIndex` pour le visuel et la selection de modele.

**Validation**

- Le projet compile.
- Le monde de course instancie la voiture avec un profil runtime explicite et verifiable.

**Commits recommandes**

- `feat(racing-casa): propagate selected car profile to runtime`
- `refactor(racing-casa): add car profile contract to pawn`

**Sous-etapes**

- `⏳ 3.1` Resoudre le profil de voiture complet a partir de la selection front-end
- `⏳ 3.2` Propager ce profil jusqu'au `RacingCarPawn`
- `⏳ 3.3` Exposer un contrat explicite pour que `VehicleDynamicsComponent` consomme ce profil

**Notes**

- Tant que cette etape n'est pas terminee, toute logique per-car dans les solveurs restera fragile ou dupliquee.

## ⏳ Etape 4 - Supprimer les constantes vehicule communes du coeur runtime

**But**

Retirer du runtime les valeurs hardcodees qui uniformisent toutes les voitures.

**Travail**

- Remplacer dans `VehicleDynamicsComponent` la masse fixe, la transmission par defaut et la definition de roues generique par des donnees configurees a partir du profil.
- Introduire des types de donnees runtime ou builders si necessaire : transmission, chassis, roues, forces max, grip, freinage.
- Verifier que `TargetTopSpeedMph`, la telemetrie et les valeurs consommees par la camera et le HUD sont alimentees par le profil courant.
- Garder une couche de fallback raisonnable si un profil est incomplet.

**Validation**

- Build borne du projet.
- Le runtime ne depend plus d'une seule masse ou d'une seule transmission pour toutes les voitures.

**Commits recommandes**

- `refactor(racing-casa): remove shared hardcoded vehicle config`
- `feat(racing-casa): bind transmission and chassis config from profile`

**Sous-etapes**

- `⏳ 4.1` Sortir la masse de chassis du hardcode commun
- `⏳ 4.2` Sortir la transmission par defaut du hardcode commun
- `⏳ 4.3` Sortir la geometrie et les coefficients de roue du hardcode commun
- `⏳ 4.4` Brancher la telemetrie et `TargetTopSpeedMph` sur le profil courant

**Notes**

- Cette etape est le pivot du chantier : tant qu'elle n'est pas faite, les differences entre voitures resteront cosmetiques.

## ⏳ Etape 5 - Retablir la parite legacy dans le mode Arcade

**But**

Retrouver au minimum les differences que le jeu original appliquait deja entre voitures.

**Travail**

- Injecter dans le solveur `Arcade` au moins la vitesse max, la masse et l'acceleration max propres a chaque voiture.
- Verifier que le rapport acceleration / inertie varie bien selon le profil.
- Garder le ressenti global actuel si possible, sauf la ou la parite legacy impose une difference nette.
- Preserver les contrats actuels du HUD, de la camera et de l'audio.

**Validation**

- Build borne du projet.
- Capture track audit reussi.
- Le mode `Arcade` montre des differences observables entre au moins deux voitures sur l'acceleration et la vitesse atteinte.

**Commits recommandes**

- `feat(racing-casa): apply legacy car tuning to arcade solver`
- `test(racing-casa): verify arcade car profile differences`

**Sous-etapes**

- `⏳ 5.1` Brancher vitesse max, masse et acceleration par voiture dans le solveur `Arcade`
- `⏳ 5.2` Verifier la parite minimale avec le legacy sur ces trois dimensions
- `⏳ 5.3` Stabiliser les regressions eventuelles de HUD, camera et telemetry

**Notes**

- Si une partie de l'ancienne variation de comportement provenait uniquement d'effets indirects, l'agent doit le documenter plutot que d'inventer une equivalence trompeuse.

## ⏳ Etape 6 - Introduire une vraie differenciation de boite et de direction si le design le demande

**But**

Honorer le constat utilisateur selon lequel les boites et la tenue de route devraient aussi differer, ce qui va au-dela de la simple parite legacy.

**Travail**

- Definir par voiture une vraie `VehicleTransmissionDefinition` ou des coefficients derives qui modifient les seuils de passage, les rapports ou la courbe de force.
- Introduire des coefficients de direction et de grip propres a chaque voiture pour que les trajectoires et l'insistance au braquage different.
- Ne pas trahir l'identite du mode `Arcade` : la difference doit etre lisible mais stable et jouable.
- Documenter explicitement que cette etape est une extension gameplay par rapport au legacy si ce n'etait pas deja present historiquement.

**Validation**

- Build borne du projet.
- Capture track audit reussi.
- Au moins deux voitures presentent une difference verifiable de passages de rapports et de ressenti en virage en mode `Arcade`.

**Commits recommandes**

- `feat(racing-casa): add per-car transmission tuning`
- `feat(racing-casa): add per-car steering and grip tuning`

**Sous-etapes**

- `⏳ 6.1` Introduire une transmission configurable par voiture
- `⏳ 6.2` Introduire des coefficients de direction et de grip par voiture
- `⏳ 6.3` Ajuster le solveur `Arcade` pour consommer ces nouvelles donnees sans regression majeure

**Notes**

- Si le produit prefere rester strictement au legacy, cette etape peut etre scindee et reportee, mais le plan doit alors le documenter explicitement.

## ⏳ Etape 7 - Brancher les profils voiture sur le mode Simulation

**But**

Faire en sorte que le mode `Simulation` n'uniformise pas a nouveau toutes les voitures.

**Travail**

- Raccorder au solveur `Simulation` la masse, la transmission, les forces motrices, les freins, le grip lateral et les roues derives du profil.
- Verifier que les differences de charge, de reacceleration et d'appui lateral restent coherentes avec le profil choisi.
- Eviter que le solveur `Simulation` ecrase ces differences par ses constantes locales de securite.
- Garder un fallback stable si certaines valeurs n'existent pas encore pour un profil donne.

**Validation**

- Build borne du projet.
- Capture track audit reussi.
- Le mode `Simulation` montre des differences mesurables entre voitures sans casser le roulage de base.

**Commits recommandes**

- `feat(racing-casa): apply per-car tuning to simulation solver`
- `test(racing-casa): validate simulation car profile differences`

**Sous-etapes**

- `⏳ 7.1` Brancher masse, transmission et forces motrices par voiture dans le solveur `Simulation`
- `⏳ 7.2` Brancher grip lateral, freinage et roues par voiture dans le solveur `Simulation`
- `⏳ 7.3` Stabiliser les fallbacks et la robustesse hors piste ou en perte de contact

**Notes**

- Cette etape ne doit pas redoubler la configuration ; le solveur doit consommer le meme profil que le mode `Arcade`.

## ⏳ Etape 8 - Rendre la difference entre voitures observable et testable

**But**

Pouvoir verifier rapidement que le chantier est reussi sans se fier uniquement au ressenti manuel.

**Travail**

- Enrichir `RuntimeRaceSession` ou un audit borne pour exposer le profil actif, la masse, la transmission, la vitesse cible, le mode de conduite et quelques echantillons telemetry.
- Ajouter si necessaire un audit runtime dedie du style `--capture-car-profile-audit` ou equivalent.
- Produire une comparaison bornee entre les trois voitures sur quelques indicateurs simples : vitesse atteinte, rapport courant, acceleration moyenne, comportement en virage sur une fenetre de temps definie.
- Faire en sorte que cet audit reste utile pour les regressions futures.

**Validation**

- Build borne du projet.
- L'audit borne existe et permet de differencier les voitures sans session manuelle ouverte.

**Commits recommandes**

- `feat(racing-casa): add bounded car profile audit`
- `test(racing-casa): compare runtime car profiles`

**Sous-etapes**

- `⏳ 8.1` Exposer les donnees runtime utiles au debug des profils voiture
- `⏳ 8.2` Ajouter un audit borne pour comparer les voitures
- `⏳ 8.3` Verifier que cet audit couvre `Arcade` et `Simulation` si les deux modes sont en perimetre

**Notes**

- Si la comparaison automatique des trois voitures dans les deux modes est trop lourde, l'agent doit au minimum produire un audit reproductible pour un sous-ensemble cible documente.

## ⏳ Etape 9 - Clore le chantier et documenter les limites

**But**

Fermer proprement le travail avec un etat clair du niveau de parite et des ecarts volontaires par rapport au legacy.

**Travail**

- Repasser sur l'ensemble du chantier pour supprimer les derniers tableaux ou constantes decoratives devenus obsoletes.
- Documenter ce qui est parite legacy et ce qui est extension gameplay.
- Mettre a jour ce plan avec les statuts finaux, les validations lancees et les limites connues.
- Noter explicitement les suites eventuelles si la differenciation des voitures reste a affiner.

**Validation**

- Build borne du projet.
- Smoke front-end reussi.
- Audit borne reussi sur le perimetre choisi.

**Commits recommandes**

- `docs(racing-casa): close car profile parity plan`
- `refactor(racing-casa): remove obsolete hardcoded car stats`

**Sous-etapes**

- `⏳ 9.1` Nettoyer les reliquats de stats decoratives ou de fallback obsoletes
- `⏳ 9.2` Documenter parite legacy, extensions gameplay et limites connues
- `⏳ 9.3` Clore le plan avec les validations effectuees

## Questions a trancher pendant l'execution

- Le produit veut-il s'arreter a la parite legacy, ou faut-il absolument que la boite et l'adherence different fortement par voiture meme si c'est un ajout gameplay ?
- Les valeurs affichees dans le front-end doivent-elles etre strictement issues de coefficients physiques, ou certaines peuvent-elles rester des notes derivees plus marketing tant qu'elles restent coherentes ?
- Le mode `Simulation` doit-il viser la meme hierarchie de differences entre voitures que `Arcade`, ou une lecture plus physique qui peut changer l'ordre des ressentis ?

## Criteres de fin de chantier

- Les voitures n'ont plus un comportement uniformise par une configuration runtime unique.
- Le front-end affiche des stats derivees de la vraie source de verite des profils voiture.
- `SelectedCarIndex` ne sert plus seulement au visuel ; il selectionne un vrai profil gameplay.
- Le mode `Arcade` respecte au moins la parite legacy sur vitesse max, masse et acceleration.
- Si le scope l'exige, la transmission et la tenue de route different aussi de facon mesurable entre voitures.
- Le mode `Simulation` consomme le meme profil et ne reintroduit pas une uniformisation silencieuse.
- Une validation bornee permet de verifier les differences entre voitures.