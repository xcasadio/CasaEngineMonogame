namespace CasaEngine.Design.Event
{
    public class TriggerManager
    {
        readonly List<Trigger> m_Triggers = new List<Trigger>();



        public List<Trigger> Triggers => m_Triggers;


        public void Update(float elapsedTime_)
        {
            foreach (Trigger t in m_Triggers.ToArray())
            {
                t.Update(elapsedTime_);
            }
        }

        public void Reset()
        {
            foreach (Trigger t in m_Triggers.ToArray())
            {
                t.Reset();
            }
        }

    }
}
