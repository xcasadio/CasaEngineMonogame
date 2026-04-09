# CasaEngine Folder Hierarchy Audit Tasks

## Objectif

Auditer `CasaEngine/Core`, `CasaEngine/Engine`, `CasaEngine/Framework` pour vérifier :

- la pertinence des noms de dossiers
- les redondances ou dossiers inutiles
- le placement des classes
- la lisibilité du découpage entre couches moteur
- la forme d'une hiérarchie cible plus moderne et plus compréhensible

## Contraintes

- Ne pas refactorer le code pendant cette phase.
- Relier les constats à des dossiers et fichiers réels.
- Produire un rapport exploitable par un futur agent de refactor.

## Statut d'exécution

- ✅ Done — cartographier `Core`, `Engine`, `Framework` et leurs sous-dossiers
- ✅ Done — relever les hotspots de densité de fichiers
- ✅ Done — comparer dossiers et namespaces
- ✅ Done — vérifier les dépendances entre couches
- ✅ Done — vérifier les cas concrets de placement de classes ambigus
- ✅ Done — identifier les dossiers trop larges, trop fins ou historiquement pollués
- ✅ Done — proposer une hiérarchie cible plus moderne
- ✅ Done — formaliser le backlog de refactor pour un prochain agent

## Contrôles effectués

### 1. Cartographie

- inventaire de tous les sous-dossiers sous `CasaEngine/Core`
- inventaire de tous les sous-dossiers sous `CasaEngine/Engine`
- inventaire de tous les sous-dossiers sous `CasaEngine/Framework`
- relevé des fichiers présents directement à la racine des trois couches

### 2. Qualité de nommage

- détection des dossiers aux noms ambigus (`Helpers`, `Game`, `GameFramework`, `Graphics`, `GUI`, `Debugger`, `Graphics2D`)
- détection des noms peu homogènes (`Maths`, `Parser`, `Reinforcement Learning`)

### 3. Placement des classes

- vérification dossier vs namespace
- vérification des classes placées à la racine de `Framework`
- vérification des classes rangées dans des dossiers qui n'expriment pas leur rôle réel

### 4. Réalité du découpage en couches

- recherche de dépendances montantes
- vérification `Core -> Engine -> Framework`
- vérification de l'écart entre hiérarchie physique et dépendances réelles

## Livrables produits

- `docs/architecture/CasaEngine_folder_hierarchy_audit_report.md`
- `ai-agent/casaengine-folder-hierarchy-refactor-tasks.md`

## Résultat

L'audit a été exécuté intégralement. La phase suivante n'est plus une phase d'analyse, mais une phase de refactor pilotée par backlog.