# XAML Support Matrix

## Objectif

Cette matrice décrit le périmètre du parser et du serializer XAML du screen editor.

Le principe de la v1 était le suivant :
- couvrir un sous-ensemble stable et déterministe
- préserver correctement les cas simples nécessaires au document model
- rendre explicites les limites avant d'introduire la preview complète et l'édition visuelle avancée

T4.1 (engine ADR-0038 « Lossless editor round trip ») ajoute la garantie qui manquait à la v1 : **ouvrir puis
sauvegarder un écran ne perd rien**, y compris ce que le document model abstrait ne représente pas
lui-même (commentaires, déclarations de namespace, préfixes, ordre des attributs, ordre des éléments de
propriété). Le mécanisme : le parser garde le `XDocument` d'origine (`UIScreenDocument.SourceXDocument`,
`UIScreenNode.SourceElement`, `UIScreenPropertyValue.SourceElement`/`ElementQualifiedName`, tous internes à
`CasaEngine.EditorServices`) ; le serializer, quand ce document existe, corrige cet arbre en place au lieu
d'en reconstruire un nouveau -- il ne touche que les attributs et les éléments qui ont réellement changé,
et laisse tout le reste (commentaires, indentation, namespaces, préfixes) tel quel. Un document créé dans
l'éditeur sans texte source (pas de `SourceXDocument`) garde le comportement de synthèse v1 ci-dessous,
inchangé.

## Les deux garanties (T4.1)

| Garantie | Comportement | Comment |
|---|---|---|
| Sauvegarde sans modification | Réécrit le fichier source **identique octet pour octet** (mêmes fins de ligne, même encodage, même présence de BOM). | `UIScreenEditorSession.Save` compare un instantané sémantique du document courant (`UIScreenSemanticSnapshot`, qui ignore volontairement toute la mise en forme) à celui pris juste après le parsing ; s'ils sont égaux, les octets bruts du fichier d'origine (`UIScreenDocument.OriginalBytes`) sont réécrits tels quels, sans repasser par le serializer. |
| Sauvegarde modifiée | Conserve tout ce que le modèle abstrait ne représente pas : commentaires (avant la racine, entre les enfants, avant une balise fermante), déclarations de namespace et préfixes (éléments et attributs), ordre des attributs (un attribut ajouté va après les autres), éléments de propriété avec leur nom qualifié complet (`Canvas.Left`, `Window.Resources`) à leur position d'origine, valeurs de markup extension verbatim, présence ou absence de la déclaration XML, style de fin de ligne de la source. Seule la mise en forme des régions réellement éditées peut changer. | Le serializer corrige en place l'arbre `XDocument` gardé depuis le parsing (voir ci-dessus) au lieu de le reconstruire. |
| Suppression d'un nœud | Supprime aussi les commentaires attachés juste avant lui. | `UIScreenXamlSerializer.RemoveWithLeadingComments` : en remontant les nœuds frères qui précèdent l'élément supprimé, chaque commentaire rencontré (et le texte d'espacement pur qui le sépare d'un autre commentaire) est supprimé avec lui ; un simple espacement d'indentation sans commentaire devant n'est pas touché. |

Limites connues de ce mécanisme (non couvertes par les tests T4.1, acceptées comme risque documenté) :
- réordonner des enfants existants déplace leurs éléments XML mais ne déplace pas les commentaires qui les
  précédaient dans le texte d'origine ;
- un nœud ou un élément de propriété ajouté dans l'éditeur (donc sans `SourceElement`) est toujours synthétisé
  comme en v1 (attributs triés, espaces de noms par défaut), sans mise en forme d'origine à préserver ;
- remplacer la racine du document par un nœud entièrement nouveau (`UIScreenDocument.SetRoot` avec un nœud
  qui n'a jamais été parsé) ne renomme pas la balise XML racine d'origine.

## Support v1 (base, inchangée)

| Élément | Statut | Notes |
|---|---|---|
| Type racine unique | Supporté | Le parser exige un élément racine unique. |
| Attributs simples | Supporté | Les attributs XAML simples sont stockés comme `UIScreenPropertyValue`. |
| `Name` | Supporté | `Name` est mappé vers la propriété dédiée du nœud de document. |
| Hiérarchie d'enfants directe | Supporté | Les enfants non qualifiés sont convertis en `UIScreenNode`. |
| Collections d'enfants simples | Supporté | Les panels et autres conteneurs multi-enfants sont représentés par la liste `Children`. |
| Éléments de propriété avec contenu brut | Supporté | Le contenu est conservé comme XAML brut dans une propriété de type `xaml`, avec son nom qualifié complet (T4.1) et, quand le document a une source, sa position d'origine. |
| Sortie déterministe (document sans source) | Supporté | Le serializer synthétique écrit les propriétés simples triées, puis les propriétés XAML, puis les enfants -- inchangé par T4.1. |
| Round-trip structurel du sous-ensemble v1 | Supporté | Couvert par tests unitaires ciblés. |
| Commentaires XML | Supporté (T4.1) | Préservés à leur place tant que le document a une source ; supprimés avec un nœud quand celui-ci est supprimé (voir tableau ci-dessus). |
| Déclarations de namespace et préfixes | Supporté (T4.1) | Jamais touchés par le serializer quand le document a une source : ni les attributs `xmlns:*`, ni le préfixe d'un élément ou d'un attribut existant. |
| Ordre des attributs | Supporté (T4.1) | Un attribut modifié garde sa position ; un attribut ajouté va après les autres ; un attribut supprimé disparaît. |
| Éléments de propriété qualifiés par leur propriétaire (`Canvas.Left`, `Window.Resources`) | Supporté (T4.1) | Le nom qualifié complet est conservé verbatim (`UIScreenPropertyValue.ElementQualifiedName`), y compris pour une propriété attachée dont le propriétaire diffère du type du nœud -- ce que la reconstruction `{node.ControlType}.{Name}` de la v1 faisait à tort. |
| Valeurs de markup extension | Supporté (T4.1) | Conservées verbatim tant qu'elles ne sont pas l'attribut modifié (comparaison de valeur avant toute écriture). |
| Déclaration XML et fin de ligne | Supporté (T4.1) | Présence/absence de la déclaration et style de fin de ligne (`\r\n` vs `\n`) de la source conservés. |

## Support partiel

| Élément | Statut | Notes |
|---|---|---|
| Espaces de noms additionnels dans le document model | Partiel | Les noms locaux sont ceux exposés par `UIScreenNode`/`UIScreenPropertyValue` (pas de préfixe dans les clés) ; le serializer préserve le préfixe réel dans le XML de sortie tant que l'attribut ou l'élément correspondant n'est pas modifié, mais le document model lui-même ne distingue pas deux attributs de même nom local avec des préfixes différents. |
| Valeurs complexes sérialisées en attribut | Partiel | Conservées comme texte brut, sans normalisation sémantique. |
| `Window.Resources` | Partiel | Préservé verbatim tant que `UIScreenDocument.Resources` n'a pas changé depuis le parsing (signature comparée à `BaselineResourcesSignature`) ; reconstruit entièrement dès qu'une entrée change, ajoutée ou retirée -- la mise en forme interne de ce bloc n'est alors plus garantie. |
| Réordonnancement d'enfants | Partiel | Les éléments sont déplacés dans l'arbre XML dans le nouvel ordre, mais un commentaire qui précédait un enfant déplacé reste à sa position textuelle d'origine. |

## Non supporté en v1

| Élément | Statut | Notes |
|---|---|---|
| Styles complexes | Non supporté | Pas de modèle dédié ni d'édition structurée. |
| Bindings complexes | Non supporté | Le parser v1 ne projette pas la sémantique complète de binding dans le document model (au-delà de `{Binding Path}` -> `UIScreenBindingValue`) ; toute autre markup extension (par exemple `{dataBinding:MGBinding ...}`) est conservée comme texte brut opaque. |
| Templates | Non supporté | Les templates ne sont pas modélisés comme structure éditable v1. |
| Validation sémantique complète MGUI | Non supporté | La v1 valide surtout la structure XML attendue par le document model ; le round trip sans perte ne valide pas non plus que le XAML produit est sémantiquement correct pour MGUI au-delà de ce que les tests exercent explicitement. |

## Conséquences pratiques

- Le screen editor est adapté à des écrans simples à modérément structurés, et préserve désormais fidèlement
  (T4.1) tout ce qu'un écran authored à la main contient au-delà de ce que le document model modélise.
- Les écrans dépendant fortement des styles, templates ou bindings avancés devront être traités comme hors
  périmètre ou partiellement préservés au niveau sémantique -- mais leur texte XAML brut n'est plus perdu par
  un aller-retour ouverture/sauvegarde sans modification.
- La preview runtime devra s'appuyer d'abord sur les cas supportés nativement par le document model.

## Candidats post-v1

- projection structurée des éléments de propriété complexes
- support explicite des styles
- support de bindings éditables
- support des templates et composants réutilisables
- déplacer les commentaires avec le nœud qu'ils précèdent lors d'un réordonnancement
