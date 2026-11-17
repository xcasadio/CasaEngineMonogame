# Plan agent IA — Mipmaps réglés par l'asset `.texture`

Plan d'exécution de la tranche S1m du chantier du jeu RacingGame (`ai-agent/tasks/rgce-pbr-handover-tasks.md` dans le dépôt du jeu).
Les décisions D1 → D4 ci-dessous ont été arbitrées le 2026-11-17 dans le cadre du programme approuvé par l'auteur (mode AUTO) : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Un asset `.texture` peut demander que son image soit dessinée avec toute sa chaîne de mipmaps. Un jeu qui charge ses textures par identifiant, depuis des assets, obtient alors les mipmaps sans code propre.

## État vérifié du dépôt (2026-11-17)

- `Texture2DLoader.GenerateMipmaps` (ADR-0101) construit la chaîne quand il vaut vrai ; faux par défaut. Il n'est réglable qu'en code, pour tout le chargeur (`CasaEngine/Framework/Assets/Loaders/Texture2DLoader.cs:15`).
- `AssetContentManager` a un chargeur par type et un seul exemplaire partagé par identifiant (`Acquire<T>`, `AssetContentManager.cs:67-88`) : une image est chargée une fois, sans mipmaps, pour tous ses utilisateurs.
- Un `Texture` chargé depuis un asset tient l'image partagée par un handle (`Texture.Load`, ADR-0037) ; `Texture.Load(JObject)` ne lit que `sampler_state` et `texture_asset_id` (`Texture.cs`).
- Le sérialiseur de l'éditeur écrit ces deux champs (`EditorAssetJsonSerializer.SaveTexture`, `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:1064-1072`).
- `MaterialCompiler` résout les textures d'un matériau par `Acquire<Texture>` puis `Texture.Load(assetContentManager)` (`MaterialCompiler.cs:400-420`).
- `MipmapChain.UploadChain` écrit une chaîne dans une texture créée avec `mipMap: true`, au format `Color` (ADR-0101).
- Branche `chantier/texture-asset-mipmaps`, créée depuis `chantier/static-model-tangents` (D4).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | L'asset `.texture` porte un champ optionnel `generate_mipmaps` (booléen, faux s'il est absent). Changement additif : un fichier existant se charge comme avant. |
| D2 | Un `Texture` qui demande les mipmaps possède sa propre texture GPU à chaîne complète, construite au chargement à partir des pixels de l'image partagée ; l'image partagée n'est pas modifiée et reste sans mipmaps pour ses autres utilisateurs. Le handle sur l'image partagée est rendu une fois la copie faite. |
| D3 | Après une perte du périphérique, un `Texture` à mipmaps reconstruit sa chaîne de la même façon. |
| D4 | La branche part de `chantier/static-model-tangents`, dont le jeu dépend déjà ; aucun commit sur `main`, aucun push. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/texture-asset-mipmaps`.** Ne jamais committer sur `main`. Ne jamais pousser.
- **Une seule tâche à la fois**, icône de statut tenue à jour, **un commit par tâche** qui inclut la mise à jour de ce fichier, message en anglais `type(area): summary`.
- **Ne rien inventer** : sinon ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build** `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --blame-hang-timeout 5m`.
- `git add` fichier par fichier ; aucune commande git dans `MGUI` ni `NvgSharp`.
- Rappel moteur : sérialisation additive ; aucune allocation ni calcul de chaîne par image, seulement au chargement ; restaurer l'état GPU modifié ; le runtime ne dépend pas de l'éditeur.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked.

## Validation globale

- Les deux solutions à 0 erreur.
- Tests : tous verts, à l'exception des 8 tests GPU qui échouent déjà sur ce PC (6 de `DesktopFrameGpuAllocationTests`, `DrawTransactionRearmGpuTests.RearmThenDrawingTheDesktopFrame_AllocatesNothing_AndDrawsTheReferencePixels`, `IblCubeGeneratorGpuTests.OpenGlChain_IgnoresTheRequestedCubeLevel_SoTheAtlasIsUsed`) ; total de 6382 plus les nouveaux tests.

Retour arrière : `git switch chantier/static-model-tangents` puis `git branch -D chantier/texture-asset-mipmaps`, sur demande de l'auteur seulement.

---

## Phase 1 — Asset

### ⏳ T1 — Champ `generate_mipmaps` et texture à chaîne complète

- Objectif : D1 à D3.
- Fichiers : `CasaEngine/Framework/Assets/Textures/Texture.cs` (propriété publique `GenerateMipmaps`, lecture du champ, construction de la chaîne dans `Load(AssetContentManager)`, reconstruction dans `OnDeviceReset`, libération dans `Dispose`) ; `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs` (`SaveTexture` écrit `generate_mipmaps` seulement quand il vaut vrai) ; tests dans `CasaEngine.Tests` (sérialisation à côté de `EditorServices/TextureAssetDocumentTests.cs` ; GPU à côté des tests GPU de `MipmapChain` s'il en existe, avec `GpuDeviceHost`).
- Étapes :
  1. Lecture et écriture du champ, défaut faux.
  2. `Load` : sans mipmaps, comportement inchangé. Avec : si la texture possédée existe déjà et n'est pas libérée, `Load` ne fait rien (il est appelé par chaque utilisateur d'un même `Texture` : matériaux, maillages, sprites, cartes de tuiles) ; sinon, acquérir l'image partagée, lire ses pixels de niveau 0 (format `Color` exigé, sinon erreur avec le nom de l'asset), créer une texture `mipMap: true` et y écrire la chaîne par `MipmapChain.UploadChain`, rendre le handle sur l'image partagée. `Texture.Resource` renvoie la texture à chaîne.
  3. `OnDeviceReset` et `Dispose` traitent la texture possédée.
- Tests :
  - sérialisation : sans le champ, `GenerateMipmaps` faux et le champ n'est pas réécrit ; avec, l'aller-retour le garde ;
  - GPU : un asset sans le champ donne la texture partagée à un niveau, la même instance que `Acquire<Texture2D>` ; un asset avec le champ donne une texture à `MipmapChain.GetLevelCount(w, h)` niveaux, dont le niveau 0 a les pixels de l'image et le niveau 1 ceux de `MipmapChain.ComputeNextLevel` ; l'image partagée reste à un niveau ; deux appels de `Load` sur le même `Texture` laissent `Resource` à la même instance, non libérée, et l'image partagée n'est plus tenue après le premier (elle est libérée par `CollectUnreferenced`) ; après `Dispose`, la texture possédée est libérée.
- Validation : validation globale.
- Commit : `feat(assets): let a texture asset ask for its mipmap chain`

### ⏳ T2 — Décision et documentation

- Fichiers : `docs/decisions/0124-…md` (skill `adr`, numéro suivant de l'index), `docs/decisions/README.md`, `docs/engine/texture-mipmaps.md` (section sur le champ de l'asset), `docs/editor/asset-documents.md` si sa section `.texture` décrit les champs, ce plan, `ai-agent/README.md`.
- Validation : liens vérifiés par `rg` ; docs seulement.
- Commit : `docs(adr): record the mipmap setting of the texture asset`

---

## Points ouverts

- O1 : remontée de ce chantier (et de `chantier/static-model-tangents`, sur lequel il repose) dans `main`, et push : décision de l'auteur.

## Hors périmètre

- Lire des mipmaps stockés dans un fichier (DDS).
- Un réglage de mipmaps dans le document `.texture` de l'éditeur (l'asset se règle dans le fichier).
- Les textures repeintes en code par un jeu (elles appellent `MipmapChain.UploadChain` elles-mêmes, ADR-0101).
