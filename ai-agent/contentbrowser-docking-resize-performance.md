# ContentBrowser + MGDockHost resize performance

Date: 2026-06-01

## Probleme observe

- Quand le ContentBrowser est visible et qu'un dossier contenant beaucoup d'elements est selectionne, le redimensionnement d'un panel du `MGDockHost` ralentit fortement l'editeur.
- Le scroll dans le ContentBrowser reste fluide dans le meme cas.
- Objectif: determiner si le cout vient de MGUI ou du code editeur, sans workaround et en respectant l'architecture moderne MGUI/editeur.

## Surfaces inspectees

- `CasaEngine.Editor/Controls/ContentBrowserPanel.cs`
- `CasaEngine.Editor/ContentBrowser/Views/GridView.cs`
- `CasaEngine.Editor/ContentBrowser/Views/DetailView.cs`
- `CasaEngine.Editor/ContentBrowser/ContentBrowserItemQuery.cs`
- `MGUI/MGUI.Core/UI/Docking/Controls/MGDockSplitterBar.cs`
- `MGUI/MGUI.Core/UI/Docking/Controls/MGDockSplitContainer.cs`
- `MGUI/MGUI.Core/UI/Docking/Controls/MGDockHost.cs`
- `MGUI/MGUI.Core/UI/Containers/MGWrapPanel.cs`
- `MGUI/MGUI.Core/UI/MGScrollViewer.cs`
- `MGUI/MGUI.Core/Tooling/UIPerformanceProbe.cs`
- `CasaEngine.Editor/Diagnostics/EditorPerformanceProbe.cs`

## Faits etablis dans le code

- `MGDockSplitterBar.UpdateSelf()` met a jour le split pendant le drag via `ParentSplitContainer.SetSplitRatioWithoutSync(newRatio, clamp: false)`.
- `MGDockSplitContainer.SetSplitRatioWithoutSync()` appelle `LayoutChanged(this, true)`, mais ne declenche ni `NPC(nameof(SplitRatio))` ni `SplitRatioChanged`; le modele de docking n'est donc pas synchronise a chaque frame de drag.
- `MGDockSplitContainer.CommitRatioToModel()` n'est appele qu'a la fin du drag, quand le bouton souris est relache.
- `MGDockHost.RebuildVisualTree()` est appele sur changement structurel du modele de docking, mais le chemin de drag ci-dessus evite ce rebuild pendant le drag normal.
- `ContentBrowserPanel.RefreshAssetList()` reconstruit les deux vues (`GridView.SetItems` et `DetailView.SetItems`) quand le dossier courant ou le filtre change, pas dans `ContentBrowserPanel.Update()` hors changement disque externe.
- `GridView.SetItems()` cree une carte MGUI par item visible et les ajoute toutes dans un `MGWrapPanel`.
- `MGWrapPanel.UpdateContentMeasurement()` et `MGWrapPanel.UpdateContentLayout()` mesurent tous les enfants via `MeasureChildren()`; `UpdateContentLayout()` re-mesure aussi avant d'arranger tous les enfants.
- `MGScrollViewer.DrawContents()` saute les enfants directs dont `ActualLayoutBounds` est vide, ce qui peut expliquer un scroll fluide meme si la mesure/layout d'un resize reste couteuse.
- `UIPerformanceProbe` existe deja cote MGUI. Il mesure update/draw/layout/measure par element, compte les invalidations layout, et s'active avec `CASA_MGUI_PERF_PROBE`.
- `EditorPerformanceProbe` existe deja cote editeur. Il mesure les phases d'update, dont `ContentBrowserPanel.Update` et `MGUI.Desktop.Update`, avec `CASA_EDITOR_PERF_PROBE`.

## Hypothese locale falsifiable

Le ralentissement pendant le resize est probablement domine par le layout/measure MGUI du contenu du ContentBrowser, en particulier le `MGWrapPanel` de la vue grille et/ou les elements qu'il contient, parce que le splitter dock invalide le layout a chaque frame de drag alors que le scroll normal beneficie du clipping/culling de rendu.

Cette hypothese sera fausse si les mesures montrent que:

- `MGUI.Desktop.Update` reste faible pendant le drag tandis qu'une phase editeur hors MGUI domine;
- ou les tops `UIPerformanceProbe` ne montrent pas de cout significatif sur `MGWrapPanel`, `MGScrollViewer`, `GridView` ou leurs enfants pendant le drag;
- ou le ralentissement est lie a un rebuild de tree (`ContentBrowserPanel.RefreshAssetList`, `GridView.SetItems`, `DetailView.SetItems`, `MGDockHost.RebuildVisualTree`) declenche pendant le drag.

## Controles a faire

1. Lancer l'editeur avec `CASA_EDITOR_PERF_PROBE` et `CASA_MGUI_PERF_PROBE` actifs, selectionner un dossier volumineux dans le ContentBrowser, puis redimensionner un splitter du docking.
2. Comparer les frames lentes:
   - si `EditorPerformanceProbe` pointe `MGUI.Desktop.Update`, le probleme est d'abord dans le layout/update MGUI;
   - si `ContentBrowserPanel.Update` ou une autre phase editeur domine, inspecter ce chemin editeur.
3. Lire les tops `UIPerformanceProbe` sur les memes frames:
   - cout `measure`/`layout` eleve sur `MGWrapPanel` ou les cartes ContentBrowser => probleme de strategie de layout/virtualisation MGUI ou d'utilisation editor d'un panel non virtualise;
   - invalidations venant du splitter dock seulement, sans rebuild du contenu => comportement attendu du resize, cout a traiter dans l'architecture layout;
   - appels repetes a `SetItems`/tree rebuild pendant drag => probleme editeur.

## Metriques ajoutees

- `GameEditor.CreateEditorPerformanceContext()` ajoute maintenant le contexte ContentBrowser aux frames `EditorPerformanceProbe`:
   - `cbView`
   - `cbItems`
   - `cbTreeFolders`
   - `cbSearchLength`
   - `cbThumbs`
- `ContentBrowserPanel.RebuildTree()` est mesure par une phase `ContentBrowserPanel.RebuildTree`.
- `ContentBrowserPanel.RefreshTreeView()` est mesure par une phase `ContentBrowserPanel.RefreshTreeView`.
- `ContentBrowserPanel.RefreshAssetList()` est mesure par une phase `ContentBrowserPanel.RefreshAssetList`.
- `GridView.SetItems()` est mesure par une phase `ContentBrowser.GridView.SetItems count=N`.
- `DetailView.SetItems()` est mesure par une phase `ContentBrowser.DetailView.SetItems count=N`.

Ces metriques sont optionnelles: elles ne produisent rien tant que `CASA_EDITOR_PERF_PROBE` n'est pas defini.

## Commande de capture recommandee

Exemple PowerShell depuis la racine repo:

```powershell
$env:CASA_EDITOR_PERF_PROBE = "artifacts/perf/editor-contentbrowser-resize.txt"
$env:CASA_EDITOR_PERF_SAMPLE_INTERVAL = "1"
$env:CASA_EDITOR_PERF_THRESHOLD_MS = "0"
$env:CASA_MGUI_PERF_PROBE = "artifacts/perf/mgui-contentbrowser-resize.txt"
$env:CASA_MGUI_PERF_SAMPLE_INTERVAL = "1"
$env:CASA_MGUI_PERF_TOP = "25"
dotnet run --project CasaEngine.Editor/CasaEngine.Editor.csproj
```

Scenario a capturer:

1. Ouvrir le projet dans l'editeur.
2. Afficher le ContentBrowser.
3. Selectionner un dossier avec beaucoup d'elements.
4. Redimensionner un panel du docking jusqu'a observer le ralentissement.
5. Comparer les frames lentes dans `editor-contentbrowser-resize.txt` et `mgui-contentbrowser-resize.txt`.

Lecture attendue:

- Si les frames lentes ont `MGUI.Desktop.Update` dominant et que le top MGUI montre `measure/layout` sur `MGWrapPanel`, `MGScrollViewer` ou les cartes du ContentBrowser, le cout est cote layout MGUI ou usage d'un panel non virtualise par la vue editeur.
- Si les phases `ContentBrowserPanel.RefreshAssetList`, `ContentBrowser.GridView.SetItems` ou `ContentBrowser.DetailView.SetItems` apparaissent pendant le drag, l'editeur reconstruit la liste pendant le resize et il faut corriger ce chemin editeur.
- Si `MGUI.Desktop.Update` est faible et qu'une autre phase editeur domine, l'investigation doit basculer vers cette phase.

## Etat courant

- Analyse statique: le drag du splitter dock invalide le layout sans synchroniser le modele de docking a chaque frame, donc aucun rebuild de `MGDockHost` n'est attendu pendant le drag normal.
- Metriques ajoutees pour verifier si le ContentBrowser reconstruit ses vues pendant le drag ou si le cout vient du layout/measure MGUI.
- Correction architecturale ajoutee: MGUI expose maintenant `VirtualizingWrapPanel`, un panel de wrap vertical a items uniformes qui ne realise que les lignes visibles plus un buffer.
- `GridView` du ContentBrowser utilise `VirtualizingWrapPanel` au lieu de creer toutes les cartes dans un `MGWrapPanel`; les cartes realisees sont recyclees et rebindees sur les `ContentItem` courants.
- Les drop targets des cartes ContentBrowser sont maintenant attachees une seule fois par element recycle et resolvent le dossier courant via le mapping `_externalDropTargetFolders`, afin d'eviter les handlers obsoletes captures sur un ancien item.
- Tests ajoutes: `VirtualizingWrapPanelLayoutTests` couvre les calculs purs de mesure, plage visible et bounds d'item; la regle d'architecture border-backed inclut aussi `VirtualizingWrapPanel`.
- Validation: `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj --filter "VirtualizingWrapPanelLayoutTests|BorderBackedControls_ExposeCornerRadiusProperty" --no-restore` reussit.
- Validation: `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj --no-restore` reussit; le build conserve des avertissements preexistants du projet.
- Validation bloquee: `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter ContentBrowser --no-restore` ne compile pas a cause d'erreurs existantes hors ContentBrowser (`Pool<>`, `DualQuaternion`, signature `EditorViewportCameraController.Update`, `LightComponent.Coordinates`, `PreviewEnvironmentFactory`).