using CasaEngine.Editor.Assets;
using CasaEngine.Framework;
using CasaEngine.Framework.Game;

namespace Editor
{
    public partial class BuildAssetForm : Form
    {
        /// <summary>
        /// 
        /// </summary>
        public BuildAssetForm()
        {
            InitializeComponent();

            foreach (AssetInfo info in CasaEngineGame.Game.GameManager.AssetManager.Assets)
            {
                ListViewItem item = new ListViewItem(
                    new string[] {
                        info.Name,
                        Enum.GetName(typeof(AssetType), info.Type)});
                item.Tag = info;

                listView1.Items.Add(item);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonBuild_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listView1.CheckedItems)
            {
                CasaEngineGame.Game.GameManager.AssetManager.RebuildAsset((AssetInfo)item.Tag);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
            {
                propertyGrid1.SelectedObject = null;
            }
            else
            {
                propertyGrid1.SelectedObject = listView1.SelectedItems[0];
            }
        }
    }
}
