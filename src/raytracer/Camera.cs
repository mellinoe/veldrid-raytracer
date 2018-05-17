using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RayTracer
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Camera
    {
        public readonly Vector3 Origin;
        private readonly float _padding0;
        public readonly Vector3 LowerLeftCorner;
        private readonly float _padding1;
        public readonly Vector3 Horizontal;
        private readonly float _padding2;
        public readonly Vector3 Vertical;
        private readonly float _padding3;
        public readonly Vector3 U;
        public readonly float LensRadius;
        public readonly Vector3 V;
        private readonly float _padding4;
        public readonly Vector3 W;
        private readonly float _padding5;

        public Camera(Vector3 origin, Vector3 lookAt, Vector3 up, float vfov, float aspect, float aperture, float focusDist)
        {
            LensRadius = aperture / 2f;
            float theta = vfov * MathF.PI / 180f;
            float halfHeight = MathF.Tan(theta / 2f);
            float halfWidth = aspect * halfHeight;
            Origin = origin;
            W = Vector3.Normalize(origin - lookAt);
            U = Vector3.Normalize(Vector3.Cross(up, W));
            V = Vector3.Cross(W, U);
            LowerLeftCorner = Origin - halfWidth * focusDist * U - halfHeight * focusDist * V - focusDist * W;
            Horizontal = 2 * halfWidth * focusDist * U;
            Vertical = 2 * halfHeight * focusDist * V;

            _padding0 = _padding1 = _padding2 = _padding3 = _padding4 = _padding5 = 0;
        }

        public static Ray GetRay(in Camera cam, float s, float t, ref uint state)
        {
            Vector3 rd = cam.LensRadius * RandUtil.RandomInUnitDisk(ref state);
            Vector3 offset = cam.U * rd.X + cam.V * rd.Y;
            return new Ray(
                cam.Origin + offset,
                cam.LowerLeftCorner + s * cam.Horizontal + t * cam.Vertical - cam.Origin - offset);
        }
    }
}
