# Scaffolding du projet C# gameplay

Faire de « Nouveau projet » un vrai point de départ de jeu : l'éditeur génère un
projet Visual Studio de gameplay (`.csproj` + `.sln` + code de départ), câble les
settings, et le pipeline existant (build sur Play, hot reload, DLL canonique — voir
[play-in-editor.md](play-in-editor.md)) fonctionne sans étape manuelle.

Le déploiement se fait en deux phases :

- **Phase 1** — le csproj référence les DLL de l'installation de l'éditeur
  (modèle « Unity » pour la référence moteur, mais csproj possédé par l'utilisateur) ;
- **Phase 2** — la référence moteur migre vers des packages NuGet
  (modèle Godot 4 / Stride). La structure générée en Phase 1 est conçue pour que
  seule la référence moteur change.

## État actuel

Tout le pipeline aval existe déjà ; il manque uniquement le maillon amont :

| Maillon | État |
| --- | --- |
| `CreateProject` (`EditorProjectAuthoringService`) | génère `.json` projet + `DefaultWorld` + catalogue d'assets — **aucun csproj, aucun code** |
| `ProjectSettings.GameplayCsprojName` / `GameplayDllName` | existent, mais renseignés à la main |
| Build hors process (`EditorScriptBuildService`) | `dotnet build` vers `.casaeditor/script-build/<timestamp>/` |
| Rebuild sur Play + hot reload (`EditorScriptReloadCoordinator`) | opérationnel (ALC collectible, shadow copy, DLL canonique) |
| Chargement runtime (`AssemblyManager` → `IPlugin`) | opérationnel |

Aujourd'hui, le csproj de gameplay est écrit à la main (ex. `Projects/CasaEngine.RPGDemo`)
avec un `ProjectReference` vers les sources du moteur — impossible hors du repo moteur.

## Positionnement par rapport aux moteurs modernes

| Moteur | csproj | Source de vérité | Référence moteur |
| --- | --- | --- | --- |
| Unity | régénéré en permanence, jetable | pipeline interne (asmdef) | DLL de l'installation |
| Godot 4 (C#) | généré une fois, **possédé par l'utilisateur** | le csproj | SDK NuGet `Godot.NET.Sdk` |
| Stride | solution .NET ordinaire | le csproj | packages NuGet |
| Flax | généré par Flax.Build | modules Flax | DLL + build tool |

Choix CasaEngine : le modèle Godot/Stride — **le csproj appartient à l'utilisateur et
n'est jamais régénéré ni écrasé** ; la compilation reste un `dotnet build` standard
(déjà le cas). Seule la *localisation du moteur* est un fichier machine-local géré
par l'éditeur (Phase 1), destiné à disparaître au profit de NuGet (Phase 2).

## Principes de conception

1. **Généré une fois, possédé par l'utilisateur** : `.csproj`, `.sln` et sources de
   départ ne sont créés que par `CreateProject`. L'éditeur ne les modifie jamais après.
2. **La référence moteur est isolée** dans un fichier props dédié, machine-local,
   gitignoré, régénérable par l'éditeur. C'est le seul fichier que l'éditeur a le
   droit de réécrire — et le seul qui change en Phase 2.
3. **Aucun nouveau mécanisme de build** : le scaffolding produit exactement ce que
   `EditorScriptBuildService` / `EditorScriptReloadCoordinator` consomment déjà.
4. **Fail-soft** : un projet sans csproj gameplay (projets existants, assets purs)
   reste valide ; le scaffolding est le défaut de `CreateProject`, pas une obligation.

## Phase 1 — référence aux DLL de l'éditeur

### Structure générée

```text
MonProjet/
├── MonProjet.json                      (existant — settings câblés, voir plus bas)
├── DefaultWorld.world                  (existant)
├── AssetInfos.json                     (existant)
├── MonProjet.sln                       (généré une fois, possédé par l'utilisateur)
└── Gameplay/
    ├── MonProjet.Gameplay.csproj       (généré une fois, possédé par l'utilisateur)
    ├── CasaEngine.EnginePath.props     (machine-local, régénérable par l'éditeur)
    ├── .gitignore                      (bin/, obj/, CasaEngine.EnginePath.props)
    ├── GamePlugin.cs                   (IPlugin minimal)
    └── Scripts/
        └── SampleProxy.cs              (GameplayProxy d'exemple)
```

### `MonProjet.Gameplay.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="CasaEngine.EnginePath.props" Condition="Exists('CasaEngine.EnginePath.props')" />
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <OutputType>Library</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>MonProjet.Gameplay</AssemblyName>
  </PropertyGroup>
  <Target Name="EnsureCasaEnginePath" BeforeTargets="ResolveAssemblyReferences"
          Condition="'$(CasaEnginePath)' == ''">
    <Error Text="CasaEnginePath est introuvable : ouvrez le projet dans l'éditeur CasaEngine (le fichier CasaEngine.EnginePath.props sera régénéré) ou définissez la variable d'environnement CASAENGINE_PATH." />
  </Target>
  <ItemGroup>
    <Reference Include="CasaEngine" Private="false"
               HintPath="$(CasaEnginePath)\CasaEngine.dll" />
    <Reference Include="MonoGame.Framework" Private="false"
               HintPath="$(CasaEnginePath)\MonoGame.Framework.dll" />
    <Reference Include="Newtonsoft.Json" Private="false"
               HintPath="$(CasaEnginePath)\Newtonsoft.Json.dll" />
  </ItemGroup>
</Project>
```

Décisions :

- **TFM explicite** `net9.0-windows` (aligné sur `$(WindowsTargetFramework)` du repo,
  `DotNetVersion` 9.0) : le projet généré vit hors du repo moteur et n'hérite pas de
  son `Directory.Build.props`. Le scaffolding écrit le TFM courant du moteur.
- **Références minimales** : `CasaEngine` (API gameplay), `MonoGame.Framework`
  (`Vector3`, `GameTime`…), `Newtonsoft.Json` (`JObject` dans
  `GameplayProxy.Save/Load`). L'utilisateur ajoute `MGUI.Core` ou autres DLL du
  dossier éditeur à la main s'il en a besoin.
- **`Private="false"`** : les DLL moteur ne sont pas copiées dans `bin/`. Inutile au
  chargement — `ScriptAssemblyHost.ScriptLoadContext` résout de toute façon les
  assemblies partagées vers le contexte par défaut — et évite des copies périmées.
- **Pas de `MonoGame.Content.Builder.Task`** : le projet gameplay ne construit pas de
  content ; les assets passent par le pipeline CasaEngine.

### `CasaEngine.EnginePath.props` (machine-local)

```xml
<!-- Généré par l'éditeur CasaEngine. Machine-local : ne pas committer, ne pas éditer. -->
<Project>
  <PropertyGroup>
    <CasaEnginePath Condition="'$(CASAENGINE_PATH)' != ''">$(CASAENGINE_PATH)</CasaEnginePath>
    <CasaEnginePath Condition="'$(CasaEnginePath)' == ''">C:\chemin\vers\CasaEngine.Editor</CasaEnginePath>
  </PropertyGroup>
</Project>
```

- Le chemin en dur est celui de l'installation de l'éditeur au moment de la
  génération (`AppContext.BaseDirectory`).
- La variable d'environnement `CASAENGINE_PATH` a priorité : CI, machine sans
  éditeur, installation déplacée.
- **Régénération à l'ouverture du projet** : si le fichier manque ou si son chemin ne
  contient plus `CasaEngine.dll`, l'éditeur le réécrit (log info). C'est ce qui rend
  le projet portable d'une machine à l'autre — et c'est le *seul* fichier régénéré.

### `MonProjet.sln`

Généré une fois par template texte (format 12.00, un seul projet, GUID stable tiré au
scaffolding) — déterministe et hors ligne, pas de dépendance à `dotnet new`. Permet le
double-clic dans Visual Studio / Rider.

### Code de départ

`GamePlugin.cs` — le point d'entrée que `AssemblyManager.Load` recherche :

```csharp
using CasaEngine.Engine.Plugins;

namespace MonProjet.Gameplay;

public class GamePlugin : IPlugin
{
    public void Initialize()
    {
        // Enregistrements globaux du jeu.
    }
}
```

`Scripts/SampleProxy.cs` — un `GameplayProxy` d'exemple (overrides vides + `Clone`),
prêt à être assigné à une entité via `script_class_name`. Les types sont résolus par
`ElementFactory` (`RegisterScriptAssembly` côté éditeur, scan AppDomain côté runtime).

### Câblage des settings et première compilation

À la fin de `CreateProject` :

- `GameplayCsprojName = "Gameplay/MonProjet.Gameplay.csproj"` ;
- `GameplayDllName = "MonProjet.Gameplay.dll"` (DLL canonique à la racine du projet,
  entretenue par `RefreshCanonicalGameplayDll`) ;
- **premier build immédiat** via `EditorScriptReloadCoordinator.TryRebuildAndReload`
  (fail-soft : en cas d'échec — pas de SDK .NET, etc. — le projet reste utilisable,
  l'erreur part dans le panneau Logs, le Play suivant retentera).

Sans ce premier build, `AssemblyManager` loguerait « Gameplay assembly not found » à
chaque ouverture tant que l'utilisateur n'a pas joué une première fois.

### Impacts sur l'existant

- `ContentBrowserConfig.ExcludedDirectories` (`bin`, `obj`, `.git`) : ajouter
  `.casaeditor` et `.vs`. Les dossiers `Gameplay/bin` et `Gameplay/obj` sont déjà
  couverts (exclusion par nom de dossier).
- Les projets existants (`RPGDemo`, `SandBoxGame`) ne changent pas : le
  `ProjectReference` vers les sources du moteur reste un mode supporté pour le
  développement dans le repo. Leur étape `PostBuild` de copie devient redondante avec
  `RefreshCanonicalGameplayDll` pour les flux éditeur, mais reste utile pour un build
  VS pur.
- Le Launcher (runtime standalone) ne change pas : il charge `GameplayDllName`
  directement (`Assembly.LoadFile`).

## Phase 2 — migration NuGet (direction)

Objectif : remplacer le props machine-local par une référence versionnée, comme
Godot (`Godot.NET.Sdk`) et Stride.

Ce qui change dans le projet généré — et **uniquement** cela :

```xml
<ItemGroup>
  <PackageReference Include="CasaEngine" Version="x.y.z" />
</ItemGroup>
```

(l'`Import` du props, la cible `EnsureCasaEnginePath` et les trois `Reference`
disparaissent ; `MonoGame.Framework` et `Newtonsoft.Json` arrivent transitivement).
À terme, un vrai SDK MSBuild `CasaEngine.Sdk` peut masquer le boilerplate restant.

Prérequis à instruire avant de lancer la phase :

1. **Packaging** : publier `CasaEngine` + les projets MGUI référencés
   (`MGUI.Core`, `MGUI.Shared`, `MGUI.FontStashSharp`) en packages, avec leurs
   dépendances (`MonoGame.Framework.DesktopGL`, `BepuPhysics`, `Newtonsoft.Json`,
   `SharpGLTF`, `YarnSpinner`) déclarées.
2. **Natifs** : `BulletSharp.dll` (DLL locale `ThirdParties/`) à empaqueter en
   `runtimes/win-x64/native` ou équivalent.
3. **Content moteur** (shaders `CasaEngine.Shaders`) : décider comment il est livré —
   il accompagne l'éditeur/runtime, pas la compilation du gameplay, donc
   vraisemblablement hors package gameplay.
4. **Alignement de versions éditeur ↔ package** : l'éditeur charge la DLL gameplay
   dans son propre process ; `ScriptLoadContext` résout les assemblies partagées *par
   nom simple* vers le contexte par défaut. Une DLL compilée contre CasaEngine
   `x.y+1` chargée dans un éditeur `x.y` casserait tard (`MissingMethodException`).
   Prévoir un contrôle de version au chargement (attribut de version moteur stampé
   dans la DLL, vérifié par `AssemblyManager`/`EditorScriptAssemblyService`).
5. **Feed** : nuget.org ou feed local/GitHub Packages pour commencer.

## Découpage en tâches (Phase 1)

> **État (2026-08-19)** : tâches 1 à 3 implémentées et vérifiées (25 tests ciblés
> verts, dont la compilation réelle d'un projet scaffoldé contre les DLL de
> l'installation ; aucune régression sur la suite complète). La compilation initiale
> de `CreateProject` passe par `EditorScriptReloadCoordinator.TryBuildCanonicalDll`
> (build + refresh de la DLL canonique **sans** chargement d'assembly, pour ne jamais
> verrouiller le fichier via le loader par défaut). Reste la tâche 4 manuelle (VS +
> Play + Launcher sur un projet hors repo).

1. **Service de scaffolding** (`CasaEngine.EditorServices`, par ex.
   `EditorGameplayProjectScaffolder`) : génération csproj / props / sln / sources /
   .gitignore à partir de templates ; tests avec golden files (mêmes patterns que
   `EditorScriptBuildServiceTests`).
2. **Intégration `CreateProject`** : appel du scaffolder, câblage
   `GameplayCsprojName`/`GameplayDllName`, premier build fail-soft, événements projet.
3. **Régénération du props à l'ouverture** (`LoadProject`) : détection chemin
   invalide, réécriture, log. Exclusions content browser (`.casaeditor`, `.vs`).
4. **Validation de bout en bout** : créer un projet hors du repo moteur → ouvrir le
   `.sln` dans VS (build OK) → Play dans l'éditeur (build + hot reload OK) →
   Launcher sur le projet (chargement `IPlugin` OK). Le scénario d'automation
   `--play-smoke` sert de filet.

Chaque tâche est livrable et testable indépendamment ; la 1 et la 3 sont pures
EditorServices (testables sans UI).

## Risques et points ouverts

- **Chemin éditeur périmé** dans le props : couvert par la régénération à l'ouverture,
  mais un build VS *hors éditeur* après déplacement de l'installation échoue avec le
  message de la cible `EnsureCasaEnginePath` (comportement voulu, à documenter).
- **Identité de types** : en Phase 1, DLL gameplay et éditeur proviennent de la même
  installation — risque faible. Le contrôle de version (point 4 de la Phase 2) peut
  être anticipé si des installations multiples cohabitent.
- **Dépendances NuGet propres au gameplay** : elles sont présentes dans le dossier de
  build `.casaeditor/script-build/<timestamp>/` (résolues via `deps.json` par
  `AssemblyDependencyResolver`), mais `RefreshCanonicalGameplayDll` ne copie que
  `dll` + `pdb` : le Launcher ne les verrait pas à la racine du projet. Point à
  traiter quand le besoin apparaîtra (copier les dépendances non-moteur à côté de la
  DLL canonique).
- **Templates texte vs API MSBuild** : la génération actuelle par templates string
  est volontaire (contenu fixe généré une fois, déterministe, zéro dépendance —
  `Microsoft.Build.Construction.SolutionFile` ne sait d'ailleurs que *lire* les
  `.sln`). Critères de bascule : si l'éditeur doit un jour **modifier le csproj de
  l'utilisateur** (ajouter une référence/un package depuis l'UI), passer à
  `Microsoft.Build.Construction.ProjectRootElement` (+ `Microsoft.Build.Locator`
  pour se lier au MSBuild du SDK installé) — jamais de manipulation de texte sur un
  fichier possédé par l'utilisateur ; si le `.sln` généré doit accueillir
  **plusieurs projets**, utiliser `Microsoft.VisualStudio.SolutionPersistence`
  (l'API d'écriture `.sln`/`.slnx` qui motorise `dotnet sln` depuis .NET 9).
- **Chemin `buildGameplayProject: true` non couvert par un test** : les tests de
  `CreateProject` sautent la compilation initiale (~10-30 s) ; le fail-soft du premier
  build est vérifié par inspection, pas par exécution. Un test dédié (source invalide
  volontaire, assertion d'absence d'exception) reste à écrire si le besoin se présente.
- **SDK .NET requis** sur la machine utilisateur : prérequis déjà existant
  (`EditorScriptBuildService`), à documenter côté installation.
- **TFM figé Windows** (`net9.0-windows`, x64) : assumé tant que le moteur est
  DesktopGL/Windows ; le multi-plateforme est hors périmètre.
- **Nom du dossier** : `Gameplay/` retenu (cohérent avec `GameplayDllName`) ;
  `Scripts/` réservé au sous-dossier des sources d'exemple.

## Points d'entrée dans le code

| Élément | Fichier |
| --- | --- |
| Création de projet | `CasaEngine.EditorServices/EditorProjectAuthoringService.cs` |
| Settings gameplay | `CasaEngine/Framework/Configuration/Project/ProjectSettings.cs` |
| Build hors process | `CasaEngine.EditorServices/Scripting/EditorScriptBuildService.cs` |
| Orchestration rebuild/reload | `CasaEngine.EditorServices/Scripting/EditorScriptReloadCoordinator.cs` |
| ALC collectible | `CasaEngine/Engine/Plugins/ScriptAssemblyHost.cs` |
| Chargement runtime (`IPlugin`) | `CasaEngine/Engine/Plugins/AssemblyManager.cs` |
| Résolution des types scripts | `CasaEngine/Framework/Assets/ElementFactory.cs` |
| Exclusions content browser | `CasaEngine.Editor/ContentBrowser/ContentBrowserConfig.cs` |
| Exemple de csproj manuel (mode in-repo) | `Projects/CasaEngine.RPGDemo/CasaEngine.RPGDemo.csproj` |
