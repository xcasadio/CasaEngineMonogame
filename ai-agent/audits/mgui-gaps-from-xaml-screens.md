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

## G1 — Un curseur déclaré en XAML ne peut pas afficher sa valeur

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

## G2 — Une fenêtre racine ne sait pas se placer par rapport au bureau

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

## G3 — Déclarer la taille d'une fenêtre en XAML l'épingle, et empêche tout redimensionnement au contenu

**Priorité : haute.** Celui-ci ne se voit pas : il ne casse rien à l'analyse, ne lève aucune erreur, et
transforme simplement `ApplySizeToContent` en opération sans effet. Il a coûté deux tests rouges et une
bisection pour être trouvé.

**Ce que le code doit faire.** La boîte de dialogue part d'une hauteur minimale de 150 px puis **grandit**
pour contenir une ligne qui se replie sur plusieurs rangs plus autant de boutons de choix que le dialogue en
offre. C'est un défaut signalé par un joueur : le second bouton de choix sortait de la fenêtre.

**Ce qui se passe.** Sur le DTO XAML `Window`, `Width` et `Height` servent **deux fois**. `ToElement` les
passe au constructeur de `MGWindow` (`MGUI/MGUI.Core/UI/XAML/Controls.cs`, `Math.Clamp(Height ?? 0, ...)`),
ce qui est l'effet attendu — mais ce sont aussi les alias de `PreferredWidth` et `PreferredHeight` hérités
d'`Element` (`MGUI/MGUI.Core/UI/XAML/Element.cs:241` et `:244`), que la passe générale de réglages applique
également. La fenêtre se retrouve donc avec une hauteur **préférée** que rien n'a demandée, et
`MGWindow.ApplySizeToContent` la respecte : la fenêtre reste à 150 px quoi que contienne son arbre.

**Ce que ça coûte aujourd'hui.** Une fenêtre qui doit s'adapter à son contenu ne peut pas déclarer sa taille
de départ du tout. `DialogueScreen.xaml` déclare `MinHeight="150"` — qui alimente le constructeur via le
`Clamp` sans toucher à la taille préférée — et sa largeur est posée en C#. C'est une **astuce**, pas une
expression : rien dans le document ne dit qu'il s'agit d'une hauteur de départ, et le prochain qui écrira
`Height="150"` par réflexe repassera par le même diagnostic.

**L'API absente.** De quoi distinguer, sur une fenêtre racine, la taille **initiale** de la taille
**imposée**. Par exemple un `SizeToContent="Height"` déclaratif qui neutralise la taille préférée
correspondante, ou des propriétés distinctes :

```xml
<Window WindowWidth="720" WindowHeight="150" SizeToContent="Height" MinHeight="150" />
```

**Coût estimé.** Petit à moyen, mais il touche une sémantique existante : il faut décider si `Width` sur une
fenêtre racine doit cesser d'alimenter `PreferredWidth`, ce qui serait un changement de comportement, ou si
de nouvelles propriétés s'ajoutent à côté. Le second chemin est additif et sans risque.

**Au minimum, et sans rien changer :** que `Width`/`Height` sur un `Window` soient documentés comme fixant
aussi la taille préférée. Le piège est entièrement silencieux aujourd'hui.

**Sa portée est plus large que le seul cas qui l'a révélé.** Il a été trouvé sur `DialogueScreen`, où il
neutralisait `ApplySizeToContent`. Mais **deux autres écrans déclarent `Height` en XAML et plafonnent
ensuite leur hauteur en C#** : `DemoInfoScreen` (`Math.Min(440, viewport - 20)`) et
`BlendingControlsScreen` (`Math.Min(560, viewport - 20)`). L'analyse dit que le plafond l'emporte —
`MGElement.UpdateMeasurement` termine par un `Clamp` sur la place disponible, et le rectangle de mise en page
d'une fenêtre est bâti sur `WindowWidth`/`WindowHeight`, pas sur sa taille préférée. **Cette conclusion est
analytique, pas observée.** Elle se vérifie en une fois : lancer une démo dans une vue de moins de 460 px de
haut et regarder si le navigateur de démos est rogné. Tant que ce n'est pas fait, c'est le seul endroit du
chantier où le raisonnement remplace la mesure.

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
