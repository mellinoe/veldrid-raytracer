using System.Numerics;
using System.Runtime.InteropServices;

namespace RayTracer
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Ray
    {
        public Vector3 Origin;
        private float _padding0;
        public Vector3 Direction;
        private float _padding1;

        public static Ray Create(Vector3 origin, Vector3 direction)
        {
            Ray r;
            r.Origin = origin;
            r.Direction = direction;
            r._padding0 = 0;
            r._padding1 = 0;
            return r;
        }

        internal static Vector3 PointAt(Ray ray, float t) => ray.Origin + ray.Direction * t;
    }
}
