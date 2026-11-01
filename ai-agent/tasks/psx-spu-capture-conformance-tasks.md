# Plan agent IA — Conformité de `PsxSpu` aux captures SPU

Plan d'exécution du chantier « `PsxSpu` suit les captures de référence ».
Les décisions D1 → D4 ci-dessous ont été arbitrées avec l'auteur le 2026-11-01 : **ce plan les applique, il ne les rediscute pas**. Les points P1 → P6, proposés par l'agent, ont été approuvés par l'auteur le 2026-11-01.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Remplacer par des comportements mesurés plusieurs lectures marquées `AMBIGUOUS (psx-spx)` dans `PsxSpu` : arithmétique du prédicteur ADPCM, shifts 13 à 15, départ à `KeyOn`, arrondi de l'interpolation gaussienne, décroissance exponentielle de l'ADSR, seuil de decay, minutage de la première marche d'enveloppe.

Sauf l'arrondi gaussien, corrigé dans `PsxGaussianInterpolator` pour que la classe livrée suive les captures, ces écarts ne sont pas injectables.

La référence est un jeu de captures SPU publié sous MIT. Le chantier ne change pas l'interpolation par défaut et ne touche pas à la réverbe, qui n'a pas de capture.

**Effet audible attendu :** une voix au mot ADSR `1FC0000F` passe de 7FFFh à 3FFFh après l'attaque, soit -6 dB. C'est le comportement des captures. `AudioDemo.cs:1012` utilise ce mot.

## État vérifié du dépôt (2026-11-01)

- `main` et le pointeur du dépôt parent valent `3c778dd8`. Arbre : `ai-agent/README.md` modifié et deux plans non suivis, écrits par l'agent. Chaque ligne d'index part avec le T0.1 de son plan.
- ADR-0058 dit que les tests recalculent la lecture de psx-spx, sans référence matérielle (`docs/decisions/0058-*.md:48`). ADR-0061 prévoyait de juger ces lectures à l'écoute (`:51`).
- `PsxSpu.cs:553` : prédicteur par division entière. `:524-527` : shifts 13 à 15 traités comme 9. `:291-293` : historique du décodeur remis à zéro à `KeyOn`. `:285-294` : rien n'est décodé à `KeyOn`.
- `PsxSpu.cs:451` : le passage de decay à sustain se teste avant le pas, avec `<=`. `:380`, `:436` : l'enveloppe avance après la sortie de la trame.
- `PsxSpu.cs:637` : décroissance exponentielle par division tronquée, dans `StepEnvelope`, partagée avec les sweeps de volume (`:595`).
- `PsxSpuInterpolation.cs:61-64` : quatre termes décalés de 15 chacun.

**Mesure, hors de ce dépôt, le 2026-11-01.** Captures `.test.pcm` et tables ENVX du projet nugget (MIT), `nugget/tests/spu`. 31 fichiers, 27 comparables : sans `silent`, les deux saturations et `flag_end_mute`. Table de Gauss de psx-spx injectée. Sortie de voix lue par réflexion sur `_outx`.

| Variante | Résultat |
|---|---|
| Code actuel, enveloppe forcée | 20 dans ±1 LSB, 7 hors : `predictor_1` à `_4`, `state_carry`, `shift_12_15`, `sine_pitch_1500` |
| Code actuel, tables ENVX | 17 captures sur 39, 256 assertions sur 690 en échec |
| Tous correctifs de ce plan, copie jetable | 27 sur 27 **exactes**, 39 sur 39 tables, 0 échec sur 690 |

`Exactes` : 26 captures depuis `max(warmup, 15)`, `predictor_3` de l'indice 169 à 511 (343 échantillons).

Tests qui fixent l'ancien comportement, mesurés sur une copie par `dotnet test --filter` sur l'audio : 47 échecs sur 1654 (22 méthodes), 0 au départ. T1.1 : 18 cas. T1.2 : 40 cas. Seuil : 33 cas. Arrondi ADSR : 5 cas. Les listes se recouvrent.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Les écarts se corrigent dans le moteur. |
| D2 | La référence de validation est constituée des captures et tables du projet nugget (MIT) et de psx-spx. La lecture de tout code est permise. Le code est écrit de nouveau. Rien n'est copié d'un projet sous GPL, LGPL ou MPL. |
| D3 | `PsxCubicInterpolator` reste le défaut : le moteur ne livre pas de table de Gauss (ADR-0062). |
| D4 | Le moteur reste générique : ni exemple, ni nombre, ni nom tiré d'un jeu dans le code, les documents ou les tests. |

## Points validés

| Réf | Proposition |
|---|---|
| P1 | Le moteur n'embarque pas les captures. Ses tests utilisent des blocs synthétiques avec des valeurs attendues calculées à la main. Le banc de comparaison vit dans le dépôt parent, hors moteur. Ses comptes sont notés sous chaque tâche, sans citer d'autre source que les captures et psx-spx. |
| P2 | Branche `chantier/psx-spu-capture-conformance` depuis `main`, contrôle `main` = pointeur. ADR-0084 réservé. Conflit d'index résolu à la fusion. |
| P3 | L'historique du décodeur reste à zéro à `KeyOn`. Les captures le contredisent sur le premier passage d'un échantillon dont le premier bloc a un filtre non nul (`predictor_1` à `_4`), mais elles dépendent de ce qui jouait avant, et le rejeu ne le reproduit pas. Commentaire de fait, limite d'ADR-0084. |
| P4 | Shifts 13 à 15 : les captures ne jouent que les nibbles -8 et 7. La règle « négatif donne -128, sinon 0 » est étendue aux autres nibbles sur décision de l'auteur, commentaire `AMBIGUOUS` conservé sur l'extension. |
| P5 | Sweeps de volume inchangés : aucune capture n'existe. L'arrondi corrigé ne vaut que pour l'ADSR, commentaire `AMBIGUOUS` conservé, limite d'ADR-0084. |
| P6 | `flag_end_mute` : le moteur coupe 8 trames après la capture, mesuré après T1.2. Limite d'ADR-0084, car une seule capture le montre. Le moment d'ENDX, déplacé par T1.2, n'est prouvé par aucune capture : effet accepté, limite écrite. |

## Marques `AMBIGUOUS`

| Ligne | Sort | Tâche |
|---|---|---|
| `PsxSpu.cs:551` | Levée | T1.1 |
| `PsxSpu.cs:530` (filtres 5 à 7) | Levée, code déjà conforme | T1.1 |
| `PsxSpu.cs:522` | Levée pour -8 et 7, conservée pour l'extension (P4) | T1.1 |
| `PsxSpu.cs:291` | Contredite, commentaire de fait (P3) | T1.2 |
| `PsxSpu.cs:357` | Levée, la capture vaut `_outx` | T1.2 |
| `PsxSpu.cs:636` | Levée pour l'ADSR, conservée pour les sweeps (P5) | T2.1 |
| `PsxSpu.cs:436` | Levée | T2.2 |
| `PsxSpu.cs:298`, `:412`, `:458`, `:501`, `:652`, `PsxSpuInterpolation.cs:65` | Conservées, hors captures | — |

`PsxSpu.cs:438-439` décrit un comportement retiré par T2.2 : à réécrire.

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/psx-spu-capture-conformance`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : un comportement corrigé est prouvé par une capture nommée. Si aucune capture ne le prouve et qu'aucun point validé ne le couvre : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Un test existant ne change que si une capture contredit ce qu'il fixe** : le motif est écrit dans la note de la tâche.
- **Build obligatoire** avant de passer une tâche en ✅ (`dotnet build CasaEngine.MonoGame.sln`) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`, sans `--no-build`. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- **Chaque tâche de code lance `AudioDemo`** (touche G, ou `CASAENGINE_AUDIO_STRESS_SECONDS`). Sinon elle reste 🧪. Tout pilote d'entrées refuse d'agir sans handle valide, jeu au premier plan et jeu sous le pointeur.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`. La ligne d'index de l'autre plan reste hors de ce chantier : `git apply --cached` d'un correctif d'une ligne, jamais `git add -p`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : `Render` reste sans allocation et sans verrou.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts.
- Banc du dépôt parent, ADSR de `PsxSpu` actif, table de Gauss de psx-spx injectée : 27 captures à l'égalité, au sens défini plus haut, et 39 tables ENVX sans échec.
- `AudioDemo` : baisse attendue de 6 dB sur les voix au mot `1FC0000F`, pas d'autre écart à l'oreille de l'auteur.

---

## Phase 0 — Décision

### ✅ T0.1 — ADR-0084 et plan

- Objectif : écrire la référence retenue, ses limites et ce qu'elle remplace.
- Fichiers : `docs/decisions/0084-the-software-spu-follows-published-captures.md`, `docs/decisions/README.md`, ce plan, la ligne de ce plan dans `ai-agent/README.md`.
- Étapes :
  1. Écrire l'ADR : référence, licence, limites P3 à P6, provenance des captures. Dire « vérifié par les captures » : elles n'ont pas dérivé chaque règle, et d'autres implémentations ont été lues.
  2. Écrire les réserves : provenance d'après les commentaires seulement, `spu-adsr.c:132-138` contre `spu.c:50-53` sur `adsr_decay_shift`, goldens `adpcm_edge` sur option. `adsr_sustain_level` reste la preuve principale de l'ADSR.
  3. Marquer ADR-0058 et ADR-0061 comme amendés dans l'index.
- Validation : relecture, aucun lien cassé, `git show --stat` ne contient pas l'autre plan.
- Commit : `docs(audio): decide that the software SPU follows published captures (ADR-0084)`
- Note : plan et points approuvés par l'auteur le 2026-11-01. Le banc est dans `tools/spu-check` du dépôt parent, ce qui ferme O1. Sur le code de départ, il redonne 17 tables sur 39 et 256 contrôles sur 690 en échec.

---

## Phase 1 — Décodage et interpolation

### ✅ T1.1 — Prédicteur et shifts

- Objectif : `predictor_1` à `_4`, `state_carry` et `shift_12_15` à l'égalité, sur la plage définie plus haut.
- Fichiers : `CasaEngine/Framework/Audio/Psx/PsxSpu.cs`, `CasaEngine.Tests/Audio/Psx/PsxSpuTests.cs`, `PsxSpuDefaultTablesTests.cs`.
- Étapes :
  1. Corriger l'arithmétique de `DecodeBlock`. Les captures ne retiennent qu'une forme sur les variantes essayées.
  2. Corriger les shifts 13 à 15 selon P4.
  3. Tests synthétiques : valeurs négatives où division et décalage diffèrent, shifts 13 à 15 sur les nibbles -8 et 7.
- Validation : `dotnet test` vert, comptes du banc notés.
- Commit : `fix(audio): match the SPU ADPCM predictor to the captures`
- Note : `dotnet test` 4746/4746, `CasaEngine.MonoGame.sln` à 0 erreur. Banc à enveloppe fixe : 26 captures sur 27 à ±1 LSB, contre 20 au départ. L'égalité attend T1.2, T1.3 et la phase 2. `AudioDemo` 10 s en mode stress : underruns=0.

### ✅ T1.2 — Départ à `KeyOn`

- Objectif : la première sortie interpole les quatre premiers échantillons. Preuves : `sine_pitch_1500` et `sine_pitch_2000` à l'égalité, et les débuts de 21 captures.
- Fichiers : `PsxSpu.cs`, `PsxSpuTests.cs` (`Delay=3` en `:16` devient une avance), `PsxSpuEnvelopeTests.cs`, `PsxSpuReverbTests.cs`, `PsxSpuDefaultTablesTests.cs`.
- Étapes :
  1. Faire entrer quatre échantillons dans l'historique à `KeyOn`.
  2. Corriger le doc-comment de `KeyOn` et de `ReadEndx`, ajouter deux tests sur ENDX et sur `SetRepeatAddress` après `KeyOn` (P6).
  3. Mesurer la dernière trame non nulle de `flag_end_mute`.
- Validation : `dotnet test` vert, comptes du banc notés, `adpcm_edge_flag_*` et `loop_t*` sans régression.
- Commit : `fix(audio): start an SPU voice on its first decoded samples`
- Note : `dotnet test` 4748/4748, `AudioDemo` 10 s : underruns=0. Banc à enveloppe fixe : 27 captures sur 27 à ±1 LSB. `flag_end_mute` : dernier échantillon non nul 51 dans la capture, trame 52 dans le moteur, soit 8 trames de retard une fois le décalage de 7 retiré (P6). Tests changés parce que la capture les contredit : la trame n joue l'échantillon n + 1, et l'impulsion de réverbe passe au cinquième échantillon, le premier n'étant jamais entendu seul avec la cubique.

### ✅ T1.3 — Arrondi de l'interpolation gaussienne

- Objectif : passer de ±1 LSB à l'égalité.
- Fichiers : `CasaEngine/Framework/Audio/Psx/PsxSpuInterpolation.cs`, `PsxSpuTests.cs`.
- Étapes :
  1. Corriger `PsxGaussianInterpolator`, sans changer `IPsxSpuInterpolator`.
  2. Test à échantillons impairs où l'ancienne et la nouvelle règle diffèrent, vérifié en mutation : le test de `PsxSpuTests.cs:285` ne les distingue pas.
- Validation : `dotnet test` vert, comptes du banc notés.
- Commit : `fix(audio): match the SPU Gaussian rounding to the captures`
- Note : `dotnet test` 4749/4749. Banc à enveloppe fixe : 27 captures sur 27 exactes. `AudioDemo` lancé : il emploie la cubique, cette classe ne le touche pas.

---

## Phase 2 — Enveloppe

### ✅ T2.1 — Décroissance exponentielle de l'ADSR

- Objectif : arrondi vers le bas pour l'ADSR, sans figeage à 0FFFh. Sweeps inchangés (P5).
- Fichiers : `PsxSpu.cs`, `PsxSpuEnvelopeTests.cs`.
- Étapes :
  1. Séparer l'arrondi selon l'appelant de `StepEnvelope`, sans allocation.
  2. Tests : ADSR décroissant négatif, sweep inchangé.
- Validation : `dotnet test` vert, tables comparées, comptes notés. Les tests de sweep passent sans changement.
- Commit : `fix(audio): match the SPU ADSR exponential decrease to the captures`
- Note : `dotnet test` 4750/4750, `AudioDemo` 10 s lancé. Banc : 23 tables sur 39, 138 contrôles sur 690 en échec, contre 17 et 256. Les tests de sweep passent sans changement (P5).

### ✅ T2.2 — Seuil de decay et première marche

- Objectif : tenir 3FFFh après l'attaque, sur `1FC0000F`, et 39 tables sans échec.
- Fichiers : `PsxSpu.cs`, `PsxSpuEnvelopeTests.cs`, `PsxSpuTests.cs` (`:19-21`), `PsxSpuDefaultTablesTests.cs`, `CasaEngine.Tests/Audio/Software/SoftwareAudioBackendPsxSpuTests.cs` (`:18`, `:106`, `:361`).
- Étapes :
  1. Tester le passage à sustain uniquement après un pas de decay effectivement appliqué, jamais sur un tick sans pas : `adsr_release_linear` et `adsr_release_exponential` tombent sinon.
  2. Borne stricte : après un pas, sustain si le niveau est `< (N+1)*800h`. Preuve : `adsr_sustain_level`, mot `5FE004A0`.
  3. Faire avancer la première marche d'enveloppe une trame plus tard : débuts de 21 captures, 4 exacts aujourd'hui, 21 après. Revoir `FullEnvelopeAt`.
  4. Réécrire `PsxSpu.cs:438-439` et les commentaires « sustain au sommet ».
- Validation : `dotnet test` vert, 27 captures et 39 tables sans échec au banc.
- Commit : `fix(audio): apply the SPU decay threshold after a step`
- Note : `dotnet test` 4753/4753, `AudioDemo` 10 s : underruns=0. Banc : 27 captures exactes de bout en bout, 39 tables sur 39, 0 contrôle en échec sur 690. Débuts de captures : 23 sur 24 exacts sur 6 échantillons, contre 5 sur 24 sans la première marche retardée. Tests changés parce que les captures les contredisent : le mot `1FC0000F` tient 3FFFh et non 7FFFh, le modèle d'enveloppe suit la même règle.

---

## Phase 3 — Clôture

### 🧪 T3.1 — Documents et validation globale

- Objectif : dire ce qui est prouvé et ce qui ne l'est pas.
- Fichiers : `docs/engine/psx-spu.md`, `ai-agent/tasks/audio-modern-tasks.md` (O19), `ai-agent/README.md`, ce plan, et les commentaires qui citent l'URL morte.
- Étapes :
  1. Mettre à jour `psx-spu.md` : référence, écarts levés, limites.
  2. Remplacer `…/ps1/spu/soundprocessingunitspu/` et `…/ps1/cdr/cdromformat/` (404) par `…/soundprocessingunitspu/` et `…/cdromformat/` sur `psx-spx.consoledev.net`, dans `PsxSpu.cs:7-8`, `PsxSpuReverb.cs`, `PsxSpuHardwareTables.cs`, `PsxSpuEnvelopeTests.cs:7`, `PsxSpuDefaultTablesTests.cs` et le document.
  3. Lancer la validation globale.
- Validation : tous les points sauf l'écoute. Tâche en 🧪 jusqu'à l'écoute de l'auteur.
- Commit : `docs(audio): record the SPU capture conformance`
- Note : toutes les validations automatiques faites, voir le rapport de la branche. Reste : écoute d'`AudioDemo` par l'auteur, baisse de 6 dB attendue sur les voix au mot `1FC0000F`.

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Le banc vit-il déjà dans le dépôt parent à l'heure de T1.1 ? Sinon il faut l'y mettre d'abord. | T1.1 |

## Hors périmètre

- Réverbe, volume principal, entrées CD, IRQ, capture, sweeps de volume.
- Changement de l'interpolation par défaut, livraison d'une table de Gauss ou de captures.
- Tout pilote ou séquenceur.
