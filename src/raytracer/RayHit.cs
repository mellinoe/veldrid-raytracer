using System.Numerics;
using System.Runtime.InteropServices;

namespace RayTracer
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct RayHit
    {
        public readonly Vector3 Position;
        public readonly float T;
        public readonly Vector3 Normal;

        public RayHit(Vector3 position, float t, Vector3 normal)
        {
            Position = position;
            T = t;
            Normal = normal;
        }
    }
}