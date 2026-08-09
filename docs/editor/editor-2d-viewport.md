# Viewport 2D de l'éditeur

Le viewport monde de l'éditeur peut basculer entre une vue 3D perspective (caméra ArcBall, mode
historique) et une vue 2D orthographique. Le mode est une propriété **du viewport**, pas de la
scène : voir [rendering-2d-3d-spaces.md](../engine/rendering-2d-3d-spaces.md) pour la règle de
projection côté moteur.

## Bascule 2D / 3D

Bouton à cocher **« 2D »** dans la barre d'outils du viewport (`WorldViewportPanel`,
`BuildGizmoToolbar`), après un séparateur, à la suite des boutons de gizmo et des boutons
World/Local. Il n'est donc visible que là où la barre de gizmo est active, c'est-à-dire le viewport
monde de l'éditeur.

Le panneau possède **deux caméras et deux contrôleurs indépendants** :

```text
3D : ArcBallCameraComponent  + EditorViewportCameraController      (inchangés)
2D : Camera2dComponent       + EditorViewport2dCameraController
```

La caméra 2D est créée à la demande, portée par une entity cachée `EditorViewport2dCamera`
(parallèle à `EditorViewportCamera`). Comme chaque contrôleur conserve son propre état, **rien n'est
perdu à la bascule** : aller-retour 2D → 3D → 2D restitue exactement les deux cadrages.

Le mode effectif est donné par un prédicat unique : `UsesCamera2d = _is2dViewMode && !HasWorldOverride`.
Une préview est une vue de type runtime et rend **toujours** par la caméra perspective, quel que soit
le mode du panneau ; basculer pendant une préview est mémorisé et appliqué à la sortie.

## Navigation

- **Pan** : drag au clic milieu. Le delta n'est appliqué qu'à partir de la deuxième frame du drag,
  pour éviter un saut au clic.
- **Zoom** : molette, par crans entiers, centré sur le curseur (le point monde sous le curseur reste
  sous le curseur). `ZoomFromStep` donne ×1, ×2, ×3… pour les crans positifs et ½, ⅓, ¼… pour les
  crans négatifs ; les crans sont bornés à `[-7, 31]`, soit 1/8 → ×32.

## `PixelSnap` (opt-in, défaut `false`)

Le contrôleur 2D expose `PixelSnap`, qui pilote `Camera2dComponent.PixelSnap`. Il n'a **pas de
bouton** dans l'UI : il se règle par code ou via le champ `pixel_snap` de l'état persisté.

Le défaut est `false`, volontairement. Avec le snap actif, la caméra est quantifiée sur la grille
texel et le zoom centré curseur **dérive** : la dérive est bornée (~0.5 × zoomAprès / zoomAvant +
0.5 pixel écran par changement) mais visible en navigation. C'est le compromis à trancher :

| `PixelSnap` | Pour | Contre |
| --- | --- | --- |
| `false` (défaut) | navigation exacte, zoom curseur invariant | l'image n'est pas alignée sur la grille texel |
| `true` | prévisualise le rendu pixel-perfect du jeu | le point sous le curseur dérive à chaque zoom |

Règle pratique : `true` pour vérifier un rendu pixel-perfect, `false` pour éditer.

## Grille 2D

En mode 2D, `GridComponent.DrawForView2d` remplace la grille 3D (`DrawForView`, inchangée). Grille
dans le plan XY graduée en tuiles (32 px par défaut), une ligne accentuée toutes les 8 tuiles, axes
X rouge / Y vert, même étendue que la grille 3D (±50 tuiles, soit ±1600 px). Le tableau de sommets
est construit une seule fois et reconstruit uniquement si la taille de tuile change : aucune
allocation par frame.

## Gizmo contraint XY

En mode 2D, `Gizmo.ConstrainToXYPlane` est activé (propriété opt-in, défaut `false`, donc le
comportement 3D est strictement inchangé). `SelectAxis` ignore alors l'axe Z, la sphère Z et les
plans ZX/YZ : seules les poignées X, Y et XY restent sélectionnables, toute manipulation reste dans
le plan XY.

> **Limitation connue** — `GizmoTool` dimensionne le gizmo sur la distance caméra
> (`_screenScale = |cameraPos − gizmoPos| / SCREEN_SCALE_FACTOR`). En orthographique cette distance
> est constante (500), le gizmo a donc une taille **monde** fixe et sa taille **écran** varie avec le
> zoom au lieu de rester constante. Corriger cela demanderait de modifier la logique d'échelle de
> `GizmoTool`.

## Persistance de l'état de vue

L'état est stocké dans un fichier compagnon `viewport.editor.json`, dans le même dossier
`.casaeditor/` du projet que `layout.editor.json`.

Contenu (`EditorViewportViewStateSerializer`) :

```json
{
  "world_viewport": {
    "is_2d_view_mode": true,
    "target_x": 480.0,
    "target_y": 176.0,
    "target_z": 0.0,
    "zoom_step": 1,
    "pixel_snap": false
  }
}
```

Écriture :

- à la fermeture de l'éditeur (`GameEditor.Dispose(bool disposing)`, en premier dans le travail
  d'arrêt) — **seul le fichier compagnon est écrit**, le layout de docking garde sa sauvegarde
  explicite par commande ;
- par la commande **Save Layout** (`SavePersistedDockLayout`).

Lecture : au chargement du layout persisté (chargement de projet, commande Load layout). Le panneau
de viewport étant créé paresseusement, un état lu avant sa création est mémorisé puis appliqué à la
création. Toute erreur d'E/S ou de parsing est journalisée en warning et n'empêche jamais le
chargement du layout ; sans projet chargé, la sauvegarde est un no-op.

L'état de la caméra ArcBall 3D n'est volontairement pas persisté (il ne l'était pas non plus avant :
`SaveLayoutToJson` ne sérialise que l'arbre de docking).
