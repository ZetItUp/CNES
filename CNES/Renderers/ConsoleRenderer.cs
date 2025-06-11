using System;
using System.Drawing;
using CNES.Utils;

namespace CNES.Renderers
{
    public class ConsoleRenderer : IRenderer
    {
        private bool UseAnsi = true;
        private int width;
        private int height;
        private Color[] currentBuffer;

        public ConsoleRenderer() 
        { 

        }

        public void Clear(Color clearColor)
        {
            Console.Clear();
        }

        public void Initialize(int width, int height)
        {
            this.width = width;
            this.height = height;
            currentBuffer = new Color[width * height];
            Console.Clear();
        }

        public void RenderFrame(byte[] framebuffer)
        {
            for (int i = 0; i < framebuffer.Length; i++)
            {
                byte index = framebuffer[i];
                currentBuffer[i] = NESPalette.Colors[index % 64]; // Safety check
            }
        }

        public void Present()
        {
            Console.SetCursorPosition(0, 0);

            for (int y = 0; y < height; y += 2)
            {
                for (int x = 0; x < width; x++)
                {
                    var color = currentBuffer[y * width + x];

                    if (UseAnsi)
                    {
                        Console.Write(GetAnsiColorBlock(color));
                    }
                    else
                    {
                        Console.Write(IsBright(color) ? "#" : " ");
                    }
                }

                Console.WriteLine();
            }
        }

        private bool IsBright(Color c)
        {
            return c.R + c.G + c.B > 128 * 3;
        }

        private string GetAnsiColorBlock(Color color)
        {
            // ANSI 24-bit foreground color + Unicode full block
            return $"\x1b[38;2;{color.R};{color.G};{color.B}m█\x1b[0m";
        }
    }
}
