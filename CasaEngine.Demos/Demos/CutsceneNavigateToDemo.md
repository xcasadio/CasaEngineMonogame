# Cutscene NavigateTo Demo

Validation visuelle attendue : lancer la demo `Cutscene NavigateTo demo`, verifier que le cube rouge part du marqueur bleu et rejoint le marqueur jaune via l'asset `Content/Cutscenes/navigate_to_grid.cutscene`.

La demo utilise une vraie `NavigationGrid2D` injectee dans le `NavigationAgentComponent` du cube. Le mouvement doit donc passer par l'action cutscene `NavigateTo`, l'agent de navigation et le driver `CharacterControllerNavigationDriverComponent`.

Commandes : `Space` relance la cutscene depuis le debut, `S` stoppe la cutscene en cours, `R` replace l'acteur au depart.

La validation finale reste utilisateur : verifier navigation visible, stop manuel, retour au mode de controle joueur, puis relance sans etat residuel.