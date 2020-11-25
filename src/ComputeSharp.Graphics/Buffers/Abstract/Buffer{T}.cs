﻿using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using ComputeSharp.Core.Interop;
using ComputeSharp.Exceptions;
using ComputeSharp.Graphics.Buffers.Enums;
using ComputeSharp.Graphics.Extensions;
using Microsoft.Toolkit.Diagnostics;
using TerraFX.Interop;
using static TerraFX.Interop.D3D12_SRV_DIMENSION;
using static TerraFX.Interop.D3D12_UAV_DIMENSION;
using FX = TerraFX.Interop.Windows;

namespace ComputeSharp.Graphics.Buffers.Abstract
{
    /// <summary>
    /// A <see langword="class"/> representing a typed buffer stored on GPU memory.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the buffer.</typeparam>
    public unsafe abstract class Buffer<T> : NativeObject
        where T : unmanaged
    {
        /// <summary>
        /// The <see cref="ID3D12Resource"/> instance currently mapped.
        /// </summary>
        private ComPtr<ID3D12Resource> d3D12Resource;

        /// <summary>
        /// The <see cref="D3D12_GPU_DESCRIPTOR_HANDLE"/> instance for the current resource.
        /// </summary>
        internal readonly D3D12_GPU_DESCRIPTOR_HANDLE D3D12GpuDescriptorHandle;

        /// <summary>
        /// The size in bytes of the current buffer (this value is never negative).
        /// </summary>
        protected readonly nint SizeInBytes;

        /// <summary>
        /// Creates a new <see cref="Buffer{T}"/> instance with the specified parameters.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/> associated with the current instance.</param>
        /// <param name="length">The number of items to store in the current buffer.</param>
        /// <param name="elementSizeInBytes">The size in bytes of each buffer item (including padding, if any).</param>
        /// <param name="bufferType">The buffer type for the current buffer.</param>
        internal Buffer(GraphicsDevice device, int length, uint elementSizeInBytes, BufferType bufferType)
        {
            device.ThrowIfDisposed();

            Guard.IsGreaterThanOrEqualTo(length, 0, nameof(length));

            SizeInBytes = checked((nint)(length * elementSizeInBytes));
            GraphicsDevice = device;
            Length = length;

            this.d3D12Resource = device.D3D12Device->CreateCommittedResource(bufferType, (ulong)SizeInBytes);

            GraphicsDevice.AllocateShaderResourceViewDescriptorHandles(out D3D12_CPU_DESCRIPTOR_HANDLE d3D12CpuDescriptorHandle, out D3D12GpuDescriptorHandle);

            switch (bufferType)
            {
                case BufferType.Constant: CreateConstantBufferView(d3D12CpuDescriptorHandle); break;
                case BufferType.ReadOnly: CreateShaderResourceView(d3D12CpuDescriptorHandle); break;
                case BufferType.ReadWrite: CreateUnorderedAccessView(d3D12CpuDescriptorHandle); break;
            }
        }

        /// <summary>
        /// Gets the <see cref="GraphicsDevice"/> associated with the current instance.
        /// </summary>
        public GraphicsDevice GraphicsDevice { get; }

        /// <summary>
        /// Gets the length of the current buffer.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the size in bytes of each <typeparamref name="T"/> value contained in the buffer.
        /// </summary>
        protected int ElementSizeInBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.SizeOf<T>();
        }

        /// <summary>
        /// Gets whether or not there is some padding between elements in the current buffer.
        /// </summary>
        internal bool IsPaddingPresent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => SizeInBytes > Length * Unsafe.SizeOf<T>();
        }

        /// <summary>
        /// Gets the <see cref="ID3D12Resource"/> instance currently mapped.
        /// </summary>
        internal ID3D12Resource* D3D12Resource => this.d3D12Resource;

        /// <summary>
        /// Reads the contents of the current <see cref="Buffer{T}"/> instance and returns an array.
        /// </summary>
        /// <returns>A <typeparamref name="T"/> array with the contents of the current buffer.</returns>
        [Pure]
        public T[] GetData() => GetData(0, Length);

        /// <summary>
        /// Reads the contents of the current <see cref="Buffer{T}"/> instance in a given range and returns an array.
        /// </summary>
        /// <param name="offset">The offset to start reading data from.</param>
        /// <param name="count">The number of items to read.</param>
        /// <returns>A <typeparamref name="T"/> array with the contents of the specified range from the current buffer.</returns>
        [Pure]
        public T[] GetData(int offset, int count)
        {
            T[] data = GC.AllocateUninitializedArray<T>(count);

            GetData(data.AsSpan(), offset);

            return data;
        }

        /// <summary>
        /// Reads the contents of the current <see cref="Buffer{T}"/> instance and writes them into a target array.
        /// </summary>
        /// <param name="destination">The input array to write data to.</param>
        public void GetData(T[] destination) => GetData(destination.AsSpan(), 0);

        /// <summary>
        /// Reads the contents of the specified range from the current <see cref="Buffer{T}"/> instance and writes them into a target array.
        /// </summary>
        /// <param name="destination">The input array to write data to.</param>
        /// <param name="destinationOffset">The starting offset within <paramref name="source"/> to write data to.</param>
        /// <param name="bufferOffset">The offset to start reading data from.</param>
        /// <param name="count">The number of items to read.</param>
        public void GetData(T[] destination, int destinationOffset, int bufferOffset, int count)
        {
            Span<T> span = destination.AsSpan(destinationOffset, count);

            GetData(span, bufferOffset);
        }

        /// <summary>
        /// Reads the contents of the current <see cref="Buffer{T}"/> instance and writes them into a target <see cref="Span{T}"/>.
        /// The input data will be read from the start of the buffer.
        /// </summary>
        /// <param name="destination">The input <see cref="Span{T}"/> to write data to.</param>
        public void GetData(Span<T> destination) => GetData(destination, 0);

        /// <summary>
        /// Reads the contents of the specified range from the current <see cref="Buffer{T}"/> instance and writes them into a target <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="destination">The input <see cref="Span{T}"/> to write data to.</param>
        /// <param name="offset">The offset to start reading data from.</param>
        public abstract void GetData(Span<T> destination, int offset);

        /// <summary>
        /// Writes the contents of a given <typeparamref name="T"/> array to the current <see cref="Buffer{T}"/> instance.
        /// </summary>
        /// <param name="source">The input <typeparamref name="T"/> array to read data from.</param>
        public void SetData(T[] source) => SetData(source.AsSpan());

        /// <summary>
        /// Writes the contents of a given <typeparamref name="T"/> array to a specified area of the current <see cref="Buffer{T}"/> instance.
        /// </summary>
        /// <param name="source">The input <typeparamref name="T"/> array to read data from.</param>
        /// <param name="sourceOffset">The starting offset within <paramref name="source"/> to read data from.</param>
        /// <param name="bufferOffset">The offset to start writing data to.</param>
        /// <param name="count">The number of items to write.</param>
        public void SetData(T[] source, int sourceOffset, int bufferOffset, int count)
        {
            ReadOnlySpan<T> span = source.AsSpan(sourceOffset, count);

            SetData(span, bufferOffset);
        }

        /// <summary>
        /// Writes the contents of a given <see cref="ReadOnlySpan{T}"/> to the current <see cref="Buffer{T}"/> instance.
        /// The input data will be written to the start of the buffer, and all input items will be copied.
        /// </summary>
        /// <param name="source">The input <see cref="ReadOnlySpan{T}"/> to read data from.</param>
        public void SetData(ReadOnlySpan<T> source) => SetData(source, 0);

        /// <summary>
        /// Writes the contents of a given <see cref="ReadOnlySpan{T}"/> to a specified area of the current <see cref="Buffer{T}"/> instance.
        /// The input data will be written into the buffer starting at the specified offset, and all input items will be copied.
        /// </summary>
        /// <param name="source">The input <see cref="ReadOnlySpan{T}"/> to read data from.</param>
        /// <param name="offset">The offset to start writing data to.</param>
        /// <param name="count">The number of items to write.</param>
        public abstract void SetData(ReadOnlySpan<T> source, int offset);

        /// <summary>
        /// Writes the contents of a given <see cref="Buffer{T}"/> to the current <see cref="Buffer{T}"/> instance.
        /// </summary>
        /// <param name="source">The input <see cref="Buffer{T}"/> to read data from.</param>
        public abstract void SetData(Buffer<T> source);

        /// <summary>
        /// Writes the contents of a given <see cref="Buffer{T}"/> to the current <see cref="Buffer{T}"/> instance, using a temporary CPU buffer.
        /// </summary>
        /// <param name="source">The input <see cref="Buffer{T}"/> to read data from.</param>
        protected void SetDataWithCpuBuffer(Buffer<T> source)
        {
            T[] array = ArrayPool<T>.Shared.Rent(source.Length);

            try
            {
                Span<T> span = array.AsSpan(0, source.Length);

                source.GetData(span);

                SetData(span);
            }
            finally
            {
                ArrayPool<T>.Shared.Return(array);
            }
        }

        /// <summary>
        /// Creates a view for a constant buffer.
        /// </summary>
        /// <param name="d3D12CpuDescriptorHandle">The <see cref="D3D12_CPU_DESCRIPTOR_HANDLE"/> instance for the current resource.</param>
        private void CreateConstantBufferView(D3D12_CPU_DESCRIPTOR_HANDLE d3D12CpuDescriptorHandle)
        {
            uint constantBufferSize = checked((uint)((SizeInBytes + 255) & ~255));

            D3D12_CONSTANT_BUFFER_VIEW_DESC d3D12ConstantBufferViewDescription;
            d3D12ConstantBufferViewDescription.BufferLocation = D3D12Resource->GetGPUVirtualAddress();
            d3D12ConstantBufferViewDescription.SizeInBytes = constantBufferSize;

            GraphicsDevice.D3D12Device->CreateConstantBufferView(&d3D12ConstantBufferViewDescription, d3D12CpuDescriptorHandle);
        }

        /// <summary>
        /// Creates a view for a readonly buffer.
        /// </summary>
        /// <param name="d3D12CpuDescriptorHandle">The <see cref="D3D12_CPU_DESCRIPTOR_HANDLE"/> instance for the current resource.</param>
        private void CreateShaderResourceView(D3D12_CPU_DESCRIPTOR_HANDLE d3D12CpuDescriptorHandle)
        {
            D3D12_SHADER_RESOURCE_VIEW_DESC d3D12ShaderResourceViewDescription = default;
            d3D12ShaderResourceViewDescription.ViewDimension = D3D12_SRV_DIMENSION_BUFFER;
            d3D12ShaderResourceViewDescription.Shader4ComponentMapping = FX.D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
            d3D12ShaderResourceViewDescription.Buffer.NumElements = (uint)Length;
            d3D12ShaderResourceViewDescription.Buffer.StructureByteStride = (uint)ElementSizeInBytes;

            GraphicsDevice.D3D12Device->CreateShaderResourceView(D3D12Resource, &d3D12ShaderResourceViewDescription, d3D12CpuDescriptorHandle);
        }

        /// <summary>
        /// Creates a view for a buffer than be both read and written to.
        /// </summary>
        /// <param name="d3D12CpuDescriptorHandle">The <see cref="D3D12_CPU_DESCRIPTOR_HANDLE"/> instance for the current resource.</param>
        private void CreateUnorderedAccessView(D3D12_CPU_DESCRIPTOR_HANDLE d3D12CpuDescriptorHandle)
        {
            D3D12_UNORDERED_ACCESS_VIEW_DESC d3D12UnorderedAccessViewDescription = default;
            d3D12UnorderedAccessViewDescription.ViewDimension = D3D12_UAV_DIMENSION_BUFFER;
            d3D12UnorderedAccessViewDescription.Buffer.NumElements = (uint)Length;
            d3D12UnorderedAccessViewDescription.Buffer.StructureByteStride = (uint)ElementSizeInBytes;

            GraphicsDevice.D3D12Device->CreateUnorderedAccessView(D3D12Resource, null, &d3D12UnorderedAccessViewDescription, d3D12CpuDescriptorHandle);
        }

        /// <inheritdoc/>
        protected override void OnDispose()
        {
            this.d3D12Resource.Dispose();
        }

        /// <summary>
        /// Throws a <see cref="GraphicsDeviceMismatchException"/> if the target device doesn't match the current one.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ThrowIfDeviceMismatch(GraphicsDevice device)
        {
            if (GraphicsDevice != device)
            {
                void Throw() => throw GraphicsDeviceMismatchException.Create(this, device);

                Throw();
            }
        }
    }
}