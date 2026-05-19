# Shell tools available

The repository is developed on Windows.

Available tools:
- `rg` / ripgrep is installed and should be preferred for fast code search.
- `rtk` is installed and should be used when command output may be large or noisy.
- `fd` is installed and must be preferred for file discovery.
- `jq` is installed and must be used for JSON inspection.
- `yq` is installed and must be used for YAML/XML/INI/CSV inspection.
- `ast-grep` is installed and should be used for structural code search when plain text search would be too noisy.

Usage rules:
- Prefer `rg "pattern" .` for precise code search.
- Prefer `rtk rg "pattern" .` when the search may return a lot of output.
- Prefer `rtk git status`, `rtk git diff`, and `rtk git log` for Git commands.
- If `rtk` fails or is unavailable, fall back to the normal command.
- If `rg` fails or is unavailable, fall back to VS Code search or PowerShell search.
- Never run broad recursive listing commands like `dir /s`, `tree /f`, or unfiltered `Get-ChildItem -Recurse`.
- Prefer `fd` with extension and depth filters.
- Prefer `rg` with path, glob, context, and file-type filters.
- Prefer `jq`/`yq` to extract only relevant fields from structured files.

At the start of a task, the agent may verify tools with:
- `rg --version`
- `rtk --version`
- `rtk gain`
- `fd --version`
- `jq --version`
- `yq --version`
- `ast-grep --version`

# RTK usage rules

Before running shell commands, prefer RTK-wrapped commands to reduce context noise.

Use:
- `rtk git status` instead of `git status`
- `rtk git diff` instead of `git diff`
- `rtk git log -n 20` instead of `git log -n 20`
- `rtk grep "pattern" .` instead of `rg "pattern" .` when the output may be large
- `rtk test <command>` for verbose test commands
- `rtk dotnet test` or `rtk test "dotnet test"` for .NET test output if supported

At the start of a task, verify RTK is available with:
- `rtk --version`
- `rtk gain`

If RTK is not available, fall back to normal commands.

# Copilot Instructions — CasaEngineMonogame (C# / MonoGame)

Tu codes en C# dans un moteur MonoGame orienté éditeur + runtime.

## Priorités
1) Correctness (input/layout/render)  
2) Stabilité API (compat > breaking)  
3) Perf (hot path)  
4) Lisibilité (code clair, noms explicites)  
5) Démo / sample (quand utile)

## Perf (règles strictes)
- Pas de LINQ / closures / allocations dans Update/Draw.
- Pas de nouvelles Lists/Dictionaries par frame : réutiliser + Clear.
- Éviter string formatting dans Draw (pré-calculer/cacher).
- Batching SpriteBatch : minimiser Begin/End.
- Si clipping : stack (Push/Pop) et **restaurer** l’état GraphicsDevice.

## Layout / UI
- Toute modif de propriété impactant la taille/position doit invalider le layout proprement.
- Hit-test déterministe : z-order + visiblité + enabled + clipping.
- Input : capture souris pour drag, focus clavier unique, navigation tab si applicable.

## Shaders / Rendering
- Séparer : données (materials/meshes) / pipeline (passes) / backend (GraphicsDevice).
- Éviter les “state leaks” : RasterizerState/BlendState/DepthStencilState.
- Préférer des structures de passes (ForwardPass, GBufferPass, LightingPass…).
- Prévoir fallback (matériel sans feature, shader manquant, etc.)

## Physique
- Viser une abstraction stable (interfaces) + adaptateurs par backend.
- Ajouter debug draw et synchronisation transform claire (qui drive quoi ?).

## Commits
- 1 commit par sous-tâche, message explicite.
- Toujours laisser le build dans un état OK.

## Documentation
- Si API publique : doc courte + snippet dans README/docs.
- Si feature “éditeur” : au moins un sample / écran de démo.
