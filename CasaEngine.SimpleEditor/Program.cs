using System;

public class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var game = new CasaEngine.SimpleEditor.Game1();
        game.Run();
    }
}