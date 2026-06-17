# Editor — Contextual Tool Panels per Asset Type

## Contexte

Dans l'éditeur CasaEngine, l'interface doit s'adapter au type d'asset ouvert :

- L'asset ouvert détermine les outils pertinents.
- Les contrôles spécialisés (ex: « Animation2D Timeline ») ne sont affichés que
  lorsqu'ils ont un intérêt pour l'asset courant.
- Les contrôles génériques (Inspector, Hierarchy) restent disponibles mais
  changent de contenu selon le contexte. **Déjà géré** par `ContextualDockPanelHost`.
- Le changement d'asset doit mettre à jour proprement l'UI.
- Un contrôle devenu inutile ne doit pas continuer à afficher / modifier des
  données obsolètes.

### Décisions validées (utilisateur)

1. Panneau spécialisé non pertinent → **retiré du dock** (réajouté quand pertinent).
2. **Mécanisme générique réutilisable** : registre `EditorDocumentKind → panneaux`.
3. À l'ouverture : le panneau **apparaît sans voler le focus**.
4. Seul « Animation2D Timeline » est concerné aujourd'hui.

### État existant (audité)

- `EditorContextService.Current` : `ActiveDocument` (`EditorDocumentKind`) +
  événement `ActiveDocumentChanged`.
- `ContextualDockPanelHost` adapte déjà Inspector / Hierarchy par type d'asset.
- `Animation2dTimeline` est un **tool panel fixe** toujours présent (bas du dock),
  ajouté par `EnsureAnimation2dTimelineDockPanel()` (appelé dans `LoadDockLayout`
  et `ResetDockLayout`). Quand on ouvre un material il affiche
  « No Animation2D timeline available. » au lieu de disparaître.
- Dock API : `DockOperation.DockAsTab` / `RemovePanel(LayoutModel, panel)`,
  `LayoutModel.FindPanelById`, `_dockHost.RebuildVisualTree()` (public).
- Les panneaux issus d'un layout chargé ne sont **pas** dans
  `MGDockHost._panelRegistry` → suppression via `DockOperation.RemovePanel` +
  `RebuildVisualTree()`.

---

## Tâches

> Légende : ✅ Done · 🚧 In progress · ⏳ Todo · 🧪 Needs testing · ⚠️ Blocked

- ✅ **T1 — Plan** : créer ce fichier de plan.
- ⏳ **T2 — Registre générique** : créer
  `ContextualToolPanelRegistry` (mapping `EditorDocumentKind → panel ids`,
  données/policy réutilisables, sans dépendance UI).
- ⏳ **T3 — Intégration GameEditor** : instancier et configurer le registre
  (`Animation2d → Animation2dTimeline`) ; ajouter `SyncContextualToolPanels()`
  et les helpers `EnsureContextualToolPanelPresent/Absent(panelId)` en
  généralisant `EnsureAnimation2dTimelineDockPanel()` ; ajout **sans voler le
  focus** (préserver l'onglet actif du groupe cible).
- ⏳ **T4 — Câblage** : remplacer les appels à
  `EnsureAnimation2dTimelineDockPanel()` par `SyncContextualToolPanels()`
  (load/reset) ; s'abonner à `EditorContextService.ActiveDocumentChanged` pour
  re-synchroniser à chaque changement d'asset.
- ⏳ **T5 — Données obsolètes & ré-entrance** : détacher la vue spécialisée
  (`_animation2dTimelinePanel.SetInspectorPanel(null)`) quand le panneau est
  retiré ; garde de ré-entrance sur la synchro.
- ⏳ **T6 — Build & vérification** : compiler la solution éditeur ; vérifier
  l'ouverture Animation2D → Material → World et l'apparition/disparition du
  panneau Timeline.

---

## Notes d'implémentation

- `SyncContextualToolPanels()` : pour chaque panneau géré, si pertinent pour le
  `ActiveDocument.Kind` → ensure présent ; sinon → ensure absent.
- Ajout sans focus : capturer `group.ActivePanelId`, `AddPanel(node, -1)`,
  restaurer l'onglet actif, puis `RebuildVisualTree()`.
- Suppression : retrouver le node via `LayoutModel.FindPanelById`,
  `DockOperation.RemovePanel`, `RebuildVisualTree()`.
- Aucune régression API publique ; aucun changement de format de sérialisation.
