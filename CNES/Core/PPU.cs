using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CNES.Core
{
    public class PPU
    {
        // PPU Registers (internal state)
        private byte PPUCTRL;    // $2000
        private byte PPUMASK;    // $2001
        private byte PPUSTATUS;  // $2002
        private byte OAMADDR;    // $2003
        private byte OAMDATA;    // $2004
        private byte PPUSCROLL;  // $2005 (needs latch logic)
        private byte PPUADDR;    // $2006 (needs latch logic)
        private byte PPUDATA;    // $2007

        // Latch for $2005/$2006 writes
        private bool addressLatch = false;
        private ushort vramAddress = 0;
        private ushort tempAddress = 0;
        private byte fineXScroll = 0;
        private byte bufferedData = 0;

        // 2KB VRAM (nametables, attribute tables)
        private byte[] vram = new byte[0x800];
        // 32B Palette RAM
        private byte[] palette = new byte[0x20];
        // 256B Object Attribute Memory (OAM)
        private byte[] oam = new byte[0x100];

        public const int ScreenWidth = 256;
        public const int ScreenHeight = 240;
        private const int FramebufferSize = ScreenWidth * ScreenHeight;

        private byte[] framebuffer;
        public byte[] Framebuffer => framebuffer;

        public PPU()
        {
            framebuffer = new byte[FramebufferSize]; // RGB
        }

        public void GenerateTestFrame()
        {
            byte colorIndex = 0x22; // Blue color index for testing
            for (int i = 0; i < FramebufferSize; i++)
            {
                // Simple color pattern for testing
                framebuffer[i] = colorIndex;
            }
        }

        public byte ReadRegister(ushort addr)
        {
            switch(addr & 0x2007)
            {
                case 0x2002: // PPUSTATUS
                    {
                        // Read PPUSTATUS and clear the VBlank flag
                        byte status = PPUSTATUS;

                        addressLatch = false; // Reset address latch
                        PPUSTATUS &= 0x7F; // Clear VBlank flag

                        return status;
                    }
                case 0x2004: // OAMDATA
                    {
                        // Read OAM data
                        return OAMDATA;
                    }
                case 0x2007: // PPUDATA
                    {
                        // Read from PPUDATA (VRAM)
                        byte data = bufferedData; // Return buffered data

                        bufferedData = ReadPPUMemory(vramAddress); // Read next byte from VRAM
                        if (vramAddress >= 0x3F00)
                        {
                            data = bufferedData; // If reading from palette, return the same byte
                        }
                        vramAddress++; // Increment VRAM address

                        return data;
                    }
                default:
                    {
                        return 0; // Unmapped or not implemented addresses return 0
                    }
            }
        }

        public void WriteRegister(ushort addr, byte value)
        {
            switch (addr & 0x2007)
            {
                case 0x2000: // PPUCTRL
                    {
                        PPUCTRL = value;
                        tempAddress = (ushort)((tempAddress & 0xF3FF) | ((value & 0x03) << 10));
                        break;
                    }
                case 0x2001: // PPUMASK
                    {
                        PPUMASK = value;
                        break;
                    }
                case 0x2003: // OAMADDR
                    {
                        OAMADDR = value;
                        break;
                    }
                case 0x2004: // OAMDATA
                    {
                        OAMDATA = value;
                        break;
                    }
                case 0x2005: // PPUSCROLL
                    {
                        if (!addressLatch)
                        {
                            fineXScroll = (byte)(value & 0x07);
                            tempAddress = (ushort)((tempAddress & 0xFFE0) | (value >> 3));
                            addressLatch = true;
                        }
                        else
                        {
                            tempAddress = (ushort)((tempAddress & 0x8FFF) | ((value & 0x07) << 12));
                            tempAddress = (ushort)((tempAddress & 0xFC1F) | ((value & 0xF8) << 2));
                            addressLatch = false;
                        }
                        break;
                    }
                case 0x2006: // PPUADDR
                    {
                        if (!addressLatch)
                        {
                            tempAddress = (ushort)((tempAddress & 0x00FF) | ((value & 0x3F) << 8));
                            addressLatch = true;
                        }
                        else
                        {
                            tempAddress = (ushort)((tempAddress & 0xFF00) | value);
                            vramAddress = tempAddress;
                            addressLatch = false;
                        }
                        break;
                    }
                case 0x2007: // PPUDATA
                    {
                        WritePPUMemory(vramAddress, value);
                        vramAddress++;
                        break;
                    }
            }
        }

        private byte ReadPPUMemory(ushort addr)
        {
            addr &= 0x3FFF; // PPU address bus is 14 bits

            if (addr < 0x2000)
            {
                // Pattern tables, handled by cartridge, not implemented here)
                return 0;
            }
            else if (addr < 0x3F00)
            {
                // Nametables (mirrored every 0x1000)
                return vram[(addr - 0x2000) % 0x800];
            }
            else if (addr < 0x4000)
            {
                // Palette RAM indexes (mirrored every 32 bytes)
                return palette[(addr - 0x3F00) % 0x20]; 
            }

            return 0;
        }

        private void WritePPUMemory(ushort addr, byte value)
        {
            addr &= 0x3FFF; // PPU address bus is 14 bits

            if (addr < 0x2000)
            {
                // Pattern tables, handled by cartridge, not implemented here
                return;
            }
            else if (addr < 0x3F00)
            {
                // Nametables (mirrored every 0x1000)
                vram[(addr - 0x2000) % 0x800] = value;
            }
            else if (addr < 0x4000)
            {
                // Palette RAM indexes (mirrored every 32 bytes)
                palette[(addr - 0x3F00) % 0x20] = value;
            }
        }
    }
}
