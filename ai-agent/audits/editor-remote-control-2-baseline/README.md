# Référence des smokes d'automatisation (2026-11-14)

Référence du plan [editor-remote-control-2-tasks.md](../../tasks/editor-remote-control-2-tasks.md), tâche T0.2, enregistrée avant toute modification de la deuxième tranche (`main` `069ca155`, éditeur construit en Debug). Lecture seule : T3.1 rejoue les mêmes smokes et compare.

Commande : `bash Tools/automation-smokes.sh <dossier>` (copie neuve de `Projects/SampleProject` par smoke, `--diagnostics-out` ajouté à chacun).

| Smoke | Options | Résultat de référence |
| --- | --- | --- |
| S1 | `--entity ground --activate-panel inspector` | panneau `panel_inspector` activé ; projet inchangé |
| S2 | `--open-asset NewLitDiffuseMaterial.material --set-material-property alpha_cutoff=0.5` | asset ouvert, propriété changée et sauvegardée, fichier restauré à la sortie ; projet identique à S1 |
| S3 | `--open-asset Particles/FireLoop_Minimal.particle --set-particle-property maxparticles=500 --particle-undo-redo-smoke` | trois lignes d'automatisation ; fichier restauré ; projet identique à S1 |
| S4 | `--create-particle-asset Particles` | `Particles\NewParticleEffect.particle` créé (preset `Default`) puis supprimé à la sortie ; projet identique à S1 |
| S5 | `--entity ground --drop-particle-asset Particles/SparkBurst_Additive.particle` | asset déposé sur `ground` (monde non sauvegardé) ; projet identique à S1 |
| S6 | `--open-asset Screens/main-menu.uiscreen --set-screen-property btnPlay:Content=Start` | `btnPlay.Content` passé de `Play` à `Start`, non sauvegardé ; projet identique à S1 |
| S7 | `--entity ground --save-project` | `DefaultWorld.world` réécrit (seul fichier différent de S1) |
| S8 | `--entity ground --play-smoke` | 21 lignes `[PlaySmoke]`, dont 15 `PASS` et aucun `FAIL` ; projet identique à S1 |

Fichiers par smoke : `sN-automation.txt` (lignes `[Automation]` et `[PlaySmoke]`, horodatage et chemins de la copie retirés), `sN-hashes.txt` (SHA-256 de chaque fichier de la copie après la sortie, `.casaeditor/` exclu), `sN-exit.txt` (code de sortie, 0 pour les huit).
