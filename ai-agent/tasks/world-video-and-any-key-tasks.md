# Plan agent IA — Film dans le monde et touche quelconque

Demande d'un projet consommateur, arbitrée avec l'auteur le 2026-11-09. Une cutscene doit pouvoir jouer un film sous des sprites du monde, par exemple un sprite qui apparaît en fondu par-dessus le film. Elle doit aussi pouvoir sortir quand le joueur appuie sur n'importe quelle touche du clavier.

Les décisions D1 → D8 ont été arbitrées avec l'auteur le 2026-11-09. Les choix C1 → C9 sont proposés par ce plan et verrouillés par son approbation. **Ce plan les applique, il ne les rediscute pas.**

Relu par trois plan-verifiers neufs, qui ont tous rendu REVISE :
1. Alt+Tab, entité vidéo cachée au chargement, format JSON d'`AnyKey` ;
2. échecs de création du film ;
3. image 0 indécodable, issue d'un échec selon le chemin de chargement.

Chaque point est corrigé. Aucune relecture n'a rendu READY. L'auteur approuve le plan le 2026-11-09 (« J'approuve le plan »). Le lancement attend son go.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

- **Un composant vidéo du monde.** Une entité du `.world` porte un `VideoSpriteComponent`, qui dessine un film comme un sprite, avec une position et un Z. Les sprites de Z plus petit se dessinent par-dessus.
- **Une action `PlayVideoSprite`.** Elle montre l'entité, joue le film depuis le début, attend sa fin, puis cache l'entité.
- **Un appareil d'entrée `AnyKey`.** Une entrée de `.buttonsMapping` qui le porte réagit à chaque nouvel appui d'une touche du clavier, sauf Maj, Ctrl, Alt et Windows seules. `ExitOnInput` (ADR-0090) l'utilise comme n'importe quelle entrée.
- **Livrables associés** : ADR-0091 et un sample `Projects/VideoSpriteSample` avec un clip synthétique.

Le chantier ne change ni `PlayVideo`, ni l'élément `<Video>`, ni la navigation de l'UI (voir « Hors périmètre »).

## État vérifié du dépôt (2026-11-09)

Lu sur `main` à `5600f4d3`, égal au pointeur du projet consommateur. Lecture seule, par une découverte en deux parties dont les faits décisifs ont été recontrôlés.

- **Lecteur vidéo (ADR-0089).**
  - `MdecVideoPlayer(GraphicsDevice, MdecVideoAsset, Action<MdecVideoPlayer> disposed = null)` (`CasaEngine/Framework/Video/MdecVideoPlayer.cs:47`) ouvre le fichier et décode l'image 0 à la construction.
  - Il possède une `Texture2D` privée de la taille affichée (`:28`, créée en `:64`), qu'il met à jour par un seul `SetData` par nouvelle image (`:150`).
  - Il expose `CurrentImage` (un `IUIImageResource`, `:85`), `Advance(TimeSpan)` (`:99`), `Restart()` (`:122`), `IsEnded` (`:91`) et `Dispose()` (`:166`). Il implémente `IUIVideo` de MGUI.
  - Aucun accès public à sa `Texture2D`.
  - `CasaUIAssetProvider.TryCreateVideo` (`CasaEngine/Framework/UI/Backend/MonoGame/Assets/CasaUIAssetProvider.cs:249`) trouve un asset catalogué `mdecvideo` par nom ou id, l'acquiert par l'`AssetContentManager`, et crée le lecteur avec le `GraphicsDevice` de ce dernier.
- **Ordre de dessin.**
  - Les sprites du monde se trient par Z quand aucune entrée n'a de clé : le plus grand Z se dessine d'abord (`SpriteRendererComponent.cs:553-569`).
  - L'UI d'une vue se dessine après le rendu de la scène (`UI/UIRoot.cs:195-206`). L'écran d'une vidéo de `PlayVideo` couvre donc les sprites du monde.
  - `SpriteRendererComponent.DrawSprite(Texture2D, Rectangle, Point, Vector2, float, Vector2, Color, float, SpriteEffects)` dessine une texture brute, avec un rectangle source et une origine, sans clé et par le chemin opaque (`:907`).
  - **Profondeur.** Les sprites triés testent et écrivent la profondeur en `LessEqual` (`:137-141`). Le code suppose qu'ils partagent un même Z et se dessinent dans l'ordre du peintre (`:230-235`). Une entrée marquée `IgnoresDepth` passe en `DepthStencilState.None` le temps de sa suite (`:275-278`). Seule la surcharge qui porte `ignoresDepth` le règle, et elle impose une clé de tri (`:969-975`).
  - **Caméra 2D.** Elle est placée à `target.Z + DistanceToTargetPlane` et regarde la cible (`Scene/Entities/Components/Camera2dComponent.cs`, `SetPositionAndTarget`). Un Z plus grand est donc plus près d'elle. Comme le plus grand Z se dessine d'abord, un film qui écrit la profondeur, avec un Z plus grand qu'un sprite, ferait échouer ce sprite au test de profondeur.
- **Sprite d'entité (ADR-0090).** Après T7 :
  - `StaticSpriteComponent` garde le nom de son sprite au chargement et le résout dans `InitializeWithWorld`.
  - Il passe sa couleur au rendu, et son `Update` fait avancer mouvement et teinte avec le temps reçu.
  - La sauvegarde de l'éditeur a une branche par composant (`CasaEngine.EditorServices/EditorEntityJsonSerializer.cs:267`, `:387`).
  - `Entity.IsVisible` se lit de `is_visible`.
- **`PlayVideo`** ouvre un écran et attend ses vidéos (`CasaEngine/Framework/Cutscenes/CutsceneActionCoroutineFactory.cs:167-186`, `:380-413`). Une action touche six endroits : le type, la classe de données, le sérialiseur, le validateur, la fabrique et le builder de l'éditeur.
- **Entrée.**
  - `InputDevices` vaut `NoDevice`, `Keyboard`, `GamePad` ou `Mouse` (`CasaEngine/Engine/Input/InputDevices.cs`).
  - `KeyButton.IsPressed` est un `switch` sur l'appareil, qui lève sur une valeur inconnue (`KeyButton.cs:42-52`). `KeyButton.Load` lit `input_device` sans tenir compte de la casse (`:59`, `JsonHelper.cs:187-190`). L'éditeur l'écrit par `ToString()` (`EditorAssetJsonSerializer.cs:185`).
  - `KeyboardManager` garde l'état courant et le précédent et offre `IsKeyJustPressed(Keys)` (`KeyboardManager.cs:12-15`). Il n'a rien pour « une touche quelconque ».
  - MonoGame DesktopGL est en 3.8.5.1 (`Directory.Packages.props:18`). Le moteur appelle déjà `GetPressedKeyCount()` et `GetPressedKeys(Keys[])` avec un tableau réutilisé, sans allouer (`CasaEngine/Framework/Input/MonoGameWindowInputSource.cs:138-141`).
  - `UINavigationInput` ne lie que `Keyboard` et `GamePad`. Une autre valeur n'est liée à rien (`UINavigationInput.cs:210`, `:260-266`, `:334-359`, `:408`).
  - La veille d'`ExitOnInput` fait avancer ses propres `InputMapping` et réagit à `Pressed && !PressedPreviousFrame` (`Cutscenes/CutsceneInputWatch.cs:56-69`).
- **Tests.**
  - `GpuDeviceHost` sert aux tests qui ont besoin d'un appareil graphique.
  - `ExitOnInputCutsceneActionTests` simule le clavier par `KeyboardState`.
  - `VideoSampleDataTests` et `SpriteCutsceneSampleDataTests` gardent les samples.
- **ADR.** Prochain numéro libre : 0091.
- **Arbre de travail.** Branche `chantier/world-video-and-any-key` créée depuis `main` `5600f4d3`. Aucun changement préexistant.

## Décisions verrouillées

**Arbitrées avec l'auteur le 2026-11-09.**

| Réf | Décision |
|---|---|
| D1 | **Contenu demandé.** Un sprite du monde peut se dessiner au-dessus d'un film. Une entrée « touche quelconque » du clavier peut faire sortir d'une cutscene. Une ADR, et un sample : un film sous un sprite qui apparaît en fondu, et une sortie par n'importe quelle touche |
| D2 | **Composant vidéo du monde.** Une entité du `.world` porte un composant vidéo, dessiné comme un sprite (position, Z) et caché au chargement. Une action la montre, joue le film depuis le début, attend la fin, puis la cache. Les sprites de Z plus petit passent devant |
| D3 | **Touche quelconque déclarée dans le mapping.** Un nouvel appareil pour une entrée de `.buttonsMapping`. `ExitOnInput` l'utilise comme les autres entrées, et d'autres écrans pourront s'en servir |
| D4 | **Touches qui comptent** : chaque nouvel appui, sauf Maj, Ctrl, Alt et Windows seules. Alt+Tab ou la touche Windows ne font pas sortir |
| D5 | **Temps.** Le film avance avec le temps de jeu écoulé, comme les actions de sprite d'ADR-0090 |
| D6 | **Format et API additifs.** Un projet, un monde, un mapping ou une cutscene existants se chargent et se comportent à l'identique. ADR-0091 enregistre les décisions |
| D7 | Rien dans ce plan, l'ADR, le code, les tests, le sample ni les commits ne nomme un projet consommateur, un jeu ou une décompilation. Les données sont synthétiques |
| D8 | Simple et optimisé : pas d'allocation par image dans l'`Update` du composant vidéo, dans son dessin, ni dans la lecture de la touche quelconque |

**Proposés par ce plan, verrouillés par son approbation.**

| Réf | Choix |
|---|---|
| C1 | **`VideoSpriteComponent`**, composant de scène dessinable. <ul><li>**JSON** : `video`, nom ou id requis d'un asset catalogué `mdecvideo`, et `color` facultatif, au format objet des entités (blanc s'il est absent). Un champ malformé fait échouer le chargement avec une `InvalidDataException` qui nomme le composant, le champ et la valeur.</li><li>**Caché au chargement (D2).** Le composant rend son entité invisible (`Entity.IsVisible = false`) dans `InitializeWithWorld`, quelle que soit la valeur d'`is_visible`. Seule `PlayVideoSprite` la montre. La doc le dit.</li><li>**Échecs de création**, tous dans `InitializeWithWorld`. Les cinq cas :<ol><li>le nom ou l'id n'est pas au catalogue ;</li><li>l'asset catalogué n'est pas de type `mdecvideo` ;</li><li>`Acquire<MdecVideoAsset>` échoue ;</li><li>le constructeur de `MdecVideoPlayer` lève : fichier absent ou impossible à ouvrir, ou échec de création de la texture (`MdecVideoPlayer.cs:64-78`) ;</li><li>l'image 0 ne se décode pas. Le constructeur ne lève pas : `ShowFrame` attrape l'erreur, rend un lecteur déjà fini (`IsEnded` vrai) et la logue une fois (`:134-163`). Le composant le détecte juste après la construction et le traite comme un échec.</li></ol>Dans chaque cas, le composant libère lui-même ce qu'il a acquis (lecteur, handle de l'asset), car le chargeur ne détache pas une entité en échec. Puis il lève une `InvalidDataException` qui nomme le composant, le champ `video` et la valeur, avec l'exception d'origine en exception interne quand il y en a une.<br>**Issue selon le chemin** :<ul><li>chargement d'un `.world` : `World.InternalAddEntities` logue l'exception et n'ajoute pas l'entité (`World.cs:886-916`). L'entité est absente du monde, et une `PlayVideoSprite` sur son nom finit en `Failed` comme entité inconnue ;</li><li>ajout d'une référence d'entité (`World.cs:1142-1149`), d'un enfant (`:1211-1217`) ou d'un composant après coup (`Entity.cs:319-327`) : l'exception remonte à l'appelant.</li></ul></li><li>**Chargement et création.** Comme `StaticSpriteComponent` après T7, `Load` ne fait que retenir le nom. `InitializeWithWorld` acquiert l'`MdecVideoAsset` par l'`AssetContentManager` du jeu et crée un `MdecVideoPlayer`. Un nom introuvable y lève une exception qui nomme la vidéo.</li><li>**Libération.** `Detach` et `Dispose` libèrent le lecteur et le handle de l'asset.</li><li>**Copie.** Le constructeur de copie reprend le nom et la couleur, et chaque clone crée son propre lecteur.</li><li>**Dessin.** Le film passe par une nouvelle surcharge publique additive de `SpriteRendererComponent`, `DrawSprite(Texture2D, Rectangle, Point, Vector2, float, Vector2, Color, float z, SpriteEffects, bool ignoresDepth)`. Elle met l'entrée en file sans clé (`hasSortKey: false`), par le chemin opaque, et marque `IgnoresDepth` comme demandé. Le composant l'appelle avec `ignoresDepth: true` : le film ne teste ni n'écrit la profondeur, et se range par Z comme un sprite sans clé.<ul><li>Avec un Z plus grand que celui d'un sprite, le film est dessiné avant lui. Le sprite, dessiné après, n'est pas bloqué par la profondeur et se mélange au film.</li><li>La source est la texture entière, et l'origine est son centre : la position est donc le centre du film. Le Z est `Position.Z`, et la couleur est passée telle quelle.</li><li>Rien n'est dessiné tant que le lecteur n'existe pas.</li></ul></li><li>**Accès à la texture.** `MdecVideoPlayer` gagne une propriété interne qui rend sa `Texture2D`. C'est un ajout interne, sans API publique nouvelle.</li><li>**Sauvegarde** par le chemin de l'éditeur, comme `StaticSpriteComponent` : `video` toujours, `color` seulement s'il n'est pas blanc.</li></ul> |
| C2 | **Lecture.** <ul><li>Le composant expose `Play()` (remet le film au début et le lance), `Stop()`, `IsPlaying` et `IsEnded`.</li><li>Son `Update` fait avancer le lecteur du temps reçu, le temps de jeu mis à l'échelle, seulement pendant la lecture.</li><li>À la fin du film, la lecture s'arrête et la dernière image reste dessinée tant que l'entité est visible.</li><li>Aucune allocation dans `Update` ni dans le dessin.</li></ul> |
| C3 | **Action `PlayVideoSprite`** : `{"type": "PlayVideoSprite", "entity": "<nom>"}`. <ul><li>Elle rend l'entité visible et appelle `Play()`, attend que le film soit fini, puis cache l'entité.</li><li>Si la lecture de la cutscene s'arrête avant (arrêt, remplacement par `ExitOnInput`, échec), le nettoyage de l'instruction arrête le film et cache l'entité, sauf quand le monde est effacé.</li><li>L'image à laquelle le film démarre et celle à laquelle l'attente se termine sont mesurées par un test et écrites dans la doc, comme pour T2.4 d'ADR-0090.</li><li>**Échecs** (`Failed`, message qui nomme le champ et la valeur) : une entité inconnue, une entité dont le composant racine n'est pas un `VideoSpriteComponent`, ou un composant non initialisé, c'est-à-dire sans lecteur parce que son `InitializeWithWorld` n'a pas tourné. Aucun chemin de chargement connu n'y mène : c'est une garde défensive, dont le message nomme l'entité. Un composant dont la création échoue n'est pas dans le monde (C1) : l'action le traite comme une entité inconnue. Le validateur refuse un `entity` absent ou vide.</li></ul> |
| C4 | **Appareil `AnyKey`**, ajouté à la fin de `InputDevices`. <ul><li>**Format JSON.** `KeyButton.Load` ne change pas : les quatre champs restent obligatoires, comme pour toute entrée aujourd'hui (`KeyButton.cs:54-60`), et un champ absent fait échouer le chargement comme aujourd'hui. Pour `AnyKey`, `key`, `game_pad_button` et `mouse_button` sont lus mais ignorés. Le sample et l'ADR les écrivent ainsi :<br>`"key_button": { "key": "None", "game_pad_button": "None", "mouse_button": "LeftButton", "input_device": "AnyKey" }`<br>dans une entrée complète du même format que celles de `Projects/VideoSample/ui-input.buttonsMapping`.</li><li>`KeyButton.IsPressed` le traite comme vrai à l'image où une touche est enfoncée alors qu'elle ne l'était pas à l'image précédente du `KeyboardManager`, à deux conditions : <ul><li>la touche n'est ni Maj, ni Ctrl, ni Alt, ni Windows (gauche et droite) ;</li><li>aucune touche Alt ni Windows n'est tenue à cette image. Ainsi, Alt+Tab, Alt+F4 ou Windows+D ne comptent pas, alors que Maj+A ou Ctrl+A comptent.</li></ul></li><li>`KeyboardManager` gagne cette lecture. Elle passe par `GetPressedKeyCount` et `GetPressedKeys` avec un tableau réutilisé, sans allocation par image.</li><li>Une entrée `AnyKey` signale donc un appui, pas un maintien. Dans `ExitOnInput`, l'appui de l'image et la règle d'armement d'ADR-0090 restent ceux d'aujourd'hui.</li><li>Deux nouveaux appuis à deux images de suite ne donnent qu'un front : limite documentée.</li><li>La navigation de l'UI ne lie rien à `AnyKey`, comme aujourd'hui pour toute valeur autre que `Keyboard` et `GamePad`.</li></ul> |
| C5 | **Éditeur.** Le builder en lecture seule montre `PlayVideoSprite`. La sauvegarde écrit le composant (C1) et l'appareil par son nom. L'édition de ces données dans l'éditeur n'est pas faite |
| C6 | **Sample `Projects/VideoSpriteSample`.** <ul><li>Un projet sans DLL, avec un monde à caméra 2D et deux entités cachées au chargement : une entité vidéo, et un sprite de Z plus petit, qui passe donc devant.</li><li>Un mapping `sample-input` avec une entrée `exit` en `AnyKey` et une entrée en Start de la manette.</li><li>La cutscene de démarrage arme `ExitOnInput`, puis lance un `Repeat` sans fin. Il contient un `Parallel` : `PlayVideoSprite` d'un côté, et de l'autre une attente puis `ShowSprite` et `TintSprite`, pour que le sprite apparaisse en fondu par-dessus le film.</li><li>La sortie fait un fondu, puis change de monde vers un monde de fin, qui revient au premier.</li><li>Le clip est celui du générateur de `VideoSample` (`VideoSampleClip`). Le test de données vérifie qu'il en est la copie octet pour octet, et vérifie aussi les références et la validation des cutscenes.</li></ul> |
| C7 | **Tests.** <ul><li>**Sans GPU** : le chargement et la sauvegarde du composant, avec ses champs malformés ; la sérialisation, la validation et les échecs de `PlayVideoSprite` ; la touche quelconque, simulée par `KeyboardState` : nouvel appui, maintien sans effet, modificateurs seuls sans effet, Maj ou Ctrl tenu puis une lettre qui compte, Alt tenu puis Tab sans effet, Windows tenu puis une lettre sans effet ; le chargement de l'entrée `AnyKey` exacte de C4, et l'échec d'un `KeyButton` sans `key` ; et `ExitOnInput` avec une entrée `AnyKey`, y compris une touche tenue à l'armement.</li><li>**Sans GPU** aussi : une entité vidéo chargée avec `is_visible` absent ou vrai est cachée après `InitializeWithWorld`, si ce chemin se teste sans appareil. Sinon, ce test passe dans les tests GPU.</li><li>**Échecs de création de C1**, un test par cas.<ul><li>Les cas : nom introuvable, asset d'un autre type, chargement de l'asset en échec, constructeur du lecteur en échec (fichier absent), et image 0 indécodable (un conteneur dont l'image 0 est corrompue).</li><li>Chaque test vérifie le type et le message de l'exception, et la libération du lecteur et du handle.</li><li>Ils passent sans GPU quand le cas le permet, sinon dans les tests GPU.</li></ul></li><li>**Chemin d'un monde** : un monde dont l'entité vidéo nomme un film absent. Le test vérifie l'erreur loguée, l'absence de l'entité, la libération des handles, et une `PlayVideoSprite` sur ce nom qui finit en `Failed` comme entité inconnue.</li><li>**Garde défensive** : `PlayVideoSprite` sur un composant non initialisé, construit et placé dans le monde sans `InitializeWithWorld`, finit en `Failed`.</li><li>**Avec GPU**, quand le lecteur réel est en jeu : le composant joue, avance, finit et garde sa dernière image ; l'action attend la fin puis cache ; le nettoyage à l'arrêt ; un sprite additif de Z plus petit qui se mélange au film (pixels relus : film plus sprite), et l'entrée du film en file, sans clé et marquée `IgnoresDepth` ; l'absence d'allocation dans `Update`.</li></ul> |
| C8 | **Docs.** ADR-0091. `docs/engine/video.md` gagne le composant et l'action, la doc des cutscenes gagne `PlayVideoSprite`, et la doc d'entrée (ou celle d'ADR-0090) gagne `AnyKey` |
| C9 | **Lancement du Launcher.** Toute exécution du Launcher par l'agent est annoncée à l'auteur avant de commencer : la fenêtre prend le focus, et la manette est lue même sans focus (O5 d'ADR-0090) |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/world-video-and-any-key`**, créée depuis `main`. Ne jamais committer sur `main`.
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
- Rappel moteur :
  - pas d'allocation, de LINQ ni de closure dans les chemins chauds : `VideoSpriteComponent.Update`, son dessin, la lecture de `AnyKey` ;
  - restaurer tout état GPU modifié ;
  - le runtime ne dépend pas de l'éditeur ;
  - sérialisation additive.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts, dont les nouveaux tests.
- **Non-régression** : les tests existants restent verts sans être modifiés. Les samples existants ne sont pas touchés.
- **Smoke** : `dotnet run --project CasaEngine.Launcher -- Projects/VideoSpriteSample/VideoSpriteSample.json`.

---

## Phase 0 — Plan et ADR

### ✅ T0.1 — Plan et ADR-0091

- Objectif : committer ce plan et sa ligne d'index, et écrire ADR-0091, qui reprend D1 à D8 et C1 à C9.
- Fichiers : ce plan, `ai-agent/README.md`, `docs/decisions/0091-a-world-video-component-and-an-any-key-input.md`, `docs/decisions/README.md`.
- Validation : ADR relue contre D1 à D8 et C1 à C9 ; contrôle D7.
- Commit : `docs(video): plan a world video component and an any-key input, and record ADR-0091`
- Note : plan approuvé par l'auteur le 2026-11-09 (« J'approuve le plan »), lancé le même jour (« Oui, lance-le »). ADR-0091 relue contre D1 à D8 et C1 à C9. Contrôle D7 fait par `rg` sur le plan, l'index et l'ADR : aucun nom.

---

## Phase 1 — Film dans le monde

### ✅ T1.1 — `VideoSpriteComponent`

- Objectif : C1 et C2.
- Fichiers : `CasaEngine/Framework/Video/MdecVideoPlayer.cs` (accès interne à la texture), `CasaEngine/Framework/Application/Components/SpriteRendererComponent.cs` (surcharge de C1), le nouveau composant, `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs`, et des tests.
- Validation :
  - tests sans GPU du chargement, de la sauvegarde et des champs malformés ;
  - une entité vidéo chargée avec `is_visible` absent ou vrai n'est pas dessinée avant `PlayVideoSprite` ;
  - les cinq échecs de création de C1, un test par cas, et le chemin d'un monde dont l'entité vidéo nomme un film absent ;
  - tests GPU : lecture, avance, fin, dernière image, libération, et un sprite de Z plus petit dessiné après le film ;
  - absence d'allocation dans `Update` ;
  - deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(video): draw a video as a sprite of the world`
- Note : Fait. `VideoSpriteComponent` (JSON `video` + `color`, nom retenu au `Load`, création dans `InitializeWithWorld` avec les cinq échecs et l'auto-libération, entité cachée, `Play`/`Stop`/`IsPlaying`/`IsEnded`, dessin sans clé avec `ignoresDepth`, copie, `Detach`/`Dispose`), accès interne `MdecVideoPlayer.Texture`, surcharge publique `DrawSprite(..., bool ignoresDepth)`, sauvegarde de l'éditeur. Validation : deux `.sln` à 0 erreur ; `CasaEngine.Tests` 5481 verts (41 nouveaux : 27 sans GPU, 14 avec GPU), aucun test existant modifié. Le test GPU de pixels relit le film plus le sprite additif de Z plus petit, avec un témoin (film qui écrit la profondeur) où le sprite est rejeté. Écarts : (1) `GetBoundingBox` est surchargé (taille de l'image autour de la position), sinon le film serait coupé par le frustum du monde ; (2) un gestionnaire d'assets sans appareil graphique est un sixième échec de création, traité comme les autres (libération du handle) ; (3) `VideoName` est une propriété publique modifiable, nécessaire à la sauvegarde de l'éditeur et aux composants construits en code ; (4) l'origine est `(largeur/2, hauteur/2)` en entiers : demi-pixel de décalage pour une dimension impaire ; (5) le composant applique `Scale` du transform, comme `StaticSpriteComponent`.

### ⏳ T1.2 — Action `PlayVideoSprite`

- Objectif : C3 et C5.
- Fichiers : les six endroits d'une action, et des tests.
- Validation :
  - sérialisation, validation et échecs testés : entité inconnue, mauvais composant racine, composant non initialisé ;
  - avec GPU : l'action montre l'entité, attend la fin et la cache ; le nettoyage à l'arrêt ; les images mesurées, notées ici ;
  - deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(cutscenes): play a world video from a cutscene`

---

## Phase 2 — Touche quelconque

### ⏳ T2.1 — Appareil `AnyKey`

- Objectif : C4 et C5.
- Fichiers : `CasaEngine/Engine/Input/InputDevices.cs`, `KeyButton.cs`, `KeyboardManager.cs`, et des tests (entrée, mapping, `ExitOnInput`).
- Validation : tests de C7 sur la touche quelconque ; absence d'allocation dans sa lecture ; tests existants de `UINavigationInput` et des mappings verts sans modification ; deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(input): let a mapping entry react to any key`

---

## Phase 3 — Sample et documentation

### ⏳ T3.1 — Sample `Projects/VideoSpriteSample`

- Objectif : C6.
- Validation :
  - le test de données passe ;
  - au Launcher, après annonce à l'auteur (C9), avec des captures hors dépôt : le film se joue, le sprite apparaît en fondu par-dessus, et la boucle repart ;
  - la sortie par une touche quelconque, et l'absence de sortie sur Alt+Tab, sont vérifiées, sinon notées pour la recette (T4.2) ;
  - deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `feat(samples): add a sample that plays a video under a sprite`

### ⏳ T3.2 — Documentation

- Objectif : C8.
- Validation : relecture contre le code livré ; deux `.sln` à 0 erreur.
- Commit : `docs(video): describe the world video component and the any-key input`

---

## Phase 4 — Vérification et recette

### ⏳ T4.1 — Vérificateur neuf

- Objectif : une passe de `verifier` neuf sur la branche : C1 à C9, D2 à D8, la non-régression, D7, les builds et les tests.
- Validation : verdict CONFIRMED. Chaque constat est traité en `FIX`, `DEFER` ou `REJECT` selon sa priorité, et noté ici.
- Commit : `docs(ai-agent): record the verification of the world video and any-key input`

### ⏳ T4.2 — Recette de l'auteur

- Objectif : l'auteur lance le sample et l'accepte, y compris la sortie par une touche quelconque et l'absence de sortie sur Alt+Tab.
- Validation : accord de l'auteur, écrit ici. La tâche reste 🧪 jusque-là.
- Commit : `docs(ai-agent): record the accepted check of the world video sample`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Le film dans le monde n'a pas de saut par Submit ou Cancel, contrairement à `PlayVideo` : la sortie se fait par `ExitOnInput`. Un saut propre au composant serait une décision suivante | Après T4.2 |
| O2 | La manette est lue même quand la fenêtre n'a pas le focus (O5 d'ADR-0090). Ce chantier ne le change pas | T3.1 |
| O3 | Des sprites mélangés de Z différents se dessinent dans l'ordre du plus grand Z, en écrivant la profondeur. Un sprite de Z plus petit, dessiné après, échoue alors sous un sprite mélangé de Z plus grand. Seul le film est sorti de ce cas par `IgnoresDepth`. Les sprites qui doivent se mélanger entre eux gardent donc un même Z, comme le suppose le code (`SpriteRendererComponent.cs:230-235`). Leur ordre à Z égal reste celui du tri. Documenté, sans changement | T1.1, T3.2 |

## Hors périmètre

- Changer `PlayVideo`, l'élément `<Video>` ou l'ordre de dessin entre le monde et l'UI.
- Le son d'un film.
- Un « bouton quelconque » de la manette ou de la souris.
- L'édition des nouvelles données dans l'éditeur (seuls le builder en lecture seule et la sauvegarde sont faits).
