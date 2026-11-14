# Plan agent IA — Pilotage de l'éditeur, deuxième tranche : assets, panneaux, automatisation

Suite du chantier archivé [editor-remote-control-tasks.md](archive/editor-remote-control-tasks.md) (ADR-0092), demandée par l'auteur le 2026-11-14 : « 1, écris le plan puis on exécute et à la fin avec mon accord on fait le 2 » (1 = finir le pilotage de l'éditeur ; 2 = ménage, hors de ce plan).
Les décisions D1 → D3 ont été arbitrées avec l'auteur le 2026-11-14 : **ce plan les applique, il ne les rediscute pas**. Les propositions P1 → P13 sont à valider avec le plan (P11, P12 et P13 changent le comportement d'options d'automatisation : décision de l'auteur).
Relecture de sécurité avant approbation (2026-11-14, `security-reviewer`) : aucun P0 ; S2-1 (P1, propriétés d'écran sans liste blanche), S2-2 à S2-6 (P2 : valeurs non bornées des analyseurs, fermeture de panneau qui perd du travail, création d'asset qui détourne un nom ou écrase, `asset.open` sur un monde, chemin rendu par le catalogue), S2-7 à S2-10 (P3-P4 : instances du tube, cycles de reparentage, contournement par l'automatisation, contexte d'annulation) intégrés dans P1, P3 à P5, P7 à P9, P11, P12 et les validations des tâches.
Relecture du plan : `plan-verifier` REVISE (bornes non chiffrées, P13 non annoncé, sauvegarde d'un seul asset, référence des smokes, listes fermées, refus sur le vrai éditeur), corrigé ; relecture de clôture REVISE sur deux points rédactionnels (liste fermée des options à chemin de P11, plafond chiffré d'`asset.list`), corrigés tels que demandés sans nouveau tour (plafond de relecture atteint). Plan approuvé par l'auteur le 2026-11-14 : « continue en mode AUTO », et P13 tranché « Oui, durcir ».

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Un agent pilote aussi les assets (matériaux, particules, écrans UI, sons), les panneaux de l'éditeur et le reparentage d'entités ; les avis P3 et P4 restants de la première tranche sont corrigés ; les options de ligne de commande de l'automatisation passent sur le registre de commandes, sans changer leurs noms ni leurs effets. Décision : ADR-0093 (prolonge ADR-0092).

## État vérifié du dépôt (2026-11-14)

Reconnaissance du 2026-11-14 (scout en lecture seule), faits porteurs relus.

- Registre existant : `CasaEngine.Editor/Remote/` (commandes monde, entités, composants, propriétés, sélection, historique, captures, Play, journal), `GameEditor.Remote.cs`, `EditorWorldOperations`, `EditorPropertyRules`. Pompe dans `GameEditor.Update` après `ProcessPendingProjectLauncherAction` ; automatisation par `RunAutomation` plus loin dans `Update`. Tout sur le fil du jeu.
- **Matériaux** : ouverture `TryOpenMaterialAsset` (`GameEditor.cs:4833`), panneau `panel_material_asset_<id>` ; modification avec historique `MaterialAssetInspectorPanel.ExecuteMaterialPropertyCommand` (`:808`, contexte `Material`) ; le chemin d'automatisation `TryApplyAutomationPropertyOverrideAndSave` (`:248`) modifie **sans** historique puis sauvegarde ; sauvegarde `TrySaveLoadedAsset` (`:749`), aussi par `SaveCurrentProject` (`SaveDirtyMaterialInspectors`). **Aucune création de matériau.** Affectation : `StaticModelComponent.SetMaterialOverride(slot, guid)` (`StaticModelComponent.cs:154`), via l'éditeur de composant (`StaticModelComponentEditor.cs:117`, appels de `SetMaterialOverride` aux lignes 151 et 180).
- **Particules** : création `TryCreateParticleAssetInFolder` (`GameEditor.cs:4257`, presets `TryParseParticlePreset` `:4550`), sans historique ; modification par `ParticleAssetInspectorPanel.ApplyChange` (`:1013`, contexte `Particle`) ; dépôt dans le monde `TryApplyAutomationParticleDrop` (`:7983`, contexte du monde, `EditorParticleSystemComponentService`) ; sauvegarde `TrySaveLoadedAsset` (`:186`), aussi par `SaveCurrentProject`.
- **Écrans UI** : ouverture `TryOpenUIScreenAsset` (`:4757`) ; nœud trouvé par nom `FindAutomationScreenNode` (`:7836`) ; modification `SetPropertyCommand` sur la pile de l'écran (`GetOrCreateScreenCommandStack` `:1950`, contexte `UIScreen`) ; sauvegarde par `SaveCurrentProject` (`SaveDirtyScreenDocuments`).
- **Sons** : création `TryCreateSoundAssetInFolder` (`:4042`) ; l'inspecteur `SoundAssetInspectorPanel` modifie l'asset **sans historique** (`SetDirty`) et se sauvegarde par `TrySaveLoadedAsset` (`:237`) ; `SaveCurrentProject` **ne sauvegarde pas** les sons. Pas de contexte d'historique `Sound`.
- **Générique** : sauvegarde `EditorAssetWriterService.SaveAsset` / `SaveDocument` ; catalogue `AssetCatalog.Get(Guid)`, `Get(name)`, `GetByFileName` ; ouverture par extension `TryOpenEditorAsset(fullPath)` (`:4616`, routes `:4582`).
- **Panneaux** : identifiants `EditorPanelIds` ; activation `ActivateDockPanel(panelId)` (`:2420`) ; fermeture par le dock (`OnDockHostPanelClosing` `:2845`, qui annule si un écran a des changements) ; pas de liste des panneaux ouverts (seulement des parcours de `GetAllTabGroups()`).
- **Reparentage** : absent. `EditorWorldEditingService.DetachEntity` appelle `entity.Destroy()` quand l'entité a un parent (`:72`) : il ne peut pas servir à déplacer une entité. `Entity.AddChild` lève si l'entité est encore listée dans un monde (`Entity.cs:291`).
- **Automatisation** : 24 options (`EditorAutomationOptions.cs`). Contenu : `--project`, `--entity`, `--entity-index`, `--component`, `--open-asset`, `--activate-panel`, `--content-folder`, `--create-particle-asset`, `--create-particle-preset`, `--drop-particle-asset`, `--set-particle-property`, `--set-material-property`, `--set-screen-property`, `--save-project`. Automatisation seule : `--content-scroll`, `--dock-resize`, `--dock-input-drag`, `--particle-undo-redo-smoke`, `--diagnostics-out`, `--play-smoke`, `--screenshot-out`, `--capture-delay`, `--keep-open`, `--remote`. Nettoyage de fin : `RestoreAutomationEditedFilesIfNeeded`, `DeleteAutomationCreatedParticleAssetIfNeeded`.
- Avis reportés de la première tranche (plan archivé, T5.2) : MCP qui croit l'éditeur absent quand un autre client garde le tube ; client tué qui occupe le tube jusqu'à 5 min ; titre « [Remote Control] » qui reste si la boucle meurt ; `EditorConnection.DisposeAsync` sans verrou ; A5 (propriétés masquées par les inspecteurs spécialisés).
- Branche `chantier/editor-remote-control-2` créée depuis `main` `069ca155`, worktree `D:\development\repo\CasaEngineMonogame-prefs`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Les commandes couvrent les quatre types d'assets : matériaux, particules, écrans UI, sons. |
| D2 | Les options de ligne de commande d'`EditorAutomationOptions` sont gardées (mêmes noms, mêmes effets) ; celles qui modifient du contenu passent par les commandes du registre. |
| D3 | Des commandes de panneaux, pour l'essentiel : ouvrir un asset, activer, fermer, lister les panneaux. Pas de docking ni de redimensionnement. |

## Propositions à valider avec le plan

| Réf | Proposition |
|---|---|
| P1 | **Plusieurs clients** : le tube accepte jusqu'à 4 clients à la fois (une requête en cours par client, file commune vers le fil du jeu). Corrige les deux avis P3 : un client lent ou tué ne cache plus l'éditeur aux autres. Sécurité (S2-7) : toutes les instances sont créées par la même fabrique, même ACL et même nombre maximal ; les instances 2 à 4 sont créées pendant que l'instance 1 existe et sont réutilisées par `Disconnect`, jamais détruites ; une instance recréée seule l'est avec `FirstPipeInstance` ; `IsLocalClient` sur chaque instance ; les tests lisent l'ACL de chaque instance. |
| P2 | **MCP** : un éditeur trouvé dans la découverte mais injoignable dans le délai est « occupé », pas « absent » : le serveur garde sa liste d'outils et l'outil appelé répond « occupé, réessayer ». `DisposeAsync` prend le verrou. Le titre perd « [Remote Control] » quand la boucle du serveur s'arrête (événement relayé par la file vers le fil du jeu). |
| P3 | **Référence d'un asset** : par son `id` (catalogue) ou son fichier relatif au projet. Le contrôle porte sur l'entrée **et** sur ce que le catalogue rend (S2-6) : `FileName` résolu contenu dans le projet (`TryResolveInsideProject`), `AssetType` et extension attendus (un `.material` pour `material.*`), fichier présent. Même règle pour le matériau de `component.material_override` et l'asset de `particle.drop`. |
| P4 | **Création d'asset** (`material.create`, `particle.create`, `sound.create`) : un **nom** et un **dossier** relatif au projet (S2-4). Nom validé par la règle des mondes, extension imposée ; nom déjà présent dans le catalogue (tout type, sans casse) refusé, entrée de catalogue déjà présente pour ce fichier refusée ; dossier résolu par `TryResolveInsideProject` (racine du projet acceptée pour les assets, sous-dossiers existants), `Directory.Exists` ; seul le chemin relatif part vers l'écrivain, et le fichier est créé sans écrasement (vérifié de nouveau sur le fil du jeu juste avant l'écriture, `FileMode.CreateNew` quand le chemin d'écriture le permet). `material.create` part du matériau par défaut que l'inspecteur sait charger (forme exacte à lire dans le code en T2.2 ; sinon ⚠️ Blocked). |
| P5 | **Modification d'asset** : par le code du panneau de l'asset, ouvert au besoin (l'agent voit le changement dans l'éditeur). Matériaux et particules : historique de leur contexte (`Material`, `Particle`), donc annulables ; le chemin d'automatisation sans historique des matériaux est remplacé par le chemin avec historique. Écrans : `SetPropertyCommand` sur la pile de l'écran. **Sons : pas d'historique**, comme l'inspecteur aujourd'hui ; `sound.save` sauvegarde (D1 sans changer `File > Save`). **Valeurs bornées comme S7** (S2-2), avec ces sources de bornes : **particules** : les bornes des champs de l'inspecteur (`ParticleAssetInspectorPanel.cs:425-475` : durée 0,01-3600 ; particules max 1-100 000 ; débit 0-100 000 ; durée de vie 0,001-3600 ; vitesse 0-100 000 ; colonnes et lignes de flipbook 1-64 ; images 1-4096 ; images par seconde 0-240 ; rafales 0-100 000 ; tailles 0-100 000) ; **matériaux** : `MaterialPropertyDefinition.MinValue` / `MaxValue` de la définition du matériau (`MaterialAssetInspectorPanel.cs:583-603`), et nombre fini à défaut de borne ; **sons** : les bornes des champs de l'inspecteur des sons (constantes `AudioVoiceParameters.MinVolume`/`MaxVolume`, `MinPitch`/`MaxPitch`, `SoundAsset.MaxExclusionFadeSeconds` et les bornes des `CreateNumericField` de `SoundAssetInspectorPanel.cs`). Pour tous : nombres finis (NaN et infini refusés), énumérations par nom défini (`Enum.IsDefined`, jamais un nombre), textes de 4096 caractères au plus (comme `property.set`), GUID présents dans le catalogue avec le type attendu (texture, fichier audio, matériau) ; sons en JSON typé (pas d'analyseur texte existant) ; clés inconnues refusées. **Écrans (S2-1)** : seuls les noms de propriété que `UIPropertyRegistry.Default.GetDescriptors(type du nœud)` connaît ; noms avec `{`, `:` ou `.` refusés ; **toute valeur commençant par `{` refusée** (aucun descripteur ne l'autorise) ; 4096 caractères au plus. `history.undo` / `history.redo` prennent un `context` optionnel : `"world"` (défaut) ou l'`id` d'un asset dont le panneau est ouvert ; un contexte sans panneau ouvert est refusé, jamais créé (S2-10). **Sauvegarde d'un seul asset** : `material.save` et `particle.save` sauvegardent l'asset par le `TrySaveLoadedAsset` de son panneau puis le marquent enregistré, comme le font aujourd'hui les options d'automatisation ; `sound.save` de même. |
| P6 | **Affectation et dépôt** : `component.material_override` (entité, composant, `slot` = index entier de l'emplacement, matériau ou `null` pour retirer) et `particle.drop` (entité cible) passent par le contexte du monde, annulables, comme les éditeurs de composants et le dépôt existant ; références contrôlées par P3. |
| P7 | **Panneaux** : `panel.list` (identifiants et titres ouverts, actif), `asset.open` (S2-5) : **seulement les extensions `.material`, `.particle`, `.uiscreen` et `.sound`** (les quatre types de D1, routes existantes sans boîte de dialogue) ; toute autre extension est refusée, `.world` compris (qui passe par `world.open`). `panel.activate` : tout panneau ouvert. **`panel.close` (S2-3)** : la fermeture par le code ne passe par aucun contrôle, donc la commande vérifie elle-même l'état modifié, et ne ferme **que ces quatre types de panneaux** (liste fermée), chacun avec sa source d'état modifié : matériau (`panel_material_asset_<id>`, contexte d'historique `Material` du panneau et indicateur de l'inspecteur), particules (contexte `Particle` du panneau), écran (`panel_ui_screen_<…>`, contexte `UIScreen` du panneau), son (indicateur `IsDirty`/`SetDirty` de `SoundAssetInspectorPanel`). Un panneau modifié est refusé ; tout autre panneau (shell, sprite, animation, entité, mixeur, cutscene, TileMap…) est refusé. `asset.list` lit le catalogue (filtre par type facultatif), **500 entrées au plus**, triées par fichier ; au-delà, la réponse porte `truncated: true` et `total` (nombre d'entrées correspondantes) ; textes tronqués comme les autres réponses (2000 caractères). |
| P8 | **`entity.reparent`** (entité, nouveau parent ou racine) : annulable, sans `Destroy` ; refuse un cycle, **y compris le nouveau parent égal à l'entité** (S2-8 : `Entity.AddChild` ne vérifie rien, une boucle ferait déborder la pile et tuerait l'éditeur), un parent détruit, une entité d'un autre monde, et le Play ; la règle est revérifiée au Redo. La position dans le monde n'est pas recalculée (le transform local est gardé), comme `entity.create` sous un parent. Le monde réinitialise un enfant ajouté (`World.OnEntityChildAdded` appelle `child.Initialize()`) : T1.2 vérifie que composants et état restent cohérents après reparentage, Undo et Redo. |
| P9 | **Automatisation sur le registre** : un exécuteur interne appelle les commandes du registre sur le fil du jeu, sans tube ; chaque option de contenu devient l'appel d'une ou plusieurs commandes ; les options d'automatisation seule ne changent pas. Aucune commande joignable par le tube n'a d'argument « de confiance » (pas de chemin hors projet), et l'exécuteur interne n'est jamais une méthode du tube (S2-9). Les sauvegardes, restaurations et suppressions de fin d'automatisation ne portent que sur ce que l'automatisation a elle-même créé ou modifié (avec `--remote`, le travail d'un agent ne doit jamais être annulé à la fermeture). Validation : les smokes d'automatisation existants rendent les mêmes résultats avant et après, sous les réserves de P11, P12 et P13. |
| P11 | **Chemins des options d'automatisation** (décision de l'auteur demandée, S2-9) : les quatre options qui passent par `ResolveAutomationAssetPath` (`GameEditor.cs:7383`, `:7675`/`:7700`, `:7723`, `:8005`) acceptent aujourd'hui un chemin absolu : **`--open-asset`, `--content-folder`, `--create-particle-asset` et `--drop-particle-asset`** (liste fermée). Pour ces quatre options : un chemin absolu situé dans le projet est converti en chemin relatif, un chemin hors du projet est refusé avec un message clair (changement de comportement documenté). **Non concernées** : `--project` (le projet lui-même), `--diagnostics-out` et `--screenshot-out` (fichiers de sortie, librement placés hors du projet). |
| P12 | **`--set-screen-property`** (décision de l'auteur demandée, S2-1) : l'option passe par la liste blanche de P5, donc un nom de propriété inconnu du registre des propriétés d'écran est désormais refusé (changement de comportement documenté). |
| P13 | **`--set-material-property` et `--set-particle-property`** (décision de l'auteur demandée) : passant par les commandes, elles appliquent les bornes de P5 ; elles refusent désormais NaN et l'infini, un GUID de texture absent du catalogue ou d'un autre type, une énumération donnée par un nombre, et les valeurs au-delà des bornes de l'inspecteur (changement de comportement documenté ; aujourd'hui `maxparticles=2147483647` peut faire planter l'éditeur). |
| P10 | **Hors tranche** (avis A5) : les propriétés masquées par les inspecteurs spécialisés restent accessibles par `property.set` ; la documentation l'indique déjà. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/editor-remote-control-2`**, créée depuis `main`, dans le worktree `D:\development\repo\CasaEngineMonogame-prefs`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois**, icône ⏳ → 🚧 → ✅ / 🧪 / ⚠️, note de validation, **un commit par tâche** incluant ce fichier, message `type(area): summary` en anglais.
- **Ne jamais pousser.** Le merge sur `main` et le ménage (option 2) se font avec l'accord de l'auteur, après ce plan.
- **Ne rien inventer** : toute API vient du code ; sinon ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build** des deux `.sln` ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` ; `git add` fichier par fichier.
- Rappel moteur : rien d'alloué par image quand rien n'arrive ; aucune exception d'une commande ne sort de `Update` ; aucun changement de comportement des panneaux en usage manuel.
- **Sécurité** : écriture de fichiers et nouvelles entrées vers l'éditeur : relecture `security-reviewer` et `plan-verifier` avant approbation ; vérificateur neuf à la fin.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked.

## Validation globale

- Les deux `.sln` : 0 erreur ; `CasaEngine.Tests` : tout passe.
- Smoke de bout en bout (T4.1), éditeur lancé avec `--remote` sur une copie de `Projects/SampleProject` : un script de la CLI crée et modifie un matériau, une particule et un son, modifie un écran, affecte le matériau à un `StaticModel`, dépose la particule, reparente une entité, ouvre, active et ferme des panneaux, annule une modification d'asset ; le serveur MCP fait de même pendant qu'un appel long de la CLI est en cours (P1).
- Smokes d'automatisation existants rejoués avant et après T3.1, mêmes résultats (P9).

---

## Phase 0 — Décision

### ✅ T0.1 — ADR-0093, plan et index

- Fichiers : `docs/decisions/0093-editor-remote-control-covers-assets-panels-and-automation.md` (Accepted à l'approbation), `docs/decisions/README.md`, ce plan, `ai-agent/README.md`.
- Validation : relecture.
- Note de validation (2026-11-14) : ADR-0093 passée en Accepted et ajoutée à l'index ; plan ajouté au tableau d'`ai-agent/README.md`.
- Commit : `docs(editor): record the second remote control decision and plan`

### ⏳ T0.2 — Référence des smokes d'automatisation (avant toute modification)

- Objectif : enregistrer le comportement actuel des options de contenu, avant la phase 2, pour la comparaison de T3.1 (P9).
- Smokes, chacun sur une copie neuve de `Projects/SampleProject` (`<copie>`), éditeur construit depuis `main` :
  - S1 : `--project <copie>/SampleProject.json --entity ground --activate-panel inspector --diagnostics-out s1.txt`
  - S2 : `--project … --open-asset NewLitDiffuseMaterial.material --set-material-property alpha_cutoff=0.5 --diagnostics-out s2.txt`
  - S3 : `--project … --open-asset Particles/FireLoop_Minimal.particle --set-particle-property maxparticles=500 --particle-undo-redo-smoke --diagnostics-out s3.txt`
  - S4 : `--project … --create-particle-asset Particles --diagnostics-out s4.txt`
  - S5 : `--project … --entity ground --drop-particle-asset Particles/SparkBurst_Additive.particle --diagnostics-out s5.txt`
  - S6 : `--project … --open-asset Screens/main-menu.uiscreen --set-screen-property btnPlay:Content=Start --diagnostics-out s6.txt`
  - S7 : `--project … --entity ground --save-project --diagnostics-out s7.txt`
  - S8 : `--project … --entity ground --play-smoke --diagnostics-out s8.txt`
- Artefacts comparés : lignes `[Automation]` et `[PlaySmoke]` du diagnostic (horodatage retiré) ; empreinte SHA-256 de chaque fichier de la copie après la sortie (fichiers restaurés ou non) ; liste des fichiers créés et supprimés. Les commandes exactes et les artefacts sont rangés dans `ai-agent/audits/editor-remote-control-2-baseline/` (lecture seule ensuite).
- Validation : les huit smokes tournent et leurs artefacts sont enregistrés ; un smoke qui échoue déjà est noté tel quel.
- Commit : `docs(ai-agent): record the automation baseline before the remote control slice`

## Phase 1 — Transport et monde

### ⏳ T1.1 — Plusieurs clients, MCP « occupé », reliquats P4

- Objectif : P1 et P2.
- Fichiers : `CasaEngine.Editor/Remote/EditorRemoteServer.cs`, `EditorRemoteControl.cs`, `Tools/CasaEngine.Editor.Mcp/*`, `GameEditor.cs`, tests.
- Validation : tests : deux clients servis en même temps ; un client qui ne lit plus ne bloque pas un second ; une instance squattée reste refusée ; client par le redirecteur refusé sur chaque instance ; ACL lue sur chaque instance ; nombre d'instances jamais à zéro tant que le serveur tourne ; MCP qui garde ses outils quand l'éditeur est occupé ; titre mis à jour quand la boucle s'arrête ; build.
- Commit : `feat(editor): serve several remote clients at once`

### ⏳ T1.2 — `entity.reparent`

- Objectif : P8, opération `EditorWorldOperations.ReparentEntity` et commande.
- Fichiers : `CasaEngine.Editor/Runtime/EditorWorldOperations.cs`, `CasaEngine.Editor/Remote/EditorRemoteEditCommands.cs`, tests.
- Validation : tests : racine → enfant, enfant → racine, enfant → autre parent, chacun suivi d'Undo et de Redo ; cycle refusé (descendant, et l'entité elle-même) ; parent détruit refusé ; refus en Play ; Redo revérifié ; entité jamais détruite ; build.
- Commit : `feat(editor): reparent entities from remote commands`

## Phase 2 — Assets et panneaux

### ⏳ T2.1 — Commandes génériques d'assets et de panneaux

- Objectif : P3 et P7 : `asset.list`, `asset.open`, `panel.list`, `panel.activate`, `panel.close` ; `history.undo` / `history.redo` avec `context` (P5).
- Fichiers : `CasaEngine.Editor/Remote/*`, `GameEditor.Remote.cs` (accès au dock et aux routes), tests.
- Validation : tests (référence hors projet ou de mauvais type refusée, y compris par un `FileName` du catalogue qui sort du projet ; `asset.open` sur un `.world` refusé ; panneaux de matériau, de particules, d'écran et de son modifiés non fermés ; panneau fixe et panneau inconnu non fermés ; contexte d'annulation inexistant refusé ; plafond de `asset.list` : 501 entrées correspondantes au catalogue → 500 rendues, `truncated: true`, `total: 501`) ; build.
- Commit : `feat(editor): add the remote asset and panel commands`

### ⏳ T2.2 — Matériaux

- Objectif : `material.create`, `material.get`, `material.set_property` (historique `Material`), `material.save`, `component.material_override` (P4, P5, P6). L'option `--set-material-property` n'est pas touchée ici : T3.1 la fait passer par ces commandes.
- Fichiers : `MaterialAssetInspectorPanel.cs` (méthode avec historique exposée aux commandes), `CasaEngine.Editor/Remote/*`, `GameEditor.Remote.cs`, tests.
- Validation : tests (création ; nom déjà au catalogue, dossier hors projet, fichier existant refusés ; NaN, infini, énumération indéfinie, GUID de texture absent ou d'un autre type refusés ; propriété changée puis annulée ; affectation annulée) ; build.
- Commit : `feat(editor): add the remote material commands`

### ⏳ T2.3 — Particules

- Objectif : `particle.create` (preset), `particle.get`, `particle.set_property` (historique `Particle`), `particle.save`, `particle.drop` (P4, P5, P6). Les options de particules ne sont pas touchées ici (T3.1).
- Fichiers : `ParticleAssetInspectorPanel.cs` si besoin, `CasaEngine.Editor/Remote/*`, `GameEditor.Remote.cs`, tests.
- Validation : tests (création ; nom et fichier refusés comme T2.2 ; nombre maximal de particules au-delà de la borne refusé ; énumération numérique refusée ; propriété changée puis annulée ; dépôt annulé ; asset déposé contrôlé par P3) ; build.
- Commit : `feat(editor): add the remote particle commands`

### ⏳ T2.4 — Écrans UI

- Objectif : `screen.get` (arbre des nœuds : nom, type, propriétés modifiables), `screen.set_property` (`SetPropertyCommand`, historique de l'écran).
- Fichiers : `CasaEngine.Editor/Remote/*`, `GameEditor.Remote.cs`, tests.
- Validation : tests (nœud absent ; propriété hors registre, nom avec `{`, `:` ou `.`, valeur commençant par `{` refusés ; texte trop long refusé ; annulation) ; build.
- Commit : `feat(editor): add the remote UI screen commands`

### ⏳ T2.5 — Sons

- Objectif : `sound.create`, `sound.get`, `sound.set_property` (sans historique, P5), `sound.save`.
- Fichiers : `SoundAssetInspectorPanel.cs` (point d'entrée pour les commandes, même chemin que l'interface), `CasaEngine.Editor/Remote/*`, tests.
- Validation : tests (création ; nom et fichier refusés comme T2.2 ; valeurs JSON typées bornées ; fichier audio référencé contrôlé par P3 ; panneau de son modifié non fermé par `panel.close`) ; build.
- Commit : `feat(editor): add the remote sound commands`

## Phase 3 — Automatisation

### ⏳ T3.1 — Options de contenu sur le registre

- Objectif : P9 : exécuteur interne et options de contenu réécrites en appels de commandes. P11, P12 et P13 appliqués ; restauration et suppression de fin limitées à ce que l'automatisation a créé ou modifié. Correspondance (la sauvegarde instantanée et la restauration de fin restent celles d'aujourd'hui) :
  - `--entity`, `--entity-index`, `--component` → `selection.set` (la recherche par nom et index reste l'actuelle `FindAutomationEntity`, qui produit les identifiants) ;
  - `--open-asset` → `asset.open` (`.world` garde `world.open`) ;
  - `--activate-panel` → `panel.activate` (alias de `ResolveAutomationPanelId` gardés) ;
  - `--create-particle-asset` (+ `--create-particle-preset`) → `particle.create`, avec le nom unique que produit aujourd'hui `CreateUniqueParticleAssetPath` ;
  - `--set-particle-property` → `particle.set_property` puis `particle.save` (seul le fichier de l'asset est écrit, comme aujourd'hui) ;
  - `--set-material-property` → `material.set_property` puis `material.save` (idem) ;
  - `--drop-particle-asset` → `particle.drop` ;
  - `--set-screen-property` → `screen.set_property` (la sauvegarde reste celle d'aujourd'hui) ;
  - `--save-project` → `project.save` ;
  - `--content-folder` : navigation inchangée, mais la règle de chemin de P11 s'applique ;
  - options d'automatisation seule (`--content-scroll`, `--dock-resize`, `--dock-input-drag`, `--particle-undo-redo-smoke`, `--diagnostics-out`, `--play-smoke`, `--screenshot-out`, `--capture-delay`, `--keep-open`, `--remote`) et `--project` : inchangées.
- Fichiers : `GameEditor.cs` (`RunAutomation` et ses étapes), `CasaEngine.Editor/Remote/*`, tests.
- Validation : smokes S1 à S8 de T0.2 rejoués, artefacts identiques à la référence (seules différences admises : celles qu'annoncent P11 à P13, notées) ; un smoke de plus avec `--remote` vérifie que la fin d'automatisation ne restaure ni ne supprime une modification faite par commande à distance ; tests ; build.
- Commit : `refactor(editor): run the content automation flags through the command registry`

## Phase 4 — Documentation et vérification

### ⏳ T4.1 — Documentation et smoke de bout en bout

- Objectif : `docs/editor/editor-remote-control.md` (nouvelles commandes, plusieurs clients, contextes d'annulation, sons non annulables, changements P11 à P13), smoke de la Validation globale.
- Fichiers : `docs/editor/editor-remote-control.md`, `docs/README.md` si l'intitulé change, ce plan.
- Validation : smoke de bout en bout sur le vrai éditeur, chemins réussis de la Validation globale **et refus**, chacun noté avec sa réponse : fermer un panneau de matériau, de particules, d'écran et de son modifiés (refusé, fichier inchangé) ; `asset.open` d'un `.world` (refusé) ; `history.undo` avec un contexte sans panneau ouvert (refusé) ; référence dont le `FileName` du catalogue sort du projet (entrée préparée dans `AssetInfos.json` de la copie, refusée) ; création d'un asset sous un nom déjà au catalogue (refusée).
- Commit : `docs(editor): document the second remote control slice`

### ⏳ T4.2 — Vérification indépendante et recette de l'auteur

- Étapes : vérificateur neuf (contrat d'ADR-0093, P1 à P9, sécurité des chemins et des références, comportements manuels des panneaux inchangés, automatisation identique) ; recette de l'auteur.
- Commit : `docs(ai-agent): record the check of the second remote control slice`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Valider ou modifier P1 à P13 avant l'exécution, en particulier P11, P12 et P13 (changements de comportement de l'automatisation). | T0.1 |
| O2 | Forme du matériau par défaut créé par `material.create` : à lire dans le code ; sinon ⚠️ Blocked. | T2.2 |
| R1 | Risque : T3.1 réécrit des étapes d'automatisation dont dépendent des smokes ; atténuation : smokes rejoués avant et après, mêmes fichiers restaurés. | T3.1 |
| R2 | Risque : plusieurs clients en même temps (P1) ; atténuation : une requête par client, file unique vers le fil du jeu, tests de concurrence. | T1.1 |

## Hors périmètre

- Ménage (pousser `main`, supprimer le worktree et les branches fusionnées) : après ce plan, avec l'accord de l'auteur.
- Historique d'annulation des sons ; docking et redimensionnement des panneaux ; autres types d'assets (sprites, animations, TileMaps, mixeurs audio, modèles).
- Avis A5 (P10).
