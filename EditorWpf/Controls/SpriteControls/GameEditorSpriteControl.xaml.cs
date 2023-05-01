using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;

namespace EditorWpf.Controls.SpriteControls
{
    public partial class GameEditorSpriteControl : UserControl
    {
        public GameEditorSprite GameEditor => gameEditor;

        public GameEditorSpriteControl()
        {
            InitializeComponent();
        }

        private void OnZoomChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gameEditor == null)
            {
                return;
            }
            var value = ((e.AddedItems[0] as ComboBoxItem).Content as string).Remove(0, 1);
            gameEditor.Scale = float.Parse(value);
        }

        private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonHotSpot_OnClick(object sender, RoutedEventArgs e)
        {
            var checkBox = (sender as CheckBox);
            gameEditor.Game.GetGameComponent<Renderer2dComponent>().DrawSpriteOrigin = checkBox.IsChecked ?? false;
        }

        private void ButtonSpriteBorder_OnClick(object sender, RoutedEventArgs e)
        {
            var checkBox = (sender as CheckBox);
            gameEditor.Game.GetGameComponent<Renderer2dComponent>().DrawSpriteBorder = checkBox.IsChecked ?? false;
        }

        private void ButtonDisplaySpriteSheet_OnClick(object sender, RoutedEventArgs e)
        {
            var checkBox = (sender as CheckBox);
            gameEditor.Game.GetGameComponent<Renderer2dComponent>().DrawSpriteSheet = checkBox.IsChecked ?? false;
        }

        private void Transparency_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (gameEditor?.Game == null)
            {
                return;
            }

            var slider = (sender as Slider);
            gameEditor.Game.GetGameComponent<Renderer2dComponent>().SpriteSheetTransparency = (int)slider.Value;
        }
    }
}
