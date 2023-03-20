using CasaEngine.Framework;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Gameplay.Actor.Event;

namespace Editor.Tools.Event.EventForm
{
    public partial class AnimationSoundEventForm
        : Form, IAnimationEventBaseWindow
    {
        public EventActor EventActor
        {
            get;
            private set;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="event_"></param>
        public AnimationSoundEventForm(EventActor event_)
        {
            InitializeComponent();

            EventActor = event_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOK_Click(object sender, EventArgs e)
        {
            Entity obj = EngineComponents.ObjectManager.GetObjectByPath(textBox1.Text);

            if (obj == null)
            {

                return;
            }

            /*if (obj is SoundActor)
            {

                return;
            }*/

            DialogResult = DialogResult.OK;
        }
    }
}
