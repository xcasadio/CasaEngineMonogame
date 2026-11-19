# Recette de l'auteur — Éditeur de cutscenes visuel (tranches A à G)

Liste des gestes à essayer dans l'éditeur lancé, tranche par tranche. Elle couvre ce qu'aucun agent n'a pu vérifier : **les clics, le glisser, le clavier et le son réels**. Tout le reste a été vérifié par des tests et par des lancements automatisés, sans entrée envoyée à l'éditeur.

Chaque ligne se coche ; en cas d'écart, noter ce qui s'est passé à côté. Les limites connues sont listées à la fin : elles ne sont pas des défauts à signaler.

Plans concernés : [preview](cutscene-editor-preview-tasks.md) (A), [outliner-details](cutscene-editor-outliner-details-tasks.md) (B), [timeline](cutscene-editor-timeline-tasks.md) (C), [transport](cutscene-editor-transport-tasks.md) (D), [structure](cutscene-editor-structure-tasks.md) (E), [placement](cutscene-editor-placement-tasks.md) (F), [fidelity](cutscene-editor-fidelity-tasks.md) (G). Documentation d'usage : `docs/editor/cutscene-editor.md`.

## Préparation

- Branche `chantier/cutscene-property-editing`, tête `80c70fcc` ou plus récente ; `dotnet build CasaEngine.Editor.MonoGame.sln`.
- Ouvrir le projet `Projects/SpriteCutsceneSample/SpriteCutsceneSample.json` dans l'éditeur. Cutscenes utiles : `Cutscenes/loop.cutscene` (boucle sans fin, `ExitOnInput`), `Cutscenes/exit.cutscene` (fondu puis changement de monde), `Cutscenes/stagger.cutscene` (quatre `At` dans un `Parallel`).
- **Attention à l'enregistrement** : un Ctrl+S réécrit le fichier du sample dans sa forme canonique. Pour une recette sans trace, ne pas enregistrer, ou rétablir ensuite par `git checkout -- Projects/SpriteCutsceneSample`.
- Pour le son : il faut une cutscene d'un de vos projets qui joue un `PlaySound`, un `PlayMusic` ou un `FadeMusic`. Aucun sample du dépôt n'en a.

## A — Aperçu du monde

- [ ] Ouvrir `loop.cutscene` depuis le Content Browser : l'onglet montre un viewport avec le monde d'aperçu, et la liste « Preview world » affiche `SpriteWorld.world`.
- [ ] `Play` : les deux orbes et les deux anneaux apparaissent et se déplacent ; la ligne d'état dit « Skipped: ExitOnInput -> SpriteExit ».
- [ ] Choisir `EndWorld.world` dans « Preview world », puis revenir à `SpriteWorld.world` : le viewport suit. Fermer et rouvrir le document : le dernier monde choisi est retenu.
- [ ] `exit.cutscene`, `Play` : la lecture se termine seule et la ligne d'état dit « Skipped: ChangeWorld -> EndWorld.world ». Le monde de l'éditeur n'a pas changé.
- [ ] Molette et glisser dans le viewport de l'aperçu : la caméra bouge sans rien casser.

## B — Arbre des actions et détails typés

- [ ] Cliquer une action dans **Hierarchy** : **Inspector** montre son type, son chemin et un champ par propriété.
- [ ] Champ numérique : taper une valeur puis Entrée ; boutons `-` et `+` ; bouton `x` s'il y en a un. Une valeur refusée remet l'ancienne et dit pourquoi sous le champ.
- [ ] Liste « Entity » d'un `MoveSprite` : elle propose les entités du monde d'aperçu.
- [ ] Taper dans un champ **sans valider**, puis cliquer une autre action dans l'arbre : la valeur tapée est bien prise, pour l'action d'origine.
- [ ] Ctrl+Z puis Ctrl+Y (ou Ctrl+Maj+Z) : chaque modification s'annule et se rétablit ; l'onglet porte `*` quand le document est modifié et le perd quand on revient à l'état enregistré.
- [ ] Liste des types + `Add` ; `Duplicate` ; `Delete` : l'arbre et le compteur d'actions suivent, et chaque opération s'annule.
- [ ] Sélectionner une action tout en bas de l'arbre, puis une tout en haut : la ligne sélectionnée reste visible à chaque fois.
- [ ] Ouvrir deux cutscenes, modifier l'une, passer à l'onglet de l'autre, Ctrl+Z : l'annulation ne touche que le document actif.

## C — Timeline

- [ ] À l'ouverture d'une cutscene, l'onglet **Cutscene Timeline** passe au premier plan en bas. `loop.cutscene` y montre sept rangées sans barre de défilement : Loops, Flow, une par entité, World and input.
- [ ] Cliquer une barre ou un losange : la même action est sélectionnée dans l'arbre et dans l'Inspector. Et dans l'autre sens.
- [ ] Molette sur la règle : le zoom change autour du curseur. Redimensionner le panneau, le détacher, le rattacher.
- [ ] `Play` : la tête de lecture avance ; les barres `MoveSprite`, en pointillé au repos (estimées), deviennent pleines (mesurées) ; sur la boucle sans fin, la tête revient au début du tour.
- [ ] Survoler une barre : l'infobulle donne le début et la durée.

## D — Pause, pas à pas, tête de lecture

- [ ] Pendant une lecture, `Pause` : l'image se fige, le bouton devient `Play`, le texte dit « Paused ». `Play` : la lecture **reprend au même endroit**.
- [ ] `+1 frame` plusieurs fois : le compteur d'images monte de un en un. `-1 frame` : il redescend de un.
- [ ] Clic dans la règle de la timeline : l'aperçu va à cet instant et reste en pause.
- [ ] **Glisser** dans la règle, bouton enfoncé : la tête de lecture suit en continu, vers l'avant comme vers l'arrière.
- [ ] Clic à 0 : les quatre sprites sont visibles à leur position de départ, « 0.00 s · 0 frames ».
- [ ] `Stop` : l'aperçu revient à l'état d'avant la cutscene (sprites cachés), tête à 0.
- [ ] `exit.cutscene` jouée jusqu'au bout : le texte dit « Ended » et la tête reste à la fin, vers 1 s.
- [ ] En pause au milieu d'un déplacement, changer la vitesse d'un `MoveSprite` dans l'Inspector : l'aperçu est rejoué jusqu'au même instant et montre le sprite ailleurs.
- [ ] Déplacer la caméra de l'aperçu, puis cliquer ailleurs dans la règle : la caméra garde sa place.

## E — Édition de la structure à la souris

- [ ] **Glisser une ligne de l'arbre** vers le haut d'une autre ligne : un trait bleu avec un petit carré montre « avant » ; vers le bas : « après » ; sur le milieu d'un conteneur (`Repeat`, `Sequence`, `Parallel`) : la ligne s'encadre, « dans ».
- [ ] Relâcher : l'action a changé de place et reste sélectionnée, même si elle ne l'était pas avant. Ctrl+Z la remet.
- [ ] Glisser un conteneur sur un de ses propres enfants : aucun repère, rien ne se passe au relâchement.
- [ ] Relâcher hors de l'arbre, ou appuyer sur Échap pendant le glisser : rien ne bouge.
- [ ] Un simple clic sur une ligne (sans bouger) sélectionne toujours, sans rien déplacer.
- [ ] **Clic droit** sur une action : menu `Add` (catégorie, puis type), `Wrap in`, `Ungroup` (sur un conteneur), `Move up`, `Move down`, `Cut`, `Copy`, `Paste`, `Duplicate`, `Delete`. Le menu se ferme en cliquant ailleurs.
- [ ] `Wrap in ▸ Parallel` sur un `MoveSprite`, puis glisser les trois autres dedans : un `Parallel` de quatre actions, déplié.
- [ ] `Ungroup` sur ce `Parallel` : les quatre actions reprennent leur place.
- [ ] Clic droit sur une **barre de la timeline** : le même menu, pour l'action de cette barre.
- [ ] Ctrl+C sur une action, sélectionner une autre action, Ctrl+V ; Ctrl+X ; Ctrl+D. Vérifier qu'un seul collage a lieu par Ctrl+V.
- [ ] Copier dans une cutscene, coller dans une autre.
- [ ] **Suppr** avec le focus dans l'arbre : l'action sélectionnée est supprimée. Suppr **pendant qu'on tape dans un champ de l'Inspector** : seul le texte est touché, aucune action n'est supprimée.

## F — Placement libre dans le temps

- [ ] Ouvrir `stagger.cutscene` : l'arbre montre quatre nœuds « At … s » dans un `Parallel` ; la timeline montre quatre barres décalées de 0,4 s, chacune avec un bord gauche clair.
- [ ] **Glisser une de ces barres** vers la droite : elle suit le curseur **sans sauter**, même saisie par son milieu. Relâcher : le nœud « At » de l'arbre prend le nouvel instant ; Ctrl+Z le remet.
- [ ] Glisser une barre vers la gauche au-delà de 0 : elle s'arrête au début de son bloc.
- [ ] Glisser une barre plus loin que la fin actuelle : possible, la timeline s'allonge.
- [ ] Cliquer une barre qui se glisse, sans vouloir la bouger : elle est sélectionnée et **ne change pas d'instant**.
- [ ] Dans `loop.cutscene`, essayer de glisser un `TintSprite` : rien ne bouge (une action enchaînée dans une séquence ne se glisse pas).
- [ ] Dans `loop.cutscene`, regrouper deux `MoveSprite` dans un `Parallel`, puis glisser la barre du second : il est enveloppé dans un « At » au nouvel instant, et un seul Ctrl+Z défait le tout.
- [ ] Sélectionner un nœud « At » dans l'arbre : son champ `Seconds` se règle dans l'Inspector, pour un instant exact.
- [ ] Sélectionner le `Parallel` de `stagger.cutscene`, clic droit dans le vide d'une rangée de la timeline : `Add here` (catégorie, puis type) ajoute une action à l'instant du curseur.
- [ ] `Play` après avoir déplacé un clip : le sprite part bien au nouvel instant.
- [ ] Facultatif, sur une copie : enregistrer une cutscene qui contient un « At », la fermer, la rouvrir : elle revient identique. La forme écrite est `{"type": "At", "seconds": …, "actions": [ … ]}`.

## G — Aperçu plus fidèle

- [ ] `exit.cutscene`, `Play` : l'image de l'aperçu **s'assombrit progressivement jusqu'au noir**. `Stop` : elle redevient normale.
- [ ] Clic au milieu du fondu dans la règle : l'image a l'assombrissement de cet instant. Cliquer plus tôt puis plus tard : l'image suit, sans rester noire.
- [ ] Le voile ne déborde pas du viewport de l'aperçu (ni sur les panneaux voisins, ni sur la timeline).
- [ ] `loop.cutscene`, `Play` : un bouton `Press "exit"` apparaît sous la barre de transport. Le cliquer : la lecture s'arrête, « Ended », et la ligne d'état dit que le jeu jouerait `SpriteExit`. Le bouton disparaît.
- [ ] **Son**, sur une cutscene de votre projet qui en joue :
  - [ ] bouton `Sound: off` à l'ouverture, et aucun son en lecture ;
  - [ ] `Sound: on` puis `Restart` : on entend les sons et la musique ;
  - [ ] `Pause` : le son s'arrête (il ne reprend pas avec `Play`, il faut `Restart`) ;
  - [ ] clic dans la règle et `+1 frame` : aucun son ;
  - [ ] après une cutscene qui baisse la musique (`FadeMusic`), lancer le mode Play de l'éditeur (F5) : la musique du jeu est à son volume normal ;
  - [ ] passer à un autre onglet pendant que le son joue : il s'arrête.
- [ ] `Projects/VideoSpriteSample`, `Cutscenes/loop.cutscene`, `Play` : la vidéo s'affiche et avance dans l'aperçu.

## Arbitrages à confirmer

Chaque plan a une table « Arbitrages de l'agent, à confirmer » (points P). Les plus visibles, à valider ou à faire changer :

- L'arbre des actions est dans **Hierarchy** et les détails dans **Inspector**, pas dans l'onglet du document (plan B).
- La timeline est un panneau ancrable, amené au premier plan à l'ouverture d'une cutscene (plan C).
- `Stop` remet l'aperçu à l'état de départ ; à la fin d'une lecture la tête reste à la fin ; une modification en pause rejoue jusqu'au même instant (plan D).
- Une seule action à la fois, sans sélection multiple ; aucun glisser de structure dans la timeline (plan E).
- Seuls se glissent dans le temps le premier enfant d'un `At` et un enfant direct d'un `Parallel` ; l'instant s'aligne sur 1/60 s (plan F).
- Une entrée simulée signale et arrête, sans jouer la cutscene suivante ; le son est éteint à chaque ouverture et s'arrête au lieu de se mettre en pause (plan G).

## Limites connues, à ne pas signaler

- Animations de sprites, physique et scripts restent figés dans l'aperçu.
- Pas de sélection multiple ; pas d'étirement d'une barre ; pas de changement de rangée ; pas de magnétisme entre éléments ; pas de déplacement au clavier ; pas de raccourci clavier pour le transport.
- Pendant un glisser dans l'arbre : pas de défilement automatique près des bords, et un conteneur replié ne se déplie pas tout seul.
- Chaque retour en arrière de la tête de lecture reconstruit le monde d'aperçu : sur un grand monde, un glisser vers l'arrière peut être lent. **Le mesurer sur un de vos mondes serait utile.**
- Les branches d'un `Parallel` démarrent une image après lui : un clip posé à 0,5 s part une image plus tard dans l'aperçu.
- Au premier instant (0 s), un `FadeScreen` et les actions placées dans un `Parallel` ne sont pas encore visibles.
- Après un déplacement de clip, une barre dont la durée se mesure reprend sa durée estimée jusqu'à la lecture suivante.
- Avec deux documents cutscene ouverts, un aperçu resté en arrière-plan peut couper le son de celui du premier plan.
- Cosmétique : « A ExitOnInput » dans un message de refus ; un chemin long coupé dans l'Inspector ; la ligne d'état dit « Preview: Stopped » quand la barre de transport dit « Ended » après une entrée simulée ou un changement de monde.

## Après la recette

- Les écarts trouvés se notent ici ou se disent à l'agent, qui les corrige sur la même branche.
- La fusion de la branche dans `main` et tout push restent des décisions de l'auteur.
