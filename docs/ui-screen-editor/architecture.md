# Architecture

## 1. Objectif architectural

Le screen editor doit permettre l'édition de screens MGUI dans CasaEngine sans faire des instances runtime MGUI la source de vérité.

Le principe central est le suivant :
- le **Screen Document Model** porte l'état éditable
- la **preview runtime** est reconstruite à partir du document
- la **sérialisation XAML** est le format d'échange principal avec le disque
- l'**Editor UI** et les **Editor Services** manipulent le document, jamais directement l'arbre runtime comme vérité métier

## 2. Couches

### 2.1 Screen Asset

Responsabilité :
- représenter un screen comme asset CasaEngine
- référencer le fichier source XAML et les métadonnées d'édition minimales
- s'intégrer au catalogue d'assets et au cycle open/save/reload du projet

Exemples de contenu attendus :
- identifiant d'asset
- nom
- chemin du XAML
- métadonnées de preview ou de thème si nécessaire

Cette couche ne doit pas :
- contenir l'arbre éditable complet du screen
- porter la logique de preview runtime
- dépendre directement des contrôles runtime MGUI

### 2.2 Screen Document Model

Responsabilité :
- représenter la source de vérité de l'écran en mémoire côté éditeur
- stocker la hiérarchie des nœuds, les propriétés, les ids stables et les métadonnées de design-time
- supporter les opérations d'édition, de sélection et plus tard d'undo/redo

Exemples de types :
- `UIScreenDocument`
- `UIScreenNode`
- `UIScreenPropertyValue`

Cette couche ne doit pas :
- contenir des références directes à `MGElement`, `MGWindow` ou autres instances runtime
- dépendre d'une vue d'éditeur concrète
- dépendre du host de preview

### 2.3 XAML Serializer / Parser

Responsabilité :
- convertir le XAML MGUI vers le document model
- convertir le document model vers un XAML déterministe et valide
- centraliser les limites de support de la v1

Cette couche doit :
- rester déterministe
- exposer des erreurs structurées pour le chargement et la sauvegarde
- encapsuler les détails de parsing et de génération XAML

Cette couche ne doit pas :
- stocker l'état d'édition courant
- reconstruire ou posséder durablement la preview runtime

### 2.4 Runtime Preview Adapter

Responsabilité :
- transformer un `UIScreenDocument` en arbre runtime MGUI visualisable
- reconstruire la preview à chaque changement selon la stratégie v1
- isoler les objets runtime des couches d'édition

Exemples de types :
- `UIScreenPreviewBuilder`
- mapping `DocumentNodeId -> runtime control`

Cette couche peut dépendre :
- du document model
- des APIs runtime MGUI et CasaEngine nécessaires à la preview

Cette couche ne doit pas :
- modifier le document directement sans passer par les services d'édition
- devenir une source de vérité secondaire

### 2.5 Editor Services

Responsabilité :
- orchestrer le cycle open/save/reload
- maintenir la session d'édition
- gérer sélection, dirty state, commandes et coordination entre document et preview
- fournir des services testables indépendants de l'UI concrète

Exemples de types :
- `UIScreenEditorSession`
- `UIScreenSelectionService`
- futurs services de commandes et d'insertion

Cette couche ne doit pas :
- contenir des contrôles WPF ou MGUI d'interface
- sérialiser l'UI visuelle elle-même

### 2.6 Editor UI

Responsabilité :
- afficher la hiérarchie, l'inspector, la toolbox et la preview
- relayer les actions utilisateur vers les services d'édition
- refléter l'état de session sans l'héberger comme vérité métier diffuse

Exemples de vues :
- panneau hiérarchie
- panneau propriétés
- host de preview
- toolbox

Cette couche ne doit pas :
- muter directement le document hors des services dédiés
- persister le XAML elle-même
- contenir la logique métier de conversion document/runtime

## 3. Dépendances autorisées

Le graphe de dépendances cible est :

- `Screen Asset` -> aucune dépendance vers les autres couches métier du screen editor
- `Screen Document Model` -> aucune dépendance vers les couches runtime ou UI
- `XAML Serializer / Parser` -> dépend de `Screen Document Model`
- `Runtime Preview Adapter` -> dépend de `Screen Document Model`
- `Editor Services` -> dépend de `Screen Asset`, `Screen Document Model`, `XAML Serializer / Parser`, `Runtime Preview Adapter`
- `Editor UI` -> dépend de `Editor Services` et peut consommer des DTO ou view-models issus de la session

Représentation textuelle :

```text
Screen Asset
Screen Document Model
    ^            ^
    |            |
XAML Serializer  Runtime Preview Adapter
          ^      ^
           \    /
         Editor Services
               ^
               |
            Editor UI
```

## 4. Dépendances interdites

Sont explicitement interdites :
- `Screen Document Model` -> runtime MGUI
- `Screen Document Model` -> Editor UI
- `Screen Asset` -> Runtime Preview Adapter
- `Editor UI` -> XAML Serializer / Parser en accès direct pour muter le document
- `Editor UI` -> contrôles runtime MGUI comme source de vérité d'édition
- `Runtime Preview Adapter` -> Editor UI

Conséquences pratiques :
- un clic dans la preview ne modifie pas directement un contrôle runtime pour le rendre persistant
- la preview sélectionne un `DocumentNodeId`, puis les services mettent à jour la session
- toute sauvegarde passe par le document puis par le serializer

## 5. Flux principaux

### 5.1 Ouverture
1. l'utilisateur ouvre un `UIScreenAsset`
2. `Editor Services` charge le XAML
3. le parser produit un `UIScreenDocument`
4. la session devient propriétaire du document courant
5. le preview adapter reconstruit l'arbre runtime
6. l'Editor UI affiche hiérarchie, inspector et preview

### 5.2 Édition
1. l'utilisateur agit depuis la hiérarchie, l'inspector ou la preview
2. l'Editor UI traduit l'action en commande ou appel de service
3. `Editor Services` modifie le document
4. la session passe en dirty
5. la preview runtime est reconstruite depuis le document
6. l'UI se resynchronise sur l'état de session

### 5.3 Sauvegarde
1. `Editor Services` récupère le document courant
2. le serializer produit le XAML
3. le fichier est écrit sur disque
4. la session repasse en état non dirty si l'opération réussit

## 6. Règles de séparation à respecter pendant l'implémentation

- Toute nouvelle feature doit choisir explicitement sa couche avant d'être codée.
- Si une donnée doit survivre à open/save/reload, elle appartient au document ou à l'asset, pas à la preview.
- Si une donnée n'a de sens que pour l'affichage ou l'interaction immédiate, elle appartient à la session ou à l'UI, pas au document sérialisé.
- La preview v1 peut être reconstruite intégralement après chaque modification, tant que l'isolation architecturale reste stricte.
- Les mappings runtime vers `DocumentNodeId` sont autorisés, mais uniquement comme index de preview, pas comme source métier.

## 7. Décisions de v1

Décisions retenues :
- le document model est la source de vérité unique
- le XAML MGUI est le format principal de persistance
- la preview runtime est reconstruite intégralement en v1
- la session d'édition centralise dirty state, sélection, document courant et preview

Décisions reportées :
- mises à jour incrémentales de preview
- support complet des bindings, styles et resources avancés
- édition visuelle avancée avec drag and drop complet et resize intelligent
- composants réutilisables et templates authoring avancés
