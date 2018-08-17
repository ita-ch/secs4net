using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Secs4Net.Extensions;

namespace Secs4Net
{
    public readonly struct ItemValue<T> where T : unmanaged
    {
        internal delegate T Reader(ReadOnlySpan<byte> bytes);

        internal static readonly Reader ReadT;

        static unsafe ItemValue()
        {
            if (BitConverter.IsLittleEndian)
            {
                ReadT = MemoryMarshal.Read<T>;
                return;
            }

            var type = typeof(T);
            if (type == typeof(short))
            {
                ReadT = span =>
                {
                    var v = BinaryPrimitives.ReadInt16BigEndian(span);
                    return Unsafe.As<short, T>(ref v);
                };
            }
            else if (type == typeof(ushort))
            {
                ReadT = span =>
                {
                    var v = BinaryPrimitives.ReadUInt16BigEndian(span);
                    return Unsafe.As<ushort, T>(ref v);
                };
            }
            else if (type == typeof(int))
            {
                ReadT = span =>
                {
                    var v = BinaryPrimitives.ReadInt32BigEndian(span);
                    return Unsafe.As<int, T>(ref v);
                };
            }
            else if (type == typeof(uint))
            {
                ReadT = span =>
                {
                    var v = BinaryPrimitives.ReadUInt32BigEndian(span);
                    return Unsafe.As<uint, T>(ref v);
                };
            }
            else if (type == typeof(long))
            {
                ReadT = span =>
                {
                    var v = BinaryPrimitives.ReadInt64BigEndian(span);
                    return Unsafe.As<long, T>(ref v);
                };
            }
            else if (type == typeof(ulong))
            {
                ReadT = span =>
                {
                    var v = BinaryPrimitives.ReadUInt64BigEndian(span);
                    return Unsafe.As<ulong, T>(ref v);
                };
            }
            else if (type == typeof(float))
            {
                ReadT = span =>
                {
                    var v = ReaderBigEndian.ReadFloatBigEndian(span);
                    return Unsafe.As<float, T>(ref v);
                };
            }
            else if (type == typeof(double))
            {
                ReadT = span =>
                {
                    var v = ReaderBigEndian.ReadDoubleBigEndian(span);
                    return Unsafe.As<double, T>(ref v);
                };
            }

        }

        private readonly byte[] _data;

        public ItemValue(byte[] data)
        {
            _data = data;
        }

        public unsafe T this[int index] => (sizeof(T) > 1)
            ? ReadT(new ReadOnlySpan<byte>(_data, index, sizeof(T)))
            : Unsafe.As<byte, T>(ref _data[index]);

        public unsafe int Length => _data.Length / sizeof(T);

        /// <summary>Gets an enumerator for this span.</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>Enumerates the elements of a <see cref="Span{T}"/>.</summary>
        public struct Enumerator
        {
            /// <summary>The span being enumerated.</summary>
            private readonly ItemValue<T> _span;
            /// <summary>The next index to yield.</summary>
            private int _index;
            private int _length;

            /// <summary>Initialize the enumerator.</summary>
            /// <param name="span">The span to enumerate.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(ItemValue<T> span)
            {
                _span = span;
                _index = -1;
                _length = span.Length;
            }

            /// <summary>Advances the enumerator to the next element of the span.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                int index = _index + 1;
                if (index < _length)
                {
                    _index = index;
                    return true;
                }

                return false;
            }

            /// <summary>Gets the element at the current position of the enumerator.</summary>
            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _span[_index];
            }
        }
    }
}
