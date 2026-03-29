# Runtime Save(JObject) cleanup plan

Objective: remove the last authoring-only `Save(JObject)` methods from `CasaEngine`, keep editor serialization in `CasaEngine.EditorServices`, and leave runtime loading intact.

Status legend:
- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

Tasks:
1. ✅ Inventory every remaining `Save(JObject)` method in `CasaEngine` and identify editor call sites.
2. ✅ Confirm the runtime serialization contract only requires `Load(JObject)` for loaders and factories.
3. ✅ Remove `Save(JObject)` from runtime base types and leaf types in `CasaEngine`.
4. ✅ Move the last runtime-backed asset catalog serialization (`AssetInfo.Save`) into `CasaEngine.EditorServices`.
5. ✅ Re-scan the workspace to confirm `CasaEngine` no longer contains `Save(JObject)` methods.
6. ✅ Build the touched projects/solution to validate the refactor.

Acceptance criteria:
- `CasaEngine` contains no remaining `Save(JObject)` methods.
- `CasaEngine.EditorServices` still serializes editor/authored data correctly.
- Runtime JSON loading paths still compile unchanged.

Validation note:
- `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug` succeeded.
- Existing warnings remain in editor projects (mostly nullable-context warnings and DPI manifest warnings), but no new build error was introduced by this cleanup.