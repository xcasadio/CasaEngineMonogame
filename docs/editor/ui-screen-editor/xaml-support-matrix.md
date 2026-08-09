# XAML Support Matrix

## Objectif

Cette matrice décrit le périmètre du parser et du serializer XAML de la v1 du screen editor.

Le principe de la v1 est le suivant :
- couvrir un sous-ensemble stable et déterministe
- préserver correctement les cas simples nécessaires au document model
- rendre explicites les limites avant d'introduire la preview complète et l'édition visuelle avancée

## Support v1

| Élément | Statut | Notes |
|---|---|---|
| Type racine unique | Supporté | Le parser exige un élément racine unique. |
| Attributs simples | Supporté | Les attributs XAML simples sont stockés comme `UIScreenPropertyValue`. |
| `Name` | Supporté | `Name` est mappé vers la propriété dédiée du nœud de document. |
| Hiérarchie d'enfants directe | Supporté | Les enfants non qualifiés sont convertis en `UIScreenNode`. |
| Collections d'enfants simples | Supporté | Les panels et autres conteneurs multi-enfants sont représentés par la liste `Children`. |
| Éléments de propriété avec contenu brut | Supporté partiellement | Le contenu est conservé comme XAML brut dans une propriété de type `xaml`. |
| Sortie déterministe | Supporté | Le serializer écrit les propriétés simples triées, puis les propriétés XAML, puis les enfants. |
| Round-trip structurel du sous-ensemble v1 | Supporté | Couvert par tests unitaires ciblés. |

## Support partiel

| Élément | Statut | Notes |
|---|---|---|
| `Window.TitleBar` et autres éléments de propriété | Partiel | Préservés comme XAML brut, mais non projetés dans un modèle fortement typé. |
| Espaces de noms additionnels | Partiel | Les noms locaux sont lus, mais la v1 n'essaie pas de reconstruire toute la sémantique multi-namespace. |
| Valeurs complexes sérialisées en attribut | Partiel | Conservées comme texte brut, sans normalisation sémantique. |

## Non supporté en v1

| Élément | Statut | Notes |
|---|---|---|
| Styles complexes | Non supporté | Pas de modèle dédié ni d'édition structurée. |
| Resources avancées | Non supporté | Pas de prise en charge complète des dictionnaires et références avancées. |
| Bindings complexes | Non supporté | Le parser v1 ne projette pas la sémantique complète de binding dans le document model. |
| Templates | Non supporté | Les templates ne sont pas modélisés comme structure éditable v1. |
| Extensions markup complexes | Non supporté | Conserver le texte brut est possible, mais sans compréhension sémantique robuste. |
| Validation sémantique complète MGUI | Non supporté | La v1 valide surtout la structure XML attendue par le document model. |

## Conséquences pratiques

- Le screen editor v1 est adapté à des écrans simples à modérément structurés.
- Les écrans dépendant fortement des styles, resources, templates ou bindings avancés devront être traités comme hors périmètre ou partiellement préservés.
- La preview runtime devra s'appuyer d'abord sur les cas supportés nativement par le document model.

## Candidats post-v1

- projection structurée des éléments de propriété complexes
- support explicite des resources et styles
- support de bindings éditables
- support des templates et composants réutilisables
