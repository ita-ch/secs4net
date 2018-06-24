using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Secs4Net
{
    public static class SecsExtension
    {
        public static string GetName(this SecsFormat format)
        {
            switch (format)
            {
                case SecsFormat.List: return nameof(SecsFormat.List);
                case SecsFormat.ASCII: return nameof(SecsFormat.ASCII);
                case SecsFormat.JIS8: return nameof(SecsFormat.JIS8);
                case SecsFormat.Boolean: return nameof(SecsFormat.Boolean);
                case SecsFormat.Binary: return nameof(SecsFormat.Binary);
                case SecsFormat.U1: return nameof(SecsFormat.U1);
                case SecsFormat.U2: return nameof(SecsFormat.U2);
                case SecsFormat.U4: return nameof(SecsFormat.U4);
                case SecsFormat.U8: return nameof(SecsFormat.U8);
                case SecsFormat.I1: return nameof(SecsFormat.I1);
                case SecsFormat.I2: return nameof(SecsFormat.I2);
                case SecsFormat.I4: return nameof(SecsFormat.I4);
                case SecsFormat.I8: return nameof(SecsFormat.I8);
                case SecsFormat.F4: return nameof(SecsFormat.F4);
                case SecsFormat.F8: return nameof(SecsFormat.F8);
                default: throw new ArgumentOutOfRangeException(nameof(format), (int)format, @"Invalid enum value");
            }
        }

        public static void AppendHexString(this StringBuilder sb, in ReadOnlySpan<byte> value)
        {
			if (value.Length == 0)
				return;

            var length = value.Length * 3;
            Span<char> chs = stackalloc char[length];
            for (int ci = 0, i = 0; ci < length; ci += 3)
            {
                var num = value[i++];
                chs[ci] = GetHexValue(num / 0x10);
                chs[ci + 1] = GetHexValue(num % 0x10);
                chs[ci + 2] = ' ';
            }
			sb.Append(chs.Slice(0, length - 1));

            char GetHexValue(int i) => (i < 10) ? (char)(i + 0x30) : (char)((i - 10) + 0x41);
        }

		internal static void AppendItemValues<T>(this StringBuilder b, object src) where T : unmanaged
		{
			var arr = Unsafe.As<T[]>(src);
			b.Append(arr.Length).Append("]: ").AppendJoin(' ', arr);
		}


		public static bool IsMatch(this SecsMessage src, in SecsMessage target)
        {
            return src.S == target.S && src.F == target.F &&
                   (target.SecsItem == null || src.SecsItem.IsMatch(target.SecsItem));
        }

        internal static void Reverse(this byte[] bytes, in int begin, in int end, in int offSet)
        {
            if (offSet <= 1) return;
            for (var i = begin; i < end; i += offSet)
                Array.Reverse(bytes, i, offSet);
        }

		public static void ReverseByOffset(this Span<byte> span, int offset)
		{
			if (offset <= 1)
			{
				return;
			}

			ref var spanFirst = ref MemoryMarshal.GetReference(span);
			ref var spanLast = ref Unsafe.Add(ref Unsafe.Add(ref spanFirst, span.Length), -1);

			do
			{
				ref var offsetLast = ref Unsafe.Add(ref spanFirst, offset - 1);
				do
				{
					var temp = spanFirst;
					spanFirst = offsetLast;
					offsetLast = temp;
					spanFirst = ref Unsafe.Add(ref spanFirst, 1);
					offsetLast = ref Unsafe.Add(ref offsetLast, -1);
				} while (Unsafe.IsAddressLessThan(ref spanFirst, ref offsetLast));
			} while (Unsafe.IsAddressLessThan(ref spanFirst, ref spanLast));
		}
	}
}