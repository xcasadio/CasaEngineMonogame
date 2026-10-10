# Plan agent IA — Point de boucle de la musique en flux (E19.L-a)

> Note (2026-10-10) : l'ADR de ce chantier est numérotée **ADR-0074** depuis la fusion d'`origin/main` du moteur, dont l'ADR-0072 enregistre l'écran principal des démos. Les comptes rendus ci-dessous gardent l'ancien numéro, comme les messages de commit.

Plan d'exécution de la moitié moteur de la boucle au repère de séquence, décidée dans le dépôt parent (décision D-E19-102, section 1.2w.1 de
`docs/plan-e19-opcodes.md` du dépôt parent : règles LA-R1 à LA-R7, faits et valeurs dans `docs/plan-e19-loop-annexe/la-1008/` du dépôt parent).
Les décisions D1 → D6 ci-dessous sont celles du plan du dépôt parent, relu deux fois jusqu'à READY : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Une piste de musique streamée et bouclée revient, à la fin de ses données, à la trame `loop_start_sample` de son `.sound` au lieu de la
trame 0. Livré par la tâche T1 : `SoundAsset.LoopStartSample`, `WavStreamReader.SeekToSample`, les deux sites de boucle (`MusicPlayer.FillQueue` et
le travailleur de flux), le sérialiseur de l'éditeur et la copie d'aperçu de l'inspecteur, les tests, la documentation et l'ADR-0072.

## État vérifié du dépôt (2026-10-08)

- Branche `chantier/e19-suite2` au-dessus de `f86a981b` (merge du navigateur de démos par l'auteur). L'arbre de travail contient les
  modifications de l'auteur à `CasaEngine.Launcher/Program.cs` : jamais indexées.
- Deux sites rembobinent au début : `MusicPlayer.FillQueue` et `StreamingWorker.Channel.Service` (chemin de production, `AudioService.cs:98`).
- `SoundAsset.ReadNumber` lit par un flottant : 16 777 217 deviendrait 16 777 216 ; la clé se lit donc par un entier exact.
- Dernier numéro d'ADR de toutes les branches et worktrees locaux : 0071 ; ce plan prend 0072.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | `SoundAsset.LoopStartSample` (`long`, en trames, défaut 0, un négatif ramené à 0) ; clé entière `loop_start_sample`, écrite seulement si non nulle, juste après `is_streaming` ; aucune ligne d'inspecteur (LA-R1, LA-R2). |
| D2 | `WavStreamReader.SeekToSample(trame)` se place à l'offset des données de l'en-tête plus `trame × BlockAlign`, lève hors de `[0, DataLength / BlockAlign)`, n'alloue rien ; `Rewind()` devient `SeekToSample(0)` (LA-R3). |
| D3 | `Play` vérifie le point de boucle une fois, pour une piste bouclée ; à la fin des données ou au-delà : un avertissement (délai unique du lecteur) et retour à 0 (LA-R4). |
| D4 | Les deux sites de boucle se placent au point de boucle ; `Acquire` et `Channel.Reset` reçoivent l'argument, écrit avant que l'état du canal devienne actif (LA-R5). |
| D5 | La lecture s'arrête toujours à la fin des données : un tampon court à chaque raccord, aucun remplissage à travers le raccord (LA-R6). |
| D6 | Hors périmètre : fin de boucle, fondu du moteur, points de boucle des sons résidents, interface des backends, la DLL et les données (LA-R7). |
| D7 | (2026-10-08, l'auteur, D-E19-104) La profondeur par défaut de la file de musique passe de 3 à 24 buffers pour tout le moteur (T2, ADR-0073). |
| D8 | (2026-10-08, l'auteur, D-E19-107) La démo versionne un WAV généré de 529 260 octets (T3). |

## Règles d'exécution pour l'agent

- Branche dédiée `chantier/e19-suite2` (réutilisée). Une seule tâche à la fois, un commit par tâche, jamais de push.
- Tests d'abord, rouges, puis le code (chaque règle LA-R1 à LA-R4 a au moins un test nommé) ; mutations une par une, chacune rouge sur ses tests.
- Langue : ce plan en français ; code, commits, docs et ADR en anglais.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj -c Release` puis `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build -c Release` :
  vert, base de 4194 tests au `f86a981b` plus les tests nouveaux, aucun test existant ne bouge.

---

## Phase 1 — Moteur (T1)

### ✅ T1.1 — Tests d'abord

- Objectif : les tests W1 à W7, S1 à S7, L1 à L8, B2 et P1 du plan du dépôt parent, avec les valeurs écrites à l'avance, rouges contre des
  bouchons qui lèvent `NotImplementedException`.
- Fichiers : `CasaEngine.Tests/Audio/MusicPlayerLoopStartTests.cs` (nouveau, l'aide de la rampe dedans), `WavStreamReaderTests.cs`,
  `SoundAssetTests.cs`, `SoundAssetEditorSerializationTests.cs`, `CasaEngine.Tests/Editor/SoundAssetInspectorPanelTests.cs`.
- Validation : 30 des 31 cas nouveaux rouges (le 31e, « aucune clé écrite à 0 », est le garde de la mutation « clé écrite à 0 »).
- Commit : `test(audio): a streamed music track loops back to its loop-start sample, red first (E19.L-a)` (`674549cc`).

### ✅ T1.2 — Code

- Objectif : les règles LA-R1 à LA-R6.
- Fichiers : `CasaEngine/Framework/Audio/SoundAsset.cs`, `Streaming/WavFormatInfo.cs`, `Streaming/WavStreamReader.cs`, `Streaming/MusicPlayer.cs`,
  `Streaming/StreamingWorker.cs`, `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`, `CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs`.
- Validation : tous les tests nouveaux verts ; mutations (chacune rouge sur ses tests) : offset des données ignoré, rembobinage gardé dans
  `FillQueue`, rembobinage gardé dans le travailleur, plafond par appel retiré, décalage de un dans la borne, clé non écrite, clé écrite à 0,
  lecture flottante, copie d'aperçu absente.
- Commit : `feat(audio): a streamed music track loops back to its loop-start sample (E19.L-a T1, ADR-0072)`.

### ✅ T1.3 — Documentation et ADR-0074 (numérotée ADR-0072 jusqu'au 2026-10-10)

- Fichiers : `docs/engine/audio-system.md` (§3, §5, §9), `docs/decisions/0074-a-streamed-music-track-loops-back-to-its-loop-start-sample.md`,
  `docs/decisions/README.md`, ce plan, `ai-agent/README.md`.
- Validation : relecture ; numéro d'ADR revérifié sur toutes les branches avant le commit.
- Commit : avec T1.2.

---

## Phase 2 — En attente de l'auteur

### ✅ T2 — Profondeur de la file par défaut (D-E19-75, D-E19-104, O-E19-63)

- `MusicPlayer.DefaultQueuedBufferTarget` de 3 à 24 (marge à la couture d'environ 2 s contre 96 ms) ; coût : 384 Kio de file et un anneau de
  768 Kio par piste jouée ; `audio-system.md` §5 réécrit pour 44,1 kHz ; la ligne de doc de `StereoVoiceMixer.cs` corrigée (le mélangeur
  garde ses 3 buffers) ; deux tests bougent (`Play_StartsAStreamAndQueuesAhead`, `Update_TopsTheQueueBackUpAsBuffersAreConsumed` : 24, 22, 24
  avec `SampleCount` = 106 496). Tâche à part, révocable seule.
- Décision enregistrée dans l'ADR-0073 : l'ADR-0072 est acceptée et une décision ne se réécrit pas (`docs/decisions/README.md`), donc un
  nouvel ADR ; plus grand numéro de toutes les branches et worktrees avant lui : 0072.
- Commit : `perf(audio): the default music queue depth goes from 3 to 24 (E19.L-a T2, D-E19-104)`.

### 🧪 T3 — Démo du point de boucle

- Touche `Q` de `AudioDemo.cs` (libre parmi A, E, Q, U, W, X, Y, Z ; ligne de l'aide et ligne d'état avec la position et la file),
  `loop_point_demo.wav` (529 260 o, données à l'offset 60, généré par `docs/plan-e19-loop-annexe/la-1008/gen_loop_point_demo.py` du dépôt
  parent, versionné : D8) et `loop_point_demo.sound` (`loop_start_sample` 44 100), `AssetInfos.json`, `Content.mgcb`, ligne de §11 de
  `audio-system.md`, ligne ajoutée à la liste fixe de `Loader_DemoSoundFiles_HaveTheVariationDefaults`.
- Validation faite : la démo lancée depuis `CasaEngine.Demos/`, touche `Q`, capture du back-buffer par le chemin de la démo (la piste joue, 24
  buffers en file, position 3,0 s, 0 sous-alimentation), journal sans avertissement. **Reste 🧪 : l'écoute par l'auteur** (un ton de 440 Hz
  une fois, puis des bips de 660 Hz toutes les 0,5 s, sans trou ni clic au raccord).

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | ~~Profondeur par défaut de la file~~ : tranchée le 2026-10-08, 24 pour tout le moteur (D7) | T2 |
| O2 | Le fichier WAV de la démo est-il versionné dans le moteur ? Tranché le 2026-10-08 : oui (D8) | T3 |

## Hors périmètre

- Fin de boucle, fondu de raccord du moteur, points de boucle des sons résidents, interface `IAudioBackend`, la DLL et les données du port (parent, L-b).
- Une ligne d'inspecteur pour le point de boucle (son champ numérique est flottant).
