# PlayerInput dans CasaEngine

Façade d'input par joueur qui filtre les lectures gameplay derrière trois gates au lieu de lire directement les managers globaux. Note du 2026-08-19.

## Vue d'ensemble

Avant cette façade, le gameplay lisait directement `KeyboardManager`, `MouseManager`, `GamePadManager` ou `InputMappingManager`. Cela contournait deux mécanismes existants : le routage par vue (`InputRouter`, utile en multi-viewport/éditeur) et l'arbitrage UI-first de MGUI (une UI qui capture le clavier ou la souris doit passer avant le gameplay). Le gate `PlayerController.IsInputEnable` existait déjà mais n'était pas toujours respecté par les lectures directes.

[`PlayerInput`](../../CasaEngine/Framework/Input/PlayerInput.cs) est une façade `sealed` par joueur qui applique systématiquement trois gates avant de renvoyer une valeur :

1. **`PlayerController.IsInputEnable`** — coupe tout (clavier, souris, gamepad, mappings) quand `false`. Vaut `true` par défaut.
2. **Routage par vue** (`InputRouter.CurrentInputContext`) — clavier et souris uniquement : la lecture n'est autorisée que si le snapshot d'input courant appartient à la vue du joueur (`InputRouter.GetRenderViewForPlayer`), ou si le contexte est le fallback partagé `ViewId.Empty`, ou s'il n'existe aucun `InputRouter` (contexte sans routage par vue).
3. **Arbitrage UI-first** — `InputRouter.IsKeyboardCapturedByUI` coupe le clavier, `InputRouter.IsMouseHandledByUI` coupe la souris, pour la vue du joueur.

Le gamepad est associé à `PlayerController.PlayerIndex` (dérivé de `LocalPlayer.ControllerId`) et n'est filtré que par le gate 1 : il n'y a pas de routage par vue ni de capture UI pour le gamepad aujourd'hui.

## Câblage

`World.InitializePlayerControllers()` crée la façade automatiquement :

```csharp
playerController.Input = new PlayerInput(playerController, Game.InputComponent);
```

... mais seulement quand un `Game`/`InputComponent` existe. `PlayerController.Input` peut donc être `null` :

- en contexte headless ou preview éditeur (pas de `InputComponent`) ;
- pour un `PlayerController` créé manuellement en dehors de `InitializePlayerControllers()`.

Dans ces cas, le code appelant doit construire la façade lui-même avec le constructeur public :

```csharp
new PlayerInput(playerController, inputComponent);
```

## Utilisation

Le gameplay résout la façade via le `World`, avec un repli manuel si elle est absente (voir [`HumanPlayerController`](../../Projects/CasaEngine.RPGDemo/Controllers/HumanPlayerController.cs)) :

```csharp
var playerInput = world.GetPlayerController(entity)?.Input;

if (playerInput != null && playerInput.IsInputEnabled)
{
    if (playerInput.IsGamePadConnected)
    {
        var move = playerInput.ThumbStickLeft;
    }
    else if (playerInput.IsKeyPressed(Keys.Up))
    {
        // ...
    }

    var jump = playerInput.GetButtonState("Jump");
}
```

## Règles de disponibilité

| Source            | Gate 1 (`IsInputEnable`) | Gate 2 (routage par vue) | Gate 3 (capture UI) |
|--------------------|:---:|:---:|:---:|
| Clavier            | oui | oui | oui (clavier) |
| Souris              | oui | oui | oui (souris) |
| Gamepad             | oui | non | non |
| Mappings (`GetButtonState`) | oui | via clavier | via clavier, sauf si un gamepad est connecté (voir Limitations) |

## Limitations

- **Mapping mixte clavier+gamepad (V1)** : `GetButtonState(name)` ne renvoie un état neutre que si le clavier est indisponible **et** qu'aucun gamepad n'est connecté pour ce joueur. Un mapping mixte peut donc encore lire son côté gamepad alors que le clavier est capturé par l'UI.
- **Contrôleurs créés manuellement** : un `PlayerController` instancié en dehors de `World.InitializePlayerControllers()` n'a pas de `Input` câblé automatiquement ; l'appelant doit construire `PlayerInput` lui-même.

## Tests

Voir [`CasaEngine.Tests/Input/PlayerInputTests.cs`](../../CasaEngine.Tests/Input/PlayerInputTests.cs) (7 tests couvrant les trois gates).

## Prochaines étapes

- ~~Unifier `Pawn.InputEnabled` avec `PlayerController.IsInputEnable` en un seul gate.~~ Fait : `Pawn.InputEnabled` a été supprimé, `PlayerController.IsInputEnable` est l'unique gate.
- ~~Ajouter une API `Possess`/`UnPossess` reliant `PlayerController` ↔ `Entity` et pilotant `CharacterControllerComponent.SetControlMode`.~~ Fait, voir [gameplay-possession.md](gameplay-possession.md).
- ~~Câbler le multi-joueur local de bout en bout (plusieurs `LocalPlayer`/`PlayerStart`).~~ Fait côté spawn/contrôleurs, voir [gameplay-possession.md](gameplay-possession.md) ; restent le join/leave à chaud et l'appairage des devices.

Origine : [ai-agent/audits/analysis-possession-gameplay-framework.md](../../ai-agent/audits/analysis-possession-gameplay-framework.md), point 3 des recommandations.
