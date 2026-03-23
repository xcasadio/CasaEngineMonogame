# World Message Bus Migration Tasks

Etat observe le 2026-03-23.

## Objectif

Supprimer l'usage de `MessageManagerRouter` comme point d'entree principal du messaging IA partage et le remplacer par une architecture plus moderne basee sur un `WorldMessageBus` scope au world.

Le resultat vise est le suivant :

- plus de dependance de gameplay a `#sym:MessageManagerRouter` ;
- plus de singleton global impose pour la communication entre entites ;
- un transport de messages pilote par le temps de simulation du world ;
- une architecture exploitable pour migrer `WestWorldWithMessaging` dans `CasaEngine.AISamples`.

## Scope

- Inclure `CasaEngineMonogame/CasaEngine/Framework/AI/Messaging`
- Inclure les integrations runtime necessaires dans `CasaEngineMonogame/CasaEngine/Framework/World` si utile
- Inclure `CasaEngine.AISamples` pour le futur port de `WestWorldWithMessaging`
- Exclure tout grand rewrite sans lien direct avec le messaging IA

## Constat court

- `MessageManagerRouter` repose sur un singleton statique global.
- `MessageManagerRouter` utilise `DateTime.Now.Ticks`, donc une horloge systeme et non le temps de simulation.
- le routage immediat et le dispatch effectif ne sont pas completes.
- l'architecture actuelle ne colle pas bien a un world runtime multi-instance ni a une simulation deterministe.
- `WestWorldWithMessaging` a besoin d'un bus de messages par world, pas d'un service global partage par tout le process.

## Architecture cible

### 1. World scoped message bus

Introduire un `WorldMessageBus` possede par le world ou par un service runtime attache au world.

Responsabilites :

- enregistrer les endpoints de messages du world ;
- accepter les messages immediats et differes ;
- ordonnancer les dispatches selon le temps de simulation ;
- vider la queue a chaque tick du world ;
- ne jamais dependre de `DateTime.Now`.

### 2. Message endpoints proches des entites

Les entites ou leurs composants doivent exposer une capacite claire de reception :

- via `IMessageable` deja existant ;
- ou via un `MessageInboxComponent` / `FSMComponent` qui delegue a la FSM.

### 3. Router global obsolete

`MessageManagerRouter` ne doit plus etre le point d'entree recommande du gameplay moderne.

Le code gameplay cible doit dependre de :

- `WorldMessageBus`
- ou d'une abstraction world-scoped equivalente

et non d'un singleton statique partage.

### 4. Temps de simulation

Le bus doit etre pilote par un temps de simulation explicite :

- `currentSimulationTime`
- ou un compteur de ticks / secondes du runner

et non par l'horloge machine.

## Legende des statuts

- `⬜` a faire
- `🟨` en cours
- `✅` termine
- `⛔` bloque

## Regles d'execution pour l'agent

1. Faire exactement un commit par tache terminee.
2. Mettre a jour l'icone de statut avant et apres chaque tache.
3. Ne pas marquer une tache `✅` sans validation minimale reussie.
4. Si une tache revele un blocage d'architecture, la passer en `⛔`, documenter le blocage puis traiter ce blocage dans une tache dediee.
5. Les commits doivent etre petits, lisibles, et faciles a revert.
6. Le gameplay nouveau ne doit plus dependre directement de `MessageManagerRouter`.
7. Si `MessageManagerRouter` est conserve temporairement pour compatibilite, il doit devenir un simple adaptateur obsolete et non le coeur du systeme.

## Validation minimale par tache

- `dotnet build CasaEngineMonogame/CasaEngine/CasaEngine.csproj -c Debug`
- si une tache touche `CasaEngine.AISamples` : `dotnet build CasaEngine.AISamples/CasaEngine.AISamples.csproj -c Debug`
- si une tache touche le flux messaging runtime : lancer un test borne ou un sample borne et verifier les dispatches immediats et differes

## Format de commit recommande

- `docs(ai-messaging): define world message bus migration plan`
- `refactor(ai-messaging): introduce world message bus contract`
- `feat(ai-messaging): add world scoped message queue`
- `feat(ai-messaging): add entity message endpoint registration`
- `refactor(ai-messaging): decouple router from system clock`
- `refactor(ai-messaging): route fsm messages through world bus`
- `feat(westworld): port messaging sample to world bus`
- `test(ai-messaging): validate immediate and delayed dispatch`
- `docs(ai-messaging): document migration and deprecation path`

## Plan committable

---

### ✅ MSG-001 - Cadrer la migration et figer la cible

**Objectif**

Figer noir sur blanc que `MessageManagerRouter` n'est plus l'architecture cible et que `WorldMessageBus` devient le modele recommande.

**Travail**

- documenter les limites du router actuel ;
- decrire la cible `WorldMessageBus` ;
- expliciter la regle de travail un commit par tache.

**Resultat attendu**

- un plan executable existe dans le depot ;
- l'agent sait quel symbole sortir du chemin critique du gameplay.

**Validation minimale**

- le present fichier existe et decrit l'ordre de migration.

**Commit suggere**

- `docs(ai-messaging): define world message bus migration plan`

---

### 🟨 MSG-002 - Introduire l'abstraction moderne de bus scope au world

**Objectif**

Creer le contrat runtime d'un bus de messages par world, sans casser le build existant.

**Travail**

- introduire `WorldMessageBus` ou `IWorldMessageBus` ;
- definir un modele de message runtime pilote par temps de simulation ;
- separer clairement l'API moderne du router legacy.

**Fichiers / zones probables**

- `CasaEngineMonogame/CasaEngine/Framework/AI/Messaging/*`
- eventuellement `CasaEngineMonogame/CasaEngine/Framework/World/*`

**Resultat attendu**

- une API world-scoped existe ;
- aucune dependance a `DateTime.Now` dans cette nouvelle API.

**Validation minimale**

- `dotnet build CasaEngineMonogame/CasaEngine/CasaEngine.csproj -c Debug`

**Commit suggere**

- `refactor(ai-messaging): introduce world message bus contract`

---

### ⬜ MSG-003 - Implementer la queue de messages differee pilotee par simulation

**Objectif**

Remplacer la logique de dispatch basee sur l'horloge machine par une queue ordonnee sur le temps de simulation.

**Travail**

- implementer l'enqueue des messages immediats et differes ;
- faire dependre le dispatch d'un temps de simulation explicite ;
- garantir un ordre stable de dispatch.

**Resultat attendu**

- les messages differes ne dependent plus de `DateTime.Now.Ticks` ;
- le bus peut etre ticke depuis une boucle de world ou de sample deterministe.

**Validation minimale**

- build runtime OK ;
- test borne ou harness confirmant message immediat + message differe.

**Commit suggere**

- `feat(ai-messaging): add world scoped message queue`

---

### ⬜ MSG-004 - Ajouter le routage vers des endpoints d'entites

**Objectif**

Permettre au bus de trouver et notifier les recepteurs sans singleton global de gameplay.

**Travail**

- definir comment un world resolve `EntityId -> endpoint` ;
- brancher les entites ou composants recepteurs ;
- clarifier le chemin de reception vers `IMessageable` ou `FSMComponent`.

**Resultat attendu**

- une entite du world peut recevoir un message adresse par son `Guid` ;
- le bus ne depend pas d'un `EntityManager` global legacy.

**Validation minimale**

- build runtime OK ;
- test borne sur un envoi entre deux endpoints du meme world.

**Commit suggere**

- `feat(ai-messaging): add entity message endpoint registration`

---

### ⬜ MSG-005 - Integrer la reception dans la FSM et les composants IA

**Objectif**

Faire du messaging un flux naturel pour les agents IA modernes du moteur.

**Travail**

- brancher la reception de messages vers `FSMComponent` ou un composant inbox dedie ;
- conserver la logique `HandleMessage` cote etats ;
- eviter que les states aillent lire une queue globale eux-memes.

**Resultat attendu**

- les etats peuvent reagir aux messages via le composant de FSM ;
- le moteur garde une separation claire entre transport et logique IA.

**Validation minimale**

- build runtime OK ;
- scenario borne avec reception immediate et reception differee par une FSM.

**Commit suggere**

- `refactor(ai-messaging): route fsm messages through world bus`

---

### ⬜ MSG-006 - Reduire `MessageManagerRouter` a un adaptateur legacy ou le deprecier

**Objectif**

Faire disparaitre `MessageManagerRouter` du chemin nominal du gameplay moderne.

**Travail**

- retirer ses `NotImplementedException` si un adaptateur transitoire est necessaire ;
- sinon le marquer obsolete et rediriger les appels restants ;
- supprimer toute recommandation de l'utiliser dans le nouveau code.

**Resultat attendu**

- plus de nouveau code qui depend directement de `MessageManagerRouter` ;
- le symbole peut rester temporairement pour compatibilite, mais n'est plus le coeur du systeme.

**Validation minimale**

- build runtime OK ;
- recherche workspace montrant que le nouveau gameplay cible n'utilise plus ce symbole comme dependance principale.

**Commit suggere**

- `refactor(ai-messaging): deprecate legacy message manager router`

---

### ⬜ MSG-007 - Migrer `WestWorldWithMessaging` sur `WorldMessageBus`

**Objectif**

Porter le sample Buckland avec messagerie sur la nouvelle architecture moderne.

**Travail**

- creer les entites du world cible dans `CasaEngine.AISamples` ;
- utiliser `GameplayProxy` pour initialiser FSM et dependances ;
- brancher les messages immediats et differes via `WorldMessageBus` ;
- conserver le comportement narratif de l'exemple de reference.

**Resultat attendu**

- `WestWorldWithMessaging` ne depend plus d'un dispatcher global singleton ;
- les interactions entre agents passent par le bus du world.

**Validation minimale**

- `dotnet build CasaEngine.AISamples/CasaEngine.AISamples.csproj -c Debug`
- validation bornee du scenario messaging principal.

**Commit suggere**

- `feat(westworld): port messaging sample to world bus`

---

### ⬜ MSG-008 - Ajouter une validation bornee du messaging moderne

**Objectif**

Eviter une regression silencieuse sur les dispatches immediats, differes et l'ordre de traitement.

**Travail**

- ajouter un test borne ou un validateur de sample ;
- verifier au minimum :
  - dispatch immediat ;
  - dispatch differe ;
  - ordre stable ;
  - reception par la bonne entite.

**Resultat attendu**

- la nouvelle couche messaging est verifiable sans lancer un scenario manuel long.

**Validation minimale**

- commande de validation bornee documentee et executable.

**Commit suggere**

- `test(ai-messaging): validate immediate and delayed dispatch`

---

### ⬜ MSG-009 - Documenter la voie moderne et la strategie de retrait

**Objectif**

Laisser une documentation claire pour les prochains agents et eviter le retour a `MessageManagerRouter` par habitude.

**Travail**

- documenter l'usage recommande de `WorldMessageBus` ;
- documenter le statut legacy de `MessageManagerRouter` ;
- noter les ecarts volontaires avec Buckland et les choix de simulation.

**Resultat attendu**

- la direction d'architecture est claire pour les futures migrations IA.

**Validation minimale**

- une note ou doc de migration existe dans le depot.

**Commit suggere**

- `docs(ai-messaging): document migration and deprecation path`