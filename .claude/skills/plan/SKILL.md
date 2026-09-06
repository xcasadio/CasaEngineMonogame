---
name: plan
description: "Écrire, faire approuver et exécuter un plan d'agent IA CasaEngine : questions groupées, modèle ai-agent/plan-template.md, statuts à icônes, un commit par tâche, arrêt sur blocage."
---

# Skill : plan

## Quand l'utiliser

Dès que le travail demande **plus d'un commit** (`AGENTS.md` §3). En dessous, exécution directe avec le rapport de fin de tâche.

## Règle absolue : ne rien inventer, ne rien supposer, demander

Toute règle, tout fait, toute API, tout fichier cité dans le plan provient d'un fichier du dépôt (vérifié par `rg`, `fd` ou lecture), d'une réponse de l'auteur, ou d'une documentation officielle citée avec son URL. Sinon : ne pas écrire, poser la question. Pendant l'exécution, un blocage se traite par ⚠️ Blocked, une question dans « Points ouverts », et un **arrêt** : jamais de contournement, jamais de supposition.

## Procédure

1. **Questions groupées, avant le plan.** Réunir toutes les questions dont la réponse change le travail et les poser en une seule fois à l'auteur. Attendre les réponses.
2. **Lire le modèle** `ai-agent/plan-template.md` et le suivre à la lettre : sections, tableau des décisions, règles d'exécution, légende des cinq statuts, gabarit de tâche (`Objectif`, `Fichiers` ou `Sources`, `Étapes`, `Validation`, `Commit`), identifiants `T<phase>.<numéro>`.
3. **Écrire le plan** dans `ai-agent/tasks/<sujet>-tasks.md`, en français. Les réponses de l'auteur deviennent des décisions verrouillées `D1 → Dn` ; les arbitrages proposés par l'agent deviennent des points à valider `P1 → Pn`. La section « État vérifié du dépôt » ne contient que des faits vérifiés, avec la commande ou le fichier:ligne.
4. **L'ajouter au tableau** de `ai-agent/README.md` (fichier, sujet, reste à faire).
5. **Soumettre le plan à l'auteur et attendre son approbation explicite.** Aucune modification de code avant.
6. **Exécuter tâche par tâche** sur la branche dédiée du chantier (jamais `main`) :
   - passer la tâche en 🚧 avant de commencer ;
   - à la fin, lancer la validation indiquée, passer en ✅ (ou 🧪 si une validation manuelle manque), écrire une courte note de validation sous la tâche ;
   - créer **un commit dédié** qui inclut la mise à jour du plan, message en anglais `type(area): summary` ;
   - une seule tâche à la fois ; jamais de 🚧 en fin de session ; jamais de push.
7. **Clôturer** : validation globale, rapport de fin (`AGENTS.md` §12), tableau du README mis à jour ; un plan terminé passe dans `ai-agent/tasks/archive/`.

## Légende des statuts

⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked (définitions dans `ai-agent/plan-template.md`).
