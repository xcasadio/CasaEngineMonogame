# Plan d'implementation — Panels contextuels et selection globale

## Contexte

`CasaEngine.Editor` utilise aujourd'hui un systeme de layout special par type d'editeur.
Le `MGDockHost` change de disposition selon que le document actif soit un world, un ecran UI ou un material.

Ce modele n'est pas viable :
- le shell docking devient instable
- le code de `Game1` melange layout, selection et contexte metier
- ajouter un nouvel editeur impose de toucher au systeme de layout global

L'objectif de ce plan est d'introduire une architecture simple :
- un shell docking unique et persistant
- des panels semantiques `Hierarchy`, `Inspector`, `Toolbox`
- un contexte d'editeur global qui projette le document actif et la selection active
- des vues specialisees branchees sur ces panels selon le type de document

---

## Objectif

Mettre en place un systeme ou :
- le layout du `MGDockHost` ne depend plus du type d'editeur actif
- `Entities` devient un panel generique `Hierarchy`
- `Details` devient un panel generique `Inspector`
- la selection globale expose un element principal et un compteur de selection
- l'ajout d'un nouvel editeur se fait en enregistrant un nouveau type de document et ses vues contextuelles

---

## Regles obligatoires pour l'agent IA

- Langue du document : francais
- Langue du code : anglais
- Pas de refactor non relie au sujet
- Toujours garder un build compilable
- Validation ciblee avant de considerer une tache terminee
- Si une ancienne architecture devient obsolete, la supprimer ou la neutraliser clairement

---

## Legende des statuts

- ⏳ A faire
- 🚧 En cours
- ✅ Termine
- 🧪 A valider
- ⚠️ Bloque

---

## Architecture cible

### 1. Shell unique

Le shell garde une seule disposition par defaut :
- gauche : `Hierarchy` et `Toolbox`
- centre : documents
- droite : `Inspector`
- bas : `Content Browser` et `Output`

Le shell ne change plus automatiquement quand on change d'editeur.

### 2. Contexte global

Un service `EditorContextService` devient la projection globale de l'etat editeur :
- document actif
- type de document actif
- selection active
- compteur de selection

Chaque editeur garde sa logique locale, mais publie son etat dans ce contexte global.

### 3. Panels contextuels

Les panneaux `Hierarchy`, `Inspector` et `Toolbox` deviennent des hosts contextuels.
Ils choisissent quelle vue afficher selon le type du document actif.

### 4. Vues specialisees

Les vues metier restent separees :
- world : hierarchy entites + inspector entite/composant
- screen : hierarchy controles + inspector proprietes + toolbox controles
- material : inspector material + hierarchy informative minimale

Ajouter un nouvel editeur revient a :
1. declarer un nouveau type de document
2. publier son contexte actif
3. enregistrer ses vues pour `Hierarchy`, `Inspector` et/ou `Toolbox`

---

## Decoupage des taches

### ✅ Phase 1 — Formaliser le nouveau socle

#### ✅ Tache 1.1 — Ajouter le contexte global d'editeur

**Objectif :** introduire des types explicites pour le document actif et la selection globale.

**A faire :**
1. Ajouter `EditorDocumentKind`
2. Ajouter `EditorSelectionKind`
3. Ajouter `EditorDocumentContext`
4. Ajouter `EditorSelectionState`
5. Ajouter `EditorContextService`

**Resultat attendu :**
- le shell peut raisonner sur un document et une selection globaux sans dependre d'un layout special

---

#### ✅ Tache 1.2 — Introduire un host contextuel reutilisable pour les panels

**Objectif :** fournir un container unique capable d'afficher la bonne vue selon le document actif.

**A faire :**
1. Ajouter un role de panel (`Hierarchy`, `Inspector`, `Toolbox`)
2. Ajouter une definition contextuelle de panel
3. Ajouter un host `ContextualDockPanelHost`

**Resultat attendu :**
- les panneaux generiques n'ont plus de logique metier hard-codee

---

### ✅ Phase 2 — Remplacer le layout special par un shell unique

#### ✅ Tache 2.1 — Ajouter un builder de layout shell unique

**Objectif :** remplacer les workspaces par une seule disposition persistable.

**A faire :**
1. Creer `EditorShellLayoutBuilder`
2. Definir un layout par defaut stable
3. Garder `World Viewport` comme document par defaut

**Resultat attendu :**
- le shell ne depend plus du type d'editeur actif

---

#### ✅ Tache 2.2 — Supprimer le systeme de workspace special

**Objectif :** retirer le code de bascule automatique de layout.

**A faire :**
1. Supprimer `EditorWorkspaceManager` et les layouts par workspace
2. Remplacer la persistance multi-layouts par un seul layout shell
3. Retirer le code de preservation speciale des panels communs

**Resultat attendu :**
- le `MGDockHost` garde toujours la meme topologie de shell

---

### ✅ Phase 3 — Migrer les panels vers les vues contextuelles

#### ✅ Tache 3.1 — Migrer `Hierarchy`

**Objectif :** brancher les vues world/screen/material sur le panel generique `Hierarchy`.

**A faire :**
1. Utiliser `EntitiesPanel` comme vue world
2. Utiliser `UIScreenHierarchyPanel` comme vue screen
3. Ajouter une vue material minimale dediee

**Resultat attendu :**
- `Hierarchy` change de contenu sans changer de place dans le layout

---

#### ✅ Tache 3.2 — Migrer `Inspector`

**Objectif :** brancher les vues world/screen/material sur le panel generique `Inspector`.

**A faire :**
1. Utiliser `EntityDetailsPanel` comme vue world
2. Utiliser `UIScreenInspectorPanel` comme vue screen
3. Ajouter `MaterialInspectorView` comme vue material

**Resultat attendu :**
- `Inspector` reste stable et affiche la bonne vue selon le document actif

---

#### ✅ Tache 3.3 — Rendre `Toolbox` contextuel

**Objectif :** garder le shell stable meme pour les outils specifiques au screen editor.

**A faire :**
1. Utiliser `UIScreenToolboxPanel` comme vue screen
2. Afficher un empty state propre pour les autres documents

**Resultat attendu :**
- plus besoin d'un layout screen special pour exposer la toolbox

---

### ✅ Phase 4 — Synchroniser la selection globale

#### ✅ Tache 4.1 — Projeter la selection world/screen/material dans le contexte global

**Objectif :** faire converger les selections locales vers un etat global unique.

**A faire :**
1. Projeter `EditorSelection` dans `EditorContextService`
2. Projeter `UIScreenSelectionService` dans `EditorContextService`
3. Projeter l'asset material actif dans `EditorContextService`

**Resultat attendu :**
- le shell peut adapter ses panels sans connaitre les details internes de chaque editeur

---

#### ✅ Tache 4.2 — Afficher un compteur de selection simple

**Objectif :** exposer la multi-selection sans complexifier l'UI.

**A faire :**
1. Ajouter un compteur de selection dans `EditorSelectionState`
2. Afficher ce compteur dans la synthese du panel world hierarchy
3. Ne pas introduire d'UI de multi-selection complexe

**Resultat attendu :**
- la multi-selection reste lisible via un simple compte

---

### ✅ Phase 5 — Nettoyage et validation

#### ✅ Tache 5.1 — Nettoyer les ids et la registry des panels

**Objectif :** remplacer les ids specialises par des ids semantiques de shell.

**A faire :**
1. Remplacer `Entities` par `Hierarchy`
2. Remplacer `Details` par `Inspector`
3. Garder les documents dynamiques pour screen/material

---

#### ✅ Tache 5.2 — Mettre a jour la documentation

**Objectif :** documenter la nouvelle architecture.

**A faire :**
1. Mettre a jour la doc de layout editeur
2. Expliquer comment brancher un nouvel editeur demain

---

#### ✅ Tache 5.3 — Valider par build cible

**Objectif :** verifier que l'editeur compile apres suppression du systeme special.

**Validation attendue :**
- `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`

---

## Resultat final attendu

- plus de layout special par type d'editeur
- un shell unique et stable
- une selection globale simple
- `Hierarchy` / `Inspector` / `Toolbox` contextuels
- architecture facile a etendre pour un futur editeur

---

## Validation realisee

- Build cible execute : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
- Resultat : succes
- Note : le projet conserve de nombreux avertissements historiques `CS8632` hors scope de cette tache