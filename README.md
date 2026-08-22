# CasaEngineMonogame
Editor + game engine written in C# .Net 9 with MonoGame.

## Objective:
I developed this engine with the aim of migrating all my game projects to the same engine.

## Contributors
You are welcome, you can contact me with [GitHub Discussions](https://github.com/xcasadio/CasaEngineMonogame/discussions).

## Features
* Editor (written with MGUI)
  - World & Entity editor
  - 2D Graphics
    - Visualization : Sprites, Animations, Tiled map
  - Asset Manager
* UI
  - [MGUI](https://github.com/xcasadio/MGUI)
* 2D
  - Sprites
  - Animations
  - Tiled Map
* 3D
  - Materials
  - Static & Skinned Models (from [willmotil/MonoGameAssimpModelLoader](https://github.com/willmotil/MonoGameAssimpModelLoader))
  - Lighting/Shadows
* Particles
* Physics
  - RigidBody
* Fonts
  - load font from true type font ([FontStashSharp](https://github.com/FontStashSharp/FontStashSharp))

## 3rd parties
* Physics : [bepuphysics2](https://github.com/bepu/bepuphysics2) (2.5.0-beta.29)
* Models & animations: AssimpNET

## Getting started (Windows)
1. Download and install **Visual Studio 2022** and **NET 9**

2. Clone the repository:

```sh
git clone https://github.com/xcasadio/CasaEngineMonogame.git
```

3. Launch the editor
    * Select the CasaEngine.Editor project as startup project
    * Compile and launch

4. Launch a demo
    * Select Debug or Release in the configuration Manager
    * Select the DemosGame project as startup project
    * Compile and launch

## Documentation
- Materials workflow: [docs/rendering/materials-workflow.md](docs/rendering/materials-workflow.md)
- Render stats validation workflow: [docs/rendering/render-stats-demo-workflow.md](docs/rendering/render-stats-demo-workflow.md)

## Screenshots
![Editor](/github/screenshot_editor.jpg)
![Sprite Editor](/github/screenshot_sprite_editor.jpg)
![Demo physics 2d](/github/demo_physics_2d.jpg)
![Demo physics 3d](/github/demo_physics_3d.jpg)
