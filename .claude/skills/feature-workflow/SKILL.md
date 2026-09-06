---
name: feature-workflow
description: Implémenter une feature complète de CasaEngine (éditeur, UI, rendu, physique, sample) en suivant le workflow d'AGENTS.md, du plan à la démo.
---

# Skill : feature-workflow

## Quand l'utiliser

Pour implémenter une feature complète (éditeur, UI, rendu, physique, sample).

## Entrées attendues

- Nom de la feature.
- Dossiers ciblés.
- Comportement attendu et cas limites.
- Comment tester (ou, à défaut, comment le prouver).

## Workflow

1. Poser en une seule fois les questions dont la réponse change le travail (`AGENTS.md` §3). Ne rien inventer.
2. Dès que le travail demande plus d'un commit : écrire le plan avec le skill `plan`, le faire approuver, puis exécuter tâche par tâche avec un commit par tâche (`AGENTS.md` §3 et §4).
3. Scaffold : interfaces, classes, points d'extension.
4. Implémenter par sous-tâches.
5. Ajouter une démo ou un sample si la feature est visible (`AGENTS.md` §6).
6. Build, puis lancement.
7. Doc courte (`AGENTS.md` §10) ; ADR avec le skill `adr` si une décision d'architecture a été prise.

## Definition of Done

Feature testable, build OK, doc à jour, règles moteur d'`AGENTS.md` §9 respectées.
