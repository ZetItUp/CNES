using System.Drawing;
using CNES.Data;
using CNES.Renderers;

namespace CNES.Core
{
    public class Emulator
    {
        private IRenderer renderer;
        private CPU6502 cpu;
        private MemoryBus memoryBus;
        private PPU ppu;

        public Emulator(IRenderer renderer) 
        { 
            var romLoader = new RomLoader();
            romLoader.Load("./roms/SMB.nes");

            Console.WriteLine($"PRG Banks: {romLoader.PrgBanks}");
            Console.WriteLine($"CHR Banks: {romLoader.ChrBanks}");
            Console.WriteLine($"Mapper: {romLoader.MapperId}");
            string trainerStatus = romLoader.HasTrainer ? "Yes" : "No";
            Console.WriteLine($"Trainer: {trainerStatus}");

            ppu = new PPU();
            this.renderer = renderer;
            memoryBus = new MemoryBus(romLoader.PrgRom, ppu);
            cpu = new CPU6502(memoryBus);
            renderer.Initialize(PPU.ScreenWidth, PPU.ScreenHeight);
            ppu.GenerateTestFrame();

            cpu.Reset();
        }

        public void Run()
        {
            cpu.Step();

            renderer.RenderFrame(ppu.Framebuffer);
            renderer.Present();

            Console.ReadKey();
        }
    }
}
