using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RayTracer
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Camera
    {
        public Vector3 Origin;
        private float _padding0;
        public Vector3 LowerLeftCorner;
        private float _padding1;
        public Vector3 Horizontal;
        private float _padding2;
        public Vector3 Vertical;
        private float _padding3;
        public Vector3 U;
        public float LensRadius;
        public Vector3 V;
        private float _padding4;
        public Vector3 W;
        private float _padding5;

        public static Camera Create(Vector3 origin, Vector3 lookAt, Vector3 up, float vfov, float aspect, float aperture, float focusDist)
        {
            Camera cam;
            cam.LensRadius = aperture / 2f;
            float theta = vfov * MathF.PI / 180f;
            float halfHeight = MathF.Tan(theta / 2f);
            float halfWidth = aspect * halfHeight;
            cam.Origin = origin;
            cam.W = Vector3.Normalize(origin - lookAt);
            cam.U = Vector3.Normalize(Vector3.Cross(up, cam.W));
            cam.V = Vector3.Cross(cam.W, cam.U);
            cam.LowerLeftCorner = cam.Origin - halfWidth * focusDist * cam.U - halfHeight * focusDist * cam.V - focusDist * cam.W;
            cam.Horizontal = 2 * halfWidth * focusDist * cam.U;
            cam.Vertical = 2 * halfHeight * focusDist * cam.V;

            cam._padding0 = cam._padding1 = cam._padding2 = cam._padding3 = cam._padding4 = cam._padding5 = 0;
            return cam;
        }

        public static Ray GetRay(Camera cam, float s, float t, ref uint state)
        {
            Vector3 rd = cam.LensRadius * RandUtil.RandomInUnitDisk(ref state);
            Vector3 offset = cam.U * rd.X + cam.V * rd.Y;
            return Ray.Create(
                cam.Origin + offset,
                cam.LowerLeftCorner + s * cam.Horizontal + t * cam.Vertical - cam.Origin - offset);
        }
    }
}
