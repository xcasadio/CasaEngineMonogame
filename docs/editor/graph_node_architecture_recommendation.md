# Recommandation d'architecture pour l'integration de graphes a noeuds dans CasaEngine

## Statut du document

Ce document remplace la version precedente qui decrivait en grande partie une implementation theorique du systeme de graphe.

L'objectif ici est different:

- de decrire l'architecture cible en restant aligne avec l'etat reel du depot;
- de distinguer clairement ce qui existe deja de ce qui reste a implementer;
- de fournir une base exploitable pour planifier les prochaines versions cote CasaEngine.

Ce document ne suppose pas un socle MGUI vierge.

## Constat verifie dans le depot

Le depot contient deja une V1 fonctionnelle du controle de graphe generique dans MGUI.

Elements verifies:

- un controle `MGGraphView` dans `MGUI/MGUI.Core/UI/MGGraphControls.cs`;
- un modele et des services dans `MGUI/MGUI.Core/UI/Graph/`;
- un guide V1 dans `MGUI/docs/graph-view-v1-guide.md`;
- un plan de taches dans `MGUI/docs/Tasks/graph-tasks.md` (l'ancien `MGUI/docs/graph_node_system_agent_plan.md` cite ici n'existe plus);
- un sample `GraphViewDialogue` dans `MGUI/MGUI.Samples/Features/GraphViewDialogue.xaml` et `GraphViewDialogue.xaml.cs`;
- une couverture de tests dediee dans `MGUI/MGUI.Tests/Graph/`.

Le depot ne montre pas aujourd'hui de couche CasaEngine equivalente deja en place pour:

- des editeurs de graphes metiers;
- des assets de graphes metiers;
- des compilers de graphes generiques;
- un pipeline runtime generique de graphes pour dialogue, material, shader ou visual scripting.

Exception importante:

- CasaEngine possede deja une base runtime de graphe d'animation avec `IAnimationGraphNode` et `AnimationController.PlayGraph` dans `CasaEngine/Framework/Animations/`.

Conclusion:

- le sujet n'est plus de concevoir la V1 MGUI;
- le sujet est maintenant d'integrer ce socle dans CasaEngine par versions, domaine par domaine.

## Role des couches

### MGUI

MGUI fournit le controle generique de graphe et son infrastructure d'edition.

Cela comprend deja, dans le depot:

- les controles visuels de graphe;
- le document de graphe et ses modeles;
- les commandes undo / redo;
- la validation structurelle;
- la serialization JSON versionnee;
- la navigation viewport;
- la selection;
- les connexions;
- les commentaires;
- le sample et les tests V1.

Regle:

```text
MGUI sait afficher, editer, valider structurellement et serialiser un graphe generique.
```

### CasaEngine.Editor

CasaEngine.Editor doit donner un sens metier au graphe dans les outils.

Cela doit couvrir:

- l'ouverture d'un asset dans un editeur de graphe;
- la bibliotheque de noeuds du domaine choisi;
- la validation metier;
- les interactions avec l'Asset Browser, les proprietes et les documents de l'editeur;
- la sauvegarde du format de donnees metier.

Regle:

```text
CasaEngine.Editor decide quels noeuds existent, ce qu'ils signifient et comment ils sont authorises dans un domaine donne.
```

### CasaEngine runtime

Le runtime CasaEngine ne doit pas executer l'UI MGUI.

Il doit consommer soit:

- des donnees compilees produites depuis le graphe d'edition;
- soit une representation runtime specifique au domaine.

Regle:

```text
Le runtime execute des donnees metier, pas des controles MGUI.
```

## Contrat reel du socle MGUI actuel

Le contrat a respecter pour toute integration doit partir des types reels existants, et non d'un modele theorique.

### Controles publics deja presents

- `MGGraphView`
- `MGGraphNode`
- `MGGraphPort`
- `MGGraphCommentBox`

### Modele et services deja presents

- `GraphDocument`
- `GraphNodeModel`
- `GraphPortModel`
- `GraphEdgeModel`
- `GraphCommentModel`
- `GraphViewportTransform`
- `GraphCommandStack`
- `GraphSerializer`
- `GraphDocumentValidator`
- `GraphTypeCompatibilityService`

### Consequences importantes

Le modele actuel n'utilise pas les formes suivantes:

- pas de `TypeId` sur `GraphNodeModel`, mais `NodeType`;
- pas de listes `Inputs` et `Outputs` sur le noeud, mais une seule collection `Ports`;
- pas de `Dictionary<string, object>` dans le modele de base, mais principalement des metadonnees texte;
- pas de `GraphDocumentSerializer`, mais `GraphSerializer`.

Toute architecture CasaEngine doit donc se brancher sur ce contrat reel, ou bien expliciter une migration future. Elle ne doit pas supposer une autre API que celle deja livree.

## Contraintes d'architecture a conserver

### Dependances a autoriser

```text
CasaEngine.Editor -> MGUI
CasaEngine.Editor -> CasaEngine runtime
```

### Dependances a eviter

```text
MGUI -> CasaEngine
MGUI -> CasaEngine.Editor
MGUI -> systems runtime CasaEngine
```

### Consequence pratique

Le sens metier d'un noeud ne doit pas vivre dans MGUI.

Exemples a ne pas ajouter dans MGUI:

- noeuds de materiau CasaEngine;
- references d'assets CasaEngine;
- execution runtime de dialogue;
- compilation shader ou material;
- logique de gameplay ou visual scripting.

## Validation

### Validation deja prise en charge par MGUI

Le socle MGUI actuel prend en charge la validation structurelle.

Exemples:

- compatibilite de direction;
- compatibilite de type;
- respect de la cardinalite;
- prevention optionnelle des cycles;
- verification des ports requis;
- verification d'edges invalides dans un document charge.

### Validation a ajouter cote CasaEngine

La validation metier reste a faire pour chaque domaine.

Exemples selon le domaine:

- presence d'un noeud d'entree obligatoire;
- presence d'une sortie metier valide;
- references d'assets resolvables;
- contraintes semantiques propres au domaine;
- interdiction de certains noeuds editor-only dans les donnees runtime.

Regle:

```text
MGUI valide la structure.
CasaEngine valide le sens metier.
```

## Serialization

Le socle MGUI possede deja une serialization JSON versionnee avec migration.

Cette serialization doit etre consideree comme le format du document d'edition generique, pas forcement comme le format final des assets runtime CasaEngine.

Recommendation:

- utiliser `GraphSerializer` pour la persistance du document de travail en editeur;
- definir ensuite, cote CasaEngine, soit un asset qui encapsule ce document, soit une etape de transformation vers un format metier propre;
- ne pas faire dependre le runtime de controles UI MGUI.

## Rendu et performance

Le socle actuel suit deja une decision importante:

- les edges sont rendus centralement par la vue de graphe;
- une connexion n'est pas un controle UI individuel;
- la geometrie d'edge et le culling existent deja dans MGUI.

Toute extension CasaEngine doit conserver cette direction et ne pas reintroduire un modele de rendu plus couteux base sur un element par edge.

## Ce qui ne doit pas etre relance comme chantier V1

Les points suivants ne doivent pas etre reouverts comme s'il fallait repartir de zero:

- creation de `MGGraphView`;
- creation du modele de document de base;
- undo / redo de base;
- serialization JSON de base;
- sample Dialogue Graph de reference;
- tests de base du graphe MGUI.

Ces briques existent deja dans le depot.

## Ce qui reste a implementer dans CasaEngine

Le vrai chantier commence au-dessus du socle MGUI.

### Besoins editor

- un type de document CasaEngine pour ouvrir un graphe dans l'editeur;
- une facon de lier un asset ou un document a `MGGraphView`;
- une bibliotheque de noeuds metiers pour un premier domaine;
- une validation metier exploitable dans l'UI editor;
- une integration minimale avec les proprietes et la sauvegarde.

### Besoins runtime

- une representation runtime propre au domaine choisi;
- une transformation depuis le document edite vers cette representation;
- une execution ou evaluation runtime si le domaine le demande.

## Domaine recommande pour la premiere integration CasaEngine

Le premier slice recommande reste un graphe de dialogue simple.

Pourquoi:

- le depot contient deja un sample `GraphViewDialogue` dans MGUI;
- le dialogue demande moins de dependances bas niveau que material ou shader graph;
- le domaine permet de valider le cycle complet edition -> validation -> sauvegarde -> transformation runtime;
- il permet de prouver l'integration editor sans toucher immediatement au pipeline de rendu.

Ce choix doit rester borne.

Le premier objectif n'est pas:

- un material graph complet;
- un shader graph;
- un visual scripting generaliste;
- un behavior tree complet;
- une integration animation complete.

## Cas particulier de l'animation

L'animation ne doit pas etre traitee comme un domaine vierge.

CasaEngine possede deja une representation runtime de graphe d'animation dans `CasaEngine/Framework/Animations/`.

Donc, si un editeur de graphe d'animation est ajoute plus tard, il devra:

- soit produire des objets ou des donnees compatibles avec `IAnimationGraphNode` et le runtime existant;
- soit introduire une nouvelle representation en expliquant explicitement comment elle remplace ou migre l'existant.

Il ne faut pas concevoir une architecture d'animation editor completement deconnectee de cette base runtime deja presente.

## Plan recommande par versions

### Version 1

Prendre le socle MGUI actuel comme baseline officielle.

Objectifs:

- ne rien reimplementer dans MGUI sur le perimetre deja livre;
- s'appuyer sur `MGUI/docs/graph-view-v1-guide.md` et `MGUI/docs/Tasks/graph-tasks.md` comme references d'implementation;
- verifier que l'integration future CasaEngine consomme les types reels deja exposes.

Livrable:

- documentation alignee sur l'existant;
- point de depart clair pour les travaux CasaEngine.

### Version 2

Ajouter une premiere integration CasaEngine.Editor sur un graphe de dialogue simple.

Objectifs:

- ouvrir un document ou asset de dialogue dans l'editeur;
- afficher et manipuler le graphe via `MGGraphView`;
- fournir une palette minimale de noeuds metiers;
- afficher les erreurs de validation metier.

Livrable:

- un premier editeur de graphe metier utilisable dans CasaEngine.Editor;
- aucun compiler generique multi-domaines;
- aucun systeme plugin global de noeuds.

### Version 3

Ajouter la transformation runtime du meme domaine.

Objectifs:

- convertir le document edite en donnees runtime de dialogue;
- charger et executer ces donnees dans le runtime du domaine;
- prouver le round-trip minimal entre editeur et runtime.

Livrable:

- une chaine verticale complete sur un seul domaine.

### Version 4

Etendre ensuite le pattern a un deuxieme domaine seulement apres validation du premier.

Ordre recommande:

1. dialogue;
2. animation si l'integration reutilise proprement le runtime existant;
3. behavior tree ou autre domaine logique;
4. material / shader graph seulement si le pipeline de compilation et preview est clairement borne.

## Architecture recommandee a court terme

### Cote MGUI

Conserver le socle existant sans lui ajouter de semantique CasaEngine.

### Cote CasaEngine.Editor

Ajouter une couche d'adaptation qui fait le lien entre:

- les assets ou documents editor;
- le `GraphDocument` MGUI;
- la bibliotheque de noeuds du domaine;
- la validation metier;
- l'etat de presentation de l'editeur.

### Cote runtime CasaEngine

Executer une representation metier propre, eventuellement compilee depuis le document d'edition.

## Architecture a moyen terme

Si un besoin reel apparait pour reutiliser le modele de graphe hors de MGUI sans embarquer la couche UI, une extraction du modele vers un assembly neutre pourra etre etudiee plus tard.

Cette extraction n'est pas un prerequis pour commencer l'integration CasaEngine, car le depot est aujourd'hui organise autour du contrat MGUI existant.

## Recommandation finale

La bonne strategie n'est pas de redessiner un systeme de graphe complet depuis zero.

La bonne strategie est:

```text
1. accepter le socle V1 MGUI deja present comme base officielle ;
2. integrer un premier domaine metier dans CasaEngine.Editor ;
3. produire une representation runtime du meme domaine ;
4. n'etendre a d'autres graphes qu'apres validation d'un premier slice vertical complet.
```

En l'etat du depot, le point de depart recommande est donc:

- socle generique de graphe dans MGUI: deja present;
- premiere integration editor CasaEngine: dialogue graph simple;
- premiere extension runtime CasaEngine: pipeline dialogue;
- integration animation: seulement en se branchant explicitement sur le runtime d'animation deja existant.