// Uncomment this to run the VS profiler
//#define PROFILER

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;

namespace ComputeSharp.Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
#if PROFILER
            DnnBenchmark benchmark = new();

            benchmark.Setup();

            while (true)
            {
                benchmark.GpuWithNoTemporaryBuffers();
            }
#else
            var config = DefaultConfig.Instance
                .AddJob(
                    Job.Default.WithToolchain(
                        CsProjCoreToolchain.From(
                            new NetCoreAppSettings(
                                targetFrameworkMoniker: "net5.0-windows10.0.19041.0",
                                runtimeFrameworkVersion: null,
                                name: "5.0")))
                                .AsDefault());

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
#endif
        }
    }
}
