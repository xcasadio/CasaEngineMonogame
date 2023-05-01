using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EditorWpf.Controls.Common;
using EditorWpf.Controls.SpriteControls;

namespace EditorWpf.Controls.Animation2dControls
{
    public partial class Animation2dListControl : UserControl
    {
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(Animation2dDataViewModel), typeof(Animation2dListControl));
        private GameEditorAnimation2d _gameEditor;

        public Animation2dDataViewModel SelectedItem
        {
            get => (Animation2dDataViewModel)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public Animation2dListControl()
        {
            InitializeComponent();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedItem = ListBox.SelectedItem as Animation2dDataViewModel;
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

        private void ListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem != null)
            {
                var inputTextBox = new InputTextBox();
                inputTextBox.Description = "Enter a new name";
                inputTextBox.Title = "Rename";
                var animation2dDataViewModel = (listBox.SelectedItem as Animation2dDataViewModel);
                inputTextBox.Text = animation2dDataViewModel.Name;

                if (inputTextBox.ShowDialog() == true)
                {
                    _gameEditor.Game.GameManager.AssetContentManager.Rename(animation2dDataViewModel.Name, inputTextBox.Text);
                    animation2dDataViewModel.Name = inputTextBox.Text;
                }
            }
        }

        public void LoadAnimations2d(string fileName)
        {
            var animation2dListModelView = DataContext as Animation2dListModelView;
            animation2dListModelView.LoadAnimations2d(fileName);

            if (animation2dListModelView.Animation2dDatas.Count > 0)
            {
                Dispatcher.Invoke(() => ListBox.SelectedIndex = 0);
            }
        }
    }
}