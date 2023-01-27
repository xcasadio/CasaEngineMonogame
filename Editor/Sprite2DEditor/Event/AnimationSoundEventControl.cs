using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CasaEngine.Game;
using CasaEngine.Editor;
using CasaEngine.Design.Event;
using CasaEngine.Gameplay.Actor.Event;
using Editor.Sprite2DEditor.Event;
using CasaEngine.Editor.Assets;

namespace Editor.Sprite2DEditor.Event
{
    /// <summary>
    /// 
    /// </summary>
    public partial class AnimationSoundEventControl
        : AnimationEventBaseControl
    {
        #region Fields

        private PlaySoundEvent m_Event = null;

        #endregion

        #region Properties

        /// <summary>
        /// Gets
        /// </summary>
        public PlaySoundEvent PlaySoundEvent
        {
            get { return m_Event; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public AnimationSoundEventControl(IEvent event_)
        {
            InitializeComponent();

            string[] assets = GameInfo.Instance.AssetManager.GetAllAssetByType(AssetType.Audio);

            if (assets.Length == 0)
            {
                comboBoxSoundAsset.Visible = false;
                label1.Text = "Please add a sound before set an event";
            }
            else
            {
                comboBoxSoundAsset.Items.AddRange(assets);

                if (event_ is PlaySoundEvent)
                {
                    m_Event = (PlaySoundEvent)event_;
                }
                else
                {
                    m_Event = new PlaySoundEvent((string)comboBoxSoundAsset.Items[0]);
                }

                //base class AnimationEventBaseControl
                m_EventActor = m_Event;

                if (comboBoxSoundAsset.Items.Count > 0)
                {
                    comboBoxSoundAsset.SelectedIndex = 0;
                }                
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxSoundAsset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxSoundAsset.SelectedIndex != -1)
            {
                m_Event.AssetName = (string)comboBoxSoundAsset.SelectedItem;
            }
        }

        #endregion
    }
}
