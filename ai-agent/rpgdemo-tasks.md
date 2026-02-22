# RPGDemo — Migration Neoforce → MGUI

## Contexte

Le projet **CasaEngine.RPGDemo** (`Projects/CasaEngine.RPGDemo/`) et ses
ressources runtime (`Projects/RPGDemo/`) utilisent Neoforce pour trois écrans :

| Écran | Fichier `.screen` | Script C# | Contrôles Neoforce |
|---|---|---|---|
| **TitleScreen** | `Screens/TitleScreen/TitleScreen.screen` | `ScriptTitleScreen.cs` | Label "Rpg Demo", Button "Start Game", Button "Exit" |
| **MainHUD** | `Screens/MainHUD/MainHUD.screen` | `ScriptMainHUDScreen.cs` | ImageBox (portrait), ProgressBar (vie) |
| **GameOverScreen** | `Screens/GameOver/GameOverScreen.screen` | *(pas de script)* | Label "GAME OVER" |

### Flux actuel (Neoforce)

1. **TitleScreenWorld.world** → `ScriptTitleScreenWorld.OnBeginPlay()` :
   - `AssetContentManager.Load<ScreenGui>("TitleScreen.screen")`
   - Assigne `ScriptTitleScreen` en gameplay proxy
   - `world.AddScreen(screen)` → les contrôles sont gérés par `Manager` Neoforce
2. **DefaultWorld.world** → `ScriptWorld.OnBeginPlay()` :
   - Charge `MainHUD.screen` → `ScriptMainHUDScreen` (maj HP chaque frame)
   - `ScriptWorld.Update()` vérifie `Character.IsDead` → charge `TitleScreenWorld`

### Architecture cible (MGUI)

- Chaque écran devient une classe `UIScreenBase` (MGUI) poussée sur le `ScreenStack` du `UIRoot` de la vue active.
- Les `.screen` Neoforce sont remplacés par des fichiers **XAML MGUI** (optionnel — approche code-first aussi acceptable pour commencer).
- Les scripts (`ScriptTitleScreen`, `ScriptMainHUDScreen`) sont simplifiés : au lieu de chercher les contrôles par nom dans un `ScreenGui`, ils manipulent directement les propriétés de leur `UIScreenBase`.

---

## Tâches

### PR 1 — TitleScreen MGUI (code-first + XAML)

**Objectif :** Remplacer l'écran titre Neoforce par un écran MGUI.

- [ ] **1.1** Créer `Scripts/Screens/TitleScreen.cs` (hérite `UIScreenBase`)
  - `UILayer.Menu`, `IsModal = true`
  - `OnInitialize(UIRoot root)` : créer un `MGWindow` centré (sans barre de titre, fond semi-transparent)
  - Contenu : `MGStackPanel(Vertical)` avec :
    - `MGTextBlock` titre "RPG Demo" (bold, blanc, grande police)
    - `MGButton` "Start Game" → callback `OnStartGame`
    - `MGButton` "Exit" → callback `OnExit`
  - Callbacks stockés en `Action` injectées au constructeur

- [ ] **1.2** *(Optionnel)* Créer `Projects/RPGDemo/Screens/TitleScreen/TitleScreen.xaml`
  - XAML MGUI (namespace `clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core`)
  - `<Window>` avec `<StackPanel Orientation="Vertical">` + `<TextBlock>` + 2 × `<Button Name="...">`
  - Charger via `XAMLParser.LoadRootWindow(desktop, xamlString)` au lieu du code-first
  - Binder les événements par `Name` après parsing

- [ ] **1.3** Modifier `ScriptTitleScreenWorld.OnBeginPlay()`
  - Supprimer le `Load<ScreenGui>` + `AddScreen`
  - Récupérer `UIRoot` depuis `world.Game.GameManager.ViewManager.Views[0].UIRoot`
  - Créer `TitleScreen(onStartGame, onExit)` et faire `uiRoot.PushScreen(titleScreen)`
  - `onStartGame` → `world.Game.GameManager.SetWorldToLoad("DefaultWorld.world")`
  - `onExit` → `world.Game.Exit()`

- [ ] **1.4** Supprimer `ScriptTitleScreen.cs` (l'ancien script Neoforce)

- [ ] **1.5** Build + test manuel

**Comment tester :**
1. Lancer le RPGDemo, vérifier que l'écran titre MGUI s'affiche.
2. Cliquer "Start Game" → le monde DefaultWorld se charge.
3. Cliquer "Exit" → l'application se ferme.

---

### PR 2 — MainHUD MGUI (barre de vie + portrait)

**Objectif :** Remplacer le HUD de gameplay Neoforce par un HUD MGUI.

- [ ] **2.1** Créer `Scripts/Screens/MainHUDScreen.cs` (hérite `UIScreenBase`)
  - `UILayer.HUD`, `IsModal = false`
  - `OnInitialize(UIRoot root)` : créer un `MGWindow` ancré en bas-à-gauche (sans titre, fond semi-transparent)
  - Contenu : `MGDockPanel` ou `MGStackPanel(Horizontal)` avec :
    - `MGImage` pour le portrait du personnage (charger la texture `link_hud_portrait.sprite`)
    - `MGProgressBar` pour la barre de vie (`Minimum=0`, `Maximum=100`, `Value=100`)
  - Exposer `public MGProgressBar LifeBar { get; }` pour mise à jour externe

- [ ] **2.2** *(Optionnel)* Créer `Projects/RPGDemo/Screens/MainHUD/MainHUD.xaml`
  - Même patron que PR 1.2 mais avec `<ProgressBar Name="LifeBar" />` + `<Image />`

- [ ] **2.3** Modifier `ScriptWorld.OnBeginPlay()`
  - Supprimer le `Load<ScreenGui>("MainHUD.screen")` + `AddScreen`
  - Récupérer `UIRoot` et pousser `MainHUDScreen`
  - Stocker une référence à `MainHUDScreen` pour la mise à jour HP

- [ ] **2.4** Migrer la logique de `ScriptMainHUDScreen.Update()` dans `MainHUDScreen.Update()`
  - Recevoir le `Character` (ou HP/HPMax) via une interface/callback
  - Mettre à jour `LifeBar.Value` = `(HP / HPMax) * 100`

- [ ] **2.5** Supprimer `ScriptMainHUDScreen.cs` (l'ancien script Neoforce)

- [ ] **2.6** Build + test manuel

**Comment tester :**
1. Lancer le jeu, naviguer jusqu'au monde DefaultWorld.
2. Le portrait + la barre de vie s'affichent en bas-à-gauche via MGUI.
3. Se faire toucher par un ennemi → la barre de vie diminue correctement.

---

### PR 3 — GameOverScreen MGUI

**Objectif :** Créer l'écran Game Over (qui n'a pas encore de script C# associé).

- [ ] **3.1** Créer `Scripts/Screens/GameOverScreen.cs` (hérite `UIScreenBase`)
  - `UILayer.Modal`, `IsModal = true`
  - `MGWindow` centré plein écran ou grand panneau
  - `MGTextBlock` "GAME OVER" (police grande, centré, blanc/rouge)
  - `MGButton` "Return to Title" → callback `OnReturnToTitle`

- [ ] **3.2** *(Optionnel)* Créer `Projects/RPGDemo/Screens/GameOver/GameOverScreen.xaml`

- [ ] **3.3** Modifier `ScriptWorld.Update()` :
  - Quand `_playerCharacter.IsDead` → pousser `GameOverScreen` sur le `ScreenStack`
  - Le bouton "Return to Title" → `SetWorldToLoad("TitleScreenWorld.world")`
  - *(Alternative : garder le comportement actuel — retour direct au title, mais le Game Over serait plus intéressant)*

- [ ] **3.4** Build + test manuel

**Comment tester :**
1. Se laisser tuer par les ennemis.
2. L'écran "GAME OVER" s'affiche soit en overlay modal, soit en écran dédié.
3. Cliquer "Return to Title" → retour à l'écran titre.

---

### PR 4 — Nettoyage Neoforce

**Objectif :** Supprimer toute dépendance Neoforce du RPGDemo.

- [ ] **4.1** Supprimer les fichiers `.screen` Neoforce :
  - `Screens/TitleScreen/TitleScreen.screen`
  - `Screens/MainHUD/MainHUD.screen`
  - `Screens/MainHUD/MainHUD.texture`
  - `Screens/GameOver/GameOverScreen.screen`

- [ ] **4.2** Supprimer les skins Neoforce si non utilisés ailleurs :
  - `Skins/Default/*`
  - `Skins/Green/*`
  - *(Vérifier qu'aucun autre système ne les référence d'abord)*

- [ ] **4.3** Dans `ScriptTitleScreenWorld` et `ScriptWorld` :
  - Supprimer tout `using CasaEngine.Framework.GUI.Neoforce`
  - Supprimer tout `using CasaEngine.Framework.GUI` lié à `ScreenGui`
  - S'assurer que `world.AddScreen()` n'est plus appelé pour les Neoforce screens

- [ ] **4.4** Vérifier que `CasaEngine.RPGDemo.csproj` ne référence pas directement Neoforce
  - (La référence est indirecte via `CasaEngine.csproj` — ne pas y toucher pour l'instant)

- [ ] **4.5** Build complet `CasaEngine.RPGDemo.csproj` (Debug + Release)

- [ ] **4.6** Vérifier que le DLL est copié correctement dans `Projects/RPGDemo/`

---

### PR 5 — (Bonus) Fichiers XAML MGUI pour les screens

**Objectif :** Créer les fichiers `.xaml` MGUI pour chaque écran, permettant l'édition visuelle et le data-binding futur.

- [ ] **5.1** `Screens/TitleScreen/TitleScreen.xaml`
  ```xml
  <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
          SizeToContent="WidthAndHeight" TitleBarVisibility="Collapsed"
          Padding="20" Background="Black * 0.8">
      <StackPanel Orientation="Vertical" Spacing="12"
                  HorizontalAlignment="Center">
          <TextBlock Text="RPG Demo" IsBold="True" FontSize="24"
                     Foreground="White" HorizontalAlignment="Center" />
          <Button Name="ButtonStartGame" Padding="16,8"
                  Content="Start Game" />
          <Button Name="ButtonExit" Padding="16,8"
                  Content="Exit" />
      </StackPanel>
  </Window>
  ```

- [ ] **5.2** `Screens/MainHUD/MainHUD.xaml`
  ```xml
  <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
          Left="10" Bottom="10" Width="200" Height="60"
          TitleBarVisibility="Collapsed" Background="Black * 0.6">
      <StackPanel Orientation="Horizontal" Spacing="8" Padding="4">
          <Image Name="Portrait" Width="48" Height="48" />
          <ProgressBar Name="LifeBar" Minimum="0" Maximum="100"
                       Value="100" Width="130" Height="16"
                       VerticalAlignment="Center" />
      </StackPanel>
  </Window>
  ```

- [ ] **5.3** `Screens/GameOver/GameOverScreen.xaml`
  ```xml
  <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
          SizeToContent="WidthAndHeight" TitleBarVisibility="Collapsed"
          Background="Black * 0.9" Padding="40"
          HorizontalAlignment="Center" VerticalAlignment="Center">
      <StackPanel Orientation="Vertical" Spacing="16"
                  HorizontalAlignment="Center">
          <TextBlock Text="GAME OVER" Foreground="Red" IsBold="True"
                     FontSize="32" HorizontalAlignment="Center" />
          <Button Name="ButtonReturnToTitle" Padding="16,8"
                  Content="Return to Title" />
      </StackPanel>
  </Window>
  ```

- [ ] **5.4** Modifier les classes `UIScreenBase` pour charger depuis XAML :
  ```csharp
  protected override void OnInitialize(UIRoot root)
  {
      var xaml = File.ReadAllText(Path.Combine(EngineEnvironment.ProjectPath,
          "Screens/TitleScreen/TitleScreen.xaml"));
      _window = XAMLParser.LoadRootWindow(root.Desktop, xaml);
      var startBtn = _window.GetDescendantByName<MGButton>("ButtonStartGame");
      startBtn.AddCommandHandler(_ => _onStartGame());
      // ...
  }
  ```

- [ ] **5.5** Build + test

---

## Résumé des dépendances

```
PR1 (TitleScreen)  ──┐
PR2 (MainHUD)      ──┼──► PR4 (Nettoyage Neoforce)
PR3 (GameOverScreen)─┘         │
                               ▼
                         PR5 (XAML optionnel)
```

PR1, PR2, PR3 peuvent être faits en parallèle.
PR4 ne peut être fait qu'après que les trois écrans soient migrés.
PR5 est optionnel et indépendant.

## Notes techniques

- **`UIRoot` disponibilité** : Le `UIRoot` est créé automatiquement par `CasaEngineGame` quand une vue est ajoutée au `ViewManager`. Il est donc disponible dans `OnBeginPlay()` (qui est appelé après `world.LoadContent()`).
- **`ScreenGui` / `world.AddScreen()`** : Ce pattern Neoforce ne sera plus utilisé. Les écrans MGUI sont poussés directement sur `uiRoot.ScreenStack`.
- **`Character` référence pour le HUD** : Le `MainHUDScreen` a besoin d'accéder aux HP du joueur. Deux approches :
  1. Injecter le `Character` au constructeur du screen.
  2. Utiliser un système d'événements / observable (plus découplé).
- **`XAMLParser`** : `XAMLParser.LoadRootWindow(MGDesktop, string xamlContent)` retourne un `MGWindow`. Les contrôles nommés peuvent être retrouvés via `GetDescendantByName<T>(name)` (à vérifier pour l'API exacte).
- **Images/Textures** : Le portrait dans le HUD (`link_hud_portrait.sprite`) devra être chargé comme texture MonoGame et assigné à un `MGImage`. Vérifier le support `MGImage` en MGUI.
