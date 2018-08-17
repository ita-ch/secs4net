using System;
using System.Collections.Generic;

namespace Secs4Net
{
    using static SecsFormat;
    public static class SecsExtension
    {
        private static readonly IReadOnlyDictionary<SecsFormat, byte> SizeOfMap =
            new Dictionary<SecsFormat, byte>(16){
                [List   ] = 0,
                [Binary ] = 1,
                [Boolean] = 1,
                [ASCII  ] = 1,
                [JIS8   ] = 1,
                [I8     ] = 8,
                [I1     ] = 1,
                [I2     ] = 2,
                [I4     ] = 4,
                [F8     ] = 8,
                [F4     ] = 4,
                [U8     ] = 8,
                [U1     ] = 1,
                [U2     ] = 2,
                [U4     ] = 4,
            };

        public static int SizeOf(this SecsFormat format)
        {
            return SizeOfMap[format];
        }

        public static string GetName(this SecsFormat format)
        {
            switch (format)
            {
                case List: return nameof(List);
                case ASCII: return nameof(ASCII);
                case JIS8: return nameof(JIS8);
                case Boolean: return nameof(Boolean);
                case Binary: return nameof(Binary);
                case U1: return nameof(U1);
                case U2: return nameof(U2);
                case U4: return nameof(U4);
                case U8: return nameof(U8);
                case I1: return nameof(I1);
                case I2: return nameof(I2);
                case I4: return nameof(I4);
                case I8: return nameof(I8);
                case F4: return nameof(F4);
                case F8: return nameof(F8);
                default: throw new ArgumentOutOfRangeException(nameof(format), (int)format, @"Invalid enum value");
            }
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
    }
}