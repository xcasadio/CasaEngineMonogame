namespace CasaEngine.Framework.Gameplay.Design.Event
{
    public class TriggerManager
    {
        private readonly List<Trigger> _triggers = new();



        public List<Trigger> Triggers => _triggers;


        public void Update(float elapsedTime)
        {
            foreach (var t in _triggers.ToArray())
            {
                t.Update(elapsedTime);
            }
        }

        public void Reset()
        {
            foreach (var t in _triggers.ToArray())
            {
                t.Reset();
            }
        }

    }
}
