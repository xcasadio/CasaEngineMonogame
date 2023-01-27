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
using Editor.Tools.Event;
using CasaEngineCommon.Logger;

namespace Editor.Sprite2DEditor.Event
{
    /// <summary>
    /// 
    /// </summary>
    public partial class AnimationListEventForm 
        : Form
    {
        #region Static

        static private Dictionary<string, IEventWindowFactory> m_EventWindowFactories = new Dictionary<string, IEventWindowFactory>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="event_"></param>
        /// <param name="factory_"></param>
        static public void AddEventWindowFactory(string event_, IEventWindowFactory factory_)
        {
            m_EventWindowFactories.Add(event_, factory_);
        }

        #endregion

        #region Fields

        private List<EventActor> m_Events;

        #endregion

        #region Properties

        /// <summary>
        /// Gets
        /// </summary>
        public List<EventActor> EventActorList
        {
            get { return m_Events; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public AnimationListEventForm(List<EventActor> events_)
        {
            m_Events = events_;

            if (m_Events == null)
            {
                m_Events = new List<EventActor>();
            }

            InitializeComponent();

            listBoxEvent.Items.AddRange(m_Events.ToArray());
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAddEvent_Click(object sender, EventArgs e)
        {
            List<string> eventNames = new List<string>();

            foreach (var pair in m_EventWindowFactories)
            {
                eventNames.Add(pair.Key);
            }

            Editor.WinForm.InputComboBox InputComboBoxForm = new Editor.WinForm.InputComboBox("Add an event", "Choose a type of event", eventNames.ToArray());

            if (InputComboBoxForm.ShowDialog(this) == DialogResult.OK)
            {
                if (string.IsNullOrWhiteSpace(InputComboBoxForm.SelectedItem) == false)
                {
                    if (m_EventWindowFactories.ContainsKey(InputComboBoxForm.SelectedItem) == true)
                    {
                        IAnimationEventBaseWindow form = m_EventWindowFactories[InputComboBoxForm.SelectedItem].CreateNewForm(null);

                        if (((Form)form).ShowDialog(this) == DialogResult.OK)
                        {
                            if (form.EventActor != null)
                            {
                                AddEvent(form.EventActor);
                            }
                        }

                        ((Form)form).Dispose();
                    }
                    else
                    {
                        LogManager.Instance.WriteLineError("Can't find a AnimationEventBaseWindow object for the type of event " + InputComboBoxForm.SelectedItem);
                    }
                }
            }

            InputComboBoxForm.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="event_"></param>
        private void AddEvent(EventActor event_)
        {
            m_Events.Add(event_);
            listBoxEvent.Items.Add(event_.ToString());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDeleteEvent_Click(object sender, EventArgs e)
        {
            if (listBoxEvent.SelectedIndex != -1)
            {
                m_Events.RemoveAt(listBoxEvent.SelectedIndex);
                listBoxEvent.Items.RemoveAt(listBoxEvent.SelectedIndex);                
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonModifyEvent_Click(object sender, EventArgs e)
        {
            if (listBoxEvent.SelectedIndex != -1)
            {
                throw new NotImplementedException();
                /*AnimationEventForm form = new AnimationEventForm(m_Events[listBoxEvent.SelectedIndex]);

                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    m_Events[listBoxEvent.SelectedIndex] = form.EventActor;
                }
                 
                 form.Dispose();*/
            }
        }

        #endregion
    }
}
