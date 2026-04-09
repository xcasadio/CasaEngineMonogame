# CasaEngine Folder Hierarchy Namespace Compatibility

Cette note documente l'état final du renommage des namespaces après l'alignement sur la hiérarchie physique.

## Principe

Les catégories historiques ont été renommées vers les emplacements réels du code. Les namespaces génériques ou « fourre-tout » ont été retirés au profit de segments explicites.

## Mappings finaux

### Core

- `CasaEngine.Core.Log` -> `CasaEngine.Core.Logging`
- `CasaEngine.Core.Maths` -> `CasaEngine.Core.Math`
- `CasaEngine.Core.Parser` -> `CasaEngine.Core.Parsing`
- `CasaEngine.Core.MultiThreading` -> `CasaEngine.Core.Threading`
- `CasaEngine.Core.Helpers` -> `CasaEngine.Core.Math.Extensions`
- `CasaEngine.Core.Helpers` -> `CasaEngine.Core.Math.Geometry`
- `CasaEngine.Core.Helpers` -> `CasaEngine.Core.Text`
- `CasaEngine.Core.Helpers` -> `CasaEngine.Core.Time`
- `CasaEngine.Core.Helpers` -> `CasaEngine.Core.Randomization`

### Engine

- `CasaEngine.Engine.Input.InputDeviceStateProviders` -> `CasaEngine.Engine.Input.Providers`
- `CasaEngine.Engine.Input.InputSequence` -> `CasaEngine.Engine.Input.Sequences`
- `CasaEngine.Engine` -> `CasaEngine.Engine.Environment` pour `EngineEnvironment`
- `CasaEngine.Engine.Primitives2D` -> `CasaEngine.Engine.Primitives.TwoD`
- `CasaEngine.Engine.Primitives3D` -> `CasaEngine.Engine.Primitives.ThreeD`

### Framework

- `CasaEngine.Framework.Game` -> `CasaEngine.Framework.Application`
- `CasaEngine.Framework.GameFramework` -> `CasaEngine.Framework.Gameplay`
- `CasaEngine.Framework.Debugger` -> `CasaEngine.Framework.Debug`
- `CasaEngine.Framework.GUI` -> `CasaEngine.Framework.UI`
- `CasaEngine.Framework.Graphics` -> `CasaEngine.Framework.Rendering.Models`
- `CasaEngine.Framework.Graphics2D` -> `CasaEngine.Framework.Rendering.Draw2D`
- `CasaEngine.Framework.World` -> `CasaEngine.Framework.Scene.World`
- `CasaEngine.Framework.Transform` -> `CasaEngine.Framework.Scene.Transform`
- `CasaEngine.Framework.Entities` -> `CasaEngine.Framework.Scene.Entities`
- `CasaEngine.Framework.SpacePartitioning.Octree` -> `CasaEngine.Framework.Scene.Spatial.Octree`
- `CasaEngine.Framework` -> `CasaEngine.Framework.Common` pour `ObjectBase`
- `CasaEngine.Framework` -> `CasaEngine.Framework.Configuration` pour `Constants`
- `CasaEngine.Framework.Materials` -> `CasaEngine.Framework.Materials.Runtime`
- `CasaEngine.Framework.Materials` -> `CasaEngine.Framework.Materials.Definitions`
- `CasaEngine.Framework.Materials` -> `CasaEngine.Framework.Materials.Authoring`
- `CasaEngine.Framework.Materials` -> `CasaEngine.Framework.Materials.Compilation`
- `CasaEngine.Framework.Materials` -> `CasaEngine.Framework.Materials.Serialization`

## Exception documentée

- Les dossiers physiques `Engine/Primitives/2D` et `Engine/Primitives/3D` utilisent les namespaces `Primitives.TwoD` et `Primitives.ThreeD`, car un segment de namespace C# ne peut pas commencer par un chiffre.