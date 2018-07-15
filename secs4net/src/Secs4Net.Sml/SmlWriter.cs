using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Secs4Net.Sml
{
    public static partial class SmlReaderExtensions
    {
        public static string ToSml(this SecsMessage msg)
        {
            if (msg is null)
                return null;

            using (var sw = new StringWriter(CultureInfo.InvariantCulture))
            {
                msg.WriteTo(sw);
                return sw.ToString();
            }
        }

        public static void WriteTo(this SecsMessage msg, TextWriter writer, int indent = 4)
        {
            if (msg is null)
                return;

            writer.WriteLine(msg.ToString());
            if (msg.SecsItem != null)
                msg.SecsItem.Write(writer, indent);
            writer.Write('.');
        }

        public static async Task WriteToAsync(this SecsMessage msg, TextWriter writer, int indent = 4)
        {
            if (msg is null)
                return;

            await writer.WriteLineAsync(msg.ToString());
            if (msg.SecsItem != null)
                await WriteAsync(writer, msg.SecsItem, indent);
            await writer.WriteAsync('.');
        }

        public static void Write(this Item item, TextWriter writer, int indent = 4)
        {
            var indentStr = new string(' ', indent);
            writer.Write(indentStr);
            writer.Write('<');
            writer.Write(item.Format.ToSml());
            writer.Write(" [");
            writer.Write(item.Count);
            writer.Write("] ");
            switch (item.Format)
            {
                case SecsFormat.List:
                    writer.WriteLine();
                    var items = item.Items;
                    for (int i = 0, count = items.Count; i < count; i++)
                        items[i].Write(writer, indent << 1);
                    writer.Write(indentStr);
                    break;
                case SecsFormat.ASCII:
                case SecsFormat.JIS8:
                    writer.Write('\'');
                    writer.Write(item.GetString());
                    writer.Write('\'');
                    break;
                case SecsFormat.Binary:
                    writer.Write(item.GetValues<byte>().ToHexString());
                    break;
                case SecsFormat.F4:
                    writer.Write(string.Join(" ", item.GetValues<float>()));
                    break;
                case SecsFormat.F8:
                    writer.Write(string.Join(" ", item.GetValues<double>()));
                    break;
                case SecsFormat.I1:
                    writer.Write(string.Join(" ", item.GetValues<sbyte>()));
                    break;
                case SecsFormat.I2:
                    writer.Write(string.Join(" ", item.GetValues<short>()));
                    break;
                case SecsFormat.I4:
                    writer.Write(string.Join(" ", item.GetValues<int>()));
                    break;
                case SecsFormat.I8:
                    writer.Write(string.Join(" ", item.GetValues<long>()));
                    break;
                case SecsFormat.U1:
                    writer.Write(string.Join(" ", item.GetValues<byte>()));
                    break;
                case SecsFormat.U2:
                    writer.Write(string.Join(" ", item.GetValues<ushort>()));
                    break;
                case SecsFormat.U4:
                    writer.Write(string.Join(" ", item.GetValues<uint>()));
                    break;
                case SecsFormat.U8:
                    writer.Write(string.Join(" ", item.GetValues<ulong>()));
                    break;
                case SecsFormat.Boolean:
                    writer.Write(string.Join(" ", item.GetValues<bool>()));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(item.Format), item.Format, "invalid SecsFormat value");
            }
            writer.WriteLine('>');
        }

        public static async Task WriteAsync(TextWriter writer, Item item, int indent = 4)
        {
            var indentStr = new string(' ', indent);
            await writer.WriteAsync(indentStr).ConfigureAwait(false);
            await writer.WriteAsync($"<{item.Format.ToSml()} [{item.Count}] ").ConfigureAwait(false);
            await WriteItemAcyn();
            await writer.WriteLineAsync('>').ConfigureAwait(false);

            Task WriteItemAcyn()
            {
                switch (item.Format)
                {
                    case SecsFormat.List: return WriteListAsnc(writer, item, indent, indentStr);
                    case SecsFormat.ASCII:
                    case SecsFormat.JIS8: return writer.WriteAsync($"'{item.GetString()}'");
                    case SecsFormat.Binary: return writer.WriteAsync(item.GetValues<byte>().ToHexString());
                    case SecsFormat.F4: return writer.WriteAsync(string.Join(" ", item.GetValues<float>()));
                    case SecsFormat.F8: return writer.WriteAsync(string.Join(" ", item.GetValues<double>()));
                    case SecsFormat.I1: return writer.WriteAsync(string.Join(" ", item.GetValues<sbyte>()));
                    case SecsFormat.I2: return writer.WriteAsync(string.Join(" ", item.GetValues<short>()));
                    case SecsFormat.I4: return writer.WriteAsync(string.Join(" ", item.GetValues<int>()));
                    case SecsFormat.I8: return writer.WriteAsync(string.Join(" ", item.GetValues<long>()));
                    case SecsFormat.U1: return writer.WriteAsync(string.Join(" ", item.GetValues<byte>()));
                    case SecsFormat.U2: return writer.WriteAsync(string.Join(" ", item.GetValues<ushort>()));
                    case SecsFormat.U4: return writer.WriteAsync(string.Join(" ", item.GetValues<uint>()));
                    case SecsFormat.U8: return writer.WriteAsync(string.Join(" ", item.GetValues<ulong>()));
                    case SecsFormat.Boolean: return writer.WriteAsync(string.Join(" ", item.GetValues<bool>()));
                    default: throw new ArgumentOutOfRangeException($"{nameof(item)}.{nameof(item.Format)}", item.Format, "Invalid enum value");
                }

                async Task WriteListAsnc(TextWriter w, Item secsItem, int d, string dStr)
                {
                    await w.WriteLineAsync().ConfigureAwait(false);
                    var items = secsItem.Items;
                    for (int i = 0, count = items.Count; i < count; i++)
                        await WriteAsync(writer, items[i], d << 1).ConfigureAwait(false);
                    await writer.WriteAsync(dStr).ConfigureAwait(false);
                }
            }
        }

        public static string ToSml(this SecsFormat format)
        {
            switch (format)
            {
                case SecsFormat.List: return "L";
                case SecsFormat.Binary: return "B";
                case SecsFormat.Boolean: return "Boolean";
                case SecsFormat.ASCII: return "A";
                case SecsFormat.JIS8: return "J";
                case SecsFormat.I8: return "I8";
                case SecsFormat.I1: return "I1";
                case SecsFormat.I2: return "I2";
                case SecsFormat.I4: return "I4";
                case SecsFormat.F8: return "F8";
                case SecsFormat.F4: return "F4";
                case SecsFormat.U8: return "U8";
                case SecsFormat.U1: return "U1";
                case SecsFormat.U2: return "U2";
                case SecsFormat.U4: return "U4";
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), (int)format, "Invalid enum value");
            }
        }
    }
}
