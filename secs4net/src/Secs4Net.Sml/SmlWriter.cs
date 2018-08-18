using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Secs4Net.Sml
{
    public static class SmlWriterExtensions
    {
        private static readonly MemoryPool<byte> pool = new SlabMemoryPool();
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

        private static readonly byte[] List_Format_Bytes = encoding.GetBytes("L");
        private static readonly byte[] Binary_Format_Bytes = encoding.GetBytes("B");
        private static readonly byte[] Boolean_Format_Bytes = encoding.GetBytes("Boolean");
        private static readonly byte[] ASSCII_Format_Bytes = encoding.GetBytes("A");
        private static readonly byte[] JIS8_Format_Bytes = encoding.GetBytes("J");
        private static readonly byte[] I8_Format_Bytes = encoding.GetBytes("I8");
        private static readonly byte[] I1_Format_Bytes = encoding.GetBytes("I1");
        private static readonly byte[] I2_Format_Bytes = encoding.GetBytes("I2");
        private static readonly byte[] I4_Format_Bytes = encoding.GetBytes("I4");
        private static readonly byte[] U8_Format_Bytes = encoding.GetBytes("U8");
        private static readonly byte[] U1_Format_Bytes = encoding.GetBytes("U1");
        private static readonly byte[] U2_Format_Bytes = encoding.GetBytes("U2");
        private static readonly byte[] U4_Format_Bytes = encoding.GetBytes("U4");
        private static readonly byte[] F4_Format_Bytes = encoding.GetBytes("F4");
        private static readonly byte[] F8_Format_Bytes = encoding.GetBytes("F8");


        private static readonly StandardFormat HexFormat = new StandardFormat('X');

        public static string ToSml(this SecsMessage msg)
        {
            if (msg is null)
                return null;

            using (var sw = new MemoryStream())
            {
                AsyncHelper.RunSync(() => msg.WriteToAsync(sw, isAsync: false));
                sw.Position = 0;
                using (var sr = new StreamReader(sw, encoding))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        public static Task WriteToAsync(this SecsMessage msg, Stream stream, bool isAsync = true)
        {
            var channel = Channel.CreateBounded<(bool, ReadOnlyMemory<byte>)>(
                new BoundedChannelOptions(100)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = true,
                    AllowSynchronousContinuations = true,
                });

            return Task.WhenAll(
                msg.WriteAsync(channel.Writer),
                channel.Reader.WriteToStream(stream, isAsync));
        }

        private static async Task WriteToStream(this ChannelReader<(bool pooled, ReadOnlyMemory<byte> memory)> reader, Stream stream, bool isAsync)
        {
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                if (isAsync)
                {
                    Task writeTask = Task.CompletedTask;
                    while (reader.TryRead(out var data))
                    {
                        await writeTask.ConfigureAwait(false);
                        writeTask = stream.WriteAsync(data.memory).AsTask();
                    }

                    await writeTask.ConfigureAwait(false);

                    await stream.FlushAsync().ConfigureAwait(false);
                }
                else
                {
                    while (reader.TryRead(out var data))
                    {
                        stream.Write(data.memory.Span);
                        if(data.pooled && MemoryMarshal.TryGetArray(data.memory, out var arr))
                        {
                            ArrayPool<byte>.Shared.Return(arr.Array);
                        }
                    }

                    stream.Flush();
                }
            }
        }

        private static ValueTask WriteLine(this ChannelWriter<(bool, ReadOnlyMemory<byte>)> writer)
        {
            return writer.WriteAsync((false, NewLineBytes));
        }

        private static async Task WriteAsync(this SecsMessage msg, ChannelWriter<(bool, ReadOnlyMemory<byte>)> writer)
        {
            try
            {
                if (msg is null)
                    return;

                // message header
                var msgHeader = msg.ToString();
                var memory = pool.Rent().Memory;
                await writer.WriteAsync((true,
                    memory.Slice(0, encoding.GetBytes(msgHeader, memory.Span))))
                        .ConfigureAwait(false);

                await writer.WriteLine().ConfigureAwait(false);

                if (msg.SecsItem != null)
                {
                    await msg.SecsItem.WriteTo(writer, 1).ConfigureAwait(false);
                }

                await writer.WriteAsync((false, EndOfMessageBytes)).ConfigureAwait(false);
            }
            finally
            {
                writer.Complete();
            }
        }

        private static async ValueTask IndentAsync(this ChannelWriter<(bool, ReadOnlyMemory<byte>)> writer, int level)
        {
            for (int i = 0; i < level; i++)
                await writer.WriteAsync((false, IndentBytes));
        }

        private static async ValueTask WriteTo(this Item item, ChannelWriter<(bool, ReadOnlyMemory<byte>)> writer, int indentLevel)
        {
            await writer.IndentAsync(indentLevel);

            await writer.WriteAsync((false, ItemStartBytes));
            await writer.WriteAsync((false, item.Format.ToSml()));
            await writer.WriteAsync((false, ItemBracketLeft));

            {
                var itemCountMemory = pool.Rent().Memory;
                if (Utf8Formatter.TryFormat(item.Count, itemCountMemory.Span, out var bytesWritten))
                {
                    await writer.WriteAsync((true, itemCountMemory.Slice(0, bytesWritten)));
                }
            }

            await writer.WriteAsync((false, ItemBracketRight));

            switch (item.Format)
            {
                case SecsFormat.List:
                    await writer.WriteLine();
                    var items = item.Items;
                    foreach (var subItem in item.Items)
                        await subItem.WriteTo(writer, indentLevel + 1);
                    await writer.IndentAsync(indentLevel);
                    break;
                case SecsFormat.ASCII:
                case SecsFormat.JIS8:
                    await writer.WriteAsync((false, StringMarkBytes));
                    using (var charBuffer = MemoryPool<char>.Shared.Rent(4096))
                    {
                        item.GetChars(charBuffer.Memory.Span);
                        var stringMemory = pool.Rent().Memory;
                        await writer.WriteAsync((true,
                            stringMemory.Slice(0,
                                encoding.GetBytes(charBuffer.Memory.Span, stringMemory.Span))
                                )).ConfigureAwait(false);
                    }
                    await writer.WriteAsync((false, StringMarkBytes));
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

            await writer.WriteAsync((false, ItemEndBytes));
            await writer.WriteLine();

            async ValueTask WriteHexAsync()
            {
                var values = item.GetValues<byte>();
                if (values.Length == 0)
                    return;

                foreach (var num in values)
                {
                    var memory = pool.Rent().Memory;
                    Utf8Formatter.TryFormat(num, memory.Span, out var c, HexFormat);
                    await writer.WriteAsync((true, memory.Slice(0, c)));
                    await writer.WriteAsync((false, SpaceBytes));
                }
            }

            async ValueTask WriteAsync<T>(TryFormatter<T> formatter) where T : unmanaged
            {
                foreach (var v in item.GetValues<T>())
                {
                    var memory = pool.Rent().Memory;
                    if (formatter(v, memory.Span, out var c))
                    {
                        await writer.WriteAsync((true, memory.Slice(0, c)));
                        await writer.WriteAsync((false, SpaceBytes));
                    }
                }
            }
        }

        private delegate bool TryFormatter<T>(T value, Span<byte> destination, out int charsWritten) where T : unmanaged;

        private static bool TryFormat(float value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        private static bool TryFormat(double value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        private static bool TryFormat(byte value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        private static bool TryFormat(sbyte value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        private static bool TryFormat(short value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        private static bool TryFormat(ushort value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        private static bool TryFormat(int value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        private static bool TryFormat(uint value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        private static bool TryFormat(long value, Span<byte> destination, out int charsWritten)
            => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        private static bool TryFormat(ulong value, Span<byte> destination, out int charsWritten)
           => Utf8Formatter.TryFormat(value, destination, out charsWritten);
        private static bool TryFormat(bool value, Span<byte> destination, out int charsWritten)
           => Utf8Formatter.TryFormat(value, destination, out charsWritten);

        private static byte[] ToSml(this SecsFormat format)
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
