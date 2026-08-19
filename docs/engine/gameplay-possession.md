# Possession dans CasaEngine

Lien explicite `Controller` ↔ `Entity` qui pilote l'autorité de mouvement (`CharacterControlMode`), et boucle de spawn multi-joueur local qui s'appuie dessus. Note du 2026-08-19.

## Vue d'ensemble

La possession relie un [`PlayerController`](../../CasaEngine/Framework/Gameplay/PlayerController.cs) ou un [`AIController`](../../CasaEngine/Framework/Gameplay/AIController.cs) à une `Entity` quelconque — pas seulement un `Pawn` — via `Controller.Possess(Entity)` / `Controller.UnPossess()`. Quand l'entité possédée porte un `CharacterControllerComponent`, la possession capture le `ControlMode` courant du composant, applique le mode propre au controller, puis restaure le mode capturé à la libération :

- `PlayerController` impose `CharacterControlMode.Player` ;
- `AIController` impose `CharacterControlMode.AI` ;
- `Controller` (base) n'impose rien (`PossessedControlMode` vaut `null`).

Cela connecte les deux systèmes qui existaient déjà séparément : `PlayerController`/`AIController` (qui joue) et `CharacterControllerComponent.ControlMode` (qui a l'autorité de mouvement sur le corps).

## API

```csharp
var controller = new PlayerController();
controller.Possess(pawn); // capture pawn.ControlMode, applique CharacterControlMode.Player

// ... plus tard
controller.UnPossess(); // restaure le ControlMode capturé, controller.Pawn redevient null
```

- `Controller.Pawn` est de type `Entity` (setter privé) : n'importe quelle entité possédant un `CharacterControllerComponent` est possédable, sans dépendre de la classe `Pawn`.
- Reposséder l'entité déjà possédée est un no-op ; posséder une nouvelle entité alors qu'une autre l'est déjà appelle d'abord `UnPossess()` sur l'ancienne.
- Les hooks protégés `OnPossess(Entity)` / `OnUnPossess(Entity)` permettent aux controllers dérivés de réagir (câblage d'input, changement d'état FSM, etc.) sans dupliquer la logique de capture/restauration du `ControlMode`.
- Si l'entité n'a pas de `CharacterControllerComponent`, la possession pose simplement `Controller.Pawn` sans toucher à aucun composant.

## Multi-joueur local

`World.InitializePlayerControllers()` boucle désormais sur les index de joueur déclarés par les [`PlayerStartComponent`](../../CasaEngine/Framework/Scene/Entities/Components/PlayerStartComponent.cs) du monde (`PlayerStartComponent.PlayerIndex`, sérialisé sous la clé JSON `player_index`) :

1. `CollectLocalPlayerIndices` récupère les `PlayerIndex` distincts parmi les `PlayerStartComponent` du monde, triés, ou `[PlayerIndex.One]` si aucun `PlayerStartComponent` n'en déclare (repli mono-joueur, comportement inchangé) ;
2. pour chaque index, un pawn est spawné (`SpawnEntity<Pawn>(PlayerStartupSettings.DefaultPawnAssetId)`) puis un `PlayerController` est créé et lui est associé via `CreateLocalPlayerController` ;
3. `CreateLocalPlayerController` crée le `PlayerController`, pose `Player = new LocalPlayer { ControllerId = playerIndex }`, appelle `Possess(pawn)`, câble `PlayerInput` quand un `Game.InputComponent` existe, puis place le pawn sur le `PlayerStartComponent` correspondant à l'index (`FindPlayerStart`) s'il existe.

Chaque pawn spawné a désormais un `Entity.Id` propre : `World.SpawnEntity<T>(Guid)` clone l'asset avec `cache: false`, donc plusieurs joueurs spawnés depuis le même `DefaultPawnAssetId` ne partagent plus l'`Id` de l'asset source.

L'affectation vue/UI par joueur (`GameManager.SyncPlayerViewAssignments`, `PlayerController.AssignedViewId`/`UIView`) fonctionnait déjà pour N joueurs et n'a pas changé : `LocalPlayer.ControllerId` (le `PlayerIndex`) est la clé commune entre le spawn décrit ici et le routage de vue/input.

## Sérialisation et migration

`PlayerStartComponent.PlayerIndex` est additif : la clé JSON `player_index` est lue si présente (`element.ContainsKey("player_index")`), sinon la valeur par défaut `PlayerIndex.One` s'applique. Les mondes existants, qui n'ont pas cette clé, se chargent donc sans changement de comportement. Le rollback est sûr dans l'autre sens : un ancien runtime qui ne connaît pas `player_index` l'ignore simplement lors de la désérialisation JSON générique.

## Changements cassants

- **`Controller.Pawn` : `Pawn` → `Entity`, setter devenu privé.** La possession ne dépend plus de la classe `Pawn` ; le champ ne s'assigne plus que via `Possess`/`UnPossess`.
- **`Pawn.InputEnabled` et `Pawn.Controller` supprimés.** Le gate d'input est désormais uniquement `PlayerController.IsInputEnable` (via `PlayerInput`, voir [player-input.md](player-input.md)) ; la back-référence `Controller` sur `Pawn` n'était jamais assignée et a été retirée.
- **`World.SpawnEntity<T>(Guid)` retourne un clone à `Id` neuf.** Avant, l'entité spawnée gardait l'`Id` de l'asset source ; plusieurs entités spawnées depuis le même asset partageaient donc le même `Id`. Ce n'est plus le cas — nécessaire pour que le spawn multi-joueur produise des pawns distincts.

## Tests

- [`CasaEngine.Tests/Gameplay/ControllerPossessionTests.cs`](../../CasaEngine.Tests/Gameplay/ControllerPossessionTests.cs) (8 tests) — `Possess`/`UnPossess`, capture/restauration du `ControlMode`, changement de pawn, re-possession no-op, entité sans `CharacterControllerComponent`, argument null.
- [`CasaEngine.Tests/Gameplay/LocalMultiplayerTests.cs`](../../CasaEngine.Tests/Gameplay/LocalMultiplayerTests.cs) (11 tests) — `CollectLocalPlayerIndices`, `FindPlayerStart`, `CreateLocalPlayerController`, repli mono-joueur.

## Limites et prochaines étapes

- Pas de join/leave à chaud : les `PlayerController` sont créés une seule fois au chargement du monde, il n'existe pas d'API pour ajouter ou retirer un joueur en cours de partie.
- Pas d'appairage device ↔ joueur : `PlayerIndex` est posé par `PlayerStartComponent`/`LocalPlayer`, rien ne relie dynamiquement une manette ou un clavier détecté à un index de joueur.
- Pas de `ControlRotation` : contrairement au modèle Unreal d'origine, rien ne sépare la visée de l'orientation du corps possédé.
- `AIController` reste minimal : il applique `CharacterControlMode.AI` à la possession mais n'a toujours pas de logique de décision (pas de cerveau IA) ; navigation et steering restent portés par des composants dédiés (`CharacterControllerNavigationDriverComponent`, `CharacterControllerSteeringBridgeComponent`).

Origine : [ai-agent/audits/analysis-possession-gameplay-framework.md](../../ai-agent/audits/analysis-possession-gameplay-framework.md), points 2, 4, 5 et 6 des recommandations.
