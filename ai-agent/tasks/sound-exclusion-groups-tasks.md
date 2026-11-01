# Plan agent IA — Groupes d'exclusion de lecture dans `.sound`

Plan d'exécution du chantier « un son coupe le son en cours du même groupe ».
La décision D1 ci-dessous a été arbitrée avec l'auteur le 2026-11-01 : **ce plan l'applique, il ne la rediscute pas**. Les points P1 → P7, proposés par l'agent, ont été approuvés par l'auteur le 2026-11-01.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Un `.sound` peut déclarer un groupe d'exclusion. Lancer un son de ce groupe arrête la voix en cours du même groupe, quel que soit l'asset qui l'a lancée. Tout passe par les données, sans DLL.

## État vérifié du dépôt (2026-11-01)

- `main` et le pointeur du dépôt parent valent `3c778dd8`. Arbre : `ai-agent/README.md` modifié et deux plans non suivis, écrits par l'agent.
- Chaque `play_sound` empile une voix : aucune occurrence de `MaxInstances`, `Polyphony` ni `Retrigger` dans `CasaEngine/Framework/Audio` (`rg`).
- Le vol de voix par `Priority` n'agit que si le backend est plein (`AudioService.cs:583-631`, garde en `:585`). Il admet les voix en pause et en fondu (`:580`), et libère une voix finie non recyclée (`:594-596`).
- `AudioService.Stop` (`:918`) et `StopWithFade` (`:1309`) existent. `FadeVoice` met `IsFading` pour tout fondu (`:1195`), `CancelFade` ne touche pas `FadeCompletion` (`:1329`).
- `PlayClipCore` remplit `VoiceEntry` (`:304-310`) et lit l'asset par `start.Asset` (`:253`, `:311`, `:314`). `PlaySoundCore` résout le clip en `:697`, le bus en `:704`, puis appelle `PlayClipCore` en `:706`.
- `VoiceEntry` (`:2368`) a un `Reset` (`:2453`) à compléter pour tout nouveau champ.
- `SoundAsset.Load` lit les clés une à une (`SoundAsset.cs:240-270`). L'écriture est dans `SaveSoundAsset` (`EditorAssetJsonSerializer.cs:944`), `loop_start_sample` hors défaut seulement (`:954-957`).
- La preview normale de l'inspecteur joue l'asset lui-même sur le bus Editor (`SoundAssetInspectorPanel.cs:285-287`). `CreatePreviewCopy` (`:318`) ne sert qu'à la preview en flux (`:278`).
- L'inspecteur n'a aucune zone de texte (`rg MGTextBox` : 0). Modèle : `Animation2dAssetInspectorPanel.cs:1329-1351`.
- `FakeAudioBackend` ne couvre pas la rampe du backend logiciel. `AudioServiceRampFadeTests.cs:24-60` fait tourner un scénario sur les deux backends.
- `Projects/PixelUiSample` ne joue qu'un asset, `cursor_move.sound` (`Screens/title.uiscreen:56`, `options.uiscreen:27`). `ProjectScreenAssetTests.cs:245` fixe un seul `sound` au catalogue.
- Dernier ADR : 0082. Ce plan prend 0083.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Le groupe d'exclusion se déclare dans `.sound`. |
| D2 | Le moteur reste générique : ni exemple, ni nombre, ni nom tiré d'un jeu dans le code, les documents ou les tests. Données synthétiques. |

## Points validés

| Réf | Proposition |
|---|---|
| P1 | Clé additive `exclusion_group`, chaîne, vide par défaut. Comparaison `OrdinalIgnoreCase`, comme les bus. Le setter ramène null et les blancs à vide, et applique `Trim`. Un son du groupe arrête la voix du même groupe, quel que soit l'asset. |
| P2 | Clé additive `exclusion_fade_seconds`, nombre positif, 0 par défaut : 0 arrête net, une valeur positive arrête par `StopWithFade`. Elle se lit sur le son qui arrive. Limite : sous le repli MonoGame, le fondu avance par image. |
| P3 | L'arrêt se fait après la résolution du clip, juste avant `PlayClipCore` : un fichier manquant ne coupe rien. Si le backend est plein, l'arrêt est net même avec un fondu, sinon la nouvelle voix serait refusée. |
| P4 | Une voix n'est ignorée que si elle s'éteint déjà : `IsFading` et `FadeCompletion == Stop`, sauf si le backend est plein. Une voix en pause, y compris par le système, compte et s'arrête, comme pour le vol de voix. Une voix finie non recyclée est libérée. |
| P5 | Le groupe vaut pour tout le service : ni le propriétaire, ni le bus, ni un bus muet, ni la priorité ne protègent une voix. Une voix lancée par `PlayClip` ou `PlayClipStereo` n'a pas de groupe. Un `SoundEmitterComponent` qui rejoue coupe net sa propre voix avant la règle. |
| P6 | **Le bus Editor reste hors de la règle** : une preview ne coupe aucune voix du jeu, un son du jeu ne coupe pas une preview. Sinon la promesse de `SoundAssetInspectorPanel.cs:284` tomberait. |
| P7 | Branche `chantier/sound-exclusion-groups` depuis `main`. Boucle `for` sur `_voices`, sans allocation. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/sound-exclusion-groups`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché (`dotnet build CasaEngine.MonoGame.sln`, et `dotnet build CasaEngine.Editor.MonoGame.sln` dès que l'éditeur est touché) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, sans `--no-build`. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`. La ligne d'index de l'autre plan reste hors de ce chantier : `git apply --cached` d'un correctif d'une ligne, jamais `git add -p`.
- **Langue** : ce plan en français ; code, messages de commit et ADR en anglais ; `docs/engine/audio-system.md` suit la langue du fichier.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; sérialisation additive.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts, nouveaux tests compris.
- Smoke manuel : `PixelUiSample` au Launcher, direction tenue dans le menu : un seul son à la fois. Ce smoke prouve la coupure d'un seul asset, le test à deux assets de T1.3 prouve le reste.

---

## Phase 0 — Décision

### ✅ T0.1 — ADR-0083 et plan

- Objectif : fixer le format et la règle d'arrêt.
- Fichiers : `docs/decisions/0083-sounds-declare-an-exclusion-group.md`, `docs/decisions/README.md`, ce plan, la ligne de ce plan dans `ai-agent/README.md`.
- Étapes :
  1. Écrire l'ADR selon `docs/decisions/template.md` : clés, moment de l'arrêt, portée (P3 à P6), limites.
  2. L'ajouter à l'index.
- Validation : relecture, aucun lien cassé, `git show --stat` ne contient pas l'autre plan.
- Commit : `docs(audio): decide sound exclusion groups (ADR-0083)`
- Note : plan et points P1 à P7 approuvés par l'auteur le 2026-11-01. Aucun code touché.

---

## Phase 1 — Moteur et éditeur

### ✅ T1.1 — `SoundAsset`

- Objectif : porter et lire les deux clés.
- Fichiers : `CasaEngine/Framework/Audio/SoundAsset.cs`, `CasaEngine.Tests/Audio/SoundAssetTests.cs`.
- Étapes :
  1. Ajouter `ExclusionGroup` et `ExclusionFadeSeconds` (assainie par `SanitizeNonNegative`), avec leur remarque XML.
  2. Les lire dans `Load`, avec `ReadNumber` pour le nombre et un avertissement si `exclusion_group` n'est pas une chaîne.
  3. Tests : défauts, valeurs lues, type invalide, NaN et négatif, chaîne de blancs, espace finale.
- Validation : `dotnet test` vert.
- Commit : `feat(audio): add exclusion group fields to sound assets`
- Note : les deux `.sln` à 0 erreur, `dotnet test` 4755/4755 (avec T1.2, développée en même temps).

### ✅ T1.2 — Écriture éditeur

- Objectif : écrire les clés hors défaut seulement.
- Fichiers : `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`, `CasaEngine.Tests/Audio/SoundAssetEditorSerializationTests.cs`.
- Étapes :
  1. Écrire les deux clés hors défaut, comme `loop_start_sample`.
  2. Tests : aller-retour, absence au défaut, ordre des clés existantes inchangé.
- Validation : les deux `.sln` à 0 erreur, `dotnet test` vert.
- Commit : `feat(editor): write sound exclusion group keys`
- Note : validé avec T1.1, 4755/4755.

### ✅ T1.3 — Arrêt dans `AudioService`

- Objectif : appliquer la règle au lancement.
- Fichiers : `CasaEngine/Framework/Audio/AudioService.cs`, `CasaEngine.Tests/Audio/AudioServicePlaySoundTests.cs`, `CasaEngine.Tests/Audio/Software/AudioServiceRampFadeTests.cs`.
- Étapes :
  1. Ajouter à `VoiceEntry` un champ de groupe, écrit par `PlayClipCore` depuis `start.Asset`, remis à zéro par `Reset`.
  2. Dans `PlaySoundCore`, après le bus résolu, arrêter les voix du groupe selon P2 à P6, par une boucle `for` sans allocation. Rien si le bus résolu est Editor, et la boucle saute les voix de ce bus.
  3. Compléter la remarque XML de `PlaySound`.
  4. Tests : deux assets du même groupe, casse différente, groupes différents, groupe vide, clip manquant qui ne coupe rien, fondu sur les deux backends, voix en fondu d'arrêt, voix en fondu ordinaire arrêtée, `CancelFade` puis arrêt, voix en pause, `PauseAllExceptBus` sans relance après l'arrêt, voix finie non recyclée, backend plein (`RefusedVoiceCount` à 0), bus muet, priorité haute coupée, voix du monde coupée, `PlaySoundAt`, preview du bus Editor dans les deux sens.
- Validation : `dotnet test` vert. Vérifier par lecture qu'aucune allocation n'est ajoutée sur ce chemin pour un son sans groupe.
- Commit : `feat(audio): stop the voices of an exclusion group on play`
- Note : `dotnet build CasaEngine.MonoGame.sln` à 0 erreur, `dotnet test` 4781/4781, dont 32 tests de groupe. Tests dans `AudioServiceExclusionGroupTests.cs`, nouveau fichier, et `AudioServiceRampFadeTests.cs`. Une voix en pause s'arrête net même avec un fondu : elle est muette.

### 🧪 T1.4 — Inspecteur

- Objectif : éditer les deux champs.
- Fichiers : `CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs`, `CasaEngine.Tests/Editor/SoundAssetInspectorPanelTests.cs`.
- Étapes :
  1. Le groupe : un `MGTextBox` sur le modèle d'`Animation2dAssetInspectorPanel.cs:1329-1351`, avec sa garde.
  2. Le fondu : `CreateNumericRow`, avec une nouvelle constante de borne, générique.
  3. Compléter l'aide de la ligne `:406` : le groupe est ignoré pour un flux.
  4. Tests : édition des deux champs.
- Validation : `dotnet build CasaEngine.Editor.MonoGame.sln` à 0 erreur, `dotnet test` vert. Clic dans l'éditeur à la charge de l'auteur : 🧪 si non fait.
- Commit : `feat(editor): edit sound exclusion groups in the inspector`
- Note : `dotnet build CasaEngine.Editor.MonoGame.sln` à 0 erreur, `dotnet test` 4784/4784. Reste : clic dans l'éditeur par l'auteur.

---

## Phase 2 — Sample et documentation

### 🧪 T2.1 — Sample et documents

- Objectif : montrer la feature et la documenter.
- Fichiers : `Projects/PixelUiSample/Audio/cursor_move.sound`, `Projects/PixelUiSample/README.md`, `docs/engine/audio-system.md` (table `.sound`, liste de l'inspecteur, section preview), `docs/engine/data-driven-screens.md`, `ai-agent/README.md`.
- Étapes :
  1. Déclarer un groupe dans `cursor_move.sound`.
  2. Documenter les clés et les limites.
  3. Lancer `PixelUiSample` au Launcher. Tout pilote d'entrées refuse d'agir sans handle valide, jeu au premier plan et jeu sous le pointeur.
- Validation : le sample charge sans avertissement audio au journal. L'écoute revient à l'auteur : tâche en 🧪 jusqu'à sa recette.
- Commit : `docs(audio): document sound exclusion groups`
- Note : `PixelUiSample` lancé 8 s au Launcher, sans entrée : le son se charge, aucun avertissement au journal. `dotnet test` 4784/4784. Reste : écoute par l'auteur, direction tenue dans le menu.

---

## Vérification indépendante

Faite le 2026-11-01 sur le code, par mutation et sur les règles : aucun P0 ni P1 sur le code. Suites données dans le commit `fix(audio): bound the exclusion fade and close the review gaps` :

- `exclusion_fade_seconds` borné à `SoundAsset.MaxExclusionFadeSeconds`, 10 s : sans borne, un fondu sans fin laissait l'ancienne voix hors du groupe.
- Le champ de l'inspecteur n'a plus de limite de caractères : il affichait vide un groupe trop long. Un nom avec un saut de ligne reste affiché vide, limite écrite dans ADR-0083.
- ADR-0083 et `audio-system.md` disent les deux exceptions du code : backend plein, voix en pause.
- 13 tests ajoutés pour les mutants survivants, l'ordre des clés et le nom long.

**Écart accepté :** les statuts de T1.2, T1.3 et T1.4 sont arrivés avec le commit de T2.1, pas dans le commit de leur tâche. L'historique n'a pas été réécrit.

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | L'arrêt net crée-t-il un clic ? La discontinuité se mesure au rendu hors ligne, le jugement à l'oreille revient à l'auteur. | T1.3, T2.1 |

## Hors périmètre

- Limite d'instances par asset, priorité entre groupes, vol de voix.
- Groupes pour les flux, pour `MusicPlayer`, `PlayClip` et `PlayClipStereo`.
- Action d'arrêt d'un son par `.uiscreen`.
- Deuxième asset dans `PixelUiSample` : il casserait `ProjectScreenAssetTests.cs:245` sans rien prouver de plus que T1.3.
- Exemple ou réglage propre à un projet : le choix du groupe et du fondu reste aux données de chaque projet.
