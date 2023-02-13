using CasaEngine.Game;
using CasaEngineCommon.Logger;
using CasaEngine.Editor.Assets;

namespace Editor.SoundEditor
{
    public partial class SoundEditorForm : Form
    {
        public SoundEditorForm()
        {
            InitializeComponent();

            string[] assets = Engine.Instance.AssetManager.GetAllAssetByType(AssetType.Audio);

            listBoxSoundAsset.Items.AddRange(assets);
        }

        private void buttonBuildNewAsset_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Multiselect = true;
            openDialog.Filter = "Sound Files|*.wav|" +
                        "WAV Files (*.wav)|*.wav|" +
                        "All Files (*.*)|*.*";

            if (openDialog.ShowDialog(this) == DialogResult.OK)
            {
                Cursor = Cursors.WaitCursor;
                string spriteSheetName = string.Empty;

                foreach (string fileName in openDialog.FileNames)
                {
                    AddSoundAsset(fileName);
                }

                listBoxSoundAsset.SelectedIndex = listBoxSoundAsset.Items.Count - 1;

                Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        private void AddSoundAsset(string fileName_)
        {
            string assetFileName = string.Empty;
            string assetName = Path.GetFileNameWithoutExtension(fileName_);

            if (Engine.Instance.AssetManager.AddAsset(
                fileName_,
                assetName,
                AssetType.Audio,
                ref assetFileName) == true)
            {
                listBoxSoundAsset.Items.Add(assetName);
                LogManager.Instance.WriteLineDebug("Asset '" + fileName_ + "' added.");
            }
            else
            {
                LogManager.Instance.WriteLineError("Can't add the asset '" + fileName_ + "'");
            }
        }
    }
}
