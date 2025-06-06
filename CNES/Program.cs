using CNES.Core;
using CNES.Renderers;

public class Program
{
    private static void Main(string[] args)
    {
        var renderer = new ConsoleRenderer();
        Emulator emu = new Emulator(renderer);

        while(true)
        {
            emu.Run();
            Thread.Sleep(1000 / 60);    // 60 FPS
        }
    }
}