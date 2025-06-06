using CNES.Core;
using CNES.Renderers;
using CNES.Data;

public class Program
{
    private static void Main(string[] args)
    {
        //var renderer = new ConsoleRenderer();
        //Emulator emu = new Emulator(renderer);

        //while (true)
        //{
        //    emu.Run();
        //    Thread.Sleep(1000 / 60);    // 60 FPS
        //}
        var romLoader = new RomLoader();
        romLoader.Load("./roms/SMB.nes");

        Console.WriteLine($"PRG Banks: {romLoader.PrgBanks}");
        Console.WriteLine($"CHR Banks: {romLoader.ChrBanks}");
        Console.WriteLine($"Mapper: {romLoader.MapperId}");
        string trainerStatus = romLoader.HasTrainer ? "Yes" : "No";
        Console.WriteLine($"Trainer: {trainerStatus}");
    }
}