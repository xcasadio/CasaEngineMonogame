---
name: adr
description: "Enregistrer une décision d'architecture de CasaEngine en Architecture Decision Record dans docs/decisions/ : modèle, numérotation, index, source citée."
---

# Skill : adr

## Quand l'utiliser

Dès qu'une décision d'architecture, de format d'asset, d'API publique, de backend ou de règle de travail des agents est prise, pendant un plan ou une discussion avec l'auteur (`AGENTS.md` §10). Aussi pour recopier en ADR une décision qui vit déjà dans un document ou un audit.

## Règle absolue : ne rien inventer

Le contexte et la décision viennent de la conversation avec l'auteur, d'un document du dépôt, ou du code (vérifié par `rg`/`fd`). La rubrique `Source` cite toujours l'origine (`fichier:ligne`, ou le plan ou la discussion). Un audit de `ai-agent/audits/` est en lecture seule : on le cite, on ne le modifie pas. En cas de doute sur ce qui a été décidé, poser la question avant d'écrire.

## Procédure

1. Lire `docs/decisions/README.md` (règles, index) et `docs/decisions/template.md`.
2. Prendre le numéro suivant de l'index (quatre chiffres, croissant) et nommer le fichier `docs/decisions/NNNN-short-title.md` (titre court en anglais, mots séparés par des tirets).
3. Remplir le modèle **en anglais** : `Status` (`Proposed` tant que l'auteur n'a pas validé, `Accepted` ensuite), `Date`, `Source`, `Context` (faits seulement), `Decision`, `Consequences`. Plusieurs décisions prises ensemble sur la même thématique peuvent partager un fichier, listées en puces dans `Decision`.
4. Ajouter la ligne `| ADR-NNNN | <title> | <status> | <date> |` au tableau de `docs/decisions/README.md`.
5. Une décision qui en remplace une autre est un nouveau fichier ; l'ancien passe en `Superseded by ADR-NNNN`. On ne réécrit jamais une ADR.
6. Quand la décision vient d'un document de `docs/`, ajouter dans ce document une ligne « Decisions: see ADR-NNNN » (pas dans un audit).
7. Commit dédié, message en anglais `docs(adr): …` (`AGENTS.md` §4).
