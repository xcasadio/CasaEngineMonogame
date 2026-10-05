# Système audio CasaEngine — V1

Sons courts, musiques streamées et bus de mixage. Les décisions d'architecture sont figées dans
[analysis-audio-system.md](../../ai-agent/audits/analysis-audio-system.md) (§3).
Decisions: see [ADR-0001](../decisions/0001-audio-runtime-architecture-v1.md), [ADR-0002](../decisions/0002-audio-asset-format-and-editor-scope-v1.md), [ADR-0039](../decisions/0039-software-stereo-voices.md), [ADR-0040](../decisions/0040-project-audio-mute-setting.md) and [ADR-0055](../decisions/0055-engine-owned-software-audio-mixer-with-thin-native-outputs.md) (mixeur logiciel du moteur, §1 bis).
SPU PlayStation logiciel, hébergé par le backend logiciel : [psx-spu.md](psx-spu.md) ([ADR-0058](../decisions/0058-a-software-playstation-spu-hosted-by-the-software-audio-backend.md)).

---

## 1. Vue d'ensemble

```text
SoundAsset (.sound)          asset JSON : fichier audio + volume + pitch + loop + bus + streaming
        ↓
AudioService                 pool de voix, routage vers les bus, fades, propriété (owner)
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
  MonoGame, qui tourne la position OpenAL de la source.
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

Dans l'éditeur : clic droit sur un dossier → **Create Sound**, puis double-clic pour ouvrir
l'inspecteur (fichier, volume, pitch, loop, bus, streaming, preview).

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

Pas d'interface utilisateur : le réglage s'édite dans le fichier projet.

---

## 6. Composant d'entité

`SoundEmitterComponent` pose un son sur une entité. Il apparaît automatiquement dans
« Add Component ».

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

---

## 8. Play-in-editor

L'éditeur et le jeu partagent le même processus et le même périphérique. La règle :

- **Stop** coupe toutes les voix du jeu, y compris celles qu'aucun monde ne possède ;
- **Pause** met ces voix en pause (un `TimeScale` à zéro gèle la simulation, pas le matériel
  audio) et **Resume** ne relance que ce que la pause de session avait arrêté ;
- le bus **`Editor`** est épargné dans les deux cas : une preview d'asset survit au Stop et reste
  audible pendant une session.

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
- **Pas d'audio 3D.** Volume et pan uniquement : ni listener, ni atténuation par distance, ni
  Doppler.
- **Débranchement du périphérique** : le son s'arrête jusqu'au relancement du jeu (T2.6 en pause).
  Si le thread audio meurt, le backend logiciel devient muet sans faire attendre le jeu.
- **Limite de voix.** 64 par défaut côté backend. Au-delà, la voix est refusée avec un log
  throttlé, jamais une exception.
- **Pas de limiteur ni d'effet** (tranche S4) : le backend logiciel écrête simplement la sortie.
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
jeu (S2), formats Ogg et ADPCM (S3), bus et effets (S4), couche jeu avec priorités, conteneurs et
3D (S5), outils de l'éditeur (S6), puis le module PSX (SPU, séquenceur SEQ/VAB, XA). Les points
ci-dessous viennent de la V1 et y sont repris.

- Décodeur **Ogg Vorbis** branché sur `WavStreamReader`/`MusicPlayer` — NVorbis est déjà présent
  en dépendance transitive de MonoGame.
- **Audio 3D** : `SoundEmitterComponent` deviendrait un `SceneComponent`, avec listener et
  atténuation.
- **Panneau mixer** dans l'éditeur, et persistance des volumes.
- **Producteur en tâche de fond** pour le streaming, si la lecture disque devient audible.
- **Variations aléatoires** dans le `.sound` (liste de fichiers, plages de volume/pitch).

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
| `G` | mode stress : SFX en boucle et musique, allocations massives et ramasse-miettes forcé toutes les 500 ms ; arrêt par un second appui |

L'écran affiche le backend actif et le nombre de voix ; avec le backend logiciel, aussi l'avance,
le débit de sortie et le compteur de sous-alimentations.

Mode stress sans clavier, depuis `CasaEngine.Demos/bin/Debug/net9.0-windows/` :

```bash
CASAENGINE_START_DEMO="Audio demo" CASAENGINE_AUDIO_BACKEND=Software CASAENGINE_AUDIO_STRESS_SECONDS=60 ./CasaEngine.Demos.exe
```

Le jeu se ferme seul ; `log.txt` contient une ligne `Audio stress: backend=… elapsed=… underruns=…`
par seconde, puis `Audio stress done: backend=… seconds=60 underruns=… gc=…`.
