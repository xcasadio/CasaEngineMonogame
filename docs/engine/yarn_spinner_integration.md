# Intégration de Yarn Spinner dans CasaEngine

> **Note (E12.a)** : la route directe (bytecode Alundra → `DialogueService`, sans Yarn — Yarn
> reste prévu pour le chantier E15) a ajouté au mécanisme moteur un état de choix générique et un
> chemin d'enregistrement de police bitmap, en plus du pipeline ligne-simple déjà en place.
> L'API réelle (`DialogueService`, `IDialoguePresenter`, `DialogueScreen`) est documentée à jour
> dans [dialogue-choices-and-bitmap-fonts.md](dialogue-choices-and-bitmap-fonts.md) ; les
> signatures `DialogueService`/`DialogueStartRequest`/`ShowText` de ce document sont la feuille de
> route Yarn d'origine, pas l'état actuel du code.

## Objectif

L'objectif est d'intégrer **Yarn Spinner** dans CasaEngine pour obtenir un système de dialogue moderne, maintenable et extensible.

Yarn Spinner doit être utilisé comme **moteur narratif** :

- écriture des dialogues dans des fichiers `.yarn` ;
- compilation des scripts Yarn en données exécutables ;
- exécution des nœuds de dialogue ;
- gestion des lignes, choix, commandes et variables ;
- séparation claire entre le runtime narratif, l'UI, le gameplay et les outils éditeur.

CasaEngine ne doit pas réimplémenter un langage de dialogue complet. Le moteur doit plutôt fournir une **couche d'adaptation CasaEngine** autour de Yarn Spinner.

---

## Prérequis

Avant d'intégrer Yarn Spinner, il faut vérifier et stabiliser quelques points dans CasaEngine.

### Prérequis techniques

- Le projet cible doit pouvoir référencer les packages NuGet :
  - `YarnSpinner` pour le runtime ;
  - `YarnSpinner.Compiler` pour compiler les fichiers `.yarn` côté éditeur ou pipeline d'asset.
- CasaEngine étant en C#/.NET avec MonoGame, l'intégration doit être faite sans dépendre de Unity.
- La compilation des scripts Yarn doit être intégrée dans le pipeline d'assets CasaEngine, pas directement dans la boucle de jeu.
- Les fichiers `.yarn` doivent être considérés comme des sources éditables.
- Les données compilées doivent être considérées comme des assets runtime.
- Le runtime de dialogue doit être testable sans UI.
- L'UI de dialogue doit être une présentation, pas le moteur narratif.
- Les commandes Yarn doivent être dispatchées vers des services CasaEngine réels, ou explicitement marquées comme dépendances à créer.

### Prérequis CasaEngine

CasaEngine possède déjà plusieurs briques utiles :

- `Assets` : base pour ajouter un loader/importer de dialogues Yarn ;
- `UI` : base pour afficher une boîte de dialogue ;
- `ScreenStack` / écrans UI : base pour afficher une UI modale ;
- `Input` : base pour détecter le bouton d'action ;
- `Cutscenes` : base pour synchroniser dialogue et séquences scriptées ;
- `Gameplay` / `Scene` / `World` : base pour déclencher des dialogues depuis une entité, un trigger ou une interaction.

Ce qui manque pour Yarn Spinner :

- un module `Dialogue` dédié ;
- un asset `YarnDialogueAsset` ou `DialogueAsset` ;
- un service `DialogueService` ;
- un runner CasaEngine autour du runtime Yarn Spinner ;
- une UI de dialogue minimale ;
- un stockage des variables Yarn compatible avec la sauvegarde ;
- un dispatcher de commandes Yarn vers CasaEngine ;
- une validation d'asset côté éditeur.

---

## Architecture cible

```text
CasaEngine.Framework.Dialogue
 ├─ Yarn
 │   ├─ YarnDialogueAsset
 │   ├─ YarnDialogueAssetLoader
 │   ├─ YarnDialogueImporter
 │   ├─ YarnDialogueCompiler
 │   ├─ YarnDialogueRunner
 │   ├─ YarnDialoguePresenter
 │   ├─ YarnLineProvider
 │   ├─ YarnVariableStore
 │   └─ YarnCommandDispatcher
 │
 ├─ Runtime
 │   ├─ DialogueService
 │   ├─ DialogueContext
 │   ├─ DialogueStartRequest
 │   ├─ DialogueRuntimeState
 │   └─ DialogueEvents
 │
 ├─ UI
 │   ├─ DialogueScreen
 │   ├─ DialogueBoxView
 │   ├─ DialogueChoiceListView
 │   └─ DialogueViewModel
 │
 └─ Validation
     ├─ DialogueValidationResult
     ├─ DialogueValidationMessage
     └─ YarnDialogueValidator
```

---

## Principe d'intégration

Yarn Spinner doit rester responsable de la logique narrative :

```text
.yarn source
   ↓
Yarn compiler
   ↓
Yarn compiled program / dialogue asset
   ↓
YarnDialogueRunner
   ↓
DialoguePresenter CasaEngine
   ↓
UI CasaEngine
```

CasaEngine doit rester responsable de :

```text
- chargement des assets ;
- affichage UI ;
- input joueur ;
- pause / blocage gameplay ;
- interactions avec les entités ;
- commandes gameplay ;
- sauvegarde ;
- localisation runtime ;
- outils éditeur.
```

---

## Découpage recommandé

### Ne pas faire

```text
❌ Mettre toute la logique dans DialogueScreen
❌ Parser Yarn manuellement
❌ Lancer la compilation Yarn à chaque frame
❌ Mélanger dialogue, cutscene, UI et gameplay dans une seule classe
❌ Faire dépendre YarnDialogueRunner directement de MonoGame SpriteBatch
```

### Faire

```text
✅ Compiler les fichiers Yarn via le pipeline d'assets
✅ Créer un service runtime indépendant de l'UI
✅ Connecter le runner à une présentation UI remplaçable
✅ Utiliser ScreenStack ou un équivalent pour afficher la boîte de dialogue
✅ Ajouter les commandes Yarn progressivement
✅ Prévoir la sauvegarde des variables dès la V2
```

---

# Étapes d'intégration

---

## Étape 0 — Préparation du projet

### But

Préparer CasaEngine à recevoir Yarn Spinner sans encore afficher de dialogue.

### Tâches

- Ajouter les références NuGet :
  - `YarnSpinner`
  - `YarnSpinner.Compiler`
- Vérifier où les versions de packages sont centralisées :
  - `Directory.Packages.props`
  - ou `.csproj` concerné.
- Identifier le projet qui doit contenir le runtime :
  - probablement `CasaEngine.Framework`.
- Identifier le projet qui doit contenir les outils de compilation/édition :
  - probablement côté editor ou asset pipeline.
- Créer le dossier :

```text
CasaEngine/Framework/Dialogue/Yarn
```

- Ajouter un document interne :

```text
docs/engine/yarn_spinner_integration.md
```

### Livrables

```text
CasaEngine.Framework.Dialogue
Directory.Packages.props mis à jour
Documentation d'intégration créée
```

### Critère de validation

Le moteur compile avec les références Yarn Spinner ajoutées, sans changement de comportement runtime.

---

## Étape 1 — Démo visuelle minimale : ouvrir et fermer une boîte de dialogue

### But

Obtenir la première preuve visuelle, volontairement très simple :

```text
Le joueur appuie sur Action près d'un PNJ ou via une touche de test
→ une boîte de dialogue s'ouvre
→ elle affiche un texte simple
→ le joueur appuie sur Action
→ la boîte de dialogue se ferme
```

Cette étape ne doit pas encore gérer :

```text
❌ fichier .yarn réel
❌ choix
❌ variables
❌ commandes
❌ localisation
❌ typewriter
❌ portrait
❌ historique
```

### Pourquoi commencer par ça ?

Cette étape permet de valider les points critiques côté CasaEngine avant d'ajouter Yarn Spinner :

- affichage d'un écran UI modal ;
- lecture du bouton Action ;
- blocage ou filtrage des inputs gameplay ;
- ouverture/fermeture propre d'une boîte de dialogue ;
- intégration avec le monde ou une scène de démo.

### Structure minimale

```text
CasaEngine.Framework.Dialogue.Runtime
 ├─ DialogueService.cs
 ├─ DialogueStartRequest.cs
 └─ DialogueRuntimeState.cs

CasaEngine.Framework.Dialogue.UI
 ├─ DialogueScreen.cs
 └─ DialogueViewModel.cs
```

### Exemple de données temporaires

```csharp
public sealed class DialogueStartRequest
{
    public string Text { get; init; } = string.Empty;
    public string? SpeakerName { get; init; }
}
```

### Exemple de service minimal

```csharp
public sealed class DialogueService
{
    public bool IsDialogueOpen { get; private set; }

    public event Action<DialogueStartRequest>? DialogueStarted;
    public event Action? DialogueClosed;

    public void ShowText(string text, string? speakerName = null)
    {
        if (IsDialogueOpen)
        {
            return;
        }

        IsDialogueOpen = true;
        DialogueStarted?.Invoke(new DialogueStartRequest
        {
            Text = text,
            SpeakerName = speakerName
        });
    }

    public void Close()
    {
        if (!IsDialogueOpen)
        {
            return;
        }

        IsDialogueOpen = false;
        DialogueClosed?.Invoke();
    }
}
```

### Exemple de comportement attendu

```text
Input Action
 ├─ Si aucune boîte de dialogue n'est ouverte :
 │   └─ ouvrir DialogueScreen avec "Bonjour !"
 │
 └─ Si une boîte de dialogue est ouverte :
     └─ fermer DialogueScreen
```

### Points CasaEngine à connecter

- Input :
  - utiliser l'action existante correspondant au bouton d'interaction/action ;
  - si elle n'existe pas, créer une action logique `Action` ou `Interact`.
- UI :
  - utiliser `ScreenStack` ou le système UI existant ;
  - afficher un écran modal `DialogueScreen`.
- Gameplay :
  - pendant le dialogue, empêcher le joueur de déclencher plusieurs interactions ;
  - décider si le gameplay continue ou se met en pause.
- Démo :
  - ajouter une scène ou un état de test ;
  - déclencher le dialogue depuis une touche de debug ou une interaction PNJ.

### Critère de validation

La V1 visuelle est validée si :

```text
✅ une boîte de dialogue apparaît à l'écran ;
✅ elle contient un texte simple ;
✅ le bouton Action ferme la boîte ;
✅ aucun crash si on appuie plusieurs fois ;
✅ le gameplay ne reçoit pas l'action en double pendant que la boîte est ouverte.
```

---

## Étape 2 — Ajout du concept de DialoguePresenter

### But

Séparer le service runtime de la présentation UI.

Même si l'étape 1 n'utilise pas encore Yarn, il faut déjà préparer l'architecture qui accueillera Yarn Spinner.

### Interface proposée

```csharp
public interface IDialoguePresenter
{
    void ShowLine(DialogueLinePresentation line);
    void ShowChoices(IReadOnlyList<DialogueChoicePresentation> choices);
    void Hide();
}
```

### Données de présentation

```csharp
public sealed class DialogueLinePresentation
{
    public string Text { get; init; } = string.Empty;
    public string? SpeakerName { get; init; }
}

public sealed class DialogueChoicePresentation
{
    public int Index { get; init; }
    public string Text { get; init; } = string.Empty;
    public bool IsAvailable { get; init; } = true;
}
```

### Pourquoi ?

Yarn Spinner envoie au jeu :

```text
- lignes de dialogue ;
- options/choix ;
- commandes ;
- événements de début/fin.
```

CasaEngine doit convertir cela vers son UI, sans que le runtime Yarn connaisse les détails de MGUI, SpriteBatch ou ScreenStack.

### Critère de validation

La démo de l'étape 1 fonctionne encore, mais passe par `IDialoguePresenter`.

---

## Étape 3 — Premier asset `.yarn` minimal

### But

Introduire un vrai fichier Yarn, mais rester sur un seul texte.

### Exemple de fichier

```yarn
title: Start
---
Bonjour depuis Yarn Spinner.
===
```

### Emplacement proposé

```text
Content/Dialogues/demo_hello.yarn
```

### Objectif technique

À ce stade, deux options sont possibles.

#### Option A — Compilation temporaire au démarrage

Acceptable uniquement pour prototypage.

```text
.yarn
 → compilation au démarrage
 → DialogueRunner
```

Avantage :

- plus rapide pour tester.

Inconvénient :

- pas propre pour le runtime final ;
- erreurs de compilation visibles trop tard ;
- coût inutile au lancement du jeu.

#### Option B — Compilation via pipeline d'assets

Recommandé pour CasaEngine.

```text
.yarn source
 → importer éditeur
 → asset compilé
 → chargement runtime
```

Avantage :

- architecture moteur plus propre ;
- validation éditeur ;
- erreurs détectées avant le runtime ;
- cohérent avec le système d'assets CasaEngine.

### Recommandation

Pour CasaEngine, utiliser l'option B dès que possible.

L'option A peut être utilisée seulement pour valider rapidement Yarn Spinner dans une branche de test.

### Critère de validation

Le texte affiché ne vient plus d'une string codée en dur mais d'un fichier Yarn contenant un nœud `Start`.

---

## Étape 4 — Créer `YarnDialogueAsset`

### But

Faire de Yarn un vrai asset CasaEngine.

### Asset proposé

```csharp
public sealed class YarnDialogueAsset
{
    public string Name { get; init; } = string.Empty;
    public string StartNode { get; init; } = "Start";

    // Données compilées Yarn.
    // Le type exact dépendra de l'API Yarn Spinner retenue.
    public byte[] CompiledProgramBytes { get; init; } = Array.Empty<byte>();

    // Métadonnées utiles côté moteur/éditeur.
    public string SourcePath { get; init; } = string.Empty;
    public IReadOnlyList<string> NodeNames { get; init; } = Array.Empty<string>();
}
```

### Fichiers à créer

```text
CasaEngine/Framework/Dialogue/Yarn/YarnDialogueAsset.cs
CasaEngine/Framework/Dialogue/Yarn/YarnDialogueAssetLoader.cs
CasaEngine/Framework/Dialogue/Yarn/YarnDialogueImporter.cs
CasaEngine/Framework/Dialogue/Yarn/YarnDialogueCompiler.cs
```

### Rôle des classes

| Classe | Rôle |
|---|---|
| `YarnDialogueAsset` | Asset runtime chargé par CasaEngine |
| `YarnDialogueImporter` | Lit les `.yarn` source |
| `YarnDialogueCompiler` | Appelle YarnSpinner.Compiler |
| `YarnDialogueAssetLoader` | Charge l'asset compilé dans le jeu |
| `YarnDialogueValidator` | Vérifie les nœuds, erreurs et warnings |

### Critère de validation

Un fichier `.yarn` est importé comme asset CasaEngine et peut être chargé par le moteur.

---

## Étape 5 — Créer `YarnDialogueRunner`

### But

Créer l'équivalent CasaEngine du `DialogueRunner` Yarn Spinner.

Dans Yarn Spinner, le Dialogue Runner est la passerelle entre les scripts Yarn et le reste du jeu : il lance un nœud, livre les lignes, les options et les commandes aux composants de présentation, et utilise un système de stockage de variables.

### Classe proposée

```csharp
public sealed class YarnDialogueRunner
{
    private readonly IDialoguePresenter _presenter;
    private readonly YarnVariableStore _variableStore;
    private readonly YarnCommandDispatcher _commandDispatcher;

    public bool IsRunning { get; private set; }

    public YarnDialogueRunner(
        IDialoguePresenter presenter,
        YarnVariableStore variableStore,
        YarnCommandDispatcher commandDispatcher)
    {
        _presenter = presenter;
        _variableStore = variableStore;
        _commandDispatcher = commandDispatcher;
    }

    public void Start(YarnDialogueAsset asset, string nodeName)
    {
        // Charger le programme compilé Yarn.
        // Démarrer le nœud.
        // Brancher les callbacks lignes / choix / commandes.
    }

    public void Continue()
    {
        // Avancer après une ligne.
    }

    public void SelectChoice(int choiceIndex)
    {
        // Sélectionner une option.
    }

    public void Stop()
    {
        // Arrêter proprement le dialogue.
    }
}
```

### Responsabilités

```text
YarnDialogueRunner
 ├─ démarre un nœud Yarn
 ├─ reçoit les lignes Yarn
 ├─ transmet les lignes au DialoguePresenter
 ├─ reçoit les options Yarn
 ├─ transmet les options au DialoguePresenter
 ├─ reçoit les commandes Yarn
 ├─ les transmet au YarnCommandDispatcher
 └─ signale la fin du dialogue
```

### Critère de validation

Le dialogue `demo_hello.yarn` est exécuté via Yarn Spinner et affiché via l'UI CasaEngine.

---

## Étape 6 — Input : Continue, Close et état modal

### But

Remplacer la fermeture directe de la boîte par une vraie progression de dialogue.

### Comportement

```text
Action pendant une ligne :
 ├─ si le texte est en cours d'animation :
 │   └─ afficher toute la ligne immédiatement
 │
 ├─ sinon si Yarn attend une continuation :
 │   └─ appeler YarnDialogueRunner.Continue()
 │
 └─ sinon si le dialogue est terminé :
     └─ fermer la DialogueScreen
```

Pour la V1, sans typewriter :

```text
Action
 ├─ Continue si le dialogue continue
 └─ Close si le dialogue est fini
```

### Point important

Le bouton Action ne doit pas être traité deux fois :

```text
❌ interaction PNJ + fermeture dialogue dans la même frame
✅ consommer l'input quand DialogueScreen est ouvert
```

### Critère de validation

Le même bouton Action peut :

```text
- ouvrir le dialogue ;
- passer à la ligne suivante ;
- fermer la boîte à la fin.
```

---

## Étape 7 — Plusieurs lignes de dialogue

### But

Valider un dialogue multi-lignes.

### Exemple Yarn

```yarn
title: Start
---
Bonjour.
Bienvenue dans CasaEngine.
Ceci est un dialogue exécuté avec Yarn Spinner.
===
```

### Comportement attendu

```text
Action → ligne 1
Action → ligne 2
Action → ligne 3
Action → fermeture
```

### Critère de validation

Le runner avance correctement ligne par ligne.

---

## Étape 8 — Speakers simples

### But

Afficher le nom du personnage qui parle.

### Exemple Yarn possible

```yarn
title: Start
---
Guide: Bonjour.
Guide: Bienvenue dans CasaEngine.
Player: Merci.
===
```

### Côté CasaEngine

Ajouter à `DialogueLinePresentation` :

```csharp
public string? SpeakerName { get; init; }
```

### UI

Afficher :

```text
[Guide]
Bonjour.
```

### Critère de validation

Le nom du speaker est visible dans la boîte de dialogue.

---

## Étape 9 — Choix simples

### But

Permettre au joueur de choisir une réponse.

### Exemple Yarn

```yarn
title: Start
---
Guide: Veux-tu continuer ?

-> Oui
    Guide: Très bien.
-> Non
    Guide: D'accord.
===
```

### UI minimale

```text
Veux-tu continuer ?

> Oui
  Non
```

### Input nécessaire

```text
Up / Down : changer le choix sélectionné
Action : valider le choix
Cancel : optionnel
```

### Côté code

Ajouter :

```csharp
public void ShowChoices(IReadOnlyList<DialogueChoicePresentation> choices);
public void SelectChoice(int selectedIndex);
```

### Critère de validation

Le joueur peut sélectionner un choix et Yarn continue dans la bonne branche.

---

## Étape 10 — Variables Yarn

### But

Connecter les variables Yarn à un stockage CasaEngine.

### Exemple Yarn

```yarn
title: Start
---
<<set $hasMetGuide = true>>
Guide: Je me souviendrai de toi.
===
```

### Store proposé

```csharp
public sealed class YarnVariableStore
{
    private readonly Dictionary<string, object> _values = new();

    public bool TryGetValue(string name, out object? value)
    {
        return _values.TryGetValue(name, out value);
    }

    public void SetValue(string name, object value)
    {
        _values[name] = value;
    }
}
```

### À terme

Ce store devra être branché au système de sauvegarde CasaEngine.

### Critère de validation

Une variable définie dans un dialogue peut influencer un dialogue suivant.

---

## Étape 11 — Conditions

### But

Permettre des dialogues qui changent selon l'état du jeu.

### Exemple Yarn

```yarn
title: Start
---
<<if $hasMetGuide>>
Guide: Content de te revoir.
<<else>>
Guide: Bonjour, je ne crois pas qu'on se connaisse.
<<set $hasMetGuide = true>>
<<endif>>
===
```

### Critère de validation

Le premier passage affiche le texte de découverte, le deuxième passage affiche le texte de retour.

---

## Étape 12 — Commandes Yarn vers CasaEngine

### But

Permettre à Yarn de déclencher des actions de jeu.

### Exemples de commandes

```yarn
<<play_sound "dialogue_open">>
<<give_item "key">>
<<start_cutscene "intro_camera_pan">>
<<set_flag "met_guide" true>>
```

### Architecture

```text
Yarn command
   ↓
YarnCommandDispatcher
   ↓
IYarnCommandHandler
   ↓
CasaEngine service
```

### Interfaces proposées

```csharp
public interface IYarnCommandHandler
{
    bool CanHandle(string commandName);
    void Handle(YarnCommandContext context);
}
```

```csharp
public sealed class YarnCommandDispatcher
{
    private readonly List<IYarnCommandHandler> _handlers = new();

    public void Register(IYarnCommandHandler handler)
    {
        _handlers.Add(handler);
    }

    public void Dispatch(YarnCommandContext context)
    {
        foreach (var handler in _handlers)
        {
            if (handler.CanHandle(context.CommandName))
            {
                handler.Handle(context);
                return;
            }
        }

        // Log warning: commande inconnue.
    }
}
```

### Handlers V1/V2 recommandés

```text
SetFlagYarnCommandHandler
PlaySoundYarnCommandHandler
StartCutsceneYarnCommandHandler
GiveItemYarnCommandHandler
MoveEntityYarnCommandHandler
SetCameraTargetYarnCommandHandler
```

### Critère de validation

Une commande Yarn déclenche une action CasaEngine visible ou vérifiable.

---

## Étape 13 — Intégration avec les cutscenes

### But

Permettre aux cutscenes CasaEngine de lancer un dialogue et d'attendre sa fin.

CasaEngine possède déjà un système de cutscenes avec actions de type `Wait`, `MoveTo`, `Sequence`, `Parallel`, etc. Le dialogue ne doit pas remplacer les cutscenes. Il doit pouvoir être appelé par elles.

### Nouvelle action proposée

```text
StartDialogueCutsceneActionData
```

### Exemple conceptuel

```text
Sequence
 ├─ MoveTo NPC
 ├─ StartDialogue "intro_dialogue" node "Start"
 └─ MoveTo Player
```

### Données

```csharp
public sealed class StartDialogueCutsceneActionData : CutsceneActionData
{
    public string DialogueAssetName { get; set; } = string.Empty;
    public string StartNode { get; set; } = "Start";
    public bool WaitForCompletion { get; set; } = true;
}
```

### Critère de validation

Une cutscene peut lancer un dialogue et reprendre seulement quand il est terminé.

---

## Étape 14 — Localisation

### But

Préparer le système pour plusieurs langues.

Yarn Spinner est conçu pour extraire les textes visibles dans des fichiers de chaînes localisables. Même si la V1 peut rester en texte simple, il faut éviter de construire une architecture qui empêchera la localisation plus tard.

### Décision recommandée

Pour la V1 :

```text
- utiliser le texte directement depuis Yarn ;
- ne pas bloquer l'intégration sur la localisation.
```

Pour la V2 :

```text
- extraire les line IDs ;
- stocker les tables de localisation dans CasaEngine ;
- permettre à YarnLineProvider de récupérer le texte dans la langue active ;
- prévoir les assets localisables associés aux lignes : voix, portrait alternatif, timing.
```

### Critère de validation

Changer la langue active permet d'afficher un autre texte pour les mêmes lignes de dialogue.

---

## Étape 15 — Sauvegarde

### But

Conserver l'état narratif entre deux sessions.

### À sauvegarder

```text
- variables Yarn ;
- flags narratifs ;
- nœuds déjà visités si nécessaire ;
- dialogue en cours si le jeu autorise la sauvegarde pendant un dialogue ;
- état de quête si les quêtes utilisent Yarn.
```

### Recommandation

Ne pas sauvegarder directement des objets Yarn complexes. Sauvegarder un format CasaEngine stable :

```json
{
  "dialogueVariables": {
    "$hasMetGuide": true,
    "$questAccepted": false,
    "$reputation": 10
  }
}
```

### Critère de validation

Une variable définie par Yarn reste active après sauvegarde puis rechargement.

---

## Étape 16 — Outils éditeur

### But

Permettre de gérer les dialogues depuis l'éditeur CasaEngine.

### V1 éditeur

```text
- importer un fichier .yarn ;
- afficher la liste des nœuds ;
- afficher les erreurs de compilation ;
- afficher les warnings ;
- bouton "Recompile";
- bouton "Run preview";
```

### V2 éditeur

```text
- preview de dialogue dans l'éditeur ;
- liste des speakers ;
- recherche dans les lignes ;
- liste des variables utilisées ;
- liste des commandes utilisées ;
- validation des commandes inconnues ;
- validation des nœuds manquants ;
- liens vers les assets audio/portrait ;
```

### V3 éditeur

```text
- graphe de dialogue ;
- édition visuelle ;
- intégration avec FlowGraph si pertinent ;
- historique des branches ;
- test de conditions ;
- simulation de variables ;
- export localisation ;
- rapport de couverture narrative.
```

### Critère de validation

Un dialogue `.yarn` invalide produit une erreur claire dans l'éditeur avant le lancement du jeu.

---

# Roadmap synthétique

## V1 — Démo visuelle ultra-simple

```text
✅ DialogueService minimal
✅ DialogueScreen minimal
✅ affichage d'un texte codé en dur
✅ bouton Action pour fermer
✅ input consommé par l'UI
```

## V1.1 — Préparation architecture

```text
✅ IDialoguePresenter
✅ DialogueLinePresentation
✅ DialogueRuntimeState
✅ séparation UI / runtime
```

## V1.2 — Premier fichier Yarn

```text
✅ ajout YarnSpinner NuGet
✅ ajout YarnSpinner.Compiler
✅ fichier demo_hello.yarn
✅ exécution d'un nœud Start
✅ affichage d'une ligne Yarn
```

## V1.3 — Multi-lignes

```text
✅ Continue
✅ plusieurs lignes
✅ fermeture à la fin
```

## V2 — Dialogue jouable

```text
✅ speakers
✅ choix
✅ variables
✅ conditions
✅ commandes simples
```

## V3 — Intégration moteur

```text
✅ asset pipeline CasaEngine
✅ sauvegarde
✅ cutscenes
✅ commandes gameplay
✅ triggers PNJ
```

## V4 — Production

```text
✅ localisation
✅ portraits
✅ sons de texte
✅ voice-over
✅ historique
✅ typewriter
✅ debug overlay
✅ éditeur de preview
```

## V5 — Outils avancés

```text
✅ graphe de dialogue
✅ validation avancée
✅ simulation de variables
✅ analyse des branches
✅ intégration forte avec quêtes/cutscenes
```

---

# Définition précise de la première étape visuelle

## Objectif exact

Créer une démo où :

```text
1. Le jeu démarre.
2. Le joueur appuie sur le bouton Action.
3. Une boîte de dialogue apparaît en bas de l'écran.
4. Le texte affiché est : "Bonjour depuis CasaEngine."
5. Le joueur appuie à nouveau sur Action.
6. La boîte disparaît.
```

## Hors scope

```text
❌ Yarn Spinner réel
❌ Asset dialogue
❌ choix
❌ conditions
❌ commandes
❌ sauvegarde
❌ localisation
❌ éditeur
```

## Classes minimales

```text
DialogueService
DialogueStartRequest
DialogueScreen
DialogueViewModel
```

## Pseudo-flow

```text
World.Update
 └─ Input.Action pressed
     ├─ if DialogueService.IsDialogueOpen == false
     │   └─ DialogueService.ShowText("Bonjour depuis CasaEngine.")
     │
     └─ else
         └─ DialogueService.Close()
```

## UI minimale

```text
+------------------------------------------------------+
| Bonjour depuis CasaEngine.                           |
|                                           [Action]   |
+------------------------------------------------------+
```

## Validation manuelle

```text
✅ La boîte s'affiche.
✅ Le texte est lisible.
✅ La boîte se ferme.
✅ L'action n'est pas propagée au gameplay pendant que la boîte est ouverte.
✅ Le système ne dépend pas encore de Yarn Spinner.
```

---

# Risques et décisions à valider

## Décision 1 — Où vit le DialogueService ?

Options :

```text
A. Service global du jeu
B. Service attaché au World
C. Composant attaché à une scène
```

Recommandation :

```text
DialogueService attaché au World ou au contexte de jeu actif.
```

Raison :

- les dialogues dépendent souvent de l'état de la scène ;
- cela évite les dialogues globaux qui survivent à un changement de monde ;
- cela facilite la sauvegarde par monde/scène.

## Décision 2 — Le dialogue met-il le gameplay en pause ?

Options :

```text
A. Le monde continue de s'updater
B. Le gameplay est suspendu
C. Seuls certains systèmes continuent
```

Recommandation V1 :

```text
Bloquer l'input gameplay, mais ne pas encore figer tout le World.
```

Puis ajouter une option :

```csharp
public bool PauseGameplay { get; init; }
```

## Décision 3 — Compilation Yarn au runtime ou à l'import ?

Recommandation :

```text
Compilation à l'import / éditeur.
```

Exception :

```text
Compilation runtime autorisée uniquement pour prototype ou tests.
```

## Décision 4 — UI actuelle ou MGUI ?

Recommandation :

```text
Utiliser l'UI CasaEngine existante pour la V1.
```

Puis fournir une implémentation MGUI propre si MGUI devient l'UI principale du moteur.

## Décision 5 — Cutscene ou Dialogue d'abord ?

Recommandation :

```text
DialogueRunner indépendant d'abord.
CutsceneAction StartDialogue ensuite.
```

Le dialogue ne doit pas être simplement une action de cutscene, car il a ses propres choix, variables, conditions et commandes.

---

# Ce que CasaEngine doit ajouter

## Court terme

```text
DialogueService
DialogueScreen
DialoguePresenter
YarnDialogueAsset
YarnDialogueRunner
YarnDialogueAssetLoader
YarnDialogueCompiler
```

## Moyen terme

```text
YarnVariableStore
YarnCommandDispatcher
StartDialogueCutsceneActionData
Dialogue validation editor
Dialogue preview editor
```

## Long terme

```text
Dialogue graph editor
Localization pipeline
Voice-over support
Quest integration
Narrative debug tools
```

---

# Exemple de fichier Yarn pour les tests futurs

```yarn
title: Start
---
Guide: Bonjour depuis Yarn Spinner.
Guide: Ce dialogue est affiché dans CasaEngine.

-> Continuer
    Guide: Très bien, continuons.
-> Arrêter
    Guide: D'accord, à plus tard.
===
```

---

# Exemple d'organisation des assets

```text
Content/
 └─ Dialogues/
     ├─ demo_hello.yarn
     ├─ village_intro.yarn
     ├─ npc_guide.yarn
     └─ compiled/
         ├─ demo_hello.yarnasset
         ├─ village_intro.yarnasset
         └─ npc_guide.yarnasset
```

---

# Sources consultées

- Yarn Spinner NuGet package : https://www.nuget.org/packages/YarnSpinner
- Yarn Spinner Dialogue Runner documentation : https://github.com/YarnSpinnerTool/YSDocs/blob/main/docs/yarn-spinner-for-unity/components/dialogue-runner.md
- Yarn Spinner localisations and assets documentation : https://docs.yarnspinner.dev/2.3/using-yarnspinner-with-unity/assets-and-localization
- CasaEngineMonogame repository : https://github.com/xcasadio/CasaEngineMonogame
- CasaEngine Framework tree : https://github.com/xcasadio/CasaEngineMonogame/tree/main/CasaEngine/Framework

---

# Résumé final

L'intégration propre de Yarn Spinner dans CasaEngine doit se faire progressivement.

La première étape ne doit pas chercher à intégrer tout Yarn Spinner. Elle doit seulement valider la partie visible et interactive :

```text
ouvrir une boîte de dialogue
afficher un texte simple
fermer avec le bouton Action
```

Ensuite seulement, il faut brancher Yarn Spinner derrière cette UI avec :

```text
YarnDialogueAsset
YarnDialogueRunner
YarnDialoguePresenter
YarnVariableStore
YarnCommandDispatcher
```

Cette approche évite de mélanger UI, gameplay, cutscenes et narration, et permet à CasaEngine d'obtenir un système de dialogue moderne, testable et extensible.

Decisions: see [ADR-0022](../decisions/0022-yarn-spinner-dialogue-integration.md).
