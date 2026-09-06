---
name: mgui-control-scaffold
description: Créer un nouveau contrôle MGUI propre (layout, input, draw), avec sa checklist et un sample de démonstration.
---

# Skill : mgui-control-scaffold

## But

Créer un nouveau contrôle MGUI propre : layout, input, draw.

## Sortie

- Un contrôle (bouton, panneau, scroll viewer, dock host…).
- Optionnel : un sample de démonstration.

## Règles à respecter

Celles de la règle par chemin `mgui-framework` (`.github/instructions/mgui-framework.instructions.md`, jumelle `.claude/rules/mgui-framework.md`) : invalidation du layout sur toute propriété de taille ou de position, hit-test correct (bornes et clip), capture souris pour le drag, clipping en Push/Pop, batching, aucune allocation par frame.

## Étapes

1. Identifier la classe de base (`Control` ou `Element`, selon l'existant dans MGUI).
2. Définir l'API minimale : propriétés, événements.
3. Implémenter le layout (measure et arrange, ou l'équivalent du framework).
4. Implémenter l'input : hover, press, click, capture si drag.
5. Implémenter le draw : fond, enfants, premier plan, clipping si besoin.
6. Ajouter un sample et une doc courte.

## Done

Contrôle utilisable, sample, build OK.
