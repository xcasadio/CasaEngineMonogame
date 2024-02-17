# CasaEngineMonogame
Editor + game engine written in C# .Net 6 and WPF with MonoGame.

## Objective:
I developed this engine with the aim of migrating all my game projects to the same engine.

## Contributors
You are welcome, you can contact me with [GitHub Discussions](https://github.com/xcasadio/CasaEngineMonogame/discussions).

## Features
* Editor (based on [Unreal Engine](https://www.unrealengine.com) & [Stride3d](https://github.com/stride3d/stride))
  - World & Entity editor
  - FlowGraph
    * Create dynamically C# source code from graph (work in progress) (use [Flowgraph](https://github.com/xcasadio/FlowGraph) & [CSharpSyntax](https://github.com/pvginkel/CSharpSyntax))
  - 2D Graphics
    - Visualization : Sprites, Animations, Tiled map
  - Asset Manager
  - WPF Controls
    * [EditableTextBlock](https://www.codeproject.com/script/Articles/ViewDownloads.aspx?aid=31592)
    * Vector, Quaternion control from [Stride3d](https://github.com/stride3d/stride)
* UI
  - Neoforce ([NeoforceControls/Neoforce-Mono](https://github.com/NeoforceControls/Neoforce-Mono))
* 2D
  - Sprites
  - Animations
  - 2d Map
* 3D
  - Models
  - Textures
  - Skinned Models & animations (from [willmotil/MonoGameAssimpModelLoader](https://github.com/willmotil/MonoGameAssimpModelLoader))
  - Lighting
* Physics
  - RigidBody
* Fonts
  - load font from true type font ([FontStashSharp](https://github.com/FontStashSharp/FontStashSharp))

## 3rd parties
* Physics : BulletSharp
* Models & animations: AssimpNET

## Getting started (Windows)
1. Download and install **Visual Studio 2022** and **NET 6**

2. Clone the repository:

```sh
git clone https://github.com/xcasadio/CasaEngineMonogame.git
```

3. Launch the editor
    * Select DebugEditor or ReleaseEditor in the configuration Manager
    * Select the EditorWpf project as startup project
    * Compile and launch

4. Launch a demo
    * Select Debug or Release in the configuration Manager
    * Select the RPGDemo or DemosGame project as startup project
    * Compile and launch

## Screenshots
![Editor](/github/screenshot_editor.jpg)
![Sprite Editor](/github/screenshot_sprite_editor.jpg)
![Demo physics 2d](/github/demo_physics_2d.jpg)
![Demo physics 3d](/github/demo_physics_3d.jpg)
