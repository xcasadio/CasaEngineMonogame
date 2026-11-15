# Plan agent IA — Pipeline de texte de `MGTextBlock` sans allocation

Plan préparé par T3.1 de [mgui-slider-nonalloc-tasks.md](mgui-slider-nonalloc-tasks.md) (décision D7 du 2026-10-06 :
« tout le pipeline de `MGTextBlock` devient sans allocation, dans un chantier séparé avec son propre plan »).
**État : questions tranchées par l'auteur le 2026-11-14 (D2 à D6, « Fait ça et fais l'implémentation de manière autonome »), tâches figées, exécution en mode AUTO. Branches `chantier/mgui-nonalloc` dans le moteur (depuis `main` `f6f8006a`) et dans MGUI (depuis `develop` `e9d6a95d`).** Relu READY le 2026-11-14 (troisième relecture).


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
| D2 | (Q1, 2026-11-14) (c) : surcharges `ReadOnlySpan<char>` ajoutées à `ITextMeasurementEngine`, `ITextDrawEngine` et `IUIDrawContext`, sans retirer les surcharges `string` ; implémentées dans chaque backend (`SpriteFontTextEngine`, `FontStashSharpTextEngine`, `DrawTransaction`, `CasaDrawTransaction`). Si un backend ne peut ni mesurer ni dessiner sans `string`, la tâche passe en ⚠️ et le repli est (a). |
| D3 | (Q2) (b) : représentation interne réutilisable ; `Runs` et `Lines` gardent leur type et leur contenu, construits seulement quand on les lit. |
| D4 | (Q3) (c) : ici seulement la mesure du `MGTextBlock` ; le layout du parent est dans le plan du bureau (sa D5, T2.1). |
| D5 | (Q4) (b) : texte sans balise d'abord (phase 1), balises ensuite (phase 2). |
| D6 | (Q5) (a) : révélation progressive et listes de bornes dans ce chantier, en dernier (phase 3). |

## Règles d'exécution pour l'agent

Celles de [mgui-slider-nonalloc-tasks.md](mgui-slider-nonalloc-tasks.md) : branches dédiées dans le moteur et dans
MGUI, une tâche et un commit à la fois, jamais de push ni de merge, tests d'allocation contre-éprouvés, arrêt sur toute
allocation imprévue. Langue : plan en français, code et commits en anglais.

## Validation globale

- `MGUI.Tests` vert, nouveaux tests d'allocation nuls (changement de texte, mesure, dessin) et contre-éprouvés ;
  `MGUI.Samples` sans erreur ; les deux solutions du moteur sans erreur ; `CasaEngine.Tests` vert ; `verifier` frais.

## Tâches

Validation de chaque tâche MGUI : `dotnet build MGUI/MGUI.sln`, `dotnet test MGUI/MGUI.Tests/MGUI.Tests.csproj`.
Chaque tâche qui change le pipeline garde le rendu identique : mêmes runs, mêmes lignes, mêmes tailles mesurées, mêmes
appels de dessin sur un corpus de textes (nombre court, longueur variable, plusieurs lignes, coupure en mots, balises).

### ✅ T0.1 — Banc de mesure par étape, et corpus de rendu

- Objectif : un test d'allocation par étape (setter, `UpdateRuns`, mesure, `UpdateLines`, dessin) sur le corpus, et les
  tests de rendu identique qui serviront de garde à toutes les tâches suivantes. La mesure du dessin passe par
  `DrawSelf` avec une transaction réutilisée et vidée (`DrawTextCalls` compris), comme
  `MGUI.Tests/Controls/SliderDrawAllocationTests.cs`. Les tests de mesure écrivent leurs chiffres sans affirmer zéro.
- Fin : les chiffres par étape sont écrits sous cette tâche ; aucun code de production touché.
- Commit (MGUI) : `test(text): measure the MGTextBlock pipeline step by step`
- **Fait le 2026-11-14** (MGUI `dbeacc95`, par un agent `executor`, relu). `MGUI.Tests` : 3256 verts, aucun sauté.
  Banc : `MGUI.Tests/Text/TextBlockAllocationMeasurements.cs` ; garde de rendu (runs, lignes, taille mesurée, appels de
  dessin, valeurs figées sur le code actuel) : `MGUI.Tests/Text/TextBlockRenderingCorpusTests.cs`. Octets par
  opération, `AllowsInlineFormatting` faux puis vrai :

  | Texte | Setter `Text` | `SetText(ContentOnly)` | Mesure après changement | `DrawSelf` inchangé |
  |---|---|---|---|---|
  | Nombre court | 2104 / 3208 | 9040 / 10144 | 3488 | 120 |
  | Phrase d'une ligne | 2312 / 4312 | 17552 / 19552 | 7796 | 120 |
  | Plusieurs lignes | 14464 / 16496 | 33064 / 35096 | 10604 | 200 |
  | Phrase coupée en mots | 2432 / 6624 | 60088 / 64280 | 30332 | 240 |
  | Texte à balises | 2360 / 46384 | 15248 / 66360 | 6432 / 9976 | 120 |

  `UpdateLines` vaut la mesure plus 24 octets. Cycle complet (changement, `Update`, `Draw`) d'un nombre court : environ
  12 000 octets, dont 2921 pour une image sans changement (plan du bureau).
- Limites : `UpdateRuns` est privé, son coût est dans les lignes du setter ; la mesure passe par `MeasureSelfOverride`.
  Le moteur de texte de test mesure 6 px par caractère, gras ou non : les largeurs de la garde de rendu en dépendent.
- À vérifier dans T1.3 (constat de l'agent, cause non vérifiée) : le chemin local `SetText(ContentOnly)` coûte plus que
  le setter suivi d'une mesure, sans doute parce que `CachedTextMeasurementsStillMatch` refait chaque mesure du cache.

### ✅ T1.1 — Surcharges `ReadOnlySpan<char>` des moteurs de texte (D2)

- Fichiers (MGUI) : `MGUI.Shared/Text/Engines/ITextMeasurementEngine.cs`, `ITextDrawEngine.cs`,
  `MGUI.Shared/Rendering/IUIDrawContext.cs`, `MGUI.MonoGame.Integration` (`SpriteFontTextEngine`, `DrawTransaction`),
  `MGUI.FontStashSharp/FontStashSharpTextEngine.cs`, runtimes de test ; ADR de MGUI.
  Fichiers (runtime du moteur) : `CasaEngine/Framework/UI/Backend/MonoGame/CasaDrawTransaction.cs`.
  Tests : `MGUI.Tests` avec `MGUI.Tests/Integration/GpuDeviceHost.cs` pour `SpriteFontTextEngine`,
  `FontStashSharpTextEngine` et `DrawTransaction` ; un fichier de `CasaEngine.Tests/UI/Backend/` (hôte GPU
  `GpuDeviceHost.cs`, comme `UiClipViewSpaceGpuTests.cs`) pour `CasaDrawTransaction`.
- Les membres ajoutés aux interfaces ont une implémentation par défaut qui retombe sur la surcharge `string` : un
  backend externe continue de compiler. Les moteurs et transactions de test utilisés par un test de zéro
  implémentent la surcharge eux-mêmes.
- Fin : mesurer et dessiner un `ReadOnlySpan<char>` n'alloue rien dans les deux moteurs de texte et les deux
  transactions, chacun prouvé par un test de zéro après chauffe, contre-éprouvé (il échoue avec l'implémentation par
  défaut) ; même largeur et même rendu que la surcharge `string` ; les deux solutions du moteur compilent ;
  `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` vert. Les tests de zéro passent par un GPU (`[GpuFact]`, qui
  saute le test sans GPU) : la tâche n'est ✅ que si les quatre tests de zéro et leurs contre-épreuves ont tourné et
  réussi, aucun sauté, dans les deux projets de test ; les comptes (réussis, sautés) sont écrits sous la tâche. Sans
  GPU utilisable : ⚠️ et arrêt.
- Commits : MGUI `feat(text): measure and draw a span of characters` ; moteur `feat(ui): draw a span of characters
  through the engine backend`, avec la référence du sous-module.
- **Fait le 2026-11-14** (MGUI `c24158d1`, ADR-0026 de MGUI). O2 levé : les deux polices mesurent et dessinent un
  `StringBuilder`, que chaque moteur garde et remplit depuis le span.
  - `MGUI.Tests/Integration/TextSpanGpuTests.cs` : 8 réussis, 0 sauté (taille égale, pixels égaux, zéro à la mesure et
    au dessin, pour `SpriteFontTextEngine` et `FontStashSharpTextEngine` à travers `DrawTransaction`). Contre-épreuve :
    avec les implémentations par défaut, les 4 tests de zéro échouent (9600 octets pour 200 appels).
  - `CasaEngine.Tests/UI/Backend/TextSpanGpuTests.cs` : 2 réussis, 0 sauté (`CasaDrawTransaction`, moteur
    FontStashSharp). Contre-épreuve : avec un repli sur `string`, le test de zéro échoue (9600 octets).
  - `MGUI.Tests` 3264 verts, `CasaEngine.Tests` 5455 verts, aucun sauté ; les deux solutions du moteur : 0 erreur.
  - `HostRuntimeContractTests` de MGUI attend maintenant deux surcharges `DrawText`.
  - La référence du sous-module passe de `a3c42cb0` à `c24158d1` : elle embarque aussi les deux commits de `develop`
    que l'auteur avait déjà dans son arbre (O3).

### ✅ T1.2 — Texte sans balise : changement de texte sans allocation (D3, D5)

- Fichiers : `MGUI.Core/UI/MGTextBlock.cs` (`SetTextCore`, `ApplyTextMutation`, `UpdateRuns`), `Text/MGTextRun.cs`.
- Fin : deux surcharges nouvelles, `public void SetText(ReadOnlySpan<char> Value)` (mode `RelayoutParent`, comme le
  setter `Text`) et `public void SetText(ReadOnlySpan<char> Value, MGTextInvalidationMode InvalidationMode)`, qui suit
  les règles de résolution de `SetText(string, MGTextInvalidationMode)` (`AllowLegacyLocalInvalidation` faux,
  `MGTextBlock.cs:910-911`, `:993-1006`). Elles copient le texte dans un tampon gardé ; la `string` de `Text` n'est
  construite qu'à sa lecture. Zéro prouvé ici, sans abonné à `PropertyChanged`, quand `AllowsInlineFormatting` est faux et que le
  changement ne lance pas le calcul des lignes dans le setter (mode `RelayoutParent`) ; les modes locaux (`ContentOnly`,
  `ReflowLocal`), qui appellent `UpdateLines` et la mesure depuis `ApplyTextMutation`, sont prouvés à zéro par T1.3 ; le setter `Text` (`string`) ne garde que ce que `Text`, `Runs` et `Lines`
  demandent à la lecture ; un texte inchangé ne fait rien. `Lines` reste `null` tant que les lignes ne sont pas
  calculées (des tests et `MGTextBlock` lisent ce `null`).
- Commit (MGUI) : `perf(text): change a plain text without allocating`
- **Fait le 2026-11-15** (MGUI `2723447e`, par un agent `executor`). `PlainTextSetTextTests` : zéro pour les deux
  surcharges ; contre-épreuve : 2104 octets par changement avec le setter `string`, 32 avec un repli sur `ToString()`.
  `Text`, `Runs` et `Lines` relus égaux à ceux du setter `string`.

### ✅ T1.3 — Texte sans balise : mesure et lignes sans allocation (D4)

- Fichiers : `MGUI.Core/UI/MGTextBlock.cs` (mesure, `UpdateLines`), `Text/MGTextLine.cs` (`ParseLines`, coupure en
  mots).
- Fin : mesure et découpe en lignes d'un texte sans balise sans allocation, une ligne et plusieurs lignes, avec et sans
  coupure en mots. `SetText(span, mode)` dans les modes locaux (`ContentOnly`, `ReflowLocal`) : zéro, setter compris, sur un
  `MGTextBlock` avec `HasStableTextFootprint = true` et une largeur de layout connue (`HasKnownTextLayoutWidth()`), et le
  test vérifie que `ApplyTextMutation` a bien pris la branche locale (`UpdateLines`, `:985`) : pas de `LayoutChanged`
  levé pour un texte de même taille.
- Commit (MGUI) : `perf(text): measure and wrap a plain text without allocating`
- **Fait le 2026-11-15** (MGUI `09d9ff12`). Nouveau `MGUI.Core/UI/Text/PlainTextLayout.cs` : listes de lignes et de
  morceaux réutilisées, qui refont `MGTextLine.ParseLines` pour un texte sans balise. `PlainTextAllocationTests` : zéro
  pour la mesure et les lignes (7 cas) et pour les modes locaux (6 cas, branche locale vérifiée par l'absence de
  `LayoutChanged`) ; contre-épreuves sur l'ancien chemin : 63 312 octets par itération (phrase coupée), 9072 par
  changement local. Le constat de T0.1 était juste : `CachedTextMeasurementsStillMatch` refaisait chaque mesure.
- `PlainTextDifferentialTests` : 41 textes fixes et 260 pseudo-aléatoires, plus de 20 000 layouts comparés à
  `ParseLines`, tailles mesurées, invalidations locales et appels de dessin comparés à l'ancien chemin, gardé
  appelable par `internal bool ForceLegacyTextPath`.

### ✅ T1.4 — Texte sans balise : dessin sans allocation

- Fichiers : `MGUI.Core/UI/MGTextBlock.cs` (`DrawSelf`), `Text/TextRenderInfo.cs`.
- Fin : `DrawSelf` d'un texte sans balise n'alloue rien, texte inchangé ou changé à chaque image par `SetText` ; test
  de bout en bout : changement, mesure, dessin d'un nombre au format `F2` à chaque image, zéro octet.
- Commit (MGUI) : `perf(text): draw a plain text without allocating`
- **Fait le 2026-11-15** (MGUI `31b859a8`, correctif `a05134f6`). `DrawSelf` sans balise : zéro (10 cas, ombre et
  `TextProgress` compris) ; bout en bout d'un nombre `F2` : zéro, contre 5586 octets par itération avant la tâche.
  Un fond ou un soulignement renvoie à l'ancienne boucle, qui alloue (T2.2).
- **Vérification indépendante (T1.2 à T1.4)** : premier verdict REFUTED sur un constat P2, introduit : `Lines`,
  construit à la lecture, était mesuré avec la police du moment de la lecture après un changement de police. Corrigé
  (`KeepLinesOfTheCurrentFont`, test de régression qui échouait avant) ; second vérificateur neuf : **CONFIRMED**
  (24 séquences aléatoires de 1500 opérations, changements de police compris, égales à l'ancien chemin ; les 24
  échouent sur le code d'avant le correctif).
- Validation : `MGUI.Tests` 3344 verts, `CasaEngine.Tests` 5455 verts, aucun sauté ; les deux solutions : 0 erreur.
- Constats P4 reportés, non corrigés : après un changement de moteur de texte (ou un `TextEngineOverride` posé sur un
  ancêtre), `Lines` lu avant le layout suivant est mesuré par le nouveau moteur (dans ce dépôt le moteur ne change
  qu'à l'initialisation) ; le premier changement de police après un layout alloue environ 11,7 Ko, donc un zoom qui
  change la police à chaque image (`MGGraphControls`) alloue ; les tampons d'un bloc ne rétrécissent jamais.
- Bizarreries de `ParseLines` reproduites telles quelles, non corrigées : une ligne vide se dessine avec la police
  normale même si le bloc est gras ; après un mot coupé, la suite ignore la largeur du morceau resté sur la ligne ; si
  `-` est plus large que la ligne, le reste du texte est abandonné.

### ✅ T2.1 — Balises : tokenizer et runs sans allocation (D5)

- Fichiers : `Text/FormattedTextTokenizer.cs`, `Text/FormattedTextParser.cs`, `Text/MGTextRun.cs`,
  `MGUI.Core/UI/MGTextBlock.cs` (branche à balises de `UpdateRuns`, `SanitizeRuns`).
- Fin : changer un texte à balises (`[b]`, `[color=…]`, actions, infobulles) n'alloue rien hors des objets que la
  lecture de `Runs` demande.
- Commit (MGUI) : `perf(text): tokenize and parse formatted text without allocating`
- **Fait le 2026-11-15** (MGUI `b7d2ecfc`, correctif `1080e587`). Le tokenizer et le parser historiques ne sont pas
  modifiés : un lecteur en une passe (`Text/FormattedTextScanner.cs`, sans Regex ni sous-chaîne) écrit dans des runs
  réutilisés (`Text/FormattedTextRuns.cs`). Les valeurs de balise sont interprétées par les fonctions historiques,
  rendues partagées dans `MGTextRun.cs`, puis gardées dans un cache par bloc (128 entrées par sorte, vidé quand il
  est plein). `Runs` est construit à la lecture depuis ces enregistrements.
- Mesures : `SetText` (chaîne ou span) sur 11 familles de balises : zéro, contre 46 600 octets par changement avant.
  Une valeur jamais vue alloue une fois (7512 octets pour 7 valeurs), puis zéro.
- Textes laissés au code historique (ils allouent, environ 9,5 Ko par changement) : balisage où il lève une
  exception (même type et même message qu'avant), caractère non ASCII là où la grammaire compare des lettres ou des
  chiffres, image sans taille quand le bloc n'a pas de bureau, et type de balise écrit avec un `I` majuscule quand le
  processus a démarré sous une culture turque ou azérie.
- Changement annexe : `IsTrackingMouseClicks` garde son délégué (64 octets par bascule avant).

### ✅ T2.2 — Balises : lignes et dessin sans allocation

- Fichiers : `MGUI.Core/UI/MGTextBlock.cs` (mesure et `DrawSelf`, branche à balises, dont `Foreground.AsFillBrush()` qui
  crée un `MGSolidFillBrush` par dessin), `Text/MGTextLine.cs`, `Text/TextRenderInfo.cs`.
- Fin : mesure, découpe et dessin d'un texte à balises sans allocation, le fond et le soulignement étant des brosses
  unies fournies par le test. Les classes de brosses ne sont pas modifiées : si le `Draw` d'une brosse unie alloue, la
  mesure est écrite ici, la tâche passe en ⚠️ et s'arrête.
- Commit (MGUI) : `perf(text): wrap and draw formatted text without allocating`
- **Fait le 2026-11-15** (MGUI `0e11f6ca`). `Text/FormattedTextLayout.cs` porte `MGTextLine.ParseLines` pas à pas
  (runs de réglages différents, images, mots qui continuent d'un run à l'autre) ; `MGTextLine.cs` n'est pas modifié.
  Le soulignement par défaut passe par `DrawUnderline`, qui remplit le rectangle qu'un `MGSolidFillBrush` remplirait ;
  l'ancienne boucle utilise la même fonction. Le `Draw` d'une brosse unie n'alloue pas.
- Mesures : mesure et `UpdateLines` après un changement : zéro, contre 85 832 octets avant ; `ContentOnly` et
  `ReflowLocal` : zéro, contre 59 896 ; `DrawSelf` (fond, soulignement, ombre, opacité, images, retour à la ligne) :
  zéro, contre 200 octets par dessin.

### ✅ T3.1 — Révélation progressive et listes de bornes (D6)

- Fichiers : `MGUI.Core/UI/MGTextBlock.cs` (`TextProgress` `:1600`, `ActionBounds` `:1671`, `ToolTipBounds` `:1683`).
- Fin : une révélation progressive pilotée par des écritures directes de `TextProgress` à chaque image n'alloue rien
  (setter, mesure, dessin) ; les listes de bornes sont réutilisées. Hors de cette preuve : le pilotage par
  `TextCharactersPerSecond` (tick du moteur d'animation, `TextRevealRun` recréé à chaque resynchronisation), listé
  comme suite.
- Commit (MGUI) : `perf(text): reveal text and track its bounds without allocating`
- **Fait le 2026-11-15** (MGUI `b1f5a50b`). `Text/TextBoundsTracker.cs` remplace les deux dictionnaires de bornes et
  garde leur ordre d'énumération. `TryInvokeActionAt` est extrait de `Mouse_ReleasedInside` (même ordre, même sortie
  anticipée) pour qu'un test clique sur une action.
- Mesures : `TextProgress` écrit à chaque image : zéro, contre 231 octets (sans balise) et 300 octets (à balises) par
  image avant ; bornes reconstruites à chaque dessin, clic et recherche d'infobulle : zéro.
- **Vérification indépendante (T2.1 à T3.1)** : premier verdict REFUTED sur un constat P2, introduit : sous une
  culture turque ou azérie, l'ancien tokenizer ne lit pas `[I]`, `[ITALIC]` ou `[IMG=…]` comme des balises (ses
  expressions régulières ignorent la casse selon la culture du moment où il est créé), et le lecteur les lisait
  (351 écarts sur 200 séquences aléatoires sous `tr-TR`). Corrigé (MGUI `1080e587` : le lecteur interroge le
  tokenizer une fois et, s'il ne replie pas `I`, laisse ces textes au code historique ; tests
  `FormattedTextCultureTests`). Second vérificateur neuf : **CONFIRMED** (0 écart sur 900 séquences sous `tr-TR` et
  `az-Latn-AZ`, 70 textes écrits à la main ; aucune autre lettre ASCII n'est concernée, sur 890 cultures ; zéro
  allocation inchangé sous `fr-FR` et `en-US`).
- Couverture différentielle contre `ForceLegacyTextPath` : 609 textes (290 fixes, 320 générés), 47 040 layouts,
  24 670 appels de dessin, 32 780 sondes de bornes ; le premier vérificateur y a ajouté environ 140 000 opérations
  aléatoires sans écart introduit. API publique de MGUI inchangée par ces commits.
- Constats reportés, non corrigés :
  - P3, antérieur à ces tâches (présent à `a05134f6`) : après un balisage invalide posé pendant que
    `AllowsInlineFormatting` passe de faux à vrai, `Runs` et `Lines` montrent le dernier texte (`[bold=y`) quand
    l'ancien chemin garde le précédent (`[bold=x`) ;
  - P3 : le zéro tient tant qu'un bloc utilise au plus 64 noms d'infobulles ou d'actions et au plus 128 valeurs par
    sorte de balise ; au-delà, le cache vidé réalloue (résultats toujours égaux à l'ancien chemin) ;
  - P4 : sous une culture turque ou azérie, un texte dont un type de balise contient un `I` majuscule alloue (environ
    11 Ko par changement) ;
  - un bloc sans balise avec `IsUnderlined` ou un fond repasse par l'ancienne boucle de dessin, qui alloue ;
  - un bloc à balises garde un lecteur, deux tampons de runs et deux layouts (environ 4 à 5 Ko par bloc) ;
  - les moteurs réels (SpriteFont, FontStashSharp) sont prouvés à zéro par T1.1, le bloc par les moteurs de test :
    aucun test ne mesure les deux ensemble.
- Bizarreries du code historique reproduites telles quelles, non corrigées : tout ce qui suit un `[` sans type connu
  devient un seul littéral, sauts de ligne compris ; les paires de barres obliques inverses ne sont réduites que
  devant un `[` ; `[/u]` sans ouverture donne un soulignement désactivé de hauteur 0 ; le soulignement par défaut
  applique l'opacité deux fois ; `[shadow=Red 1,2]` retombe sur le décalage (1,1).

### ✅ T4.1 — Documentation et clôture

- Fin : `MGUI/docs` (texte) et `docs/engine` du moteur disent quand utiliser `SetText(ReadOnlySpan<char>)` ; ADR de
  MGUI indexé ; `verifier` frais sur le chantier ; les deux solutions et `CasaEngine.Tests` verts.
- Commit : `docs(text): document the allocation-free text path`
- **Fait le 2026-11-15** (MGUI `06b5c965`). Section « Texte sans allocation (MGTextBlock) » de
  `MGUI/Docs/text-architecture.md` (usage, ce qui est à zéro, limites) ; section « Allocation-free text » de
  `docs/engine/casaengine-mgui-backend.md`, signalée dans `docs/README.md` ; ADR-0026 de MGUI indexé depuis T1.1.
  Vérification de clôture : celle de T2.1 à T3.1 ci-dessus (CONFIRMED sur MGUI `1080e587` ; seule la documentation a
  changé depuis).
- Validation sur MGUI `06b5c965` : `MGUI.Tests` 3543 verts (sur `1080e587`), `CasaEngine.Tests` 5455 verts, aucun
  sauté ; `MGUI.sln`, `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- Suites possibles, hors de ce plan : faire passer par le chemin sans allocation le bloc sans balise souligné ou avec
  fond ; `TextCharactersPerSecond` sans `TextRevealRun` par changement ; le reste de l'image du bureau (plan du
  bureau).
- Le plan reste dans `tasks/` jusqu'à l'acceptation de l'auteur ; la branche `chantier/mgui-nonalloc` (moteur et
  MGUI) n'est ni poussée ni fusionnée.

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Les mesures ci-dessus viennent du runtime de test ; refaire la mesure du dessin avec une transaction non enregistreuse (T0.1). | T0.1 |
| O2 | `SpriteFont` de MonoGame et FontStashSharp n'ont pas été vus accepter un `ReadOnlySpan<char>` ; la voie prévue passe par leurs surcharges `StringBuilder`, à vérifier dans T1.1 (voir D2 pour le repli). | T1.1 |
| O3 | Référence du sous-module MGUI : voir O2 du plan du bureau. | T1.1 |

## Hors périmètre

- Le reste d'une image du bureau (`MGDesktop`, `MGWindow`, layout des autres contrôles) : plan « image du bureau »
  ([mgui-desktop-frame-nonalloc-tasks.md](mgui-desktop-frame-nonalloc-tasks.md)), sauf réponse contraire à Q3.
