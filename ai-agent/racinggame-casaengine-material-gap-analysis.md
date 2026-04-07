# RacingGame -> CasaEngine - Analyse detaillee des manques materiaux legacy

## Objectif du document

Ce document formalise l'etat actuel de la reproduction des materiaux legacy `.X` de RacingGame dans CasaEngine, puis liste de facon detaillee ce qui manque encore pour atteindre une vraie parite visuelle.

Le point important est le suivant :

- l'importeur CasaEngine preserve maintenant une partie importante des metadonnees legacy,
- le runtime CasaEngine remappe deja correctement une partie des parametres vers `LitDiffuseMaterial`,
- mais la chaine de rendu statique n'a pas encore de chemin complet pour les effets avances du pipeline legacy, en particulier la reflection et les variantes pilotees par technique.

## Resume executif

Aujourd'hui, CasaEngine sait deja reproduire correctement une bonne partie des materiaux "classiques" :

- texture diffuse,
- normal map,
- teinte diffuse,
- couleur/specular power,
- alpha cutout approche par heuristique,
- ambiance legacy approximee via emissive,
- sampler `AnisotropicWrap` plus proche du rendu d'origine.

En revanche, il manque encore les briques suivantes pour parler de vraie parite :

1. un vrai chemin de rendu pour les materiaux reflechissants,
2. une utilisation runtime de `LegacyTechniqueIndex`,
3. une exploitation reelle du type `Material` multi-texture dans le renderer statique,
4. une gestion moins approximative de l'ambient legacy,
5. une suppression des heuristiques residuelles pour l'alpha cutout,
6. un flux d'import/editor qui remonte aussi les textures de reflection.

## Ce qui est deja en place

### 1. Les metadonnees legacy importantes sont preservees a l'import

Le coeur du progres realise est dans `StaticModelImporter`.

Le parseur extrait maintenant les donnees suivantes depuis les blocs `EffectInstance` des fichiers `.X` :

- `EffectFilePath`,
- `LegacyTechniqueIndex`,
- `AmbientColor`,
- `DiffuseColor`,
- `SpecularColor`,
- `SpecularPower` (`shininess`),
- `DiffuseTextureFilePath`,
- `NormalTextureFilePath`,
- `ReflectionTextureFilePath`,
- `UsesReflection`.

Points de preuve dans le code :

- `LegacyTechniqueIndex` est renseigne dans `StaticModelImporter` : `CasaEngine/CasaEngine/Framework/Assets/Loaders/StaticModelImporter.cs:338`
- `UsesReflection` est derive ensuite : `CasaEngine/CasaEngine/Framework/Assets/Loaders/StaticModelImporter.cs:377`
- la texture de reflection legacy est bien lue depuis `reflectionCubeTexture` : `CasaEngine/CasaEngine/Framework/Assets/Loaders/StaticModelImporter.cs:371`

Cela corrige le plus gros trou initial du pipeline CasaEngine : avant ce travail, le moteur importait bien une geometrie et parfois une texture, mais perdait l'intention artistique encodee dans le pipeline legacy MonoGame.

### 2. Le runtime cree deja un materiau beaucoup plus fidele qu'avant

Dans `LegacyTrackSceneFactory`, les materiaux importes ne sont plus construits comme de simples textures planes. Le moteur cree maintenant un `LitDiffuseMaterial` qui conserve une partie utile des parametres legacy :

- texture diffuse si presente,
- normal map si presente,
- `DiffuseColor` legacy,
- `SpecularColor`,
- `SpecularPower`,
- `SamplerState.AnisotropicWrap`,
- `RenderQueue.AlphaTest` et `CullNone` pour certains objets alpha-cutout,
- conversion de `AmbientColor` vers `EmissiveColor`.

Point de preuve principal : creation du materiau dans `RacingGameCasaEngine/Worlds/LegacyTrackSceneFactory.cs:1298`.

Le sampler wrap plus proche du rendu d'origine est pose ici :

- `RacingGameCasaEngine/Worlds/LegacyTrackSceneFactory.cs:1307`

L'ambient legacy est actuellement replie dans l'emissive ici :

- `RacingGameCasaEngine/Worlds/LegacyTrackSceneFactory.cs:1314`
- `RacingGameCasaEngine/Worlds/LegacyTrackSceneFactory.cs:1316`

### 3. Le shader CasaEngine supporte deja une partie utile des besoins

Le shader statique actuel (`basicEffect`) n'est pas vide fonctionnellement. Il sait deja gerer :

- une texture de base,
- un normal map,
- une couleur diffuse,
- une emissive,
- une specular color,
- une specular power,
- un alpha cutoff.

Points de preuve :

- `LitDiffuseMaterial` selectionne une variante normal map si besoin : `CasaEngine/CasaEngine/Framework/Materials/LitDiffuseMaterial.cs:27`
- `LitDiffuseMaterial` bind effectivement `NormalTexture` : `CasaEngine/CasaEngine/Framework/Materials/LitDiffuseMaterial.cs:68`
- le calcul de lumiere ajoute `EmissiveColor` dans `Lighting.fxh` : `CasaEngine/CasaEngine/Content/Shaders/Lighting.fxh:75`
- la liste des techniques disponibles dans `basicEffect.fx` se termine sur la variante normal map : `CasaEngine/CasaEngine/Content/Shaders/basicEffect.fx:481`

Conclusion : pour les assets simples a moyennement riches, le socle runtime est deja correct.

## Ce qui manque encore cote CasaEngine

### 1. La reflection cubemap est preservee dans les metadonnees, mais pas rendue

#### Etat actuel

L'importeur lit bien l'information `reflectionCubeTexture`, la stocke dans `ReflectionTextureFilePath`, et deduit un flag `UsesReflection`.

En revanche, cette information ne modifie pas encore le type de materiau runtime cree pour les meshes importes : on reste sur `LitDiffuseMaterial` dans `LegacyTrackSceneFactory`.

#### Pourquoi ce n'est pas suffisant

Un `LitDiffuseMaterial` ne sait pas reproduire un rendu de type miroir, verre, ou surface reflechissante legacy. Il sait eclairer et utiliser une normal map, mais il n'a pas de branche runtime pour une cubemap de reflection.

#### Blocages techniques identifies

- `Material` possede bien des slots multi-textures, y compris la reflection :
  - `CasaEngine/CasaEngine/Framework/Materials/Material.cs:24`
  - `CasaEngine/CasaEngine/Framework/Materials/Material.cs:33`
- mais son `Bind` runtime ne pousse aujourd'hui que la texture de base :
  - `CasaEngine/CasaEngine/Framework/Materials/Material.cs:44`
- le resolver de shader rabat tout materiau generique sur `basicEffect` :
  - `CasaEngine/CasaEngine/Framework/Rendering/Shaders/EffectiveShaderResolver.cs:59`
- le renderer statique n'enregistre actuellement que `basicEffect` et `UnlitTexture` :
  - `CasaEngine/CasaEngine/Framework/Game/Components/StaticMeshRendererComponent.cs:124`
  - `CasaEngine/CasaEngine/Framework/Game/Components/StaticMeshRendererComponent.cs:125`
- `basicEffect.fx` n'expose pas de technique reflection pour les meshes statiques ; la derniere variante declaree est la normal map :
  - `CasaEngine/CasaEngine/Content/Shaders/basicEffect.fx:481`

#### Consequence visuelle

Les objets legacy qui dependaient d'une cubemap de reflection peuvent etre importes avec la bonne metadata, mais seront rendus comme des surfaces diffuses/speculaires classiques.

#### Travail restant

- creer un materiau runtime dedie aux surfaces reflechissantes,
- creer ou brancher un shader statique reflection-aware,
- binder effectivement la cubemap de reflection,
- faire resoudre ce nouveau materiau vers son shader dedie,
- enregistrer ce shader dans le renderer statique.

### 2. `LegacyTechniqueIndex` est stocke, mais il n'est pas encore utilise

#### Etat actuel

L'importeur preserve maintenant `LegacyTechniqueIndex`, donc l'information de selection de technique du pipeline legacy n'est plus perdue.

#### Ce qui manque

Cette valeur ne pilote encore aucune decision runtime dans CasaEngine. Aujourd'hui, la selection de technique dans `LitDiffuseMaterial` ne depend que de la presence d'une texture de base, d'une normal map et du nombre de lumiere(s) actives.

Points de preuve :

- technique legacy preservee : `CasaEngine/CasaEngine/Framework/Assets/Loaders/StaticModelImporter.cs:338`
- selection de technique actuelle de `LitDiffuseMaterial` : `CasaEngine/CasaEngine/Framework/Materials/LitDiffuseMaterial.cs:27`

#### Consequence

Deux materiaux legacy ayant des techniques differents peuvent aujourd'hui converger vers le meme rendu CasaEngine si leurs textures et leurs couleurs se ressemblent, alors que le pipeline d'origine distinguait explicitement leurs comportements.

#### Travail restant

- definir une table de mapping `LegacyTechniqueIndex -> comportement runtime`,
- distinguer au minimum :
  - materiau diffuse/specular classique,
  - materiau avec normal map,
  - materiau reflechissant,
  - materiau potentiellement special-case si certains indices legacy portent une signification forte,
- utiliser ce mapping soit pour choisir un type de `MaterialBase`, soit pour choisir un shader/variant specifique.

### 3. La voie `Material` generique est incomplete pour le rendu statique

#### Etat actuel

CasaEngine dispose deja d'un type `Material` plus riche que `LitDiffuseMaterial`. Ce type porte plusieurs slots :

- base color,
- opacity,
- normal,
- specular,
- roughness,
- tangent,
- height,
- reflection.

Mais cette richesse n'est pas encore exploitee dans le chemin de rendu statique courant.

#### Limite exacte

Le `Bind` de `Material` ne transmet aujourd'hui au shader que la base color. Il ne bind ni la normal map, ni l'opacity, ni la specular texture, ni la reflection.

Point de preuve :

- `CasaEngine/CasaEngine/Framework/Materials/Material.cs:44`

#### Consequence

Meme si l'editeur ou le pipeline authoring commence a fabriquer des `Material` plus riches, les meshes statiques ne profiteront pas automatiquement de ces donnees tant que le renderer et le shader n'en font rien.

#### Travail restant

- etendre `Material.Bind`,
- exposer les flags/features associes,
- definir une resolution de shader specifique pour ces materiaux,
- verifier le comportement dans le draw path statique standard.

### 4. L'ambient legacy est encore une approximation via emissive

#### Etat actuel

La reproduction actuelle convertit `AmbientColor` en `EmissiveColor` pour retomber sur le modele d'eclairage actuellement disponible dans CasaEngine.

Points de preuve :

- conversion dans `LegacyTrackSceneFactory` :
  - `RacingGameCasaEngine/Worlds/LegacyTrackSceneFactory.cs:1314`
  - `RacingGameCasaEngine/Worlds/LegacyTrackSceneFactory.cs:1316`
- consommation dans le shader :
  - `CasaEngine/CasaEngine/Content/Shaders/Lighting.fxh:75`

#### Pourquoi c'est seulement une approximation

L'emissive ajoute une contribution lumineuse constante. Ce n'est pas equivalent a un vrai terme d'ambient/environnement pilote par le modele d'eclairage legacy. Visuellement, cela marche assez bien pour beaucoup d'objets, mais ce n'est pas mathematiquement equivalent.

#### Consequence

On peut encore observer des divergences sur :

- les objets fortement dependants de l'ambient legacy,
- les scenes dont l'equilibre lumineux varie davantage,
- les assets speciaux dont le rendu attendait une reponse plus nuancee que "diffuse + emissive".

#### Travail restant

- soit accepter ce compromis pour les assets non critiques,
- soit ajouter un vrai terme d'ambient/environnement dans le shader statique cible,
- soit introduire une interpretation plus fine de l'ambient legacy selon le type de materiau.

### 5. L'alpha cutout reste encore pilote par heuristiques

#### Etat actuel

Le rendu alpha cutout fonctionne deja raisonnablement sur la vegetation et certains objets alpha. Mais la decision repose encore sur des heuristiques de nommage.

Point de preuve :

- la decision passe par `ShouldUseAlphaCutout` : `RacingGameCasaEngine/Worlds/LegacyTrackSceneFactory.cs:1334`

La logique regarde notamment :

- le prefixe du modele (`Alpha...`),
- certains fragments du nom de texture (`Palm`, `Leave`, `Ast`).

#### Pourquoi c'est fragile

Cette logique marche sur les assets actuellement observes, mais elle reste inferentielle. Elle ne repose pas sur une information explicite issue du pipeline legacy.

#### Consequence

- risque de faux positifs,
- risque de faux negatifs,
- maintenance fragile si de nouveaux assets ou de nouveaux noms arrivent.

#### Travail restant

- trouver un signal legacy explicite pour les materiaux cutout,
- ou bien formaliser une metadata d'import derivable une fois puis persistante,
- et sortir le runtime des heuristiques de nommage.

### 6. Le flux d'import des textures n'inclut pas encore la reflection

#### Etat actuel

`GetTextureFilePaths()` dans `StaticModelImporter` remonte bien les textures diffuses et normal maps pour l'import associe, mais pas la texture de reflection.

Points de preuve :

- fonction : `CasaEngine/CasaEngine/Framework/Assets/Loaders/StaticModelImporter.cs:105`
- la derniere categorie remontee explicitement est la normal map : `CasaEngine/CasaEngine/Framework/Assets/Loaders/StaticModelImporter.cs:125`

#### Consequence

On peut avoir une metadata de reflection preservee cote model import, mais ne pas avoir de flux editor/content symetrique qui importe aussi automatiquement la cubemap associee.

#### Travail restant

- inclure `ReflectionTextureFilePath` dans la liste des textures a importer,
- verifier le traitement des formats utilises par les cubemaps legacy (`.dds`),
- valider le flux editor jusqu'a l'asset exploitable par le runtime.

### 7. Certains parametres legacy secondaires ne sont pas encore exploites

Le pipeline legacy historique pouvait transporter plus d'intention que ce qui est rendu aujourd'hui. Par exemple, il existait aussi des references du type `NormalizeCubeTexture` dans les assets legacy, mais la voie CasaEngine actuelle ne les exploite pas encore.

Cela ne bloque pas la majorite des objets, mais c'est un rappel utile : meme apres ajout d'un chemin reflection, il pourra rester une couche de parite fine a traiter si certains assets utilisaient un shader legacy plus specifique.

## Ce que cela veut dire en pratique

### Ce qui est deja "assez bon"

CasaEngine est maintenant proche du bon rendu pour :

- panneaux et elements simples textures,
- vegetation alpha-cutout classique,
- objets avec normal map simple,
- objets speculaires sans reflection complexe,
- assets dont le rendu legacy se resumait essentiellement a `diffuse + specular + normal + ambient`.

### Ce qui restera faux tant qu'on ne va pas plus loin

CasaEngine ne pourra pas encore reproduire exactement :

- les materiaux relies a une cubemap de reflection,
- les comportements qui dependaient vraiment de la technique legacy choisie,
- les surfaces qui attendaient autre chose qu'une simple translation de l'ambient en emissive,
- les cas ou l'alpha/cutout doit etre determine par metadata plutot que par convention de nommage.

## Priorites recommandees

### Priorite 1 - Reflection runtime reelle

Faire en sorte que `UsesReflection` et `ReflectionTextureFilePath` debouchent sur un vrai rendu reflection-aware.

Objectif concret :

- nouveau type de materiau runtime ou extension du systeme actuel,
- cubemap bindee au shader,
- shader statique supportant la reflection,
- branchement dans `EffectiveShaderResolver` et `StaticMeshRendererComponent`.

### Priorite 2 - Mapping explicite de `LegacyTechniqueIndex`

Transformer l'information importee en comportement runtime stable.

Objectif concret :

- table de mapping documentee,
- choix centralise du materiau ou du shader,
- fin des approximations basees uniquement sur la presence des textures.

### Priorite 3 - Completer la voie `Material` multi-texture

Eviter d'avoir un type riche cote donnees mais pauvre cote rendu.

Objectif concret :

- bind des textures supplementaires,
- features coherentes,
- compatibilite avec le renderer statique.

### Priorite 4 - Sortir des heuristiques sur l'alpha

Fiabiliser le comportement sur la duree.

Objectif concret :

- metadata explicite,
- persistance a l'import,
- decision runtime deterministic et non basee sur les noms.

## Etat de validation actuel

Au moment de cette analyse :

- le build runtime cible est repasse au vert apres les changements du pipeline d'import/material mapping,
- un harness borne a valide les cas reels `AlphaPalm.X` et `Sign.X`,
- `CasaEngine.Tests` reste bloque par des erreurs pre-existantes hors de cette sous-tache,
- la passe materiau n'est pas encore committee dans l'etat local actuel.

## Conclusion

Le travail le plus important a deja ete fait : CasaEngine ne perd plus aveuglement l'intention des materiaux legacy a l'import. En revanche, le moteur ne consomme pas encore toute cette information dans sa chaine de rendu statique.

Autrement dit :

- le probleme n'est plus principalement un probleme d'import de textures,
- le probleme restant est surtout un probleme d'architecture runtime material/shader,
- la vraie etape suivante est la mise en place d'un chemin de rendu reflection/technique-aware pour les meshes statiques.

Tant que cette couche n'existe pas, CasaEngine restera tres proche du rendu legacy sur les cas simples, mais partiellement faux sur les materiaux les plus riches.

## Analyse critique approfondie du document actuel

Le document actuel est globalement juste sur le diagnostic fonctionnel, mais il sous-estime encore plusieurs sujets d'architecture importants si l'objectif est de garder CasaEngine generique, modulable et moderne.

### 1. CasaEngine possede deja une architecture material plus moderne que ce que le document laisse voir

Le moteur ne repose pas uniquement sur `MaterialBase` et quelques classes runtime simples.

Il existe deja une vraie chaine authoring/runtime :

- `MaterialDefinitionRegistry` decrit des schemas de materials : `CasaEngine/CasaEngine/Framework/Materials/MaterialDefinitionRegistry.cs:7`
- `MaterialAsset` porte l'authoring editable : `CasaEngine/CasaEngine/Framework/Materials/MaterialAsset.cs`
- `MaterialCompiler` compile un `MaterialAsset` vers :
  - un `CompiledMaterial`,
  - un `MaterialBase` runtime : `CasaEngine/CasaEngine/Framework/Materials/MaterialCompiler.cs:138`
- `MaterialCache` stocke a la fois la version compilee et la version runtime : `CasaEngine/CasaEngine/Framework/Materials/MaterialCache.cs:7`

Autrement dit, CasaEngine a deja commence a sortir d'un systeme de materiaux purement ad hoc. C'est un point fort reel du moteur et il faut capitaliser dessus, pas le contourner.

### 2. En revanche, cette architecture reste inachevee au moment ou elle touche le renderer

Le point faible principal n'est pas seulement "il manque la reflection".

Le point faible principal est plutot :

- la compilation authoring/runtime devient plus moderne,
- mais le renderer continue encore a raisonner surtout en termes de classes runtime concretes (`LitDiffuseMaterial`, `UnlitTextureMaterial`, `Material`).

Symptomes concrets :

- `EffectiveShaderResolver` route encore par `switch` sur les types runtime : `CasaEngine/CasaEngine/Framework/Rendering/Shaders/EffectiveShaderResolver.cs:55`
- `RenderFeatureResolver` detecte encore les features via des checks de type hardcodes : `CasaEngine/CasaEngine/Framework/Rendering/Shaders/RenderFeatureResolver.cs:132`
- `MaterialCompiler` compile bien un `legacy-multi-texture`, mais le runtime final reste un objet `Material` binde comme un materiau pauvre : `CasaEngine/CasaEngine/Framework/Materials/MaterialCompiler.cs:185`
- `Material` expose huit slots textures, mais son `Bind` n'en pousse effectivement qu'un seul au shader courant : `CasaEngine/CasaEngine/Framework/Materials/Material.cs:44`

Conclusion :

- l'architecture authoring est en avance sur l'architecture de rendu,
- et c'est ce decalage qui produit aujourd'hui la plupart des limitations observees.

### 3. Le moteur a deja des points d'extension utiles pour moderniser sans tout casser

Le renderer n'est pas completement fige. Il y a deja :

- une resolution de shader effective : `EffectiveShaderResolver`
- une resolution centralisee des features : `RenderFeatureResolver`
- une bibliotheque de variantes : `ShaderVariantLibrary`
- un selecteur de shader runtime : `RenderShaderSelector`
- une separation en passes de rendu : `RenderPass`

Points de preuve :

- `StaticMeshRendererComponent` construit des `RenderItem`, resout shader + features puis delegue au pipeline : `CasaEngine/CasaEngine/Framework/Game/Components/StaticMeshRendererComponent.cs:197`
- seuls deux shaders built-in sont aujourd'hui enregistres dans ce composant : `CasaEngine/CasaEngine/Framework/Game/Components/StaticMeshRendererComponent.cs:124`
- la selection de technique peut deja etre faite par le selector si le materiau le permet : `CasaEngine/CasaEngine/Framework/Rendering/Draw/RenderPass.cs:61`

Cela veut dire qu'une modernisation propre est possible sans re-ecrire toute la chaine de draw. Le bon axe n'est pas une refonte totale, mais une generalisation des mecanismes deja introduits.

### 4. Une partie de la logique RacingGame a deja glisse dans CasaEngine lui-meme

Le document actuel pointe surtout `LegacyTrackSceneFactory`, mais il faut aller plus loin : la contamination n'est plus seulement dans le jeu, elle est aussi dans l'import editor du moteur.

Points de preuve :

- `EditorAssetImportService` cree systematiquement un `MaterialAsset("lit-diffuse")` pour les models importes : `CasaEngine/CasaEngine.EditorServices/EditorAssetImportService.cs:152`
- il applique aussi les heuristiques `ShouldUseAlphaCutout` dans le service generique d'import : `CasaEngine/CasaEngine.EditorServices/EditorAssetImportService.cs:155` et `CasaEngine/CasaEngine.EditorServices/EditorAssetImportService.cs:215`
- il replie l'ambient legacy dans l'emissive a l'import moteur : `CasaEngine/CasaEngine.EditorServices/EditorAssetImportService.cs:164` et `CasaEngine/CasaEngine.EditorServices/EditorAssetImportService.cs:195`
- il applique aussi le boost special `Sign/Banner/Windmill` dans le moteur : `CasaEngine/CasaEngine.EditorServices/EditorAssetImportService.cs:198` et `CasaEngine/CasaEngine.EditorServices/EditorAssetImportService.cs:208`

Ce point est architecturalement important :

- un moteur generique peut supporter des profils d'import legacy,
- mais il ne doit pas embarquer en dur des conventions de nommage propres a RacingGame dans son chemin d'import general.

### 5. Le document actuel traite l'ambient comme une approximation locale, alors que le probleme est plus structurel

Le sujet n'est pas seulement que `AmbientColor` est converti en `EmissiveColor`.

Le point plus profond est :

- `LightingContext` expose deja `AmbientColor` et le bind au shader : `CasaEngine/CasaEngine/Framework/Rendering/LightingContext.cs:17` et `CasaEngine/CasaEngine/Framework/Rendering/LightingContext.cs:38`
- mais `Lighting.fxh` n'en fait rien dans le calcul statique courant, qui additionne `EmissiveColor` mais pas `AmbientColor` : `CasaEngine/CasaEngine/Content/Shaders/Lighting.fxh:75`

Autrement dit, CasaEngine possede deja le contrat runtime d'un terme ambient, mais le shader statique principal ne le consomme pas.

Ce detail change la nature du probleme :

- ce n'est pas un manque de donnees,
- c'est une incoherence entre contrat render context et implementation shader.

### 6. Le document actuel ne met pas assez en avant le fait que la voie `CompiledMaterial` n'est pas encore consommee par le renderer

`CompiledMaterial` existe, est documente et est mis en cache, mais le draw path reste pilote par `MaterialBase`.

Points de preuve :

- `MaterialCompiler` produit `CompiledMaterial` : `CasaEngine/CasaEngine/Framework/Materials/MaterialCompiler.cs:28`
- `MaterialCache` le conserve : `CasaEngine/CasaEngine/Framework/Materials/MaterialCache.cs:24`
- mais `StaticMeshRendererComponent` manipule ensuite `MaterialBase` sur les `RenderItem` : `CasaEngine/CasaEngine/Framework/Game/Components/StaticMeshRendererComponent.cs:199`

Ce n'est pas forcement un probleme a court terme. En revanche, tant que le renderer ne consomme pas au moins un descripteur compile stable, il restera plus difficile de :

- declarer des capacites de materiau,
- brancher de nouveaux shaders sans modifier le coeur,
- stabiliser le systeme pour de futurs materials PBR / reflection / deferred.

## Ce qui doit etre ameliore dans CasaEngine

### A. A ameliorer rapidement sans casser l'architecture

1. Completer la consommation des donnees deja presentes.

Cela inclut :

- support reel de `reflection_texture`,
- import editor des textures de reflection,
- consommation optionnelle de `AmbientColor`,
- suppression progressive des heuristiques runtime les plus fragiles.

2. Aligner l'import, l'asset authoring et le runtime.

Aujourd'hui :

- l'importeur preserve plus d'information que le runtime n'en rend,
- l'editor cree des materials moins riches que les metadonnees disponibles,
- le runtime sait compiler un `legacy-multi-texture`, mais l'import editor cree surtout du `lit-diffuse`.

3. Arreter d'utiliser le type `Material` comme promesse implicite de "futur PBR" tant qu'il n'a pas de contrat clair.

Le type est utile comme conteneur de slots, mais sa semantique reste floue :

- legacy multi-texture,
- proto PBR,
- materiau riche generique,
- ou simple passerelle de compatibilite.

Il faut clarifier ce role avant de l'etendre davantage.

### B. A rajouter si CasaEngine doit rester moderne

1. Un vrai concept d'environnement/reflection generique.

Pas seulement une cubemap pour RacingGame, mais un systeme generic de surface reflectante :

- texture de reflection/cubemap,
- parametre d'intensite,
- fallback quand la reflection n'est pas disponible,
- possibilite future d'un environnement probe plus moderne.

2. Un systeme de "material capabilities" ou "surface semantics".

Le renderer doit pouvoir demander a un materiau :

- a-t-il une base color,
- une normal map,
- une reflection,
- un masque d'opacite,
- un besoin tangent-space,
- une route opaque / alpha test / transparent,
- un besoin de technique speciale.

Sans cela, chaque nouveau materiau exigera encore des `switch` sur les types.

3. Une politique de selection de technique par shader, pas par classe materiau.

Aujourd'hui `LitDiffuseMaterial.SelectTechnique()` encode directement des noms de techniques de `basicEffect.fx` : `CasaEngine/CasaEngine/Framework/Materials/LitDiffuseMaterial.cs:27`.

Pour une architecture moderne, il vaut mieux :

- declarer des intentions canoniques,
- laisser le shader ou sa policy mapper cela vers la technique concrete.

4. Une notion explicite de profile d'import.

CasaEngine doit pouvoir dire :

- import generique FBX/OBJ/GLTF,
- import legacy X/MonoGame,
- import RacingGame legacy,
- import d'un autre jeu.

Le moteur ne doit pas deduire cela par heuristiques dans un service d'import unique.

## Ce qui releve d'une modification d'architecture de CasaEngine

### 1. Remplacer les checks de type par des contrats de capacites

C'est la modification la plus importante.

Aujourd'hui les points de verrou sont surtout :

- `EffectiveShaderResolver.cs:55`
- `RenderFeatureResolver.cs:132`
- `MaterialCompiler.cs:138`

Le moteur doit evoluer vers :

- un contrat declare par le materiau ou son resultat compile,
- un resolver de shader fonde sur des semantiques/capacites,
- une derivation des features qui ne depend pas de la classe concrete.

Sinon CasaEngine restera extensible uniquement en modifiant le noyau.

### 2. Introduire un descripteur runtime stable entre `MaterialAsset` et `MaterialBase`

Le point ideal n'est probablement pas de faire dessiner directement `CompiledMaterial` tel quel, mais de disposer d'un objet runtime stable, par exemple :

- `CompiledMaterial`, ou
- un `RenderMaterialDescriptor`, ou
- une `SurfaceDescription`.

Ce descripteur devrait porter :

- le shader effectif,
- les features,
- les textures par semantique,
- les constantes uniformes,
- les render states,
- les besoins specifiques du mesh.

Ensuite `MaterialBase` peut rester une couche d'adaptation de compatibilite pendant la migration, au lieu d'etre la seule verite runtime.

### 3. Rendre `MaterialDefinitionRegistry` extensible

Le registre actuel est statique : `CasaEngine/CasaEngine/Framework/Materials/MaterialDefinitionRegistry.cs:7`.

Pour un moteur modulable, il faudrait pouvoir :

- enregistrer des definitions supplementaires,
- enregistrer leurs compilateurs/adaptateurs runtime,
- enregistrer les policies editor associees,
- sans modifier le coeur du moteur.

Sinon chaque nouveau type de materiau restera une modification centrale de CasaEngine.

### 4. Sortir la logique legacy/game-specific de l'import editor central

`EditorAssetImportService` ne devrait pas savoir qu'un model nomme `Sign*` doit recevoir un boost d'ambient ou qu'un nom de texture contenant `Palm` doit etre en alpha-cutout.

Cette logique doit vivre dans une couche de profil, par exemple :

- `ILegacyMaterialImportProfile`,
- `IXMaterialInterpretationProfile`,
- ou un service similaire injecte a l'import.

Le moteur fournirait :

- le parse brut des metadonnees,
- les hooks de transformation vers `MaterialAsset`,
- les fallbacks generiques.

Le jeu ou le module legacy fournirait :

- le mapping `LegacyTechniqueIndex -> SurfaceIntent`,
- les exceptions de naming historiques,
- les boosts/compensations connus.

### 5. Clarifier la direction long terme du systeme de materiaux

CasaEngine doit choisir explicitement entre deux directions compatibles mais differentes :

#### Direction minimale et robuste

- materials forward classiques,
- systeme de variantes par features,
- reflection simple,
- normal map,
- alpha test,
- transparence,
- quelques semantics stables.

Cette direction est tres raisonnable pour un moteur generique MonoGame moderne.

#### Direction plus ambitieuse

- surface description plus generale,
- PBR ou pseudo-PBR,
- environment lighting,
- eventuellement material graph plus tard.

Dans les deux cas, il faut d'abord stabiliser les contracts de semantique et de compilation. Sans cela, toute ambition "moderne" restera dispersee entre plusieurs objets partiellement redondants.

## Ce qui doit rester hors du moteur generique

Pour garder CasaEngine proprement generique, les elements suivants ne doivent pas etre hardcodes dans le coeur :

1. Le mapping exact des indices legacy RacingGame.

Le moteur peut exposer le champ `LegacyTechniqueIndex`, mais le sens precis de chaque valeur doit etre interprete par un profile d'import ou une couche de compatibilite.

2. Les heuristiques de nommage `Alpha*`, `Palm`, `Leave`, `Ast`, `plants`.

Cela peut exister comme fallback legacy local, pas comme comportement canonique du moteur.

3. Les boosts d'ambient specifiques a `Sign`, `Banner`, `Windmill`.

Cela releve d'une compatibilite data/game, pas d'une loi generale du moteur.

4. Le choix force de `lit-diffuse` pour toute importation de model.

Le moteur doit offrir une strategie de materialisation configurable, pas imposer une seule classe runtime pour tous les cas importes.

## Architecture cible recommandee

La bonne cible pour CasaEngine me semble etre la suivante :

### Couche 1 - Import brut

L'importeur lit la geometrie et les metadonnees sans interpretation forte.

Exemples :

- textures detectees,
- technique legacy brute,
- couleurs,
- shininess,
- reflection cubemap,
- flags supplementaires.

### Couche 2 - Interpretation par profil

Un profil optionnel traduit ces donnees vers une intention de surface generique.

Exemples :

- opaque lit,
- cutout vegetation,
- reflective coated surface,
- unlit decal,
- legacy fallback.

Le profil peut etre celui de RacingGame, ou un profil neutre fourni par CasaEngine.

### Couche 3 - Compilation runtime

Le moteur compile cette intention vers un descripteur stable :

- shader effectif,
- features,
- textures par semantique,
- constantes,
- render states.

### Couche 4 - Adaptation draw path

Le renderer consomme ce descripteur et choisit :

- la variante,
- la technique,
- les binds,
- les passes.

Avec cette architecture :

- CasaEngine reste generique,
- RacingGame garde sa compatibilite legacy,
- l'ajout d'un futur materiau moderne ne force pas a modifier tout le noyau.

## Feuille de route recommandeee

### Phase 1 - Nettoyage des glissements game-specific

- sortir les heuristiques RacingGame de `EditorAssetImportService`,
- introduire un profile d'import legacy optionnel,
- conserver les comportements actuels via ce profile pour ne pas casser la compatibilite.

### Phase 2 - Generalisation du contrat material

- introduire un contrat de capacites/semantiques,
- adapter `EffectiveShaderResolver` et `RenderFeatureResolver` pour utiliser ce contrat,
- laisser les anciens chemins types-specifiques en fallback transitoire.

### Phase 3 - Reflection generique

- importer aussi les textures de reflection,
- ajouter un shader/policy reflection-aware,
- supporter une surface reflective generique au runtime,
- brancher ce support sans logique RacingGame hardcodee.

### Phase 4 - Consommation d'un descripteur runtime stable

- faire converger le renderer vers un resultat compile plus stable que les classes runtime concretes,
- garder `MaterialBase` comme facade de compatibilite si necessaire,
- documenter clairement la semantique des textures et des features.

## Conclusion architecture approfondie

Le document actuel a raison sur le fait que la prochaine etape n'est plus principalement un probleme de simple import. En revanche, l'analyse complete du moteur montre que le vrai chantier n'est pas seulement "reflection + technique mapping".

Le vrai chantier est le suivant :

- terminer la migration vers un systeme de materials pilote par des semantiques et non par des classes concretes,
- retirer les conventions RacingGame du coeur de CasaEngine,
- faire converger l'import, l'authoring, la compilation runtime et le renderer vers le meme contrat.

Si cette direction est suivie, CasaEngine pourra :

- rester compatible avec les assets legacy actuels,
- rester generique pour d'autres jeux,
- ajouter des surfaces plus modernes sans dette structurelle supplementaire.

Si au contraire le moteur ajoute seulement une cubemap de reflection et quelques `if` de plus, il gagnera une parite locale utile pour RacingGame, mais il degradera encore sa modularite a moyen terme.