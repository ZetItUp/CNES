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

        private int screenWidth = 256;
        private int screenHeight = 240;

        public Emulator(IRenderer renderer) 
        { 
            this.renderer = renderer;
            renderer.Initialize(screenWidth, screenHeight);
            var romLoader = new RomLoader();
            romLoader.Load("./roms/coredump.nes");

            Console.WriteLine($"PRG Banks: {romLoader.PrgBanks}");
            Console.WriteLine($"CHR Banks: {romLoader.ChrBanks}");
            Console.WriteLine($"Mapper: {romLoader.MapperId}");
            string trainerStatus = romLoader.HasTrainer ? "Yes" : "No";
            Console.WriteLine($"Trainer: {trainerStatus}");

            memoryBus = new MemoryBus(romLoader.PrgRom);
            cpu = new CPU6502(memoryBus);

            cpu.Reset();
        }

        public void Run()
        {
            cpu.Step();

            Color[] framebuffer = new Color[screenWidth * screenHeight];

            for(int i = 0; i < framebuffer.Length; i++)
            {
                framebuffer[i] = Color.Black;
            }

            //renderer.Clear(Color.Black);
            //renderer.RenderFrame(framebuffer);
            //renderer.Present();
        }
    }
}
