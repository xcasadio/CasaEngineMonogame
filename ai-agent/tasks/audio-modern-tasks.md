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

### ⏳ T1.4 — Backend logiciel et suite de conformité

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

### ⏳ T1.5 — Choix du backend par réglage de projet

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

### ⏳ T1.6 — Démo : statistiques et harnais de stress

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

### ⏳ T1.7 — Documentation

- Objectif : documenter l'architecture et le réglage.
- Fichiers : `docs/engine/audio-system.md`.
- Étapes : section sur le mixeur logiciel et la sortie OpenAL, le réglage `AudioBackend`, la
  sémantique (P5), l'avance (P6), les limites connues mises à jour (ADPCM en S3, pas de limiteur).
- Validation : relecture ; liens vers ADR-0055.
- Commit : `docs(audio): document the software mixer and the backend setting`

### ⏳ T1.8 — Vérification de la tranche et recette de l'auteur

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

### ⏳ T1.9 — Bascule du défaut

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

## Hors périmètre

- Le reciblage multiplateforme du moteur (D2) ; toute validation hors Windows x64.
- L'exécution des tranches S2 à S6 et X1 à X5 : chacune aura son détail et son approbation.
- SoundFlow, FMOD, Wwise et tout middleware ; la spatialisation HRTF.
- Les sorties XAudio2/FAudio (WindowsDX, plateforme Native de MonoGame, consoles).
- **Pour la tranche S1** : toute modification de `IAudioBackend`, de l'API publique
  d'`AudioService` (seuls des types nouveaux s'ajoutent) ou du dépôt parent. Les tranches suivantes
  suivent la règle de l'« Enveloppe du programme ».
