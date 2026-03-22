# Skill: shader-variant-workflow

## But
Gérer proprement des permutations shader (skinned, instancing, normal map, etc.)

## Étapes
1) Définir flags de variant (bitmask)
2) Définir clé de cache (material + flags)
3) Charger/resolve l’Effect/Technique
4) Binding des paramètres (constants/textures/samplers)
5) Fallback technique (si variant manquant)
6) Sample de validation

## Checklist
- Cache stable (pas de recompilation/lookup coûteux par frame)
- Paramètres nommés de façon cohérente
- Logs uniquement au chargement, pas en draw

## Done
- Variants fonctionnels + sample + build OK.
