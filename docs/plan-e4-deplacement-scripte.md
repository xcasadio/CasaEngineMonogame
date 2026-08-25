
### E4.g — Moteur : propriété externe de la verticale ⏳ (moteur, plan-verifier)

- **Pourquoi (dette relevée le 2026-08-25)** : depuis `5c3bd58`, la DLL possède la verticale des PNJ
  scriptés (déplacement par tick via `Move`, port de `PosZ += ForceZ`). Mais `UpdateGround`
  (`CharacterControllerComponent.cs:1114-1118`, correction appliquée `:1209-1213`) re-plaque au sol
  toute entité dont le pied est dans la fenêtre `StepHeight + max(GroundSnapDistance, SkinWidth)`,
  **sauf** si `Dot(Velocity, up) > 0`. Une vitesse réellement nulle échoue à ce test, donc la DLL
  pose une vitesse symbolique `RisingVelocitySignal = 1e-6` pendant les ticks montants
  (`AlundraEntityScriptProxy.cs:661`). C'est une accommodation à l'interface du moteur : la dette.
- **Contrainte de rémanence (blocker du plan-verifier, à respecter)** : `Update` tourne à CHAQUE
  frame rendue (`CharacterMotionSystem.cs:245-251`) alors que la DLL n'appelle `Move` qu'à son tick
  logique 50 Hz : à dt 1/123 ou 1/240, 60 à 80 % des `Update` ne suivent aucun `Move`. Le signal
  actuel tient parce que c'est un ÉTAT DE VITESSE persistant. Tout remplacement doit donc être
  **rémanent entre les ticks**, jamais effacé par `Update`.
- **Scope (moteur, API additive)** : `CharacterControllerComponent` gagne
  1. `public bool IsVerticalOwnedExternally { get; set; }` — défaut **false**, aucun comportement
     existant ne change ;
  2. `public void SetExternalVerticalDisplacement(float displacementAlongUp)` — le propriétaire
     DÉCLARE le déplacement vertical de son tick (la DLL appelle exactement là où elle appelle
     aujourd'hui `SetVerticalVelocity(rising ? 1e-6 : 0)`, soit une fois par tick logique). La
     valeur est **rémanente** : elle vaut jusqu'au prochain appel, `Update` ne l'efface JAMAIS
     (c'est la durée de vie exacte du `1e-6` qu'elle remplace). Choix d'une déclaration explicite
     plutôt qu'une déduction depuis `Move` : la DLL émet 2 `Move` par tick (un horizontal à
     composante verticale nulle, un vertical), donc « le dernier `Move` gagne » effacerait le
     signal et « accumuler » exigerait de deviner la frontière de tick — `_lastRequestedDisplacement`
     est de toute façon inutilisable (`Update` l'écrase `:210` avant `UpdateGround` `:218`).
  Quand `IsVerticalOwnedExternally` vaut true :
  - **Gate `UpdateGround`** : déclaration > 0 → « en l'air » (`SetGroundInfo(None)`), condition
    évaluée **AVANT la branche `_hasStepSupportHit`** (`:1103-1110`) qui sinon regrounde et sort
    avant le gate existant. `_hasStepSupportHit` n'est **pas** remis à zéro en tête d'`Update` : la
    DLL dépend de cette branche pour suivre les marches sur les ticks non montants
    (`MoveWithCollisions` le remet déjà à false à chaque appel, `:889`).
  - **Aucune verticale moteur** : `ApplyVerticalVelocity` n'intègre ni gravité ni vitesse verticale
    et n'écrête plus la composante descendante au sol ; **et** la composante le long de `up` est
    exclue du déplacement piloté par la vitesse (`:205`), sinon une vitesse résiduelle (recalcul
    `:213-216` après une marche, `SetVerticalVelocity` externe, snapshot restauré) produirait un
    mouvement vertical permanent non amorti. Documenter sur `SetVerticalVelocity` que sa composante
    verticale n'est plus intégrée tant que le drapeau est posé.
  - Inchangé : résolution de sol pour `IsGrounded`, snap descendant, support, marches sur les ticks
    non montants. Ajout d'API documenté selon `.github/copilot-instructions.md`.
- **DLL (même livraison, après bump)** : `IsVerticalOwnedExternally = true` au spawn des PNJ
  scriptés ; `SetVerticalVelocity(FinalForceZ > 0 ? RisingVelocitySignal : 0f)` remplacé par
  `SetExternalVerticalDisplacement(FinalForceZ / 65536f)` au MÊME site et à la même cadence ;
  `RisingVelocitySignal` supprimée ; héros inchangé (drapeau false, verticale moteur d'E3-3).
- **Acceptation** :
  - **Moteur, défaut false** : 12 scénarios d'E3.c et tests `SetVerticalVelocity` (E4.0) inchangés ;
    `CasaEngine.Tests` sans nouvel échec (18 préexistants).
  - **Moteur, drapeau posé** (chaque test échoue sans SA moitié) : (a) au sol, déclaration montante
    (valeur dans la fenêtre de snap) puis **N `Update(1/240)` consécutifs SANS aucun appel** → le
    pied ne redescend pas et `IsGrounded` reste false sur TOUTES ces frames (échoue si la
    déclaration est effacée par `Update` — c'est le test de rémanence) ; (b) déclaration montante
    suivie d'un `Move` horizontal à composante verticale nulle dans la même frame → toujours en
    l'air (échoue avec « le dernier Move gagne ») ; (c) au sol contre une géométrie qui fait passer
    `Move` par `TryStepMove` puis `Update` → pas de re-plaquage sous déclaration montante (échoue
    avec l'ordre de branches actuel) ; (d) vitesse verticale positive injectée puis 60
    `Update(1/50)` sans `Move` ni déclaration → coordonnée verticale immobile, vitesse non
    croissante (échoue sans l'exclusion `:205`) ; (e) déclaration descendante → le sol est retrouvé
    et `IsGrounded` redevient vrai.
  - **DLL, valeurs inchangées** : mouette 171 ticks / 209,25 px (dt 1/50, 1/123, **1/240**, et avec
    à-coups de 0,3 s), chute quantifiée + palier, escalier, pin du Z supporté (26214401 bit-exact),
    sortie sur collision du 0x1F, invariants d'horloge unique ; trace d'intro byte-identique
    (jalons 554 / 555-678-801 / 1034 / 1202 / 1704) ; suites vertes (DLL 452+, convertisseur 137).
- **Rollback** : revert du commit moteur + pointeur, revert du commit DLL. **Budget** : un commit
  moteur + un commit parent (bump + DLL). **Arrêt** : si le gate additif ne peut pas laisser les
  tests moteur existants inchangés, ou si la branche marches ne peut pas être préservée pour les
  ticks non montants.
