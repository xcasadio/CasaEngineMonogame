
### E4.g — Moteur : propriété externe de la verticale ⏳ (moteur, plan-verifier)

- **Pourquoi (dette relevée le 2026-08-25)** : depuis `5c3bd58`, la DLL possède la verticale des PNJ
  scriptés (déplacement par tick via `Move`, port de `PosZ += ForceZ`). Mais `UpdateGround`
  (`CharacterControllerComponent.cs:1114-1118`) re-plaque au sol toute entité dont le pied est à
  moins de `StepHeight + max(GroundSnapDistance, SkinWidth)` du sol, **sauf** si
  `Dot(Velocity, up) > 0`. Une vitesse réellement nulle échoue à ce test (0 n'est pas > 0), donc la
  DLL doit poser une vitesse symbolique `RisingVelocitySignal = 1e-6` pendant les ticks montants
  (`AlundraEntityScriptProxy.cs:661`) pour désactiver ce plaquage. C'est une accommodation à
  l'interface du moteur, pas une mécanique fidèle : la dette à solder.
- **Scope (moteur, API additive)** : `CharacterControllerComponent` gagne
  `public bool IsVerticalOwnedExternally { get; set; }` (défaut **false** → aucun comportement
  existant ne change) ET un signal dédié. Quand le drapeau vaut true :
  1. **Signal de montée externe** : nouveau champ privé `_externalVerticalDisplacement` (float, le
     long de `up`), **accumulé par `Move`** (`+= Dot(requestedDisplacement, up)` — la valeur
     DEMANDÉE, pas la résolue : l'intention de monter doit défaire le plaquage même si le sweep a
     raboté le pas ; à documenter), remis à zéro **à la fin d'`Update`**, jamais par `Move`.
     **`_lastRequestedDisplacement` est inutilisable** : `Update` l'écrase par son propre
     déplacement (`:210`) avant d'appeler `UpdateGround` (`:218`), et il ne retient que le dernier
     sous-pas alors que la DLL peut appeler `Move` jusqu'à 4 fois par frame rendue.
  2. **Gate d'`UpdateGround`** : quand le drapeau est posé et `_externalVerticalDisplacement > 0`,
     l'entité est « en l'air » — condition évaluée **AVANT la branche `_hasStepSupportHit`**
     (`:1103-1110`), qui sinon `SetGroundInfo(grounded)` et sort avant le gate existant `:1114`.
     `_hasStepSupportHit` n'est **pas** remis à zéro en tête d'`Update` : la DLL dépend justement de
     cette branche pour suivre les marches d'escalier sur les ticks non montants (écart à documenter).
  3. **Aucune verticale moteur** : `ApplyVerticalVelocity` n'intègre ni gravité ni vitesse verticale
     et n'écrête plus la composante descendante au sol ; **et** la composante le long de `up` est
     exclue du déplacement piloté par la vitesse (`:205 requestedDisplacement = velocity * dt`) —
     sans quoi une vitesse verticale résiduelle (retour de `:215` après une marche, appel externe à
     `SetVerticalVelocity`, snapshot restauré) produirait un mouvement vertical permanent et non
     amorti. `SetVerticalVelocity` reste appelable mais sa composante verticale n'est jamais
     intégrée tant que le drapeau est posé (à documenter sur la méthode).
  4. Le reste est inchangé : résolution de sol pour `IsGrounded`, snap descendant, support, marches
     sur les ticks non montants — c'est ce que la DLL consomme.
  Documenter l'ajout d'API selon `.github/copilot-instructions.md`.
- **DLL (même livraison, après bump)** : `IsVerticalOwnedExternally = true` posé sur le contrôleur
  des PNJ scriptés au spawn ; `RisingVelocitySignal` et son `SetVerticalVelocity` supprimés ; le
  héros (E3-3, verticale moteur) reste à `false`, inchangé.
- **Acceptation** :
  - **Moteur, défaut false** : les 12 scénarios d'E3.c et les tests `SetVerticalVelocity` (E4.0)
    inchangés ; `CasaEngine.Tests` sans nouvel échec (18 préexistants).
  - **Moteur, drapeau posé** (chaque test doit échouer sans SA moitié) : (a) au sol, `Move(+up × d)`
    avec `d` dans la fenêtre de snap puis `Update(1/50)` → le pied n'est pas ramené au sol,
    `IsGrounded == false` ; (b) deux `Move` dans la même frame (un montant, un nul/descendant, somme
    montante) → quitte quand même le sol (échoue avec un signal non accumulé) ; (c) au sol contre une
    géométrie qui fait passer `Move` par `TryStepMove` (donc `_hasStepSupportHit` posé) puis
    `Update(1/50)` avec vitesse moteur nulle → pas de re-plaquage (échoue avec l'ordre de branches
    actuel) ; (d) vitesse verticale positive injectée puis 60 `Update(1/50)` sans `Move` → la
    coordonnée verticale ne bouge pas et la vitesse ne croît pas (échoue sans 3).
  - **DLL, valeurs inchangées** : mouette 171 ticks / 209,25 px (dt 1/50, 1/123, 1/240 et avec
    à-coups), chute quantifiée + palier, escalier, pin du Z supporté (26214401 bit-exact), sortie sur
    collision du 0x1F, invariants d'horloge unique ; trace d'intro byte-identique (jalons 554 /
    555-678-801 / 1034 / 1202 / 1704) ; suites vertes (moteur, DLL 452+, convertisseur 137).
- **Rollback** : revert du commit moteur + pointeur, revert du commit DLL. **Budget** : un commit
  moteur + un commit parent (bump + DLL). **Arrêt** : si le gate additif ne peut pas laisser les
  tests moteur existants inchangés, ou si la branche marches ne peut pas être préservée pour les
  ticks non montants.
