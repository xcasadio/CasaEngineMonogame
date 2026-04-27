# Plan d'implementation - Theme MGUI dark pour CasaEngine.Editor

## Contexte

L'objectif est de definir un theme visuel dark pour `CasaEngine.Editor`, proche de l'esthetique de l'editeur Unity montre en reference:

- surfaces sombres et neutres ;
- contrastes lisibles mais moderes ;
- accent bleu de selection ;
- densite compacte d'outillage ;
- docking, tabs, menus, arbres, listes, inspecteurs et overlays visuellement coherents.

La contrainte principale est la suivante:

- mettre **le maximum du look dans un ou plusieurs fichiers XAML** ;
- garder **le moins possible en C#** ;
- n'ajouter du C# que pour charger les assets XAML, exposer les proprietes encore non themables, ou brancher des `ControlTemplate`/styles sur des controles encore hybrides.

---

## Faits verifies dans le depot

### Capacites MGUI deja presentes

- `MGResources.LoadThemesFromXaml(...)` existe deja.
- `MGResources.LoadControlTemplatesFromXaml(...)` existe deja.
- `ThemeDefinition` et `ThemeDefinitionsDocument` permettent deja de definir un theme MGUI en XAML.
- Les `ControlTemplate` XAML sont supportes pour les controles deja migres vers le modele a `TemplatePart`.
- Les themes peuvent mapper un `ElementType` ou un `ControlTypeName` vers un template via `ThemeDefinition.ControlTemplates`.
- `MGResources` sait deja enregistrer des styles implicites et explicites via `AddImplicitStyle(...)` et `AddStyle(...)`.

### Limites actuelles importantes

- Il n'y a pas, a ce stade, de `LoadStylesFromXaml(...)` equivalent pour charger un document de styles partages depuis un fichier XAML autonome.
- Les styles XAML MGUI vivent surtout dans `Window.Styles` ou dans des sous-arbres XAML, puis sont appliques pendant le parsing.
- `CasaEngine.Editor` est aujourd'hui entierement code-first cote shell MGUI: il n'y a pas encore de fichiers `.xaml` dans `CasaEngine.Editor`.
- Une partie des controles MGUI reste encore hybride ou trop couplee au theme via du code imperatif.
- Une partie du chrome visuel de `CasaEngine.Editor` est encore codee directement dans les panels editoriaux.

### Points d'ancrage concrets

- `CasaEngine.Editor/GameEditor.cs`
  - initialise `MGDesktop` ;
  - appelle `LoadDefaultResources()` ;
  - fixe encore plusieurs defaults en C# ;
  - construit la fenetre principale et le shell en code.
- `MGUI/MGUI.Core/UI/Themes/BuiltInThemes.xaml`
  - contient les themes built-in existants.
- `MGUI/MGUI.Core/UI/Templates/BuiltInControlTemplates.xaml`
  - contient deja certains templates XAML built-in.
- `MGUI/MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs`
  - reste un point central pour les controles template-aware.
- `MGUI/Docs/audit-theme-style-runtime-deep.md`
  - documente clairement quelles familles de controles sont deja themables proprement et lesquelles restent hybrides.

---

## Vision cible

Obtenir une architecture ou:

- la palette, les backgrounds, les brosses d'etat, les bordures, les tailles editoriales et les mappings de templates vivent dans des fichiers XAML dedies ;
- les controles deja template-aware basculent vers des `ControlTemplate` XAML editoriaux ;
- les controles encore hybrides recoivent seulement les plus petites extensions C# necessaires pour externaliser leur chrome ;
- les panels editoriaux de `CasaEngine.Editor` cessent de dupliquer des couleurs, paddings et opacites hard-codees ;
- le theme de l'editeur peut evoluer par edition des fichiers XAML sans repasser par un refactor general du shell.

---

## Direction visuelle cible

La cible n'est pas le theme `Dark_Blue` actuel de MGUI. Il faut tendre vers un dark theme outillage proche d'Unity:

- fond global quasi noir chaud ou neutre ;
- surfaces secondaires legerement plus claires ;
- borders sobres, jamais blanches ;
- texte principal gris clair, texte secondaire gris moyen ;
- selection et focus bleus ;
- composants compacts ;
- tabs et docking tres lisibles ;
- tooltips, menus contextuels et overlays visuellement alignes sur le shell.

Proposition de tokens visuels de depart a verifier pendant la phase d'audit:

- `Editor.Background = rgb(30,30,30)`
- `Editor.Surface = rgb(42,42,42)`
- `Editor.SurfaceRaised = rgb(51,51,51)`
- `Editor.Border = rgb(63,63,63)`
- `Editor.TextPrimary = rgb(210,210,210)`
- `Editor.TextSecondary = rgb(160,160,160)`
- `Editor.TextDisabled = rgb(110,110,110)`
- `Editor.Accent = rgb(58,121,187)`
- `Editor.AccentHover = rgb(77,146,214)`
- `Editor.Selection = rgba(58,121,187,160)`
- `Editor.Warning = rgb(201,145,53)`
- `Editor.Error = rgb(196,78,82)`

Note:

- pour une premiere iteration, conserver `JetBrainsMono` si l'editeur ne charge pas encore une police UI non monospace ;
- une passe police peut venir plus tard si on veut se rapprocher davantage d'Unity.

---

## Livrables attendus

### Assets XAML

1. `CasaEngine.Editor/Content/UI/Themes/CasaEditor.Dark.Theme.xaml`
2. `CasaEngine.Editor/Content/UI/Templates/CasaEditor.Dark.ControlTemplates.xaml`
3. `CasaEngine.Editor/Content/UI/Styles/CasaEditor.Dark.Styles.xaml`

Le fichier de styles n'est requis que si une solution propre est mise en place pour charger des styles partages depuis XAML.

### Glue C# minimal

4. un bootstrap de theme cote editeur, par exemple `CasaEngine.Editor/Styling/EditorThemeBootstrap.cs`
5. si necessaire, une petite extension MGUI pour charger des styles depuis XAML, par exemple `MGResources.LoadStylesFromXaml(...)`
6. si necessaire, des extensions minimales de `MGTheme`, `ThemePropertyTarget`, `ThemeDefinition` ou `MGControlTemplateCatalog`

### Validation

7. un sample MGUI ou un mode de preview pour verifier le theme sans lancer tout l'editeur a chaque iteration
8. des tests de chargement/theme/template quand des capacites framework sont ajoutees
9. une verification manuelle de `CasaEngine.Editor` avec les panels principaux ouverts

---

## Regles obligatoires pour l'agent IA

- Langue du document: francais
- Langue du code: anglais
- Un commit atomique par sous-tache terminee
- Toujours laisser le build dans un etat compilable avant commit
- Mettre a jour ce fichier apres chaque tache importante
- Ne pas introduire de WPF nouveau
- Ne pas construire un systeme WPF-like complet de `ResourceDictionary`, triggers generiques et styles dynamiques si un petit loader cible suffit
- Ne pas dupliquer une palette en C# si sa source de verite peut vivre dans le theme XAML
- Toute propriete themee qui change la mesure ou l'arrangement doit verifier explicitement l'invalidation correspondante
- Aucune allocation evitable dans les hot paths `Update`/`Draw`

---

## Legende des statuts

- `⏳ Todo`
- `🚧 In progress`
- `✅ Done`
- `🧪 Needs testing`
- `⚠️ Blocked`

---

## Strategie generale

### Principe 1 - XAML d'abord

Toujours preferer, dans cet ordre:

1. `ThemeDefinition` XAML
2. `ControlTemplate` XAML
3. styles XAML partages
4. C# minimal d'exposition ou de chargement

### Principe 2 - Extraire le chrome, pas la logique

Les controles doivent conserver en C#:

- leur logique metier ;
- leur input ;
- leur navigation ;
- leur comportement de layout ;
- leur draw custom quand il est structurel.

Le theme doit absorber:

- palette ;
- opacites ;
- brosses d'etat ;
- paddings et marges editoriales ;
- bordures ;
- mappings de templates ;
- variantes de tabs, menus, listes, docking.

### Principe 3 - Pas de faux 100% XAML

Ne pas pretendre qu'un controle purement custom-draw est deja totalement lookless s'il ne l'est pas. Dans ce cas:

- extraire ses tokens visuels dans `MGTheme` ou `ThemePropertyTarget` ;
- garder le draw en C# ;
- documenter clairement la limite.

---

## Matrice de migration par famille de controles

| Famille | Etat actuel | Levier prioritaire | Travail C# autorise | Priorite |
| --- | --- | --- | --- | --- |
| Docking (`MGDockTabItem`, `MGDockAutoHideDrawer`, `MGDockAutoHideStrip`, `MGDockSplitterBar`, `MGDockDropIndicators`) | mature | `ThemeDefinition` + `ControlTemplate` XAML | glue minimal | haute |
| `MGWindow`, `MGOverlay`, `MGContextMenu` | template-aware/hybride | `ThemeDefinition` + templates | nettoyage cible | haute |
| `MGListBox`, `MGListView`, `MGTreeView` | deja largement themables | theme + templates + quelques styles | faible | haute |
| `MGComboBox`, `MGTabControl`, `MGTextBox`, `MGToolTip` | hybrides | templates + extraction de tokens | moyenne | haute |
| `MGButton`, `MGToggleButton`, `MGCheckBox`, `MGRadioButton`, `MGProgressBar`, `MGProgressButton`, `MGSlider`, `MGResizeGrip`, `MGMenuBar` | couples au theme | extraction des tokens + theme | moyenne a forte | moyenne |
| `MGScrollViewer` | custom-draw | tokens de scrollbar dans `MGTheme` | faible a moyenne | moyenne |
| panels editoriaux `CasaEngine.Editor` | hard-codes locaux | tokens editoriaux + styles partages + suppression des couleurs inline | moyenne | haute |

---

## Choix d'architecture recommandes

### A. Themes

Creer un theme editeur dedie, par exemple `CasaEditor.Dark`, derive d'un built-in existant, probablement `Dark_Blue`, mais avec remplacement quasi complet de la palette.

Le theme devra couvrir au minimum:

- `Backgrounds` par `MGElementType`
- groupes `Window`, `Overlay`, `ContextMenu`, `ContextMenuItem`, `ListBox`, `ListView`, `ComboBox`, `TreeViewTemplate`, `TabControl`, `Docking`
- `Properties` pour les tokens top-level restants: flèches, scrollbars, selection, resize grip, checkmark, radio, progress, etc.
- `ControlTemplates` pour selectionner les templates editoriaux selon les familles de controles

### B. ControlTemplates

Creer des templates editoriaux dedies pour les controles ou le chrome doit changer significativement:

- fenetres ;
- overlays ;
- tooltips ;
- tabs et headers de tabs ;
- combo box et dropdown ;
- listes et arbres ;
- menus contextuels ;
- docking tabs/strips/drawers/splitters.

### C. Styles partages

Le depôt n'ayant pas aujourd'hui de loader global de styles XAML, il faut decider proprement entre deux branches:

#### Branche recommandee si on veut vraiment un theme XAML-first global

Ajouter une capacite framework minimale:

- racine `StyleDefinitionsDocument` ou equivalent ;
- `MGResources.LoadStylesFromXaml(...)` ;
- enregistrement des styles implicites via `AddImplicitStyle(...)` ;
- enregistrement des styles nommes via `AddStyle(...)`.

Cette branche est preferable si l'on veut partager proprement des styles pour:

- labels de section ;
- captions secondaires ;
- badges ;
- boutons d'outillage ;
- lignes d'inspecteur ;
- surfaces editoriales reutilisees dans plusieurs panels code-first.

#### Branche acceptable en premiere iteration

Ne pas ajouter de loader global de styles, et:

- garder les styles XAML uniquement dans des vues XAML dediees ;
- ou convertir seulement certains sous-arbres editoriaux en XAML ;
- ou utiliser un micro glue C# provisoire qui enregistre quelques styles partages, sans dupliquer la palette.

Important:

- ne pas ecrire un parseur ad hoc local a `CasaEngine.Editor` ;
- si un loader de styles est necessaire, il doit vivre dans MGUI et rester generique.

---

## Decoupage detaille

### Phase 0 - Audit et baseline

#### `✅` Tache 0.1 - Definir la reference visuelle editoriale

**Objectif :** fixer la direction artistique avant de coder des tokens.

**A faire :**

1. Extraire de la capture Unity les familles visuelles majeures:
   - fond global ;
   - fond de panneau ;
   - fond de tab selectionnee / non selectionnee ;
   - bordures ;
   - texte principal / secondaire ;
   - accent de selection et hover.
2. Ecrire une mini table de tokens cibles.
3. Valider si la densite visee est compacte ou standard.

**Sortie attendue :**

- palette de reference nominale ;
- noms de tokens stables ;
- pas de discussion ouverte sur les couleurs pendant les phases suivantes.

Execution :

- la direction retenue pour `CasaEditor.Dark` est desormais figee autour de surfaces neutres sombres, d'un accent bleu editorial et d'une densite compacte proche d'Unity ;
- cette reference a ete transformee en tokens stables cote theme XAML et en tokens editoriaux semantiques cote `EditorThemePalette` pour les panneaux encore code-first ;
- la baseline validee pour la suite est: chrome sombre neutre, contrastes moderes, focus/selection bleus, previews volontairement plus neutres que le shell.

#### `✅` Tache 0.2 - Inventorier les surcharges visuelles hard-codees dans l'editeur

**Objectif :** savoir exactement quoi sortir du code.

**A faire :**

1. Relever les hard-codes visuels dans:
   - `CasaEngine.Editor/GameEditor.cs`
   - `CasaEngine.Editor/Controls/ContentBrowserPanel.cs`
   - `CasaEngine.Editor/Controls/LogsPanel.cs`
   - `CasaEngine.Editor/Controls/EntitiesPanel.cs`
   - `CasaEngine.Editor/Controls/EntityDetailsPanel.cs`
   - `CasaEngine.Editor/Controls/UIScreenHierarchyPanel.cs`
   - `CasaEngine.Editor/Controls/UIScreenInspectorPanel.cs`
   - `CasaEngine.Editor/Controls/UIScreenPreviewPanel.cs`
   - `CasaEngine.Editor/Controls/UIScreenToolboxPanel.cs`
   - `CasaEngine.Editor/Controls/MaterialAssetInspectorPanel.cs`
   - `CasaEngine.Editor/Controls/MaterialPreviewViewport.cs`
   - `CasaEngine.Editor/Controls/AnimationClipPreviewPanel.cs`
   - `CasaEngine.Editor/ContentBrowser/Views/GridView.cs`
   - `CasaEngine.Editor/ContentBrowser/Controls/InlineRenameOverlay.cs`
2. Classer chaque hard-code:
   - palette globale ;
   - token editorial semantique ;
   - valeur locale legitime ;
   - comportement non themable.

**Sortie attendue :**

- une matrice claire `hard-coded -> theme/style/template/local`.

Execution :

- l'audit a confirme que `GameEditor.cs` portait le bootstrap de theme, que les assets MGUI generiques devaient aller en XAML, et que le chrome restant cote editeur se concentrait surtout dans `ContentBrowserPanel`, `GridView`, `InlineRenameOverlay`, `WorldViewportPanel`, `UIScreen*`, `MaterialAssetInspectorPanel`, `MaterialPreviewViewport` et `AnimationClipPreviewPanel` ;
- les panneaux `LogsPanel`, `EntitiesPanel` et `EntityDetailsPanel` n'ont pas revele de palette dupliquee significative sur la passe de nettoyage finale ;
- la matrice cible retenue est maintenant stable: theme/template pour le chrome MGUI generique, `EditorThemePalette` pour le chrome editor-specifique, et valeurs locales conservees seulement quand elles restent purement structurelles.

#### `✅` Tache 0.3 - Classer les controles MGUI par niveau de themabilite

**Objectif :** eviter de lancer l'agent sur tous les controles en meme temps.

**A faire :**

1. Partir de l'audit MGUI existant.
2. Confirmer les familles haute-priorite pour l'editeur:
   - docking ;
   - window/overlay/context menu ;
   - list/tree/tab/text input ;
   - scroll bars et boutons editoriaux.
3. Marquer les controles qui auront besoin d'un ajout C# avant de pouvoir etre pilotes en XAML.

**Sortie attendue :**

- une roadmap ordonnee par ROI visuel.

Execution :

- la priorite haute a ete confirmee sur `Window`, `Overlay`, `ContextMenu`, `ToolTip`, `ListBox`, `ListView`, `TreeView`, `ComboBox`, `TabControl` et le docking ;
- deux prealables framework ont ete identifies puis livres pour debloquer cette roadmap XAML-first: `ControlTemplateDefinition.BasedOn` et l'instanciation XAML de `MGResizeGrip` ;
- les controles encore fortement couples au code ont ete limites a une extraction de tokens stables plutot qu'a une fausse conversion lookless.

---

### Phase 1 - Bootstrap du theme editeur

#### `✅` Tache 1.1 - Creer l'asset `CasaEditor.Dark.Theme.xaml`

**Objectif :** centraliser la palette et les mappings de base.

**A faire :**

1. Creer un `ThemeDefinitionsDocument` avec au moins:
   - un theme de base editor ;
   - eventuellement un theme derive `CasaEditor.Dark.Compact` si la densite compacte est aussi voulue.
2. Declarer `BasedOn` sur un built-in existant.
3. Remplacer les couleurs bleues actuelles par des surfaces neutres et un accent bleu editorial.
4. Renseigner les groupes `Window`, `Overlay`, `ContextMenu`, `ListBox`, `ListView`, `ComboBox`, `TreeViewTemplate`, `TabControl`, `Docking` autant que possible.

**Acceptation :**

- le fichier parse ;
- le theme peut etre enregistre dans `MGResources` ;
- le shell recupere deja une identite dark unifiee sans toucher aux panels editoriaux.

Execution :

- asset ajoute sous `CasaEngine.Editor/Content/UI/Themes/CasaEditor.Dark.Theme.xaml` ;
- packaging projete vers la sortie via `CasaEngine.Editor.csproj` ;
- le theme couvre deja la palette de base, les backgrounds majeurs, les groupes `Window`, `Overlay`, `ContextMenu`, `ListBox`, `ListView`, `ComboBox`, `TreeViewTemplate`, `TabControl` et `Docking` ;
- les defaults editoriaux `DefaultTextBlockWrapText`, `DefaultTextBlockAutoWidthFromContent`, `DefaultButtonAutoWidthFromContent` et `DefaultComboBoxAutoWidthFromContent` sont prepares dans le theme XAML pour la tache suivante.

Commit : `5a949477` - `feat(editor-theme): add editor dark theme asset`
Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore`
Notes : le chargement runtime n'est pas encore branche ; cette tache livre seulement l'asset et son packaging.

#### `✅` Tache 1.2 - Brancher le chargement du theme dans l'editeur

**Objectif :** appliquer le theme XAML au bootstrap de `CasaEngine.Editor`.

**A faire :**

1. Ajouter un bootstrap minimal apres `LoadDefaultResources()`.
2. Charger le theme via `LoadThemesFromXaml(...)`.
3. Charger les templates via `LoadControlTemplatesFromXaml(...)` si le fichier existe deja.
4. Appliquer le theme sur le scope editor approprie.

**Regle :**

- le bootstrap ne doit pas embarquer la palette en dur ;
- il doit seulement charger les assets et choisir le theme.

Execution :

- ajout d'un bootstrap cible dans `GameEditor.cs` qui charge `CasaEditor.Dark.Theme.xaml` via `XamlDocumentSource.FromFile(...)` ;
- application du theme charge via `MGResources.DefaultTheme` ;
- chargement conditionnel des templates editoriaux si `CasaEditor.Dark.ControlTemplates.xaml` existe deja ;
- fallback propre sur le theme MGUI par defaut avec warning si l'asset n'est pas disponible ou si le chargement echoue.

Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore` -> succes ; warnings C# preexistants uniquement.
Commit : `b3656555` - `feat(editor-theme): load editor theme assets at startup`

#### `✅` Tache 1.3 - Nettoyer les defaults editoriaux deja poses en C#

**Objectif :** reduire le bruit visuel dans `GameEditor.cs`.

**A faire :**

1. Auditer les valeurs actuellement posees sur `_desktop.Theme`.
2. Si la valeur est deja pilotable par `ThemeDefinition`, la migrer dans le theme XAML.
3. Si elle n'est pas encore pilotable, laisser temporairement le C# mais ajouter un TODO precis dans le plan.

**Acceptation :**

- `GameEditor.cs` ne garde que le strict minimum non encore exprimable en XAML.

Execution :

- suppression cible des defaults `_desktop.Theme.DefaultTextBlockWrapText`, `_desktop.Theme.DefaultTextBlockAutoWidthFromContent`, `_desktop.Theme.DefaultButtonAutoWidthFromContent` et `_desktop.Theme.DefaultComboBoxAutoWidthFromContent` ;
- ces valeurs restent definies dans `CasaEditor.Dark.Theme.xaml` pour que le theme devienne la source de verite ;
- aucun autre nettoyage adjacent n'est introduit dans cette tache pour garder un commit atomique.

Validation : `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug --no-restore` -> succes ; warnings C# preexistants uniquement.
Commit : `53cc78e1` - `refactor(editor-theme): remove hardcoded editor theme defaults`

---

### Phase 2 - Templates XAML editoriaux

#### `✅` Tache 2.1 - Creer `CasaEditor.Dark.ControlTemplates.xaml`

**Objectif :** redefinir le chrome des controles template-aware avec une structure editoriale coherente.

**A faire :**

1. Ajouter des `ControlTemplate` nommes pour:
   - `Window`
   - `Overlay`
   - `ContextMenu`
   - `ToolTip`
   - `ListBox`
   - `ListView`
   - `TreeView`
   - `ComboBox`
   - `TabControl`
   - docking controls supportes
2. Respecter strictement les `TemplatePart` requises.
3. Garder les templates compacts pour ne pas exploser le nombre de noeuds visuels.

Execution :

- ajout d'un support `ControlTemplateDefinition.BasedOn` dans MGUI pour reutiliser les callbacks `ApplyDefaults` du catalogue avec une structure declaree en XAML ;
- ajout d'un test cible dans `MGUI.Tests` pour verifier qu'un template XAML herite bien du comportement de son template de base ;
- ce prealable debloque l'externalisation de `Window`, `Overlay`, `ComboBox`, `TabControl` et `TreeView` dans un asset editeur sans rebasculer leur chrome en C#.
- creation de `CasaEngine.Editor/Content/UI/Templates/CasaEditor.Dark.ControlTemplates.xaml` avec les structures editoriales pour `Window`, `ToolTip`, `Overlay`, `ContextMenu`, `ListBox`, `ListView`, `ComboBox`, `TreeView`, `TabControl` et des variantes `BasedOn` pour les controles de docking ;
- ajout d'un test de chargement d'asset reel dans `CasaEngine.Tests` pour verifier que le fichier se charge correctement depuis le depot.

Validation :

- `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~EditorControlTemplateAssetLoadingTests` -> succes ; le build embarque aussi `CasaEngine.Editor` dans la resolution des references.

Commit : `ca0449ee` - `feat(editor-theme): add editor control template asset`

#### `✅` Tache 2.2 - Mapper les templates depuis le theme

**Objectif :** faire du theme la source de verite pour le choix de skin.

**A faire :**

1. Utiliser `ThemeDefinition.ControlTemplates`.
2. Cibler par `ElementType` ou `ControlTypeName` selon le cas.
3. Verifier le comportement lors d'un changement de theme.

**Acceptation :**

- le meme controle garde son comportement et change de chrome via le theme.

Execution :

- ajout du bloc `ThemeDefinition.ControlTemplates` dans `CasaEditor.Dark.Theme.xaml` pour les controles standards editoriaux et les controles de docking via `ControlTypeName` ;
- extension du test de chargement d'assets pour verifier que le theme `CasaEditor.Dark` resolve bien chaque mapping attendu apres chargement des themes et templates XAML ;
- correction de deux declarations XAML invalides exposees par ce chargement reel (`ThemeListBoxSettingsDefinition.TitleForeground` et `ThemeListViewSettingsDefinition.HeaderForeground`) pour utiliser les objets `ThemeVisualStateColorSettingDefinition` attendus.

Validation : `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~EditorThemeAsset_Maps_Editor_Control_Templates` -> succes.

Commit : `dfc319c6` - `feat(editor-theme): map dark theme control templates`

#### `✅` Tache 2.3 - Verifier les templates avec l'outillage MGUI

**Objectif :** eviter les regressions de parts manquantes.

**A faire :**

1. Utiliser `UIToolingService.CaptureVisualTree(...)` si necessaire.
2. Verifier pour chaque template:
   - template applique ;
   - parts presentes ;
   - absence d'erreur de validation.

Execution :

- extension de `CasaEngine.Tests/UI/EditorControlTemplateAssetLoadingTests.cs` avec un scenario d'application reelle qui charge les assets `theme + templates`, cree un `MGDesktop` de test minimal, puis instancie `Window`, `ToolTip`, `Overlay`, `ContextMenu`, `ContextMenuItem`, `ListBox`, `ListView`, `ComboBox`, `TreeView`, `TabControl` et plusieurs controles de docking ;
- verification directe via les API publiques `AppliedControlTemplateName`, `TryGetTemplatePart(...)` et `LastControlTemplateError`, ce qui a rendu `UIToolingService.CaptureVisualTree(...)` inutile pour cette tache ;
- correction du fichier `CasaEditor.Dark.ControlTemplates.xaml` pour que `CasaEditor.ContextMenu` fournisse a la fois les parts de `MGWindow` et les parts de menu requises pendant la construction ;
- correction framework dans `MGUI` pour autoriser l'instanciation XAML de `MGResizeGrip` et rendre `MGContextMenu` tolerant a l'absence de `TitleBarTextBlockElement` pendant sa phase de construction.

Validation : `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~EditorControlTemplateAssetLoadingTests` -> succes (3 tests, 0 echec).

Commit : `46e19368` - `test(editor-theme): verify live control templates`

---

### Phase 3 - Strategie de styles partages

#### `✅` Tache 3.1 - Prendre une decision explicite sur les styles XAML globaux

**Objectif :** ne pas rester dans un entre-deux fragile.

**Decision gate :**

- si les besoins partages se limitent a quelques familles simples, rester sans loader global de styles ;
- si plusieurs panels code-first ont besoin des memes labels, badges, captions, boutons et lignes d'inspecteur, ajouter un loader minimal de styles.

Execution :

- decision explicite prise: **ne pas** ajouter de loader global `LoadStylesFromXaml(...)` pour cette iteration ;
- l'audit a montre que les besoins partages residuels tenaient dans un petit vocabulaire editorial semantique, pas dans un systeme de styles autonome ;
- la solution retenue est donc `ThemeDefinition` + `ControlTemplate` XAML pour le generique, puis `EditorThemePalette` pour le chrome editor-specifique qui reste code-first.

#### `✅` Tache 3.2 - Si necessaire, ajouter `LoadStylesFromXaml(...)` a MGUI

**Objectif :** rendre possible un vrai XAML-first pour les styles reutilisables de l'editeur.

**Portee volontairement limitee :**

- pas de triggers generiques ;
- pas de `ResourceDictionary` complet ;
- pas de re-style runtime global magique ;
- seulement le chargement de styles implicites et nommes deja compatibles avec le modele MGUI existant.

**A faire :**

1. Definir une racine de document de styles.
2. Parser les `Style` existants.
3. Enregistrer les styles via `AddImplicitStyle(...)` et `AddStyle(...)`.
4. Ajouter des tests de loader.

Execution :

- non necessaire sur cette iteration ;
- aucun `LoadStylesFromXaml(...)` n'a ete ajoute pour eviter d'ouvrir un axe framework de plus alors que le theme dark de l'editeur tenait deja avec les leviers existants.

#### `✅` Tache 3.3 - Creer `CasaEditor.Dark.Styles.xaml`

**Objectif :** centraliser les styles editoriaux reutilises.

**Styles candidats :**

- titre de section ;
- sous-titre ou caption ;
- bouton d'outillage compact ;
- badge d'etat ;
- label de champ ;
- ligne d'inspecteur ;
- zone de hint secondaire ;
- message vide ;
- panneau de surface editoriale standard.

**Acceptation :**

- les styles ne reintroduisent pas une palette parallele ;
- les styles consomment les tokens du theme et non des couleurs arbitraires.

Execution :

- fichier volontairement non cree ;
- la centralisation des styles reutilises a ete couverte par `CasaEditor.Dark.Theme.xaml`, `CasaEditor.Dark.ControlTemplates.xaml` et `CasaEngine.Editor/Styling/EditorThemePalette.cs`, sans palette parallele ni document XAML supplementaire.

---

### Phase 4 - Migration des controles MGUI par priorite

#### `✅` Tache 4.1 - Docking, shell, fenetres, overlays, menus

**Objectif :** obtenir l'essentiel du look Unity-like le plus vite possible.

**A faire :**

1. Finaliser les tokens `Docking`.
2. Themer:
   - tabs de docking ;
   - strips auto-hide ;
   - splitters ;
   - drop indicators ;
   - fenetres ;
   - menu bar ;
   - context menus ;
   - tooltips.

**Acceptation :**

- l'editeur a deja une silhouette recognisable de dark editor outillage.

Execution :

- le theme `CasaEditor.Dark` mappe des templates editoriaux pour `Window`, `Overlay`, `ContextMenu`, `ToolTip`, `ContextMenuItem` et les controles de docking (`MGDockTabItem`, `MGDockAutoHideDrawer`, `MGDockAutoHideStrip`, `MGDockSplitterBar`, `MGDockDropIndicators`) ;
- l'application runtime de ces templates est verifiee par les tests `EditorControlTemplateAssetLoadingTests`, qui couvrent aussi les parts critiques et les erreurs de validation de template.

#### `✅` Tache 4.2 - Listes, arbres, tabs et surfaces de navigation

**Objectif :** homogeniser les controles les plus visibles dans les panneaux.

**A faire :**

1. Finaliser `ListBox`, `ListView`, `TreeView`, `TabControl`.
2. Uniformiser:
   - alternance de lignes ;
   - selection ;
   - hover ;
   - foreground ;
   - bordures ;
   - headers.

Execution :

- `ListBox`, `ListView`, `TreeView` et `TabControl` sont maintenant couverts par les mappings du theme et leurs templates editoriaux ;
- les surfaces de navigation restantes cote editeur ont ete alignees via `EditorThemePalette` dans `ContentBrowserPanel`, `GridView`, `UIScreenHierarchyPanel` et `UIScreenInspectorPanel`.

#### `✅` Tache 4.3 - Text inputs et controles de saisie

**Objectif :** rendre les zones de saisie coherentes avec le shell.

**A faire :**

1. Stabiliser `TextBox`, `ComboBox`, `NumericUpDown`, `ToolTip`.
2. Extraire vers le theme les couleurs et paddings encore fixes dans le code.
3. Laisser la logique de caret, selection et draw en C# quand necessaire, mais alimentee par des tokens theme.

Execution :

- `ComboBox` et `ToolTip` sont thematises via `CasaEditor.Dark` et ses templates editoriaux ;
- `TextBox` et les aides de saisie editoriales restent code-first mais alimentes par des tokens semantiques (`InlineRenameOverlay`, `UIScreenPreviewPanel`, panneaux preview) ;
- aucun besoin supplementaire n'a ete confirme pour `NumericUpDown` dans le scope editor dark de cette iteration.

#### `✅` Tache 4.4 - Controles encore couples au code

**Objectif :** traiter les controles custom-draw sans sur-ingenierie.

**A faire :**

1. Pour `ScrollViewer`, `Slider`, `CheckBox`, `RadioButton`, `ProgressBar`, `ProgressButton`, `ResizeGrip`, `Button`, `ToggleButton`:
   - sortir les couleurs et epaisseurs stables ;
   - ne pas chercher a transformer integralement le draw en template si ce n'est pas justifie.
2. Ajouter des `ThemePropertyTarget` uniquement si le besoin est stable et reusable.

**Acceptation :**

- le look est pilotable par theme ;
- la logique de rendu reste performante.

Execution :

- les controles restant couples au code ont ete traites sans sur-ingenierie: extraction de tokens stables la ou le shell editeur en avait besoin, maintien du draw en C# ailleurs ;
- `MGResizeGrip` a recu le petit ajout framework necessaire pour que les templates editoriaux restent pilotables en XAML ;
- aucune nouvelle couche de theming generique n'a ete ajoutee pour les controles qui n'apportaient pas de ROI visuel clair dans l'editeur.

---

### Phase 5 - Extraction du chrome propre a CasaEngine.Editor

#### `✅` Tache 5.1 - Creer un petit vocabulaire de tokens editoriaux

**Objectif :** eviter de remplir les panels avec des couleurs brutes.

**Approche recommandee :**

- utiliser d'abord `ThemeDefinition` et `ThemePropertyTarget` quand le token est generique a plusieurs controles ;
- sinon, introduire un petit facade C# de lecture du theme, par exemple `EditorThemePalette`, mais sans dupliquer la source de verite.

**Tokens editoriaux probables :**

- surface de panneau ;
- surface levee ;
- surface de viewport preview ;
- fond subtil ;
- drop target ;
- badge info ;
- badge warning ;
- badge error ;
- couleur de grille de preview ;
- surbrillance de selection d'outil.

Execution :

- creation de `CasaEngine.Editor/Styling/EditorThemePalette.cs` avec des tokens semantiques pour les surfaces d'outillage, previews, overlays, inline rename, selection, drag and drop et badges d'etat ;
- les valeurs sont regroupees par intention (`ToolbarBackground`, `DropHighlight`, `PreviewSurfaceBackground`, `OverrideBadge`, etc.) pour eviter le retour des `new Color(...)` dupliques dans les panels.

#### `✅` Tache 5.2 - Migrer les panels editoriaux les plus visibles

**Ordre recommande :**

1. `ContentBrowserPanel`
2. `LogsPanel`
3. `EntitiesPanel`
4. `EntityDetailsPanel`
5. `UIScreenHierarchyPanel`
6. `UIScreenInspectorPanel`
7. `UIScreenPreviewPanel`
8. `UIScreenToolboxPanel`
9. `MaterialAssetInspectorPanel`
10. `MaterialPreviewViewport`
11. `AnimationClipPreviewPanel`
12. `InlineRenameOverlay`

**Regle :**

- toute couleur, opacite ou padding repris dans plusieurs panels doit sortir du panel ;
- une exception purement locale doit etre documentee.

Execution :

- migration effective des panneaux qui portaient le chrome le plus duplique: `ContentBrowserPanel`, `GridView`, `UIScreenHierarchyPanel`, `UIScreenInspectorPanel`, `UIScreenPreviewPanel`, `UIScreenToolboxPanel`, `MaterialAssetInspectorPanel`, `MaterialPreviewViewport`, `AnimationClipPreviewPanel`, `InlineRenameOverlay` et `WorldViewportPanel` ;
- les panneaux listant peu ou pas de palette hard-codee (`LogsPanel`, `EntitiesPanel`, `EntityDetailsPanel`) n'ont pas necessite d'extraction supplementaire sur cette iteration.

#### `✅` Tache 5.3 - Nettoyer les aides visuelles et overlays

**Objectif :** coherencer les surcouches editoriales.

**A faire :**

1. Uniformiser les backgrounds d'overlays et popups.
2. Uniformiser les zones de rename inline, messages vides, hints et erreurs.
3. Uniformiser les surbrillances de drag and drop.

Execution :

- uniformisation des popups et overlays editoriaux via `OverlayPopupBackground`, `InlineRenameBorder`, `InlineRenameInvalidBorder` et `DropHighlight` ;
- harmonisation des previews et des opacites de texte secondaires dans les panneaux de preview et les panneaux `UIScreen*`.

---

### Phase 6 - Sample et outillage de validation

#### `✅` Tache 6.1 - Ajouter un preview sample dedie au theme editor

**Objectif :** iterer sans relancer tout l'editeur sur chaque retouche.

**Options acceptables :**

1. un sample MGUI dedie dans `MGUI.Samples` ;
2. un ecran debug editor ;
3. un mode preview dans `CasaEngine.Editor` accessible facilement.

**Le sample doit montrer au minimum :**

- docking ;
- tabs ;
- list/tree ;
- text inputs ;
- context menu ;
- tooltip ;
- buttons et toggles ;
- overlays editoriaux.

Execution :

- ajout d'un sample dedie `MGUI.Samples/Features/EditorDarkThemePreview.xaml` avec enregistrement dans `Compendium` ;
- le sample charge les vrais assets `CasaEngine.Editor/Content/UI/Themes/CasaEditor.Dark.Theme.xaml` et `CasaEngine.Editor/Content/UI/Templates/CasaEditor.Dark.ControlTemplates.xaml`, puis previsualise `ComboBox`, `ListBox`, `ListView`, `TreeView`, `TabControl`, `ContextMenu`, `Overlay`, `ToolTip` ;
- tant que ce preview est visible, `Desktop.Resources.DefaultTheme` bascule temporairement vers `CasaEditor.Dark`, ce qui permet aussi d'ouvrir `DockingDemo` dans la meme session pour inspecter le docking avec le meme theme.

Validation : `dotnet build .\MGUI\MGUI.Samples\MGUI.Samples.csproj -c Debug --no-restore` -> succes.
Commits :

- sous-module `MGUI`: `2dc7993` - `feat(samples): add editor dark theme preview sample`
- sous-module `MGUI`: `906a89a` - `feat(samples): apply editor theme during preview session`

#### `✅` Tache 6.2 - Ajouter les tests de framework si MGUI est etendu

**A faire selon les changements :**

- tests pour `ThemeDefinition` si de nouveaux targets sont ajoutes ;
- tests pour le loader de styles si implemente ;
- tests de templates si de nouvelles parts sont requises ;
- tests de non-regression de scope/theme si le refresh change.

Execution :

- ajout de tests framework `MGUI.Tests` pour `ControlTemplateDefinition.BasedOn` ;
- ajout et extension des tests `CasaEngine.Tests/UI/EditorControlTemplateAssetLoadingTests.cs` pour le chargement des assets reels, les mappings de theme et l'application live des templates editoriaux.

---

### Phase 7 - Validation finale

#### `✅` Tache 7.1 - Validation compilee

**Minimum attendu :**

1. build cible de `CasaEngine.Editor.MonoGame.sln`
2. si MGUI est modifie, build et tests des projets touches

Execution :

- `dotnet build .\CasaEngine.Editor.MonoGame.sln -c Debug --no-restore` -> succes ;
- `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~EditorControlTemplateAssetLoadingTests` -> succes (3 tests) ;
- `dotnet build .\MGUI\MGUI.Samples\MGUI.Samples.csproj -c Debug --no-restore` -> succes ;
- `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -t:Compile -nologo -v:minimal` -> succes apres compactage des paddings par defaut non exposes au theme XAML ;
- `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -t:Compile -nologo -v:minimal` -> echec sur des erreurs preexistantes hors du perimetre des fichiers retouches ;
- correctif compile additionnel : `NvgSharp/src/XNA/NvgSharp.MonoGame.csproj` exclut explicitement `artifacts\**\*.cs` pour eviter de recompiler des `AssemblyInfo` generes sous `artifacts/agent-build/...`, ce qui dupliquait les attributs d'assembly ;
- `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -t:Compile -nologo -clp:ErrorsOnly` -> succes apres ce correctif ;
- `dotnet build .\CasaEngine.Editor.MonoGame.sln -nologo -clp:ErrorsOnly` -> succes apres ce correctif ;
- `get_errors` reste propre sur `CasaEditor.Dark.Theme.xaml`, `ComponentEditorBase.cs`, `MGButton.cs`, `MGTextBlock.cs` et `MGControlTemplateCatalog.cs`.

Commit : `3e4dc31` - `style(theme): tighten default control paddings`

#### `🧪` Tache 7.2 - Validation visuelle manuelle

**Checklist :**

1. shell principal coherent
2. docking lisible
3. tabs selectionnees et non selectionnees distinctes
4. listes et arbres coherents
5. inspecteurs lisibles
6. text boxes et combo boxes harmonises
7. context menus et tooltips alignes visuellement
8. overlays editoriaux non agressifs
9. pas de texte illisible ou d'accent trop faible
10. pas de template part manquante

Etat :

- encore a verifier manuellement dans `CasaEngine.Editor` et dans `MGUI.Samples` ;
- les validations automatisees couvrent deja les templates et le chargement d'assets, mais pas l'appreciation visuelle finale du shell.
- cette passe applique aussi le polish demande sur le theme sombre : bouton d'expander reduit au triangle seul, paddings verticaux compactes pour labels/boutons/textboxes/combobox, et parite `selected == hovered` pour `ListBox`/`ListView`, `TreeView`, items de `ComboBox` et `ContextMenu`.

#### `🧪` Tache 7.3 - Validation perf et robustesse

**A verifier :**

1. pas d'allocations nouvelles dans `Draw`/`Update` a cause du theme
2. pas de refresh de theme excessivement couteux sur le shell courant
3. pas de layout casse par une taille themee non invalidee
4. pas de fuite evidente de resources ou d'abonnements si un loader de styles/dynamic resources a ete touche

Etat :

- aucune allocation recurrente n'a ete introduite dans les hot paths touches cote editeur: la palette est statique et les changements portent surtout sur des valeurs de construction et de theme ;
- une passe profiler/inspection runtime reste souhaitable pour cloturer definitivement cette tache.

#### `✅` Tache 7.4 - Correctifs post-retour visuel

Points traites :

- `MGUI/MGUI.Core/UI/MGTreeViewItem.cs` applique maintenant la meme brosse de selection au header de noeud, au conteneur de header et au bouton expander, avec restauration explicite de l'etat initial a la deselection ;
- `MGUI/MGUI.Core/UI/MGTreeView.cs` expose la configuration des scrollbars internes du `TreeView`, puis `CasaEngine.Editor/Controls/EntityDetailsPanel.cs` active la scrollbar horizontale pour l'arbre des composants et pour la zone de details afin de laisser les editeurs prendre leur largeur naturelle ;
- `CasaEngine.Editor/Controls/UIScreenInspectorPanel.cs`, `CasaEngine.Editor/Controls/UIScreenHierarchyPanel.cs` et `CasaEngine.Editor/Controls/UIScreenToolboxPanel.cs` n'affichent plus de titre interne redondant quand le titre d'onglet docking est deja present.

Validation :

- `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -t:Compile -nologo -clp:ErrorsOnly` -> succes ;
- `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -t:Compile -nologo -clp:ErrorsOnly` -> succes apres activation du scroll horizontal dans l'inspector ;
- `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -t:Compile -nologo -clp:ErrorsOnly` -> succes apres suppression des titres internes redondants.

---

## Points d'entree techniques probables

### Cote editor

- `CasaEngine.Editor/GameEditor.cs`
- `CasaEngine.Editor/Controls/...`
- `CasaEngine.Editor/ContentBrowser/...`

### Cote MGUI runtime

- `MGUI/MGUI.Core/UI/MGTheme.cs`
- `MGUI/MGUI.Core/UI/MGResources.cs`
- `MGUI/MGUI.Core/UI/XAML/ThemeDefinitionLoader.cs`
- `MGUI/MGUI.Core/UI/XAML/Element.cs`
- `MGUI/MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs`
- `MGUI/MGUI.Core/UI/Templates/BuiltInControlTemplates.xaml`
- `MGUI/MGUI.Core/UI/Themes/BuiltInThemes.xaml`
- controles hybrides ou couples:
  - `MGComboBox.cs`
  - `MGTabControl.cs`
  - `MGTextBox.cs`
  - `MGToolTip.cs`
  - `MGMenuBar.cs`
  - `MGButton.cs`
  - `MGToggleButton.cs`
  - `MGCheckBox.cs`
  - `MGRadioButton.cs`
  - `MGProgressBar.cs`
  - `MGProgressButton.cs`
  - `MGScrollViewer.cs`
  - `MGSlider.cs`
  - `MGResizeGrip.cs`

### Cote tests

- `MGUI/MGUI.Tests/Architecture/ThemeDefinitionTests.cs`
- `MGUI/MGUI.Tests/Architecture/ControlTemplateLoaderTests.cs`
- nouveaux tests si `LoadStylesFromXaml(...)` est ajoute

---

## Definition of Done

La tache sera consideree terminee quand:

1. `CasaEngine.Editor` charge un theme editor dedie depuis XAML.
2. Les templates editoriaux principaux sont choisis par le theme, pas en dur dans le shell.
3. Les couleurs/paddings/opacites partages ne sont plus disperses dans les panels editoriaux.
4. Les controles majeurs visibles dans l'editeur sont soit themables en XAML, soit accompagnes d'une justification claire pour la part residuelle en C#.
5. Un sample ou preview permet de verifier rapidement le theme.
6. Le build passe.
7. Les tests ajoutes pour le framework passent.
8. Les limitations restantes sont documentees en fin de fichier.

---

## Limitations acceptees pour une premiere iteration

- certains controles custom-draw peuvent rester partiellement pilotes par C# si seuls leurs tokens sont externalises ;
- l'absence initiale d'un loader global de styles peut etre toleree si la valeur est deja majoritairement apportee par le theme et les templates ;
- la police UI peut rester celle deja chargee par l'editeur en v1.

---

## Risques a surveiller

- ajouter trop de tokens editoriaux specifiques dans `MGTheme` au lieu de garder un contrat stable ;
- vouloir faire un faux WPF complet alors qu'un loader cible suffit ;
- casser l'invalidation layout en sortant des tailles depuis le theme ;
- multiplier les wrappers visuels et degrader le cout du shell ;
- laisser coexister plusieurs sources de verite de palette ;
- convertir massivement des panels en XAML alors qu'un theme + templates + quelques styles suffisent.

---

## Ordre de commits recommande

1. `feat(editor-theme): add editor dark theme xaml bootstrap`
2. `feat(editor-theme): add editor control templates xaml`
3. `feat(mgui): add xaml style loader` si necessaire
4. `refactor(mgui): expose missing theme tokens for editor controls`
5. `refactor(editor-theme): migrate docking and shell chrome`
6. `refactor(editor-theme): migrate list tree tab input controls`
7. `refactor(editor-theme): migrate editor panels to shared tokens`
8. `test(mgui): cover theme template style loading`
9. `docs(editor-theme): document residual limitations`

---

## Notes finales pour l'agent

- Prioriser le ROI visuel: docking, shell, menus, tabs, listes, text inputs.
- Ne pas commencer par les panels les plus specifiques si le socle theme/template n'est pas en place.
- Si un style ne peut vivre en XAML qu'au prix d'un petit loader framework generique, faire ce loader plutot que de rebasculer massivement sur du code imperatif editor-only.
- A chaque fois qu'une valeur reste en C#, repondre explicitement a la question: "pourquoi cette valeur ne peut-elle pas encore vivre dans le theme XAML ?"