using System;
using System.Threading.Tasks;

namespace Secs4Net
{
    interface IDecodeReader
    {
        ValueTask<int> ReadAsync(Memory<byte> buffer);
    }
}
