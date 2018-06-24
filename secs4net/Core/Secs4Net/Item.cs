using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Secs4Net
{
	internal delegate int Encoder(Item item, Span<byte> buffer);

	public sealed class Item
    {
		private readonly object _values;
		private readonly Encoder _encoder;

		private static readonly Encoder ListEncoder = (item, buffer) =>
		{
			// if Format is List RawData is only header bytes.
			buffer[0] = (byte)SecsFormat.List | 0b_01;
			buffer[1] = unchecked((byte)(Unsafe.As<IReadOnlyList<Item>>(item._values).Count));
			return 2;
		};

		private static readonly Encoder StringEncoder = (item, buffer) =>
		{
			var str = Unsafe.As<string>(item._values).AsSpan();
			var encoder = item.Format == SecsFormat.ASCII ? Encoding.ASCII : Jis8Encoding;

			var bytelength = encoder.GetByteCount(str);
			var headerLength = EncodeItemHeader(item.Format, bytelength, buffer);
			encoder.GetBytes(str, buffer.Slice(headerLength));
			return headerLength + bytelength;
		};

		private static unsafe Encoder GetEncoder<T>() where T : unmanaged =>
			(item, buffer) =>
			{
				var bytes = MemoryMarshal.AsBytes(Unsafe.As<T[]>(item._values).AsSpan());
				var headerLength = EncodeItemHeader(item.Format, bytes.Length, buffer);
				var dataBytes = buffer.Slice(headerLength, bytes.Length);
				bytes.CopyTo(dataBytes);
				if (!BitConverter.IsLittleEndian)
				{
					dataBytes.ReverseByOffset(sizeof(T));
				}
				return headerLength + bytes.Length;
			};

		private Item(SecsFormat format, IEnumerable value, Encoder encoder)
		{
			Format = format;
			_values = value;
			_encoder = encoder;
		}

        public SecsFormat Format { get; }

		public int Count =>
            Format == SecsFormat.List
            ? Unsafe.As<IReadOnlyList<Item>>(_values).Count
            : Unsafe.As<Array>(_values).Length;

        /// <summary>
        /// List items
        /// </summary>
        public IReadOnlyList<Item> Items => Format != SecsFormat.List
            ? throw new InvalidOperationException("The item is not a list")
            : Unsafe.As<IReadOnlyList<Item>>(_values);

        /// <summary>
        /// get value by specific type
        /// </summary>
        public T GetValue<T>() where T : unmanaged
        {
            if (Format == SecsFormat.List)
                throw new InvalidOperationException("The item is a list");

            if (Format == SecsFormat.ASCII || Format == SecsFormat.JIS8)
                throw new InvalidOperationException("The item is a string");

            if (_values is T[] arr)
                return arr[0];

            throw new InvalidOperationException("The type is incompatible");
        }

        public string GetString() => Format != SecsFormat.ASCII && Format != SecsFormat.JIS8
            ? throw new InvalidOperationException("The type is incompatible")
            : Unsafe.As<string>(_values);

        /// <summary>
        /// get value array by specific type
        /// </summary>
        public T[] GetValues<T>() where T : unmanaged
        {
            if (Format == SecsFormat.List)
                throw new InvalidOperationException("The item is list");

            if (Format == SecsFormat.ASCII || Format == SecsFormat.JIS8)
                throw new InvalidOperationException("The item is a string");

            if (_values is T[] arr)
                return arr;

            throw new InvalidOperationException("The type is incompatible");
        }

        public bool IsMatch(Item target)
        {
			if (ReferenceEquals(this, target))
				return true;

            if (Format != target.Format)
                return false;

            if (Count != target.Count)
                return target.Count == 0;

            if (Count == 0)
                return true;

			switch (target.Format)
			{
				case SecsFormat.List:
					return IsMatch(
						Unsafe.As<IReadOnlyList<Item>>(_values),
						Unsafe.As<IReadOnlyList<Item>>(target._values));
				case SecsFormat.ASCII:
				case SecsFormat.JIS8:
					return Unsafe.As<string>(_values) == Unsafe.As<string>(target._values);
				default:
					//return memcmp(Unsafe.As<byte[]>(_values), Unsafe.As<byte[]>(target._values), Buffer.ByteLength((Array)_values)) == 0;
					return UnsafeCompare(Unsafe.As<Array>(_values), Unsafe.As<Array>(target._values));
			}

			//[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
			//static extern int memcmp(byte[] b1, byte[] b2, long count);
			// http://stackoverflow.com/questions/43289/comparing-two-byte-arrays-in-net/8808245#8808245
			unsafe bool UnsafeCompare(Array a1, Array a2)
            {
				int length = Buffer.ByteLength(a2);		
                fixed (byte* p1 = Unsafe.As<byte[]>(a1), p2 = Unsafe.As<byte[]>(a2))
                {
                    byte* x1 = p1, x2 = p2;
                    int l = length;
                    for (int i = 0; i < l / 8; i++, x1 += 8, x2 += 8)
                        if (*((long*)x1) != *((long*)x2)) return false;
                    if ((l & 4) != 0) { if (*((int*)x1) != *((int*)x2)) return false; x1 += 4; x2 += 4; }
                    if ((l & 2) != 0) { if (*((short*)x1) != *((short*)x2)) return false; x1 += 2; x2 += 2; }
                    if ((l & 1) != 0) if (*x1 != *x2) return false;
                    return true;
                }
            }

            bool IsMatch(IReadOnlyList<Item> a, IReadOnlyList<Item> b)
            {
                for (int i = 0, count = a.Count; i < count; i++)
                    if (!a[i].IsMatch(b[i]))
                        return false;
                return true;
            }
		}

		public override string ToString()
		{
			var sb = new StringBuilder(Format.GetName()).Append('[');
			switch (Format)
			{
				case SecsFormat.List:
					sb.Append(Unsafe.As<IReadOnlyList<Item>>(_values).Count).Append("]: ...");
					break;
				case SecsFormat.ASCII:
				case SecsFormat.JIS8:
					sb.Append(Unsafe.As<string>(_values).Length).Append("]: ").Append(Unsafe.As<string>(_values));
					break;
				case SecsFormat.Binary:
					var arr = Unsafe.As<byte[]>(_values);
					sb.Append(arr.Length).Append("]: ").AppendHexString(arr);
					break;
				case SecsFormat.Boolean: sb.AppendItemValues<bool>(_values); break;
				case SecsFormat.I1: sb.AppendItemValues<sbyte>(_values); break;
				case SecsFormat.I2: sb.AppendItemValues<short>(_values); break;
				case SecsFormat.I4: sb.AppendItemValues<int>(_values); break;
				case SecsFormat.I8: sb.AppendItemValues<long>(_values); break;
				case SecsFormat.U1: sb.AppendItemValues<byte>(_values); break;
				case SecsFormat.U2: sb.AppendItemValues<ushort>(_values); break;
				case SecsFormat.U4: sb.AppendItemValues<uint>(_values); break;
				case SecsFormat.U8: sb.AppendItemValues<ulong>(_values); break;
				case SecsFormat.F4: sb.AppendItemValues<float>(_values); break;
				case SecsFormat.F8: sb.AppendItemValues<double>(_values); break;
			}
			return sb.ToString();
		}

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

		internal static Item L(IList<Item> items) => items.Count > 0 ? new Item(SecsFormat.List, new ReadOnlyCollection<Item>(items), ListEncoder) : L();        public static Item L(IEnumerable<Item> items) => L(items.ToList());
        public static Item L(params Item[] items) => L((IList<Item>) items);

        public static Item B(params byte[] value) => value.Length > 0 ? new Item(SecsFormat.Binary, value, GetEncoder<byte>()) : B();
        public static Item B(IEnumerable<byte> value) => B(value.ToArray());

        public static Item U1(params byte[] value) => value.Length > 0 ? new Item(SecsFormat.U1, value, GetEncoder<byte>()) : U1();
        public static Item U1(IEnumerable<byte> value) => U1(value.ToArray());

        public static Item U2(params ushort[] value) => value.Length > 0 ? new Item(SecsFormat.U2, value, GetEncoder<ushort>()) : U2();
        public static Item U2(IEnumerable<ushort> value) => U2(value.ToArray());

        public static Item U4(params uint[] value) => value.Length > 0 ? new Item(SecsFormat.U4, value, GetEncoder<uint>()) : U4();
        public static Item U4(IEnumerable<uint> value) => U4(value.ToArray());

        public static Item U8(params ulong[] value) => value.Length > 0 ? new Item(SecsFormat.U8, value, GetEncoder<ulong>()) : U8();
        public static Item U8(IEnumerable<ulong> value) => U8(value.ToArray());

        public static Item I1(params sbyte[] value) => value.Length > 0 ? new Item(SecsFormat.I1, value, GetEncoder<sbyte>()) : I1();
        public static Item I1(IEnumerable<sbyte> value) => I1(value.ToArray());

        public static Item I2(params short[] value) => value.Length > 0 ? new Item(SecsFormat.I2, value, GetEncoder<short>()) : I2();
        public static Item I2(IEnumerable<short> value) => I2(value.ToArray());

        public static Item I4(params int[] value) => value.Length > 0 ? new Item(SecsFormat.I4, value, GetEncoder<int>()) : I4();
        public static Item I4(IEnumerable<int> value) => I4(value.ToArray());

        public static Item I8(params long[] value) => value.Length > 0 ? new Item(SecsFormat.I8, value, GetEncoder<long>()) : I8();
        public static Item I8(IEnumerable<long> value) => I8(value.ToArray());

        public static Item F4(params float[] value) => value.Length > 0 ? new Item(SecsFormat.F4, value, GetEncoder<float>()) : F4();
        public static Item F4(IEnumerable<float> value) => F4(value.ToArray());

        public static Item F8(params double[] value) => value.Length > 0 ? new Item(SecsFormat.F8, value, GetEncoder<double>()) : F8();
        public static Item F8(IEnumerable<double> value) => F8(value.ToArray());

        public static Item Boolean(params bool[] value) => value.Length > 0 ? new Item(SecsFormat.Boolean, value, GetEncoder<bool>()) : Boolean();
        public static Item Boolean(IEnumerable<bool> value) => Boolean(value.ToArray());

		public static Item A(string value) => string.IsNullOrEmpty(value) ? A() : new Item(SecsFormat.ASCII, value, StringEncoder);

		public static Item J(string value) => string.IsNullOrEmpty(value) ? J() : new Item(SecsFormat.JIS8, value, StringEncoder);
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

		private static readonly Item EmptyL = L(Array.Empty<Item>());
        private static readonly Item EmptyA = new Item(SecsFormat.ASCII, string.Empty, StringEncoder);
		private static readonly Item EmptyJ = new Item(SecsFormat.JIS8, string.Empty, StringEncoder);
		private static readonly Item EmptyBoolean = new Item(SecsFormat.Boolean, Array.Empty<bool>(), GetEncoder<bool>());
		private static readonly Item EmptyBinary = new Item(SecsFormat.Binary, Array.Empty<byte>(), GetEncoder<byte>());
		private static readonly Item EmptyU1 = new Item(SecsFormat.U1, Array.Empty<byte>(), GetEncoder<byte>());
		private static readonly Item EmptyU2 = new Item(SecsFormat.U2, Array.Empty<ushort>(), GetEncoder<ushort>());
		private static readonly Item EmptyU4 = new Item(SecsFormat.U4, Array.Empty<uint>(), GetEncoder<uint>());
        private static readonly Item EmptyU8 = new Item(SecsFormat.U8, Array.Empty<ulong>(), GetEncoder<ulong>());
        private static readonly Item EmptyI1 = new Item(SecsFormat.I1, Array.Empty<sbyte>(), GetEncoder<sbyte>());
        private static readonly Item EmptyI2 = new Item(SecsFormat.I2, Array.Empty<short>(), GetEncoder<short>());
        private static readonly Item EmptyI4 = new Item(SecsFormat.I4, Array.Empty<int>(), GetEncoder<int>());
        private static readonly Item EmptyI8 = new Item(SecsFormat.I8, Array.Empty<long>(), GetEncoder<long>());
        private static readonly Item EmptyF4 = new Item(SecsFormat.F4, Array.Empty<float>(), GetEncoder<float>());
        private static readonly Item EmptyF8 = new Item(SecsFormat.F8, Array.Empty<double>(), GetEncoder<double>());

        private static readonly Encoding Jis8Encoding = Encoding.GetEncoding(50222);
		#endregion

		/// <summary>
		/// Encode item to raw data buffer
		/// </summary>
		/// <param name="buffer"></param>
		/// <returns></returns>
		internal int EncodeTo(Span<byte> buffer)
		{
			var length = _encoder(this, buffer);
			if (Format == SecsFormat.List)
				foreach (var subItem in Items)
					length += subItem.EncodeTo(buffer.Slice(length));
			return length;
		}

		private static unsafe int EncodeItemHeader(SecsFormat format, int dataLength, Span<byte> buffer)
        {
			Span<byte> lengthBytes = stackalloc byte[4];
			MemoryMarshal.Write(lengthBytes, ref dataLength);
            if (dataLength <= 0xff)
            {//	1 byte
				buffer[0] = (byte)((int)format | 0b_01);
				buffer[1] = lengthBytes[0];
				return 2;
            }
            if (dataLength <= 0xff_ff)
            {//	2 byte
				buffer[0] = (byte)((byte)format | 0b_10);
				buffer[1] = lengthBytes[1];
				buffer[2] = lengthBytes[0];
				return 3;
			}
            if (dataLength <= 0xff_ff_ff)
            {//	3 byte
				buffer[0] = (byte)((byte)format | 0b_11);
				buffer[1] = lengthBytes[2];
				buffer[2] = lengthBytes[1];
				buffer[3] = lengthBytes[0];
				return 4;
			}
			throw new ArgumentOutOfRangeException(nameof(dataLength), dataLength, $"Item data length:{dataLength} is overflow");
        }

        internal static Item BytesDecode(in SecsFormat format, in byte[] data, in int index, in int length)
        {
            switch (format)
            {
                case SecsFormat.ASCII: return length == 0 ? A() : A(Encoding.ASCII.GetString(data, index, length));
                case SecsFormat.JIS8: return length == 0 ? J() : J(Jis8Encoding.GetString(data, index, length));
                case SecsFormat.Boolean: return length == 0 ? Boolean() : Boolean(Decode<bool>(data, index, length));
                case SecsFormat.Binary: return length == 0 ? B() : B(Decode<byte>(data, index, length));
                case SecsFormat.U1: return length == 0 ? U1() : U1(Decode<byte>(data, index, length));
                case SecsFormat.U2: return length == 0 ? U2() : U2(Decode<ushort>(data, index, length));
                case SecsFormat.U4: return length == 0 ? U4() : U4(Decode<uint>(data, index, length));
                case SecsFormat.U8: return length == 0 ? U8() : U8(Decode<ulong>(data, index, length));
                case SecsFormat.I1: return length == 0 ? I1() : I1(Decode<sbyte>(data, index, length));
                case SecsFormat.I2: return length == 0 ? I2() : I2(Decode<short>(data, index, length));
                case SecsFormat.I4: return length == 0 ? I4() : I4(Decode<int>(data, index, length));
                case SecsFormat.I8: return length == 0 ? I8() : I8(Decode<long>(data, index, length));
                case SecsFormat.F4: return length == 0 ? F4() : F4(Decode<float>(data, index, length));
                case SecsFormat.F8: return length == 0 ? F8() : F8(Decode<double>(data, index, length));
                default: throw new ArgumentException("Invalid format", nameof(format));
            }

            T[] Decode<T>(byte[] data2, in int index2, in int length2) where T : struct
            {
                var elmSize = Unsafe.SizeOf<T>();
                data2.Reverse(index2, index2 + length2, elmSize);
                var values = new T[length2 / elmSize];
                Buffer.BlockCopy(data2, index2, values, 0, length2);
                return values;
            }
        }
	}
}