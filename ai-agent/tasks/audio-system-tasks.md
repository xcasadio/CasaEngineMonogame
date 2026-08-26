# Moteur de son — plan agent IA

Plan d'exécution du chantier audio décrit dans [analysis-audio-system.md](../audits/analysis-audio-system.md).
Toutes les décisions d'architecture (D1 → D13) sont figées dans le §3 de cette analyse : **ce plan les applique, il ne les rediscute pas**.

## Règles d'exécution

- **Branche dédiée `audio-system`**, créée depuis `main` (D13). Ne jamais commiter sur `main`.
- **Un commit par tâche terminée**, atomique et compilable. Le message de commit suggéré est donné dans chaque tâche.
- **Mettre à jour l'icône de statut de la tâche dans ce fichier**, et inclure cette mise à jour **dans le commit de la tâche**.
- Statuts autorisés : ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked.
- Passer une tâche en 🚧 **avant** de commencer, en ✅ (ou 🧪 si une validation humaine à l'oreille est requise) quand elle est finie.
- **Build de la solution obligatoire** avant de considérer une tâche terminée (`dotnet build CasaEngine.MonoGame.sln`).
- **Tests** : lancer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` dès qu'une tâche touche du code testé.
- **Ne rien inventer** : toute API utilisée doit exister dans le code ou être créée par une tâche explicite de ce plan. Si une tâche est bloquée par une information manquante, la passer en ⚠️ Blocked et l'écrire dans « Points ouverts ».
- **Hot path** (`Update`, remplissage de buffers, mixage) : pas de LINQ, pas de closure, pas d'allocation par frame, listes réutilisées avec `Clear()` (CLAUDE.md).
- **Séparation runtime/éditeur** : la lecture et le mixage vivent dans `CasaEngine` ; la création et la sauvegarde d'assets dans `CasaEngine.EditorServices` ; l'UI dans `CasaEngine.Editor`.

## Rappel des décisions appliquées

| Réf | Décision |
|---|---|
| D1 | « Channel » = **bus de mixage nommés** (Master / Music / SFX / Voice / UI). |
| D2-bis | Musique = **streaming maison** `DynamicSoundEffectInstance` + lecteur RIFF PCM. `MediaPlayer`/`Song` **abandonné**. |
| D3 / D4 | Asset **JSON `.sound`** : fichier audio (Guid) + `Volume` + `Pitch` + `IsLooped` + `Bus` + `IsStreaming`. |
| D5 | **2D uniquement** (volume + pan). Pas d'`Apply3D`, pas de listener. |
| D6 | **WAV** pour SFX et musique. `.mp3` retiré du mapping Content Browser. Ogg = évolution ultérieure. |
| D7 | **Device + bus globaux** (`GameComponent`), **voix rattachées au monde** et coupées par `World.Clear()`. |
| D8 | Éditeur V1 = **inspecteur `.sound`** (avec preview) + **menu « Create Sound »**. Pas de preview dans le Content Browser, pas de panneau mixer. |
| D9 | **Supprimer** `Sound.cs`, `IAudioEmitter.cs`, `AudioComponent.cs`. |
| D10 | **`IAudioBackend`** + implémentation MonoGame + fake de test. |
| D11 | Consommateurs V1 : **démo**, **commandes de cutscene**, **`SoundEmitterComponent`**. Pas d'événement audio d'animation 2D. |
| D12 | **Bus « Editor »** séparé des bus du jeu. |
| D13 | Branche `audio-system`, un commit par tâche. |

## Nommage retenu

À utiliser tel quel, pour éviter toute divergence entre tâches.

| Élément | Emplacement |
|---|---|
| `IAudioBackend`, `AudioVoiceHandle`, `AudioVoiceParameters` | `CasaEngine/Framework/Audio/` |
| `MonoGameAudioBackend` | `CasaEngine/Framework/Audio/Backends/` |
| `FakeAudioBackend` | `CasaEngine.Tests/Audio/` |
| `AudioBus`, `AudioMixer`, `AudioBusNames` | `CasaEngine/Framework/Audio/Mixing/` |
| `SoundAsset` | `CasaEngine/Framework/Audio/SoundAsset.cs` |
| `WavStreamReader`, `WavFormatInfo` | `CasaEngine/Framework/Audio/Streaming/` |
| `MusicTrackHandle`, `MusicPlayer` | `CasaEngine/Framework/Audio/Streaming/` |
| `AudioService` | `CasaEngine/Framework/Audio/AudioService.cs` — ajouté pendant V3.2 : toute la logique (pool de voix, routage, fades, musique) y vit **sans aucun type MonoGame ni `Game`**, sinon rien n'aurait été testable (D10). |
| `AudioSystemComponent` | `CasaEngine/Framework/Application/Components/` — enveloppe mince qui pilote `AudioService` depuis la boucle de jeu |
| `SoundEffectLoader`, `SoundAssetLoader` | `CasaEngine/Framework/Assets/Loaders/` |
| `SoundEmitterComponent` | `CasaEngine/Framework/Scene/Entities/Components/` |
| `SoundAssetInspectorPanel` | `CasaEngine.Editor/Controls/` |
| Tests | `CasaEngine.Tests/Audio/` |
| Extension | `Constants.FileNameExtensions.Sound = ".sound"` |

---

## Phase 0 — Préparation

### ✅ Done - P0.1. Créer la branche et committer le plan

**Objectif** : ouvrir le chantier sur une branche isolée.

**Livrables** :

- branche `audio-system` créée depuis `main` ;
- `ai-agent/audits/analysis-audio-system.md` et `ai-agent/tasks/audio-system-tasks.md` commités.

**Validation** :

- `git branch --show-current` retourne `audio-system` ;
- les deux fichiers markdown sont dans l'index et sans diagnostic markdown.

**Commit** : `docs(audio): add audio system analysis and agent task plan`

---

### ✅ Done - P0.2. Intégrer les fichiers audio de test au projet Demos

**Objectif** : rendre les deux WAV fournis chargeables au runtime par le projet de démos.

**Contexte vérifié** : `CasaEngine.Demos/Content/` **est un projet éditeur** (`DemosGame.json` + `AssetInfos.json`, 287 entrées). Les fichiers sont recopiés vers la sortie de build par les entrées `/copy:` de `CasaEngine.Demos/Content/Content.mgcb`.

**Fichiers présents** :

- `CasaEngine.Demos/Content/Audio/menu_screenclick.wav` — PCM 16 bits, 2 canaux, 44 100 Hz, ~0,88 s ;
- `CasaEngine.Demos/Content/Audio/RacingGame Game Music 1.wav` — PCM 16 bits, 2 canaux, 22 050 Hz, ~280,7 s, 23,6 Mo.

**Livrables** :

- renommer `RacingGame Game Music 1.wav` en `racing_game_music_1.wav` (le nom actuel contient des espaces — cohérence avec le reste du contenu). Utiliser `git mv` ;
- ajouter les deux entrées `#begin Audio/… ` / `/copy:Audio/…` dans `CasaEngine.Demos/Content/Content.mgcb` ;
- ajouter les deux `asset_infos` correspondants dans `CasaEngine.Demos/Content/AssetInfos.json` (Guid neufs, `file_name` avec `\\` comme séparateur, à l'identique des entrées existantes).

**Validation** :

- build de `CasaEngine.Demos` ;
- les deux `.wav` sont présents dans `CasaEngine.Demos/bin/<config>/<tfm>/Content/Audio/` après build ;
- `AssetInfos.json` reste un JSON valide et le nombre d'entrées passe de 287 à 289.

**Commit** : `chore(demos): register the test audio files in the demos content project`

---

### ✅ Done - P0.3. Supprimer le code audio mort (D9)

**Objectif** : partir d'une base propre, sans deux modèles audio concurrents.

**Fichiers supprimés** :

- `CasaEngine/Framework/Audio/Sound.cs` ;
- `CasaEngine/Framework/Audio/IAudioEmitter.cs` ;
- `CasaEngine/Framework/Application/Components/AudioComponent.cs`.

**Vérification préalable obligatoire** : `rg "AudioComponent|IAudioEmitter|Framework\.Audio\.Sound" --glob "*.cs" --glob "!bin/**" --glob "!obj/**"` ne doit remonter que ces trois fichiers. Si un appelant apparaît, passer la tâche en ⚠️ Blocked.

**Validation** :

- build de la solution ;
- `dotnet test` inchangé (aucun test ne référençait ces types).

**Commit** : `chore(audio): remove the dead audio prototype (Sound, IAudioEmitter, AudioComponent)`

---

## Phase 1 — Socle backend testable (D10)

### ✅ Done - S1.1. Définir les contrats du backend audio

**Objectif** : poser la frontière backend avant toute implémentation, pour que la logique de mixage soit testable sans OpenAL.

**Livrables** (`CasaEngine/Framework/Audio/`) :

- `AudioVoiceHandle` : `readonly struct` (index + génération) avec `IsValid`, égalité, `None` — permet de détecter un handle périmé après recyclage ;
- `AudioVoiceParameters` : `struct` (`Volume`, `Pan`, `Pitch`, `IsLooped`) avec valeurs par défaut neutres (1, 0, 0, false) et bornage documenté (`Volume` 0..1, `Pan` -1..1, `Pitch` -1..1 — bornes de `SoundEffectInstance`) ;
- `IAudioBackend` : créer/démarrer/arrêter/mettre en pause une voix non-streamée, appliquer des `AudioVoiceParameters`, interroger l'état d'une voix, libérer une voix. **Aucun type MonoGame ne doit apparaître dans la signature.**

**Contraintes** :

- pas de méthode 3D (D5) ;
- l'interface ne connaît ni bus ni asset : elle ne parle que de voix et de paramètres bruts.

**Validation** : build ; aucun `using Microsoft.Xna.Framework.Audio` dans les fichiers de contrat.

**Commit** : `feat(audio): add the audio backend contracts (IAudioBackend, voice handle, voice parameters)`

---

### ✅ Done - S1.2. Implémenter le backend MonoGame

**Objectif** : implémenter `IAudioBackend` sur `SoundEffect` / `SoundEffectInstance`.

**Livrables** (`CasaEngine/Framework/Audio/Backends/MonoGameAudioBackend.cs`) :

- table de voix indexée avec génération, réutilisation des entrées libérées, **aucune allocation** lors d'un `Play` sur une voix recyclée ;
- application de `Volume`/`Pan`/`Pitch`/`IsLooped` sur `SoundEffectInstance` ;
- capture de `InstancePlayLimitException` (limite de 256 sources OpenAL) : la voix est refusée proprement, `AudioVoiceHandle.None` est retourné, et un **log throttlé** est émis (pas un log par frame) ;
- capture de `NoAudioHardwareException` à l'initialisation : le backend passe en mode « muet » et le jeu continue.

**Validation** : build ; relecture ciblée des chemins d'allocation.

**Commit** : `feat(audio): implement the MonoGame/OpenAL audio backend`

---

### ✅ Done - S1.3. Ajouter le backend fake et le harnais de test

**Objectif** : rendre toute la logique audio testable en CI sans device.

**Livrables** (`CasaEngine.Tests/Audio/`) :

- `FakeAudioBackend` : enregistre les appels (voix créées, arrêtées, paramètres appliqués), permet de simuler la fin d'une voix et l'épuisement des sources ;
- premiers tests : cycle de vie d'une voix, invalidation d'un handle après libération, refus quand le backend est saturé.

**Validation** : `dotnet test` vert.

**Commit** : `test(audio): add a fake audio backend and voice lifecycle tests`

---

## Phase 2 — Bus de mixage (D1)

### ✅ Done - B2.1. Implémenter les bus et le calcul de gain

**Objectif** : le cœur métier des « channels ».

**Livrables** (`CasaEngine/Framework/Audio/Mixing/`) :

- `AudioBus` : nom, `Volume` (0..1, borné), `IsMuted`, parent optionnel ;
- `AudioMixer` : enregistrement/lookup des bus par nom, calcul du **gain effectif** (produit des gains de la chaîne jusqu'au Master, 0 si un ancêtre est muet), détection des cycles et des noms dupliqués ;
- invalidation : un changement de volume/mute doit permettre au système de réappliquer le gain aux voix actives (événement ou numéro de version, **pas** de recalcul complet par frame).

**Tests** (`CasaEngine.Tests/Audio/AudioMixerTests.cs`) :

- gain effectif d'un bus enfant = produit des volumes ;
- mute d'un parent force le gain à 0 sans écraser le volume de l'enfant ;
- bornage des volumes hors [0,1] ;
- nom dupliqué et cycle de parenté rejetés.

**Validation** : `dotnet test` vert ; build.

**Commit** : `feat(audio): add named mixing buses with effective gain computation`

---

### ✅ Done - B2.2. Déclarer les bus par défaut

**Objectif** : fixer la hiérarchie standard du moteur.

**Livrables** :

- `AudioBusNames` : `Master`, `Music`, `Sfx`, `Voice`, `Ui`, `Editor` (constantes `string`) ;
- construction par défaut : `Music`, `Sfx`, `Voice`, `Ui` et `Editor` enfants de `Master` ;
- **`Editor` est déclaré ici** mais son usage (preview, règles play-in-editor) est traité en phase 8 (D12).

**Tests** : la hiérarchie par défaut existe, `Master` à 1.0 propage 1.0, `Master` muet coupe tout y compris `Editor`.

**Validation** : `dotnet test` vert.

**Commit** : `feat(audio): declare the default mixing bus hierarchy`

---

## Phase 3 — Service audio et voix (D7)

### ✅ Done - V3.1. Brancher `AudioSystemComponent` dans le moteur

**Objectif** : donner au moteur un point d'entrée audio unique.

**Livrables** :

- `CasaEngine/Framework/Application/Components/AudioSystemComponent.cs` : `GameComponent`, possède le `IAudioBackend` et l'`AudioMixer`, expose `Mixer`, un volume maître, `Update(GameTime)` et `StopAll()` ;
- instanciation dans `CasaEngineGame.Initialize()` à côté des autres systèmes (`CasaEngine/Framework/Application/CasaEngineGame.cs`, bloc ~ligne 341) et propriété publique `AudioSystemComponent` sur `CasaEngineGame` ;
- valeur d'`UpdateOrder` alimentée par `ComponentUpdateOrder` comme les autres systèmes (`UpdateOrder = (int)ComponentUpdateOrder.Audio;`, cf. `PhysicsSystemComponent.cs:16`). Ajouter `Audio` **en fin** de l'énumération `ComponentUpdateOrder` pour ne pas décaler les valeurs existantes : une mise à jour audio en fin de frame est sans conséquence (elle ne fait que pomper des buffers et avancer des rampes).

**Contraintes** : l'échec d'initialisation audio (pas de device) ne doit **jamais** empêcher le jeu de démarrer.

**Validation** : build ; lancer `CasaEngine.Demos` et vérifier qu'aucune régression de démarrage n'apparaît.

**Commit** : `feat(audio): add AudioSystemComponent and wire it into CasaEngineGame`

---

### ✅ Done - V3.2. Implémenter le pool de voix

**Objectif** : borner et recycler les voix, sans allocation par frame.

**Livrables** :

- pool interne à `AudioSystemComponent` : liste réutilisée de voix actives (asset joué, bus, paramètres demandés, handle backend) ;
- balayage par frame : les voix terminées sont libérées côté backend et rendues au pool ;
- **limite de voix** configurable (globale, valeur par défaut nettement sous les 256 sources OpenAL — proposer 64) ; au-delà, la nouvelle demande est refusée et loggée de façon throttlée ;
- réapplication du gain effectif quand un bus change de volume/mute.

**Tests** (sur `FakeAudioBackend`) :

- une voix terminée est libérée et son handle devient invalide ;
- la limite de voix refuse la voix surnuméraire sans exception ;
- un changement de volume de bus réapplique le gain sur les voix actives du bus, et seulement celles-là ;
- aucune allocation dans le chemin `Update` (vérification par relecture, documentée dans le test).

**Validation** : `dotnet test` vert.

**Commit** : `feat(audio): add a recycling voice pool with a global voice limit`

---

### ✅ Done - V3.3. Rattacher les voix au monde

**Objectif** : qu'un changement de monde ne laisse pas de son orphelin (D7).

**Livrables** :

- notion de « propriétaire » d'une voix (monde courant vs global/éditeur) ;
- coupure des voix du monde dans `World.Clear()` (`CasaEngine/Framework/Scene/World/World.cs:107`) ou via `WorldRuntimeSystems.Clear()` — choisir le point qui n'introduit **pas** de dépendance du monde vers `CasaEngineGame` si elle n'existe pas déjà (`World.Game` existe, donc la voie directe est acceptable) ;
- les voix marquées globales/éditeur survivent.

**Tests** : après un `Clear()` simulé, les voix du monde sont arrêtées et les voix globales intactes.

**Validation** : `dotnet test` vert ; build.

**Commit** : `feat(audio): scope voices to the current world and stop them on World.Clear()`

---

### ✅ Done - V3.4. Implémenter les fades

**Objectif** : fade in/out réutilisable par les SFX et par la musique.

**Livrables** :

- rampe de volume par voix : volume de départ, cible, durée, action de fin (`None` / `Stop`) ;
- avancement dans `AudioSystemComponent.Update` à partir du temps écoulé, sans allocation ;
- API : démarrer un fade sur une voix, l'annuler, interroger s'il est en cours.

**Tests** :

- une rampe atteint exactement la cible à la fin de la durée ;
- une durée nulle applique la cible immédiatement ;
- `Stop` en fin de fade libère la voix ;
- un second fade sur la même voix remplace le premier en partant du volume courant.

**Validation** : `dotnet test` vert.

**Commit** : `feat(audio): add per-voice volume fades`

---

## Phase 4 — Assets (D3, D4, D6)

### ⏳ Todo - A4.1. Ajouter le loader de `SoundEffect` (`.wav`)

**Objectif** : charger un WAV via le système d'assets JSON du moteur, pas via MGCB.

**Livrables** :

- `CasaEngine/Framework/Assets/Loaders/SoundEffectLoader.cs` : `IAssetLoader`, `LoadAsset` via `SoundEffect.FromStream`, `IsFileSupported` sur `.wav`. Modèle : `Texture2DLoader.cs` ;
- enregistrement dans `AssetLoaderRegistry.RegisterLoaders` : `assetContentManager.RegisterAssetLoader(typeof(SoundEffect), new SoundEffectLoader());` ;
- erreur de chargement loggée avec le nom de fichier (pas d'exception silencieuse).

**Validation** : build ; test qui charge le WAV de test **si** un device est disponible, sinon test limité à `IsFileSupported`.

**Commit** : `feat(assets): register a SoundEffect loader for .wav files`

---

### ⏳ Todo - A4.2. Créer l'asset `.sound`

**Objectif** : le type d'asset son du moteur (D3, D4).

**Livrables** :

- `Constants.FileNameExtensions.Sound = ".sound"` dans `CasaEngine/Framework/Configuration/Constants.cs` ;
- `CasaEngine/Framework/Audio/SoundAsset.cs` : `ObjectBase, ISerializable`, propriétés `AudioFileAssetId` (Guid), `Volume`, `Pitch`, `IsLooped`, `BusName`, `IsStreaming` ; `Load(JObject)` tolérant aux champs absents (valeurs par défaut), validation des bornes ;
- `CasaEngine/Framework/Assets/Loaders/SoundAssetLoader.cs` sur le modèle de `ParticleEffectAssetLoader.cs` ;
- enregistrement dans `AssetLoaderRegistry`.

**Noms de champs JSON** : `audio_file_asset_id`, `volume`, `pitch`, `is_looped`, `bus_name`, `is_streaming` (snake_case, comme le reste des assets).

**Tests** : `Load` d'un JSON complet, d'un JSON minimal (défauts appliqués), rejet d'un `volume` hors bornes.

**Validation** : `dotnet test` vert ; build.

**Commit** : `feat(assets): add the .sound asset type and its loader`

---

### ⏳ Todo - A4.3. Sérialiser l'asset `.sound` côté éditeur

**Objectif** : pouvoir écrire un `.sound` depuis l'éditeur.

**Livrables** :

- branche `case SoundAsset soundAsset:` dans `EditorAssetJsonSerializer.TrySerialize` (`CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`, switch ~ligne 27) + méthode `SaveSoundAsset` ;
- valeur `SoundEditorPanel = 6` dans l'énumération `EditorAssetSaveSource` (`CasaEngine.EditorServices/EditorAssetWriterService.cs:9`).

**Tests** : aller-retour `SoundAsset` → JSON → `Load` conservant tous les champs (test dans `CasaEngine.Tests/Audio/`).

**Validation** : `dotnet test` vert.

**Commit** : `feat(editor-services): serialize SoundAsset documents`

---

### ⏳ Todo - A4.4. Mettre à jour le Content Browser

**Objectif** : que `.sound` soit reconnu et que `.mp3` cesse d'être annoncé comme jouable (D6).

**Livrables** (`CasaEngine.Editor/ContentBrowser/Models/ContentItem.cs`, table `ExtensionMap` ~ligne 208) :

- ajout de `{ ".sound", ContentItemType.Sound }` ;
- **suppression** de `{ ".mp3", ContentItemType.Sound }` ;
- `.wav` et `.ogg` conservés.

**Tests** : compléter `CasaEngine.Tests/ContentBrowser/ContentItemTests.cs` — `.sound` et `.wav` donnent `Sound`, `.mp3` donne `Unknown`.

**Validation** : `dotnet test` vert.

**Commit** : `feat(editor): map .sound in the content browser and drop unsupported .mp3`

---

## Phase 5 — Lecture des SFX (D5 : 2D uniquement)

### ⏳ Todo - L5.1. Exposer l'API de lecture des sons

**Objectif** : jouer un `.sound` en one-shot ou en boucle.

**Livrables** sur `AudioSystemComponent` :

- lecture par `SoundAsset` et par Guid d'asset (résolution via `AssetContentManager`) ;
- surcharges de `Volume`, `Pan`, `Pitch`, bus cible et bouclage par rapport aux valeurs de l'asset ;
- retour d'un `AudioVoiceHandle` permettant `Stop`, `StopWithFade`, changement de volume ;
- un asset dont `IsStreaming` est vrai est **refusé** ici avec un message clair (il relève de la phase 6) ;
- fichier audio absent ou format non supporté : log d'erreur explicite et voix silencieuse, **pas** d'exception qui remonte au gameplay.

**Tests** (sur `FakeAudioBackend`) : les valeurs de l'asset sont appliquées, les surcharges gagnent, le gain du bus est combiné au volume de l'asset, un handle arrêté devient invalide.

**Validation** : `dotnet test` vert.

**Commit** : `feat(audio): add one-shot and looping sound playback on top of SoundAsset`

---

### ⏳ Todo - L5.2. Créer `AudioDemo` (SFX)

**Objectif** : première validation à l'oreille.

**Livrables** :

- `CasaEngine.Demos/Demos/AudioDemo.cs` sur le modèle des démos existantes, enregistré dans `CasaEngine.Demos/DemosGame.cs` (liste `_demos`) ;
- `.sound` de test référençant `menu_screenclick.wav`, créé dans `CasaEngine.Demos/Content/Audio/` et déclaré dans `AssetInfos.json` + `Content.mgcb` ;
- interactions clavier : jouer le SFX en one-shot, le jouer en boucle, l'arrêter, monter/baisser le volume du bus `Sfx` et du bus `Master`, couper/rétablir le mute ;
- affichage à l'écran des volumes de bus et du nombre de voix actives.

**Validation** :

- build et lancement de `CasaEngine.Demos` ;
- 🧪 **validation humaine requise** : le son sort, la boucle boucle, les volumes de bus agissent, le mute du Master coupe tout.

**Commit** : `feat(demos): add AudioDemo with SFX playback and bus controls`

---

## Phase 6 — Streaming de la musique (D2-bis)

### ⏳ Todo - M6.1. Écrire le lecteur WAV en streaming

**Objectif** : lire un WAV par blocs, sans le charger entièrement.

**Livrables** (`CasaEngine/Framework/Audio/Streaming/`) :

- `WavFormatInfo` : `SampleRate`, `Channels`, `BitsPerSample`, `BlockAlign`, `DataOffset`, `DataLength` ;
- `WavStreamReader` : ouverture d'un `Stream`, parcours des chunks RIFF (`fmt `, `data`, chunks inconnus sautés, **taille de chunk `fmt ` de 16 ou 18 octets acceptée** — le fichier de musique fourni a un `fmt ` de 18 octets), lecture dans un `byte[]` fourni par l'appelant (pas d'allocation interne par appel), `Rewind()` au début du chunk `data`, `IsEndOfStream` ;
- **portée V1** : PCM 16 bits uniquement ; tout autre format lève une exception explicite à l'ouverture (message indiquant le format lu et le format attendu).

**Tests** (`CasaEngine.Tests/Audio/WavStreamReaderTests.cs`) : construire des WAV en mémoire —

- en-tête `fmt ` de 16 et de 18 octets acceptés ;
- chunk inconnu (`LIST`) entre `fmt ` et `data` correctement sauté ;
- lecture séquentielle complète, `IsEndOfStream` en fin ;
- `Rewind()` redonne exactement les mêmes octets ;
- rejet d'un WAV 8 bits et d'un WAV IEEE float avec un message explicite ;
- rejet d'un fichier non-RIFF.

**Validation** : `dotnet test` vert. Ces tests **ne nécessitent aucun device audio**.

**Commit** : `feat(audio): add a streaming RIFF PCM16 wav reader`

---

### ⏳ Todo - M6.2. Ajouter la voix de streaming au backend

**Objectif** : alimenter un `DynamicSoundEffectInstance` sans allouer par frame.

**Livrables** :

- extension de `IAudioBackend` avec la création d'une voix de streaming (`sampleRate`, `channels`), la soumission d'un buffer, la lecture de `PendingBufferCount`, l'arrêt et la libération ;
- implémentation dans `MonoGameAudioBackend` via `DynamicSoundEffectInstance` ;
- **pool de buffers** `byte[]` réutilisés (taille de buffer et profondeur de file constantes, documentées ; viser plusieurs centaines de ms de marge) ;
- le bouclage est géré par le lecteur (`WavStreamReader.Rewind()`), **jamais** par `DynamicSoundEffectInstance.IsLooped` — vérifier au passage le comportement réel du setter et le noter dans « Points ouverts » ;
- implémentation correspondante dans `FakeAudioBackend` (compte les buffers soumis, simule la consommation).

**Validation** : `dotnet test` vert ; build.

**Commit** : `feat(audio): add streaming voices backed by DynamicSoundEffectInstance`

---

### ⏳ Todo - M6.3. Implémenter le lecteur de musique

**Objectif** : l'API musique du moteur, indépendante du format source.

**Livrables** (`CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs` + `MusicTrackHandle`) :

- démarrer une piste depuis un `SoundAsset` en mode streaming, l'arrêter, la mettre en pause/reprendre ;
- **plusieurs pistes simultanées** (au minimum deux, pour le crossfade) ;
- bouclage sans discontinuité (pas de buffer partiel soumis avant le rebouclage) ;
- fade in / fade out réutilisant la rampe de V3.4 ;
- `Crossfade(trackA → assetB, durée)` : démarre B en fade in pendant que A fait un fade out avec `Stop` en fin ;
- volume final = volume de la piste × gain du bus `Music` ;
- remplissage piloté depuis `AudioSystemComponent.Update` : tant que `PendingBufferCount` est sous le seuil, lire un bloc et le soumettre. Documenter dans le code que la lecture disque se fait sur le thread de jeu et pourquoi c'est acceptable ici (~88 Ko/s pour le fichier de test).

**Tests** (sur `FakeAudioBackend`, sans device) :

- une piste rejouée en boucle rembobine le lecteur au lieu de s'arrêter ;
- le crossfade laisse exactement deux pistes actives pendant la transition, puis une seule ;
- la piste sortante est libérée en fin de fade ;
- un `Stop` pendant un fade libère immédiatement.

**Validation** : `dotnet test` vert.

**Commit** : `feat(audio): add streaming music playback with fades and crossfade`

---

### ⏳ Todo - M6.4. Étendre `AudioDemo` à la musique

**Objectif** : validation à l'oreille du streaming.

**Livrables** :

- `.sound` de test référençant `racing_game_music_1.wav` avec `IsStreaming = true`, `IsLooped = true`, `BusName = Music` ;
- touches de la démo : démarrer/arrêter la musique, fade in/out, crossfade vers une seconde piste (réutiliser le SFX en boucle si aucun second morceau n'est disponible), régler le volume du bus `Music` ;
- affichage du temps écoulé de la piste et du nombre de buffers en attente.

**Validation** :

- build et lancement ;
- 🧪 **validation humaine requise** : lecture continue sans hoquet sur plusieurs minutes, boucle propre au bout de 4 min 41, crossfade audible, volume du bus `Music` indépendant du bus `Sfx`.

**Commit** : `feat(demos): extend AudioDemo with streamed music, fades and crossfade`

---

## Phase 7 — Gameplay (D11)

### ⏳ Todo - G7.1. Ajouter `SoundEmitterComponent`

**Objectif** : poser un son sur une entité depuis l'éditeur.

**Livrables** :

- `CasaEngine/Framework/Scene/Entities/Components/SoundEmitterComponent.cs`, dérivé d'`EntityComponent` (ou `SceneComponent` si un ancrage dans la scène est utile — justifier le choix dans le commit), attribut `[DisplayName("Sound Emitter")]` comme `ParticleSystemComponent` ;
- propriétés : `SoundAssetId`, `PlayOnStart`, `IsLooped`, `BusName`, `VolumeOverride`, `PitchOverride` ;
- `Load(JObject)` pour la lecture runtime ;
- **case explicite dans `EditorEntityJsonSerializer.SaveComponent`** (`CasaEngine.EditorServices/EditorEntityJsonSerializer.cs`, switch ~ligne 191). ⚠️ Sans ce case, les réglages sont silencieusement perdus à la sauvegarde — c'est exactement le bug corrigé par le commit `e828affa` pour `DepthSortable2DComponent` ;
- arrêt de la voix quand le composant est détruit ou l'entité retirée.

**Tests** : aller-retour sauvegarde → chargement d'une entité portant le composant, tous champs conservés.

**Validation** : `dotnet test` vert ; le composant apparaît dans « Add Component » de l'éditeur (découverte par réflexion, `EntityDetailsPanel.CreateComponentTypeLookup`).

**Commit** : `feat(engine): add SoundEmitterComponent with entity serialization`

---

### ⏳ Todo - G7.2. Ajouter les commandes de cutscene audio

**Objectif** : lever la dette documentée dans `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md`.

**Livrables** :

- constantes `PlaySound`, `PlayMusic`, `StopMusic`, `FadeMusic` dans `CasaEngine/Framework/Cutscenes/CutsceneActionTypes.cs` ;
- classes de données correspondantes sur le modèle de `WaitCutsceneActionData.cs` ;
- désérialisation dans `CasaEngine/Framework/Cutscenes/Serialization/CutsceneAssetJsonSerializer.cs` (switch ~ligne 101) ;
- exécution dans `CutsceneActionCoroutineFactory.ExecuteAction` : `PlaySound` et `PlayMusic` sont non bloquants par défaut, `FadeMusic` attend la fin du fade ;
- validation dans `CutsceneValidator.cs` : asset manquant ou durée négative signalés.

**Tests** : désérialisation de chaque nouvelle action, validation d'une action mal formée, exécution du fade jusqu'à complétion via le `FakeAudioBackend`.

**Validation** : `dotnet test` vert.

**Commit** : `feat(cutscenes): add PlaySound, PlayMusic, StopMusic and FadeMusic actions`

---

## Phase 8 — Éditeur (D8, D12)

### ⏳ Todo - E8.1. Isoler l'audio de l'éditeur et régler le play-in-editor

**Objectif** : que la preview et les sons du jeu ne se marchent pas dessus (D12).

**Livrables** :

- toute lecture déclenchée par l'éditeur passe par le bus `Editor` ;
- `EditorPlayModeService.TryStopPlay` arrête toutes les voix du jeu et les pistes de musique, sans toucher au bus `Editor` ;
- `TryPause` met en pause les voix du jeu, `TryResume` les reprend ;
- comportement du bus `Editor` **pendant** une session de play : le laisser actif (une preview lancée reste audible) — à documenter dans `docs/engine/audio-system.md`.

**Validation** :

- build ;
- 🧪 **validation humaine requise** dans l'éditeur : lancer Play avec un son en boucle, faire Stop → silence ; relancer une preview → audible.

**Commit** : `feat(editor): route editor audio to a dedicated bus and stop game voices on play stop`

---

### ⏳ Todo - E8.2. Ajouter le menu « Create Sound »

**Objectif** : créer un `.sound` depuis le Content Browser.

**Livrables** (`CasaEngine.Editor/GameEditor.cs`) :

- `_contentBrowserPanel.RegisterContextMenuExtension(ContentItemType.Folder, "Create Sound", CreateSoundAssetInFolder)` à côté des entrées particules (~ligne 1223) ;
- `CreateSoundAssetInFolder` calqué sur `TryCreateParticleAssetInFolder` (~ligne 3465) : nom de fichier unique, `EditorAssetCatalogService.Add` + `Save`, `EditorAssetWriterService.SaveAsset(..., EditorAssetSaveSource.SoundEditorPanel)`, rollback du catalogue si l'écriture échoue, rafraîchissement du Content Browser puis ouverture du document.

**Validation** :

- build ;
- 🧪 **validation humaine** : clic droit sur un dossier → « Create Sound » crée un `.sound` visible avec l'icône Volume, et `AssetInfos.json` contient la nouvelle entrée.

**Commit** : `feat(editor): add a Create Sound context menu entry in the content browser`

---

### ⏳ Todo - E8.3. Ajouter l'inspecteur d'asset `.sound`

**Objectif** : éditer et écouter un `.sound` (D8).

**Livrables** :

- `CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs` sur le modèle de `ParticleAssetInspectorPanel.cs` : sélection du fichier audio (via `AssetSelector`), `Volume`, `Pitch`, `IsLooped`, `BusName`, `IsStreaming`, bouton Play/Stop de preview ;
- constante `SoundAssetDocumentPrefix = "panel_sound_asset_"` dans `CasaEngine.Editor/Workspaces/EditorPanelIds.cs` ;
- route `new AssetDocumentRoute(Constants.FileNameExtensions.Sound, TryOpenSoundAsset)` dans `GameEditor.GetAssetDocumentRoutes()` (~ligne 3795) et méthode `TryOpenSoundAsset` sur le modèle de `TryOpenParticleAsset` ;
- la preview passe par le bus `Editor` (E8.1) ;
- sauvegarde via `EditorAssetWriterService` avec `EditorAssetSaveSource.SoundEditorPanel`.

**Validation** :

- build ;
- 🧪 **validation humaine** : double-clic sur un `.sound` ouvre le document, la modification du volume est sauvegardée et rechargée, la preview joue le bon fichier.

**Commit** : `feat(editor): add the .sound asset inspector with preview`

---

## Phase 9 — Clôture

### ⏳ Todo - Z9.1. Écrire la documentation moteur

**Objectif** : documenter la feature comme les autres systèmes du moteur.

**Livrables** : `docs/engine/audio-system.md` —

- vue d'ensemble (backend, bus, voix, streaming) ;
- format de l'asset `.sound` avec un exemple JSON complet ;
- extrait d'utilisation : jouer un SFX, jouer une musique, faire un crossfade, régler un bus ;
- limites connues : WAV PCM 16 bits uniquement en streaming, pas de MP3, pas de 3D en V1, lecture disque sur le thread de jeu ;
- évolutions prévues : décodeur Ogg via NVorbis, audio 3D, panneau mixer.

**Validation** : relecture ; liens relatifs valides.

**Commit** : `docs(audio): document the audio system, the .sound asset and its limits`

---

### ⏳ Todo - Z9.2. Passe performance et allocations

**Objectif** : vérifier les contraintes hot path de CLAUDE.md.

**Livrables** :

- relecture de tous les chemins `Update` ajoutés : aucune LINQ, aucune closure, aucun `List<T>`/`byte[]` alloué par frame, buffers de streaming poolés ;
- vérification que les logs d'erreur audio sont throttlés (pas un log par frame quand un asset est cassé ou que les sources sont saturées) ;
- corrections éventuelles.

**Validation** : `dotnet test` vert ; build ; notes de relecture dans le message de commit.

**Commit** : `perf(audio): remove per-frame allocations and throttle audio error logs`

---

### ⏳ Todo - Z9.3. Validation finale et clôture

**Objectif** : livrer.

**Livrables** :

- toutes les tâches en ✅ ou 🧪 avec la validation humaine faite ;
- ligne du chantier **mise à jour** dans le tableau de `ai-agent/README.md` (elle y est déjà, en ⏳) ;
- section « Points ouverts » de ce fichier mise à jour (résolus / reportés).

**Validation** :

- `dotnet build CasaEngine.MonoGame.sln` ;
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` ;
- `CasaEngine.Demos` lancé, `AudioDemo` parcourue de bout en bout ;
- éditeur lancé : création d'un `.sound`, édition, preview, play-in-editor.

**Commit** : `docs(audio): close the audio system plan and record the remaining follow-ups`

Le **merge sur `main` reste une décision humaine** : ne pas merger ni pousser sans demande explicite.

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | `DynamicSoundEffectInstance.IsLooped` : confirmer que le setter à `true` lève `InvalidOperationException` (sémantique XNA) et le noter. La conception ne dépend pas de la réponse (la boucle est gérée par `WavStreamReader.Rewind()`). | M6.2 |
| O2 | Taille de buffer et profondeur de file du streaming : valeurs retenues, et marge en millisecondes. À figer et documenter. | M6.2 |
| O3 | Limite globale de voix (proposition : 64, contre 256 sources OpenAL disponibles). | V3.2 |
| O4 | Les bus par défaut sont-ils figés dans le moteur, ou déclarables par projet (`ProjectSettings`) ? V1 : figés. | B2.2 |
| O5 | Persistance des volumes utilisateur (modèle `DisplaySettingsPersistence`) : hors périmètre V1, à confirmer. | — |
| O6 | Comportement quand un `.sound` pointe un fichier absent ou non supporté : retenu = log d'erreur + voix silencieuse, pas d'exception. À confirmer à l'usage. | L5.1 |
| O7 | Coût des 23,6 Mo de WAV recopiés dans la sortie à chaque build : acceptable, ou faut-il passer la musique en Ogg plus tard ? | P0.2 |
| O8 | `SoundEmitterComponent` dérive-t-il d'`EntityComponent` ou de `SceneComponent` ? Le choix dépend de l'utilité d'un ancrage dans la scène en 2D. | G7.1 |
| O9 | ⚠️ **Bloque le lancement des exécutables, sans rapport avec l'audio.** `MGUI/Directory.Packages.props` épingle `FontStashSharp.MonoGame` en version flottante `1.*` (résolue en 1.5.7 dans le cache NuGet local), alors que le `Directory.Packages.props` racine épingle 1.5.6. `MGUI.FontStashSharp.dll` référence donc 1.5.7 tandis que les applications embarquent 1.5.6 → `FileNotFoundException` au premier rendu d'UI (`UIRoot.CreateFontStashSharpTextEngine`). Correctif candidat : aligner les deux épinglages (et supprimer la version flottante). **Changement de dépendance : nécessite une validation humaine.** | L5.2, M6.4, E8.x |
