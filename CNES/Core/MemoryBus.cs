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
                // 0x0000-0x1FFF: Internal RAM (2Kb)
                // Mirror ever 0x800 bytes in the 2 Kb RAM
                return ram[addr % 0x0800];
            }
            else if(addr >= 0x8000)
            {
                // 0x8000-0xFFFF: PRG-ROM (ROM from Cartridge)
                // Program Rom Area
                if(prgRomSize == 16384)
                {
                    // If 16 Kb Program Rom, mirror it twice
                    int mirrorAddr = addr % 0x4000;     // 0x8000 - 0xBFFF and 0xC000 - 0xFFFF is mirrored.
                    
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
