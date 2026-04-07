# Profil d'import RacingGame optionnel — Liste de taches IA

## Objectif

Faire sortir du moteur CasaEngine ce qui doit rester specifique a RacingGame :

- le mapping exact des `LegacyTechniqueIndex`,
- les exceptions `Sign`, `Banner`, `Windmill`,
- les heuristiques `Alpha`, `Palm`, `Leave`, `Ast`, `plants`.

Le resultat cible doit etre un profil d'import RacingGame optionnel, branche sur des points d'extension generiques du moteur, sans hardcode de logique de contenu RacingGame dans CasaEngine lui-meme.

## Regles obligatoires pour l'agent IA

1. Une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. Ne jamais commencer la tache suivante avant d'avoir termine la tache courante.
4. A la fin de chaque tache, mettre a jour ce fichier et remplacer l'icone par `✅`, `🧪` ou `⚠️`.
5. Commiter entre chaque tache, avec le code et la mise a jour du statut dans le meme commit.
6. Toute modification du moteur CasaEngine doit obligatoirement etre accompagnee de :
   - mise a jour ou ajout d'une demo dans `CasaEngine.Demos`,
   - mise a jour ou ajout de tests unitaires dans `CasaEngine.Tests`.
7. Toute logique purement RacingGame doit etre placee hors de `CasaEngine/CasaEngine/**` et hors de `CasaEngine/CasaEngine.EditorServices/**`.
8. Si une verification unitaire cote RacingGame manque, ajouter au minimum une verification bornee reproductible (harness, snapshot ou smoke test script) dans la zone RacingGame touchee.
9. Statuts autorises :
   - `⏳ Todo`
   - `🚧 In progress`
   - `✅ Done`
   - `🧪 Needs testing`
   - `⚠️ Blocked`

## Validation minimale par tache

- Build moteur : `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- Build demos : `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug --no-restore`
- Build RacingGameCasaEngine : `dotnet build RacingGameCasaEngine/RacingGameCasaEngine.csproj -c Debug --no-restore`
- Tests moteurs bornes : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter <FiltreCible> --no-restore`

## Criteres d'acceptation finaux

- CasaEngine expose un contrat ou un point d'extension neutre pour interpreter les metadonnees legacy importees.
- Les mappings et exceptions RacingGame vivent dans une implementation optionnelle cote RacingGame.
- Le comportement sans profil optionnel reste neutre et generique.
- Les modifications moteur sont couvertes par demos/tests et les modifications RacingGame par verification bornee explicite.

---

## Phase 1 — Ouvrir un point d'extension neutre dans CasaEngine

- ✅ **T01.01 — Definir le contrat de profil d'import material legacy**
  Objectif :
  - Introduire une interface ou un service generique permettant d'interpreter des metadonnees importees sans connaitre RacingGame.
  - Garder le contrat centré sur des intentions de surface et des hints d'import, pas sur des noms de contenu.
  Validation :
  - Build moteur borne.
  - Test unitaire cible sur le contrat.
  Commit conseille :
  - `feat(import): add generic legacy material import profile contract`

- ✅ **T01.02 — Definir les donnees d'entree et de sortie du profil**
  Objectif :
  - Introduire les structures de contexte necessaires pour passer la metadata brute et recuperer des hints neutres exploitable par le moteur.
  Validation :
  - Build moteur borne.
  - Tests cibles sur le mapping des structures.
  Commit conseille :
  - `feat(import): add neutral legacy import interpretation context`

- ✅ **T01.03 — Ajouter une implementation par defaut neutre dans CasaEngine**
  Objectif :
  - Fournir un comportement generique lorsque aucun profil specifique n'est branche.
  - Ne pas encoder de conventions RacingGame dans cette implementation par defaut.
  Validation :
  - Build moteur + demos.
  - Tests cibles sur le profil par defaut.
  Commit conseille :
  - `feat(import): add default neutral legacy import profile`

- ✅ **T01.04 — Brancher le profil optionnel dans la voie d'import moteur**
  Objectif :
  - Faire accepter un profil optionnel par `EditorAssetImportService` et les autres entrees de la chaine d'import concernee.
  - Garder une surcharge simple ou un bootstrap par defaut pour les appels existants.
  Validation :
  - Build moteur + demos.
  - Tests cibles sur le branchement du profil.
  Commit conseille :
  - `refactor(import): plug optional legacy import profile into engine pipeline`

- ✅ **T01.05 — Ajouter une demo et des tests du profil neutre**
  Objectif :
  - Montrer dans `CasaEngine.Demos` qu'un import legacy neutre fonctionne sans logique RacingGame.
  - Couvrir les cas de fallback par tests unitaires.
  Validation :
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~ImportProfile --no-restore`
  Commit conseille :
  - `test(import): cover default legacy import profile behavior`

---

## Phase 2 — Implementer le profil RacingGame hors du moteur

- ✅ **T02.01 — Creer l'emplacement du profil RacingGame optionnel**
  Objectif :
  - Ajouter une implementation cote `RacingGameCasaEngine` ou dans une zone RacingGame adaptee, sans la mettre dans CasaEngine.
  - Documenter son point de bootstrap.
  Validation :
  - Build RacingGameCasaEngine borne.
  Commit conseille :
  - `feat(racinggame): add optional legacy material import profile`

- ✅ **T02.02 — Deplacer le mapping `LegacyTechniqueIndex` dans le profil RacingGame**
  Objectif :
  - Encoder dans le profil RacingGame le sens exact des techniques legacy du jeu.
  - Garder dans le moteur uniquement la preservation du champ brut.
  Validation :
  - Build moteur + RacingGameCasaEngine.
  - Verification bornee sur quelques assets legacy representatifs.
  Commit conseille :
  - `refactor(racinggame): move legacy technique mapping into optional import profile`

- ✅ **T02.03 — Deplacer les exceptions `Sign/Banner/Windmill` dans le profil RacingGame**
  Objectif :
  - Sortir ces exceptions du moteur et les exprimer via le profil RacingGame.
  Validation :
  - Build moteur + RacingGameCasaEngine.
  - Verification visuelle bornee sur un asset signage et un windmill.
  Commit conseille :
  - `refactor(racinggame): move bright ambient exceptions into import profile`

- ✅ **T02.04 — Deplacer les heuristiques `Alpha/Palm/Leave/Ast/plants` dans le profil RacingGame**
  Objectif :
  - Sortir ces heuristiques du moteur et les conserver uniquement dans le profil optionnel si elles restent necessaires.
  Validation :
  - Build moteur + RacingGameCasaEngine.
  - Verification bornee sur un asset vegetation et un asset non-alpha.
  Commit conseille :
  - `refactor(racinggame): move naming heuristics into optional import profile`

- ✅ **T02.05 — Ajouter une verification bornee cote RacingGame**
  Objectif :
  - Ajouter un harness, snapshot ou script borne pour verifier que le profil RacingGame reproduit bien les interpretations attendues.
  Validation :
  - Run borne explicite documente dans le fichier ou le commit.
  Commit conseille :
  - `test(racinggame): add bounded verification for legacy import profile`

---

## Phase 3 — Nettoyer CasaEngine et verrouiller la separation moteur / jeu

- ✅ **T03.01 — Supprimer les heuristiques RacingGame de `EditorAssetImportService`**
  Objectif :
  - Retirer du moteur les checks de noms et boosts specifiques a RacingGame.
  - Les remplacer par des hints issus du profil ou par le comportement neutre par defaut.
  Validation :
  - Build moteur + demos + RacingGameCasaEngine.
  - Tests moteurs cibles sur le chemin d'import.
  Commit conseille :
  - `refactor(import): remove RacingGame-specific material logic from engine`

- ✅ **T03.02 — Nettoyer les chemins de compatibilite qui doublonnent l'interpretation**
  Objectif :
  - Verifier qu'il ne reste pas une seconde couche d'interpretation RacingGame cachee dans le moteur qui contredirait le profil.
  - Documenter les rares fallbacks generiques encore admis.
  Validation :
  - Build moteur + RacingGameCasaEngine.
  - Verification bornee sur les principaux assets legacy cibles.
  Commit conseille :
  - `refactor(import): consolidate profile-driven legacy interpretation`

- ✅ **T03.03 — Documenter le bootstrap et les garanties d'isolation**
  Objectif :
  - Documenter comment brancher le profil RacingGame et ce qui reste volontairement hors du moteur.
  - Rendre explicite la difference entre profil neutre et profil optionnel.
  Validation :
  - Build docs non requis, verification manuelle du markdown.
  Commit conseille :
  - `docs(import): document optional RacingGame legacy import profile`

- ✅ **T03.04 — Ajouter les tests et demos finaux de separation**
  Objectif :
  - Ajouter les derniers tests cibles dans `CasaEngine.Tests` pour verrouiller le contrat moteur.
  - Garder une demo ou verification bornee cote RacingGame pour la couche optionnelle.
  Validation :
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~LegacyImport --no-restore`
  Commit conseille :
  - `test(import): lock engine and optional profile separation`
