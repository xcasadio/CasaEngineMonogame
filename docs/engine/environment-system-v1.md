# Environment System V1

## Vue d'ensemble

Le systeme d'environnement V1 ajoute un environnement global par world, avec une resolution par vue et un override optionnel pour les previews editor.

Source principale:
- `World.EnvironmentSettings`

Resolution runtime:
- `EnvironmentResolver.Resolve(RenderView view)`
- priorite: `view.EnvironmentOverride` -> `view.World.EnvironmentSettings` -> fallback legacy

Bindings shader:
- `EnvironmentShaderBinder`
- `Lighting.fxh`
- `LitForward.fx`

## Environnement global du world

Le world expose `WorldEnvironmentSettings` comme source d'authoring et de runtime.

Parametres V1 supportes:
- mode de fond (`LegacyClearColor`, `SolidColor`, `Environment`)
- couleur de fond fallback
- `EnvironmentAssetId`
- cubemap de fond global
- cubemap specular global
- couleur ambient globale
- multiplicateur ambient
- multiplicateur specular

Le runtime peut resoudre ces donnees depuis:
- des ids d'assets (`EnvironmentAsset`, `TextureCube`)
- des `TextureCube` runtime affectees directement

Le resultat est stocke sous forme de `ResolvedEnvironmentSettings` et mis en cache par vue via `ResolvedEnvironmentCache`.

## Overrides par vue

Chaque `RenderView` peut definir `EnvironmentOverride`.

Usage V1 actuel:
- le viewport world principal de l'editor suit l'environnement du world courant
- les previews material utilisent un override dedie pour ne pas heriter visuellement de l'environnement du world principal

Implementation concrete:
- `PreviewEnvironmentFactory.CreateNeutralPreview(...)`
- `WorldViewportPanel.SetEnvironmentOverride(...)`
- `GameEditor.GetOrCreateMaterialViewportPanel(...)` (`CasaEngine.Editor/GameEditor.cs`)

Le smoke editor existant rapporte l'etat d'override du material preview dans `editor-material-preview-smoke.txt`, a la racine du depot (derniere capture : 2026-04-05).

## Authoring editor

L'editor supporte maintenant:
- selection du `WorldRoot` dans la hierarchie
- propagation `World` / `WorldEntity` / `WorldComponent` dans `EditorSelection`
- inspecteur world pour les parametres d'environnement V1
- undo/redo et dirty state via l'historique editor world
- persistance des parametres dans le serializer du world editor

## Demo de validation

`EnvironmentShowcaseDemo` fournit un cas de validation minimal dans `CasaEngine.Demos`.

Objectif:
- montrer qu'un cubemap global peut piloter la contribution ambient/specular
- montrer que le fond visible peut etre bascule independamment du lighting

Controles:
- `B` : fond cubemap <-> fond couleur unie
- `E` : change de preset de cubemap
- `L` : active/coupe la contribution ambient/specular de l'environnement

## Limites de la V1

Ce qui est volontairement hors scope:
- pas d'entree panorama HDR
- pas de reflection probes locales
- pas de blending local interieur / exterieur
- pas de ciel procedural
- pas d'atmosphere physique
- pas de pipeline de convolution IBL complet (irradiance/prefilter/BRDF LUT)

Limites techniques assumees:
- le diffuse environment actuel echantillonne directement le cubemap global par normale monde
- le specular environment reste une premiere passe simple
- l'environnement continue d'alimenter l'ambient et le specular globaux, mais la lumiere directe forward vient des lumieres collectees du world via `LightingContext` et `ForwardLightBinder`

Les chantiers V2 correspondants restent dans le plan sous `ENV-022` a `ENV-026`.