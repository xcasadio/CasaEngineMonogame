# Skill: mgui-control-scaffold

## But
Créer un nouveau control MGUI propre (layout + input + draw).

## Output
- Un control (ex: ButtonLike, Panel, ScrollViewer, DockHost…)
- Optionnel : sample de démonstration

## Checklist
- Props publiques + invalidation layout
- Hit-test correct (bounds + clip)
- Input : hover/press/click + capture si drag
- Draw : batching + clipping stack si besoin
- Aucun `new` par frame

## Étapes
1) Identifier classe base (Control/Element)
2) Définir API minimale (props/events)
3) Implémenter layout (measure/arrange ou équivalent)
4) Implémenter input
5) Implémenter draw (background/children/foreground)
6) Ajouter sample + doc

## Done
- Control utilisable + sample + build OK.
