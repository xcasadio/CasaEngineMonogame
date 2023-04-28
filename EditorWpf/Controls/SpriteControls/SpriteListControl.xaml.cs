using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Assets.Map2d;

namespace EditorWpf.Controls.SpriteControls
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
}
