# World Message Bus Migration Notes

Etat finalise le 2026-03-23.

## Direction moderne

Le gameplay IA moderne du depot doit passer par un bus scope au `World`.

Point d'entree recommande :

- `World.MessageBus`
- ou l'abstraction `IWorldMessageBus`

Le bus moderne apporte :

- un scope par world, donc compatible multi-world et runtime multi-instance ;
- un temps de simulation explicite via `CurrentSimulationTime` et `DispatchDueMessages(...)` ;
- un enregistrement automatique d'endpoints a partir des `Entity` et de leurs composants `IMessageable` ;
- une file differée stable pour les dispatches ordonnes.

## Pattern recommande

1. Construire les agents IA comme `Entity` + `EntityComponent`.
2. Exposer la reception de messages via un composant `IMessageable` proche de la logique IA.
3. Pour une FSM, utiliser un composant de FSM qui implemente `IMessageable`.
4. Enregistrer les entites dans `World.MessageBus` via le cycle de vie du world.
5. Dispatcher les messages differes avec le temps de simulation du runner, pas avec l'horloge machine.

## Legacy

`MessageManagerRouter` reste seulement pour compatibilite legacy.

Statut :

- obsolete ;
- hors du chemin nominal du gameplay moderne ;
- a n'utiliser qu'en fallback pour du vieux code qui n'est pas encore world-scoped.

`PathPlanner` privilegie maintenant `World.MessageBus` et ne retombe sur le router legacy qu'en dernier recours hors contexte world.

## Ecart volontaire avec Buckland

Le pattern original Buckland repose sur un dispatcher global singleton.

Le port moderne prefere :

- un bus attache au world ;
- des endpoints d'entites ;
- une simulation deterministic-friendly ;
- des FSM alimentees par messages via composant.

Le comportement narratif reste proche, mais l'architecture suit les contraintes d'un moteur runtime moderne.

## Validation bornee

Commande utile :

- `dotnet build CasaEngine/CasaEngine.csproj -c Debug`

Note (2026-09-06) : le projet `CasaEngine.AISamples` et ses samples `westworld` cites par les versions precedentes de cette note ne sont plus dans le depot (aucun `.csproj` ni fichier source ne les contient). Les commandes de validation par sample (`--validate-fsm-message-routing`, `--validate-westworld-with-messaging`) et la selection `--sample westworld1` / `--sample westworld-with-messaging` sont donc historiques et ne peuvent plus etre relancees.

## Selection du sample interactif

Historique : le host `CasaEngine.AISamples` acceptait `--sample westworld1` et `--sample westworld-with-messaging` ; ce host n'existe plus dans le depot.

Important : le host fixe explicitement `ContentPath` avant l'initialisation pour eviter que `CasaEngineGame` interprete `--sample` comme un chemin de contenu.