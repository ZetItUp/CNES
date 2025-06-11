using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CNES.Utils
{
    public static class NESPalette
    {
        // All NES colors are represented as ARGB values.
        public static readonly Color[] Colors = new Color[64]
        {
            Color.FromArgb(84, 84, 84),      // 0x00
            Color.FromArgb(0, 30, 116),      // 0x01
            Color.FromArgb(8, 16, 144),      // 0x02
            Color.FromArgb(48, 0, 136),      // 0x03
            Color.FromArgb(68, 0, 100),      // 0x04
            Color.FromArgb(92, 0, 48),       // 0x05
            Color.FromArgb(84, 4, 0),        // 0x06
            Color.FromArgb(60, 24, 0),       // 0x07
            Color.FromArgb(32, 42, 0),       // 0x08
            Color.FromArgb(8, 58, 0),        // 0x09
            Color.FromArgb(0, 64, 0),        // 0x0A
            Color.FromArgb(0, 60, 0),        // 0x0B
            Color.FromArgb(0, 50, 60),       // 0x0C
            Color.FromArgb(0, 0, 0),         // 0x0D
            Color.FromArgb(0, 0, 0),         // 0x0E
            Color.FromArgb(0, 0, 0),         // 0x0F

            Color.FromArgb(152, 150, 152),   // 0x10
            Color.FromArgb(8, 76, 196),      // 0x11
            Color.FromArgb(48, 50, 236),     // 0x12
            Color.FromArgb(92, 30, 228),     // 0x13
            Color.FromArgb(136, 20, 176),    // 0x14
            Color.FromArgb(160, 20, 100),    // 0x15
            Color.FromArgb(152, 34, 32),     // 0x16
            Color.FromArgb(120, 60, 0),      // 0x17
            Color.FromArgb(84, 90, 0),       // 0x18
            Color.FromArgb(40, 114, 0),      // 0x19
            Color.FromArgb(8, 124, 0),       // 0x1A
            Color.FromArgb(0, 118, 40),      // 0x1B
            Color.FromArgb(0, 102, 120),     // 0x1C
            Color.FromArgb(0, 0, 0),         // 0x1D
            Color.FromArgb(0, 0, 0),         // 0x1E
            Color.FromArgb(0, 0, 0),         // 0x1F
            
            Color.FromArgb(236, 238, 236),   // 0x20
            Color.FromArgb(76, 154, 236),    // 0x21
            Color.FromArgb(120, 124, 236),   // 0x22
            Color.FromArgb(176, 98, 236),    // 0x23
            Color.FromArgb(228, 84, 236),    // 0x24
            Color.FromArgb(236, 88, 180),    // 0x25
            Color.FromArgb(236, 106, 100),   // 0x26
            Color.FromArgb(212, 136, 32),    // 0x27
            Color.FromArgb(160, 170, 0),     // 0x28
            Color.FromArgb(116, 196, 0),     // 0x29
            Color.FromArgb(76, 208, 32),     // 0x2A
            Color.FromArgb(56, 204, 108),    // 0x2B
            Color.FromArgb(56, 180, 204),    // 0x2C
            Color.FromArgb(60, 60, 60),      // 0x2D
            Color.FromArgb(0, 0, 0),         // 0x2E
            Color.FromArgb(0, 0, 0),         // 0x2F

            Color.FromArgb(236, 238, 236),   // 0x30
            Color.FromArgb(168, 204, 236),   // 0x31
            Color.FromArgb(188, 188, 236),   // 0x32
            Color.FromArgb(212, 178, 236),   // 0x33
            Color.FromArgb(236, 174, 236),   // 0x34
            Color.FromArgb(236, 174, 212),   // 0x35
            Color.FromArgb(236, 180, 176),   // 0x36
            Color.FromArgb(228, 196, 144),   // 0x37
            Color.FromArgb(204, 210, 120),   // 0x38
            Color.FromArgb(180, 222, 120),   // 0x39
            Color.FromArgb(168, 226, 144),   // 0x3A
            Color.FromArgb(152, 226, 180),   // 0x3B
            Color.FromArgb(160, 214, 228),   // 0x3C
            Color.FromArgb(160, 162, 160),   // 0x3D
            Color.FromArgb(0, 0, 0),         // 0x3E
            Color.FromArgb(0, 0, 0)          // 0x3F
        };
    }
}
