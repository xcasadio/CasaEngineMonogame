# Dialogue : choix et police bitmap — E12.a (moteur)

Mécanisme générique ajouté au pipeline de dialogue existant (`DialogueService` / `IDialoguePresenter` /
`DialogueScreen`, cf. [yarn_spinner_integration.md](yarn_spinner_integration.md) pour le contexte
architectural d'origine — cette page documente l'état réel du code, plus avancé que la feuille de
route Yarn sur ces deux points précis). Décision de conception : **le moteur ne porte aucune
sémantique Alundra** (pas de masque de fermeture, pas de `Result`, pas de codes de contrôle `\` —
tout cela reste côté DLL). Le moteur expose uniquement le **mécanisme** : un état de choix générique
et un chemin d'enregistrement de police bitmap.

---

## 1. État de choix sur `DialogueService` / `IDialoguePresenter`

`DialogueRuntimeState` a un troisième membre :

```text
Closed            aucun dialogue ouvert
Open              dialogue ouvert, une ligne affichée
AwaitingChoice     dialogue ouvert, une liste de choix affichée, en attente de sélection
```

`IsOpen` vaut `true` pour `Open` **et** `AwaitingChoice` — la boîte de dialogue reste ouverte
pendant qu'un choix est affiché.

`IDialoguePresenter` gagne :

```csharp
IReadOnlyList<string> Choices { get; }   // vide hors AwaitingChoice
bool HasChoices { get; }                 // Choices.Count > 0

event EventHandler<DialogueChoiceSelectedEventArgs> ChoiceSelected;

bool ShowChoices(IReadOnlyList<string> labels);  // labels non vide, sinon ArgumentException
bool SelectChoice(int index);                    // l'UI rapporte l'index sélectionné
```

`DialogueChoiceSelectedEventArgs` porte `SelectedIndex` et les `Labels` affichés au moment de la
sélection.

### Cycle de vie

```text
ShowLine(...)              → Open
ShowChoices(["Oui","Non"]) → AwaitingChoice, Choices = ["Oui","Non"]
SelectChoice(1)            → ChoiceSelected(SelectedIndex=1, Labels=["Oui","Non"]), puis Open, Choices vide
Close()                    → Closed, Choices vide
```

- `ShowChoices` remplace toujours la liste précédente (copie défensive) : un second appel — avec
  ou sans sélection entre les deux — ne laisse jamais de libellés de l'appel précédent dans
  `Choices`.
- `SelectChoice` hors `AwaitingChoice` retourne `false` sans effet. Un index hors bornes lève
  `ArgumentOutOfRangeException`.
- `Close()` efface `Choices` quel que soit l'état courant.

Toute la logique « qu'est-ce que représente le choix » (OUI/NON, texte système, etc.) est décidée
par l'appelant (DLL) : le moteur ne fait que porter des `string` et un index.

### UI : `DialogueScreen`

`DialogueScreen` affiche la liste de choix sous la ligne de texte, sous forme de `MGButton`
empilés dans un `MGStackPanel` dédié (masqué — `Visibility.Collapsed` — quand
`presenter.HasChoices` est faux). La navigation utilise la mécanique de focus **existante** de
MGUI (`UIFocusNavigationService` : Haut/Bas/manette naviguent entre les boutons focusables par
position, comme n'importe quel autre écran MGUI) ; valider (`Submit`, clavier Entrée/Espace ou
bouton A manette) déclenche le clic du bouton focalisé, qui appelle `presenter.SelectChoice(index)`
avec l'index capturé à la construction du bouton. Aucun `MGListBox` n'a été introduit : la
sélection ET la validation d'un `MGListBox` sont couplées à la navigation (déplacer le focus
sélectionne déjà l'item), ce qui aurait rendu ambigu le moment où « sélection = validation » côté
`SelectChoice` ; des boutons indépendants gardent Submit comme seul déclencheur explicite.

```csharp
var screen = new DialogueScreen(dialogueService, closeRequestAction);
// ... plus tard, côté DLL/appelant :
dialogueService.ShowChoices(new[] { "Oui", "Non" });
// le joueur navigue et valide dans l'UI → dialogueService.SelectChoice(index) est appelé par DialogueScreen
```

---

## 2. Enregistrement d'une police bitmap (BMFont)

`FontStashSharpTextEngine` (MGUI.FontStashSharp) ne consommait jusqu'ici que des polices TTF via
`FontSystem` (rasterisation à la taille demandée). Une nouvelle méthode enregistre une police à
taille fixe :

```csharp
public void AddStaticFont(string family, CustomFontStyles style, SpriteFontBase font);
```

`font` est n'importe quel `FontStashSharp.SpriteFontBase` déjà construit — typiquement un
`StaticSpriteFont` obtenu via `StaticSpriteFont.FromBMFont(...)` à partir d'un `.fnt` + sa
texture. La méthode ne connaît rien du contenu de la police (pas de nom `font3`, pas de
supposition Alundra) : elle se contente de câbler l'objet dans la table `(famille, style)` déjà
utilisée par `AddFontSystem`.

`ResolveFont` consulte cette table **avant** les `FontSystem` TTF : une police statique
enregistrée pour `(famille, style)` — ou à défaut `(famille, Normal)`, même règle de repli que
pour les `FontSystem` — est renvoyée telle quelle, **sans re-rasterisation** à la taille demandée
par le `FontSpec` : c'est tout l'intérêt d'une police « statique ».

```csharp
StaticSpriteFont font3 = StaticSpriteFont.FromBMFont(fntFileContents, pageName => LoadTexture(pageName));
engine.AddStaticFont("font3", CustomFontStyles.Normal, font3);
```

> **Depuis l'ADR-0036** : un jeu n'enregistre plus sa police bitmap sur un moteur de texte, qui est
> reconstruit à chaque monde. Il la tient par `CasaEngineGame.UIFonts.Acquire(idDuFnt)`, et le registre la
> donne à chaque moteur de texte. Voir [asset-handles-and-bitmap-fonts.md](asset-handles-and-bitmap-fonts.md).

### `DialogueScreen` sur une police enregistrée

`DialogueScreen` a un constructeur `(presenter, requestClose, fontFamily)` : si `fontFamily` est
renseigné, la ligne de texte et les libellés de choix reçoivent `FontFamily = fontFamily` ; sinon
(constructeurs à 1 ou 2 arguments, inchangés), l'écran garde le TTF par défaut du thème. Aucune
police n'est câblée en dur : c'est l'appelant qui choisit d'utiliser `"font3"` (ou tout autre nom
enregistré via `AddStaticFont`) ou de laisser le comportement par défaut.

### Remplacer le balisage de la boîte par un asset du projet

Le balisage par défaut (`DialogueScreen.xaml`, ressource embarquée du moteur) peut être remplacé par un écran du
projet (programme bound-screens, D12, ADR-0038). Le réglage optionnel `DialogueScreenAsset` du fichier de projet
nomme l'asset `.uiscreen`, par identifiant ou par nom ; vide ou absent, rien ne change.

```json
{ "DialogueScreenAsset": "<id de l'asset .uiscreen>" }
```

L'écran lit ce réglage par le gestionnaire d'assets qu'on lui donne : seul le constructeur
`(presenter, requestClose, fontFamily, assetContentManager)` le consulte, les autres gardent le balisage embarqué.
L'enveloppe est tenue de la construction à `Dispose`.

Le balisage de remplacement doit déclarer les éléments que l'écran pilote :

| Nom | Type XAML | Rôle |
|---|---|---|
| `pnlContent` | `StackPanel` | conteneur de la boîte ; sa largeur préférée suit celle de la fenêtre |
| `lblLine` | `TextBlock` | la ligne (orateur en gras, puis le texte) ; reçoit `fontFamily` |
| `pnlChoices` | `StackPanel` | reçoit un `Button` par choix ; masqué sans choix |
| `btnClose` | `Button` | enfant direct de `pnlContent` ; obligatoire si `ShowCloseButton` est vrai, facultatif sinon (retiré de l'arbre) |

Le reste du balisage est libre (fond, marges, éléments décoratifs). La position et la taille de la fenêtre sont
calculées par l'écran : `Left`, `Top`, la largeur et la hauteur déclarés ne sont que des valeurs de départ. La
fenêtre prend ensuite la hauteur de son contenu, 150 px au minimum, quelle que soit la hauteur déclarée.

Si l'asset ne se charge pas (identifiant ou nom inconnu, fichier absent, enveloppe illisible, XAML invalide) ou si
le balisage ne respecte pas ce contrat, l'écran écrit un avertissement qui nomme le réglage et la raison, puis
utilise le balisage embarqué. Une fenêtre de remplacement rejetée rend ses bindings avant d'être abandonnée.

---

## 3. Ce qui reste hors moteur

Toute sémantique Alundra — masques de fermeture (`g_etcAnimationMode`), `Result` du choix,
résolution des libellés OUI/NON via `etc-index.json`, découpage en pages, codes de contrôle `\`,
machine à écrire — est portée par la DLL (`AlundraDialogueDirector`, tranches E12.a-DLL et
E12.c). Le moteur ne fournit que : un état de choix générique, et un chemin d'enregistrement de
police bitmap générique.

---

## 4. Tests

- `CasaEngine.Tests/Dialogue/DialogueServiceChoiceTests.cs` : forme miroir de
  `DialogueServiceTests` — `ShowChoices` expose les libellés, `SelectChoice` complète avec
  l'index exact, `Close` efface l'état de choix, un second `ShowChoices` (après complétion ou en
  cours d'attente) repart à neuf, et un test de contrat pilote `DialogueService` uniquement via
  `IDialoguePresenter` pour vérifier l'ordre des notifications
  (`ShowLine` → `ShowChoices` → `SelectChoice` → `Close`).
- `CasaEngine.Tests/UI/FontStashSharpBitmapFontRegistrationTests.cs` : `AddStaticFont` puis
  `ResolveFont` renvoie la police enregistrée quelle que soit la taille demandée dans le
  `FontSpec` (pas de re-rasterisation) ; repli sur `Normal` si le style demandé n'est pas
  enregistré ; sans enregistrement, `ResolveFont` retombe sur le placeholder existant. Ces tests
  n'utilisent pas `StaticSpriteFont.FromBMFont` directement : décompiler FontStashSharp.MonoGame
  1.5.6 a confirmé que même sa variante « chargement de texture injectable »
  (`FromBMFont(string, Func<string, TextureWithOffset>)`) exige un `Texture2D` non nul — donc un
  `GraphicsDevice` vivant — et que `SpriteFontBase` ne peut pas être sous-classée hors de son
  assembly (`PreDraw`/`GetKerning` sont `internal abstract`). Les tests utilisent donc un
  `SpriteFontBase` bien réel obtenu de façon 100 % headless via `FontSystem.GetFont(...)` (TTF,
  rasterisation CPU StbTrueType, déjà exploité sans périphérique par
  `MGUI.Tests.Text.FSSMeasureDrawConsistencyTests`) pour vérifier le seul point qui nous
  intéresse ici : `AddStaticFont` + `ResolveFont` câblent correctement une police *fixe* par nom,
  sans jamais la re-rasteriser.
- `CasaEngine.Tests/Dialogue/DialogueScreenReplacementTests.cs` : sans réglage, le balisage embarqué, sans
  avertissement ; un remplacement valide est utilisé, pilote la ligne et rend son enveloppe à `Dispose` ; un
  `btnClose` de remplacement est gardé quand la boîte en montre un ; un asset inconnu, un XAML invalide ou un
  contrat rompu (élément absent, mauvais type, `btnClose` hors de `pnlContent`, `btnClose` absent alors que la
  boîte en montre un) retombent sur le balisage embarqué avec un avertissement qui dit pourquoi.
- `CasaEngine.Tests/Dialogue/DialogueScreenReplacementBindingTests.cs` : une fenêtre de remplacement rejetée ne
  laisse aucun binding dans le registre de MGUI.
- `CasaEngine.Tests/Configuration/ProjectSettingsDialogueScreenTests.cs` : `DialogueScreenAsset` n'est écrit que
  s'il est renseigné et se relit tel quel ; un fichier sans le champ le laisse vide.
