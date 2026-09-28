# Plan agent IA — Service de sauvegarde de partie

Plan d'exécution du service de sauvegarde générique demandé par le portage Alundra
(plan parent `docs/plan-e16-etat-partie.md`, étape E16 de `docs/plan-conversion-totale.md`).
Les décisions D1 → D5 ci-dessous ont été arbitrées avec l'auteur le 2026-09-27 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Statut** : proposé le 2026-09-27, **en attente d'approbation**. Révision 2 : relecture de plan
> (REVISE, quatre P2 corrigés) puis revue de sécurité (quinze constats, tous tranchés au §« Revue de
> sécurité »), puis relecture fraîche de clôture : **READY**. Fichier non commité : le sous-module est sur `main` avec une modification en cours de
> l'auteur (`CasaEngine.Launcher/Program.cs`), donc aucune branche n'a été créée pour ne pas déplacer
> son checkout. Il sera commité en T0.1, sur la branche du chantier.
>
> **Révision 3 (2026-09-28)**, après le merge du chantier Yarn dans `main` (`793d1ee8`) : les
> citations du code sont revérifiées, inchangées. L'ADR de T0.1 prend le numéro **0044**, les
> ADR-0042 et 0043 venant du chantier Yarn. La ligne citée du document Yarn devient `:164`. Une note
> est ajoutée sur les variables Yarn d'Alundra (plan parent E16.f). Relecture fraîche de la
> révision 3 : **READY**. **Approuvé par l'auteur le 2026-09-28** (« fait tout E16 », mode ASK),
> branche `chantier/save-game-service` créée depuis `main` (`793d1ee8`).

## Objectif

Donner au moteur un service de sauvegarde de partie, au runtime, sur le modèle des trois moteurs de
référence :

- **emplacements nommés** dans un dossier propre à l'utilisateur, comme `user://` (Godot),
  `Application.persistentDataPath` (Unity) et `Saved/SaveGames` (Unreal) ;
- **un objet de sauvegarde** rempli par le jeu et écrit d'un bloc dans un emplacement, comme
  `USaveGame` et `SaveGameToSlot` (Unreal) ;
- **une seule méthode de sérialisation dans les deux sens**, comme `Serialize(FArchive&)` (Unreal),
  pour que lecture et écriture ne puissent pas diverger ;
- **deux formats** au choix de l'appelant : JSON lisible (débogage) et binaire compact (jeu) ;
- une **version de données** portée par le fichier, lue par l'objet pour migrer, comme
  `ULocalPlayerSaveGame` (Unreal) ;
- une **somme de contrôle** sur le binaire contre la corruption accidentelle, et une **écriture par
  remplacement** qui laisse l'ancien contenu ou le nouveau, au mieux de ce que le système de fichiers
  garantit ;
- une **lecture défensive** : un fichier de sauvegarde peut venir d'un autre joueur ou être édité à
  la main ; le lire ne fait jamais planter le jeu et ne consomme jamais de mémoire sans borne.

Premier consommateur : la DLL Alundra (tranches E16.c à E16.e du plan parent). Ce qui n'est pas livré
est dans « Hors périmètre ».

## État vérifié du dépôt (2026-09-27)

- **Aucun service de sauvegarde.** À l'exécution, seuls deux fichiers s'écrivent sur disque : les
  réglages d'affichage (`CasaEngine/Framework/Application/DisplaySettingsPersistence.cs:36`, `:47`,
  écriture non atomique ; sa lecture capture toute exception et revient à une valeur de repli,
  `:10-28`) et les réglages de projet
  (`CasaEngine/Framework/Configuration/Project/ProjectSettingsHelper.cs:101`). Aucun usage
  d'`Environment.SpecialFolder` dans le runtime.
- **Le runtime est en lecture seule par contrat.** `AGENTS.md` §9.9 : « Sauvegarde et export
  appartiennent à l'éditeur et à l'outillage (`CasaEngine.EditorServices`, `CasaEngine.Editor`) ;
  chargement et exécution au runtime (`CasaEngine`) ». Le chantier archivé
  `ai-agent/tasks/archive/runtime-save-jobject-cleanup-plan.md` a retiré tous les `Save(JObject)`
  de `CasaEngine`. Une sauvegarde de partie du joueur est par nature une écriture au runtime :
  voir le prérequis du programme.
- **Pas de localisateur de services** : les services globaux sont des propriétés statiques
  initialisées à la déclaration de `GameSettings` (`CasaEngine/Framework/Application/GameSettings.cs:9-12`).
- `ProjectSettings.ProjectName` vaut par défaut `"Project name undefined"` (`ProjectSettings.cs:11`) ;
  il n'y a ni société ni produit distincts. Le projet Alundra le fixe à `"AlundraGame"`
  (`alundra-project/AlundraGame.json:3`, dépôt parent).
- Sérialisation disponible : Newtonsoft.Json 13.0.4 (`Directory.Packages.props:19`), pas de
  System.Text.Json dans le runtime ; `BinaryWriter`/`BinaryReader` sont dans la BCL. Aucun
  `JsonConvert.DefaultSettings` ni `TypeNameHandling` dans le code du dépôt.
- MGUI interprète par défaut le formatage en ligne d'un texte (`MGUI/MGUI.Core/UI/MGTextBlock.cs:837`,
  balises analysées par `MGUI/MGUI.Core/UI/Text/FormattedTextParser.cs:145`).
- Tests : `CasaEngine.Tests`, un dossier par système, dossiers temporaires sous
  `Path.GetTempPath()` (`CasaEngine.Tests/ContentBrowser/FileOperationServiceTests.cs:21`).
- Dernière ADR : ADR-0043 (`docs/decisions/README.md`) ; les ADR-0042 et 0043 viennent du chantier
  Yarn, mergé dans `main` le 2026-09-28.
- `docs/engine/yarn_spinner_integration.md:164` prévoit « la sauvegarde des variables dès la V2 » :
  ce service pourra la porter plus tard, hors de ce plan. Pour Alundra, la question ne se pose pas :
  ses variables Yarn sont ses propres drapeaux (plan parent, tranche E16.f, ADR-0010 et ADR-0011 du
  dépôt parent), sauvegardés par l'objet de sauvegarde du jeu.
- **Modification préexistante de l'auteur** : `CasaEngine.Launcher/Program.cs` (modifié, non indexé).
  Ne jamais l'indexer.

Sources (consultées le 2026-09-27) :
- Godot : <https://docs.godotengine.org/en/stable/tutorials/io/saving_games.html>,
  <https://docs.godotengine.org/en/stable/tutorials/io/data_paths.html>.
- Unity : <https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html>,
  <https://learn.unity.com/tutorial/implement-data-persistence-between-sessions>.
- Unreal : <https://dev.epicgames.com/documentation/en-us/unreal-engine/saving-and-loading-your-game-in-unreal-engine>,
  <https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Runtime/Engine/UGameplayStatics>,
  <https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Runtime/Engine/ULocalPlayerSaveGame>.
- .NET, sécurité des désérialiseurs (une sauvegarde partagée entre joueurs est une entrée non
  fiable) : <https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-security-guide>.
- Newtonsoft : `TypeNameHandling` (défaut `None`)
  <https://www.newtonsoft.com/json/help/html/P_Newtonsoft_Json_JsonSerializerSettings_TypeNameHandling.htm>,
  `DateParseHandling` (défaut `DateTime`)
  <https://www.newtonsoft.com/json/help/html/P_Newtonsoft_Json_JsonSerializerSettings_DateParseHandling.htm>,
  `JsonConvert.DefaultSettings` (appliqué aussi à `ToObject`)
  <https://www.newtonsoft.com/json/help/html/P_Newtonsoft_Json_JsonConvert_DefaultSettings.htm>,
  `MaxDepth` à 64 par défaut depuis la 13.0.1 <https://github.com/JamesNK/Newtonsoft.Json/releases/tag/13.0.1>.
- Windows, noms réservés et caractères interdits :
  <https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file> ;
  `Environment.GetFolderPath` rend une chaîne vide si le dossier n'existe pas :
  <https://learn.microsoft.com/en-us/dotnet/api/system.environment.getfolderpath>.

## Prérequis du programme

- **O1 accepté par l'auteur** (préciser §9.9 : les assets restent à l'éditeur, la sauvegarde de
  partie du joueur est un service runtime). **Si O1 est refusé, le programme s'arrête avant T0.1 et
  se replanifie** : aucune écriture sur disque n'est ajoutée à `CasaEngine` sans cette règle.
  **Levé le 2026-09-27 : O1 accepté par l'auteur.**

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Un service de sauvegarde **générique dans le moteur**, inspiré de Godot, Unity et Unreal. |
| D2 | Contrat **objet de sauvegarde** (modèle Unreal `USaveGame` + `SaveGameToSlot`) : le jeu remplit un objet, le service l'écrit dans un emplacement nommé. |
| D3 | **Deux formats**, au choix : JSON lisible et binaire compact. |
| D4 | Fichiers dans un **dossier propre à l'utilisateur**. |
| D5 | Premier consommateur : le portage Alundra, étape E16, **après E15**. |

Choix techniques proposés par ce plan (approuvés avec lui) :

- **Aucune instanciation pilotée par le fichier.** L'appelant donne le type à charger
  (`TryLoad<T>()`) ; le fichier ne nomme aucune classe, et le code du service n'utilise ni
  `TypeNameHandling`, ni `JsonConvert`, ni `ToObject`, ni `JsonSerializer`, ni `BinaryFormatter`
  (vérifié par recherche en T2.2 et T4.2). Cela écarte la désérialisation pilotée par un nom de type
  (CWE-502). **Risques résiduels**, traités par les gardes de T1.1 à T3.1 : l'épuisement de
  ressources et les valeurs qui ont la bonne forme mais un sens invalide.
- **Intégrité, pas authenticité.** Le CRC-32 ne détecte que la corruption accidentelle : il se
  recalcule librement, et le JSON n'en a pas. Les fichiers ne sont pas authentifiés ; toutes les
  gardes de lecture et la validation par le jeu doivent tenir face à un fichier fabriqué.
- **Archive symétrique à `ref`** plutôt que réflexion : `archive.Value("hp", ref hp)` lit ou écrit
  selon le sens. Pas de réflexion, rien à configurer pour le trimming, ordre déterministe.
- **JSON nommé, binaire positionnel.** Le JSON retrouve chaque champ par son nom ; le binaire les
  écrit dans l'ordre d'appel, et c'est la version de données qui garde la compatibilité, comme
  `FArchive::CustomVer`.
- **CRC-32 écrit dans le code** (table précalculée) plutôt que le paquet NuGet
  `System.IO.Hashing` : pas de nouvelle dépendance. Le JSON reste éditable à la main, c'est son
  rôle de format de débogage.
- **Stockage concret, sans interface.** Une seule implémentation (fichiers) : pas d'abstraction
  tant qu'il n'y a pas de second backend (§9.2). Le point d'extension d'Unreal (`ISaveGameSystem`)
  est noté pour plus tard.
- **API synchrone.** Une sauvegarde d'Alundra pèse quelques kilo-octets ; l'API asynchrone
  qu'Unreal recommande est hors périmètre.
- **Erreurs** : un mauvais usage développeur lève une exception (`ArgumentException` pour un nom
  d'emplacement, `InvalidOperationException` pour le dossier du projet) ; tout le reste — fichier
  absent, illisible, trop gros, corrompu, disque plein, fichier verrouillé — devient un **résultat**
  journalisé avec son contexte, jamais une exception vers la boucle de jeu (§9.10). Une exception
  levée par le code du jeu dans son `Serialize` n'est pas avalée : elle remonte.
- **Aucune démo** : la fonctionnalité n'a rien de visible, la règle §6 des démos ne s'applique pas ;
  la preuve est la suite de tests plus l'usage réel par Alundra.

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/save-game-service`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché (`dotnet build CasaEngine.MonoGame.sln`) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` dès qu'une tâche touche du code testé (le projet de tests se builde explicitement). Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- **Travail sensible à la sécurité** (lecture d'entrées non fiables, validation de chemins) :
  exécution par un exécutant de sécurité, jamais par un exécutant général.
- Rappel moteur : le service ne tourne pas dans un chemin chaud (appel ponctuel du jeu).

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` : 0 erreur.
- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj` puis
  `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build --blame-hang-timeout 60s` : aucun échec.
- Clôture : un **verifier frais** sur tout le chantier (format de données et lecture d'entrées non
  fiables, donc revue indépendante obligatoire), contre l'acceptation de chaque tâche.
- Pas de smoke visuel : la recette en jeu est celle du plan parent (tranche E16.d).

---

## Phase 0 — Cadre

### ✅ T0.1 — ADR et règle §9.9

- Objectif : consigner D1 à D5 et les choix techniques de ce plan ; lever la contradiction avec
  §9.9 (O1, prérequis du programme).
- Fichiers : `docs/decisions/0044-runtime-save-games.md`, `docs/decisions/README.md`,
  `AGENTS.md` (§9.9), `ai-agent/README.md` (ligne du tableau), ce plan.
- Étapes :
  1. Créer la branche `chantier/save-game-service` depuis `main` (O4 : la modification de l'auteur
     suit dans l'arbre de travail, jamais indexée) et y commiter ce plan.
  2. Écrire l'ADR-0044 avec le skill `adr` : contexte (§9.9, chantier de nettoyage archivé, besoin
     d'Alundra), décision (D1 à D5, choix techniques), conséquences, sources citées ; la phrase
     « CRC-32 detects accidental corruption only; save files are not authenticated; all bounds checks
     and consumer validation must hold for attacker-crafted files » y figure mot pour mot.
  3. Préciser §9.9 (« sauvegarde et export **des assets** » ; « la sauvegarde de partie du joueur
     est un service runtime, ADR-0044 »), selon la réponse de l'auteur à O1.
  4. Ajouter ce plan au tableau d'`ai-agent/README.md`.
- Validation : relecture ; liens de l'index valides.
- Commit : `docs(save-games): record the runtime save-game service decision`
- Validation (2026-09-28) : ADR-0044 écrite (la phrase sur le CRC-32 y figure mot pour mot), ligne
  d'index ajoutée ; §9.9 d'`AGENTS.md` précisé ; plan ajouté au tableau d'`ai-agent/README.md` ; liens
  vérifiés (fichiers présents). Numéro 0044 libre sur toutes les branches du moteur.

---

## Phase 1 — Stockage

### ✅ T1.1 — Emplacements sur disque

- Objectif : `SaveGameFileStorage`, qui résout le dossier de sauvegarde, valide les noms et lit ou
  écrit un emplacement sans jamais laisser d'exception d'entrée-sortie remonter.
- Fichiers : `CasaEngine/Framework/SaveGames/SaveGameFileStorage.cs`,
  `CasaEngine/Framework/SaveGames/SaveGameNames.cs` (nouveaux),
  `CasaEngine.Tests/SaveGames/SaveGameFileStorageTests.cs` (nouveau).
- Étapes :
  1. **Dossier par défaut**, résolu au **premier appel d'entrée-sortie**, jamais à la construction
     (les réglages du projet peuvent ne pas être encore chargés) :
     `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` / `ProjectName` / `SaveGames`.
     `InvalidOperationException` (usage développeur invalide) si :
     - `GetFolderPath` rend une chaîne vide ;
     - `ProjectName` est vide ou vaut encore `"Project name undefined"` (`ProjectSettings.cs:11`) ;
     - `ProjectName` ne passe pas la règle des noms de dossier (étape 2) : pas d'assainissement
       silencieux, qui ferait partager un dossier à deux projets ;
     - le chemin complet obtenu ne commence pas par la racine `LocalApplicationData`.
     Constructeur qui accepte un dossier explicite (tests, outils).
  2. **Règles de nom** (`SaveGameNames`), en liste blanche ASCII :
     - emplacement : `^[a-z0-9_-]{1,32}$` (minuscules seules, donc aucune collision de casse entre
       Windows et Linux) ;
     - dossier du projet : `^[A-Za-z0-9](?:[A-Za-z0-9 _-]{0,62}[A-Za-z0-9_-])?$` (ni espace ni
       point final) ;
     - pour les deux, rejet explicite des noms réservés de Windows, quelle que soit la casse et même
       suivis d'une extension : `CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`.
     Un nom d'emplacement refusé lève `ArgumentException`.
  3. **Lecture** : `Exists` ; `TryRead`, qui compare la longueur du fichier à une **taille maximale
     d'emplacement** (constante, 1 Mio) **avant** de lire, puis lit au plus ce plafond + 1 octet ; au-delà,
     résultat « trop gros » sans allocation.
  4. **Écriture** : fichier temporaire au nom unique `<emplacement>.<guid>.tmp` dans le même dossier,
     ouvert en `FileMode.CreateNew` et `FileShare.None` (échoue si un fichier existe déjà, donc ne
     suit pas un lien préexistant), vidé sur disque (`FileStream.Flush(true)`), puis
     `File.Move(..., overwrite: true)`. En cas d'échec, le temporaire est supprimé et l'emplacement
     précédent reste intact. Les `.tmp` orphelins ne sont pas nettoyés (hors périmètre).
  5. `Delete` ; `EnumerateSlots` : seulement les fichiers d'extension exactement `.sav` (comparaison
     ordinale) dont le nom passe la règle d'emplacement ; tri ordinal, stable.
  6. **Entrée-sortie** : toute `IOException` ou `UnauthorizedAccessException` de `TryRead`, `Write`,
     `Delete` ou `EnumerateSlots` devient un résultat « erreur d'entrée-sortie », journalisé avec
     l'opération et le chemin.
- Validation : tests —
  - aller-retour d'octets, écrasement, énumération triée, dossier créé à la première écriture ;
  - noms d'emplacement refusés : séparateur, `..`, vide, 33 caractères, majuscule, `:` (flux
    alternatif), point ou espace final, `<>"|?*`, caractère de contrôle, caractère non ASCII,
    `con`, `nul`, `com1`, `lpt9` ;
  - aucun fichier temporaire laissé après une écriture réussie ;
  - **écriture qui échoue en cours de route** (source de données qui lève au milieu) : l'ancien
    contenu de l'emplacement est inchangé octet pour octet et aucun `.tmp` ne reste ;
  - un `.tmp` orphelin de même préfixe ne bloque pas l'écriture, reste intact, et n'apparaît ni
    dans `EnumerateSlots` ni dans `Exists` ; un fichier au nom invalide ou d'extension `.SAV` posé à
    la main n'apparaît pas dans `EnumerateSlots` ;
  - fichier de 1 Mio + 1 octet → « trop gros », aucune exception ;
  - fichier verrouillé (ouvert en `FileShare.None` par le test) pendant `TryRead`, `Write` et
    `Delete` → « erreur d'entrée-sortie », emplacement précédent intact, aucune exception ;
  - le dossier par défaut suit `ProjectName` lu au premier appel, pas à la construction ;
  - `ProjectName` par défaut, vide, contenant `:`, `..` ou un séparateur → `InvalidOperationException`
    au premier appel ; `"AlundraGame"` accepté.
- Commit : `feat(save-games): add slot file storage`
- Validation (2026-09-28, exécutant de sécurité) : `SaveGameNames` et `SaveGameFileStorage`
  (internes, une seule classe concrète) ; 76 tests couvrant toute la liste de validation ; build de
  la solution à 0 erreur ; `CasaEngine.Tests` 2067/2067. Choix consignés :
  - les expressions régulières utilisent `\A…\z`, car `$` laisse passer un saut de ligne final ;
  - le dossier par défaut est mis en cache après la première résolution réussie ;
  - `Write(slot, Action<Stream>)` sert de point d'injection : les erreurs d'entrée-sortie deviennent
    un résultat, toute autre exception remonte après suppression du `.tmp` ;
  - les tests n'écrivent jamais sous le vrai `LocalApplicationData` (O3).
  Noms réservés : la liste de Microsoft (page citée, relue le 2026-09-28) est CON, PRN, AUX, NUL,
  COM1-COM9, COM¹-COM³, LPT1-LPT9, LPT¹-LPT³ ; `COM0` et `LPT0` n'y figurent pas, et les exposants sont
  exclus par la liste blanche ASCII. `Write` n'impose pas le plafond de 1 Mio : T3.1 s'en charge (le
  service sérialise en mémoire et refuse une sauvegarde trop grosse avant d'écrire).

---

## Phase 2 — Archive et formats

### ✅ T2.1 — Archive symétrique

- Objectif : `SaveGameArchive`, API unique de lecture et d'écriture, et le contrat
  `ISaveGameData`.
- Fichiers : `CasaEngine/Framework/SaveGames/SaveGameArchive.cs`, `ISaveGameData.cs`,
  `SaveGameDataException.cs` (nouveaux), tests associés.
- Étapes :
  1. `ISaveGameData` : `int LatestDataVersion { get; }` et `void Serialize(SaveGameArchive archive)`.
  2. `SaveGameArchive` : `IsLoading`, `DataVersion` (celle du fichier au chargement, la plus récente
     à l'écriture), `Value(string name, ref T value)` pour `bool`, `byte`, `short`, `ushort`, `int`,
     `uint`, `long`, `float`, `string`, et les tableaux de ces types entiers ; `BeginObject(name)` /
     `EndObject()` pour imbriquer.
  3. Tableaux à longueur fixe : `Value(name, array)` remplit le tableau fourni et signale une
     longueur différente comme donnée invalide (les tableaux d'Alundra ont des tailles fixes).
  4. Les `float` non finis (`NaN`, infinis) sont refusés comme donnée invalide, à l'écriture comme à
     la lecture, dans les deux formats.
  5. Toute erreur de donnée est signalée par `SaveGameDataException` (type interne), qui nomme le
     champ ; c'est le seul type que le service convertit en résultat (T3.1).
- Validation : tests via les deux implémentations de T2.2.
- Commit : `feat(save-games): add the symmetric save-game archive`
- Validation (2026-09-28, exécutant de sécurité) : `ISaveGameData` (public), `SaveGameArchive`
  (public abstraite, constructeur et primitives `private protected` : un jeu ne peut pas en dériver),
  `SaveGameDataException` (interne, chemin du champ compris, par exemple `player.inventory[3]`).
  Les contrôles communs aux deux formats sont dans la base :
  - plage du type C# pour chaque entier ;
  - flottants non finis refusés dans les deux sens ;
  - chaîne nulle ou demi-paire UTF-16 refusée ;
  - longueur d'un tableau comparée avant tout élément ;
  - profondeur d'objets limitée à 32.

  Tableaux pris en charge : `byte`, `short`, `ushort`, `int`, `uint`, `long`. En plus de la
  validation par T2.2, 46 tests via une archive d'essai qui ne contrôle rien elle-même. Build à
  0 erreur, `CasaEngine.Tests` 2113/2113.

### ⏳ T2.2 — Formats JSON et binaire

- Objectif : les deux implémentations de l'archive et l'enveloppe de fichier.
- Fichiers : `CasaEngine/Framework/SaveGames/JsonSaveGameArchive.cs`,
  `BinarySaveGameArchive.cs`, `SaveGameEnvelope.cs`, `Crc32.cs` (nouveaux), tests associés.
- Étapes :
  1. **Enveloppe JSON** : un document `{ "container": "casaengine-savegame", "containerVersion": 1,
     "dataVersion": n, "metadata": { … }, "data": { … } }`, entiers écrits comme entiers. Lecture par
     `JToken.Load` sur un `JsonTextReader` aux réglages explicites : `DateParseHandling.None`,
     `FloatParseHandling.Double`, `MaxDepth = 64`, et `JsonLoadSettings` avec
     `DuplicatePropertyNameHandling.Error`. Jamais `JsonConvert`, `ToObject` ni `JsonSerializer`.
     Chaque valeur est vérifiée par son `JTokenType` avant conversion : entier attendu → `Integer`
     seulement, et dans les bornes du type C# cible ; `null` → donnée invalide.
  2. **Enveloppe binaire** : magie de 4 octets, version du conteneur, version des données, nombre de
     métadonnées puis paires de chaînes (préfixe `int32` explicite, UTF-8 strict
     `new UTF8Encoding(false, true)`), charge utile positionnelle en petit-boutiste, CRC-32 final sur
     tout ce qui précède. Jamais `BinaryReader.ReadString`.
  3. **Ordre de lecture défensif du binaire** : taille minimale vérifiée d'abord (magie + versions +
     CRC), puis **CRC vérifié avant tout décodage**, métadonnées comprises ; chaque longueur comparée
     sous la forme `longueur > restant` (jamais `position + longueur`, qui peut déborder) avant toute
     allocation ; nombre de métadonnées borné par `restant / taille minimale d'une paire` ; clé de
     métadonnée en double → donnée invalide ; version des données négative → donnée invalide ;
     version du conteneur nulle, négative ou inconnue → conteneur non pris en charge.
  4. Détection du format à la lecture, après un éventuel BOM UTF-8 et des espaces : magie binaire ou
     `{` ; sinon donnée invalide.
  5. Lecture des métadonnées seules, sans décoder la charge utile (liste des emplacements), avec les
     mêmes gardes.
  6. Un fichier illisible ne lève jamais d'exception vers l'appelant : il devient un résultat
     (`Corrupted`, `InvalidData` avec le contexte, ou `UnsupportedContainer`).
- Validation : tests —
  - aller-retour identique dans les deux formats pour un objet couvrant tous les types ; deux
    écritures du même objet → octets identiques (déterminisme) ; une chaîne au format de date ISO
    revient identique ; métadonnées lues sans la charge utile ;
  - binaire : octet modifié → `Corrupted` ; fichier tronqué, plus court que magie + versions + CRC →
    `Corrupted` ; préfixe de longueur négatif, égal à `int.MaxValue`, à `0x7FFFFFF0`, ou plus grand
    que le reste → `Corrupted` ou `InvalidData` sans allocation (le CRC est recalculé pour que ce
    soit bien la borne qui arrête la lecture) ; nombre de métadonnées énorme (CRC recalculé) ; clé en
    double ; UTF-8 invalide ; version de données négative ; version de conteneur 0, négative ou
    supérieure → `UnsupportedContainer` ;
  - JSON : document mal formé → `InvalidData` ; champ manquant → `InvalidData` nommant le champ ;
    `1.5`, `true`, `null` ou une chaîne dans un champ entier → `InvalidData` nommant le champ ;
    70000 lu dans un `short` → `InvalidData` ; `NaN` ou `1e999` dans un `float` → `InvalidData` ;
    clé en double → `InvalidData` ; imbrication de 10 000 niveaux → `InvalidData` ;
    `containerVersion` supérieure → `UnsupportedContainer` ; document précédé d'un BOM et d'espaces
    → lu normalement ;
  - lecture des métadonnées seules sur chacun de ces fichiers : même résultat, aucune exception ;
  - recherche dans `CasaEngine/Framework/SaveGames` : aucune occurrence de `TypeNameHandling`,
    `JsonConvert.`, `ToObject`, `JsonSerializer`, `BinaryFormatter`, `ReadString(`.
- Commit : `feat(save-games): add JSON and binary save-game formats`

---

## Phase 3 — Service

### ⏳ T3.1 — `SaveGameService`

- Objectif : l'API publique du jeu, sur le modèle de `UGameplayStatics` (Unreal).
- Fichiers : `CasaEngine/Framework/SaveGames/SaveGameService.cs`, `SaveGameFormat.cs`,
  `SaveGameLoadResult.cs`, `SaveGameSaveResult.cs`, `SaveGameSlotInfo.cs` (nouveaux),
  `CasaEngine/Framework/Application/GameSettings.cs` (propriété `SaveGames`), tests associés.
- Étapes :
  0. (Ajout du 2026-09-28, suite de T1.1.) Le service sérialise en mémoire ; une sauvegarde qui
     dépasse le plafond de 1 Mio de T1.1 est refusée (`TooLarge`) sans rien écrire, pour qu'aucun
     emplacement écrit ne soit illisible ensuite.
  1. `SaveGameSaveResult Save(slot, ISaveGameData data, SaveGameFormat format,
     IReadOnlyDictionary<string,string>? metadata)` : `Saved`, `IoError`, `TooLarge` (étape 0) (ou `InvalidData` si
     l'objet écrit une valeur refusée, par exemple un `float` non fini).
  2. `SaveGameLoadResult TryLoad<T>(slot, out T? data) where T : ISaveGameData, new()` ; résultats
     `Loaded`, `NotFound`, `TooLarge`, `Corrupted`, `UnsupportedContainer`, `NewerDataVersion`
     (fichier plus récent que le jeu : refusé, comme les versions d'Unreal qui empêchent un code
     ancien de charger une donnée nouvelle), `InvalidData` (avec le message de contexte), `IoError`.
     **`data` vaut `default` pour tout résultat autre que `Loaded`** : le jeu ne voit jamais un objet
     à moitié rempli.
  3. Frontière des exceptions : le service convertit en résultat, toujours journalisé avec son
     contexte, seulement `SaveGameDataException`, les exceptions du lecteur JSON et les exceptions
     d'entrée-sortie ; toute autre exception, notamment levée par le `Serialize` du jeu, remonte.
  4. `Exists`, `Delete` (→ `SaveGameSaveResult`), `ListSlots()` → `SaveGameSlotInfo` (nom, format,
     version de données, métadonnées, date d'écriture UTC, ou l'état « illisible » avec sa raison).
  5. `GameSettings.SaveGames { get; } = new()`, initialisé comme les autres propriétés de
     `GameSettings` (`GameSettings.cs:9-12`) ; la construction ne touche pas au disque, le dossier se
     résout au premier appel d'entrée-sortie (T1.1). Les tests construisent leur propre service sur
     un dossier temporaire.
- Validation : tests — sauvegarde puis chargement dans chaque format ; emplacement absent ; fichier
  trop gros ; fichier corrompu ; version de données plus récente refusée ; version plus ancienne
  chargée et visible par `archive.DataVersion` (migration) ; `data == default` pour chaque résultat
  autre que `Loaded` ; fichier verrouillé → `IoError` pour `Save`, `TryLoad` et `Delete` ; un
  `Serialize` qui lève `InvalidOperationException` → l'exception remonte ; liste des emplacements
  avec leurs métadonnées ; **`ListSlots` avec un emplacement corrompu et un emplacement trop gros
  parmi des emplacements sains** : les sains sont listés, les autres signalés comme illisibles avec
  leur raison, aucune exception.
- Commit : `feat(save-games): add the save-game service`

---

## Phase 4 — Documentation et clôture

### ⏳ T4.1 — Documentation

- Objectif : documenter l'usage pour un jeu.
- Fichiers : `docs/engine/save-games.md` (nouveau), `docs/README.md` (index).
- Étapes : résumé, extrait d'usage (objet de sauvegarde, `Serialize`, sauvegarde et chargement),
  formats et enveloppe, migration par version, limites (synchrone, pas de chiffrement,
  remplacement « ancien ou nouveau contenu, au mieux » sans garantie de durabilité du renommage),
  et une section **« Trust model and consumer contract »** :
  - les fichiers ne sont pas authentifiés (phrase de l'ADR-0044) ;
  - l'archive ne garantit que la forme et la plage du type C# ; **le jeu valide le sens de chaque
    valeur** (ids, index, compteurs, coordonnées) après `Loaded` et **avant** de toucher à son état
    vivant, puis applique l'objet d'un bloc ou pas du tout ;
  - les métadonnées sont du texte brut non fiable : longueur bornée à l'affichage, et formatage en
    ligne de MGUI désactivé pour les afficher.
- Validation : relecture ; liens valides.
- Commit : `docs(save-games): document the save-game service`

### ⏳ T4.2 — Vérification de clôture

- Objectif : verifier frais sur le chantier entier ; tableau d'`ai-agent/README.md` à jour.
- Validation : verdict **CONFIRMED** ; suites vertes ; recherche de T2.2 (types interdits) refaite
  sur l'état final.
- Commit : `docs(save-games): close the save-game service plan`

---

## Revue de sécurité (2026-09-27)

Revue en lecture seule par un `security-reviewer` frais sur la révision 1 de ce plan : aucun P0 ni
P1. Chaque constat est tranché ci-dessous ; un `FIX` renvoie à l'étape ou à la validation qui
l'applique.

| Réf | Prio | Constat | Décision | Où |
|---|---|---|---|---|
| F1 | P2 | Aucune taille maximale de fichier (épuisement mémoire, y compris en listant) | FIX | T1.1 étape 3 et validation ; T3.1 `TooLarge` et `ListSlots` |
| F2 | P2 | Erreurs d'entrée-sortie non spécifiées | FIX | T1.1 étape 6 ; T3.1 étapes 1-3 (`IoError`), validations |
| F3 | P2 | Aucune règle ne demande au jeu de valider les valeurs chargées | FIX | T4.1 « consumer contract » ; plan parent E16.c (contrôles et tests) |
| F4 | P3 | Règle de nom d'emplacement indéfinie, tests incomplets | FIX | T1.1 étapes 2 et 5, validation |
| F5 | P3 | Assainissement de `ProjectName` indéfini, dossier vide possible | FIX | T1.1 étapes 1-2, validation |
| F6 | P3 | Nom du temporaire et courses d'écriture | FIX | T1.1 étape 4, validation |
| F7 | P3 | Arithmétique des bornes du binaire, préfixes non couverts | FIX | T2.2 étapes 2-3, validation |
| F8 | P3 | Réglages du lecteur JSON non fixés, types non vérifiés | FIX | T2.1 étape 4 ; T2.2 étapes 1 et 4, validation |
| F9 | P3 | Intégrité et authenticité non écrites | FIX (texte) ; variante DEFER | Choix techniques ; T0.1 étape 2 ; T4.1 ; variante « refuser le JSON dans une build finale » → O5 |
| F10 | P3 | « Pas de vecteur CWE-502 » trop large | FIX | Choix techniques reformulés ; recherche en T2.2 et T4.2 |
| F11 | P3 | Frontière des exceptions et état partiel au chargement | FIX | T2.1 étape 5 ; T3.1 étapes 2-3, validation |
| F12 | P4 | Métadonnées affichées avec le formatage en ligne de MGUI | FIX (une phrase) | T4.1 ; rappelé au plan parent E16.e |
| F13 | P4 | Atomicité promise trop fort | FIX (texte) | Objectif ; T4.1 |
| F14 | P4 | Liens et jonctions dans le dossier de sauvegarde | REJECT (aucune action au-delà de F6) | Même compte utilisateur, aucune frontière de privilège franchie ; F6 couvre le temporaire |
| F15 | P4 | Longueur de chemin proche de 260 caractères | FIX | T1.1 étape 2 : emplacement ≤ 32, dossier ≤ 64 |

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | ~~§9.9 contredit ce service~~ — **tranché le 2026-09-27 : accepté**, §9.9 sera précisé en T0.1 (les assets restent à l'éditeur, la sauvegarde de partie du joueur est un service runtime). | Tout le programme |
| O2 | Nom du dossier : `%LOCALAPPDATA%\<ProjectName>\SaveGames` proposé, faute de champ société. | T1.1 |
| O3 | Une écriture sous AppData faite depuis l'app Claude est virtualisée et invisible ailleurs : les recettes manuelles se lancent hors de l'app, les tests écrivent sous un dossier temporaire. | T1.1, recette parente |
| O4 | ~~Modification préexistante de l'auteur~~ — **tranché le 2026-09-27 : accepté**, la branche du chantier porte `CasaEngine.Launcher/Program.cs` dans l'arbre de travail, jamais indexé. | T0.1 |
| O5 | Reporté (F9) : une option pour refuser le JSON au chargement dans une build finale, puisque le JSON n'a pas de somme de contrôle. Décision de l'auteur, plus tard. | Hors de ce plan |

## Hors périmètre

- API asynchrone, sauvegarde dans le nuage, chiffrement, compression, authentification des fichiers.
- Nettoyage des `.tmp` orphelins.
- Plusieurs utilisateurs locaux (l'index utilisateur d'Unreal), second backend de stockage.
- Participants à la Godot (groupe « persist ») : le contrat retenu est l'objet de sauvegarde (D2).
- Stockage des variables Yarn (`yarn_spinner_integration.md:164`), écran de sauvegarde générique,
  interface d'éditeur.
