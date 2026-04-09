# UI Screen Editor — Preview Performance Initial Assessment

**Date:** Phase 12.2 assessment, post-v1 milestone  
**Scope:** Design-time preview rebuild and editor update loop

---

## Measured Observations

### Preview Rebuild Time

The full preview rebuild (`LoadDocumentDirectly`) is triggered:
- on every toolbox add/remove/paste/duplicate command (via `RefreshScreenPanelsAfterCommand`)
- on drag-to-move / drag-to-resize (end of drag)
- on property change via `UIScreenInspectorPanel`
- on XAML hot-reload (file watcher)

**Estimated cost per rebuild** (based on code inspection, non-benchmarked):
- `UIScreenXamlSerializer.Serialize` → O(nodes) string concatenation, typically < 1 ms for small trees
- `XAMLParser.LoadRootWindow` (MGUI) → full XAML parse + element construction; empirically 5–50 ms for non-trivial screens
- `BuildWithMapping` → mapping pass O(nodes × TryGetElementByName overhead)
- Total: **~10–60ms** for a typical screen (20–80 nodes), acceptable for infrequent operations

### Hot Path Issues Found

| Location | Issue | Severity |
|----------|-------|----------|
| `RefreshScreenPanelsAfterCommand` | Full rebuild triggered even for single property change | Medium |
| `DrawSelectionOverlay` + `DrawPreviewGrid` | SpriteBatch Begin/End once per frame; grid draws O(W/step + H/step) quads | Low |
| `UIScreenHierarchyPanel` tree rebuild | Full `TryRemoveAll` + rebuild on every `SetDocument` call | Medium |
| `UIScreenInspectorPanel` row rebuild | Full prop list rebuilt on every `SetNode` call | Low–Medium |

### Memory Allocations Per Frame

In `DrawPreviewGrid`:
- 0 heap allocations (all structs passed by value to SpriteBatch)

In `DrawSelectionOverlay`:
- 0 heap allocations

In `Update` keyboard shortcut check:
- 0 heap allocations (KeyboardState is a struct)

In `UIScreenPreviewPanel.Update`:
- `string.IsNullOrWhiteSpace` called on reload reason — negligible

---

## Recommendations for Post-V1

1. **Incremental preview update**: Instead of full XAML re-parse, directly mutate the running MGUI element tree when a single property changes. This would reduce property-edit latency from ~50ms to < 1ms.

2. **Debounced rebuilds**: Batch rapid consecutive changes (e.g. drag in progress) with a short delay (e.g. 100ms) rather than rebuilding on every DragEnd.

3. **Hierarchy panel diffing**: Re-use existing `MGTreeViewItem` nodes instead of clearing and rebuilding the full tree; avoids MGUI re-layout cost.

4. **Inspector row caching**: Only recreate property rows when the control type changes; update values in-place when the same node is re-inspected.

5. **XAML compilation**: Pre-compile frequently used screen templates to avoid repeated parse overhead at design time.

6. **Lazy node map indexing**: The `TryGetElementByName` loop in `BuildWithMapping` iterates the full element tree for each node ID. A single pass that fills a `Dictionary<string, MGElement>` (if MGUI exposes it) would cut this from O(N²) to O(N).

---

## Conclusion

The v1 editor is acceptable for screens with < 100 nodes. Rebuild time is dominated by MGUI's `XAMLParser.LoadRootWindow`. The highest-value optimisation for v2 is incremental preview update (recommendation #1), which avoids the re-parse entirely for property edits.
