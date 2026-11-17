# Plan agent IA — Sortie audio à faible latence

Plan d'exécution du chantier « un son se fait entendre dès qu'il est demandé ».
La décision D1 ci-dessous a été donnée par l'auteur le 2026-11-17 : **ce plan l'applique, il ne la rediscute pas**. Les points P1 → P9, proposés par l'agent, ont été approuvés par l'auteur le 2026-11-17 (« J'approuve et lance »). Avant l'approbation, deux relectures neuves : REVISE (perte du périphérique, code `unsafe`, seuil des sous-alimentations, budget et retour arrière, corrigés), puis READY.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Avec le backend logiciel, un son d'interface s'entend environ 100 ms après le geste qui le lance : le marqueur de focus a déjà bougé quand le son arrive. Le chantier ramène ce délai au plus bas que permet la sortie OpenAL Soft partagée de Windows :

- le tampon du périphérique passe de 60 ms à la plus petite valeur que Windows accorde (22 ms mesurées sur le PC de l'auteur) ;
- l'avance de 40 ms que le mixeur garde dans la file de la source OpenAL disparaît : OpenAL Soft demande lui-même le mix, au moment où il en a besoin.

Il ne livre pas de sortie WASAPI propre, ni de mode exclusif (« Hors périmètre »).

## État vérifié du dépôt (2026-11-17)

- `main` vaut `00b24bca`, égal au pointeur du dépôt parent. La branche `chantier/low-latency-audio-output` en part le 2026-11-17. Arbre : seul `Projects/PixelUiSample/.casaeditor/` est non suivi, hors de ce chantier.
- **Chemin d'un son.** `AudioService.PlaySound` (`AudioService.cs:658-666`) appelle `SoftwareAudioBackend.Play` (`SoftwareAudioBackend.cs:162`), qui pousse une commande dans l'anneau du mixeur (`SoftwareMixer.TryStartResidentVoice`, `SoftwareMixer.cs:290-303`). Le thread audio ne lit l'anneau qu'au début de `SoftwareMixer.Render` (`SoftwareMixer.cs:1060-1083`), donc à la prochaine recharge d'un tampon. La nouvelle voix entre dans ce tampon, derrière ceux déjà en file.
- **File.** `OpenAlAudioOutput(int bufferCount = 4, int bufferFrames = 0)` (`OpenAlAudioOutput.cs:38-44`) : 4 tampons de `sampleRate / 100` trames (`:230`), soit 40 ms d'avance (`SoftwareAudioBackend.LeadMilliseconds`, `SoftwareAudioBackend.cs:147-154`). Seul `SoftwareAudioBackend()` la construit (`:80-81`), lui-même construit sans argument par `AudioSystemComponent.TryCreateSoftwareBackend` (`AudioSystemComponent.cs:171`).
- **Rien d'autre ne dépend de l'avance.** Hors de la sortie et du backend, aucun code ne lit `LeadMilliseconds`, `BufferCount` ni le `BufferFrames` de la sortie (`rg`). Le `BufferFrames` de `StereoVoiceMixer.cs:33-87` est le sien. Le mixeur prend `BufferFrames` comme taille de bloc (`SoftwareAudioBackend.cs:108`) et rend une demande plus longue en plusieurs blocs (`SoftwareMixer.cs:1077-1082`).
- **Boucle.** `RunLoop` (`OpenAlAudioOutput.cs:236-260`) appelle `OpenAlRefillLoop.Pump`, puis `Thread.Sleep(2)`. Aucun `timeBeginPeriod` dans le moteur : le thread se réveille en fait toutes les 15,6 ms environ (`docs/engine/audio-system.md:704-706`, mesure de `audio-modern-tasks.md:415-416`).
- **Thread et contexte.** Le thread « CasaEngine Audio » ouvre son propre périphérique (`alcOpenDevice(null)`, `OpenAlAudioOutput.cs:169`) et un contexte local au thread (`ALC_EXT_thread_local_context`, `:176-191`), sans toucher au contexte global de MonoGame.
- **OpenAL Soft 1.24.3** (`MonoGame.Library.OpenAL` 1.24.3.4, `OpenAlNative.cs:7`), sources lues au tag `1.24.3` (https://github.com/kcat/openal-soft/tree/1.24.3) :
  - tampon par défaut : `period_size` 960 trames, 3 périodes, soit 60 ms à 48 kHz (`alc/alc.cpp:1111-1133`, `core/device.h:45-46`) ;
  - seul un fichier de configuration le change. `ALC_REFRESH` n'est pas lu à la création du contexte (`alc/alc.cpp:1259-1329`), et aucun attribut ne fixe la période ;
  - fichiers lus sous Windows, chacun écrasant les clés du précédent : `%AppData%\alsoft.ini`, puis **`alsoft.ini` dans le dossier de l'exécutable**, puis le fichier de `ALSOFT_CONF` (`alc/alconfig.cpp:333-378`). La lecture a lieu une fois, au premier `alcOpenDevice` (`alc/alc.cpp:436`, `754-755`, `2951`) ;
  - en WASAPI partagé, `period_size × periods` n'est que la durée demandée ; Windows accorde la taille du tampon (`alc/backends/wasapi.cpp:2044`, `2267-2279`, `2315-2318`), et la latence rapportée est le remplissage de ce tampon (`wasapi.cpp:2370-2385`) ;
  - `AL_SOFT_callback_buffer` : `alBufferCallbackSOFT(buffer, format, freq, callback, userptr)`, rappel `ALsizei (*)(ALvoid *userptr, ALvoid *sampledata, ALsizei numbytes)` (`include/AL/alext.h:583-591`), appelé depuis le mixage d'OpenAL Soft (`core/voice.cpp:916-945`), sur le thread de lecture WASAPI. Un tampon par source, non mis en file (`al/source.cpp:1621-1623`, `3572-3574`). Un retour plus court arrête la voix (`core/voice.cpp:930-936`) ;
  - `alSourceStop` attend la fin du mix en cours (`al/source.cpp:569-579`, `waitForMix`) ;
  - **perte du périphérique** : le thread WASAPI appelle `handleDisconnect` puis sort de sa boucle de mix (`alc/backends/wasapi.cpp:1306-1312`, `1358-1364`). Plus aucun rappel n'arrive. `ALC_CONNECTED` (`ALC_EXT_disconnect`, `alext.h:158-160`) se lit par `alcGetIntegerv`, déjà lié (`OpenAlNative.cs`) ;
  - la latence rapportée est le remplissage du tampon à l'instant de la lecture (`wasapi.cpp:1313`, `1341`, `1347`, `2370-2376`) : elle vaut 0 avant le premier passage du mix (`wasapi.cpp:1597-1606`) et baisse entre deux recharges. Une lecture unique à l'ouverture ne vaut rien.
- **Mesures hors dépôt** (sondes en C# appelant l'`openal.dll` du Launcher, PC de l'auteur, sortie à 48 kHz) :
  - réglages par défaut : latence du périphérique de 60 ms (`ALC_DEVICE_LATENCY_SOFT` et `AL_SEC_OFFSET_LATENCY_SOFT`) ; l'attribut `ALC_REFRESH` n'y change rien ;
  - `alsoft.ini` à côté de l'exécutable, `period_size = 480`, `periods = 2` : 22 ms. Les demandes de 240 × 2 et 128 × 2 donnent aussi 22 ms, le plancher accordé par Windows ;
  - tampon à rappel, avec ce fichier : OpenAL Soft demande 480 trames par appel en moyenne (504 au plus), un appel toutes les 10 ms ; l'intervalle le plus long entre deux appels vaut 10,5 ms sur 10 s, et 15,5 ms sur 30 s sous une pression du ramasse-miettes d'environ 60 collections de génération 2 par seconde ;
  - pour comparaison, une file courte relue par `Thread.Sleep` sans minuterie de 1 ms se vide (347 à 651 coupures sur 10 s pour 2 × 10 ms, 3 × 5 ms ou 2 × 5 ms).
- **Attente du jeu.** Une commande d'état du jeu (lecture, arrêt, gains, bus, SPU…) qui trouve l'anneau du mixeur plein (4096 commandes, `SoftwareMixer.cs:52`) tourne dans `RingWait` jusqu'à 100 ms, sauf si la sortie n'est plus disponible (`SoftwareAudioBackend.cs:1140-1171`). En file, une source arrêtée par la perte du périphérique compte tous ses tampons comme traités, et la boucle continue d'appeler le mixeur (`OpenAlRefillLoop.cs:51-61`, `al/source.cpp:2369-2394`) : l'anneau se vide.
- **Règles déjà écrites pour une liaison native dans ce sous-système** (`ai-agent/tasks/audio-modern-tasks.md:900-916`, tâche en pause) : liaison **sans code `unsafe`** (le projet n'active pas `AllowUnsafeBlocks`, `CasaEngine.csproj` ne change pas pour du code), par `Marshal.GetDelegateForFunctionPointer` et `Marshal.GetFunctionPointerForDelegate`, délégués créés une fois et **gardés dans un champ** ; pendant une coupure du périphérique, le thread audio **continue d'appeler le rendu du mixeur au rythme du périphérique**, dans un tampon jeté, pour qu'**aucun appel du jeu n'attende**.
- **Tests.** `OpenAlRefillLoopTests.cs` (5 tests, faux flux `IOpenAlStream`), `OfflineAudioOutput.cs` (sortie factice, 4 × 480 trames), `SoftwareAudioBackendTests.cs:41-43` (arithmétique de `LeadMilliseconds`). Aucun test ne pilote `OpenAlAudioOutput` ni `OpenAlNative` (`docs/engine/audio-system.md:708-709`).
- **Harnais de stress.** `CasaEngine.Demos/Demos/AudioDemo.cs` : `CASAENGINE_AUDIO_STRESS_SECONDS=<n>` joue un son en boucle et la musique, alloue 8 × 512 Ko par image et force `GC.Collect(2, Forced, true)` toutes les 500 ms, puis consigne `Audio stress done: backend=… seconds=<n> underruns=<n> gc=<n>` et ferme le jeu (`audio-modern-tasks.md:499-535`). Il fait du son pendant tout le passage.
- **Sample.** `Projects/PixelUiSample` joue `cursor_move.sound` à chaque déplacement du focus (`Screens/title.uiscreen:56`).
- Dernier ADR : 0093. Ce plan prend 0094.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | « il ne faut aucune latence » : le délai entre la demande d'un son et sa sortie est ramené au plus bas que la sortie permet. Le plancher qui reste (tampon accordé par Windows, matériel) est mesuré et écrit. |
| D2 | Le moteur reste générique : ni nom, ni donnée, ni exemple tiré d'un jeu dans le code, les documents ou les tests. |

## Points validés

| Réf | Proposition |
|---|---|
| P1 | Le moteur livre un `alsoft.ini` à côté de chacun de ses exécutables (Launcher, éditeur, démos, et tout projet qui référence `CasaEngine`) : `[general]`, `period_size = 480`, `periods = 2`. Il remplace ces deux clés d'un `%AppData%\alsoft.ini` ; `ALSOFT_CONF` reste lu après lui et peut encore les changer. Il vaut aussi pour le backend MonoGame, qui charge la même bibliothèque. Une variable `ALSOFT_CONF` posée par le moteur n'est pas retenue : OpenAL Soft la lit par `_wgetenv` (`common/strutils.cpp:62-68`), et rien ne prouve qu'une variable posée par le processus après son démarrage y arrive. |
| P2 | `OpenAlAudioOutput` lit le mix par `AL_SOFT_callback_buffer` quand l'extension est présente : un seul tampon à rappel sur la source, plus aucune file. **Liaison sans `unsafe`**, selon la règle déjà écrite : `alBufferCallbackSOFT` obtenu par `alGetProcAddress`, appelé par un délégué de `Marshal.GetDelegateForFunctionPointer` ; le rappel est un délégué gardé dans un champ, passé par `Marshal.GetFunctionPointerForDelegate`. Le mixeur rend la demande par blocs d'au plus `BufferFrames` trames dans un tableau `float[]` alloué à l'ouverture, et `Marshal.Copy` copie chaque bloc à sa place dans la mémoire d'OpenAL Soft : la taille d'une demande n'est pas bornée par ce tableau. `CasaEngine.csproj` ne change pas pour ce code. Sans l'extension, ou si `alBufferCallbackSOFT` échoue, la sortie garde la file actuelle (4 × 10 ms), avec un avertissement au journal. |
| P3 | Aucune exception ne sort du rappel. Une exception est journalisée une fois, la sortie devient indisponible comme aujourd'hui (`OpenAlAudioOutput.cs:154-160`), et le rappel rend du silence jusqu'à l'arrêt. |
| P4 | En mode rappel, une sous-alimentation est comptée quand deux appels sont séparés de plus qu'un **seuil fixé après une fenêtre de 500 ms** de lecture. Pendant la fenêtre, le thread « CasaEngine Audio » lit `ALC_DEVICE_LATENCY_SOFT` à chaque passage et garde la plus grande valeur, et le rappel garde sa plus grande demande. Seuil = la plus grande des deux valeurs : la plus grande latence lue, et deux fois la durée de la plus grande demande. Sans `ALC_SOFT_device_clock`, seule la seconde vaut. Rien n'est compté pendant la fenêtre. C'est une estimation, écrite comme telle dans la doc. `UnderrunCount` garde son nom et son sens pour la file. |
| P5 | `LeadMilliseconds` vaut 0 en mode rappel. À la fin de la fenêtre de 500 ms, la sortie écrit une ligne au journal : mode (rappel ou file), fréquence, latence du périphérique (la plus grande lue dans la fenêtre, ou « inconnue »), avance, et en mode rappel le seuil de P4 retenu. |
| P6 | Ordre d'arrêt : `alSourceStop`, suppression de la source et du tampon, contexte, périphérique ; le délégué du rappel reste gardé dans son champ jusqu'après `alcCloseDevice`. Après `alcCloseDevice`, le thread de lecture d'OpenAL Soft est arrêté : aucun rappel ne peut plus arriver. |
| P8 | **Perte du périphérique en mode rappel**, même comportement qu'en file : le thread « CasaEngine Audio » lit `ALC_CONNECTED` à chaque passage. Une fois le périphérique perdu, il écrit une ligne au journal et appelle lui-même le rendu du mixeur au rythme du périphérique (trames dues d'après le temps écoulé, par blocs de `BufferFrames`), dans un tampon jeté. Les commandes du jeu sont vidées, les voix avancent et finissent, `IsAvailable` reste vrai. Pas de réouverture dans ce chantier. |
| P9 | Un seul rendu à la fois : un drapeau `Interlocked` garde le rendu. Un rappel qui le trouve pris rend du silence. |
| P7 | Branche `chantier/low-latency-audio-output` depuis `main` `00b24bca`, dans le checkout du sous-module, comme les chantiers précédents. Un worktree à part ne peut pas initialiser `MGUI` : son commit `a3d6c87d` n'est sur aucune branche distante. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/low-latency-audio-output`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Les sources d'OpenAL Soft se lisent au tag `1.24.3`. Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché (`dotnet build CasaEngine.MonoGame.sln`, et `dotnet build CasaEngine.Editor.MonoGame.sln`, l'éditeur ayant la même sortie) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, sans `--no-build`. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- **Budget.** Par tâche, deux essais d'exécution au plus ; au troisième échec, ⚠️ Blocked et arrêt. Deux passages du harnais de 60 s au plus par build et par tâche. T1.4 : un vérificateur, puis au plus un vérificateur de clôture après correction. Budget épuisé → ⚠️ Blocked, arrêt, question à l'auteur.
- **Retour arrière.** Tout reste sur la branche tant que l'auteur ne fusionne pas : `main` ne change pas. Revenir à la file : `git revert` du commit de T1.2. Revenir au tampon de 60 ms : `git revert` du commit de T1.1 (ou retrait de `alsoft.ini`). Après une fusion, le retour se fait par un commit de revert sur une nouvelle branche, au choix de l'auteur.
- **Le harnais de stress fait du son pendant 60 s** : le lancer seulement quand l'auteur est là ou l'a accepté.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit et ADR en anglais. `docs/engine/audio-system.md` est en français : le compléter dans sa langue.
- Rappel moteur : le rappel est un chemin chaud sur un thread natif temps réel. Ni allocation, ni LINQ, ni closure, ni verrou bloquant, ni journal à chaque appel, ni exception qui en sort, ni appel AL ou ALC.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout passe.
- Harnais de stress, 60 s, depuis `CasaEngine.Demos/bin/Debug/net9.0-windows/` avec `CASAENGINE_START_DEMO="Audio demo"`, `CASAENGINE_AUDIO_BACKEND=Software`, `CASAENGINE_AUDIO_STRESS_SECONDS=60` : `underruns=0`, et la ligne de P5 montre le mode rappel, la latence du périphérique, une avance de 0 ms et le seuil retenu.
- Recette de l'auteur : `Projects/PixelUiSample` au Launcher ; le son du curseur part avec le déplacement du marqueur.

---

## Phase 0 — Plan et décision

### ✅ T0.1 — Plan, index et ADR-0094

- Objectif : enregistrer le plan approuvé et la décision.
- Fichiers : `ai-agent/tasks/low-latency-audio-output-tasks.md`, `ai-agent/README.md`, `docs/decisions/0094-the-openal-output-pulls-the-mix-with-a-small-device-buffer.md`, `docs/decisions/README.md`.
- Étapes :
  1. Écrire ce plan, avec les P1 à P7 approuvés et la date de l'approbation.
  2. Ajouter sa ligne au tableau de `ai-agent/README.md`.
  3. Écrire l'ADR-0094 depuis `docs/decisions/template.md` : contexte (100 ms mesurées, sources d'OpenAL Soft citées), décision (P1, P2, P4, P8), conséquences. Il remplace, pour la sortie OpenAL, la phrase de l'ADR-0055 sur l'avance de 40 ms, et le dit.
- Validation : relecture du texte ; `rg -i` des noms de jeux et de projets connus de l'auteur : 0 occurrence (D2).
- Commit : `docs(audio): plan a low-latency OpenAL output and record ADR-0094`
- Note de validation (2026-11-17) : plan, ligne d'index, ADR-0094 et sa ligne d'index écrits. D2 : aucune occurrence d'un nom de jeu ni de projet (`rg`).

---

## Phase 1 — Sortie

### ✅ T1.1 — Tampon du périphérique réduit et latence au journal

- Objectif : le tampon du périphérique passe de 60 ms au plancher accordé par Windows, et la sortie dit ce qu'elle obtient.
- Fichiers : `CasaEngine/Framework/Audio/Output/OpenAl/alsoft.ini` (nouveau), `CasaEngine/CasaEngine.csproj`, `CasaEngine/Framework/Audio/Output/OpenAl/OpenAlNative.cs`, `CasaEngine/Framework/Audio/Output/OpenAl/OpenAlAudioOutput.cs`, un test dans `CasaEngine.Tests/Audio/Output/`.
- Étapes :
  1. Écrire `alsoft.ini` (P1), avec un commentaire qui renvoie à l'ADR-0094.
  2. Le déclarer dans `CasaEngine.csproj` pour qu'il soit copié à la racine de la sortie de tout projet qui référence `CasaEngine`. Si la copie n'atteint pas un exécutable du moteur, ajouter l'élément au projet de cet exécutable.
  3. Ajouter à `OpenAlNative` `alcGetInteger64vSOFT` (par `alcGetProcAddress` et un délégué, sans `unsafe`) et `ALC_DEVICE_LATENCY_SOFT` (`alext.h:494-501`), avec le test de `ALC_SOFT_device_clock`.
  4. Dans la boucle de la file, pendant les 500 ms qui suivent le départ, lire la latence à chaque passage et garder la plus grande, puis écrire la ligne de P5 (mode file, avance 40 ms). Sans l'extension, la latence est notée inconnue. La mesure de fenêtre est une petite classe sans code natif, testée : une première lecture à 0 ou basse ne change pas le maximum retenu.
  5. Test : le fichier copié dans la sortie des tests contient exactement les deux clés de P1 sous `[general]`.
- Validation : builds et tests de la « Validation globale ». `alsoft.ini` est à la racine de `CasaEngine.Launcher/bin/Debug/net9.0-windows/`, de `CasaEngine.Editor/bin/Debug/net9.0-windows/` et de `CasaEngine.Demos/bin/Debug/net9.0-windows/`. Le harnais de stress (60 s) écrit la ligne de P5, avec une latence du périphérique de 22 ms sur le PC de l'auteur, et `underruns=0`.
- Stop : `alsoft.ini` absent d'une des trois sorties après l'étape 2 et son repli → ⚠️ Blocked. Latence de la ligne de P5 qui ne descend pas sous 60 ms → ⚠️ Blocked, question à l'auteur.
- Commit : `feat(audio): ask OpenAL Soft for a small device buffer and log the output latency`
- Note de validation (2026-11-17) : exécuté par un sous-agent `executor`, diff relu en session principale. `alsoft.ini` déclaré par `<None Update=… Link="alsoft.ini" CopyToOutputDirectory="PreserveNewest" />` dans `CasaEngine.csproj` (le SDK le prend déjà comme `None`) : il arrive à la racine des sorties du Launcher, de l'éditeur, des démos et des tests sans toucher à leurs projets. `OpenAlLatencyWindow` (sans code natif) garde la plus grande lecture des 500 ms ; `alcGetInteger64vSOFT` est lié par `alcGetProcAddress` et un délégué gardé en champ, sans `unsafe`. Builds : `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln`, 0 erreur. Tests : 5656 sur 5656 (9 nouveaux). Harnais de stress, 60 s, PC de l'auteur : `OpenAL audio output: mode=queue, sample rate=48000 Hz, device latency=22.0 ms, lead=40.0 ms.`, puis `Audio stress done: backend=SoftwareAudioBackend seconds=60 underruns=0 gc=119`.

### 🧪 T1.2 — Mix lu par le rappel d'OpenAL Soft

- Objectif : plus aucune avance entre le mixeur et OpenAL Soft (P2 à P6).
- Fichiers : `CasaEngine/Framework/Audio/Output/OpenAl/OpenAlNative.cs`, `OpenAlAudioOutput.cs`, une nouvelle classe testable pour le rappel et la vidange (nom au choix, dans le même dossier), `CasaEngine/Framework/Audio/Output/IAudioOutput.cs` (doc de `BufferCount` et `UnderrunCount`), `CasaEngine/Framework/Audio/Backends/SoftwareAudioBackend.cs` si `LeadMilliseconds` doit changer, tests dans `CasaEngine.Tests/Audio/Output/` et `CasaEngine.Tests/Audio/`. `CasaEngine.csproj` ne change pas.
- Étapes :
  1. Ajouter à `OpenAlNative`, sans `unsafe` (P2) : le nom de l'extension `AL_SOFT_callback_buffer`, `AL_BUFFER`, `ALC_CONNECTED` (`alext.h:158-160`), `alGetProcAddress`, le type de délégué de `alBufferCallbackSOFT` et celui du rappel (`alext.h:583-591`), avec les valeurs d'`alext.h` et d'`al.h` au tag `1.24.3`.
  2. Écrire la classe du rappel et de la vidange, sans code natif : le rappel reçoit un pointeur, un nombre d'octets et une horloge ; il calcule les trames (8 octets par trame stéréo float), rend par blocs dans le tableau alloué à l'ouverture et copie chaque bloc par `Marshal.Copy`, mesure la fenêtre et le seuil (P4), compte les appels en retard, applique P3 et P9. La vidange (P8) rend, d'après le temps écoulé, les trames dues dans un tampon jeté. Aucune allocation.
  3. Dans `OpenAlAudioOutput`, après l'ouverture : si l'extension est là, créer un tampon, lui poser le rappel, l'attacher à la source par `AL_BUFFER`, jouer la source ; sinon, la file actuelle, avec un avertissement. En mode rappel, le thread « CasaEngine Audio » garde l'ouverture et la fermeture et, à chaque passage, lit la latence pendant la fenêtre (P4) et `ALC_CONNECTED` (P8) ; après une perte du périphérique, il vidange.
  4. Arrêt dans l'ordre de P6.
  5. `LeadMilliseconds` vaut 0 en mode rappel (P5) ; `UnderrunCount` lit le compte de P4.
  6. Tests de la classe : trames calculées depuis les octets ; rendu copié dans la mémoire donnée, y compris une demande plus longue que le tableau ; exception qui donne du silence et rend la sortie indisponible ; seuil tiré de la fenêtre, y compris une première lecture à 0 ou basse, et sans `ALC_SOFT_device_clock` ; rien compté pendant la fenêtre ; appel en retard compté au-delà du seuil et pas en deçà ; rappel qui trouve le rendu pris rend du silence ; aucune allocation sur 1000 appels (`GC.GetAllocatedBytesForCurrentThread`).
  7. Test de la perte du périphérique, sur `SoftwareAudioBackend` avec une sortie factice qui joue le mode rappel puis la vidange : après la coupure simulée, plus de commandes du jeu que l'anneau n'en tient (4096), et chaque appel rend la main en moins de 5 ms ; `IsAvailable` reste vrai.
- Validation : builds et tests. `git diff` de la tâche sans `unsafe` ni `AllowUnsafeBlocks`. Harnais de stress, 60 s : ligne de P5 « rappel », avance 0 ms, seuil écrit, `underruns=0`. Le son du harnais s'entend sans coupure (l'auteur, s'il est là). Essai manuel avec l'auteur : couper ou désactiver le périphérique de sortie pendant le harnais ; le jeu continue et le journal a une ligne sur la perte. Sans l'auteur, la tâche reste 🧪 pour cet essai.
- Stop : des appels en retard sous le harnais → ⚠️ Blocked, O1. Un appel du jeu qui dépasse 5 ms au test de coupure → ⚠️ Blocked. Extension absente sur le PC de l'auteur (repli en file) → ⚠️ Blocked, question à l'auteur.
- Commit : `feat(audio): let OpenAL Soft pull the mix through a callback buffer`
- Note de validation (2026-11-17) : exécuté par un sous-agent `executor`, diff relu en session principale. `OpenAlCallbackFeeder` (sans code natif, horloge injectée) porte le rappel, le seuil et la vidange ; `OpenAlAudioOutput` le relie à OpenAL Soft par des délégués gardés en champ, sans `unsafe`. Builds : `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln`, 0 erreur. Tests : 5674 sur 5674 (18 nouveaux, dont la perte du périphérique : 3 × 4096 commandes après la coupure, chacune sous 5 ms ; sans la vidange, le test reste bloqué). Harnais de stress, 60 s, PC de l'auteur, ligne `OpenAL audio output: mode=callback, sample rate=48000 Hz, device latency=22.0 ms, lead=0.0 ms, underrun threshold=22.0 ms.` : passage 1 `underruns=1 gc=119`, la coupure estimée tombant dans la première seconde (`elapsed=1 underruns=1`, puis plus rien) ; passage 2 `underruns=0 gc=119`. Budget des passages épuisé pour ce build. **⚠️ par la condition d'arrêt : O1 posé à l'auteur.** Réponse de l'auteur le 2026-11-17 : « Garder 22 ms ». La tâche passe en 🧪 : seul manque l'essai manuel de la perte du périphérique. Restent aussi l'essai manuel de la perte du périphérique avec l'auteur, et une limite à écrire en T1.3 : après une perte du périphérique, la vidange passe au rythme du thread audio (environ 15,6 ms, minuterie Windows), donc un appel du jeu qui trouverait l'anneau plein attendrait jusqu'à ce délai.

### ✅ T1.3 — Documentation

- Objectif : la doc dit la nouvelle latence et ses limites.
- Fichiers : `docs/engine/audio-system.md` (section « Latence du backend logiciel », `:704-709`), et la doc de `IAudioOutput`.
- Étapes : remplacer les 40 ms et le réveil de 15,6 ms par le mode rappel, le tampon accordé par Windows, le fichier `alsoft.ini` (comment le changer : `ALSOFT_CONF`), l'estimation des sous-alimentations (P4) et la mesure du harnais.
- Validation : relecture ; D2.
- Commit : `docs(audio): describe the low-latency OpenAL output`
- Note de validation (2026-11-17) : `docs/engine/audio-system.md` §1 bis (texte, schéma, sortie) et limites (latence, sous-alimentations estimées, perte du périphérique, attente possible jusqu'au réveil du thread après une perte). La doc de `IAudioOutput` a été mise à jour en T1.2. D2 : aucune occurrence d'un nom de jeu (`rg`).

### ✅ T1.4 — Vérification

- Objectif : relecture neuve de tout le chantier.
- Étapes : un vérificateur neuf reçoit le plan, l'ADR-0094 et le diff de la branche depuis `00b24bca`. Il relance builds, tests et harnais (si l'auteur l'accepte), et cherche : rappel qui alloue ou appelle AL, exception qui sort, rappel après l'arrêt, délégué libéré trop tôt, deux rendus à la fois, appel du jeu qui attend après une perte du périphérique, seuil lu avant la fin de la fenêtre, repli sur la file cassé, `alsoft.ini` absent d'une sortie, `unsafe` dans le diff.
- Validation : CONFIRMED, ou écarts traités selon leur gravité.
- Commit : `docs(ai-agent): record the verification of the low-latency audio output`
- Note de validation (2026-11-17) : un vérificateur neuf rend **CONFIRMED** sur `00b24bca..d056718d`, sans écart P0 à P2. Il a relancé les builds (0 erreur sur les deux solutions) et les tests (5674 sur 5674), vérifié `alsoft.ini` dans les quatre sorties (`GetCopyToOutputDirectoryItems`, copies identiques à la source), relu le rappel, le seuil, la vidange, l'ordre d'arrêt et les constantes contre `alext.h`. Il n'a rien joué : les passages du harnais et l'essai de perte du périphérique viennent du relevé. Constats reportés, aucun bloquant :
  - **P3.** Le test de perte du périphérique passe par une copie de la boucle (`CallbackModeAudioOutput`), qui ne lit pas `ALC_CONNECTED` et vidange par `Thread.Yield` : la branche réelle de `RunCallbackLoop` n'est vérifiée qu'à la lecture, et l'attente réelle peut aller jusqu'au réveil du thread (environ 15,6 ms, écrit dans la doc). L'essai manuel de T1.2 la couvre.
  - **P4.** La plus grande demande retenue pour le seuil peut venir des quelques ms qui suivent la fin de la fenêtre. Effet négligeable.
  - **P4.** `ALC_CONNECTED` est lu sans tester `ALC_EXT_disconnect` : une bibliothèque qui n'écrirait pas la valeur ferait croire à une perte. OpenAL Soft 1.24.3 l'écrit toujours.
  - **P4.** Hors du code du rappel : le premier appel inverse attache le thread natif au runtime (une allocation), et un rappel qui tombe pendant une collection l'attend. L'ADR-0094 le dit déjà.
  - **P4.** La ligne d'index de `ai-agent/README.md` était restée à T0.1 : mise à jour avec ce commit.

---

## Phase 2 — Recette

### ✅ T2.1 — Recette de l'auteur

- Objectif : l'auteur entend le son partir avec le geste.
- Étapes : l'auteur lance `Projects/PixelUiSample` au Launcher de la branche, déplace le focus au clavier et écoute le son du curseur ; puis la démo audio (clic, musique).
- Validation : accord de l'auteur, écrit ici avec ses mots.
- Commit : `docs(ai-agent): record the acceptance of the low-latency audio output`
- Note de validation (2026-11-17) : l'auteur a écouté au Launcher de la branche (`c125c959`, ligne `OpenAL audio output: mode=callback, sample rate=48000 Hz, device latency=22.0 ms, lead=0.0 ms, underrun threshold=40.0 ms.`, aucun avertissement sur 9 min) un projet de l'auteur dont l'écran de menu joue un son à chaque déplacement du focus, celui où l'écart avait été entendu. Ses mots : « ok », puis « Oui, recette OK » à la question « Votre « ok » vaut-il recette acceptée du son du curseur […] désormais sans latence ? ». `PixelUiSample` et la démo audio n'ont pas été écoutés. L'essai de perte du périphérique de T1.2 est reporté par l'auteur (« Plus tard ») : T1.2 reste 🧪. La fusion dans `main` reste sa décision.

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Si le harnais compte des appels en retard en mode rappel : agrandir le tampon de Windows (`period_size` plus grand), ou revenir à une file courte relue par une minuterie de 1 ms ? Choix de l'auteur, sur mesures. **Posé le 2026-11-17** : une seule coupure estimée sur deux passages de 60 s (238 collections forcées), dans la première seconde du premier passage ; aucune en régime établi. **Répondu le 2026-11-17 : « Garder 22 ms »** : le tampon reste à `period_size = 480`, `periods = 2`. | T1.2 |
| O2 | La latence de Windows et du matériel après le tampon d'OpenAL Soft n'est pas mesurable ici. Elle reste écrite comme inconnue. | T1.3 |

## Hors périmètre

- Une sortie WASAPI propre au moteur (`IAudioClient3`, périodes partagées courtes) : OpenAL Soft 1.24.3 ne les demande pas (`wasapi.cpp:1017-1025`). À décider à part, sur mesure, si le plancher de 22 ms ne suffit pas.
- Le mode exclusif de WASAPI (`[wasapi] exclusive-mode`) : il prend le périphérique aux autres applications.
- Un réglage de projet pour la latence : l'avance disparaît, et le tampon se règle par `alsoft.ini`.
- Le délai propre aux fichiers son (silence en tête) : c'est du contenu.
- Le backend MonoGame, hors de l'effet de P1.
