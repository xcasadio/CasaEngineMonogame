# Editor / InGame / UI Input Routing Tasks

Ce fichier définit une suite de tâches committables une par une pour refondre le routage d'input entre le moteur, les viewports éditeur et MGUI.

## But

Obtenir une architecture cohérente, simple et durable :

- une seule acquisition raw de l'input au niveau fenêtre
- une seule traduction vers les vues / viewports
- un seul routeur de décision pour focus, capture, modalité et coordonnées locales
- des consommateurs runtime clairs pour le gameplay, la caméra éditeur et le gizmo
- aucun polling Win32 ou workaround local dans les panneaux UI

## Principes d'architecture

- Le panneau UI ne doit pas décider de la logique d'input gameplay.
- Le moteur doit posséder le cycle de vie de l'input et du routage.
- MGUI doit annoncer l'état UI (`pointer over`, `keyboard captured`, `modal`) sans contenir la logique caméra/gizmo.
- Les comportements éditeur doivent être portés par des contrôleurs runtime dédiés, pas par `WorldViewportPanel`.
- La solution finale doit fonctionner de manière cohérente en mode in-game, `CasaEngine.Editor` et `CasaEngine.SimpleEditor`.

## Règles d'exécution pour l'agent

- Traiter une seule tâche à la fois.
- Créer exactement un commit par tâche terminée.
- Mettre à jour l'icône de statut de la tâche avant de passer à la suivante.
- Ne pas regrouper plusieurs tâches dans le même commit.
- Si une tâche est bloquée, passer son statut à `⛔` et expliquer le blocage dans la tâche.
- Après chaque tâche, exécuter une validation bornée sur les projets touchés.
- Ne pas ajouter de nouveau workaround local dans `WorldViewportPanel` ou dans `Game1` pour contourner une faiblesse du routeur.

## Légende des statuts

- ⬜ À faire
- 🟠 En cours
- ✅ Terminé
- ⛔ Bloqué

## Résultat cible

À la fin du plan :

- `InputRouter` route un snapshot complet et cohérent pour chaque vue
- `RenderView` / `UIRoot` / MGUI restent les sources de vérité pour le focus UI et la modalité
- le viewport éditeur ne fait plus de polling direct Win32 pour la souris, la molette ou le clavier
- la caméra éditeur et le gizmo consomment un flux runtime commun, compatible avec le mode in-game

## Tâches

### ✅ EIR-001 — Formaliser l'architecture cible du routage d'input
**Objectif**  
Produire la structure cible côté moteur avant les changements de code pour éviter de continuer à empiler des correctifs locaux.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Input/InputRouter.cs`
- `CasaEngine/Framework/Input/InputComponent.cs`
- `CasaEngine/Framework/GUI/MGUI/UIRoot.cs`
- `CasaEngine/Framework/GUI/MGUI/ViewRenderHost.cs`
- `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
- nouveau document d'architecture si nécessaire dans `docs/` ou `ai-agent/`

**Actions**
- Décrire le flux cible : acquisition raw fenêtre → traduction par vue → arbitrage UI / gameplay → consommation runtime.
- Identifier explicitement les responsabilités interdites dans les panneaux UI.
- Définir les abstractions qui manqueront pour atteindre cette cible.

**Critères d’acceptation**  
- l'architecture cible est documentée clairement
- les responsabilités moteur / UI / éditeur sont séparées sans ambiguïté
- le document sert de référence pour les tâches suivantes

**Résultat**
- document créé : `ai-agent/editor-input-routing-architecture.md`
- le flux cible et les responsabilités interdites dans les panneaux UI sont explicités

**Commit suggéré**  
`Document target input routing architecture`

---

### ✅ EIR-002 — Introduire une source raw fenêtre partagée
**Objectif**  
Supprimer la duplication d'acquisition raw entre `Game1`, `SimpleEditor` et les panneaux, en introduisant une seule source de vérité pour clavier, souris et molette.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Input/`
- `CasaEngine.Editor/Game1.cs`
- `CasaEngine.SimpleEditor/Game1.cs`
- éventuellement `CasaEngine/Framework/Game/CasaEngineGame.cs`

**Actions**
- Créer une abstraction partagée du type `IWindowInputSource` ou `IInputSnapshotSource`.
- Faire produire un snapshot unique par frame au niveau host fenêtre.
- Remplacer `EditorRawInputSource` dupliqué par cette abstraction commune.

**Critères d’acceptation**  
- il n'existe plus plusieurs implémentations ad hoc du même raw input fenêtre
- `CasaEngine.Editor` et `CasaEngine.SimpleEditor` utilisent la même acquisition raw
- la molette, les boutons et les coordonnées proviennent de la même source

**Résultat**
- ajout de `CasaEngine/Framework/Input/IWindowInputSource.cs`
- ajout de `CasaEngine/Framework/Input/Win32WindowInputSource.cs`
- remplacement des deux `EditorRawInputSource` locaux par la source partagée

**Commit suggéré**  
`Add shared window input source for editor hosts`

---

### ✅ EIR-003 — Étendre le routeur pour transporter un contexte d'entrée complet par vue
**Objectif**  
Faire de `InputRouter` la seule couche qui choisit la vue cible et produit l'état d'entrée local à cette vue, y compris la molette.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Input/InputRouter.cs`
- `CasaEngine/Framework/Input/InputComponent.cs`
- nouvelles classes dans `CasaEngine/Framework/Input/`
- `CasaEngine/Framework/Rendering/ViewManager.cs`

**Actions**
- Introduire une structure dédiée, par exemple `ViewInputContext`, contenant au minimum :
  - `ViewId`
  - `KeyboardState`
  - `MouseState` local à la vue
  - delta molette vertical / horizontal
  - raisons de routage (`pointer`, `capture`, `modal`, `keyboard focus`)
- Faire en sorte que `TryDispatch` retourne un contexte complet, pas seulement un couple clavier/souris.
- Centraliser la traduction screen-space → view-local dans le routeur ou dans une abstraction dédiée au moteur.

**Critères d’acceptation**  
- le routeur gère explicitement la molette et les coordonnées locales
- la décision de vue cible n'est plus répliquée côté panneau
- `InputComponent` consomme le contexte routé sans logique parallèle

**Résultat**
- ajout de `CasaEngine/Framework/Input/ViewInputContext.cs`
- `InputRouter` expose désormais `TryDispatchContext(...)` et `CurrentInputContext`
- `InputComponent` consomme le contexte routé tout en conservant la compatibilité avec `KeyboardManager` et `MouseManager`

**Commit suggéré**  
`Extend input router with per-view input context`

---

### ⬜ EIR-004 — Aligner le routeur avec la UI runtime per-view
**Objectif**  
Faire en sorte que les signaux UI per-view (`pointer over`, `keyboard captured`, `modal`) soient les seules informations UI utilisées pour arbitrer gameplay et édition.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/GUI/MGUI/UIRoot.cs`
- `CasaEngine/Framework/GUI/IUIViewRuntime.cs`
- `CasaEngine/Framework/Input/InputRouter.cs`
- `CasaEngine/Framework/Game/CasaEngineGame.cs`

**Actions**
- Vérifier et compléter les signaux exposés par `IUIViewRuntime` si nécessaire.
- Utiliser ces signaux dans `InputRouter` comme unique vérité d'arbitrage UI.
- Supprimer toute logique éditeur qui tente de recomposer cet état côté panneau.

**Critères d’acceptation**  
- le routeur ne dépend pas de détails UI editor-specific
- MGUI reste responsable de l'état UI, pas de la logique gameplay
- la priorité modal > capture > pointer > keyboard focus reste centralisée

**Commit suggéré**  
`Align input router with per-view UI runtime state`

---

### ✅ EIR-005 — Introduire un contrôleur runtime pour la caméra éditeur
**Objectif**  
Déplacer les règles de navigation caméra hors de `WorldViewportPanel` vers une classe runtime dédiée qui consomme l'input routé.

**Fichiers / classes concernés**  
- nouveau contrôleur dans `CasaEngine.Editor/Runtime/` ou `CasaEngine/Framework/Game/Components/Editor/`
- `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
- `CasaEngine.Editor/Game1.cs`
- `CasaEngine.SimpleEditor/Game1.cs`

**Actions**
- Créer un composant ou service du type `EditorViewportCameraController`.
- Lui faire consommer le contexte d'entrée routé par vue.
- Déplacer orbit, pan, zoom et déplacement clavier dans ce contrôleur.
- Réduire `WorldViewportPanel` au rôle d'hôte visuel de la `RenderView`.

**Critères d’acceptation**  
- `WorldViewportPanel` ne contient plus la logique caméra principale
- le contrôleur caméra fonctionne dans `CasaEngine.Editor` et `CasaEngine.SimpleEditor`
- le code de navigation n'a plus besoin de polling Win32 local

**Résultat**
- ajout de `CasaEngine.Editor/Runtime/EditorViewportCameraController.cs`
- déplacement de l'orbit, du pan, du zoom et du déplacement clavier hors de `WorldViewportPanel`
- `WorldViewportPanel` délègue désormais la navigation caméra au contrôleur runtime via `ViewInputContext`
- validation bornée : `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -nologo` et `dotnet build CasaEngine.SimpleEditor/CasaEngine.SimpleEditor.csproj -nologo`

**Commit suggéré**  
`Move editor camera input into runtime viewport controller`

---

### ⬜ EIR-006 — Introduire un contrôleur runtime pour le gizmo et le picking éditeur
**Objectif**  
Sortir de `WorldViewportPanel` la logique de sélection, hover, drag et hotkeys du gizmo, pour la replacer dans le runtime éditeur.

**Fichiers / classes concernés**  
- `CasaEngine.Framework/Game/Components/Editor/GizmoComponent.cs`
- `GizmoTool/`
- nouveau contrôleur editor-side si nécessaire
- `CasaEngine.Editor/Controls/WorldViewportPanel.cs`

**Actions**
- Définir un point d'entrée clair pour les actions gizmo alimentées par le contexte d'entrée routé.
- Déplacer la gestion des hotkeys et de la sélection viewport-local hors du panneau.
- Faire en sorte que le gizmo soit piloté par une couche runtime editor dédiée.

**Critères d’acceptation**  
- le panneau ne pilote plus directement `Gizmo.Update()` ni `SelectEntities()`
- hover, drag, sélection et raccourcis du gizmo passent par un contrôleur runtime
- le chemin gizmo est compatible avec le routeur central

**Commit suggéré**  
`Move editor gizmo input flow into runtime controller`

---

### ⬜ EIR-007 — Nettoyer `WorldViewportPanel` et supprimer les contournements locaux
**Objectif**  
Faire de `WorldViewportPanel` un hôte de rendu et de focus, sans hooks Win32 ni logique d'orchestration d'input.

**Fichiers / classes concernés**  
- `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
- `CasaEngine.Editor/Game1.cs`
- `CasaEngine.SimpleEditor/Game1.cs`

**Actions**
- Retirer `MouseWheelMessageHook`, `ViewportMouseStateProvider` et les appels directs à `Keyboard.GetState()` / `Mouse.GetState()` du panneau.
- Garder au maximum : création de la vue, resize, binding texture, activation/focus explicite si nécessaire.
- Supprimer les chemins d'update spécifiques ajoutés pour corriger le viewport.

**Critères d’acceptation**  
- le panneau ne contient plus de logique d'input spécifique device
- il ne reste pas de code Win32 de routage dans le panneau
- l'éditeur continue de fonctionner via le routeur et les contrôleurs runtime

**Commit suggéré**  
`Remove viewport-local input workarounds from world panel`

---

### ⬜ EIR-008 — Aligner le mode in-game et le mode éditeur sur le même modèle de vue
**Objectif**  
S'assurer que les mêmes abstractions de vue, UI runtime et input routing s'appliquent au jeu et à l'éditeur, au lieu de maintenir deux modèles mentaux.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Game/CasaEngineGame.cs`
- `CasaEngine/Framework/Rendering/RenderView.cs`
- `CasaEngine/Framework/Input/`
- `CasaEngine.Editor/Runtime/`

**Actions**
- Vérifier que les vues in-game et editor utilisent bien le même contrat de routage.
- Éliminer les branches editor-specific qui réimplémentent la logique du moteur sans nécessité.
- Clarifier les rares différences légitimes entre in-game et editor.

**Critères d’acceptation**  
- l'éditeur et l'in-game partagent la même chaîne conceptuelle d'input
- les spécificités editor sont portées par des contrôleurs, pas par une architecture parallèle
- les points de divergence sont documentés et justifiés

**Commit suggéré**  
`Align editor and ingame input routing model`

---

### ⬜ EIR-009 — Ajouter une validation ciblée et durable du routage d'input
**Objectif**  
Sécuriser la nouvelle architecture avec des validations ciblées sur les cas critiques déjà régressés.

**Fichiers / classes concernés**  
- tests existants ou nouveaux tests dans les projets adaptés
- documentation technique si nécessaire
- `CasaEngine.Editor`
- `CasaEngine.SimpleEditor`

**Actions**
- Ajouter des tests ciblés pour le routeur lorsque c'est faisable.
- Ajouter au minimum des validations bornées pour :
  - focus de vue
  - capture d'input
  - molette
  - modalité UI
  - coordonnées locales
- Documenter la stratégie de validation manuelle si certains cas ne sont pas automatisables.

**Critères d’acceptation**  
- la nouvelle architecture est couverte par des validations ciblées
- les anciennes régressions connues ont un scénario de vérification explicite
- l'agent peut démontrer que la solution ne repose plus sur des workarounds locaux

**Commit suggéré**  
`Add targeted validation for unified input routing`

---

## Ordre recommandé

1. EIR-001
2. EIR-002
3. EIR-003
4. EIR-004
5. EIR-005
6. EIR-006
7. EIR-007
8. EIR-008
9. EIR-009

## Rappel important pour l'agent

Le critère de réussite n'est pas seulement que les inputs fonctionnent.  
Le critère de réussite est que le chemin d'input soit lisible, centralisé et identique dans son modèle mental entre moteur, éditeur et MGUI.
