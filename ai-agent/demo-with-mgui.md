# Demo avec MGUI — Tâches d'intégration

## Objectif

Remplacer la navigation clavier (flèches gauche/droite) par une fenêtre MGUI persistante affichée dans chaque démo.
Cette fenêtre sert de **panneau d'information et de navigation** entre les démos.

---

## Contexte actuel

- La navigation se fait via `DemosGame.Update()` : flèches gauche/droite → `ChangeDemo()`.
- Chaque démo expose `Title` (string). Il n'y a pas de propriété `Description`.
- L'UIRoot MGUI est déjà fonctionnel par vue (`RenderView.UIRoot`), démontré dans `UIOverlayDemo`.
- Le `ScreenStack` + `UIScreenBase` existent et fonctionnent (cf. `HudScreen`, `PauseMenuScreen`).

---

## Spécification fonctionnelle

### Fenêtre MGUI « Demo Info »

1. **Description de la démo** : texte en anglais décrivant la démo courante.
2. **Liste de navigation** : liste cliquable de toutes les démos (affiche le titre de chaque démo). Un clic sur un élément change la démo active.
3. **Touche F1** :
   - Si la fenêtre est visible → la masquer.
   - Si la fenêtre est masquée → la réafficher.
4. **Texte d'aide quand la fenêtre est masquée** : un petit texte overlay en bas de l'écran indiquant `"Press F1 to show demo info"`.
5. **Suppression de la navigation clavier** : enlever la navigation gauche/droite dans `DemosGame.Update()`.

---

## Tâches

### 1 — Ajouter `Description` à la classe `Demo`

- [ ] Ajouter une propriété virtuelle `public virtual string Description => "";` dans `Demo.cs`.
- [ ] Remplir la description (en anglais) pour chaque démo existante :
  - `Collision3dBasicDemo` : "Demonstrates basic 3D collision detection between rigid bodies."
  - `Collision2dBasicDemo` : "Demonstrates basic 2D collision detection with physics."
  - `TileMapDemo` : "Renders a tile map loaded from sprite and animation assets."
  - `SkinnedMeshDemo` : "Displays an animated skinned mesh model."
  - `SceneManagementDemo` : "Shows scene management with a grid of rotating entities."
  - `SplitScreenDemo` : "Demonstrates 2-view split-screen rendering."
  - `RenderToTextureDemo` : "Renders a scene to a texture and displays it on a quad."
  - `ViewManagerSandbox` : "Comprehensive sandbox for the ViewManager v2 system: multi-view, dynamic add/remove, update modes."
  - `UIOverlayDemo` : "Demonstrates the MGUI per-view UI overlay system with HUD and modal pause menu."

### 2 — Créer le `DemoInfoScreen` (UIScreenBase)

- [ ] Créer `CasaEngine.Demos/Demos/DemoUI/DemoInfoScreen.cs` héritant de `UIScreenBase`.
- [ ] La fenêtre contient :
  - Un titre : nom de la démo courante (bold).
  - Un bloc texte : description de la démo.
  - Un séparateur.
  - Une liste scrollable de boutons/labels cliquables, un par démo (texte = `Demo.Title`).
  - La démo courante est mise en surbrillance (couleur différente ou bold).
- [ ] Exposer un callback `Action<int> OnDemoSelected` déclenché au clic sur un item de la liste.
- [ ] Exposer une méthode `UpdateCurrentDemo(int index, string title, string description)` pour rafraîchir le contenu sans recréer la fenêtre.
- [ ] La fenêtre doit être positionnée en haut à droite, taille raisonnable (~300×400), semi-transparente.

### 3 — Créer le `DemoHintOverlay` (texte "Press F1")

- [ ] Créer `CasaEngine.Demos/Demos/DemoUI/DemoHintOverlay.cs` héritant de `UIScreenBase`.
- [ ] Affiche un simple texte centré en bas de l'écran : `"Press F1 to show demo info"`.
- [ ] Style discret : texte blanc, fond semi-transparent, petite taille.

### 4 — Intégrer dans `DemosGame`

- [ ] Supprimer la navigation par flèches gauche/droite dans `DemosGame.Update()`.
- [ ] Dans `DemosGame`, après le `LoadContentPrivate` (quand l'UIRoot est disponible) :
  - Créer et pousser `DemoInfoScreen` sur le ScreenStack.
  - Passer la liste des démos (titres) au `DemoInfoScreen`.
  - Brancher `OnDemoSelected` → `ChangeDemo(index)`.
- [ ] Gérer la touche **F1** dans `DemosGame.Update()` :
  - Si `DemoInfoScreen` est visible → pop `DemoInfoScreen`, push `DemoHintOverlay`.
  - Si `DemoHintOverlay` est visible → pop `DemoHintOverlay`, push `DemoInfoScreen`.
- [ ] Lors d'un `ChangeDemo()`, appeler `DemoInfoScreen.UpdateCurrentDemo()` pour rafraîchir le titre, la description et la surbrillance.

### 5 — Adapter les démos existantes

- [ ] Vérifier que chaque démo qui utilise F1 pour autre chose (ex: `ViewManagerSandbox` utilise F1..F4 pour cycle UpdateMode) n'entre pas en conflit. Si conflit, changer le raccourci de la démo (pas celui de la fenêtre d'info).
- [ ] S'assurer que `UIOverlayDemo` fonctionne toujours correctement avec le `DemoInfoScreen` superposé (le HUD de la démo et le panneau d'info doivent cohabiter).

### 6 — Tests et validation

- [ ] Compiler en Debug.
- [ ] Lancer les démos, vérifier :
  - La fenêtre d'info apparaît au démarrage avec la description de la première démo.
  - Cliquer sur un titre dans la liste change la démo.
  - Le titre et la description se mettent à jour.
  - F1 masque la fenêtre → le texte "Press F1 to show demo info" apparaît.
  - F1 de nouveau → la fenêtre réapparaît.
  - Les flèches gauche/droite ne changent plus de démo.
  - Aucune régression sur les démos existantes.

---

## Notes techniques

- Le `DemoInfoScreen` et le `DemoHintOverlay` sont gérés au niveau `DemosGame` (pas dans chaque démo individuelle), car la navigation est globale.
- On utilise le `UIRoot` de la première vue (`ViewManager.Views[0].UIRoot`) comme cible.
- Le `DemoInfoScreen` vit sur la couche `UILayer.HUD` (ou un layer dédié) pour ne pas bloquer les modals des démos.
- Quand une démo pousse ses propres écrans (ex: `UIOverlayDemo` avec HUD + PauseMenu), ceux-ci se superposent naturellement au-dessus ou en dessous selon leur layer.
