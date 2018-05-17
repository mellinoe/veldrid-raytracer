using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace RayTracer
{
    internal unsafe class RayTracingApplication
    {
        public const uint Width = 400;
        public const uint Height = 250;
        public const uint ViewScale = 4;
        public const uint NumSamples = 25;

        private Sdl2Window _window;
        private GraphicsDevice _gd;
        private CommandList _cl;
        private Texture _transferTex;
        private TextureView _texView;
        private RgbaFloat[] _fb;
        private ResourceSet _graphicsSet;
        private Pipeline _graphicsPipeline;

        private Sphere[] _spheres;
        private Material[] _materials;

        private Camera _camera;
        private uint _randState;
        public void Run()
        {
            VeldridStartup.CreateWindowAndGraphicsDevice(
                new WindowCreateInfo(100, 100, (int)(Width * ViewScale), (int)(Height * ViewScale), WindowState.Normal, "Veldrid Ray Tracer"),
                new GraphicsDeviceOptions(debug: false, swapchainDepthFormat: null, syncToVerticalBlank: true),
                GraphicsBackend.Direct3D11,
                out _window,
                out _gd);
            _window.Resized += () => _gd.ResizeMainWindow((uint)_window.Width, (uint)_window.Height);
            CreateDeviceResources();

            _randState = (uint)new Random().Next();
            CreateScene();

            _fb = new RgbaFloat[Width * Height];

            Vector3 camPos = new Vector3(0, 1, 10);
            Vector3 lookAt = new Vector3(0, 0, -1);
            float distToFocus = (camPos - lookAt).Length();
            float aperture = 0.01f;
            _camera = new Camera(
                camPos,
                lookAt,
                Vector3.UnitY,
                45f,
                (float)Width / Height,
                aperture,
                distToFocus);

            _randState = (uint)new Random().Next();
            while (_window.Exists)
            {
                _window.PumpEvents();
                if (!_window.Exists) { break; }
                RenderFrame();
            }

            _gd.Dispose();
        }

        private void CreateScene()
        {
            int n = 500;
            _spheres = new Sphere[n + 1];
            _materials = new Material[n + 1];
            _spheres[0] = new Sphere(new Vector3(0, -1000, 0), 1000);
            _materials[0] = Material.Lambertian(new Vector3(0.5f, 0.5f, 0.5f));

            int i = 1;

            for (int a = -11; a < 11; a++)
                for (int b = -11; b < 11; b++)
                {
                    float chooseMaterial = RandomFloat();
                    Vector3 center = new Vector3(a + 0.9f * RandomFloat(), 0.2f, b + 0.9f * RandomFloat());
                    _spheres[i] = new Sphere(center, 0.2f);
                    if ((center - new Vector3(4, 0.2f, 0)).Length() > 0.9f)
                    {
                        if (chooseMaterial < 0.8f)
                        {
                            _materials[i] = Material.Lambertian(
                                new Vector3(
                                    RandomFloat() * RandomFloat(),
                                    RandomFloat() * RandomFloat(),
                                    RandomFloat() * RandomFloat()));
                        }
                        else if (chooseMaterial < 0.95f)
                        {
                            _materials[i] = Material.Metal(
                                new Vector3(
                                    0.5f * (1 + RandomFloat()),
                                    0.5f * (1 + RandomFloat()),
                                    0.5f * (1 + RandomFloat())),
                                0.5f * (1 + RandomFloat()));
                        }
                        else
                        {
                            _materials[i] = Material.Dielectric(1.5f);
                        }
                    }

                    i += 1;
                }

            _spheres[i] = new Sphere(new Vector3(0, 1, 0), 1);
            _materials[i] = Material.Dielectric(1.5f);

            i += 1;
            _spheres[i] = new Sphere(new Vector3(-4, 1, 0), 1);
            _materials[i] = Material.Lambertian(new Vector3(0.4f, 0.2f, 0.1f));

            i += 1;
            _spheres[i] = new Sphere(new Vector3(4, 1, 0), 1);
            _materials[i] = Material.Metal(new Vector3(0.7f, 0.6f, 0.5f), 0f);

            //_spheres = new[]
            //{
            //    new Sphere(new Vector3(0, 0, -1), 0.5f),
            //    new Sphere(new Vector3(0, -100.5f, 0), 100f),
            //    new Sphere(new Vector3(1, 0, -1), 0.5f),
            //    new Sphere(new Vector3(-1, 0, -1), 0.5f),
            //    new Sphere(new Vector3(-1, 0, -1), -0.45f)
            //};

            //_materials = new[]
            //{
            //    Material.Lambertian(new Vector3(0.1f, 0.2f, 0.5f)),
            //    Material.Lambertian(new Vector3(0.8f, 0.8f, 0f)),
            //    Material.Metal(new Vector3(0.8f, 0.8f, 0f), 0.2f),
            //    Material.Dielectric(1.5f),
            //    Material.Dielectric(1.5f)
            //};
        }

        private void RenderFrame()
        {
            Vector3 lowerLeft = new Vector3(-2f, -1f, -1f);
            Vector3 horizontal = new Vector3(4f, 0f, 0f);
            Vector3 vertical = new Vector3(0f, 4f * ((float)Height / Width), 0f);
            Vector3 origin = Vector3.Zero;
            Parallel.For(0, Height, y =>
            {
                //Parallel.For(0, Width, x =>
                //for (uint y = 0; y < Height; y++)
                for (uint x = 0; x < Width; x++)
                {
                    Vector4 color = Vector4.Zero;
                    for (uint sample = 0; sample < NumSamples; sample++)
                    {
                        float u = (x + RandomFloat()) / Width;
                        float v = (y + RandomFloat()) / Height;
                        Ray ray = Camera.GetRay(_camera, u, v, ref _randState);
                        color += Color(ray, 0);
                    }
                    color /= NumSamples;
                    _fb[y * Width + x] = new RgbaFloat(color.X, color.Y, color.Z, color.W);
                }
                //});
            });
            //}

            fixed (RgbaFloat* pixelDataPtr = _fb)
            {
                _gd.UpdateTexture(_transferTex, (IntPtr)pixelDataPtr, Width * Height * (uint)sizeof(RgbaFloat), 0, 0, 0, Width, Height, 1, 0, 0);
            }

            _cl.Begin();
            _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
            _cl.SetPipeline(_graphicsPipeline);
            _cl.SetGraphicsResourceSet(0, _graphicsSet);
            _cl.Draw(3);
            _cl.End();
            _gd.SubmitCommands(_cl);
            _gd.SwapBuffers();
        }

        private Vector4 Color(in Ray ray, int depth)
        {
            RayHit hit = default;
            float closest = 9999999f;
            bool hitAnything = false;
            int hitID = 0;
            for (int i = 0; i < _spheres.Length; i++)
            {
                if (Sphere.Hit(_spheres[i], ray, 0.001f, closest, out RayHit tempHit))
                {
                    hitAnything = true;
                    hit = tempHit;
                    hitID = i;
                    closest = hit.T;
                }
            }

            if (hitAnything)
            {
                if (depth < 50 && Scatter(ray, hit, _materials[hitID], out Vector3 attenuation, out Ray scattered))
                {
                    return new Vector4(attenuation, 1f) * Color(scattered, depth + 1);
                }
                else
                {
                    return Vector4.Zero;
                }
            }
            else
            {
                Vector3 unitDir = Vector3.Normalize(ray.Direction);
                float t = 0.5f * (unitDir.Y + 1f);
                return (1f - t) * Vector4.One + t * new Vector4(0.5f, 0.7f, 1f, 1f);
            }
        }

        private Vector3 RandomInUnitSphere() => RandUtil.RandomInUnitSphere(ref _randState);

        private float RandomFloat() => RandUtil.RandomFloat(ref _randState);

        public bool Scatter(in Ray ray, in RayHit hit, in Material material, out Vector3 attenuation, out Ray scattered)
        {
            switch (material.Type)
            {
                case MaterialType.Lambertian:
                {
                    Vector3 target = hit.Position + hit.Normal + RandomInUnitSphere();
                    scattered = new Ray(hit.Position, target - hit.Position);
                    attenuation = material.Albedo;
                    return true;
                }
                case MaterialType.Metal:
                {
                    Vector3 reflected = Vector3.Reflect(Vector3.Normalize(ray.Direction), hit.Normal);
                    scattered = new Ray(hit.Position, reflected + material.FuzzOrRefIndex * RandomInUnitSphere());
                    attenuation = material.Albedo;
                    return Vector3.Dot(scattered.Direction, hit.Normal) > 0;
                }
                case MaterialType.Dielectric:
                {
                    Vector3 outwardNormal;
                    Vector3 reflectDir = Vector3.Reflect(ray.Direction, hit.Normal);
                    float niOverNt;
                    attenuation = new Vector3(1, 1, 1);
                    Vector3 refractDir;
                    float reflectProb;
                    float cosine;
                    if (Vector3.Dot(ray.Direction, hit.Normal) > 0)
                    {
                        outwardNormal = -hit.Normal;
                        niOverNt = material.FuzzOrRefIndex;
                        cosine = material.FuzzOrRefIndex * Vector3.Dot(ray.Direction, hit.Normal) / ray.Direction.Length();
                    }
                    else
                    {
                        outwardNormal = hit.Normal;
                        niOverNt = 1f / material.FuzzOrRefIndex;
                        cosine = -Vector3.Dot(ray.Direction, hit.Normal) / ray.Direction.Length();
                    }
                    if (Refract(ray.Direction, outwardNormal, niOverNt, out refractDir))
                    {
                        reflectProb = Schlick(cosine, material.FuzzOrRefIndex);
                    }
                    else
                    {
                        reflectProb = 1f;
                    }
                    if (RandomFloat() < reflectProb)
                    {
                        scattered = new Ray(hit.Position, reflectDir);
                    }
                    else
                    {
                        scattered = new Ray(hit.Position, refractDir);
                    }

                    return true;
                }

                default: throw new InvalidOperationException();
            }
        }

        public static bool Refract(in Vector3 v, in Vector3 n, float niOverNt, out Vector3 refracted)
        {
            Vector3 uv = Vector3.Normalize(v);
            float dt = Vector3.Dot(uv, n);
            float discriminant = 1f - niOverNt * niOverNt * (1 - dt * dt);
            if (discriminant > 0)
            {
                refracted = niOverNt * (uv - n * dt) - n * MathF.Sqrt(discriminant);
                return true;
            }
            else
            {
                refracted = Vector3.Zero;
                return false;
            }
        }

        public static float Schlick(float cosine, float refIndex)
        {
            float r0 = (1 - refIndex) / (1 + refIndex);
            r0 = r0 * r0;
            return r0 + (1 - r0) * MathF.Pow(1 - cosine, 5);
        }

        // Create Veldrid resources
        private void CreateDeviceResources()
        {
            ResourceFactory factory = _gd.ResourceFactory;
            _cl = factory.CreateCommandList();
            _transferTex = factory.CreateTexture(
                TextureDescription.Texture2D(Width, Height, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Sampled));
            _texView = factory.CreateTextureView(_transferTex);

            ResourceLayout graphicsLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("SourceTex", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("SourceSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            _graphicsSet = factory.CreateResourceSet(new ResourceSetDescription(graphicsLayout, _texView, _gd.LinearSampler));

            _graphicsPipeline = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.CullNone,
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(
                    Array.Empty<VertexLayoutDescription>(),
                    new[]
                    {
                        factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, LoadShaderBytes("FramebufferBlitter-vertex"), "main")),
                        factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, LoadShaderBytes("FramebufferBlitter-fragment"), "main"))
                    }),
                graphicsLayout,
                _gd.MainSwapchain.Framebuffer.OutputDescription));
        }

        private byte[] LoadShaderBytes(string name)
        {
            string extension;
            switch (_gd.BackendType)
            {
                case GraphicsBackend.Direct3D11:
                    extension = "hlsl.bytes";
                    break;
                case GraphicsBackend.Vulkan:
                    extension = "450.glsl.spv";
                    break;
                case GraphicsBackend.OpenGL:
                    extension = "330.glsl";
                    break;
                case GraphicsBackend.Metal:
                    extension = "macos.metallib";
                    break;
                case GraphicsBackend.OpenGLES:
                    extension = "300.glsles";
                    break;
                default: throw new InvalidOperationException();
            }

            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", $"{name}.{extension}"));
        }
    }
}