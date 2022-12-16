using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace RayTracer
{
    internal unsafe class RayTracingApplication
    {
        public const uint Width = 1280;
        public const uint Height = 720;
        public const uint ViewScale = 1;
        public const uint NumSamples = 16;
        public const uint MaxDepth = 50;
        public const float Epsilon = 0.0005f;

        private Sdl2Window _window;
        private GraphicsDevice _gd;
        private CommandList _cl;
        private Texture _transferTex;
        private TextureView _texView;
        private RgbaFloat[] _fb;
        private ResourceSet _graphicsSet;
        private Pipeline _graphicsPipeline;
        private DeviceBuffer _spheresBuffer;
        private DeviceBuffer _materialsBuffer;
        private DeviceBuffer _sceneParamsBuffer;
        private DeviceBuffer _rayCountBuffer;
        private DeviceBuffer _rayCountReadback;
        private Sphere[] _spheres;
        private Material[] _materials;
        private SceneParams _sceneParams;

        private uint _randState;
        private Stopwatch _stopwatch;
        private ResourceSet _computeSet;
        private Pipeline _computePipeline;
        private ulong _totalRays = 0;
        private bool _drawModeCPU = false;

        public void Run()
        {
            GraphicsBackend backend = GraphicsBackend.OpenGL;//VeldridStartup.GetPlatformDefaultBackend();

            VeldridStartup.CreateWindowAndGraphicsDevice(
                new WindowCreateInfo(100, 100, (int)(Width * ViewScale), (int)(Height * ViewScale), WindowState.Normal, "Veldrid Ray Tracer"),
                new GraphicsDeviceOptions(debug: false, swapchainDepthFormat: null, syncToVerticalBlank: false),
                backend,
                out _window,
                out _gd);
            _window.Resized += () => _gd.ResizeMainWindow((uint)_window.Width, (uint)_window.Height);

            _randState = (uint)new Random().Next();

            CreateBookScene(ref _randState);
            //CreateToyPathTracerScene();

            Debug.Assert(_spheres.Length == _materials.Length);
            _sceneParams.SphereCount = (uint)_spheres.Length;

            CreateDeviceResources();

            _fb = new RgbaFloat[Width * Height];

            _randState = (uint)new Random().Next();
            _stopwatch = Stopwatch.StartNew();
            while (_window.Exists)
            {
                _window.PumpEvents();
                if (!_window.Exists) { break; }
                RenderFrame();
            }

            _gd.Dispose();
        }

        private void CreateBookScene(ref uint state)
        {
            Vector3 camPos = new Vector3(9.5f, 2f, 2.5f);
            Vector3 lookAt = new Vector3(3, 0.5f, 0.65f);
            float distToFocus = (camPos - lookAt).Length();
            float aperture = 0.01f;
            _sceneParams.Camera = Camera.Create(
                camPos,
                lookAt,
                Vector3.UnitY,
                25f,
                (float)Width / Height,
                aperture,
                distToFocus);

            List<Sphere> spheres = new List<Sphere>();
            List<Material> materials = new List<Material>();
            spheres.Add(Sphere.Create(new Vector3(0, -1000, 0), 1000));
            materials.Add(Material.Lambertian(new Vector3(0.5f, 0.5f, 0.5f)));

            int dimension = 5;
            for (int a = -dimension; a < dimension; a++)
                for (int b = -dimension; b < dimension; b++)
                {
                    float chooseMaterial = RandUtil.RandomFloat(ref state);
                    Vector3 center = new Vector3(a + 0.9f * RandUtil.RandomFloat(ref state), 0.15f, b + 0.9f * RandUtil.RandomFloat(ref state));
                    if ((center - new Vector3(4, 0.2f, 0)).Length() > 0.9f)
                    {
                        float randOffset = RandUtil.RandomFloat(ref state) * 0.15f;
                        spheres.Add(Sphere.Create(center + Vector3.UnitY * randOffset, 0.15f + randOffset));
                        if (chooseMaterial < 0.8f)
                        {
                            materials.Add(Material.Lambertian(
                                new Vector3(
                                    RandUtil.RandomFloat(ref state) * RandUtil.RandomFloat(ref state),
                                    RandUtil.RandomFloat(ref state) * RandUtil.RandomFloat(ref state),
                                    RandUtil.RandomFloat(ref state) * RandUtil.RandomFloat(ref state))));
                        }
                        else if (chooseMaterial < 0.95f)
                        {
                            materials.Add(Material.Metal(
                                new Vector3(
                                    0.5f * (1 + RandUtil.RandomFloat(ref state)),
                                    0.5f * (1 + RandUtil.RandomFloat(ref state)),
                                    0.5f * (1 + RandUtil.RandomFloat(ref state))),
                                0.5f * (1 + RandUtil.RandomFloat(ref state))));
                        }
                        else
                        {
                            materials.Add(Material.Dielectric(1.5f));
                        }
                    }

                    Debug.Assert(spheres.Count == materials.Count);
                }

            spheres.Add(Sphere.Create(new Vector3(0, 1, 0), 1));
            materials.Add(Material.Dielectric(1.5f));

            spheres.Add(Sphere.Create(new Vector3(-4, 1, 0), 1));
            materials.Add(Material.Lambertian(new Vector3(0.4f, 0.2f, 0.1f)));

            spheres.Add(Sphere.Create(new Vector3(4, 1, 0), 1));
            materials.Add(Material.Metal(new Vector3(0.7f, 0.6f, 0.5f), 0f));

            _spheres = spheres.ToArray();
            _materials = materials.ToArray();
        }

        private void CreateToyPathTracerScene()
        {
            // This is the scene used in the "ToyPathTracer" project by Aras Pranckevičius
            // https://github.com/aras-p/ToyPathTracer

            Vector3 lookfrom = new Vector3(0, 2, 3);
            Vector3 lookat = new Vector3(0, 0, 0);
            float distToFocus = 3;
            float aperture = 0.1f;
            aperture *= 0.2f;

            _sceneParams.Camera = Camera.Create(
                lookfrom,
                lookat,
                Vector3.UnitY,
                60,
                (float)Width / Height,
                aperture,
                distToFocus);

            _spheres = new[]
            {
                Sphere.Create(new Vector3(0,-100.5f,-1), 100),
                Sphere.Create(new Vector3(2,0,-1), 0.5f),
                Sphere.Create(new Vector3(0,0,-1), 0.5f),
                Sphere.Create(new Vector3(-2,0,-1), 0.5f),
                Sphere.Create(new Vector3(2,0,1), 0.5f),
                Sphere.Create(new Vector3(0,0,1), 0.5f),
                Sphere.Create(new Vector3(-2,0,1), 0.5f),
                Sphere.Create(new Vector3(0.5f,1,0.5f), 0.5f),
                Sphere.Create(new Vector3(-1.5f,1.5f,0f), 0.3f),
                Sphere.Create(new Vector3(4,0,-3), 0.5f), Sphere.Create(new Vector3(3,0,-3), 0.5f), Sphere.Create(new Vector3(2,0,-3), 0.5f), Sphere.Create(new Vector3(1,0,-3), 0.5f), Sphere.Create(new Vector3(0,0,-3), 0.5f), Sphere.Create(new Vector3(-1,0,-3), 0.5f), Sphere.Create(new Vector3(-2,0,-3), 0.5f), Sphere.Create(new Vector3(-3,0,-3), 0.5f), Sphere.Create(new Vector3(-4,0,-3), 0.5f),
                Sphere.Create(new Vector3(4,0,-4), 0.5f), Sphere.Create(new Vector3(3,0,-4), 0.5f), Sphere.Create(new Vector3(2,0,-4), 0.5f), Sphere.Create(new Vector3(1,0,-4), 0.5f), Sphere.Create(new Vector3(0,0,-4), 0.5f), Sphere.Create(new Vector3(-1,0,-4), 0.5f), Sphere.Create(new Vector3(-2,0,-4), 0.5f), Sphere.Create(new Vector3(-3,0,-4), 0.5f), Sphere.Create(new Vector3(-4,0,-4), 0.5f),
                Sphere.Create(new Vector3(4,0,-5), 0.5f), Sphere.Create(new Vector3(3,0,-5), 0.5f), Sphere.Create(new Vector3(2,0,-5), 0.5f), Sphere.Create(new Vector3(1,0,-5), 0.5f), Sphere.Create(new Vector3(0,0,-5), 0.5f), Sphere.Create(new Vector3(-1,0,-5), 0.5f), Sphere.Create(new Vector3(-2,0,-5), 0.5f), Sphere.Create(new Vector3(-3,0,-5), 0.5f), Sphere.Create(new Vector3(-4,0,-5), 0.5f),
                Sphere.Create(new Vector3(4,0,-6), 0.5f), Sphere.Create(new Vector3(3,0,-6), 0.5f), Sphere.Create(new Vector3(2,0,-6), 0.5f), Sphere.Create(new Vector3(1,0,-6), 0.5f), Sphere.Create(new Vector3(0,0,-6), 0.5f), Sphere.Create(new Vector3(-1,0,-6), 0.5f), Sphere.Create(new Vector3(-2,0,-6), 0.5f), Sphere.Create(new Vector3(-3,0,-6), 0.5f), Sphere.Create(new Vector3(-4,0,-6), 0.5f),
                Sphere.Create(new Vector3(1.5f,1.5f,-2), 0.3f),
            };

            _materials = new[]
            {
                Material.Lambertian(new Vector3(0.8f, 0.8f, 0.8f)),
                Material.Lambertian(new Vector3(0.8f, 0.4f, 0.4f)),
                Material.Lambertian(new Vector3(0.4f, 0.8f, 0.4f)),
                Material.Metal(new Vector3(0.4f, 0.4f, 0.8f), 0),
                Material.Metal(new Vector3(0.4f, 0.8f, 0.4f), 0),
                Material.Metal(new Vector3(0.4f, 0.8f, 0.4f), 0.2f),
                Material.Metal(new Vector3(0.4f, 0.8f, 0.4f), 0.6f),
                Material.Dielectric(1.5f),
                Material.Lambertian(new Vector3(0.8f, 0.6f, 0.2f)),
                Material.Lambertian(new Vector3(0.1f, 0.1f, 0.1f)), Material.Lambertian(new Vector3(0.2f, 0.2f, 0.2f)), Material.Lambertian(new Vector3(0.3f, 0.3f, 0.3f)), Material.Lambertian(new Vector3(0.4f, 0.4f, 0.4f)), Material.Lambertian(new Vector3(0.5f, 0.5f, 0.5f)), Material.Lambertian(new Vector3(0.6f, 0.6f, 0.6f)), Material.Lambertian(new Vector3(0.7f, 0.7f, 0.7f)), Material.Lambertian(new Vector3(0.8f, 0.8f, 0.8f)), Material.Lambertian(new Vector3(0.9f, 0.9f, 0.9f)),
                Material.Metal(new Vector3(0.1f, 0.1f, 0.1f), 0f), Material.Metal(new Vector3(0.2f, 0.2f, 0.2f), 0f), Material.Metal(new Vector3(0.3f, 0.3f, 0.3f), 0f), Material.Metal(new Vector3(0.4f, 0.4f, 0.4f), 0f), Material.Metal(new Vector3(0.5f, 0.5f, 0.5f), 0f), Material.Metal(new Vector3(0.6f, 0.6f, 0.6f), 0f), Material.Metal(new Vector3(0.7f, 0.7f, 0.7f), 0f), Material.Metal(new Vector3(0.8f, 0.8f, 0.8f), 0f), Material.Metal(new Vector3(0.9f, 0.9f, 0.9f), 0f),
                Material.Metal(new Vector3(0.8f, 0.1f, 0.1f), 0f), Material.Metal(new Vector3(0.8f, 0.5f, 0.1f), 0f), Material.Metal(new Vector3(0.8f, 0.8f, 0.1f), 0f), Material.Metal(new Vector3(0.4f, 0.8f, 0.1f), 0f), Material.Metal(new Vector3(0.1f, 0.8f, 0.1f), 0f), Material.Metal(new Vector3(0.1f, 0.8f, 0.5f), 0f), Material.Metal(new Vector3(0.1f, 0.8f, 0.8f), 0f), Material.Metal(new Vector3(0.1f, 0.1f, 0.8f), 0f), Material.Metal(new Vector3(0.5f, 0.1f, 0.8f), 0f),
                Material.Lambertian(new Vector3(0.8f, 0.1f, 0.1f)), Material.Lambertian(new Vector3(0.8f, 0.5f, 0.1f)), Material.Lambertian(new Vector3(0.8f, 0.8f, 0.1f)), Material.Lambertian(new Vector3(0.4f, 0.8f, 0.1f)), Material.Lambertian(new Vector3(0.1f, 0.8f, 0.1f)), Material.Lambertian(new Vector3(0.1f, 0.8f, 0.5f)), Material.Lambertian(new Vector3(0.1f, 0.8f, 0.8f)), Material.Lambertian(new Vector3(0.1f, 0.1f, 0.8f)), Material.Metal(new Vector3(0.5f, 0.1f, 0.8f), 0f),
                Material.Lambertian(new Vector3(0.1f, 0.2f, 0.5f))
            };
        }

        private void RenderFrame()
        {
            _sceneParams.FrameCount += 1;

            _cl.Begin();

            if (_drawModeCPU)
            {
                RenderCPU();
            }
            else
            {
                RenderGPU();
            }

            fixed (RgbaFloat* pixelDataPtr = _fb)
            {
                _gd.UpdateTexture(_transferTex, (IntPtr)pixelDataPtr, Width * Height * (uint)sizeof(RgbaFloat), 0, 0, 0, Width, Height, 1, 0, 0);
            }

            _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
            _cl.SetPipeline(_graphicsPipeline);
            _cl.SetGraphicsResourceSet(0, _graphicsSet);
            _cl.Draw(3);
            _cl.End();
            _gd.SubmitCommands(_cl);
            _gd.SwapBuffers();

            if (!_drawModeCPU)
            {
                MappedResourceView<uint> rayCountView = _gd.Map<uint>(_rayCountReadback, MapMode.Read);
                _totalRays += rayCountView[0];
                _gd.Unmap(_rayCountReadback);
            }

            float seconds = _stopwatch.ElapsedMilliseconds / 1000f;
            float rate = _totalRays / seconds;
            float mRate = rate / 1_000_000;
            float frameRate = _sceneParams.FrameCount / (float)_stopwatch.Elapsed.TotalSeconds;
            _window.Title = $"Elapsed: {seconds} sec. | Rate: {mRate} MRays / sec. | {frameRate} fps";
        }

        private void RenderGPU()
        {
            _cl.UpdateBuffer(_sceneParamsBuffer, 0, ref _sceneParams);
            _cl.UpdateBuffer(_rayCountBuffer, 0, new Vector4());
            _cl.SetPipeline(_computePipeline);
            _cl.SetComputeResourceSet(0, _computeSet);
            Debug.Assert(Width % 16 == 0 && Height % 16 == 0);
            uint xCount = Width / 16;
            uint yCount = Height / 16;
            _cl.Dispatch(xCount, yCount, 1);
            _cl.CopyBuffer(_rayCountBuffer, 0, _rayCountReadback, 0, _rayCountBuffer.SizeInBytes);
        }

        private void RenderCPU()
        {
            int frameRays = 0;
            float invWidth = 1f / Width;
            float invHeight = 1f / Height;

            Parallel.For(0, Height, y =>
            {
                int rayCount = 0;
                uint state = (uint)(y * 9781 + _sceneParams.FrameCount * 6271) | 1;
                for (uint x = 0; x < Width; x++)
                {
                    Vector4 color = Vector4.Zero;
                    for (uint sample = 0; sample < NumSamples; sample++)
                    {
                        float u = (x + RandUtil.RandomFloat(ref state)) * invWidth;
                        float v = (y + RandUtil.RandomFloat(ref state)) * invHeight;
                        Ray ray = Camera.GetRay(_sceneParams.Camera, u, v, ref state);
                        color += Color(_sceneParams.SphereCount, _spheres, _materials, ref state, ref ray, 0, ref rayCount);
                    }
                    color /= NumSamples;
                    _fb[y * Width + x] = new RgbaFloat(color.X, color.Y, color.Z, color.W);
                }

                Interlocked.Add(ref frameRays, rayCount);
            });

            _totalRays += (uint)frameRays;
        }

        public static Vector4 Color(
            uint sphereCount,
            Sphere[] spheres,
            Material[] materials,
            ref uint randState,
            ref Ray ray,
            int depth,
            ref int rayCount)
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
                if (Sphere.Hit(spheres[i], ray, Epsilon, closest, out RayHit tempHit))
                {
                    hitAnything = true;
                    hit = tempHit;
                    hitID = i;
                    closest = hit.T;
                }
            }

            if (hitAnything)
            {
                if (depth < MaxDepth && Scatter(ray, hit, materials[hitID], ref randState, out Vector3 attenuation, out Ray scattered))
                {
                    return new Vector4(attenuation, 1f)
                        * Color(sphereCount, spheres, materials, ref randState, ref scattered, depth + 1, ref rayCount);
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

        public static bool Scatter(Ray ray, RayHit hit, Material material, ref uint state, out Vector3 attenuation, out Ray scattered)
        {
            switch (material.Type)
            {
                case MaterialType.Lambertian:
                {
                    Vector3 target = hit.Position + hit.Normal + RandUtil.RandomInUnitSphere(ref state);
                    scattered = Ray.Create(hit.Position, target - hit.Position);
                    attenuation = material.Albedo;
                    return true;
                }
                case MaterialType.Metal:
                {
                    Vector3 reflected = Vector3.Reflect(Vector3.Normalize(ray.Direction), hit.Normal);
                    scattered = Ray.Create(
                        hit.Position,
                        reflected + material.FuzzOrRefIndex * RandUtil.RandomInUnitSphere(ref state));
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
                    if (RandUtil.RandomFloat(ref state) < reflectProb)
                    {
                        scattered = Ray.Create(hit.Position, reflectDir);
                    }
                    else
                    {
                        scattered = Ray.Create(hit.Position, refractDir);
                    }

                    return true;
                }

                default:
                    attenuation = new Vector3();
                    scattered = Ray.Create(new Vector3(), new Vector3());
                    return false;
            }
        }

        public static bool Refract(Vector3 v, Vector3 n, float niOverNt, out Vector3 refracted)
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
                TextureDescription.Texture2D(Width, Height, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Sampled | TextureUsage.Storage));
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
                        factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, LoadShaderBytes("FramebufferBlitter-vertex"), "VS")),
                        factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, LoadShaderBytes("FramebufferBlitter-fragment"), "FS"))
                    }),
                graphicsLayout,
                _gd.MainSwapchain.Framebuffer.OutputDescription));

            _spheresBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<Sphere>() * _sceneParams.SphereCount,
                BufferUsage.StructuredBufferReadOnly,
                (uint)Unsafe.SizeOf<Sphere>()));
            _gd.UpdateBuffer(_spheresBuffer, 0, _spheres);

            _materialsBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<Material>() * _sceneParams.SphereCount,
                BufferUsage.StructuredBufferReadOnly,
                (uint)Unsafe.SizeOf<Material>()));
            _gd.UpdateBuffer(_materialsBuffer, 0, _materials);

            _sceneParamsBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<SceneParams>(),
                BufferUsage.UniformBuffer));
            _gd.UpdateBuffer(_sceneParamsBuffer, 0, new Vector4(0));

            _rayCountBuffer = factory.CreateBuffer(new BufferDescription(16, BufferUsage.StructuredBufferReadWrite, 4));
            _rayCountReadback = factory.CreateBuffer(new BufferDescription(16, BufferUsage.Staging));

            ResourceLayout computeLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Spheres", ResourceKind.StructuredBufferReadOnly, ShaderStages.Compute),
                new ResourceLayoutElementDescription("Materials", ResourceKind.StructuredBufferReadOnly, ShaderStages.Compute),
                new ResourceLayoutElementDescription("Output", ResourceKind.TextureReadWrite, ShaderStages.Compute),
                new ResourceLayoutElementDescription("Params", ResourceKind.UniformBuffer, ShaderStages.Compute),
                new ResourceLayoutElementDescription("RayCount", ResourceKind.StructuredBufferReadWrite, ShaderStages.Compute)));

            _computeSet = factory.CreateResourceSet(new ResourceSetDescription(computeLayout,
                _spheresBuffer,
                _materialsBuffer,
                _texView,
                _sceneParamsBuffer,
                _rayCountBuffer));

            _computePipeline = factory.CreateComputePipeline(new ComputePipelineDescription(
                factory.CreateShader(new ShaderDescription(ShaderStages.Compute, LoadShaderBytes("RayTraceCompute-compute"), "CS")),
                computeLayout,
                16, 16, 1));
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
                    extension = "metallib";
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
