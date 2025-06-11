using System.Drawing;

namespace CNES.Renderers
{
    public interface IRenderer
    {
        void Initialize(int width, int height);
        void RenderFrame(byte[] framebuffer);
        void Present();
        void Clear(Color clearColor);
    }
}
