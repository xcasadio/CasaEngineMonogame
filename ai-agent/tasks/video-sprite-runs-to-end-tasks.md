# Plan agent IA — Film du monde qui va à sa fin

Demande d'un projet consommateur, arbitrée avec l'auteur le 2026-11-12. Un film du monde lancé par une cutscene doit pouvoir lui survivre : quand la cutscene s'arrête, le film continue jusqu'à sa dernière image, puis son entité est cachée.

Les décisions D1 → D6 ont été arbitrées avec l'auteur le 2026-11-12. Les choix C1 → C9 sont proposés par ce plan et verrouillés par son approbation. **Ce plan les applique, il ne les rediscute pas.**

Relu le 2026-11-12 par un plan-verifier neuf, qui rend READY sans constat. L'auteur approuve le plan et le lance le même jour (« J'approuve et lance »).

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

- **Une action `StartVideoSprite`.** Elle montre l'entité, lance son film depuis la première image et rend la main aussitôt. Aucune fin de la cutscene ne touche le film : il va à sa fin, puis son composant cache l'entité.
- **Trois cas couverts :**
  1. la cutscene qui a lancé le film s'arrête, par `ExitOnInput` (ADR-0090), par `Stop` ou par un échec : le film va à sa fin, puis son entité est cachée ;
  2. le film finit pendant la cutscene : son entité est cachée à l'update où il finit ;
  3. un tour suivant d'un `Repeat` relance l'action : le film repart de sa première image et se montre de nouveau.
- **Livrables associés** : ADR-0093 et un sample `Projects/VideoSpriteExitSample`.
- `PlayVideoSprite` (ADR-0091) ne change pas.

## État vérifié du dépôt (2026-11-12)

Lu sur `main` à `fd831709`, égal au pointeur du projet consommateur. Arbre de travail propre.

- **`PlayVideoSprite`.** Le cas de la fabrique montre l'entité et appelle `Play()`. Il attend ensuite tant que le film joue et que `PlayGeneration` n'a pas changé. Son `finally` appelle `Stop()` et cache l'entité, sauf si une autre lecture a pris le film ou si le monde est effacé (`CasaEngine/Framework/Cutscenes/CutsceneActionCoroutineFactory.cs:228-263`).
- **Arrêt d'une cutscene.**
  - La veille d'`ExitOnInput` appelle `CutsceneDirector.Play(next)` (`CutsceneInputWatch.cs:68`). `Play` arrête la lecture en cours (`CutsceneDirector.cs:52-62`), et l'arrêt marque toutes les coroutines du directeur comme arrêtées (`:88-103`, `:181-186`).
  - Une coroutine arrêtée n'exécute son `finally` qu'à une mise à jour suivante du `CoroutineManager` (`CoroutineManager.cs:50-82`, `:430`, et le commentaire de `CutsceneActionCoroutineFactory.cs:250-252`).
  - Un échec (`MarkRuntimeFailure`) déroule la lecture puis arrête ses branches (`CutsceneDirector.cs:139-160`).
- **Ordre d'une image.** `World.Update` met à jour les coroutines (`RuntimeSystems.Update`, `World.cs:618` ; `WorldRuntimeSystems.cs:23`), puis les entités (`World.cs:620-635`).
- **Composant.** Dans `VideoSpriteComponent.cs` :
  - `Play()` remet le lecteur sur l'image 0, passe en lecture et incrémente `PlayGeneration` (`:170-180`) ;
  - `Stop()` arrête la lecture (`:183-186`) ;
  - `Update` avance le lecteur du temps reçu tant que le film joue, puis arrête la lecture quand le lecteur est fini (`:188-202`) ;
  - `Detach` et `Dispose` libèrent le lecteur (`:302-321`).
- **Lecteur.** `MdecVideoPlayer.Advance` passe `IsEnded` à vrai quand le temps cumulé atteint la fin du film. La dernière image reste alors affichée, et une image qui ne se décode pas finit aussi le film (`CasaEngine/Framework/Video/MdecVideoPlayer.cs:105-125`, `:139-169`).
- **Fin vue par `PlayVideoSprite`.** Le film finit dans l'`Update` du composant, après les coroutines. L'action voit donc la fin à l'image suivante, et la dernière image reste dessinée une image de plus. C'est le comportement documenté de l'action (`docs/engine/video.md` §9.4), que ce plan ne change pas.
- **Messages d'échec.** `TryGetVideoSpriteEntity` écrit le préfixe `PlayVideoSprite:` en dur (`CutsceneActionCoroutineFactory.cs:619-644`).
- **Points d'entrée d'un type d'action.**
  - `CutsceneActionTypes.cs:23` ;
  - chargement et sauvegarde dans `Serialization/CutsceneAssetJsonSerializer.cs:206-211`, `:365` et `:413-418` ;
  - validateur `CutsceneValidator.cs:173-174` ;
  - constructeur en lecture seule de l'éditeur, `CasaEngine.EditorServices/Cutscenes/CutsceneReadOnlyDocumentBuilder.cs:105-107` ;
  - fabrique.
- **`Repeat` sans fin.** Un tour où aucune image ne passe fait échouer la lecture (`CutsceneActionCoroutineFactory.cs:369-373`).
- **Tests existants réutilisables.**
  - `CasaEngine.Tests/Video/PlayVideoSpriteCutsceneActionGpuTests.cs` simule une image du jeu : les coroutines, puis l'entité. Il utilise `WorldVideoHarness` et `WorldVideoTestFiles`.
  - `CasaEngine.Tests/Cutscenes/ExitOnInputCutsceneActionTests.cs` déclenche une vraie veille par un `KeyboardManager` de test, à travers `ExitOnInputResolver` (`:438-490`).
- **Sample voisin.** `Projects/VideoSpriteSample`, accepté en recette, avec un clip synthétique de 4 s généré par `CasaEngine.Tests/Video/VideoSampleClip.cs`.
- **ADR.** Prochain numéro libre : 0093.

## Décisions verrouillées

**Arbitrées avec l'auteur le 2026-11-12.**

| Réf | Décision |
|---|---|
| D1 | **Contenu.** Un film du monde lancé par une cutscene va à sa fin quand cette cutscene s'arrête, puis son entité est cachée. Les trois cas de l'objectif sont couverts |
| D2 | **Forme : une nouvelle action `StartVideoSprite`.** Elle montre l'entité, lance le film et rend la main aussitôt. Le composant cache l'entité à la fin du film. Aucune fin de la cutscene (arrêt, remplacement, échec, fin normale) ne touche le film, comme pour `MoveSprite` et `TintSprite` (ADR-0090). Seul l'effacement du monde l'arrête. `PlayVideoSprite` ne change pas |
| D3 | **Fin du film.** Le composant cache l'entité à l'update même où le film finit. La dernière image reste donc affichée exactement sa durée, sans l'image de plus que montre `PlayVideoSprite` |
| D4 | **Sample.** Un nouveau projet, `Projects/VideoSpriteExitSample`. `Projects/VideoSpriteSample` ne change pas |
| D5 | **Format et API additifs.** Les projets, mondes et cutscenes existants se chargent et se comportent comme avant. ADR-0093 enregistre la décision |
| D6 | Rien dans ce plan, l'ADR, le code, les tests, le sample ni les commits ne nomme un projet consommateur, un jeu ou une décompilation. Les données sont synthétiques |

**Proposés par ce plan, verrouillés par son approbation.**

| Réf | Choix |
|---|---|
| C1 | **JSON.** `{"type": "StartVideoSprite", "entity": "<name>"}`, avec une constante `CutsceneActionTypes.StartVideoSprite` et une classe `StartVideoSpriteCutsceneActionData`, jumelle de `PlayVideoSpriteCutsceneActionData` (`EntityName`, `FormatErrors`). <ul><li>Le chargement et la sauvegarde suivent `PlayVideoSprite`.</li><li>Le validateur refuse un `entity` absent, vide ou malformé, avec le nom de l'action.</li><li>Le constructeur en lecture seule de l'éditeur montre `entity`.</li></ul> |
| C2 | **Composant.** <ul><li>Surcharge publique `Play(bool hideWhenEnded)`. `Play()` devient `Play(false)`, sans autre changement de comportement.</li><li>Chaque `Play` pose le drapeau à la valeur reçue, et `Stop()` le remet à faux.</li><li>Dans `Update`, quand le lecteur finit pendant la lecture, y compris sur une image qui ne se décode pas, la lecture s'arrête comme aujourd'hui. Si le drapeau est vrai, il repasse à faux et `Owner.IsVisible` passe à faux, dans la même update.</li><li>Accesseur interne `HidesWhenEnded`, pour les tests.</li><li>Ni allocation ni journal par image.</li></ul> |
| C3 | **Action.** <ul><li>Mêmes contrôles que `PlayVideoSprite` : entité inconnue, racine qui n'est pas un `VideoSpriteComponent`, composant sans lecteur. Chacun fait échouer la lecture (`Failed`), avec un message préfixé `StartVideoSprite:` qui nomme l'entité.</li><li>`TryGetVideoSpriteEntity` reçoit le nom de l'action en paramètre. Les messages de `PlayVideoSprite` restent identiques, octet pour octet.</li><li>Ensuite, l'entité devient visible et l'action appelle `Play(hideWhenEnded: true)`.</li><li>L'action ne cède aucune image : l'action suivante d'une `Sequence` s'exécute dans la même update. Elle ne laisse ni coroutine, ni `finally`, ni nettoyage. Un arrêt, un remplacement ou un échec de la lecture ne touche donc pas le film.</li><li>Seul l'effacement du monde l'arrête, par `Detach`.</li></ul> |
| C4 | **Cohabitation avec `PlayVideoSprite`, par `PlayGeneration`.** <ul><li>Un `StartVideoSprite` sur un film qu'attend un `PlayVideoSprite` prend le film. L'ancienne action sort de son attente sans le toucher, comme pour deux `PlayVideoSprite` aujourd'hui.</li><li>Un `PlayVideoSprite` sur un film lancé par `StartVideoSprite` appelle `Play()`, ce qui remet le drapeau à faux. L'action possède alors le film comme aujourd'hui, et son nettoyage s'applique.</li></ul> |
| C5 | **`Repeat`.** Un tour qui ne contient qu'un `StartVideoSprite` ne laisse passer aucune image. Dans un `Repeat` sans fin, il fait donc échouer la lecture par la règle existante. Un tour met un `Wait` ou une autre action qui prend du temps, et la doc le dit |
| C6 | **Tests.** Les tests GPU utilisent le harnais existant : une image du jeu exécute les coroutines, puis l'entité. Détail en T1.1 et T1.2 |
| C7 | **Docs.** <ul><li>ADR-0093.</li><li>`docs/engine/video.md` : un nouveau §9.5 « The `StartVideoSprite` cut-scene action ». Le §9.3 gagne la surcharge `Play(bool)` et la fin cachée, et sa note sur la première image vaut pour les deux actions.</li><li>`docs/engine/cutscene_commandes_sequentielles_async_coroutine.md` gagne une section `StartVideoSprite`.</li><li>`docs/README.md` mentionne la nouvelle action.</li></ul> |
| C8 | **Sample `Projects/VideoSpriteExitSample`.** <ul><li>Un projet sans DLL. Le monde a une caméra 2D, un fond noir et une entité `clip` cachée au chargement, dont la racine est un `VideoSpriteComponent`. Le clip est une copie de `Projects/VideoSpriteSample/Videos/clip.mdecvideo`.</li><li>Un mapping `sample-input` a une entrée `exit` : `AnyKey`, et Start de la manette.</li><li>La cutscene de boucle arme `ExitOnInput` vers la cutscene de sortie, puis lance un `Repeat` sans fin. Chaque tour est `StartVideoSprite` sur `clip`, suivi d'un `Wait` de 5 s : le film dure 4 s, puis l'écran reste noir 1 s.</li><li>La cutscene de sortie assombrit l'écran sans le noircir, par un `FadeScreen` soustractif vers un gris de 128 en 1 s. Elle attend 5 s, plus que le reste du film, puis passe à un monde de fin par `ChangeWorld`.</li><li>Le monde de fin attend 2 s, efface le voile, puis revient au monde du film.</li><li>Un test de données vérifie le clip, les références, le catalogue et la validation des cutscenes, comme `VideoSpriteSampleDataTests`.</li><li>Un README en anglais dit ce qu'il faut regarder : après une touche, le film continue sous le voile jusqu'à sa dernière image, puis disparaît ; sans touche, il rejoue à chaque tour.</li></ul> |
| C9 | **Launcher.** Toute exécution du Launcher par l'agent est annoncée à l'auteur avant de commencer (C9 d'ADR-0091) |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/video-sprite-runs-to-end`**, créée depuis `main` (`fd831709`). Ne jamais committer sur `main`. Si `main` n'est plus à `fd831709` au lancement, s'arrêter et demander la base à l'auteur.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin :
  1. lancer la validation indiquée ;
  2. remplacer l'icône par `✅`, `🧪` ou `⚠️` ;
  3. ajouter une courte note de validation sous la tâche ;
  4. **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` à 0 erreur.
- **Tests** : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, jamais avec `--no-build`.
- Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans `Update` et le dessin du composant ; sérialisation additive.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts, dont les nouveaux tests.
- **Non-régression.** Les tests existants restent verts sans être modifiés, ceux de `PlayVideoSprite` compris, messages d'échec compris. `Projects/VideoSpriteSample` n'est pas touché.
- **Smoke** : `dotnet run --project CasaEngine.Launcher -- Projects/VideoSpriteExitSample/VideoSpriteExitSample.json`, annoncé d'abord (C9).

---

## Phase 0 — Plan et ADR

### ✅ T0.1 — Plan et ADR-0093

- Objectif : committer ce plan et sa ligne d'index, et écrire ADR-0093, qui reprend D1 à D6 et C1 à C9.
- Fichiers : ce plan, `ai-agent/README.md`, `docs/decisions/0093-a-world-video-can-run-to-its-end-after-its-cutscene.md`, `docs/decisions/README.md`.
- Validation : l'ADR est relue contre D1 à D6 et C1 à C9. Contrôle D6 par `rg` sur le plan, l'index et l'ADR.
- Commit : `docs(video): plan a world video that runs to its end, and record ADR-0093`
- Note : fait le 2026-11-12, en mode AUTO choisi par l'auteur. ADR-0093 relue contre D1 à D6 et C1 à C9. Contrôle D6 par `rg` sur le plan, l'index et l'ADR : aucun nom. Aucun code touché.

---

## Phase 1 — Action et composant

### ✅ T1.1 — Composant : `Play(bool hideWhenEnded)`

- Objectif : C2.
- Fichiers : `VideoSpriteComponent.cs` et des tests GPU dans `CasaEngine.Tests/Video/`.
- Tests, avec le harnais d'images de `PlayVideoSpriteCutsceneActionGpuTests` :
  - `Play(true)` : l'entité reste visible à chaque update tant que `IsEnded` est faux, et elle est cachée à l'update où `IsEnded` devient vrai. L'indice de cette update est écrit dans le test : la fin attendue, calculée d'après le pas et le nombre d'images du film de test.
  - `Play(false)` et `Play()` : l'entité reste visible après la fin, comme aujourd'hui.
  - `Play(true)` puis `Stop()` : l'entité n'est jamais cachée par le composant, et `HidesWhenEnded` est faux.
  - `Play(true)` puis `Play()` : à la fin, l'entité reste visible.
  - `Play(true)` sur un film dont une image après la première ne se décode pas (`corruptFrame` de `WorldVideoTestFiles.Write`) : l'entité est cachée à l'update où le lecteur finit.
  - Aucune allocation dans `Update` sur un film lancé par `Play(true)`, y compris à l'update de la fin, mesurée par `GC.GetAllocatedBytesForCurrentThread` comme dans `VideoSpriteComponentGpuTests`.
- Exécutions à consigner dans la note : le test de la fin cachée échoue sans la ligne qui cache l'entité ; le test `Stop()` échoue sans la remise à faux dans `Stop()` ; le test `Play(true)` puis `Play()` échoue sans la remise à faux dans `Play()`. Avec tout en place, tout passe.
- Validation : deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert, sans test existant modifié.
- Commit : `feat(video): let a world video hide its entity when it ends`
- Note : fait le 2026-11-12.
  - Code : `VideoSpriteComponent` gagne le champ `_hideWhenEnded`, la surcharge publique `Play(bool hideWhenEnded)`, à laquelle `Play()` délègue avec `false`, et l'accesseur interne `HidesWhenEnded`. `Stop()` remet le drapeau à faux. Dans `Update`, quand le lecteur finit pendant la lecture, le drapeau repasse à faux et `Owner.IsVisible` à faux.
  - Tests : `CasaEngine.Tests/Video/VideoSpriteHideWhenEndedGpuTests.cs`, 7 tests. Film de 3 images à 10 images par seconde, update de 0,1 s : l'entité est visible avant les updates 1 à 3 et cachée à l'update 3, celle où `IsEnded` devient vrai. `Play()` et `Play(false)` laissent l'entité visible. `Stop()` remet le drapeau. `Play(true)` puis `Play()` laissent l'entité visible. Une image 1 corrompue cache l'entité dès l'update 1. Enfin, 0 octet alloué sur 32 updates qui comprennent la fin. Écart de calcul du test : 1/30 s arrondi en ticks donne 32 updates un peu plus courtes que 32 images, donc la fenêtre compte 32 updates après 2 updates de chauffe.
  - Exécutions en échec consignées : sans la ligne qui cache l'entité, 3 tests échouent (la fin, l'image corrompue, l'allocation, qui vérifie aussi l'entité cachée) ; sans la remise à faux dans `Stop()`, le test `Stop()` échoue ; avec un `|=` au lieu de l'affectation dans `Play(bool)`, le test `Play(true)` puis `Play()` échoue. Avec le code livré, 7 sur 7 passent.
  - Validation : `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` à 0 erreur ; `CasaEngine.Tests` 5611 réussis sur 5611, aucun test existant modifié.

### ✅ T1.2 — Action `StartVideoSprite`

- Objectif : C1, C3, C4 et C5.
- Fichiers :
  - `CutsceneActionTypes.cs`, `StartVideoSpriteCutsceneActionData.cs` (nouveau) ;
  - `Serialization/CutsceneAssetJsonSerializer.cs`, `CutsceneValidator.cs`, `CutsceneActionCoroutineFactory.cs` ;
  - `CasaEngine.EditorServices/Cutscenes/CutsceneReadOnlyDocumentBuilder.cs` ;
  - des tests dans `CasaEngine.Tests/Cutscenes/` et `CasaEngine.Tests/Video/`.
- Tests sans GPU : aller-retour JSON, refus d'un `entity` absent, vide ou malformé, constructeur en lecture seule, échecs nommés pour une entité inconnue, une racine qui n'est pas vidéo et un composant sans lecteur.
- Tests GPU, avec le harnais d'images :
  - **Aucune image cédée.** Dans `Sequence[StartVideoSprite, ShowSprite]`, ou avec une autre action dont l'effet se lit dans la même update, l'action suivante a son effet à l'update du lancement.
  - **Cas 2, fin pendant la cutscene.** Une `Sequence` qui lance le film puis attend plus longtemps que lui. L'entité est cachée à l'update où le film finit, et non à la suivante. Dans le même fichier, un `PlayVideoSprite` sur le même film cache l'entité une update plus tard : c'est l'écart de D3, mesuré.
  - **Cas 1, arrêt de la cutscene.** Le film lancé, la lecture est arrêtée de quatre façons :
    1. par une veille d'`ExitOnInput`, armée par l'action et déclenchée par une touche du `KeyboardManager` de test, à travers `ExitOnInputResolver`, comme dans `ExitOnInputCutsceneActionTests` ;
    2. par `CutsceneDirector.Play` d'une autre cutscene ;
    3. par `CutsceneDirector.Stop()` ;
    4. par une action suivante qui échoue.

    Dans chaque cas, au moins deux updates après l'arrêt, le film joue encore, son image courante avance, et l'entité est visible. L'entité est cachée à l'update où le film finit.
  - **Cas 3, relance par `Repeat`.**
    - Deux tours de `Sequence[StartVideoSprite, Wait plus long que le film]` : le film part deux fois de l'image 0, et `PlayGeneration` augmente de 2. L'entité est cachée à la fin du premier passage, puis visible au second tour, et cachée à sa fin.
    - Un tour plus court que le film : le second lancement remet le film sur l'image 0 avant sa fin, et l'entité reste visible jusqu'à la fin du dernier passage.
    - Un `Repeat` sans fin dont le tour ne contient que `StartVideoSprite` échoue par la règle existante (C5).
  - **Cohabitation (C4).** Un `StartVideoSprite` pendant qu'un `PlayVideoSprite` attend le même film : l'ancienne action sort sans arrêter ni cacher le film, et l'entité est cachée à la fin du film. Un `PlayVideoSprite` après un `StartVideoSprite` : son nettoyage s'applique comme aujourd'hui, si sa lecture est arrêtée.
  - **Monde effacé** pendant le film : aucune exception, et le composant est libéré.
- Exécutions à consigner dans la note : chaque test du cas 1 échoue quand l'action délègue au cas `PlayVideoSprite` ; le test du cas 2 échoue sans l'appel `Play(hideWhenEnded: true)`, remplacé par `Play()`. Avec le code livré, tout passe.
- Validation : deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert, sans test existant modifié.
- Commit : `feat(cutscenes): add a StartVideoSprite action that lets the film run to its end`
- Note : fait le 2026-11-12.
  - Code : constante `CutsceneActionTypes.StartVideoSprite` et classe `StartVideoSpriteCutsceneActionData`, ajoutées au chargement, à la sauvegarde, au validateur et au constructeur en lecture seule. Le cas de la fabrique fait les contrôles, rend l'entité visible et appelle `Play(hideWhenEnded: true)`, sans `yield` ni `finally`. `TryGetVideoSpriteEntity` reçoit le nom de l'action, et `PlayVideoSprite` lui passe `"PlayVideoSprite"`.
  - Tests sans GPU : `CasaEngine.Tests/Cutscenes/StartVideoSpriteCutsceneActionTests.cs`, 12 tests. Ils couvrent l'aller-retour JSON, le refus d'un `entity` absent ou malformé (4 formes) et le constructeur en lecture seule. Ils vérifient le texte exact des trois échecs, et les trois messages de `PlayVideoSprite`, inchangés à l'octet.
  - Tests GPU : `CasaEngine.Tests/Video/StartVideoSpriteCutsceneActionGpuTests.cs`, 11 tests.
    - Aucune image n'est cédée : un `HideSprite` qui suit agit dans la même update.
    - Cas 2 : film de 3 images à 10 images par seconde, lancé à l'update 1. L'entité est cachée à l'update 3 et visible après 2 updates ; le même film par `PlayVideoSprite` est caché à l'update 4.
    - Cas 1, par `ExitOnInput` (vraie veille, touche Espace du `KeyboardManager` de test), par une autre lecture, par `Stop()` et par un échec : le film avance et reste visible deux updates après l'arrêt, puis l'entité est cachée à l'update 30, la fin d'un film de 30 images.
    - Cas 3 : deux tours plus longs que le film donnent deux suites de 2 updates visibles et `PlayGeneration` + 2. Un tour plus court remet le film à l'image 0 une fois et l'entité reste visible jusqu'à la dernière fin. Un `Repeat` sans fin de l'action seule échoue.
    - C4 dans les deux sens, et un monde effacé sans erreur.
  - Exécutions en échec consignées. Quand l'action délègue au cas `PlayVideoSprite`, 11 tests GPU échouent, dont les quatre variantes du cas 1. Avec `Play()` au lieu de `Play(hideWhenEnded: true)`, 9 tests GPU échouent, dont le cas 2. Avec le code livré, 23 sur 23 passent.
  - Validation : `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` à 0 erreur ; `CasaEngine.Tests` 5634 réussis sur 5634, aucun test existant modifié.

---

## Phase 2 — Sample et documentation

### 🧪 T2.1 — Sample `Projects/VideoSpriteExitSample`

- Objectif : C8.
- Validation :
  - le test de données passe ;
  - au Launcher, après annonce à l'auteur (C9), une observation ou une capture hors dépôt montre, après une touche pendant le film, le film qui continue sous le voile jusqu'à sa dernière image, puis l'entité cachée. Sinon, c'est noté pour la recette ;
  - deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(samples): add a sample where a world video runs to its end after an exit`
- Note : faite le 2026-11-12, reste 🧪 jusqu'à la recette.
  - Sample `Projects/VideoSpriteExitSample`. Projet sans DLL, monde `ExitFilmWorld.world` (caméra 2D, fond noir, entité `clip` cachée, `VideoSpriteComponent` à l'échelle 1,5) et monde `EndWorld.world`. Trois cutscenes, comme en C8 : boucle `StartVideoSprite` puis `Wait` de 5 s ; sortie par un voile soustractif de 128 en 1 s, une attente de 5 s, puis `ChangeWorld` ; fin qui attend 2 s, efface le voile et revient. Le mapping est `AnyKey` plus Start de la manette. Le clip est copié de `Projects/VideoSpriteSample`. Les GUID sont neufs. README en anglais.
  - Test de données `CasaEngine.Tests/Cutscenes/VideoSpriteExitSampleDataTests.cs`, 13 tests. Le clip est égal au générateur et à la copie voisine, et les cutscenes se valident. Les attentes dépassent la durée du clip, lue dans son conteneur (4 s). Le test couvre aussi le voile soustractif de 128, le mapping, les mondes, les cibles de `ChangeWorld`, le catalogue et le projet.
  - Launcher, annoncé avant (C9) : lancé 20 s sur le sample. La fenêtre « Video Sprite Exit Sample » s'ouvre, et la sortie de la console ne montre aucune erreur. L'agent ne voit pas l'écran : le contrôle visuel reste à la recette (T3.2). La fermeture de la fenêtre par l'agent a été refusée par le contrôle de sécurité de la session ; la fenêtre est restée ouverte pour l'auteur.
  - Validation : `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` à 0 erreur ; `CasaEngine.Tests` 5647 réussis sur 5647.

### ✅ T2.2 — Documentation

- Objectif : C7.
- Validation : relecture contre le code livré ; deux `.sln` à 0 erreur.
- Commit : `docs(video): describe the StartVideoSprite action`
- Note : faite le 2026-11-12.
  - `docs/engine/video.md` : le §9.3 gagne `Play(bool hideWhenEnded)` et la fin cachée, et sa note sur la première image vaut pour les deux actions. Nouveau §9.5 « The `StartVideoSprite` cut-scene action ».
  - `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md` : section `StartVideoSprite` (champ, absence d'attente et de nettoyage, fin, relance, cohabitation, échecs, images mesurées par les tests de T1.2, limites).
  - `docs/README.md` : la ligne de `video.md` cite l'action et ADR-0093.
  - ADR-0093 relue contre le code livré : aucune correction.
  - Validation : relecture contre le code et les mesures des tests ; aucun code touché depuis T2.1, dont les deux `.sln` étaient à 0 erreur.

---

## Phase 3 — Vérification et recette

### ✅ T3.1 — Vérificateur neuf

- Objectif : une passe de `verifier` neuf sur la branche : D1 à D6, C1 à C9, les trois cas, la non-régression de `PlayVideoSprite`, D6, les builds et les tests.
- Validation : verdict CONFIRMED. Chaque constat est traité en `FIX`, `DEFER` ou `REJECT` selon sa priorité, et noté ici.
- Commit : `docs(ai-agent): record the verification of the world video that runs to its end`
- Note : passe du 2026-11-12 sur `fd831709..752d84db` : **CONFIRMED**, aucun P0 à P3.
  - Exécuté par le vérificateur :
    - `CasaEngine.Tests` : 5647 sur 5647, tests GPU compris ;
    - `CasaEngine.Editor.MonoGame.sln` : 0 erreur ;
    - `CasaEngine.MonoGame.sln` : aucune erreur de compilation. Deux erreurs de copie (MSB3027, MSB3021) viennent de `CasaEngine.dll`, que verrouille la fenêtre du Launcher restée ouverte depuis T2.1. Tous les autres projets compilent.
  - Points confirmés :
    - pas de `yield` ni de `finally` dans l'action ;
    - entité cachée dans l'update de la fin, avant le dessin ;
    - fin sur une image qui ne se décode pas ;
    - garde par `PlayGeneration` dans les deux ordres possibles du `finally` tardif d'un `PlayVideoSprite` arrêté ;
    - mise à jour d'une entité cachée, indépendante de `IsVisible` ;
    - tous les points d'entrée d'un type d'action ;
    - messages de `PlayVideoSprite` identiques ;
    - aucun test existant modifié ;
    - sample conforme à C8, clip identique au voisin ;
    - doc conforme au code ;
    - contrôle D6 vide sur le diff et les messages.
  - **DEFER**, P4 : aucun test ne remplace une lecture qui attend dans un `PlayVideoSprite` par une cutscene qui commence par `StartVideoSprite`. Ce cas est celui du `finally` tardif. Il est correct à la lecture dans les deux ordres, grâce à la garde par `PlayGeneration`.
  - **DEFER**, P4 : le sample n'est pas vu à l'écran. C'est la recette T3.2.

### ⏳ T3.2 — Recette de l'auteur

- Objectif : l'auteur lance `Projects/VideoSpriteExitSample` et vérifie :
  - sans touche, le film rejoue à chaque tour ;
  - après une touche pendant le film, le film continue sous le voile jusqu'à sa dernière image, puis disparaît ;
  - après le monde de fin, le sample recommence.
- Validation : accord de l'auteur, noté ici.
- Commit : `docs(ai-agent): record the accepted check of the world video sample`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Aucun pour l'instant | — |

## Hors périmètre

- Changer `PlayVideoSprite` : son attente, son nettoyage et l'image de plus à la fin restent ceux d'ADR-0091.
- Une action qui arrête ou cache un film lancé par `StartVideoSprite` depuis une autre cutscene.
- La première image dessinée, qui est celle atteinte après la première avance du composant, dans l'update même du lancement (`docs/engine/video.md` §9.3). La règle vaut pour les deux actions et n'est pas changée.
- Le son des films, l'édition des nouvelles données dans l'éditeur (le constructeur en lecture seule et la sauvegarde sont faits).
