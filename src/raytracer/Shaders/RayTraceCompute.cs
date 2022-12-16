using ShaderGen;
using System.Numerics;

[assembly: ComputeShaderSet("RayTraceCompute", "RayTracer.Shaders.RayTraceCompute.CS")]

namespace RayTracer.Shaders
{
    public class RayTraceCompute
    {
        public const uint MaxDepth = RayTracingApplication.MaxDepth;

        public StructuredBuffer<Sphere> Spheres;
        public StructuredBuffer<Material> Materials;
        public RWTexture2DResource<Vector4> Output;
        public SceneParams Params;
        public AtomicBufferUInt32 RayCount;

        [ComputeShader(16, 16, 1)]
        public void CS()
        {
            UInt3 dtid = ShaderBuiltins.DispatchThreadID;
            Vector4 color = Vector4.Zero;
            uint randState = (dtid.X * 1973 + dtid.Y * 9277 + Params.FrameCount * 26699) | 1;

            uint rayCount = 0;
            for (uint smp = 0; smp < RayTracingApplication.NumSamples; smp++)
            {
                float u = (dtid.X + RandUtil.RandomFloat(ref randState)) / RayTracingApplication.Width;
                float v = (dtid.Y + RandUtil.RandomFloat(ref randState)) / RayTracingApplication.Height;
                Ray ray = Camera.GetRay(Params.Camera, u, v, ref randState);
                color += Color(Params.SphereCount, ref randState, ray, ref rayCount);
            }
            color /= RayTracingApplication.NumSamples;
            ShaderBuiltins.Store(Output, new UInt2(dtid.X, dtid.Y), color);
            ShaderBuiltins.InterlockedAdd(RayCount, 0, rayCount);
        }

        // Cannot call static version in RayTracingApplication -- it's not possible to pass StructuredBuffers as parameters in GLSL.
        private Vector4 Color(uint sphereCount, ref uint randState, Ray ray, ref uint rayCount)
        {
            Vector3 color = Vector3.Zero;
            Vector3 currentAttenuation = Vector3.One; // Start at full strength

            for (int curDepth = 0; curDepth < MaxDepth; curDepth++)
            {
                rayCount += 1;
                RayHit hit;
                hit.Position = new Vector3();
                hit.Normal = new Vector3();
                hit.T = 0;
                float closest = 9999999f;
                bool hitAnything = false;
                uint hitID = 0;
                for (uint i = 0; i < sphereCount; i++)
                {
                    if (Sphere.Hit(Spheres[i], ray, 0.001f, closest, out RayHit tempHit))
                    {
                        hitAnything = true;
                        hit = tempHit;
                        hitID = i;
                        closest = hit.T;
                    }
                }

                if (hitAnything)
                {
                    if (RayTracingApplication.Scatter(ray, hit, Materials[hitID], ref randState, out Vector3 attenuation, out Ray scattered))
                    {
                        currentAttenuation *= attenuation;
                        ray = scattered;
                    }
                    else
                    {
                        color += currentAttenuation;
                        break;
                    }
                }
                else // Hit nothing -- sky
                {
                    Vector3 unitDir = Vector3.Normalize(ray.Direction);
                    float t = 0.5f * (unitDir.Y + 1f);
                    color += currentAttenuation * ((1f - t) * Vector3.One + t * new Vector3(0.5f, 0.7f, 1f));
                    break;
                }
            }

            return new Vector4(color, 1f);
        }
    }
}
