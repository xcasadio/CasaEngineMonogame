using System;
using CasaEngine.Demos;

public static class Program
{
    [STAThread]
    static void Main()
    {
        using var game = new DemosGame();
        game.Run();
    }
}