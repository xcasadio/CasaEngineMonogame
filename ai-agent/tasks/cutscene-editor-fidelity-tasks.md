# Plan agent IA — Éditeur de cutscenes : un aperçu plus fidèle (tranche G)

Septième tranche du programme « éditeur de cutscenes visuel » (ADR-0096 ; plans [cutscene-editor-preview-tasks.md](cutscene-editor-preview-tasks.md), [cutscene-editor-outliner-details-tasks.md](cutscene-editor-outliner-details-tasks.md), [cutscene-editor-timeline-tasks.md](cutscene-editor-timeline-tasks.md), [cutscene-editor-transport-tasks.md](cutscene-editor-transport-tasks.md), [cutscene-editor-structure-tasks.md](cutscene-editor-structure-tasks.md), [cutscene-editor-placement-tasks.md](cutscene-editor-placement-tasks.md)).
Elle rapproche l'aperçu de ce que montre le jeu sur trois points laissés ouverts par la tranche A : les **fondus d'écran sont dessinés**, une **entrée du joueur peut être simulée**, et le **son peut être écouté**.

Les décisions D1 → D5 ci-dessous ont été arbitrées avec l'auteur : **ce plan les applique, il ne les rediscute pas**.
L'auteur a demandé le 2026-11-18 « Oui, tranche G », dans le mode autonome des tranches précédentes : ce plan est approuvé d'avance. Après trois relectures refusées, la dernière pour un seul point sur le son, l'auteur a tranché le 2026-11-19 : « Non, tout ensemble » — les trois sujets restent dans cette tranche, le dernier point est corrigé (relevé des pas, ci-dessous) et le plan s'exécute. Les points P1 → P15 sont des arbitrages de l'agent, à confirmer par l'auteur à la recette.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Dans l'aperçu d'un document `.cutscene` :

- un `FadeScreen` ou un `FadeScreenFrames` **assombrit, éclaircit ou teinte réellement l'image** de l'aperçu, avec le même mélange que dans le jeu, à l'instant de la tête de lecture ;
- quand la lecture a passé un `ExitOnInput`, un bouton **simule l'appui** sur l'entrée nommée : l'aperçu dit vers quelle cutscene le jeu partirait et la lecture s'arrête là ;
- un bouton **Sound** fait entendre les sons et les musiques de la cutscene pendant une lecture ; il est éteint à l'ouverture.

## État vérifié du dépôt (2026-11-18)

Branche `chantier/cutscene-property-editing`, tête `8cd29fb8`, 89 commits depuis `main` `c365b514`, non fusionnée.
Fichiers non suivis préexistants, à ne jamais indexer : `Projects/PixelUiSample/.casaeditor/`, `Projects/SpriteCutsceneSample/.casaeditor/`. Un lancement de l'éditeur sur un autre projet de `Projects/` y crée un dossier `.casaeditor/` du même genre : il se supprime après le lancement et ne s'indexe jamais.
Référence des tests avant ce plan : 6564 tests verts. `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur (refait éditeur de l'auteur fermé, le 2026-11-18).

Faits lus par l'agent principal :

- Dans le jeu, le fondu est dessiné par `ScreenEffectComponent` (`CasaEngine/Framework/Application/Components/ScreenEffectComponent.cs`) : un quad d'un pixel blanc étiré sur toute la vue, de couleur `(R, G, B, A)` du `ScreenEffectService`, soumis à `SpriteRendererComponent.DrawSprite(…, Service.Blend, ignoresDepth: true)` (`:232-266`). Sa position est calculée pour une `Camera2dComponent` (`:138-163`, `:173-185`).
- Les états de mélange du moteur sont privés à `SpriteRendererComponent` : additif `Add`, `One`/`One`, alpha `Add`, `Zero`/`One` ; soustractif `ReverseSubtract`, `One`/`One`, alpha `Add`, `Zero`/`One` (`CasaEngine/Framework/Application/Components/SpriteRendererComponent.cs:108-128`) ; `AlphaBlend` correspond à `BlendState.NonPremultiplied` (`GetBlendState`, `:362-371`). D'après la relecture de ce plan : l'« opaque » du moteur n'est pas `BlendState.Opaque` mais un champ d'instance (`_blendState`, couleur `One`/`Zero`, alpha `Max`, `:150-158`), atteint par la branche par défaut de `GetBlendState` ; des tests construisent déjà un `SpriteRendererComponent` (`CasaEngine.Tests/Scene/SpriteComponentDrawOrderTests.cs:344`). Le shader de sprites du moteur écarte un texel dont l'alpha, après le produit par la couleur, est de 0,01 ou moins (`CasaEngine/Content/Shaders/SpriteBatch.fx:54-58`) ; l'effet par défaut de `SpriteBatch` ne le fait pas.
- Le moteur offre un crochet par vue : `RenderView.RegisterPostUIOverlay(IPostUIOverlay overlay, int order)` et `UnregisterPostUIOverlay` (`CasaEngine/Framework/Rendering/RenderView.cs:193`, `:217`) ; `IPostUIOverlay.Draw(GraphicsDevice, RenderView, in RenderFrame)` est appelé par `DefaultUICompositionService.Compose`, après l'interface de la vue. Le pipeline de surcouches de l'éditeur passe aussi par cette composition (`CasaEngine.Editor/Runtime/Rendering/OverlayViewPipeline.cs:143-148`). D'après la relecture de ce plan : le viewport reçoit toujours ce pipeline par son contrôleur de gizmo (`EditorViewportGizmoController.cs:149-151`) ; quand les ombres sont actives, la cible liée au moment de l'appel est une cible de scène intermédiaire (`RenderPipeline.cs:174-182`) : la taille du rectangle se lit dans `graphicsDevice.Viewport`, pas dans la taille de la surface. La vue de rendu du viewport n'est créée qu'une fois (`EnsureRenderViewCreated`, `WorldViewportPanel.cs:1285-1350`) et annulée à la libération (`:2165-2168`) : ni un redimensionnement ni un changement de monde d'aperçu ne la recréent.
- Le viewport de l'aperçu est un `WorldViewportPanel` créé avec `ShowEditorOverlays = false` et `UseFront2dCamera = true` ; sa vue de rendu (`_renderView`) et sa surface sont privées (`CasaEngine.Editor/Controls/WorldViewportPanel.cs:159-160`, `:1307-1322`). Avec un monde d'aperçu, sa caméra n'est pas une `Camera2dComponent` (`UsesCamera2d` est faux, `:1865`).
- L'éditeur dessine déjà en 2D sur une vue avec `SpriteBatch` (`CasaEngine.Editor/Runtime/Overlays/EditorLightBillboardOverlayRenderer.cs`).
- Politique d'exécution de l'éditeur (`CasaEngine/Framework/Application/GameplayExecutionPolicy.cs:42-54`) : `EditorPreview` met à faux `UpdateAnimatedSprites`, `UpdatePhysicsComponents`, `UpdatePhysicsEngine`, `UpdateGameplayScripts` et `RunBeginPlay`. Elle est portée par le jeu de l'éditeur, donc commune à tous ses mondes.
- Essai du 2026-11-18 sur `Projects/VideoSpriteSample/Cutscenes/loop.cutscene`, par l'automatisation : la vidéo jouée sur un sprite (`PlayVideoSprite`) s'affiche et avance dans l'aperçu. Rien n'est à faire pour elle.

Faits relevés par exploration (lecture seule, 2026-11-18), **à relire par l'exécutant avant de s'y appuyer** :

- `ScreenEffectService` (`CasaEngine/Framework/Rendering/ScreenEffects/ScreenEffectService.cs`) : `Active` (`:36`), `R`, `G`, `B` (`:38-42`), `A` (`:53`), `Blend` de type `SpriteBlendMode` (`:56`), `IsFading` (`:59`), `Clear()` (`:100`), `Update(float)` (`:212`). `SpriteBlendMode` a au moins `Opaque`, `AlphaBlend`, `Additive`, `Subtractive`.
- `CutscenePreviewEffectHost` (`CasaEngine.EditorServices/Cutscenes/CutscenePreviewEffectHost.cs`) : `Audio` rend toujours `null` (`:23`) ; `ScreenEffects` est sa propre instance (`:26`) ; `AllowGameRequest` refuse tout, enregistre chaque action refusée une fois dans `Requests` (`CutscenePreviewRequest(Type, Target)`, `:10`, `:35-55`) et lève `EndRequested` pour `ChangeWorld` ; `Reset()` vide les demandes et efface l'effet d'écran (`:61-67`).
- `ICutsceneEffectHost` (`CasaEngine/Framework/Cutscenes/ICutsceneEffectHost.cs`) : `Audio` (« Null plays nothing and never falls back on the game's service »), `ScreenEffects`, `AllowGameRequest`. La fabrique prend le service de son par `owner.EffectHost.Audio` dès qu'un hôte est posé (`CutsceneActionCoroutineFactory.cs:518-519`) ; sans service, `PlaySound`, `PlayMusic` et `StopMusic` ne font rien et `FadeMusic` n'attend pas sa durée (`:103-117`, `:563-585`).
- `AudioService` (`CasaEngine/Framework/Audio/AudioService.cs`) : `AudioService(IAudioBackend backend, AudioMixer mixer = null)` (`:86`), `Update(float)` (`:1548`), `StopAll()` (`:1482`), `Music.StopAll(fondu)`. Le panneau des sons de l'éditeur prend le sien par `_editorRuntime?.AudioSystemComponent?.Service` (`CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs:333-336`) ; c'est `AudioSystemComponent` qui appelle `Service.Update` (`CasaEngine/Framework/Application/Components/AudioSystemComponent.cs:112`).
- Tests de son sans carte : `new AudioService(new FakeAudioBackend())` (`CasaEngine.Tests/Audio/FakeAudioBackend.cs:11`, exemple dans `CasaEngine.Tests/Cutscenes/CutsceneEffectHostTests.cs:159`).
- **Aucun fichier `.cutscene` du dépôt n'utilise `PlaySound`, `PlayMusic`, `StopMusic` ni `FadeMusic`.**
- Faits lus par l'agent principal après la relecture : `ScreenEffectService.Clear()` ne fait que `Active = false` et arrête la rampe ; il **garde** `R`, `G`, `B`, `A` (`ScreenEffectService.cs:100-104`). Un `FadeScreen` part de la couleur courante du service (`CutsceneActionCoroutineFactory.cs:904-919`). Le moteur a un périphérique de son muet public, `NullAudioBackend` (`CasaEngine/Framework/Audio/Backends/NullAudioBackend.cs:7`). `AudioService` offre `CaptureSnapshot()` et `ApplySnapshot(snapshot, durée)` (`AudioService.cs:1331`, `:1346`), `StopVoicesOwnedBy(object owner)` (`:1400`) et `StopAllExceptBus(bus)` (`:1420`).
- D'après la relecture de ce plan : `FadeMusic` écrit le volume du bus `Music` du mélangeur du service (`CutsceneActionCoroutineFactory.cs:111-114`, `:861-881`) ; l'éditeur n'a qu'un service et qu'un mélangeur pour tout le processus (`AudioSystemComponent.cs:25`, `:50`), dont se sert aussi le mode Play (`CasaEngine.Editor/PlayMode/EditorPlaySessionController.cs:65`, `:101`). Les voix lancées par une cutscene ont pour propriétaire son monde (`CutsceneActionCoroutineFactory.cs:576`, `:596`). `StopMusic` et un `PlayMusic` en fondu enchaîné appellent `Music.StopAll` du lecteur partagé. Si le projet coupe le bus `Master` (ADR-0040, `AudioSystemComponent.cs:42`), le son reste inaudible même allumé. Côté automatisation : la lecture puis les étapes du transport sont appelées dans la même frame (`GameEditor.cs:7148-7150`), alors que `CutsceneDirector.Play` ne fait que mettre la coroutine en file (`CutsceneDirector.cs:119`) ; deux gardes décident si une étape de transport est demandée et terminée (`GameEditor.cs:8321-8329`, `:8339-8348`).
- D'après la seconde relecture : aller au premier instant fait son pas de durée nulle (`ShowFirstInstant`) **avant** que le transport passe en recherche, donc en mode `Running` (`CutscenePreviewSession.cs:299-304`, `:343-354`) ; les pas d'image se font en mode `Paused`, sans passer par `Pause()` de la session (`:243-257` ; `CutscenePreviewTransport.cs:97`). L'instruction de `FadeMusic` garde le bus qu'elle a reçu à sa création et y écrit à chaque mise à jour, quel que soit le service que l'hôte donne ensuite (`CutsceneActionCoroutineFactory.cs:861-881`). `Launch` fait dans l'ordre `Stop()`, `_host.Reset()`, puis la reconstruction du monde (`CutscenePreviewSession.cs:356-369`). Un `AudioService` sans fournisseur de clips écrit un avertissement à chaque `PlaySound` ou `PlayMusic` (`AudioService.cs:1827-1834`, `MusicPlayer.cs:418-423`). Le chargeur de sons de `CutsceneEffectHostTests` rend un son sans fichier : un test qui veut atteindre le faux périphérique doit fournir un identifiant de fichier de son et un fournisseur de clips, comme le font les tests de `CasaEngine.Tests/Audio/`.
- `ExitOnInput` (`CasaEngine/Framework/Cutscenes/ExitOnInputCutsceneActionData.cs`) : `Mapping`, `Inputs` (liste de noms), `NextCutscene`. Dans le jeu, l'action arme une veille (`CutsceneDirector.ArmInputWatch`, `CutsceneDirector.cs:192-203`) qui remplace la veille précédente, joue la cutscene suivante à l'appui d'une des entrées, et s'arrête à la fin de la lecture (`StopInputWatch`, `:206-213`, appelé avant `Ended`, `:230`). Dans l'aperçu, la demande est refusée avant d'armer quoi que ce soit (`CutsceneActionCoroutineFactory.cs:327-345`, `:522-523`).
- Document (`CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`) : `PreviewLimitText` vaut « Sprite animations, physics and scripts are frozen in the preview. » (`:36`) ; `RefreshPreviewStatus` (`:1590`) écrit « Skipped: … » pour les demandes refusées (`:1643-1656`), « Would change the world: the play ends here. » (`:1658-1661`), et aujourd'hui l'état du fondu en texte.
- Session (`CasaEngine.Editor/Runtime/CutscenePreviewSession.cs`) : `EndedByWorldChange` est levé quand l'hôte a reçu une demande de changement de monde pendant qu'une lecture court (`:444`) ; `Status` vient de `CutscenePreviewTransport.GetStatus(lecture en cours, mode, état, fin par changement de monde)` (`:115`).
- Samples : `Projects/SpriteCutsceneSample/Cutscenes/exit.cutscene` est un `FadeScreen` blanc en mélange `Subtractive` d'une seconde, puis `ChangeWorld` ; `loop.cutscene` commence par `ExitOnInput` (`mapping` `sample-input`, `inputs` `["exit"]`, `next_cutscene` `SpriteExit`).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | L'aperçu joue dans une copie isolée d'un monde, jamais dans le monde de l'éditeur ni dans le jeu (ADR-0096). |
| D2 | Dans l'aperçu, un changement de monde et l'enchaînement vers une autre cutscene sont **signalés**, pas exécutés ; le son est **coupé par défaut**, avec un bouton ; les fondus sont **dessinés sur l'aperçu** (réponses de l'auteur avant la tranche A). |
| D3 | Le temps s'affiche en secondes et en images. |
| D4 | Mode autonome : plan approuvé d'avance, exécution enchaînée, aucun push ni fusion. |
| D5 | L'auteur valide à distance sur captures d'écran ; sa recette réelle a lieu à la fin. |

## Arbitrages de l'agent, à confirmer

| Réf | Arbitrage |
|---|---|
| P1 | **Le fondu est dessiné dans l'image de l'aperçu**, par une surcouche enregistrée sur la vue de rendu du viewport (le crochet `IPostUIOverlay` du moteur). Elle dessine un rectangle sur toute la surface de la vue, de la couleur et du mélange de l'effet d'écran de l'aperçu. Le runtime n'est pas modifié. |
| P2 | Le rectangle est dessiné en coordonnées d'écran, avec `SpriteBatch`, comme le fait déjà une surcouche de l'éditeur : la caméra de l'aperçu n'est pas une caméra 2D, et la formule du jeu ne s'y applique pas. Les états du périphérique graphique changés pour ce dessin sont restaurés. |
| P3 | Les états de mélange sont ceux du moteur, recopiés dans l'éditeur parce qu'ils sont privés au runtime : l'opaque du moteur (couleur `One`/`Zero`, alpha `Max`), alpha non prémultiplié, additif, soustractif. Un test compare champ par champ les états de l'éditeur à ceux du moteur, pour qu'une divergence se voie. Le voile n'est pas dessiné quand son alpha vaut 2 ou moins, comme le shader du moteur écarte ces texels. |
| P4 | La ligne d'état garde la mention du fondu en texte (« Screen fade in progress », « Screen overlay rgba(…) ») : elle dit ce que l'image montre, ce qui sert quand le voile est faible. |
| P5 | **Entrée simulée.** Quand une lecture en cours a passé un `ExitOnInput`, l'aperçu le tient pour « armé », comme le jeu : le dernier `ExitOnInput` passé remplace le précédent, et l'arme tombe à la fin de la lecture. Un bouton par entrée armée, libellé `Press "<nom>"`, apparaît sous la barre de transport ; il n'y a aucun bouton tant que rien n'est armé. |
| P6 | Appuyer sur ce bouton arrête la lecture et l'aperçu écrit : « Input "<nom>": the game would play <cutscene suivante>. The play ends here. ». La cutscene suivante n'est pas jouée dans l'aperçu (D2). L'état affiché est « Ended », le monde garde son état, la tête de lecture reste où elle était. |
| P7 | L'entrée simulée n'agit que pendant une lecture en cours ou en pause, pas pendant une recherche. Elle ne passe par aucun périphérique : c'est un bouton, pas une touche. |
| P8 | **Son.** Un bouton `Sound: off` / `Sound: on` dans la barre de transport. Éteint à chaque ouverture d'un document : il n'est pas mémorisé, pour qu'aucun aperçu ne fasse de bruit sans qu'on l'ait demandé. |
| P9 | **Les actions de son ont toujours un service.** L'hôte d'aperçu possède un **service muet** (un `AudioService` sur le périphérique muet du moteur), recréé à chaque remise à zéro. Il ne donne le service de son de l'éditeur que si le son est allumé **et** que le pas en cours est un pas de lecture normale : un pas que le transport fait en mode `Running`. Pour tout autre pas, il donne le service muet : pas d'une recherche, pas d'image (`+1 frame`), et le pas de durée nulle du premier instant. Conséquences : les durées sont toujours celles du jeu (`FadeMusic` attend sa durée dans tous les cas, ce qui lève la limite connue depuis la tranche C) ; une recherche rejoue exactement le même déroulement qu'une lecture ; on n'entend rien pendant une recherche, sinon tous les sons du trajet partiraient d'un coup. Une musique lancée avant l'instant atteint par une recherche ne s'entend pas après : il faut `Restart`. |
| P10 | L'état du mélangeur de l'éditeur est relevé **une seule fois par lancement**, juste après le nettoyage de la lecture précédente, que le son soit allumé ou non ; il n'est jamais relevé quand on allume le bouton. Quand l'aperçu a prêté le service de l'éditeur depuis ce relevé, il le rend propre à chaque pause, `Stop`, recherche, pas d'image, relance, fin de lecture, extinction du bouton, changement de monde d'aperçu et fermeture du document : il arrête les voix dont le monde d'aperçu est propriétaire, arrête les musiques, et **remet le mélangeur dans l'état relevé** (un `FadeMusic` écrit le volume du bus de musique, qui est celui de tout l'éditeur). Le prêt reste noté jusqu'au relevé du lancement suivant : chaque point d'arrêt remet donc le même état, même si un fondu commencé avant l'extinction du bouton a continué d'écrire entre-temps. C'est un arrêt, pas une pause : reprendre après une pause ne relance pas une musique. L'arrêt des musiques porte sur le lecteur de musique partagé de l'éditeur. |
| P11 | Si le projet coupe le bus principal du mélangeur, le son reste inaudible même allumé : les diagnostics et la documentation le disent. |
| P12 | Ce qui reste figé : animations de sprites, physique, scripts. La politique d'exécution est celle du jeu de l'éditeur, commune à tous ses mondes ; la changer pour le seul monde d'aperçu demande une décision à part. La vidéo sur sprite, elle, joue déjà. La mention de la ligne d'état ne change pas. |
| P13 | Automatisation : `--cutscene-preview-sound` allume le son avant la lecture ; `--cutscene-preview-input <nom>` simule l'entrée après les autres étapes du transport. Avec une lecture en cours, cette étape attend que l'aperçu ait fait au moins un pas et qu'une entrée soit armée, pendant 120 frames de l'éditeur au plus ; passé ce délai sans arme, ou tout de suite sans lecture, elle est refusée et notée. Les diagnostics écrivent l'état du voile (actif, couleur, mélange), les entrées armées, l'entrée simulée et sa cible, et l'état du son (bouton, service présent, muet ou non). |
| P14 | Aucun sample ne joue de son et ce plan n'en ajoute pas : un fichier de son serait une donnée binaire de plus dans le dépôt pour un seul essai. Le son est validé par des tests avec le faux périphérique, et par l'oreille de l'auteur à la recette. |
| P15 | Le runtime `CasaEngine/` n'est pas modifié par ce plan. |

## Règles d'exécution pour l'agent

- **Branche `chantier/cutscene-property-editing`**. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API utilisée existe dans le dépôt et a été lue. Sinon : ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build obligatoire** avant ✅ : `dotnet build CasaEngine.Editor.MonoGame.sln`, et `dotnet build CasaEngine.MonoGame.sln` quand `CasaEngine.EditorServices` change ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, la suite entière, verte à chaque commit. Si l'éditeur de l'auteur tourne et verrouille `CasaEngine.Editor/bin`, compiler le projet de l'éditeur vers un dossier hors dépôt et lancer les tests avec un dossier de sortie à part, sans toucher à son processus ; le dire dans la note.
- **Ne jamais indexer** les fichiers préexistants non suivis : `git add` fichier par fichier.
- **Aucune entrée clavier ou souris envoyée à l'éditeur.** Les captures passent par les options d'automatisation, avec `--diagnostics-out` hors dépôt. Aucun lancement automatisé n'enregistre un asset.
- **Aucun fichier de `Projects/`, de `MGUI/` ni de `CasaEngine/` (runtime) n'est modifié.**
- **Aucun test d'avant ce plan n'est modifié**, à deux exceptions possibles, nommées dans la note de validation de la tâche : un test qui affirme le texte exact de la ligne d'état ou de la barre de transport, si une tâche y ajoute un élément ; un test de l'hôte d'aperçu qui affirme que `Audio` est nul ou que `FadeMusic` n'attend pas dans l'aperçu, ce que P9 change.
- **Langue** : ce plan en français ; code, commits, `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ, de closure ni de formatage de chaîne dans `Update`, `Draw` et le layout. Le dessin du voile n'alloue rien par frame : le `SpriteBatch`, la texture d'un pixel et les états de mélange sont créés une fois. Les boutons des entrées armées ne sont reconstruits que lorsque la liste des entrées armées change.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- Les deux solutions buildent à 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts ; référence avant ce plan : 6564 tests.
- `git diff 8cd29fb8 -- Projects/ MGUI/ CasaEngine/` : vide.
- Captures automatisées (T2.6) : le voile d'`exit.cutscene` à mi-fondu et en fin de fondu, son retrait par `Stop`, le bouton d'entrée et l'entrée simulée sur `loop.cutscene`, le bouton du son.
- Retour arrière : un commit par tâche ; annuler une tâche revient à annuler son commit.

---

## Phase 0 — Décisions

### ✅ T0.1 — ADR-0103

- Objectif : enregistrer P1 → P15.
- Fichiers : `docs/decisions/0103-<short-title>.md` (créé), `docs/decisions/README.md`, `ai-agent/README.md` (ligne de ce plan), ce plan.
- Étapes :
  1. Écrire l'ADR en anglais avec le skill `adr`, statut `Accepted`, date 2026-11-19, source : ce plan et ADR-0096 (dont elle complète les points laissés à plus tard : dessin des fondus, bouton du son, entrée simulée). Contenu : le voile dessiné par une surcouche de la vue du viewport, en coordonnées d'écran, avec les états de mélange du moteur recopiés et testés (P1 → P4) ; l'entrée simulée qui signale et arrête (P5 → P7) ; le son à la demande, le service muet qui garde les durées du jeu, l'état du mélangeur de l'éditeur relevé et rendu, l'arrêt et non la pause (P8 → P11) ; ce qui reste figé et pourquoi (P12) ; l'automatisation (P13) ; l'absence de sample sonore (P14) ; runtime inchangé (P15).
  2. Ligne d'index ; ligne de ce plan dans `ai-agent/README.md`.
- Validation : les liens de l'index pointent sur des fichiers existants ; aucun nom de jeu ni de projet tiers dans les fichiers touchés.
- Commit : `docs(decisions): record the drawn fades, the simulated input and the sound of the cutscene preview`
- Note de validation : ADR-0103 écrit (`docs/decisions/0103-the-cutscene-preview-draws-fades-simulates-an-input-and-plays-sound-on-demand.md`), statut `Accepted`, date 2026-11-19, P1 à P15 enregistrés ; ligne ajoutée à l'index `docs/decisions/README.md` et à `ai-agent/README.md`. Documentation seule : aucun build ni test lancé. Liens de l'index vérifiés sur des fichiers existants ; aucun nom de jeu ni de projet tiers dans les fichiers touchés. Aucun test existant modifié.

---

## Phase 1 — Services d'édition (`CasaEngine.EditorServices`)

### ✅ T1.1 — L'hôte d'aperçu sait ce qui est armé et peut donner le son

- Objectif : la part de P5 → P11 qui se teste sans l'éditeur.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/CutscenePreviewEffectHost.cs`, tests dans `CasaEngine.Tests/EditorServices/Cutscenes/CutscenePreviewEffectHostTests.cs` (tests ajoutés).
- Étapes :
  1. Entrées armées : chaque appel d'`AllowGameRequest` pour un `ExitOnInput` arme cette action à la place de la précédente, **avant** le contrôle qui évite d'enregistrer deux fois la même demande. L'hôte expose l'action armée, la liste de ses noms d'entrée et sa cutscene suivante, et un numéro de version qui ne change que lorsque l'action armée change (réarmer la même action à chaque tour d'une boucle ne le change pas). Une opération désarme ; `Reset()` désarme aussi.
  2. Simulation : pour un nom, l'hôte dit s'il fait partie des entrées armées et rend la cutscene suivante ; il ne fait rien d'autre. Un nom inconnu ou l'absence d'arme rend un refus.
  3. Effet d'écran : `Reset()` remet **la même instance** de `ScreenEffects` dans son premier état, par son API publique seulement : couleur noire, alpha 255, mélange par défaut, couche sous l'interface, inactif (par exemple `SetOverlay(0, 0, 0, 255, défaut)` puis `Clear()`, puis la couche). L'instance ne change jamais : la surcouche de T2.1 la garde en référence.
  4. Son : l'hôte possède un service muet (`new AudioService(new NullAudioBackend())`), libéré et recréé par `Reset()` et libéré avec l'hôte. Il reçoit en plus le service de l'éditeur (ou rien), un drapeau « allumé » et un drapeau « pas de lecture normale », faux par défaut. `Audio` rend le service de l'éditeur seulement s'il existe, que le son est allumé et que ce drapeau est vrai ; sinon le service muet. L'hôte devient libérable (`IDisposable`) pour libérer son service muet. `Reset()` n'efface ni la note de prêt ni le relevé du mélangeur (étape 5).
  4 bis. **Relevé des pas**, lisible hors d'un pas : à chaque `Update` de l'hôte (la session l'appelle une fois par pas), l'hôte compte le pas dans l'un de deux compteurs selon la valeur du drapeau « pas de lecture normale » à ce moment, et retient cette valeur comme celle du dernier pas. Les deux compteurs et la valeur du dernier pas sont exposés en lecture ; « relever » (étape 5) les remet à zéro, donc ils valent pour le lancement en cours. `Audio` ne rend donc plus jamais `null`. `Reset()` ne change ni le service de l'éditeur ni le drapeau « allumé ». L'hôte appelle `Update` du service muet dans son propre `Update` ; le service de l'éditeur est mis à jour par l'éditeur.
  5. Prêt : l'hôte note que le service de l'éditeur a été prêté dès qu'`Audio` le rend. Une opération « relever » capture le mélangeur (`CaptureSnapshot`) et efface cette note. Une opération « rendre propre », si le service a été prêté depuis le dernier relevé, arrête les voix d'un propriétaire donné (`StopVoicesOwnedBy`), arrête les musiques (`Music.StopAll(0)`) et applique le relevé (`ApplySnapshot`, durée 0) ; elle **n'efface pas** la note, pour que les points d'arrêt suivants remettent encore le même état. Sans service de l'éditeur, les deux ne font rien.
- Validation : build des deux solutions ; tests ajoutés : un `ExitOnInput` passé arme ses entrées ; un second remplace le premier ; le même repassé (boucle) reste armé sans doubler la demande enregistrée ; désarmer ; `Reset` ; simulation d'un nom armé, d'un nom inconnu, sans arme ; `Audio` dans chaque combinaison des deux drapeaux et sans service de l'éditeur : jamais `null`, le service de l'éditeur seulement quand le son est allumé et que le pas est un pas de lecture normale. Effet d'écran : après un `FadeScreen` blanc joué jusqu'au bout, `Reset`, puis le même fondu rejoué jusqu'à sa moitié, `R` vaut environ 128 et non 255 ; l'instance de `ScreenEffects` est la même avant et après `Reset`. Avec un vrai `World` et son directeur (comme `CutsceneEffectHostTests`), un `AudioService` sur `FakeAudioBackend` comme service de l'éditeur : (le test fournit un son avec un fichier et un fournisseur de clips, sur le modèle des tests de `CasaEngine.Tests/Audio/`) : hôte allumé en pas de lecture normale, une cutscene `PlaySound` ou `PlayMusic` atteint le faux périphérique ; hôte éteint, ou pas qui n'est pas de lecture normale, rien ne l'atteint ; **dans les trois cas**, un `FadeMusic` d'une seconde suivi d'une action repère fait partir le repère à environ 1 s. Prêt : après un `FadeMusic` vers 0 joué allumé, « rendre propre » remet le bus de musique à son volume du relevé et arrête les voix du monde. Bus de musique à 1,0, relevé, son allumé, un `FadeMusic` d'une seconde vers 0 lancé, son éteint à 0,3 s puis rallumé à 0,6 s sans nouveau relevé, la lecture va au bout, « rendre propre » : le bus est revenu à 1,0. Une lecture précédente a laissé le bus à 0 : « rendre propre » puis « relever » lisent 1,0. Relevé des pas : trois `Update` avec le drapeau faux puis deux avec le drapeau vrai donnent 3 et 2 et un dernier pas « normal » ; « relever » remet tout à zéro ; `Reset()` ne les change pas. Un test existant de l'hôte qui affirme `Audio` nul est mis à jour (exception nommée dans la note).
- Commit : `feat(editor-services): track the armed input and lend the audio service in the cutscene preview host`
- Note de validation : `dotnet build CasaEngine.Editor.MonoGame.sln` et `dotnet build CasaEngine.MonoGame.sln` : 0 erreur chacun (sorties par défaut : l'éditeur de l'auteur ne tournait pas, aucun verrou). `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, suite entière : 6592 réussis, 0 échec (6564 avant ce plan, 28 tests ajoutés dans `CutscenePreviewEffectHostTests`). Le test d'hôte avec le faux périphérique fournit un son avec un fichier et un fournisseur de clips (et un son en flux pour la musique) ; il prouve les trois cas du `FadeMusic` d'une seconde (son allumé en pas normal, son éteint, pas qui n'est pas de lecture normale : le repère part entre 10 et 12 images de 0,1 s), le retour du bus de musique à 1,0 après un fondu vers 0 (y compris avec extinction à 0,3 s puis rallumage à 0,6 s sans nouveau relevé), l'arrêt des voix du monde et des musiques, et le relevé des pas (3 et 2, remis à zéro par `TakeMixerSnapshot` seulement). Test existant modifié (première exception nommée du plan) : `TheHostPlaysNoSound_AndOwnsItsScreenEffectService` est devenu `TheHostAlwaysHasAnAudioService_AndOwnsItsScreenEffectService` (il affirmait `Audio` nul ; il affirme maintenant qu'`Audio` n'est jamais nul). Aucun autre test existant modifié. API ajoutée à l'hôte : constructeur optionnel `CutscenePreviewEffectHost(AudioService editorAudio = null)`, `IsSoundOn`, `IsNormalPlayStep`, `EditorAudio`, `EditorAudioLent`, `NormalPlayStepCount`, `OtherStepCount`, `LastStepWasNormalPlay`, `ArmedInput`, `ArmedInputNames`, `ArmedNextCutscene`, `ArmedVersion`, `Disarm()`, `TrySimulateInput(nom, out suivante)`, `TakeMixerSnapshot()`, `RestoreEditorAudio(propriétaire)`, `Dispose()`. La session n'est pas modifiée (T2.2) : elle construit toujours l'hôte sans service de l'éditeur, donc son `Audio` est maintenant le service muet et `FadeMusic` attend sa durée dans l'aperçu dès ce commit ; l'hôte n'y est pas encore libéré (T2.2).

---

## Phase 2 — Éditeur (`CasaEngine.Editor`)

### ✅ T2.1 — Le voile dessiné sur l'aperçu

- Objectif : P1 → P4.
- Fichiers : `CasaEngine.Editor/Runtime/Overlays/` (un fichier créé pour la surcouche), `CasaEngine.Editor/Controls/WorldViewportPanel.cs`, `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`, tests dans `CasaEngine.Tests/Editor/`.
- Étapes :
  1. Une surcouche `IPostUIOverlay` de l'éditeur, qui lit un `ScreenEffectService` : inactive, ou d'alpha 2 ou moins, elle ne dessine rien ; active, elle dessine un pixel blanc étiré sur tout le viewport courant du périphérique (`graphicsDevice.Viewport`), de couleur `(R, G, B, A)`, avec l'état de mélange de `Blend`. Lire `EditorLightBillboardOverlayRenderer` pour la façon de dessiner en 2D sur une vue, et `IPostUIOverlay` / `DefaultUICompositionService` pour le moment de l'appel et l'état du périphérique à ce moment. Sauver puis restaurer les états du périphérique que le dessin change.
  2. Les quatre états de mélange, définis une fois, statiques, d'après `SpriteRendererComponent.cs:108-128`, `:150-158` et `:362-371` (les lignes citées en commentaire).
  3. `WorldViewportPanel` : un moyen interne à l'éditeur de poser une surcouche sur sa vue de rendu : enregistrée tout de suite si la vue existe, sinon à sa création ; retirée à la libération du panneau. Les autres utilisateurs du viewport ne changent pas.
  4. Le document pose cette surcouche sur le viewport de son aperçu, liée à l'effet d'écran de sa session ; rien ne change sans aperçu.
- Validation : build ; tests ajoutés : pour chaque mode, l'état de mélange de l'éditeur a les mêmes champs que celui du moteur (lecture par réflexion, dans le test seulement, des champs statiques privés et du champ d'instance d'un `SpriteRendererComponent` construit comme dans les tests existants) ; la surcouche ne demande aucun dessin quand l'effet est inactif ou d'alpha 2 ou moins (ce qui se teste sans carte). L'enregistrement et le retrait sur le viewport sont testés si un `WorldViewportPanel` peut être construit dans les tests ; sinon ils sont validés par les captures de T2.6, et la note le dit. Si un test avec carte graphique est possible dans le projet de tests (voir les tests `Gpu` existants), un test dessine le voile soustractif blanc sur une cible claire et lit un pixel noir ; sinon le dessin est validé par les captures de T2.6, et la note le dit.
- Commit : `feat(editor): draw the screen fade of a cutscene on its preview`
- Note de validation : `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur (sortie par défaut : l'éditeur de l'auteur ne tournait pas, aucun verrou) ; `CasaEngine.EditorServices` n'a pas changé, la solution du moteur n'a donc pas été reconstruite. `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, suite entière : 6609 réussis, 0 échec, 0 ignoré (6592 avant cette tâche, 17 tests ajoutés dans `CasaEngine.Tests/Editor/CutsceneScreenFadeOverlayTests.cs`). Ils prouvent : les quatre états de mélange de l'éditeur ont les mêmes champs que ceux du moteur (lus par réflexion sur un `SpriteRendererComponent` construit comme dans les tests existants, y compris l'opaque d'instance) ; un effet inactif ou d'alpha 2 ou moins ne demande aucun dessin ; et, avec la carte graphique du poste (les tests `Gpu` ont tourné, aucun n'a été ignoré), le voile soustractif blanc rend noir une image claire, un voile soustractif à 128 la ramène de 200 à environ 72, un voile additif l'éclaircit, le voile ne couvre que le viewport courant du périphérique, et la cible, le viewport, le mélange, la profondeur et le rastériseur sont restaurés après le dessin. L'enregistrement et le retrait sur le viewport ne sont pas testés (un `WorldViewportPanel` ne se construit pas sans le runtime de l'éditeur) : ils sont validés par les captures de T2.6, et par un premier essai de cette tâche : `exit.cutscene` avec `--cutscene-preview-seek 0.5` donne un fond d'aperçu `(27, 47, 87)` contre `(150, 170, 210)` sans lecture, soit un voile d'environ 123 par canal, cohérent avec une mi-course soustractive (essai préalable, pas la recette formelle de T2.6). Aucun test existant modifié. Fichiers : `CasaEngine.Editor/Runtime/Overlays/CutsceneScreenFadeOverlay.cs` (créé), `WorldViewportPanel.cs` (`SetPostUiOverlay`, interne, enregistrée à la création de la vue ou tout de suite si elle existe, retirée à la libération), `CutsceneAssetInspectorPanel.cs` (pose la surcouche, liée à `ScreenEffects` de la session, et la libère). La mention du fondu de la ligne d'état (P4) est inchangée.

### ✅ T2.2 — La session simule une entrée et prête le son

- Objectif : P5 → P11, côté session.
- Fichiers : `CasaEngine.Editor/Runtime/CutscenePreviewSession.cs`.
- Étapes :
  1. Entrée : la session expose les entrées armées et leur version (celles de l'hôte) ; `SimulateInput(nom)` n'agit que si une lecture court et que le transport n'est pas en recherche : elle lit d'abord dans l'hôte le nom armé et la cutscene suivante (l'arrêt lève `Ended`, qui désarme), les retient, puis arrête la lecture, et lève un indicateur « fin par entrée » qui compte comme la fin par changement de monde pour l'état affiché (« Ended »). L'arme tombe à la fin de toute lecture ; l'indicateur et ce qui a été retenu sont remis à zéro par toute relance, par `Rewind` et par `SetWorld`.
  2. Son : la session reçoit le service de son du runtime de l'éditeur à sa création (il peut manquer) et le donne à l'hôte ; elle expose le drapeau « allumé ». Le drapeau « pas de lecture normale » de l'hôte n'est vrai que pendant un pas que le transport fait en mode `Running` : dans `Tick`, la session le met à la valeur de « le mode du transport est `Running` » juste avant `_transport.Advance`, et à faux juste après ; il reste donc faux pour les pas d'une recherche, les pas d'image, et le pas de durée nulle du premier instant, qui est fait hors de `Tick`. Dans `Launch`, l'ordre est : arrêt de la lecture précédente, « rendre propre » (avec l'ancien monde comme propriétaire des voix), puis « relever », puis la suite ; « relever » n'est appelé nulle part ailleurs, en particulier pas quand le son est allumé. « Rendre propre », avec le monde d'aperçu comme propriétaire des voix, est appelé dans tous les autres cas de P10 : à la fin de toute lecture (`OnEnded`), dans `Pause`, dans `StepFrames`, dans la branche de `Seek` qui repart de l'état courant, dans `SetWorld` (explicitement, même si rien ne joue), à l'extinction du bouton et à la libération. La session libère l'hôte à sa propre libération, après ce nettoyage et avant de libérer le pilote du monde. La fin d'une lecture qui se termine d'elle-même en fait partie ; une boucle sans fin ne se termine pas, et son son s'arrête par `Pause`, `Stop` ou le bouton.
- Validation : build ; tests verts (aucun test ne construit la session : sa validation est celle de T2.5 et T2.6).
- Commit : `feat(editor): simulate an input and lend the sound in the cutscene preview session`
- Note de validation : `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur (sortie par défaut : l'éditeur de l'auteur ne tournait pas, aucun verrou) ; `CasaEngine.EditorServices` n'a pas changé, la solution du moteur n'a pas été reconstruite. `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, suite entière : 6609 réussis, 0 échec, 0 ignoré (aucun test ajouté, comme le plan le prévoit). Une première exécution complète avait donné 1 échec, `VideoSpriteComponentGpuTests.InitializeWithWorld_WhenTheFirstPictureCannotBeDecoded_ThrowsDisposesThePlayer_AndReleasesTheHold` (test GPU de la vidéo, sans lien avec la session) ; seul, il passe (14 sur 14 de la classe), et la suite entière relancée est verte : instabilité isolée du test, non corrigée ici. Aucun test existant modifié. Seul fichier de code touché : `CasaEngine.Editor/Runtime/CutscenePreviewSession.cs`. La session reçoit `editorRuntime.AudioSystemComponent?.Service` à sa création et le donne à l'hôte. Ajouts : `SetSound(bool)`, `IsSoundOn`, `SimulateInput(nom)`, `EndedByInput`, `SimulatedInput`, `SimulatedNextCutscene`, `ArmedInputNames`, `ArmedInputVersion`, `ArmedNextCutscene`. `Status` compte `EndedByInput` comme la fin par changement de monde. `SimulateInput` n'agit que pendant une lecture en cours ou en pause (pas en recherche) : elle lit le nom armé et la cutscene suivante dans l'hôte avant d'arrêter la lecture. Dans `Tick`, `IsNormalPlayStep` vaut « le mode du transport est `Running` » juste avant `_transport.Advance` et faux juste après ; le pas de durée nulle du premier instant (fait hors de `Tick`) reste donc un pas qui n'est pas de lecture normale. `Launch` : `Stop()`, puis `TakeMixerSnapshot()`, puis `_host.Reset()` et la suite ; `TakeMixerSnapshot` n'est appelé nulle part ailleurs. `Stop()` de la session fait lui-même le « rendre propre » (propriétaire des voix : le monde d'aperçu courant, donc l'ancien monde dans `Launch` et `SetWorld`), ce qui couvre `Launch` et `SetWorld` sans appel redondant ; `OnEnded` (désarme l'entrée et rend propre), `Pause`, `StepFrames`, la branche de `Seek` qui repart de l'état courant, `SetSound(false)` et `Dispose` (avant la libération du pilote, l'hôte étant libéré avant le pilote) le font aussi. Les indicateurs de l'entrée simulée sont remis à zéro par `Launch`, `Rewind` et `SetWorld`. Vérifié par un lancement automatisé de `loop.cutscene --cutscene-preview-play --capture-delay 1` : état `Playing`, mode `Running`, une lecture lancée, aucune erreur ; l'entrée simulée et le son restent à valider par T2.5 et T2.6 comme le plan le prévoit.

### ⏳ T2.3 — Boutons d'entrée

- Objectif : P5, P6, côté document.
- Fichiers : `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`, tests dans `CasaEngine.Tests/Editor/`.
- Étapes :
  1. Une rangée sous la barre de transport, vide et repliée tant que rien n'est armé ; un bouton `Press "<nom>"` par entrée armée, reconstruits seulement quand la version des entrées armées change.
  2. Le clic appelle une méthode publique du document, `SimulatePreviewInput(nom)`, qui rend faux sans aperçu, sans lecture ou pour un nom non armé.
  3. Ligne d'état : après une entrée simulée, la phrase de P6 ; la clé qui décide de réécrire la ligne en tient compte.
- Validation : build ; tests ajoutés pour ce qui se teste sans aperçu : `SimulatePreviewInput` rend faux et ne lève rien sur un document sans aperçu. Le reste est validé par T2.5 et T2.6.
- Commit : `feat(editor): press a simulated input in the cutscene preview`

### ⏳ T2.4 — Bouton du son

- Objectif : P8, P10.
- Fichiers : `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`, `CasaEngine.Editor/GameEditor.cs` si le document doit être prévenu de sa fermeture ou de sa désactivation, tests.
- Étapes :
  1. Un bouton dans la barre de transport, `Sound: off` / `Sound: on`, dont le libellé ne change qu'au clic ; éteint à la création du document.
  2. Méthode publique `SetPreviewSound(bool)` ; rend faux sans aperçu.
  3. Arrêt du son à la libération du document et quand son onglet n'est plus le document actif (vérifier comment le document en est prévenu aujourd'hui ; s'il ne l'est pas, le noter et n'arrêter qu'à la libération).
- Validation : build ; tests ajoutés : `SetPreviewSound` rend faux et ne lève rien sans aperçu. Le reste est validé par T2.5 et T2.6.
- Commit : `feat(editor): add a sound switch to the cutscene preview`

### ⏳ T2.5 — Automatisation et diagnostics

- Objectif : P13.
- Fichiers : `CasaEngine.Editor/EditorAutomationOptions.cs`, `CasaEngine.Editor/GameEditor.cs`, `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`, `CasaEngine.Tests/Editor/EditorAutomationOptionsTests.cs` (tests ajoutés).
- Étapes :
  1. `--cutscene-preview-sound` : allume le son du document ouvert avant toute lecture ou recherche. `--cutscene-preview-input <nom>` : après les étapes du transport existantes (recherches, pas, éditions après transport, reprise), simule l'entrée selon la règle d'attente de P13 ; l'option rejoint les deux gardes qui disent si une étape de transport est demandée et terminée, pour que l'étape soit atteinte et que le délai de capture se compte depuis elle. Incompatible avec `--cutscene-preview-stop` de la même façon que la reprise : seul le Stop est appliqué, et les diagnostics le disent.
  2. Diagnostics : voile (actif ou non, `rgba`, mélange, fondu en cours) ; entrées armées ; entrée simulée et cutscene suivante ; son (bouton, service de l'éditeur présent, bus principal coupé ou non, service de l'éditeur prêté ou non depuis le lancement, et le relevé des pas : nombre de pas de lecture normale, nombre d'autres pas, nature du dernier pas).
- Validation : build ; tests d'analyse des deux options ; un lancement `loop.cutscene --cutscene-preview-play --cutscene-preview-input exit --capture-delay 1` écrit l'entrée `exit` simulée vers `SpriteExit` et l'état « Ended » ; sans lecture, la même option écrit un refus.
- Commit : `feat(editor): drive the simulated input and the sound of the cutscene preview from the automation`

### ⏳ T2.6 — Captures de validation

- Objectif : produire les captures de la tranche et vérifier que l'image et les diagnostics disent la même chose.
- Étapes, sur `Projects/SpriteCutsceneSample`, `--diagnostics-out` hors dépôt, fichiers préfixés `t` :
  1. `exit.cutscene`, `--cutscene-preview-seek 0.5` : le voile est actif, soustractif, à peu près à mi-course ; l'image de l'aperçu est **nettement plus sombre** que sans voile (comparer à une capture du même document sans lecture, `t0`). Pour ce lancement et les trois suivants, la note donne le `rgba` du voile lu dans les diagnostics et la couleur d'un pixel du fond de l'aperçu relevée sur la capture, à côté de la description.
  2. `exit.cutscene`, `--cutscene-preview-play --capture-delay 3` : fin de lecture, voile blanc soustractif complet : l'image de l'aperçu est **noire** ; état « Ended ».
  3. `exit.cutscene`, `--cutscene-preview-seek 1.5 --cutscene-preview-stop` : plus de voile, l'image est celle de `t0`.
  3 bis. `exit.cutscene`, `--cutscene-preview-seek 1.5,0.5` : la première recherche va jusqu'à la fin du fondu, la seconde relance et s'arrête à 0,5 s dans le même lancement : le voile est à mi-course (`rgba` proche de celui du lancement 1) et l'image aussi sombre que celle du lancement 1, **pas noire**.
  4. `loop.cutscene`, `--cutscene-preview-play --capture-delay 1` : le bouton `Press "exit"` est visible sous la barre de transport ; les diagnostics donnent l'entrée armée `exit` vers `SpriteExit`.
  5. `loop.cutscene`, `--cutscene-preview-play --cutscene-preview-input exit --capture-delay 1` : lecture arrêtée, état « Ended », la ligne d'état porte la phrase de P6, le bouton a disparu.
  6. `loop.cutscene` sans lecture : aucun bouton d'entrée ; le bouton `Sound: off` est visible. Avec `--cutscene-preview-sound` : `Sound: on`, et les diagnostics disent le son allumé, si le service de l'éditeur est présent et si le bus principal est coupé.
  6 bis. Relevé des pas, sur `loop.cutscene` avec `--cutscene-preview-sound` : avec `--cutscene-preview-seek 0`, aucun pas de lecture normale et au moins un autre pas (celui du premier instant) ; avec `--cutscene-preview-seek 0.5 --cutscene-preview-step 3`, aucun pas de lecture normale, au moins 33 autres pas, et un dernier pas qui n'est pas de lecture normale ; avec `--cutscene-preview-play --capture-delay 1`, au moins un pas de lecture normale et un dernier pas de lecture normale. Les trois lancements donnent donc trois relevés différents, et seul un pas fait en mode `Running` peut produire le troisième.
  7. `loop.cutscene`, `--cutscene-preview-input exit` sans lecture : refusé, rien ne change.
  8. Non-régression : relancer `p2` (tranche F), `e3` et `e1` (tranche D) ; les valeurs sont celles notées dans leurs plans.
- Validation : chaque capture est regardée et décrite honnêtement ; voile absent, mal placé (qui déborde du viewport ou n'en couvre qu'une partie), bouton coupé ou barre qui déborde : défaut à corriger avant ✅, ou à noter en 🧪. `git status` ne montre aucun nouveau fichier sous `Projects/`.
- Limite d'essais : deux séries de corrections au plus dans cette tâche ; au-delà, 🧪 avec la liste exacte de ce qui reste.
- Commit : `docs(plan): record the validation screenshots of the cutscene preview fidelity`, ou `fix(editor): …` si une correction est nécessaire.

---

## Phase 3 — Documentation et recette

### ⏳ T3.1 — Documentation

- Objectif : `docs/editor/cutscene-editor.md` décrit le voile dessiné, l'entrée simulée, le bouton du son et ses règles (muet en recherche, arrêt et non pause, service partagé avec le panneau des sons), ce qui reste figé, les deux options d'automatisation et les diagnostics ; trois limites du son : un fondu de musique commencé pendant une lecture audible continue d'agir sur le volume de musique de l'éditeur pendant une extinction du bouton, une recherche ou des pas d'image qui suivent, jusqu'au point d'arrêt suivant, où l'état est rendu ; rendre l'état du mélangeur annule aussi un réglage du mélangeur fait par ailleurs entre le lancement et ce point d'arrêt ; le service muet écrit un avertissement dans le journal pour chaque son qu'il ne peut pas jouer ; les phrases qui disaient les fondus non dessinés et le son coupé sans bouton sont corrigées.
- Fichiers : ce document, ce plan, `ai-agent/README.md`.
- Validation : relecture contre le code livré ; aucun nom de jeu ni de projet tiers.
- Commit : `docs(editor): document the drawn fades, the simulated input and the sound of the cutscene preview`

### ⏳ T3.2 — Recette de l'auteur

- Validation à distance sur les captures de T2.6, puis recette réelle à la fin (D5) : en particulier l'écoute du son, qu'aucune capture ne peut montrer. Passe en 🧪 quand les captures sont envoyées.

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Le son ne peut être validé ni par une capture ni sur un sample : il l'est par des tests avec le faux périphérique, puis par l'écoute de l'auteur. | T1.1, T2.2, T2.4 |
| O2 | L'arrêt des musiques de l'aperçu porte sur le lecteur de musique partagé de l'éditeur ; `StopMusic` et un `PlayMusic` en fondu enchaîné joués par une cutscene agissent aussi sur lui. Les voix, elles, ne sont arrêtées que pour le monde d'aperçu. À confirmer à la recette. | T1.1, T2.2 |
| O3 | Le voile est dessiné après l'interface de la vue ; une cutscene qui ouvre un écran dans le jeu (`OpenScreen`) n'en affiche pas dans l'aperçu, donc l'ordre entre le voile et un écran ne se pose pas ici. | T2.1 |
| O4 | Confirmation de P1 → P15 par l'auteur. | Toutes |
| O5 | Levé le 2026-11-19. Le plan avait été mis en pause le 2026-11-18 après trois relectures refusées : la première sur les trois sujets (4 défauts corrigés), les deux suivantes sur le son seulement (2 défauts corrigés, puis 1). Le dernier — le lancement 6 bis ne pouvait pas prouver que le son reste muet hors lecture normale — est corrigé par le relevé des pas (T1.1 étape 4 bis, diagnostics de T2.5, lancement 6 bis réécrit). L'auteur a décidé de garder les trois sujets ensemble (« Non, tout ensemble ») : le plan s'exécute sans quatrième relecture ; la vérification indépendante après exécution reste due. | T1.1, T2.2, T2.5, T2.6 |

## Hors périmètre

- Animations de sprites, physique et scripts dans l'aperçu (politique d'exécution par monde).
- Jouer dans l'aperçu la cutscene vers laquelle une entrée ou un changement de monde mènerait.
- Mettre le son en pause et le reprendre ; faire jouer après une recherche la musique lancée avant l'instant atteint.
- Mémoriser l'état du bouton du son.
- Un sample sonore.
- Tout changement du format `.cutscene`, du runtime `CasaEngine/` ou de MGUI.
