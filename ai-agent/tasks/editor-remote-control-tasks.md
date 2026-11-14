# Plan agent IA — Pilotage de l'éditeur par une CLI et un serveur MCP

Demande de l'auteur le 2026-11-14 : « faire l'automatisation de l'éditeur en pensant cela comme le début d'une CLI (ou autre approche plus pertinente pour l'IA, MCP ?) pour que dans l'avenir un agent IA puisse complètement interagir avec l'éditeur ». Suite notée pendant le chantier `demos-onboarding` : l'automatisation ne sait pas créer un monde ni une entité.
Les décisions D1 → D4 ci-dessous ont été arbitrées avec l'auteur le 2026-11-14 : **ce plan les applique, il ne les rediscute pas**. Les propositions P1 → P11 sont à valider avec le plan.
Relecture de sécurité avant approbation (2026-11-14, `security-reviewer`) : aucun P0 ; constats S1 à S12 (tube squatté, accès SMB, éditeur administrateur, fichier de découverte, écrasement par `world.create`, instanciation de types, propriétés, blocage du fil du jeu, permissions MCP et injection, données qui quittent la machine, ordre des refus de `world.open`, déclaration MCP) intégrés dans P1, P3, P5, P7 à P11 et dans les validations de T1.1, T1.2, T3.1 et T5.2.
Relecture du plan : `plan-verifier` REVISE (journal déjà présent, contexte d'historique, placement des composants, sauvegarde en échec), corrigé, puis READY à la seconde relecture (2026-11-14).

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Un agent IA (ou un script) pilote l'éditeur **ouvert** : il lit son état, crée et modifie mondes, entités et composants, sauvegarde, voit le résultat par capture d'écran, lance et arrête le mode Play, et l'auteur peut annuler ses changements avec Ctrl+Z. Deux outils séparés de l'éditeur portent cet accès : une CLI `casa-editor` et un serveur MCP. Les deux appellent une seule couche de commandes typées, tenue par l'éditeur. Décision : ADR-0092.

## État vérifié du dépôt (2026-11-14)

- Automatisation existante : `CasaEngine.Editor/EditorAutomationOptions.cs:62` `Parse(string[])`, 23 options lues au démarrage, rejouées par la machine à états `GameEditor.RunAutomation` (`GameEditor.cs:6884`, appelée à `:6510`). Aucune option ne crée de monde, d'entité ni de composant.
- Boucle : `GameEditor.Update` (`GameEditor.cs:6289`). Précédent de file d'actions externes : `QueueProjectOpen` (`:2419`), vidée par `ProcessPendingProjectLauncherAction` (`:2451`, appelée à `:6422`, avant `UpdateHost` à `:6434` et les panneaux à `:6464`).
- Historique : `EditorHistoryService.Current` ; `Execute(EditorHistoryContext, IEditorCommand)` (`EditorHistoryService.cs:111`), `OpenTransaction` (`:124`), `Undo`/`Redo` (`:146`, `:157`), `IsSuspended` (`:109`) mis pendant le mode Play (`GameEditor.cs:1196`, `:1218`). Commande générique : `EditorDelegateCommand`. L'état modifié suit la révision de l'historique (`EditorDirtyStateService`).
- Entités : `Controls/EntitiesPanel.cs` `AddEntity` (`:744`), `DuplicateEntity` (`:780`), `DeleteEntity` (`:815`), `RenameEntity` (`:840`, via une boîte de saisie), tous dans l'historique par `ExecuteWorldCommand` (`:868`). Opérations sans historique dans `CasaEngine.EditorServices/EditorWorldEditingService.cs` (`AddEntity :15`, `RemoveEntity :40`, `AttachEntity :48`, `DetachEntity :64`). Pas de reparentage. Identité : `Entity(Guid id)` (`Entity.cs:183`).
- Composants : ajout par `EntityDetailsPanel.AddComponent` (`:1402`, historique) ; types trouvés par réflexion, `CreateComponentTypeLookup` privé (`:1535`). Aucun chemin de retrait de composant pour l'utilisateur.
- Propriétés : découverte par `TypeDescriptor.GetProperties` (`ComponentEditors/ComponentEditorBase.cs:149`), écriture par `ApplyPropertyChange` (`:232`) dans l'historique.
- Monde : ouverture `GameEditor.OpenWorldAsset` (`:4597`, demande d'enregistrer si modifié) ; sauvegarde de tout le projet `SaveCurrentProject` (`:2463`, refusée en Play) ; pas de création de monde dans l'éditeur, mais le motif existe dans `EditorProjectAuthoringService.CreateProject` (`:94-101` : `new World { Name, FileName }`, `EditorWorldWriter.SaveWorld`, `EditorAssetCatalogService.Add`). Monde courant : `_editorRuntime.GameManager.CurrentWorld`.
- Sélection : `EditorSelection.Current` (`EditorSelection.cs`), hors historique.
- Capture : `GameEditor.CaptureAutomationScreenshot` (`:6850`), synchrone, dessine le bureau MGUI dans un `RenderTarget2D` puis `SaveAsPng`.
- Play : `StartPlayMode` (`:1285`), `StopPlayMode` (`:1307`) ; état dans `EditorPlayModeService` (`EditorServices/PlayMode/EditorPlayModeService.cs`, `StateChanged`).
- Journal : `Logs` (`CasaEngine/Core/Logging/Logs.cs`) diffuse vers des `ILogger` ajoutés par `AddLogger`. L'éditeur enregistre déjà `LoggerEditor` et `DebugLogger` (`GameEditor.cs:407-408`) ; `LoggerEditor` (`CasaEngine.Editor/Log/LoggerEditor.cs:35`) garde jusqu'à 10 000 entrées (`Entries`), les élague par lots (`EntriesTrimmed`) et n'est pas thread-safe. Second tampon : `EditorDiagnosticsBuffer` (256 entrées, lecture par instantané).
- Historique par contexte : `Undo`/`Redo` agissent sur la pile active (`EditorHistoryService.cs:69-89`, `:146-166`), qui suit le panneau qui a le focus (`SetActiveContext`, `GameEditor.cs:3101`, `:3112`, `:7936`). `EntitiesPanel` exécute dans le contexte du monde (`EntitiesPanel.cs:868-873`), `EntityDetailsPanel` dans son propre `HistoryContext` ou le contexte par défaut (`EntityDetailsPanel.cs:1429-1434`).
- Placement d'un composant : `EntityDetailsPanel.AddComponent` (`:1402-1427`) dépend du composant sélectionné : enfant du `SceneComponent` sélectionné, sinon racine si l'entité n'en a pas et que le composant est un `SceneComponent`, sinon liste des composants.
- `SaveCurrentProject` (`GameEditor.cs:2463-2494`) ne rend rien : en Play, sans projet, ou si l'écriture des modes de jeu échoue, il journalise un avertissement et rend la main.
- SDK MCP : paquet NuGet `ModelContextProtocol` 2.4.0 (dernière mise à jour 2026-11-07, stable, Apache-2.0, cibles net8.0/net9.0/net10.0/netstandard2.0, dépend de `Microsoft.Extensions.Hosting.Abstractions` et `ModelContextProtocol.Core`). Sources : https://github.com/modelcontextprotocol/csharp-sdk, https://www.nuget.org/packages/ModelContextProtocol. Les signatures de l'API serveur stdio restent à lire dans https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html (T4.2).
- Paquets centralisés dans `Directory.Packages.props`. `Tools/` ne contient qu'un script Python.
- Branche `chantier/editor-remote-control` créée depuis `main` `c787e8bd` puis avancée sur `main` `7a83467b` (fusion de `demos-onboarding` et de A2 à A5), dans le worktree `D:\development\repo\CasaEngineMonogame-prefs`. A2 à A5 sont dans `main` depuis `7a83467b`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Une CLI **et** un serveur MCP, chacun dans son propre projet, outils séparés du moteur et de l'éditeur. Ils appellent la même couche de commandes de l'éditeur. |
| D2 | Première tranche : lire l'état ; créer et modifier (monde, entité, composant, propriété, sauvegarde) ; voir (capture d'écran) ; Play (lancer, arrêter, lire le journal). |
| D3 | Les modifications d'un agent passent par l'historique annuler/rétablir de l'éditeur. |
| D4 | Serveur MCP sur le SDK C# officiel (`ModelContextProtocol`). |

## Propositions à valider avec le plan

| Réf | Proposition |
|---|---|
| P1 | **Transport** (durci après la relecture de sécurité, S1 à S4) : tube nommé local, nom `casaengine-editor-<guid aléatoire>`. Le serveur crée le tube avec `PipeOptions.FirstPipeInstance` (échec propre si le nom existe déjà) et une sécurité explicite par `NamedPipeServerStreamAcl.Create` : accès à l'utilisateur courant seulement, refus explicite du groupe NETWORK (S-1-5-2), donc pas d'accès par `\\machine\pipe\…` ; en plus, le serveur coupe tout client venu par le redirecteur réseau (`GetNamedPipeClientComputerName` rend un nom), même en boucle locale (ajouté en T1.2). Les clients se connectent au serveur `"."` uniquement, avec `PipeOptions.CurrentUserOnly` (le serveur doit appartenir à l'utilisateur) et `TokenImpersonationLevel.Identification`. Fichier de découverte `%LOCALAPPDATA%/CasaEngine/editor-remote/<pid>.json` (pid, heure de démarrage du processus, nom du tube, jeton aléatoire, projet), écrit de façon atomique, supprimé à la fermeture. Les outils ne lui font pas confiance : nom de tube validé par une expression régulière, processus vivant avec la même heure de démarrage, jeton redemandé par `editor.hello`. `--remote` et le menu sont refusés quand l'éditeur tourne en administrateur. Un client à la fois ; un nouveau client attend. |
| P2 | **Désactivé par défaut.** Le serveur démarre avec l'option `--remote` ou le menu `Tools > Remote Control` (case à cocher), et s'arrête par le même menu. La barre de titre ou la ligne d'état indique qu'il tourne. |
| P3 | **Protocole** : messages de forme JSON-RPC 2.0, un par ligne (UTF-8), `System.Text.Json`. Méthodes réservées `editor.hello` (version du protocole, version de l'éditeur, jeton de P1) et `commands.list` (nom, description, schéma JSON des arguments, caractère lecture seule ou modifiant). Erreurs : codes JSON-RPC standard plus des codes de l'éditeur (`play_mode_active`, `unsaved_changes`, `not_found`, `invalid_argument`, `timeout`). Projet `Tools/CasaEngine.Editor.Remote.Protocol`, sans dépendance au moteur ni à MonoGame, référencé par l'éditeur et les deux outils. |
| P10 | **Limites contre le blocage** (S8) : lecture d'une ligne bornée à 1 Mio ; délai d'inactivité du client ; une seule requête en cours par connexion ; au plus quelques commandes exécutées par image ; seules les écritures sur le tube se font sur le fil d'arrière-plan, jamais sur le fil du jeu ; toute commande différée a un délai et répond `timeout` ; `play.start` répond une erreur si la compilation des scripts ou le démarrage échoue (aujourd'hui `StartPlayMode` journalise et rend la main, `GameEditor.cs:1289-1300`) ; de même, `project.save` répond une erreur si la sauvegarde échoue ou si le monde reste marqué modifié après (aujourd'hui `SaveCurrentProject` ne rend rien). Une commande peut travailler de façon synchrone sur le fil du jeu (compilation des scripts au Play, lecture et écriture des fichiers, comme le font les menus) ; ce qui est interdit, c'est d'attendre le tube ou un client sur ce fil. |
| P11 | **Données qui quittent la machine** (S10) : ce que lit un agent (journal, captures, noms) part chez le fournisseur du modèle. `logs.read` est plafonné en nombre d'entrées et en taille ; la documentation le dit. |
| P4 | **Emplacement** : `Tools/CasaEngine.Editor.Remote.Protocol`, `Tools/CasaEngine.Editor.Cli` (exécutable `casa-editor`), `Tools/CasaEngine.Editor.Mcp`. Ajoutés à `CasaEngine.Editor.MonoGame.sln` dans un dossier de solution « Tools ». Aucune référence des outils vers l'éditeur ou le moteur. |
| P5 | **Règles des commandes** : une commande qui modifie le monde est une entrée d'historique (une transaction si elle fait plusieurs changements), **toujours dans le contexte `EditorHistoryContext(World, WorldViewport)`** ; `history.undo` et `history.redo` agissent explicitement sur ce contexte, pas sur la pile active du panneau qui a le focus. `world.create` et `world.open` ne sont pas annulables : ouvrir un monde vide son historique (`GameEditor.cs:4651`), comme aujourd'hui. **`component.add`** prend l'`id` de l'entité, le nom du type (liste blanche ci-dessous) et, en option, l'`id` d'un `SceneComponent` parent : avec un parent, le nouveau `SceneComponent` devient son enfant ; sans parent, un `SceneComponent` devient la racine si l'entité n'en a pas, sinon il est refusé (`invalid_argument`, le parent est à donner) ; tout autre composant va dans la liste des composants. Ce choix reprend la règle du panneau sans dépendre de la sélection. **`component.remove`** refuse un `SceneComponent` qui a des enfants (`invalid_argument`) ; il peut retirer la racine sans enfant ; Undo remet le composant exactement à sa place (parent, racine ou liste). Refusée pendant le Play (`play_mode_active`) ; `world.open` et `world.create` refusés si le projet a des changements non enregistrés (`unsaved_changes`) ; ces refus et celui du Play sont vérifiés **avant** d'appeler `OpenWorldAsset`, qui arrêterait sinon le Play et ouvrirait une boîte de dialogue (S11) ; `world.open` garde le contrôle du catalogue d'assets (`GameEditor.cs:4605-4612`). Une entité est désignée par son `Guid` ; `world.entities` donne aussi son chemin de noms pour la lecture. **Composants** (S6) : un type n'est accepté que par la liste blanche de l'inspecteur (sous-classe concrète d'`EntityComponent`, `Browsable`, constructeur sans paramètre, `EntityDetailsPanel.cs:1535-1569`), jamais par `ElementFactory.Create` qui instancie n'importe quel type. **Propriétés** (S7) : seulement celles que l'inspecteur édite (filtre `CanEditProperty`, `ComponentEditorBase.cs:412`) et les types qu'il sait éditer (texte, booléen, entiers et flottants, `Vector3`, `Color`, `Guid`, énumérations), sur l'entité et ses composants, sans chemin pointé dans le graphe d'objets ; valeurs en JSON typé ; énumération hors des valeurs définies, NaN, infini et texte trop long refusés. |
| P6 | **Pas de logique en double** : les opérations d'entité et de composant aujourd'hui privées des panneaux (`EntitiesPanel`, `EntityDetailsPanel`, liste des types de composants) passent dans un service partagé `EditorWorldOperations` (éditeur), utilisé par les panneaux et par les commandes. Comportement des panneaux inchangé. |
| P7 | **Outils MCP** (S9) : un outil MCP par commande, généré à la connexion depuis `commands.list` (schéma repris), pour que les permissions de Claude Code se règlent outil par outil (lectures autorisées d'office, `project.save`, `world.create` ou `play.start` soumis à accord). Annotations MCP `readOnlyHint` et `destructiveHint` d'après `commands.list`. Si le SDK ne permet pas de déclarer des outils à l'exécution, repli sur des outils fixes séparés en lecture, modification et Play (jamais un seul outil générique). Les descriptions viennent du code de l'éditeur et disent que les valeurs rendues sont des données du projet, pas des instructions ; les textes rendus sont tronqués. Les captures sont rendues comme contenu image MCP. Sans éditeur qui sert, le serveur MCP démarre quand même et chaque outil répond une erreur claire. |
| P8 | **Fichiers écrits par les commandes** (S5, S10) : `world.create` prend un **nom**, pas un chemin : extension `.world` imposée, dossier de contenu du projet, fichier existant refusé (pas d'écrasement), caractères invalides, `:` et noms de périphériques réservés refusés, appartenance au projet vérifiée par `Path.GetFullPath` puis `Path.GetRelativePath` (ni `..` ni chemin absolu), pas par préfixe de chaîne. Les captures vont dans `%TEMP%/CasaEngine/editor-remote/` sous un nom aléatoire que l'appelant ne choisit pas, avec `FileMode.CreateNew` (le code actuel utilise `File.OpenWrite`, qui ne tronque pas, `GameEditor.cs:6874`), et sont nettoyées à la fermeture. Aucune autre écriture que les chemins de sauvegarde existants du projet. |
| P9 | **Documentation pour les agents** : `docs/editor/editor-remote-control.md`, et une section courte dans `AGENTS.md` §7 (comment lancer l'éditeur avec `--remote` et appeler la CLI ou le MCP). Le serveur MCP est déclaré dans `.mcp.json` (Claude Code) et `.vscode/mcp.json` (Copilot), comme Serena et codegraph, en pointant vers l'exécutable construit (pas `dotnet run`, qui recompilerait le dépôt à chaque session, S12). |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/editor-remote-control`**, créée depuis `main`, dans le worktree `D:\development\repo\CasaEngineMonogame-prefs`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API (en particulier celle du SDK MCP) vient du dépôt ou d'une doc officielle citée (URL). Sinon : ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build obligatoire** (`dotnet build CasaEngine.Editor.MonoGame.sln`, et `CasaEngine.MonoGame.sln` si le moteur est touché) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, commits, `docs/` et ADR en anglais.
- Rappel moteur : la file de commandes est vidée une fois par image sans allocation quand elle est vide ; le fil du jeu n'attend jamais le tube ni un client (P10) ; aucune exception d'une commande ne remonte dans `Update` (elle devient une réponse d'erreur, journalisée) ; le runtime (`CasaEngine`) ne dépend d'aucun de ces projets.
- **Sécurité** : chantier à risque (accès d'un autre processus à l'éditeur, écriture de fichiers). Relecture `security-reviewer` et `plan-verifier` avant approbation ; vérificateur neuf après la dernière tâche de code.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.Editor.MonoGame.sln` et `dotnet build CasaEngine.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout passe.
- Smoke de bout en bout (T5.1) : éditeur lancé avec `--remote` sur une copie temporaire de `Projects/SampleProject` ; un script de la CLI crée un monde, une entité, un composant, change une propriété, sauvegarde, capture, lance et arrête le Play ; le `.world` écrit contient l'entité et la valeur ; après un clic dans le viewport du monde (contexte d'historique du monde actif), Ctrl+Z dans l'éditeur annule le dernier changement fait par commande.
- Smoke MCP (T5.1) : poignée de main MCP et liste des outils par un client MCP, puis un appel d'outil.

---

## Phase 0 — Décision

### ✅ T0.1 — ADR-0092, plan et index

- Objectif : enregistrer la décision et le plan.
- Fichiers : `docs/decisions/0092-editor-remote-control-for-tools-and-ai-agents.md` (statut Accepted à l'approbation), `docs/decisions/README.md`, ce plan, `ai-agent/README.md`.
- Validation : relecture ; liens valides.
- Note de validation (2026-11-14) : plan approuvé par l’auteur (« les commits de A2 à A5 sont à merger dans main puis lance le plan complet »), P1 à P11 compris ; ADR-0092 passée en Accepted et ajoutée à l’index ; ligne du plan dans `ai-agent/README.md`.
- Commit : `docs(editor): record the remote control decision and plan`

## Phase 1 — Protocole et serveur

### ✅ T1.1 — Projet de protocole

- Objectif : enveloppes requête, réponse et erreur, lecture et écriture d'un message par ligne, codes d'erreur, version du protocole, fichier de découverte (P1, P3).
- Fichiers : `Tools/CasaEngine.Editor.Remote.Protocol/*` (nouveau projet), `CasaEngine.Editor.MonoGame.sln`, `CasaEngine.Tests/CasaEngine.Tests.csproj` (référence au projet de protocole ; `CasaEngine.Tests` n'est dans aucune des deux `.sln`), tests dans `CasaEngine.Tests/Editor/Remote/`.
- Validation : tests d'aller-retour des messages, d'une ligne invalide (erreur `parse_error`, pas d'exception), d'une ligne de plus de 1 Mio (refusée sans tout lire), d'un fichier de découverte falsifié (nom de tube `\\autre\pipe\x`, nom hors expression régulière, processus mort ou heure de démarrage différente : ignoré) ; build.
- Note de validation (2026-11-14) : projet `Tools/CasaEngine.Editor.Remote.Protocol` (net9.0, sans dépendance) : enveloppes JSON-RPC en snake_case, codes et raisons d'erreur, `RemoteLineReader` borné à 1 Mio (ligne trop longue sautée, lecteur toujours utilisable), `RemoteMessageCodec` qui ne lève jamais sur une entrée invalide, `RemoteDiscovery` (écriture atomique, lecture qui refuse nom de tube, jeton, nom de fichier, processus ou heure de démarrage qui ne correspondent pas), `RemoteEditorClient` (serveur `.` seulement, `CurrentUserOnly`, `Identification`, contrôle du jeton) partagé par les deux outils et testé en T1.2. 20 tests `Editor.Remote` verts ; `CasaEngine.Editor.MonoGame.sln` 0 erreur.
- Commit : `feat(tools): add the editor remote control protocol`

### ✅ T1.2 — Serveur de l’éditeur et registre de commandes

- Objectif : `EditorRemoteServer` (tube sur un fil d'arrière-plan, P1), file de requêtes vidée dans `GameEditor.Update` juste après `ProcessPendingProjectLauncherAction`, `EditorRemoteCommandRegistry` (nom, description, schéma, gestionnaire immédiat ou différé), commandes `editor.hello`, `commands.list`, `editor.status` (projet, monde, Play, changements non enregistrés). Activation par `--remote` et le menu `Tools > Remote Control` (P2). Fichier de découverte écrit et supprimé.
- Fichiers : `CasaEngine.Editor/Remote/*` (nouveau), `GameEditor.cs`, `EditorAutomationOptions.cs` (option `--remote`), tests.
- Validation : tests du registre et de la file avec des gestionnaires factices (commande inconnue, arguments invalides, exception du gestionnaire convertie en erreur, réponse différée, délai dépassé, plafond de commandes par image) ; aller-retour réel sur un tube dans les tests ; **nom de tube déjà pris par un autre serveur : le serveur de l'éditeur échoue proprement** (P1, `FirstPipeInstance`) ; client passé par le redirecteur réseau refusé (P1) ; client inactif qui ne bloque pas le fil du jeu (P10) ; refus en administrateur (revue du code) ; aucune allocation par image quand la file est vide (revue du code) ; build.
- Note de validation (2026-11-14) : `CasaEngine.Editor/Remote/` (`EditorRemoteCall`, `EditorRemoteCommandRegistry`, `EditorRemoteDispatcher` : au plus 4 appels par image, appels différés avec délai, aucune exception ne sort de `Pump`, rien alloué à vide ; `EditorRemoteServer` : tube `FirstPipeInstance`, ACL utilisateur courant et refus de NETWORK, lecture bornée, délai d'inactivité de 10 min, réponse écrite par le fil du tube ; `EditorRemoteControl` : découverte, refus en administrateur), `GameEditor.Remote.cs` (`GameEditor` devient `partial`), option `--remote`, menu `Tools > Remote Control`, titre « [Remote Control] », `Pump` après `ProcessPendingProjectLauncherAction`. **O3 tranché par l'essai** : une connexion par `\\localhost`, `\\127.0.0.1` ou le nom de la machine passe par le redirecteur réseau mais n'est pas dans le groupe NETWORK, donc l'ACL seule la laissait passer ; le serveur refuse désormais tout client dont `GetNamedPipeClientComputerName` rend un nom (Windows répond ERROR_PIPE_LOCAL pour `\\.\pipe\` seulement), et toute réponse inattendue. 21 tests ajoutés (file, aller-retour réel, mauvais jeton, nom squatté qui échoue, client inactif coupé sans bloquer `Pump`, clients `localhost` et `127.0.0.1` coupés sans réponse ni commande exécutée, client local servi). Refus en administrateur relu dans le code (non testable sans élévation). `CasaEngine.Tests` 5234/5234, `CasaEngine.Editor.MonoGame.sln` 0 erreur.
- Commit : `feat(editor): serve typed commands on a local pipe`

## Phase 2 — Opérations partagées et lecture

### 🧪 T2.1 — Service `EditorWorldOperations`

- Objectif : sortir des panneaux les opérations avec historique : créer, supprimer, dupliquer, renommer une entité ; ajouter un composant avec un placement explicite (parent, racine ou liste, P5) ; lister les types de composants (liste blanche) ; changer une propriété par `PropertyDescriptor`. Ajouter le retrait d'un composant (avec historique, règles de P5). Chaque opération prend le contexte d'historique en paramètre : les panneaux passent le leur (comportement inchangé), les commandes passent le contexte du monde. Les panneaux appellent le service (P6) ; `EntityDetailsPanel` calcule le placement depuis sa sélection comme aujourd'hui.
- Fichiers : `CasaEngine.Editor/Runtime/EditorWorldOperations.cs` (nouveau), `Controls/EntitiesPanel.cs`, `Controls/EntityDetailsPanel.cs`, `Controls/ComponentEditors/ComponentEditorBase.cs` si nécessaire, tests.
- Validation : tests du service sur un `World` en mémoire, chaque opération suivie d'Undo et de Redo : création, suppression, duplication, renommage ; ajout comme racine, comme enfant, dans la liste ; retrait de la racine sans enfant, retrait refusé d'un `SceneComponent` qui a des enfants, retrait d'un enfant puis Undo qui le remet sous le même parent ; build ; panneaux inchangés à l'usage (recette de l'auteur en fin de chantier).
- Note de validation (2026-11-14) : `CasaEngine.Editor/Runtime/EditorWorldOperations.cs` (ajout, duplication, suppression, renommage d'entité ; ajout de composant à une place explicite `List`, `Root` ou `Child` validée ; `TryGetPlacement` ; `TryRemoveComponent` qui refuse un `SceneComponent` avec enfants et remet le composant à sa place à l'annulation ; `SetProperty` ; liste blanche `ComponentTypes` et `TryCreateComponent` déplacées depuis `EntityDetailsPanel`). Chaque opération prend le contexte d'historique et un rappel de sélection. `EntitiesPanel`, `EntityDetailsPanel` (placement calculé depuis sa sélection comme avant) et `ComponentEditorBase.ApplyPropertyChange` passent par le service. 12 tests sur un `World` en mémoire, chacun suivi d'Undo et de Redo ; `"Process"`, un type `[Browsable(false)]` ou abstrait refusés. `CasaEngine.Tests` 5246/5246, `CasaEngine.Editor.MonoGame.sln` 0 erreur. **Manque** : vérifier à la main que les panneaux d'entités et de détails se comportent comme avant (recette T5.2).
- Commit : `refactor(editor): share the world editing operations of the panels`

### ⏳ T2.2 — Commandes de lecture

- Objectif : `world.entities` (arbre : id, nom, chemin, composants avec leur id et leur parent), `entity.get` (propriétés éditables de l'entité et de ses composants : nom, type, valeur JSON, lecture seule), `component.types`, `selection.get`, `logs.read`. `logs.read` lit `LoggerEditor.Entries` (déjà enregistré, `GameEditor.cs:407`) et `EditorDiagnosticsBuffer` sur le fil du jeu, sans nouveau logger ; un curseur (numéro de séquence) reste valable après un `EntriesTrimmed` et signale les entrées perdues ; plafonds de P11.
- Fichiers : `CasaEngine.Editor/Remote/Commands/*`, `CasaEngine.Editor/Log/LoggerEditor.cs` seulement si un numéro de séquence y manque (ajout additif), tests.
- Validation : tests sur un monde de test ; test du curseur de `logs.read` à travers un élagage de `LoggerEditor` (entrées perdues signalées, pas de doublon) ; test des plafonds en nombre et en taille ; build.
- Commit : `feat(editor): add the remote read commands`

## Phase 3 — Modification, capture et Play

### ⏳ T3.1 — Commandes de modification

- Objectif : `world.create`, `world.open`, `project.save`, `entity.create` (parent optionnel), `entity.delete`, `entity.rename`, `entity.duplicate`, `component.add`, `component.remove`, `property.set`, `selection.set`, `history.undo`, `history.redo`. Règles de P5 et P8.
- Fichiers : `CasaEngine.Editor/Remote/Commands/*`, `GameEditor.cs` (création de monde sur le motif de `CreateProject`, ouverture sans boîte de dialogue), tests.
- Validation : tests (chaque commande, refus en Play, refus si non enregistré, Undo d'une commande) ; **contexte d'historique** : un autre contexte rendu actif, puis `history.undo` n'annule que le changement fait par commande dans le monde et laisse l'autre pile intacte ; **sauvegarde en échec** (par exemple écriture des modes de jeu impossible) : `project.save` répond une erreur, pas un succès ; placement et retrait de composants selon P5 ; et tests de sécurité : `component.add` avec `"Process"` ou un type hors liste blanche refusé sans instancier ; `property.set` sur une propriété non éditable, en lecture seule, `Owner`, un chemin pointé, une énumération indéfinie, NaN ou un texte trop long refusé ; `world.create` avec `../x`, un chemin absolu, `Gameplay.csproj`, `CON`, un nom contenant `:` ou un monde existant refusé ; dossier `C:\proj-x` non confondu avec `C:\proj` ; build.
- Commit : `feat(editor): add the remote edit commands`

### ⏳ T3.2 — Capture et Play

- Objectif : `capture.window` (bureau entier, code de `CaptureAutomationScreenshot` partagé) et `capture.viewport` (rendu du viewport du monde), terminées après le dessin de l'image ; `play.start`, `play.stop`, `play.status`, terminées quand `EditorPlayModeService` a changé d'état. Fichiers PNG dans le dossier de P8 ; réponse : chemin, taille. Le mode d'ouverture `FileMode.CreateNew` ne vaut que pour les captures à distance ; les captures de l'automatisation existante (`--screenshot-out`, qui réutilisent un même chemin) passent à `FileMode.Create` (écrase et tronque, corrige `File.OpenWrite`).
- Fichiers : `CasaEngine.Editor/Remote/Commands/*`, `GameEditor.cs`, tests de la partie sans GPU.
- Validation : tests des réponses différées ; capture vérifiée au smoke T5.1 ; build.
- Commit : `feat(editor): add the remote capture and play commands`

## Phase 4 — Outils

### ⏳ T4.1 — CLI `casa-editor`

- Objectif : `casa-editor editors` (éditeurs trouvés), `casa-editor commands`, `casa-editor call <commande> [json]`, `casa-editor run <script.json>` (suite d'appels, arrêt à la première erreur). Option `--pid` quand plusieurs éditeurs servent. Sortie JSON, code de sortie non nul en cas d'erreur.
- Fichiers : `Tools/CasaEngine.Editor.Cli/*` (nouveau), `.sln`, `CasaEngine.Tests/CasaEngine.Tests.csproj` (référence au projet CLI), tests de l'analyse des arguments et du script.
- Validation : tests ; build ; appel réel au smoke T5.1.
- Commit : `feat(tools): add the casa-editor command line`

### ⏳ T4.2 — Serveur MCP

- Objectif : exécutable MCP sur stdio avec `ModelContextProtocol` 2.4.0 (ajout dans `Directory.Packages.props`), outils générés depuis `commands.list` ou repli de P7, captures rendues en image. API du SDK lue dans sa documentation officielle et citée dans la note de validation.
- Fichiers : `Tools/CasaEngine.Editor.Mcp/*` (nouveau), `Directory.Packages.props`, `.sln`.
- Validation : build ; poignée de main et liste des outils au smoke T5.1.
- Commit : `feat(tools): add the editor MCP server`

## Phase 5 — Documentation et vérification

### ⏳ T5.1 — Documentation, déclaration MCP et smoke de bout en bout

- Objectif : `docs/editor/editor-remote-control.md` (activation, commandes, erreurs, sécurité, limites), `docs/README.md`, section de `AGENTS.md` §7 et de `.claude/rules` si une règle par chemin le demande, `.mcp.json` et `.vscode/mcp.json` (P9) ; smoke de la Validation globale.
- Validation : smoke réussi, résultats notés ici.
- Commit : `docs(editor): document the remote control for tools and agents`

### ⏳ T5.2 — Vérification indépendante et recette de l'auteur

- Étapes : validation globale ; vérificateur neuf (`verifier`) sur la branche (contrat d'ADR-0092, P1 à P11, aucune exception dans `Update`, ouverture de `\\localhost\pipe\<nom>` refusée, tube restreint à l'utilisateur, tests de sécurité de T1.1, T1.2 et T3.1 présents et verts) ; recette de l'auteur (panneaux d'entités et d'inspecteur inchangés, pilotage par MCP depuis Claude Code).
- Validation : CONFIRMED et accord de l'auteur ; sinon 🧪 avec ce qui manque.
- Commit : `docs(ai-agent): record the check of the editor remote control`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Valider ou modifier P1 à P11 avant l'exécution. | T0.1 |
| O3 | Sécurité Windows du tube (S2, S3) raisonnée par le relecteur, non reproduite : refus NETWORK et comportement en administrateur à confirmer par un test réel en T1.2 et T5.2 ; si l'un ne tient pas, ⚠️ Blocked. | T1.2, T5.2 |
| O2 | Le SDK MCP permet-il de déclarer des outils à l'exécution ? À lire dans sa documentation ; sinon repli de P7. | T4.2 |
| R1 | Risque : tant que le serveur tourne, un processus local du même utilisateur peut modifier et sauvegarder le projet. Atténuation : P1 (utilisateur courant), P2 (désactivé par défaut), P8 (chemins sous le projet). | T1.2, T3.1 |
| R2 | Risque : T2.1 touche deux panneaux très utilisés. Atténuation : tests du service, comportement inchangé, recette de l'auteur. | T2.1 |

## Hors périmètre

- Migration des 23 options d'`EditorAutomationOptions` vers le registre de commandes (plus tard, commande par commande).
- Reparentage d'entités, édition des assets autres que le monde (matériaux, particules, écrans, sons), gestion des panneaux et du docking.
- Accès réseau ou distant (autre machine) ; plusieurs clients simultanés.
- Mode sans fenêtre (éditeur invisible) : l'éditeur tourne avec sa fenêtre.
