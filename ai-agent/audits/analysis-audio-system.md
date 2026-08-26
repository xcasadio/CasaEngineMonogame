# Analyse — Moteur de son, ressources sonores et intégration éditeur

## Contexte

Demande : démarrer le **moteur de son** de CasaEngine :

1. gestion des **ressources sonores** (assets) côté runtime ;
2. **assets de sons dans le projet et dans le Content Browser** de l'éditeur ;
3. lecture **one-shot**, **loop** et **streaming** (musiques de fond) ;
4. gestion des **channels**.

Analyse basée sur une lecture statique du code au commit `6384bf4d` et sur une **inspection des métadonnées de `MonoGame.Framework.dll` (DesktopGL 3.8.4.1)** et de `NVorbis.dll` (0.10.4) via `MetadataLoadContext`. **Le moteur n'a pas été exécuté** : aucun test audio runtime n'a été effectué.

Ce document ne contient aucune implémentation. Il établit les faits, les contraintes dures du backend, les décisions prises (§3), ce qu'il faut construire (§4) et les points de blocage restants (§5).

---

## Verdict global

| Élément | État |
|---|---|
| Code audio runtime | ❌ quasi inexistant — 3 fichiers, jamais instanciés (code mort) |
| Loader d'asset son (`IAssetLoader`) | ❌ absent |
| Extension d'asset son (`Constants.FileNameExtensions`) | ❌ absente |
| Sérialisation éditeur d'un asset son | ❌ absente |
| Reconnaissance des fichiers audio dans le Content Browser | ✅ déjà en place (type, icône, libellé) |
| Enregistrement au catalogue à l'import | ✅ déjà automatique (tout fichier importé est catalogué) |
| Aperçu / lecture d'un son dans l'éditeur | ❌ absent |
| Composant entité émetteur de son | ❌ absent |
| Notion de bus / channel / mixage | ❌ absente |
| Streaming musique | ❌ absent |
| Fichiers audio de test dans le repo | ✅ depuis le 2026-08-26 : 2 WAV dans `CasaEngine.Demos/Content/Audio` (voir §1.6) |
| Hooks audio dans cutscenes / dialogues / animations 2D | ❌ aucun (mentionnés comme hypothétiques dans `docs/`) |

Conclusion : **tout est à construire**, mais l'infrastructure d'assets et le Content Browser sont déjà prêts à accueillir des sons sans refonte.

---

## 1. État des lieux (faits vérifiés dans le code)

### 1.1 Le code audio existant est du code mort

Trois fichiers seulement :

| Fichier | Contenu | Utilisé ? |
|---|---|---|
| [Sound.cs](../../CasaEngine/Framework/Audio/Sound.cs) | Wrapper minimal `SoundEffect` → `SoundEffectInstance` (`Initialize()` crée l'instance) | ❌ jamais instancié |
| [IAudioEmitter.cs](../../CasaEngine/Framework/Audio/IAudioEmitter.cs) | Interface `Position`/`Forward`/`Up`/`Velocity` | Référencé uniquement par `AudioComponent` |
| [AudioComponent.cs](../../CasaEngine/Framework/Application/Components/AudioComponent.cs) | `GameComponent` : `AudioListener`, dictionnaire `SoundEffect`, liste de sons 3D actifs, `Play3DSound(name, isLooped, emitter)` + `Apply3D` par frame | ❌ jamais construit (absent de `CasaEngineGame.Initialize`) |

Points notables sur `AudioComponent` :

- il charge via `Game.Content.Load<SoundEffect>(soundName)`, c'est-à-dire le **pipeline MGCB (`.xnb`)** — donc en dehors du système d'assets JSON du moteur (`AssetCatalog` / `AssetContentManager`) ;
- il fixe en dur `SoundEffect.DistanceScale = 2000` et `DopplerScale = 0.1f` dans `Initialize()` (valeurs globales statiques du framework) ;
- il ne connaît ni bus, ni volume, ni priorité, ni streaming.

### 1.2 Le système d'assets est prêt, mais ne connaît pas le son

Chaîne actuelle ([AssetContentManager.cs](../../CasaEngine/Framework/Assets/AssetContentManager.cs)) :

```text
AssetInfo (Guid, Name, FileName, AssetType)  ← AssetInfos.json (AssetCatalog)
        ↓ Load<T>(Guid)
IAssetLoader enregistré par Type (AssetLoaderRegistry)
        ↓
cache par catégorie ; Unload() dispose les IDisposable ; OnDeviceReset() pour IAssetable
```

Faits :

- [AssetLoaderRegistry.cs](../../CasaEngine/Framework/Assets/AssetLoaderRegistry.cs) enregistre 26 loaders : **aucun pour `SoundEffect` ni pour la musique**.
- [Constants.cs](../../CasaEngine/Framework/Configuration/Constants.cs) définit 23 extensions d'assets : **aucune pour le son**.
- [EditorAssetJsonSerializer.cs](../../CasaEngine.EditorServices/EditorAssetJsonSerializer.cs) sérialise 17 types d'assets : **aucun son**.
- `AssetContentManager.Unload(category)` appelle `Dispose()` sur les assets `IDisposable` : `SoundEffect` étant `IDisposable`, la libération fonctionnerait telle quelle.
- `AssetContentManager.Load<T>` **met en cache par défaut** (`cache: true`) : correct pour un `SoundEffect` court, **inadapté** à un flux de musique (une position de lecture ne se partage pas).

### 1.3 Le Content Browser reconnaît déjà les fichiers son (mais ne fait rien avec)

Déjà en place :

- `ContentItemType.Sound` existe ([ContentItemType.cs](../../CasaEngine.Editor/ContentBrowser/Models/ContentItemType.cs)) ;
- mapping extensions → type : `.wav`, `.mp3`, `.ogg` ([ContentItem.cs:223](../../CasaEngine.Editor/ContentBrowser/Models/ContentItem.cs)) ;
- icône `EditorIcons.Volume` et libellé « Sound » ([ContentItemDisplay.cs:23](../../CasaEngine.Editor/ContentBrowser/ContentItemDisplay.cs)) ;
- **import = catalogage automatique** : `FileOperationService` copie le fichier externe puis appelle `EditorAssetImportService.ImportFile` → `EnsureFileAssetRegistered` → création d'un `AssetInfo` avec `AssetType` = extension ([EditorAssetImportService.cs:503](../../CasaEngine.EditorServices/EditorAssetImportService.cs)). Un `.wav` déposé dans le projet **est déjà présent dans `AssetInfos.json` avec un Guid stable**.

Absents :

- aucune vignette (le `ThumbnailCache` ne traite que `Texture`, `Particle`, `Sprite` — [ThumbnailCache.cs:245](../../CasaEngine.Editor/ContentBrowser/Services/ThumbnailCache.cs)) ;
- aucune route d'ouverture de document (`GameEditor.TryOpenEditorAsset` → `GetAssetDocumentRoutes()`, [GameEditor.cs:3821](../../CasaEngine.Editor/GameEditor.cs)) : double-clic sur un son ne fait rien ;
- aucune entrée de menu contextuel « Create Sound… » (le seul mécanisme existant est `RegisterContextMenuExtension`, utilisé pour les particules — [GameEditor.cs:1223](../../CasaEngine.Editor/GameEditor.cs)) ;
- aucune lecture/preview.

### 1.4 Où un système audio se brancherait dans le moteur

Deux emplacements existants, avec deux sémantiques différentes :

| Emplacement | Nature | Exemples actuels |
|---|---|---|
| `CasaEngineGame.Initialize()` | services **globaux au process**, `GameComponent` MonoGame, ordonnés par `ComponentUpdateOrder`/`ComponentDrawOrder` | `InputComponent`, `PhysicsSystemComponent`, renderers ([CasaEngineGame.cs:341](../../CasaEngine/Framework/Application/CasaEngineGame.cs)) |
| `WorldRuntimeSystems` | services **par monde**, avec `Update(FrameTime)` et `Clear()` | `CharacterMotionSystem`, `CoroutineManager`, `CutsceneDirector` ([WorldRuntimeSystems.cs](../../CasaEngine/Framework/Scene/World/WorldRuntimeSystems.cs)) |

Le device audio est unique par process (OpenAL) → le **device et les bus** sont naturellement globaux ; les **voix jouées et les émetteurs** sont naturellement liés au monde (à couper au `World.Clear()`).

Autres points d'ancrage :

- composants d'entité découverts **par réflexion** ([EntityDetailsPanel.cs:1233](../../CasaEngine.Editor/Controls/EntityDetailsPanel.cs)) : un nouveau `EntityComponent` apparaît automatiquement dans « Add Component », et se sérialise via `ElementFactory` (nom de type) ;
- play-in-editor : `EditorPlayModeService` expose `TryStartPlay / TryStopPlay / TryPause / TryResume` ([EditorPlayModeService.cs](../../CasaEngine.EditorServices/PlayMode/EditorPlayModeService.cs)) ;
- `GameSettings` / `ProjectSettings` : aucun réglage audio aujourd'hui ; `DisplaySettingsPersistence` est le modèle existant pour persister des réglages utilisateur en JSON.

### 1.5 Consommateurs futurs déjà identifiés dans la documentation (rien d'implémenté)

- `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md` : `PlaySound`, `PlayMusic`, `StopMusic`, `FadeMusic` listés **hors périmètre V1**, avec la consigne explicite de ne pas inventer d'`AudioSystem` tant qu'il n'existe pas.
- `docs/editor/timeline-generic.md` : « Audio timeline » explicitement **hypothétique** (« aucun asset audio n'existe »).
- `docs/editor/animation2d_editor_casaengine.md:529` : exemple `PlaySound("step")` — les événements d'animation 2D n'ont aujourd'hui que `changeSprite` et `restart` ([Animation2dEventNames.cs](../../CasaEngine/Framework/Assets/Animations/Animation2dEventNames.cs)).

### 1.6 Fichiers audio de test fournis (2026-08-26)

Déposés dans `CasaEngine.Demos/Content/Audio/`. En-têtes RIFF lus directement :

| Fichier | Format | Canaux | Fréquence | Bits | Durée | Taille |
|---|---|---|---|---|---|---|
| `menu_screenclick.wav` | PCM (`tag=1`) | 2 | 44 100 Hz | 16 | ~0,88 s | 152 Ko |
| `RacingGame Game Music 1.wav` | PCM (`tag=1`) | 2 | 22 050 Hz | 16 | ~280,7 s (4 min 41) | 23,6 Mo |

Trois conséquences :

1. **La musique est un WAV, pas un Ogg** → `Song`/`MediaPlayer` (§2.3) ne peut pas la jouer. C'est ce qui a fait réviser la décision D2 (§3).
2. Les deux fichiers sont du **PCM 16 bits**, donc directement compatibles avec le format de buffer attendu par `DynamicSoundEffectInstance` : **aucune conversion de format nécessaire** pour le streaming.
3. Le fichier de musique fait 23,6 Mo et sera recopié dans la sortie de build à chaque compilation (mécanisme `/copy:` de `Content.mgcb`). Le chemin contient des espaces — un renommage sans espace est recommandé (voir le plan de tâches).

---

## 2. Ce que MonoGame DesktopGL 3.8.4.1 fournit réellement

Faits vérifiés dans l'assembly livrée (pas de la documentation en ligne).

### 2.1 Backend

- Plateforme : **DesktopGL** → audio **OpenAL** (`openal.dll` et `SDL2.dll` présents dans `runtimes/win-x64/native`).
- `OpenALSoundController.MAX_NUMBER_OF_SOURCES = 256` (constante interne). Au-delà, `ReserveSource()` lève `InstancePlayLimitException` (type public).
- `FrameworkDispatcher.Update()` est appelé automatiquement par `Game` → le pompage des buffers dynamiques suit la boucle de jeu.

### 2.2 `SoundEffect` — sons courts

- `SoundEffect.FromFile(string)` / `FromStream(Stream)` : **RIFF WAV uniquement**. Formats supportés (doc XML de l'assembly) : PCM 8 bits non signé, PCM 16 bits signé, PCM 24 bits signé, IEEE float 32 bits, MS-ADPCM 4 bits, IMA/ADPCM (IMA4) 4 bits.
- Constructeurs publics à partir d'un `byte[]` PCM + `sampleRate` + `AudioChannels`, avec variante `loopStart`/`loopLength`.
- `SoundEffectInstance` : `Volume`, `Pan`, `Pitch`, `IsLooped`, `State`, `Apply3D(listener, emitter)`. **Pas de fade natif**, pas de position de lecture, pas de seek.
- `SoundEffect.MasterVolume`, `DistanceScale`, `DopplerScale`, `SpeedOfSound` : **statiques globales**.

→ **one-shot** et **loop** sont couverts nativement, à condition que la source soit un WAV.

### 2.3 `Song` / `MediaPlayer` — musique

- `Song.FromUri(string name, Uri uri)` est la **seule** fabrique publique ; les constructeurs `Song(string fileName[, int durationMS])` sont internes.
- Implémentation DesktopGL : `Song.PlatformInitialize(string fileName)` → type interne `OggStream(string filename, Action finishedAction, int bufferCount)`, qui référence **NVorbis** (`VorbisReader`). Le nom de paramètre `oggFileName` est présent dans les métadonnées.
  → **`Song` = fichier Ogg Vorbis sur disque, streamé.** Aucune trace de MP3 ou WMA dans l'assembly.
- `Song.Volume`, `Play`, `Pause`, `Resume`, `Stop` sont **internes** : impossible de contrôler une chanson individuellement depuis le moteur.
- Seul `MediaPlayer` (statique) est public : `Play(Song[, startPosition])`, `Pause`, `Resume`, `Stop`, `Volume`, `IsMuted`, `IsRepeating`, `IsShuffled`, `PlayPosition`, `State`, `Queue`, événements `MediaStateChanged` / `ActiveSongChanged`.

Conséquences directes, **structurantes** :

| Besoin | Faisable avec `MediaPlayer`/`Song` ? |
|---|---|
| Une musique de fond en boucle | ✅ (`IsRepeating`) |
| Volume de la musique séparé des SFX | ✅ (`MediaPlayer.Volume`, global à la musique) |
| **Deux musiques simultanées / crossfade** | ❌ une seule chanson active |
| **Fade in/out** | ⚠️ pas natif — rampe manuelle sur `MediaPlayer.Volume` (volume global de la musique) |
| **Musique routée dans un bus commun avec les SFX** | ❌ `MediaPlayer.Volume` et `SoundEffect.MasterVolume` sont deux chaînes séparées |
| **Formats autres que Ogg Vorbis** | ❌ |
| **Ambiances multiples en streaming** | ❌ |

### 2.4 `DynamicSoundEffectInstance` — streaming maison

- Public : ctor `(int sampleRate, AudioChannels channels)`, `SubmitBuffer(byte[] [,offset,count])`, événement `BufferNeeded`, `PendingBufferCount`, `IsLooped`, hérite de `SoundEffectInstance` (donc `Volume`/`Pan`/`Pitch`/`Apply3D`).
- **NVorbis 0.10.4 est déjà présent** dans l'arbre de dépendances (dépendance transitive de `MonoGame.Framework.DesktopGL`, `NVorbis.dll` est copié dans `bin`). API disponible : `VorbisReader(Stream, bool)`, `ReadSamples(float[], int, int)`, `SeekTo(...)`, `SampleRate`, `Channels`, `TotalTime`, `IsEndOfStream`.

→ Un streaming maison (Ogg → `float[]` → PCM16 → `SubmitBuffer`) donnerait : plusieurs flux simultanés, volume/pan/pitch par flux, fade et crossfade, seek, routage dans les mêmes bus que les SFX. **Coût** : code à écrire et à maintenir (pompage frame ou thread, fin de flux, point de bouclage, buffer underrun).

### 2.5 XACT (`AudioEngine` / `SoundBank` / `WaveBank` / `AudioCategory`)

Présent dans l'assembly, mais nécessite des fichiers `.xgs`/`.xsb`/`.xwb` produits par l'outil XACT (Microsoft, déprécié). Aucun fichier de ce type dans le repo, aucun outil d'authoring intégré. **Non retenu.**

---

## 3. Décisions prises

Décisions arbitrées avec l'auteur du projet le 2026-08-26. Elles fixent le périmètre de la V1.

| # | Sujet | Décision |
|---|---|---|
| D1 | Sémantique de « channel » | **Bus de mixage nommés** (Master / Music / SFX / Voice / UI) : volume, mute, hiérarchie. Pas de pool de voix numérotées à la rétro. |
| D2 | Streaming musique | ~~`MediaPlayer` + `Song`~~ → **révisé le 2026-08-26** (voir D2-bis). |
| D2-bis | Streaming musique (révision) | **Streaming maison dès la V1** : `DynamicSoundEffectInstance` + lecteur **RIFF PCM** écrit dans le moteur. `MediaPlayer`/`Song` est **abandonné** (il ne lit que de l'Ogg, or la musique fournie est un WAV — §1.6). Conséquence positive : crossfade, plusieurs flux et routage dans le bus Music sont possibles dès la V1. Un décodeur Ogg (NVorbis, déjà en dépendance transitive) pourra être branché plus tard **sur la même API**, sans rupture. |
| D3 | Modèle d'asset | **Asset JSON `.sound`** référençant le fichier audio + métadonnées (comme `.texture` référence un `.png`). |
| D4 | Métadonnées `.sound` V1 | Référence fichier (Guid) + **volume** + **pitch** + **loop**, **bus cible**, **mode streaming** explicite. ❌ pas de variations aléatoires en V1. |
| D5 | Spatialisation | **2D seulement** (volume + pan). Pas d'`Apply3D`, pas de listener, pas de Doppler en V1. |
| D6 | Formats | **WAV pour les SFX et — depuis D2-bis — pour la musique.** L'Ogg Vorbis devient une extension ultérieure (décodeur NVorbis branché sur la même API). Le `.mp3` est **retiré** du mapping du Content Browser (aujourd'hui affiché « Sound » alors qu'il est injouable). |
| D7 | Emplacement du système | **Device + bus globaux** (`GameComponent` sur `CasaEngineGame`), **voix rattachées au monde** et coupées par `World.Clear()`. |
| D8 | Périmètre éditeur V1 | **Inspecteur d'asset `.sound`** (document ouvert au double-clic, avec preview) + **menu contextuel « Create Sound »** sur les dossiers. ❌ pas de preview directe dans le Content Browser, ❌ pas de panneau mixer, ❌ pas de forme d'onde. |
| D9 | Code mort existant | **Suppression** de `Sound.cs`, `IAudioEmitter.cs`, `AudioComponent.cs` (aucun appelant dans le repo). |
| D10 | Testabilité | **Abstraction backend `IAudioBackend`** (implémentation OpenAL + fake de test) pour rendre bus, voix, fades et routage testables sans device. Justifiée par CLAUDE.md comme vraie frontière backend. |
| D11 | Consommateurs V1 | **Démo** dans `CasaEngine.Demos` + **commandes de cutscene** (`PlaySound`/`PlayMusic`/`StopMusic`/`FadeMusic`) + **`SoundEmitterComponent`** d'entité. ❌ pas d'événement audio d'animation 2D en V1. |
| D12 | Play-in-editor | **Bus « Editor » séparé** des bus du jeu (volume/mute propres) : la preview de l'inspecteur ne passe jamais par les bus du jeu et survit au `Stop`. |
| D13 | Branche de travail | Tout le chantier est développé sur une branche dédiée **`audio-system`**, un commit par tâche, merge sur `main` après validation. |

---

## 4. Ce qu'il faut construire

Découpage par couche, à la lumière des décisions §3.

### 4.1 Couche « ressource » (runtime, `CasaEngine`)

1. **`SoundAsset`** (`.sound`) : `ObjectBase, ISerializable` dans `CasaEngine/Framework/Audio/`.
   Champs V1 (D4) : Guid du fichier audio, `Volume`, `Pitch`, `IsLooped`, `Bus` (nom), `IsStreaming`.
2. **Extension** `.sound` dans `Constants.FileNameExtensions` ; mapping `.sound` → `ContentItemType.Sound` dans `ContentItem.ExtensionMap` ; **retrait de `.mp3`** du même mapping (D6).
3. **Loaders `IAssetLoader`** enregistrés dans `AssetLoaderRegistry` :
   - `SoundEffect` ← `.wav`, via `SoundEffect.FromStream`, sur le modèle de [Texture2DLoader.cs](../../CasaEngine/Framework/Assets/Loaders/Texture2DLoader.cs) ;
   - `SoundAsset` ← `.sound` (JSON), sur le modèle de `ParticleEffectAssetLoader`.
4. **Sérialisation éditeur** : branche `SoundAsset` dans `EditorAssetJsonSerializer.TrySerialize`.
5. **Politique de cache** : un `SoundEffect` passe par le cache de `AssetContentManager` (déjà `IDisposable` → libéré par `Unload`) ; un asset en mode streaming ne doit **pas** partager une instance de lecture.

### 4.2 Couche « moteur de son » (runtime)

- **`IAudioBackend`** (D10) : frontière minimale (charger un son, jouer/arrêter une voix, régler volume/pan/pitch, jouer/arrêter la musique, volume musique). Implémentation OpenAL/MonoGame + implémentation fake pour les tests.
- **Service audio global** : `GameComponent` créé dans `CasaEngineGame.Initialize` (D7), volume maître, `Update` par frame (fades + recyclage des voix), arrêt global.
- **Bus de mixage** (D1) : arbre de bus nommés, volume et mute, calcul du gain effectif (produit des gains parents). Bus par défaut : Master, Music, SFX, Voice, UI, **+ Editor** (D12).
- **Voix** : suivi des `SoundEffectInstance` actives, arrêt/recyclage en fin de lecture, rattachement au monde courant et coupure sur `World.Clear()` (D7), garde-fou `InstancePlayLimitException` (256 sources) avec log throttlé.
- **API de lecture** : one-shot, loop, musique (streaming), avec volume/pan/pitch et bus cible ; **pas d'API 3D en V1** (D5).
- **Fades** : rampes de volume par frame (rien de natif côté MonoGame). Le streaming maison (D2-bis) donne un `Volume` par flux, donc **fade in/out et crossfade sont possibles en V1**.
- **Contraintes hot path** (CLAUDE.md) : pas de LINQ, pas d'allocation par frame, listes réutilisées avec `Clear()`.

### 4.3 Musique — streaming maison (D2-bis)

- **Lecteur RIFF PCM** : parcours des chunks (`fmt `, `data`), exposition de `SampleRate`/`Channels`/`BitsPerSample`, lecture par blocs, rembobinage pour la boucle. Logique **pure**, testable sans device.
- **Voix de streaming** : `DynamicSoundEffectInstance(sampleRate, channels)`, file de buffers PCM16 alimentée depuis le lecteur, remplissage piloté par l'`Update` du système audio (`PendingBufferCount`), buffers **poolés** (pas d'allocation par frame).
- **Bouclage** géré par le lecteur (rembobinage au début du chunk `data`), **pas** par `DynamicSoundEffectInstance.IsLooped` (sémantique XNA : le setter à `true` lève `InvalidOperationException` — à confirmer à l'implémentation).
- **Portée V1** : streaming du **PCM 16 bits** uniquement (c'est le format des deux fichiers fournis et celui attendu par `SubmitBuffer`). Les autres formats WAV (8/24 bits, float, ADPCM) restent lisibles en mode non-streamé via `SoundEffect.FromStream`.
- L'API moteur expose une **piste musicale** (identifiant, play/stop/pause, fade, crossfade) sans exposer le type MonoGame sous-jacent : brancher un décodeur Ogg (NVorbis) plus tard ne changera que l'implémentation du lecteur.

### 4.4 Couche « gameplay » (D11)

- **`SoundEmitterComponent`** (`EntityComponent`) : asset `.sound`, `PlayOnStart`, loop, bus, volume/pitch de surcharge. Apparaîtra automatiquement dans « Add Component » (réflexion) ; sérialisation via `ElementFactory` + complément dans `EditorEntityJsonSerializer`.
- **Commandes de cutscene** : `PlaySound`, `PlayMusic`, `StopMusic`, `FadeMusic`, sur le modèle des commandes existantes du `CutsceneDirector`.

### 4.5 Couche éditeur (D8)

- **Inspecteur d'asset `.sound`** : route dans `GameEditor.GetAssetDocumentRoutes()` + panneau document sur le modèle de `ParticleAssetInspectorPanel` — édition des champs D4 et **preview Play/Stop routée sur le bus Editor** (D12).
- **Menu contextuel « Create Sound »** sur les dossiers, via `RegisterContextMenuExtension` (mécanisme déjà utilisé pour les particules).
- **Séparation runtime/éditeur** (CLAUDE.md) : lecture et bus dans `CasaEngine` ; création/sauvegarde de l'asset dans `CasaEngine.EditorServices`.

### 4.6 Tests, doc, sample

- **Tests** (`CasaEngine.Tests`, xUnit) avec le backend fake (D10) : gain effectif des bus, mute, fades, cycle de vie des voix, coupure au `World.Clear()`, parsing/sérialisation aller-retour du `.sound`.
- **Fichiers audio de test** : le repo n'en contient **aucun** — voir point de blocage §5.4.
- **Doc** : `docs/engine/audio-system.md`.
- **Sample** : `AudioDemo` dans `CasaEngine.Demos` (`DemosGame`), conformément à AGENTS.md règle 5.

---

## 5. Points de blocage et risques

### 5.1 Bloquant dur — MP3 impossible

MonoGame DesktopGL 3.8.4.1 **ne lit pas de MP3** (ni `SoundEffect`, ni `Song`). Décision D6 : le `.mp3` sort du mapping du Content Browser. **Conséquence contenu** : toute source MP3 devra être convertie en `.wav` avant import — il n'y a pas de conversion automatique dans le moteur.

### 5.2 Risque déplacé — le streaming est désormais du code maison (D2-bis)

`MediaPlayer`/`Song` est écarté (une seule musique, Ogg uniquement, contrôles `internal`). Le streaming maison lève ces limites mais introduit ses propres risques, à traiter dans le plan :

- **Underrun** : si une frame est longue (compilation de shader, chargement de monde), la file de buffers se vide et la musique hoquette. Mitigation : file suffisamment profonde (plusieurs centaines de ms) et remplissage dès que `PendingBufferCount` descend sous un seuil.
- **I/O sur le thread de jeu** : lire le WAV par blocs dans l'`Update` fait de la lecture disque synchrone dans la boucle. À 22 050 Hz stéréo 16 bits c'est ~88 Ko/s, donc marginal — mais c'est un choix à documenter, avec le passage à un producteur en tâche de fond comme évolution possible.
- **Fin de flux et boucle** : le rembobinage doit être sans discontinuité audible (pas de buffer partiel soumis avant le rebouclage).

### 5.3 Résolu par D10 — pas de device audio en CI

`SoundEffect`, `SoundEffectInstance` et `DynamicSoundEffectInstance` exigent un contexte OpenAL initialisé, indisponible en CI headless. L'abstraction `IAudioBackend` (D10) lève le blocage pour la logique (bus, voix, fades) ; **la lecture réelle restera non couverte automatiquement** et devra être validée par la démo et l'éditeur à la main.

### 5.4 Levé le 2026-08-26 — fichiers audio de test fournis

Deux WAV PCM 16 bits sont désormais dans `CasaEngine.Demos/Content/Audio` (§1.6) : un SFX court et une musique de 4 min 41. Ils couvrent les deux chemins de lecture de la V1 (non-streamé et streamé).

Restent à traiter dans le plan : renommage du fichier de musique (espaces dans le nom), entrées `/copy:` dans `Content.mgcb`, entrées dans `AssetInfos.json`, et coût de la recopie de 23,6 Mo à chaque build.

### 5.5 Risque — cohabitation avec le pipeline MGCB

Le moteur a deux chemins de contenu : `Content.mgcb` (shaders, polices) et le système d'assets JSON (`AssetInfos.json`). Le code mort `AudioComponent` utilisait le premier ; tout le reste du moteur utilise le second. Les décisions D3/D9 tranchent : **les sons passent par le système d'assets JSON**, jamais par MGCB.

### 5.6 Traité par D12 — sons de l'éditeur et sons du jeu

L'éditeur (`GameEditor : Game`) et le jeu tournent dans le **même process** avec le **même device**. Le bus « Editor » (D12) isole la preview. Restent à définir au moment du plan de tâches : le comportement du bus Editor **pendant** une session de play-in-editor (coupé ? atténué ? inchangé ?), et l'arrêt des voix du jeu au `TryStopPlay` / la mise en pause au `TryPause`.

### 5.7 Risque — limite de 256 sources OpenAL

`InstancePlayLimitException` est levée quand les 256 sources sont épuisées. Sans limite de voix ni politique de remplacement, un jeu qui spamme des one-shots plantera. À traiter dès la V1 : limite de voix par bus ou globale, plus capture de l'exception avec log **throttlé** (pas de spam par frame — CLAUDE.md).

### 5.8 Points restant à préciser au moment du plan de tâches

- Liste exacte et éventuelle configurabilité des bus par défaut (en dur dans le moteur, ou déclarés dans les réglages du projet ?).
- Persistance des volumes utilisateur (le modèle existant est `DisplaySettingsPersistence`) — non tranché.
- Nommage exact des types publics (`SoundAsset`, `AudioSystemComponent`, `SoundEmitterComponent`, `IAudioBackend`…).
- Comportement attendu quand un `.sound` référence un fichier absent ou d'un format non supporté (échec dur au chargement vs son silencieux + log).

---

## 6. Plan de tâches

Le découpage détaillé, tâche par tâche, avec critères de « done », commits et statuts, vit dans
[ai-agent/tasks/audio-system-tasks.md](../tasks/audio-system-tasks.md).

---

## Annexe — méthode de vérification

- Lecture statique du code au commit `6384bf4d`.
- Inspection des métadonnées de `MonoGame.Framework.dll` (DesktopGL 3.8.4.1) et `NVorbis.dll` (0.10.4) via `System.Reflection.MetadataLoadContext` (signatures publiques **et** internes, constantes).
- Lecture de la documentation XML livrée avec `MonoGame.Framework.dll` (formats WAV supportés).
- Vérification des natifs livrés (`runtimes/win-x64/native` : `openal.dll`, `SDL2.dll`) et de la présence de `NVorbis.dll` dans la sortie de build de l'éditeur.
- **Aucune exécution du moteur ni test audio runtime.** Les comportements décrits pour `Song`/`OggStream` sont déduits des métadonnées et restent à confirmer par un smoke test.
