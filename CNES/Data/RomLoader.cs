using System;
using System.IO;

namespace CNES.Data
{
    public class RomLoader
    {
        // PRG-ROM (Program Code)
        public byte[] PrgRom { get; private set; }
        // CHR-ROM (Graphics Data)
        public byte[] ChrRom { get; private set; }

        // Number of 16Kb Program Banks
        public int PrgBanks {  get; private set; }
        // Number of 8Kb Graphics Banks
        public int ChrBanks { get; private set; }

        // Mapper ID
        public int MapperId { get; private set; }
        // File contains 512-byte trainer block
        public bool HasTrainer { get; private set; }

        public void Load(string path)
        {
            // Read ROM file 
            byte[] data = File.ReadAllBytes(path);

            // Verify the iNES File Format
            // The iNES header always starts with 'N, 'E', 'S', 0x1A
            if (data[0] != 'N' || data[1] != 'E' || data[2] != 'S' || data[3] != 0x1A)
            {
                Console.WriteLine("ERROR: Invalid NES File! Missing iNES header.");

                return;
            }

            // Byte 4 = Number of 16 Kb Program Banks
            PrgBanks = data[4];
            // Byte 5 = Number of 8 Kb Graphics Banks
            ChrBanks = data[5];

            // Byte 6 = Bit 2 indicates presence of 512-byte trainer block
            HasTrainer = (data[6] & 0b00000100) != 0;

            // Mapper ID = upper 4 bits of byte 7 + upper 4 bits of byte 6
            int mapperLow = (data[6] >> 4);     // Lower 4 bits from byte 6
            int mapperHigh = (data[7] >> 4);    // Upper 4 bits from byte 7
            MapperId = (mapperHigh << 4) | mapperLow;

            // Calculate sizes of PRG and CHR ROM in bytes
            int prgSize = PrgBanks * 16 * 1024; // 16 Kb per Program Bank
            int chrSize = ChrBanks * 8 * 1024;  // 8 Kb per Graphics Bank

            // Offset starts after the 16-byte iNES header
            int offset = 16;

            // Skip 512-byte trainer block if present
            if(HasTrainer)
            {
                offset += 512;
            }

            // Extract Program Code
            PrgRom = new byte[prgSize];
            Array.Copy(data, offset, PrgRom, 0, prgSize);
            offset += prgSize;

            // Extract Graphics Data (may be 0 if using CHR-RAM instead)
            ChrRom = new byte[chrSize];
            if(chrSize > 0)
            {
                Array.Copy(data, offset, ChrRom, 0, chrSize);
            }

            // (Note: Bytes 8–15 of the header are currently ignored; they contain mirroring, NES 2.0 format, etc.)
        }
    }
}
