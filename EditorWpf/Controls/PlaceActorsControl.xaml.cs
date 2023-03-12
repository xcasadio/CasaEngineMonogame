using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EditorWpf.Datas;

namespace EditorWpf.Controls
{
    /// <summary>
    /// Interaction logic for PlaceActorsControl.xaml
    /// </summary>
    public partial class PlaceActorsControl : UserControl
    {
        public PlaceActorsControl()
        {
            InitializeComponent();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (sender != null && e.LeftButton == MouseButtonState.Pressed)
            {
                DragDrop.DoDragDrop(sender as Label,
                    JsonSerializer.Serialize(new DragAndDropInfo
                    {
                        Action = DragAndDropInfoAction.Create,
                        Type = DragAndDropInfoType.Actor
                    }), DragDropEffects.Copy);
            }
        }
    }
}
