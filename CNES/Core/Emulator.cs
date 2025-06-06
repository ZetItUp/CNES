using System.Drawing;
using CNES.Renderers;

namespace CNES.Core
{
    public class Emulator
    {
        private IRenderer renderer;
        private int screenWidth = 256;
        private int screenHeight = 240;

        public Emulator(IRenderer renderer) 
        { 
            this.renderer = renderer;
            renderer.Initialize(screenWidth, screenHeight);
        }

        public void Run()
        {
            Color[] framebuffer = new Color[screenWidth * screenHeight];

            for(int y = 0; y < screenHeight; y++)
            {
                for(int x = 0; x < screenWidth; x++)
                {
                    int pixel = y * screenWidth + x;
                    framebuffer[pixel] = Color.FromArgb(x % 256, y % 256, (x + y) % 256);
                }
            }

            renderer.Clear(Color.Black);
            renderer.RenderFrame(framebuffer);
            renderer.Present();
        }
    }
}
