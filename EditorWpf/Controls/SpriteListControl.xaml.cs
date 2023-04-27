using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls
{
    public partial class SpriteListControl : UserControl
    {
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(SpriteData), typeof(SpriteListControl));
        private GameEditorSprite _gameEditorSprite;

        public SpriteData SelectedItem
        {
            get => (SpriteData)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public SpriteListControl()
        {
            InitializeComponent();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedItem = ListBox.SelectedItem as SpriteData;
        }

        public void InitializeFromGameEditor(GameEditorSprite gameEditorSprite)
        {
            _gameEditorSprite = gameEditorSprite;
            _gameEditorSprite.GameStarted += OnGameStarted;
        }

        private void OnGameStarted(object? sender, System.EventArgs e)
        {
            DataContext = new SpritesModelView(_gameEditorSprite);
        }
    }

    public class SpritesModelView
    {
        private AssetContentManager _assetContentManager;
        public ObservableCollection<SpriteData> SpriteDatas { get; } = new();

        public SpritesModelView(GameEditorSprite gameEditorSprite)
        {
            _assetContentManager = gameEditorSprite.Game.GameManager.AssetContentManager;
            var spriteDatas = SpriteLoader.LoadFromFile(Path.Combine(GameSettings.ProjectManager.ProjectPath, "Spritesheets", "sprites.json"),
                _assetContentManager);
            var animations = Animation2dLoader.LoadFromFile(Path.Combine(GameSettings.ProjectManager.ProjectPath, "Spritesheets", "animations.json"),
                _assetContentManager);

            foreach (var spriteData in spriteDatas)
            {
                SpriteDatas.Add(spriteData);
            }
        }
    }
}
