using System;

namespace InputSequenceSample
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (InputSequenceSampleGame game = new InputSequenceSampleGame())
            {
                game.Run();
            }
        }
    }
#endif
}

