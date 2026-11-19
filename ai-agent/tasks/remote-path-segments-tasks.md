# Plan agent IA — Le pilotage refuse tout chemin dont un segment finit par un point ou une espace

Plan né du point ouvert O4 de [origin-main-integration-2-tasks.md](archive/origin-main-integration-2-tasks.md) (constat de sa tâche T1.4). Il n'y a pas d'audit dédié : les faits sont dans « État vérifié du dépôt ».
Les décisions D1 → D3 ci-dessous ont été arbitrées avec l'auteur le 2026-11-19 : **ce plan les applique, il ne les rediscute pas**. Les propositions P1 → P12 sont à valider avec le plan.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Le contrôle des liens du pilotage de l'éditeur (ADR-0093, A3) peut être trompé par un dossier dont le nom finit par une espace ou un point : il lit les attributs d'un **autre** dossier que celui par lequel l'accès passe. T1.4 du plan d'intégration a fermé ce trou pour les quatre commandes `*.create` seulement. Ce plan le ferme **pour tout chemin** que le pilotage résout (références d'assets, entrées de catalogue, `world.open`, chemins d'automatisation, `--open-asset`), par un refus de texte placé dans le contrôle commun, avec ses tests, sa documentation, un essai dans l'éditeur lancé avant et après, une vérification indépendante, puis l'avance de `main` et de `remaster` et le push.

## État vérifié du dépôt (2026-11-19)

- **Branches.** Moteur : `main` = `origin/main` = `f82f213fc` (`git ls-remote origin main`) ; la tête de travail `9d15c42b8` y ajoute deux commits de documents (`82997fbb9` ADR-0144 et 0145 acceptées, `9d15c42b8` clôture et archivage du plan d'intégration). Jeu : `remaster` = `origin/remaster` = `0ee00f1`, pointeur `CasaEngine` = `f82f213fc`. Arbres propres hors `.serena/` et `log.txt` de l'auteur (dépôt du jeu).
- **Le contrôle** (`CasaEngine.Editor/Remote/EditorRemoteValidation.cs`) : `TryResolveInsideProject` (ligne 64) normalise par `Path.GetFullPath`, refuse ce qui sort du projet, puis appelle `IsFreeOfLinksBelowRoot` (ligne 104) ; `TryResolveAutomationPath` (ligne 195) fait de même pour un chemin complet. `IsFreeOfLinksBelowRoot` parcourt les segments du chemin relatif, lit les attributs de chaque préfixe par `File.GetAttributes` (ligne 149) et **s'arrête au premier composant absent** (ligne 129, « le reste est à créer »). Elle n'a pas d'autre appelant (`rg "IsFreeOfLinksBelowRoot" --glob "*.cs"` : les deux lignes 84 et 227).
- **Appelants** (`rg "TryResolveInsideProject|TryResolveAutomationPath" --glob "*.cs"`) : `EditorRemoteAssets.Resolve` (lignes 54 et 72 : la référence, puis le nom de fichier de l'entrée de catalogue), `EditorRemoteEditCommands` (`world.create` ligne 47, `world.open` ligne 83), `EditorRemoteAssetCreation.Prepare` (lignes 45 et 63, après `HasOnlyPlainSegments`), `GameEditor.Remote.cs:133` (automatisation et `--open-asset`).
- **La normalisation de Windows, sondée hors du dépôt** (`<scratchpad>/segprobe.ps1`, PowerShell 7, .NET 10.0.15, dossier temporaire supprimé ensuite ; T1.1 l'établit sous le runtime des tests) :
  - `Path.GetFullPath` garde l'espace finale d'un segment **intermédiaire** (`Real \x.txt` reste tel quel) et retire celle du **dernier** segment (`Real ` devient `Real`) ; un point final seul d'un segment intermédiaire est retiré (`Real.\x.txt` devient `Real\x.txt`), mais `Real..`, `Real...` et `Real. ` sont gardés en position intermédiaire ; `Real .\x.txt` devient `Real \x.txt` ; une tabulation finale est gardée partout.
  - Avec une jonction nommée `Real ` à côté d'un vrai dossier `Real` : `File.GetAttributes("…\Real ")` rend `Directory` (il lit le **vrai** dossier), alors que `File.Exists("…\Real \secret.txt")` rend vrai pour un fichier qui n'existe que **sous la cible de la jonction**, hors du projet. `File.GetAttributes("…\Real \")` (séparateur final) et le chemin `\\?\` lisent bien la jonction.
  - **Un tel dossier ou une telle jonction se crée sans chemin `\\?\`**, avec un séparateur final : `CreateDirectory("…\Made \")`, `mkdir "…\Cmd \"` et `mklink /J "…\Link \" <cible>` créent `Made `, `Cmd ` et la jonction `Link `. La phrase d'O4 « créable seulement par un chemin `\\?\` » était donc fausse.
- **Ce qui est prouvé et ce qui ne l'est pas.** Prouvé (T1.4 du plan d'intégration, test `Create_InAFolderEndingWithASpace_NextToAJunctionOfThatName_IsRefused_AndWritesNothing`) : avant T1.4, `material.create` dans le dossier `Entities ` écrivait hors du projet. **Non essayé** : la même sortie par une référence d'asset (il faut une entrée de catalogue écrite avec ce nom), par `world.open` et par les chemins d'automatisation. T0.1 l'essaie avant toute correction.
- **Règle de T1.4** (`EditorRemoteAssetCreation.cs:95`, `HasOnlyPlainSegments`) : contrôle de texte sur le dossier d'une création, segment vide ou finissant par `.` ou par un blanc (`char.IsWhiteSpace`).
- **Aucun projet connu n'est touché par la règle** (`<scratchpad>/scan_segments.py`, lecture seule) : sous `CasaEngine/Projects/` et `RacingGameCasaEngine/Content/`, 0 dossier ou fichier dont le nom finit par un point ou un blanc ; 12 catalogues `AssetInfos.json`, 1920 entrées, 0 avec un tel segment.
- **L'éditeur ne tourne que sous Windows** (`CasaEngine.Editor.csproj:4`, `$(WindowsTargetFramework)`), les tests aussi. Le jeu ne référence pas `CasaEngine.Editor` (`rg "CasaEngine.Editor" --glob "*.csproj"` hors du moteur : seulement `CasaEngine.EditorServices`, par deux outils).
- **Précédent pour l'ADR** : la clôture du constat A3 (`3390ed4b1`, `fix(editor): refuse project paths that cross a link`) a ajouté une note à l'ADR-0093 (sa ligne 28) plutôt qu'une nouvelle ADR.
- **Tests** : `CasaEngine.Tests` 8241 sur 8241 sur `4d3f4298f` (aucun fichier de code n'a changé depuis). Tests du contrôle : `CasaEngine.Tests/Editor/Remote/EditorRemotePathLinkTests.cs` (jonctions par `mklink /J`, « inconclusive » sans échec quand la création est impossible).
- **Outils de l'éditeur lancé** (dépôt du jeu, ignorés par git) : `artifacts/entity-asset-remote/scripts/editorctl.py`, `t22_run.py` (185 contrôles, variables `EAR_SCRATCH` et `EAR_COMMANDS_REFERENCE`), `m9_run.py` ; smokes `Tools/automation-smokes.sh` ; références : `artifacts/entity-asset-remote/t22/commands-after.json` (54 commandes), `artifacts/entity-asset-remote/integration/smokes/`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | **Remède : « Refuser partout »** (réponse de l'auteur du 2026-11-19) : le contrôle commun refuse tout chemin dont un segment finit par un point ou une espace ; pas de contrôle par des chemins `\\?\`. Un vrai dossier ainsi nommé devient inaccessible au pilotage : accepté. |
| D2 | **À la fin : « Avancer et pousser »** (2026-11-19) : après la vérification indépendante, avance rapide de `main` du moteur et de `remaster` du jeu, puis push par `artifacts/push-main-remaster.ps1` (essai à blanc, push, `git ls-remote`). |
| D3 | **Mode `AUTO`** une fois ce plan approuvé (2026-11-19), avec les arrêts que le plan définit. |

## Points à valider avec ce plan

| Réf | Proposition |
|---|---|
| P1 | **Branches.** Moteur : `chantier/remote-path-segments`, créée sur `9d15c42b8` (le `main` poussé plus les deux commits de clôture du chantier précédent). Jeu : la branche `chantier/rgce-worlds-as-assets`, pour le pointeur et ses documents. |
| P2 | **La règle.** Dans `IsFreeOfLinksBelowRoot`, **avant toute lecture du disque**, chaque segment du chemin relatif normalisé, **existant ou non** (donc aussi après un composant absent), dont le dernier caractère est `.` ou un blanc (`char.IsWhiteSpace`, le prédicat de T1.4) fait refuser le chemin ; `linkPath` nomme ce composant. Les deux résolveurs en héritent, sans autre changement de leur code que le message (P3). La règle porte sur le chemin **normalisé**, celui par lequel l'accès passe : `Real./a.world` (normalisé en `Real/a.world`) et `sub/../a.world` restent acceptés comme aujourd'hui ; `Real ./a.world` (normalisé en `Real /a.world`) est refusé. La racine du projet et ses ancêtres ne sont pas contrôlés, comme aujourd'hui. |
| P3 | **Messages.** Quand le composant refusé l'est pour son nom, `LinkRefusal` et l'erreur de `TryResolveAutomationPath` le disent (le nom finit par un point ou une espace, Windows lirait une autre entrée) au lieu de parler d'un lien ; même classe d'erreur (`invalid_argument` pour les commandes). Le contrat des 54 commandes (noms, schémas, descriptions) ne change pas. |
| P4 | **Rien n'est retiré.** `HasOnlyPlainSegments` reste devant les créations (plus strict : segments vides, `.` et `..` du texte brut) ; aucun contrôle existant n'est retiré ni déplacé ; aucune signature publique ne change. |
| P5 | **Tests** (`CasaEngine.Tests/Editor/Remote/`), écrits pour échouer contre le code d'avant : (a) texte seul, sans disque : refus de `Real /a.world`, `Real../a.world`, `Real. /a.world`, `Real ./a.world`, `sub/Real /a.world`, `Absent /a.world`, `Absent/Real /a.world`, d'une tabulation finale, des formes à barres inverses, avec `linkPath` égal au composant ; acceptation inchangée de `a.world`, `sub/../a.world`, `Real./a.world`, `v1.2/a.world`, `.hidden/a.world` ; les mêmes cas pour `TryResolveAutomationPath`, avec son message ; (b) avec une jonction nommée `Real ` à côté d'un vrai `Real` (créée comme dans le test de T1.4, « inconclusive » sans échec si la création est impossible) : `TryResolveInsideProject` et `TryResolveAutomationPath` refusent un fichier qui n'existe que sous la cible ; `EditorRemoteAssets.Resolve` refuse une entrée de catalogue écrite `Real /x.material`, par son chemin et par son identifiant ; `world.open` refuse `Real /a.world` ; la variante sans vrai dossier `Real` (composant « absent ») est refusée aussi ; la cible reste intacte ; (c) les tests existants passent sans changement d'attente (tout changement d'attente est listé et expliqué). |
| P6 | **Documents.** `docs/editor/editor-remote-control.md` : la règle dans « Assets » et dans les limites (un vrai dossier ainsi nommé n'est plus atteignable par le pilotage ; résidus de P11). ADR-0093 : une phrase ajoutée à sa note A3, comme le fit `3390ed4b1` ; pas de nouvelle ADR (la règle précise A3, déjà décidée). |
| P7 | **Essai dans l'éditeur lancé, avant et après**, sur une copie de `Projects/SampleProject` hors du dépôt : une jonction `<Dossier> ` créée par `mklink /J` à côté d'un dossier existant, pointant vers un dossier hors de la copie qui porte une copie d'un matériau du projet ; une entrée ajoutée au `AssetInfos.json` **de la copie** avec ce nom de fichier. Essayés : `material.get` et `asset.open` par le chemin et par l'identifiant, `world.open` d'un chemin de ce dossier, et le lancement avec `--open-asset` sur ce chemin. Jamais d'enregistrement. Avant (T0.1) : la réponse est notée telle quelle, qu'elle prouve la sortie ou non. Après (T2.1) : tout est refusé, la cible est intacte. |
| P8 | **Jeu.** Pointeur `CasaEngine` sur la tête du moteur ; `dotnet build RacingGame.slnx` à 0 erreur ; `m9_run.py` rejouée (31 contrôles, fichier identique à `artifacts/entity-asset-remote/m9/saved-1.entity`). **Pas de rejeu de la parité** : `git diff --name-only f82f213fc <tête>` ne doit nommer que des fichiers de `CasaEngine.Editor/Remote/`, `CasaEngine.Tests/`, `docs/` et `ai-agent/` ; sinon la parité est rejouée (`slice_runs.sh`, `slice_check.py`) avant tout push. |
| P9 | **Avance et push** (D2), comme T4.2 du plan d'intégration : `git fetch` des deux dépôts ; `origin/main` ancêtre de la tête du moteur et `origin/remaster` ancêtre de la tête du jeu ; depuis les têtes vérifiées en T4.1, seuls `ai-agent/` (moteur), `ai-agent/` et `CasaEngine` (jeu) ont changé ; `git ls-tree` : `MGUI` `2f9773d0…` et `NvgSharp` `9c0da031…` inchangés ; pointeur du jeu = tête du moteur ; ni `main` ni `remaster` n'est la branche d'un arbre de travail ; `push.followTags` vide ; avance rapide par `git update-ref <ref> <nouvelle> <ancienne>` ; script en essai à blanc puis réel ; `git ls-remote` égal aux têtes locales. Un contrôle qui échoue : ⚠️, rien n'est avancé ni poussé ; rien n'est jamais forcé. L'approbation de ce plan vaut demande de push pour les deux têtes qu'il produit. |
| P10 | **Copies hors dépôt** : `<scratchpad>/remote-path-segments/` (dossier temporaire de la session), supprimées à la clôture après retrait de chaque jonction seule (jamais à travers elle), contrôle qu'il n'en reste aucune et que le chemin résolu est sous le dossier temporaire de la session. Sorties gardées dans `artifacts/remote-path-segments/` du dépôt du jeu (ignoré par git). Les réglages de l'utilisateur sous `%LOCALAPPDATA%/CasaEngine/` ne sont jamais modifiés (empreintes avant et après, hors `editor-remote/`) ; aucun jeton ni fichier de découverte dans une note. |
| P11 | **Résidus relevés, non traités** : la racine du projet et ses ancêtres (non contrôlés, par choix de l'ADR-0093) ; les liens physiques ; la course entre contrôle et usage ; les noms réservés de Windows (`NUL`, `CON`, …) et les noms courts 8.3, non essayés. |
| P12 | **Dernier commit et statut de T4.2.** Le résultat d'un push ne peut pas être écrit dans le commit poussé. Le dernier commit du moteur archive ce plan et marque T4.2 ✅ avec la note « faite si ce commit est lu sur `origin/main` ; les sorties des contrôles sont dans le rapport de fin ». Si un contrôle de P9 échoue ou si le push est refusé, un commit de plus sur la branche repasse T4.2 en ⚠️ avec la question, et rien n'est poussé. |

## Relecture de sécurité

À remplir par la relecture du `security-reviewer`, avant l'approbation.

## Règles d'exécution pour l'agent

- **Branches** de P1. Jamais de commit sur `main`, `master` ni `remaster`. **Ne pas pousser avant T4.2**, et seulement comme P9 le dit.
- **Une seule tâche à la fois**, icône `⏳` → `🚧` → `✅` / `🧪` / `⚠️`, note de validation, **un commit par tâche** qui inclut ce fichier, message en anglais `type(area): summary`, par fichier (`git commit -F`), terminé par la ligne `Co-Authored-By` de la session ; `git add` fichier par fichier ; jamais `.serena/`, `.codegraph/`, `log.txt`.
- Jamais `reset`, `checkout` ni `switch` d'une autre branche, `stash`, ni `git submodule update` ; aucune commande git dans `MGUI/` ni `NvgSharp/` hors lecture (`status`, `rev-parse`, `merge-base --is-ancestor`).
- **Ne rien inventer** : sinon ⚠️ Blocked, question dans « Points ouverts », arrêt. Au plus 3 cycles de correction par tâche.
- **Build** avant ✅ : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln`, 0 erreur ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` (jamais `--no-build`), tout passe.
- **Sécurité** : le code de T1.1 est écrit par `security-executor` sur le contrat de la tâche ; un `verifier` frais contrôle l'ensemble en T4.1.
- **Lancements** : un seul lancement de l'éditeur ou du jeu à la fois, jamais pendant un build ; rebuild avant de lancer ; l'éditeur se ferme par `taskkill /PID` sans `/F`.
- **Langue** : ce plan en français ; code, commits, `docs/` et ADR en anglais.
- **Retour arrière** : avant T4.2 rien n'est poussé et `main` ne bouge pas ; tout se défait en abandonnant la branche. Après le push, un défaut se corrige par un nouveau commit, jamais par un push forcé.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- Les deux solutions du moteur à 0 erreur ; `CasaEngine.Tests` tout vert (8241 plus les tests ajoutés), aucun ignoré.
- Les tests de P5 échouent contre le code d'avant (preuve qu'ils voient le défaut) et passent après.
- Éditeur lancé : l'essai de P7 refusé après correction, cible intacte ; `t22_run.py` 185 sur 185 ; contrat des 54 commandes inchangé ; smokes d'automatisation identiques à `integration/smokes/`.
- Jeu : build à 0 erreur, M9 rejouée identique (P8).
- Vérification indépendante **CONFIRMED** (T4.1), puis push contrôlé par `git ls-remote` (T4.2).

---

## Phase 0 — Les faits avant la correction

### ⏳ T0.1 — Essai de la sortie du projet par une référence et par l'automatisation

- Objectif : savoir si le défaut est atteignable ailleurs que par les créations (P7), sur le code d'aujourd'hui.
- Sources : éditeur Debug construit sur la tête de la branche (aucun code changé) ; copie de `Projects/SampleProject` sous `<scratchpad>/remote-path-segments/before/`.
- Étapes : empreintes de `%LOCALAPPDATA%/CasaEngine/` (hors `editor-remote/`) ; build de l'éditeur ; préparation de la copie selon P7 ; les appels de P7, chaque réponse gardée ; empreintes du dossier cible avant et après ; fermeture de l'éditeur ; retrait de la jonction seule.
- Validation : les réponses sont notées (sortie prouvée ou non, par voie) ; la cible et les réglages de l'utilisateur sont intacts. Aucun résultat ne bloque la suite : D1 demande le refus partout.
- Commit : `docs(ai-agent): record the reach of the path segment weakness before the fix`

## Phase 1 — La règle

### ⏳ T1.1 — Le contrôle commun refuse un segment finissant par un point ou un blanc

- Objectif : P2, P3, P4, P5, P6.
- Fichiers : `CasaEngine.Editor/Remote/EditorRemoteValidation.cs` ; tests sous `CasaEngine.Tests/Editor/Remote/` (`EditorRemotePathLinkTests.cs`, et les fichiers de tests des commandes d'assets et de monde pour `Resolve` et `world.open`) ; `docs/editor/editor-remote-control.md` ; `docs/decisions/0093-editor-remote-control-covers-assets-panels-and-automation.md` (une phrase dans la note A3).
- Étapes (écrites par `security-executor`) : les tests de P5 d'abord, lancés contre le code d'avant (ils échouent, la liste des échecs est gardée) ; la règle de P2 dans `IsFreeOfLinksBelowRoot` et les messages de P3 ; les deux builds ; tous les tests ; documents de P6. La session principale relit le diff et rejoue builds et tests.
- Validation : celle de P5 ; `git diff --name-only` limité aux fichiers ci-dessus et à ce plan ; aucun contrôle retiré (lecture du diff).
- Commit : `fix(editor): refuse project paths with a segment ending with a dot or a space`

## Phase 2 — Éditeur lancé

### ⏳ T2.1 — L'essai refusé, le parcours et les smokes

- Objectif : P7 après correction, et aucune régression du pilotage.
- Étapes : rebuild ; l'essai de T0.1 rejoué sur une copie neuve (`…/after/`) ; `t22_run.py` sur une copie neuve (`EAR_SCRATCH` sur `<scratchpad>/remote-path-segments`, `EAR_COMMANDS_REFERENCE` sur `t22/commands-after.json`) ; `Tools/automation-smokes.sh` vers `artifacts/remote-path-segments/smokes/`, comparé à `artifacts/entity-asset-remote/integration/smokes/` ; empreintes des réglages de l'utilisateur avant et après.
- Validation : tous les appels de P7 refusés (`invalid_argument` pour les commandes, message d'erreur pour `--open-asset`), cible intacte ; 185 contrôles sur 185 ; contrat inchangé ; fichiers des smokes identiques octet pour octet (toute différence est listée et expliquée, sinon ⚠️).
- Commit : `docs(ai-agent): record the launched-editor run of the path segment rule`

## Phase 3 — Jeu

### ⏳ T3.1 — Le jeu sur le moteur corrigé

- Objectif : P8.
- Fichiers (dépôt du jeu, branche `chantier/rgce-worlds-as-assets`) : pointeur `CasaEngine`, `ai-agent/HANDOVER.md` (ADR-0144 et 0145 du moteur acceptées, cette correction).
- Étapes : pointeur sur la tête du moteur ; `dotnet build RacingGame.slnx` ; `m9_run.py` sur une copie neuve ; contrôle de la liste de fichiers de P8.
- Validation : celle de P8.
- Commit (jeu) : `chore(engine): point CasaEngine at the path segment rule`

## Phase 4 — Vérification, clôture, push

### ⏳ T4.1 — Vérification indépendante

- Étapes : `verifier` frais sur les têtes du moteur et du jeu : builds, tests, les tests de P5 rejoués contre le code d'avant par lui-même (ou leur preuve relue), l'essai de P7 refait à sa façon (autres écritures, autre dossier), non-régression (`t22_run.py`, contrat), M9, état des dépôts. Traitement des constats (`FIX`, `DEFER`, `REJECT`) ; un correctif de code relance l'acceptation concernée et une vérification. La note écrit les deux têtes vérifiées.
- Validation : **CONFIRMED**, têtes vérifiées notées.
- Commit : `docs(ai-agent): record the check of the path segment rule`

### ⏳ T4.2 — Archivage, avance, push

- Étapes : ce plan passe dans `tasks/archive/`, lignes de `ai-agent/README.md`, statut selon P12 (dernier commit du moteur) ; pointeur du jeu sur cette tête (dernier commit du jeu) ; copies de P10 supprimées ; contrôles, avance et push de P9.
- Validation : les sorties des contrôles de P9 ; les deux `ls-remote` ; arbres propres ; rapport de fin (`AGENTS.md` §12).
- Commits : `docs(ai-agent): archive the path segment plan` (moteur), `chore(engine): point CasaEngine at the pushed engine main` (jeu).

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Valider ou modifier P1 à P12 ; en particulier P2 (le prédicat de T1.4, blancs compris, appliqué à tout segment, existant ou non), P6 (une phrase dans l'ADR-0093, pas de nouvelle ADR), P8 (pas de rejeu de la parité si aucun fichier du runtime ne change), P9 (l'approbation vaut demande de push) et P12 (T4.2 marquée ✅ dans le commit poussé). | Approbation |
| O2 | La sortie du projet est-elle atteignable par une référence d'asset, `world.open` ou `--open-asset` ? Inconnu avant T0.1. | T0.1 |

## Hors périmètre

- Le contrôle par des chemins `\\?\` (écarté par D1) ; le contrôle de la racine du projet et de ses ancêtres ; les liens physiques ; la course entre contrôle et usage ; les noms réservés de Windows et les noms courts (P11).
- Tout chemin que le pilotage ne fait pas passer par `TryResolveInsideProject` ou `TryResolveAutomationPath` ; l'explorateur d'assets et les dialogues de l'éditeur.
- Le runtime (`CasaEngine/`), `CasaEngine.EditorServices`, MGUI, NvgSharp ; le jeu hors pointeur et documents.
- L'archivage du plan `offline-world-writer-tasks.md` (terminé, laissé à l'auteur).
