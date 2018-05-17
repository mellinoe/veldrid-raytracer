using System.Numerics;
using System.Runtime.InteropServices;

namespace RayTracer
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Ray
    {
        public readonly Vector3 Origin;
        private readonly float _padding0;
        public readonly Vector3 Direction;
        private readonly float _padding1;

        public Ray(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            Direction = direction;
            _padding0 = 0;
            _padding1 = 0;
        }

        internal static Vector3 PointAt(in Ray ray, float t) => ray.Origin + ray.Direction * t;
    }
}
