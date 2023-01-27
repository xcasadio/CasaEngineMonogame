using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CasaEngine.Gameplay.Actor.Event;
using CasaEngine.Design.Event;
using Editor.Sprite2DEditor.Event;

namespace Editor.Sprite2DEditor.Event
{
    /// <summary>
    /// 
    /// </summary>
    public partial class AnimationEventForm 
        : Form
    {
        #region Fields

        private EventActor m_Event;

        #endregion

        #region Properties

        /// <summary>
        /// Gets
        /// </summary>
        public EventActor EventActor
        {
            get { return m_Event; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public AnimationEventForm(EventActor event_)
        {
            InitializeComponent();
            comboBoxEventType.Items.AddRange(Enum.GetNames(typeof(EventActorType)));
            comboBoxEventType.SelectedIndex = 0;

            m_Event = event_;

            if (m_Event is EventActor)
            {
                EventActor ea = (EventActor)m_Event;
                SetEventPanel(ea.EventActorType);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxEventType_SelectedIndexChanged(object sender, EventArgs e)
        {
            EventActorType type = (EventActorType)Enum.Parse(typeof(EventActorType), (string)comboBoxEventType.SelectedItem);
            SetEventPanel(type);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type_"></param>
        private void SetEventPanel(EventActorType type_)
        {
            panelEvent.Controls.Clear();

            switch (type_)
            {
                case EventActorType.PlaySound:
                    panelEvent.Controls.Add(new AnimationSoundEventControl(m_Event));
                    break;

                case EventActorType.SpawnActor:

                    break;

                case EventActorType.MoveActor:

                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOK_Click(object sender, EventArgs e)
        {
            AnimationEventBaseControl c = (AnimationEventBaseControl)panelEvent.Controls[0];
            m_Event = c.EventActor;
        }

        #endregion
    }
}
