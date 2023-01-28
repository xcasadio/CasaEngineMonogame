
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Editor.Sprite2DEditor.Event;
using CasaEngine.Gameplay.Actor.Event;
using CasaEngine.Gameplay;
using CasaEngine.Game;
using CasaEngine.Gameplay.Actor.Object;

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
            BaseObject obj = Engine.Instance.ObjectManager.GetObjectByPath(textBox1.Text);

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
