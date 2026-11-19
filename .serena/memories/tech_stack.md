# Tech stack

- .NET 9: `Directory.Build.props` defines `$(BaseTargetFramework)`=net9.0 and `$(WindowsTargetFramework)`=net9.0-windows; csproj files use these properties, never literal TFMs.
- Central package management: versions only in `Directory.Packages.props`.
- Key packages: MonoGame.Framework.DesktopGL 3.8.5.1 (+ Content.Builder.Task), MonoGame.Extended 6.1.1, BepuPhysics 2.5.0-beta.29, SharpGLTF 1.0.6, AssimpNetter 6.0.4, FontStashSharp, YarnSpinner 3.2.1 (dialogue), Microsoft.CodeAnalysis.CSharp 5.3.0 (script compilation), Newtonsoft.Json, NVorbis, xunit 2.9.3.
- Local dotnet tools (`.config/dotnet-tools.json`): mgcb / mgcb-editor 3.8.5.1 (`dotnet tool restore`).
- `artifacts/` dirs are excluded from compilation (DefaultItemExcludes).
