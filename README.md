# Veldrid Ray Tracer

![Image](https://i.imgur.com/kFMTcu8.jpg)

This is a simple, brute-force ray tracer written in C# and capable of running on the CPU (using .NET Core) and on the GPU in a compute shader (using [Veldrid](https://mellinoe.github.io/veldrid-docs/) and [ShaderGen](https://github.com/mellinoe/shadergen)). Everything is written in C#, and a majority of the logic is shared between the versions that run on the CPU and GPU.

## How To Run

This is a .NET Core application, so you will need [the .NET Core SDK](https://www.microsoft.com/net/download/windows) for your platform to build and run it.

`dotnet run -c Release -p raytracer.csproj`

By default, the ray tracing will be done on the GPU. You can change this by setting `_drawModeCPU` to true. When drawing on the CPU, you may want to lower the `Width` and `Height` of the output image, as well as the `NumSamples` used. These are all found at the top of RayTracingApplication.cs.

You can force a different graphics API to be used by changing the `backend` variable at the beginning of `RayTracingApplication.Run()`. By default, the application will automatically choose the "platform default" graphics API.
