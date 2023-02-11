namespace CasaEngine.Gameplay
{
    public class GlobalVar
    {

        private static GlobalVar _instance;
        private Dictionary<string, int> _vars;

        public static GlobalVar Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GlobalVar();
                }
                return _instance;
            }
        }

        public Dictionary<string, int> Vars
        {
            get => _vars;
            set => _vars = value;
        }

        private GlobalVar()
        {
            _vars = new Dictionary<string, int>();
        }

    }
}
