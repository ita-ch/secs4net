using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;

namespace Secs4Net.Sml
{
    static partial class SmlReaderExtensions
    {
        public static async Task<IList<SecsMessage>> ToSecsMessagesAsync(this Stream input)
        {
            var decoder = new PipelineDecoder();
            var pipe = new Pipe();
            var writing = FillPipeAsync(input, pipe.Writer);
            var reading = ReadPipeAsync(pipe.Reader, decoder);

            await Task.WhenAll(reading, writing);

            return decoder.Output;
        }

        static async Task FillPipeAsync(Stream input, PipeWriter writer)
        {
            const int minimumBufferSize = 512;

            while (true)
            {
                // Allocate at least 512 bytes from the PipeWriter
                var buffer = writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await input.ReadAsync(buffer);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    // Tell the PipeWriter how much was read from the Socket
                    writer.Advance(bytesRead);
                }
                catch (Exception ex)
                {
                    break;
                }

                // Make the data available to the PipeReader
                var result = await writer.FlushAsync();

                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Tell the PipeReader that there's no more data coming
            writer.Complete();
        }

        static async Task ReadPipeAsync(PipeReader reader, PipelineDecoder decoder)
        {
            while (true)
            {
                var result = await reader.ReadAsync();

                var buffer = result.Buffer;
                SequencePosition? position = null;

                do
                {
                    // Look for a EOL in the buffer
                    position = buffer.PositionOf((byte)'\n');

                    if (position != null)
                    {
                        // Process the line
                        var line = buffer.Slice(0, position.Value);

                        var arr = ArrayPool<char>.Shared.Rent((int)line.Length);
                        GetAsciiChars(line, arr);
                        decoder.ProcessLine(arr);
                        ArrayPool<char>.Shared.Return(arr);

                        // Skip the line + the \n character (basically position)
                        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                    }
                }
                while (position != null);

                // Tell the PipeReader how much of the buffer we have consumed
                reader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming
                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Mark the PipeReader as complete
            reader.Complete();

            void GetAsciiChars(ReadOnlySequence<byte> buffer, Span<char> chars)
            {
                if (buffer.IsSingleSegment)
                {
                    Encoding.ASCII.GetChars(buffer.First.Span, chars);
                    return;
                }

                foreach (var segment in buffer)
                {
                    Encoding.ASCII.GetChars(segment.Span, chars);

                    chars = chars.Slice(segment.Length);
                }
            }
        }

        //static string GetAsciiString(ReadOnlySequence<byte> buffer)
        //{
        //	if (buffer.IsSingleSegment)
        //	{
        //		return Encoding.ASCII.GetString(buffer.First.Span);
        //	}

        //	return string.Create((int)buffer.Length, buffer, (span, sequence) =>
        //	{
        //		foreach (var segment in sequence)
        //		{
        //			Encoding.ASCII.GetChars(segment.Span, span);

        //			span = span.Slice(segment.Length);
        //		}
        //	});
        //}
    }
}
