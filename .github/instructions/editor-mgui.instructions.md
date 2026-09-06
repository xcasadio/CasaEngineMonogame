---
name: editor-mgui
description: Règles propres à l'éditeur CasaEngine (UI MGUI, services d'édition, gizmos).
applyTo: "{CasaEngine.Editor/**,CasaEngine.EditorServices/**,GizmoTool/**}"
---

# Instructions — Éditeur (MGUI)

Les règles générales sont dans `AGENTS.md` (séparation runtime/éditeur §9.9, MGUI §9.5). Ce fichier n'ajoute que ce qui est propre à l'éditeur.

## Objectifs

- L'UI de l'éditeur est écrite avec MGUI (UI runtime), sans autre toolkit.
- Multi-vues : viewport(s), inspector, hierarchy, console, asset browser.
- UX de type « éditeur de moteur » : docking, split panes, onglets, layout persisté.

## Répartition

- La logique d'édition (création, sauvegarde, export d'assets) va dans `CasaEngine.EditorServices` ; l'UI dans `CasaEngine.Editor` ; rien de tout cela dans `CasaEngine` (runtime).

## Fonctionnalités récurrentes

- Undo/redo transactionnel.
- Gizmos (move, rotate, scale) et snapping.
- Sélection et picking.
- Docking : calcul de layout pur, persistance en JSON.
- Palette de commandes et raccourcis.

## Validation propre à l'éditeur

- Le calcul de layout du docking (pur) est couvert par des tests.
- Toute feature visible se vérifie dans l'éditeur lancé, ou dans un écran testable.
