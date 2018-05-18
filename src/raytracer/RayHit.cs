using System.Numerics;
using System.Runtime.InteropServices;

namespace RayTracer
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RayHit
    {
        public Vector3 Position;
        public float T;
        public Vector3 Normal;

        public static RayHit Create(Vector3 position, float t, Vector3 normal)
        {
            RayHit hit;
            hit.Position = position;
            hit.T = t;
            hit.Normal = normal;
            return hit;
        }
    }
}