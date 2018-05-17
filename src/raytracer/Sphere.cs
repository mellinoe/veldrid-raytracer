using System;
using System.Numerics;

namespace RayTracer
{
    public readonly struct Sphere
    {
        public readonly Vector3 Center;
        public readonly float Radius;

        public Sphere(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public static bool Hit(in Sphere sphere, in Ray ray, float tMin, float tMax, out RayHit hit)
        {
            Vector3 oc = ray.Origin - sphere.Center;
            float a = Vector3.Dot(ray.Direction, ray.Direction);
            float b = Vector3.Dot(oc, ray.Direction);
            float c = Vector3.Dot(oc, oc) - sphere.Radius * sphere.Radius;
            float discriminant = b * b - a * c;
            if (discriminant > 0)
            {
                float tmp = MathF.Sqrt(b * b - a * c);
                float t = (-b - tmp) / a;
                if (t < tMax && t > tMin)
                {
                    Vector3 position = Ray.PointAt(ray, t);
                    Vector3 normal = (position - sphere.Center) / sphere.Radius;
                    hit = new RayHit(Ray.PointAt(ray, t), t, normal);
                    return true;
                }
                t = (-b + tmp) / a;
                if (t < tMax && t > tMin)
                {
                    Vector3 position = Ray.PointAt(ray, t);
                    Vector3 normal = (position - sphere.Center) / sphere.Radius;
                    hit = new RayHit(position, t, normal);
                    return true;
                }
            }

            hit = default;
            return false;
        }
    }
}