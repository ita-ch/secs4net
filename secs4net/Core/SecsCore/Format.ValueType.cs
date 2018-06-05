using System;
using System.Collections.Generic;
using System.Linq;

namespace Secs4Net
{
    public abstract class ValueTypeFormat<TFormat, TValue> : IFormat<TValue>
       where TFormat : IFormat<TValue>
       where TValue : struct
    {
        private static readonly Pool<ValueItem<TFormat, TValue>> ValueItemPool
            = new Pool<ValueItem<TFormat, TValue>>(p => new ValueItem<TFormat, TValue>(p));

        public static readonly SecsItem Empty = new ValueItem<TFormat, TValue>();

        internal ValueTypeFormat()
        {
        }

        /// <summary>
        /// Create <typeparamref name="TValue"/> item
        /// </summary>
        /// <param name="value">dynamic allocated <typeparamref name="TValue"/> collection</param>
        /// <returns></returns>
        public static SecsItem Create(IEnumerable<TValue> value)
        {
            var arr = value as TValue[] ?? value.ToArray();
            return arr.Length == 0 ? Empty : Create(arr);
        }

        /// <summary>
        /// Create <typeparamref name="TValue"/> item
        /// </summary>
        /// <param name="value">dynamic allocated <typeparamref name="TValue"/> array</param>
        /// <returns></returns>
        public static SecsItem Create(TValue[] value)
        {
            var item = ValueItemPool.Rent();
            item.SetValues(new ArraySegment<TValue>(value), fromPool: false);
            return item;
        }

        /// <summary>
        /// Create <typeparamref name="TValue"/> item
        /// </summary>
        /// <param name="value"><typeparamref name="TValue"/> item from pool</param>
        /// <returns></returns>
        internal static SecsItem Create(ArraySegment<TValue> value)
        {
            var item = ValueItemPool.Rent();
            item.SetValues(value, fromPool: true);
            return item;
        }
    }

    public sealed class BooleanFormat : ValueTypeFormat<BooleanFormat, bool>
    {
        public const SecsFormat Format = SecsFormat.Boolean;
    }

    public sealed class BinaryFormat : ValueTypeFormat<BinaryFormat, byte>
    {
        public const SecsFormat Format = SecsFormat.Binary;
    }

    public sealed class F4Format : ValueTypeFormat<F4Format, float>
    {
        public const SecsFormat Format = SecsFormat.F4;
    }

    public sealed class F8Format : ValueTypeFormat<F8Format, double>
    {
        public const SecsFormat Format = SecsFormat.F8;
    }

    public sealed class I1Format : ValueTypeFormat<I1Format, sbyte>
    {
        public const SecsFormat Format = SecsFormat.I1;
    }

    public sealed class I2Format : ValueTypeFormat<I2Format, short>
    {
        public const SecsFormat Format = SecsFormat.I2;
    }

    public sealed class I4Format : ValueTypeFormat<I4Format, int>
    {
        public const SecsFormat Format = SecsFormat.I4;
    }

    public sealed class I8Format : ValueTypeFormat<I8Format, long>
    {
        public const SecsFormat Format = SecsFormat.I8;
    }

    public sealed class U1Format : ValueTypeFormat<U1Format, byte>
    {
        public const SecsFormat Format = SecsFormat.U1;
    }

    public sealed class U2Format : ValueTypeFormat<U2Format, ushort>
    {
        public const SecsFormat Format = SecsFormat.U2;
    }

    public sealed class U4Format : ValueTypeFormat<U4Format, uint>
    {
        public const SecsFormat Format = SecsFormat.U4;
    }

    public sealed class U8Format : ValueTypeFormat<U8Format, ulong>
    {
        public const SecsFormat Format = SecsFormat.U8;
    }
}
