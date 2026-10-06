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
| D5 | (O23-1, 2026-10-06) Les variations aléatoires s'écrivent **dans le `.sound`** (liste de fichiers, plages de volume et de pitch), en champs additifs ; la règle des surcharges de l'émetteur et des cinématiques devra composer avec le tirage au lieu de l'écraser (O17). **Appliquée en S5a (ADR-0063).** |
| D6 | (O23-2) **Le refus actuel reste le comportement par défaut** quand les voix sont toutes prises ; une voix n'est volée que pour un son de priorité explicitement plus haute (la plus basse, puis la plus ancienne) ; une voix sans priorité n'est jamais volée ; les voix streamées sont toujours protégées. **Appliquée en S5a (ADR-0063).** |
| D7 | (O23-2c) **Pas de voix virtuelles** pour l'instant. **Respectée en S5a.** |
| D8 | (O23-3a) Le point d'écoute vient d'un **composant** (`AudioListenerComponent`) qui pousse sa pose dans `AudioService`. |
| D9 | (O23-3b) **Un mode spatial par asset : aucun, 2D ou 3D**, « aucun » par défaut (les assets existants ne changent pas). |
| D10 | (O23-3c) Courbes d'atténuation : **les modèles de distance de la spécification OpenAL 1.1**, source publique citée, aucun code repris. |
| D11 | (O23-4) **Doppler désactivé par défaut**, activé sur demande. |
| D12 | (O23-5) Paramètres de jeu : **volume et pitch d'abord** ; un filtre par voix (nouvel étage du mixeur) viendra plus tard. |
| D13 | (O23-6) **`SoundEmitterComponent` devient un composant de scène** ; l'auteur confirme qu'aucun projet hors de ce dépôt ne contient d'émetteur sauvegardé (chargement tolérant tout de même, AGENTS.md §9.7). |
| D14 | (O23-7) **Pas de délai ni de séquence** dans un premier temps. **Respectée en S5a.** |
| D15 | (O24-1) **Un seul asset de mixeur par projet**, désigné par un réglage de projet facultatif (vide = mixeur par défaut), extension `.audioMixer`. |
| D16 | (O24-2) Le panneau de mixage **édite l'asset** (une seule source de vérité) et applique au mixeur vivant, **toujours dans le sens asset → mixeur**, jamais l'inverse. |
| D17 | (O24-3) **Solo fait dans le panneau** avec les muets existants (moteur inchangé) ; il garde audibles les bus de retour (réverbération) et le bus Editor. |
| D18 | (O24-4) Formes d'onde : **le niveau de la sortie dans le temps** (depuis la mesure existante) **et le dessin d'un fichier son dans l'inspecteur** ; pas d'oscilloscope. |
| D19 | (O24-5) **Le bus Master reste hors de l'asset de mixeur** (muet au projet, ADR-0040 ; volume écrit par Alundra). |
| D20 | (O24-6) L'allocation de `MGSlider` à chaque changement est **à corriger dans une session et un worktree séparés** (tâche proposée le 2026-10-06), pas dans ce chantier. |
| D21 | (O24-7) L'inspecteur de son propose **les bus de l'asset de mixeur** (liste par défaut sans asset). |
| D22 | (O16) **On garde les musiques en WAV** : pas de séquenceur SEQ/VAB pour l'instant (aucune décompilation de libsnd reprise). |
| D23 | (O4) Si un séquenceur est écrit un jour : cadence **réglable par jeu** (valeur d'Alundra à mesurer sur l'original) et pilote **sur le thread audio**. |
| D24 | (X5) **Oui aux pistes XA** : par décodage hors ligne en fichiers à l'extraction (travail du dépôt parent), lus par le lecteur de musique existant. |
| D25 | (O12) Le moteur **livre les 10 coefficients des filtres ADPCM** (psx-spx, risque de licence accepté par l'auteur) **et calcule une FIR de réverbération approximative par formule** (aucune valeur matérielle) ; la table gaussienne reste à l'appelant. Remplace en partie P17 (ADR-0061). |
| D26 | (O19) Les lectures ambiguës se jugent **par une écoute comparée à un enregistrement de la console** dès que des tables réelles sont disponibles. |
| D27 | (T2.6) **Pas de reprise automatique après un débranchement** : statu quo documenté. |
| D28 | (O11) **L'Ogg reste résident** (pas de streaming Ogg). |
| D29 | (O13) **Pas de MP3, FLAC ni Opus** : conversion en WAV ou Ogg. |
| D30 | (O25) Le petit saut de gain au reciblage d'un fondu de bus très court **reste tel quel et se documente**. |
| D31 | (X2/X4, 2026-10-06) **La musique d'Alundra reste en WAV.** Faire passer les bruitages par la puce n'est pas demandé : X2 et X4 ne sont pas lancées. |
| D32 | (O22-3) **Le limiteur du Master reste actif par défaut, avec un réglage de projet additif pour le couper** (par exemple pour Alundra). Les autres choix de O22 seront revus par l'auteur après écoute. |
| D33 | Les tranches débloquées s'exécutent **en mode AUTO** (accord de l'auteur du 2026-10-06), dans les limites déjà annoncées : pas de push ni de merge vers GitHub, rien dans le dépôt parent, pas de nouvelle dépendance, pas de rupture d'API, chaque tranche détaillée et relue avant exécution puis vérifiée. |

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
| S4 — Bus et effets | Vrais bus de submix avec effets insérés (EQ et filtres biquad, compresseur, limiteur du master), départs auxiliaires (reverb), ducking, snapshots, rampes à durée explicite. *(Ajusté en vague 3 : la configuration sérialisée passe en S6, P19.)* | S2 | moteur |
| S5 — Couche jeu | Priorités et vol de voix, voix virtuelles, conteneurs (aléatoire, séquence, pitch, volume et délai aléatoires), atténuation 2D/3D, écouteur, Doppler, paramètres de jeu. | S4 | moteur |
| S6 — Éditeur et outils | Panneau de mixage, vu-mètres, formes d'onde, profileur audio ; configuration sérialisée du mixeur (nouveau type d'asset, chargement, édition, ADR ; reçue de S4 en vague 3, P19). | S4 | moteur |
| X1 — Module SPU PSX | ADPCM VAG, 24 voix, pas SPU, interpolation gaussienne, ADSR (d'après psx-spx), boucles, gains gauche/droite exacts, reverb SPU, comme groupe de voix du mixeur. Base : le mixeur SPU de l'analyseur (MIT, même auteur), à nettoyer. | S1 (S2 conseillé) | moteur |
| X2 — Données PSX | Export VAB/VAG/SEQ par le convertisseur et types d'assets moteur correspondants (ADR, sérialisation). | X1 | parent + moteur |
| X3 — Séquenceur SEQ/SEP | Musiques en temps réel et bruitages déclenchés par séquence. *(En pause : O16, O4.)* | X1, X2 | moteur |
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

### ✅ T2.2 — Comptes de streaming fiables et arrêts garantis (O7, O8)

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
- Note de validation (2026-10-05) : exécuté par un sous-agent `executor`, diff relu. Le mixeur
  publie par emplacement `(génération << 32) | consommés` par une écriture `Volatile` unique de
  64 bits (cible x64), remise à zéro à la création ; le backend calcule soumis − consommés, en
  ignorant une autre génération. Chunk abandonné qui termine un buffer compté comme consommé ;
  évènements `BufferConsumed` toujours émis, mais le compte n'en dépend plus. Arrêt perdu : le
  handle devient périmé, l'emplacement reste « arrêt en attente » hors de la liste libre et l'ordre
  est renvoyé, sans attente, à l'appel suivant ; sortie morte → libération immédiate.
  `MusicPlayer.FillQueue` borné à `QueuedBufferTarget` par appel. Tests (+7) : anneau
  d'évènements saturé, débordement de 128 chunks, génération ancienne ignorée, réutilisation
  d'emplacement (3 soumis sans rendu → 3 en attente), `MusicPlayer.Play` en boucle sur un
  emplacement réutilisé (3 en attente, borne montrée par la valeur), arrêt perdu puis renvoyé
  (voix muette, emplacement rendu). `MusicPlayerTests.cs` inchangé. `CasaEngine.Tests` 2805/2805.
  Non testé : la libération des arrêts en attente quand la sortie meurt (`FreePendingStopsAfterDeath`).
  O7 et O8 corrigés.

### ✅ T2.3 — Voix stéréo au thread audio (P10)

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
- Note de validation (2026-10-05) : exécuté par un sous-agent `executor`, diff relu. Capacité
  publique `IStereoVoiceBackend` (`PlayStereo`, `SetStereoGains`), implémentée par
  `SoftwareAudioBackend` ; mode « gains explicites » d'une voix résidente mono dans le mixeur ;
  `AudioService.PlayClipStereo` l'utilise, sinon `StereoVoiceMixer`. Correction en session
  principale : la capacité ne sert que pour un `PcmAudioClip` ; un autre clip qui expose ses
  échantillons garde le chemin de streaming d'ADR-0039 au lieu de lever une exception (régression
  évitée, test ajouté). ADR-0056 écrite (décisions P9 à P14), ADR-0039 marquée remplacée en partie,
  index mis à jour. Tests (+18) : gains exacts à 1e-6, changement visible au bloc suivant (rampe à
  1e-4 près à la dernière trame, cumul flottant), tons à 3 370 et 172 610 Hz sans refus et à la
  bonne durée, recyclage en fin de voix, fondu et bus, propriétaire, pause, handle périmé, clip
  stéréo refusé, zéro allocation, repli des clips non PCM. `AudioServiceStereoVoiceTests` inchangé.
  `CasaEngine.Tests` 2823/2823.

### ✅ T2.4 — Région de boucle et multiplicateur de vitesse (P11, P12)

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
- Note de validation (2026-10-05) : exécuté par un sous-agent `executor`, diff relu. API ajoutée
  à `AudioVoiceParameters` : `MaxRateMultiplier` (16), `HasLoopRegion`, `LoopStartFrame`,
  `LoopEndFrame`, `RateMultiplier`, `WithLoopRegion`, `WithoutLoopRegion`, `WithRateMultiplier` ;
  constructeur à 4 arguments, bornes de pitch et `Default` inchangés ; chaque `With*` copie tous les
  champs ; égalité et hachage incluent les nouveaux champs. Correction en session principale :
  `ToString()` reste identique à avant tant que les nouvelles options ne sont pas utilisées (test
  ajouté). Mixeur : pas = débit × 2^pitch × multiplicateur / sortie ; boucle sur la région avec les
  voisins d'interpolation bouclés dans la région (l'intro avant la région est jouée) ; région
  invalide → clip entier et avertissement limité ; voix de streaming : multiplicateur seulement.
  Backend MonoGame : région ignorée (documenté), multiplicateur replié sur le pitch borné. Tests
  (+30) : paramètres, région 1 000–2 000 exacte à l'échantillon, jointure continue comparée à une
  référence Hermite, multiplicateur 4, de bout en bout par `AudioService` (faux backend et rendu du
  backend logiciel). `CasaEngine.Tests` 2853/2853.

### ✅ T2.5 — Lecture des musiques hors du thread de jeu (P13)

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
- Note de validation (2026-10-05) : exécuté par un sous-agent `executor`, conception relue.
  `StreamingWorker` interne : un thread d'arrière-plan « CasaEngine Audio Streaming » par
  `MusicPlayer` (donc un par service audio), créé au premier `Play` en mode travailleur ; par piste
  un anneau de 6 emplacements de 16 384 octets (≈ 98 Ko), compteurs publiés par `Volatile`, sommeil
  sur un évènement (garde de 250 ms), aucune allocation en régime établi. `Play` reste synchrone
  (ouverture, en-tête, premier remplissage), puis le lecteur appartient au seul travailleur ;
  `Stop`/fin de fondu : état « libération », le travailleur ferme le lecteur ; `Dispose` joint le
  thread (2 s). Choix du mode : `AudioService.cs:32` (backend logiciel ou MonoGame → travailleur,
  sinon en ligne). Changement observable mineur : en mode travailleur, `GetPosition` donne la
  position du dernier buffer transmis à la voix (au lieu de la position de lecture du fichier),
  plus proche de ce qu'on entend ; le mode en ligne est inchangé. `MusicPlayerTests.cs` : diff vide,
  vert. Tests (+7) : aucune lecture sur le thread de jeu après `Play`, boucle au-delà de deux
  longueurs, fin de piste, erreur de lecture sans exception côté jeu, `Stop` et réutilisation,
  `Dispose` avec pistes en cours, zéro allocation de l'`Update` ; stables sur 5 passages
  supplémentaires (attentes bornées à 2 s). Stress de 60 s (musique streamée, défaut logiciel) :
  `underruns=0 gc=119`. `CasaEngine.Tests` 2860/2860.

### ⚠️ T2.6 — Débranchement et changement de périphérique (séparée de S2, en pause)

**Abandonnée sur décision de l'auteur le 2026-10-06 (D27)** : le son reste perdu jusqu'au relancement du jeu après un débranchement, limite documentée.

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

### ✅ T2.7 — Documentation, ADR et vérification de la tranche

- Objectif : documenter S2 et prouver la tranche.
- Fichiers : `docs/engine/audio-system.md`, `docs/decisions/0056-…md`, index des ADR, ce plan.
- Étapes : doc (voix stéréo, boucles, vitesse, travailleur, limites du repli
  MonoGame) ; vérificateur frais sur S2 ; stress de 60 s sur le build final.
- Validation : verdict **CONFIRMED** ; stress 0 sous-alimentation.
- Commit : `docs(audio): document real-time stereo voices, loops and streaming`
- Note de validation (2026-10-05) : doc (`audio-system.md` §5, §5 bis, limites) écrite en session
  principale (`b459742f`) ; ADR-0056 écrite en T2.3. Vérificateur frais dans le worktree de
  vérification figé sur `b459742f`, S2 réduite (T2.6 hors périmètre) : **CONFIRMED**, aucun constat
  P0–P3. Il a rejoué build et tests (2860/2860), le stress de 60 s sur le défaut logiciel
  (`underruns=0 gc=119`, `monogame-openal-initialized=false`), vérifié que `MusicPlayerTests` et
  `AudioServiceStereoVoiceTests` sont inchangés depuis avant S2, relu la concurrence (publication
  64 bits, propriété du lecteur par le travailleur, croissance du tableau des canaux), l'additivité
  des API publiques et l'absence d'allocation. Deux avis P4 reportés : O17 et O18. **Tranche S2
  terminée** (hors T2.6, en pause).

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

### ✅ T3.3 — Documentation, ADR et vérification

- Fichiers : `docs/engine/audio-system.md`, `docs/decisions/0057-…md` (P15, P16), index, ce plan.
- Validation : vérificateur frais **CONFIRMED**.
- Commit : `docs(audio): document ogg and adpcm support`
- Note de validation (2026-10-05) : doc `audio-system.md` (formats lus, limites O11 et O13) et
  ADR-0057 écrites en session principale (`3b37c9e7`). Vérificateur frais, dans un worktree de
  vérification figé sur `3b37c9e7` : **CONFIRMED**, aucun P0–P2. Il a rejoué build et tests
  (2796/2796), passé 84 fichiers ADPCM supplémentaires de `ffmpeg` (blocs MS de 32 à 8 192
  octets, IMA en puissances de deux, bruit blanc pleine échelle, sinus, carré) : 84/84 identiques à
  l'échantillon ; montré que la forme fermée IMA égale `ffmpeg` (42/42) et la forme du document IMA
  non (0/42) ; 8 000 ADPCM et 775 Ogg corrompus → uniquement `InvalidDataException` ou
  `NotSupportedException`, aucun blocage ; NVorbis à 1 LSB de `ffmpeg` ; fixtures ADPCM regénérées
  à l'octet près depuis les commandes du README. Avis P3 traité : deux liens Microsoft Learn morts
  restaient dans des commentaires de `WavDecoder.cs` alors que la note de T3.2 les disait retirés ;
  supprimés (`rg learn.microsoft.com` sur `Decoding/` ne trouve plus rien). Reste 🧪 T3.1 (écoute
  d'un `.ogg` dans l'inspecteur de l'éditeur). **Tranche S3 terminée.**

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

### ✅ T4.1 — Cœur SPU pur (voix, ADPCM, pitch, volumes)

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
- Note de validation (2026-10-06) : `PsxSpu`, `PsxSpuHardwareTables` (validées et copiées à la
  construction ; coefficient ADPCM hors de −128..127 → `ArgumentException`, borne choisie par le
  moteur), `IPsxSpuInterpolator` (cubique par défaut, gaussienne si la table est fournie) ; 38 tests
  dans `CasaEngine.Tests/Audio/Psx/PsxSpuTests.cs` (chaque filtre 0 à 4, décalages, historique à
  travers les blocs et les sauts, boucles et ENDX, fin muette, pitch 1000h/2000h/4000h et écrêtage,
  pitch 0, gaussienne, volumes négatifs, sweep dans dix combinaisons de modes, saturation, RAM
  circulaire, tables invalides, zéro allocation sur 1 000 rendus de 441 trames à 24 voix) ; `rg` de
  suites de littéraux hexadécimaux dans `Psx/` → rien ; les deux solutions : 0 erreur ; suite
  complète 2898/2898. ENVX et key off restent des substituts jusqu'à T4.2. Lectures ambiguës de
  psx-spx : O19.

### ✅ T4.2 — ADSR, ENVX, bruit et PMON

- Fichiers : `Psx/PsxSpu.cs` et ses types, tests.
- Étapes : enveloppe ADSR depuis la formule psx-spx (attaque, décroissance, maintien, relâchement ;
  modes linéaire et exponentiel ; pas et décalage ; pas de table) ; ENVX lisible ; key off →
  relâchement ; générateur de bruit ; PMON (pitch modulé par la voix précédente).
- Validation : courbes d'enveloppe calculées pour plusieurs mots ADSR (dont les cas limites) et
  comparées échantillon par échantillon ; bruit : séquence déterministe attendue ; PMON sur un cas
  calculé ; suite complète.
- Commit : `feat(psx): add ADSR envelopes, noise and pitch modulation to the SPU`
- Note de validation (2026-10-06) : membres additifs `SetAdsr`, `SetNoiseMode`,
  `SetPitchModulation` (bit 0 ignoré), `SetNoiseClock`, `GetNoiseLevel` ; le sweep et l'ADSR
  partagent un même pas d'enveloppe (tests du sweep inchangés et verts) ; décroissance exponentielle
  à pas −8, relâchement linéaire ou exponentiel selon le bit 21 (vérifié sur psx-spx, ainsi que la
  disposition des registres ADSR, le pseudo-code du bruit et les bits 13-8 de SPUCNT) ; key on part
  de 0, key off passe au relâchement, fin muette → relâchement et ENVX 0. Les tests de T4.1 donnent
  à leurs voix un mot ADSR (attaque linéaire la plus rapide puis maintien qui ne bouge jamais :
  7FFFh à la 3e trame, égal au retard d'interpolation) ; le test de key off devient un test de
  relâchement. 21 tests nouveaux (`PsxSpuEnvelopeTests.cs`) : dix mots ADSR comparés trame par trame
  à un modèle écrit dans le test (ENVX et sortie), key on, key off en attaque, fin muette, bruit
  sous quatre horloges avec l'ADPCM qui continue, PMON (facteur négatif, bit 0 ignoré), zéro
  allocation. Les deux solutions : 0 erreur ; suite complète 2919/2919. Lectures ambiguës : O19 ;
  test d'allocation instable vu une fois : O20.

### ✅ T4.3 — Réverbération du SPU

- Fichiers : `Psx/PsxSpu.cs` (ou `PsxSpuReverb.cs`), tests.
- Étapes : zone de travail en SPU RAM de l'adresse ESA à 7FFFEh ; algorithme psx-spx à partir des
  registres (pas de préréglage codé en dur) ; filtre FIR d'entrée et de sortie avec ses coefficients
  **fournis par `PsxSpuHardwareTables`** (P17) ; envoi par voix ; volume de sortie gauche/droite.
- Validation : réponse impulsionnelle d'un jeu de registres de test et de coefficients FIR
  synthétiques, calculée dans le test depuis la formule ; zone de travail respectée (aucune
  écriture hors zone) ; suite complète.
- Commit : `feat(psx): add the SPU reverb unit`
- Note de validation (2026-10-06) : `PsxSpuReverb` (interne) ; membres additifs `SetReverbVoices`
  (EON), `SetReverbEnabled` (SPUCNT bit 7), `SetReverbWorkAreaStart` (ESA, fixe aussi l'adresse
  courante), `SetReverbOutputVolume`, `SetReverbRegister` (index = (adresse − 1F801DC0h)/2, table
  de correspondance dans la doc XML) ; accès interne `ReadRam` pour les tests. Ordre du rendu :
  voix sèches, sortie de la réverbération, une saturation. Sans coefficients FIR : réverbération
  court-circuitée (ni sortie, ni écriture). **Écart avec la consigne, tranché pour psx-spx** :
  enable maître coupé → plus aucune écriture, mais les lectures et la sortie continuent
  (« Reverb Disable ») ; la sortie n'est sèche que si la zone de travail est vide. Relu en session
  principale : formule, ordre des registres et croisement DIFF vérifiés ; un jeu de registres de
  test ne produisait rien à droite (offsets qui se recouvrent), renvoyé et corrigé : chaque côté a
  désormais plus de 100 échantillons de réverbération vérifiés contre le modèle. 9 tests
  (`PsxSpuReverbTests.cs`) : réponse impulsionnelle échantillon par échantillon pour deux jeux de
  registres, volumes négatifs, zone de travail respectée sur une dizaine de tours, quatre cas de
  court-circuit, lecture conservée enable coupé, zéro allocation. Corrigé au passage : quatre
  annotations `?` de T4.1 qui produisaient CS8632 (le projet n'active pas le contexte nullable) ;
  0 avertissement dans `Psx/`. Les deux solutions : 0 erreur ; suite complète 2928/2928. Lectures
  ambiguës : O19 (20 à 29), dont le gain de sortie, à confirmer à l'écoute.

### ✅ T4.4 — Branchement au mixeur et accès public

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
- Note de validation (2026-10-06) : API additive : capacité `IPsxSpuHost.TryCreatePsxSpu(tables,
  out PsxSpuPort)` (seul `SoftwareAudioBackend` l'implémente ; `IAudioBackend` inchangé) ;
  `AudioService.TryCreatePsxSpu(tables, busName, out port)` (sans la capacité : `false` et un
  journal « SPU unavailable » limité) ; `PsxSpuPort` (thread de jeu) : un `Try*` par setter de
  `PsxSpu`, `TryUpload`, `ReadEndx`, `GetEnvx`, `SetGain`, `RefusedWriteCount`, `Dispose`. Un
  seul SPU vivant par backend. Interne : `PsxSpuSource` (anneau de 16 384 écritures, tampon d'étape
  de 1 Mo en morceaux de 64 Ko, au plus 256 Ko appliqués par bloc, marqueur numéroté par
  téléversement qui arrête l'anneau jusqu'à la fin du téléversement ; instantané ENDX/ENVX publié à
  chaque bloc sous compteur de séquence, sans verrou côté audio ; gain en dernière valeur, rampé
  sur le bloc ; rééchantillonnage cubique 44 100 Hz → sortie, ajouté avant l'écrêtage final) ;
  commandes `AttachPsxSpu`/`DetachPsxSpu` dans l'anneau du mixeur, le détachement réessayé comme
  les arrêts ; `StopAll` ne touche pas le SPU. Volume « comme une voix » : gain × gain effectif du
  bus, réappliqué quand les bus changent (vrai routage en T5.1). Relu en session principale ; corrigé
  dans la démo : le bloc ADPCM synthétique ne donnait qu'un niveau continu, remplacé par une onde
  carrée. 10 tests (`SoftwareAudioBackendPsxSpuTests.cs`, `AudioServicePsxSpuLoggingTests.cs`) :
  bout en bout, borne (4 096 écritures et 512 Ko entre deux blocs, chaque appel < 1 ms, zéro
  allocation au rendu), au-delà de la borne (16 384 acceptées, 3 616 refusées, préfixe seul
  appliqué), ordre (key on après un téléversement de 512 Ko : rien au bloc 1, données téléversées
  décodées au bloc 2), gains et gain de bus, deuxième création refusée, détachement malgré un
  anneau plein, lecture concurrente de l'instantané, absence de capacité. Les deux solutions :
  0 erreur ; suite complète 2938/2938 (7 passages). **Stress de 60 s, sans clavier** :
  `Audio backend: SoftwareAudioBackend (source: environment)`,
  `monogame-openal-initialized=false`, `Audio demo: SPU started, 4 voices, all writes
  accepted=True`, `Audio stress done: backend=SoftwareAudioBackend seconds=60 underruns=0 gc=119`,
  `SPU stopped, refused writes=0`, sortie code 0. Écoute de la démo (touche G) : 🧪 pour l'auteur,
  avec les autres écoutes.

### ✅ T4.5 — Documentation, ADR et vérification

- Fichiers : `docs/engine/` (nouvelle page `psx-spu.md`), `docs/decisions/0058-…md`, index, ce plan.
- Validation : vérificateur frais **CONFIRMED**.
- Commit : `docs(psx): document the software SPU module`
- Note de validation (2026-10-06) : `docs/engine/psx-spu.md` (anglais, comme les pages récentes),
  ADR-0058 (numéro libre sur toutes les branches), index `docs/README.md` et
  `docs/decisions/README.md`, renvoi depuis `audio-system.md` (commit `497c3d5e`). Vérificateur
  frais sur `497c3d5e` (worktree `audio-verify`) : **CONFIRMED**, aucun P0–P2. Il a relu les
  formules contre psx-spx téléchargé (ADPCM, boucles, pitch et PMON, volumes et sweep, ADSR, bruit,
  gaussienne, réverbération et ordre des registres), vérifié l'absence de table matérielle dans
  `CasaEngine/`, les chemins chauds, le contrat d'hébergement de T4.4 test par test, l'API
  additive (`IAudioBackend` inchangé), les deux builds (0 erreur, `--no-incremental`), la suite
  (2938/2938, trois passages) et refait le stress de 60 s avec le SPU actif (`underruns=0`,
  `gc=119`). Quatre avis P4, non bloquants, reportés en O21.

---

## Vague 3 — tranche S4 (détail du 2026-10-05) et pause de X3

Découverte en lecture seule de S4 et X3 (deux enquêtes, chacune passée par un vérificateur
contradictoire), sur l'instantané `3b37c9e7`.

**X3 en pause (O16, O4).** Un séquenceur SEQ/VAB « à la libsnd » n'a pas de spécification publique
au-delà des en-têtes (psx-spx) : le comportement de lecture (événements, NRPN, boucles, choix des
tons, hauteur, allocation des voix) n'existe que dans des décompilations du libsnd propriétaire de
Sony (transcription de l'analyseur depuis `ALUN_CD.EXE`, `psyz/decomp`, `sotn-decomp`). Le choix de
la source et sa licence reviennent à l'auteur (O16). Le séquenceur de l'analyseur ne se transpose
pas tel quel non plus (verrou partagé, allocations à chaque note, globals du jeu). Rien de X3 n'est
fait en mode AUTO.

### Enveloppe ajustée (vague 3)

- **S4** perd la « configuration sérialisée (ADR) » (P19, à confirmer par l'auteur) ; elle garde
  bus, effets insérés, départs et réverbération, ducking, snapshots, et reçoit les rampes à durée
  explicite (P14, P21).
- **S6** gagne la **configuration sérialisée du mixeur** (nouveau type d'asset, son chargement, son
  édition et son ADR), en plus de ses outils (panneau de mixage, vu-mètres, formes d'onde,
  profileur).
- **X3** est en pause (O16, O4).

### Décisions de la vague 3 (arbitrages de l'agent, à confirmer par l'auteur)

| Réf | Arbitrage |
|---|---|
| P18 | **P9 étendue à S4** : bus réels, effets, départs, ducking et rampes passent par une capacité optionnelle `IAudioBusBackend` implémentée par `SoftwareAudioBackend` ; `IAudioBackend` et le dépôt parent ne changent pas. Sans la capacité (backend MonoGame, faux backends), `AudioService` garde exactement le repli actuel : gain de bus multiplié dans le volume de chaque voix, fondus par frame ; effets, départs et ducking y sont absents (une ligne de journal, une fois). |
| P19 | **Configuration sérialisée du mixeur déplacée en S6** (outils de l'éditeur) : S4 livre l'API d'exécution (code) ; un nouveau type d'asset et son édition relèvent de l'éditeur. |
| P20 | **Algorithmes depuis des sources libres, citées** : filtres biquad d'après l'« Audio EQ Cookbook » (note W3C de 2021, licence permissive W3C ; forme directe I ou transposée II, coefficients calculés depuis les formules), compresseur à action directe d'après Giannoulis, Massberg et Reiss (JAES, 2012), réverbération Freeverb (domaine public, « Jezar at Dreampoint », décrite sur la page CCRMA de J. O. Smith), longueurs de retard mises à l'échelle du débit de sortie ; garde contre les nombres dénormaux. Aucun code GPL, LGPL ou MPL. |
| P21 | **Rampes « à l'échantillon »** : une rampe à durée explicite (voix, bus) est interpolée échantillon par échantillon ; son départ est arrondi au début du bloc suivant (≤ 10 ms), faute d'horodatage des commandes (documenté). Les fondus de voix et de musique d'`AudioService` l'utilisent sous la capacité. **Le contrat public des fondus ne change pas** : le thread de jeu garde sa propre chronologie de chaque rampe (départ, cible, durée, temps écoulé en `Update`), qui répond à `GetVoiceVolume`, `MusicPlayer.GetVolume`, `IsFading`, décide la libération en fin de `StopWithFade` et sert de point de départ aux fondus enchaînés ; `CancelFade` envoie une commande « figer à la valeur courante » et le thread de jeu garde la valeur atteinte selon sa chronologie. L'écart entre cette chronologie et le rendu est d'au plus un bloc. |

---

## Phase 5 — Tranche S4 : bus et effets (exécution)

Résultat attendu : sous le backend logiciel, chaque voix est routée vers son bus ; les bus forment
un vrai graphe mixé au thread audio (enfants puis parents, puis Master) ; chaque bus a un gain
lissé, jusqu'à 4 effets insérés (filtres biquad, compresseur), des départs vers des bus de retour
(réverbération) ; le Master a un limiteur ; un bus peut en atténuer un autre (ducking) ; l'état des
bus se capture et se rétablit en fondu (snapshots) ; les fondus sont des rampes à durée explicite.
Non-objectifs : configuration sérialisée et édition (P19, S6) ; `IAudioBackend` et dépôt parent
inchangés. Prérequis : S2 (fichiers du mixeur et de `AudioService` modifiés par T2.2–T2.5) et X1
T4.4 (la source SPU doit être routée vers un bus comme une voix). Retour arrière : revert, ou
`CASAENGINE_AUDIO_BACKEND=MonoGame`.

État vérifié (2026-10-05, instantané `3b37c9e7`) :
- Le gain de bus est multiplié dans le volume de chaque voix à chaque changement
  (`AudioService.cs:73-74` et `AudioMixer.InvalidateGains`) ; aucune voix du mixeur ne porte de bus
  (`MixerVoice.cs`).
- `AudioMixer.CreateBus` est public et appelable à tout moment ; le mixeur par défaut est créé à la
  construction d'`AudioService` (`AudioService.cs:31`) ; l'ordre inverse de création est un ordre
  « enfants avant parents » (`AudioMixer.cs:31-70`).
- `AudioBus` est une classe du thread de jeu : le thread audio n'y lit rien ; tout état de bus, de
  rampe et d'effet du côté audio doit vivre dans le mixeur, préalloué, alimenté par commandes
  (`SoftwareMixer.cs:12-15`).
- `Render` accepte n'importe quel nombre de trames et écrit les voix directement dans la sortie,
  puis écrête (`SoftwareMixer.cs:340-396`) : des tampons par bus exigent une taille de bloc maximale.
- Le bus Editor est tenu hors du mix du jeu et le muet du Master vient des réglages du projet
  (`AudioBusNames.cs:24-28`, `ProjectAudioSettings.cs`, `EditorProjectAudioMuteSync.cs`).
- Alundra pilote ses fondus de musique en écrivant le volume du Master à chaque tick, et ses tests
  supposent le gain replié sur le faux backend du dépôt parent
  (`Alundra/Scripts/AlundraBgmFadeDirector.cs:92-96`, `:256-264`) : le repli doit rester identique.
- `SetVolume` est abandonné sans attente quand l'anneau est plein (T1.4) : les gains de bus et les
  paramètres d'effets doivent être publiés en « dernière valeur » (non perdables).

Budget : identique à S2.

### ✅ T5.1 — Graphe de bus au thread audio et capacité `IAudioBusBackend`

- Objectif : routage des voix vers leur bus, mix hiérarchique, gains lissés, sans changer le repli.
- Fichiers : `CasaEngine/Framework/Audio/IAudioBusBackend.cs` (capacité publique), `Software/`
  (nouveau `MixerBus` préalloué, capacité fixe 32 bus, commandes de création et de routage),
  `SoftwareMixer.cs`, `MixerVoice.cs`, `MixerMessages.cs`, `Backends/SoftwareAudioBackend.cs`,
  `AudioService.cs`, `Mixing/AudioMixer.cs` et `AudioBus.cs` (notification interne de création et de
  gain), tests `Software/` et `SoftwareAudioBackendTests.cs`.
- Étapes : taille de bloc maximale fixée à la construction du mixeur (`BufferFrames` de la sortie) ;
  `Render` découpe une demande plus grande ; tampon par bus ; chaque voix (résidente, streaming,
  stéréo de T2.3, source SPU de T4.4) porte un indice de bus ; ordre de mix enfants puis parents ;
  gain de bus publié en dernière valeur par bus (tableau `Volatile`, pas de file), lissé par bloc ;
  au-delà de 32 bus → bus rattaché à Master côté audio, avertissement une fois ; sous la capacité,
  `AudioService` cesse de multiplier le gain de bus dans le volume des voix (sinon il serait appliqué
  deux fois) ; muet et bus Editor conservés.
- Validation : tests : voix sur un bus enfant à gain 0,5 sous un parent à 0,5 → amplitude 0,25 ;
  changement de gain lissé sur un bloc ; voix stéréo et streaming routées ; 33e bus → Master et
  avertissement ; muet du Master ; repli MonoGame/faux backend : `git diff` vide des tests
  d'`AudioService` existants et verts ; zéro allocation avec 32 bus et 64 voix ; suite complète.
- Commit : `feat(audio): mix a real bus graph on the audio thread`
- Note de validation (2026-10-06) : capacité publique `IAudioBusBackend` (`BusCapacity`,
  `TryCreateBus(parent, out index)`, `SetBusGain(index, gain)`, `SetNextVoiceBus(index)`,
  `TrySetPsxSpuBus(port, index)`), implémentée par `SoftwareAudioBackend` seul ; `IAudioBackend`
  inchangé. Mixeur : 32 bus préalloués (0 = Master), index donnés dans l'ordre de création (un
  parent a toujours un index plus petit, donc le mix de l'index le plus haut vers 0 donne « enfants,
  puis parents, puis Master ») ; un tampon par bus, bloc maximal fixé à la construction
  (`BufferFrames` de la sortie), `Render` découpe une demande plus grande ; gain propre de chaque bus
  publié en dernière valeur (`Volatile`, jamais perdu), rampé sur le bloc ; chaque voix (résidente,
  stéréo, streaming) et la source SPU portent leur bus. `AudioService` sous la capacité : ne
  multiplie plus le gain de bus dans le volume des voix ni dans le gain du SPU, route chaque voix par
  `SetNextVoiceBus` juste avant son démarrage (le bus fait partie de l'ordre de démarrage : la voix
  ne sonne jamais d'abord sur Master), crée les bus du mixeur à la demande et publie
  `muet ? 0 : volume` par bus quand `Mixer.Version` change (même produit que `EffectiveGain`) ; sans
  la capacité, chemin identique à avant. **Écarts relus et acceptés** : pas de notification ajoutée
  à `AudioMixer`/`AudioBus` (la détection par `Mixer.Version` existait déjà) ; le 33e bus est refusé
  sur le thread de jeu, un avertissement, routé vers Master (le thread audio ne peut pas journaliser
  sans allouer) ; un bus dont la création échoue sur un anneau plein plus de 100 ms reste sur Master
  (pas de nouvel essai, avis P4). 18 tests (`SoftwareMixerBusTests.cs`,
  `AudioServiceBusRoutingTests.cs`) : enfant 0,5 sous parent 0,5 → 0,25 ; Master ; lissage sur un
  bloc ; gain conservé malgré un anneau plein ; voix stéréo et streaming routées ; 33e bus ; bus
  inconnu → Master ; découpage = mix non découpé ; zéro allocation avec 32 bus et 64 voix
  (`AllocationWindow`) ; côté service : gain appliqué une fois, chaîne de bus, muet du Master, SPU
  routé, faux backend toujours replié ; tests existants d'`AudioService` inchangés et verts. Les deux
  solutions : 0 erreur, aucun avertissement dans les fichiers touchés ; suite complète 2956/2956
  (six passages ; un échec isolé d'un test de durée de S2, traité à part, O20).

### ✅ T5.2 — Rampes à durée explicite (P21)

- Objectif : fondus de voix, de musique et de bus sans pas par frame.
- Fichiers : `Software/` (rampe par voix et par bus), `IAudioBusBackend.cs` (rampe), `AudioService.cs`
  (fondus de voix et de bus sous la capacité), `Streaming/MusicPlayer.cs` (fondus de piste), tests.
- Étapes : commande « rampe vers v en n trames » (non perdable : réessai borné comme `SetParameters`)
  et commande « figer » ; interpolation par échantillon, départ au bloc suivant ; `FadeVoice`, les
  fondus de `MusicPlayer` et un nouveau `AudioService.FadeBus(bus, cible, durée)` (additif)
  l'utilisent sous la capacité ; repli inchangé. Sous la capacité, le thread de jeu tient la
  chronologie de chaque rampe (P21) et en tire `GetVoiceVolume`, `MusicPlayer.GetVolume`,
  `IsFading`, la libération en fin de `StopWithFade` et le départ d'un fondu enchaîné ;
  `CancelFade` envoie « figer ».
- Validation : tests du mixeur : rampe de 1 à 0 en 0,25 s → enveloppe linéaire à 1e-5 près
  échantillon par échantillon depuis le début du bloc suivant ; rampe interrompue par une autre →
  reprise depuis la valeur courante ; « figer » arrête l'enveloppe. **Tests du contrat public sur
  `SoftwareAudioBackend` avec la sortie hors ligne**, comparés au même scénario sur le faux backend
  (repli) : `GetVoiceVolume` et `MusicPlayer.GetVolume` pendant un fondu, `IsFading` avant, pendant
  et après, `CancelFade` (valeur gardée, voix vivante), fondu enchaîné repartant de la valeur
  atteinte, libération de la voix à la fin de `StopWithFade` — mêmes résultats à un bloc près ; tests
  de fondu existants (faux backend) inchangés et verts ; suite complète.
- Commit : `feat(audio): sample-interpolated fades on voices and buses`
- Note de validation (2026-10-06) : `IAudioBusBackend` gagne `TryRampVoiceVolume(voice, cible,
  secondes)`, `FreezeVoiceVolume(voice)`, `TryRampBusGain(bus, cible, secondes)`,
  `FreezeBusGain(bus)` (interface encore non publiée hors de cette branche) ; `AudioService.FadeBus(bus,
  cible, secondes)` (additif). Mixeur : commandes `RampVoice`, `FreezeVoice`, `RampBus`, `FreezeBus`
  (envoyées avec l'attente bornée de `SetParameters`, jamais perdues en silence ; échec → repli au
  pas par frame), départ au bloc suivant, interpolation linéaire par échantillon en double précision
  (y compris à travers le découpage des blocs) ; une rampe repart de la valeur courante ; « figer »
  arrête l'enveloppe ; pendant une rampe, `SetVolume` y met fin et le volume de `SetParameters` est
  ignoré (pan et pitch s'appliquent) ; un bus garde la valeur atteinte jusqu'à la prochaine
  publication, qui l'emporte même à valeur égale (compteur de publications). Thread de jeu (P21) :
  `FadeVoice` garde sa chronologie et envoie la rampe une fois ; `GetVoiceVolume`, `IsFading`, la
  libération en fin de `StopWithFade` et le départ d'un fondu enchaîné suivent la chronologie ;
  `CancelFade` envoie « figer ». `MusicPlayer` inchangé (ses fondus passent déjà par `AudioService`).
  Relu en session principale ; **corrigé** : couper ou rétablir le son d'un bus pendant un `FadeBus`
  ne prenait effet qu'à la fin du fondu sous la capacité ; désormais au `Update` suivant sur les deux
  chemins (la publication explicite met fin à la rampe, la suite du fondu est publiée par frame
  depuis la chronologie). Écarts acceptés : une libération en fin de `StopWithFade` peut couper
  jusqu'à un bloc de rampe restant (écart documenté d'un bloc) ; un changement de pan pendant une
  rampe saute dans le bloc. 24 tests (`SoftwareMixerRampTests.cs`, `AudioServiceRampFadeTests.cs`) :
  rampe 1 → 0 en 0,25 s linéaire à 1e-5 par échantillon, interruption, figer, voix en pause, `SetVolume`
  pendant une rampe, génération périmée, rampes de bus et publication qui l'emporte, contrat public
  comparé au faux backend tick par tick (`GetVoiceVolume`, `IsFading`, `CancelFade`, fondu enchaîné,
  `StopWithFade`, fondu croisé de musique, `FadeBus`, muet pendant un fondu de bus), zéro allocation
  (`AllocationWindow`) ; tests de fondu existants inchangés et verts. Les deux solutions : 0 erreur ;
  suite complète 2980/2980 (quatre passages).

### ✅ T5.3 — Effets insérés : filtres biquad et compresseur (P20)

- Objectif : jusqu'à 4 effets par bus, dans l'ordre d'insertion.
- Fichiers : `CasaEngine/Framework/Audio/Effects/` (`AudioEffect` public abstrait côté jeu,
  `BiquadFilterEffect` : passe-bas, passe-haut, passe-bande, crête, plateaux ; `CompressorEffect`),
  état DSP interne côté mixeur, `AudioBus.cs` (ajout et retrait d'effets, additif), tests.
- Étapes : l'effet est construit sur le thread de jeu (allocation permise) et transmis par
  l'anneau ; ses paramètres sont publiés en dernière valeur ; coefficients calculés depuis les
  formules du Cookbook (URL citée) ; compresseur depuis l'article cité (seuil, rapport, genou,
  attaque, relâchement, gain de sortie) ; garde contre les dénormaux.
- Validation : tests : réponse en fréquence d'un passe-bas à fc (−3 dB à ±0,5 dB, mesurée sur
  sinus) et d'une crête (+6 dB à fc) ; coefficients recalculés dans le test depuis les formules ;
  compresseur : réduction de gain stabilisée conforme à la formule pour 3 niveaux, attaque et
  relâchement à ±10 % des constantes ; queue décroissante longue sans dénormaux (temps de rendu
  stable) ; zéro allocation ; suite complète.
- Commit : `feat(audio): biquad filter and compressor bus effects`
- Note de validation (2026-10-06) : types publics `AudioEffect` (abstrait, constructeur interne :
  seuls les effets du moteur existent), `BiquadFilterEffect` (`BiquadFilterType` : passe-bas,
  passe-haut, passe-bande, crête, plateaux bas et haut ; fréquence, Q, gain), `CompressorEffect`
  (seuil, rapport, genou, attaque, relâchement, gain de sortie) ; `AudioBus.AddEffect`,
  `RemoveEffect`, `Effects`, `MaxEffects = 4` ; `AudioMixer.EffectsVersion` ; `IAudioBusBackend`
  gagne `TryAddBusEffect`, `TryRemoveBusEffect` (tout additif). Paramètres : instantané immuable
  publié en dernière valeur (`Volatile`), coefficients recalculés sur le thread audio quand
  l'instantané change ; ajout et retrait par commandes, réessayés au `Update` suivant si l'anneau
  est plein, dans l'ordre ; 32 × 4 emplacements préalloués ; effets appliqués dans le tampon du bus
  avant son gain, y compris sur un bus silencieux (les queues continuent) ; garde contre les
  dénormaux (état ramené à 0 sous 1e-30). Sources lues : Audio EQ Cookbook du W3C
  (https://www.w3.org/TR/audio-eq-cookbook/, six jeux de coefficients, forme directe transposée II
  en double) ; Giannoulis, Massberg, Reiss, JAES 2012 (page QMUL vide, PDF lu sur une copie de la
  Wayback Machine de la même URL ; équations 1, 4, 7, 16, 23 : genou doux, réduction de gain,
  détecteur de crête lissé à branches, coefficients, gain de sortie). Choix documentés : détecteur
  stéréo lié (maximum des deux canaux) ; pas de lissage des changements de paramètres ; sans la
  capacité, effets absents et un journal limité. 34 tests (`AudioEffectDspTests.cs`,
  `SoftwareMixerEffectTests.cs`, `AudioServiceEffectsTests.cs`) : coefficients recalculés depuis le
  Cookbook (à 1e-12, sept cas), passe-bas et passe-haut à −3 dB ± 0,5 à fc, crête +6 dB ± 0,1,
  compresseur stabilisé à 0,01 dB sur sept niveaux, attaque et relâchement à ± 10 %, queue sans
  dénormaux, ordre d'insertion, effet avant le gain, retrait, 5e effet refusé, paramètres
  conservés malgré un anneau plein, zéro allocation (32 bus × 4 effets, 64 voix), repli. Les deux
  solutions : 0 erreur ; suite complète 3014/3014 (quatre passages).

### ✅ T5.4 — Limiteur du Master, bus de retour et réverbération (P20)

- Objectif : sortie protégée et réverbération partagée.
- Fichiers : `Effects/LimiterEffect.cs`, `Effects/ReverbEffect.cs` (Freeverb), bus de retour et
  départs (`AudioBus.SetSend(bus, niveau)`, additif), mixeur, tests.
- Étapes : limiteur à action directe sur le Master (avant l'écrêtage dur conservé en dernier
  recours), actif par défaut sous le backend logiciel (plafond −1 dBFS) ; bus de retour mixés après
  les autres, sans départ vers eux-mêmes (cycle refusé) ; Freeverb d'après la description citée,
  retards mis à l'échelle du débit de sortie.
- Validation : tests : somme de voix à +12 dB → crête de sortie ≤ −1 dBFS après l'attaque ;
  réponse impulsionnelle de la réverbération : énergie décroissante, aucune valeur non finie,
  longueurs de retard attendues au débit de sortie ; cycle de départs refusé ; zéro allocation ;
  stress de 60 s avec réverbération et limiteur : 0 sous-alimentation ; suite complète.
- Commit : `feat(audio): master limiter, return buses and reverb`
- Note de validation (2026-10-06) : types publics `LimiterEffect` (plafond −1 dB par défaut, attaque,
  relâchement, `IsEnabled`), `ReverbEffect` (taille de pièce, amortissement, wet, dry, séparation
  stéréo ; longueurs des retards exposées), `AudioBusSend` ; `AudioBus.SetSend`, `GetSend`, `Sends`,
  `MaxSends = 4` ; `AudioMixer.SendsVersion` ; `AudioService.MasterLimiter` ; `IAudioBusBackend`
  gagne `TrySetBusSend`, `TrySetMasterLimiter` (tout additif). Ordre du mix : voix et SPU dans leur
  bus ; chaque bus après ceux qui l'alimentent (enfants et bus qui lui envoient), effets insérés
  puis gain puis parent ; un bus de retour est simplement une cible de départ ; Master, puis le
  limiteur, puis l'écrêtage dur conservé. Ordre recalculé sur le thread audio, sans allocation,
  seulement quand un bus ou un départ change. Départ « post-fader » (après les effets et le gain
  propre du bus, pas celui de ses ancêtres), niveau rampé sur le bloc ; cycle refusé sur le thread
  de jeu (`InvalidOperationException`) et de nouveau côté mixeur. Limiteur : détecteur et genou de
  T5.3 (Giannoulis et al., équations 4, 16, 23, rapport infini), **plus un plafonnement par
  échantillon au niveau du plafond** (sans lui la crête dépassait de 0,12 dB entre deux crêtes d'un
  sinus) ; actif par défaut sous le backend logiciel. Réverbération : Freeverb d'après les pages
  CCRMA de J. O. Smith (structure, peigne passe-bas en rétroaction, passe-tout g = 0,5, longueurs
  lues sur la figure 3.8 : peignes 1557, 1617, 1491, 1422, 1277, 1356, 1188, 1116, passe-tout 225,
  556, 441, 341, +23 à droite, mises à l'échelle du débit de sortie) ; mémoire des retards allouée sur
  le thread de jeu et portée par la commande d'ajout. Trois fichiers de tests existants coupent le
  limiteur (ils lisent des niveaux exacts jusqu'à la pleine échelle). Démo : pendant le stress, un
  bus « StressReverb » avec `ReverbEffect`, départs depuis Sfx (0,4) et Music (0,3). 28 tests
  (`SoftwareMixerSendTests.cs`, `ReverbLimiterDspTests.cs`, `AudioServiceSendsTests.cs`) : voix à
  +12 dB → crête ≤ −1 dBFS après l'attaque, réponse impulsionnelle (énergie décroissante, aucune
  valeur non finie ni dénormale, premier échantillon aux index attendus), longueurs à 22,05, 44,1,
  48 et 96 kHz, cycles refusés à trois niveaux, départ post-fader, ordre en chaîne, zéro allocation
  (32 bus, 64 voix, 2 départs par bus, réverbération et limiteur). Les deux solutions : 0 erreur ;
  suite complète 3041/3041 (trois passages). **Stress de 60 s, sans clavier** :
  `Audio stress: reverb send on, master limiter enabled=True ceiling=-1 dB`, SPU actif,
  `Audio stress done: backend=SoftwareAudioBackend seconds=60 underruns=0 gc=119`. Choix à
  confirmer : O22.

### ✅ T5.5 — Ducking et snapshots

- Objectif : atténuer un bus selon l'activité d'un autre, et rétablir un état de mix en fondu.
- Fichiers : `Effects/DuckingEffect.cs` (ou propriété de bus), `Mixing/AudioMixerSnapshot.cs`
  (capture et application, additif), `AudioService.cs`, tests.
- Étapes : ducking : enveloppe du bus source calculée dans le bloc, atténuation du bus cible
  (profondeur, seuil, attaque, relâchement) ; snapshot : gains de bus et paramètres d'effets, appliqué
  en rampe ; le bus Editor et le muet du Master ne sont jamais touchés ; repli : snapshots de gains
  seulement, ducking absent (journal une fois).
- Validation : tests : dialogue actif → musique atténuée de la profondeur réglée, retour après le
  relâchement ; snapshot capturé, modifié, réappliqué en 0,5 s → gains d'origine, Editor et muet
  inchangés ; suite complète.
- Commit : `feat(audio): bus ducking and mixer snapshots`
- Note de validation (2026-10-06) : `DuckingEffect(source, profondeur, seuil, attaque, relâchement)`,
  effet inséré sur le bus cible (même transport que T5.3/T5.4 ; sa place dans la chaîne décide de ce
  qui est atténué) ; `AudioMixerSnapshot.Capture(mixer)`, `TryGetBusVolume` ;
  `AudioService.CaptureSnapshot()`, `ApplySnapshot(snapshot, durée)` ; `IAudioBusBackend` gagne
  `TryAddBusDucking` (tout additif). Ducking : niveau de la source (maximum des deux canaux, après
  ses effets, multiplié par son gain propre en fin de bloc ; ancêtres ignorés, comme les départs),
  réduction fixe de la profondeur au-dessus du seuil, lissée par le détecteur de Giannoulis et al.
  (équations 1, 4, 7, 16, 23, déjà citées) ; **ajout du moteur** : un suiveur de crête de 50 ms
  devant le seuil (sinon la réduction tremble à chaque passage par zéro d'une voix périodique ; le
  relâchement démarre 50 à 120 ms après l'arrêt de la source) ; relation source → cible ajoutée à
  l'ordre des bus (même bloc), cycles refusés sur le thread de jeu (`InvalidOperationException`, y
  compris à travers les départs et d'autres ducking) et de nouveau côté mixeur. Snapshots : volume
  propre de chaque bus sauf Editor (Master compris) et paramètres des effets insérés ; jamais le
  muet d'un bus, jamais le bus Editor, jamais le limiteur ; appliqués par `FadeBus` (rampes par
  échantillon sous la capacité, pas par frame sinon, durée 0 immédiate) ; paramètres d'effets
  publiés aussitôt, sans rampe ; sans la capacité, volumes seulement. 16 tests
  (`SoftwareMixerDuckingTests.cs`, `AudioServiceDuckingSnapshotTests.cs`) : profondeur atteinte puis
  retour, ordre dans le même bloc pour les deux ordres de création (test de mutation : sans l'arête,
  0,600 au lieu de 0,2256), source sous le seuil ou muette, cycles, retrait, zéro allocation (32 bus,
  64 voix, 9 relations), musique atténuée par le bus Voice via `AudioService`, aller-retour de
  snapshot en 0,5 s avec Editor et muet du Master inchangés, repli sur le faux backend (volumes
  seulement, un journal). Les deux solutions : 0 erreur, aucun avertissement dans les fichiers
  touchés ; suite complète 3057/3057 (trois passages). Choix à confirmer : O22.

### 🧪 T5.6 — Démo, documentation, ADR et vérification

- Fichiers : `CasaEngine.Demos/Demos/AudioDemo.cs` (réverbération, filtre et ducking à la touche,
  stress inchangé), `docs/engine/audio-system.md`, `docs/decisions/0059-…md` (numéro revérifié sur
  toutes les branches, P18 à P21), index, ce plan.
- Validation : stress de 60 s avec effets : 0 sous-alimentation ; vérificateur frais **CONFIRMED** ;
  écoute de la démo : 🧪 pour l'auteur.
- Commit : `docs(audio): document buses, effects, ducking and snapshots`
- Note de progression (2026-10-06) : démo (touches R, T, D ; stress inchangé), section 2 bis
  d'`audio-system.md`, limites et tableau des touches, ADR-0059 (numéro libre sur toutes les
  branches), statut d'ADR-0001, index (commit `247330ba`) ; stress de 60 s avec SPU, réverbération
  et limiteur : `underruns=0`. **Vérificateur frais sur `247330ba` : REFUTED** sur un seul point,
  **F1 (P2, introduit par T5.2)** : une rampe de bus lisait le compteur de publications quand le
  thread audio l'appliquait, pas quand le jeu l'envoyait ; un `FadeBus` remplacé avant le bloc
  suivant (fondu de durée nulle, snapshot immédiat) laissait le bus bloqué sur l'ancienne cible,
  silencieux. Tout le reste confirmé (repli inchangé, graphe de bus, effets contre leurs sources,
  chemins chauds, API additive, builds, 3057/3057 en quatre passages, stress, documentation dont
  l'exemple compilé). Avis : A1 (P4, un bus au-delà du 32e perd son gain et celui de ses parents :
  précisé dans la doc), A2 (P3, muet dans la même frame qu'un fondu de bus appliqué un tick en
  retard : même cause que F1), A3 (P4, la rampe suit le temps audio et la chronologie le temps de
  jeu : un à-coup plus long que le pas maximal de MonoGame les écarte de plus d'un bloc jusqu'à la
  fin du fondu), A4 (P4, deux bornes de durée de tests relâchées par `9fdd1f12`, documenté en O20).
  **Correctif de F1** (passe 1 sur 5 du budget de reprise) : le compteur de publications du bus
  est lu sur le thread de jeu à l'envoi de la rampe et porté par la commande ; toute publication
  postérieure à l'envoi l'emporte au bloc suivant (`FreezeBus` n'a pas ce trou : il garde le
  compteur de la rampe). 5 tests de non-régression (scénarios A, C, D et E du vérificateur, chacun
  comparé au repli, et un test du mixeur) ; les deux solutions : 0 erreur ; suite complète
  3062/3062 (trois passages) ; stress de 60 s : `underruns=0`. Avis A1 traité dans la doc.
  **Nouveau vérificateur frais sur `580a8e66` : REFUTED** — F1 et A2 corrigés (sonde d'origine et
  cinq scénarios d'ordre supplémentaires justes ; builds, 3062/3062 en trois passages, stress),
  mais **N1 (P2, présent depuis T5.2, manqué au premier passage)** : un muet posé *avant* un fondu
  de bus rampé dans la même frame n'est pas appliqué sous la capacité ; le bus reste audible pendant
  tout le fondu (cas réaliste : couper Sfx puis appliquer un snapshot de 2 s). **Correctif de N1**
  (passe 2 sur 5) : `FadeBus` n'envoie plus de rampe au backend pour un bus muet ; la chronologie
  publie alors `muet ? 0 : volume` à chaque frame, comme pour un muet posé en cours de fondu.
  2 tests (scénarios F et F2 du vérificateur, comparés au repli, avec rétablissement du son) ; les
  deux solutions : 0 erreur ; suite complète 3064/3064 (trois passages) ; stress de 60 s :
  `underruns=0`. **Troisième vérificateur frais sur `be32267f` : REFUTED** — N1 et F1 corrigés
  (toutes les sondes justes, y compris muet puis rétablissement, et dix combinaisons muettes ou
  mixtes à erreur nulle), mais **R1 (P2, présent depuis T5.2)** : une rampe de bus part du gain
  que le backend tenait avant la frame, pas du volume du bus, quand le volume ou le muet a changé
  plus tôt dans la même frame (cas courant : `FadeBus(bus, 0, 0)` puis `FadeBus(bus, 1, 2 s)` → pas
  de fondu d'entrée). **Correctif de R1** (passe 3 sur 5, reprise d'urgence) : la commande de
  rampe de bus porte sa valeur de départ (`fade.Start`, le volume du bus côté jeu à l'envoi) ;
  surcharge additive `IAudioBusBackend.TryRampBusGain(bus, départ, cible, secondes)` (interface non
  publiée) ; règles F1 et N1 inchangées. 6 tests (un du mixeur ; Q1, Q2, Q3, P1, P2 comparés au
  repli tick par tick à 0,01 près) ; les deux solutions : 0 erreur, aucun avertissement dans les
  fichiers touchés ; suite complète 3070/3070 (trois passages) ; stress de 60 s : `underruns=0`.
  Toutes les sondes des vérificateurs rejouées en session principale : écart nul avec le repli.
  **Quatrième vérificateur frais sur `0d876d5f` : REFUTED** — chemin des bus propre (F1, N1, R1
  corrigés ; test de mutation : chaque correctif annulé fait échouer ses tests ; fuzz de 300 graines
  par réglage, aucun écart au-delà d'un bloc), mais **V1/V2 (P2, présent depuis T5.2)** sur le
  chemin des **voix** : un `SetVoiceVolume` suivi dans la même frame d'un `CancelFade` ou d'un
  `FadeVoice` est ignoré par le backend (`CancelFade` fige la valeur rendue alors que
  `GetVoiceVolume` rend la valeur demandée, de façon permanente ; la rampe de voix part de la valeur
  rendue, pas de la chronologie). Avis A1 (P3, introduit par `0d876d5f`, **reporté**) : recibler un
  fondu de bus entre deux blocs fait sauter le gain d'au plus un bloc de pente en un échantillon
  (petit clic). Avis A2 (P4) : un `CancelFade` entre deux blocs garde la valeur rendue, dans la
  tolérance d'un bloc. **Correctif de V1/V2** (passe 4 sur 5) : la rampe de voix porte sa valeur
  de départ (`FadeStartVolume`, surcharge additive `IAudioBusBackend.TryRampVoiceVolume(voix,
  départ, cible, secondes)`) ; `CancelFade` fige la rampe puis renvoie au backend la valeur de la
  chronologie (si ce `SetVolume` est perdu, anneau plein, la rampe est au moins arrêtée). 4 tests
  (un du mixeur ; V1, V2, `SetVoiceVolume` puis `StopWithFade`, comparés au repli tick par tick) ;
  les deux solutions : 0 erreur, aucun avertissement dans les fichiers touchés ; suite complète
  3074/3074 (trois passages) ; stress de 60 s : `underruns=0`.
  **Cinquième vérificateur frais sur `eea0ddee` : CONFIRMED**, aucun P0–P2 : V1/V2 corrigés et
  gardés (test de mutation : chacun des trois morceaux du correctif annulé fait échouer ses tests) ;
  sondes de bus et de voix à écart nul ; deux fuzz (300 et 400 graines, quatre rythmes de `Update`)
  à erreur finale nulle, écarts transitoires d'au plus un bloc ou un `Update`, aucune libération de
  `StopWithFade` décalée sur 1 600 essais ; repli inchangé ; 3074/3074 (trois passages) ; stress :
  `underruns=0`. Avis reportés : A1 (P3) en O25 ; A2 (P4, banc d'essai seulement). Budget de
  reprise : quatre passes de correction sur cinq. Reste l'écoute de la démo (touches R, T, D) :
  🧪 pour l'auteur.

---

## Vague 4 — tranches S5 et S6 (détail du 2026-10-06)

Découverte en lecture seule de S5 et S6 (deux enquêtes, chacune passée par un vérificateur
contradictoire, sur l'instantané `b459742f`) ; les faits dont dépend la partie exécutable ont été
revérifiés sur `247330ba` (fin de S4).

**Faits établis.**
- Alundra n'utilise ni `.sound`, ni `SoundAsset`, ni `SoundEmitterComponent` ; son seul contact avec
  S5 serait de nouveaux champs d'`AudioVoiceParameters`, qu'il construit directement.
- `SoundPlaybackOverrides.ApplyTo` et `SoundAsset.CreateVoiceParameters` reconstruisent les
  paramètres par le constructeur à 4 arguments (O17) : tout nouveau champ de `.sound` y serait perdu.
  La sauvegarde d'un `.sound` est dans `CasaEngine.EditorServices`, le chargement dans le runtime.
- `AudioService` n'a accès ni au monde ni à une caméra active ; `SceneComponent` n'expose pas de
  vecteur « droite » ; `SceneComponent.Load` déréférence `local_transform` et `children_component`
  sans contrôle, absents des émetteurs sauvegardés : convertir `SoundEmitterComponent` en
  `SceneComponent` change la sérialisation.
- Les voix streamées (musique, voix stéréo du chemin de repli) prennent leurs emplacements dans le
  même réservoir de 64 voix que les effets sonores ; `SetParameters` peut attendre jusqu'à 100 ms
  quand l'anneau est plein ; aucun filtre par voix n'existe (les effets de S4 sont par bus) ;
  `SoundAsset` n'a pas de champ de pan.
- Le « solo » n'existe nulle part ; le muet du Master appartient au projet (ADR-0040) et Alundra
  écrit le volume du Master à chaque tick ; l'inspecteur de son code en dur la liste de bus
  {Sfx, Music, Voice, Ui} ; `MGSlider.ValueChanged` alloue un `EventArgs` à chaque changement
  (sous-module MGUI).
- Le moteur n'expose ses éléments internes qu'à `CasaEngine.EditorServices`, `CasaEngine.Tests` et
  `CasaEngine.AposShapes` (`CasaEngine/InternalsVisibleTo.*.cs`) : un panneau de l'éditeur ne lit
  que des API publiques. Les statistiques sont déjà publiques : `SoftwareAudioBackend.ActiveVoiceCount`,
  `UnderrunCount`, `OutputSampleRate`, `LeadMilliseconds`, `DroppedChunkCount`, `DroppedEventCount`
  (`SoftwareAudioBackend.cs:131-155`), `AudioService.ActiveVoiceCount` et `RefusedVoiceCount`
  (`AudioService.cs:101-104`). Le panneau « Output » (`LogsPanel`, enregistré dans `GameEditor.cs`,
  identifiant `EditorPanelIds.Output`) est le modèle d'un panneau en lecture seule.
- Les dépassements de pleine échelle ne se voient pas après le mix : l'écrêtage final et le
  limiteur du Master (T5.4) les effacent ; ils se comptent sur le thread audio, avant eux.

**Enveloppe ajustée (vague 4).**
- **S5 (couche jeu) en pause entière** sur les questions O23 : aucune de ses tâches ne s'exécute
  sans trancher la forme des variations, la sémantique des priorités, l'écouteur et l'atténuation,
  le Doppler, la liaison des paramètres de jeu et la migration de `SoundEmitterComponent`.
- **S6 est découpée.** **S6a (mesure des niveaux et profileur audio de l'éditeur, en lecture
  seule)** ne demande aucune décision produit et se détaille ci-dessous. **S6b (asset du mixeur, panneau
  de mixage éditable, solo, formes d'onde)** est en pause sur les questions O24.

### Décisions de la vague 4 (arbitrages de l'agent, à confirmer par l'auteur)

| Réf | Arbitrage |
|---|---|
| P22 | **Mesure par capacité optionnelle publique** `IAudioMeteringBackend` (même schéma que P9 et P18), implémentée par `SoftwareAudioBackend` seul : pour chaque bus et pour la sortie, crête et valeur efficace de chaque bloc, plus un compteur de dépassements de pleine échelle mesuré à l'entrée du limiteur, avant l'écrêtage. Publication sans verrou, lisible par plusieurs lecteurs sans perte de crête tant que chacun lit au moins toutes les 80 ms (historique circulaire préalloué de 16 blocs par bus et compteur de blocs, ou schéma équivalent justifié). Aucune allocation ni attente sur le thread audio. |
| P23 | **Panneau « Audio » de l'éditeur en lecture seule** : backend, voix (actives, refusées), avance, débit, sous-alimentations, pertes, état du SPU s'il existe, vu-mètres par bus (crête, valeur efficace, dépassements). Il ne modifie rien : pas de source de vérité concurrente (O24). Contrôle de vu-mètre dessiné par un `MGElement` propre à `CasaEngine.Editor/Controls` (ni dans le runtime, ni dans le sous-module MGUI). Textes rafraîchis au plus 4 fois par seconde et seulement quand une valeur change. |

## Phase 6 — Tranche S6a : mesure et profileur audio (exécution)

Résultat attendu : sous le backend logiciel, le jeu et l'éditeur lisent sans verrou les niveaux de
chaque bus et de la sortie ; l'éditeur a un panneau « Audio » qui les affiche avec les statistiques
du backend. Non-objectifs : asset du mixeur, panneau de mixage éditable, solo, formes d'onde (S6b,
O24) ; aucune modification du sous-module MGUI ; pas de changement de la disposition par défaut
de l'éditeur ni des dispositions `.casaeditor` suivies. **Prérequis : S4 clôturée** — T5.6 passée
en ✅ (ou 🧪 pour la seule écoute de l'auteur) après un vérificateur frais **CONFIRMED** sur S4, et
ADR-0059 présent sur la branche. **Arrêt** : tant que T5.6 n'est pas dans cet état, T6.1 ne
démarre pas (les deux toucheraient `SoftwareMixer.RenderBlock`/`MixBuses`). Retour arrière : revert ;
rien du runtime ne dépend de la mesure.

Budget : identique à S2.

Revue du détail (2026-10-06) : deux relecteurs frais **REVISE** (prérequis « S4 clôturée » et arrêt ;
chemin d'ouverture, source et rafraîchissement du panneau ; puis rythme de lecture des mesures
distinct de celui des textes), corrigés ; relecture de clôture **READY**.

### ✅ T6.1 — Mesure des niveaux au thread audio (P22)

- Fichiers : `CasaEngine/Framework/Audio/IAudioMeteringBackend.cs` (capacité publique),
  `Software/SoftwareMixer.cs`, `Backends/SoftwareAudioBackend.cs`, tests `CasaEngine.Tests/Audio/Software/`.
- Étapes : dans le mix des bus (T5.1–T5.5), mesurer la crête et la valeur efficace de chaque bus
  après son gain, et de la sortie ; compter les échantillons au-delà de ±1 à l'entrée du limiteur ;
  publier par bloc selon P22 ; lecture côté jeu par index de bus et pour la sortie, avec la
  correspondance nom de bus → index fournie par `AudioService` (additif).
- Validation : tests : sinus d'amplitude connue sur un bus → crête et valeur efficace attendues
  (à 1e-4) ; voix sur un bus enfant mesurée sur l'enfant et le parent ; dépassements comptés
  quand la somme dépasse la pleine échelle, avec le limiteur actif (la sortie, elle, reste sous le
  plafond) ; deux lecteurs qui lisent à des rythmes différents voient chacun la crête d'un bloc
  isolé ; zéro allocation au rendu (`AllocationWindow`) ; sans la capacité, `AudioService` le dit
  (une ligne de journal limitée) et rien d'autre ne change ; suite complète.
- Commit : `feat(audio): publish bus levels from the audio thread`
- Note de validation (2026-10-06) : capacité publique `IAudioMeteringBackend` (`MeterHistoryBlocks`,
  `ReadLevels(ref AudioMeterCursor, Span<AudioLevel> bus, out AudioLevel sortie)`), types
  `AudioMeterCursor` (un par lecteur), `AudioLevel` (crêtes gauche et droite, `Peak`, `Rms`,
  `Overs`), `AudioMeterRead` (blocs lus, blocs manqués, trames) ; `AudioService.IsMeteringAvailable`,
  `TryGetMeterBusIndex`, `TryReadLevels` (tout additif). Mesure : chaque bus après effets et gain
  propre (le signal passé à son parent ; Master avant le limiteur) ; la sortie après le limiteur et
  l'écrêtage ; dépassements comptés juste après le mix des bus, avant le limiteur (par échantillon
  et par canal). Publication : historique circulaire préalloué de **64 blocs** (au lieu de 16 :
  un bloc est un tampon du périphérique, sans durée minimale garantie ; 64 couvrent 80 ms dès
  1,25 ms par bloc), chaque emplacement sous compteur de séquence (impair pendant l'écriture), le
  compteur de blocs publié en dernier ; le lecteur relit la séquence après copie, un emplacement
  réécrit est compté « manqué », jamais agrégé ; aucune perte de crête pour un lecteur à moins de
  64 blocs ; aucune allocation ni attente d'un côté ou de l'autre. Un bus au-delà du 32e est
  « indisponible », jamais Master. 13 tests (`SoftwareMixerMeteringTests.cs`,
  `AudioServiceMeteringTests.cs`) : sinus 0,8 à gain 0,5 → crête 0,4 et efficace 0,4/√2 à 1e-4 ;
  bus enfant et parent ; dépassements avec limiteur actif (sortie sous le plafond) ; deux lecteurs
  à rythmes différents voient la crête d'un bloc isolé ; blocs manqués comptés ; lecture concurrente
  sans enregistrement déchiré ; zéro allocation (32 bus, 32 voix, limiteur) ; repli sans la capacité
  (une ligne de journal). Les deux solutions : 0 erreur, aucun avertissement dans les fichiers
  touchés ; suite complète 3087/3087 (trois passages) ; stress de 60 s : `underruns=0`.

### 🧪 T6.2 — Panneau « Audio » de l'éditeur (P23)

- Fichiers : `CasaEngine.Editor/Controls/AudioProfilerPanel.cs`, un contrôle de vu-mètre dans
  `CasaEngine.Editor/Controls/`, `CasaEngine.Editor/Workspaces/EditorPanelIds.cs` (nouvel
  identifiant), `CasaEngine.Editor/GameEditor.cs` (descripteur d'outil dans le registre de
  panneaux, comme celui d'« Output » vers `GameEditor.cs:2128-2134` ; entrée de menu ; appel de
  rafraîchissement), tests `CasaEngine.Tests/` (construction et logique sans GPU, sur le modèle des
  tests de panneaux existants).
- Étapes : **ouverture** par une nouvelle entrée « Audio » du menu « Windows »
  (`GameEditor.cs:532-540`, qui n'a aujourd'hui que Save, Load et Reset Layout) : elle ancre le
  panneau d'outil enregistré par le même chemin que `EnsureContextualToolPanelPresent`
  (`GameEditor.cs:3154-3189` : `CreateRegisteredToolPanelNode`, ajout au groupe d'onglets du
  Content Browser ou d'Output, `RebuildVisualTree`), ou le rend actif s'il est déjà ancré ; la
  disposition par défaut (`EditorShellLayoutBuilder`) ne change pas. **Source** : le seul service de
  l'éditeur, `_editorRuntime.AudioSystemComponent.Service`, qui sert aussi le jeu dans l'éditeur
  (`EditorPlaySessionController` réutilise le même jeu). **Deux rythmes distincts** : la
  **lecture des mesures** se fait à chaque `GameEditor.Update` (`GameEditor.cs:5755`) tant que le
  panneau est ancré, donc à moins de 80 ms d'intervalle (P22), sans allocation : le maintien et la
  retombée de crête intègrent chaque bloc publié non encore vu ; seule la **reconstruction des
  textes** est limitée à 4 fois par seconde, et seulement quand une valeur affichée change.
  Contenu : vu-mètres par bus (crête et valeur efficace en dBFS, maintien de crête, dépassements),
  statistiques du backend ; sous un backend sans la capacité : « mesure indisponible » et seulement
  les statistiques communes.
- Validation : tests de logique (conversion dBFS, maintien et retombée de la crête, textes
  reconstruits au plus 4 fois par seconde et jamais quand rien ne change ; **une crête isolée
  publiée sur un seul bloc entre deux reconstructions de texte apparaît dans le maintien de crête
  et dans le texte suivant** ; lecture des mesures sans allocation) ; build des deux solutions ;
  vérification visuelle du panneau dans l'éditeur : 🧪 pour l'auteur.
- Commit : `feat(editor): an audio profiler panel with bus meters`
- Note de validation (2026-10-06) : `AudioProfilerPanel` (forme de `LogsPanel`), `AudioMeterControl`
  (`MGElement` : barre de crête verte, jaune, rouge avec bascules à −12 et −3 dBFS sur −60..0,
  barre efficace, repère de maintien, voyant de dépassement) et **`AudioProfilerModel.cs` en plus de
  la liste** (logique sans UI, testable sans GPU) ; identifiant `EditorPanelIds.AudioProfiler` ;
  dans `GameEditor.cs` (ajouts seulement) : descripteur d'outil « Audio » à côté de « Logs », entrée
  « Windows > Audio » qui ancre le panneau par `EnsureContextualToolPanelPresent` (groupe du Content
  Browser et des Logs ; disposition par défaut inchangée) ou active son onglet, indicateur de
  présence tenu par les événements du dock (pas de recherche par frame), lecture des mesures à chaque
  `Update` tant qu'il est ancré. Constantes : maintien de crête 1,5 s, retombée 20 dB/s, plancher
  −90 dBFS (« -inf »), textes au plus toutes les 0,25 s et seulement quand une valeur affichée change
  au dixième de dB. Sans la capacité : « Metering unavailable » et les statistiques communes, sans
  avertissement. **Écart** : l'état du SPU se limite à « hébergé ou non » (`AudioService` ne publie
  pas son port SPU ; un accesseur public additif serait une petite suite possible). 44 tests
  (`CasaEngine.Tests/Editor/AudioProfilerTests.cs`) dont la crête isolée d'un bloc entre deux
  reconstructions de texte (visible dans le maintien et le texte suivant), l'intervalle de 0,25 s
  (test de mutation à 0,05 : deux échecs), la lecture sans allocation, le panneau construit sans GPU.
  Les deux solutions : 0 erreur, aucun avertissement dans les fichiers touchés ; suite complète
  3131/3131 (trois passages). Éditeur non lancé : vérification visuelle 🧪 pour l'auteur.

### ✅ T6.3 — Documentation, ADR et vérification

- Fichiers : `docs/editor/` (nouvelle page du panneau), `docs/engine/audio-system.md`,
  `docs/decisions/0060-…md` (numéro revérifié sur toutes les branches, P22–P23), index, ce plan.
- Validation : vérificateur frais **CONFIRMED**.
- Commit : `docs(audio): document bus metering and the audio profiler panel`
- Note de validation (2026-10-06) : `docs/editor/audio-profiler-panel.md` (anglais, comme les pages
  récentes de l'éditeur), puce « Mesure des niveaux » et exemple dans `audio-system.md` §2 bis,
  ADR-0060 (numéro libre sur toutes les branches), index (commit `6d3d6f83`). Vérificateur frais sur
  `6d3d6f83` (worktree `audio-verify`, éditeur non lancé) : **CONFIRMED**, aucun P0–P2. Sa propre
  sonde : bus imbriqués et effet inséré mesurés aux valeurs attendues, dépassements avant le
  limiteur (sortie au plafond), crête d'un bloc isolé vue par des lecteurs à 1, 63 et 64 blocs, le
  lecteur à 65 blocs signalant exactement un bloc manqué ; stress concurrent de 3 s (blocs de
  4 trames, trois lecteurs) : 6,2 millions de lectures, plus de 4 millions de blocs écrasés pendant
  une lecture, aucun enregistrement déchiré ; zéro allocation ; API additive ; deux builds
  `--no-incremental` sans avertissement dans les fichiers changés ; 3131/3131 (trois passages) ;
  stress de 60 s : `underruns=0` ; exemple de la doc compilé tel quel. Avis P4 reportés en O26.

---

## Fusion dans `main` (2026-10-06, à la demande de l'auteur)

- Le `main` local (`ebeb81c9` : S1 et la pile e19) et `origin/main` (`dd91efe7` : correctif
  `MouseManager.HasMoved`) avaient divergé. `origin/main` a d'abord été fusionné dans la branche du
  chantier (`e3d2d5f8`) ; les deux solutions compilent sans erreur, la suite passe à 3140/3140 (un
  test de durée de T2.1 a échoué une fois au premier passage et a été corrigé, O20).
- `main` a ensuite été avancé en avance rapide sur la branche (`git fetch . chantier/audio-modern:main`,
  aucun commit sur `main`) : il contient S1 à S4, X1, S6a et `origin/main` ; un push serait une
  avance rapide d'`origin/main`. **Rien n'a été poussé.**
- Restent ouverts : S5 (O23), S6b (O24), X3 (O4, O16), T2.6, O11, O13, les écoutes 🧪 (T1.8, T3.1,
  T5.6, T6.2) et les choix à confirmer (O12, O19, O22). Le plan reste dans `tasks/` tant que ces
  tranches ne sont pas tranchées.

## Phase 7 — Tranche T7 : tables par défaut du SPU et réglage du limiteur (D25, D32)

Résultat attendu : le moteur livre les coefficients des filtres ADPCM du SPU et une FIR de réverbération
calculée par formule, de sorte qu'un jeu crée un SPU qui décode de vrais sons PlayStation et fait de la
réverbération sans fournir de table ; un projet peut couper le limiteur du Master par un réglage.
Non-objectifs : la table gaussienne (toujours à l'appelant) ; les lectures ambiguës de O19 (D26) ;
les autres choix de O22. Prérequis : X1 et S4 clôturées. Retour arrière : revert.

État vérifié (2026-10-06) :
- `PsxSpuHardwareTables` valide et copie des tables fournies ; ADPCM obligatoire, FIR (39 coefficients) et
  gaussienne (512) facultatives (`PsxSpuHardwareTables.cs`) ; sans FIR la réverbération est court-circuitée
  (`PsxSpuReverb.cs`).
- Les réglages audio du projet suivent un modèle : `IsAudioMuted` et `AudioBackend` dans
  `ProjectSettings.cs:47-61`, lus en `ProjectSettingsHelper.cs:36-39`, écrits seulement quand ils sont
  posés (`:134-141`) ; le muet s'applique au démarrage par `AudioSystemComponent` et dans l'éditeur à
  chaque ouverture de projet (`EditorProjectAudioMuteSync`).
- Le limiteur est créé actif par `AudioService` (`AudioService.MasterLimiter`, `IsEnabled`).

Budget : identique à S2.

Revue du détail (2026-10-06) : deux relecteurs frais **REVISE** (FIR : coupure chiffrée, fenêtre, somme exacte
et seuils mesurables ; éditeur : fichiers et forme additive pour atteindre le limiteur), corrigés ; relecture
de clôture **READY**.

### ✅ T7.1 — Tables par défaut du SPU (D25)

- Fichiers : `CasaEngine/Framework/Audio/Psx/PsxSpuHardwareTables.cs` (fabrique additive des tables par
  défaut), un calcul de FIR dans `Psx/`, `CasaEngine.Demos/Demos/AudioDemo.cs` (le SPU de la démo utilise
  les tables par défaut), tests `CasaEngine.Tests/Audio/Psx/`.
- Étapes : les 5 couples de coefficients ADPCM recopiés de psx-spx (page lue, section citée par URL ;
  aucun autre tiers) ; une FIR de 39 coefficients (M = 38) calculée par le noyau en sinus cardinal fenêtré
  de S. W. Smith, « The Scientist and Engineer's Guide to Digital Signal Processing », chapitre 16 : noyau
  de l'éq. 16-4 (https://www.dspguide.com/ch16/2.htm, point central i = M/2 traité comme décrit), fenêtre de
  **Blackman** de l'éq. 16-2 (https://www.dspguide.com/ch16/1.htm), **fréquence de coupure fc = 11 025 Hz**
  (0,25 × 44 100 Hz, la fréquence de Nyquist du débit réduit de 22 050 Hz que la réverbération utilise),
  normalisée à un gain continu de 1, puis arrondie en entiers Q15 (× 32 768) ; après arrondi, le
  coefficient central est ajusté pour que la somme des 39 entiers vaille **exactement 32 768** ; aucune
  valeur matérielle pour la FIR ; la table gaussienne reste à l'appelant ; l'usage de la FIR par
  `PsxSpuReverb` ne change pas (le gain de sortie reste la lecture 20 de O19).
- Validation : tests : les coefficients ADPCM livrés égalent la table de la source citée ; un bloc de
  chaque filtre décodé avec les tables par défaut égale la formule ; FIR : 39 coefficients symétriques,
  somme exactement 32 768 ; réponse en fréquence calculée dans le test sur les entiers livrés, à 44 100 Hz :
  gain à 5 512,5 Hz entre −0,1 et +0,1 dB, gain à 11 025 Hz à −6,0 ± 0,5 dB, atténuation **≥ 60 dB pour
  toute fréquence de 14 000 à 22 050 Hz** (pas de 50 Hz) — un filtre coupé à 5 512,5 Hz échouerait au
  premier critère ; réponse impulsionnelle de la réverbération non nulle et décroissante avec les tables
  par défaut ; zéro allocation inchangé ; suite complète.
- Commit : `feat(psx): ship default ADPCM filters and a formula reverb FIR`
- Note de validation (2026-10-06) : `PsxSpuHardwareTables.CreateDefault()` (additif) ; coefficients ADPCM
  recopiés de psx-spx, page « CDROM XA Audio ADPCM Compression », section « Pos/neg Tables »
  (https://psx-spx.consoledev.net/ps1/cdr/cdromformat/ ; la page du SPU renvoie au CD-XA pour ce filtre) :
  positifs 0, 60, 115, 98, 122 ; négatifs 0, 0, −52, −55, −60 (le SPU utilise les cinq filtres) ; FIR calculée
  une fois par `PsxSpuDefaultReverbFir` (interne) : noyau de l'éq. 16-4 et fenêtre de Blackman de l'éq. 16-2
  de Smith (équations en images sur le site : formes standard décrites par le texte, noté dans le code),
  M = 38, fc = 11 025 Hz, arrondi Q15, coefficient central ajusté à 16 382 pour une somme de 32 768 ;
  filtre demi-bande (coefficients pairs hors centre nuls). Mesures sur les entiers livrés : 0 dB à 0 Hz,
  −0,0014 dB à 5 512,5 Hz, −6,02 dB à 11 025 Hz, au moins 64,09 dB d'atténuation de 14 000 à 22 050 Hz.
  La démo utilise les tables par défaut (son inchangé : son bloc utilise le filtre 0 ; elle n'active pas la
  réverbération du SPU). 11 tests (`PsxSpuDefaultTablesTests.cs`). Les deux solutions : 0 erreur, aucun
  avertissement dans `Psx/` ; suite complète 3151/3151.

### ✅ T7.2 — Réglage de projet du limiteur du Master (D32)

- Fichiers : `CasaEngine/Framework/Configuration/Project/ProjectSettings.cs` et
  `ProjectSettingsHelper.cs` (réglage additif `bool? IsMasterLimiterEnabled`, absent ou nul = limiteur
  actif, écrit seulement quand il a une valeur, comme `AudioBackend`), `CasaEngine/Framework/Audio/ProjectAudioSettings.cs`
  (nouvelle surcharge additive `Apply(AudioService service, ProjectSettings projectSettings)` qui applique
  le muet du Master par l'`Apply(AudioMixer, …)` existant puis l'état du limiteur), `CasaEngine/Framework/Application/Components/AudioSystemComponent.cs`
  (au démarrage, appel de la nouvelle surcharge à la place de `Apply(Mixer, …)`, `AudioSystemComponent.cs:38`),
  `CasaEngine.EditorServices/EditorProjectAudioMuteSync.cs` (nouveau constructeur additif
  `EditorProjectAudioMuteSync(AudioService service)` qui applique la nouvelle surcharge à chaque
  `ProjectLoaded` ; le constructeur `EditorProjectAudioMuteSync(AudioMixer)` et `Apply(AudioMixer, …)`
  restent inchangés), `CasaEngine.Editor/GameEditor.cs` (`GameEditor.cs:1074-1078` : construction par
  `editorAudio.Service`), tests `CasaEngine.Tests/` (dont `CasaEngine.Tests/EditorServices/EditorProjectAudioMuteSyncTests.cs`).
- Étapes : aucune signature publique existante ne change ; la nouvelle surcharge ne touche au limiteur que
  si le backend du service a la capacité de bus (`service.Backend is IAudioBusBackend`), pour ne pas
  déclencher le journal « master limiter is absent » de `AudioService.MasterLimiter` sous le backend
  MonoGame ; elle met `MasterLimiter.IsEnabled` à la valeur du réglage, **ou à vrai quand le réglage est
  absent** (un projet ouvert après un projet qui coupait le limiteur le réactive) ; le réglage n'a pas
  d'interface (édité dans le fichier de projet, comme `IsAudioMuted`) ; un fichier de projet sans le
  réglage reste identique octet pour octet à l'écriture.
- Validation : tests : lecture et écriture du réglage (absent, vrai, faux ; fichier inchangé quand
  absent) ; limiteur coupé au démarrage quand le réglage vaut faux ; dans l'éditeur, ouverture d'un projet
  A (limiteur coupé) puis d'un projet B (réglage absent) → `MasterLimiter.IsEnabled` vrai, et le muet
  suit toujours chaque projet ; sous un faux backend sans capacité : aucun journal du limiteur, muet
  appliqué comme avant ; tests existants de `EditorProjectAudioMuteSyncTests` inchangés et verts ; suite
  complète ; les deux solutions.
- Commit : `feat(audio): a project setting to switch the Master limiter off`
- Note de validation (2026-10-06) : membres additifs `bool? ProjectSettings.IsMasterLimiterEnabled` (lu comme
  `AudioBackend`, écrit seulement s'il a une valeur), `ProjectAudioSettings.Apply(AudioService, ProjectSettings)`
  (muet par l'`Apply(AudioMixer, …)` existant, puis `MasterLimiter.IsEnabled = réglage ?? vrai`, seulement sous
  la capacité de bus), `EditorProjectAudioMuteSync(AudioService)` ; `AudioSystemComponent` appelle la nouvelle
  surcharge au démarrage, `GameEditor` construit la synchronisation depuis le service ; aucune signature
  existante modifiée. 8 tests (`ProjectMasterLimiterSettingTests.cs`) : fichier identique quand le réglage
  est absent, aller-retour vrai et faux, projet sans clé après un projet qui l'avait, limiteur coupé puis
  rallumé, aucun journal sous un faux backend, éditeur projet A puis projet B ; `EditorProjectAudioMuteSyncTests`
  inchangés et verts. Limite : aucun test ne construit un vrai `AudioSystemComponent` (il faut un `Game`) ; le
  démarrage passe par la surcharge testée. Les deux solutions : 0 erreur ; suite complète 3159/3159.

### ✅ T7.3 — Documentation, ADR et vérification

- Fichiers : `docs/engine/psx-spu.md`, `docs/engine/audio-system.md`, `docs/decisions/0062-…md` (numéro
  revérifié sur toutes les branches), index, ce plan.
- Validation : vérificateur frais **CONFIRMED**.
- Commit : `docs(audio): document the default SPU tables and the limiter project setting`
- Note de validation (2026-10-06) : `psx-spu.md` (tables par défaut, exemple `CreateDefault`, journal limité
  à une fois par 5 s, SPU mixé dans son bus, plus de séquenceur prévu), `audio-system.md` (réglage du limiteur),
  ADR-0062, statut d'ADR-0058, index (commit `3fa755dc`). Vérificateur frais sur `3fa755dc` : **CONFIRMED**,
  aucun P0–P2 : coefficients ADPCM comparés à psx-spx téléchargé, FIR recalculée indépendamment (même tableau
  bit pour bit, gains mesurés conformes), réglage et éditeur vérifiés, API additive, deux builds
  `--no-incremental` sans avertissement nouveau, 3159/3159 deux fois. Deux avis P4 sans suite (l'étage FIR
  tourne désormais dans la démo pour une sortie nulle ; coefficient central arrondi de 2 unités, voulu).

## Vague 5 — tranche S5a : variations et priorités (détail du 2026-10-06)

Découverte en lecture seule (un brouillon par tranche, passé par un contrôle contradictoire) ; les faits
dont dépend la tranche ont été revérifiés sur `6f06298f` (fin de T7, `main` au même commit). S5 est
découpée : **S5a (variations aléatoires et priorités de voix, D5, D6, D7, D14)** se détaille ci-dessous ;
**S5b (écouteur, mode spatial, atténuation, Doppler, paramètres de jeu, émetteur en composant de scène,
D8 à D13)** suivra avec son propre détail.

**Faits établis.**
- `SoundPlaybackOverrides.ApplyTo` (`SoundPlaybackOverrides.cs:36-43`) reconstruit les paramètres par le
  constructeur à 4 arguments et perd région de boucle et multiplicateur (O17) ; les méthodes `With*`
  d'`AudioVoiceParameters` (`AudioVoiceParameters.cs:69-88`) les conservent.
- `SoundAsset` porte six champs (fichier, volume, pitch, boucle, bus, streaming) ; `Load` lit par
  `GetSingle`/`GetBoolean`, qui lèvent sur un type inattendu, et `SoundAssetLoader` rend alors `null`
  (l'asset entier est perdu). La sauvegarde est `EditorAssetJsonSerializer.SaveSoundAsset` (`:740`) ; un
  `.sound` neuf est créé par `GameEditor.TryCreateSoundAssetInFolder` (`:3810`).
- Les surcharges passent des valeurs absolues : l'émetteur `asset.Volume * VolumeOverride` et
  `asset.Pitch + PitchOverride` (`SoundEmitterComponent.cs:201-210`), l'action de cinématique
  `asset.Volume * action.Volume` (`CutsceneActionCoroutineFactory.cs:195-197`).
- `AudioService.PlayClip` (`AudioService.cs:178-207`) compte un refus quand le backend rend une poignée
  invalide ; `PlaySound` (`:223-252`) résout le clip puis appelle `PlayClip` ; `VoiceEntry` (`:1546-1588`)
  ne porte aucune priorité ; `Update` recycle les voix finies (`:930`).
- Alundra n'appelle que `PlayClip(…, owner: …)`, `PlayClipStereo` et `new AudioService(backend)` ; elle
  n'utilise ni `.sound` ni émetteur (vague 4).
- Le dépôt contient trois `.sound` (`CasaEngine.Demos/Content/Audio/`) et aucun émetteur ni aucune
  cinématique sauvegardés qui référencent un son : aucune donnée à migrer.
- ADR-0002 écrit « no random variations in V1 » ; aucune ADR 0063 n'existe sur les branches locales.

### Décisions de la vague 5 (arbitrages de l'agent, à confirmer par l'auteur)

| Réf | Arbitrage |
|---|---|
| P24 | **Plages relatives** : un facteur de volume et un décalage de pitch en octaves, tirés **par-dessus** la valeur déjà surchargée (la surcharge remplace la base comme aujourd'hui, le tirage s'applique ensuite, le résultat est borné). C'est la lecture qui fait composer l'émetteur et la cinématique sans les modifier (D5) ; des plages absolues obligeraient à changer `CreateOverrides` et l'action de cinématique pour qu'ils passent des facteurs. Un tirage par lecture : une boucle garde le sien. |
| P25 | **Bornes** : facteur de volume dans [0, 1] (atténuation seule : le volume final est borné à 1, un facteur supérieur serait le plus souvent écrêté) ; décalage de pitch dans [`MinPitch`, `MaxPitch`] ; tirage uniforme et linéaire ; une plage inversée est conservée telle quelle et triée au tirage ; une plage dégénérée ne tire rien. |
| P26 | **Fichiers** : le tirage choisit entre le fichier principal (`AudioFileAssetId`) et une liste additionnelle `variation_audio_file_asset_ids` ; entrées vides ignorées ; tirage uniforme sans anti-répétition (O27) ; pas de repli si le fichier tiré ne se charge pas (même issue qu'aujourd'hui : journal étranglé, `AudioVoiceHandle.None`). Un asset streaming ignore variations et priorité (le lecteur de musique ne les lit pas). |
| P27 | **Hasard injectable** : `AudioService.VariationRandom` (`System.Random`, défaut `Random.Shared`, `null` y revient, thread de jeu seulement) ; ordre des appels figé (fichier, volume, pitch) ; aucun appel quand rien n'est à tirer ; valeurs rendues bornées (indice dans [0, n−1], réel dans [0, 1)) pour qu'un `Random` défaillant ne fasse jamais lever `PlaySound`. |
| P28 | **Priorité** : entier porté par `SoundAsset.Priority` (0 = aucune, borné à [0, `MaxPriority` = 100], valeur haute arbitraire) et par `SoundPlaybackOverrides.Priority` (`int?`, propriété `init` ; 0 retire la priorité) ; **ni dans `AudioVoiceParameters` ni dans `IAudioBackend`** : Alundra et son faux backend ne changent pas, et `PlayClip`, `PlayClipStereo` et `PlayStream` jouent toujours sans priorité. |
| P29 | **Vol** (D6) : seulement pour une priorité > 0, quand le backend est disponible et plein (`ActiveVoiceCount >= VoiceCapacity`) ; d'abord recycler une voix déjà finie (ce n'est pas un vol) ; sinon victime = voix non streamée, non stéréo du backend, de priorité > 0 et strictement inférieure, la plus basse puis la plus ancienne ; voix en pause ou en fondu éligibles. `StolenVoiceCount` n'augmente que si le `Play` qui suit réussit ; un `Play` refusé après un vol compte un refus et la victime reste perdue (cas dégradé documenté). Pas de journal par vol. |
| P30 | **Sérialisation additive** : chaque nouvelle clé (`priority`, `variation_audio_file_asset_ids`, `variation_volume_min`, `variation_volume_max`, `variation_pitch_min`, `variation_pitch_max`) n'est écrite que si sa propre valeur diffère de son défaut (la liste : ses seuls GUID non vides, dans l'ordre, s'il y en a) ; un `.sound` sans ces champs se resauvegarde clé pour clé à l'identique. Chargement tolérant : une valeur de type inattendu garde le défaut avec un avertissement qui nomme l'asset et la clé ; aucune exception. |
| P31 | **Éditeur** : la prévisualisation de l'inspecteur joue avec `Priority = 0` (elle ne vole jamais une voix du jeu) et tire les variations ; le compteur de vols n'est montré que dans la démo (panneau Audio : O28). |

## Phase 8 — Tranche S5a : variations aléatoires et priorités de voix (D5, D6, D7, D14)

Résultat attendu : un `.sound` peut déclarer des fichiers de variation, des plages de volume et de pitch et
une priorité ; `PlaySound` tire une variation par lecture, composée avec les surcharges de l'émetteur et des
cinématiques ; quand les voix sont toutes prises, le refus reste le défaut et une voix n'est volée que pour
un son de priorité explicitement plus haute. Non-objectifs : voix virtuelles (D7) ; délai et séquence
(D14) ; écouteur, spatialisation, Doppler, paramètres de jeu et passage de l'émetteur en composant de scène
(S5b) ; affichage des vols dans le panneau Audio (O28) ; toute modification d'`IAudioBackend`,
d'`AudioVoiceParameters`, des backends ou du dépôt parent. Prérequis : T7 clôturée ; **base `6f06298f`**
pour toutes les comparaisons `git diff 6f06298f -- …` de la phase (`main` peut avancer). Retour arrière :
revert des commits de la phase ; un `.sound` écrit avec les nouvelles clés se charge encore (le chargeur
ne lit que ses clés connues). Approbation : D33 (AUTO), après relecture **READY** de ce détail.

Budget : identique à S2.

Revue du détail (2026-10-06) : brouillon passé par un contrôle contradictoire (instantané périmé, choix de format
à inscrire en P24 à P31, preuves sur les chemins réels de l'émetteur et de la cinématique, vol sous le backend
logiciel, lecture tolérante, allocations sur le chemin accepté), corrigé ; relecteur frais sur le détail
**READY**.

### ✅ T8.1 — Les surcharges conservent région de boucle et multiplicateur (O17)

- Fichiers : `CasaEngine/Framework/Audio/SoundPlaybackOverrides.cs` (corps et doc XML d'`ApplyTo`),
  `CasaEngine.Tests/Audio/SoundPlaybackOverridesTests.cs` (nouveau).
- Étapes : `ApplyTo` part de `parameters` et enchaîne `WithVolume`, `WithPan`, `WithPitch` et `WithLooping`
  pour les seuls champs surchargés ; signature, `ResolveBus` et constructeur inchangés ; doc XML : les champs
  non surchargés, région de boucle et multiplicateur compris, gardent la valeur d'entrée.
  `SoundAsset.CreateVoiceParameters` ne change pas.
- Validation : tests avec `p = AudioVoiceParameters.Default.WithLoopRegion(1000, 2000).WithRateMultiplier(4f)` :
  `None.ApplyTo(p)` égale `p` ; une surcharge des quatre champs les change et garde région et multiplicateur ;
  une surcharge partielle (volume seul) garde le reste ; `None.ApplyTo(Default)` égale `Default` ; une surcharge
  `volume: float.NaN` donne le même volume qu'avant la tâche (valeur relevée avant la modification et figée
  dans le test). Mutation notée : remettre le constructeur à 4 arguments fait échouer les deux premiers tests.
  Deux solutions sans erreur, aucun avertissement nouveau dans les fichiers touchés ; suite complète verte.
- Commit : `fix(audio): keep the loop region and rate multiplier when overrides are applied`
- Note de validation (2026-10-06) : `ApplyTo` enchaîne les `With*` des seuls champs surchargés ; cinq tests
  (`SoundPlaybackOverridesTests`). Mutation : le constructeur à 4 arguments remis fait échouer trois tests (les deux
  attendus et la surcharge partielle). Deux solutions sans erreur ni avertissement dans les fichiers touchés ;
  3164/3164.

### ✅ T8.2 — Champs additifs du `.sound` : variations et priorité (D5, D6, P25, P26, P28, P30)

- Fichiers : `CasaEngine/Framework/Audio/SoundAsset.cs`, `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`
  (`SaveSoundAsset`), `CasaEngine.Tests/Audio/SoundAssetTests.cs`,
  `CasaEngine.Tests/Audio/SoundAssetEditorSerializationTests.cs`. Rien d'autre ne lit ces champs dans cette tâche.
- Étapes :
  1. `SoundAsset` : `public const int MaxPriority = 100` et `Priority` (setter borné à [0, `MaxPriority`]) ;
     `public List<Guid> VariationAudioFileAssetIds { get; } = new()` (entrées `Guid.Empty` tolérées) ;
     `VariationVolumeMin`/`VariationVolumeMax` (défaut 1, chaque extrémité bornée à [0, 1], NaN garde la valeur
     précédente, motif de `SanitizeVolume`) ; `VariationPitchMin`/`VariationPitchMax` (défaut 0, bornées à
     [`MinPitch`, `MaxPitch`], NaN garde). Une plage inversée est conservée. Doc XML (anglais) sur chaque membre.
  2. `Load` : clés de P30 ; liste vidée avant lecture ; chaque clé absente donne son défaut. Lecteur tolérant
     privé : un nombre est attendu (`JTokenType.Integer` ou `Float`), sinon défaut et `Logs.WriteWarning`
     nommant l'asset et la clé (`using CasaEngine.Core.Logging;`) ; pour la liste, une valeur qui n'est pas un
     tableau est ignorée avec un avertissement, une entrée que `Guid.TryParse` refuse est ignorée avec un
     avertissement, les autres sont gardées ; jamais d'exception. Les six clés d'avant gardent leur lecture.
  3. `SaveSoundAsset` : après les clés existantes, chaque nouvelle clé seulement si sa valeur diffère de son
     défaut ; la liste en `JArray` des GUID non vides, dans l'ordre, seulement s'il y en a un.
- Validation : `SoundAssetTests` : document complet ; document minimal et document à six clés copié de
  `menu_click.sound` → défauts (liste vide, 1/1, 0/0, priorité 0) ; `null`, une chaîne et un objet pour
  `priority`, pour une clé de plage et pour la liste → asset chargé, défaut pour la clé fautive, autres champs
  intacts, un avertissement chacun (journal de test par `Logs.AddLogger` puis `Logs.Close` en `finally`, modèle
  `TileMapDepthSettingsLoadWarningsTests.cs`) ; entrée GUID invalide ignorée, les autres gardées ; bornes
  (priorité −3 → 0, 500 → 100 ; facteur 9 → 1, −1 → 0 ; pitch 3 → 1 ; NaN garde) ; plage inversée conservée ;
  les trois `.sound` de la démo chargés par le vrai `SoundAssetLoader` ont les nouveaux champs aux défauts.
  `SoundAssetEditorSerializationTests` : aller-retour de tous les champs ; un asset neuf et les trois `.sound` de
  la démo ne produisent aucune nouvelle clé et exactement leurs clés d'origine ; min 0,5 et max 1 n'écrit que
  `variation_volume_min` ; `Guid.Empty` n'est pas écrit ; sauvegarde → chargement → sauvegarde égale
  (`JToken.DeepEquals`). Deux solutions sans erreur ni avertissement nouveau dans les fichiers touchés ; suite
  complète verte.
- Commit : `feat(audio): additive variation and priority fields in the .sound asset`
- Note de validation (2026-10-06) : six membres additifs sur `SoundAsset`, lecteur numérique tolérant (un type
  inattendu garde le défaut avec un avertissement nommant l'asset et la clé), liste lue par `Guid.TryParse` ;
  `SaveSoundAsset` n'écrit chaque nouvelle clé que hors défaut. Une priorité fractionnaire est tronquée après la
  borne (2,7 → 2) ; une entrée GUID invalide donne un avertissement par entrée. 38 tests ajoutés, dont les trois
  `.sound` de la démo chargés par le vrai chargeur et resérialisés avec leurs seules clés d'origine. Deux
  annotations `string?` (CS8632, hors contexte nullable) retirées après une build `--no-incremental`. Deux
  solutions sans erreur ni avertissement nouveau dans les fichiers touchés ; 3202/3202 ; diffs vides depuis
  `6f06298f` sur `IAudioBackend.cs`, `AudioVoiceParameters.cs`, `AudioService.cs`.

### ✅ T8.3 — Tirage des variations à `PlaySound` (D5, P24, P26, P27)

- Fichiers : `CasaEngine/Framework/Audio/SoundVariation.cs` (nouveau, `internal`),
  `CasaEngine/Framework/Audio/AudioService.cs` (`PlaySound`, `ResolveClip`, `VariationRandom`),
  `CasaEngine.Tests/Audio/SoundVariationTests.cs` et `CasaEngine.Tests/Audio/AudioServiceVariationTests.cs`
  (nouveaux). L'émetteur, l'action de cinématique, `MusicPlayer`, `AudioVoiceParameters`, `IAudioBackend` et
  les backends ne changent pas.
- Étapes :
  1. `SoundVariation.cs` : `internal readonly struct SoundVariationDraw` (`AudioFileAssetId`, `VolumeFactor`,
     `PitchOffset`) avec `ApplyTo(in AudioVoiceParameters)` : tirage neutre (1 et 0) → entrée rendue telle
     quelle ; sinon `WithVolume(Volume * VolumeFactor).WithPitch(Pitch + PitchOffset)`. `internal static class
     SoundVariation` avec `Draw(SoundAsset, Random)` : candidats = fichier principal non vide puis entrées non
     vides de la liste (boucle `for`, sans LINQ ni liste temporaire) ; 0 candidat → `Guid.Empty`, 1 → lui sans
     appel au hasard, n ≥ 2 → `Next(n)` borné à [0, n−1] ; volume puis pitch : extrémités triées, plage non
     dégénérée → `lo + (hi − lo) * u` avec `u = NextSingle()` borné à [0, 1), sinon `lo` sans appel. Aucune
     allocation.
  2. `AudioService.VariationRandom` (P27 ; doc XML : thread de jeu, injectable pour les tests ou un rejeu) ;
     constructeur inchangé.
  3. `PlaySound` : après le refus des assets streaming, `Draw`, puis `ResolveClip(asset, draw.AudioFileAssetId)`
     (nouveau paramètre de la méthode privée ; messages inchangés et toujours étranglés), puis
     `draw.ApplyTo(overrides.ApplyTo(asset.CreateVoiceParameters()))`. Doc XML de `PlaySound` : règle de
     composition de P24. `PlayClip`, `PlayStream`, `PlayClipStereo` et `MusicPlayer.Play` ne changent pas.
- Validation : `SoundVariationTests` (sous-classe de test de `Random` qui scripte `Next(int)` et `NextSingle()`
  et note ses appels) : 0 et 1 candidat sans appel ; indices scriptés 0, 1, 2 sur trois fichiers ; entrées vides
  ignorées ; plage [0,5 ; 1] avec 0 et 0,5 → 0,5 et 0,75 ; plage inversée triée ; plage dégénérée sans appel ;
  ordre fichier, volume, pitch ; `Random` hostile (−1, n, 1,0) borné sans exception ; tirage neutre →
  paramètres égaux (région et multiplicateur gardés). `AudioServiceVariationTests` (`FakeAudioBackend` et
  `FakeAudioClipProvider`) : asset sans variation → paramètres reçus inchangés et hasard jamais appelé
  (`AudioServicePlaySoundTests` verts sans modification) ; facteur 0,5 sur un volume 0,8 → 0,4, avec la
  surcharge `volume: 0,5` → 0,25 ; décalage 0,25 sur un pitch surchargé à 0,5 → 0,75 ; trois clips, indice 2 →
  `GetClip` rend le troisième ; fichier tiré introuvable → `None`, sans repli ; asset streaming toujours
  refusé ; `VariationRandom = null` retombe sur le défaut. **Chemins réels** : `SoundEmitterComponent.Play()`
  (émetteur à `VolumeOverride` 0,5 et `PitchOverride` 0,25 ; `AudioService` injecté par les champs de stockage
  de `CasaEngineGame.AudioSystemComponent` et de `AudioSystemComponent.Service`, gabarit
  `SoundEmitterComponentAssetHandleTests.cs` ; chargeur qui rend un asset à plages dégénérées) et la méthode
  privée `CutsceneActionCoroutineFactory.PlaySound` appelée par réflexion (gabarit
  `CutsceneActionCoroutineFactorySoundAssetHandleTests.cs`, action à volume 0,5) → paramètres reçus = surcharge
  puis tirage. Zéro allocation : 1 000 `PlaySound` **acceptés** avec variations (voix arrêtée dans la boucle ou
  capacité suffisante) après 20 tours de chauffe, fenêtre `AllocationWindow.Start()` ; si la même boucle sans
  variation alloue déjà, mesurer et comparer A/B (règle O20) au lieu d'affaiblir la borne. Deux solutions sans
  erreur ni avertissement nouveau dans les fichiers touchés ; suite complète verte ; `git diff 6f06298f --
  CasaEngine/Framework/Audio/AudioVoiceParameters.cs CasaEngine/Framework/Audio/IAudioBackend.cs
  CasaEngine/Framework/Audio/Backends CasaEngine/Framework/Audio/Software` vide.
- Commit : `feat(audio): draw random variations when playing a sound asset`
- Note de validation (2026-10-06) : `SoundVariation.Draw` (boucles `for`, sans allocation, valeurs du hasard
  bornées) et `SoundVariationDraw.ApplyTo` (tirage neutre = entrée rendue telle quelle) ; `PlaySound` tire puis
  résout le fichier tiré et applique le tirage sur les paramètres surchargés. 34 tests, dont les chemins réels
  `SoundEmitterComponent.Play()` (volume 0,8 × 0,5 × 0,5 = 0,2 ; pitch 0,1 + 0,25 + 0,25 = 0,6) et
  `CutsceneActionCoroutineFactory.PlaySound` par réflexion. Allocation : 0 octet sur 1 000 lectures acceptées
  avec variations, 0 sans (même boucle). Un asset sans fichier principal mais avec des variations joue une
  variation (P26 : seuls les candidats non vides comptent). Deux solutions sans erreur, aucun avertissement dans
  les fichiers touchés (builds `--no-incremental`) ; 3236/3236 ; diffs vides depuis `6f06298f` sur
  `AudioVoiceParameters.cs`, `IAudioBackend.cs`, `Backends`, `Software`.

### ✅ T8.4 — Priorités de voix et vol (D6, D7, P28, P29)

- Fichiers : `CasaEngine/Framework/Audio/SoundPlaybackOverrides.cs` (`Priority`, `ResolvePriority`),
  `CasaEngine/Framework/Audio/AudioService.cs` (cœur privé de `PlayClip`, `PlaySound`, `StolenVoiceCount`,
  `VoiceEntry`, `TryFreeVoiceFor`), tests `CasaEngine.Tests/Audio/AudioServiceVoicePriorityTests.cs` (faux
  backend), `CasaEngine.Tests/Audio/Software/AudioServiceVoicePriorityOnSoftwareBackendTests.cs` (backend
  logiciel hors ligne), compléments à `SoundPlaybackOverridesTests.cs`. Ne changent pas : `IAudioBackend.cs`,
  `AudioVoiceParameters.cs`, `Backends/`, `Software/`, `Streaming/`.
- Étapes :
  1. `SoundPlaybackOverrides` : `public int? Priority { get; init; }` (constructeur et `None` inchangés) et
     `public int ResolvePriority(int assetPriority)` = `Math.Clamp(Priority ?? assetPriority, 0, SoundAsset.MaxPriority)`.
  2. `AudioService` : `StolenVoiceCount` (à côté de `RefusedVoiceCount`) ; compteur de démarrages ;
     `VoiceEntry.Priority` et `StartSequence`, remis à 0 par `Reset()`. Le corps de `PlayClip` devient un cœur
     privé qui reçoit la priorité ; `PlayClip` public (signature et noms de paramètres inchangés) l'appelle avec
     0, `PlaySound` avec `overrides.ResolvePriority(asset.Priority)`. Dans le cœur, si la priorité est > 0,
     `TryFreeVoiceFor` avant `RouteNextVoice` et `_backend.Play` ; après un `Play` réussi, l'entrée note sa
     priorité et son rang de démarrage, et `StolenVoiceCount` augmente si une victime a été libérée pour lui.
  3. `TryFreeVoiceFor(priority)` (boucles `for`, sans LINQ ni allocation) : rien si le backend est indisponible
     ou s'il reste de la place ; d'abord libérer une entrée non streamée dont l'état backend est `Stopped` (même
     test que `Update`), sans compter de vol ; sinon victime selon P29, `_backend.Stop` puis `ReleaseEntry`
     (motif de `StopVoicesOwnedBy`) ; sans victime, le `Play` qui suit est refusé comme aujourd'hui.
  4. Doc XML : `PlayClip` (refus par défaut inchangé), `PlaySound` (règle de vol), `StolenVoiceCount`.
- Validation : `ResolvePriority` (null garde l'asset, 0 retire, 500 → 100, négatif → 0). Faux backend (capacité
  3 ou 4) : réservoir plein sans priorité → refus, aucun vol ; priorité 5 contre {1, 2, 2, 3} → la voix 1 meurt,
  la nouvelle vit, un vol, aucun refus ; égalité des plus basses → la plus ancienne ; priorité égale → refus ;
  voix sans priorité jamais volée, même pour 100 ; voix de `PlayStream`, de `MusicPlayer` et stéréo de repli
  jamais victimes ; voix finie non recyclée remplacée sans vol compté ; surcharge `Priority = 0` sur un asset à
  5 : ne vole pas et n'est pas volable ; `Priority = 9` sur un asset sans priorité : vole ; emplacement repris
  par `PlayClip` à 4 arguments non volable ; poignée volée : `Stop`, `SetVoiceVolume` et `FadeVoice` ignorés, la
  nouvelle voix du même index intacte ; `Play` refusé après un vol → `StolenVoiceCount` 0, `RefusedVoiceCount` 1 ;
  zéro allocation de 1 000 vols répétés après chauffe (`AllocationWindow.Start()`). Backend logiciel hors ligne
  (`new SoftwareAudioBackend(new OfflineAudioOutput(), 2)`, `service.MasterLimiter.IsEnabled = false`, clips PCM
  stéréo de niveaux constants 0,05 pour la victime, 0,10 pour la survivante, 0,20 pour la nouvelle, collection
  `ProjectEnvironmentCollection`, modèle `AudioServiceRampFadeTests.cs`) : après le vol et `Pump(480)`, la sortie
  égale (à 1e-4) celle d'un banc de référence qui ne joue que la survivante et la nouvelle, et diffère de celle
  qui inclut la victime ; dans la même image, avant le `Pump`, `FadeVoice`, `SetVoiceVolume` et `Stop` sur
  l'ancienne poignée laissent la nouvelle voix (même index d'emplacement) vivante et à son niveau ; une victime
  en fondu rampé par le backend ne transmet pas sa rampe ; une voix `PlayClipStereo` et une piste `MusicPlayer`
  qui occupent des emplacements ne sont jamais volées. Deux solutions sans erreur ni avertissement nouveau dans
  les fichiers touchés ; suite complète verte trois fois ; `git diff 6f06298f --` sur `IAudioBackend.cs`,
  `AudioVoiceParameters.cs`, `Backends`, `Software` et `Streaming` vide ; signatures publiques d'`AudioService`
  inchangées ; tests audio existants verts sans modification.
- Commit : `feat(audio): steal a lower-priority voice for a higher-priority sound`
- Note de validation (2026-10-06) : `PlayClipCore` reçoit la priorité (0 pour `PlayClip`, résolue par
  `SoundPlaybackOverrides.ResolvePriority` pour `PlaySound`) ; `TryFreeVoiceFor` recycle d'abord toutes les voix
  finies (`ReleaseEntry`, comme `Update`, sans vol compté) puis vole selon P29 ; seul `PlayClipCore` pose
  `Priority` et `StartSequence`, que `Reset()` remet à 0 (`PlayStream`, `PlayClipStereoOnBackend` et la musique
  restent à 0). 25 tests (15 sur le faux backend, 5 sur le backend logiciel hors ligne, 4 sur
  `ResolvePriority`), dont : poignée volée périmée sur le même index d'emplacement, rampe de la victime non
  transmise, voix `PlayClipStereo` et piste de musique jamais volées sous le backend logiciel. Le faux backend de
  test reçoit un interrupteur `RefusesPlay` (défaut faux) pour le cas « `Play` refusé après un vol ». Allocation :
  0 octet sur 1 000 vols. Deux solutions sans erreur, aucun avertissement dans les fichiers touchés ; 3261/3261
  quatre fois ; diffs vides depuis `6f06298f` sur `IAudioBackend.cs`, `AudioVoiceParameters.cs`, `Backends`,
  `Software`, `Streaming`.

### 🧪 T8.5 — Inspecteur de son : variations, plages et priorité (P31)

- Fichiers : `CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs`,
  `CasaEngine.Tests/Editor/SoundAssetInspectorPanelTests.cs` (nouveau). Sous-module MGUI, `GameEditor.cs` et
  dispositions de l'éditeur inchangés.
- Étapes :
  1. Sous la ligne « Audio file » : une ligne par fichier de variation (`AssetSelector` configuré comme
     `CreateAudioFileRow`, même filtre et même garde `_suppressControlCallbacks`, copie locale de l'indice dans
     la boucle) avec un bouton « Remove », puis un bouton « Add variation file ». Les boutons appellent des
     méthodes internes `AddVariationFile()` et `RemoveVariationFile(int index)` qui modifient l'asset, appellent
     `SetDirty(true)` puis reconstruisent l'inspecteur (motif de `ParticleAssetInspectorPanel.RemoveEmitter`).
  2. Sous la ligne « Pitch » : « Volume variation » (min et max dans [0, 1]) et « Pitch variation » (min et max
     dans [`MinPitch`, `MaxPitch`]), pas de 0,05, valeurs arrondies à 2 décimales à l'écriture ; « Priority »
     (0 à `MaxPriority`, pas de 1, arrondie à l'entier ; libellé : 0 = aucune, jamais volée, ne vole jamais).
     Chaque changement écrit l'asset et appelle `SetDirty(true)` en respectant `_suppressControlCallbacks`. Une
     ligne d'aide dit que variations et priorité sont ignorées pour un asset streaming.
  3. `PlayPreview` : surcharge `new SoundPlaybackOverrides(busName: AudioBusNames.Editor) { Priority = 0 }`.
     `CreatePreviewCopy` (chemin streaming) inchangé.
- Validation : tests de panneau sans GPU (modèle `AudioProfilerTests.cs` : `ContentBrowserViewTestHarness`,
  `Window.SetContent(panel.CreateContent())` ; `EngineEnvironment.ProjectPath` posé sur un dossier temporaire
  puis restauré, collection `ProjectEnvironmentCollection`) : construire le contenu ne marque pas l'asset
  modifié ; `AddVariationFile` ajoute `Guid.Empty` et marque modifié ; une sélection dans un `AssetSelector` de
  variation met à jour la bonne entrée ; `RemoveVariationFile` retire la bonne ; les champs de plage et de
  priorité écrivent l'asset (arrondis, bornés) ; `TrySaveLoadedAsset` écrit les six clés seules sans variation
  ni priorité, et les nouvelles quand elles sont posées, relues à l'identique. 🧪 pour l'auteur : aspect des
  lignes et prévisualisation dans l'éditeur hébergé (ouvrir un `.sound`, ajouter deux fichiers, régler plages et
  priorité, Save, rouvrir, Play plusieurs fois). Deux solutions sans erreur ; suite complète verte.
- Commit : `feat(editor): variation and priority rows in the sound inspector`
- Note de validation (2026-10-06) : lignes « Variation N » (sélecteur partagé avec « Audio file », même filtre et
  même garde) avec « Remove », bouton « Add variation file », « Volume variation » et « Pitch variation » (Min et
  Max, arrondis à 2 décimales), « Priority » (entier), deux lignes d'aide ; `AddVariationFile` et
  `RemoveVariationFile` internes ; prévisualisation à `Priority = 0`. 26 tests sans GPU, dont des clics réels sur
  « Add variation file » et sur le second « Remove », l'enregistrement (huit clés seules sans champ posé, nouvelles
  clés relues à l'identique) et la prévisualisation refusée sans vol quand une voix du jeu de priorité 1 tient la
  seule voix. Mutation : retirer `{ Priority = 0 }` et les arrondis fait échouer six tests. Deux solutions sans
  erreur, aucun avertissement dans les fichiers touchés ; 3287/3287. **🧪 pour l'auteur** (éditeur non lancé) :
  ouvrir un `.sound`, ajouter deux fichiers de variation, régler plages et priorité, Save, rouvrir, Play
  plusieurs fois ; vérifier que les libellés « Volume variation » et « Pitch variation » tiennent dans leur
  colonne et que les lignes d'aide ne forcent pas de défilement horizontal. Une plage inversée est acceptée
  (P25 : triée au tirage).

### 🧪 T8.6 — Démo : variations et vol de voix

- Fichiers : `CasaEngine.Demos/Demos/AudioDemo.cs`, `CasaEngine.Demos/Content/Audio/menu_click_varied.sound`
  (nouveau, GUID neuf), `CasaEngine.Demos/Content/AssetInfos.json`, `CasaEngine.Demos/Content/Content.mgcb`.
- Étapes : le nouveau `.sound` référence le même `.wav` que `menu_click.sound`, avec un facteur de volume
  [0,6 ; 1] et un pitch [−0,15 ; 0,15] ; il est déclaré aux trois endroits comme `menu_click.sound` (fichier,
  entrée `AssetInfos.json`, bloc `Content.mgcb` avec `/copy:`) ; constante d'identifiant, poignée acquise à
  l'initialisation (`TryAcquire`) et rendue au nettoyage, comme les sons existants. Touches (libres, revérifiées
  par `rg` au moment de l'exécution), placées après la garde du son de clic : V joue le son varié ; J remplit les
  voix libres de boucles de priorité 1 à faible volume (`new SoundPlaybackOverrides(isLooped: true, volume:
  0.05f) { Priority = 1 }`, possédées par le monde courant ; S les arrête) ; H joue le clic avec `Priority = 10`.
  La ligne `Voices:` affiche `StolenVoiceCount`. Le tirage de fichier n'est pas audible dans la démo (un seul
  `.wav` court) : il est couvert par les tests.
- Validation : deux solutions sans erreur ; le `.sound` arrive dans la sortie de build ; démo lancée sans
  clavier (`CASAENGINE_START_DEMO="Audio demo"`, backends `Software` puis `MonoGame`) : elle démarre et charge le
  nouveau `.sound` sans erreur au journal. 🧪 pour l'auteur : J puis Espace → refus, compteur de vols inchangé ;
  J puis H → un vol de plus et le clic joue ; V → hauteurs et volumes variés.
- Commit : `feat(demos): demo keys for sound variations and voice priorities`
- Note de validation (2026-10-06) : `menu_click_varied.sound` (`f5c920e0-ab46-45d8-81c0-feb6f481b27d`, même `.wav`
  que le clic, `variation_volume_min` 0,6, pitch ±0,15 ; `variation_volume_max` omis car égal au défaut) déclaré dans
  `AssetInfos.json` et `Content.mgcb` ; touches V, J, H (libres) ; la ligne `Voices:` affiche `stolen`. Deux
  solutions et la démo sans erreur ; aucun avertissement nouveau dans `AudioDemo.cs` (les 11 CS8632 du fichier
  préexistent). Lancement sans clavier (capture d'écran après 3 s, fermeture automatique) sous `Software` puis
  `MonoGame` : sortie 0, le nouveau `.sound` chargé, aucun avertissement ni erreur au journal, ligne
  `Voices: 0 active, 0 refused, 0 stolen` visible. **🧪 pour l'auteur**, sous les deux backends : V plusieurs fois
  (volume et hauteur varient) ; J puis Espace (refus, `stolen` inchangé) ; J puis H (`stolen` + 1, le clic joue) ;
  S arrête les boucles de J.

### ✅ T8.7 — Documentation, ADR et vérification de la tranche

- Fichiers : `docs/engine/audio-system.md` (sections « 3. L'asset `.sound` », « 4. Jouer un son », « 6.
  Composant d'entité », « 7. Cutscenes », « 8. Play-in-editor », « 9. Limites connues », « 10. Évolutions
  prévues », « 11. Démo »), `docs/decisions/0063-…md` (numéro revérifié sur toutes les branches), note de statut
  d'ADR-0002, `docs/decisions/README.md`, `docs/README.md` si l'entrée audio change, ce plan, `ai-agent/README.md`.
- Étapes : doc (français) : nouvelles clés et exemple JSON, forme relative et bornes, écriture seulement si
  posée ; règle de composition et `VariationRandom` ; vol, `StolenVoiceCount` et `SoundPlaybackOverrides.Priority` ;
  l'émetteur et l'action de cinématique composent sans changer ; la prévisualisation joue sans priorité ;
  limites : refus par défaut, vol seulement pour une priorité plus haute, voix sans priorité et streamées jamais
  volées, un `Play` refusé après un vol perd la victime, une boucle de basse priorité peut être volée (l'émetteur
  le voit par `IsPlaying`), pas de voix virtuelles ni de délai ni de séquence, premier chargement d'un fichier de
  variation sur le thread de jeu puis clip gardé, fichiers de variation à cataloguer dans `AssetInfos.json` ;
  retirer « Variations aléatoires » des évolutions ; touches V, J, H. ADR-0063 (anglais) : P24 à P31,
  alternatives écartées, conséquences (Alundra inchangée, sérialisation additive sans migration, `ApplyTo` garde
  région et multiplicateur, aucune priorité par `PlayClip`). ADR-0002 : note de statut (variations ajoutées par
  ADR-0063). Plan : notes, O17 résolu, D5, D6, D7 et D14 appliquées, ligne de `ai-agent/README.md`.
- Validation : vérificateur frais **CONFIRMED** (déclencheur : sérialisation d'un format d'asset) sur : (1) un
  `.sound` sans les nouveaux champs se charge et se resauvegarde clé pour clé à l'identique ; (2) l'émetteur et
  la cinématique composent avec le tirage ; (3) par défaut le réservoir plein refuse ; un vol n'arrive que pour
  une priorité explicitement plus haute (la plus basse, puis la plus ancienne) ; voix sans priorité et streamées
  jamais volées ; (4) Alundra inchangée (diffs vides depuis `6f06298f` sur `IAudioBackend.cs`,
  `AudioVoiceParameters.cs`, `Backends`, `Software`, `Streaming` ; signatures publiques d'`AudioService`
  conservées) ; (5) aucune allocation dans le tirage ni dans le vol. Au plus cinq passes de correction pour un
  P1 ou un P2. La note dit ce que l'auteur teste (build des deux solutions, inspecteur, touches de la démo) et
  que `Alundra.Tests` ne tourne pas contre ce worktree (Alundra référence le moteur de son propre checkout) :
  il se lance quand ce checkout contient la tranche.
- Commit : `docs(audio): document sound variations and voice priorities`
- Note (2026-10-06) : `audio-system.md` (vue d'ensemble, §3 nouvelles clés, exemple, bornes et tolérance, §4
  composition, `VariationRandom`, vol de voix et `StolenVoiceCount`, §6 et §7 composition, §8 prévisualisation sans
  priorité, §9 limite de voix et variations, §10 puce retirée, §11 touches V, J, H), ADR-0063 (aucune 0063 sur les
  branches locales), note de statut d'ADR-0002, index des ADR et `docs/README.md` (commit `df5cd356`). Premier
  vérificateur frais sur `df5cd356` : **REFUTED** sur un seul P2 introduit (F1) — la théorie qui vérifiait les défauts
  sur tous les `.sound` de la démo échouait sur `menu_click_varied.sound`, ajouté par T8.6 sans relancer la suite ; le
  comportement du moteur (points 1 à 5) confirmé sur toutes les sondes. Correctif `0072b77b` (la théorie liste les
  trois fichiers d'avant S5a, le clic varié a son propre test). Vérificateur frais sur `0072b77b` : **CONFIRMED**,
  aucun P0–P2 : suite 3289/3289 quatre fois, valeurs hostiles sur les six clés sans exception, composition émetteur et
  cinématique, règles de vol, diffs vides depuis `6f06298f` sur les backends, API additive, aucune allocation. Deux avis
  P4 sans suite : une clé écrite à la main à sa valeur par défaut disparaît à la resauvegarde (voulu par P30, aucun
  fichier concerné) ; le test des types inattendus ne couvre que quatre des six clés (même lecteur pour toutes).
  **🧪 pour l'auteur** : T8.5 (inspecteur) et T8.6 (touches de la démo).

## Vague 6 — tranche S5b : écouteur, spatialisation, Doppler et paramètres de jeu (détail du 2026-10-06)

Découverte en lecture seule (brouillon passé par un contrôle contradictoire) ; les faits dont dépend la tranche
ont été revérifiés après S5a (base de la phase : le commit de clôture de S5a, noté `d83b2410` ci-dessous et remplacé
par son SHA à l'écriture).

**Faits établis.**
- Spécification OpenAL 1.1 (https://www.openal.org/documentation/openal-1.1-specification.pdf, juin 2005) :
  §3.4.1 à §3.4.6, modèles de distance (inverse, linéaire, exponentiel, chacun borné ou non ; « if the formula can
  not be evaluated then the source will not be attenuated ») ; attributs de source AL_REFERENCE_DISTANCE (défaut 1),
  AL_ROLLOFF_FACTOR (défaut 1), AL_MAX_DISTANCE (défaut MAX_FLOAT), modèle par défaut AL_INVERSE_DISTANCE_CLAMPED ;
  §3.5.2 Doppler (SS = 343,3, DF = 1, `vls`, `vss` bornés à SS/DF, `f' = f·(SS − DF·vls)/(SS − DF·vss)`) ; la
  spécification ne définit pas le panoramique d'une source mono ; elle ne fixe aucune unité de distance.
- Le moteur n'a aucune constante d'unité par mètre ; un monde 2D est en pixels, +Y vers le haut, caméra le long de
  −Z (`Camera2dComponent.cs`).
- Le mixeur logiciel calcule `Step = SourceRatio · 2^pitch · RateMultiplier` (`SoftwareMixer.SetParameters`) ; le
  backend MonoGame replie `log2(RateMultiplier)` dans le pitch et borne le total à ±1 octave. Pan d'une source
  mono : puissance constante ; stéréo : balance ; voix à gains explicites (`PlayClipStereo`) : pan ignoré.
- Sous le backend logiciel, une rampe de voix est interpolée à l'échantillon ; `SetVolume` y met fin ; le volume
  d'un `SetParameters` est ignoré pendant une rampe ; `SetParameters` peut attendre jusqu'à 100 ms sur une file
  pleine, `SetVolume` jamais. `SetBusGain` publie une dernière valeur sans file (entier `Volatile`).
- Motif « valeur de la prochaine voix » : `IAudioBusBackend.SetNextVoiceBus`, consommé tout en haut de
  `SoftwareAudioBackend.PlayResident` et `CreateStreamingVoice` par `TakeNextVoiceBus`, avant toute validation ; la
  génération de l'emplacement est incrémentée (`slot.Generation++`) juste avant l'envoi du démarrage.
- `AudioService` : `PlayClipCore(clip, busName, parameters, owner, priority)` (S5a) est le seul démarrage de
  `PlayClip`/`PlaySound` ; `SetVoicePan` pousse tout par `SetParameters` avec le volume replié ; aucun pitch de voix
  réglable après coup ; `ApplyGain(entry)` = `SetVolume(BackendVolume(...))` ; `BackendVolume` = volume sous la
  capacité de bus, volume × gain de bus sinon ; `VoiceEntry.Reset()` remet l'entrée à zéro.
- `SceneComponent` : `Position`/`Orientation` additionnent sans composer les rotations (faux sous un parent tourné) ;
  `WorldMatrixNoScale` compose correctement ; pour un composant qui a un parent dans une entité enfant, la racine de
  l'entité parente est appliquée deux fois (`SceneComponent.cs`, comportement existant, aussi reproduit par
  `RenderProjectionComponent`) ; un composant de scène de niveau entité (`Entity.Components`, sans parent) n'est
  pas rattaché à la racine de l'entité, et le gizmo de l'éditeur le place à sa propre matrice.
- `SceneComponent.Load` déréférence `local_transform` et `children_component` sans contrôle : l'ancienne forme
  d'un émetteur (niveau entité, sans ces clés) lèverait. L'auteur confirme qu'aucun projet hors du dépôt ne
  contient d'émetteur sauvegardé (D13) ; le dépôt n'en contient aucun.
- L'éditeur ajoute un composant de scène en enfant du composant sélectionné, sinon en racine si l'entité n'en a
  pas, sinon au niveau entité ; un `SceneComponent` reçoit `TransformComponentEditor` ; l'éditeur générique ne sait
  pas éditer un nullable comme tri-état.
- Une entité ne met à jour ses composants que si sa politique de tick le dit ; `RenderProjectionComponent` impose
  `DynamicDefault` par `IEntityPolicyDefaultsProvider`. `AudioSystemComponent` est mis à jour après le monde dans
  la même frame. Un `AudioSystemComponent` ne se construit pas sans `Game`.
- Alundra n'utilise ni `.sound`, ni `SoundEmitterComponent`, ni `SoundPlaybackOverrides` ; le seul nom
  `SoundEmitterComponent` du dépôt parent est dans un commentaire.

### Décisions de la vague 6 (arbitrages de l'agent, à confirmer par l'auteur)

| Réf | Arbitrage |
|---|---|
| P32 | **Canal de modulation par voix** : capacité optionnelle publique `IAudioVoiceModulationBackend` (même schéma que P9, P18, P22), implémentée par `SoftwareAudioBackend` seul. Par emplacement de voix, trois dernières valeurs publiées sans file et étiquetées par la génération : un gain multiplicatif [0, 1], un pan spatial (NaN = pan propre de la voix) et un rapport de vitesse [1/16, 16]. Elles sont orthogonales au volume, à ses rampes et au gain des bus ; les valeurs initiales voyagent avec le démarrage (`SetNextVoiceModulation`, comme `SetNextVoiceBus`). Sans la capacité (backend MonoGame, faux backend), **repli** : gain replié dans le volume envoyé, pan et pitch par `SetParameters` seulement quand ils changent, pitch total borné à ±1 octave. |
| P33 | **Formules de la spécification OpenAL 1.1 à la lettre** (citées, aucun code repris), plus des additions du moteur déclarées comme telles : gain de distance borné à [0, 1] ; dénominateur ≤ 0 du modèle inverse = formule non évaluable = pas d'atténuation ; rapport de Doppler borné à [0,25 ; 4] ; somme des liaisons de pitch bornée à [−1, 1] octave avant `2^somme`. |
| P34 | **Modèle de distance par asset** (`distance_model`, défaut `InverseDistanceClamped`, défaut de la spécification) et **distances par défaut de la spécification** (référence 1, maximale `float.MaxValue`, rolloff 1), en unités monde, réglées par asset. |
| P35 | **Écouteur** : `AudioListenerComponent` pousse sa pose ; **le dernier enregistré gagne** (pile), un avertissement limité quand un second s'enregistre ; le retrait (détachement ou entité désactivée) réactive le précédent ; **aucun écouteur = aucune spatialisation** (gain 1, pan propre, vitesse 1 : comportement actuel, l'éditeur hors jeu n'est pas rendu muet). C'est un **état de chaque frame** : une voix spatiale démarrée avant tout écouteur joue neutre puis se spatialise dès l'`Update` qui suit l'enregistrement d'un écouteur, et redevient neutre quand le dernier disparaît. L'écouteur impose `DynamicDefault` à son entité. |
| P36 | **Mode 2D** = distances, directions et vitesses projetées sur le plan X/Y (Z ignoré). **Pan spatial** = produit scalaire de la direction normalisée de la source avec le vecteur droit de l'écouteur (sinus de l'azimut, pas de distinction avant/arrière), appliqué par la loi de pan existante du mixeur ; le pan spatial remplace le pan de base sur les deux chemins. |
| P37 | **Doppler** : `doppler_factor` par asset (0 = désactivé, défaut 0, D11) ; vitesse du son `AudioService.SpeedOfSound` en unités monde par seconde, défaut 343,3 (défaut de la spécification, à régler par le jeu) ; vitesses dérivées de deux poses poussées successives (déplacement / temps de l'`Update`, nulle à la première pose et quand la pose n'a pas été poussée dans la frame) ; bornes de la spécification sur les projections, rapport borné par P33 ; **pas de détection de téléportation** (O32), pas de lissage. |
| P38 | **Un son spatial joué sans position** (`PlaySound`, action de cinématique, prévisualisation de l'inspecteur) joue comme un son non spatial, sans journal ; seules `PlaySoundAt` et `SetVoicePosition` spatialisent. Le mode mémorisé par la voix ne dépend que du mode de l'asset et de la présence d'une position, jamais de la présence d'un écouteur au démarrage (P35). Les liaisons de paramètres s'appliquent avec ou sans position. |
| P39 | **Paramètres de jeu** : liaison linéaire `{parameter, input_min, input_max, output_min, output_max, target}` (cible `Volume` : facteur dans [0, 1] ; cible `Pitch` : octaves dans [−1, 1]) ; entrée bornée à la plage, plage d'entrée dégénérée = marche ; un paramètre jamais écrit est neutre ; registre de 64 paramètres préalloués (recherche linéaire insensible à la casse, sans allocation après la création d'un nom), 8 liaisons au plus par son ; les pistes de musique (`MusicPlayer`) reçoivent les liaisons de leur asset (pas la spatialisation). |
| P40 | **Pose** lue dans `WorldMatrixNoScale` du composant (jamais `Position`/`Orientation`) : position = translation, avant et haut = axes transformés et normalisés. Un composant de niveau entité garde sa propre matrice, comme le gizmo de l'éditeur (O30). La double application de la racine d'une entité parente est un comportement existant : documenté, non corrigé. |
| P41 | **`SoundEmitterComponent` devient un `SceneComponent`** (D13 ; seule rupture d'API de la tranche, autorisée par D13) ; chargement tolérant par une valve additive `protected virtual bool AllowsMissingSceneData => false` de `SceneComponent.Load` (vraie pour l'émetteur : transform identité et aucun enfant quand les clés manquent ; comportement inchangé pour tous les autres composants). Le mode spatial vient de l'asset seul (O29). |
| P42 | **Sérialisation additive** du `.sound` : `spatial_mode`, `distance_model`, `reference_distance`, `max_distance`, `rolloff_factor`, `doppler_factor`, `parameter_bindings`, écrites seulement hors défaut ; lecture tolérante (énumérations par `TryParse` puis `Enum.IsDefined`, valeur inconnue = défaut avec avertissement ; liaison invalide ignorée avec avertissement ; jamais d'exception). L'inspecteur édite mode, modèle, distances, rolloff et Doppler ; les liaisons y sont seulement comptées (édition dans le fichier, O31). |
| P43 | **Visibilité** : publics `AudioSpatialMode`, `AudioDistanceModel`, `AudioListenerPose`, `AudioParameterTarget`, `AudioParameterBinding`, `IAudioVoiceModulationBackend` ; internes `AudioSpatialMath`, `AudioDoppler`, `AudioDistanceAttenuation`, `AudioGameParameterRegistry`. Positions en `System.Numerics.Vector3` (`AudioService` reste sans type MonoGame). |
| P44 | **Seuils d'envoi** côté jeu, pour n'envoyer que ce qui change : gain 0,001 ; pan 0,002 ; rapport relatif 0,0005 (constantes internes documentées). |

## Phase 9 — Tranche S5b : écouteur, spatialisation, Doppler et paramètres de jeu (D8 à D13)

Résultat attendu : un `AudioListenerComponent` fixe le point d'écoute ; un `.sound` peut être 2D ou 3D, avec un
modèle de distance d'OpenAL 1.1, un Doppler à la demande et des liaisons « paramètre de jeu → volume ou pitch » ;
`SoundEmitterComponent` est un composant de scène dont la position suit l'entité. Sous le backend logiciel, gain,
pan et vitesse passent par un canal orthogonal au volume, à ses rampes et aux bus ; sous les autres backends, par
repli. Non-objectifs : filtre par voix (D12) ; cônes, gain d'écouteur, `AL_MIN_GAIN`/`AL_MAX_GAIN`, HRTF ;
spatialisation des pistes streamées ; sons de cinématique positionnés ; réglage de projet de la vitesse du son ;
éditeur de liaisons ; surcharge du mode spatial ou du Doppler par l'émetteur (O29) ; correction de la double
application de la racine d'une entité parente ; tout changement d'`IAudioBackend`, d'`IAudioBusBackend`,
d'`AudioVoiceParameters` ou du dépôt parent. Prérequis : S5a clôturée (`d83b2410`, base des comparaisons
`git diff d83b2410 -- …` de la phase). Retour arrière : revert ; tout `.sound` existant reste en mode « aucun » et se
réenregistre à l'identique ; **une entité enregistrée après la tranche avec un émetteur en racine ou en enfant, ou
avec un `AudioListenerComponent`, ne se recharge plus dans le moteur d'avant** (l'ancienne forme reste lisible).
Approbation : D33 (AUTO), après relecture **READY** de ce détail. Budget : identique à S2.

Revue du détail (2026-10-06) : brouillon passé par un contrôle contradictoire (T8.4 du brouillon scindée en quatre
tâches, symboles définis, choix produit en O29 à O33), puis deux relecteurs frais **REVISE** (une voix spatiale démarrée
avant tout écouteur restait non spatiale ; une voix liée à des paramètres de jeu, musique comprise, n'avait pas de
valeurs de départ et jouait son premier bloc à plein gain), corrigés ; relecture de clôture **READY**.

### ✅ T9.1 — Canal de modulation par voix au thread audio (P32)

- Fichiers : `CasaEngine/Framework/Audio/IAudioVoiceModulationBackend.cs` (nouveau),
  `CasaEngine/Framework/Audio/Software/MixerVoice.cs`, `CasaEngine/Framework/Audio/Software/SoftwareMixer.cs`,
  `CasaEngine/Framework/Audio/Backends/SoftwareAudioBackend.cs`, remarque de tête d'`IAudioBackend.cs` (commentaire
  seulement), tests `CasaEngine.Tests/Audio/Software/SoftwareMixerModulationTests.cs` et
  `CasaEngine.Tests/Audio/Software/SoftwareAudioBackendModulationTests.cs` (nouveaux).
- Étapes :
  1. Interface publique `IAudioVoiceModulationBackend` : `void SetNextVoiceModulation(float gain, float pan, float rate)`
     (valeurs initiales de la prochaine voix démarrée, consommées par elle seule) et
     `void SetVoiceModulation(AudioVoiceHandle voice, float gain, float pan, float rate)` (dernières valeurs, jamais
     mises en file, jamais d'attente). Domaines documentés : gain [0, 1] (NaN = 1) ; pan [−1, 1] ou NaN = pan propre ;
     rate [1/16, `AudioVoiceParameters.MaxRateMultiplier`] (NaN ou ≤ 0 = 1).
  2. `SoftwareMixer` : trois tableaux `long[]` dimensionnés au nombre de voix dans le constructeur, valeur =
     `(génération << 32) | bits du float`, écrits par `internal void PublishVoiceModulation(int slot, int generation,
     float gain, float pan, float rate)` (assainit puis `Volatile.Write`) et lus au thread audio par `Volatile.Read`
     (même technique que `_consumedBuffers`). Aucune allocation, aucun verrou.
  3. `MixerVoice` : `ModGainApplied`, `ModGainTarget`, `ModRate`, `SpatialPanActive`, `SpatialPan`. `StartResident`
     et `CreateStreaming` les **posent toujours** (lus pour le couple emplacement/génération de la commande ;
     génération différente = gain 1, pan NaN, rate 1 ; `ModGainApplied = ModGainTarget`) avant l'appel
     `SetParameters(..., immediate: true)` : une voix ne sonne jamais d'abord à plein gain ni à la mauvaise vitesse.
  4. `RenderBlock` : pour chaque voix démarrée et non en pause, avant son rendu, relire les trois valeurs ; génération
     égale → `ModGainTarget` ; pan ou rate changé → recalcul des facteurs par `SetParameters(ref voice, voice.Volume,
     voice.Pan, voice.Pitch, false)`. `voice.Pan` reste toujours le pan propre (reçu des commandes) :
     `SetParameters` calcule le pan effectif `SpatialPanActive ? SpatialPan : pan` pour les facteurs seulement, et
     `Step = SourceRatio · 2^pitch · RateMultiplier · ModRate`. Une voix en pause garde ses valeurs et les applique à
     la reprise sans saut. **Voix streamée créée mais pas encore démarrée** : à l'application de sa commande de
     démarrage (`Start`), relire les trois valeurs publiées pour sa génération et poser `ModGainApplied = ModGainTarget`
     (sans rampe depuis 1) et `ModRate` : une valeur publiée par `SetVoiceModulation` entre la création et `Start` est
     sa valeur de départ (la file publie sa queue en libération, la valeur écrite avant l'enfilage est visible).
  5. `RenderResident` et `RenderStreaming` : hors rampe, gain de début de bloc `CurrentGain · ModGainApplied` et de fin
     `TargetGain · ModGainTarget` (la boucle par échantillon ne change pas) ; dans le chemin de rampe, le facteur de
     modulation est interpolé linéairement sur le bloc ; en fin de bloc `ModGainApplied = ModGainTarget`.
     `CurrentLeftGain`/`TargetLeftGain` et `FinishVoiceRamp` restent « pan × volume » sans le facteur. Avec (1, NaN, 1)
     toutes les multiplications valent 1,0 : sortie identique bit à bit.
  6. `SoftwareAudioBackend` implémente la capacité : `SetNextVoiceModulation` mémorise trois floats ;
     `TakeNextVoiceModulation()` appelé tout en haut de `PlayResident` et `CreateStreamingVoice`, à côté de
     `TakeNextVoiceBus()` (rien ne fuit sur la voix suivante si le Play est refusé) ; publication pour
     (emplacement, `slot.Generation`) entre `slot.Generation++` et l'envoi du démarrage ; `SetVoiceModulation` :
     emplacement valide et vivant, sinon ignoré, sans attente.
  7. Remarque d'`IAudioBackend.cs` : le contrat de base reste 2D ; la spatialisation passe par une capacité optionnelle.
- Validation : mixeur hors ligne (modèle des tests de rampe existants) : (a) valeurs initiales portées par le
  démarrage (le premier bloc est déjà au gain attendu) ; (b) dernière valeur : gain 1 puis 0,5 → rampe linéaire sur
  un bloc (1e-5) puis constante ; (c) composition volume 0,8 × bus 0,5 × gain 0,5 ; (d) rampe de voix 1 → 0 en 0,25 s
  avec gain 0,5 : enveloppe = 0,5 × rampe ; `SetVolume` pendant la rampe la termine sans toucher au gain ;
  `SetParameters` pendant la rampe garde le gain ; (e) valeur publiée pour l'ancienne génération ignorée par une
  nouvelle voix du même emplacement ; (f) pan publié : balance stéréo et puissance constante mono à −1, 0, 1
  recalculées dans le test ; NaN rend exactement le rendu du pan propre ; un `SetParameters` après le pan publié ne
  le défait pas ; voix à gains explicites : pan sans effet, gain actif ; (g) rate 2 : un clip de 480 trames finit en
  240 ; produit pitch × multiplicateur × rate ; bornes NaN, 0, 100 ; (h) voix en pause : gain appliqué à la reprise
  sans saut ; (i) **voix streamée** : gain, pan, rate 2 qui consomme les tampons deux fois plus vite ; une valeur publiée
  par `SetVoiceModulation` entre la création et `Start` (gain 0,25, rate 2) donne dès le premier bloc rendu 0,25 et la
  vitesse double, sans rampe depuis 1 ; (j) voix démarrée sans publication : gain 1 (champs posés explicitement) ; (k) invariance :
  scénario sans publication identique bit à bit à (1, NaN, 1) publié ; tests existants du mixeur inchangés ; (l) zéro
  allocation avec 64 voix dont les trois valeurs changent à chaque bloc (`AllocationWindow.Start()`). Backend :
  `SetNextVoiceModulation` consommé même quand le Play est refusé ; handle périmé ignoré ; aucune attente sur file
  saturée ; capacité absente de `NullAudioBackend`, `MonoGameAudioBackend` et du faux backend. Deux solutions sans
  erreur ni avertissement dans les fichiers touchés ; suite verte trois fois.
- Commit : `feat(audio): per-voice gain, pan and speed published as last values on the audio thread`
- Note de validation (2026-10-06) : capacité publique `IAudioVoiceModulationBackend` implémentée par
  `SoftwareAudioBackend` ; trois tableaux `long[]` étiquetés par la génération (valeur sentinelle `int.MinValue` pour un
  emplacement jamais publié), lus par `ApplyStartModulation` au démarrage et à l'application de `Start` d'une voix
  streamée pas encore démarrée, et par `RefreshModulation` avant le rendu de chaque voix démarrée non en pause ; pan
  effectif pour les facteurs seulement ; rampe : facteur interpolé sur le bloc. 30 tests (mixeur et backend), dont
  l'invariance bit à bit d'un scénario mixte sans publication et avec (1, NaN, 1), la voix streamée publiée avant
  `Start`, l'absence d'attente sur une file saturée et l'absence de la capacité sur les autres backends. Après `SetVolume`
  en pleine rampe, le volume rejoint sa nouvelle valeur sur le bloc suivant (comportement existant). Deux solutions sans
  erreur ni avertissement dans les fichiers touchés ; 3319/3319 quatre fois ; stress de 60 s : `underruns=0 gc=119`.

### ✅ T9.2 — Calcul spatial d'OpenAL 1.1, liaisons de paramètres et registre (P33, P36, P37, P39, P43)

- Fichiers : nouveaux, `CasaEngine/Framework/Audio/Spatial/` (espace de noms `CasaEngine.Framework.Audio.Spatial`) :
  `AudioSpatialMode.cs`, `AudioDistanceModel.cs`, `AudioDistanceAttenuation.cs`, `AudioListenerPose.cs`,
  `AudioSpatialMath.cs`, `AudioDoppler.cs`, `AudioParameterTarget.cs`, `AudioParameterBinding.cs`,
  `AudioGameParameterRegistry.cs` ; tests `CasaEngine.Tests/Audio/Spatial/*Tests.cs`.
- Étapes : fonctions pures, sans état de service ni allocation ; chaque formule commentée avec l'URL de la
  spécification et sa section ; visibilité de P43, doc XML (anglais) des types publics.
  1. `AudioSpatialMode { None = 0, Spatial2D = 1, Spatial3D = 2 }` ; `AudioDistanceModel { None, InverseDistance,
     InverseDistanceClamped, LinearDistance, LinearDistanceClamped, ExponentDistance, ExponentDistanceClamped }`.
  2. `AudioDistanceAttenuation.Evaluate(model, distance, referenceDistance, maxDistance, rolloffFactor)` : formules
     §3.4.1 à §3.4.6 ; non évaluable (division par zéro, dénominateur ≤ 0, résultat non fini, entrée NaN, linéaire
     avec ref = max) = 1 ; résultat borné à [0, 1] ; `None` = 1.
  3. `AudioListenerPose` (readonly struct : `Position`, `Forward`, `Up`, `Right` = `Cross(Forward, Up)` normalisé,
     repli (1, 0, 0)) ; `Create` normalise ; pose par défaut : origine, avant (0, 0, −1), haut (0, 1, 0) (§4.2.1).
  4. `AudioSpatialMath.Distance(mode, listenerPosition, sourcePosition)` (3D euclidienne ; 2D plan X/Y) et
     `Pan(mode, in listener, sourcePosition)` (P36 ; 2D : projections X/Y renormalisées, repli (1, 0) ; distance
     nulle = 0 ; résultat dans [−1, 1]).
  5. `AudioDoppler.Ratio(mode, listenerPosition, listenerVelocity, sourcePosition, sourceVelocity, speedOfSound,
     dopplerFactor)` : §3.5.2 ; 2D projeté ; bornes P33 ; DF ≤ 0, SS ≤ 0, distance nulle ou entrée non finie = 1 ;
     constantes `DefaultSpeedOfSound = 343.3f`, `MinRatio = 0.25f`, `MaxRatio = 4f`.
  6. `AudioParameterTarget { Volume, Pitch }` et `AudioParameterBinding` (classe scellée immuable : `ParameterName`,
     `Target`, `InputMin`, `InputMax`, `OutputMin`, `OutputMax` ; `Evaluate(float input)` de P39).
  7. `AudioGameParameterRegistry` (thread de jeu) : 64 emplacements ; `GetOrCreateIndex(string)` (recherche
     linéaire `OrdinalIgnoreCase`, −1 et un avertissement limité quand plein) ; `Set(int, float)` ; `Get(int)` (NaN =
     jamais écrit) ; version par paramètre, incrémentée seulement sur changement.
- Validation : valeurs recalculées dans les tests depuis la spécification : atténuation (ref 2, max 10, r 1) —
  inverse d=6 → 1/3, inverse borné d=1 → 1, d=50 → 0,2 ; linéaire d=6 → 0,5, d=50 → 0 ; exponentiel r=2 d=4 → 0,25,
  borné d=50 → 0,04 ; r=0 → 1 ; linéaire ref = max → 1 ; inverse ref 2, r 3, d 1 → 1 (dénominateur négatif) ;
  NaN → 1. Pan : écouteur à l'origine regardant −Z, source (10,0,0) → +1, (−10,0,0) → −1, (0,0,−10) → 0,
  (7,07 ; 0 ; −7,07) → 0,7071 ; écouteur tourné de 90° autour de Y : source (0,0,−10) → +1 ; 2D : source (10,0,5000) →
  distance 10, pan +1. Doppler (SS 343,3, DF 1) : source qui approche à 34,33 → 1,111111 ; qui s'éloigne → 0,909091 ;
  écouteur qui approche → 1,1 ; qui s'éloigne → 0,9 ; DF 0 → 1 ; vitesse au-delà de SS/DF → borne 4 ; 2D ignore Z.
  Liaisons : (0..10 → 1..0,5) v=5 → 0,75, v=−3 → 1, v=20 → 0,5 ; plage inversée ; plage dégénérée ; bornes de sortie.
  Registre : indices stables, casse, 65e nom → −1 avec un seul journal, `Get` d'un paramètre jamais écrit = NaN,
  version sur changement seulement, zéro allocation de `GetOrCreateIndex` sur un nom existant. Zéro allocation de
  `Evaluate`, `Pan`, `Ratio`, `Set`/`Get`. Deux solutions ; suite verte.
- Commit : `feat(audio): OpenAL 1.1 distance models, Doppler, spatial pan and game parameter bindings`
- Note de validation (2026-10-06) : neuf fichiers dans `Framework/Audio/Spatial/` (publics : deux énumérations de mode
  et de modèle, `AudioListenerPose`, `AudioParameterTarget`, `AudioParameterBinding` ; internes : atténuation, calcul
  spatial, Doppler, registre de 64 paramètres) ; formules relues dans le PDF de la spécification (§3.4.1 à §3.4.6,
  §3.5.2, §4.2.1), conformes aux faits du plan ; le modèle linéaire non borné ne fait que `min(distance, MAX)`, comme la
  spécification (au-dessous de la référence il dépasse 1, puis la borne du moteur le ramène à 1). Une liaison dont une
  sortie stockée n'est pas finie rend la valeur neutre. 61 tests, valeurs recalculées depuis la spécification, zéro
  allocation. Deux solutions sans erreur ni avertissement dans les fichiers ajoutés ; 3380/3380.

### 🧪 T9.3 — Champs spatiaux et liaisons dans le `.sound`, inspecteur (P34, P42)

- Fichiers : `CasaEngine/Framework/Audio/SoundAsset.cs`, `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`
  (`SaveSoundAsset`), `CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs`, tests `SoundAssetTests.cs`,
  `SoundAssetEditorSerializationTests.cs`, `CasaEngine.Tests/Editor/SoundAssetInspectorPanelTests.cs`.
- Étapes :
  1. `SoundAsset` : `SpatialMode` (défaut `None`), `DistanceModel` (défaut `InverseDistanceClamped`),
     `ReferenceDistance` (défaut 1, ≥ 0), `MaxDistance` (défaut `float.MaxValue`, ≥ 0 ; +∞ → `float.MaxValue`),
     `RolloffFactor` (défaut 1, ≥ 0), `DopplerFactor` (défaut 0, ≥ 0), `ParameterBindings` (liste en lecture seule,
     8 au plus, avertissement au-delà) ; setters : NaN ou négatif garde la valeur précédente.
     `CreateVoiceParameters` ne change pas.
  2. `Load` : clés de P42 ; énumérations en chaînes insensibles à la casse par `TryParse` puis `Enum.IsDefined` ;
     nombres par le lecteur tolérant de T8.2 ; `parameter_bindings` = tableau d'objets `{parameter, input_min,
     input_max, output_min, output_max, target}` ; liaison invalide (nom vide, nombre non fini, cible inconnue)
     ignorée avec avertissement nommant l'asset ; jamais d'exception.
  3. `SaveSoundAsset` : chaque nouvelle clé seulement hors défaut ; énumérations écrites par leur nom.
  4. Inspecteur, après la ligne du bus : « Spatial » (combo des trois modes), « Distance model » (combo des sept
     modèles), « Reference distance », « Max distance » (affiche « no limit » à `float.MaxValue`, sans réécrire la
     valeur tant qu'on ne la modifie pas), « Rolloff », « Doppler factor » (minimum 0) et une ligne en lecture seule
     « Parameter bindings: N (edit the .sound file) » ; garde `_suppressControlCallbacks`, `SetDirty(true)`. La
     prévisualisation reste non spatiale (P38).
- Validation : chargement de chaque champ ; défauts sur un document minimal ; mode ou modèle inconnu, chaîne
  numérique (« 7 ») → défaut avec avertissement ; liaison invalide et neuvième liaison ignorées ; setters ; aller-retour
  de tous les champs ; un asset aux défauts et les quatre `.sound` de la démo gardent exactement leurs clés ;
  inspecteur : lignes, écriture, `float.MaxValue` non réécrit par la construction. 🧪 aspect dans l'éditeur. Deux
  solutions ; suite verte.
- Commit : `feat(audio): spatial mode, distance model, Doppler factor and parameter bindings in the .sound asset`
- Note de validation (2026-10-06) : `SoundAsset` reçoit le mode spatial, le modèle de distance, les distances, le
  rolloff, le facteur de Doppler et `ParameterBindings` (`SetParameterBindings`, huit au plus) ; lecture des énumérations
  par `TryParse` puis `IsDefined` (un nombre en chaîne qui nomme une valeur définie, « 1 », est accepté ; « 7 » ne
  l'est pas), +∞ ramené à `float.MaxValue` pour les quatre réglages numériques ; écriture seulement hors défaut ;
  inspecteur : Spatial, Distance model, Reference distance et Max distance (0 à 100 000, pas 1), Rolloff et Doppler
  factor (0 à 10, pas 0,1), lignes d'aide « no limit » et nombre de liaisons ; construire l'inspecteur ne réécrit pas
  `float.MaxValue`. 31 tests. Deux solutions sans erreur, aucun avertissement dans les fichiers touchés ; 3411/3411.
  **🧪 pour l'auteur** : aspect des six lignes dans l'éditeur.

### ✅ T9.4 — Écouteur, voix spatiales et repli dans `AudioService` (P32, P35, P36, P38, P44)

- Fichiers : `CasaEngine/Framework/Audio/AudioService.cs`, tests `CasaEngine.Tests/Audio/Spatial/AudioServiceListenerTests.cs`
  et `AudioServiceSpatialVoiceTests.cs` (nouveaux, collection `ProjectEnvironmentCollection`), faux backend de test
  `CasaEngine.Tests/Audio/FakeAudioBackend.cs` (compteurs `SetParametersCount`, `SetVolumeCount`, additifs).
- Étapes :
  1. Champs : `_modulationBackend = _backend as IAudioVoiceModulationBackend` ; pile d'écouteurs (source, pose, pose
     précédente, vitesse) préallouée.
  2. API publique additive : `SetListener(object source, in AudioListenerPose pose)` (une source déjà enregistrée
     met à jour sa pose sans doublon), `RemoveListener(object source)` (retrait où qu'elle soit ; le sommet retiré
     réactive le précédent), `HasListener` ; `PlaySoundAt(SoundAsset asset, System.Numerics.Vector3 position, in
     SoundPlaybackOverrides overrides, object owner = null)` ; `SetVoicePosition(AudioVoiceHandle voice,
     System.Numerics.Vector3 position)`. Avertissement limité (`AudioLogThrottle`) quand un second écouteur s'enregistre.
     `Dispose` vide la pile.
  3. Démarrage : `PlayClipCore` reçoit en plus `in VoiceModulationStart start` (struct privée : `SoundAsset Asset`,
     null = aucune modulation ; `bool HasPosition` ; `Vector3 Position`) ; `PlayClip` passe `default` (comportement
     identique) ; `PlaySound` passe l'asset sans position ; `PlaySoundAt` avec la position. Mode mémorisé = mode de
     l'asset si une position est donnée, sinon `None` (P38) ; la présence d'un écouteur n'entre pas dans ce mode (P35) :
     elle est relue à chaque `Update`, et une voix spatiale sans écouteur reçoit des valeurs neutres. `HasModulation` est
     vrai pour toute voix de mode mémorisé non `None` (ou à liaisons, T9.6). Avant `_backend.Play`, avec la
     capacité et une modulation non neutre : `SetNextVoiceModulation(gain, pan, rate)` juste après `RouteNextVoice` ;
     sans la capacité : gain replié dans le volume envoyé, pan spatial dans les paramètres envoyés.
  4. `VoiceEntry` : `HasModulation`, `SpatialMode`, `DistanceModel`, `ReferenceDistance`, `MaxDistance`,
     `RolloffFactor`, `Position`, `HasPosition`, `SentGain`, `SentPan`, `SentRate`, `FoldedGain` (1 sous la capacité),
     `FoldedPan` (NaN = aucun), `FoldedPitchOffset` ; **chaque démarrage les écrit explicitement** (y compris
     `PlayStream` et `PlayClipStereoOnBackend`, à leurs valeurs neutres) et `Reset()` les remet à zéro.
  5. `Update` : après `AdvanceBusFades`, avancer l'écouteur ; dans la boucle des voix, entre le recyclage et le
     fondu, `UpdateModulation(entry)` seulement si `entry.HasModulation` : sans écouteur actif, valeurs neutres (gain 1,
     pan NaN, rate 1) ; sinon gain de distance et pan spatial ; envoi
     seulement au-delà des seuils de P44 ; avec la capacité, `SetVoiceModulation` (aucune file) ; sans la capacité,
     `FoldedGain`/`FoldedPan` puis `ApplyGain(entry)` si seul le gain a changé et que la voix n'est pas en fondu
     (le fondu l'applique juste après), `SetParameters(BuildBackendParameters(entry))` si le pan a changé.
  6. Repli : `BuildBackendParameters(entry)` = `BaseParameters` avec volume `BackendVolume(...) × FoldedGain`, pan
     `FoldedPan` s'il n'est pas NaN, pitch `BaseParameters.Pitch + FoldedPitchOffset` borné ; `ApplyGain` et
     `SetVoicePan` l'utilisent. Sous la capacité, rien ne change.
- Validation (backend logiciel hors ligne et faux backend ; le logiciel est comparé au **niveau rendu** gauche et
  droite recalculé dans le test par la loi de pan, le repli à `GetParameters` recalculé dans le test ; tolérance
  0,01 et seuils de P44) : (1) source à 2 × référence, modèle inverse borné → gain 0,5 des deux côtés ;
  (2) `SetVoiceVolume` puis déplacement dans la même frame ; (3) `FadeVoice` en cours pendant que la distance change :
  niveau = chronologie × gain, à un bloc près ; (4) `FadeVoice` suivi d'un déplacement et l'inverse ; (5) `CancelFade`,
  `StopWithFade`, `SetVoiceVolume` puis `CancelFade` dans la même frame (cas V1/V2 de S4) ; (6) muet de bus, `FadeBus`
  et snapshot pendant un son spatial (cas F1, N1, R1) ; (7) son lointain : le premier bloc n'est jamais à plein gain ;
  (8) Play refusé suivi d'un `PlayClip` normal : gain 1, rien n'a fui ; (9) écouteurs : retrait au sommet et au milieu,
  double enregistrement, second écouteur (un avertissement), aucun écouteur → voix neutres à la frame suivante ;
  (9 bis) **`PlaySoundAt` d'un asset 3D sans écouteur** : la voix joue neutre ; `SetListener` ensuite ; après l'`Update`
  suivant, niveau rendu (logiciel) et paramètres reçus (faux backend) montrent le gain de distance et le pan spatial ;
  retrait de l'écouteur : neutre à nouveau ;
  (10) pan : stéréo et mono ; 2D ignore Z ; (11) son spatial joué par `PlaySound` sans position : non spatial ;
  (12) seulement ce qui change est envoyé : compteurs du faux backend immobiles sur 100 frames ; (13) emplacement
  d'une voix spatiale arrêtée repris par `PlayClip` dans la même frame : gain 1, aucun envoi de modulation ;
  (14) zéro allocation de l'`Update` avec 64 voix spatiales et un déplacement par frame. Tests existants
  d'`AudioService` inchangés et verts ; deux solutions ; suite verte trois fois.
- Commit : `feat(audio): audio listener and spatial voices in AudioService`
- Note de validation (2026-10-06) : `SetListener`, `RemoveListener`, `HasListener`, `PlaySoundAt`, `SetVoicePosition`
  (API additive) ; pile d'écouteurs préallouée ; `PlayClipCore` reçoit `VoiceModulationStart` ; chaque démarrage écrit les
  champs de modulation (`WriteStartModulation`, `WriteNeutralModulation` pour `PlayStream` et `PlayClipStereoOnBackend`) ;
  `Update` : fondus de bus, écouteur, puis par voix recyclage, `UpdateModulation`, fondu ; repli : gain et pan repliés,
  un seul `SetVolume` par voix et par frame (une voix en fondu ne reçoit son gain que par le fondu). Choix de l'exécutant
  gardés : `SetVoicePosition` rend spatiale une voix d'asset spatial lancée par `PlaySound` (P38) ; sur le repli, un
  changement de pan d'une voix en fondu envoie `SetParameters` puis le `SetVolume` du fondu, tous deux au bon volume.
  88 cas de test sous le backend logiciel hors ligne (niveau rendu recalculé par la loi de pan), le faux backend (repli)
  et un faux backend avec la capacité, dont les ordres de S4 (F1, N1, R1, V1/V2), le cas « 9 bis » et le slot réutilisé ;
  mutation : retirer le repliement du gain fait échouer 17 tests ; 0 octet sur 100 frames de 64 voix spatiales déplacées.
  Deux solutions sans erreur, aucun avertissement dans les fichiers touchés ; 3499/3499 quatre fois.

### ⏳ T9.5 — Doppler et pitch de voix (P33, P37)

- Fichiers : `CasaEngine/Framework/Audio/AudioService.cs`, test `CasaEngine.Tests/Audio/Spatial/AudioServiceDopplerTests.cs`.
- Étapes : `VoiceEntry` : `DopplerFactor`, position précédente, vitesse, drapeau « position poussée dans la frame » ;
  `SpeedOfSound` (propriété, assainie > 0, défaut `AudioDoppler.DefaultSpeedOfSound`) ; vitesses de P37 (la dernière
  vitesse est gardée si le temps écoulé est ≤ 1e-6) ; rapport = Doppler (si facteur > 0) envoyé dans le `rate` du canal
  ou replié en `FoldedPitchOffset = log2(rapport)` borné ; API additive `SetVoicePitch(voice, pitch)` /
  `GetVoicePitch(voice)` (pitch de base en octaves, poussé par `SetParameters` comme `SetVoicePan`, conservé après un
  changement spatial).
- Validation : Doppler désactivé par défaut (rapport 1) ; activé : source qui approche → vitesse de lecture × 1,1111
  au logiciel (positions comptées sur un clip) et pitch replié + log2(1,1111) sur le faux backend ; pose non poussée
  dans la frame → vitesse 0 ; sous le repli le pitch total reste borné à ±1 octave (rapport 4 demandé) ; `SetVoicePitch`
  borné et conservé ; zéro allocation. Deux solutions ; suite verte.
- Commit : `feat(audio): Doppler on spatial voices and a settable voice pitch`

### ⏳ T9.6 — Paramètres de jeu (P39)

- Fichiers : `CasaEngine/Framework/Audio/AudioService.cs`, `CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs`
  (un appel entre `PlayStream` et `StartVoice`), test `CasaEngine.Tests/Audio/Spatial/AudioServiceGameParameterTests.cs`.
- Étapes : registre dans le service ; API additive `GetGameParameterIndex(string)`, `SetGameParameter(int, float)`,
  `SetGameParameter(string, float)`, `GetGameParameter(int)` ; à la création d'une voix liée, résolution des noms en
  indices une fois (tableau d'indices de 8 conservé dans l'entrée et réutilisé) ; `HasModulation` vrai pour une voix à
  liaisons ; gain = gain de distance × produit des liaisons `Volume` ; rapport = Doppler × `2^(somme bornée des liaisons
  Pitch)` ; recalcul seulement si une version de paramètre a changé ou si la voix est spatiale, **et toujours au premier
  `UpdateModulation` qui suit la création ou la liaison** ; `internal void BindSoundParameters(AudioVoiceHandle voice,
  SoundAsset asset)` appelé par `MusicPlayer.Play` entre `PlayStream` et `StartVoice` (pas de spatialisation des pistes).
  **Valeurs de départ** : le facteur des liaisons `Volume` et le rapport des liaisons `Pitch` font partie des valeurs
  de départ de la voix, jamais une rampe depuis le plein gain : pour `PlaySound`/`PlaySoundAt`, par le chemin de T9.4
  étape 3 (`SetNextVoiceModulation` avec la capacité ; sans elle, volume et pitch repliés dans les paramètres du `Play`) ;
  pour `BindSoundParameters`, avant `StartVoice` : avec la capacité, `SetVoiceModulation` sur la voix créée (sa valeur
  de départ, T9.1 étape 4) ; sans elle, volume et pitch repliés envoyés par `SetVolume`/`SetParameters` avant le
  démarrage.
- Validation : non écrit = neutre ; écrit change volume et pitch des deux côtés ; entrée bornée ; somme de pitch bornée ;
  **valeurs de départ, sous le backend logiciel (niveau rendu) et le faux backend (paramètres reçus)** : (1) `PlaySound`
  sans position d'un asset dont une liaison `Volume` vaut 0,25 : premier bloc rendu à 0,25 ± 0,01, et le premier `Play`
  du faux backend porte déjà 0,25 × volume ; (2) piste de musique avec la même liaison, sans fondu d'entrée : premier bloc
  après `StartVoice` à 0,25, et le faux backend reçoit le volume replié avant `Start` ; (3) même chose avec une liaison
  `Pitch` : premier bloc déjà à la vitesse liée ; piste de musique liée sous fondu d'entrée et fondu enchaîné ; 65e paramètre refusé ; `SetGameParameter(string)` sans
  allocation après création ; zéro allocation de l'`Update` avec 64 voix liées. Deux solutions ; suite verte.
- Commit : `feat(audio): game parameters bound to sound volume and pitch`

### ⏳ T9.7 — Sondes d'ordre et fuzz logiciel contre repli

- Fichiers : test `CasaEngine.Tests/Audio/Spatial/AudioServiceModulationOrderTests.cs` (nouveau) ; correctifs dans
  `AudioService.cs` seulement si un écart est trouvé.
- Étapes : fuzz à graines fixes (300 graines, quatre rythmes d'`Update`) mélangeant `SetVoiceVolume`, `FadeVoice`,
  `CancelFade`, `StopWithFade`, `SetVoicePosition`, `SetGameParameter`, `FadeBus`, muets ; même scénario sous le
  backend logiciel (niveau rendu) et le faux backend (paramètres reçus), comparés après chaque `Update`.
- Validation : erreur finale au plus égale aux seuils de P44 ; écarts transitoires d'au plus un bloc ; test de mutation
  (retirer le repliement du gain fait échouer le repli). Deux solutions ; suite verte trois fois.
- Commit : `test(audio): order probes and fuzz of spatial modulation against the fallback`

### ⏳ T9.8 — `AudioListenerComponent` et `SoundEmitterComponent` en composants de scène (D8, D13, P35, P40, P41)

- Fichiers : `CasaEngine/Framework/Scene/Entities/Components/AudioListenerComponent.cs` (nouveau),
  `AudioScenePose.cs` (nouveau, `internal static`), `SoundEmitterComponent.cs`, `SceneComponent.cs` (valve de
  chargement), `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs` (`SaveSoundEmitterComponent`), tests
  `CasaEngine.Tests/Audio/AudioListenerComponentTests.cs` (nouveau), `SoundEmitterComponentTests.cs`,
  `SoundEmitterComponentSpatialTests.cs` (nouveau).
- Étapes :
  1. `SceneComponent.Load` : valve `protected virtual bool AllowsMissingSceneData => false` (P41).
  2. `AudioScenePose.GetWorldPose(SceneComponent, out Vector3 position, out Vector3 forward, out Vector3 up)` depuis
     `WorldMatrixNoScale` (P40), conversion `System.Numerics` sans allocation.
  3. `AudioListenerComponent` : `SceneComponent`, `IEntityPolicyDefaultsProvider` (`DynamicDefault`),
     `[DisplayName("Audio Listener")]`, constructeurs et `Clone` ; `InitializeWithWorld` : service lu comme
     l'émetteur, `SetListener(this, pose)` si l'entité est active ; `Update` : `base.Update(elapsedTime)` d'abord,
     puis repousse la pose seulement si elle a changé ; `OnEnabledValueChange` : retrait quand l'entité est désactivée,
     réenregistrement sinon ; `Detach` : `RemoveListener(this)` puis `base.Detach()` ; petite boîte englobante autour
     de la pose (modèle `PlayerStartComponent`) ; point d'injection `internal void BindServiceForTests(AudioService)`.
  4. `SoundEmitterComponent` : base `SceneComponent`, `AllowsMissingSceneData` vrai, remarque « on purpose » retirée ;
     `Play` : asset streaming → chemin actuel ; mode de l'asset `None` → `PlaySound` actuel ; sinon `PlaySoundAt`
     (position d'`AudioScenePose`) ; `Update` : `base.Update(elapsedTime)` d'abord, puis `SetVoicePosition` seulement
     quand la position change et que la voix vit ; petite boîte englobante ; `Detach` arrête la voix comme avant ;
     `internal void BindServicesForTests(AudioService service, SoundAsset asset)`.
  5. `SaveSoundEmitterComponent` : écrit d'abord le transform et les enfants comme un `SceneComponent`, puis les clés
     existantes.
- Validation : (a) pose sous un parent tourné de 90° autour de Y à (10, 0, 0), enfant en (1, 0, 0) → position
  (10, 0, −1), avant (−1, 0, 0) (le test documente l'écart avec `SceneComponent.Position`) ; échelle non uniforme sans
  effet ; pose dans une entité enfant documentée telle que le moteur la calcule ; (b) ancienne forme figée (JSON écrit à
  la main : id, name, type, `sound_asset_id`, `play_on_start`, `bus_name`, `volume_override`, `pitch_override`,
  `is_looped_override`) chargée par `Entity.Load` : aucune exception, champs gardés, transform identité ; un autre
  composant sans `local_transform` lève toujours ; (c) aller-retour d'un émetteur en racine, en enfant et au niveau
  entité, et d'un `AudioListenerComponent` ; tests existants de l'émetteur verts ; (d) `Clone` ; (e) émetteur spatial
  lié : gain de distance à la création, suit l'entité déplacée, rien d'envoyé à l'arrêt, `Detach` arrête la voix ;
  émetteur dont l'asset est en mode `None` : chemin `PlaySound` inchangé ; (f) écouteur : pose à l'enregistrement,
  `Update` sur changement seulement, `Detach` et désactivation retirent, deux écouteurs ; tick forcé avec un
  `StaticModelComponent` ; un émetteur `PlayOnStart` initialisé **avant** un `AudioListenerComponent` d'une autre entité
  est spatialisé dès le premier `Update` qui suit l'enregistrement de l'écouteur ; (g) boîte englobante d'une entité sans émetteur inchangée, et mesure avec un émetteur
  enfant éloigné. 🧪 éditeur : ajouter un émetteur et un écouteur, les déplacer au gizmo, sauvegarder, recharger.
  Deux solutions ; suite verte.
- Commit : `feat(audio): audio listener component and a scene-component sound emitter`

### ⏳ T9.9 — Démo : son spatial, Doppler et paramètre de jeu

- Fichiers : `CasaEngine.Demos/Demos/AudioDemo.cs`.
- Étapes : un `SoundAsset` créé en code (copie du clic en boucle, `SpatialMode = Spatial3D`, distances explicites) joué
  par `PlaySoundAt` ; écouteur posé à l'origine par `SetListener` ; touche O : orbite on/off (rayon et vitesse en
  unités par seconde) ; K : Doppler on/off sur ce son ; une touche libre (vérifiée par `rg`) : paramètre de jeu lié au
  volume et au pitch ; lignes d'état (mode, distance, gain, pan, rapport) ; stress (G) inchangé.
- Validation : deux solutions ; lancement sans clavier sous `Software` puis `MonoGame` (capture, journal propre) ;
  stress de 60 s sous `Software` : 0 sous-alimentation. 🧪 écoute de l'auteur.
- Commit : `feat(demos): spatial sound, Doppler and game parameter keys`

### ⏳ T9.10 — Documentation, ADR et vérification de la tranche

- Fichiers : `docs/engine/audio-system.md` (nouvelle section « 5 quater. Spatialisation, Doppler et paramètres de
  jeu », §1 bis lois de pan, §3, §6, §9, §10, §11), `docs/decisions/0064-…md` (numéro revérifié sur toutes les
  branches), statut d'ADR-0001 (« 2D only » remplacé en partie), index, `docs/README.md`, ce plan, `ai-agent/README.md`.
- Étapes : doc en français : écouteur et règles, modes, modèles et formules citées (URL), unités (distances et vitesses
  en unités monde, `SpeedOfSound` en unités par seconde), Doppler et additions du moteur, liaisons (format exact et
  exemple), composition (canal orthogonal, repli, ±1 octave sous MonoGame), pose des composants, limites (pistes non
  spatialisées, cinématiques non spatiales, pas de cône ni gain d'écouteur, pas de téléportation, double application de
  la racine d'une entité parente, retour arrière avec perte pour les entités enregistrées après la tranche). ADR
  (anglais) : P32 à P44, D8 à D13 rappelées, alternatives écartées, conséquences.
- Validation : vérificateur frais **CONFIRMED** sur : sondes d'ordre de S4 rejouées sur le canal, mutation des
  correctifs, formules contre le PDF de la spécification, ancienne forme de l'émetteur, absence d'allocation, API
  additive hors la classe de base de `SoundEmitterComponent` (D13). Au plus cinq passes de correction pour un P1 ou P2.
- Commit : `docs(audio): document spatial audio, Doppler and game parameters`

---

## Réponses de l'auteur du 2026-10-06

L'auteur a répondu à O11, O12, O13, O16 (avec O4 et X5), O19, O23, O24, O25 et T2.6 : décisions D5 à D30
ci-dessus, consignées dans l'ADR-0061. Précisions du même jour : D31 (musique en WAV, X2/X4 non lancées), D32 (limiteur
actif par défaut avec un réglage de projet pour le couper), D33 (exécution en AUTO). Restent ouverts :
les autres choix de O22 (revus par l'auteur après écoute) et O26 (coup d'œil au panneau Audio). Les tranches débloquées (S5, S6b, tables
du SPU) auront chacune leur détail, relu, avant exécution.

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
| O11 | **Question à l'auteur** — Ogg en streaming : NVorbis 0.10.4 alloue environ 41 Ko par lecture de 4 096 trames (≈ 444 Ko/s en 44,1 kHz stéréo), ce qu'AGENTS.md §9.3 interdit pour le streaming d'assets. Options : exception documentée par une ADR (le décodage tourne sur le travailleur de S2, hors des threads de jeu et audio), ou décodeur adapté/forké sans allocation, ou rester en Ogg résident. Aucun consommateur actuel (Alundra joue ses musiques en clips résidents). **Réponse de l'auteur (2026-10-06) : D28.** | S3 (en pause) |
| O12 | **Question à l'auteur** — tables de constantes matérielles du SPU : les 5 couples de coefficients des filtres ADPCM, les coefficients du filtre FIR de la réverbération et la table gaussienne de 512 entrées. psx-spx en est la seule source et ne déclare pas de licence (ce sont des données mesurées sur le matériel, mais la décision de les reprendre revient à l'auteur). En attendant, X1 les reçoit de l'appelant (`PsxSpuHardwareTables`, P17) et n'en livre aucune : le SPU est complet et testé, mais pas utilisable en jeu tant que la question n'est pas tranchée. **Réponse de l'auteur (2026-10-06) : D25.** | X1, X4 |
| O13 | **Question à l'auteur** — MP3, FLAC, Opus : nouvelles dépendances (NLayer 3.0.0 MIT ; Concentus 2.2.2 BSD-3 + Concentus.OggFile MIT pour Opus ; aucun décodeur FLAC géré maintenu trouvé). À décider avant toute intégration. **Réponse de l'auteur (2026-10-06) : D29.** | S3 (en pause) |
| O14 | **Information pour l'auteur** — le dépôt de l'analyseur (MIT) contient du code dérivé de P.E.Op.S, sous GPL (table ADSR de `SoundBin.cs`, fonction `GetAdsrRate`) : problème de licence existant, hors de ce chantier ; X1 n'en reprend rien. | hors chantier |
| O15 | Adoption par Alundra des régions de boucle (T2.4) et des voix stéréo exactes (T2.3) : travail du dépôt parent (passer `LoopStart`/`LoopEnd` de chaque ton à `PlayClipStereo`), avec son propre plan ; non fait en mode AUTO. | après S2 |
| O16 | **Question à l'auteur** — source du séquenceur SEQ/VAB (X3) : le comportement de lecture de libsnd n'existe que dans des décompilations du libsnd propriétaire de Sony (transcription de l'analyseur depuis `ALUN_CD.EXE` ; `psyz/decomp`, étiqueté MIT ; `sotn-decomp`, AGPL-3.0 avec des fichiers MIT). Laquelle peut servir de référence pour un moteur MIT, et sous quelle forme (référence de comportement réécrite, ou rien) ? Avec O4 (cadence du pilote : libsnd en mode 50 Hz chez Alundra, rendus actuels à 60 Hz ; tick sur le thread audio ou de jeu). X3 en pause. Faits utiles (O16) : la table note→pitch de 192 entrées se calcule (`floor(4096·2^(k/192))`) ; les données d'Alundra débordent cette table (piste 19), il faudra une règle documentée ; un séquenceur doit gérer plusieurs séquences en même temps (BGM et SFX de séquence) et partager les 24 voix avec les SFX directs. **Réponse de l'auteur (2026-10-06) : D22, D23 ; X5 : D24.** | X3 |
| O17 | Avis P4 du vérificateur de S2 : `SoundPlaybackOverrides.ApplyTo` (`SoundPlaybackOverrides.cs:38`) reconstruit les paramètres par le constructeur à 4 arguments et perdrait une région de boucle ou un multiplicateur ; sans effet aujourd'hui (son entrée vient de `SoundAsset`, qui n'en porte pas). À traiter si `.sound` reçoit ces champs (S5). **Résolu le 2026-10-06 par T8.1.** | ✅ |
| O18 | Avis P4 du vérificateur de S2 : le thread « CasaEngine Audio Streaming » ne redémarre pas s'il meurt sur une exception hors de son `try` (par exemple à la fermeture d'un lecteur) ; la musique s'arrêterait sans repli. Lecture du code seulement, non reproduit. | après S2 |
| O19 | **Question à l'auteur** — lectures retenues là où psx-spx est ambigu (chacune marquée `AMBIGUOUS (psx-spx)` dans le code ; les tests recalculent les formules, donc une mauvaise lecture y serait reproduite). T4.1 : (1) le « /64 » de la formule ADPCM lu comme une division entière tronquée vers zéro, pas comme un décalage arithmétique ; (2) indices de filtre 5 à 7 traités comme le filtre 0 ; (3) décalages 13 à 15 traités comme 9 (documenté pour le XA seulement) ; (4) historiques ADPCM et d'interpolation remis à zéro au key on ; (5) ENDX levé à l'arrivée sur le bloc de fin, saut et mise en sourdine après ses 28 échantillons (les 3 dernières trames sont muettes à cause du retard d'interpolation) ; (6) somme gaussienne saturée à 16 bits ; (7) interpolation cubique par défaut entre « older » et « old » (choix du moteur) ; (8) compteur du sweep remis à 0 après un pas ; (9) « / 8000h » du pas exponentiel décroissant tronqué vers zéro ; (10) sweep avancé à chaque trame, voix allumée ou non, après le calcul de la trame ; (11) échelle en deux temps `(ENVX·VOL)>>15` puis `(échantillon·Lvol)>>15`, saturation par voix puis somme. T4.2 : (12) le pas d'enveloppe suit la sortie de la trame (ENVX vaut 0 à la première trame après key on) ; (13) changements de phase évalués au début du pas : attaque → décroissance à 7FFFh, décroissance → maintien quand le niveau est ≤ (N+1)·800h, comparé avant le pas (niveau Fh : décroissance sautée) ; (14) compteur d'enveloppe remis à 0 au key on et conservé d'une phase à l'autre ; (15) VxOUTX pris comme `(échantillon·ENVX)>>15`, avant le volume de voix ; (16) PMON utilise la valeur de la voix précédente calculée dans la même trame ; (17) bruit : minuterie et niveau initiaux à 0, avancé une fois par trame avant les voix, les trois « IF Timer<0 » dans l'ordre écrit ; (18) décroissance et relâchement exponentiels lents qui calent à bas niveau quand le produit tronqué vaut 0 (formule appliquée à la lettre) ; (19) une écriture d'ADSR sur une voix qui joue s'applique au pas suivant. T4.3 : (20) rééchantillonnage : psx-spx donne les 39 coefficients mais ni leur disposition, ni l'arrondi, ni le gain ; lu comme un FIR simple à 44 100 Hz (filtré puis une trame sur deux en entrée, zéros intercalés puis filtré en sortie), décalage arithmétique, sans compensation de gain — la réverbération peut sortir environ deux fois moins fort que sur le matériel, à confirmer à l'écoute ; (21) la réverbération tourne sur la première des deux trames ; (22) gauche et droite calculées dans le même pas, comme la formule est écrite (psx-spx dit que le matériel les alterne d'un cycle à l'autre) ; (23) chaque produit décalé de 15 bits, chaque résultat intermédiaire saturé à 16 bits ; (24) le bogue de négation de vIIR = −8000h n'est pas reproduit ; (25) dLSAME, dRSAME, dLDIFF, dRDIFF lus relativement à l'adresse courante du tampon, seuls dAPF1/2 sont des déplacements depuis mAPF ; (26) historiques FIR à zéro, adresse du tampon 0 tant qu'ESA n'est pas écrit ; (27) volumes de sortie de la réverbération fixes (pas de sweep) ; (28) somme des voix envoyées saturée à 16 bits avant le FIR, chaque voix envoyée après son volume ; (29) adresses circulaires dans la zone de travail, décalages négatifs compris. Vérifiés contre psx-spx le 2026-10-06 (lecture de la page) : key on ne copie pas l'adresse de départ dans LSAX, le saut de boucle a lieu après le bloc, pseudo-code du sweep, formule gaussienne, compteur de pitch, codes de boucle 0 à 3. Une référence matérielle (O12) trancherait ces points. **Réponse de l'auteur (2026-10-06) : D26.** | X1 |
| O20 | Test instable possible : `SoftwareMixerLoopRegionTests.Render_WithRegionsAndMultipliers_DoesNotAllocate` (T2.4) a échoué une fois dans une suite complète lancée par l'exécutant de T4.2 (fichiers sans rapport), puis a passé ; non reproduit en 7 exécutions le 2026-10-06 (une suite complète, six filtrées sur les tests `DoesNotAllocate`). Hypothèse non vérifiée : une compilation JIT (OSR) sur le thread du test pendant la fenêtre mesurée. À surveiller ; si l'échec revient, mesurer ce qui alloue avant de toucher au test. Même schéma vu une fois pendant T4.3 sur le nouveau test d'allocation de la réverbération (6 408 octets, puis cinq passages verts) : l'exécutant y a ajouté 50 rendus de chauffe avant la mesure. Deux tests touchés une fois chacun : la cause reste à mesurer. **Résolu le 2026-10-06** : pendant T4.4, les tests d'allocation du SPU ont échoué plusieurs fois (1 016 à 5 896 octets) ; un essai avec `DOTNET_TieredCompilation=0` n'a pas départagé (aucun échec sur 7 passages normaux ; les deux échecs sans tiering étaient des tests de durée). La cause est déjà documentée dans le dépôt (`CasaEngine.Tests/AllocationWindow.cs`, commit `075886f5` de la pile e19) : un GC d'arrière-plan annule le contexte d'allocation de chaque thread sans en reprendre la fin inutilisée, et ajoute jusqu'à environ 8 Ko jamais alloués au compteur du thread. Les sept mesures audio du chantier lisaient le compteur directement ; elles ouvrent désormais leur fenêtre par `AllocationWindow.Start()` (commit `test(audio): open audio zero-allocation windows with an empty allocation context`). Même famille, sur des durées : `AtTheBound_…` (T4.4, « chaque appel < 1 ms », vu à 2,4 ms une fois) et `OutputDyingWhileACommandWaitsOnAFullRing_ReturnsWithoutTheFullWait` (T2.1, borne de 5 ms, échec isolé deux fois) ; le premier juge désormais le meilleur de trois tours de pression (un appel lent par nature l'est à chaque tour), le second compare à la moitié de l'attente de 100 ms qu'il doit éviter (commit `test(audio): judge wall-clock bounds against parallel test load`). Troisième de la famille, vu une fois avant la fusion dans `main` : `DeadOutput_MutesTheBackend_AndNeverWaitsOnAFullRing` (T2.1, lot d'appels sur une sortie morte, borne de 5 ms dépassée au premier tour par la compilation JIT sous charge) ; même correction, borne à la moitié de l'attente de 100 ms (commit `test(audio): bound the dead-output call batch by half the retry wait`). | ✅ |
| O21 | Avis P4 du vérificateur de X1 (reportés, non bloquants). A1 : la fenêtre d'interpolation a un échantillon de retard sur la lecture de psx-spx « Counter.Bit12 and up indicates the current sample » (le plus récent de la fenêtre est n−1, pas n) ; non marqué AMBIGUOUS, à ajouter aux lectures de O19. A2 : relâchement au décalage 1Fh : la prose de psx-spx dit qu'il ne bouge jamais, le pseudo-code appliqué à la lettre fait un pas toutes les 0,74 s environ (le code suit le pseudo-code) ; contradiction de la source, non marquée, à trancher avec O19. A3 : `psx-spu.md` dit que « SPU unavailable » est journalisé « once », c'est une fois par fenêtre de 5 s (`AudioLogThrottle`) ; mot à corriger au prochain passage sur la page. A4 : `PsxSpuPort.Dispose` attend brièvement (comme un arrêt de voix) ; une écriture de registre soumise *avant* un téléversement peut être appliquée après lui dans le même bloc, sans effet visible puisqu'aucun setter ne lit la SPU RAM et que le rendu suit les deux (le contrat ne garantit que l'inverse, qui tient). | X1 |
| O22 | **À confirmer par l'auteur** — choix de T5.4 que la source ne tranche pas : (1) gain d'entrée de la réverbération à 1/32, valeur du moteur (les pages CCRMA ne la donnent pas ; le code d'origine de Freeverb, domaine public, n'a pas été lu) ; (2) limiteur du Master : plafonnement par échantillon ajouté au modèle de l'article (sans lui, la crête dépasse le plafond d'environ 0,12 dB) — alternatives : anticipation (look-ahead) ou attaque plus courte ; (3) le limiteur est actif par défaut à −1 dBFS sous le backend logiciel (décision P20 du plan) : tout son au-dessus de −1 dBFS est désormais réduit au lieu d'être écrêté à 0 dBFS, y compris dans Alundra — à écouter ; `service.MasterLimiter.IsEnabled = false` le coupe ; (4) un départ suit le gain propre de son bus, pas celui de ses ancêtres ; (5) niveaux de départ limités à [0, 1] ; T5.5 : (6) suiveur de crête de 50 ms devant le seuil du ducking (ajout du moteur) ; (7) le ducking lit le gain propre de la source en fin de bloc, pas celui de ses ancêtres ; (8) un snapshot applique les paramètres d'effets aussitôt, sans rampe. | S4 |
| O25 | Avis A1 du vérificateur de S4 (P3, reporté, introduit par `0d876d5f`) : recibler un fondu de bus en cours après un `Update` qui n'a rendu aucun bloc fait sauter le gain, en un échantillon, de l'écart entre la chronologie du jeu et la valeur rendue (au plus un bloc de pente ; mesuré 0,09 sur un fondu de 0,1 s) : petit clic possible sur des fondus très courts. Remède possible : raccorder la nouvelle rampe depuis la valeur rendue sur le premier bloc. **Réponse de l'auteur (2026-10-06) : D30.** | S4 |
| O26 | Avis P4 du vérificateur de S6a (reportés) : A1 le panneau « Audio » n'a pas été lancé dans l'éditeur (ancrage, indicateur de présence et dessin vérifiés par lecture et tests ; T6.2 🧪 pour l'auteur : ouvrir Windows > Audio, le fermer, le rouvrir) ; A2 le test de lecture concurrente utilise des blocs identiques et ne détecterait pas un enregistrement déchiré (la sonde du vérificateur, à signal variable, n'en trouve aucun) ; A3 `audio-system.md` et `audio-profiler-panel.md` ne répètent pas la condition de l'ADR-0060 (blocs d'au moins 1,25 ms) — sans effet avec les blocs de 10 ms par défaut. | S6a |
| O27 | **Question à l'auteur (non bloquante)** — anti-répétition du tirage de fichier (par exemple pour des bruits de pas : ne jamais rejouer le même fichier deux fois de suite). S5a tire uniformément (P26) ; un champ additif du `.sound` pourra l'ajouter plus tard si l'auteur le souhaite. | S5a |
| O28 | **Questions à l'auteur (non bloquantes)** — (1) une surcharge de priorité par émetteur et par action de cinématique (champ sérialisé à ajouter ; aujourd'hui ils jouent avec la priorité de l'asset) ; (2) l'affichage des voix volées dans le panneau Audio de l'éditeur. | après S5a |
| O29 | **Question à l'auteur (non bloquante)** — surcharge du mode spatial et du Doppler par `SoundEmitterComponent` (par exemple des énumérations à trois états « depuis l'asset / non / oui ») : S5b n'utilise que le mode de l'asset (D9 le dit par asset). | S5b |
| O30 | **Question à l'auteur (non bloquante)** — pose d'un composant de scène placé au niveau de l'entité (sans parent) : S5b garde sa propre matrice, comme le gizmo de l'éditeur (P40) ; un émetteur de l'ancienne forme, sans transform, sonne donc à l'origine (s'il est spatial). Alternative : le rattacher à la racine de l'entité pour l'audio seulement (le gizmo et le son divergeraient). Avec : l'éditeur fait d'un composant de scène ajouté à une entité sans racine sa racine, donc un émetteur peut devenir racine d'entité. | S5b |
| O31 | **Question à l'auteur (non bloquante)** — édition des liaisons de paramètres dans l'inspecteur de son (S5b les compte seulement, édition dans le fichier `.sound`, P42). | S5b |
| O32 | **Question à l'auteur (non bloquante)** — Doppler : détection de téléportation (seuil de déplacement par frame en unités monde, ou API de remise à zéro de la vitesse) et réglage de projet de la vitesse du son ; S5b n'a ni l'un ni l'autre (P37 : une téléportation donne un rapport extrême, borné, pendant une frame ; Doppler désactivé par défaut). | S5b |
| O33 | **Information pour l'auteur** — pour un composant qui a un parent dans une entité enfant, `WorldMatrixWithScale`/`WorldMatrixNoScale` appliquent deux fois la racine de l'entité parente (`SceneComponent.cs`, aussi reproduit par `RenderProjectionComponent`) : comportement existant, peut-être voulu ; l'audio en hérite, S5b ne le corrige pas. | S5b |
| O23 | **Questions à l'auteur — S5 (couche jeu), en pause.** (1) Variations aléatoires : dans le `.sound` (direction écrite dans `audio-system.md` §10 : liste de fichiers, plages de volume, pitch et délai) ou un asset « conteneur » séparé (type, chargeur, extension, sauvegarde éditeur et ADR en plus) ? (2) Priorités : par défaut, garder le refus actuel quand les 64 voix sont prises et ne voler que pour une priorité explicite plus haute (la plus basse, puis la plus ancienne) ? Les voix streamées (musique, voix stéréo) sont-elles toujours protégées ? Faut-il des voix virtuelles (reprise à la position écoulée, seulement possible sous le backend logiciel) ? (3) Écouteur et atténuation : qui fournit la pose de l'écouteur (composant `AudioListenerComponent` poussé dans `AudioService`, ou la caméra active) ; 2D, 3D ou les deux ; modèle d'atténuation (proposition : les modèles de distance de la spécification OpenAL 1.1, source citée) ; drapeau 3D par asset ? (4) Doppler actif par défaut ou sur demande (formule de la spécification OpenAL 1.1, aucun code repris) ? (5) Paramètres de jeu (type RTPC) : syntaxe de liaison dans le `.sound` et cibles (volume, pitch ; un filtre par voix demanderait un nouvel étage du mixeur) ? (6) `SoundEmitterComponent` : devenir un `SceneComponent` (changement de sérialisation avec migration et chargement tolérant) ou lire la pose de `Owner.RootComponent` sans changer de type ? (7) Démarrage différé : quel handle rendre pour une voix pas encore démarrée ? **Réponses de l'auteur (2026-10-06) : D5 à D14.** | S5 |
| O24 | **Questions à l'auteur — S6b (asset du mixeur et panneau de mixage), en pause.** (1) Un seul asset de mixeur par projet (réglage de projet facultatif, vide = mixeur par défaut, comme `DialogueScreenAsset`) ou plusieurs ? Extension en camelCase comme les autres (par exemple `.audioMixer`) ? (2) Panneau de mixage éditable : ses changements restent-ils en direct seulement, ou marquent-ils l'asset comme modifié et s'y enregistrent-ils (une seule source de vérité) ? (3) Solo : sémantique (un bus en solo coupe tous les autres sauf ses ancêtres et descendants ?) et repli sous le backend MonoGame ? (4) Formes d'onde : mix de sortie, préécoute seule (prise sur le bus Editor) ou dessin du clip ? (5) Le bus Master hors de l'asset (son muet appartient au projet, ADR-0040, et Alundra réécrit son volume) ? (6) `MGSlider` alloue à chaque changement : accepter l'allocation pendant un glissement dans l'éditeur, ou modifier le sous-module MGUI ? (7) L'inspecteur de son doit-il proposer les bus du mixeur au lieu de sa liste fixe ? **Réponses de l'auteur (2026-10-06) : D15 à D21.** | S6b |

## Hors périmètre

- Le reciblage multiplateforme du moteur (D2) ; toute validation hors Windows x64.
- L'exécution des tranches S2 à S6 et X1 à X5 : chacune aura son détail et son approbation.
- SoundFlow, FMOD, Wwise et tout middleware ; la spatialisation HRTF.
- Les sorties XAudio2/FAudio (WindowsDX, plateforme Native de MonoGame, consoles).
- **Pour la tranche S1** : toute modification de `IAudioBackend`, de l'API publique
  d'`AudioService` (seuls des types nouveaux s'ajoutent) ou du dépôt parent. Les tranches suivantes
  suivent la règle de l'« Enveloppe du programme ».
