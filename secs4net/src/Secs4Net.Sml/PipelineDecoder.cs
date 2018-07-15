using System;
using System.Collections.Generic;
using System.Diagnostics;
using static Secs4Net.Item;

namespace Secs4Net.Sml
{
    internal sealed class PipelineDecoder
    {
        internal readonly List<SecsMessage> Output = new List<SecsMessage>();

        bool _decodeItem;
        private string _name;
        private byte _s;
        private byte _f;
        private bool _replyExpected;
        private readonly Stack<List<Item>> _stack = new Stack<List<Item>>();

        public void ProcessLine(in ReadOnlySpan<char> line)
        {
            _decodeItem = _decodeItem ? ParseItem(line.TrimStart()) : ParseHeader(line);
            if (!_decodeItem)
            {
                _stack.Clear();
            }
        }

        private bool ParseHeader(in ReadOnlySpan<char> line)
        {
            int i = line.IndexOf(':');

            _name = line.Slice(0, i).Trim().ToString();
            var line1 = line.Slice(i);

            i = line1.IndexOf("'S", StringComparison.Ordinal) + 2;
            int j = line1.IndexOf('F');
            _s = byte.Parse(line1.Slice(i, j - i));
            i = line1.LastIndexOf('\'');
            _f = byte.Parse(line1.Slice(j + 1, i - (j + 1)));

            _replyExpected = line1.Slice(i).LastIndexOf('W') != -1;

            return true;
        }

        private bool ParseItem(in ReadOnlySpan<char> line)
        {
            if (line[0] == '.')
                return false;

            if (line[0] == '>')
            {
                var itemList = _stack.Pop();
                var item = itemList.Count > 0 ? L(itemList) : L();
                if (_stack.Count > 0)
                {
                    _stack.Peek()
                         .Add(item);
                }
                else
                {
                    Output.Add(new SecsMessage(_s, _f, _name, item, _replyExpected));
                }
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
                _stack.Push(new List<Item>());
            }
            else
            {
                int indexSizeR = line.IndexOf(']');
                Debug.Assert(indexSizeR != -1);
                int indexItemR = line.LastIndexOf('>');
                Debug.Assert(indexItemR != -1);
                var valueStr = line.Slice(indexSizeR + 1, indexItemR - indexSizeR - 1).TrimStart();

                var item = format.Create(valueStr);

                if (_stack.Count > 0)
                {
                    _stack.Peek()
                         .Add(item);
                }
                else
                {
                    Output.Add(new SecsMessage(_s, _f, _name, item, _replyExpected));
                }
            }

            return true;
        }
    }
}
