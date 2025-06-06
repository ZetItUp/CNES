using System;
using System.Drawing;

namespace CNES.Renderers
{
    public class ConsoleRenderer : IRenderer
    {
        private int width;
        private int height;

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
            Console.Clear();
        }

        public void RenderFrame(Color[] framebuffer)
        {
            Console.SetCursorPosition(0, 0);

            for(int y = 0; y < height; y++)
            {
                for(int x = 0; x < width; x++)
                {
                    var color = framebuffer[y * width + x];
                    Console.Write(IsBright(color) ? "#" : " ");
                }

                Console.WriteLine();
            }
        }

        public void Present()
        {

        }

        private bool IsBright(Color c)
        {
            return c.R + c.G + c.B > 128 * 3;
        }
    }
}
