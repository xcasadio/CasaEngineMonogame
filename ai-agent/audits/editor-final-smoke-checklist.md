# Checklist finale de smoke tests editeur

## Objectif

Fermer proprement les deux validations editeur encore ouvertes :

- `LIGHTVIS-006` dans `ai-agent/tasks/archive/light-component-editor-visualization-tasks.md`
- `Valider le flux d'edition material` dans `ai-agent/tasks/archive/material-inspector-stability-plan.md`

Cette checklist est volontairement manuelle et courte. Elle doit etre executee sur un workspace propre, sans introduire de modifications persistantes aux worlds utilises pour le test.

## Preconditions

1. Build editeur OK :

```powershell
dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore
```

2. Projet de reference : `Projects/SampleProject/SampleProject.json`
3. Asset material de reference : `Projects/SampleProject/NewLitDiffuseMaterial.material`
4. World de reference : `Projects/SampleProject/DefaultWorld.world`
5. Si vous ajoutez des lumieres temporaires dans le world de smoke, ne pas sauvegarder ces changements a la fin.

## Trace d'execution

Renseigner ce tableau pendant le smoke :

| Item | Resultat | Notes |
|---|---|---|
| Date / testeur |  |  |
| Validation 1 - Inspecteur material |  |  |
| Validation 2 - Overlays LightComponent |  |  |
| Anomalies / blocages |  |  |

## Validation 1 - Inspecteur material

But : fermer la tache `Valider le flux d'edition material` sans melanger ce smoke avec un rework plus large de l'editeur.

1. Ouvrir `Projects/SampleProject/SampleProject.json` dans l'editeur.
2. Ouvrir `Projects/SampleProject/NewLitDiffuseMaterial.material`.
3. Verifier que le panneau inspector material s'affiche correctement et que le preview material est visible.
4. Modifier successivement au moins un champ de chaque famille presente sur ce material :
   - une valeur numerique (`SpecularPower` ou equivalent),
   - une couleur (`DiffuseColor`, `Tint` ou equivalent),
   - un asset texture (`Albedo`, `NormalMap` ou equivalent),
   - une propriete structurelle si exposee (`Queue`, transparence, override, etc.).
5. Pendant ces edits, verifier :
   - pas de rebuild complet visible du panneau,
   - pas de flicker important,
   - pas de perte de focus immediate sur le champ en cours,
   - pas de reset de scroll intempestif.
6. Verifier que le preview material reage avant meme le save.
7. Sauvegarder le material.
8. Verifier apres save :
   - pas de reload complet du panneau depuis le disque,
   - pas de reset brutal des rows,
   - preview toujours coherent.
9. Fermer puis rouvrir le material.
10. Verifier que les valeurs persistent et que le preview recharge correctement.

Resultat attendu :

- l'inspector reste stable pendant l'edition,
- le preview se met a jour avant et apres save,
- aucun symptome visible de reload global du panneau n'apparait,
- aucun blocage nouveau n'apparait cote chargement ou preview.

## Validation 2 - Overlays LightComponent

But : fermer `LIGHTVIS-006` avec un smoke qui couvre a la fois les billboards et les helpers selectionnes.

Base de test recommandee : `Projects/SampleProject/DefaultWorld.world`.

1. Ouvrir le world `DefaultWorld.world`.
2. Verifier que les billboards existants des lights deja presentes sont visibles.
3. Selectionner `light1` puis `light2` et verifier le helper point light :
   - billboard ampoule,
   - sphere filaire de range,
   - mise a jour correcte du gizmo et absence de conflit avec la grille et l'axe.
4. Ajouter temporairement une entite avec un `LightComponent` de type `Spot`.
5. Ajouter temporairement une entite avec un `LightComponent` de type `Directional`.
6. Verifier le mapping des billboards :
   - `Point` -> ampoule,
   - `Spot` -> cone,
   - `Directional` -> soleil.
7. Selectionner l'entite de type `Spot` et verifier le cone filaire oriente.
8. Selectionner directement le `LightComponent` spot dans l'inspector ou la hierarchy si disponible, et verifier que le helper suit bien le composant selectionne.
9. Selectionner l'entite de type `Directional` et verifier les trois fleches paralleles orientees.
10. Modifier a chaud `Range`, `OuterConeAngleDegrees`, position et rotation ; verifier que les overlays suivent sans redemarrage du viewport.
11. Selectionner ensuite un composant non-light ; verifier que :
   - les billboards restent visibles,
   - aucun helper parasite ne reste selectionne,
   - le gizmo transform, la grille, l'axe et les panels MGUI restent fonctionnels.
12. Fermer le world sans sauvegarder les lumieres temporaires si elles ont ete ajoutees uniquement pour le smoke.

Resultat attendu :

- les trois types de billboards sont corrects,
- les trois helpers selectionnes sont corrects,
- la selection d'entite et la selection de composant light sont toutes deux prises en charge,
- aucun conflit visuel ou d'etat GPU n'apparait dans le viewport editeur.

## Cloture documentaire

Une fois le smoke termine :

1. Reporter le resultat dans `ai-agent/tasks/archive/light-component-editor-visualization-tasks.md` sous `LIGHTVIS-006`.
2. Reporter le resultat dans `ai-agent/tasks/archive/material-inspector-stability-plan.md` sur la tache `Valider le flux d'edition material`.
3. Si un blocage apparait, noter precisement : condition d'apparition, asset/world concerne, symptome visible, et si le build editeur restait vert.