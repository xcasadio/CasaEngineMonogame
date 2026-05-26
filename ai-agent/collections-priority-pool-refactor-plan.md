# Plan IA - Collections Core : priorites et pools

Date : 2026-05-26

## Perimetre verifie

- Dossier audite : `CasaEngine/Core/Collections`.
- Fichiers presents : `IPriorityQueue.cs`, `PriorityQueue.cs`, `UniquePriorityQueue.cs`, `IndexedPriorityQueue.cs`, `Pool.cs`.
- `PoolWithoutAccessor<T>` n'est pas present dans ce dossier.
- Le repo cible `net9.0` / `net9.0-windows`, donc `System.Collections.Generic.PriorityQueue<TElement, TPriority>` est disponible.
- `GridPathfinder2D` utilise deja la priority queue BCL : `PriorityQueue<int, float>`.
- Usages internes trouves des collections custom :
  - `AStarSearch` et `DijkstraSearch` utilisent `IndexedPriorityQueue<double>` et appellent `ChangePriority` apres baisse de cout.
  - `MessageManagerHandler` utilise `UniquePriorityQueue<Message>`.
  - Aucun usage C# interne de `Pool<T>` n'a ete trouve par recherche texte.
- Aucun test dedie aux priority queues custom ou a `Pool<T>` n'a ete trouve. Des tests existent pour `GridPathfinder2D`, mais ils couvrent la priority queue BCL via le pathfinder moderne.

## Analyse et critique

L'analyse de depart est globalement coherente, avec deux nuances importantes.

Pour `IPriorityQueue<T>`, `PriorityQueue<T>` et `UniquePriorityQueue<T>`, la suppression a terme est defendable parce que la BCL fournit deja une priority queue moderne, et le codebase l'utilise deja dans `GridPathfinder2D`. En revanche, ce n'est pas une suppression immediate sans migration : `UniquePriorityQueue<Message>` est encore branchee dans `MessageManagerHandler`, et `IndexedPriorityQueue<T>` herite encore de `PriorityQueue<int>`.

`PriorityQueue<T>` expose une API fragile pour un moteur moderne : elle implemente `IList<T>` alors que plusieurs operations jettent `NotSupportedException`, `Clone` n'est pas implemente, le non-generic enumerator jette aussi, `Dequeue`/`Peek` retournent `default` quand la file est vide, et l'operation `Update(index)` n'est accessible qu'a travers l'heritage ou l'indexeur. Cela confirme que cette classe est davantage un vieux support technique qu'une abstraction moteur a conserver telle quelle.

`UniquePriorityQueue<T>` n'est pas seulement une priority queue BCL avec un nom different. Elle refuse les doublons par recherche lineaire et considere deux elements identiques quand `Comparer.Compare(...) == 0`. Pour `MessageComparer`, ce `0` encode une notion metier precise : meme sender, receiver, type, extra info, et dispatch times dans une precision donnee. Toute migration de `MessageManagerHandler` doit donc soit conserver explicitement cette semantique de deduplication, soit documenter sa suppression comme un changement de comportement.

`IndexedPriorityQueue<T>` est la seule priority queue custom justifiee par les usages actuels : A* et Dijkstra modifient la priorite d'un noeud deja dans la frontier via `ChangePriority`. La critique de l'heritage est justifiee : la classe derive de `PriorityQueue<int>` pour reutiliser le heap et `Update`, mais son invariant principal est ailleurs, dans `ReversedIndexes` et la liste externe de couts. Elle merite une implementation autonome, avec validation claire des index presents/absents et sans API heritee non pertinente.

`Pool<T>` porte une bonne idee moteur : tableau dense d'elements actifs, `Count`, et release par swap avec le dernier actif. Le probleme est la securite des handles. `Accessor` ne contient qu'un index ; apres `Release`, un ancien accessor peut pointer vers un autre element, une double release peut corrompre l'etat, et rien ne prouve qu'un accessor vienne de ce pool. `Elements` est public, ce qui permet aussi de modifier directement la zone active ou inactive. La proposition d'un `DensePool<T>` avec handles generationnels est donc justifiee. Comme `Pool<T>` est public, il faut toutefois privilegier une phase de compatibilite avant suppression.

## Regles pour l'agent IA

- Faire un commit apres chaque tache atomique, avec le build et les tests pertinents au vert.
- Ne pas supprimer une API publique sans phase de compatibilite ou validation explicite du caractere breaking.
- Ajouter des tests avant les refactors qui changent une structure de donnees.
- Garder les changements scopes a `CasaEngine/Core/Collections` et aux appels internes identifies.
- Pour chaque tache, verifier au minimum `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore` si les dependances sont deja restaurees ; sinon lancer la commande equivalente sans `--no-restore`.

## Taches proposees

### 1. ⏳ Baseline tests des comportements actuels

Objectif : figer les comportements utiles avant refactor.

Travail :
- Ajouter des tests unitaires pour `IndexedPriorityQueue<T>` : ordre de dequeue, `ChangePriority` apres baisse de cout, absence de duplication dans les usages A*/Dijkstra.
- Ajouter des tests pour `UniquePriorityQueue<Message>` ou pour le comportement equivalent attendu dans `MessageManagerHandler` : deduplication selon `MessageComparer` et ordre par `DispatchTime`.
- Ajouter des tests pour `Pool<T>` : `Fetch`, `Release`, maintien du prefixe dense, et exposition du probleme de handle stale comme comportement a remplacer.

Validation :
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore --filter Collections`
- Si le filtre ne matche aucun test runner local, lancer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore`.

Commit attendu : `test(collections): cover legacy queue and pool behavior`

### 2. ⏳ Reecrire `IndexedPriorityQueue<T>` sans heritage

Objectif : conserver le besoin moteur legitime de priorite mutable, mais supprimer la dependance a `PriorityQueue<int>`.

Travail :
- Remplacer l'heritage par une implementation autonome avec heap d'index, reverse indexes, comparer de valeurs indexees et operations `Enqueue`, `Dequeue`, `Peek`, `ChangePriority`, `Clear`, `Count`.
- Preserver les constructeurs actuellement utilises par A*/Dijkstra.
- Ajouter des validations explicites pour index hors bornes, index absent de la queue, et double enqueue d'un meme index.
- Eviter l'exposition d'API heritee non pertinente (`IList<T>`, indexeur mutable, `Clone`).

Validation :
- Tests collections ajoutes a la tache 1.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore --filter FullyQualifiedName~AI`

Commit attendu : `refactor(collections): decouple indexed priority queue heap`

### 3. ⏳ Migrer ou isoler `UniquePriorityQueue<Message>`

Objectif : retirer la dependance interne a `UniquePriorityQueue<T>` sans perdre la semantique de deduplication des messages.

Travail :
- Verifier si `MessageManagerHandler` est encore une API runtime supportee ou un chemin legacy, car `World` utilise `WorldMessageBus`.
- Si `MessageManagerHandler` doit rester supporte, remplacer `UniquePriorityQueue<Message>` par une structure locale explicite : priority queue BCL pour l'ordre temporel, plus deduplication equivalente a `MessageComparer`.
- Si `MessageManagerHandler` est legacy, le marquer obsolete et rediriger la documentation vers `WorldMessageBus`, sans suppression immediate.
- Conserver ou adapter les tests de deduplication de la tache 1.

Validation :
- Tests messaging existants ou nouveaux tests cibles sur `MessageManagerHandler` / `WorldMessageBus`.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore --filter FullyQualifiedName~Messaging`

Commit attendu : `refactor(ai): remove legacy unique priority queue dependency`

### 4. ⏳ Deprecier `IPriorityQueue<T>`, `PriorityQueue<T>` et `UniquePriorityQueue<T>`

Objectif : rendre le chemin de migration explicite sans casser les consommateurs externes immediatement.

Travail :
- Ajouter `[Obsolete]` sur les trois types avec message indiquant `System.Collections.Generic.PriorityQueue<TElement, TPriority>` ou `IndexedPriorityQueue<T>` pour les priorites mutables.
- Mettre a jour les commentaires XML pour expliquer le statut legacy.
- Ne supprimer les fichiers que dans une tache breaking separee et validee.

Validation :
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Commit attendu : `chore(collections): deprecate legacy priority queues`

### 5. ⏳ Introduire `DensePool<T>` avec handles generationnels

Objectif : fournir une pool moteur sure avec tableau dense d'actifs.

Travail :
- Ajouter `DensePool<T>` sans remplacer immediatement `Pool<T>`.
- Utiliser un handle valeur contenant au minimum slot/index interne et generation.
- Garantir que les handles stale, doubles releases et handles d'un autre pool echouent proprement via `TryGet`, `TryRelease` ou exceptions d'erreur de programmation.
- Garder un prefixe dense parcourable sans allocations evitables.
- Prevoir une politique claire de reset/reuse des elements actifs.

Validation :
- Tests : fetch/release, reutilisation de slots, generation incrementee, stale handle invalide, double release invalide, iteration dense.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore --filter DensePool`

Commit attendu : `feat(collections): add generational dense pool`

### 6. ⏳ Deprecier `Pool<T>` et documenter la migration

Objectif : proteger la compatibilite tout en dirigeant le code vers `DensePool<T>`.

Travail :
- Ajouter `[Obsolete]` sur `Pool<T>` avec message de migration vers `DensePool<T>`.
- Ajouter un court guide dans la documentation ou dans les commentaires XML : ancien `Accessor` contre nouveau handle generationnel.
- Ne pas supprimer `Pool<T>` tant qu'aucun consommateur externe n'a ete pris en compte.

Validation :
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Commit attendu : `chore(collections): deprecate unsafe pool`

### 7. ⚠️ Tache breaking optionnelle : suppression des types legacy

Objectif : supprimer le code obsolete uniquement quand une version breaking est acceptee.

Preconditions :
- Plus aucun usage interne des types obsoletes.
- Validation explicite du maintien ou non de compatibilite publique.
- Documentation de migration disponible.

Travail :
- Supprimer `IPriorityQueue<T>`, `PriorityQueue<T>`, `UniquePriorityQueue<T>`.
- Supprimer `Pool<T>` si la compatibilite publique n'est plus requise.
- Nettoyer les usings et references restantes.

Validation :
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-restore`
- `dotnet build CasaEngine.MonoGame.sln --no-restore`

Commit attendu : `break(collections): remove obsolete priority queues and pool`