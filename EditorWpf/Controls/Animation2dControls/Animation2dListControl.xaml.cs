using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Assets.Animations;

namespace EditorWpf.Controls.Animation2dControls
{
    public partial class Animation2dListControl : UserControl
    {
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(Animation2dData), typeof(Animation2dListControl));
        private GameEditorAnimation2d _gameEditor;

        public Animation2dData SelectedItem
        {
            get => (Animation2dData)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public Animation2dListControl()
        {
            InitializeComponent();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedItem = ListBox.SelectedItem as Animation2dData;
        }

        public void InitializeFromGameEditor(GameEditorAnimation2d gameEditor)
        {
            _gameEditor = gameEditor;
            _gameEditor.GameStarted += OnGameStarted;
        }

        private void OnGameStarted(object? sender, System.EventArgs e)
        {
            DataContext = new Animation2dListModelView(_gameEditor);
        }
    }
}