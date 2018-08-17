using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Secs4Net
{
    /// <summary>
    ///  Pipeline based HSMS/SECS-II message decoder
    /// </summary>
    internal sealed class SecsDecoder
    {
        private delegate PipelineStep Decoder(in ReadOnlySequence<byte> buffer, out SequencePosition position);

        /// <summary>
        /// decode pipelines
        /// </summary>
        private readonly Decoder[] _pipelines;

        private PipelineStep _currentStep;

        private readonly Action<MessageHeader, SecsMessage> _dataMsgHandler;
        private readonly Action<MessageHeader> _controlMsgHandler;

        private readonly Stack<List<Item>> _stack = new Stack<List<Item>>();
        private long _messageDataLength;
        private MessageHeader _msgHeader;
        private SecsFormat _format;
        private int _itemLength;

        private readonly int _streamBufferSize;
        private readonly ISecsGemLogger _logger;
        private readonly ITimer _timer8;

        public Task ProcessAsync(IDecodeReader source)
        {
            var pipe = new Pipe(new PipeOptions(minimumSegmentSize: _streamBufferSize, useSynchronizationContext: false));
            var writing = FillPipeAsync(source, pipe.Writer);
            var reading = DecodeFromPipeAsync(pipe.Reader);

            return Task.WhenAll(reading, writing);
        }

        async Task FillPipeAsync(IDecodeReader source, PipeWriter writer)
        {
            while (true)
            {
                // Rent buffer from the PipeWriter
                var memory = writer.GetMemory();
                try
                {
                    var bytesRead = await source.ReadAsync(memory).ConfigureAwait(false);

                    _timer8.Stop();

                    if (bytesRead == 0)
                    {
                        _logger.Error("Received 0 byte.");
                        break;
                    }

                    // Tell the PipeWriter how much was read from the Socket
                    writer.Advance(bytesRead);
                }
                catch (Exception ex)
                {
                    _logger.Error("Error on filling pipeline from source", ex);
                    break;
                }

                // Make the data available to the PipeReader
                var result = await writer.FlushAsync().ConfigureAwait(false);

                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Tell the PipeReader that there's no more data coming
            writer.Complete();
        }

        async Task DecodeFromPipeAsync(PipeReader reader)
        {
            while (true)
            {
                var result = await reader.ReadAsync().ConfigureAwait(false);

                var buffer = result.Buffer;
                var lastPosition = default(SequencePosition);
                var nexStep = _currentStep;
                do
                {
                    _currentStep = nexStep;
                    nexStep = _pipelines[(int)_currentStep].Invoke(buffer, out lastPosition);
                } while (nexStep != _currentStep);

                // Tell the PipeReader how much of the buffer we have consumed
                reader.AdvanceTo(buffer.Start, lastPosition);

#if !DISABLE_T8
                if (_messageDataLength > 0 && _timer8 != null)
                {
                    _logger.Debug($"Start T8 Timer: {_timer8.Interval / 1000} sec.");
                    _timer8.Start();
                }
#endif

                // Stop reading if there's no more data coming
                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Mark the PipeReader as complete
            reader.Complete();
        }

        private enum PipelineStep
        {
            GetTotalMessageLength = 0,
            GetMessageHeader = 1,
            GetItemHeader = 2,
            GetItem = 3,
        }

        internal SecsDecoder(
            Action<MessageHeader> controlMsgHandler,
            Action<MessageHeader, SecsMessage> dataMsgHandler,
            int streamBufferSize,
            ISecsGemLogger logger = null,
            ITimer timer = null)
        {
            _streamBufferSize = streamBufferSize;
            _timer8 = timer;

            _logger = logger;
            _dataMsgHandler = dataMsgHandler;
            _controlMsgHandler = controlMsgHandler;

            _pipelines = new Decoder[4];
            _pipelines[(int)PipelineStep.GetTotalMessageLength] = GetTotalMessageLength;
            _pipelines[(int)PipelineStep.GetTotalMessageLength] = GetTotalMessageLength;
            _pipelines[(int)PipelineStep.GetMessageHeader] = GetMessageHeader;
            _pipelines[(int)PipelineStep.GetItemHeader] = GetItemHeader;
            _pipelines[(int)PipelineStep.GetItem] = GetItem;
        }

        PipelineStep GetTotalMessageLength(in ReadOnlySequence<byte> buffer, out SequencePosition position)
        {
            if (buffer.Length < 4)
            {
                position = buffer.Start;
                return PipelineStep.GetTotalMessageLength;
            }

            position = buffer.GetPosition(4);
            if (buffer.IsSingleSegment)
            {
                _messageDataLength = BinaryPrimitives.ReadUInt32BigEndian(buffer.First.Span);
            }
            else
            {
                Span<byte> bytes = stackalloc byte[4];
                buffer.Slice(buffer.Start, position).CopyTo(bytes);
                _messageDataLength = BinaryPrimitives.ReadUInt32BigEndian(bytes);
            }

            Trace.WriteLine($"Get Message Length: {_messageDataLength}");
            return GetMessageHeader(buffer.Slice(position), out position);
        }

        PipelineStep GetMessageHeader(in ReadOnlySequence<byte> buffer, out SequencePosition position)
        {
            if (buffer.Length < 10)
            {
                position = buffer.Start;
                return PipelineStep.GetMessageHeader;
            }

            position = buffer.GetPosition(10);
            if (buffer.IsSingleSegment)
            {
                _msgHeader = MessageHeader.Decode(buffer.First.Span);
            }
            else
            {
                Span<byte> bytes = stackalloc byte[10];
                buffer.Slice(buffer.Start, position).CopyTo(bytes);
                _msgHeader = MessageHeader.Decode(buffer.First.Span);
            }

            _messageDataLength -= 10;

            if (_messageDataLength == 0)
            {
                if (_msgHeader.MessageType == MessageType.DataMessage)
                {
                    _dataMsgHandler(_msgHeader, new SecsMessage(_msgHeader.S, _msgHeader.F, string.Empty, replyExpected: _msgHeader.ReplyExpected));
                }
                else
                {
                    _controlMsgHandler(_msgHeader);
                }

                return PipelineStep.GetTotalMessageLength;
            }

            if (buffer.Length >= _messageDataLength)
            {
                Trace.WriteLine("Get Complete Data Message with total data");

                _dataMsgHandler(_msgHeader,
                    new SecsMessage(_msgHeader.S, _msgHeader.F, string.Empty,
                        BufferedDecodeItem(buffer, ref position), _msgHeader.ReplyExpected));

                _messageDataLength = 0;
                return PipelineStep.GetTotalMessageLength; //completeWith message received
            }

            return GetItemHeader(buffer.Slice(position), out position);
        }


        PipelineStep GetItemHeader(in ReadOnlySequence<byte> buffer, out SequencePosition position)
        {
            if (buffer.Length < 1)
            {
                position = buffer.Start;
                return PipelineStep.GetItemHeader;
            }

            var formatByte = buffer.First.Span[0];
            var lengthBits = formatByte & 0b_0000_00_11;

            var headerLength = lengthBits + 1;
            if (buffer.Length < headerLength)
            {
                position = buffer.Start;
                return PipelineStep.GetItemHeader;
            }

            _format = (SecsFormat)(formatByte & 0b_1111_11_00);

            Span<byte> itemLengthBytes = stackalloc byte[4];
            if (buffer.First.Span.Length >= headerLength)
            {
                buffer.First.Span.Slice(1, lengthBits).CopyTo(itemLengthBytes);
            }
            else
            {
                buffer.Slice(1, lengthBits).CopyTo(itemLengthBytes);
            }
            _itemLength = BinaryPrimitives.ReadInt32BigEndian(itemLengthBytes);

            Trace.WriteLineIf(_format != SecsFormat.List, $"Get format: {_format}, length: {_itemLength}");

            _messageDataLength -= headerLength;

            return GetItem(buffer.Slice(headerLength), out position);
        }

        PipelineStep GetItem(in ReadOnlySequence<byte> buffer, out SequencePosition position)
        {
            Item item;
            if (_format == SecsFormat.List)
            {
                if (_itemLength == 0)
                {
                    position = buffer.Start;
                    item = Item.L();
                }
                else
                {
                    _stack.Push(new List<Item>(_itemLength));
                    return GetItemHeader(buffer, out position);
                }
            }
            else
            {
                if (buffer.Length < _itemLength)
                {
                    position = buffer.Start;
                    return PipelineStep.GetItem;
                }

                var itemBytes = buffer.Slice(0, _itemLength);
                item = Item.BytesDecode(_format, itemBytes);
                Trace.WriteLine($"Complete Item: {_format}");

                position = itemBytes.End;
                _messageDataLength -= (uint)_itemLength;
            }

            if (_stack.Count == 0)
            {
                Trace.WriteLine("Get Complete Data Message by stream decoded");
                _dataMsgHandler(_msgHeader,
                    new SecsMessage(_msgHeader.S, _msgHeader.F, string.Empty, item, _msgHeader.ReplyExpected));

                return PipelineStep.GetTotalMessageLength;
            }

            var list = _stack.Peek();
            list.Add(item);
            while (list.Count == list.Capacity)
            {
                item = Item.L(_stack.Pop());
                Trace.WriteLine($"Complete List: {item.Count}");
                if (_stack.Count > 0)
                {
                    list = _stack.Peek();
                    list.Add(item);
                }
                else
                {
                    Trace.WriteLine("Get Complete Data Message by stream decoded");
                    _dataMsgHandler(_msgHeader, new SecsMessage(_msgHeader.S, _msgHeader.F, string.Empty, item, _msgHeader.ReplyExpected));
                    return PipelineStep.GetTotalMessageLength;
                }
            }

            return GetItemHeader(buffer.Slice(_itemLength), out position);
        }


        Item BufferedDecodeItem(in ReadOnlySequence<byte> bytes, ref SequencePosition position)
        {
            var formatSeq = bytes.Slice(position, 1);
            var format = (SecsFormat)(formatSeq.First.Span[0] & 0xFC);
            var lengthBits = (byte)(formatSeq.First.Span[0] & 3);
            position = formatSeq.End;

            Span<byte> itemLengthBytes = stackalloc byte[4];
            var itemLengthSeq = bytes.Slice(position, lengthBits);
            itemLengthSeq.CopyTo(itemLengthBytes);
            int dataLength = BinaryPrimitives.ReadInt32BigEndian(itemLengthBytes); // max to 3 byte dataLength
            position = itemLengthSeq.End;

            if (format == SecsFormat.List)
            {
                if (dataLength == 0)
                {
                    return Item.L();
                }

                var list = new List<Item>(dataLength);
                for (var i = 0; i < dataLength; i++)
                {
                    list.Add(BufferedDecodeItem(bytes, ref position));
                }

                return Item.L(list);
            }

            var itemBytes = bytes.Slice(position, dataLength);
            var item = Item.BytesDecode(format, itemBytes);
            position = itemBytes.End;
            return item;
        }
    }
}
