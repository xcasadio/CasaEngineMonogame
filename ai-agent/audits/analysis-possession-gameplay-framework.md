# Analyse — possession et framework gameplay (Pawn / Controller / PlayerController)

Date : 2026-08-19. Analyse en lecture seule de la gestion de la « possession » (quelle entité a accès aux inputs du joueur) dans CasaEngine, et comparaison avec Unreal Engine, Unity et Godot.

## TL;DR

La façade inspirée d'Unreal (`Pawn`, `Controller`, `PlayerController`, `AIController`, `Player`) est un squelette : les commentaires promettent `Possess()` / `UnPossess()` / `GetControlRotation()`, mais **aucune de ces méthodes n'existe**. La possession réelle se réduit à une référence `PlayerController.Pawn` posée une fois au chargement du monde. Aucun input ne transite par les controllers : le gameplay (RPGDemo) lit directement les managers globaux d'`InputComponent` et a même recréé sa **propre** hiérarchie `Controller` sans lien avec celle du moteur.

En parallèle, le moteur a développé deux systèmes **modernes et fonctionnels** qui couvrent déjà les deux moitiés du problème que la possession est censée résoudre :

- `InputRouter` — routage des inputs par vue/joueur (`PlayerIndex` → `ViewId`, arbitrage UI/gameplay, prêt pour le splitscreen) ;
- `CharacterControllerComponent.ControlMode` (`Player`/`AI`/`Script`/`Cutscene`/`Disabled`) — arbitrage de « qui a l'autorité de mouvement », déjà consommé par navigation, steering, cutscenes et `CharacterMotionSystem`.

Il manque uniquement le **pont** entre les deux : rien ne relie « ce joueur local » à « cette entité reçoit ses inputs ». Recommandation : ne pas finir le modèle Unreal, mais formaliser un modèle léger type Unity/Godot au-dessus de l'existant (§ Recommandations).

## 1. État actuel de l'implémentation

### 1.1 Les classes gameplay (`CasaEngine/Framework/Gameplay/`)

| Classe | Contenu réel | État |
| --- | --- | --- |
| `Pawn : Entity` | `InputEnabled` (bool, défaut `true`) + back-référence `Controller` | Squelette. `Controller` n'est **jamais assignée** nulle part (référence morte). Pas de `PossessedBy`/`UnPossessed`, pas de setup d'input. |
| `Controller : ObjectBase` | `Pawn { get; set; }` | Squelette. Le commentaire de classe décrit `Possess()`/`UnPossess()`/`ControlRotation`, rien n'est implémenté. Pas d'`Update`, pas de cycle de vie : c'est un objet de données passif, hors du monde (hérite d'`ObjectBase`, pas d'`Entity`). |
| `PlayerController : Controller` | `Player`, `IsInputEnable`, `AssignedViewId`, `UIView`, helpers HUD/pause menu | La partie vue/UI est réelle et branchée (voir 1.3). **Aucun accès à l'input** : pas de référence à `InputComponent`, aucun traitement d'input. `IsInputEnable` est écrit par `ScriptSign` mais **jamais lu** → sans effet. |
| `AIController : Controller` | Vide (uniquement des champs Unreal en commentaire) | Coquille vide. Le RPGDemo utilise son propre `AiController` sans rapport. |
| `Player : ObjectBase` | Vide | Coquille vide. |
| `LocalPlayer : Player` | `ControllerId` (`PlayerIndex`, défaut `One`) | Minimal mais utilisé (résolution du `PlayerIndex` par `GameManager`). |
| `PlayerStartupSettings` | `DefaultPawnAssetId`, `PlayerControllerClass`, `AIControllerClass`, `HUDClass` (sérialisé, ex-`.gameMode`) | Fonctionnel : consommé par `World.InitializePlayerControllers()`. `AIControllerClass` et `HUDClass` ne sont consommés par personne. |

### 1.2 Le cycle de vie réel de la « possession »

Tout se passe dans [World.cs:281](CasaEngine/Framework/Scene/World/World.cs:281) (`InitializePlayerControllers`, appelée au chargement si `GameplayExecutionPolicy.InitializePlayerControllers`) :

1. spawn du pawn par défaut (`PlayerStartupSettings.DefaultPawnAssetId`) ;
2. création du `PlayerController` via `ElementFactory` (`PlayerControllerClass`) ;
3. `playerController.Pawn = pawn` — c'est **toute** la possession ; `pawn.Controller` n'est pas renseigné (lien unidirectionnel) ;
4. `playerController.Player = new LocalPlayer()` (marqué `// TODO`) — toujours `PlayerIndex.One`, un seul joueur possible ;
5. placement du pawn sur le `PlayerStartComponent` de l'index One.

Ensuite, plus rien : personne n'update les `PlayerControllers` (la classe n'a pas d'`Update`), la possession ne change jamais en cours de partie, et il n'existe ni `Possess()` ni `UnPossess()`.

La seule API de possession consommée par du gameplay est [World.GetPlayerController(entity)](CasaEngine/Framework/Scene/World/World.cs:828) (recherche linéaire `Pawn == entity`), utilisée par `ScriptSign` du RPGDemo pour tester « est-ce l'entité joueur ? ».

### 1.3 Ce qui fonctionne vraiment : l'association joueur ↔ vue ↔ UI

La partie la plus aboutie du `PlayerController` n'a rien à voir avec la possession : c'est un **porteur d'affectation de vue et d'UI**. [GameManager.SyncPlayerViewAssignments()](CasaEngine/Framework/Application/GameManager.cs:154) synchronise pour chaque `PlayerController` :

- `InputRouter.AssignPlayer(playerIndex, viewId)` — mapping joueur → vue ;
- `AssignedViewId` et `UIView` — la `RenderView` et le runtime UI (HUD) du joueur.

[InputRouter](CasaEngine/Framework/Input/InputRouter.cs) est un système moderne et complet : routage par vue (modal > capture souris > capture pointeur UI > vue sous le curseur > focus clavier > fallback), coordonnées souris locales à la vue, arbitrage UI-first (`IsMouseHandledByUI`, `IsKeyboardCapturedByUI`), et mapping `PlayerIndex → ViewId` pensé pour le splitscreen. Son commentaire promet que « gamepad and abstracted keyboard input are sent to the correct view / PlayerController » — mais **le routage s'arrête au niveau de la vue** : rien n'achemine jamais un état d'input jusqu'au `PlayerController` ou au pawn.

### 1.4 Ce que fait le jeu réel (RPGDemo) : il contourne tout

Le RPGDemo illustre le vide du système moteur en le réimplémentant :

- il définit sa propre hiérarchie [Controllers/Controller.cs](Projects/CasaEngine.RPGDemo/Controllers/Controller.cs) (FSM + `Character`), avec `HumanPlayerController` et `AiController`/`CpuPlayerController`, **sans aucun lien** avec `CasaEngine.Framework.Gameplay.Controller` ;
- [ScriptPlayer](Projects/CasaEngine.RPGDemo/Scripts/ScriptPlayer.cs:25) (un `GameplayProxy`) crée `new HumanPlayerController(Character, PlayerIndex.One)` en dur et l'update lui-même chaque frame ;
- [HumanPlayerController](Projects/CasaEngine.RPGDemo/Controllers/HumanPlayerController.cs:53) lit l'input **directement** dans les managers globaux (`KeyboardManager.IsKeyPressed(Keys.Up)`, `GamePadManager.GetGamePad(index)`), touches en dur, sans `InputMappingManager`, sans le contexte par vue de l'`InputRouter`, et sans respecter l'arbitrage UI ;
- le gate d'input testé est `Character.Owner.InputEnabled` (le flag du `Pawn`), alors que `ScriptSign` désactive `PlayerController.IsInputEnable` → **les deux flags ne se parlent pas**, le blocage d'input du panneau ne bloque rien.

### 1.5 Le système parallèle : `CharacterControlMode`

Le chantier character-controller a introduit une notion d'autorité de contrôle au niveau composant : [CharacterControllerComponent.ControlMode](CasaEngine/Framework/Scene/Entities/Components/CharacterControllerComponent.cs:77) (`Player`, `AI`, `Script`, `Cutscene`, `Disabled`), avec `SetControlMode()`, et des consommateurs réels qui l'empruntent et le restaurent proprement : `CharacterControllerNavigationDriverComponent`, `CharacterControllerSteeringBridgeComponent`, `CutsceneActionCoroutineFactory`, `CharacterMotionSystem`.

C'est, de fait, la moitié « autorité » d'un système de possession — qui a le droit de pousser des `SetMoveIntent`/`RequestJump` sur ce corps — mais il manque le producteur côté joueur : **rien** ne relie `PlayerController` à `SetControlMode(Player)` ni ne pousse les inputs du joueur vers `SetMoveIntent`.

### 1.6 Synthèse du diagnostic

Le concept de possession existe en trois morceaux non connectés :

1. `PlayerController.Pawn` — lien statique posé au chargement, jamais exploité pour router de l'input ;
2. `InputRouter` — sait router l'input vers **une vue/un joueur**, pas vers une entité ;
3. `CharacterControlMode` — sait dire **qui a l'autorité** sur une entité, sans notion de joueur.

Le chemin réellement utilisé par le gameplay est : `GameplayProxy` (script) → lecture directe des managers globaux d'`InputComponent`. Conséquences concrètes :

- pas de multi-joueur local possible malgré un `InputRouter` prêt pour le splitscreen (index One en dur partout) ;
- deux flags « input désactivé » concurrents dont un mort (`PlayerController.IsInputEnable`) ;
- l'arbitrage UI-first de l'`InputRouter` est contournable (et contourné) par tout script qui lit les managers globaux ;
- pas de transfert de contrôle possible (véhicule, spectateur, cutscene qui rend la main) au niveau moteur — seul `ControlMode` en couvre une partie, côté mouvement uniquement.

## 2. Comparaison avec les moteurs modernes

### 2.1 Unreal Engine (le modèle d'origine)

- La possession est un mécanisme central et complet : `AController::Possess(APawn*)` avec callbacks des deux côtés (`OnPossess`/`OnUnPossess`, `APawn::PossessedBy`/`UnPossessed`), `GameMode` qui orchestre spawn/restart/respawn, `PlayerState` répliqué.
- L'input est attaché à l'acteur possédé : chaque `Pawn` a un `UInputComponent` (pile d'input avec priorités), le `PlayerController` pousse l'input vers le pawn possédé, `ControlRotation` sépare la visée de l'orientation du corps.
- La possession est aussi le mécanisme d'**autorité réseau** (quel client a le droit d'envoyer des inputs pour quel pawn) — c'est la principale raison de sa lourdeur.
- `AIController` est un vrai cerveau (behavior trees, blackboard, perception, path following).

CasaEngine a copié les noms et les commentaires de ce modèle, mais aucune de ses mécaniques. Et une bonne partie de la valeur du modèle Unreal (réplication, respawn orchestré par le GameMode, spectateurs) correspond à des besoins que CasaEngine n'a pas aujourd'hui (pas de réseau ; le `GameplayMode` maison couvre déjà les règles de partie).

### 2.2 Unity

- **Aucun concept de possession** dans le moteur. L'Input System package fournit `PlayerInput` (component posé sur le GameObject du joueur, qui bind un `InputActionAsset` et pousse les actions vers les scripts) et `PlayerInputManager` (multi-joueur local : join/leave, appairage des devices par joueur via `InputUser`, splitscreen automatique).
- « Posséder » une autre entité = déplacer/activer un `PlayerInput`, ou re-router ses événements vers un autre script — chaque jeu le code à la main.
- Le point fort est l'**appairage device ↔ joueur** et les action maps par contexte (`Gameplay`/`UI`) avec bascule d'une ligne.

### 2.3 Godot

- Pas de possession non plus. L'input est global (singleton `Input` + actions déclarées dans l'`InputMap`) ou événementiel (`_input`/`_unhandled_input` propagés dans l'arbre de nodes, l'UI consommant les événements avant le gameplay — même philosophie que l'arbitrage UI-first de l'`InputRouter` CasaEngine).
- Le contrôle d'une entité = « quel script lit quelles actions ». Le transfert de contrôle se fait en swappant un node/script contrôleur ; le multi-joueur local se fait par filtrage `device` sur les événements ou par suffixes d'actions (`move_left_p1`, `move_left_p2`) — manuel mais trivial.

### 2.4 Positionnement de CasaEngine

| Aspect | Unreal | Unity | Godot | CasaEngine aujourd'hui |
| --- | --- | --- | --- | --- |
| Possession intégrée | Oui, complète | Non | Non | Non (façade vide) |
| Input par joueur local | `PlayerController` | `PlayerInput` + `PlayerInputManager` | filtrage device manuel | `InputRouter` (`PlayerIndex → ViewId`) — sans consommateur gameplay |
| Arbitrage UI vs gameplay | Oui (input stack) | Oui (action maps) | Oui (propagation) | Oui (`InputRouter`) — contournable via managers globaux |
| Autorité de contrôle sur l'entité | possession | à la main | à la main | `CharacterControlMode` (fonctionnel) |
| Mapping d'actions | Enhanced Input | InputActionAsset | InputMap | `InputMappingManager` — présent, non utilisé par le RPGDemo |

L'infrastructure existante de CasaEngine (routage par vue + autorité par composant + scripts `GameplayProxy`) ressemble structurellement bien plus à Unity/Godot qu'à Unreal. La couche Pawn/Controller est précisément la partie ni finie, ni utilisée, ni alignée avec le reste du moteur.

## 3. Recommandations

S'inspirer d'Unreal n'était pas une erreur en soi, mais **finir le modèle Unreal serait la mauvaise direction** : sa valeur (réseau, respawn orchestré, AIController-cerveau) répond à des besoins absents, et le moteur a évolué vers un modèle component-based + scripts qui appelle un design plus léger. Proposition, par ordre de valeur :

1. **Assumer le rôle réel du `PlayerController` : une session de joueur local.** Il porte déjà `Player`/`PlayerIndex`, la vue et l'UI. C'est l'équivalent du couple `PlayerInput`+`InputUser` d'Unity. (Un renommage conceptuel — `PlayerSession`, `LocalPlayerContext` — peut attendre ; c'est une API publique.)
2. **Définir la possession comme un lien `PlayerController ↔ Entity` avec effets de bord explicites.** `Possess(entity)` : pose le lien, met `CharacterControllerComponent.SetControlMode(Player)` si présent ; `UnPossess()` : retire le lien, repasse en `AI`/`Disabled`. Pas besoin de la classe `Pawn` : n'importe quelle `Entity` est possédable, ce qui évite de forcer une hiérarchie d'héritage (le RPGDemo n'utilise `Pawn` que comme cast de commodité).
3. **Donner au gameplay un point d'accès input par joueur.** Une petite façade (sur le `PlayerController` ou accessible par lui) qui expose l'état filtré : snapshot de la vue assignée via `InputRouter`/`ViewInputContext`, mappings de `InputMappingManager`, et qui retourne « rien » quand l'UI capture le clavier ou quand l'input du joueur est désactivé. C'est elle qui fait respecter l'arbitrage UI-first et le gate d'input — aujourd'hui contournés par la lecture directe des managers globaux. **[Implémenté le 2026-08-19 — voir `docs/engine/player-input.md` ; commits `fda5fa81`, `a07244a7`, `ee046095`.]**
4. **Réconcilier les deux flags d'input.** Garder un seul chemin (proposition : `PlayerController.IsInputEnable` comme source, la façade du point 3 l'applique ; supprimer `Pawn.InputEnabled` ou le déprécier). Corrige au passage le bug latent de `ScriptSign`. **[Partiellement : la démo RPG ne lit plus `Pawn.InputEnabled`, mais l'unification des deux flags reste à faire.]**
5. **Nettoyer le code mort** : `AIController` moteur (vide, doublonné par les briques AI réelles : navigation driver, steering bridge, FSM), back-référence `Pawn.Controller` jamais assignée, `Player` vide (fusionner avec `LocalPlayer` ou l'étoffer le jour où un besoin réseau existe).
6. **Brancher le multi-joueur local de bout en bout** (optionnel, plus tard) : `InitializePlayerControllers` sait déjà presque tout faire pour N joueurs ; il manque une boucle sur les `PlayerStart` par index et la fin du `// TODO` sur `LocalPlayer`.

Les points 2–4 sont le cœur : ils connectent les trois morceaux existants (lien de possession, `InputRouter`, `ControlMode`) sans rien réécrire, et le RPGDemo peut migrer dessus progressivement (son `HumanPlayerController` FSM resterait, mais lirait la façade au lieu des managers globaux).

## Limites de l'analyse

- Analyse statique uniquement (aucun code exécuté).
- Le RacingGame et les démos n'ont pas été audités en détail ; seuls les usages trouvés par recherche (`RPGDemo`, `SampleProject`) sont couverts.
- L'aspect réseau des trois moteurs de référence est décrit de mémoire (résumé de leurs docs publiques), pas re-vérifié en ligne.
