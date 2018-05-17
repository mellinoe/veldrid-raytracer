using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RayTracer
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Material
    {
        public readonly Vector3 Albedo;
        public readonly MaterialType Type;
        public readonly float FuzzOrRefIndex;
        private readonly float _padding0;
        private readonly float _padding1;
        private readonly float _padding2;

        private Material(MaterialType type, Vector3 albedo, float fuzzOrRefIndex)
        {
            Albedo = albedo;
            Type = type;
            FuzzOrRefIndex = fuzzOrRefIndex;

            _padding0 = _padding1 = _padding2 = 0;
        }

        public static Material Lambertian(Vector3 albedo) => new Material(MaterialType.Lambertian, albedo, 0f);
        public static Material Metal(Vector3 albedo, float fuzz) => new Material(MaterialType.Metal, albedo, MathF.Min(1, fuzz));
        public static Material Dielectric(float refIndex) => new Material(MaterialType.Dielectric, Vector3.Zero, refIndex);
    }
}
