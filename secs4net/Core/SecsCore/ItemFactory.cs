using System;
using System.Collections.Generic;

namespace Secs4Net
{
    public static class Item
    {
        public static SecsItem L() => ListFormat.Empty;
        public static SecsItem L(IEnumerable<SecsItem> items) => ListFormat.Create(items);
        public static SecsItem L(params SecsItem[] secsItems) => ListFormat.Create(secsItems);
        
        public static SecsItem B() => BinaryFormat.Empty;
        public static SecsItem B(IEnumerable<byte> value) => BinaryFormat.Create(value);
        public static SecsItem B(params byte[] value) => BinaryFormat.Create(value);
        public static SecsItem B(ArraySegment<byte> value) => BinaryFormat.Create(value);
        
        public static SecsItem U1() => U1Format.Empty;
        public static SecsItem U1(IEnumerable<byte> value) => U1Format.Create(value);
        public static SecsItem U1(params byte[] value) => U1Format.Create(value);
        public static SecsItem U1(ArraySegment<byte> value) => U1Format.Create(value);
        
        public static SecsItem U2() => U2Format.Empty;
        public static SecsItem U2(IEnumerable<ushort> value) => U2Format.Create(value);
        public static SecsItem U2(params ushort[] value) => U2Format.Create(value);
        public static SecsItem U2(ArraySegment<ushort> value) => U2Format.Create(value);
        
        public static SecsItem U4() => U4Format.Empty;
        public static SecsItem U4(IEnumerable<uint> value) => U4Format.Create(value);
        public static SecsItem U4(params uint[] value) => U4Format.Create(value);
        public static SecsItem U4(ArraySegment<uint> value) => U4Format.Create(value);
        
        public static SecsItem U8() => U8Format.Empty;
        public static SecsItem U8(IEnumerable<ulong> value) => U8Format.Create(value);
        public static SecsItem U8(params ulong[] value) => U8Format.Create(value);
        public static SecsItem U8(ArraySegment<ulong> value) => U8Format.Create(value);
        
        public static SecsItem I1() => I1Format.Empty;
        public static SecsItem I1(IEnumerable<sbyte> value) => I1Format.Create(value);
        public static SecsItem I1(params sbyte[] value) => I1Format.Create(value);
        public static SecsItem I1(ArraySegment<sbyte> value) => I1Format.Create(value);
        
        public static SecsItem I2() => I2Format.Empty;
        public static SecsItem I2(IEnumerable<short> value) => I2Format.Create(value);
        public static SecsItem I2(params short[] value) => I2Format.Create(value);
        public static SecsItem I2(ArraySegment<short> value) => I2Format.Create(value);
        
        public static SecsItem I4() => I4Format.Empty;
        public static SecsItem I4(IEnumerable<int> value) => I4Format.Create(value);
        public static SecsItem I4(params int[] value) => I4Format.Create(value);
        public static SecsItem I4(ArraySegment<int> value) => I4Format.Create(value);
        
        public static SecsItem I8() => I8Format.Empty;
        public static SecsItem I8(IEnumerable<long> value) => I8Format.Create(value);
        public static SecsItem I8(params long[] value) => I8Format.Create(value);
        public static SecsItem I8(ArraySegment<long> value) => I8Format.Create(value);
        
        public static SecsItem F4() => F4Format.Empty;
        public static SecsItem F4(IEnumerable<float> value) => F4Format.Create(value);
        public static SecsItem F4(params float[] value) => F4Format.Create(value);
        public static SecsItem F4(ArraySegment<float> value) => F4Format.Create(value);
        
        public static SecsItem F8() => F8Format.Empty;
        public static SecsItem F8(IEnumerable<double> value) => F8Format.Create(value);
        public static SecsItem F8(params double[] value) => F8Format.Create(value);
        public static SecsItem F8(ArraySegment<double> value) => F8Format.Create(value);
        
        public static SecsItem Boolean() => BooleanFormat.Empty;
        public static SecsItem Boolean(IEnumerable<bool> value) => BooleanFormat.Create(value);
        public static SecsItem Boolean(params bool[] value) => BooleanFormat.Create(value);
        internal static SecsItem Boolean(ArraySegment<bool> value) => BooleanFormat.Create(value);
        
        public static SecsItem A() => ASCIIFormat.Empty;
        public static SecsItem A(string value) => ASCIIFormat.Create(value);

        public static SecsItem J() => JIS8Format.Empty;
        public static SecsItem J(string value) => JIS8Format.Create(value);
    }
}
