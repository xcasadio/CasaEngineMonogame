# Plan agent IA — Pipeline de texte de `MGTextBlock` sans allocation

Plan préparé par T3.1 de [mgui-slider-nonalloc-tasks.md](mgui-slider-nonalloc-tasks.md) (décision D7 du 2026-10-06 :
« tout le pipeline de `MGTextBlock` devient sans allocation, dans un chantier séparé avec son propre plan »).
**État : questions posées à l'auteur (section « Questions à l'auteur »), plan à compléter avec ses réponses, puis à
approuver. Aucune modification de code avant approbation.**

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Un `MGTextBlock` dont le texte change (par exemple le label de valeur d'un slider glissé, format `F2`) n'alloue plus
rien, ni au changement de texte, ni à la mesure et au layout qui suivent, ni au dessin ; un texte inchangé ne coûte rien
par image. Ce qui ne peut pas l'être sans changer une API (chaîne du nouveau texte, voir Q1) est dit et tranché par
l'auteur.

## État vérifié du dépôt (2026-10-06)

Mesures sur MGUI `chantier/mgui-slider-nonalloc` (`1166a5a`), runtime de test `GraphTestRuntime`, sonde temporaire non
commitée (moyenne par opération après chauffe) :

- `MGTextBlock.Text = "0.42"` puis `"0.43"` en alternance (chaînes déjà construites) : environ 3 200 octets par
  changement pour le setter seul ; environ 3 280 quand la longueur change (`"0.42"` / `"10.00"`).
- Le même changement suivi d'une image complète du bureau (update et dessin) : environ 23 900 octets, contre 12 400 pour
  une image sans changement de texte : environ 11 500 octets par changement de texte, mesure et layout compris.

Sites lus dans le code (lecture par un agent `scout`, points décisifs relus) :

- Setter : `MGTextBlock.Text` → `SetTextCore` → `ApplyTextMutation` (`MGUI/MGUI.Core/UI/MGTextBlock.cs:897-990`) →
  `UpdateRuns` (`:1167-1189`) : `MGTextRun.ParseRuns` puis `SanitizeRuns` (`new List`, `AsReadOnly`, `:1144-1165`) ;
  `Runs.Any(...)` et `Runs.Where(...).Cast(...).Sum(...)` (LINQ) ; chemin sans balises (`AllowsInlineFormatting` faux) :
  `FTTokenizer.TokenizeLineBreaks(...).ToList()` puis `ParseRuns(...).ToList().AsReadOnly()`.
- `MGTextRun.ParseRuns` (`MGUI/MGUI.Core/UI/Text/MGTextRun.cs`, à partir de `:169`) : `Replace("\t", ...)`, `new List`,
  `Parser.ParseTokens(...).ToList()`, `StringBuilder`, `ToString()`, un objet `MGTextRunText` par run, itérateur `yield`.
- Tokenizer (`MGUI/MGUI.Core/UI/Text/FormattedTextTokenizer.cs`) : `Tokenize(...).ToList()`, `Substring` dans
  `TokenizeLineBreaks` (10 lignes avec `ToList`, `new List`, LINQ ou `Substring`).
- Invalidation : le setter `Text` demande `RelayoutParent` (`MGTextBlock.cs:900`), donc un layout du parent ; la mesure
  refait `MGTextLine.ParseLines(...).ToList()` (`:1053`) ; `ParseLines` et la coupure en mots
  (`MGUI/MGUI.Core/UI/Text/MGTextLine.cs`, `WrappableRunGroup`, `WrappableRun`) : 20 lignes avec `new List`,
  `StringBuilder`, `ToList`, LINQ, `string.Concat`, `Substring` ou `Split`. Un cache de mesures existe
  (`RecentSelfMeasurements`, `MGTextBlock.cs:1407`, vidé par `InvokeLayoutChanged`).
- Dessin : `ActionBounds` et `ToolTipBounds` sont des dictionnaires réutilisés, mais leurs listes internes sont
  recréées (`:1671`, `:1683`) ; la révélation progressive (`TextProgress`) fait un `Substring` par image (`:1600`).
- API publique concernée : `MGTextBlock.Runs` et `Lines` (`ReadOnlyCollection`, `:1191-1192`), `MGTextRun` (classe
  publique abstraite, `MGTextRun.cs:144`), `MGTextRunText.Text` (`readonly string`, `:300`), `MGTextLine` (`record
  class` publique, `MGTextLine.cs:18`).
- Les moteurs de texte n'acceptent que des `string` : `ITextMeasurementEngine.MeasureText(ResolvedFont, string)`
  (`MGUI/MGUI.Shared/Text/Engines/ITextMeasurementEngine.cs:12`), `IUIDrawContext.DrawTextViaEngine(..., string, ...)`
  (`MGUI/MGUI.Shared/Rendering/IUIDrawContext.cs:21`) ; ils ont plusieurs implémentations (MonoGame, FontStashSharp, et
  le renderer MGUI de CasaEngine).

## Questions à l'auteur

À trancher en une fois avant d'écrire les tâches ; chaque réponse devient une décision verrouillée.

| Réf | Question | Options vues (sans recommandation définitive tant que la mesure par site n'est pas faite) |
|---|---|---|
| Q1 | Un texte affiché qui change demande une nouvelle `string`, puisque la mesure et le dessin n'acceptent que des `string`. Jusqu'où aller ? | (a) accepter une chaîne par vrai changement, zéro allocation partout ailleurs ; (b) cache borné de chaînes par `MGTextBlock` (zéro allocation pour un texte déjà vu) ; (c) ajouter aux moteurs de texte et à `IUIDrawContext` des surcharges `ReadOnlySpan<char>`, à implémenter dans chaque backend, CasaEngine compris (changement d'API transverse). |
| Q2 | `Runs` et `Lines` sont publics et leurs objets immuables. Peut-on réutiliser les mêmes objets d'un texte à l'autre (visible de l'extérieur : une référence gardée verrait le nouveau contenu) ? | (a) oui, objets réutilisés et rendus modifiables en interne ; (b) non : représentation interne réutilisable, et `Runs`/`Lines` construits seulement à la demande ; (c) non : seul un chemin rapide « texte simple d'une ligne » réutilise ses objets internes. |
| Q3 | Le setter `Text` relance le layout du parent. Le layout du parent (hors `MGTextBlock`) fait-il partie de ce chantier, ou du chantier « image du bureau » (T3.2) ? | (a) ce chantier ; (b) T3.2 ; (c) ici seulement la mesure du `MGTextBlock`. |
| Q4 | Tokenizer et balises (`[b]`, `[color=…]`, actions, infobulles) : tout le langage sans allocation, ou d'abord le texte sans balise ? | (a) tout ; (b) texte sans balise d'abord, balises dans une seconde phase. |
| Q5 | Révélation progressive (`TextProgress`, `Substring` par image) et listes d'`ActionBounds`/`ToolTipBounds` recréées au dessin : dans ce chantier ? | (a) oui ; (b) non, suite séparée. |

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | (D7 du plan du slider) Tout le pipeline de `MGTextBlock` devient sans allocation, dans ce chantier séparé. |

## Règles d'exécution pour l'agent

Celles de [mgui-slider-nonalloc-tasks.md](mgui-slider-nonalloc-tasks.md) : branches dédiées dans le moteur et dans
MGUI, une tâche et un commit à la fois, jamais de push ni de merge, tests d'allocation contre-éprouvés, arrêt sur toute
allocation imprévue. Langue : plan en français, code et commits en anglais.

## Validation globale

- `MGUI.Tests` vert, nouveaux tests d'allocation nuls (changement de texte, mesure, dessin) et contre-éprouvés ;
  `MGUI.Samples` sans erreur ; les deux solutions du moteur sans erreur ; `CasaEngine.Tests` vert ; `verifier` frais.

## Tâches (provisoires, à figer après les réponses)

### ⏳ T0.1 — Banc de mesure par étape

- Objectif : un test d'allocation par étape (setter, `UpdateRuns`, mesure, `UpdateLines`, dessin) sur des textes
  représentatifs (nombre court, nombre de longueur variable, texte avec balises, texte coupé en mots), pour classer les
  sites mesurés avant tout changement. Le runtime de test recrée une transaction de dessin enregistreuse à chaque
  `Draw` : la mesure du dessin passe par `DrawSelf` avec une transaction réutilisée et vidée, comme
  `MGUI.Tests/Controls/SliderDrawAllocationTests.cs`.

### ⏳ T1.x — Selon Q1 à Q5

Une tâche par étape du pipeline (runs, tokenizer, lignes et coupure, mesure, dessin, révélation), chacune avec son test
d'allocation contre-éprouvé et des tests de rendu identique (runs, lignes, tailles mesurées, appels de dessin) sur un
corpus de textes.

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Les mesures ci-dessus viennent du runtime de test ; refaire la mesure du dessin avec une transaction non enregistreuse (T0.1). | T0.1 |

## Hors périmètre

- Le reste d'une image du bureau (`MGDesktop`, `MGWindow`, layout des autres contrôles) : plan « image du bureau »
  ([mgui-desktop-frame-nonalloc-tasks.md](mgui-desktop-frame-nonalloc-tasks.md)), sauf réponse contraire à Q3.
