
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CasaEngine.Gameplay.Actor.Event;
using CasaEngine.Design.Event;

namespace Editor.Sprite2DEditor.Event
{
    /// <summary>
    /// 
    /// </summary>
    public partial class AnimationListEventForm
        : Form
    {

        private List<EventActor> m_Events;



        /// <summary>
        /// Gets
        /// </summary>
        public List<EventActor> EventActorList
        {
            get { return m_Events; }
        }



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



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAddEvent_Click(object sender, EventArgs e)
        {
            AnimationEventForm form = new AnimationEventForm(null);

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                AddEvent(form.EventActor);
            }
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
                AnimationEventForm form = new AnimationEventForm(m_Events[listBoxEvent.SelectedIndex]);

                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    m_Events[listBoxEvent.SelectedIndex] = form.EventActor;
                }
            }
        }

    }
}
