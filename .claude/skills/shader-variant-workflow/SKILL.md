---
name: shader-variant-workflow
description: "Gérer proprement des permutations de shader (skinned, instancing, normal map…) : flags, clé de cache, binding, repli, sample."
---

# Skill : shader-variant-workflow

## But

Gérer proprement des permutations de shader (skinned, instancing, normal map, etc.).

## Étapes

1. Définir les flags de variant (bitmask), en partant des flags existants du moteur.
2. Définir la clé de cache : material + flags.
3. Charger ou résoudre l'`Effect` et la technique.
4. Binder les paramètres : constantes, textures, samplers.
5. Technique de repli si le variant manque.
6. Sample de validation.

## Checklist

- Cache stable : pas de recompilation ni de lookup coûteux par frame.
- Paramètres nommés de façon cohérente.
- Logs uniquement au chargement, jamais dans `Draw`.
- Règle par chemin `rendering` respectée.

## Done

Variants fonctionnels, sample, build OK.
