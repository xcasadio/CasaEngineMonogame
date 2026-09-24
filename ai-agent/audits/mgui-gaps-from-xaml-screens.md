# Ce qui manque à MGUI et au moteur, vu depuis la conversion des écrans en XAML

Rapport ouvert le 2026-09-20 pendant le chantier
[xaml-runtime-bridge-tasks.md](../tasks/xaml-runtime-bridge-tasks.md).

**Pourquoi ce document.** Règle de l'auteur, 2026-09-20 : *« je ne veux pas de workaround, et le projet sert
aussi à détecter les fonctionnalités manquantes dans MGUI et le moteur CasaEngine »*. Convertir dix écrans
d'un arbre C# vers un document XAML est un banc d'essai : chaque fois que quelque chose ne peut **pas** être
déclaré, la limite se voit. Un contournement écrit en silence effacerait l'information ; elle est écrite ici.

**Ce qui entre dans ce document et ce qui n'y entre pas.** Trois cas se ressemblent et ne se valent pas :

1. **Un vrai manque d'API** — le runtime sait faire la chose, mais rien ne permet de la déclarer, ou personne
   ne sait la faire du tout. **Seuls ceux-là sont listés ci-dessous.**
2. **Un choix de conception délibéré** — par exemple le fond du HUD de smoke, qui reste en C# parce qu'une
   autre partie du code lit la même constante et que deux sources de vérité divergeraient. Ce n'est pas un
   manque, c'est une décision ; elle vit dans le plan.
3. **Une limite volontaire de la règle XAML** — le code câble les gestionnaires et pousse les données, par
   décision (ADR-0035, D7). Ce n'est pas un manque non plus.

Chaque entrée dit : ce que le code doit faire, pourquoi il ne peut pas le déclarer (avec la preuve), ce que
ça coûte aujourd'hui, et à quoi ressemblerait l'API absente.

---

## ~~G1~~ — Un curseur déclaré en XAML ne peut pas afficher sa valeur — **CORRIGÉ le 2026-09-20**

> **Comblé dans MGUI**, branche `chantier/declarable-window-size-and-slider-label`, commit `3c48741` :
> `ShowValueLabel` et `ValueLabelFormat` sont deux propriétés facultatives du DTO `Slider`, sur la forme
> exacte de `ProgressBar`. Les six curseurs du panneau de mélange déclarent désormais leur étiquette avec
> leur plage et leur valeur de départ, et le contournement a disparu de
> `BlendingControlsScreen.BindSlider`. Suite MGUI 2961 verts, +3.

**Priorité : haute.** Six curseurs concernés dans un seul écran, et tout écran de réglages futur le
rencontrera.

**Ce que le code doit faire.** Chaque curseur du panneau de mélange affiche sa valeur courante, formatée :
`"F3"` pour le pas, `"F2"` pour les durées et les poids.

**Pourquoi il ne peut pas le déclarer.** `MGSlider.ShowValueLabel` et `MGSlider.ValueLabelFormat` sont
publiques et pilotent un `ValueLabelElement` interne (`MGUI/MGUI.Core/UI/MGSlider.cs:606-625`). Mais le DTO
XAML `Slider` (`MGUI/MGUI.Core/UI/XAML/Controls.cs:2654`) **ne les expose ni l'une ni l'autre** : une
recherche des deux noms dans tout `MGUI/MGUI.Core/UI/XAML/` ne rend rien. Le DTO expose pourtant `Minimum`,
`Maximum`, `Value`, les ticks, le pouce, les pinceaux — tout sauf l'étiquette de valeur.

**Ce que ça coûte aujourd'hui.** Le format de chaque curseur vit dans
`BlendingControlsScreen.BindSlider`, à des dizaines de lignes de la plage et de la valeur de départ qu'il
accompagne. Changer un `"F2"` en `"F3"` demande d'ouvrir le C#, alors que tout le reste du curseur est dans
le document. C'est exactement la séparation que la règle XAML cherchait à supprimer.

**L'API absente.** Deux propriétés nullables sur le DTO `Slider`, et deux lignes dans son
`ApplyDerivedSettings` :

```xml
<Slider Name="sldIdleWeight" Minimum="0" Maximum="1" Value="0"
        ShowValueLabel="True" ValueLabelFormat="F2" />
```

**Ce qui rend le manque certain plutôt que discutable.** `ProgressBar`, le contrôle voisin, **expose bien**
son affichage de valeur en XAML : `ShowValue`, `ValueDisplayFormat` et `NumberFormat` figurent tous trois sur
son DTO (`MGUI/MGUI.Core/UI/XAML/Controls.cs`, classe `ProgressBar`). Deux contrôles du même genre, l'un
complet et l'autre non : ce n'est pas un choix de conception, c'est un oubli.

**Coût estimé.** Petit et purement additif : ~10 lignes dans `MGUI.Core`, plus un test. Aucun appelant
existant n'est touché, puisque les deux propriétés sont facultatives.

---

## ~~G2~~ — Une fenêtre racine ne sait pas se placer par rapport au bureau — **CORRIGÉ le 2026-09-20**

> **Comblé dans MGUI**, même branche, commits `8748b72` puis `1bfd401` : `ScreenHorizontalAlignment`,
> `ScreenVerticalAlignment` et `ScreenMargin` sur `MGWindow` et sur son DTO XAML, réappliqués à chaque tick
> du bureau. Le plafonnement au viewport **tombe de la sémantique d'alignement** au lieu d'être une seconde
> fonctionnalité : un élément aligné ne dépasse pas la place dont il dispose, et `MinWidth`/`MinHeight`
> l'emportent encore. Sept écrans du moteur ont perdu leur arithmétique ; dix tests vérifient qu'ils
> atterrissent exactement où leur calcul C# les mettait, plafond compris. Suite MGUI 2982 verts, +17.
>
> **La syntaxe proposée plus bas était fausse** : `HorizontalAlignment` est déjà pris sur une fenêtre racine,
> qui refuse tout autre chose que `Stretch` et lève. D'où les noms préfixés.
>
> **Et l'inset n'est pas `Margin`**, contrairement à ce que cette entrée disait : sur une fenêtre racine,
> `Margin` rogne déjà le **contenu**, comme un second padding — mesuré, 260 px de large au lieu de 280. Le
> réutiliser aurait discrètement resserré chaque fenêtre placée. D'où `ScreenMargin`, qui ne fait que l'inset.

**Priorité : haute.** Quatre des six écrans convertis ont dû calculer leur position en C#, et deux d'entre
eux aussi leur hauteur.

**Ce que le code doit faire.** Centrer une fenêtre (menu de pause), la poser en bas au centre (rappel F1),
l'ancrer en haut à droite (navigateur de démos, panneau de mélange), et plafonner sa hauteur au viewport —
ce dernier point comptant vraiment, car dans une démo en écran partagé chaque vue n'est qu'une fraction du
back-buffer.

**Pourquoi il ne peut pas le déclarer.** `MGWindow` n'expose que `Left` et `Top`, des entiers absolus
(`MGUI/MGUI.Core/UI/MGWindow.cs:97` et `:113`). Il n'existe **aucune** notion de placement relatif au
bureau : pas de `WindowStartupLocation`, pas d'alignement contre `Desktop.ValidScreenBounds`, pas de taille
exprimée en fraction de l'écran. La seule chose que MGUI fasse avec `ValidScreenBounds` est de **ramener**
une fenêtre déjà déplacée à l'intérieur de l'écran (`:1904-1926`) — une correction après coup, pas un
placement. Le DTO XAML `Window` n'a donc rien non plus : `Left`, `Top`, `SizeToContent`, et c'est tout.

**Ce que ça coûte aujourd'hui.** Quatre `OnWindowLoaded` contiennent la même arithmétique, avec les mêmes
constantes de largeur et de hauteur dupliquées entre le C# et le XAML — puisque centrer demande de connaître
sa propre taille, déjà écrite dans le document. C'est une duplication qui peut diverger en silence : changer
`Width` dans le XAML sans changer `WindowWidth` dans le C# décentre la fenêtre sans que rien ne le signale.

**L'API absente.** De quoi exprimer le placement dans le document, par exemple :

```xml
<Window Width="300" Height="200" HorizontalAlignment="Center" VerticalAlignment="Center" />
<Window Width="300" Height="36"  HorizontalAlignment="Center" VerticalAlignment="Bottom" Margin="0,0,0,14" />
<Window Width="320" HorizontalAlignment="Right" VerticalAlignment="Top" Margin="10" MaxHeight="Viewport" />
```

Le dernier point — plafonner une dimension au viewport — est le plus utile et le moins évident : c'est ce
qui rend un écran correct en vue partagée sans que chaque écran le recalcule.

**Coût estimé.** Moyen. Il faut décider où l'alignement d'une fenêtre racine s'applique (au moment de
l'attachement au bureau, et à chaque changement de taille du bureau), et ce que devient un `Left`/`Top`
explicite quand un alignement est déclaré. C'est une vraie petite fonctionnalité, pas deux propriétés.

---

## ~~G3~~ — Une fenêtre ne peut pas déclarer une taille de départ qu'un redimensionnement ultérieur surcharge — **CORRIGÉ le 2026-09-20**

> **Comblé dans MGUI**, même branche, commit `f2c2f88` : `ApplySizeToContent` libère la taille préférée
> des dimensions qu'on lui demande de dimensionner, et **seulement** celles-là — une largeur déclarée
> survit à une demande en hauteur, et `SizeToContent.Manual` ne libère rien. `DialogueScreen.xaml`
> déclare de nouveau `Height="150"`, qui se lit, au lieu de l'astuce `MinHeight`. Les cinq
> `DialogueScreenLayoutTests` passent sans modification. Suite MGUI 2965 verts, +4, et les 2958 tests
> d'origine confirment que rien ne dépendait de l'ancien comportement.

**Priorité : haute.** Silencieux de bout en bout : rien ne lève, rien n'avertit, et une méthode publique
devient une opération sans effet. Il a coûté deux tests rouges et une bisection.

**Rectification d'un premier diagnostic.** Ce manque a d'abord été décrit ici comme un oubli — « `Width` et
`Height` posent aussi la taille préférée ». C'est inexact, et il faut le dire : le DTO `Window` a un modèle
**délibéré et cohérent**, implémenté dans son `ApplyDerivedSettings` (`MGUI/MGUI.Core/UI/XAML/Controls.cs`) :
ni `Width` ni `Height` déclarés signifie « s'adapte au contenu dans les deux sens » ; `Width` seul, hauteur
adaptée ; `Height` seul, largeur adaptée ; les deux, taille fixe ; et `SizeToContent` surcharge tout. Poser
`PreferredHeight` quand `Height` est déclaré est donc **le contrat**, pas une étourderie.

**Le trou réel, plus étroit.** Il n'existe aucune façon d'exprimer « commence à cette taille, mais grandis
ensuite ». Et la surcharge censée servir à cela ne fonctionne pas : quand `Height` **et**
`SizeToContent="Height"` sont tous deux déclarés, `ApplySettings` pose la taille préférée **avant** que
`ApplyDerivedSettings` n'appelle `ApplySizeToContent`, dont la mesure rend alors la taille préférée. Deux
déclarations se contredisent, et c'est la moins explicite qui l'emporte, sans un mot.

**Mesuré, pas déduit.** Une fenêtre dont le contenu réclame 155 px :

| Ce qui est déclaré | `WindowHeight` obtenu | `PreferredHeight` |
|---|---|---|
| `Height="40" SizeToContent="Height"` | **50** | 40 |
| `MinHeight="40" SizeToContent="Height"` | **155** | null |

La première ligne est la surcharge défaite : `SizeToContent="Height"` est déclaré, et la fenêtre ne grandit
pas. La seconde est l'astuce qui marche.

**Ce que le code doit faire.** La boîte de dialogue part de 150 px puis **grandit** pour contenir une ligne
repliée sur plusieurs rangs plus autant de boutons de choix que le dialogue en offre — un défaut signalé par
un joueur : le second bouton sortait de la fenêtre.

**Ce que ça coûte aujourd'hui.** `DialogueScreen.xaml` déclare `MinHeight="150"` et aucune `Height`, parce
que le `Math.Clamp(Height ?? 0, MinHeight ?? 0, ...)` du constructeur atteint 150 par ce biais sans toucher à
la taille préférée. Ça marche et c'est une **astuce** : rien dans le document ne dit qu'il s'agit d'une
hauteur de départ, et le prochain qui écrira `Height="150"` par réflexe repassera par le même diagnostic.

**Le correctif**, dans `MGWindow.ApplySizeToContent`, avant la mesure :

```csharp
if (Value is SizeToContent.Width or SizeToContent.WidthAndHeight)
    PreferredWidth = null;
if (Value is SizeToContent.Height or SizeToContent.WidthAndHeight)
    PreferredHeight = null;
```

Demander à une fenêtre de se dimensionner sur son contenu dans une direction tout en lui tenant une taille
préférée dans cette même direction est une contradiction : la demande explicite et postérieure doit gagner.

**Ce que ça ne peut pas casser.** Cette combinaison ne fonctionne pas aujourd'hui — la fenêtre reste
épinglée — donc rien ne peut en dépendre. Les autres appelants (`MGComboBox` pour sa liste déroulante,
`MGContextMenu`, `MGDesktop`) construisent leurs fenêtres en code sans taille préférée : les deux lignes n'y
font rien. `MGWindow` réapplique déjà les réglages mémorisés au changement de mise en page, ce qui reste
cohérent.

**Ce que ça gagne.** `SizeToContent="Height"` fait enfin ce qu'il annonce ; le `MinHeight` du dialogue
redevient un `Height="150" SizeToContent="Height"` qui se lit ; et le contournement disparaît.

**Portée à vérifier par la mesure.** Deux autres écrans déclarent `Height` puis plafonnent leur hauteur en
C# : `DemoInfoScreen` (`Math.Min(440, viewport - 20)`) et `BlendingControlsScreen`
(`Math.Min(560, viewport - 20)`). L'analyse dit que le plafond l'emporte — `MGElement.UpdateMeasurement`
termine par un `Clamp` sur la place disponible, et le rectangle de mise en page d'une fenêtre est bâti sur
`WindowWidth`/`WindowHeight`. **Cette conclusion n'a pas été observée.** Elle se vérifie en lançant une démo
dans une vue de moins de 460 px de haut.

---

## Ce qui n'est **pas** un manque, et pourquoi

Pour que la liste ci-dessus garde son sens, voici ce que la conversion a laissé en C# **sans** que ce soit
une limite de MGUI :

- **Les gestionnaires d'événements.** Le code les câble par décision (ADR-0035, D7), pas par impuissance.
- **Les valeurs poussées à chaque image** — le compteur de temps, le titre de la démo courante. C'est la
  définition même de la donnée.
- **Les listes pilotées par les données**, comme le bouton par démo du navigateur. MGUI sait faire des
  `ListBox` avec `ItemTemplate` ; c'est la règle « pas de binding en ligne » qui fait remplir le panneau au
  code, et c'est un choix.
- **Le fond du HUD de smoke.** Une autre partie du code lit la même constante ; deux sources de vérité
  dériveraient. Décision, pas manque.

---

## ~~G4~~ — La garde de plage du `Slider` XAML lit `MaxHeight` au lieu de `Maximum` — **CORRIGÉ le 2026-09-20**

> **Corrigé dans MGUI**, même branche. Un identifiant, pas une fonctionnalité.

Dans `Slider.ApplyDerivedSettings` (`MGUI/MGUI.Core/UI/XAML/Controls.cs`), la garde qui applique la plage
lisait `MaxHeight` — une propriété de **mise en page** héritée d'`Element` — là où elle veut dire `Maximum` :

```csharp
if (Minimum.HasValue || MaxHeight.HasValue)   // devenu : || Maximum.HasValue
{
    Slider.SetRange(Minimum ?? Slider.Minimum, Maximum ?? Slider.Maximum);
}
```

**Rectification d'une première affirmation.** Ce rapport disait d'abord qu'« un curseur déclarant `Maximum`
sans `Minimum` voit son maximum ignoré en silence ». **C'est faux, et la mesure l'a montré** : deux tests
écrits pour attraper ce cas sont passés *avant* le correctif. La raison est une ligne plus haut —
`CreateElementInstance` fait `new MGSlider(Window, Minimum ?? 0, Maximum ?? 100, ...)`, donc le
**constructeur** applique déjà la plage, et le `SetRange` de cette garde est redondant sur le chemin de
chargement. La faute était donc **latente** : du code faux que rien ne révélait.

Elle est corrigée quand même, et le fait qu'elle soit redondante est précisément ce qui rend le correctif
sûr : au chargement, le comportement est identique au bit près. Seul un éventuel ré-appel d'
`ApplyDerivedSettings` sur un curseur déjà construit en tirerait une différence — et ce chemin-là serait
aujourd'hui le seul à perdre un `Maximum` déclaré seul.

Les deux tests restent : ils épinglent qu'une plage déclarée est honorée, ce qui vaut d'être tenu, même s'ils
ne distinguent pas l'avant de l'après.

## ~~G5~~ — Un moteur de texte ne sait pas retirer une police statique — **CORRIGÉ le 2026-09-21**

> **Corrigé dans MGUI** (`3075d93`, branche `chantier/asset-handles`), pour le chantier
> [asset-handles-tasks.md](../tasks/archive/asset-handles-tasks.md), ADR-0036.

**Ce que le code doit faire.** Une police bitmap tenue au niveau du jeu est donnée par référence à chaque
moteur de texte de l'interface. Quand le gestionnaire de ressources la libère, elle doit quitter les moteurs
de texte encore vivants. L'éditeur, par exemple, garde ses vues d'un monde à l'autre : sans retrait, un de ses
moteurs de texte résoudrait encore la famille vers une police dont la texture a été libérée.

**Pourquoi il ne le pouvait pas.** `FontStashSharpTextEngine.AddStaticFont`
(`MGUI/MGUI.FontStashSharp/FontStashSharpTextEngine.cs:270`) n'avait pas d'inverse, et `_staticFonts` est
privé.

**L'API ajoutée.** `bool RemoveStaticFont(string family, CustomFontStyles style)`, symétrique d'`AddStaticFont` :
elle retire l'entrée, invalide le cache de résolution, et rend `true` si une police a été retirée. Elle ne
touche pas une résolution déjà rendue à un élément de texte : qui affiche une police la tient. Cinq tests dans
`MGUI.Tests/Text/FontStashSharpStaticFontRemovalTests.cs`.

## G6 — Un `FontFamily` XAML inconnu est ignoré sans message

> **Consigné, non corrigé** (hors périmètre du chantier asset-handles, ADR-0036).

**Ce que le code doit faire.** Un écran déclare `FontFamily="font3"` sur ses `TextBlock`, et la famille doit
être connue du moteur de texte au moment où la fenêtre est construite.

**Ce qui se passe quand elle ne l'est pas.** Le DTO XAML applique la famille par `MGTextBlock.TrySetFont`
(`MGUI/MGUI.Core/UI/XAML/Controls.cs:3268-3275`). Pour une famille inconnue, `ResolveFont` rend une police de
repli marquée `IsFallback` sans rien signaler (`MGUI/MGUI.FontStashSharp/FontStashSharpTextEngine.cs:535-556`),
puis `TrySetFont` rend `false` et garde l'ancienne famille (`MGUI/MGUI.Core/UI/MGTextBlock.cs:109-122`). Le
texte s'affiche alors dans la police par défaut du thème, sans message.

**Ce que ça coûte.** C'est exactement la forme du défaut trouvé en jeu par le portage Alundra (E13.d, D6). Après
un changement de carte, l'inventaire s'est affiché en police TTF blanche, et rien dans le journal ne le disait :
la cause n'a été trouvée qu'en lisant le code et en rejouant le parcours avec un harnais.

**À quoi ressemblerait l'API absente.** Un avertissement journalisé, une fois par famille et par moteur de
texte, quand une famille déclarée en XAML retombe sur la police de repli. Ou un mode strict du chargeur XAML
(qui existe déjà pour les éléments inconnus) qui en ferait une erreur.

## G7 — Une image d'une fenêtre ne voit pas les textures ajoutées ou retirées à la racine

> **Consigné, non corrigé** (programme bound-screens, constat F4 du vérificateur de la phase 4, 2026-09-24 ;
> [bound-screens-tasks.md](../tasks/bound-screens-tasks.md), point ouvert O5). Le correctif touche une règle
> d'architecture de MGUI : décision de l'auteur.

**Ce que le code doit faire.** Quand l'hôte change la source de ses images (l'éditeur qui ouvre un autre
projet), `MGResources.ForgetHostResolvedTextures` retire de la portée racine les textures résolues par l'hôte.
Chaque image qui en affichait une doit se rafraîchir, et redemander son nom au nouveau catalogue.

**Pourquoi elle ne le peut pas.** Un `MGImage` s'abonne à `OnTextureAdded`/`OnTextureRemoved` de sa portée la
plus proche (`MGUI/MGUI.Core/UI/MGImage.cs:66-74`). Or chaque `MGWindow` crée sa propre portée dans son
constructeur (`MGUI/MGUI.Core/UI/MGWindow.cs:1640`), et une portée enfant ne relaie de sa parente que deux
événements, par un lien faible volontairement étroit (`MGUI/MGUI.Core/UI/MGResources.cs:124-133`, MGUI
ADR-0001) : le thème par défaut et les ressources statiques, pas les textures. Une image de fenêtre ne voit
donc aucune texture ajoutée ou retirée à la racine ; elle garde la texture qu'elle avait jusqu'à ce que son
`SourceName` change.

**Ce que ça coûte.** Reproduit par le vérificateur hors dépôt : après l'oubli, l'image garde l'ancienne
texture et ne redemande pas son nom. Dans l'éditeur, le fournisseur rend aussi ses handles au changement de
projet, et le chargement du premier monde collecte les assets sans référence : une image encore affichée
pourrait dessiner une texture libérée. Non observé : il faudrait qu'un aperçu de l'ancien projet reste ouvert
après le changement, ce qui n'a pas été vérifié en session. Les images animées ne sont pas touchées : leur
image courante devient nulle quand le fournisseur est libéré.

**À quoi ressemblerait l'API absente.** Soit les événements de texture relayés vers les portées enfants par
le même lien faible (un troisième événement dans un mécanisme que l'ADR-0001 de MGUI veut étroit) ; soit un
`MGImage` qui s'abonne à la portée qui a réellement fourni sa texture. Les deux sont des décisions de MGUI.

## G8 — Une `Image` XAML ne peut pas déclarer son filtrage à la réduction

> **Consigné, non corrigé** (programme bound-screens, tranche B2 du dépôt parent, 2026-09-24).

**Ce que le code doit faire.** L'inventaire d'Alundra dessine ses images en pixels natifs, agrandis d'un facteur
entier ; chaque `MGImage` y désactive le filtrage linéaire à la réduction, comme celles du HUD.

**Pourquoi il ne peut pas le déclarer.** `MGImage.UseLinearFilteringWhenDownscaling`
(`MGUI/MGUI.Core/UI/MGImage.cs:244`) n'a pas de pendant dans le DTO XAML `Image`
(`MGUI/MGUI.Core/UI/XAML/Controls.cs:1264-1312` : `SourceName`, `Source`, `TextureColor`, `Stretch`,
`AnimationStartOffset`, `IsAnimationPlaying`).

**Ce que ça coûte.** L'écran, dont toute la structure et toutes les valeurs sont désormais déclarées ou liées dans
son XAML, garde une boucle en C# qui parcourt ses images pour poser ce réglage.

**À quoi ressemblerait l'API absente.** Une propriété `UseLinearFilteringWhenDownscaling` sur le DTO `Image`,
appliquée dans `ApplyDerivedSettings` comme `Stretch`.
