using System.Drawing;

namespace CNES.Renderers
{
    public interface IRenderer
    {
        void Initialize(int width, int height);
        void RenderFrame(Color[] framebuffer);
        void Clear(Color clearColor);
    }
}
