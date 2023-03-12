using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
