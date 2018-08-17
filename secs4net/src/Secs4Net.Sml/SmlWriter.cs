using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Secs4Net.Sml
{
    public static partial class SmlWriterExtensions
    {
        private static readonly MemoryPool<byte> pool = MemoryPool<byte>.Shared;
        private static readonly Encoding encoding = Encoding.UTF8;
        private static readonly byte[] NewLineBytes = encoding.GetBytes(Environment.NewLine);
        private static readonly byte[] EndOfMessageBytes = encoding.GetBytes(".");
        private static readonly byte[] IndentBytes = encoding.GetBytes(new string(' ', 4));
        private static readonly byte[] ItemStartBytes = encoding.GetBytes("<");
        private static readonly byte[] ItemEndBytes = encoding.GetBytes(">");
        private static readonly byte[] ItemBracketLeft = encoding.GetBytes(" [");
        private static readonly byte[] ItemBracketRight = encoding.GetBytes("] ");
        private static readonly byte[] StringMarkBytes = encoding.GetBytes("'");
        private static readonly byte[] SpaceBytes = encoding.GetBytes(" ");

        private static readonly StandardFormat HexFormat = new StandardFormat('X');

        public static string ToSml(this SecsMessage msg)
        {
            if (msg is null)
                return null;
            
            using (var sw = new MemoryStream())
            {
                msg.WriteToAsync(sw, isAsync: false);
                sw.Flush();
                return Encoding.UTF8.GetString(sw.GetBuffer());
            }
        }

        public static Task WriteToAsync(this SecsMessage msg, Stream writer, bool isAsync = true)
        {
            var channel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = true,
            });

            var reader = channel.Reader;

            return Task.WhenAll(
                msg.WriteAsync(channel.Writer),
                Task.Run(async delegate
                {

                    while (await reader.WaitToReadAsync().ConfigureAwait(false))
                    {
                        if (isAsync)
                        {
                            Task writeTask = Task.CompletedTask;
                            while (reader.TryRead(out var bytes))
                            {
                                await writeTask.ConfigureAwait(false);
                                writeTask = writer.WriteAsync(bytes).AsTask();
                            }

                            await writeTask.ConfigureAwait(false);
                            // FlushAsync here rather than block in Dispose on Flush
                            await writer.FlushAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            while (reader.TryRead(out var bytes))
                            {
                                writer.Write(bytes.Span);
                            }
                        }
                    }
                }));
        }

        private static ValueTask WriteLine(this ChannelWriter<ReadOnlyMemory<byte>> writer)
        {
            return writer.WriteAsync(NewLineBytes);
        }

        private static async Task WriteAsync(this SecsMessage msg, ChannelWriter<ReadOnlyMemory<byte>> writer)
        {
            if (msg is null)
                return;
            
            // message header
            var msgHeader = msg.ToString();
            int minLength = encoding.GetByteCount(msgHeader);
            using (var buffer = pool.Rent())
            {
                await writer.WriteAsync(buffer.Memory
                    .Slice(0, encoding.GetBytes(msgHeader, buffer.Memory.Span))).ConfigureAwait(false);
                //writer.Write(buffer.Memory.Span.Slice(0, encoding.GetBytes(msgHeader, buffer.Memory.Span)));

                await writer.WriteLine().ConfigureAwait(false);

                msg.SecsItem?.WriteTo(writer, 1, buffer);
            }
            await writer.WriteAsync(EndOfMessageBytes).ConfigureAwait(false);
        }

        private static async ValueTask IndentAsync(this ChannelWriter<ReadOnlyMemory<byte>> writer, int level)
        {
            for (int i = 0; i < level; i++)
                await writer.WriteAsync(IndentBytes);
        }

        private static async ValueTask WriteTo(this Item item, ChannelWriter<ReadOnlyMemory<byte>> writer, int indentLevel, IMemoryOwner<byte> buffer)
        {
            await writer.IndentAsync(indentLevel);

            await writer.WriteAsync(ItemStartBytes);
            await writer.WriteAsync(item.Format.ToSml());
            await writer.WriteAsync(ItemBracketLeft);

            if (Utf8Formatter.TryFormat(item.Count, buffer.Memory.Span, out var count))
            {
                await writer.WriteAsync(buffer.Memory.Slice(0, count));
            }

            await writer.WriteAsync(ItemBracketRight);

            switch (item.Format)
            {
                case SecsFormat.List:
                    await writer.WriteLine();
                    var items = item.Items;
                    foreach (var subItem in item.Items)
                        await subItem.WriteTo(writer, indentLevel + 1, buffer);
                    await writer.IndentAsync(indentLevel);
                    break;
                case SecsFormat.ASCII:
                case SecsFormat.JIS8:
                    await writer.WriteAsync(StringMarkBytes);
                    using (var charBuffer = MemoryPool<char>.Shared.Rent(4096))
                    {
                        item.GetChars(charBuffer.Memory.Span);
                        await writer.WriteAsync(buffer.Memory
                            .Slice(0, encoding.GetBytes(charBuffer.Memory.Span, buffer.Memory.Span))).ConfigureAwait(false);
                    }
                    await writer.WriteAsync(StringMarkBytes);
                    break;
                case SecsFormat.Binary:
                    await WriteHexAsync();
                    break;
                case SecsFormat.F4:
                    await WriteAsync<float>(TryFormat);
                    break;
                case SecsFormat.F8:
                    await WriteAsync<double>(TryFormat);
                    break;
                case SecsFormat.I1:                    
                    await WriteAsync<sbyte>(TryFormat);
                    break;
                case SecsFormat.I2:
                    await WriteAsync<short>(TryFormat);
                    break;
                case SecsFormat.I4:
                    await WriteAsync<int>(TryFormat);
                    break;
                case SecsFormat.I8:
                    await WriteAsync<long>(TryFormat);
                    break;
                case SecsFormat.U1:
                    await WriteAsync<byte>(TryFormat);
                    break;
                case SecsFormat.U2:
                    await WriteAsync<ushort>(TryFormat);
                    break;
                case SecsFormat.U4:
                    await WriteAsync<uint>(TryFormat);
                    break;
                case SecsFormat.U8:
                    await WriteAsync<ulong>(TryFormat);
                    break;
                case SecsFormat.Boolean:
                    await WriteAsync<bool>(TryFormat);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(item.Format), item.Format, "invalid SecsFormat value");
            }

            await writer.WriteAsync(ItemEndBytes);
            await writer.WriteLine();

            async ValueTask WriteHexAsync()
            {
                var values = item.GetValues<byte>();
                if (values.Length == 0)
                    return;

                foreach (var num in values)
                {
                    Utf8Formatter.TryFormat(num, buffer.Memory.Span, out _, HexFormat);
                    await writer.WriteAsync(buffer.Memory.Slice(0, 2));
                    await writer.WriteAsync(SpaceBytes);
                }
            }

            async ValueTask WriteAsync<T>(TryFormatter<T> formatter) where T:unmanaged
            {
                foreach (var v in item.GetValues<T>())
                {
                    if (formatter(v, buffer.Memory.Span, out var c))
                    {
                        await writer.WriteAsync(buffer.Memory.Slice(0, c));
                        await writer.WriteAsync(SpaceBytes);
                    }
                }
            }
        }

        delegate bool TryFormatter<T>(T value, Span<byte> destination, out int charsWritten) where T: unmanaged;

        static bool TryFormat(float value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        static bool TryFormat(double value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        static bool TryFormat(byte value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        static bool TryFormat(sbyte value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        static bool TryFormat(short value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        static bool TryFormat(ushort value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        static bool TryFormat(int value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        static bool TryFormat(uint value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        static bool TryFormat(long value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        static bool TryFormat(ulong value, Span<byte> destination, out int charsWritten)
           => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        static bool TryFormat(bool value, Span<byte> destination, out int charsWritten)
           => Utf8Formatter.TryFormat(value, destination, out charsWritten);

        private static readonly byte[] List_Format_Bytes = Encoding.UTF8.GetBytes("L");
        private static readonly byte[] Binary_Format_Bytes = Encoding.UTF8.GetBytes("B");
        private static readonly byte[] Boolean_Format_Bytes = Encoding.UTF8.GetBytes("Boolean");
        private static readonly byte[] ASSCII_Format_Bytes = Encoding.UTF8.GetBytes("A");
        private static readonly byte[] JIS8_Format_Bytes = Encoding.UTF8.GetBytes("J");
        private static readonly byte[] I8_Format_Bytes = Encoding.UTF8.GetBytes("I8");
        private static readonly byte[] I1_Format_Bytes = Encoding.UTF8.GetBytes("I1");
        private static readonly byte[] I2_Format_Bytes = Encoding.UTF8.GetBytes("I2");
        private static readonly byte[] I4_Format_Bytes = Encoding.UTF8.GetBytes("I4");
        private static readonly byte[] U8_Format_Bytes = Encoding.UTF8.GetBytes("U8");
        private static readonly byte[] U1_Format_Bytes = Encoding.UTF8.GetBytes("U1");
        private static readonly byte[] U2_Format_Bytes = Encoding.UTF8.GetBytes("U2");
        private static readonly byte[] U4_Format_Bytes = Encoding.UTF8.GetBytes("U4");
        private static readonly byte[] F4_Format_Bytes = Encoding.UTF8.GetBytes("F4");
        private static readonly byte[] F8_Format_Bytes = Encoding.UTF8.GetBytes("F8");

        static byte[] ToSml(this SecsFormat format)
        {
            switch (format)
            {
                case SecsFormat.List: return List_Format_Bytes;
                case SecsFormat.Binary: return Binary_Format_Bytes;
                case SecsFormat.Boolean: return Boolean_Format_Bytes;
                case SecsFormat.ASCII: return ASSCII_Format_Bytes;
                case SecsFormat.JIS8: return JIS8_Format_Bytes;
                case SecsFormat.I8: return I8_Format_Bytes;
                case SecsFormat.I1: return I1_Format_Bytes;
                case SecsFormat.I2: return I2_Format_Bytes;
                case SecsFormat.I4: return I4_Format_Bytes;
                case SecsFormat.F8: return F8_Format_Bytes;
                case SecsFormat.F4: return F4_Format_Bytes;
                case SecsFormat.U8: return U8_Format_Bytes;
                case SecsFormat.U1: return U1_Format_Bytes;
                case SecsFormat.U2: return U2_Format_Bytes;
                case SecsFormat.U4: return U4_Format_Bytes;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), (int)format, "Invalid SecsFormat value");
            }
        }
    }
}
