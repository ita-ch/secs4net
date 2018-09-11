using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Secs4Net
{
    public sealed class Item
    {
        public SecsFormat Format { get; }

        public int Count => Format == SecsFormat.List ? _list.Count : (_values.Length / Format.SizeOf());

        /// <summary>
        /// List items
        /// </summary>
        public IReadOnlyList<Item> Items => Format == SecsFormat.List ? _list : throw new InvalidOperationException("The item is not a list");

        private readonly IReadOnlyList<Item> _list;
        private readonly byte[] _values;

        /// <summary>
        /// List
        /// </summary>
        private Item(IReadOnlyList<Item> items)
        {
            if (items.Count > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(items) + "." + nameof(items.Count), items.Count,
                    @"List items length out of range, max length: 255");

            Format = SecsFormat.List;
            _list = items;
            _values = new byte[] { (byte)SecsFormat.List | 1, unchecked((byte)_list.Count) };
        }

        /// <summary>
        /// U1, U2, U4, U8
        /// I1, I2, I4, I8
        /// F4, F8
        /// Boolean,
        /// Binary
        /// </summary>
        private Item(SecsFormat format, Array value)
        {
            Format = format;

            var bytelength = Buffer.ByteLength(value);
            var encodedBytes = new byte[bytelength];
            Buffer.BlockCopy(value, 0, encodedBytes, 0, bytelength);
            if (BitConverter.IsLittleEndian)
            {
                encodedBytes.Reverse(0, bytelength, bytelength / value.Length);
            }

            _values = encodedBytes;
        }

        /// <summary>
        /// A,J
        /// </summary>
        private Item(SecsFormat format, string value)
        {
            Format = format;
            var encodedBytes = new byte[value.Length];
            var encoder = Format == SecsFormat.ASCII ? Encoding.ASCII : Jis8Encoding;
            encoder.GetBytes(value, encodedBytes);
            _values = encodedBytes;
        }

        private Item(SecsFormat format, byte[] bytes)
        {
            Format = format;
            _values = bytes;
        }

        /// <summary>
        /// Empty Item
        /// </summary>
        /// <param name="format"></param>
        private Item(SecsFormat format)
        {
            Format = format;
            if (format == SecsFormat.List)
            {
                _list = Array.Empty<Item>();
            }
            else
            {
                _values = Array.Empty<byte>();
            }

        }

        /// <summary>
        /// get value by specific type
        /// </summary>
        public T GetValue<T>() where T : unmanaged
        {
            if (Format == SecsFormat.List)
                throw new InvalidOperationException("The item is a list");

            if (Format == SecsFormat.ASCII || Format == SecsFormat.JIS8)
                throw new InvalidOperationException("The item is a string");

            return new ItemValue<T>(_values)[0];
        }

        public unsafe T GetValueOrDefault<T>(T defaultValue = default) where T : unmanaged
        {
            if (Format == SecsFormat.List)
                throw new InvalidOperationException("The item is a list");

            if (Format == SecsFormat.ASCII || Format == SecsFormat.JIS8)
                throw new InvalidOperationException("The item is a string");

            if (_values.Length < sizeof(T))
                return defaultValue;

            return new ItemValue<T>(_values)[0];
        }

        /// <summary>
        /// get value array by specific type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public ItemValue<T> GetValues<T>() where T : unmanaged
        {
            if (Format == SecsFormat.List)
                throw new InvalidOperationException("The item is list");

            if (Format == SecsFormat.ASCII || Format == SecsFormat.JIS8)
                throw new InvalidOperationException("The item is a string");

            return new ItemValue<T>(_values);
        }

        public string GetString()
        {
            if (Format == SecsFormat.ASCII)
            {
                return Encoding.ASCII.GetString(_values);
            }
            else if (Format == SecsFormat.JIS8)
            {
                return Jis8Encoding.GetString(_values);
            }

            throw new InvalidOperationException("The type is incompatible");
        }

        public void GetChars(Span<char> chars)
        {
            if (Format == SecsFormat.ASCII)
            {
                Encoding.ASCII.GetChars(_values, chars);
            }
            else if (Format == SecsFormat.JIS8)
            {
                Jis8Encoding.GetChars(_values, chars);
            }

            throw new InvalidOperationException("The type is incompatible");
        }

        public bool IsMatch(Item target)
        {
            if (Format != target.Format)
            {
                return false;
            }

            if (Count != target.Count)
            {
                return target.Count == 0;
            }

            if (Count == 0)
            {
                return true;
            }

            if (target.Format == SecsFormat.List)
            {
                return IsMatch(_list, target._list);
            }
            else
            {
                return _values.AsSpan().SequenceEqual(target._values.AsSpan());
            }

            bool IsMatch(IReadOnlyList<Item> a, IReadOnlyList<Item> b)
            {
                for (int i = 0, count = a.Count; i < count; i++)
                    if (!a[i].IsMatch(b[i]))
                        return false;
                return true;
            }
        }

        public override string ToString() => $"{Format.GetName()} [{Count}]";

        #region Type Casting Operator
        public static implicit operator string(Item item) => item.GetString();
        public static implicit operator byte(Item item) => item.GetValue<byte>();
        public static implicit operator sbyte(Item item) => item.GetValue<sbyte>();
        public static implicit operator ushort(Item item) => item.GetValue<ushort>();
        public static implicit operator short(Item item) => item.GetValue<short>();
        public static implicit operator uint(Item item) => item.GetValue<uint>();
        public static implicit operator int(Item item) => item.GetValue<int>();
        public static implicit operator ulong(Item item) => item.GetValue<ulong>();
        public static implicit operator long(Item item) => item.GetValue<long>();
        public static implicit operator float(Item item) => item.GetValue<float>();
        public static implicit operator double(Item item) => item.GetValue<double>();
        public static implicit operator bool(Item item) => item.GetValue<bool>();

        #endregion

        #region Factory Methods

        internal static Item L(IList<Item> items) => items.Count > 0 ? new Item(new ReadOnlyCollection<Item>(items)) : L();
        public static Item L(IEnumerable<Item> items) => L(items.ToList());
        public static Item L(params Item[] items) => L((IList<Item>)items);

        public static Item B(params byte[] value) => value.Length > 0 ? new Item(SecsFormat.Binary, value: value) : B();
        public static Item B(IEnumerable<byte> value) => B(value.ToArray());

        public static Item U1(params byte[] value) => value.Length > 0 ? new Item(SecsFormat.U1, value: value) : U1();
        public static Item U1(IEnumerable<byte> value) => U1(value.ToArray());

        public static Item U2(params ushort[] value) => value.Length > 0 ? new Item(SecsFormat.U2, value) : U2();
        public static Item U2(IEnumerable<ushort> value) => U2(value.ToArray());

        public static Item U4(params uint[] value) => value.Length > 0 ? new Item(SecsFormat.U4, value) : U4();
        public static Item U4(IEnumerable<uint> value) => U4(value.ToArray());

        public static Item U8(params ulong[] value) => value.Length > 0 ? new Item(SecsFormat.U8, value) : U8();
        public static Item U8(IEnumerable<ulong> value) => U8(value.ToArray());

        public static Item I1(params sbyte[] value) => value.Length > 0 ? new Item(SecsFormat.I1, value) : I1();
        public static Item I1(IEnumerable<sbyte> value) => I1(value.ToArray());

        public static Item I2(params short[] value) => value.Length > 0 ? new Item(SecsFormat.I2, value) : I2();
        public static Item I2(IEnumerable<short> value) => I2(value.ToArray());

        public static Item I4(params int[] value) => value.Length > 0 ? new Item(SecsFormat.I4, value) : I4();
        public static Item I4(IEnumerable<int> value) => I4(value.ToArray());

        public static Item I8(params long[] value) => value.Length > 0 ? new Item(SecsFormat.I8, value) : I8();
        public static Item I8(IEnumerable<long> value) => I8(value.ToArray());

        public static Item F4(params float[] value) => value.Length > 0 ? new Item(SecsFormat.F4, value) : F4();
        public static Item F4(IEnumerable<float> value) => F4(value.ToArray());

        public static Item F8(params double[] value) => value.Length > 0 ? new Item(SecsFormat.F8, value) : F8();
        public static Item F8(IEnumerable<double> value) => F8(value.ToArray());

        public static Item Boolean(params bool[] value) => value.Length > 0 ? new Item(SecsFormat.Boolean, value) : Boolean();
        public static Item Boolean(IEnumerable<bool> value) => Boolean(value.ToArray());

        public static Item A(string value) => value != string.Empty ? new Item(SecsFormat.ASCII, value) : A();

        public static Item J(string value) => value != string.Empty ? new Item(SecsFormat.JIS8, value) : J();
        #endregion

        #region Share Object

        public static Item L() => EmptyL;
        public static Item B() => EmptyBinary;
        public static Item U1() => EmptyU1;
        public static Item U2() => EmptyU2;
        public static Item U4() => EmptyU4;
        public static Item U8() => EmptyU8;
        public static Item I1() => EmptyI1;
        public static Item I2() => EmptyI2;
        public static Item I4() => EmptyI4;
        public static Item I8() => EmptyI8;
        public static Item F4() => EmptyF4;
        public static Item F8() => EmptyF8;
        public static Item Boolean() => EmptyBoolean;
        public static Item A() => EmptyA;
        public static Item J() => EmptyJ;

        private static readonly Item EmptyL = new Item(SecsFormat.List);
        private static readonly Item EmptyA = new Item(SecsFormat.ASCII);
        private static readonly Item EmptyJ = new Item(SecsFormat.JIS8);
        private static readonly Item EmptyBoolean = new Item(SecsFormat.Boolean);
        private static readonly Item EmptyBinary = new Item(SecsFormat.Binary);
        private static readonly Item EmptyU1 = new Item(SecsFormat.U1);
        private static readonly Item EmptyU2 = new Item(SecsFormat.U2);
        private static readonly Item EmptyU4 = new Item(SecsFormat.U4);
        private static readonly Item EmptyU8 = new Item(SecsFormat.U8);
        private static readonly Item EmptyI1 = new Item(SecsFormat.I1);
        private static readonly Item EmptyI2 = new Item(SecsFormat.I2);
        private static readonly Item EmptyI4 = new Item(SecsFormat.I4);
        private static readonly Item EmptyI8 = new Item(SecsFormat.I8);
        private static readonly Item EmptyF4 = new Item(SecsFormat.F4);
        private static readonly Item EmptyF8 = new Item(SecsFormat.F8);

        internal static readonly Encoding Jis8Encoding = Encoding.GetEncoding(50222);
        #endregion

        internal int EncodeTo(List<ArraySegment<byte>> buffer)
        {
            var length = _values.Length;

            if (Format == SecsFormat.List)
            {
                buffer.Add(_values);
                foreach (var subItem in Items)
                {
                    length = unchecked(length + subItem.EncodeTo(buffer));
                }
            }
            else
            {
                var itemHeader = GetEncodedHeader(Format, _values.Length);
                length = unchecked(length + itemHeader.Length);
                buffer.Add(itemHeader);
                buffer.Add(_values);
            }

            return length;
        }

        /// <summary>
        /// Encode Item header + value (initial array only)
        /// </summary>
        /// <param name="valueCount">Item value bytes length</param>
        /// <returns>header bytes + initial bytes of value </returns>
        private static unsafe byte[] GetEncodedHeader(SecsFormat format, int valueCount)
        {
            Span<byte> lengthBytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(lengthBytes, valueCount);

            if (valueCount <= 0xff)
            {//	1 byte
                var result = new byte[2];
                result[0] = (byte)((byte)format | 1);
                result[1] = lengthBytes[1];
                return result;
            }
            if (valueCount <= 0xffff)
            {//	2 byte
                var result = new byte[3];
                result[0] = (byte)((byte)format | 2);
                result[1] = lengthBytes[1];
                result[2] = lengthBytes[2];
                return result;
            }
            if (valueCount <= 0xffffff)
            {//	3 byte
                var result = new byte[4];
                result[0] = (byte)((byte)format | 3);
                result[1] = lengthBytes[1];
                result[2] = lengthBytes[2];
                result[3] = lengthBytes[3];
                return result;
            }
            throw new ArgumentOutOfRangeException(nameof(valueCount), valueCount, $@"Item data length:{valueCount} is overflow");
        }

        internal static Item BytesDecode(in SecsFormat format, in ReadOnlySequence<byte> data)
        {
            if (format == SecsFormat.List)
                throw new ArgumentException("Invalid format", nameof(format));

            return (data.Length == 0)
                ? new Item(format)
                : new Item(format, bytes: data.IsSingleSegment ? data.First.Span.ToArray() : data.ToArray());
        }
    }
}