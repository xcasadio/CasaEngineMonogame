# Plan agent IA — Audio moderne : mixeur logiciel du moteur et module PSX

Programme demandé par l'auteur le 2026-10-05 : donner au moteur une partie audio « au minimum
moderne », avec une partie spéciale pour les jeux PSX (bruitages et musiques). Ce fichier porte
l'**enveloppe du programme** (toutes les tranches) et le **détail de la première tranche, S1 (le
socle)**. Chaque tranche suivante aura son propre détail, relu et approuvé avant d'être exécutée.
Les décisions D1 → D4 ci-dessous ont été arbitrées avec l'auteur le 2026-10-05 : **ce plan les
applique, il ne les rediscute pas**. Les points P1 → P8 sont des arbitrages proposés par l'agent :
l'approbation du plan les valide.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Statut** : **approuvé par l'auteur le 2026-10-05** (« 1 OK 2 worktree 3 AUTO » : plan et
> P1 à P8 validés, travail dans un worktree séparé, mode AUTO jusqu'à T1.8). Branche
> `chantier/audio-modern` créée depuis `main` (`3e0770d6`) dans le worktree
> `.claude/worktrees/audio-modern` ; le checkout de l'auteur n'a pas bougé.
>
> Historique de relecture : proposé le 2026-10-05. Révision 2 : relecture de
> l'enveloppe (REVISE : numéro d'ADR déjà pris, périmètre contradictoire), corrigée, puis relecture
> fraîche de l'enveloppe : **READY**. Révision 3 : relecture de la tranche S1 (REVISE : choix du
> backend dans l'éditeur, harnais de stress non automatisable), corrigée par la variable
> `CASAENGINE_AUDIO_BACKEND` (P3, T1.5) et un mode stress sans clavier (T1.6). Révision 4 :
> seconde relecture de S1 (REVISE, deux points, tous deux tranchés FIX) : réglage `AudioBackend`
> « non renseigné » quand il est absent, avec un défaut unique dans `AudioBackendSelection`
> (T1.5, T1.9), et budget chiffré de la tranche (section « Budget de la tranche S1 »), puis
> relecture fraîche de clôture de S1 : **READY**. Fichier non
> commité : le checkout est sur une branche `e19` non mergée (`chantier/e19s2-ui-clip-view-space`
> au moment de la révision 2) avec une modification en cours de l'auteur
> (`CasaEngine.Launcher/Program.cs`), donc aucune branche n'a été créée pour ne pas déplacer son
> checkout. Le plan sera commité en T0.1, sur la branche du chantier.

## Objectif

**Programme.** Le moteur mixe lui-même tout son audio, en C#, sur un thread audio dédié qui calcule
quelques dizaines de millisecondes d'avance. Une sortie native minimale par plateforme envoie ce son
à la carte : OpenAL Soft (déjà livré par MonoGame) d'abord, XAudio2 ou FAudio plus tard. Sur ce
mixeur viennent, tranche par tranche : le temps réel et le streaming hors du thread de jeu, les
formats (Ogg, ADPCM), de vrais bus avec effets, la couche jeu (priorités, conteneurs, 3D), les outils
de l'éditeur, puis le module PSX (SPU exact, séquenceur SEQ/VAB pour les musiques). C'est
l'architecture des moteurs de référence : l'Audio Mixer d'Unreal est « a multi-platform audio
renderer that lives in its own module »
(https://dev.epicgames.com/documentation/en-us/unreal-engine/audio-mixer-overview-in-unreal-engine),
Godot mixe ses bus et leurs effets dans son propre moteur audio
(https://docs.godotengine.org/en/stable/tutorials/audio/audio_buses.html).

**Tranche S1, détaillée ici.** Un backend logiciel (`SoftwareAudioBackend`) prend place derrière le
contrat existant `IAudioBackend`, sans le changer : mixeur C# pur et déterministe, sortie OpenAL Soft
sur un thread audio, clip PCM neutre et nouveau chargeur WAV. Le backend MonoGame reste disponible
en repli ; le choix se fait par une variable d'environnement valable pour tous les hôtes, sinon par
un réglage de projet (P3). Avec le backend logiciel, tout le contenu existant
(démos, éditeur, Alundra) joue sans initialiser l'audio de MonoGame, avec la même API publique.
La tranche se termine par la recette d'écoute de l'auteur puis la bascule du défaut.

Ce qui n'est pas livré par S1 est dans l'enveloppe du programme et dans « Hors périmètre ».

## État vérifié du dépôt (2026-10-05)

Faits relevés sur `main` (la branche du chantier en partira), sauf mention contraire.

- **Contrat de backend.** `IAudioBackend` (`CasaEngine/Framework/Audio/IAudioBackend.cs`) : 2D
  seulement (volume, pan), `IsAvailable`, `VoiceCapacity`, `Play`, `SetParameters`, `SetVolume`,
  `GetState`, `Pause`, `Resume`, `Stop`, `Release`, `StopAll`, et le streaming poussé
  (`CreateStreamingVoice`, `SubmitBuffer` en PCM 16 bits copié, `GetPendingBufferCount`, `Start`).
  Sur `main`, un refus de débit de streaming « is a normal outcome and must not throw » (commit
  `6921e8d0`, qui ajoute aussi `CasaEngine.Tests/Audio/MonoGameAudioBackendStreamingTests.cs`).
- **Le contrat est implémenté aussi hors du moteur** : `Alundra.Tests/FakeAudioBackend.cs` (dépôt
  parent). Toute modification de `IAudioBackend` oblige à modifier le dépôt parent ; S1 ne le
  modifie pas.
- **Point d'injection.** `AudioSystemComponent(Game game, IAudioBackend backend = null)`
  (`CasaEngine/Framework/Application/Components/AudioSystemComponent.cs:22-25`) ; le défaut est
  codé en dur : `new MonoGameAudioBackend()`, repli `NullAudioBackend` (`:108-120`). Les réglages
  de projet sont déjà chargés à la construction du composant (`:31-34`, ADR-0040).
- **Le clip est lié au backend.** `AssetLoaderRegistry.cs:31` enregistre `SoundEffectLoader` pour
  `IAudioClip` ; ce chargeur crée toujours un `MonoGameAudioClip` par `SoundEffect.FromStream`
  (`CasaEngine/Framework/Assets/Loaders/SoundEffectLoader.cs:31-40`) ; `MonoGameAudioBackend.Play`
  refuse tout autre type de clip (`MonoGameAudioBackend.cs:65-70`). `MonoGameAudioClip` rapporte
  `SampleRate` et `ChannelCount` à 0 sans échantillons décodés (`MonoGameAudioClip.cs:52-57`).
- **Usages directs de l'audio MonoGame** (rg sur les deux dépôts, `alundra-datas-analyser`
  exclu) : `Framework/Audio/Backends/`, `SoundEffectLoader.cs`, et `CasaEngine.Demos/Demos/AudioDemo.cs:115`
  (`new SoundEffect(...)`). MGUI, l'éditeur et les scripts Alundra n'en ont aucun.
- **MonoGame ouvre OpenAL à la demande** : au premier `SoundEffect` ou
  `DynamicSoundEffectInstance` (MonoGame 3.8.5.1 décompilé avec `ilspycmd` :
  `OpenALSoundController.cs:107-120`, `SoundEffect.cs:160-162`, `DynamicSoundEffectInstance.cs:80`).
  Son contrôleur fixe le contexte courant global (`alcMakeContextCurrent`, `:120` et `:168`) et
  réserve 256 sources (`:22`, `:80-81`). Si le moteur ne touche aucun type audio de MonoGame,
  MonoGame n'ouvre jamais OpenAL.
- **OpenAL Soft déjà livré.** `MonoGame.Framework.DesktopGL` 3.8.5.1 (`Directory.Packages.props:18`)
  dépend de `MonoGame.Library.OpenAL` 1.24.3.4 : douze binaires (win-x64, win-arm64, linux-x64,
  linux-arm64, osx, quatre Android, trois iOS en `.a` statique), tous construits depuis OpenAL Soft
  1.24.3. Les chaînes `ALC_EXT_thread_local_context`, `AL_EXT_FLOAT32`, `AL_SOFT_direct_channels`,
  `ALC_SOFT_loopback`, `ALC_SOFT_system_events`, `ALC_SOFT_reopen_device` et `ALC_EXT_EFX` sont
  présentes dans les douze (recherche binaire, vérifiée par deux relectures indépendantes).
  Présence statique seulement : rien n'a été exécuté. Licence d'OpenAL Soft : LGPL 2 ou ultérieure
  (https://raw.githubusercontent.com/kcat/openal-soft/1.24.3/COPYING) ; `Licences/` ne contient pas
  sa notice (`ls Licences`).
- **Aucun binding OpenAL tiers ne convient tel quel** : Silk.NET.OpenAL 2.23.0 n'expose ni
  `ALC_SOFT_loopback` ni `ALC_SOFT_system_events` et cherche `soft_oal.dll`/`openal32.dll` ;
  OpenTK.Audio.OpenAL 4.9.4 n'expose ni `AL_SOFT_callback_buffer` ni `ALC_SOFT_reopen_device` ;
  aucun des deux ne charge le `openal.dll` de MonoGame sans surcharge (paquets décompilés,
  relecture indépendante). MonoGame lui-même lie `openal` par `DllImport` (`__Internal` sur iOS).
- **Limites du chemin actuel** (relevées dans le code) : voix stéréo logicielles poussées depuis le
  thread de jeu, file d'environ 60 ms, au plus 3 buffers par `Update`
  (`Streaming/StereoVoiceMixer.cs:32-42`) ; une allocation `OALSoundBuffer` par `SubmitBuffer`
  côté MonoGame (décompilé, `DynamicSoundEffectInstance.cs:350-363`) ; pitch borné à ±1 octave
  (`AudioVoiceParameters.cs:18-19`) ; 64 voix (`MonoGameAudioBackend.cs:24` sur `main`, `:21` sur
  les branches `e19`).
- **Sémantique actuelle à reproduire ou à changer explicitement** : pitch appliqué comme 2^pitch,
  pan d'une source mono par rotation de sa position OpenAL de `pan × π/3`, canaux d'une source
  stéréo rendus à ±30° (décompilé `SoundEffectInstance.cs:297-300`, `:424-447` ; rappelé par
  ADR-0039).
- **Formats WAV présents** (script de scan des en-têtes `fmt ` sur le dépôt et `../alundra-project`) :
  1 044 fichiers, tous PCM entier 16 bits (996 mono et 46 stéréo pour Alundra, 2 stéréo pour le
  moteur). Aucun ADPCM, aucun flottant.
- **Hôtes et réglages au démarrage** : `CasaEngineGame` charge les réglages du projet seulement
  si un fichier de projet est donné (`CasaEngineGame.cs:342-344`), puis crée
  `AudioSystemComponent` (`:366`). Le runtime hébergé par l'éditeur est créé sans projet hors
  automatisation (`CasaEngine.Editor/GameEditor.cs`, `new HostedEditorGameAdapter(_automationOptions.HasAutomation ? _automationOptions.ProjectPath : null, …)`,
  commentaire « the editor runtime starts without a project ») : il voit toujours les réglages par
  défaut. Le runtime `CasaEngine` ne lit aujourd'hui aucune variable d'environnement
  (`git grep GetEnvironmentVariable main -- CasaEngine/` : rien).
- **Démos** : projet `Content/DemosGame.json` (`CasaEngine.Demos/DemosGame.cs:40`), journal
  `log.txt` dans le dossier courant (`:47`, `FileLogger`), démo de départ choisie par
  `CASAENGINE_START_DEMO`, comparée au titre (`:107-121`) ; la démo audio s'appelle
  « Audio demo » (`Demos/AudioDemo.cs:73`) et ne lit le clavier que fenêtre active (`:150`).
  Exécutable : `CasaEngine.Demos/bin/Debug/net9.0-windows/CasaEngine.Demos.exe`.
- **Réglages de projet** : propriété `IsAudioMuted` avec `[Category("Audio")]`
  (`CasaEngine/Framework/Configuration/Project/ProjectSettings.cs:52-53`), lue avec « absent =
  false » (`ProjectSettingsHelper.cs:32-34`) et écrite seulement si vraie (`:129-132`) : modèle
  pour un réglage additif.
- **Tests** : `CasaEngine.Tests/Audio` contient 25 fichiers sur `main` (`git ls-tree`), dont un
  faux backend (`FakeAudioBackend.cs`) ; tous les tests de contrat tournent contre le faux.
- **Doc** : `docs/engine/audio-system.md` (en français, sections 1 à 11, dont « Limites connues »).
- **ADR** : `main` s'arrête à ADR-0047 ; les ADR-0048 à 0054 sont sur les branches `e19` non
  mergées (relevé de `docs/decisions/` sur toutes les branches locales, `git ls-tree`), la plus
  haute étant `0054-mgui-clip-rectangles-are-local-to-the-view.md` sur
  `chantier/e19s2-ui-clip-view-space`. Ce chantier prend **ADR-0055**, avec la règle de T0.1 si un
  autre chantier le prend d'ici là.
- **Modification préexistante de l'auteur** : `CasaEngine.Launcher/Program.cs` (modifié, non
  indexé). Ne jamais l'indexer.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Objectif : une partie audio « au minimum moderne », avec une partie spéciale pour les jeux PSX. |
| D2 | Plateformes : les mêmes que MonoGame. Le reciblage multiplateforme du moteur (aujourd'hui `net9.0-windows`, x64) est **hors chantier** : le code audio reste neutre, mais il n'est validé que sur Windows x64. |
| D3 | Côté PSX, les bruitages (SFX) et les musiques (BGM) sont dans le périmètre. |
| D4 | On commence par le socle (tranche S1). |

## Points à valider par l'auteur (arbitrages proposés)

L'approbation du plan les valide ; un refus renvoie à la question avant toute exécution.

| Réf | Arbitrage proposé |
|---|---|
| P1 | **Architecture** : le moteur mixe tout en C# (float 32 bits) sur un thread audio dédié, avec de l'avance ; une sortie native minimale par plateforme. Réponse à « c'est quoi le mieux pour un moteur moderne ? » : c'est le modèle d'Unreal et de Godot (sources dans l'Objectif). Ni SoundFlow ni middleware propriétaire. |
| P2 | **Première sortie** : OpenAL Soft livré par MonoGame, par un binding écrit à la main (sous-ensemble minimal, bibliothèque `openal`). Le moteur crée **son propre** périphérique et contexte, rendu courant **seulement sur son thread audio** (`ALC_EXT_thread_local_context`), sans jamais toucher le contexte global de MonoGame. Sortie stéréo float 32 bits (`AL_EXT_FLOAT32`) jouée sans spatialisation (`AL_SOFT_direct_channels`), au débit natif du périphérique. Extension manquante à l'exécution : sortie indisponible, consignée une fois, jeu sans son. |
| P3 | **Transition** : `IAudioBackend` et l'API publique d'`AudioService` ne changent pas en S1. Le backend est choisi au démarrage, dans cet ordre : (1) la variable d'environnement `CASAENGINE_AUDIO_BACKEND` (`Software` ou `MonoGame`), qui vaut pour tous les hôtes, éditeur compris, et sert à la recette et au retour arrière ; (2) sinon le réglage de projet additif `AudioBackend` quand un projet est chargé ; (3) sinon le défaut, `MonoGame` pendant S1, `Software` après T1.9. Le choix et sa source sont consignés une fois. L'éditeur, dont le runtime démarre sans projet, suit (1) ou (3), jamais le projet ouvert ensuite (O5). Le retrait du backend MonoGame (rupture d'API publique) n'est pas dans S1 : décision ultérieure de l'auteur. |
| P4 | **Clip neutre** : nouveau `PcmAudioClip` (PCM 16 bits entrelacé, mono ou stéréo, débit et durée réels, échantillons mono exposés par `IAudioClipSamples`) produit par un nouveau chargeur WAV, enregistré à la place de `SoundEffectLoader`. Le décodeur lit le PCM entier 8/16/24/32 bits et le flottant 32 bits, `WAVE_FORMAT_EXTENSIBLE` compris. L'ADPCM (MS, IMA) passe en S3 : aucun fichier du dépôt n'en utilise. `SoundEffectLoader` reste, marqué `[Obsolete]`, non enregistré. Le backend MonoGame accepte `PcmAudioClip` en créant une fois son `SoundEffect` par `FromStream` sur un WAV 16 bits reconstruit en mémoire : comportement inchangé, aucun débit refusé. |
| P5 | **Sémantique à parité** : volume linéaire borné à [0, 1], pitch en octaves borné à ±1 et appliqué comme 2^pitch, boucle sur le clip entier. **Changement audible assumé** : pan d'un clip mono à puissance constante (−3 dB au centre) au lieu de la rotation OpenAL ; balance pour un clip stéréo ; voix stéréo (musique, `PlayClipStereo`) restituées exactement gauche → gauche, droite → droite. |
| P6 | **Mixage** : rééchantillonnage cubique (Hermite à 4 points), avance par défaut de 4 buffers de 10 ms, configurable, écrêtage dur à la sortie. Pas de limiteur en S1 (il vient en S4). Aucune allocation, aucun verrou bloquant, aucun LINQ ni closure dans le rendu ; commandes du thread de jeu vers le thread audio par file sans verrou ; fins de voix renvoyées au thread de jeu par une seconde file. |
| P7 | **Exécution** : chaque tâche de code est confiée à un sous-agent `executor` (sonnet) avec un brief complet ; diagnostic, intégration et décisions restent en session principale. Un vérificateur frais (`verifier`, opus) contrôle la tranche S1 avant T1.9. |
| P8 | **Licence** : la notice LGPL d'OpenAL Soft (avec le lien vers ses sources) entre dans `Licences/` en T1.3, puisque le moteur lie désormais OpenAL directement. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/audio-modern`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché (`dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln`) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` dès qu'une tâche touche du code testé. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit et ADR en anglais ; `docs/engine/audio-system.md` reste en français, comme aujourd'hui.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds (ici : le rendu du mixeur et la boucle du thread audio) ; le runtime ne dépend pas de l'éditeur ; sérialisation additive ; jamais d'exception avalée en silence, jamais de log par frame.
- **Arrêt du programme** : si le harnais de stress de T1.6 montre des coupures à l'avance par défaut, la tâche passe en ⚠️ Blocked (O1) et l'exécution s'arrête : c'est l'hypothèse centrale de P1.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Enveloppe du programme

Seule S1 est détaillée et exécutable par ce plan. Chaque autre tranche sera détaillée, relue et
approuvée à son tour ; l'ordre suit les prérequis.

Périmètre des tranches suivantes : elles peuvent faire des changements **additifs** de l'API
publique (`AudioService`, `AudioVoiceParameters`, `IAudioBackend`) et des changements dans le dépôt
parent, chacun sous l'approbation de sa tranche ; une rupture d'API reste une décision de l'auteur
(AGENTS.md §9.8). Une tranche qui étend `IAudioBackend` (S2, S4, S5 ou X1 le peuvent) met à jour
dans la même tranche `Alundra.Tests/FakeAudioBackend.cs` du dépôt parent. Les tranches X2, X4 et X5
travaillent dans le dépôt parent selon ses propres règles et plans (`docs/plan-*.md`).

| Tranche | Résultat attendu | Prérequis | Dépôt |
|---|---|---|---|
| **S1 — Socle** | Backend logiciel derrière `IAudioBackend`, sortie OpenAL Soft sur thread audio, clip PCM neutre, choix par réglage de projet, recette puis bascule du défaut. | — | moteur |
| S2 — Temps réel et streaming | Lecture disque et décodage hors du thread de jeu ; voix stéréo logicielles mixées au thread audio (fin des ~60 ms de latence des gains) ; rampes de volume à l'échantillon ; points de boucle et plage de pitch élargie (API additive) ; changement de périphérique à chaud (`ALC_SOFT_system_events`, `alcReopenDeviceSOFT`). | S1 | moteur |
| S3 — Formats | Ogg Vorbis (NVorbis, déjà dépendance transitive de MonoGame), ADPCM WAV ; MP3 et FLAC à arbitrer (nouvelle dépendance). | S1 | moteur |
| S4 — Bus et effets | Vrais bus de submix avec effets insérés (EQ et filtres biquad, compresseur, limiteur du master), départs auxiliaires (reverb), ducking, snapshots ; configuration sérialisée (ADR). | S2 | moteur |
| S5 — Couche jeu | Priorités et vol de voix, voix virtuelles, conteneurs (aléatoire, séquence, pitch, volume et délai aléatoires), atténuation 2D/3D, écouteur, Doppler, paramètres de jeu. | S4 | moteur |
| S6 — Éditeur et outils | Panneau de mixage, vu-mètres, formes d'onde, profileur audio. | S4 | moteur |
| X1 — Module SPU PSX | ADPCM VAG, 24 voix, pas SPU, interpolation gaussienne, ADSR (d'après psx-spx), boucles, gains gauche/droite exacts, reverb SPU, comme groupe de voix du mixeur. Base : le mixeur SPU de l'analyseur (MIT, même auteur), à nettoyer. | S1 (S2 conseillé) | moteur |
| X2 — Données PSX | Export VAB/VAG/SEQ par le convertisseur et types d'assets moteur correspondants (ADR, sérialisation). | X1 | parent + moteur |
| X3 — Séquenceur SEQ/SEP | Musiques en temps réel et bruitages déclenchés par séquence. | X1, X2 | moteur |
| X4 — Bascule d'Alundra | Bruitages et musiques d'Alundra joués par le module PSX. | X3 | parent |
| X5 — XA (optionnel) | Pistes XA et audio des vidéos. | X1 | moteur + parent |

## Validation globale (tranche S1)

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout vert, dont la suite de conformité
  (faux backend et backend logiciel), les tests de rendu déterministe et le test d'absence
  d'allocation du mixeur.
- `IAudioBackend`, `AudioService` et `IAudioClip` sans changement de signature (`git diff main -- <fichiers>`).
- Harnais de stress de la démo audio (T1.6), lancé sans clavier par variables d'environnement,
  backend `Software` : **0 sous-alimentation sur 60 s** avec un ramasse-miettes forcé toutes les
  500 ms, à l'avance par défaut (`log.txt` de la démo).
- Avec le backend logiciel, aucun type audio de MonoGame n'est atteint : rg sur le chemin logiciel,
  et `monogame-openal-initialized=false` au journal de T1.6.
- Choix du backend prouvé pour chaque hôte : tests de `AudioBackendSelection` (dont le cas sans
  projet du runtime de l'éditeur) et ligne `Audio backend: …` au journal de l'éditeur, de la démo
  et du Launcher ; retour arrière vers MonoGame par `CASAENGINE_AUDIO_BACKEND=MonoGame` partout.
- Recette d'écoute de l'auteur (T1.8) : démo audio et parcours Alundra, comparés au backend MonoGame.
- Vérificateur frais sur la tranche avant T1.9 : **CONFIRMED**.

## Budget de la tranche S1

- **Sous-agents** : un `executor` par tâche de code (T1.1 à T1.6, T1.9), soit 7 au plus, plus un
  vérificateur frais en T1.8. Après **deux échecs** d'un `executor` sur la même tâche (build ou
  validation non tenus), la session principale reprend la tâche elle-même ; pas de troisième essai
  au même niveau.
- **Corrections et revérifications** : après la correction d'un défaut reproduit, **une** seule
  revérification ciblée (reproduction d'origine et tests de base). Pour un constat P1 ou P2 du
  vérificateur de T1.8, au plus **cinq** passes de correction et de revérification au total sur la
  tranche, chacune avec un changement réel du code ou de la preuve ; jamais deux fois sur un état
  identique.
- **Harnais de stress (T1.6)** : au plus **deux** passages sur le même build (le second écarte un
  incident isolé). Deux passages avec des coupures → ⚠️ Blocked (O1) et arrêt du programme ; les
  réglages de P6 ne sont pas modifiés pour faire passer le test.
- **Budget épuisé** (quelle que soit la limite atteinte) : la tâche passe en ⚠️ Blocked, la
  question est écrite dans « Points ouverts » avec l'état et les preuves, et l'exécution s'arrête.

---

## Phase 0 — Mise en place

### ✅ T0.1 — Branche, plan, index et ADR-0055

- Objectif : ouvrir le chantier et consigner la décision d'architecture.
- Fichiers : `ai-agent/tasks/audio-modern-tasks.md` (ce plan), `ai-agent/README.md` (ligne du
  tableau), `docs/decisions/0055-engine-owned-software-audio-mixer-with-thin-native-outputs.md`,
  `docs/decisions/README.md`, `docs/decisions/0001-audio-runtime-architecture-v1.md` (ligne de statut).
- Étapes :
  1. `git switch -c chantier/audio-modern main`. La modification non indexée de l'auteur
     (`CasaEngine.Launcher/Program.cs`) doit suivre sans conflit ; sinon ⚠️ Blocked et question.
     Revérifier le numéro d'ADR : relever `docs/decisions/` sur toutes les branches locales
     (`git ls-tree --name-only <branche> docs/decisions/`). Si 0055 est pris, prendre le premier
     numéro libre sur toutes les branches et remplacer chaque mention d'ADR-0055 dans ce plan avant
     le commit.
  2. Committer ce plan et sa ligne dans `ai-agent/README.md`.
  3. Écrire ADR-0055 (modèle `docs/decisions/template.md`) : contexte (État vérifié), décision
     (P1 à P6 et P8 tels qu'approuvés), conséquences (tranches, risques GC et minuterie, repli
     MonoGame, LGPL). Statut d'ADR-0001 : « Accepted (backend decision superseded in part by ADR-0055) ».
- Validation : `git diff --stat` ne montre que des documents ; liens de l'index valides.
- Commit : `docs(adr): ADR-0055 engine-owned software audio mixer with thin native outputs`
- Note de validation (2026-10-05) : à la demande de l'auteur (« worktree »), la branche est créée
  dans un worktree (`git worktree add .claude/worktrees/audio-modern -b chantier/audio-modern main`,
  `main` = `3e0770d6`) au lieu d'un `git switch` ; la modification de l'auteur reste dans son
  checkout, intacte. Le dépôt moteur étant un sous-module du dépôt parent, le worktree a reçu un
  `config.worktree` (`core.worktree` sur son dossier, comme le worktree existant
  `blissful-jepsen-2adec6`) pour que `git submodule update --init` installe MGUI (`d3e0cd12`) et
  NvgSharp (`9c0da031`). Relevé des ADR sur toutes les branches locales : aucun fichier 0055 à
  0059, ADR-0055 libre. Statut d'ADR-0001 écrit « Accepted (MonoGame-only backend superseded in
  part by ADR-0055) », dans l'index aussi. `git diff --stat` : documents seulement.

## Phase 1 — Tranche S1 : le socle

### ✅ T1.1 — Clip PCM neutre et décodeur WAV

- Objectif : un clip sans aucun type MonoGame, lisible par les deux backends (P4).
- Fichiers : `CasaEngine/Framework/Audio/PcmAudioClip.cs`, `CasaEngine/Framework/Audio/Decoding/WavDecoder.cs`,
  `CasaEngine/Framework/Assets/Loaders/WavAudioClipLoader.cs`, `CasaEngine/Framework/Assets/AssetLoaderRegistry.cs`,
  `CasaEngine/Framework/Assets/Loaders/SoundEffectLoader.cs` (`[Obsolete]`),
  `CasaEngine/Framework/Audio/Backends/MonoGameAudioBackend.cs`, `CasaEngine.Demos/Demos/AudioDemo.cs`,
  `CasaEngine.Tests/Audio/WavDecoderTests.cs`, `CasaEngine.Tests/Audio/WavAudioClipLoaderTests.cs`.
- Étapes :
  1. `WavDecoder` : RIFF/WAVE, `fmt ` PCM entier 8/16/24/32 bits, IEEE float 32 bits,
     `WAVE_FORMAT_EXTENSIBLE` (sous-formats PCM et float), 1 ou 2 canaux ; conversion en PCM 16 bits
     entrelacé. Toute autre forme (ADPCM, plus de 2 canaux, en-tête invalide, données tronquées) :
     erreur avec le nom du fichier et la raison.
  2. `PcmAudioClip : IAudioClip, IAudioClipSamples` : débit, canaux et durée réels ;
     `MonoSamples` non vide pour un clip mono seulement (contrat ADR-0039 inchangé).
  3. `WavAudioClipLoader` (extension `.wav`) : erreurs consignées avec contexte, retour `null`
     comme aujourd'hui ; enregistré à la place de `SoundEffectLoader` (`AssetLoaderRegistry.cs:31`).
  4. `MonoGameAudioBackend.Play` accepte `PcmAudioClip` : `SoundEffect` créé une seule fois par
     `SoundEffect.FromStream` sur un WAV 16 bits reconstruit en mémoire, gardé par le clip et libéré
     avec lui. `MonoGameAudioClip` reste accepté.
  5. `AudioDemo` : le bip stéréo devient un `PcmAudioClip` (plus de `new SoundEffect`).
- Validation : build des deux solutions ; tests du décodeur (chaque format, mono et stéréo,
  extensible, en-têtes invalides, fichier tronqué) et du chargeur ; suite complète verte. Écoute
  de la démo et d'Alundra sous le backend MonoGame, inchangée : 🧪 pour l'auteur (avec T1.8).
- Commit : `feat(audio): decode wav files into a backend-neutral PCM clip`
- Note de validation (2026-10-05) : exécuté par un sous-agent `executor`, diff relu en session
  principale. Build des deux solutions : 0 erreur. `CasaEngine.Tests` : 2492/2492 (2445 avant, +47 :
  décodeur, clip, chargeur). Contrôle ponctuel non commité : les 1 044 WAV du dépôt et
  d'`alundra-project` se décodent, débit et canaux conformes à leur en-tête. Choix laissés à
  l'exécuteur : `InvalidDataException` pour un fichier malformé, `NotSupportedException` pour un
  format valide non géré ; NaN flottant → 0 ; un nombre d'échantillons non multiple du nombre de
  canaux est refusé. Changements de comportement assumés (P4) : un WAV ADPCM ou exotique que
  MonoGame chargeait est désormais refusé avec une erreur consignée ; un clip stéréo rapporte son
  vrai débit et ses canaux (0 et 0 avant). Le chemin `PcmAudioClip` du backend MonoGame n'a pas de
  test automatique (il faut un périphérique) : l'écoute sous MonoGame est dans la recette de T1.8.
  Remarque : `CasaEngine.Tests` n'est pas reconstruit par les builds de solution ; toujours lancer
  `dotnet test` sans `--no-build`.

### ✅ T1.2 — Cœur du mixeur logiciel

- Objectif : un mixeur C# pur, sans thread ni périphérique, testable à l'échantillon près (P5, P6).
- Fichiers : `CasaEngine/Framework/Audio/Software/` (mixeur, voix, rééchantillonneur cubique,
  anneau de streaming PCM 16 bits, files de commandes et d'évènements), `CasaEngine.Tests/Audio/Software/`.
- Étapes :
  1. Voix résidentes (`PcmAudioClip`) et voix de streaming (anneau alimenté par copie), en nombre
     fixe préalloué (64 par défaut) ; volume, pan (P5), pitch (2^pitch × débit du clip / débit de
     sortie), boucle du clip entier ; pause, reprise, arrêt.
  2. Rendu `Render(Span<float> stéréoEntrelacé, frames)` déterministe ; écrêtage dur.
  3. File de commandes sans verrou (thread de jeu → rendu) et file d'évènements (fins de voix,
     buffers de streaming consommés) dans l'autre sens.
- Validation : tests de rendu (silence sans voix ; signal constant → gains attendus selon le pan ;
  pitch 2 → durée divisée par deux ; boucle ; débit de clip 22 050 Hz rendu à 48 000 Hz avec la même
  fréquence de sinusoïde, à une tolérance écrite dans le test ; fins de voix ; comptes de buffers
  de streaming) ; test d'absence d'allocation : `GC.GetAllocatedBytesForCurrentThread` inchangé sur
  1 000 rendus avec 64 voix actives (résidentes et en streaming) ; suite complète verte.
- Commit : `feat(audio): add the engine software mixer core`
- Note de validation (2026-10-05) : exécuté par un sous-agent `executor`, code concurrent relu en
  session principale (anneaux SPSC : élément écrit avant publication de l'index, `Volatile`
  lecture/écriture, aucun verrou ; retour des chunks par un anneau de la taille maximale du pool).
  Types `internal` dans `CasaEngine/Framework/Audio/Software/` (`SoftwareMixer`, `MixerVoice`,
  `MixerMessages`, `SpscRingBuffer`, `SampleChunk`, `SampleChunkPool`). Build : 0 erreur, aucun
  avertissement venant de ces fichiers. `CasaEngine.Tests` : 2515/2515 (+23), dont le test sans
  allocation (64 voix, 1 000 rendus de 480 trames, commandes et alimentation du streaming comprises).
  Choix de l'exécuteur : position en `double` ; voisins hors bord bornés au bord (boucle : bouclés) ;
  streaming décalé de 2 trames pour l'interpolation, buffer déclaré consommé quand sa dernière trame
  entre dans la fenêtre ; une fin de voix n'est jamais perdue (réessai si l'anneau d'évènements est
  plein), un « buffer consommé » perdu est compté (`DroppedEventCount`) ; chunks de 4 096
  échantillons, pool initial 256 (2 Mo), maximum 4 096, au plus 128 chunks en file par voix (le
  surplus est compté dans `DroppedChunkCount`). Anneaux testés sur un seul thread (le thread audio
  vient en T1.3) : à surveiller dans le stress de T1.6.

### ✅ T1.3 — Sortie OpenAL Soft sur thread audio

- Objectif : envoyer le rendu du mixeur à la carte son sans code géré bloqué sur le thread
  temps réel d'OpenAL (P2).
- Fichiers : `CasaEngine/Framework/Audio/Output/IAudioOutput.cs`, `Output/NullAudioOutput.cs`,
  `Output/OpenAl/OpenAlNative.cs`, `Output/OpenAl/OpenAlAudioOutput.cs`, `Licences/OpenAL-Soft.txt`,
  tests de la comptabilité des buffers dans `CasaEngine.Tests/Audio/Output/`.
- Étapes :
  1. `OpenAlNative` : `DllImport("openal")`, seulement les fonctions utilisées (périphérique,
     contexte, `alcSetThreadContext`, extensions, sources, buffers, file, état, erreurs).
  2. `OpenAlAudioOutput` : thread dédié nommé, priorité au-dessus de la normale ; ouvre le
     périphérique par défaut et **son propre** contexte, courant sur ce seul thread ; vérifie les
     extensions de P2 ; lit le débit du périphérique (`ALC_FREQUENCY`) ; garde N buffers float
     stéréo en file, les remplit par un rappel de rendu, compte les sous-alimentations (source
     arrêtée faute de buffer) et relance ; arrêt propre (thread joint, objets AL supprimés,
     périphérique fermé). Aucune exception ne sort du thread : consignée une fois, sortie
     indisponible.
  3. `NullAudioOutput` : toujours indisponible, sans thread.
  4. Notice LGPL d'OpenAL Soft dans `Licences/` (P8).
- Validation : build ; tests de la comptabilité (file, sous-alimentation) avec une couche native
  simulée ; essai réel en T1.6.
- Commit : `feat(audio): add an OpenAL Soft output fed from a dedicated audio thread`
- Note de validation (2026-10-05) : exécuté par un sous-agent `executor`, boucle et cycle de vie
  relus en session principale. Fichiers `internal` sous `CasaEngine/Framework/Audio/Output/`
  (`IAudioOutput`, `NullAudioOutput`, `OpenAl/OpenAlNative`, `OpenAlAudioOutput`, `IOpenAlStream`,
  `OpenAlStream`, `OpenAlRefillLoop`) ; constantes reprises des en-têtes d'OpenAL Soft 1.24.3, lignes
  citées dans `OpenAlNative.cs` ; aucun code `unsafe`. `Licences/OpenAL-Soft.txt` : en-tête puis
  `COPYING` 1.24.3 recopié tel quel (P8). Build : 0 erreur, aucun avertissement de ces fichiers.
  `CasaEngine.Tests` : 2523/2523 (+8 : sortie nulle, boucle de remplissage avec une couche OpenAL
  simulée). Essai ponctuel non commité sur le vrai périphérique (sinus 440 Hz, 3 s) : ouvert,
  extensions présentes, 48 000 Hz, 4 × 480 trames (40 ms d'avance), **0 sous-alimentation** ;
  réveils de la boucle min 10,05 / moy 15,55 / max 17,13 ms (granularité de la minuterie Windows,
  `timeBeginPeriod` non appelé) : marge d'environ 2,5 réveils, à confirmer sous stress en T1.6.

### ✅ T1.4 — Backend logiciel et suite de conformité

- Objectif : `SoftwareAudioBackend : IAudioBackend`, avec le même contrat observable que le
  backend MonoGame.
- Fichiers : `CasaEngine/Framework/Audio/Backends/SoftwareAudioBackend.cs`,
  `CasaEngine.Tests/Audio/AudioBackendConformanceTests.cs` (classe abstraite) et ses deux
  dérivées (faux backend ; backend logiciel sur une sortie hors ligne pilotée par le test).
- Étapes :
  1. Table des voix côté thread de jeu (handles à génération, indices denses sous
     `VoiceCapacity`), `GetState` synchrone mis à jour par la file d'évènements, `Stop` et
     `Release` immédiats côté contrat.
  2. Streaming : `SubmitBuffer` copie dans l'anneau de la voix, `GetPendingBufferCount` compte les
     buffers non consommés, tout débit positif accepté (pas de bornes 8–48 kHz).
  3. Sortie indisponible : `IsAvailable` faux, chaque appel reste valide et silencieux.
- Validation : suite de conformité verte pour les deux backends (capacité, refus sans exception,
  handles périmés, états, pause/reprise, streaming et comptes, indisponibilité) ; suite complète verte.
- Commit : `feat(audio): add the software audio backend behind IAudioBackend`
- Note de validation (2026-10-05) : exécuté par un sous-agent `executor`, table des voix,
  vidage des évènements et attente bornée relus en session principale. `SoftwareAudioBackend`
  public (constructeur public sur OpenAL, constructeur interne sur une `IAudioOutput`,
  diagnostics `UnderrunCount`, `OutputSampleRate`, `LeadMilliseconds`, `DroppedChunkCount`,
  `DroppedEventCount`). Suite de conformité abstraite, exécutée sur `FakeAudioBackend` et
  `SoftwareAudioBackend` (sortie hors ligne pilotée par le test). Build : 0 erreur.
  `CasaEngine.Tests` : 2588/2588 (+65), dont un cycle de jeu sans allocation (0 octet) et un essai de
  bout en bout `AudioService` → backend logiciel (`PlayClip`, `PlayClipStereo`, recyclage des voix).
  Écarts tranchés : (1) la suite a révélé trois écarts du faux backend au contrat, corrigés dans
  `FakeAudioBackend.cs` sans changer d'attente existante — nombre de canaux hors 1–2 → exception,
  débit ≤ 0 → `None`, buffer nul → `ArgumentNullException` (comme le backend MonoGame ; le faux
  d'`Alundra.Tests`, dépôt parent, n'est pas touché) ; (2) écarts voulus avec MonoGame : tout débit
  positif accepté en streaming, seul `PcmAudioClip` est accepté par `Play` ; (3) une voix de
  streaming arrêtée ne peut pas être relancée par `Start` (le mixeur la libère) ; aucun appelant du
  moteur ne le fait (`MusicPlayer` et `StereoVoiceMixer` arrêtent puis libèrent). File de commandes
  pleine : attente bornée à 100 ms puis abandon consigné, sauf `SetVolume`, renvoyé à chaque frame
  par les fondus, abandonné sans attente avec un journal limité.

### ✅ T1.5 — Choix du backend par réglage de projet

- Objectif : sélectionner `MonoGame` ou `Software` sans recompiler (P3).
- Fichiers : `CasaEngine/Framework/Audio/AudioBackendKind.cs`,
  `CasaEngine/Framework/Audio/AudioBackendSelection.cs` (choix pur, testable),
  `CasaEngine/Framework/Configuration/Project/ProjectSettings.cs`, `ProjectSettingsHelper.cs`,
  `CasaEngine/Framework/Application/Components/AudioSystemComponent.cs`,
  `CasaEngine.Tests/Audio/AudioBackendSelectionTests.cs`, tests des réglages de projet.
  Aucun fichier de l'éditeur ne change : son runtime passe par le même `AudioSystemComponent`,
  sans projet (État vérifié).
- Étapes :
  1. Propriété `AudioBackend` (`[Category("Audio")]`) de type `AudioBackendKind?` : **non
     renseignée** (`null`) quand la clé est absente, écrite seulement quand elle est renseignée.
     Un projet qui ne dit rien ne fige donc aucun backend.
  2. `AudioBackendSelection.Resolve(valeur de CASAENGINE_AUDIO_BACKEND, réglage du projet ou null)`
     rend le backend et sa source (`environment`, `project` ou `default`) selon l'ordre de P3 ;
     le défaut est une constante **unique**, dans `AudioBackendSelection` (`MonoGame` en S1) ;
     valeur inconnue de la variable ou du réglage : ignorée avec un avertissement.
  3. `AudioSystemComponent` lit la variable **une seule fois**, à sa construction, et appelle
     `Resolve` avec les réglages du projet s'il y en a un ; `Software` → backend logiciel sur
     `OpenAlAudioOutput`, repli consigné vers MonoGame puis `NullAudioBackend` en cas d'échec.
     Une ligne `Audio backend: <type> (source: <source>)` est consignée au démarrage, dans tous les
     hôtes.
  4. Un changement du réglage ou de la variable prend effet au prochain lancement.
- Validation : tests de `Resolve` (variable prioritaire sur le projet ; sans variable ni projet,
  cas du runtime de l'éditeur → `MonoGame`, source `default` ; **projet chargé sans réglage →
  `MonoGame`, source `default`** ; projet `Software` → source `project` ; projet `MonoGame`
  explicite → source `project` ; valeurs inconnues → avertissement et ordre suivant) ; tests
  aller-retour du réglage (absent → `null` et rien d'écrit, `MonoGame`, `Software`) ; suite complète
  verte ; build des deux solutions ; éditeur lancé avec `CASAENGINE_AUDIO_BACKEND=Software` →
  ligne `Audio backend: SoftwareAudioBackend (source: environment)` dans son journal.
- Commit : `feat(audio): choose the audio backend from the environment or the project settings`
- Note de validation (2026-10-05) : exécuté par un sous-agent `executor`, câblage relu en session
  principale. Build : 0 erreur. `CasaEngine.Tests` : 2608/2608 (+20 : 14 cas de `Resolve`, 6
  aller-retour du réglage). Démos lancées sans clavier (variables `CASAENGINE_START_DEMO`,
  capture d'écran puis sortie automatique) : `[Info] Audio backend: SoftwareAudioBackend (source: environment)`
  avec `CASAENGINE_AUDIO_BACKEND=Software`, `[Info] Audio backend: MonoGameAudioBackend (source: environment)`
  avec `MonoGame`. Choix de l'exécuteur : seuls les noms `Software` et `MonoGame` comptent (pas de
  valeur numérique), espaces autour de la variable ignorés, avertissement d'une valeur inconnue
  porté par le résultat de `Resolve` (fonction pure). **Reste 🧪** : l'éditeur n'écrit pas de
  journal sur disque (`LoggerEditor` alimente son panneau Log, `DebugLogger` la sortie de
  débogage), donc la ligne `Audio backend: SoftwareAudioBackend (source: environment)` de l'éditeur
  lancé avec `CASAENGINE_AUDIO_BACKEND=Software` est à lire dans son panneau Log pendant la recette
  de T1.8 ; le chemin de code est le même (`AudioSystemComponent`) et le cas sans projet est testé.
  Levé le 2026-10-05 : recette de l'auteur sur la démo et l'éditeur, « test OK » (note de T1.8).

### ✅ T1.6 — Démo : statistiques et harnais de stress

- Objectif : mesurer la tenue du thread audio sous pression du ramasse-miettes (hypothèse de P1).
- Fichiers : `CasaEngine.Demos/Demos/AudioDemo.cs`. Aucun changement de
  `Content/DemosGame.json` : le backend du passage vient de `CASAENGINE_AUDIO_BACKEND` (T1.5).
- Étapes :
  1. Afficher le backend actif, les voix actives, l'avance et le compteur de sous-alimentations.
  2. Mode stress : son en boucle et musique de la démo joués en continu, allocations massives et
     `GC.Collect(2, GCCollectionMode.Forced, true)` toutes les 500 ms. Il se lance par une touche,
     ou **sans clavier** par la nouvelle variable `CASAENGINE_AUDIO_STRESS_SECONDS=<n>` (même
     modèle que `CASAENGINE_START_DEMO`) : la démo démarre le stress dès son activation, consigne
     chaque seconde `Audio stress: backend=<type> elapsed=<s> underruns=<n>`, puis au bout de `n`
     secondes `Audio stress done: backend=<type> seconds=<n> underruns=<n> gc=<n>` et ferme le jeu.
  3. Avec le backend logiciel, la démo consigne une fois si le contrôleur OpenAL de MonoGame a été
     initialisé (`OpenALSoundController`, état lu par réflexion) :
     `Audio stress: monogame-openal-initialized=<true|false>`.
- Validation (par l'agent, sans clavier) : build Debug, puis depuis
  `CasaEngine.Demos/bin/Debug/net9.0-windows/`, lancer `CasaEngine.Demos.exe` avec
  `CASAENGINE_START_DEMO="Audio demo"`, `CASAENGINE_AUDIO_BACKEND=Software`,
  `CASAENGINE_AUDIO_STRESS_SECONDS=60`. Le processus se ferme seul ; dans `log.txt` du même
  dossier : `Audio backend: SoftwareAudioBackend (source: environment)`,
  `monogame-openal-initialized=false` et `Audio stress done: … seconds=60 underruns=0`.
  Au plus deux passages (Budget de la tranche S1) ; deux passages avec des coupures → ⚠️ Blocked
  (O1), arrêt du programme.
- Commit : `feat(demos): show audio backend statistics and a GC stress mode`
- Note de validation (2026-10-05) : exécuté par un sous-agent `executor`, diff relu en session
  principale. Seul `AudioDemo.cs` change : affichage du backend, des voix, de l'avance, du débit et
  des sous-alimentations ; touche `G` ou variable `CASAENGINE_AUDIO_STRESS_SECONDS` ; stress =
  son en boucle et musique, 8 tableaux de 512 Ko alloués par frame, `GC.Collect(2, Forced, true)`
  toutes les 500 ms ; contrôle par réflexion du champ privé `_instance` (jamais la propriété
  `Instance`). Build : 0 erreur ; `CasaEngine.Tests` 2608/2608. **Deux passages de 60 s, même
  build, sans clavier** (budget : deux au plus), `log.txt` :
  passage 1 (exécuteur) `Audio stress done: backend=SoftwareAudioBackend seconds=60 underruns=0 gc=118` ;
  passage 2 (session principale) `Audio backend: SoftwareAudioBackend (source: environment)`,
  `monogame-openal-initialized=false`, 60 lignes par seconde,
  `Audio stress done: backend=SoftwareAudioBackend seconds=60 underruns=0 gc=119`, sortie
  automatique, code 0. Avance par défaut inchangée (4 × 10 ms à 48 000 Hz). O1 non déclenché.
  Démo sous `CASAENGINE_AUDIO_BACKEND=MonoGame` (capture puis sortie) : démarre et se ferme,
  `Audio backend: MonoGameAudioBackend (source: environment)`. L'affichage n'a pas été regardé :
  coup d'œil de l'auteur en T1.8.

### ✅ T1.7 — Documentation

- Objectif : documenter l'architecture et le réglage.
- Fichiers : `docs/engine/audio-system.md`.
- Étapes : section sur le mixeur logiciel et la sortie OpenAL, le réglage `AudioBackend`, la
  sémantique (P5), l'avance (P6), les limites connues mises à jour (ADPCM en S3, pas de limiteur).
- Validation : relecture ; liens vers ADR-0055.
- Commit : `docs(audio): document the software mixer and the backend setting`
- Note de validation (2026-10-05) : écrit en session principale, en français comme le reste du
  document. Schéma d'ensemble mis à jour ; nouvelle section « 1 bis. Mixeur logiciel du moteur »
  (choix du backend, sortie, sémantique, mixage, tests) ; limites connues mises à jour (formats
  `.wav`, ADPCM refusé, bornes 8–48 kHz propres au backend MonoGame, pas de limiteur, latence et
  mesure de stress) ; évolutions reliées au programme ; démo (touche `G`, commande sans clavier).
  Liens relatifs vérifiés (`test -f`).

### 🧪 T1.8 — Vérification de la tranche et recette de l'auteur

- Objectif : prouver le résultat de S1 avant de changer le défaut.
- Sources : tout le diff de la branche.
- Étapes :
  1. Vérificateur frais (`verifier`) sur la Validation globale de S1 ; traitement de chaque
     constat selon sa priorité, dans les limites du Budget de la tranche S1 (une revérification
     ciblée par défaut corrigé, cinq passes au plus pour les P1/P2 ; au-delà, ⚠️ Blocked).
  2. Recette d'écoute par l'auteur, avec `CASAENGINE_AUDIO_BACKEND=Software` puis `MonoGame`
     (la variable vaut pour tous les hôtes) : démo audio, éditeur (aperçu d'un `.sound`),
     parcours Alundra. La ligne `Audio backend: …` du journal confirme le backend de chaque essai.
- Validation : verdict **CONFIRMED** ; accord de l'auteur. En attente de l'auteur : 🧪.
- Commit : `docs(plan): record the S1 verification and listening test`
- Note de validation, étape 1 (2026-10-05) : vérificateur frais (`verifier`) sur `main..d2397514`,
  verdict **CONFIRMED**, aucun constat P0–P2. Preuves rejouées par lui : build des deux solutions
  (0 erreur), `CasaEngine.Tests` 2608/2608 (108/108 sur les suites audio nouvelles), aucune
  différence de signature sur `IAudioBackend`, `AudioService`, `IAudioClip`, `IAudioClipSamples`,
  `AudioVoiceParameters` ; son propre stress de 60 s : `underruns=0 gc=119`,
  `monogame-openal-initialized=false` ; retour MonoGame par la variable : ligne et capture
  « Backend: MonoGameAudioBackend » ; essai hors dépôt sur le vrai périphérique : les deux backends
  jouent des `PcmAudioClip` à 6 000, 22 050, 44 100, 48 000 et 96 000 Hz, mono et stéréo, en boucle
  ou non, relecture depuis le cache, `Dispose` propre (thread joint en 14 ms). Avis non bloquants
  reportés (règle P3/P4 : pas de correction d'un résultat confirmé) : O6 à O9.
- Étape 2, recette de l'auteur : démo et éditeur (dont la ligne `Audio backend:` du panneau Log,
  reste de T1.5) avec `CASAENGINE_AUDIO_BACKEND=Software` puis `MonoGame` : **« test OK »**
  (auteur, 2026-10-05). O10 tranché par l'auteur le même jour (« intègre la pile e19
  maintenant ») : fusion de `chantier/e19s2-ui-clip-view-space` (`a6efd2a9`, toute la pile `e19`,
  ADR-0048 à 0054) dans cette branche, commit `5d048882` ; conflits limités aux réglages de projet
  (`AudioBackend` et `VirtualResolution` gardés tous deux) et aux index ; build des deux solutions
  0 erreur, `CasaEngine.Tests` 2734/2734. Passage de contrôle en session principale : Launcher du
  worktree sur `alundra-project/AlundraGame.json` avec `CASAENGINE_AUDIO_BACKEND=Software`,
  25 s : `Audio backend: SoftwareAudioBackend (source: environment)`, 1 206 lignes de journal sans
  exception, erreur ni avertissement ; aucune écriture constatée dans `alundra-project` (ce dossier
  est ignoré par git dans le dépôt parent : `git status` n'y voit rien, le contrôle valable est par
  date de fichier, fait par le vérificateur de clôture). **Reste 🧪 : l'écoute d'Alundra
  par l'auteur** sur ce moteur, puis T1.9.

### ✅ T1.9 — Bascule du défaut

- Objectif : `Software` devient le backend par défaut (P3), après T1.8 seulement.
- Fichiers : `CasaEngine/Framework/Audio/AudioBackendSelection.cs`,
  `CasaEngine.Tests/Audio/AudioBackendSelectionTests.cs`, `docs/engine/audio-system.md`,
  ADR-0055 (conséquences).
- Étapes : la constante de défaut de `AudioBackendSelection` passe à `Software`, seul changement
  de code (le réglage absent reste `null`) ; `MonoGame` reste sélectionnable explicitement par le
  projet ou par la variable. L'éditeur passe
  donc au backend logiciel ; son retour arrière est `CASAENGINE_AUDIO_BACKEND=MonoGame`, celui
  d'un jeu est le réglage de projet ou la même variable (doc mise à jour).
- Validation : tests de `Resolve` mis à jour (projet chargé sans réglage → `Software`, source
  `default`) ; suite complète verte ; build des deux solutions ; Launcher (il charge toujours un
  projet) dont le projet ne renseigne pas `AudioBackend` → `Audio backend: SoftwareAudioBackend (source: default)` ;
  Launcher avec `CASAENGINE_AUDIO_BACKEND=MonoGame` → `MonoGameAudioBackend (source: environment)`.
- Commit : `feat(audio): make the software mixer the default audio backend`
- Note de validation (2026-10-05) : après « test Alundra OK, fais toute la suite » de l'auteur.
  Fait en session principale (une constante : délégation sans bénéfice). `DefaultKind` =
  `Software`, seul changement de code ; nouveau test qui épingle ce défaut. Doc §1 bis et
  conséquences d'ADR-0055 mises à jour. Build des deux solutions : 0 erreur ; `CasaEngine.Tests`
  2735/2735. Launcher du worktree sur `alundra-project/AlundraGame.json` (qui ne renseigne pas
  `AudioBackend`) : sans variable → `Audio backend: SoftwareAudioBackend (source: default)` ; avec
  `CASAENGINE_AUDIO_BACKEND=MonoGame` → `Audio backend: MonoGameAudioBackend (source: environment)` ;
  aucune erreur ; aucune écriture dans `alundra-project` (contrôle par date de fichier du
  vérificateur de clôture, `git status` n'y voyant rien car le dossier est ignoré par git).
- Clôture de la tranche S1 (2026-10-05) : la fusion `e19` et la bascule du défaut étant venues après
  le premier verdict, un second vérificateur frais a contrôlé l'état final `d7ef352b` :
  **CONFIRMED**, aucun constat P0–P2. Build des deux solutions 0 erreur ; `CasaEngine.Tests`
  2735/2735 ; résolution des quatre conflits comparée aux deux parents (rien de perdu, aucun
  marqueur) ; défaut unique (`AudioBackendSelection.cs:30`) ; Launcher sur Alundra : défaut →
  logiciel, variable `MonoGame` → MonoGame ; stress de 60 s sur le défaut, build fusionné :
  `underruns=0 gc=117`, `monogame-openal-initialized=false`. Deux avis P4 non bloquants : la
  preuve « rien d'écrit » doit passer par les dates de fichier (corrigé ci-dessus) ; aucun test
  n'enregistre ensemble `AudioBackend` et `VirtualResolution` (clés indépendantes, chacune testée).
  **Tranche S1 terminée.**

---

## Vague 2 — tranches S2, S3 et X1 (détail du 2026-10-05)

Détail écrit après la clôture de S1, en mode AUTO : l'auteur a demandé « fini tout » avant de
partir dormir, et cette demande vaut accord pour exécuter les tranches de l'enveloppe une fois
leur détail relu (READY). Découverte en lecture seule (trois enquêtes, chacune passée par un
vérificateur contradictoire) ; faits repris ci-dessous avec leurs fichiers. Ordre d'exécution :
**S3, puis S2, puis X1** (X1 réutilise les correctifs O6–O8 de S2 ; une seule branche, un seul
écrivain à la fois). Relecture des détails : S3 **READY** au premier passage ; S2 et X1 : deux
REVISE, chaque point tranché FIX (S2 : compteur consommé par génération, règle du travailleur,
méthodes `With*`, liaison sans `unsafe`, état « reconnexion » ; X1 : tables matérielles fournies
par l'appelant, anneau borné, ordre téléversement/registres), puis relecture de clôture : X1
**READY** ; S2 REVISE sur le seul point T2.6 (débranchement : pas de point d'injection simulable
au niveau du périphérique) → T2.6 séparée de S2 et mise en pause, puis relecture de clôture de S2
réduite (T2.1–T2.5, T2.7) : **READY**. S3 passe en premier parce qu'elle était prête la première.

### Décisions de la vague 2 (arbitrages de l'agent, à confirmer par l'auteur à son retour)

| Réf | Arbitrage |
|---|---|
| P9 | **Capacités optionnelles plutôt que `IAudioBackend`.** Les nouveautés de S2 et X1 passent par des interfaces publiques optionnelles que `SoftwareAudioBackend` implémente et qu'`AudioService` détecte (`backend is I…`). `IAudioBackend` ne change pas, donc ni le faux backend du moteur ni celui d'`Alundra.Tests` (dépôt parent). Le backend MonoGame garde le chemin actuel. |
| P10 | **Voix stéréo au thread audio** : sous le backend logiciel, `PlayClipStereo` joue une voix résidente mono avec des gains gauche/droite explicites appliqués au rendu (bloc suivant, tout débit, rééchantillonnage cubique). Fin de la file de ~60 ms et des facteurs entiers d'ADR-0039 pour ce backend ; `StereoVoiceMixer` reste le chemin des autres backends. |
| P11 | **Région de boucle par appel** : `AudioVoiceParameters` reçoit une région de boucle optionnelle (début et fin en trames, additif : nouveau `WithLoopRegion`, le constructeur existant ne change pas). Le backend logiciel la respecte sur les voix résidentes et stéréo ; le backend MonoGame boucle le clip entier (documenté). Pas de champ sérialisé en S2 (`SoundAsset` inchangé). |
| P12 | **Plage de pitch élargie** : multiplicateur de vitesse additif (`WithRateMultiplier`, borné à ]0, 16]), qui s'ajoute au pitch en octaves borné à ±1 (inchangé). Le backend MonoGame le replie sur son pitch, borné. |
| P13 | **Streaming hors du thread de jeu** : un thread unique « CasaEngine Audio Streaming » lit et décode pour toutes les pistes de `MusicPlayer` ; l'ouverture, l'en-tête et le premier remplissage restent synchrones (contrat public de `MusicPlayer` inchangé) ; le thread de jeu ne fait plus que recopier vers la voix. Mode « en ligne » pour les tests. |
| P14 | **Rampes « à l'échantillon » déplacées en S4** : le mixeur fait déjà une rampe par bloc (pas de clic) ; une rampe à durée explicite suppose de revoir le modèle gain de bus × fondu, qui est l'objet de S4. |
| P15 | **Ogg résident seulement en S3** (NVorbis 0.10.4, déjà livré par MonoGame, référencé directement à la même version) : le décodage alloue à chaque paquet, admissible au chargement mais pas en streaming (AGENTS.md §9.3). Une piste Ogg marquée streaming est refusée avec un message clair (O11). |
| P16 | **ADPCM MS et IMA en S3, résident seulement** (`WavDecoder`), avec des fichiers d'essai générés localement par le `ffmpeg` du cache NuGet et commités (sinusoïdes synthétiques). |
| P17 | **X1 sans aucune table copiée d'un tiers** : ADSR, volumes, sweep, bruit, PMON, boucles par drapeaux, réverbération implémentés depuis les formules de psx-spx ; rien de l'analyseur (tables P.E.Op.S sous GPL), de psyz (MPL-2.0) ni de DuckStation. **Un traitement unique pour toutes les tables de constantes matérielles** dont psx-spx est la seule source (les 5 couples de coefficients des filtres ADPCM, les coefficients du filtre FIR de la réverbération, la table gaussienne de 512 entrées) : le moteur n'en livre **aucune** tant que l'auteur n'a pas tranché O12 ; le SPU les reçoit de l'appelant dans un objet `PsxSpuHardwareTables` (validé à la construction), et les tests utilisent des tables synthétiques définies dans les tests. L'interpolation reste un point d'extension (cubique du moteur par défaut, gaussienne quand une table est fournie). Le SPU n'est donc utilisable en jeu qu'après O12, ce qui ne bloque rien : son adoption (X4) est en pause. |

### Enveloppe ajustée

- S2 perd les « rampes de volume à l'échantillon » (P14), ajoutées à S4.
- S3 perd l'Ogg en streaming et MP3/FLAC/Opus (nouvelles dépendances) : en pause, questions
  O11 et O13.
- X1 garde son périmètre, mais les tables de constantes matérielles (coefficients ADPCM, FIR de
  réverbération, table gaussienne) sont fournies par l'appelant, aucune n'étant livrée avant la
  décision de l'auteur (O12). Sa sortie « exacte » s'entend au débit
  interne du SPU (44 100 Hz, entiers 16 bits), avant le rééchantillonnage vers le débit du
  périphérique.

---

## Phase 2 — Tranche S2 : temps réel et streaming

Résultat attendu : sous le backend logiciel, les voix stéréo d'Alundra ont leurs gains exacts sans
file de 60 ms, les boucles peuvent viser une région, la vitesse peut dépasser une octave, la lecture
des musiques quitte le thread de jeu ; O6 à O8 sont corrigés. Non-objectifs : `IAudioBackend`,
`SoundAsset` et le dépôt parent ne changent pas ; rampes explicites (S4) ; **débranchement et
changement de périphérique (T2.6), séparés de S2 après la relecture de clôture et mis en pause**
(voir T2.6). Prérequis : S1. Retour arrière : `CASAENGINE_AUDIO_BACKEND=MonoGame`, ou
revert des commits de la tranche.

État vérifié (2026-10-05, branche) :
- `SoftwareAudioBackend.IsAvailable` vaut `_mixer != null` (`SoftwareAudioBackend.cs:107`) alors
  que la sortie remet son propre `IsAvailable` à faux quand son thread meurt
  (`OpenAlAudioOutput.cs:154-160`) ; `RingWait` attend 100 ms (`:549-571`).
- Le compte de buffers en attente repose sur des évènements qui peuvent se perdre (anneau plein,
  `SoftwareMixer.cs:817-833`) et sur le chemin d'abandon d'un chunk (`:472-487`) ; or
  `MusicPlayer` et `StereoVoiceMixer` ne relâchent une voix finie qu'à 0 en attente
  (`MusicPlayer.cs:254`, `StereoVoiceMixer.cs:146`, `:158-160`).
- `StereoVoiceMixer` borne le débit à 8–48 kHz et rééchantillonne par facteur entier, quel que
  soit le backend (`StereoVoiceMixer.cs:70`, `:284-317`) ; le mixeur garde déjà des gains gauche et
  droite cibles et courants par voix (`MixerVoice.cs:91-94`).
- Les voisins de l'interpolation bouclent sur le clip entier (`SoftwareMixer.cs:647-653`,
  `:841-850`) : une région de boucle doit aussi borner les voisins.
- `MusicPlayer` est public ; ses tests figent un contrat synchrone (lecture active et 3 buffers en
  attente juste après `Play`, `Play` invalide si le fichier ne s'ouvre pas) (`MusicPlayerTests.cs:44-52`,
  `:209-234`).
- Le mixeur n'accepte qu'un producteur (`SoftwareMixer.cs:12-15`) : un thread de lecture ne peut
  pas soumettre lui-même.
- Les fonctions `alcReopenDeviceSOFT` et `alcEvent*SOFT` ne sont pas exportées par `openal.dll` :
  `alcGetProcAddress` obligatoire. `ALC_EXT_disconnect` (`ALC_CONNECTED`) se lit par
  `alcGetIntegerv`, déjà lié. Le rappel d'évènements système tourne sur un thread système et ne doit
  appeler ni AL ni ALC. Une source jouée sur un périphérique déconnecté passe aussitôt à
  `AL_STOPPED` (OpenAL Soft 1.24.3, `al/source.cpp`).
- Les tests `AudioServiceStereoVoiceTests`, `AudioServiceStreamingTests` et `MusicPlayerTests`
  tournent sur le faux backend : ils figent le chemin de repli et restent tels quels.

Budget de la tranche : un `executor` par tâche de code ; deux échecs → reprise en session
principale ; une revérification ciblée par défaut corrigé, cinq passes au plus pour les P1/P2 du
vérificateur ; stress : deux passages au plus par build ; budget épuisé → ⚠️ Blocked, question
écrite, arrêt de la tranche (les autres continuent).

### ✅ T2.1 — Santé de la sortie (O6)

- Objectif : un thread audio mort rend le backend muet et sans attente.
- Fichiers : `CasaEngine/Framework/Audio/Backends/SoftwareAudioBackend.cs`,
  `CasaEngine.Tests/Audio/SoftwareAudioBackendTests.cs`, `CasaEngine.Tests/Audio/OfflineAudioOutput.cs`.
- Étapes : `IsAvailable` = mixeur présent **et** sortie disponible ; `RingWait` et la recherche de
  voix s'arrêtent tout de suite quand la sortie ne l'est plus ; une ligne de journal au passage à
  l'état muet. Seule la **mort du thread audio** rend la sortie indisponible (et le backend muet,
  définitivement) ; une perte de périphérique n'est pas une mort : voir l'état « reconnexion » de
  T2.6.
- Validation : test avec une sortie hors ligne qui « meurt » : `IsAvailable` faux, `Play` rend
  `None`, un appel quand l'anneau est plein ne bloque pas (moins de 5 ms mesurées) ; suite complète.
- Commit : `fix(audio): mute the software backend when its output thread dies`
- Note de validation (2026-10-05) : exécuté par un sous-agent `executor`, diff relu.
  `IsAvailable` = mixeur et sortie vivants (lecture sans allocation) ; `RingWait` s'arrête net si
  la sortie est morte ; un avertissement unique ; `_isDisposed` posé avant de libérer la sortie
  (pas d'avertissement au `Dispose`). `OfflineAudioOutput.Die()` pour les tests. Mesures : `Pause`
  sur anneau plein avec sortie morte 0,153 ms (≈ 100 ms avant) ; tour de six appels ≈ 0,0025 ms.
  L'attente bornée de 100 ms reste inchangée pour une sortie vivante (test existant vert).
  `CasaEngine.Tests` 2798/2798 (+2). O6 corrigé.

### ⏳ T2.2 — Comptes de streaming fiables et arrêts garantis (O7, O8)

- Objectif : `GetPendingBufferCount` ne peut plus rester trop haut, et un emplacement n'est rendu
  qu'une fois son arrêt transmis.
- Fichiers : `CasaEngine/Framework/Audio/Software/SoftwareMixer.cs`, `MixerVoice.cs`,
  `SoftwareAudioBackend.cs`, tests `Software/SoftwareMixerTests.cs` et `SoftwareAudioBackendTests.cs`.
- Étapes : le mixeur publie par emplacement le couple **(génération, buffers consommés)** dans une
  seule valeur 64 bits écrite par `Volatile.Write` (génération en poids fort) ; le backend la lit au
  lieu de compter des évènements et **ignore une valeur d'une autre génération** que celle de la
  voix (compte consommé = 0 tant que le thread audio n'a pas appliqué la création) ; un chunk
  abandonné qui termine un buffer compte comme consommé ; `Release`/`StopAll` gardent
  l'emplacement hors de la liste libre tant que l'ordre d'arrêt n'est pas passé, et le renvoient à
  l'appel suivant. Défense en profondeur : `MusicPlayer.FillQueue` soumet au plus
  `QueuedBufferTarget` buffers par appel (aujourd'hui sans borne, `MusicPlayer.cs:273-300`).
- Validation : tests : anneau d'évènements saturé puis vidé → compte exact ; débordement de la
  file de 128 chunks → compte qui redescend à 0 ; **réutilisation d'emplacement** : voix de
  streaming créée, N buffers consommés, relâchée, nouvelle voix dans le même emplacement, 3 buffers
  soumis sans rendu entre-temps → `GetPendingBufferCount` = 3 ; `MusicPlayer.Play` d'une piste en
  boucle dans un emplacement réutilisé revient après un nombre borné de soumissions ; arrêt
  abandonné puis renvoyé → la voix se tait et l'emplacement revient ; test sans allocation
  inchangé ; `MusicPlayerTests` inchangés et verts ; suite complète.
- Commit : `fix(audio): make streaming pending counts and voice stops reliable`

### ⏳ T2.3 — Voix stéréo au thread audio (P10)

- Objectif : `PlayClipStereo` sans file de 60 ms ni facteur entier sous le backend logiciel.
- Fichiers : `CasaEngine/Framework/Audio/IStereoVoiceBackend.cs` (capacité publique optionnelle),
  `Software/SoftwareMixer.cs`, `MixerMessages.cs`, `MixerVoice.cs`, `Backends/SoftwareAudioBackend.cs`,
  `AudioService.cs` (routage interne, aucune signature publique changée), tests
  `CasaEngine.Tests/Audio/SoftwareStereoVoiceTests.cs`, `docs/decisions/0056-…md` (nouvelle ADR des
  décisions P9 à P13, qui remplace en partie ADR-0039 pour ce backend).
- Étapes : mode « gains explicites » d'une voix résidente mono dans le mixeur (gauche = volume ×
  gainG, droite = volume × gainD, sans loi de pan) ; `IStereoVoiceBackend` : jouer un clip mono
  avec ses gains, changer les gains d'une voix vivante ; `AudioService.PlayClipStereo`,
  `SetVoiceStereoGains` et `GetVoiceStereoGains` utilisent la capacité si le backend l'a, sinon le
  chemin `StereoVoiceMixer` actuel ; bus, propriétaire, arrêts, pause et fondus inchangés.
- Validation : tests sur le backend logiciel avec sortie hors ligne : gains exacts par canal à
  1e-6 près sur un signal constant, changement de gains visible au bloc suivant, tons à 3 370 Hz et
  172 610 Hz joués sans refus, voix recyclée à sa fin, fondu et propriétaire comme une voix
  ordinaire ; les tests existants sur le faux backend inchangés et verts ; suite complète.
- Commit : `feat(audio): mix software stereo voices on the audio thread`

### ⏳ T2.4 — Région de boucle et multiplicateur de vitesse (P11, P12)

- Objectif : boucler sur une région et dépasser une octave, de façon additive.
- Fichiers : `CasaEngine/Framework/Audio/AudioVoiceParameters.cs` (membres et méthodes ajoutés,
  égalité et `ToString` mis à jour), `Software/SoftwareMixer.cs`, `MixerVoice.cs`,
  `Backends/MonoGameAudioBackend.cs` (repli documenté), tests `AudioVoiceParametersTests` (ou le
  fichier de contrat existant) et `Software/SoftwareMixerTests.cs`.
- Étapes : région `[début, fin[` en trames, validée contre la longueur du clip au départ de la
  voix (région invalide → clip entier, avertissement limité) ; voisins de l'interpolation bornés à
  la région ; multiplicateur de vitesse ]0, 16] (NaN ou hors borne → 1) appliqué au pas ; **chaque
  méthode `With*` existante conserve la région et le multiplicateur** (aujourd'hui toutes repassent
  par le constructeur à 4 arguments, `AudioVoiceParameters.cs:40-46`, et `AudioService` les utilise
  partout : gain de bus, `PlayClipStereo`, pan, fondus) ; les voix stéréo de T2.3 en profitent ; le
  backend MonoGame ignore la région et replie le multiplicateur sur son pitch borné.
- Validation : tests : une boucle 1 000–2 000 rejoue exactement les trames 1 000 à 1 999 sans saut
  à la jointure (continuité de l'interpolation vérifiée) ; multiplicateur 4 → durée divisée par 4 ;
  paramètres par défaut identiques à avant (égalité, hachage) ; **de bout en bout par
  `AudioService`** avec un backend qui enregistre les paramètres reçus : `Play` et `PlayClipStereo`
  avec `WithLoopRegion(1000, 2000).WithRateMultiplier(4)`, puis `SetVoicePan`, un fondu et un
  changement de volume de bus → le backend reçoit toujours la région et le multiplicateur ; zéro
  allocation ; suite complète.
- Commit : `feat(audio): loop regions and a rate multiplier for voices`

### ⏳ T2.5 — Lecture des musiques hors du thread de jeu (P13)

- Objectif : plus aucune lecture disque dans `Update` pour les pistes de `MusicPlayer`.
- Fichiers : `CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs`, nouveau
  `Streaming/StreamingWorker.cs` (thread unique, file par piste en anneau d'octets SPSC), tests
  `MusicPlayerTests.cs` (mode en ligne) et nouveaux tests du travailleur.
- Étapes : ouverture, en-tête et premier remplissage synchrones dans `Play` (contrat inchangé) ;
  le travailleur lit et rembobine les fichiers ; `Update` recopie de l'anneau vers la voix ;
  position tirée d'un compteur atomique ; `Dispose` et `Stop` libèrent la piste côté travailleur ;
  erreur de lecture → piste arrêtée et journal, jamais d'exception sur le thread de jeu. **Règle de
  choix** : `AudioService` crée son `MusicPlayer` (`AudioService.cs:32`) en mode « travailleur »
  quand son backend est un backend réel (`SoftwareAudioBackend` ou `MonoGameAudioBackend`), et en
  mode « en ligne » sinon (faux backends des tests, `NullAudioBackend`) ; le mode est un paramètre
  interne de `MusicPlayer`, sans changement de son API publique. Les `MusicPlayerTests`, construits
  sur `FakeAudioBackend`, restent donc en ligne et inchangés.
- Validation : `git diff` de `MusicPlayerTests.cs` vide, tests verts ; nouveau test : `AudioService`
  sur `SoftwareAudioBackend` (sortie hors ligne) joue une piste streamée, plusieurs `Update` → le
  flux de test, qui enregistre le thread de chaque lecture, ne voit **aucune lecture sur le thread
  de jeu après `Play`** ; boucle et fin de piste via le travailleur réel (attente bornée) ; démo :
  stress de 60 s avec la musique : 0 sous-alimentation ; suite complète.
- Commit : `feat(audio): read streamed music on a background worker`

### ⚠️ T2.6 — Débranchement et changement de périphérique (séparée de S2, en pause)

> **En pause (2026-10-05)** : la relecture de clôture de S2 a montré que l'acceptation ci-dessous
> n'est pas démontrable en l'état. Le seul point d'injection, `IOpenAlStream`, ne couvre que les
> sources et les buffers (`IOpenAlStream.cs:7-22`) ; l'état de sortie, le cycle de vie du thread,
> l'ouverture du périphérique et le rythme de la boucle vivent dans `OpenAlAudioOutput`, qui appelle
> `OpenAlNative` directement (`OpenAlAudioOutput.cs:46`, `:124-260`), et aucun test ne construit
> `OpenAlAudioOutput`. Avant reprise, il faut concevoir : le composant qui porte la coupure (test de
> connexion, essai de réouverture, état « reconnexion », rendu au rythme du périphérique dans un
> tampon jeté, suppression du comptage de sous-alimentations) derrière une interface simulable au
> niveau du périphérique ; et ce que construit le test (une vraie `OpenAlAudioOutput` sur la couche
> simulée, reliée à `SoftwareAudioBackend`), avec la liste complète des fichiers. Règle de relecture
> atteinte (deux REVISE puis clôture REVISE) : décision de l'auteur ou nouvelle conception, puis une
> relecture. Le reste de S2 n'en dépend pas.

- Objectif : le son continue après un débranchement ou un changement de sortie par défaut.
- Fichiers : `Output/OpenAl/OpenAlNative.cs` (fonctions obtenues par `alcGetProcAddress`,
  constantes citées depuis les en-têtes 1.24.3), `OpenAlAudioOutput.cs`, `OpenAlRefillLoop.cs`,
  `IOpenAlStream.cs`, tests `Output/OpenAlRefillLoopTests.cs`.
- Étapes : détection par `ALC_CONNECTED` (et, si l'extension existe, par l'évènement système de
  changement de sortie par défaut, simple drapeau posé par le rappel) ; le thread audio rouvre le
  périphérique avec `alcReopenDeviceSOFT` en gardant le débit courant ; tant qu'il n'y a pas de
  sortie, nouvel essai toutes les secondes sans compter de sous-alimentation ; extension absente →
  comportement de S1, une ligne d'information. **Pendant la coupure** : la sortie reste
  disponible, dans un état « reconnexion » (exposé en lecture pour la démo et les tests) ; le thread
  audio **continue d'appeler le rendu du mixeur au rythme du périphérique** (un bloc par période,
  rendu dans un tampon jeté), si bien que les commandes du jeu sont vidées et qu'aucun appel du jeu
  n'attend ; après une réouverture réussie, la lecture reprend normalement (les voix vivantes
  continuent). **Liaison sans code `unsafe`** (le projet n'active
  pas `AllowUnsafeBlocks`, `CasaEngine.csproj` ne change pas) : `alcGetProcAddress` par
  `DllImport`, puis `Marshal.GetDelegateForFunctionPointer` pour les appels et
  `Marshal.GetFunctionPointerForDelegate` pour le rappel, ces délégués étant créés une seule fois
  à l'ouverture. Le délégué du rappel est **gardé dans un champ** pendant toute l'inscription, et
  les évènements sont désactivés (`alcEventControlSOFT`) **avant** `alcCloseDevice` et au `Dispose`.
  Le rappel ne fait que poser un drapeau `Volatile` : aucun appel AL/ALC, aucune allocation.
- Validation : tests de la boucle avec la couche simulée (déconnexion → réouverture → reprise ;
  échec de réouverture → nouvel essai ; pas de comptage de sous-alimentation pendant la coupure) ;
  **pendant une coupure simulée**, plus de commandes du jeu que la capacité de l'anneau → chaque
  appel rend la main en moins de 5 ms, `IsAvailable` reste vrai (état « reconnexion »), puis après
  la réouverture simulée un nouveau `Play` rend un handle valide et sa voix est rendue ; le test
  « thread mort » de T2.1 passe toujours ;
  `git diff` de la tâche sans mot-clé `unsafe` ni `AllowUnsafeBlocks` ; relecture : délégué gardé
  en champ, désactivation avant fermeture ; essai réel ponctuel non commité : changement de la
  sortie par défaut de Windows pendant la démo **avec le mode stress actif** (ramasse-miettes
  forcé), sans plantage, consigné. Si l'essai réel n'est pas faisable sans l'auteur : 🧪 avec la
  manipulation écrite.
- Commit : `feat(audio): survive audio device loss and default device changes`

### ⏳ T2.7 — Documentation, ADR et vérification de la tranche

- Objectif : documenter S2 et prouver la tranche.
- Fichiers : `docs/engine/audio-system.md`, `docs/decisions/0056-…md`, index des ADR, ce plan.
- Étapes : doc (voix stéréo, boucles, vitesse, travailleur, limites du repli
  MonoGame) ; vérificateur frais sur S2 ; stress de 60 s sur le build final.
- Validation : verdict **CONFIRMED** ; stress 0 sous-alimentation.
- Commit : `docs(audio): document real-time stereo voices, loops and streaming`

---

## Phase 3 — Tranche S3 : formats

Résultat attendu : le moteur lit des `.ogg` comme clips résidents et des `.wav` ADPCM (MS et IMA),
dans le jeu et dans l'éditeur. Non-objectifs : Ogg en streaming (O11), MP3, FLAC, Opus (O13).
Prérequis : S1 (S2 pour l'ordre d'exécution seulement). Retour arrière : revert des commits.

État vérifié (2026-10-05, branche) :
- NVorbis 0.10.4 est déjà livré par MonoGame (dépendance de `MonoGame.Framework.DesktopGL`
  3.8.5.1), licence MIT, entièrement géré ; un décodage mesuré alloue environ 41 Ko par lecture de
  4 096 trames.
- Un seul chargeur par type dans `AssetContentManager` (dictionnaire, `Add`) : un deuxième chargeur
  `IAudioClip` lèverait une exception ; il faut un chargeur unique qui choisit par extension.
- Le Content Browser classe déjà `.ogg` en son (`ContentItem.cs:228`) ; le sélecteur de fichier de
  l'inspecteur `.sound` n'accepte que `.wav` (`SoundAssetInspectorPanel.cs:347`).
- `WavDecoder` refuse l'ADPCM et les tests l'affirment (`WavDecoderTests`) ; `WavStreamReader` ne
  lit que le PCM 16 bits (inchangé).
- `ffmpeg` est présent dans le cache NuGet (`monogame.tool.ffmpeg/7.0.0.10`) avec les encodeurs
  libvorbis, adpcm_ms et adpcm_ima_wav.

Budget : identique à S2.

### 🧪 T3.1 — Chargeur de clips multi-format et Ogg résident (P15)

- Fichiers : `Directory.Packages.props` et `CasaEngine/CasaEngine.csproj` (référence directe à
  NVorbis **0.10.4**, la version déjà livrée), `CasaEngine/Framework/Audio/Decoding/OggDecoder.cs`,
  nouveau `CasaEngine/Framework/Assets/Loaders/AudioClipLoader.cs` (choix par extension, `.wav` et
  `.ogg`) enregistré à la place de `WavAudioClipLoader` (ajouté par T1.1 sur cette branche, jamais
  publié : supprimé, ses tests repris), `Streaming/MusicPlayer.cs` (une piste Ogg marquée streaming
  est refusée avec un message clair), `CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs`
  (filtre `.wav` et `.ogg`), fichiers d'essai sous `CasaEngine.Tests/Audio/Fixtures/`, tests.
- Étapes : décodage complet au chargement en `PcmAudioClip` (plus de 2 canaux → refus avec raison) ;
  conversion flottant → 16 bits identique à `WavDecoder` ; fichiers d'essai générés par `ffmpeg`
  (sinusoïde synthétique mono et stéréo, quelques dixièmes de seconde) et commités avec la commande
  qui les recrée.
- Validation : tests du décodeur (débit, canaux, durée, fréquence de la sinusoïde par passages par
  zéro), du chargeur (`.wav`, `.ogg`, extension inconnue, fichier corrompu → `null` sans exception),
  du refus en streaming ; build des deux solutions ; inspecteur : 🧪 (choisir un `.ogg` dans
  l'éditeur, coup d'œil de l'auteur).
- Commit : `feat(audio): load ogg vorbis files as resident clips`
- Note de validation (2026-10-05) : exécuté par un sous-agent `executor`, diff relu en session
  principale. NVorbis 0.10.4 référencé directement (même version que celle livrée par MonoGame,
  restauration depuis le cache local, aucun téléchargement) ; `OggDecoder` et `PcmConversion`
  (conversion flottant → 16 bits partagée avec `WavDecoder`) ; `DecodedWav` renommé `DecodedPcm` ;
  `AudioClipLoader` (`.wav` et `.ogg`) remplace `WavAudioClipLoader` (renommé avec ses tests) ;
  `MusicPlayer` refuse une piste Ogg streamée (« OggS » détecté, une ligne d'erreur, aucune voix) ;
  filtre de l'inspecteur `.sound` élargi à `.ogg` ; trois fixtures synthétiques de 4 à 6 Ko avec
  leurs commandes `ffmpeg` (`CasaEngine.Tests/Audio/Fixtures/README.md`). Build des deux solutions :
  0 erreur ; `CasaEngine.Tests` 2750/2750 (+15). **Reste 🧪** : choisir un `.ogg` dans
  l'inspecteur d'un `.sound` de l'éditeur et l'écouter (coup d'œil de l'auteur).

### ✅ T3.2 — ADPCM MS et IMA (P16)

- Fichiers : `CasaEngine/Framework/Audio/Decoding/WavDecoder.cs`, fichiers d'essai, `WavDecoderTests.cs`.
- Étapes : décodage par blocs depuis la documentation officielle (citée en commentaire) : MS ADPCM
  (étiquette 2, table de coefficients de l'extension `fmt `, échantillons par bloc), IMA/DVI
  (étiquette 0x11, tables de pas et d'index) ; nombre d'échantillons du bloc `fact` pour couper le
  remplissage ; les tests qui affirmaient le refus de l'ADPCM passent à l'acceptation.
- Validation : comparaison échantillon par échantillon avec le décodage de `ffmpeg` des mêmes
  fichiers (fichiers de référence PCM commités), mono et stéréo ; blocs tronqués → refus avec raison ;
  suite complète.
- Commit : `feat(audio): decode MS and IMA ADPCM wav files`
- Note de validation (2026-10-05) : exécuté par un sous-agent `executor`, renvoyé une fois en
  correction (liens écrits de mémoire, dernier bloc court refusé). `AdpcmDecoder` interne appelé par
  `WavDecoder` (étiquettes 2 et 0x11, résident seulement ; `WavStreamReader` inchangé ; contrôles
  PCM inchangés ; ADPCM dans `WAVE_FORMAT_EXTENSIBLE` refusé). Sources citées dans l'en-tête du
  fichier, toutes récupérées : wiki.multimedia.cx (Microsoft ADPCM, Microsoft IMA ADPCM, IMA ADPCM)
  et le document IMA « Recommended Practices » rév. 3.00 (1992) ; les deux pages Microsoft Learn
  citées d'abord répondaient 404 et ont été retirées. Choix : reconstruction IMA sous la forme fermée
  `((2·(nibble&7)+1)·pas)>>3` (celle de `ffmpeg`) plutôt que la forme par additions du document IMA,
  qui diffère de 2 au plus par échantillon ; dernier bloc plus court accepté s'il contient l'en-tête
  complet (comme certains encodeurs l'écrivent). Fixtures : 4 fichiers ADPCM (MS et IMA, mono et
  stéréo, 22 050 Hz) et leurs références PCM décodées par `ffmpeg` (5 à 37 Ko), commandes dans le
  README. Résultat : **égalité à l'échantillon près** avec la référence, avec et sans bloc `fact`.
  Tests de refus passés de l'ADPCM à 0x55 (MP3 dans un WAV) et 6 (a-law). Build des deux solutions :
  0 erreur ; `CasaEngine.Tests` 2796/2796 (+46).

### 🚧 T3.3 — Documentation, ADR et vérification

- Fichiers : `docs/engine/audio-system.md`, `docs/decisions/0057-…md` (P15, P16), index, ce plan.
- Validation : vérificateur frais **CONFIRMED**.
- Commit : `docs(audio): document ogg and adpcm support`

---

## Phase 4 — Tranche X1 : module SPU PSX

Résultat attendu : un SPU PlayStation logiciel dans le moteur (`CasaEngine.Framework.Audio.Psx`),
piloté par registres depuis le jeu, rendu sur le thread audio à 44 100 Hz puis mélangé au reste :
SPU RAM de 512 Ko, 24 voix ADPCM décodées à la volée selon les drapeaux de bloc, pas de pitch et son
écrêtage, ADSR et ENVX, volumes fixes et sweep (y compris négatifs), key on/off et ENDX, bruit,
PMON, réverbération pilotée par registres. Non-objectifs : table gaussienne (O12), données
d'Alundra (X2), séquenceur (X3), XA (X5), entrée CD et capture. Prérequis : S1 et les correctifs O6
à O8 de S2. Retour arrière : revert ; rien n'utilise le module avant X4.

État vérifié (2026-10-05) :
- Le mixeur rend au débit du périphérique, en flottant (`SoftwareAudioBackend.cs:87`,
  `SoftwareMixer.cs:340-396`) : l'exactitude se teste sur la sortie interne 16 bits du SPU.
- Les commandes sont appliquées au début de chaque bloc (`SoftwareMixer.cs:350-353`) : les
  écritures de registres sont quantifiées à un bloc (10 ms) ; la cadence du pilote son est l'affaire
  de X3 (O4).
- L'analyseur ne sert ni de référence ni de source : interpolation linéaire, pas d'ENVX, volumes
  négatifs et sweep ramenés à 0, pitch non écrêté, réverbération Studio C codée en dur, ADSR tiré de
  P.E.Op.S (GPL, `SoundBin.cs`, fonction `GetAdsrRate`) ; psyz est sous MPL-2.0 et sa table
  gaussienne est à l'échelle SNES.
- psx-spx ne déclare pas de licence : on implémente depuis ses formules sans copier son texte.

Budget : identique à S2 ; en plus, toute fonction dont la formule psx-spx est ambiguë est notée
dans « Points ouverts » au lieu d'être devinée.

### ⏳ T4.1 — Cœur SPU pur (voix, ADPCM, pitch, volumes)

- Fichiers : `CasaEngine/Framework/Audio/Psx/PsxSpu.cs` (+ types de voix et de registres),
  `CasaEngine.Tests/Audio/Psx/`.
- Étapes : SPU RAM fixe de 512 Ko (adresses masquées) ; écriture des données d'échantillons ;
  24 voix : adresse de départ, de répétition (LSAX), décodage ADPCM par bloc de 16 octets selon la
  formule psx-spx (décalage, filtre choisi par le bloc, historique conservé aux sauts), avec les
  coefficients de filtre **fournis par `PsxSpuHardwareTables`** (P17), drapeaux de fin et de
  répétition, ENDX ; pas de pitch écrêté à 4000h, pitch 0 = arrêt ; volumes gauche/droite fixes (y
  compris négatifs) et sweep ; key on/off ; rendu `Render(Span<short> stéréo, trames)` à
  44 100 Hz, sans allocation ; interpolation par un point d'extension (cubique par défaut,
  gaussienne si une table est fournie, O12).
- Validation : vecteurs synthétiques dont la valeur attendue est calculée dans le test depuis les
  formules psx-spx **avec des tables de coefficients synthétiques définies dans le test** (bloc
  ADPCM de chaque indice de filtre, boucle par drapeaux, pitch 1000h/2000h/4000h et au-delà,
  volumes négatifs, sweep linéaire et exponentiel) ; `PsxSpuHardwareTables` invalide (taille ou
  valeur hors borne) → exception à la construction ; `rg` sur `CasaEngine/` ne trouve aucune table
  matérielle ; zéro allocation ; suite complète.
- Commit : `feat(psx): add a software PlayStation SPU core`

### ⏳ T4.2 — ADSR, ENVX, bruit et PMON

- Fichiers : `Psx/PsxSpu.cs` et ses types, tests.
- Étapes : enveloppe ADSR depuis la formule psx-spx (attaque, décroissance, maintien, relâchement ;
  modes linéaire et exponentiel ; pas et décalage ; pas de table) ; ENVX lisible ; key off →
  relâchement ; générateur de bruit ; PMON (pitch modulé par la voix précédente).
- Validation : courbes d'enveloppe calculées pour plusieurs mots ADSR (dont les cas limites) et
  comparées échantillon par échantillon ; bruit : séquence déterministe attendue ; PMON sur un cas
  calculé ; suite complète.
- Commit : `feat(psx): add ADSR envelopes, noise and pitch modulation to the SPU`

### ⏳ T4.3 — Réverbération du SPU

- Fichiers : `Psx/PsxSpu.cs` (ou `PsxSpuReverb.cs`), tests.
- Étapes : zone de travail en SPU RAM de l'adresse ESA à 7FFFEh ; algorithme psx-spx à partir des
  registres (pas de préréglage codé en dur) ; filtre FIR d'entrée et de sortie avec ses coefficients
  **fournis par `PsxSpuHardwareTables`** (P17) ; envoi par voix ; volume de sortie gauche/droite.
- Validation : réponse impulsionnelle d'un jeu de registres de test et de coefficients FIR
  synthétiques, calculée dans le test depuis la formule ; zone de travail respectée (aucune
  écriture hors zone) ; suite complète.
- Commit : `feat(psx): add the SPU reverb unit`

### ⏳ T4.4 — Branchement au mixeur et accès public

- Fichiers : `Software/SoftwareMixer.cs` (source « tirée » à 44 100 Hz rééchantillonnée vers la
  sortie), `Backends/SoftwareAudioBackend.cs`, nouvelle capacité publique optionnelle
  `CasaEngine/Framework/Audio/Psx/IPsxSpuHost.cs` (P9), anneau dédié aux écritures de registres
  (thread de jeu → thread audio), tests.
- Étapes : le jeu obtient un SPU par la capacité (backends sans capacité → « SPU indisponible »,
  journal une fois) ; lecture d'ENDX/ENVX par un instantané publié à chaque bloc ; volume et bus du
  SPU comme une voix. **Transmission bornée, un seul producteur** : seul le thread de jeu écrit ;
  écritures de registres dans un anneau SPSC préalloué de 16 384 entrées (au moins 4 096 écritures
  par bloc de 10 ms acceptées sans perte) ; téléversements copiés dans un tampon d'étape SPSC
  préalloué de 1 Mo, découpés en morceaux de 64 Ko, appliqués à la SPU RAM au début des blocs
  suivants, au plus 256 Ko par bloc (une banque de 512 Ko passe en deux blocs). Anneau ou tampon
  plein → l'appel rend `false` sans attendre et incrémente un compteur d'écritures refusées ; jamais
  d'attente ni d'allocation sur le thread audio, jamais d'allocation sur le thread de jeu après la
  création. **Ordre conservé** : chaque téléversement place aussi dans l'anneau des registres un
  marqueur portant son numéro ; le thread audio applique l'anneau dans l'ordre et **s'arrête sur un
  marqueur** tant que ce téléversement n'est pas entièrement en SPU RAM, puis reprend : une
  écriture de registre n'est jamais appliquée avant un téléversement soumis avant elle (comme sur
  le matériel, où le transfert finit avant les écritures suivantes).
- Validation : test de bout en bout avec la sortie hors ligne (téléversement, key on, sortie non
  nulle, key off, relâchement) ; pression à la borne (4 096 écritures et 512 Ko téléversés entre
  deux blocs) → aucune perte, chaque appel sous 1 ms mesuré, zéro allocation sur le thread de rendu ;
  au-delà de la borne → `false` et compteur incrémenté, état du SPU cohérent ; **ordre** : un
  téléversement de 512 Ko (données différentes du contenu précédent de la SPU RAM) suivi d'un key on
  dans le même intervalle entre deux blocs → la voix ne démarre qu'une fois le téléversement
  entièrement appliqué (deux blocs, visible par l'instantané ENDX/ENVX) et ses premières trames
  rendues décodent les données téléversées ; stress de 60 s de la
  démo avec un SPU actif (tables synthétiques, ajout à la démo audio) : 0 sous-alimentation.
- Commit : `feat(psx): host the SPU in the software audio backend`

### ⏳ T4.5 — Documentation, ADR et vérification

- Fichiers : `docs/engine/` (nouvelle page `psx-spu.md`), `docs/decisions/0058-…md`, index, ce plan.
- Validation : vérificateur frais **CONFIRMED**.
- Commit : `docs(psx): document the software SPU module`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Si le stress de T1.6 coupe le son : options à soumettre à l'auteur (avance plus grande, minuterie haute résolution, noyau de mixage natif) ; aucune n'est choisie d'avance. | T1.6 |
| O2 | iOS lie OpenAL en statique (`__Internal`) : non constructible tant que le moteur n'est pas reciblé (D2). | après S1 |
| O3 | Retrait du backend MonoGame et de `MonoGameAudioClip` (rupture d'API publique) : décision de l'auteur, après la bascule. | après T1.9 |
| O4 | PSX : cadence du séquenceur (port à 50 Hz, rendus actuels des musiques à 60 Hz) et provenance des tables ADSR de l'analyseur (convention P.E.Op.S, licence à vérifier). | X1, X3 |
| O5 | L'éditeur ne suit pas le réglage `AudioBackend` du projet ouvert (son runtime est créé sans projet ; changer de backend à chaud n'est pas prévu en S1). À reprendre si l'auteur le souhaite. | après S1 |
| O6 | Avis A1 du vérificateur (P3, introduit, reporté) : si le thread audio meurt sur une exception, `SoftwareAudioBackend.IsAvailable` reste vrai ; une fois la file de commandes pleine, chaque appel attend jusqu'à 100 ms. Attendu : le backend devient muet et sans attente. Aucun déclencheur réaliste trouvé (pas d'erreur AL au débranchement). À traiter en S2 avec le changement de périphérique à chaud. | S2 |
| O7 | Avis A2 (P4) : même situation que O6 ; `Release`/`StopAll` rendent l'emplacement même si l'ordre `Stop` a été abandonné, une voix en boucle peut continuer jusqu'à la réutilisation de l'emplacement. | S2 |
| O8 | Avis A3 (P4) : si la file de 128 chunks d'une voix déborde (environ 2,7 s en file) et que le chunk perdu termine un buffer, `GetPendingBufferCount` ne redescend plus pour ce buffer (perte comptée dans `DroppedChunkCount`). | S2 |
| O9 | Avis A4 (P4, accepté par P4) : sous le backend MonoGame, le `SoundEffect` d'un `PcmAudioClip` est construit au premier `Play` (copie WAV sur le thread de jeu, à-coup possible pour un long clip) ; un clip stéréo y est gardé deux fois. Disparaît avec le retrait du backend MonoGame (O3). | O3 |
| O10 | Recette Alundra : le worktree part de `main`, alors que la pile `e19` (non mergée) ajoute des API moteur que le `Alundra.dll` actuel utilise probablement ; lancer Alundra sur le moteur de cette branche peut échouer pour des raisons étrangères à l'audio. Options : tester Alundra quand `e19` sera dans `main` (puis intégrer `main` ici), ou intégrer dès maintenant la pile `e19` dans cette branche pour la recette. **Tranché le 2026-10-05** : pile `e19` intégrée (`5d048882`, note de T1.8). Conséquence : cette branche contient désormais la pile `e19` ; son merge dans `main` suppose celui de la pile, ou se fait après elle. | T1.8 |
| O11 | **Question à l'auteur** — Ogg en streaming : NVorbis 0.10.4 alloue environ 41 Ko par lecture de 4 096 trames (≈ 444 Ko/s en 44,1 kHz stéréo), ce qu'AGENTS.md §9.3 interdit pour le streaming d'assets. Options : exception documentée par une ADR (le décodage tourne sur le travailleur de S2, hors des threads de jeu et audio), ou décodeur adapté/forké sans allocation, ou rester en Ogg résident. Aucun consommateur actuel (Alundra joue ses musiques en clips résidents). | S3 (en pause) |
| O12 | **Question à l'auteur** — tables de constantes matérielles du SPU : les 5 couples de coefficients des filtres ADPCM, les coefficients du filtre FIR de la réverbération et la table gaussienne de 512 entrées. psx-spx en est la seule source et ne déclare pas de licence (ce sont des données mesurées sur le matériel, mais la décision de les reprendre revient à l'auteur). En attendant, X1 les reçoit de l'appelant (`PsxSpuHardwareTables`, P17) et n'en livre aucune : le SPU est complet et testé, mais pas utilisable en jeu tant que la question n'est pas tranchée. | X1, X4 |
| O13 | **Question à l'auteur** — MP3, FLAC, Opus : nouvelles dépendances (NLayer 3.0.0 MIT ; Concentus 2.2.2 BSD-3 + Concentus.OggFile MIT pour Opus ; aucun décodeur FLAC géré maintenu trouvé). À décider avant toute intégration. | S3 (en pause) |
| O14 | **Information pour l'auteur** — le dépôt de l'analyseur (MIT) contient du code dérivé de P.E.Op.S, sous GPL (table ADSR de `SoundBin.cs`, fonction `GetAdsrRate`) : problème de licence existant, hors de ce chantier ; X1 n'en reprend rien. | hors chantier |
| O15 | Adoption par Alundra des régions de boucle (T2.4) et des voix stéréo exactes (T2.3) : travail du dépôt parent (passer `LoopStart`/`LoopEnd` de chaque ton à `PlayClipStereo`), avec son propre plan ; non fait en mode AUTO. | après S2 |

## Hors périmètre

- Le reciblage multiplateforme du moteur (D2) ; toute validation hors Windows x64.
- L'exécution des tranches S2 à S6 et X1 à X5 : chacune aura son détail et son approbation.
- SoundFlow, FMOD, Wwise et tout middleware ; la spatialisation HRTF.
- Les sorties XAudio2/FAudio (WindowsDX, plateforme Native de MonoGame, consoles).
- **Pour la tranche S1** : toute modification de `IAudioBackend`, de l'API publique
  d'`AudioService` (seuls des types nouveaux s'ajoutent) ou du dépôt parent. Les tranches suivantes
  suivent la règle de l'« Enveloppe du programme ».
