# Plan agent IA — <Sujet>

<!--
Modèle canonique des plans d'agent IA (ai-agent/plan-template.md).
Copier ce fichier dans ai-agent/tasks/<sujet>-tasks.md, remplacer les <…>, supprimer ce commentaire,
puis ajouter le plan au tableau de ai-agent/README.md.
Règles générales : AGENTS.md à la racine du dépôt.
-->

Plan d'exécution du chantier décrit dans [<analyse>.md](../audits/<analyse>.md).
Les décisions D1 → Dn ci-dessous ont été arbitrées avec l'auteur le <date> : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

<Ce que le chantier livre, en quelques phrases. Ce qu'il ne livre pas est dans « Hors périmètre ».>

## État vérifié du dépôt (<date>)

- <Fait vérifié dans le code ou les fichiers, avec la commande utilisée ou le fichier:ligne.>
- <Changements préexistants de l'auteur dans l'arbre de travail : les nommer, ne jamais les indexer.>

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | <Décision arbitrée avec l'auteur.> |

## Règles d'exécution pour l'agent

- **Branche dédiée `<nom-de-branche>`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché (`dotnet build CasaEngine.MonoGame.sln` ou `dotnet build CasaEngine.Editor.MonoGame.sln` selon le périmètre) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` dès qu'une tâche touche du code testé. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; restaurer tout état GPU modifié ; le runtime ne dépend pas de l'éditeur ; sérialisation additive (détail dans `AGENTS.md` et les règles par chemin).

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- <Commande de build et résultat attendu.>
- <Commande de tests et résultat attendu.>
- <Smoke manuel : démo ou écran à lancer, ce qu'il faut observer.>

---

## Phase 0 — <Titre de la phase>

### ⏳ T0.1 — <Titre de la tâche>

- Objectif : <ce que la tâche produit>.
- Fichiers : <chemins créés ou modifiés> (`Sources :` à la place de `Fichiers :` pour une tâche de rétro-remplissage ou d'analyse).
- Étapes :
  1. <étape concrète>.
  2. <étape concrète>.
- Validation : <commande ou observation vérifiable ; résultat attendu>.
- Commit : `type(area): summary`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | <question ou point à confirmer> | <T…> |

## Hors périmètre

- <Ce que le chantier ne fait volontairement pas.>
