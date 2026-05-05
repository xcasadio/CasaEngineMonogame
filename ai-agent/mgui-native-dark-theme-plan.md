# Plan IA - rendre le theme Dark natif dans MGUI

Date: 2026-05-05

## Objectif

Le theme dark actuellement porte par `CasaEngine.Editor` doit devenir un theme natif de `MGUI.Core`. L'utilisateur doit pouvoir choisir nativement entre le theme existant de MGUI, affiche dans le sample comme `Blueprint`, et un vrai theme `Dark` base sur le rendu actuel de l'editeur CasaEngine.

Le sample MGUI qui montre le changement de theme doit utiliser ce nouveau theme natif. Il ne doit plus charger un theme depuis `CasaEngine.Editor`, et il ne doit plus contenir un faux theme dark different de celui de l'editeur.

## Faits verifies dans le depot

- Le theme source est aujourd'hui dans `CasaEngine.Editor/Content/UI/Themes/CasaEditor.Dark.Theme.xaml`.
- Les templates source sont aujourd'hui dans `CasaEngine.Editor/Content/UI/Templates/CasaEditor.Dark.ControlTemplates.xaml`.
- `CasaEngine.Editor/GameEditor.cs` charge ces deux fichiers depuis `AppContext.BaseDirectory` puis applique `CasaEditor.Dark`.
- `MGUI.Core` possede deja des themes XAML natifs dans `MGUI/MGUI.Core/UI/Themes/BuiltInThemes.xaml`.
- `MGUI.Core` possede deja des templates XAML natifs dans `MGUI/MGUI.Core/UI/Templates/BuiltInControlTemplates.xaml`.
- `MGTheme.BuiltInTheme` expose actuellement `Light_Gray` et `Dark_Blue`.
- `new MGTheme(string fontFamily)` utilise encore `Dark_Blue` par defaut.
- Le sample `MGUI.Samples/Features/EditorDarkThemePreview.*` charge le theme depuis `CasaEngine.Editor`, ce qui doit disparaitre.
- Le sample `MGUI.Samples/Features/StyleThemeRefactor.*` contient actuellement un exemple `BlueprintSkin` et `LedgerSkin`. Si la branche locale a deja `Blueprint` et `Dark`, remplacer le theme dark local par le nouveau built-in MGUI.

## Regles obligatoires pour l'agent implementant ce plan

- Avant de commencer une tache, changer son icone de titre en `🚧`.
- A la fin d'une tache validee, changer son icone de titre en `✅`.
- Si le code compile mais qu'une validation manuelle reste a faire, utiliser `🧪`.
- Si la tache est impossible sans decision humaine, utiliser `⚠️` et ecrire le blocage sous la tache.
- Committer apres chaque tache terminee. Le commit doit inclure le changement de statut de cette tache.
- Ne pas committer de changements non lies. Toujours verifier `git status --short` avant `git add`.
- Ne pas supprimer ni renommer `Dark_Blue` ou `Light_Gray`: compatibilite API prioritaire.
- Ne pas ajouter de LINQ, closures ou allocations dans `Update`/`Draw`.
- Ne pas utiliser de scripts d'ecriture ad hoc. Faire les edits avec l'outil de patch de l'agent.
- Si un build echoue sur un probleme non lie et deja present, noter l'erreur exacte et mettre la tache en `🧪 Needs testing` seulement si les changements de la tache sont coherents.

## Convention de nommage cible

- Theme natif a ajouter: `Dark`.
- Theme existant conserve: `Dark_Blue`.
- Label UI du sample pour le theme existant: `Blueprint`.
- Templates natifs dark: prefixe `Dark.` au lieu de `CasaEditor.`.
- Exemples de templates cibles: `Dark.Window`, `Dark.ToolTip`, `Dark.Overlay`, `Dark.ContextMenu`, `Dark.ListBox`, `Dark.ListView`, `Dark.ComboBox`, `Dark.TreeView`, `Dark.TabControl`, `Dark.DockTabItem`.

Ne pas garder `CasaEditor.Dark` comme nom principal dans MGUI.Core. Une alias de compatibilite peut exister temporairement seulement si cela evite une grosse migration en une seule tache, mais l'etat final doit utiliser `Dark` cote MGUI et cote editor.

## Politique de police

Le theme editeur source declare `DefaultFontFamily="JetBrainsMono"`. Pour un theme natif MGUI, ne pas imposer une police qui n'est pas livree par MGUI sauf si l'agent ajoute aussi cette police proprement aux assets MGUI.

Decision recommandee pour la premiere passe:

- Dans le theme natif `Dark`, garder les tailles de police du theme editeur.
- Retirer `DefaultFontFamily="JetBrainsMono"` ou le laisser absent pour que `DefaultFontFamily` vienne du caller (`DefaultFontFamily` passe au constructeur `MGTheme`).
- Si `CasaEngine.Editor` veut encore `JetBrainsMono`, l'appliquer cote editor apres creation du theme, avec verification que la police est disponible, ou accepter le fallback existant.

## Commandes de validation utiles

```powershell
dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore
dotnet test .\MGUI\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore
dotnet build .\MGUI\MGUI.Samples\MGUI.Samples.csproj -c Debug --no-restore
dotnet build .\CasaEngine.Editor.MonoGame.sln -c Debug --no-restore
```

Si les sorties editor sont verrouillees, utiliser temporairement:

```powershell
dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug -t:Compile --no-restore
```

## Taches

### ✅ T01 - Audit baseline theme et sample

But: verifier l'etat exact de la branche avant d'editer.

Fichiers a lire:

- `CasaEngine.Editor/GameEditor.cs`
- `CasaEngine.Editor/Content/UI/Themes/CasaEditor.Dark.Theme.xaml`
- `CasaEngine.Editor/Content/UI/Templates/CasaEditor.Dark.ControlTemplates.xaml`
- `MGUI/MGUI.Core/UI/MGTheme.cs`
- `MGUI/MGUI.Core/UI/Themes/BuiltInThemes.xaml`
- `MGUI/MGUI.Core/UI/Templates/BuiltInControlTemplates.xaml`
- `MGUI/MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs`
- `MGUI/MGUI.Samples/Features/StyleThemeRefactor.xaml.cs`
- `MGUI/MGUI.Samples/Features/EditorDarkThemePreview.xaml.cs`

Actions:

1. Lancer `git status --short`.
2. Noter les fichiers deja modifies par l'utilisateur et ne pas les inclure dans les commits.
3. Lancer une recherche sur `CasaEditor.Dark`, `LedgerSkin`, `BlueprintSkin`, `EditorDarkThemePreview`.
4. Confirmer si le sample local parle de `Ledger` ou deja de `Dark`.
5. Lancer au minimum `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore`.

Validation:

- Le build MGUI.Core passe, ou l'erreur baseline est documentee dans cette section.
- Les noms reels du sample sont connus.

Commit attendu:

```powershell
git add ai-agent/mgui-native-dark-theme-plan.md
git commit -m "plan: audit native mgui dark theme migration"
```

Resultat du 2026-05-05:

- `git status --short` initial contenait deja `Projects/RPGDemo/CasaEngine.RPGDemo.dll` et `Projects/RPGDemo/CasaEngine.RPGDemo.pdb`; exclus de tous les commits de cette migration.
- Le sample local est encore en mode `BlueprintSkin` / `LedgerSkin` dans `MGUI.Samples/Features/StyleThemeRefactor.xaml.cs`.
- `MGUI.Samples/Features/EditorDarkThemePreview.xaml.cs` charge encore `CasaEngine.Editor\Content\UI\Themes\CasaEditor.Dark.Theme.xaml` et `CasaEngine.Editor\Content\UI\Templates\CasaEditor.Dark.ControlTemplates.xaml`.
- Baseline valide: `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore` a reussi avec warnings XML doc uniquement.

### ✅ T02 - Ajouter le contrat public `MGTheme.BuiltInTheme.Dark`

But: rendre le theme selectionnable par API native sans casser les themes existants.

Fichier principal:

- `MGUI/MGUI.Core/UI/MGTheme.cs`

Actions:

1. Ajouter `Dark` a l'enum `MGTheme.BuiltInTheme`.
2. Ne pas changer le constructeur `MGTheme(string DefaultFontFamily)`: il doit continuer a utiliser le theme par defaut actuel.
3. Verifier que `TryCreateBuiltInTheme("Dark", ...)` marche naturellement grace a `Enum.TryParse`.
4. Ne pas supprimer `Dark_Blue`.
5. Ne pas renommer `Dark_Blue` en `Blueprint` dans cette tache.

Validation:

- Le projet `MGUI.Core` compile.
- Un test sera ajoute dans T05, pas obligatoirement ici.

Commit attendu:

```powershell
git add MGUI/MGUI.Core/UI/MGTheme.cs ai-agent/mgui-native-dark-theme-plan.md
git commit -m "mgui: expose native dark built-in theme"
```

### ✅ T03 - Migrer la definition du theme editeur vers `BuiltInThemes.xaml`

But: le theme dark doit etre lu depuis les resources embarquees de `MGUI.Core`.

Fichiers principaux:

- Source: `CasaEngine.Editor/Content/UI/Themes/CasaEditor.Dark.Theme.xaml`
- Destination: `MGUI/MGUI.Core/UI/Themes/BuiltInThemes.xaml`

Actions detaillees:

1. Copier le contenu de `CasaEditor.Dark.Theme.xaml` dans le document `BuiltInThemes.xaml`.
2. Renommer le theme de `CasaEditor.Dark` vers `Dark`.
3. Ajouter `IsBuiltIn="True"` sur le theme `Dark`.
4. Garder `BasedOn="Dark_Blue"` pour limiter le risque et reutiliser les valeurs non surchargees.
5. Renommer tous les mappings de templates `CasaEditor.*` en `Dark.*`.
6. Appliquer la politique de police:
   - garder `DefaultFontSize`, `SmallFontSize`, `MediumFontSize`, `LargeFontSize`, `ContextMenuFontSize`;
   - retirer `DefaultFontFamily="JetBrainsMono"` sauf si T01 a prouve que MGUI livre cette police nativement.
7. Ne pas modifier les definitions `Dark_Blue` et `Light_Gray` hors besoin de formatage minimal.
8. Verifier que le XML reste un seul `ThemeDefinitionsDocument` valide.

Validation:

- `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore` passe.
- Aucun `TemplateName="CasaEditor.` ne reste dans `BuiltInThemes.xaml`.

Commit attendu:

```powershell
git add MGUI/MGUI.Core/UI/Themes/BuiltInThemes.xaml ai-agent/mgui-native-dark-theme-plan.md
git commit -m "mgui: add editor dark palette as built-in dark theme"
```

### ✅ T04 - Migrer et enregistrer les templates dark natifs

But: les templates references par le theme `Dark` doivent etre disponibles sans chargement depuis l'editeur.

Fichiers principaux:

- Source: `CasaEngine.Editor/Content/UI/Templates/CasaEditor.Dark.ControlTemplates.xaml`
- Destination: `MGUI/MGUI.Core/UI/Templates/BuiltInControlTemplates.xaml`
- Code: `MGUI/MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs`

Actions detaillees:

1. Copier les templates source dans `BuiltInControlTemplates.xaml`.
2. Renommer chaque template `CasaEditor.*` en `Dark.*`.
3. Garder les `BasedOn` vers les templates par defaut existants (`Window.Default`, `ListBox.Default`, `Dock.TabItem.Default`, etc.).
4. Verifier que les noms cibles correspondent exactement aux mappings ajoutes dans T03.
5. Mettre a jour `MGControlTemplateCatalog.RegisterDefaults(MGResources Resources)` pour enregistrer aussi les templates XAML embarques additionnels.
6. Attention: `RegisterDefaults` enregistre deja des templates code-first avec des defaults specifiques. Ne pas les ecraser.
7. Methode recommandee:
   - conserver l'enregistrement actuel des templates par defaut;
   - ensuite parcourir les definitions XAML embarquees;
   - pour chaque definition dont le nom n'est pas deja present dans `Resources`, creer le template via `ControlTemplateLoader.BuildTemplates(...)` en resolvant `BasedOn` depuis `Resources.TryGetControlTemplate`;
   - ajouter uniquement les templates manquants.
8. Ne pas enregistrer deux fois un template deja present.
9. Ne pas changer les hot paths `Update`/`Draw`.

Validation:

- `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore` passe.
- Une recherche confirme que `BuiltInControlTemplates.xaml` ne contient plus `CasaEditor.`.
- Les templates `Dark.Window`, `Dark.ListBox`, `Dark.DockTabItem` sont enregistrables par `MGControlTemplateCatalog.RegisterDefaults`.

Commit attendu:

```powershell
git add MGUI/MGUI.Core/UI/Templates/BuiltInControlTemplates.xaml MGUI/MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs ai-agent/mgui-native-dark-theme-plan.md
git commit -m "mgui: register native dark control templates"
```

### 🚧 T05 - Ajouter les tests de theme natif

But: verrouiller le contrat pour eviter une regression.

Fichiers probables:

- `MGUI/MGUI.Tests/Architecture/ThemeDefinitionTests.cs`
- `MGUI/MGUI.Tests/Architecture/ControlTemplateInfrastructureTests.cs`
- ou un nouveau fichier `MGUI/MGUI.Tests/Architecture/BuiltInThemeTests.cs`

Tests requis:

1. `BuiltInTheme_Dark_Can_Be_Created`
   - creer `new MGTheme(MGTheme.BuiltInTheme.Dark, "Arial")`;
   - verifier quelques valeurs significatives du theme editeur, par exemple background `Window` proche de `rgb(30,30,30)`, `DropdownArrowColor`, `Docking.TabActiveAccentColor` transparent.
2. `BuiltInTheme_Dark_Maps_Control_Templates`
   - verifier que le theme `Dark` mappe `MGElementType.Window` vers `Dark.Window`;
   - verifier au moins `ListBox`, `ComboBox`, `TabControl`;
   - verifier un mapping par type runtime, par exemple `MGDockTabItem` vers `Dark.DockTabItem`.
3. `BuiltIn_Control_Template_Catalog_Registers_Dark_Templates`
   - creer `MGResources resources = new(new MGTheme("Arial"));`
   - appeler `MGControlTemplateCatalog.RegisterDefaults(resources);`
   - verifier `resources.TryGetControlTemplate("Dark.Window", out _)`;
   - verifier `resources.TryGetControlTemplate("Dark.DockSplitter", out _)`.
4. `BuiltInTheme_Dark_Does_Not_Replace_Default_Constructor`
   - creer `new MGTheme("Arial")`;
   - verifier que le comportement par defaut reste compatible avec `Dark_Blue`.

Validation:

```powershell
dotnet test .\MGUI\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~BuiltInTheme
```

Puis, si rapide:

```powershell
dotnet test .\MGUI\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore
```

Commit attendu:

```powershell
git add MGUI/MGUI.Tests ai-agent/mgui-native-dark-theme-plan.md
git commit -m "mgui: test native dark theme registration"
```

### ⏳ T06 - Basculer CasaEngine.Editor vers le theme MGUI natif

But: l'editeur ne doit plus charger ses propres fichiers XAML de theme dark.

Fichier principal:

- `CasaEngine.Editor/GameEditor.cs`

Actions detaillees:

1. Supprimer les constantes de chemin `EditorThemeAssetRelativePath` et `EditorControlTemplatesAssetRelativePath` si elles ne servent plus.
2. Remplacer `TryLoadEditorThemeAssets()` par une methode plus simple, par exemple `ApplyEditorTheme()`.
3. La nouvelle methode doit creer ou recuperer `new MGTheme(MGTheme.BuiltInTheme.Dark, _desktop.DefaultFontFamily ou _desktop.Theme.FontSettings.DefaultFontFamily)`.
4. Appliquer ce theme a `_desktop.Resources.DefaultTheme`.
5. Ne plus appeler `LoadThemesFromXaml` ni `LoadControlTemplatesFromXaml` pour les assets de `CasaEngine.Editor/Content/UI`.
6. Si l'editeur veut conserver `JetBrainsMono`, le faire dans un bloc explicite et defensif:
   - verifier que la police est disponible si une API existe deja;
   - sinon ne pas imposer la police dans cette tache.
7. Garder un warning clair seulement si la creation du built-in `Dark` echoue, ce qui ne devrait pas arriver.

Validation:

- `dotnet build .\CasaEngine.Editor.MonoGame.sln -c Debug --no-restore` passe, ou `CasaEngine.Editor.csproj -t:Compile` passe si la solution est bloquee par un probleme non lie.
- Recherche: `CasaEngine.Editor/GameEditor.cs` ne reference plus `CasaEditor.Dark.Theme.xaml` ni `CasaEditor.Dark.ControlTemplates.xaml`.

Commit attendu:

```powershell
git add CasaEngine.Editor/GameEditor.cs ai-agent/mgui-native-dark-theme-plan.md
git commit -m "editor: use native mgui dark theme"
```

### ⏳ T07 - Retirer les assets dark du projet editeur ou les deprecier proprement

But: eviter deux sources de verite pour le meme theme.

Fichiers probables:

- `CasaEngine.Editor/Content/UI/Themes/CasaEditor.Dark.Theme.xaml`
- `CasaEngine.Editor/Content/UI/Templates/CasaEditor.Dark.ControlTemplates.xaml`
- `CasaEngine.Editor/CasaEngine.Editor.csproj` si ces fichiers sont inclus explicitement ou copies en output.

Actions detaillees:

1. Verifier si les fichiers editor sont references par le `.csproj`.
2. Verifier si des tests ou docs les ouvrent directement.
3. Si plus aucune reference utile n'existe, supprimer les deux fichiers de `CasaEngine.Editor/Content/UI`.
4. Si une reference externe impose une compat temporaire, garder les fichiers mais remplacer leur contenu par un commentaire ou une note n'est pas suffisant pour XAML. Dans ce cas, ne pas les toucher et marquer la tache `🧪`, puis creer une tache de suppression ulterieure.
5. Etat final prefere: les fichiers sont supprimes et le seul theme dark vit dans `MGUI.Core`.
6. Ne pas modifier les fichiers generes sous `bin` ou `obj`.

Validation:

- Recherche globale: aucun code source ne charge `CasaEngine.Editor/Content/UI/Themes/CasaEditor.Dark.Theme.xaml`.
- Build editor passe ou compile-only passe.

Commit attendu:

```powershell
git add CasaEngine.Editor ai-agent/mgui-native-dark-theme-plan.md
git commit -m "editor: remove duplicated dark theme assets"
```

### ⏳ T08 - Remplacer le theme dark du sample MGUI par le built-in natif

But: le sample MGUI doit montrer `Blueprint` et `Dark` avec le vrai theme `Dark` natif.

Fichiers principaux:

- `MGUI/MGUI.Samples/Features/StyleThemeRefactor.xaml`
- `MGUI/MGUI.Samples/Features/StyleThemeRefactor.xaml.cs`
- possiblement `MGUI/MGUI.Samples/Features/EditorDarkThemePreview.xaml`
- possiblement `MGUI/MGUI.Samples/Features/EditorDarkThemePreview.xaml.cs`
- `MGUI/MGUI.Samples/Compendium.xaml`
- `MGUI/MGUI.Samples/Compendium.xaml.cs`
- `MGUI/MGUI.Samples/MGUI.Samples.csproj`

Actions detaillees:

1. Dans `StyleThemeRefactor`, garder le bouton/choix `Blueprint` pour le theme historique.
2. Remplacer le choix `Ledger` ou l'ancien `Dark` local par `Dark`.
3. Le choix `Dark` doit utiliser `new MGTheme(MGTheme.BuiltInTheme.Dark, Desktop.DefaultFontFamily)` ou une helper locale equivalente.
4. Enregistrer ce theme dans les ressources du sample sous un nom clair, par exemple `DarkSkin`, si le sample attend un nom de ressource.
5. Ne pas re-declarer les couleurs dark dans une string XAML locale de sample.
6. Ne pas charger de fichier depuis `CasaEngine.Editor`.
7. Mettre a jour les labels visibles:
   - bouton `Blueprint`
   - bouton `Dark`
   - status `Active theme: Blueprint` ou `Active theme: Dark`
8. Si `EditorDarkThemePreview` devient redondant, choisir une des deux options:
   - Option A recommandee: le transformer en `NativeDarkThemePreview` sans chemin vers `CasaEngine.Editor`;
   - Option B acceptable: le supprimer du compendium et du `.csproj` si `StyleThemeRefactor` couvre deja le switch `Blueprint/Dark`.
9. Supprimer les messages qui disent que le theme vient de `CasaEngine.Editor`.
10. Garder le sample simple: pas de nouveau framework de theme dans le sample.

Validation:

- `dotnet build .\MGUI\MGUI.Samples\MGUI.Samples.csproj -c Debug --no-restore` passe.
- Recherche: `MGUI/MGUI.Samples` ne contient plus de chargement de `CasaEngine.Editor\Content\UI`.
- Le sample affiche bien un choix `Blueprint` et un choix `Dark`.

Commit attendu:

```powershell
git add MGUI/MGUI.Samples ai-agent/mgui-native-dark-theme-plan.md
git commit -m "samples: switch theme demo to native dark"
```

### ⏳ T09 - Mettre a jour la documentation MGUI

But: documenter que `Dark` est un theme natif supporte.

Fichiers probables:

- `MGUI/Docs/theme-definition-migration-guide.md`
- `MGUI/README.md`
- eventuellement `MGUI/MGUI.Samples/Features/EditorDarkThemePreview.README.md` si le sample existe encore.

Actions detaillees:

1. Ajouter `Dark` a la liste des built-ins supportes.
2. Preciser que `Dark_Blue` reste disponible pour compatibilite et peut etre presente comme `Blueprint` dans le sample.
3. Ajouter un snippet minimal:

```csharp
MGTheme darkTheme = new(MGTheme.BuiltInTheme.Dark, desktop.DefaultFontFamily);
desktop.Resources.DefaultTheme = darkTheme;
```

4. Retirer les instructions demandant de charger le dark theme depuis `CasaEngine.Editor`.
5. Si le sample `EditorDarkThemePreview` est renomme ou supprime, mettre a jour sa doc.

Validation:

- Les liens markdown pointent vers des fichiers existants.
- La doc ne parle plus du theme dark editeur comme source de verite.

Commit attendu:

```powershell
git add MGUI/Docs MGUI/README.md MGUI/MGUI.Samples/Features ai-agent/mgui-native-dark-theme-plan.md
git commit -m "docs: document native mgui dark theme"
```

### ⏳ T10 - Validation complete build et tests

But: verifier l'ensemble apres migration.

Actions:

1. Lancer:

```powershell
dotnet test .\MGUI\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore
dotnet build .\MGUI\MGUI.Samples\MGUI.Samples.csproj -c Debug --no-restore
dotnet build .\CasaEngine.Editor.MonoGame.sln -c Debug --no-restore
```

2. Si la solution editor est bloquee par un verrouillage de fichiers, lancer:

```powershell
dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -c Debug -t:Compile --no-restore
```

3. Rechercher les references interdites:

```powershell
rg "CasaEditor\.Dark|CasaEngine\.Editor\\Content\\UI|LedgerSkin|EditorDarkThemePreview" MGUI CasaEngine.Editor
```

4. Pour chaque match restant, decider s'il est volontaire:
   - docs historiques: a eviter;
   - code runtime: interdit;
   - ancien sample supprime: interdit;
   - plan IA: autorise.

Validation:

- Tests MGUI passent.
- Build sample passe.
- Build editor ou compile-only passe.
- Aucun chargement runtime du theme depuis `CasaEngine.Editor/Content/UI` ne reste.

Commit attendu:

```powershell
git add ai-agent/mgui-native-dark-theme-plan.md
git commit -m "chore: validate native dark theme migration"
```

Si aucun fichier autre que le plan n'a change apres validation, ce commit peut ne contenir que le passage de T10 en `✅`.

### ⏳ T11 - Smoke test visuel manuel

But: confirmer que le theme rendu ressemble bien au theme editeur et que le sample bascule correctement.

Actions:

1. Lancer `MGUI.Samples`.
2. Ouvrir le sample de theme.
3. Cliquer `Blueprint`, noter que l'apparence historique reste utilisable.
4. Cliquer `Dark`, verifier:
   - surfaces gris sombre proches de l'editeur;
   - texte lisible;
   - selections et focus bleu-gris coherents;
   - menus contextuels sombres;
   - combo dropdown sombre;
   - listbox/listview/treeview sombres;
   - tabcontrol et docking si presents dans le sample.
5. Lancer `CasaEngine.Editor` si possible.
6. Verifier que l'editeur demarre sans assets `CasaEngine.Editor/Content/UI/Themes/CasaEditor.Dark.Theme.xaml`.
7. Ouvrir au moins Content Browser, Hierarchy, Inspector/Details, Logs si disponibles.
8. Verifier que les couleurs correspondent a l'ancien theme editeur.

Validation:

- Si tout est visuellement correct, passer T11 en `✅`.
- Si le build passe mais que le smoke manuel n'a pas pu etre lance, passer T11 en `🧪` et noter la raison.
- Si un probleme visuel majeur existe, creer une sous-tache precise avant de terminer.

Commit attendu:

```powershell
git add ai-agent/mgui-native-dark-theme-plan.md
git commit -m "test: smoke native dark theme visuals"
```

### ⏳ T12 - Nettoyage final et rapport de migration

But: finir avec un historique propre et une synthese exploitable.

Actions:

1. Lancer `git status --short`.
2. Verifier qu'il ne reste pas de changements non committes lies a la migration.
3. Verifier que chaque tache terminee a une icone finale correcte.
4. Ajouter sous cette tache une courte synthese:
   - theme natif ajoute;
   - sample mis a jour;
   - editor bascule;
   - tests/builds lances;
   - smoke manuel effectue ou non.
5. Ne pas modifier les fichiers generes `bin`/`obj`.

Validation:

- `git status --short` est propre ou ne contient que des changements utilisateur non lies.
- Le plan ne contient plus de tache `🚧`.

Commit attendu:

```powershell
git add ai-agent/mgui-native-dark-theme-plan.md
git commit -m "chore: finalize native dark theme migration plan status"
```

## Criteres d'acceptation finaux

- `MGTheme.BuiltInTheme.Dark` existe.
- `new MGTheme(MGTheme.BuiltInTheme.Dark, someFontFamily)` cree un theme valide.
- Les templates `Dark.*` sont embarques dans `MGUI.Core` et enregistres par defaut.
- `CasaEngine.Editor` utilise le theme natif MGUI au lieu de charger `CasaEngine.Editor/Content/UI/Themes/CasaEditor.Dark.Theme.xaml`.
- Le sample MGUI permet de basculer entre `Blueprint` et `Dark`.
- Le choix `Dark` du sample utilise le theme natif, pas une copie locale incomplete.
- Les fichiers XAML editoriaux ne sont plus la source de verite.
- Les tests MGUI couvrent la creation du theme et les mappings de templates.
- La documentation liste `Dark` comme built-in MGUI supporte.

## Risques connus

- `MGControlTemplateCatalog.RegisterDefaults` ne registre pas actuellement tous les templates XAML embarques additionnels: T04 doit corriger ce point.
- Le theme source utilise `JetBrainsMono`, qui peut ne pas etre disponible dans MGUI.Samples. Suivre la politique de police ci-dessus.
- Le sample actuel peut etre nomme `Blueprint/Ledger` selon la branche. Ne pas creer un deuxieme sample si un seul switch `Blueprint/Dark` suffit.
- Certains anciens fichiers sous `bin` ou `obj` peuvent contenir `CasaEditor.Dark`. Ne pas les editer et ne pas les prendre en compte comme source.
