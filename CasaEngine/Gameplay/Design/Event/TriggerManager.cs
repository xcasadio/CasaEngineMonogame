namespace CasaEngine.Design.Event
{
    public class TriggerManager
    {
        readonly List<Trigger> _triggers = new List<Trigger>();



        public List<Trigger> Triggers => _triggers;


        public void Update(float elapsedTime)
        {
            foreach (Trigger t in _triggers.ToArray())
            {
                t.Update(elapsedTime);
            }
        }

        public void Reset()
        {
            foreach (Trigger t in _triggers.ToArray())
            {
                t.Reset();
            }
        }

    }
}
