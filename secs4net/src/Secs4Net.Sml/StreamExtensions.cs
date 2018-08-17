// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

namespace Nerdbank.Streams
{
    using System;
    using System.IO;
    using System.IO.Pipelines;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Stream extension methods.
    /// </summary>
    public static class StreamExtensions
    {
        /// <summary>
        /// Enables efficiently reading a stream using <see cref="PipeReader"/>.
        /// </summary>
        /// <param name="stream">The stream to read from using a pipe.</param>
        /// <param name="readBufferSize">The size of the buffer to ask the stream to fill.</param>
        /// <param name="cancellationToken">A cancellation token that will cancel task that reads from the stream to fill the pipe.</param>
        /// <returns>A <see cref="PipeReader"/>.</returns>
        public static PipeReader UsePipeReader(this Stream stream, int readBufferSize = 2048, CancellationToken cancellationToken = default)
        {
            var pipe = new Pipe();
            Task.Run(async delegate
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Memory<byte> memory = pipe.Writer.GetMemory(readBufferSize);
                    try
                    {
                        int bytesRead = await stream.ReadAsync(memory, cancellationToken);
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        pipe.Writer.Advance(bytesRead);
                    }
                    catch (Exception ex)
                    {
                        pipe.Writer.Complete(ex);
                        throw;
                    }

                    FlushResult result = await pipe.Writer.FlushAsync();
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                // Tell the PipeReader that there's no more data coming
                pipe.Writer.Complete();
            });
            return pipe.Reader;
        }

        /// <summary>
        /// Enables writing to a stream using <see cref="PipeWriter"/>.
        /// </summary>
        /// <param name="stream">The stream to write to using a pipe.</param>
        /// <param name="cancellationToken">A cancellation token that aborts writing.</param>
        /// <returns>A <see cref="PipeWriter"/>.</returns>
        public static PipeWriter UsePipeWriter(this Stream stream, CancellationToken cancellationToken = default)
        {
            var pipe = new Pipe();
            Task.Run(async delegate
            {
                try
                {
                    while (true)
                    {
                        ReadResult readResult = await pipe.Reader.ReadAsync(cancellationToken);
                        if (readResult.Buffer.Length > 0)
                        {
                            foreach (ReadOnlyMemory<byte> segment in readResult.Buffer)
                            {
                                await stream.WriteAsync(segment, cancellationToken);
                            }

                            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }

                        pipe.Reader.AdvanceTo(readResult.Buffer.End);

                        if (readResult.IsCompleted)
                        {
                            break;
                        }
                    }

                    pipe.Reader.Complete();
                }
                catch (Exception ex)
                {
                    pipe.Reader.Complete(ex);
                    throw;
                }
            });
            return pipe.Writer;
        }
    }
}
