# Editor Input Routing Validation

Validation ciblee pour la refonte du routage d'input entre le runtime moteur, les viewports editeur et MGUI.

## Builds bornes

Executer apres chaque changement touchant le routage, les controleurs runtime ou l'hote viewport.

- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`
- `dotnet build CasaEngine.SimpleEditor/CasaEngine.SimpleEditor.csproj -nologo`

## Verifications manuelles prioritaires

### 1. Focus et activation de vue
- Ouvrir `CasaEngine.SimpleEditor`.
- Cliquer une fois dans le viewport.
- Verifier que la vue devient active et que la navigation clavier agit sur cette vue.
- Cliquer hors viewport sur un element UI MGUI.
- Verifier que le viewport ne recupere pas le focus sans action explicite.

### 2. Capture d'input
- Dans le viewport, maintenir le bouton milieu pour naviguer.
- Deplacer le curseur en dehors du viewport pendant le drag.
- Verifier que la capture reste attachee a la vue tant que le drag est actif.
- Relacher le bouton milieu.
- Verifier que la capture est relachee immediatement.

### 3. Molette
- Survoler le viewport et utiliser la molette.
- Verifier que le zoom camera reagit sans clic prealable.
- Repeter apres interaction avec un element UI MGUI hors viewport.
- Verifier que la molette du viewport continue de fonctionner via la source fenetre partagee.

### 4. Coordonnees locales
- Utiliser le gizmo sur un objet selectionnable proche d'un bord de viewport.
- Verifier que le picking et le drag utilisent les coordonnees locales de la vue, sans decalage.
- Repeter apres resize de la fenetre.

### 5. Modalite UI
- Ouvrir un ecran UI modal dans une vue MGUI si disponible.
- Verifier que l'input gameplay/editeur sous-jacent n'est pas consomme tant que la modalite est active.
- Fermer la modalite.
- Verifier que le routage revient a la vue active ou a la vue sous le pointeur.

## Notes

- Aucun projet de tests moteur ciblant `InputRouter` n'est present dans la solution courante.
- La strategie retenue est donc : builds bornes + scenarios manuels explicites sur les regressions historiques (focus, capture, molette, coordonnees locales, modalite).
