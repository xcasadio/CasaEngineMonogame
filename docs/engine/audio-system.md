# Système audio CasaEngine — V1

Sons courts, musiques streamées et bus de mixage. Les décisions d'architecture sont figées dans
[analysis-audio-system.md](../../ai-agent/audits/analysis-audio-system.md) (§3).
Decisions: see [ADR-0001](../decisions/0001-audio-runtime-architecture-v1.md), [ADR-0002](../decisions/0002-audio-asset-format-and-editor-scope-v1.md), [ADR-0039](../decisions/0039-software-stereo-voices.md), [ADR-0040](../decisions/0040-project-audio-mute-setting.md) and [ADR-0055](../decisions/0055-engine-owned-software-audio-mixer-with-thin-native-outputs.md) (mixeur logiciel du moteur, §1 bis).
SPU PlayStation logiciel, hébergé par le backend logiciel : [psx-spu.md](psx-spu.md) ([ADR-0058](../decisions/0058-a-software-playstation-spu-hosted-by-the-software-audio-backend.md)).
Graphe de bus, effets, départs, ducking, fondus et snapshots du backend logiciel : §2 bis ([ADR-0059](../decisions/0059-a-bus-graph-with-effects-mixed-by-the-software-audio-backend.md)).
Variations aléatoires et priorités de voix : §3 et §4 ([ADR-0063](../decisions/0063-sound-variations-and-voice-priorities.md)).
Écouteur, sons 2D et 3D, Doppler et paramètres de jeu : §5 quater ([ADR-0064](../decisions/0064-listener-spatial-audio-doppler-and-game-parameters.md)).
Asset de mixeur `.audioMixer` du projet : §2 ter ([ADR-0067](../decisions/0067-a-project-mixer-asset-applied-to-the-live-mixer.md)) ; panneau de mixage de l'éditeur : [audio-mixer-panel.md](../editor/audio-mixer-panel.md).

---

## 1. Vue d'ensemble

```text
SoundAsset (.sound)          asset JSON : fichier audio + volume + pitch + loop + bus + streaming
                             + variations aléatoires + priorité + mode spatial + liaisons
        ↓
AudioService                 pool de voix, routage vers les bus, fades, propriété (owner),
                             tirage des variations, vol de voix par priorité,
                             écouteur, voix spatiales, Doppler, paramètres de jeu
   ├─ AudioMixer             arbre de bus nommés, gain effectif
   ├─ MusicPlayer            pistes streamées, fade in/out, crossfade
   └─ IAudioBackend          frontière plateforme
              ├─ SoftwareAudioBackend   mixeur C# du moteur + sortie native mince (§1 bis)
              ├─ MonoGameAudioBackend   OpenAL, SoundEffect + DynamicSoundEffectInstance
              ├─ NullAudioBackend       aucun périphérique : tout devient silencieux
              └─ FakeAudioBackend       tests (dans CasaEngine.Tests)
        ↑
AudioSystemComponent         GameComponent : choisit et possède le backend, appelle Update
```

Les fichiers `.wav` et `.ogg` sont chargés par `AudioClipLoader` en `PcmAudioClip` : PCM 16 bits
entrelacé, mono ou stéréo, sans aucun type MonoGame, lisible par les deux backends (§1 bis).
Formats lus (ADR-0057) : WAV en PCM entier 8/16/24/32 bits, flottant 32 bits, ADPCM Microsoft et
IMA/DVI ; Ogg Vorbis (NVorbis 0.10.4, la version déjà livrée par MonoGame). Le décodage est
complet, au chargement.

Point d'entrée depuis le jeu : `game.AudioSystemComponent.Service`.

`AudioService` ne contient **aucun type MonoGame ni `Game`** : c'est ce qui rend les bus, les voix,
les fades et le streaming testables, un périphérique OpenAL ne pouvant pas être ouvert en CI.

---

## 1 bis. Mixeur logiciel du moteur (ADR-0055)

Le moteur mixe lui-même son audio, en C#, sur un thread audio dédié qui calcule de l'avance ; une
sortie native minimale envoie le flux à la carte. C'est la première tranche (S1) du programme
« audio moderne » ([plan](../../ai-agent/tasks/audio-modern-tasks.md)) : même contrat
`IAudioBackend`, même API d'`AudioService`, aucun changement côté jeu.

```text
thread de jeu                               thread audio « CasaEngine Audio »
AudioService ─▶ SoftwareAudioBackend         OpenAlAudioOutput (contexte OpenAL propre au thread)
                 table des voix, états          │ toutes les ~10 ms : buffers consommés
                 │ commandes (file sans verrou)  ▼
                 └──────────────▶ SoftwareMixer.Render ─▶ 4 buffers float stéréo en file
                 ◀────────────── fins de voix, buffers de streaming consommés
```

- **Choix du backend**, une fois au démarrage, dans cet ordre :
  1. la variable d'environnement `CASAENGINE_AUDIO_BACKEND` (`Software` ou `MonoGame`), valable
     pour tous les hôtes, éditeur compris ;
  2. sinon le réglage de projet `AudioBackend` (absent = non renseigné) ;
  3. sinon le défaut, `AudioBackendSelection.DefaultKind` : **`Software`** depuis la recette
     d'écoute de l'auteur du 2026-10-05 (démo, éditeur, Alundra). Retour arrière :
     `CASAENGINE_AUDIO_BACKEND=MonoGame` (tous les hôtes) ou `"AudioBackend": "MonoGame"` dans le
     fichier de projet.

  La ligne `Audio backend: <type> (source: environment|project|default)` est consignée au
  démarrage. Si la sortie logicielle ne s'ouvre pas, le moteur revient au backend MonoGame
  (`(fallback from Software)`). L'éditeur crée son runtime sans projet : il suit la variable ou le
  défaut, jamais le réglage du projet ouvert ensuite.
- **Sortie** : OpenAL Soft livré par MonoGame (`openal`), lié directement. Le moteur ouvre **son
  propre** périphérique et contexte, rendu courant sur le seul thread audio
  (`ALC_EXT_thread_local_context`), et ne touche jamais le contexte global de MonoGame. Le flux
  est stéréo, en flottant 32 bits, joué sans spatialisation (`AL_SOFT_direct_channels`) au débit
  du périphérique. Avance par défaut : 4 buffers de 10 ms (40 ms). Avec ce backend, l'audio de
  MonoGame n'est jamais initialisé.
- **Sémantique** : volume linéaire [0, 1] ; pitch en octaves (±1), appliqué comme 2^pitch ; boucle
  du clip entier. Un clip mono est panoramiqué à **puissance constante** (−3 dB au centre), un clip
  stéréo par **balance** ; une voix stéréo (musique, `PlayClipStereo`) est restituée exactement,
  gauche vers gauche et droite vers droite. C'est un changement audible par rapport au backend
  MonoGame, qui tourne la position OpenAL de la source. Un son spatial (§5 quater) remplace le pan
  de la voix par son pan spatial, avec la même loi.
- **Mixage** : flottant 32 bits, rééchantillonnage cubique (Hermite à 4 points), rampe linéaire
  des gains sur un bloc quand le volume ou le pan change (pas de clic), écrêtage dur en sortie.
  Tout débit de clip ou de flux positif est accepté (pas de bornes 8–48 kHz). Le rendu ne fait
  aucune allocation.
- **Tests** : le mixeur se rend hors ligne, à l'échantillon près, sans carte son ; une suite de
  conformité vérifie le même contrat sur le faux backend et le backend logiciel. La tenue en temps
  réel se mesure avec le mode stress de la démo (§11).

---

## 2. Bus de mixage

Les « channels » du moteur sont des **bus nommés**, créés par défaut sous `Master` :

| Bus | Usage |
|---|---|
| `Master` | racine : son volume et son mute s'appliquent à tout |
| `Music` | musiques et ambiances (streamées) |
| `Sfx` | effets sonores de gameplay |
| `Voice` | dialogues et voix |
| `Ui` | retours d'interface |
| `Editor` | **éditeur uniquement** : preview d'asset, isolée des bus du jeu |

Le volume envoyé au périphérique est `volume de la voix × gain effectif du bus`, le gain effectif
étant le produit des volumes jusqu'à `Master` (0 si un ancêtre est muet). Les gains ne sont
recalculés que lorsqu'un volume ou un mute change, jamais par frame.

```csharp
var mixer = game.AudioSystemComponent.Mixer;
mixer.GetBus(AudioBusNames.Music).Volume = 0.5f;
mixer.GetBus(AudioBusNames.Sfx).IsMuted = true;
```

Le parent d'un bus est fixé à la création et ne change jamais : l'arbre ne peut pas contenir de
cycle. Les bus par défaut sont figés dans le moteur ; un projet peut en ajouter au-dessus.

---

## 2 bis. Graphe de bus, effets et fondus sous le backend logiciel (ADR-0059)

Avec le backend logiciel (capacité `IAudioBusBackend`), les bus deviennent un vrai graphe mixé sur
le thread audio : chaque voix (y compris les voix stéréo, la musique streamée et le SPU PSX) est
mixée dans le tampon de son bus, puis chaque bus applique ses effets, son gain propre, ses départs,
et s'ajoute à son parent, jusqu'à `Master`. `AudioService` ne multiplie plus le gain du bus dans le
volume de chaque voix. Les autres backends (MonoGame, faux backends de test) gardent exactement le
comportement du §2 ; effets, départs et ducking y sont absents, avec une ligne de journal limitée.

```csharp
var mixer = game.AudioSystemComponent.Mixer;
var service = game.AudioSystemComponent.Service;
var music = mixer.GetBus(AudioBusNames.Music);
var sfx = mixer.GetBus(AudioBusNames.Sfx);

music.AddEffect(new BiquadFilterEffect(BiquadFilterType.LowPass, 800f)); // au plus 4 effets par bus
music.AddEffect(new DuckingEffect(source: mixer.GetBus(AudioBusNames.Voice), depthDb: 12f));

var reverbBus = mixer.CreateBus("Reverb", AudioBusNames.Master);
reverbBus.AddEffect(new ReverbEffect(roomSize: 0.8f));
sfx.SetSend(reverbBus, 0.4f);                       // départ post-fader ; 0 le retire

service.FadeBus(AudioBusNames.Music, 0.2f, 1.5f);   // fondu de bus à durée explicite
var calm = service.CaptureSnapshot();
// ... changements de volumes et d'effets ...
service.ApplySnapshot(calm, 0.5f);                  // retour en rampe
service.MasterLimiter.CeilingDb = -1f;              // actif par défaut
```

- **Ordre du mix** : un bus est mixé après tous ceux qui l'alimentent (ses enfants, les bus qui lui
  envoient un départ, la source d'un ducking). Dans le tampon d'un bus : effets insérés dans l'ordre
  d'ajout, puis gain propre, puis départs et parent. Après `Master` : le limiteur, puis l'écrêtage
  dur conservé en dernier recours.
- **Gains** : le gain propre d'un bus (`muet ? 0 : volume`) est publié en dernière valeur, jamais
  perdu, et rampé sur un bloc audio (10 ms). Au plus 32 bus ; au-delà, un avertissement et le bus
  est mixé directement dans `Master`, sans son gain propre ni celui de ses parents.
- **Fondus** : `FadeVoice`, les fondus de `MusicPlayer` et `FadeBus` deviennent des rampes
  interpolées à l'échantillon sur le thread audio, qui démarrent au bloc suivant. Le contrat public
  ne change pas : `GetVoiceVolume`, `IsFading`, la fin de `StopWithFade` et les fondus enchaînés
  suivent une chronologie tenue sur le thread de jeu, à un bloc près du son. Couper le son d'un bus
  pendant un fondu prend effet au `Update` suivant.
- **Effets** (`CasaEngine.Framework.Audio.Effects`) : `BiquadFilterEffect` (passe-bas, passe-haut,
  passe-bande, crête, plateaux ; Audio EQ Cookbook du W3C), `CompressorEffect` (Giannoulis,
  Massberg et Reiss, 2012), `ReverbEffect` (Freeverb, d'après J. O. Smith, CCRMA), `LimiterEffect`,
  `DuckingEffect`. Les paramètres se changent à tout moment depuis le thread de jeu ; ils sont lus
  au bloc suivant, sans lissage.
- **Départs** : au plus 4 par bus, niveau dans [0, 1], pris après les effets et le gain propre du bus
  (pas celui de ses ancêtres). Un départ ou un ducking qui fermerait une boucle est refusé
  (`InvalidOperationException`).
- **Ducking** : `DuckingEffect`, inséré sur le bus à atténuer, suit le niveau du bus source dans le
  même bloc et réduit le bus cible de `DepthDb` au-dessus de `ThresholdDb`, avec attaque et
  relâchement.
- **Limiteur du Master** : `AudioService.MasterLimiter`, actif par défaut à −1 dBFS ;
  `IsEnabled = false` le coupe. Tout son au-dessus du plafond est réduit au lieu d'être écrêté.
  Un projet peut le couper par le réglage `IsMasterLimiterEnabled: false` de son fichier de projet
  (sans interface, comme `IsAudioMuted`), appliqué au démarrage et, dans l'éditeur, à chaque
  ouverture de projet ; sans ce réglage le limiteur est actif (ADR-0062).
- **Snapshots** : `CaptureSnapshot` retient le volume propre de chaque bus et les paramètres des
  effets insérés ; `ApplySnapshot` rétablit les volumes par `FadeBus` et les paramètres aussitôt.
  Le bus `Editor`, le muet des bus (donc le muet projet du `Master`) et le limiteur ne sont jamais
  touchés.
- **Mesure des niveaux** (capacité `IAudioMeteringBackend`, ADR-0060) : le thread audio publie, pour
  chaque bloc, la crête par canal et la valeur efficace de chaque bus (après ses effets et son gain)
  et de la sortie, et compte les échantillons au-delà de la pleine échelle avant le limiteur. Chaque
  lecteur garde son curseur et ne perd aucune crête s'il lit au moins toutes les 80 ms ; rien n'est
  verrouillé ni alloué.

```csharp
var cursor = default(AudioMeterCursor);        // un par lecteur
Span<AudioLevel> buses = stackalloc AudioLevel[32];
if (service.TryGetMeterBusIndex(AudioBusNames.Music, out var music)
    && service.TryReadLevels(ref cursor, buses, out var output, out var read))
{
    var musicPeak = buses[music].Peak;          // linéaire, 1 = pleine échelle
    var overs = output.Overs;
}
```

  Le panneau « Audio » de l'éditeur (Windows > Audio) affiche ces niveaux :
  [audio-profiler-panel.md](../editor/audio-profiler-panel.md).

---

## 2 ter. Asset de mixeur `.audioMixer` (ADR-0067)

Un projet peut décrire ses bus dans **un** asset `.audioMixer`, désigné par le réglage de projet
facultatif `AudioMixerAsset` (id de l'asset, recommandé, ou son nom ; vide = mixeur par défaut du
moteur). L'asset est la seule source de vérité : il est appliqué au mixeur vivant, jamais l'inverse.

```json
{
  "id": "…",
  "name": "demo_mixer",
  "type": "AudioMixerAsset",
  "version": 1,
  "schema_version": 1,
  "buses": [
    { "name": "Music", "parent": "Master", "volume": 1.0, "effects": [], "sends": [] },
    { "name": "Sfx", "parent": "Master", "volume": 1.0, "effects": [],
      "sends": [ { "target": "DemoMixerReverb", "level": 0.3 } ] },
    { "name": "DemoMixerReverb", "parent": "Master", "volume": 1.0,
      "effects": [ { "type": "reverb", "room_size": 0.5, "damping": 0.5, "wet": 1.0, "dry": 0.0, "stereo_separation": 1.0 } ],
      "sends": [] }
  ]
}
```

- **Contenu** : bus (nom, parent, volume), effets insérés (`biquad` : `filter`, `frequency_hz`, `q`,
  `gain_db` ; `compressor` : `threshold_db`, `ratio`, `knee_db`, `attack_seconds`, `release_seconds`,
  `makeup_gain_db` ; `reverb` : `room_size`, `damping`, `wet`, `dry`, `stereo_separation` ;
  `ducking` : `source`, `depth_db`, `threshold_db`, `attack_seconds`, `release_seconds`) et départs
  (`target`, `level`). Le bus **Master** n'y figure pas (son muet appartient au projet, §5 ter), ni
  Editor, ni les muets, ni le limiteur, ni de snapshots.
- **Version** : `version` absente = 1 ; une version plus récente ou nulle est refusée (le projet garde
  le mixeur par défaut, avec un avertissement). Les clés sont dans le runtime, l'écriture dans
  l'éditeur.
- **Validation tolérante, entrée par entrée** : nom vide, réservé (Master, Editor) ou en double ;
  parent inconnu ou en cycle (le bus et ses descendants sont ignorés) ; effet, départ ou ducking
  invalide ; plus de 4 effets ou 4 départs ; au-delà de la capacité de bus du backend logiciel (32,
  Master compris, donc 26 bus d'asset au plus). Chaque entrée écartée est consignée avec le nom de
  l'asset et du bus ; seul un fichier illisible ou d'une version plus récente est refusé en entier.
  Aucune exception n'atteint le démarrage du jeu.
- **Application** (`AudioMixerAssetApplier`, idempotente) : au démarrage, juste après
  l'enregistrement des chargeurs d'assets, puis dans l'éditeur à chaque ouverture de projet. Deux
  passes : création des bus (parents d'abord) et volumes, puis effets et départs. Elle ne retire,
  ne renomme ni ne reparente jamais un bus (le moteur ne le sait pas), et ne touche ni Master, ni
  Editor, ni les muets, ni le limiteur, ni les effets et départs posés par le jeu. Un paramètre
  d'effet changé est modifié sur place ; un changement de type ou de source de ducking remplace
  l'effet. Les volumes de l'asset sont des valeurs de départ : le jeu peut les réécrire.

```csharp
var applier = new AudioMixerAssetApplier(audioService);
applier.Apply(mixerAsset);   // asset -> mixeur vivant
applier.Release();           // volumes d'origine, effets et départs retirés
```

Exemple livré, **non activé** : `CasaEngine.Demos/Content/Audio/demo_mixer.audioMixer`. Pour l'essayer,
ajouter à la racine de `CasaEngine.Demos/Content/DemosGame.json` la ligne
`"AudioMixerAsset": "23ddf15f-ea55-4c8b-9077-9b033f10b0f9"`.

---

## 3. L'asset `.sound`

Un `.sound` référence son fichier audio par identifiant de catalogue, comme un `.texture`
référence son `.png`.

```json
{
  "id": "b41f0a6c-2d58-4a19-9f73-0c5e8a91d2b4",
  "name": "menu_click",
  "audio_file_asset_id": "3f8c1b52-9a47-4c6e-a0d5-1e7b24c9f018",
  "volume": 1.0,
  "pitch": 0.0,
  "is_looped": false,
  "bus_name": "Sfx",
  "is_streaming": false
}
```

| Champ | Sens |
|---|---|
| `audio_file_asset_id` | id du `.wav` dans `AssetInfos.json` |
| `volume` | 0..1, avant le gain du bus |
| `pitch` | -1..1 (une octave en dessous / au-dessus) |
| `is_looped` | boucle |
| `bus_name` | bus par défaut, surchargeable à l'appel |
| `is_streaming` | `true` = décodé à la volée (musique), `false` = chargé en mémoire (SFX) |

Le streaming est **authoré**, pas déduit de l'extension : le même `.wav` peut servir aux deux.
Tout champ absent prend sa valeur par défaut, donc un document incomplet se charge au lieu
d'échouer.

**Variations aléatoires, priorité, spatialisation et liaisons** (ADR-0063, ADR-0064), champs additifs : chaque clé n'est écrite que si
sa valeur diffère de son défaut, donc un `.sound` qui ne s'en sert pas garde exactement les clés
ci-dessus.

```json
{
  "id": "…",
  "name": "footstep",
  "audio_file_asset_id": "…",
  "volume": 0.8,
  "pitch": 0.0,
  "is_looped": false,
  "bus_name": "Sfx",
  "is_streaming": false,
  "priority": 20,
  "variation_audio_file_asset_ids": [ "…", "…" ],
  "variation_volume_min": 0.7,
  "variation_pitch_min": -0.1,
  "variation_pitch_max": 0.1
}
```

| Champ | Défaut | Sens |
|---|---|---|
| `variation_audio_file_asset_ids` | aucun | fichiers tirés en plus de `audio_file_asset_id` ; chacun doit être catalogué dans `AssetInfos.json` |
| `variation_volume_min` / `variation_volume_max` | 1 / 1 | facteur de volume tiré dans [min, max], borné à [0, 1] : la variation ne fait qu'atténuer |
| `variation_pitch_min` / `variation_pitch_max` | 0 / 0 | décalage de pitch en octaves tiré dans [min, max], borné à [-1, 1] |
| `priority` | 0 | priorité de vol de voix, 0..100 ; 0 = aucune (§4) |
| `spatial_mode` | `None` | `None`, `Spatial2D` ou `Spatial3D` (§5 quater) |
| `distance_model` | `InverseDistanceClamped` | modèle de distance d'OpenAL 1.1 (§5 quater) |
| `reference_distance` / `max_distance` / `rolloff_factor` | 1 / sans limite / 1 | réglages du modèle, en unités monde |
| `doppler_factor` | 0 | 0 = Doppler désactivé |
| `parameter_bindings` | aucune | liaisons de paramètres de jeu vers le volume ou le pitch (§5 quater), huit au plus |

Les plages sont **relatives** à la valeur jouée : le facteur multiplie le volume, le décalage
s'ajoute au pitch. Le tirage est uniforme ; une plage inversée est triée au tirage, une plage
dégénérée (min = max) ne tire rien. Le fichier est tiré uniformément parmi le fichier principal et
les variations non vides, sans anti-répétition (le même fichier peut sortir deux fois de suite).
Le chargement est tolérant : une valeur d'un type inattendu (texte, `null`, objet) garde le défaut
avec un avertissement qui nomme l'asset et la clé ; une entrée de liste qui n'est pas un GUID est
ignorée de même. Un asset marqué streaming ignore variations et priorité.

Dans l'éditeur : clic droit sur un dossier → **Create Sound**, puis double-clic pour ouvrir
l'inspecteur (fichier, fichiers de variation, volume, pitch, plages de variation, priorité, loop,
bus, mode spatial, modèle et distances, Doppler, nombre de liaisons, streaming, preview). La liste
des bus propose les quatre bus du moteur puis les autres bus de l'asset de mixeur du projet (§2 ter) ;
un bus du `.sound` absent des deux s'affiche « (unknown bus) » sans être sélectionnable. La ligne
« Waveform » dessine le fichier audio principal (décodé une fois à l'ouverture et au changement de
fichier, jusqu'à 64 Mo de fichier).

---

## 4. Jouer un son

```csharp
var audio = game.AudioSystemComponent.Service;
var asset = game.AssetContentManager.Load<SoundAsset>(soundAssetId);

// One-shot, avec les réglages de l'asset, rattaché au monde courant.
var voice = audio.PlaySound(asset, world);

// Avec surcharges : tout champ laissé à null garde la valeur de l'asset.
audio.PlaySound(asset, new SoundPlaybackOverrides(volume: 0.5f, isLooped: true), world);

// Fade out puis libération.
audio.StopWithFade(voice, 1f);
```

`PlaySound` retourne `AudioVoiceHandle.None` quand l'asset est cassé, le fichier introuvable ou
le backend saturé : c'est un log throttlé, jamais une exception. **Le code gameplay n'a pas à se
protéger d'un asset son cassé.**

Le paramètre `owner` porte la durée de vie : une voix appartenant à un monde est coupée par
`World.Clear()`, une voix sans propriétaire (UI, preview éditeur) survit aux changements de monde.

**Composition avec les variations.** Les surcharges remplacent les valeurs de l'asset, puis le
tirage des variations s'applique par-dessus (facteur de volume, décalage de pitch), et le résultat
est borné (volume dans [0, 1], pitch dans [-1, 1]). Un seul tirage par lecture : une voix en boucle
garde le sien. La source aléatoire est `AudioService.VariationRandom` (`Random.Shared` par défaut,
thread de jeu seulement), remplaçable pour un test ou un rejeu déterministe. Si le fichier tiré ne
se charge pas, rien ne joue (journal étranglé), sans repli sur un autre fichier. Chaque fichier
tiré est chargé et décodé à sa première lecture, sur le thread de jeu, puis reste en mémoire pour
la vie du fournisseur de clips.

**Priorités et vol de voix.** Quand toutes les voix du backend sont prises, un son sans priorité
est refusé, comme avant. Un son de priorité `p > 0` (celle de l'asset, ou
`SoundPlaybackOverrides.Priority`, dont 0 retire la priorité) libère d'abord une voix déjà
terminée qui n'a pas encore été recyclée ; sinon il coupe la voix de priorité la plus basse
strictement inférieure à `p`, la plus ancienne à égalité, et prend sa place. Une voix sans
priorité, une voix streamée (musique) et une voix stéréo du backend ne sont jamais volées ;
`PlayClip`, `PlayClipStereo` et `PlayStream` jouent toujours sans priorité. Une voix en pause ou
en fondu peut être volée ; sa poignée devient périmée (`IsAlive` faux), et un émetteur en boucle de
basse priorité le voit par `IsPlaying`. `AudioService.StolenVoiceCount` compte les vols ; si le
backend refuse quand même la nouvelle voix après un vol (cas dégradé), c'est un refus et la
victime reste perdue. Il n'y a pas de voix virtuelles.

```csharp
// Ce son peut prendre la place d'une voix de priorité plus basse.
audio.PlaySound(asset, new SoundPlaybackOverrides(volume: 0.5f) { Priority = 30 }, world);
```

---

## 5. Musique streamée

```csharp
var music = game.AudioSystemComponent.Service.Music;

var track = music.Play(themeAsset, fadeInSeconds: 1f, world);
music.Stop(track, fadeOutSeconds: 2f);

// Deux pistes jouent en même temps pendant la transition.
var next = music.Crossfade(track, battleThemeAsset, durationSeconds: 2f, world);
```

Le fichier est lu par blocs et jamais chargé entièrement. La boucle **rembobine le lecteur** ;
elle ne passe pas par `IsLooped`, que MonoGame refuse sur une voix dynamique. Une piste n'est
abandonnée qu'une fois sa file de buffers vidée, jamais sur une famine passagère.

**Lecture hors du thread de jeu (ADR-0056).** Avec un backend réel (logiciel ou MonoGame), `Play`
ouvre le fichier, lit l'en-tête et remplit la première file de façon synchrone, puis un thread
« CasaEngine Audio Streaming » lit et rembobine le fichier dans un anneau par piste ; `Update` ne
fait plus que transmettre les buffers prêts à la voix. Les faux backends des tests gardent la
lecture en ligne. En mode travailleur, `GetPosition` donne la position du dernier buffer transmis.

---

## 5 bis. Voix stéréo logicielles

Une voix mono ne peut porter qu'un couple (volume, pan) : sur DesktopGL, le pan d'une source mono
ne fait que tourner sa position OpenAL, ce qui ne donne jamais des gains gauche/droite choisis. Le
port Alundra a besoin des volumes gauche et droite exacts que l'original écrit par tonalité — une
**voix stéréo logicielle** joue un clip mono sur une voix de streaming stéréo que le moteur nourrit
lui-même, chaque frame de sortie étant `gauche = son × gainGauche`, `droite = son × gainDroite`.

```csharp
var voice = audio.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, leftGain: 0.8f, rightGain: 0.2f);
audio.SetVoiceStereoGains(voice, leftGain: 0.5f, rightGain: 0.5f); // remix en direct
audio.GetVoiceStereoGains(voice, out var left, out var right);
```

`parameters.Volume` et `IsLooped` s'appliquent normalement (bus compris) ; `Pan` et `Pitch` sont
**ignorés**, les gains explicites en tenant déjà lieu. La voix retournée est une voix ordinaire
pour tout le reste : `Stop`, `StopVoicesOwnedBy`, `StopAll`, `Pause`, `Resume`, `FadeVoice`.

Un clip ne peut être joué en stéréo que s'il expose ses échantillons mono 16 bits
(`IAudioClipSamples`, porté par `PcmAudioClip` pour un clip mono) ; sinon `PlayClipStereo` renvoie
`AudioVoiceHandle.None`.

**Sous le backend logiciel (ADR-0056)**, un `PcmAudioClip` mono est joué par la capacité
`IStereoVoiceBackend` : une voix résidente dont les gains gauche et droite sont appliqués au
mixage, au bloc suivant, à n'importe quel débit, avec le rééchantillonnage cubique du mixeur. Les
limites ci-dessous ne valent alors plus ; elles restent celles des autres backends et des clips qui
ne sont pas des `PcmAudioClip`.

**Région de boucle et vitesse (ADR-0056).** `AudioVoiceParameters.WithLoopRegion(début, fin)` fait
boucler une voix résidente ou stéréo sur `[début, fin[` (en trames du clip, après une éventuelle
intro) ; `WithRateMultiplier(r)` (]0, 16]) multiplie la vitesse, au-delà de l'octave du pitch.
Toutes les méthodes `With*` les conservent. Le backend MonoGame ignore la région (boucle du clip
entier) et replie le multiplicateur sur son pitch borné à ±1 octave.

Limites du chemin historique (voir [ADR-0039](../decisions/0039-software-stereo-voices.md)) :

- Un changement de gain n'atteint que les buffers pas encore soumis : jusqu'à ~60 ms de latence à
  la profondeur de file par défaut, contre un changement immédiat côté PSX d'origine.
- MonoGame refuse une voix de streaming hors 8 000-48 000 Hz : un clip hors de cette plage est
  rééchantillonné par le moteur (facteur entier, interpolation linéaire en dessous, moyenne par
  blocs au-dessus), une approximation déclarée.
- La boucle reprend le clip entier, sans points de boucle.
- Le PCM mono de chaque clip chargé est gardé deux fois (dans le `SoundEffect` et pour la voix
  stéréo).

---

## 5 ter. Muet projet

Le mute est un **réglage projet**, pas un réglage utilisateur local (ADR-0040) : la clé
`IsAudioMuted` du fichier `.json` du projet mute le bus `Master`. Absente, elle vaut `false` ;
elle n'est écrite dans le fichier que si elle vaut `true`, donc un projet non muet garde un
fichier identique au bit près.

Appliquée au démarrage (le runtime charge le projet avant de créer `AudioSystemComponent`) et, côté
éditeur, à chaque ouverture de projet — muter `Master` coupe donc aussi les previews de l'éditeur,
puisque le bus `Editor` en descend. Accès en jeu :

```csharp
game.AudioSystemComponent.IsMuted = true; // mute Master et répercute dans les réglages projet
```

Pas d'interface utilisateur : le réglage s'édite dans le fichier projet. Autres réglages audio du projet :
`AudioBackend` (§1 bis), `IsMasterLimiterEnabled` (§2 bis) et `AudioMixerAsset` (§2 ter).

---

## 5 quater. Écouteur, spatialisation, Doppler et paramètres de jeu (ADR-0064)

**Écouteur.** Le point d'écoute vient d'un `AudioListenerComponent` (composant de scène, « Audio
Listener » dans « Add Component ») qui pousse sa pose monde dans `AudioService` chaque fois qu'elle
change ; son entité est toujours mise à jour (politique `DynamicDefault`). Le code peut aussi appeler
`AudioService.SetListener(source, pose)` et `RemoveListener(source)`. Le dernier écouteur enregistré
gagne ; un second écouteur est signalé une fois au journal ; quand il disparaît (détachement, entité
désactivée), le précédent reprend. **Sans écouteur, rien n'est spatialisé** : les sons jouent comme
avant. C'est un état de chaque frame : un son spatial lancé avant l'arrivée de l'écouteur se
spatialise dès l'`Update` qui suit.

**Mode spatial par asset** (`spatial_mode`, §3) : `None` (défaut), `Spatial2D` (distances, directions
et vitesses projetées sur le plan X/Y, Z ignoré : un monde en pixels, +Y vers le haut) ou `Spatial3D`.
Seuls `PlaySoundAt(asset, position, overrides, owner)` et `SetVoicePosition(voice, position)`
spatialisent ; un asset spatial joué par `PlaySound` sans position, par une cinématique ou par la
prévisualisation de l'inspecteur joue comme un son non spatial. Positions en
`System.Numerics.Vector3`, en **unités monde** : la spécification ne fixe aucune unité, les distances
de l'asset se règlent dans celles du jeu.

**Atténuation.** Les modèles de distance de la spécification OpenAL 1.1
(<https://www.openal.org/documentation/openal-1.1-specification.pdf>, §3.4.1 à §3.4.6), formules
appliquées à la lettre, aucun code repris : `InverseDistance`, `InverseDistanceClamped` (défaut, celui
de la spécification), `LinearDistance`, `LinearDistanceClamped`, `ExponentDistance`,
`ExponentDistanceClamped`, `None`. Réglages par asset : `reference_distance` (défaut 1),
`max_distance` (défaut sans limite), `rolloff_factor` (défaut 1). Ajouts du moteur : le gain est
borné à [0, 1] (un volume ne dépasse jamais 1) ; une formule non évaluable (division par zéro,
dénominateur négatif ou nul du modèle inverse, référence égale au maximum en linéaire) n'atténue pas.

**Pan spatial.** Produit scalaire de la direction de la source avec le vecteur droit de l'écouteur
(sinus de l'azimut, sans distinction avant/arrière) ; la spécification ne définit pas le
panoramique, c'est un choix du moteur. Il remplace le pan propre de la voix tant que la voix est
spatiale et qu'un écouteur est actif (`SetVoicePan` est alors sans effet audible ; sans écouteur, le pan
propre s'applique de nouveau).

**Doppler** (désactivé par défaut) : `doppler_factor` de l'asset (0 = désactivé), formule de la
spécification (§3.5.2) ; vitesse du son `AudioService.SpeedOfSound`, défaut 343,3 **unités monde par
seconde** (la valeur de la spécification, pensée pour des mètres) : à régler selon l'échelle du jeu
(dans un monde en pixels, des vitesses ordinaires approchent 343,3 et saturent l'effet). Les vitesses
sont dérivées des poses poussées d'une frame à l'autre (nulles quand la pose n'a pas été poussée dans
la frame). Ajout du moteur : rapport borné à [0,25 ; 4]. Il n'y a pas de détection de téléportation :
un saut de position donne un rapport extrême, borné, pendant une frame.

**Paramètres de jeu.** Une liaison du `.sound` relie un paramètre nommé au volume ou au pitch :

```json
"parameter_bindings": [
  { "parameter": "intensity", "target": "Volume", "input_min": 0.0, "input_max": 1.0, "output_min": 0.3, "output_max": 1.0 },
  { "parameter": "intensity", "target": "Pitch",  "input_min": 0.0, "input_max": 1.0, "output_min": -0.3, "output_max": 0.3 }
]
```

L'entrée est bornée à sa plage, la sortie est interpolée linéairement (une plage d'entrée dégénérée
fait une marche) ; une sortie `Volume` est un facteur dans [0, 1], une sortie `Pitch` des octaves
dans [-1, 1] ; les liaisons de volume se multiplient, celles de pitch s'additionnent et la somme est
bornée à une octave. Un paramètre jamais écrit est neutre. Huit liaisons au plus par son.

```csharp
var intensity = audio.GetGameParameterIndex("intensity"); // une fois
audio.SetGameParameter(intensity, 0.8f);                   // à chaque changement, sans allocation
```

Registre de 64 paramètres ; `SetGameParameter(string, float)` ne crée le nom qu'une fois. Les pistes
de musique (`MusicPlayer`) reçoivent les liaisons de leur asset, pas la spatialisation. Le volume et
le pitch liés font partie des valeurs de départ de la voix : un son ou une piste liés ne jouent jamais
leur premier bloc à plein volume.

**Composition.** Gain de distance × facteurs des liaisons de volume, pan spatial, et rapport de vitesse
(Doppler × `2^somme` des liaisons de pitch) forment une modulation par voix, orthogonale au volume de
la voix, à ses fondus et aux bus. Sous le backend logiciel elle passe par un canal de dernières valeurs
au thread audio (capacité `IAudioVoiceModulationBackend`, aucune file, aucune attente) ; sous le
backend MonoGame, elle est repliée dans le volume et les paramètres envoyés, et le pitch total reste
borné à ±1 octave. Seuls les changements au-delà de petits seuils sont envoyés. `SetVoicePitch` et
`GetVoicePitch` règlent le pitch de base d'une voix.

**Composants.** La pose d'un écouteur ou d'un émetteur est lue dans la matrice monde sans échelle du
composant (`WorldMatrixNoScale`), qui compose les rotations des parents. Un composant placé au niveau
de l'entité (sans parent) garde sa propre matrice, comme le gizmo de l'éditeur ; dans une entité
enfant, la racine de l'entité parente est appliquée deux fois par le moteur (comportement existant).
Un émetteur ne suit son entité que si celle-ci est mise à jour (politique de tick).

---

## 6. Composant d'entité

`SoundEmitterComponent` pose un son sur une entité. C'est un **composant de scène** (ADR-0064) : il a
un transform, peut être racine ou enfant d'un composant de scène, et l'éditeur l'édite avec la section
Transform. Il apparaît automatiquement dans « Add Component ». Un émetteur sauvegardé sous l'ancienne
forme (sans transform ni enfants) se charge toujours, avec un transform identité.

| Propriété | Sens |
|---|---|
| `SoundAssetId` | asset `.sound` à jouer |
| `PlayOnStart` | joue dès l'entrée dans le monde |
| `IsLoopedOverride` | `null` garde la valeur de l'asset |
| `BusName` | vide garde le bus de l'asset |
| `VolumeOverride` | multiplie le volume de l'asset |
| `PitchOverride` | s'ajoute au pitch de l'asset |

Un asset marqué streaming part vers le `MusicPlayer`, les autres deviennent une voix normale.
Détacher le composant coupe le son.

Les surcharges du composant composent avec les variations de l'asset (§4) : `VolumeOverride` et
`PitchOverride` donnent la valeur de base, le tirage s'applique par-dessus. Le composant joue avec
la priorité de l'asset. Si l'asset a un mode spatial, l'émetteur joue à sa position monde
(`PlaySoundAt`) et la suit quand elle change (§5 quater) ; sinon il joue comme avant.

---

## 7. Cutscenes

Quatre actions sont disponibles :

| Action | Bloquante ? | Champs |
|---|---|---|
| `PlaySound` | non | `sound_asset_id`, `volume`, `bus_name` |
| `PlayMusic` | non | `sound_asset_id`, `fade_in_seconds`, `crossfade` |
| `StopMusic` | non | `fade_out_seconds` |
| `FadeMusic` | **oui** | `target_volume`, `duration_seconds` |

Les trois premières sont non bloquantes : une cutscene veut en général un son **par-dessus** une
action, pas à la place. `FadeMusic` attend la fin de la rampe, parce que l'action suivante doit
démarrer sur le nouveau niveau.

`PlaySound` compose avec les variations de l'asset comme l'émetteur (§6) : `volume` multiplie le
volume de l'asset, puis le tirage s'applique ; le son joue avec la priorité de l'asset.

---

## 8. Play-in-editor

L'éditeur et le jeu partagent le même processus et le même périphérique. La règle :

- **Stop** coupe toutes les voix du jeu, y compris celles qu'aucun monde ne possède ;
- **Pause** met ces voix en pause (un `TimeScale` à zéro gèle la simulation, pas le matériel
  audio) et **Resume** ne relance que ce que la pause de session avait arrêté ;
- le bus **`Editor`** est épargné dans les deux cas : une preview d'asset survit au Stop et reste
  audible pendant une session ;
- la preview de l'inspecteur de son joue **sans priorité** : elle ne vole jamais une voix du jeu,
  et elle est refusée quand toutes les voix sont prises. Elle tire les variations à chaque appui.

---

## 9. Limites connues (V1)

- **Pas de MP3, de FLAC ni d'Opus.** Ce seraient de nouvelles dépendances (question ouverte O13 du
  plan). Le `.mp3` n'est pas annoncé comme jouable dans le Content Browser. Convertir en `.wav` ou
  en `.ogg`.
- **Streaming : WAV PCM 16 bits uniquement.** C'est le format du contrat
  `IAudioBackend.SubmitBuffer`. Une piste **Ogg** marquée streaming est refusée avec un message
  clair : NVorbis alloue à chaque paquet décodé, ce que les règles du moteur interdisent pour le
  streaming (question ouverte O11). Un `.ogg` se joue donc en clip résident (`is_streaming` à faux).
- **Clips résidents** : WAV PCM entier 8/16/24/32 bits, flottant 32 bits (`WAVE_FORMAT_EXTENSIBLE`
  compris), ADPCM Microsoft (étiquette 2) et IMA/DVI (étiquette 0x11), Ogg Vorbis ; mono ou stéréo.
  L'ADPCM dans `WAVE_FORMAT_EXTENSIBLE` et les autres étiquettes (MP3 dans un WAV, a-law…) sont
  refusés avec une erreur consignée.
- **Backend MonoGame : streaming entre 8 000 et 48 000 Hz.** Limite de `DynamicSoundEffectInstance`
  (`MonoGameAudioBackend.MinStreamingSampleRate` / `MaxStreamingSampleRate`) : une piste hors plage
  est refusée avec un log throttlé, sans exception ni voix perdue. Le backend logiciel n'a pas cette
  limite. Sur le chemin historique (§5 bis), les voix stéréo rééchantillonnent encore par facteur
  entier.
- **Spatialisation** (§5 quater) : un seul point d'écoute ; pas de cône directionnel, de gain
  d'écouteur, de gain minimal ou maximal par source, ni de HRTF ; les pistes de musique et les sons de
  cinématique ne sont pas spatialisés ; pas de détection de téléportation pour le Doppler ; le mode
  spatial vient de l'asset seul (pas de surcharge par l'émetteur). Les liaisons de paramètres ne
  s'éditent pas dans l'inspecteur (le fichier `.sound` seulement). Un retour arrière de cette tranche
  rend illisibles les entités enregistrées depuis avec un émetteur en racine ou en enfant, ou avec un
  `AudioListenerComponent`.
- **Débranchement du périphérique** : le son s'arrête jusqu'au relancement du jeu (T2.6 en pause).
  Si le thread audio meurt, le backend logiciel devient muet sans faire attendre le jeu.
- **Limite de voix.** 64 par défaut côté backend. Au-delà, la voix est refusée avec un log
  throttlé, jamais une exception, sauf pour un son de priorité explicitement plus haute qu'une voix
  en cours, qui la vole (§4). Voix sans priorité et voix streamées jamais volées ; pas de voix
  virtuelles (une voix volée est perdue, elle ne reprend pas) ; pas de délai ni de séquence de
  sons.
- **Variations** : pas d'anti-répétition du fichier tiré ; un fichier de variation est chargé à sa
  première lecture, sur le thread de jeu (à-coup possible pour un gros fichier), et reste en
  mémoire ensuite.
- **Effets, départs, ducking et limiteur : backend logiciel seulement** (§2 bis). Sous le backend
  MonoGame, la sortie est seulement écrêtée.
- **Asset de mixeur** (§2 ter) : un bus ne se retire, ne se renomme ni ne se reparente pas à chaud ;
  après un changement de projet dans l'éditeur, les bus du projet précédent restent vivants (remis à
  leur volume d'origine, sans effet ni départ) jusqu'au redémarrage. Les muets ne sont pas dans
  l'asset. Le dessin d'un gros Ogg décode tout le fichier (comme sa lecture, l'Ogg étant résident).
- **Latence du backend logiciel** : environ 40 ms d'avance plus la mise en tampon du périphérique.
  Le thread audio se réveille au rythme de la minuterie Windows (environ 15,6 ms). Mesure du
  2026-10-05 : 0 sous-alimentation sur 60 s de stress (ramasse-miettes forcé toutes les 500 ms).
- **Pas de persistance des volumes utilisateur.** Les réglages de bus ne sont pas sauvegardés.
- **La sortie vers la carte son n'est pas couverte par les tests automatiques.** Le mixeur logiciel
  l'est, à l'échantillon près, hors ligne ; la sortie OpenAL passe par le mode stress de la démo.

---

## 10. Évolutions prévues

Le programme « audio moderne » ([plan](../../ai-agent/tasks/audio-modern-tasks.md), ADR-0055)
enchaîne, sur le mixeur logiciel, les tranches suivantes : temps réel et streaming hors du thread de
jeu (S2), formats Ogg et ADPCM (S3), bus et effets (S4, §2 bis), couche jeu avec priorités,
conteneurs et 3D (S5), outils de l'éditeur (S6), puis le module PSX (SPU logiciel livré,
[psx-spu.md](psx-spu.md) ; séquenceur SEQ/VAB et XA à venir). Les points ci-dessous viennent de la
V1 et y sont repris.

- Décodeur **Ogg Vorbis** branché sur `WavStreamReader`/`MusicPlayer` — NVorbis est déjà présent
  en dépendance transitive de MonoGame.
- **Persistance des volumes utilisateur** (réglages du joueur, distincts de l'asset de mixeur du
  projet).
- **Producteur en tâche de fond** pour le streaming, si la lecture disque devient audible.

---

## 11. Démo

`AudioDemo` dans `CasaEngine.Demos` :

| Touche | Effet |
|---|---|
| `Espace` | joue le SFX une fois |
| `L` | boucle le SFX / l'arrête |
| `F` | fade out du SFX bouclé (1 s) |
| `S` | arrête tout |
| `B` | bip mono sur une voix stéréo logicielle : gauche seule, puis droite seule, puis les deux (un pas par appui) |
| `P` | démarre la musique (fade in 1 s) / la fade out (2 s) |
| `C` | crossfade vers l'autre piste (2 s) |
| `PagePréc` / `PageSuiv` | volume du bus `Music` |
| `Haut` / `Bas` | volume du bus `Master` |
| `Gauche` / `Droite` | volume du bus `Sfx` |
| `M` / `N` | mute `Master` / `Sfx` |
| `R` | Réverbération : envoi de `Sfx` vers un bus de retour `DemoReverb` (enfant de Master, `ReverbEffect`), niveau 0,5, activé ou désactivé (backend logiciel seulement) |
| `T` | Filtre passe-bas à 600 Hz (`BiquadFilterEffect`) inséré sur le bus `Music`, activé ou désactivé (backend logiciel seulement) |
| `D` | Ducking (`DuckingEffect`) de `Music` par `Sfx`, activé ou désactivé : lancer la musique (`P`) puis un son (`Espace` ou `L`) pour entendre la musique baisser (backend logiciel seulement) |
| `G` | mode stress : SFX en boucle et musique, allocations massives et ramasse-miettes forcé toutes les 500 ms, avec le SPU PSX, une réverbération en départ et le limiteur du Master actifs ; arrêt par un second appui |
| `V` | joue `menu_click_varied.sound` : volume tiré dans [0,6 ; 1], pitch dans [-0,15 ; 0,15] octave |
| `J` | remplit toutes les voix libres de boucles du clic à faible volume, de priorité 1 (`S` les arrête) |
| `H` | joue le clic avec la priorité 10 : après `J`, il vole une boucle de priorité 1 ; `Espace` (sans priorité) est refusé |
| `O` | son spatial 3D (boucle du clic) en orbite autour de l'écouteur posé à l'origine : le pan passe de droite à gauche, le volume suit la distance (rayon de 2 à 12 unités sur 8 s) ; second appui : arrêt |
| `K` | Doppler activé ou coupé sur ce son (`SpeedOfSound` à 16 unités/s pour la démo : seule la variation du rayon le rend audible) ; la voix est relancée |
| `I` | paramètre de jeu `demo_intensity` à 0, 0,5 puis 1, lié au volume (0,3 à 1) et au pitch (−0,3 à +0,3 octave) du son spatial |

L'écran affiche le backend actif et le nombre de voix (actives, refusées, volées) ; avec le backend
logiciel, aussi l'avance, le débit de sortie et le compteur de sous-alimentations.

Mode stress sans clavier, depuis `CasaEngine.Demos/bin/Debug/net9.0-windows/` :

```bash
CASAENGINE_START_DEMO="Audio demo" CASAENGINE_AUDIO_BACKEND=Software CASAENGINE_AUDIO_STRESS_SECONDS=60 ./CasaEngine.Demos.exe
```

Le jeu se ferme seul ; `log.txt` contient une ligne `Audio stress: backend=… elapsed=… underruns=…`
par seconde, puis `Audio stress done: backend=… seconds=60 underruns=… gc=…`.
