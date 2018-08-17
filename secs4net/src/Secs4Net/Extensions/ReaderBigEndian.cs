using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Secs4Net.Extensions
{
    internal static class ReaderBigEndian
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadFloatBigEndian(ReadOnlySpan<byte> source)
        {
            var temp = MemoryMarshal.Read<uint>(source);
            if (BitConverter.IsLittleEndian)
            {
                temp = BinaryPrimitives.ReverseEndianness(temp);
            }
            return Unsafe.As<uint, float>(ref temp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDoubleBigEndian(ReadOnlySpan<byte> source)
        {
            var temp = MemoryMarshal.Read<ulong>(source);
            if (BitConverter.IsLittleEndian)
            {
                temp = BinaryPrimitives.ReverseEndianness(temp);
            }
            return Unsafe.As<ulong, double>(ref temp);
        }
    }
}
