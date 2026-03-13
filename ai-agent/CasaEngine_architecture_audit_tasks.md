# CasaEngine Architecture Audit Tasks

## Contexte

CasaEngine est un moteur de jeu C# / MonoGame.
L’objectif de cet audit est de vérifier si le moteur est bien architecturé pour intégrer un framework UI de type MGUI / XAML selon une approche moderne inspirée de NoesisGUI.

L’architecture cible attendue côté moteur est la suivante :

- le moteur ne doit pas contenir l’UI “en dur” partout
- il doit pouvoir héberger un runtime UI externe ou semi-externe
- il doit distinguer :
  - **Screen** = logique d’usage / navigation
  - **View** = instance UI runtime
  - **Surface** = cible de rendu
- il doit pouvoir supporter :
  - HUD
  - menus
  - overlays
  - modals
  - render texture UI dans le monde
  - split-screen
  - panneaux éditeur / outils
  - plusieurs vues simultanées

L’agent ne doit pas refactorer dans cette phase.
Il doit auditer, documenter les écarts, puis générer un second fichier de petites tâches de refactor si nécessaire.

---

## Objectif principal

Vérifier si CasaEngine est bien prêt à accueillir une architecture UI moderne avec :

- un service UI central
- plusieurs vues UI
- plusieurs surfaces de rendu
- un pipeline de composition explicite
- un routage input propre
- une séparation claire entre runtime jeu, éditeur, et UI

---

## Contraintes

- Ne pas modifier l’architecture durant la phase d’audit
- Ne pas casser le moteur
- Privilégier l’analyse de la structure existante
- Être concret et relier les constats au code
- Produire des tâches de refactor petites et actionnables si nécessaire

---

## Livrables attendus

L’agent doit produire **au minimum** :

1. `docs/architecture/CasaEngine_architecture_audit_report.md`
2. `docs/architecture/CasaEngine_architecture_refactor_tasks.md`  
   Ce second fichier ne doit être créé que si des modifications sont réellement nécessaires.

---

## Format attendu du rapport d’audit

Le fichier `CasaEngine_architecture_audit_report.md` doit contenir :

1. **Résumé exécutif**
2. **Architecture actuelle observée**
3. **Capacité d’intégration UI actuelle**
4. **Écarts par rapport à l’architecture cible**
5. **Risques techniques**
6. **Points forts**
7. **Priorités de refactor**
8. **Verdict final**
   - Conforme
   - Partiellement conforme
   - Non conforme

---

## Architecture cible à vérifier

### 1. Hébergement d’un runtime UI
Le moteur doit idéalement pouvoir fournir à un framework UI :

- timing
- input brut
- viewport / taille logique
- render targets
- textures / fonts / asset loading
- ordre de composition dans la frame
- services d’éditeur si besoin

### 2. Notion de UIView
Le moteur doit idéalement pouvoir héberger plusieurs vues UI indépendantes.

Exemples :
- HUD joueur 1
- HUD joueur 2
- menu pause
- panneau inspecteur
- fenêtre outil
- terminal 3D rendu dans une texture

### 3. Notion de Surface
Le moteur doit distinguer les surfaces de rendu :

- backbuffer principal
- viewport de caméra
- render target
- panneau d’éditeur
- texture utilisée dans le monde 3D

### 4. Séparation Screen / View / Surface
Le moteur ne doit pas confondre :

- navigation d’écrans
- instance runtime UI
- cible de rendu

### 5. Pipeline de composition explicite
Le moteur doit permettre de définir clairement :

- quand la 3D est rendue
- quand l’UI est rendue
- si l’UI peut être rendue offscreen
- si l’UI peut ensuite être composée à l’écran ou dans le monde

### 6. Input routing
Le moteur doit permettre de router l’input :

- vers le gameplay
- vers l’UI
- vers une vue UI spécifique
- vers une fenêtre ou surface donnée
- avec gestion du focus et du mode modal

### 7. Intégration éditeur
Le moteur doit pouvoir, si nécessaire, supporter :

- plusieurs vues
- panneaux dockables
- fenêtres d’outils
- visualisation simultanée de scènes / assets / inspecteurs

---

## Tâches détaillées

### Phase 1 — Cartographie générale du moteur

#### Tâche 1.1
Lister les projets, dossiers et namespaces liés à :

- boucle de jeu
- rendu
- scènes / mondes
- caméras
- viewport
- input
- UI existante
- éditeur
- services globaux
- gestion de fenêtres / vues si elle existe

#### Tâche 1.2
Identifier la boucle principale du moteur.

#### Tâche 1.3
Identifier les points d’entrée de composition de frame.

#### Tâche 1.4
Cartographier les services déjà présents qui pourraient héberger l’UI.

---

### Phase 2 — Vérification de la gestion des vues et surfaces

#### Tâche 2.1
Identifier si le moteur possède déjà une notion de vue, viewport, panel, host ou render surface.

#### Tâche 2.2
Identifier si plusieurs caméras / viewports peuvent être actives en parallèle.

#### Tâche 2.3
Vérifier comment les render targets sont créées et consommées.

#### Tâche 2.4
Vérifier si une surface de rendu abstraite existe déjà ou si tout est implicitement branché au backbuffer.

#### Tâche 2.5
Déterminer si une UI pourrait être rendue :
- plein écran
- dans un viewport
- dans une texture
- dans une fenêtre/panneau d’éditeur

---

### Phase 3 — Vérification du pipeline de frame

#### Tâche 3.1
Documenter l’ordre exact de la frame :
- update
- rendu monde
- post-process
- UI
- overlays

#### Tâche 3.2
Identifier où l’UI serait injectée aujourd’hui.

#### Tâche 3.3
Vérifier si plusieurs passes UI seraient possibles.

#### Tâche 3.4
Vérifier si l’UI offscreen puis onscreen est faisable proprement.

#### Tâche 3.5
Identifier les couplages qui empêcheraient une composition flexible.

---

### Phase 4 — Vérification de l’input

#### Tâche 4.1
Cartographier la chaîne input complète :
- collecte
- état courant
- dispatch
- consommation
- priorisation

#### Tâche 4.2
Vérifier si l’input peut être routé vers autre chose que le gameplay principal.

#### Tâche 4.3
Vérifier si une notion de focus existe.

#### Tâche 4.4
Vérifier si une notion de capture souris ou de mode modal existe ou pourrait être ajoutée.

#### Tâche 4.5
Déterminer si plusieurs vues/surfaces pourraient recevoir un input ciblé.

---

### Phase 5 — Vérification des services d’intégration UI

#### Tâche 5.1
Identifier s’il existe déjà :
- un gestionnaire d’assets
- un gestionnaire de textures
- un gestionnaire de fonts
- un service de timing
- un service de fenêtres/vues
- un système de scaling / DPI / résolution

#### Tâche 5.2
Vérifier si ces services sont facilement injectables dans un runtime UI.

#### Tâche 5.3
Identifier les dépendances globales/singletons qui coupleraient trop l’UI au moteur.

---

### Phase 6 — Vérification de la séparation Screen / View / Surface

#### Tâche 6.1
Chercher si le moteur possède déjà une notion de screen manager, scene manager, panel manager, overlay manager ou équivalent.

#### Tâche 6.2
Déterminer si les concepts d’écran, de vue et de surface sont aujourd’hui mélangés.

#### Tâche 6.3
Documenter ce qui manquerait pour arriver au modèle :

- `UIScreen` ou équivalent
- `UIViewHost`
- `UIView`
- `UISurface`
- `UIInputRouter`
- `UICompositionService`

---

### Phase 7 — Vérification du support éditeur

#### Tâche 7.1
Identifier comment le moteur gère actuellement les besoins éditeur.

#### Tâche 7.2
Vérifier si plusieurs vues simultanées sont possibles pour :
- scène
- asset preview
- inspecteur
- UI tools

#### Tâche 7.3
Vérifier si l’architecture se prête à des panneaux/docks/hôtes multiples.

#### Tâche 7.4
Identifier les limitations bloquantes pour un éditeur moderne.

---

### Phase 8 — Vérification du support split-screen et multi-contexte

#### Tâche 8.1
Vérifier si plusieurs caméras et plusieurs viewports sont correctement abstraits.

#### Tâche 8.2
Vérifier si chaque viewport pourrait avoir sa propre UI.

#### Tâche 8.3
Vérifier si les données de taille, scaling, input et composition sont localisables par vue.

#### Tâche 8.4
Déterminer si l’architecture est prête pour :
- split-screen local
- multi-panel editor
- world-space UI

---

### Phase 9 — Conclusion et plan de refactor

#### Tâche 9.1
Classer chaque écart selon :
- bloquant
- important
- amélioration souhaitable

#### Tâche 9.2
Pour chaque écart, relier :
- problème
- impact
- classes/fichiers concernés
- direction de refactor

#### Tâche 9.3
Créer `docs/architecture/CasaEngine_architecture_refactor_tasks.md` si nécessaire.

---

## Format obligatoire du fichier de refactor

Le fichier `CasaEngine_architecture_refactor_tasks.md` doit être structuré en petites tâches atomiques.

Chaque tâche doit contenir :

- un identifiant (`CASA-ARCH-001`)
- un titre
- un contexte
- l’objectif
- les fichiers / classes concernés
- la modification attendue
- les critères d’acceptation
- les dépendances éventuelles

Exemple :

### CASA-ARCH-001 — Introduire une abstraction de UISurface
**Contexte**  
L’audit montre que l’UI ne peut être composée que sur le backbuffer principal.

**Objectif**  
Permettre à l’UI d’être rendue sur différentes surfaces.

**Fichiers concernés**  
- `...`

**Modification attendue**  
Créer une abstraction de surface de composition UI indépendante du backbuffer.

**Critères d’acceptation**  
- Une surface écran principal est supportée
- Une surface render target est supportée
- La composition UI n’est plus codée en dur sur une seule cible

**Dépendances**  
Aucune

---

## Règles de travail de l’agent

- Toujours s’appuyer sur le code réel
- Citer les classes/fichiers précis
- Ne pas produire de recommandations vagues
- Préférer de petites tâches concrètes
- Ne pas refactorer immédiatement
- Signaler explicitement les zones ambiguës
- Justifier les conclusions

---

## Critère de réussite global

L’audit est réussi si un développeur peut, en lisant uniquement les fichiers produits :

- comprendre si CasaEngine est prêt ou non pour intégrer proprement MGUI
- comprendre les écarts avec l’architecture cible
- disposer d’une liste claire de petites tâches de refactor
- lancer ensuite un autre agent IA pour exécuter ces tâches progressivement