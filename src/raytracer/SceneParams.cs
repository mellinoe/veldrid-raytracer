using System.Runtime.InteropServices;

namespace RayTracer
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SceneParams
    {
        public Camera Camera;
        public uint SphereCount;
        public uint FrameCount;
        private uint _padding0;
        private uint _padding1;
    }
}