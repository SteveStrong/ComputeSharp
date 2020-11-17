﻿using System;
using System.Runtime.CompilerServices;
using ComputeSharp.Graphics.Buffers.Enums;
using ComputeSharp.Graphics.Commands;
using ComputeSharp.Graphics.Helpers;
using static TerraFX.Interop.D3D12_COMMAND_LIST_TYPE;

namespace ComputeSharp.Graphics.Buffers.Abstract
{
    /// <summary>
    /// A <see langword="class"/> representing a typed structured buffer stored on GPU memory
    /// </summary>
    /// <typeparam name="T">The type of items stored on the buffer</typeparam>
    public abstract class HlslStructuredBuffer2<T> : HlslBuffer2<T>
        where T : unmanaged
    {
        /// <summary>
        /// Creates a new <see cref="HlslStructuredBuffer2{T}"/> instance with the specified parameters
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice2"/> associated with the current instance</param>
        /// <param name="size">The number of items to store in the current buffer</param>
        /// <param name="bufferType">The buffer type for the current buffer</param>
        internal HlslStructuredBuffer2(GraphicsDevice2 device, int size, BufferType bufferType)
            : base(device, size, size * Unsafe.SizeOf<T>(), bufferType)
        {
        }

        /// <inheritdoc/>
        public override unsafe void GetData(Span<T> span, int offset, int count)
        {
            using Buffer2<T> transferBuffer = new Buffer2<T>(GraphicsDevice, count, count * ElementSizeInBytes, BufferType.ReadBack);

            using (CommandList2 copyCommandList = new CommandList2(GraphicsDevice, D3D12_COMMAND_LIST_TYPE_COPY))
            {
                copyCommandList.CopyBufferRegion(D3D12Resource, offset * ElementSizeInBytes, transferBuffer.D3D12Resource, 0, count * ElementSizeInBytes);
                copyCommandList.ExecuteAndWaitForCompletion();
            }

            using MappedResource resource = transferBuffer.MapResource();

            MemoryHelper.Copy(resource.Pointer, 0, span, 0, count);
        }

        /// <inheritdoc/>
        public override unsafe void SetData(ReadOnlySpan<T> span, int offset, int count)
        {
            using Buffer2<T> transferBuffer = new Buffer2<T>(GraphicsDevice, count, count * ElementSizeInBytes, BufferType.Transfer);

            using (MappedResource resource = transferBuffer.MapResource())
            {
                MemoryHelper.Copy(span, resource.Pointer, 0, count);
            }

            using CommandList2 copyCommandList = new CommandList2(GraphicsDevice, D3D12_COMMAND_LIST_TYPE_COPY);

            copyCommandList.CopyBufferRegion(transferBuffer.D3D12Resource, 0, D3D12Resource, offset * ElementSizeInBytes, count * ElementSizeInBytes);
            copyCommandList.ExecuteAndWaitForCompletion();
        }

        /// <inheritdoc/>
        public override unsafe void SetData(HlslBuffer2<T> buffer)
        {
            if (!buffer.IsPaddingPresent)
            {
                // Directly copy the input buffer
                using CommandList2 copyCommandList = new CommandList2(GraphicsDevice, D3D12_COMMAND_LIST_TYPE_COPY);

                copyCommandList.CopyBufferRegion(buffer.D3D12Resource, 0, D3D12Resource, 0, SizeInBytes);
                copyCommandList.ExecuteAndWaitForCompletion();
            }
            else SetDataWithCpuBuffer(buffer);
        }
    }
}