using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RayTracer
{
    public class CameraManager
    {
        public float LensRadius;
        public float VFov;
        public float Aspect;
        public float Aperture;
        public float FocusDist;

        private float _pitch;
        private float _yaw = -MathF.PI / 2f;

        public float Yaw
        {
            get { return MathHelper.RadiansToDegrees(_yaw); }
            set { _yaw = MathHelper.DegreesToRadians(value); }
        }
        public float Pitch
        {
            get { return MathHelper.RadiansToDegrees(_pitch); }
            set { _pitch = MathHelper.DegreesToRadians(Math.Clamp(value, -89f, 89f)); }
        }

        public Vector3 Origin;
        public Vector3 LookAt;
        public Vector3 Up;

        public Camera Camera;

        public CameraManager(Vector3 origin, Vector3 lookAt, Vector3 up, float vfov, float aspect, float aperture, float focusDist)
        { 
            Camera = Camera.Create(origin, lookAt, up, vfov, aspect, aperture, focusDist);

            VFov = vfov;
            Aspect = aspect;
            Aperture = aperture;
            FocusDist = focusDist;
            Origin = origin;
            LookAt = lookAt;
            Up = up;
        }

        public void ChangePosition(bool[] directionKeys, float frameTime)
        {
            if (directionKeys[0] || directionKeys[1] || directionKeys[2] || directionKeys[3])
            {
                Vector3 movement = new Vector3(0, 0, 0);
                Vector3 unitVector = Origin - LookAt;
                unitVector.Y = 0;
                unitVector = Vector3.Normalize(unitVector);

                if (directionKeys[0])
                {
                    movement -= unitVector;
                }
                if (directionKeys[1])
                {
                    movement += Vector3.Normalize(Vector3.Cross(unitVector, Vector3.UnitY));
                }
                if (directionKeys[2])
                {
                    movement += unitVector;
                }
                if (directionKeys[3])
                {
                    movement -= Vector3.Normalize(Vector3.Cross(unitVector, Vector3.UnitY));
                }

                movement *= frameTime;

                updateCamera(Origin + movement, LookAt + movement, Up);
            }
        }

        public void ChangeViewAngle()
        {
            LookAt.X = MathF.Cos(_pitch) * MathF.Cos(_yaw);
            LookAt.Y = MathF.Sin(_pitch);
            LookAt.Z = MathF.Cos(_pitch) * MathF.Sin(_yaw);

            LookAt = -Vector3.Normalize(LookAt);

            updateCamera(Origin, Origin + LookAt, Up);
        }

        private void updateCamera(Vector3 origin, Vector3 lookAt, Vector3 up)
        {
            Camera.LensRadius = Aperture / 2f;
            float theta = VFov * MathF.PI / 180f;
            float halfHeight = MathF.Tan(theta / 2f);
            float halfWidth = Aspect * halfHeight;
            Camera.Origin = origin;
            Camera.W = Vector3.Normalize(origin - lookAt);
            Camera.U = Vector3.Normalize(Vector3.Cross(up, Camera.W));
            Camera.V = Vector3.Cross(Camera.W, Camera.U);
            Camera.LowerLeftCorner = Camera.Origin - halfWidth * FocusDist * Camera.U - halfHeight * FocusDist * Camera.V - FocusDist * Camera.W;
            Camera.Horizontal = 2 * halfWidth * FocusDist * Camera.U;
            Camera.Vertical = 2 * halfHeight * FocusDist * Camera.V;

            Origin = origin;
            LookAt = lookAt; 
            Up = up;
        }
    }

   
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
