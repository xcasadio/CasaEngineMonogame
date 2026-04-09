# CasaEngine Layering Project Split Evaluation

Évaluation produite dans le cadre de `HIER-011`.

## Question

Faut-il maintenant séparer `CasaEngine` en plusieurs projets du type :

- `CasaEngine.Core`
- `CasaEngine.Engine`
- `CasaEngine.Runtime`

## Réponse courte

**Oui à terme, mais pas immédiatement dans la même phase que la réorganisation physique des dossiers.**

## État actuel après refactor de hiérarchie

Points déjà favorables :

- la hiérarchie physique reflète beaucoup mieux les couches visées ;
- la fuite de dépendance `Engine -> Framework` identifiée dans `Primitive2D` a été supprimée ;
- les dossiers `Core`, `Engine` et le runtime haut niveau sont maintenant beaucoup plus lisibles ;
- les tests techniques de `Packing` ne polluent plus le runtime.

Points qui empêchent encore un split propre immédiat :

- de nombreux namespaces historiques ont été conservés pour compatibilité ;
- plusieurs couches restent encore regroupées dans un seul projet MSBuild ;
- certaines catégories ont été réorganisées physiquement sans renommage complet des namespaces ;
- un vrai split demanderait une passe dédiée sur les références, les `InternalsVisibleTo`, les tests et les projets éditeur.

## Recommandation de phasage

### Phase A — Stabilisation après refactor dossier

- valider builds, tests et outils
- résorber les warnings ou erreurs directement liés aux déplacements
- décider quels namespaces historiques doivent vraiment être migrés

### Phase B — Pré-split logique

- supprimer les dépendances transverses restantes entre couches
- réduire les accès implicites globaux
- vérifier les usages éditeur/runtime de chaque dossier candidat

### Phase C — Split par projets

- créer `CasaEngine.Core`
- créer `CasaEngine.Engine`
- créer `CasaEngine.Runtime`
- migrer progressivement les références projet par projet
- conserver si nécessaire un package ou projet agrégateur temporaire

## Verdict

`HIER-011` est considéré comme **fait** au sens demandé :

- l'opportunité du split a été étudiée ;
- la conclusion est positive ;
- le split n'est pas exécuté dans cette phase car ce serait un second chantier distinct, à risque plus élevé que la réorganisation de hiérarchie seule.