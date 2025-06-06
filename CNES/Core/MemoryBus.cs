using System;


namespace CNES.Core
{
    public  class MemoryBus
    {
        // 2 Kb RAM
        private byte[] ram = new byte[2048];
        private byte[] prgRom;
        private int prgRomSize;

        public MemoryBus(byte[] prgRomData)
        {
            prgRom = prgRomData;
            prgRomSize = prgRomData.Length;
        }

        public byte Read(ushort addr)
        {
            if(addr < 0x2000)
            {
                // Mirror ever 0x800 bytes in the 2 Kb RAM
                return ram[addr % 0x0800];
            }
            else if(addr >= 0x8000)
            {
                // Program Rom Area
                // If 16 Kb Program Rom, mirror it twice
                if(prgRomSize == 16384)
                {
                    int mirrorAddr = addr % 0x4000;     // Mirror 16 Kb bank in 32 Kb space
                    
                    return prgRom[mirrorAddr];
                }
                else
                {
                    // 32 Kb Program Rom
                    int index = addr - 0x8000;
                    return prgRom[index];
                }
            }
            else
            {
                // For now, unmapped or not implemented addresses return 0
                // TODO: Add PPU, APU and I/O 
                return 0;
            }
        }

        public void Write(ushort addr, byte value) 
        { 
            if(addr < 0x2000)
            {
                // Write to internal RAM
                ram[addr % 0x0800] = value;
            }
            else
            {
                // Write to other addresses not implemented yet
                // TODO: Add PPU, APU and I/O 
            }
        }
    }
}
