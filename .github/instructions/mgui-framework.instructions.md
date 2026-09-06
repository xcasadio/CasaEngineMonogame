---
name: mgui-framework
description: Règles propres au framework UI MGUI (layout, input, clipping, rendu).
applyTo: "MGUI/**"
---

# Instructions — Framework UI MGUI

`MGUI/` est un sous-module git : une modification s'y commite dans le sous-module, selon ses propres règles. Ce fichier s'applique quand on édite ces fichiers depuis ce dépôt. Les règles générales (chemins chauds, état GPU, API publique) sont dans `AGENTS.md`.

## Layout

- Toute propriété qui affecte la taille ou la position invalide le layout : largeur et hauteur, marges et padding, visibilité, police, texte, collection d'enfants, docking et alignement, tailles min et max, contenu.
- Le layout est déterministe.
- Ne pas reconstruire l'arbre visuel à chaque frame, ne pas recalculer un layout inchangé à chaque frame, aucun effet de bord caché dans `Draw`.
- Mettre en cache la mesure du texte ; l'invalider sur changement de police, de texte ou de largeur.

## Input

- L'input est déterministe.
- Le hit-test respecte le z-order, la visibilité, l'état activé et le clipping.
- Capture de la souris pour toute opération de drag.
- Le focus clavier est unique ; navigation Tab quand elle s'applique ; un contrôle ne vole pas le focus de façon inattendue.
- Propagation des événements : respecter la convention existante du framework.

## Clipping

- Sémantique Push/Pop ; toujours restaurer l'état de clipping précédent et l'état du `GraphicsDevice` (états et scissor rect).
- Scissor par défaut pour un clip rectangulaire ; stencil ou masque seulement pour un clip complexe ou arrondi.

## Rendu

- Limiter les `SpriteBatch.Begin` / `End` ; batcher les draw calls ; éviter les changements d'état et de texture redondants.

## Contraintes temps réel de l'éditeur

Les contrôles peuvent être rafraîchis à chaque frame : aucune allocation en layout, input ou draw ; mesures en cache ; pas de nouvelle commande ni de nouvel événement par frame ; pas de recréation des contrôles enfants par frame sauf demande explicite.

## Contrôles

- Contrôles standards et extensibles ; toute nouvelle feature est démontrée dans un sample MGUI.
