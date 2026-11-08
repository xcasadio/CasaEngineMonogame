# Plan agent IA — Lecture de vidéos MDEC

Demande d'un projet consommateur, arbitrée avec l'auteur le 2026-11-08 : le moteur lit des vidéos au format de la console, des images MDEC dans un conteneur, et les montre dans un écran MGUI par une balise `<Video>`.

Les décisions D1 → D8 ont été arbitrées avec l'auteur le 2026-11-08. Les choix C1 → C10 sont proposés par ce plan et sont verrouillés par son approbation. **Ce plan les applique, il ne les rediscute pas.**

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

- Un **conteneur de vidéo** `.mdecvideo` : un en-tête, une table d'offsets, des images MDEC telles que la console les lit, et une place pour un son facultatif.
- Un **décodeur MDEC** écrit à neuf d'après la documentation publique : codes VLC des versions 1, 2 et 3, déquantification, IDCT entière, conversion YCbCr vers RGB, recadrage des tailles qui ne sont pas des multiples de 16.
- Un **lecteur** qui avance à la cadence du conteneur et met à jour une texture de taille fixe, une copie par image affichée, sans allocation par image.
- Une **balise `<Video>`** dans MGUI, alimentée par le moteur comme les images animées, qui lève des événements de fin et de saut.
- Une **action de cutscene `PlayVideo`**.
- ADR-0089 du moteur, ADR-0026 de MGUI, et un sample `Projects/VideoSample` avec un clip synthétique.

Le chantier ne lit pas le son du conteneur et ne lit pas de flux STR entrelacés (voir « Hors périmètre »).

## État vérifié du dépôt (2026-11-08)

Lu sur `main` à `778f6aa8`, égal au pointeur du projet consommateur, et sur MGUI à `a3c42cb0`. Lecture seule, par une découverte en quatre parties dont chaque rapport a été recontrôlé.

- **Images de l'UI.**
  - `IUIImageResource` est défini dans MGUI, avec trois membres seulement : `Width`, `Height`, `IsDisposed` (`MGUI/MGUI.Rendering.Abstractions/Assets/IUIImageResource.cs:4-9`).
  - Le moteur l'implémente par `CasaMonoGameImageResource`, qui enveloppe une `Texture2D` et ne la libère pas (`CasaEngine/Framework/UI/Backend/MonoGame/Assets/CasaMonoGameImageResource.cs:7-19`).
- **Images animées, le modèle de la balise.**
  - `IUIAnimatedImage` est un objet de l'hôte, propre à chaque image, avec `Advance(TimeSpan)`, `Restart`, `CurrentImage`, `CurrentSourceRect` et `CurrentDrawOffset` (`MGUI/MGUI.Shared/Assets/IUIAnimatedImage.cs`).
  - `IUIAssetProvider.TryCreateAnimatedImage` est un membre à implémentation par défaut (`MGUI/MGUI.Shared/Assets/IUIAssetProvider.cs:19-37`).
  - `MGImage` fait avancer la source dans `UpdateSelf` (`MGUI/MGUI.Core/UI/MGImage.cs:432-441`) et la libère quand sa source change (`:108-133`).
  - Le setter d'`ActualSource` ne relance le layout que si `RenderSize` change (`MGImage.cs:199-218`).
  - Le test `AnimatedImageTests.PerFrameAnimationWork_AllocatesNothing` prouve un `UpdateSelf` sans allocation (`MGUI/MGUI.Tests/Architecture/AnimatedImageTests.cs:352-387`). Une image repliée n'avance pas (`:235`).
- **Balises XAML de MGUI.**
  - Une balise est une classe DTO de `MGUI.Core.UI.XAML`, résolue par réflexion sur son nom (`MGUI/MGUI.Core/UI/XAML/XAMLParser.cs:142-170`). Le chargement strict du moteur la valide de la même façon (`CasaEngine/Framework/UI/MGUI/UIScreenLoader.cs:84`).
  - `<Image>` est en `Controls.cs:1263-1328`.
  - `MGElementType` n'a pas de valeur `Video` (`MGUI/MGUI.Core/UI/Enums.cs`).
  - Seul `Button.CommandName` relie un élément à une commande nommée, par `GetResources().TryGetCommand` (`Controls.cs:226-251`, `MGButton.cs:286-304`).
  - `DataDrivenScreen` enregistre ses `ui_commands` par `AddCommand` (`CasaEngine/Framework/UI/DataDrivenScreen.cs:244-267`). `CheckButtons` refuse un bouton qui nomme une commande non déclarée (`:505-515`).
- **Règles de MGUI.**
  - Pas d'`AGENTS.md`. Les règles sont dans `MGUI/.github/agents/mgui-engine-developer.agent.md` : pas de rupture d'API, pas d'allocation dans `Update` et `Draw`, petits commits, build vert, tests unitaires.
  - ADR dans `MGUI/docs/decisions/`, la dernière est 0025. Plans dans `MGUI/Docs/Tasks/`.
  - Le pointeur `a3c42cb0` est `origin/develop`. Le `master` local est à `73ebfa8c`, en retard de 597 commits. Il n'y a pas de branche `develop` locale. Par précédent, le travail MGUI part de `develop` (ADR-0024 de MGUI).
- **Texture modifiée à chaque image.** Aucun code du moteur ne le fait ; `SetData` ne sert qu'à des remplissages uniques (`CasaEngine/Framework/Rendering/RenderPipeline.cs:89`, `UI/Backend/MonoGame/CasaTextureCache.cs:114`). L'UI est mise à jour sur le thread du jeu, hors de tout `SpriteBatch` (`CasaEngine/Framework/Application/CasaEngineGame.cs:573-636`).
- **Son.**
  - Aucune voix n'expose de position de lecture : `IAudioBackend` n'en a pas, et l'état n'est que `Stopped`, `Playing` ou `Paused` (`CasaEngine/Framework/Audio/IAudioBackend.cs`, `AudioVoiceState.cs`).
  - Le seul signal de progression est le compte des tampons consommés (`Audio/Software/SoftwareMixer.cs:44-46`).
- **Assets.**
  - Un nouveau type demande :
    - une constante d'extension (`CasaEngine/Framework/Configuration/Constants.cs:5-43`) ;
    - un `IAssetLoader` ;
    - une ligne dans `AssetLoaderRegistry`.
  - Le type d'asset se déduit de l'extension (`Assets/AssetInfo.cs:98-106`).
  - `ResolveAssetFullPath` sert aux systèmes qui ouvrent leur fichier eux-mêmes (`Assets/AssetContentManager.cs`).
- **Cutscenes.**
  - Une action touche six endroits (ADR-0088) :
    - `CutsceneActionTypes` ;
    - la classe de données ;
    - le sérialiseur (`Serialization/CutsceneAssetJsonSerializer.cs:148`, `:233`) ;
    - le validateur (`CutsceneValidator.cs:141`) ;
    - la fabrique (`CutsceneActionCoroutineFactory.cs:147-160`) ;
    - le builder de l'éditeur (`CasaEngine.EditorServices/Cutscenes/CutsceneReadOnlyDocumentBuilder.cs:74`).
  - `OpenScreen` passe par `GameManager.OpenScreenFromCutscene`, qui n'ouvre rien sous gestion externe des vues (`Application/GameManager.cs:198-225`).
- **Entrée de l'UI.** Les actions Submit et Cancel n'existent que si le projet nomme un mapping (`UIInputMapping`, ADR-0080). Leur chemin :
  - `UINavigationInput.Fire` envoie chaque action au bureau MGUI, qui répond si l'élément qui a le focus l'a traitée (`CasaEngine/Framework/UI/UINavigationInput.cs:200-229`, `MGUI/MGUI.Core/UI/MGDesktop.cs:640-651`).
  - Seul un Cancel que personne n'a traité est rapporté, par `CancelNotHandled` (`UINavigationInput.cs:223-228`). `UIRoot` le transmet à l'écran du sommet (`CasaEngine/Framework/UI/UIRoot.cs:96-108`), qui met sa `cancel_command` en attente (`DataDrivenScreen.cs:170-179`).
  - Submit n'est rapporté à aucun écran, traité ou non. `DataDrivenScreen` ne lit aucune entrée lui-même : son `Update` ne fait que lancer les commandes laissées par MGUI ou par `ReportCancel` (`DataDrivenScreen.cs:148-168`).
  - Ordre d'une image : le bureau, puis l'entrée de navigation, puis les écrans (`UIRoot.RunFrame`, `UIRoot.cs:157-179`).
- **Code existant.** Aucun code MDEC, IDCT ou YCbCr dans le moteur ni dans MGUI. Les modules `CasaEngine/Framework/Audio/Psx` (ADR-0058) montrent un décodeur de la console dans le moteur, écrit d'après la documentation publique.
- **Tests.**
  - `GpuDeviceHost` et `[GpuFact]` relisent des pixels (`CasaEngine.Tests/UI/Backend/GpuDeviceHost.cs`, `UiClipViewSpaceGpuTests.cs:61-100`).
  - `AllocationWindow.Start()` mesure les allocations (`CasaEngine.Tests/AllocationWindow.cs:19-23`).
  - Un sample sans DLL est gardé par un test de données (`CasaEngine.Tests/UI/TimedScreenSampleDataTests.cs`).
- **Documentation publique.**
  - psx-spx décrit le décodeur matériel (https://psx-spx.consoledev.net/macroblockdecodermdec/) et le format BS des versions 1 à 3 (https://psx-spx.consoledev.net/cdromfileformats/, sections « BS Compression »).
  - Selon cette page, le calcul matériel n'est pas reproduit au bit près.
  - La découverte n'a pas lu toutes les règles de la version 1 : ses échappements diffèrent. T1.1 les lit.
- **ADR.** Prochains numéros libres : 0089 pour le moteur, 0026 pour MGUI. Une fusion avec `origin/main` peut décaler celui du moteur (ADR-0087 et ADR-0088 l'ont été).
- **Arbre de travail.** Branche `chantier/video-playback` créée depuis `main` `778f6aa8`, égal au pointeur. Aucun changement préexistant.

## Décisions verrouillées

**Arbitrées avec l'auteur le 2026-11-08.**

| Réf | Décision |
|---|---|
| D1 | **Contenu demandé.** <ul><li>Un conteneur : en-tête (largeur, hauteur, nombre d'images, cadence), table d'offsets, images MDEC, son facultatif.</li><li>Un décodeur écrit à neuf d'après psx-spx et MPEG-1 : VLC, déquantification, IDCT, YCbCr vers RGB, chemin du DC brut et version 3.</li><li>Un lecteur avec un buffer de taille fixe et une seule copie par image.</li><li>Une balise `<Video>` dans MGUI, qui affiche le buffer par `IUIImageResource`.</li><li>Des événements de fin et de saut, et une action de cutscene `PlayVideo`.</li><li>Des tests sur des images MDEC synthétiques, avec des sorties attendues exactes.</li><li>Une comparaison locale avec une référence de lecture, hors dépôts, sur les mêmes flux.</li><li>Le décodage mesuré en images par seconde.</li><li>Un sample avec un clip synthétique, et aucune allocation par image dans la boucle de lecture.</li></ul> |
| D2 | **Sans son.** Le conteneur garde une section audio facultative, un fichier WAV, que le lecteur de ce chantier ne joue pas. Le lecteur se cale sur la cadence. La synchronisation sur le son viendra avec le premier film qui en a un |
| D3 | **Sortie toujours en 24 bits** : RGBA 8 bits par composante, alpha 255 |
| D4 | **La balise lit la vidéo, par le moteur.** Comme une image animée, `<Video SourceName="…"/>` demande au moteur une source vidéo, qui décode dans une texture de taille fixe. La balise fait avancer la vidéo, l'affiche, et lève la fin et le saut. MGUI ignore tout du MDEC. `PlayVideo` ouvre un écran qui contient la vidéo, attend sa fin ou son saut, puis ferme l'écran |
| D5 | **Saut.** Une vidéo déclarée « skippable » se saute par l'action Submit ou Cancel du mapping UI du projet (ADR-0080), et lève alors son événement de saut |
| D6 | **Format et API additifs.** Un projet ou un écran existant se charge et se comporte à l'identique. ADR-0089 du moteur et ADR-0026 de MGUI enregistrent les décisions |
| D7 | Rien dans ce plan, les ADR, le code, les tests, le sample ni les commits ne nomme un projet consommateur, un jeu ou une décompilation. Les données sont synthétiques. Les noms du matériel (PSX, MDEC) ont un précédent dans le moteur (ADR-0058) |
| D8 | Les changements restent simples et optimisés, comme dans un moteur moderne : pas d'allocation par image, et le décodage se fait sur le thread du jeu tant que la mesure le permet |

**Proposés par ce plan, verrouillés par son approbation.**

| Réf | Choix |
|---|---|
| C1 | **Conteneur `.mdecvideo`, binaire petit-boutiste.** <ul><li>Magic `MDVC` (4 octets), version de format `u16` = 1, indicateurs `u16` = 0 (réservés, refusés s'ils ne sont pas nuls).</li><li>Largeur et hauteur `u16` affichées, de 1 à 4096 chacune.</li><li>Nombre d'images `u32` (au moins 1). Cadence en images par seconde, numérateur et dénominateur `u32`, tous deux non nuls.</li><li>Offset et longueur `u32` de la section audio, (0, 0) quand il n'y en a pas.</li><li>Table de nombre d'images + 1 offsets `u32`, comptés depuis le début du fichier, strictement croissants. L'image i va de l'offset i à l'offset i+1. Le dernier offset ne dépasse ni la section audio ni la fin du fichier.</li><li>Chaque image est une image BS telle que la console la lit : en-tête de 8 octets (`u16` compte, `u16` 0x3800, `u16` échelle de quantification, `u16` version 1, 2 ou 3), puis le flux de bits.</li><li>Le chargement valide tout cela avec un message qui nomme le fichier et le champ.</li></ul> |
| C2 | **Décodeur, sur le CPU, sans état global.** Le flux BS donne des mots RLE, puis la déquantification, puis l'IDCT entière, puis la couleur, puis l'écriture en RGBA avec recadrage. <ul><li>La spécification exacte est écrite par T1.1 d'après psx-spx avant tout code : versions 1, 2 et 3, DC brut et DC de la version 3, table AC, échappements, échelle 0, saturation, ordre des blocs et des macroblocs.</li><li>Là où psx-spx dit que le matériel n'est pas reproduit au bit près, le moteur fixe sa propre formule entière, documentée. Les tests prouvent cette formule.</li><li>Une image dont le flux est invalide lève une exception qui nomme l'image ; le lecteur la traite comme une fin d'erreur (C3).</li></ul> |
| C3 | **Lecteur `MdecVideoPlayer`, du moteur, qui implémente l'`IUIVideo` de MGUI (C4).** <ul><li>Il possède une `Texture2D` de la taille affichée (`SurfaceFormat.Color`) et la libère avec lui.</li><li>Il lit les images par un flux de fichier, dans un tampon réutilisé à la taille de la plus grande image.</li><li>Il décode sur le thread du jeu, dans un tampon RGBA réutilisé, puis fait un seul `SetData` par nouvelle image.</li><li>Horloge en ticks entiers du temps passé à `Advance` : l'image k s'affiche tant que le temps écoulé est dans [k, k+1) périodes. Une image en retard n'est pas décodée, seule la dernière due l'est. L'image 0 est décodée au démarrage.</li><li>La fin est atteinte quand le temps couvre toutes les images ; la dernière reste affichée.</li><li>Une erreur de flux termine la vidéo, avec une erreur loguée une fois.</li><li>Aucune allocation après le démarrage.</li></ul> |
| C4 | **MGUI.** <ul><li>`IUIVideo : IDisposable` dans `MGUI.Shared.Assets` : `Advance(TimeSpan)`, `Restart()`, `CurrentImage`, `CurrentSourceRect` et `IsEnded`.</li><li>`IUIAssetProvider.TryCreateVideo(string, out IUIVideo)`, à implémentation par défaut qui ne crée rien.</li><li>Un élément `MGVideo` et une balise XAML `Video` : `SourceName`, `Stretch`, `IsSkippable`, `EndedCommandName`, `SkippedCommandName`, plus une valeur `MGElementType.Video`.</li><li>`MGVideo` réutilise le dessin de `MGImage` (étirement, filtrage) par héritage ou par une méthode protégée ajoutée à `MGImage`, sans dupliquer le dessin.</li><li>À chaque `UpdateSelf`, il fait avancer la vidéo. À la première fin, il lève l'événement C# `Ended` et lance la commande `EndedCommandName` par `GetResources().TryGetCommand`, comme `Button.CommandName`. `Skip()` lève `Skipped` et lance `SkippedCommandName`.</li><li>Une vidéo repliée n'avance pas, comme une image animée.</li><li>Aucune allocation par image, et pas de relayout tant que la taille ne change pas.</li></ul> |
| C5 | **Fournisseur du moteur.** `CasaUIAssetProvider.TryCreateVideo` trouve par nom ou par id un asset catalogué de type `mdecvideo`, et crée un lecteur neuf pour chaque élément. Un nom inconnu logue un avertissement une fois, comme pour les images |
| C6 | **Écrans de données et saut.** <ul><li>Une `Video` qui nomme une commande non déclarée est refusée au chargement, comme un bouton.</li><li>Le saut se fait dans le moteur, et MGUI ne lit aucune entrée pour une `Video`. `UINavigationInput` gagne un crochet appelé pour chaque appui de Submit ou de Cancel (phase `Pressed`, pas les répétitions), avant que le bureau ne voie l'action.</li><li>`UIRoot` répond au crochet en demandant à l'écran du sommet, s'il est un `DataDrivenScreen`, de sauter ses vidéos « skippable » en cours. L'écran connaît ses `MGVideo` depuis le chargement.</li><li>Si au moins une vidéo est sautée, l'action est consommée : le bouton qui a le focus n'est pas cliqué, et la `cancel_command` n'est pas lancée. Sinon, rien ne change : Submit va au bouton, et un Cancel non traité va à la `cancel_command`.</li><li>Seul l'écran du sommet est concerné. Sans mapping, aucune vidéo ne se saute par une touche.</li><li>ADR-0089 enregistre ce changement du chemin d'ADR-0080.</li></ul> |
| C7 | **`PlayVideo`** : `{"type": "PlayVideo", "target": "<id ou nom d'écran>"}`. <ul><li>L'action ouvre l'écran comme `OpenScreen`, avec les mêmes échecs et la même règle sous gestion externe des vues.</li><li>Elle attend que chaque `MGVideo` de l'écran soit finie ou sautée, puis retire l'écran. Un écran sans `Video` fait échouer la lecture.</li><li>Si la cutscene est arrêtée avant, l'écran est retiré par le nettoyage de l'instruction, sauf quand le monde est effacé, puisque ses vues partent avec lui.</li></ul> |
| C8 | **Clip synthétique.** Un encodeur minimal de test, écrit à neuf, produit des images BS de motifs connus, DC et quelques coefficients AC. Il génère le clip du sample, et un test vérifie que le fichier commité est identique à la sortie de l'encodeur |
| C9 | **Mesures.** <ul><li>Le décodage d'une image synthétique de 320x224 est mesuré en images par seconde, en Release, et la valeur est écrite dans la doc.</li><li>Si elle tombe sous 4 fois la cadence d'une vidéo à 30 images par seconde, la tâche s'arrête en ⚠️ et la question du thread de décodage remonte à l'auteur.</li></ul> |
| C10 | **Branche MGUI** `chantier/video-element`, depuis `a3c42cb0` (`origin/develop`). Elle est committée dans le sous-module, puis le pointeur MGUI du moteur avance. La fusion dans `develop` de MGUI suit la recette, avec celle du moteur |

## Règles d'exécution pour l'agent

- **Branches dédiées.**
  - `chantier/video-playback` dans le moteur, créée depuis `main`.
  - `chantier/video-element` dans MGUI, créée depuis `a3c42cb0`.
  - Ne jamais committer sur `main` ni sur `develop`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin :
  1. lancer la validation indiquée ;
  2. remplacer l'icône par `✅`, `🧪` ou `⚠️` ;
  3. ajouter une courte note de validation sous la tâche ;
  4. **créer un commit dédié** qui inclut la mise à jour de ce fichier.

  Les tâches MGUI se committent dans MGUI. Ce plan est mis à jour par le commit moteur suivant.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` ou `develop` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Interdiction de copier** le code d'une autre base de code, même lisible. Seules les spécifications publiques (psx-spx, MPEG-1) servent de source.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` à 0 erreur.
- **Tests** : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, jamais avec `--no-build`. Pour MGUI : `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj`.
- Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur :
  - pas d'allocation, de LINQ ni de closure dans les chemins chauds : `Advance`, le décodage, la mise à jour de la texture et `UpdateSelf` ;
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
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` et `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` : tous verts, dont les nouveaux tests.
- **Non-régression** : les tests existants restent verts sans être modifiés. Les samples existants ne sont pas touchés.
- **Smoke** : `dotnet run --project CasaEngine.Launcher -- Projects/VideoSample/VideoSample.json`.

---

## Phase 0 — Plan et ADR

### ✅ T0.1 — Plan et ADR-0089

- Objectif : committer ce plan et sa ligne d'index. Écrire ADR-0089, qui reprend D1 à D8 et C1 à C10, sauf ce que T1.1 fixe (renvoi vers la spécification).
- Fichiers :
  - `ai-agent/tasks/video-playback-tasks.md` ;
  - `ai-agent/README.md` ;
  - `docs/decisions/0089-the-engine-plays-mdec-videos-through-a-video-element.md` ;
  - `docs/decisions/README.md`.
- Validation : relecture de l'ADR contre ce plan. Contrôle D7 par une recherche des noms interdits, liste tenue hors du dépôt.
- Commit : `docs(video): plan MDEC video playback and record ADR-0089`
- Note : plan relu par un plan-verifier neuf (REVISE sur le chemin du saut, corrigé ; READY à la relecture de clôture), approuvé par l'auteur le 2026-11-08 (« Oui, lance-le »). ADR-0089 relue contre D1 à D8 et C1 à C10. Contrôle D7 fait.

---

## Phase 1 — Décodeur et conteneur, sans GPU

### ✅ T1.1 — Spécification du format MDEC

- Objectif : écrire, avant tout code, la spécification exacte que le décodeur suit, en nos propres mots, d'après psx-spx.
- Fichiers : `docs/engine/video.md` (nouveau), section « MDEC bitstream ».
- Étapes :
  1. Lire en entier les sections « BS Compression Headers », « BS Compression DC Values » et « BS Compression AC Values » de https://psx-spx.consoledev.net/cdromfileformats/, et la page https://psx-spx.consoledev.net/macroblockdecodermdec/.
  2. Écrire :
     - l'en-tête ;
     - l'ordre des bits ;
     - le DC des versions 1 et 2, et celui de la version 3 (tables, prédicteurs, repliement) ;
     - la table AC complète, l'échappement de chaque version et la fin de bloc ;
     - la fin d'image ;
     - la déquantification (table de quantification par défaut, échelle 0, saturation) ;
     - l'ordre en zigzag ;
     - l'IDCT entière (table d'échelle, arrondi) ;
     - la conversion de couleur en virgule fixe, avec son arrondi et son bornage ;
     - l'ordre des blocs et des macroblocs ;
     - le recadrage.
  3. Citer la section source de chaque règle. Là où psx-spx est incomplet ou laisse un choix, écrire le choix du moteur et marquer « engine choice ».
- Validation : relecture contre psx-spx. Un point que psx-spx contredit, ou laisse ouvert alors qu'il change la sortie, passe en ⚠️ avec la question dans « Points ouverts ».
- Commit : `docs(video): specify the MDEC bitstream the engine decodes`
- Note : `docs/engine/video.md` §1 écrite d'après les pages psx-spx (MDEC, BS Headers, DC, AC, Versions), chaque règle citée. Table AC contrôlée (préfixes libres, espace de codes plein sauf le préfixe inutilisé). Choix moteur marqués : matrice de quantification par défaut, ordre des macroblocs par colonnes, signe du DC v3, arrondis IDCT et couleur, recadrage (voir O2 à O4). Relecture : la matrice par défaut de psx-spx (BS fraquant) donne qt[0] = 2, corrigé (le texte disait 8 et « aucune matrice »). Disposition après revue : psx-spx laisse ouverts deux points qui changent l'image (entrées 4 à 62 de la matrice de quantification, ordre des macroblocs) ; la règle de la Validation impose ⚠️, la tâche est bloquée et O2 et O3 sont des questions à l'auteur ; T1.2 et T1.3 ne démarrent pas avant sa réponse. O4 reste un choix moteur couvert par C2 (arrondis) et par les plages de psx-spx (signe du DC v3). Si l'auteur accepte les deux choix, noter la réponse ici et repasser en ✅. Doc seule : pas de build ni de test touché.
- Note : bloqué sur O2 et O3, puis débloqué par l'auteur le 2026-11-08 : matrice MPEG-1 avec 2 en tête, une seule table pour les six blocs (« MPEG-1, 2 en tête ») ; macroblocs colonne par colonne (« Colonne par colonne »). `docs/engine/video.md` les note comme décisions de l'auteur, et intègre les précisions des relectures : le mot `FE00` testé avant la règle du dépassement de 63, la règle de bourrage limitée à une entrée RLE brute, le recadrage cité d'après la section « STR Resolution » de psx-spx, la borne de l'IDCT (4018).

### ✅ T1.2 — Lecture du flux BS

- Objectif : du flux de bits aux mots RLE, pour les versions 1, 2 et 3, selon T1.1.
- Fichiers :
  - `CasaEngine/Framework/Video/Mdec/` (nouveau dossier, selon ADR-0029) ;
  - `CasaEngine.Tests/Video/` ;
  - l'encodeur de test de C8 (`CasaEngine.Tests/Video/MdecTestEncoder.cs`).
- Validation :
  - tests sur des flux produits par l'encodeur de test, avec les mots RLE attendus exactement, pour chaque version : DC seul, AC courts et longs, échappement, fin de bloc, fin d'image ;
  - flux tronqué et code invalide : exception qui nomme l'image ;
  - aucune allocation par image (`AllocationWindow`) ;
  - build des deux `.sln` et `CasaEngine.Tests`.
- Commit : `feat(video): decode MDEC bitstreams into run-length words`
- Note : `MdecBitstreamDecoder` (statique, sans état, sans allocation) et `MdecBitReader` dans `CasaEngine/Framework/Video/Mdec/`, selon `docs/engine/video.md` 1.2 à 1.6 : un mot DC, les mots RLE tels que codés, un `FE00` par bloc. Encodeur de test de C8 écrit à neuf (`MdecTestEncoder`). 45 tests (compte corrigé par T4.3 ; la note disait 92) : flux assemblés à la main, toutes les lignes de la table AC aux deux signes, échappements (niveau 0, `FE00`), DC v3 de -255 à 255 et repli sur 10 bits, fin d'image optionnelle, flux tronqué à chaque longueur, codes invalides, `AllocationWindow` à 0 octet. Build des deux `.sln` à 0 erreur, `CasaEngine.Tests` 5123 réussis.

### ✅ T1.3 — Blocs, IDCT et couleur

- Objectif : des mots RLE à l'image RGBA recadrée, selon T1.1.
- Fichiers : `CasaEngine/Framework/Video/Mdec/` et `CasaEngine.Tests/Video/`.
- Validation :
  - sorties exactes attendues :
    - blocs à DC seul (couleur uniforme calculée par la formule de T1.1) ;
    - un coefficient AC isolé par position ;
    - un macrobloc complet ;
    - une échelle à 0 ;
    - la saturation ;
    - une taille qui n'est pas un multiple de 16, recadrée ;
  - les valeurs attendues viennent de la formule de T1.1, écrites dans le test sous forme de cas calculés à la main ou par une implémentation de référence en `double`, avec la règle d'arrondi de T1.1 ;
  - aucune allocation par image ;
  - build des deux `.sln` et `CasaEngine.Tests`.
- Commit : `feat(video): turn MDEC blocks into RGBA frames`
- Note : `MdecFrameDecoder` (statique, sans état, blocs sur la pile, sans allocation) dans `CasaEngine/Framework/Video/Mdec/`, selon `docs/engine/video.md` 1.7 à 1.11 : déquantification (matrice par défaut, échelle 0 sans zig-zag, saturation), IDCT entière à deux passes, couleur en virgule fixe, macroblocs colonne par colonne, recadrage en haut à gauche. 31 tests : DC seul calculé à la main, un coefficient AC par position (3 échelles), macrobloc complet et trames aléatoires contre une référence écrite dans le test, IDCT contre une IDCT en `double` (tolérance 2,5), échelle 0, saturation, tailles non multiples de 16, erreurs nommant l'image, `AllocationWindow` à 0 octet, chaîne complète avec `MdecBitstreamDecoder` pour les versions 1 à 3.

### ✅ T1.4 — Conteneur `.mdecvideo`

- Objectif : le type d'asset de C1, chargé par son en-tête et sa table seulement.
- Fichiers :
  - `CasaEngine/Framework/Configuration/Constants.cs` ;
  - `CasaEngine/Framework/Video/MdecVideoAsset.cs` et `MdecVideoAssetLoader.cs` ;
  - `CasaEngine/Framework/Assets/AssetLoaderRegistry.cs` ;
  - `CasaEngine.Tests/Video/` (écrivain de conteneur de test, à côté de l'encodeur).
- Validation :
  - aller-retour avec l'écrivain de test ;
  - une erreur qui nomme le champ pour chaque cas : magic, version, indicateurs, taille nulle, cadence nulle, table non croissante, image qui déborde, section audio hors du fichier ;
  - la section audio est lue comme des bornes, pas comme un son ;
  - build des deux `.sln` et `CasaEngine.Tests`.
- Commit : `feat(video): load MDEC video containers`
- Note : `MdecVideoAsset` (en-tête de 32 octets et table, lecture à la demande des images ensuite) et `MdecVideoAssetLoader` dans `CasaEngine/Framework/Video/`, constante `.mdecvideo`, une ligne dans `AssetLoaderRegistry` ; `asset_type` `mdecvideo` déduit de l'extension par `InferAssetType`. Erreurs `InvalidDataException` qui nomment le fichier et le champ. Choix local : l'offset 0 doit suivre l'en-tête et la table, et une section audio (offset, 0) est refusée. Écrivain de conteneur de test `MdecTestContainerWriter`. 28 tests (aller-retour avec et sans audio, un cas par champ invalide, chargeur sur disque, registre). Build des deux `.sln` à 0 erreur, `CasaEngine.Tests` 5182 réussis.

---

## Phase 2 — MGUI

### ✅ T2.1 — Élément `Video` de MGUI (dans le sous-module)

- Objectif : C4, sur la branche `chantier/video-element` de MGUI.
- Fichiers, sous `MGUI/` :
  - `MGUI.Shared/Assets/IUIVideo.cs` (nouveau) et `IUIAssetProvider.cs` ;
  - `MGUI.Core/UI/MGVideo.cs` (nouveau), `MGImage.cs` (si une méthode protégée est nécessaire), `Enums.cs` et `XAML/Controls.cs` ;
  - `docs/decisions/0026-a-video-element-plays-host-video-sources.md` et l'index ;
  - `MGUI.Tests/`.
- Validation :
  - tests avec une fausse `IUIVideo` :
    - avance par `UpdateSelf` ;
    - `Ended` levé une seule fois, avec sa commande ;
    - `Skip` lève `Skipped` et sa commande, seulement si la vidéo est « skippable » ;
    - une vidéo repliée n'avance pas ;
    - la source est libérée au changement de source et à la libération de l'élément ;
    - un `UpdateSelf` n'alloue rien (`AllocationWindow`) ;
    - la balise XAML est lue avec ses attributs ;
  - les tests d'inventaire par réflexion (`ThemeValueInvalidationInventoryTests`, `FillBrushLifecycleTests`) restent verts ;
  - `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj` vert, avec la build de MGUI.
- Commit (dans MGUI) : `feat(video): add a Video element fed by host video sources`
- Note : `IUIVideo`, `IUIAssetProvider.TryCreateVideo` (défaut), `MGVideo : MGImage` (hooks additifs dans `MGImage` : constructeur protégé, `UpdateActualSource` virtuelle, `ResolvesTextureNames`, `SetActualSource`), `MGElementType.Video` après `Image`, balise `<Video>`, ADR-0026 et son index. Choix locaux : `Skip()` renvoie un booléen, arrête la vidéo et ne lève jamais `Ended` ; `Restart()` ré-arme les deux événements ; `MGVideo` est `IDisposable` (MGElement ne l'est pas). Un test d'inventaire existant (`AssetProviderTests`, liste des membres de `IUIAssetProvider`) a dû recevoir `TryCreateVideo` dans sa liste attendue. Branche MGUI `chantier/video-element`. 15 tests `VideoElementTests` ; `MGUI.Tests` 3241 réussis, `MGUI.sln` à 0 erreur. Ce plan est commité par T2.2. Revue : alias `Video` ajouté à `ElementNameAliases` (chemin assaini), test sans xmlns ajouté ; 3242 tests MGUI verts.

### ✅ T2.2 — Pointeur MGUI du moteur

- Objectif : le moteur pointe sur le commit de T2.1 et compile avec.
- Fichiers : `MGUI` (pointeur) et ce plan.
- Validation : build des deux `.sln`, `CasaEngine.Tests` vert sans changement de code moteur.
- Commit : `chore(mgui): point MGUI at the video element`
- Note : pointeur MGUI sur `77db5373` (T2.1 plus sa revue), aucun code moteur modifié ; `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` à 0 erreur ; `CasaEngine.Tests` 5182 réussis, 0 échec.

---

## Phase 3 — Intégration dans le moteur

### ✅ T3.1 — Lecteur et fournisseur

- Objectif : C3 et C5.
- Fichiers :
  - `CasaEngine/Framework/Video/MdecVideoPlayer.cs` ;
  - `CasaEngine/Framework/UI/Backend/MonoGame/Assets/CasaUIAssetProvider.cs` ;
  - `CasaEngine.Tests/Video/`.
- Validation :
  - horloge : avec une cadence de 15/1 et des pas de 166 667 ticks, l'image k s'affiche de la mise à jour 4k à 4k+3 ; la fin arrive après la dernière période ; une image en retard n'est pas décodée ;
  - test GPU : la texture relue après une image vaut exactement le RGBA du décodeur ;
  - aucune allocation par `Advance`, décodage et envoi compris, hors du premier ;
  - un nom inconnu logue une fois ;
  - le lecteur libère sa texture ;
  - build des deux `.sln` et `CasaEngine.Tests`.
- Commit : `feat(video): play MDEC videos into a fixed texture`
- Note : `MdecVideoPlayer` (`IUIVideo`, texture `Color` unique enveloppée une fois, `FileStream` sans tampon, tampons trame/mots/RGBA réutilisés, horloge en ticks entiers avec `Int128`, cadence rationnelle sans arrondi de la période, fin = `ceil` des ticks totaux, trame en retard non décodée, erreur de flux = fin + log une fois), `CasaUIAssetProvider.TryCreateVideo` (nom ou id, conteneur retenu une fois par nom, joueurs suivis et libérés avec le fournisseur, `WarnOnce`). Choix locaux : `CurrentFrameIndex` et `DecodedFrameCount` publics en lecture seule ; rappel optionnel à la libération. 12 tests `MdecVideoPlayerTests` (GPU réel, aucun ignoré) ; les deux `.sln` à 0 erreur ; `CasaEngine.Tests` 5194 réussis, 0 échec.

### ✅ T3.2 — Écrans de données et saut

- Objectif : C6.
- Fichiers :
  - `CasaEngine/Framework/UI/UINavigationInput.cs` (le crochet) ;
  - `CasaEngine/Framework/UI/UIRoot.cs` (le branchement à l'écran du sommet, par une méthode statique interne que les tests appellent aussi) ;
  - `CasaEngine/Framework/UI/DataDrivenScreen.cs` (les `MGVideo` de l'écran, le saut, la validation des noms de commande) ;
  - `CasaEngine.Tests/UI/`.
- Validation :
  - une `Video` qui nomme une commande non déclarée est refusée au chargement, avec un message qui la nomme ;
  - dans un test qui passe par `UINavigationInput.Update` (surcharge à `isHeld` injecté) et `UIRoot.RunFrame` :
    - un appui de Submit, puis un appui de Cancel, sautent une vidéo « skippable » en cours de l'écran du sommet et lèvent `Skipped` ;
    - une vidéo qui n'est pas « skippable » n'est pas sautée ;
    - une vidéo d'un écran qui n'est pas au sommet n'est pas sautée ;
    - quand le saut consomme l'action, le bouton qui a le focus n'est pas cliqué, et la `cancel_command` n'est pas lancée ;
    - sans vidéo à sauter, Submit clique le bouton, et Cancel lance la `cancel_command`, comme avant ;
    - une répétition de touche ne saute rien ;
  - les tests existants d'ADR-0080 restent verts sans être modifiés ;
  - build des deux `.sln` et `CasaEngine.Tests`.
- Commit : `feat(ui): wire video commands and skips in data-driven screens`
- Note : crochet `UINavigationInput.SubmitOrCancelPressed` (`Func<bool>`, Submit et Cancel en phase `Pressed` seulement, avant le bureau ; vrai = action consommée, pas de `CancelNotHandled`), `UIRoot.SkipVideosOfTopScreen(ScreenStack)` interne et statique, `DataDrivenScreen.SkipVideos()` et refus au chargement d'une `Video` dont `EndedCommandName` ou `SkippedCommandName` n'est pas déclarée. 12 tests `DataDrivenScreenVideoTests` (via `UIRoot.RunFrame` et la surcharge à `isHeld` injecté) ; les deux `.sln` à 0 erreur ; `CasaEngine.Tests` 5206 réussis, 0 échec.

### ✅ T3.3 — Action `PlayVideo`

- Objectif : C7, aux six endroits d'une action.
- Fichiers :
  - `CasaEngine/Framework/Cutscenes/` (classe de données, types, sérialiseur, validateur, fabrique) ;
  - `CasaEngine.EditorServices/Cutscenes/CutsceneReadOnlyDocumentBuilder.cs` ;
  - `CasaEngine/Framework/Application/GameManager.cs` si une méthode interne est nécessaire ;
  - `CasaEngine.Tests/Cutscenes/`.
- Validation :
  - aller-retour JSON ;
  - erreur du validateur pour une `target` absente ;
  - l'action attend la fin de chaque vidéo, puis retire l'écran ;
  - un saut termine l'attente ;
  - un écran sans `Video` fait échouer la lecture ;
  - sous gestion externe des vues, rien ne s'ouvre et l'action se termine ;
  - un arrêt de la cutscene retire l'écran ;
  - le builder montre le champ ;
  - build des deux `.sln` et `CasaEngine.Tests`.
- Commit : `feat(cutscenes): play a video screen from a cutscene`
- Note : validé le 2026-11-08 : build des deux .sln à 0 erreur, CasaEngine.Tests 5220 réussis (13 tests PlayVideo + 1 builder ; compte corrigé par T4.3, la note disait 14 tests PlayVideo). Une vidéo dont la source ne se résout pas ne retient pas l’attente (IsPlaying faux) ; l’arrêt retire l’écran au prochain tick du gestionnaire de coroutines, qui libère l’énumérateur.

---

## Phase 4 — Sample, mesures et documentation

### 🧪 T4.1 — Sample `Projects/VideoSample`

- Objectif : D1, sans code C# de jeu.
- Contenu :
  - un projet sans DLL, avec un mapping UI pour le saut ;
  - un monde avec une cutscene de démarrage qui joue `PlayVideo` sur un écran contenant une `<Video IsSkippable="True">` du clip synthétique de C8, puis boucle ;
  - un README.
- Fichiers : `Projects/VideoSample/` et un test de données sur le modèle de `TimedScreenSampleDataTests`, qui vérifie aussi que le clip commité égale la sortie de l'encodeur.
- Validation :
  - le test de données passe ;
  - lancement au Launcher, avec des captures prises par un outil hors dépôt : images du clip qui changent à la cadence, saut par Submit, fin puis boucle ;
  - build des deux `.sln` et `CasaEngine.Tests`.
- Commit : `feat(samples): add a sample that plays a synthetic MDEC clip`
- Note : sample `Projects/VideoSample` écrit (projet sans DLL avec mapping `ui-input`, monde `VideoWorld` dont la cutscene de démarrage joue `PlayVideo` sur `ClipScreen` puis recharge le monde, `<Video IsSkippable="True">`, README) et clip synthétique de 160x112, 60 images à 15 par seconde, généré par `VideoSampleClip` (encodeur de C8) et commité. `VideoSampleDataTests` : 8 tests, dont clip commité identique octet pour octet à la sortie de l’encodeur. Build des deux .sln à 0 erreur, `CasaEngine.Tests` 5228 réussis. Launcher non lancé par cette tâche : lancement, captures, saut par Submit, fin et boucle (rechargement du monde par `ChangeWorld` sur lui-même) restent à vérifier en session principale, d’où 🧪.

### ✅ T4.2 — Mesures et documentation

- Objectif : C9, et la documentation.
- Fichiers :
  - `docs/engine/video.md` : conteneur, lecteur, balise, action, limites et mesure ;
  - `docs/engine/data-driven-screens.md` : section `<Video>` et `PlayVideo` ;
  - l'index `docs/README.md`.
- Validation :
  - la mesure en images par seconde est écrite, avec la machine et la configuration ;
  - si elle tombe sous le seuil de C9, ⚠️ ;
  - relecture contre le code livré.
- Commit : `docs(video): describe MDEC video playback`
- Note : décodage mesuré en Release (flux binaire puis image, sans lecture de fichier ni `SetData`), image synthétique de 320x224, AMD Ryzen 9 9900X, Windows 11, `CasaEngine.dll` en Release `net9.0-windows` lancé sur le runtime .NET 10.0.14, un thread : 816 à 1673 images par seconde selon la densité AC (0 à 20 codes AC par bloc), soit au moins 6,8 fois le seuil de C9 (120 images par seconde) ; chiffres, machine et méthode dans `docs/engine/video.md` §8. `video.md` complété (conteneur, lecteur, balise, saut, action, limites, mesure), section `<Video>` et `PlayVideo` ajoutée à `data-driven-screens.md`, `docs/README.md` mis à jour ; relecture contre le code livré. Build des deux .sln à 0 erreur, `CasaEngine.Tests` 5228 réussis.

### ✅ T4.3 — Corrections des relectures

- Objectif : traiter les constats des relectures de la branche.
- Validation : build des deux `.sln` à 0 erreur ; `CasaEngine.Tests` et `MGUI.Tests` verts.
- Commit : `fix(video): address the review findings of the chantier` (moteur) ; `fix(video): keep a Video's source from being replaced` (MGUI, branche `chantier/video-element`).
- Note : issues des constats.
- Note : complément en session principale après la relecture de T4.3 (aucun P0 à P2) : `DataDrivenScreen` collecte ses vidéos avant même le contrôle des erreurs de format de l'enveloppe, avec un test qui échoue sur l'ancien ordre ; la limite « sans surface visible » est précisée dans la doc (taille nulle ou élément entièrement coupé) ; ADR-0026 de MGUI liste `UsesSource` (MGUI `a3d6c87d`). Trois tests existants de `MdecVideoPlayerTests` ont dû changer pour le comportement voulu par F3 (un lecteur libéré garde `IsEnded` et ne rend plus d'image) : écart à la règle de non-modification, à confirmer par l'auteur. P4 reportés : voir la note précédente, plus une `Video` finie qui dessine encore la texture d'un lecteur libéré par le fournisseur, la troncature du DC de la version 3, et la fragilité du test de fuite qui lit un champ privé de MonoGame.
- Note : Launcher en session principale (branche à `902180de`, fenêtre 960x540, outil de capture hors dépôt). Le clip s'affiche et change à chaque capture : 89 changements sur 90 captures, 60 images distinctes, soit les 60 images du clip rejouées. La boucle par `ChangeWorld` sur le même monde fonctionne : changements de monde à 22:17:25, :30 et :34, et aucune erreur ni aucun avertissement dans le log. Le saut par Submit n'est pas vérifié au Launcher : une touche `Z` synthétique, envoyée par `keybd_event` après avoir demandé le premier plan, n'a pas sauté le clip, et sa livraison à la fenêtre n'est pas prouvée. Le chemin est prouvé par les tests de T3.2. Saut au clavier à vérifier à la recette (T5.2).
  - F1 (P2) corrigé : `DataDrivenScreen` collecte ses `MGVideo` en premier au chargement (un échec plus loin laisse donc `Dispose` les libérer) et les dispose avec lui (`Hide` puis `Dispose`) ; chaque lecteur libère sa texture et son flux. Tests : `HidingTheScreen_DisposesItsVideos`, deux échecs de chargement, `WhenTheActionEnds_TheScreenIsHidden_AndItsVideosAreDisposed`.
  - F2 (P3) corrigé : `Hide` marque l'écran comme sorti de sa pile, `IsAnyVideoPlaying` devient faux et l'attente de `PlayVideo` se termine, sans nouveau membre sur `IUIViewRuntime`. Test : `WhenTheScreenLeavesTheStackByAnotherPath_TheWaitEnds`.
  - F3 (P3) corrigé : le constructeur de `MdecVideoPlayer` libère la texture et le flux avant de relancer ; un lecteur disposé garde `IsEnded`, `Advance` et `Restart` n'ont plus d'effet et `CurrentImage` vaut null. Trois assertions de tests existants (`Dispose_ReleasesTheTextureAndTheFile_Notifies_AndIsIdempotent`, `ProviderDispose_DisposesThePlayersThatWereNotDisposed`, `TryCreateVideo_ByNameAndById_...`) figeaient l'ancien comportement (`IsEnded` vrai ou `CurrentImage` lu après `Dispose`) : elles ont été adaptées, ce qui déroge à la règle « ne pas modifier un test existant ». Tests ajoutés : lecteur disposé (en cours et terminé), texture libérée quand le fichier ne s'ouvre pas (le test échoue si on retire le `Dispose` de la texture).
  - F4 (P3) corrigé : un mot après un mot de niveau 0 qui a dépassé la position 63 est une erreur qui nomme l'image (seul le mot de fin de bloc peut suivre, `video.md` 1.7 étape 3). Test ajouté.
  - F5 (P3) corrigé : une lecture AC coupée (moins de 12 bits réels) signale la troncature et non « invalid AC code ». Tests : trame coupée juste après un DC, DC v3 de classe 7 aux deux signes et aux deux tables, avec les mots exacts.
  - F6 (P3, MGUI) corrigé : `MGVideo` ignore `MGImage.Source` comme le dit l'ADR-0026 (membre protégé virtuel `UsesSource` ajouté à `MGImage`, additif). Test dans `MGUI.Tests` ; pointeur MGUI du moteur mis à jour.
  - F7 (P3) corrigé : le README de `Projects/VideoSample` ne dit plus que la souris saute le clip (`ui-input.buttonsMapping` ne lie que clavier et manette).
  - F8 (P3) corrigé : titre `## Video element and PlayVideo` (ancre `#video-element-and-playvideo`), liens corrigés dans `data-driven-screens.md`, `video.md` et `docs/README.md`. `video.md` et `data-driven-screens.md` disent que les vidéos d'un écran sont disposées avec lui, et donnent les limites vues dans le code : un `Video` replié ou de taille nulle et non sautable n'avance jamais et `PlayVideo` attend sans fin ; aucun saut pendant que la navigation est suspendue.
  - F9 (P3, disposition seule) conservé : le test d'inventaire `AssetProviderTests` fige la liste des membres de `IUIAssetProvider` ; un membre ajouté (`TryCreateVideo`) doit y figurer. Aucune modification de plus.
  - F10 corrigé : comptes de T1.2 (45 tests, pas 92) et de T3.3 (13 tests `PlayVideo`, pas 14), relevés par `dotnet test --filter`.
  - Validation : `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` à 0 erreur ; `CasaEngine.Tests` 5239 réussis ; `MGUI.Tests` 3243 réussis.
  - P4 laissés en différé : type d'exception d'un tampon trop petit ; raisons supplémentaires dans les tests d'erreur ; bornes 2 GiB du conteneur ; hypothèses de lecture du flux ; tick de fin aux cadences extrêmes ; décalage des valeurs de `MGElementType` ; `MGVideo` retiré d'un arbre hors écrans de données non disposé par MGUI ; profondeur du test de correspondance ; chemin de régénération du clip.

### ✅ T4.4 — Correction du vérificateur

- Objectif : libérer les vidéos créées par un écran dont le XAML échoue au chargement.
- Validation : build des deux `.sln` à 0 erreur ; `CasaEngine.Tests` vert.
- Commit : `fix(video): release videos created by a screen whose XAML fails to load`
- Note : constat P2 du vérificateur neuf : un `<Video>` crée son lecteur pendant la construction du XAML (le constructeur de `MGVideo` fixe `SourceName`). Si un élément suivant échoue (par exemple `<TabItem Header="x"/>` hors d'un `TabControl`), `OnWindowLoaded` n'est jamais atteint, `CollectVideos` ne tourne pas, et le lecteur (texture et fichier ouvert) restait dans le fournisseur jusqu'à sa libération. Correctif : interface interne `IUIVideoCreationScope` (`BeginVideoCreationScope(List<IUIVideo>)`, `EndVideoCreationScope()`), implémentée par `CasaUIAssetProvider` (les scopes imbriqués se restituent : la liste précédente est rétablie à la fin) ; `DataDrivenScreen.LoadWindow` ouvre un scope autour de `base.LoadWindow` quand le fournisseur de `desktop.Resources.AssetProvider` l'implémente, libère les vidéos du scope si le chargement lève, puis relève l'exception, et ferme le scope dans `finally`. Un fournisseur sans l'interface garde l'ancien comportement. P4 traité en même temps : `TryCreateVideo` renvoie false après `Dispose` du fournisseur. Tests ajoutés : `WhenALaterElementFailsToLoad_TheVideosAlreadyCreatedAreDisposed` (échec de `BuildWindow`, vidéo libérée sans `screen.Dispose`) et `VideoCreationScope_RecordsTheVideosCreatedInside_NotTheOnesOutside_AndDisposedProviderCreatesNone`. Docs `video.md` et `data-driven-screens.md` mises à jour. Validation : `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` à 0 erreur ; `CasaEngine.Tests` 5242 réussis.
- Note : complément en session principale après la relecture de T4.4 (aucun P0 à P2 ; la relecture a prouvé que le nouveau test échoue sur l'ancien `DataDrivenScreen`) : `IUIVideoCreationScope` est interne et implémentée explicitement par `CasaUIAssetProvider`, donc aucune API publique n'est ajoutée ; un test prouve l'imbrication des portées et l'exception d'une fin sans portée ouverte ; la doc dit que la libération au chargement raté demande le `CasaUIAssetProvider` du moteur, et qu'un fournisseur libéré ne crée plus rien, sans avertissement. Deux `.sln` à 0 erreur, `CasaEngine.Tests` 5243 sur 5243.

---

## Phase 5 — Vérification et recette

### ⏳ T5.1 — Vérificateur neuf

- Objectif : une passe de `verifier` neuf sur les deux branches : C1 à C10, D2 à D8, la non-régression, D7, les builds et les tests.
- Validation : verdict CONFIRMED. Chaque constat est traité en `FIX`, `DEFER` ou `REJECT` selon sa priorité, et noté ici.
- Commit : `docs(ai-agent): record the verification of MDEC video playback`

### ⏳ T5.2 — Recette de l'auteur

- Objectif : l'auteur lance le sample et l'accepte.
- Validation : accord de l'auteur, écrit ici. La tâche reste 🧪 jusque-là.
- Commit : `docs(ai-agent): record the accepted check of the video sample`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | La comparaison locale avec la référence de lecture, sur les mêmes flux, se fait hors des dépôts, par le projet consommateur, après la fusion. Ses résultats ne vont pas dans ce dépôt (D7) | Après T5.2 |
| O2 | **Tranché par l'auteur le 2026-11-08 : « MPEG-1, 2 en tête », une seule table.** Question d'origine : psx-spx ne donne que 02h,10h,10h,13h,..,53h pour la matrice par défaut (BS fraquant) et aucune table de 64 entrées ; le conteneur C1 n'en porte pas. Le moteur utilise-t-il la matrice intra par défaut d'MPEG-1 avec l'entrée 0 à 2, pour les six blocs (psx-spx fraquant applique une seule table à la luma et à la chroma) ? Les entrées 4 à 62 viennent d'MPEG-1 par inférence, pas de psx-spx. Une vidéo encodée pour une autre matrice sera fausse | T1.1 (bloquant), T1.3 |
| O3 | **Tranché par l'auteur le 2026-11-08 : « Colonne par colonne ».** Question d'origine : psx-spx dit seulement que les macroblocs sont « généralement verticaux ». Le moteur lit-il les macroblocs colonne par colonne, de haut en bas, colonne de gauche d'abord ? C'est une inférence | T1.1 (bloquant), T1.2, T1.3 |
| O4 | Sens des bits de magnitude négative du DC v3 (`n = 0` = plus grande magnitude, choix moteur déduit des plages données) et arrondis de l'IDCT et de la couleur (psx-spx : matériel non reproduit au bit près) | T1.2, T1.3 |

## Hors périmètre

- La lecture et la synchronisation du son (D2) : la section audio est seulement réservée et validée.
- Les flux STR entrelacés, secteurs de CD et audio XA : le conteneur porte des images BS déjà séparées.
- Une sortie en 15 bits (D3).
- Un thread de décodage, sauf si la mesure de C9 le rend nécessaire, sur décision de l'auteur.
- Un éditeur de vidéo, et une ligne de l'inspecteur pour le nouvel asset.
- Les touches de sortie d'une cutscene (attente d'une touche parmi une liste) : chantier suivant.
