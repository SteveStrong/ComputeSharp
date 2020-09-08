using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ComputeSharp.Sample
{
    class Program
    {
        static void Main()
        {
            // Create the graphics buffer
            int[] array = new int[512 * 512];

            for (int i = 0; i < array.Length; i++)
            {
                array[i] = i;
            }

            int[] expected = new int[array.Length];

            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = i * 2;
            }

            int[] copy = new int[array.Length];

            for (int i = 0;;i++)
            {
                Console.WriteLine(i);

                using ReadWriteBuffer<int> gpuBuffer = Gpu.Default.AllocateReadWriteBuffer(array);

                var kernel = new MainKernel(512, gpuBuffer);

                Gpu.Default.For(512, kernel);

                gpuBuffer.GetData(copy);

                if (!copy.AsSpan().SequenceEqual(expected))
                {
                    Debugger.Break();

                    throw new InvalidOperationException("No match :(");
                }
            }
        }

        private readonly struct MainKernel : IComputeShader
        {
            private readonly int width;

            private readonly ReadWriteBuffer<int> buffer;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public MainKernel(int width, ReadWriteBuffer<int> buffer)
            {
                this.width = width;
                this.buffer = buffer;
            }

            /// <inheritdoc/>
            public void Execute(ThreadIds ids)
            {
                int offset = ids.X * width;

                for (int i = 0; i < width; i++)
                {
                    buffer[offset + i] *= 2;
                }
            }
        }
    }
}

