# Plan agent IA — Points d'extension du runner Yarn

Plan d'exécution des points d'extension génériques du runner Yarn, demandés par le portage Alundra
(plan parent `docs/plan-e15-yarn.md`, tranche E15.a).
Les décisions D1 → D4 ci-dessous ont été arbitrées avec l'auteur le 2026-09-27 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Statut** : **approuvé par l'auteur le 2026-09-27.** Révision 2 : relecture de plan (REVISE, trois
> P2 corrigés : liaison du stockage, reprise après une commande, markup visible), puis relecture
> fraîche de clôture : **READY**. Branche `chantier/yarn-extension-points` créée le 2026-09-27 depuis
> `main` (`f8629e05`), avec l'accord de l'auteur (O1) : sa modification en cours
> (`CasaEngine.Launcher/Program.cs`) suit dans l'arbre de travail, jamais indexée.

## Objectif

Donner à `YarnDialogueRunner` les points d'extension qu'un jeu utilise avec Yarn Spinner, sans
qu'aucun code propre à un jeu n'entre dans le moteur :

- un **stockage de variables injectable** (`Yarn.IVariableStorage`), pour qu'un jeu adosse les
  variables Yarn à son propre état ;
- un **registre de commandes par nom** (`<<nom arg1 arg2>>`), avec un repli pour une commande inconnue
  et une reprise automatique du dialogue après une commande traitée ;
- l'**enregistrement de fonctions** (`{nom(args)}`), transmis à la bibliothèque de chaque dialogue ;
- l'**analyse du markup** (`[attribut/]`) par le `LineParser` de Yarn Spinner, le texte nettoyé et
  les attributs livrés au présentateur par un **ajout compatible** à `DialogueLine` ;
- la lecture d'une ligne d'un `DialogueAsset` par son identifiant, hors d'un dialogue.

C'est le modèle de l'intégration Unity de Yarn Spinner (`DialogueRunner.AddCommandHandler`, stockage
de variables remplaçable), sur le cœur C# que le moteur utilise déjà.

## État vérifié du dépôt (2026-09-27)

- `YarnDialogueRunner` (`CasaEngine/Framework/Dialogue/Yarn/YarnDialogueRunner.cs`) :
  - le constructeur ne prend qu'un `IDialoguePresenter` (`:13-18`) ; `Start(asset, startNode)` crée un
    `Yarn.Dialogue` par programme (`:22-49`) ;
  - le stockage de variables est un `Yarn.MemoryVariableStore` codé en dur (`:74-77`) ;
  - `OnOptions` et `OnCommand` sont vides (`:94-100`) : **une commande Yarn bloque aujourd'hui le
    dialogue**, qui attend `Continue()` après chaque commande ;
  - le texte d'une ligne vient de `DialogueAsset.LineTexts`, les substitutions sont remplacées par
    `string.Replace` (`:109-123`) ; aucun markup n'est analysé ;
  - aucun accès à `Dialogue.Library`.
- `DialogueLine` (`CasaEngine/Framework/Dialogue/Runtime/DialogueLine.cs:3-24`) ne porte que `Text`
  et `Speaker` ; `IDialoguePresenter.ShowLine(DialogueLine)` (`IDialoguePresenter.cs:27`) ;
  `DialogueScreen` affiche `Speaker` en gras quand il est renseigné (`DialogueScreen.cs:366`).
- Tests existants : `CasaEngine.Tests/Dialogue/YarnDialogueRunnerTests.cs` (deux tests, fixture
  `Fixtures/greeting.yarn`), `YarnDialogueCompilerTests.cs`, `DialogueServiceTests.cs`,
  `DialogueServiceChoiceTests.cs`.
- Plan Yarn existant `ai-agent/tasks/yarn-spinner-integration-agent-plan.md` : tâches 12 à 18 ⏳ ;
  ce plan réalise les tâches **14** (variables) et **15** (commandes), pas les options (13) ni
  l'import éditeur (17).
- Faits du cœur Yarn Spinner 3.2.1, relevés par recherche et par un programme de test lancé contre
  la bibliothèque :
  - `Dialogue(IVariableStorage)` affecte au stockage `Program` et `SmartVariableEvaluator` ;
    `IVariableStorage` hérite d'`IVariableAccess` (`Program`, `SmartVariableEvaluator`,
    `TryGetValue<T>`, `GetVariableKind`) ;
  - un stockage maison doit retomber sur les valeurs initiales du programme, déléguer les variables
    calculées, et garder les variables internes `$Yarn.Internal.Visiting.*` et
    `$Yarn.Internal.Once.*` (sinon `visited()` et `once` cassent) ;
  - `Dialogue.CommandHandler` reçoit un `Command` dont `.Text` est le texte brut, expressions déjà
    substituées ; sans gestionnaire, la commande est ignorée ; après chaque commande le dialogue attend
    `Continue()` ;
  - `Library.RegisterFunction(string, Delegate)` lève si le nom existe déjà ; une fonction non
    enregistrée ne lève qu'à l'exécution ;
  - le markup est à la charge de l'hôte : `LineParser.ExpandSubstitutions`, `ParseString(texte,
    locale)`, marqueurs `select`/`plural`/`ordinal` via `RegisterMarkerProcessor` ;
  - le pluriel français intégré de la 3.2.1 classe toute quantité ≥ 2 en `many` (défaut connu,
    documenté, non corrigé ici).
- Dernière ADR : ADR-0041 (`docs/decisions/README.md`) ; le plan non commité
  `save-game-service-tasks.md` prévoit aussi une ADR : chacune prend le numéro libre au moment de
  son écriture.
- **Modification préexistante de l'auteur** : `CasaEngine.Launcher/Program.cs`, jamais indexée.

Sources :
- <https://docs.yarnspinner.dev/api/csharp/yarn/yarn.dialogue>
- <https://docs.yarnspinner.dev/api/csharp/yarn/yarn.ivariablestorage>
- <https://docs.yarnspinner.dev/api/csharp/yarn/yarn.library/yarn.library.registerfunction-3>
- <https://docs.yarnspinner.dev/yarn-spinner-for-unity/creating-commands-functions>
- <https://docs.yarnspinner.dev/write-yarn-scripts/advanced-scripting/markup>

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Le moteur ne reçoit que des points d'extension **génériques** ; aucun code propre à Alundra. |
| D2 | Stockage de variables injectable, registre de commandes avec repli, accès aux fonctions : le modèle de Yarn Spinner. |
| D3 | Les phrases partagées d'un jeu vivent dans des fichiers Yarn partagés, désignés par des identifiants stables (côté jeu, rien à faire ici hors de T1.5). |
| D4 | Premier consommateur : le portage Alundra (E15). |

Choix techniques proposés par ce plan (approuvés avec lui) :

- **Ajouts seulement** (§9.8) : le constructeur et les méthodes existants gardent leur comportement ;
  `DialogueLine` gagne une surcharge de constructeur et une propriété `Attributes`, vide par défaut.
- **Types de Yarn dans l'API du runner** : `YarnDialogueRunner` est déjà une classe propre à Yarn ;
  exposer `Yarn.IVariableStorage` y est naturel, comme dans l'intégration Unity.
- **Liaison du stockage seulement quand le `Start` est acquis** : le nœud demandé est vérifié sur le
  `Yarn.Program` analysé **avant** de construire le `Yarn.Dialogue` (dont le constructeur lie le
  stockage au programme) ; un `Start` refusé ne touche ni au dialogue en cours ni au stockage partagé.
- **Une commande inconnue ne bloque plus** : avertissement journalisé une fois par nom, événement
  `UnhandledCommand`, puis reprise. Aujourd'hui elle bloque le dialogue : c'est un changement de
  comportement assumé, qui corrige un blocage.
- **Reprise après une commande sous condition** : le runner ne reprend que si le `Yarn.Dialogue` qui a
  émis la commande est toujours le dialogue courant et actif. Un gestionnaire peut donc appeler
  `Stop()` ou `Start(autre)` sans que la reprise ne lève ni ne saute la première ligne de l'autre
  dialogue. **Une exception d'un gestionnaire remonte, et le dialogue est d'abord arrêté**
  (présentateur fermé) : jamais d'état à moitié avancé.
- **Noms de commandes** : comparaison ordinale, sensible à la casse ; un texte de commande vide est
  traité comme une commande inconnue.
- **Commandes synchrones seulement** ; une commande asynchrone (qui attend avant de reprendre) est
  hors périmètre.
- **L'analyse du markup est un changement visible**, assumé : une ligne `Nom: texte` affiche
  désormais `texte` avec `Nom` en `Speaker`, et les crochets `[...]` d'une ligne sont lus comme du
  markup Yarn, donc retirés du texte (aujourd'hui, `DialogueScreen` les passerait au formatage de
  MGUI). Un texte qui veut des crochets littéraux les échappe selon Yarn (`\[`, `\]`).
- **Pas de démo** : la fonctionnalité n'a pas d'écran propre ; la preuve est la suite de tests plus
  l'usage réel par Alundra.

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/yarn-extension-points`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché (`dotnet build CasaEngine.MonoGame.sln`) ; **tests** : `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj` puis `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build --blame-hang-timeout 60s` (le projet de tests n'est pas dans la solution). Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : le runner n'est pas un chemin chaud (une ligne par appui), mais aucune allocation
  inutile par ligne ; le runtime ne dépend pas de l'éditeur.
- **Budget et retour** : au plus deux tentatives par tâche au même niveau d'exécutant, puis reprise
  par la session principale ; retour arrière = abandon de la branche du chantier, rien n'est fusionné
  sans l'auteur.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` : 0 erreur.
- `CasaEngine.Tests` : aucun échec.
- Les deux tests existants de `YarnDialogueRunnerTests` passent sans modification (compatibilité).
- Clôture : un **verifier frais** sur tout le chantier, contre l'acceptation de chaque tâche.

---

## Phase 0 — Cadre

### ✅ T0.1 — Branche, ADR, index

- Objectif : ouvrir le chantier et consigner D1 à D4 et les choix techniques.
- Fichiers : `docs/decisions/<numéro libre>-yarn-runner-extension-points.md`,
  `docs/decisions/README.md`, `ai-agent/README.md`, ce plan.
- Étapes :
  1. Créer la branche `chantier/yarn-extension-points` depuis `main` (la modification de l'auteur
     suit dans l'arbre de travail, jamais indexée) et y commiter ce plan.
  2. ADR au numéro libre, skill `adr`, sources citées ; elle consigne les règles de liaison du
     stockage, de reprise après une commande, d'exception d'un gestionnaire et le changement visible
     du markup.
  3. Ligne du tableau d'`ai-agent/README.md`.
- Validation : relecture, liens valides.
- Commit : `docs(dialogue): record the Yarn runner extension points decision`
- Validation : ADR-0042 créée (`docs/decisions/0042-yarn-runner-extension-points.md`), indexée dans
  `docs/decisions/README.md` ; ligne ajoutée à `ai-agent/README.md`. Pas de build (documentation seule).

---

## Phase 1 — Points d'extension

### ✅ T1.1 — Stockage de variables injectable

- Objectif : `YarnDialogueRunner.VariableStorage` (`Yarn.IVariableStorage?`), utilisé par chaque
  `Start` ; à `null`, un `MemoryVariableStore` neuf comme aujourd'hui.
- Fichiers : `YarnDialogueRunner.cs`, `CasaEngine.Tests/Dialogue/YarnDialogueRunnerTests.cs`, une
  fixture `.yarn` de test.
- Étapes :
  1. Réordonner `Start` (aujourd'hui `YarnDialogueRunner.cs:35-47` construit le dialogue avant de
     vérifier le nœud) : analyser le programme, vérifier le nœud sur ce programme, et seulement alors
     arrêter le dialogue en cours, construire le nouveau `Yarn.Dialogue` (liaison du stockage), puis
     démarrer.
  2. Exposer `VariableStorage` ; ne jamais lier le stockage sur un chemin d'échec.
- Validation : tests —
  - un stockage injecté reçoit la valeur d'un `<<set $x to true>>` et fournit celle lue par
    `<<if $x>>` ;
  - le même stockage garde sa valeur d'un `Start` à l'autre ;
  - **un dialogue en cours sur l'asset A, puis `Start(B, "Absent")` rend `false` : A continue et lit
    toujours, par le stockage partagé, les valeurs initiales et les variables calculées d'A** ;
  - sans stockage injecté, le comportement actuel est inchangé ;
  - un stockage qui dérive de `MemoryVariableStore` garde `visited()` fonctionnel.
- Commit : `feat(dialogue): let games inject the Yarn variable storage`
- Validation : 5 tests ajoutés dans `YarnDialogueRunnerTests` (set/if, persistance entre deux `Start`,
  `Start` refusé sur nœud absent laissant A continuer via le stockage partagé, comportement inchangé
  sans stockage injecté, `visited()` fonctionnel avec un stockage injecté). `dotnet build
  CasaEngine.MonoGame.sln` 0 erreur ; `CasaEngine.Tests` 1962/1962 (+5, aucun échec), les deux tests
  existants inchangés.

### ✅ T1.2 — Registre de commandes

- Objectif : `AddCommandHandler(string name, Action<IReadOnlyList<string>> handler)`,
  `RemoveCommandHandler(string name)`, événement `UnhandledCommand`.
- Fichiers : `YarnDialogueRunner.cs`, éventuellement `YarnCommandLine.cs` (découpage du texte),
  tests.
- Étapes :
  1. Découper `Command.Text` en nom et arguments, en respectant les arguments entre guillemets ; noms
     comparés en ordinal, sensibles à la casse ; texte vide = commande inconnue.
  2. Nom connu : appeler le gestionnaire. Nom inconnu : avertissement une fois par nom,
     `UnhandledCommand`.
  3. Reprise : `Continue()` seulement si le `Yarn.Dialogue` qui a émis la commande est toujours le
     dialogue courant et actif.
  4. Une exception du gestionnaire du jeu n'est pas avalée : le dialogue est arrêté (présentateur
     fermé), puis l'exception remonte (§9.10).
- Validation : tests —
  - une commande enregistrée reçoit ses arguments (dont un argument entre guillemets et une
    expression substituée `{$n}`) ; le dialogue reprend seul et affiche la ligne suivante ;
  - une commande inconnue, et une commande au texte vide, lèvent l'événement et ne bloquent pas ;
  - un gestionnaire qui appelle `Stop()` : aucune exception, présentateur fermé ;
  - un gestionnaire qui appelle `Start(autre)` : la première ligne de l'autre dialogue est affichée,
    pas sautée ;
  - un gestionnaire qui lève : l'exception remonte, le runner est arrêté et le présentateur fermé ;
  - nom déjà enregistré → `ArgumentException`.
- Commit : `feat(dialogue): dispatch Yarn commands to game handlers`
- Validation : 6 tests ajoutés (commande enregistrée avec argument entre guillemets et substitution
  `{$n}`, reprise automatique ; commande inconnue et événement ; `Stop()` dans un gestionnaire ;
  `Start(autre)` dans un gestionnaire, première ligne affichée ; exception d'un gestionnaire qui
  remonte et arrête le runner ; nom déjà enregistré). Point technique découvert en cours de tâche,
  non couvert par les faits connus du plan : `Yarn.Dialogue.Continue()` est protégé contre la
  réentrance (`isContinuing`) et ne fait rien s'il est appelé depuis l'intérieur du gestionnaire de
  commande qui l'a déclenché ; la reprise synchrone après une commande traitée utilise donc
  `Dialogue.SignalContentComplete()` (API publique documentée pour exactement ce cas), pas
  `Continue()`. `dotnet build CasaEngine.MonoGame.sln` 0 erreur ; `CasaEngine.Tests` 1968/1968
  (+6, aucun échec), les deux tests existants inchangés.

### ✅ T1.3 — Fonctions (révisée le 2026-09-27 après O4)

- Objectif : deux ajouts génériques.
  1. **Compilation** : `YarnDialogueCompiler` gagne des surcharges de `CompileString` et
     `CompileFile` qui acceptent en option une `Yarn.Library` de **déclarations** de fonctions (nom et
     types, portés par la signature des délégués), passée à la `CompilationJob`. Sans elle, le
     comportement actuel est inchangé. C'est le jeu (ou son outil d'export) qui fournit ses
     déclarations ; le moteur n'en contient aucune.
  2. **Exécution** : `YarnDialogueRunner.RegisterFunction(string name, Delegate implementation)`,
     conservé par le runner et appliqué à la bibliothèque de chaque `Dialogue` créé.
- Fichiers : `CasaEngine.Compiler/Dialogue/YarnDialogueCompiler.cs`, `YarnDialogueRunner.cs`, tests
  (`YarnDialogueCompilerTests.cs`, `YarnDialogueRunnerTests.cs`), ADR complémentaire au numéro libre
  (la décision de compiler avec des déclarations fournies par le jeu), index des ADR.
- Validation : tests —
  - une fonction **déclarée au compilateur** et appelée dans `{…}` d'une ligne compile, et son
    implémentation enregistrée sur le runner donne le bon texte ;
  - la même fonction appelée dans une condition `<<if …>>` ;
  - sans déclaration, l'appel dans `{…}` échoue toujours à la compilation, avec le diagnostic de
    Yarn (comportement documenté) ;
  - les surcharges sans bibliothèque compilent comme aujourd'hui (tests existants inchangés) ;
  - nom déjà enregistré sur le runner → `ArgumentException`.
- Commit : `feat(dialogue): declare and register game functions for Yarn`
- **Historique** : bloquée le 2026-09-27 (le fait « une fonction non déclarée au compilateur compile »
  s'est révélé faux dans le texte d'une ligne, O4) ; code annulé, aucun commit de code ; débloquée le
  même jour par la révision ci-dessus, décidée par l'auteur.
- **Validation** : `YarnDialogueCompiler.CompileString`/`CompileFile` gagnent une surcharge à trois
  arguments (`Yarn.Library` de déclarations, optionnelle par la surcharge à deux arguments inchangée) ;
  `YarnDialogueRunner.RegisterFunction(string, Delegate)` garde les fonctions et les applique à la
  `Library` de chaque `Dialogue` créé. ADR-0043 créée (`docs/decisions/0043-yarn-function-declarations-at-compile-time.md`),
  indexée dans `docs/decisions/README.md`. 5 tests ajoutés (3 dans `YarnDialogueCompilerTests` : fonction
  déclarée compilant dans le texte d'une ligne, dans une condition `<<if>>`, et fonction non déclarée
  toujours refusée dans le texte d'une ligne ; 2 dans `YarnDialogueRunnerTests` : implémentation
  enregistrée donnant le bon texte de ligne, nom déjà enregistré → `ArgumentException`). `dotnet build
  CasaEngine.MonoGame.sln` 0 erreur ; `CasaEngine.Tests` 1973/1973 (+5, aucun échec), les deux tests
  existants de `YarnDialogueRunnerTests` et ceux de `YarnDialogueCompilerTests` inchangés.

### ✅ T1.4 — Markup et attributs

- Objectif : le runner analyse chaque ligne (`LineParser.ExpandSubstitutions` puis `ParseString`,
  marqueurs `select`/`plural`/`ordinal` enregistrés) et livre au présentateur le texte nettoyé plus
  ses attributs.
- Fichiers : `YarnDialogueRunner.cs`, `DialogueLine.cs` (surcharge du constructeur, propriété
  `Attributes`), nouveau `DialogueMarkupAttribute.cs` (nom, position, longueur, propriétés), tests.
- Étapes :
  1. Un `LineParser` par runner, créé une fois et réutilisé ; marqueurs `select`, `plural` et
     `ordinal` enregistrés par `LineParser.RegisterMarkerProcessor` avec le `BuiltInMarkupReplacer`
     de Yarn Spinner. **Si ces API ne sont pas publiques dans la 3.2.1, la tâche passe en ⚠️** et la
     question remonte, sans contournement.
  2. `YarnDialogueRunner.LocaleCode` (par défaut `"en"`) passé à `ParseString`.
  3. L'attribut `character` de Yarn (`Nom: texte`) renseigne `Speaker`, et sa plage est retirée du
     texte par `MarkupParseResult.DeleteRange` ; le reste va dans `Attributes`.
  4. Une ligne au markup invalide : avertissement, texte brut livré, pas d'exception.
- Validation : tests —
  - un marqueur autofermant `[br/]` apparaît dans `Attributes` à la bonne position et disparaît du
    texte ; `[b]…[/b]` donne un attribut de longueur correcte ;
  - une substitution `{0}` est remplacée ;
  - `Nom: texte` donne `Text == "texte"` et `Speaker == "Nom"` (le nom n'apparaît pas deux fois) ;
  - un cas `[select]` rend la bonne variante ;
  - une ligne au markup invalide est livrée brute, sans exception ;
  - les deux tests existants passent sans modification ; `DialogueScreen` affiche toujours `Text`.
- Commit : `feat(dialogue): parse Yarn markup into line attributes`
- **Validation** : `DialogueMarkupAttribute` créé (nom, position, longueur, propriétés) ; `DialogueLine`
  gagne une surcharge de constructeur à trois arguments et `Attributes` (vide par défaut, bug d'ordre
  d'initialisation statique corrigé : `NoAttributes` doit être déclaré avant `Empty`). `YarnDialogueRunner`
  crée un `LineParser` unique (marqueurs `select`/`plural`/`ordinal` via `RegisterMarkerProcessor` et
  `BuiltInMarkupReplacer`, tous publics en 3.2.1 — vérifiés par un programme de test), expose `LocaleCode`
  (par défaut `"en"`), et `ResolveLineText` utilise désormais `LineParser.ExpandSubstitutions` (même
  résultat que la boucle `string.Replace` précédente). L'attribut `character` de Yarn donne `Speaker` et
  est retiré du texte par `MarkupParseResult.DeleteRange` (qui décale aussi la position des attributs
  restants, vérifié). Une exception pendant le parsing (constatée par test : un Unicode invalide lève
  `ArgumentException`, pas `MarkupParseException`) est rattrapée au sens large : avertissement, texte
  brut, pas d'attributs, pas d'exception. O2 vérifié : le texte livré au présentateur ne contient plus les
  crochets `[...]` du markup Yarn (ils sont consommés par `ParseString`), donc plus de collision avec le
  formatage `[b]...[/b]` que `DialogueScreen.cs:366` applique lui-même au nom du locuteur ;
  `DialogueScreen` inchangé. 6 tests ajoutés dans `YarnDialogueRunnerTests` (marqueur autofermant en
  attribut et retiré du texte, marqueur `[b]...[/b]` avec la bonne longueur, substitution `{$n}`
  remplacée, préfixe `Nom:` donnant `Speaker`/`Text`, `[select value=1 .../]` rendant la bonne variante,
  markup invalide livré brut sans lever). `dotnet build CasaEngine.MonoGame.sln` 0 erreur ;
  `CasaEngine.Tests` 1979/1979 (+6, aucun échec), les deux tests existants de `YarnDialogueRunnerTests`
  inchangés.

### ✅ T1.5 — Lire une ligne hors d'un dialogue

- Objectif : `DialogueAsset.TryGetLineText(string lineId, out string text)` et un analyseur statique
  qui rend le texte nettoyé et les attributs d'un texte brut, pour les textes qu'un jeu affiche hors
  d'une boîte de dialogue (menus, inventaire).
- Fichiers : `DialogueAsset.cs`, le nouvel analyseur, tests.
- Validation : tests — identifiant présent et absent ; markup et substitutions traités comme en T1.4.
- Commit : `feat(dialogue): read single Yarn lines outside a dialogue`
- **Validation** : `DialogueAsset.TryGetLineText(string, out string)` ajouté (simple lecture de
  `LineTexts`). `YarnLineTextParser` (statique, `CasaEngine.Framework.Dialogue.Yarn`) reprend
  exactement le pipeline de `YarnDialogueRunner.OnLine` (T1.4) : `ExpandSubstitutions`, `Parse` avec un
  `LineParser` partagé (`select`/`plural`/`ordinal`), attribut `character` retiré vers `Speaker`, repli
  sur le texte brut sans lever en cas de markup invalide. `YarnDialogueRunner` a été refactoré pour
  déléguer à `YarnLineTextParser` (`OnLine` et `ResolveLineText`), au lieu de dupliquer la logique :
  aucun changement de comportement, les tests de T1.4 passent inchangés. 8 tests ajoutés
  (`DialogueAssetTests` : identifiant présent/absent ; `YarnLineTextParserTests` : texte simple
  inchangé, préfixe `Nom:` donnant `Speaker`/`Text`, marqueur `[b]...[/b]` avec la bonne longueur,
  markup invalide livré brut sans lever, substitution `{0}` remplacée, substitution puis analyse
  bout-en-bout). `dotnet build CasaEngine.MonoGame.sln` 0 erreur ; `CasaEngine.Tests` 1987/1987 (+8,
  aucun échec), les deux tests existants de `YarnDialogueRunnerTests` et ceux de `YarnDialogueCompilerTests`
  inchangés.

---

## Phase 2 — Documentation et clôture

### ✅ T2.1 — Documentation et plan Yarn

- Objectif : documenter les points d'extension ; tâches 14 et 15 de
  `yarn-spinner-integration-agent-plan.md` marquées faites, avec renvoi à ce plan.
- Fichiers : `docs/engine/yarn_spinner_integration.md` (section des points d'extension, exemple
  d'usage), `ai-agent/tasks/yarn-spinner-integration-agent-plan.md`.
- Validation : relecture ; liens valides.
- Commit : `docs(dialogue): document the Yarn runner extension points`
- **Validation** : section « Points d'extension du runner Yarn » ajoutée à
  `docs/engine/yarn_spinner_integration.md` (stockage de variables, commandes, fonctions, markup,
  lecture hors dialogue, exemples d'usage, renvoi ADR-0042/ADR-0043), note en tête de document.
  Tâches 14 et 15 de `ai-agent/tasks/yarn-spinner-integration-agent-plan.md` passées ✅ avec renvoi à
  ce plan. Relecture faite, liens vérifiés (ADR-0042, ADR-0043, plan). Documentation seule : pas de
  test dédié ; `dotnet build CasaEngine.MonoGame.sln` 0 erreur (vérification que rien n'est cassé).

### 🚧 T2.2 — Vérification de clôture

- Objectif : verifier frais sur le chantier entier ; tableau d'`ai-agent/README.md` à jour.
- Validation : verdict **CONFIRMED** ; suites vertes.
- Commit : `docs(dialogue): close the Yarn extension points plan`
- **Première vérification (2026-09-27) : REFUTED**, un seul constat bloquant, F1 (P2) : le comportement
  est juste (28 sondes sur 28), mais trois lignes de validation n'avaient pas de test — la commande au
  texte vide (T1.2), les valeurs initiales et calculées après un `Start` refusé (T1.1), une fonction
  qui décide d'un `<<if>>` au runtime (T1.3). **Corrigé** par trois tests
  (`Command_EmptyText_RaisesEventAndDoesNotBlock`,
  `Start_RefusedOnUnknownNode_CurrentDialogueStillReadsItsInitialAndComputedValues`,
  `RegisterFunction_DeclaredFunction_DecidesIfConditionAtRuntime`, deux cas) ; `CasaEngine.Tests`
  1991/1991. Deux avis P4 reportés avec l'accord de l'auteur : O5, O6. Revérification ciblée à faire.

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | ~~Modification préexistante de l'auteur~~ — **tranché le 2026-09-27 : accepté**, la branche du chantier porte `CasaEngine.Launcher/Program.cs` dans l'arbre de travail, jamais indexé. | T0.1 |
| O2 | `DialogueScreen` interprète le formatage en ligne de MGUI (`[b]…[/b]`) : un texte de jeu qui garderait des crochets littéraux après l'analyse Yarn serait réinterprété à l'affichage. À vérifier en T1.4, sans changer `DialogueScreen` hors de ce besoin. | T1.4 |
| O3 | Reporté (P3) : un nom de fonction qui entre en conflit avec une fonction intégrée de Yarn n'échoue qu'au `Start` ; un contrôle à l'enregistrement est possible plus tard. | T1.3 |
| O4 | ~~Fonctions de jeu non typées à la compilation~~ — **tranché par l'auteur le 2026-09-27** : le compilateur accepte une bibliothèque de déclarations fournie par le jeu (T1.3 révisée). Constat d'origine : `YarnDialogueCompiler` compile avec une `Library` vide, et un appel de fonction dans le texte d'une ligne échoue (« Can't determine the type of the expression ») ; dans une condition, il compile. | T1.3 |
| O5 | Reporté (avis A1, P4) : aucun test ne vérifie que l'avertissement d'une commande inconnue n'est journalisé qu'une fois par nom (le code le fait par un `HashSet`, `YarnDialogueRunner.cs:194`). | T1.2 |
| O6 | Reporté (avis A2, P4) : aucun test dédié pour « `DialogueScreen` affiche toujours `Text` » ; `DialogueScreen` n'est pas modifié par ce chantier. | T1.4 |

## Hors périmètre

- Les options Yarn (`->`) et leur aiguillage (tâche 13 du plan Yarn).
- Les commandes asynchrones.
- L'import `.yarn` dans l'éditeur (tâche 17), la localisation multi-langue, la correction du pluriel
  français de Yarn Spinner 3.2.1.
- Les shadow lines, et leur défaut dans `YarnDialogueCompiler.cs:55` (texte vide pour une shadow
  line) : noté, non traité.
- L'action de cinématique « démarrer un dialogue » (tâche 16) : étape E17 du portage.
