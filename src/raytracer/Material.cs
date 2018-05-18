using System.Numerics;
using System.Runtime.InteropServices;

namespace RayTracer
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Material
    {
        public Vector3 Albedo;
        public MaterialType Type;
        public float FuzzOrRefIndex;
        public float _padding0;
        public float _padding1;
        public float _padding2;

        public static Material Lambertian(Vector3 albedo)
        {
            Material m;
            m.Type = MaterialType.Lambertian;
            m.Albedo = albedo;
            m.FuzzOrRefIndex = 0;
            m._padding0 = m._padding1 = m._padding2 = 0;
            return m;
        }

        public static Material Metal(Vector3 albedo, float fuzz)
        {
            Material m;
            m.Type = MaterialType.Metal;
            m.Albedo = albedo;
            m.FuzzOrRefIndex = fuzz;
            m._padding0 = m._padding1 = m._padding2 = 0;
            return m;
        }

        public static Material Dielectric(float refIndex)
        {
            Material m;
            m.Type = MaterialType.Dielectric;
            m.Albedo = Vector3.Zero;
            m.FuzzOrRefIndex = refIndex;
            m._padding0 = m._padding1 = m._padding2 = 0;
            return m;
        }
    }
}
