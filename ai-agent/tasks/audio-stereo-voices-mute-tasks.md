# Plan agent IA — Voix stéréo logicielles et son coupé du projet

Plan d'exécution de la partie moteur du chantier décrit dans le plan parent
`docs/plan-audio-mix-exact-muet.md` du dépôt `alundra-casaengine-project-converter` (tranches T1.1 à T1.4 ; le
consommateur est le portage d'Alundra). Les décisions ci-dessous ont été arbitrées avec l'auteur le 2026-09-25 :
**ce plan les applique, il ne les rediscute pas**. Approuvé le 2026-09-25, mode **AUTO**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

1. Jouer un clip mono sur une **voix stéréo logicielle** dont les gains gauche et droit sont exacts et modifiables
   pendant la lecture (ADR-0039).
2. **Couper le son d'un projet** par un réglage des réglages du projet, appliqué par le runtime et par l'éditeur, avec
   une propriété `AudioSystemComponent.IsMuted` (ADR-0040).

Ce que le chantier ne livre pas est dans « Hors périmètre ».

## État vérifié du dépôt (2026-09-25)

- Base : `main` `43688074` (branche `chantier/audio-mix-exact`, clone local du worktree parent). `CasaEngine.Tests`
  **1888 / 1888** ; `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` buildent sans erreur.
- Faits détaillés, avec fichier:ligne et mesures sur MonoGame décompilé : contexte des ADR-0039 et ADR-0040, et
  section « État vérifié » du plan parent.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Voix stéréo logicielle : le moteur garde les échantillons mono et nourrit une voix stéréo en flux, gains G/D appliqués à l'échantillon (ADR-0039). |
| D2 | Fréquence d'origine dans 8 000–48 000 Hz, sinon rééchantillonnage entier ; tampons d'environ 20 ms, file cible de 3, au plus 3 tampons par voix et par `Update` (ADR-0039). |
| D3 | `MonoGameAudioClip` garde le PCM 16 bits des WAV mono chargés par `SoundEffectLoader` (ADR-0039). |
| D4 | Son coupé = `ProjectSettings.IsAudioMuted`, écrit seulement s'il est vrai, appliqué au bus `Master` par le runtime à la construction d'`AudioSystemComponent` et par un abonné de l'éditeur à `ProjectLoaded` ; pas d'interface (ADR-0040). |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/audio-mix-exact`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : sinon ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build obligatoire** (`dotnet build CasaEngine.MonoGame.sln`, `dotnet build CasaEngine.Editor.MonoGame.sln`) et **tests** (`dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj` puis `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build --blame-hang-timeout 60s`).
- **Mutations en vrai** : chaque mutation listée remplace un extrait du code de production, builde, lance le filtre de tests et rend le fichier à l'octet. Elle doit faire tomber au moins un test.
- Pas d'allocation, de LINQ ni de closure dans `Update` ; API publique additive seulement ; le runtime ne dépend pas de l'éditeur.
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ni une modification sans rapport.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- Les deux solutions buildent sans erreur.
- `CasaEngine.Tests` : 1888 + les nouveaux tests, zéro échec.
- Vérificateur frais sur T2 et T3 (tranche T1.4 du plan parent) : **CONFIRMED** le 2026-09-25, sur `7968a608`, `7c017ae5`, `af5246ca`. Builds rejoués (0 erreur), `CasaEngine.Tests` 1914 / 1914 rejoué. Aucun constat P0 à P2 ; deux remarques reportées (O1, O2).
- En direct : Launcher et éditeur sur un projet coupé puis non coupé (T3).

---

## Phase 1 — Voix stéréo et son coupé

### ✅ T1 — Plan et ADR — fait le 2026-09-25

- Objectif : ce plan, ADR-0039 et ADR-0040.
- Fichiers : `ai-agent/tasks/audio-stereo-voices-mute-tasks.md`, `ai-agent/README.md`, `docs/decisions/0039-software-stereo-voices.md`, `docs/decisions/0040-project-audio-mute-setting.md`, `docs/decisions/README.md`.
- Validation : relecture ; index à jour.
- Commit : `docs(adr): record the software stereo voices and the project mute setting, and plan them`

### ✅ T2 — Voix stéréo logicielle — fait le 2026-09-25

- Objectif : D1, D2, D3.
- Fichiers : `CasaEngine/Framework/Audio/` (interface d'accès aux échantillons ; nourrisseur interne possédé et mis à jour par `AudioService`, sur le modèle de `Streaming/MusicPlayer.cs`) ; `Backends/MonoGameAudioClip.cs` ; `Assets/Loaders/SoundEffectLoader.cs` ; `AudioService.cs` ; `CasaEngine.Tests/Audio/` (dont `FakeAudioClip`, qui gagne les échantillons) ; `docs/engine/audio-system.md`.
- Contrat public (additif) :
  - `AudioVoiceHandle PlayClipStereo(IAudioClip clip, string busName, in AudioVoiceParameters parameters, float leftGain, float rightGain, object owner = null)`. `Volume` et `IsLooped` s'appliquent, `Pan` et `Pitch` sont ignorés (doc XML). Rend `None`, avec un journal limité, si le clip n'expose pas ses échantillons, si le backend ne fait pas de flux, ou s'il refuse la voix.
  - `void SetVoiceStereoGains(AudioVoiceHandle voice, float leftGain, float rightGain)` et `GetVoiceStereoGains`. Gains bornés à [0, 1], appliqués à partir du prochain tampon soumis.
  - Échantillon produit : `G = arrondi(s × leftGain)`, `D = arrondi(s × rightGain)`, saturés en 16 bits. `Volume × gain de bus` reste appliqué par le chemin existant (`ApplyGain`, volume du backend).
  - Fin d'un clip non bouclé : quand le dernier tampon est joué, la voix est arrêtée et libérée (`IsAlive` faux). `Stop`, `StopVoicesOwnedBy`, `StopAll`, `StopAllExceptBus`, `Pause`, `Resume` : comme pour toute voix ; le nourrisseur oublie les voix mortes.
  - `IAudioBackend` inchangé.
- Étapes :
  1. Tests d'abord, sur `FakeAudioBackend` :
     - échantillons G/D exacts, gains asymétriques et nuls ;
     - un changement de gain n'affecte que les tampons suivants ;
     - gain de bus 0,5 : volume du backend = volume × 0,5, échantillons inchangés ;
     - boucle ; fin et libération ;
     - clip à 3 370 Hz et à 172 610 Hz ;
     - arrêt par propriétaire ;
     - un backend dont la file ne se remplit jamais ne fait pas boucler `Update` ;
     - clip sans échantillons → `None`.
  2. Implémentation.
  3. Mutations en vrai : G et D inversés ; gain appliqué au tampon déjà en file ; volume de bus cuit dans les échantillons ; borne par `Update` retirée.
- Validation : builds ; `CasaEngine.Tests` = 1888 + n, zéro échec.
- Commit : `feat(audio): play mono clips on software stereo voices with exact left/right gains`

**Note de validation (2026-09-25).**

- **Implémentation.** Par un exécuteur ; revue en session principale, puis deux relectures indépendantes en lecture
  seule. La relecture « contrat » n'a rien trouvé. La relecture « chemin chaud et cycle de vie » a relevé un P2,
  introduit par la tâche et donc corrigé : le rééchantillonnage arrondissait l'échantillon avant le gain, puis encore
  après. Les lectures rendent maintenant un `double`, et l'unique arrondi se fait après le gain, en double précision.
  Un test le prouve (`PlayClipStereo_WhenResampling_RoundsOnlyOnceAfterTheGain`).
- **Autres corrections en session principale.**
  - Le repli du chargeur ne capture plus que `NotSupportedException` et `InvalidDataException` (§9.10 : ne jamais
    avaler une exception en silence).
  - La doc XML de `PlayClipStereo` ne nomme plus un consommateur.
  - Un message limité signale un backend sans flux.
- **Démo.** `AudioDemo` gagne la touche `B` : un bip mono synthétisé, sans nouvel asset (les WAV de la démo sont
  stéréo), joué à gauche seule, à droite seule, puis sur les deux canaux. La démo a été lancée depuis
  `CasaEngine.Demos/`, avec une capture du back-buffer : périphérique disponible, ligne `B` affichée, aucune erreur.
  L'écoute de `B` elle-même n'est pas automatisable par l'agent.
- **Vrai backend OpenAL.** Harnais hors dépôt (`scratchpad/stereo-harness`) : trois WAV réels d'Alundra, chargés par
  `SoundEffectLoader`, joués par `PlayClipStereo`, avec un changement de gain à la 5e image.
  - `sfx_0302_0` à 11 025 Hz (natif) : 2 545 ms de clip, 2 593 ms au mur, jouées en entier.
  - `sfx_0003` à 4 274 Hz (sur-échantillonné) : 216 ms de clip, 250 ms au mur.
  - `sfx_0162_1` à 172 610 Hz (sous-échantillonné) : 19 ms de clip, 62 ms au mur.
  - Aucun refus ; chaque voix est libérée à la fin.
- **Builds et tests.** `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` : 0 erreur.
  `CasaEngine.Tests` **1904 / 1904** (1888 + 16).
- **Mutations réelles** (`scratchpad/mutate.py`), toutes tuées :
  - gauche et droite inversés ;
  - changement de gain ignoré ;
  - gain de bus cuit dans les échantillons ;
  - borne par `Update` retirée (test arrêté par `--blame-hang-timeout`) ;
  - double arrondi remis.

### 🧪 T3 — Son coupé, réglage du projet — fait le 2026-09-25, contrôle éditeur en attente

- Objectif : D4.
- Fichiers : `CasaEngine/Framework/Configuration/Project/ProjectSettings.cs`, `ProjectSettingsHelper.cs` ; `CasaEngine/Framework/Application/Components/AudioSystemComponent.cs` ; un abonné `IDisposable` dans `CasaEngine.EditorServices/` ; `CasaEngine.Editor/GameEditor.cs` (création de l'abonné juste après le runtime hébergé, `:1029`, et libération) ; tests ; `docs/engine/audio-system.md`.
- Points d'attention :
  - `LoadProject` charge dans `runtimeContext?.ProjectSettings` mais lève `ProjectLoaded` avec `GameSettings.ProjectSettings` (`EditorProjectAuthoringService.cs:18-24`). Vérifier quelle instance `GameEditor` passe, et lire la valeur sur l'instance remplie.
  - Le setter `IsMuted` met à jour les deux instances de `ProjectSettings` si elles diffèrent, comme `ApplyDisplaySettings` (`CasaEngineGame.cs:180-193`).
- Étapes :
  1. Tests. La logique testable ne dépend pas d'un `Game` : une petite fonction applique un `ProjectSettings` à un `AudioMixer`, appelée par le composant et par l'abonné.
     - Aller-retour `Save`/`Load` avec la clé.
     - Projet sans la clé → faux, et `Save` d'un projet non coupé ne l'écrit pas.
     - Réglage vrai → gain effectif de `Master` à 0, volume 0 poussé aux voix vivantes par `Update` ; retour à faux → volumes restitués.
     - Setter `IsMuted` → les réglages en mémoire suivent.
     - **Preuve unique du chemin éditeur** : un abonné sur un `AudioMixer` neuf, le vrai `EditorProjectAuthoringService.LoadProject` sur un projet coupé puis non coupé (collection `ProjectEnvironmentCollection`) ; après libération, plus d'effet.
  2. Implémentation.
  3. Mutations en vrai : réglage non appliqué au bus ; clé écrite même fausse ; abonnement retiré de l'abonné ; lecture sur la mauvaise instance si les deux diffèrent. La ligne de câblage de `GameEditor` n'est couverte par aucun test : le dire dans la note.
- Validation : builds ; tests verts ; en direct, Launcher et éditeur sur un projet coupé puis non coupé. Si l'agent ne peut pas faire un contrôle en direct : 🧪 avec ce qui manque.
- Commit : `feat(audio): mute the whole mix from the project settings`

**Note de validation (2026-09-25) : 🧪, il manque le contrôle en direct de l'éditeur.**

- **Implémentation.** Par un exécuteur :
  - `ProjectSettings.IsAudioMuted`, lu avec `?? false` et écrit seulement s'il est vrai ;
  - `ProjectAudioSettings` (`Apply`, `SetMuted`), avec une ligne de journal quand un projet coupe le son ;
  - `AudioSystemComponent` applique le réglage à sa construction, et gagne `IsMuted` ;
  - `EditorProjectAudioMuteSync` dans `CasaEngine.EditorServices`, créé par `GameEditor` après `InitializeHost`.
- **Relectures.** Deux relectures indépendantes en lecture seule. La relecture « sérialisation » n'a rien trouvé. La
  relecture « séparation et cycle de vie » classait P1 l'absence de test de nullité sur `AudioSystemComponent` dans
  `GameEditor`. Scénario inatteignable, donc reclassé P4 : `CasaEngineGame.Initialize` crée ce composant sans
  condition (`CasaEngineGame.cs:366`), et son constructeur se rabat sur `NullAudioBackend`. Le test de nullité est
  ajouté quand même, par cohérence avec les accès `?.` du reste de l'éditeur.
- **Instance lue par l'abonné.** L'émetteur de `ProjectLoaded`, c'est-à-dire `GameSettings.ProjectSettings`.
  `GameEditor` appelle `LoadProject(fileName)` sans contexte, et `FromGlobals` enveloppe cette même instance.
- **Builds et tests.** Les deux solutions : 0 erreur. `CasaEngine.Tests` **1914 / 1914** (1904 + 10), dont
  `EditorProjectAudioMuteSyncTests`, qui passe par le vrai `LoadProject`.
- **Mutations réelles**, toutes tuées : `Apply` ne coupe pas le bus ; clé écrite même fausse ; chargement sans la clé
  qui garde l'ancienne valeur ; abonnement à `ProjectLoaded` retiré ; lecture sur une autre instance que celle
  chargée.
- **Runtime en direct.** `CasaEngine.Demos` : `"IsAudioMuted": true` ajouté temporairement à
  `Content/DemosGame.json` (fichier restauré, SHA-1 identique), démo audio capturée par back-buffer. Résultat :
  « Master volume 1,00 muted True gain 0,00 », tous les bus à gain 0, et « The project settings mute the Master audio
  bus » dans `log.txt`. Sans la clé : « muted False ».
- **Éditeur en direct : non observable par l'agent.** Le projet Alundra du worktree a été ouvert coupé
  (`--project`, fichier restauré à l'octet), et l'éditeur s'ouvre et se ferme proprement. Mais l'éditeur n'écrit
  aucun fichier de journal, son panneau Logs reste vide en automatisation, et son tampon de diagnostics ne reçoit pas
  les `Logs.*`. La ligne de câblage de `GameEditor` n'est couverte par aucun test ; le contrôle passe à la recette
  T5.3 du plan parent.

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Remarque A1 du vérificateur (P3, reportée) : `GetVoiceStereoGains` interroge seulement l'entrée du mélangeur, sans demander au service si la voix vit encore. Après `Stop`, `StopAll` ou `StopVoicesOwnedBy`, il rend encore `true` et les anciens gains jusqu'au `Update` suivant ; après `Dispose`, pour toujours. Correctif possible : tester `AudioService.IsAlive` dans `GetVoiceStereoGains`/`SetVoiceStereoGains`, avec un test avant `Update` et après `Dispose`. Sans effet sur le portage Alundra, qui écarte les voix mortes avant tout remix. | T2 |
| O2 | Remarque A2 du vérificateur (P4, reportée) : dans une session d'éditeur normale, `EditorProjectAudioMuteSync` s'abonne pendant le premier `ProjectLoaded` et manque cet événement. Le comportement reste juste : le constructeur d'`AudioSystemComponent` applique le réglage déjà chargé. La phrase « à chaque `ProjectLoaded` » décrit le mécanisme de façon un peu inexacte. | T3 |

## Hors périmètre

- Points de boucle, pitch sur les voix stéréo, enveloppes ADSR, 3D.
- Toute interface pour le son coupé ; persistance des volumes de bus.
- Tout changement de MGUI (un manque se consigne).
