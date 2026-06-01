# Hot reload des fichiers shader source — Plan d'action IA

## Objectif

Ajouter un vrai workflow de hot reload pour les fichiers shader source `.fx` et `.fxh`, au meme niveau de robustesse que le hot reload material deja present.

Le resultat cible doit permettre :

- de detecter une sauvegarde de shader source cote editeur,
- de recompiler les effets concernes sans rebuild global MGCB,
- de garder le dernier shader valide en cas d'erreur de compilation,
- d'invalider proprement les caches runtime relies (`ShaderManager`, variantes, wrappers, effets charges),
- de rafraichir le preview material et les vues runtime dependantes.

## Etat actuel a garder en tete

- Le hot reload material est documente dans `ai-agent/rendering/material-hot-reload-flow.md`.
- Le save hook actuel est material-specific : il ne route pas les sauvegardes `.fx` / `.fxh`.
- `CasaEngine.Shaders/ShaderCompiler.cs` existe deja comme wrapper offline autour de `mgfxc`.
- Le runtime dispose deja de couches de cache et de routing shader (`ShaderManager`, `ShaderWrapper`, `ShaderVariantLibrary`).
- Il n'existe pas encore de graphe de dependances include -> effets racines pour savoir quels `.fx` recompiler quand un `.fxh` change.

## Contraintes obligatoires

1. Une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. Quand la tache est terminee et validee, remplacer `🚧` par `✅`.
4. Si le code est termine mais qu'une validation manque encore, utiliser `🧪`.
5. Si une tache est bloquee, utiliser `⚠️` et ajouter une note courte sous la tache.
6. Mettre a jour ce fichier dans le meme commit que la tache.
7. Faire exactement un commit compilable par tache atomique.
8. Ne pas exiger un rebuild complet du contenu pour un simple hot reload shader dans l'editeur.
9. En cas d'erreur de compilation shader, conserver le dernier `Effect` valide actif et publier les diagnostics sans casser la vue en cours.
10. Ne pas degrader le hot reload material deja existant.

## Validation minimale par tache

- `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- `dotnet build CasaEngine.Shaders/CasaEngine.Shaders.csproj -c Debug --no-restore`
- Tests cibles selon la zone, par exemple :
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~ShaderCompiler --no-restore`
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~ShaderHotReload --no-restore`
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~ShaderVariant --no-restore`

## Criteres d'acceptation finaux

- Sauver un `.fx` recharge l'effet correspondant sans redemarrage de l'editeur.
- Sauver un `.fxh` recompile tous les effets racines dependants sans rebuild global du content pipeline.
- Les erreurs `mgfxc` remontent de facon lisible dans les diagnostics editeur et n'invalident pas le dernier shader valide.
- `ShaderManager`, `ShaderVariantLibrary` et les wrappers runtime ne reutilisent pas de bytecode stale apres reload.
- Le preview material et les vues runtime se rafraichissent apres reload shader.
- Le workflow material hot reload et le workflow shader hot reload restent compatibles et distincts.

---

## Phase 1 - Cartographier les roots et les dependances shader

- ✅ **T01.01 - Cartographier les effets racines reloadables**
  Objectif :
  - Lister les fichiers `.fx` a considerer comme racines reloadables en runtime/editor.
  - Distinguer clairement les fichiers `.fx` des includes `.fxh`.
  - Identifier les chargeurs runtime/editor qui consomment ces effets.
  Fichiers probables :
  - `CasaEngine/Content/Shaders/*`
  - `CasaEngine/Framework/Rendering/Shaders/ShaderManager.cs`
  - `CasaEngine/Framework/Assets/EffectLoader.cs`
  Validation :
  - note d'audit courte versionnee ou tests statiques si utile.
  Note implementation : `CasaEngine/Framework/Rendering/Shaders/BuiltInShaderCatalog.cs` centralise les roots runtime/editor reloadables et leur mapping vers les consumers existants.
  Commit conseille :
  - `docs(shaders): map reloadable shader roots and consumers`

- ✅ **T01.02 - Introduire un index de dependances shader source**
  Objectif :
  - Construire un index source `root .fx -> includes .fxh` et l'inverse `include -> roots`.
  - Permettre de retrouver tous les effets racines a recompiler quand un include change.
  - Garder cette logique testable sans `GraphicsDevice`.
  Fichiers probables :
  - nouveau `CasaEngine.Shaders/ShaderDependencyIndex.cs`
  - tests cibles dans `CasaEngine.Tests`
  Validation :
  - tests purs sur parsing des `#include` et expansion des dependances.
  Note implementation : `CasaEngine.Shaders/ShaderDependencyIndex.cs` + `CasaEngine.Tests/Rendering/ShaderDependencyIndexTests.cs`.
  Commit conseille :
  - `feat(shaders): track source include dependencies for hot reload`

---

## Phase 2 - Router les sauvegardes `.fx` / `.fxh`

- ✅ **T02.01 - Etendre le save routing editeur aux shaders source**
  Objectif :
  - Faire remonter les sauvegardes `.fx` / `.fxh` depuis le chemin editeur deja utilise par les assets.
  - Distinguer clairement un save material d'un save shader source.
  - Eviter de surcharger `OnEditorAssetSaved(...)` avec une logique opaque.
  Fichiers probables :
  - `CasaEngine.EditorServices/EditorAssetWriterService.cs`
  - `CasaEngine.Editor/Game1.cs`
  - eventuels event args / services editor dedies
  Validation :
  - build editeur + test cible sur le routage d'evenements.
  Note implementation : un watcher dedie `CasaEngine.Editor/Runtime/EditorShaderSourceHotReloadService.cs` ecoute les changements `.fx` / `.fxh` sur les sources du moteur pour couvrir les editions VS Code sans detour par `EditorAssetWriterService`.
  Commit conseille :
  - `feat(editor): route shader source saves into reload pipeline`

- ✅ **T02.02 - Resoudre les effets impactes par un changement source**
  Objectif :
  - Pour un `.fx`, recharger uniquement ce root.
  - Pour un `.fxh`, resoudre tous les roots dependants via `ShaderDependencyIndex`.
  - Journaliser de facon lisible la liste des effets planifies pour recompilation.
  Fichiers probables :
  - nouveau service de planification hot reload shader
  - `CasaEngine.Shaders/ShaderDependencyIndex.cs`
  Validation :
  - tests cibles include -> roots.
  Note implementation : `EditorShaderSourceHotReloadService` draine les changements fichiers, resout les roots affectes via `ShaderDependencyIndex` puis journalise la recompilation/les diagnostics par root.
  Commit conseille :
  - `feat(shaders): resolve affected root effects from source changes`

---

## Phase 3 - Compiler a chaud et conserver le dernier shader valide

- ✅ **T03.01 - Introduire une compilation shader a chaud exploitable en memoire**
  Objectif :
  - Utiliser `ShaderCompiler` pour produire les bytes d'effet reloadables sans rebuild global MGCB.
  - Factoriser si necessaire pour exposer un resultat testable contenant succes, erreurs, warnings et bytes compiles.
  Fichiers probables :
  - `CasaEngine.Shaders/ShaderCompiler.cs`
  - eventuels DTO/result wrappers
  Validation :
  - build `CasaEngine.Shaders` + tests de compilation ciblee.
  Note implementation : `ShaderCompiler` localise maintenant `mgfxc` depuis `AppContext.BaseDirectory`, ce qui rend la compilation a chaud utilisable depuis l'editeur/reference tooling.
  Commit conseille :
  - `feat(shaders): compile effect sources for hot reload`

- ✅ **T03.02 - Garder le dernier effet valide sur echec de compilation**
  Objectif :
  - En cas d'echec `mgfxc`, ne pas casser la scene ou la preview en cours.
  - Conserver l'instance `Effect` precedente tant qu'aucun nouveau bytecode valide n'est disponible.
  - Publier les diagnostics dans les logs/diagnostics editor.
  Fichiers probables :
  - service hot reload shader
  - `ShaderManager` ou couche de cache equivalente
  Validation :
  - test cible sur fallback `last-known-good`.
  Note implementation : le service editeur ne pousse un reload runtime qu'apres compilation reussie ; en cas d'echec, les diagnostics sont journalises et le shader runtime precedent reste actif.
  Commit conseille :
  - `fix(shaders): preserve last valid effect on hot reload compile failure`

---

## Phase 4 - Invalider proprement les caches runtime relies

- ✅ **T04.01 - Invalider `ShaderManager` et les wrappers relies**
  Objectif :
  - Remplacer proprement l'effet charge pour un shader recompilé.
  - Eviter que des `ShaderWrapper` ou `Effect` stale restent references apres reload.
  Fichiers probables :
  - `CasaEngine/Framework/Rendering/Shaders/ShaderManager.cs`
  - `CasaEngine/Framework/Rendering/Shaders/ShaderWrapper.cs`
  Validation :
  - build solution + tests cibles sur invalidation/rechargement.
  Note implementation : `ShaderWrapper.ReplaceEffect(...)`, `CasaEngineGame.ReloadBuiltInShader(...)` et les consumers built-in remplacent les `Effect` en place sans changer l'identite des wrappers distribues.
  Commit conseille :
  - `feat(rendering): invalidate loaded shader wrappers after source reload`

- ⏳ **T04.02 - Invalider `ShaderVariantLibrary` et la selection de techniques**
  Objectif :
  - Garantir qu'un reload source ne laisse pas de variantes ou techniques stale dans le cache.
  - Reappliquer la selection de technique de facon deterministe apres recompilation.
  Fichiers probables :
  - `CasaEngine/Framework/Rendering/Shaders/ShaderVariantLibrary.cs`
  Validation :
  - tests cibles `ShaderVariant` / `Technique`.
  Commit conseille :
  - `fix(rendering): invalidate shader variant cache on source reload`

---

## Phase 5 - Rafraichir preview, materials et vues dependantes

- 🧪 **T05.01 - Rebrancher le preview material sur le hot reload shader**
  Objectif :
  - Faire en sorte que sauver `LitForward.fx`, `UnlitTexture.fx`, `skinEffect.fx` ou `Lighting.fxh` rafraichisse le preview material sans reopen manuel.
  - Garder le preview isole du runtime principal quand c'est pertinent.
  Fichiers probables :
  - `CasaEngine.Editor/Controls/MaterialPreviewViewport.cs`
  - `CasaEngine.Editor/Game1.cs`
  Validation :
  - smoke editeur cible sur material preview.
  Note implementation : les vues actives sont invalidees apres reload shader ; le branchement code est en place mais le smoke manuel preview reste a confirmer.
  Commit conseille :
  - `feat(editor): refresh material preview after shader source reload`

- ✅ **T05.02 - Rafraichir les vues runtime et les consumers de shader**
  Objectif :
  - Invalider les vues actives apres remplacement d'un shader runtime.
  - Verifier que les renderers statiques et skinned reprennent bien le nouveau bytecode.
  - Ne pas exiger un save material pour voir le changement de shader.
  Fichiers probables :
  - `CasaEngine/Framework/Game/CasaEngineGame.cs`
  - `CasaEngine/Framework/Rendering/RenderView*`
  - renderers relies
  Validation :
  - smoke runtime/editor cible.
  Note implementation : `CasaEngineGame.ReloadBuiltInShader(...)` remplace les `Effect` des consumers built-in et invalide toutes les vues actives apres application d'un nouveau bytecode.
  Commit conseille :
  - `feat(rendering): refresh active views after shader source reload`

---

## Phase 6 - Tests, diagnostics et smoke final

- ⏳ **T06.01 - Ajouter des tests bornes du hot reload shader**
  Objectif :
  - Couvrir au minimum :
    - mapping include -> roots,
    - invalidation des caches shader/variantes,
    - preservation du dernier shader valide,
    - publication des diagnostics.
  Validation :
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~ShaderHotReload --no-restore`
  Commit conseille :
  - `test(shaders): cover source hot reload dependency and cache invalidation`

- ⏳ **T06.02 - Documenter un smoke test manuel de reference**
  Objectif :
  - Decrire un scenario simple : modifier `LitForward.fx`, puis `Lighting.fxh`, verifier preview et vue runtime, puis provoquer une erreur `mgfxc` et verifier le fallback.
  - Laisser une procedure rejouable par un autre agent sans deduction implicite.
  Validation :
  - note de smoke versionnee + build solution.
  Commit conseille :
  - `docs(shaders): document manual smoke for shader source hot reload`

## Ordre recommande

1. Phase 1 pour figer les roots et dependances.
2. Phase 2 pour faire remonter les saves source.
3. Phase 3 pour compiler a chaud avec fallback last-known-good.
4. Phase 4 pour invalider les caches runtime de facon sure.
5. Phase 5 pour reconnecter preview et vues.
6. Phase 6 pour fermer la couverture et le smoke.