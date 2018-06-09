using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Secs4Net
{
    public readonly struct MessageHeader
    {
        public readonly ushort DeviceId;
        public readonly bool ReplyExpected;
        public readonly byte S;
        public readonly byte F;
        public readonly MessageType MessageType;
        public readonly int SystemBytes;

        internal MessageHeader(
            in ushort deviceId = default, 
            in bool replyExpected = default, 
            in byte s = default, 
            in byte f = default, 
            in MessageType messageType = default, 
            in int systemBytes = default)
        {
            DeviceId = deviceId;
            ReplyExpected = replyExpected;
            S = s;
            F = f;
            MessageType = messageType;
            SystemBytes = systemBytes;
        }

		internal unsafe void EncodeTo(Span<byte> buffer)
		{
			//// DeviceId
			//BinaryPrimitives.WriteUInt16BigEndian(buffer, DeviceId);

			//// S, ReplyExpected
			//buffer[2] = (byte)(S | (ReplyExpected ? 0b1000_0000 : 0));

			//// F
			//buffer[3] = F;

			//buffer[4] = 0;

			//// MessageType
			//buffer[5] = (byte)MessageType;

			//// SystemBytes
			//BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(6), SystemBytes);

			// DeviceId
			var values = (byte*)Unsafe.AsPointer(ref Unsafe.AsRef(DeviceId));
			Unsafe.Copy(ref buffer[1], values);
			Unsafe.Copy(ref buffer[0], values + 1);

			// S, ReplyExpected
			buffer[2] = (byte)(S | (ReplyExpected ? 0b1000_0000 : 0));

			// F
			buffer[3] = F;

			buffer[4] = 0;

			// MessageType
			buffer[5] = (byte)MessageType;

			// SystemBytes
			values = (byte*)Unsafe.AsPointer(ref Unsafe.AsRef(SystemBytes));
			Unsafe.Copy(ref buffer[9], values);
			Unsafe.Copy(ref buffer[8], values + 1);
			Unsafe.Copy(ref buffer[7], values + 2);
			Unsafe.Copy(ref buffer[6], values + 3);
		}

        internal static unsafe MessageHeader Decode(in ReadOnlySpan<byte> buffer)
        {
			//var deviceId=	BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(startIndex));
			//var systemBytes = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(startIndex + 6));

            // DeviceId
            ushort deviceId = 0;
            var ptr = (byte*)Unsafe.AsPointer(ref deviceId);
            Unsafe.Copy(ptr + 0, ref Unsafe.AsRef(buffer[1]));
            Unsafe.Copy(ptr + 1, ref Unsafe.AsRef(buffer[0]));

            // SystemBytes
            int systemBytes = 0;
            ptr = (byte*)Unsafe.AsPointer(ref systemBytes);
            Unsafe.Copy(ptr + 0, ref Unsafe.AsRef(buffer[9]));
            Unsafe.Copy(ptr + 1, ref Unsafe.AsRef(buffer[8]));
            Unsafe.Copy(ptr + 2, ref Unsafe.AsRef(buffer[7]));
            Unsafe.Copy(ptr + 3, ref Unsafe.AsRef(buffer[6]));

            return new MessageHeader(
                deviceId: deviceId,
                replyExpected: (buffer[2] & 0b1000_0000) != 0,
                s: (byte)(buffer[2] & 0b0111_111),
                f: buffer[3],
                messageType: (MessageType)buffer[5],
                systemBytes: systemBytes
            );
        }
    }
}
