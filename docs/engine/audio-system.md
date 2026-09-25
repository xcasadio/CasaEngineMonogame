# Système audio CasaEngine — V1

Sons courts, musiques streamées et bus de mixage. Les décisions d'architecture sont figées dans
[analysis-audio-system.md](../../ai-agent/audits/analysis-audio-system.md) (§3).
Decisions: see [ADR-0001](../decisions/0001-audio-runtime-architecture-v1.md), [ADR-0002](../decisions/0002-audio-asset-format-and-editor-scope-v1.md), [ADR-0039](../decisions/0039-software-stereo-voices.md) and [ADR-0040](../decisions/0040-project-audio-mute-setting.md).

---

## 1. Vue d'ensemble

```text
SoundAsset (.sound)          asset JSON : fichier audio + volume + pitch + loop + bus + streaming
        ↓
AudioService                 pool de voix, routage vers les bus, fades, propriété (owner)
   ├─ AudioMixer             arbre de bus nommés, gain effectif
   ├─ MusicPlayer            pistes streamées, fade in/out, crossfade
   └─ IAudioBackend          frontière plateforme
              ├─ MonoGameAudioBackend   OpenAL, SoundEffect + DynamicSoundEffectInstance
              ├─ NullAudioBackend       aucun périphérique : tout devient silencieux
              └─ FakeAudioBackend       tests (dans CasaEngine.Tests)
        ↑
AudioSystemComponent         GameComponent : possède le backend, appelle Update
```

Point d'entrée depuis le jeu : `game.AudioSystemComponent.Service`.

`AudioService` ne contient **aucun type MonoGame ni `Game`** : c'est ce qui rend les bus, les voix,
les fades et le streaming testables, un périphérique OpenAL ne pouvant pas être ouvert en CI.

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
(`IAudioClipSamples`, porté par `MonoGameAudioClip` quand `SoundEffectLoader` a pu décoder le wav
source en mono 16 bits PCM) ; sinon `PlayClipStereo` renvoie `AudioVoiceHandle.None`.

Limites (voir [ADR-0039](../decisions/0039-software-stereo-voices.md)) :

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

- **Pas de MP3.** MonoGame DesktopGL ne sait pas le décoder, ni en effet ni en musique. Le `.mp3`
  n'est plus annoncé comme jouable dans le Content Browser. Convertir en `.wav`.
- **Streaming : PCM 16 bits uniquement.** C'est le format attendu par
  `DynamicSoundEffectInstance`, donc aucune conversion n'est faite. Les autres variantes de `.wav`
  (8/24 bits, float, ADPCM) restent lisibles en mode non streamé.
- **Pas d'audio 3D.** Volume et pan uniquement : ni listener, ni atténuation par distance, ni
  Doppler.
- **Lecture disque sur le thread de jeu.** Le remplissage des buffers se fait dans `Update`
  (~88 Ko/s pour une musique 22 kHz stéréo 16 bits), avec environ une demi-seconde de file
  d'avance contre les frames longues.
- **Limite de voix.** 64 par défaut côté backend, contre 256 sources OpenAL disponibles. Au-delà,
  la voix est refusée avec un log throttlé, jamais une exception.
- **Pas de persistance des volumes utilisateur.** Les réglages de bus ne sont pas sauvegardés.
- **La lecture réelle n'est pas couverte par les tests.** Un périphérique OpenAL ne peut pas être
  ouvert en CI ; toute la logique l'est via `FakeAudioBackend`, le reste passe par la démo.

---

## 10. Évolutions prévues

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
