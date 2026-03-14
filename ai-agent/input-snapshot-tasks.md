# Plan d'implementation — Input Snapshot unifie

## Objectif

Mettre en place une architecture d'input basee sur un snapshot fenetre unique par frame, partage entre MGUI, le routage moteur et les viewports editeur.

Le but est de supprimer les decisions d'input dispersees, de clarifier capture/focus/modalite, et de faire converger le runtime in-game et le runtime editeur vers le meme modele.

## Contraintes pour l'agent IA

- Faire un commit atomique apres chaque tache terminee.
- Mettre a jour l'icone de statut dans ce fichier apres chaque tache.
- Ne pas introduire de workaround specifique a `Game1` ou `WorldViewportPanel` si la correction peut vivre a un niveau plus central.
- Preferer une architecture derivee depuis l'etat courant de la frame plutot que des flags mutables qui s'accumulent.
- Valider avec des builds bornes et des verifications manuelles ciblees.

## Legende statut

- ⚪ A faire
- 🟡 En cours
- 🟢 Termine
- 🔴 Bloque

## References

- `ai-agent/editor-input-routing-architecture.md`
- `ai-agent/editor-input-routing-validation.md`
- `CasaEngine/Framework/Input/`
- `CasaEngine/Framework/Rendering/ViewManager.cs`
- `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
- `CasaEngine.Editor/Runtime/EditorViewportCameraController.cs`
- `CasaEngine.Editor/Runtime/EditorViewportGizmoController.cs`
- `MGUI/MGUI.Shared/Input/`
- `MGUI/MGUI.Core/UI/`

---

## 🟢 Tache 1 — Auditer les sources d'input et les consommateurs

**But :** etablir une cartographie precise des lectures d'input brutes, des conversions en contexte de vue, et des consommateurs editor/in-game.

**Travail attendu :**
1. Identifier toutes les lectures de `KeyboardState`, `MouseState`, deltas de molette et positions ecran.
2. Distinguer clairement : acquisition brute, routage, capture, focus clavier, hit-test UI, consommation gameplay, consommation editeur.
3. Documenter les doublons et les endroits ou une meme frame peut etre lue plusieurs fois avec des decisions divergentes.
4. Ajouter ou mettre a jour une note d'architecture courte dans `ai-agent/` si necessaire.

**Livrable :**
- Une cartographie factuelle des flux actuels.
- Une liste des points a migrer vers le snapshot partage.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`

**Commit attendu :** `docs(input): audit raw input flow and consumers`

---

## 🟢 Tache 2 — Introduire le modele WindowInputSnapshot

**But :** creer un objet de snapshot brut unique par frame, independant des consommateurs.

**Travail attendu :**
1. Ajouter un type `WindowInputSnapshot` avec au minimum : clavier, souris, position ecran, wheel vertical, wheel horizontal, timestamp/frame id si utile.
2. Ajouter un producteur unique du snapshot au niveau hote fenetre/runtime partage.
3. Garantir qu'une seule capture brute est produite par frame pour la fenetre active.
4. Ne pas changer encore le comportement metier des controleurs ; uniquement introduire la source canonique.

**Livrable :**
- Nouveau type de snapshot.
- Point d'acquisition unique clairement identifiable.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`
- verifier qu'aucune regression evidente n'apparait au lancement de l'editeur

**Commit attendu :** `feat(input): add shared window input snapshot`

---

## ⚪ Tache 3 — Faire consommer MGUI et le routage moteur depuis le meme snapshot

**But :** eliminer les divergences entre la vue UI et le routage moteur pour une meme frame.

**Travail attendu :**
1. Brancher MGUI sur le snapshot partage ou sur un adaptateur qui lit exclusivement ce snapshot.
2. Brancher `InputRouter` et les providers moteur sur la meme source.
3. Verifier que les deltas de molette et les positions ecran restent coherents entre UI et vue moteur.
4. Supprimer les lectures brutes dupliquees devenues inutiles.

**Livrable :**
- Une source canonique commune a MGUI et au moteur.
- Moins de dependances directes a la fenetre native dans les couches hautes.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`
- test manuel focus viewport + clic UI hors viewport

**Commit attendu :** `refactor(input): unify MGUI and engine raw input source`

---

## ⚪ Tache 4 — Etendre ViewInputContext avec les donnees routees utiles

**But :** faire porter par `ViewInputContext` toutes les donnees necessaires aux consommateurs runtime, sans relecture locale.

**Travail attendu :**
1. Ajouter ou finaliser dans `ViewInputContext` : `ScreenPosition`, `LocalPosition`, deltas de molette, metadonnees de routage si necessaire.
2. Faire calculer ces valeurs par le chemin central de routage.
3. Verifier que les coordonnees locales restent correctes apres resize, docking, changement de viewport et capture.
4. Eviter les conversions locales ad hoc dans les panneaux UI.

**Livrable :**
- `ViewInputContext` suffisant pour les controleurs runtime.
- Conversion ecran -> local centralisee.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`
- test manuel picking/gizmo proche des bords du viewport

**Commit attendu :** `feat(input): route screen local and wheel data through view context`

---

## ⚪ Tache 5 — Migrer les controleurs editeur vers le contexte route

**But :** faire en sorte que camera, gizmo et viewport editeur ne dependent plus de l'input brut de la fenetre.

**Travail attendu :**
1. Verifier que `EditorViewportCameraController` et `EditorViewportGizmoController` consomment uniquement `ViewInputContext`.
2. Reduire `WorldViewportPanel` a un role de host visuel et d'activation/focus.
3. Supprimer toute logique de decision d'input qui devrait vivre dans le routage central.
4. Preserver les comportements valides : focus clavier, capture pendant drag, relachement propre hors viewport.

**Livrable :**
- Controleurs runtime autonomes basees sur le contexte route.
- Panel viewport plus simple et plus passif.

**Validation :**
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`
- rotation camera, pan, zoom, picking, gizmo

**Commit attendu :** `refactor(editor): move viewport tools fully onto routed input`

---

## ⚪ Tache 6 — Clarifier capture, focus et modalite dans le contrat UI

**But :** rendre explicite ce que la couche UI expose au routage sans embarquer la logique gameplay/editeur.

**Travail attendu :**
1. Definir les etats exposes par la couche UI : survol UI, capture pointeur, capture clavier, modalite active.
2. Verifier que ces etats sont derives proprement de l'etat courant et non de caches one-way.
3. Centraliser la politique de blocage modal au bon niveau.
4. Ajouter des garde-fous sur les transitions ouverture/fermeture de modal.

**Livrable :**
- Contrat UI clair pour le routage.
- Semantique explicite de modalite/capture/focus.

**Validation :**
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`
- ouvrir/fermer une fenetre modale puis tester clics UI et viewport

**Commit attendu :** `refactor(input): separate modal focus and capture state`

---

## ⚪ Tache 7 — Ajouter la validation de non-regression

**But :** verrouiller les regressions historiques autour de la modalite, du viewport et des clics UI.

**Travail attendu :**
1. Mettre a jour `ai-agent/editor-input-routing-validation.md` si le plan de validation doit evoluer.
2. Ajouter, si possible sans surcout excessif, des tests cibles sur les transitions de modalite et le calcul du contexte route.
3. Documenter les scenarios manuels obligatoires apres changement d'architecture input.

**Livrable :**
- Validation manuelle ciblee maintenue a jour.
- Eventuels tests automatises si le repo permet un cout raisonnable.

**Validation :**
- `dotnet build CasaEngine/CasaEngine.csproj -nologo`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo`
- `dotnet build CasaEngine.SimpleEditor/CasaEngine.SimpleEditor.csproj -nologo`

**Commit attendu :** `test(input): add regression coverage for routed snapshot flow`

---

## Ordre d'execution recommande

1. Tache 1
2. Tache 2
3. Tache 3
4. Tache 4
5. Tache 5
6. Tache 6
7. Tache 7

## Criteres de succes finaux

- Une seule acquisition brute par frame.
- MGUI et le moteur lisent la meme frame d'input.
- `ViewInputContext` suffit aux controleurs runtime.
- `WorldViewportPanel` ne porte plus la logique d'interpretation d'input.
- Les regressions historiques de modalite et de perte de clic ne reapparaissent pas.