
### E4.g — Moteur : propriété externe de la verticale ⏳ (moteur, plan-verifier)

- **Pourquoi (dette relevée le 2026-08-25)** : depuis `5c3bd58`, la DLL possède la verticale des PNJ
  scriptés (déplacement par tick via `Move`, port de `PosZ += ForceZ`). Mais `UpdateGround`
  (`CharacterControllerComponent.cs:1113-1117`) re-plaque au sol toute entité dont le pied est à
  moins de `StepHeight + max(GroundSnapDistance, SkinWidth)` du sol, **sauf** si
  `Dot(Velocity, up) > 0`. Une vitesse réellement nulle échoue à ce test (0 n'est pas > 0), donc la
  DLL doit poser une vitesse symbolique `RisingVelocitySignal = 1e-6` pendant les ticks montants
  (`AlundraEntityScriptProxy.cs:661`) pour désactiver ce plaquage. C'est une accommodation à
  l'interface du moteur, pas une mécanique fidèle : la dette à solder.
- **Scope (moteur, API additive)** : `CharacterControllerComponent` gagne
  `public bool IsVerticalOwnedExternally { get; set; }` (défaut **false** → aucun comportement
  existant ne change). Quand elle vaut true :
  1. `UpdateGround`'s airborne gate accepte AUSSI le déplacement vertical demandé du tick :
     `Dot(velocity, up) > 0 || Dot(_lastRequestedDisplacement, up) > 0` (le champ existe déjà,
     `Move` le renseigne `:349/:366`) — un `Move` montant suffit donc à sortir du sol, sans vitesse
     factice ;
  2. `ApplyVerticalVelocity` n'intègre ni gravité ni vitesse verticale, et n'écrête plus la
     composante descendante au sol (le propriétaire fournit tout le déplacement vertical) ;
  3. le reste (résolution de sol pour `IsGrounded`, snap descendant, marches, support) est
     inchangé — c'est ce que la DLL consomme.
  Documenter l'ajout d'API selon `.github/copilot-instructions.md`.
- **DLL (même livraison, après bump)** : `IsVerticalOwnedExternally = true` posé sur le contrôleur
  des PNJ scriptés au spawn ; `RisingVelocitySignal` et son `SetVerticalVelocity` supprimés ; le
  héros (E3-3, verticale moteur) reste à `false`, inchangé.
- **Acceptation** : moteur — défaut false : les 12 scénarios d'E3.c et les tests de
  `SetVerticalVelocity` (E4.0) inchangés, `CasaEngine.Tests` sans nouvel échec (18 préexistants) ;
  avec le drapeau : un `Move` montant depuis le sol quitte le sol et n'est pas re-plaqué à la frame
  suivante (échoue sans (1)), aucune gravité moteur appliquée (échoue sans (2)), un `Move`
  descendant retrouve le sol et `IsGrounded` redevient vrai. DLL — valeurs inchangées : mouette
  171 ticks / 209,25 px (dt 1/50, 1/123, 1/240 et avec à-coups), chute quantifiée + palier,
  escalier, pin du Z supporté (26214401 bit-exact), sortie sur collision du 0x1F ; trace d'intro
  byte-identique (jalons 554 / 555-678-801 / 1034 / 1202 / 1704) ; suites vertes.
- **Rollback** : revert du commit moteur + pointeur, revert du commit DLL. **Budget** : un commit
  moteur + un commit parent (bump + DLL). **Arrêt** : si le gate additif ne peut pas laisser les
  tests moteur existants inchangés.
