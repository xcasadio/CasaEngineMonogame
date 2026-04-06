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