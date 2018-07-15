using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using static Secs4Net.Item;

namespace Secs4Net.Sml
{
    static partial class SmlReaderExtensions
    {
        delegate T Parser<T>(ReadOnlySpan<char> span, NumberStyles style = NumberStyles.Integer, IFormatProvider provider = null);

        public static string LineBreak { get; set; } = Environment.NewLine;

        public static SecsMessage ToSecsMessage(this string str)
        {
            return str.AsSpan().ToSecsMessage();
        }

        public static SecsMessage ToSecsMessage(this ReadOnlySpan<char> str)
        {
            var result = GetLine(str);
            var line = result.Line;

            #region Parse First Line

            int i = line.IndexOf(':');

            var name = line.Slice(0, i).Trim().ToString();
            line = line.Slice(i);

            i = line.IndexOf("'S", StringComparison.Ordinal) + 2;
            int j = line.IndexOf('F');
            var s = byte.Parse(line.Slice(i, j - i));
            i = line.LastIndexOf('\'');
            var f = byte.Parse(line.Slice(j + 1, i - (j + 1)));

            var replyExpected = line.Slice(i).LastIndexOf('W') != -1;

            #endregion

            Item rootItem = null;
            var stack = new Stack<List<Item>>();
            while (true)
            {
                result = GetLine(result.Rest);
                line = result.Line;
                if (line.IsEmpty || !ParseItem(line, stack, ref rootItem))
                    break;
            }

            return new SecsMessage(s, f, name, rootItem, replyExpected);
        }

        ref struct LineResult
        {
            public ReadOnlySpan<char> Line;
            public ReadOnlySpan<char> Rest;
        }

        static LineResult GetLine(in ReadOnlySpan<char> str)
        {
            var i = str.IndexOf(LineBreak);
            if (i != -1)
            {
                return new LineResult
                {
                    Line = str.Slice(0, i),
                    Rest = str.Slice(i + LineBreak.Length)
                };
            }
            return default;
        }

        private static bool ParseItem(ReadOnlySpan<char> line, Stack<List<Item>> stack, ref Item rootSecsItem)
        {
            line = line.TrimStart();

            if (line[0] == '.')
                return false;

            if (line[0] == '>')
            {
                var itemList = stack.Pop();
                var item = itemList.Count > 0 ? L(itemList) : L();
                if (stack.Count > 0)
                    stack.Peek()
                         .Add(item);
                else
                    rootSecsItem = item;
                return true;
            }

            // <format[count] smlValue

            int indexItemL = line.IndexOf('<') + 1;
            Debug.Assert(indexItemL != 0);
            int indexSizeL = line.IndexOf('[');
            Debug.Assert(indexSizeL != -1);
            var format = line.Slice(indexItemL, indexSizeL - indexItemL).Trim();

            if (format.Length == 1 && format[0] == 'L')
            {
                stack.Push(new List<Item>());
            }
            else
            {
                int indexSizeR = line.IndexOf(']');
                Debug.Assert(indexSizeR != -1);
                int indexItemR = line.LastIndexOf('>');
                Debug.Assert(indexItemR != -1);
                var valueStr = line.Slice(indexSizeR + 1, indexItemR - indexSizeR - 1).TrimStart();

                var item = Create(format, valueStr);

                if (stack.Count > 0)
                {
                    stack.Peek()
                         .Add(item);
                }
                else
                {
                    rootSecsItem = item;
                }
            }

            return true;
        }

        private static byte HexByteParser(ReadOnlySpan<char> str, NumberStyles style = NumberStyles.Integer, IFormatProvider provider = null) =>
            byte.Parse(str.Slice(2), NumberStyles.AllowHexSpecifier);

        private static readonly (Func<Item>, Func<byte[], Item>, Parser<byte>)
            BinaryParser = (B, B, HexByteParser);
        private static readonly (Func<Item>, Func<sbyte[], Item>, Parser<sbyte>)
            I1Parser = (I1, I1, sbyte.Parse);
        private static readonly (Func<Item>, Func<short[], Item>, Parser<short>)
            I2Parser = (I2, I2, short.Parse);
        private static readonly (Func<Item>, Func<int[], Item>, Parser<int>)
            I4Parser = (I4, I4, int.Parse);
        private static readonly (Func<Item>, Func<long[], Item>, Parser<long>)
            I8Parser = (I8, I8, long.Parse);
        private static readonly (Func<Item>, Func<byte[], Item>, Parser<byte>)
            U1Parser = (U1, U1, byte.Parse);
        private static readonly (Func<Item>, Func<ushort[], Item>, Parser<ushort>)
            U2Parser = (U2, U2, ushort.Parse);
        private static readonly (Func<Item>, Func<uint[], Item>, Parser<uint>)
            U4Parser = (U4, U4, uint.Parse);
        private static readonly (Func<Item>, Func<ulong[], Item>, Parser<ulong>)
            U8Parser = (U8, U8, ulong.Parse);
        private static readonly (Func<Item>, Func<float[], Item>, Parser<float>)
            F4Parser = (F4, F4, float.Parse);
        private static readonly (Func<Item>, Func<double[], Item>, Parser<double>)
            F8Parser = (F8, F8, double.Parse);
        private static readonly (Func<Item>, Func<bool[], Item>, Parser<bool>)
            BoolParser = (Boolean, Boolean, (span, _, __) => bool.Parse(span));
        private static readonly (Func<Item>, Func<string, Item>)
            AParser = (A, A);
        private static readonly (Func<Item>, Func<string, Item>)
            JParser = (J, J);

        internal static Item Create(this in ReadOnlySpan<char> format, in ReadOnlySpan<char> smlValue)
        {
            if (format.Equals("A", StringComparison.Ordinal))
            {
                return ParseStringItem(smlValue, AParser);
            }
            else if (format.Equals("JIS8", StringComparison.Ordinal) || format.Equals("J", StringComparison.Ordinal))
            {
                return ParseStringItem(smlValue, JParser);
            }
            else if (format.Equals("Bool", StringComparison.Ordinal) || format.Equals("Boolean", StringComparison.Ordinal))
            {
                return ParseValueItem(smlValue, BoolParser);
            }
            else if (format.Equals("Binary", StringComparison.Ordinal) || format.Equals("B", StringComparison.Ordinal))
            {
                return ParseValueItem(smlValue, BinaryParser);
            }
            else if (format.Equals("I1", StringComparison.Ordinal))
            {
                return ParseValueItem(smlValue, I1Parser);
            }
            else if (format.Equals("I2", StringComparison.Ordinal))
            {
                return ParseValueItem(smlValue, I2Parser);
            }
            else if (format.Equals("I4", StringComparison.Ordinal))
            {
                return ParseValueItem(smlValue, I4Parser);
            }
            else if (format.Equals("I8", StringComparison.Ordinal))
            {
                return ParseValueItem(smlValue, I8Parser);
            }
            else if (format.Equals("U1", StringComparison.Ordinal))
            {
                return ParseValueItem(smlValue, U1Parser);
            }
            else if (format.Equals("U2", StringComparison.Ordinal))
            {
                return ParseValueItem(smlValue, U2Parser);
            }
            else if (format.Equals("U4", StringComparison.Ordinal))
            {
                return ParseValueItem(smlValue, U4Parser);
            }
            else if (format.Equals("U8", StringComparison.Ordinal))
            {
                return ParseValueItem(smlValue, U8Parser);
            }
            else if (format.Equals("F4", StringComparison.Ordinal))
            {
                return ParseValueItem(smlValue, F4Parser);
            }
            else if (format.Equals("F8", StringComparison.Ordinal))
            {
                return ParseValueItem(smlValue, F8Parser);
            }
            else if (format.Equals("L", StringComparison.Ordinal))
            {
                throw new SecsException("Please use Item.L(...) to create list item.");
            }
            else
            {
                throw new SecsException("Unknown SML format :" + format.ToString());
            }

            Item ParseValueItem<T>(in ReadOnlySpan<char> valueString, (Func<Item> emptyCreator, Func<T[], Item> creator, Parser<T> converter) parser)
            {
                return valueString.IsEmpty
                    ? parser.emptyCreator()
                    : parser.creator(GetValues(valueString, parser.converter));

                T[] GetValues(ReadOnlySpan<char> values, Parser<T> converter)
                {
                    var result = new List<T>();
                    do
                    {
                        int indexOfSeparator = values.IndexOf(' ');

                        var v = indexOfSeparator == -1
                            ? values
                            : values.Slice(0, indexOfSeparator);

                        if (!v.IsWhiteSpace())
                        {
                            result.Add(converter(v));
                        }

                        values = values.Slice(indexOfSeparator + 1).TrimStart();
                    } while (!values.IsEmpty);
                    return result.ToArray();
                }
            }

            Item ParseStringItem(in ReadOnlySpan<char> str, (Func<Item> emptyCreator, Func<string, Item> creator) parser)
            {
                var value = str.TrimStart().TrimStart('"');

                var index = value.IndexOf('"');
                if (index == 0)
                {
                    return parser.emptyCreator();
                }

                return parser.creator(value.Slice(0, index).ToString());
            }
        }

        public static Item Create(this SecsFormat format, string smlValue) =>
            Create(format.ToSml(), smlValue);
    }
}
