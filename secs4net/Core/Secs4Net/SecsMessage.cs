using System;
using System.Collections.Generic;

namespace Secs4Net
{
    public sealed class SecsMessage
    {
        static SecsMessage()
        {
            if (!BitConverter.IsLittleEndian)
                throw new PlatformNotSupportedException("This version is only work on little endian hardware.");
        }

        public override string ToString() => $"'S{S}F{F}' {(ReplyExpected ? "W" : string.Empty)} {Name ?? string.Empty}";

        /// <summary>
        /// message stream number
        /// </summary>
        public byte S { get; }

        /// <summary>
        /// message function number
        /// </summary>
        public byte F { get; }

        /// <summary>
        /// expect reply message
        /// </summary>
        public bool ReplyExpected { get; internal set; }

        /// <summary>
        /// the root item of message
        /// </summary>
        public Item SecsItem { get; }

        public string Name { get; set; }

        /// <summary>
        /// constructor of SecsMessage
        /// </summary>
        /// <param name="s">message stream number</param>
        /// <param name="f">message function number</param>
        /// <param name="replyExpected">expect reply message</param>
        /// <param name="name"></param>
        /// <param name="item">root item</param>
        public SecsMessage(byte s, byte f, string name = null, Item item = null, bool replyExpected = true)
        {
            if (s > 0b0111_1111)
                throw new ArgumentOutOfRangeException(nameof(s), s, Resources.SecsMessageStreamNumberMustLessThan127);

            S = s;
            F = f;
            Name = name;
            ReplyExpected = replyExpected;
            SecsItem = item;
        }

		internal int Encode(Span<byte> buffer) => SecsItem?.EncodeTo(buffer) ?? 0;

		#region ISerializable Members
		////Binary Serialization
		//SecsMessage(SerializationInfo info, StreamingContext context)
		//{
		//    S = info.GetByte(nameof(S));
		//    F = info.GetByte(nameof(F));
		//    ReplyExpected = info.GetBoolean(nameof(ReplyExpected));
		//    Name = info.GetString(nameof(Name));
		//    _rawDatas = Lazy.Create(info.GetValue(nameof(_rawDatas), typeof(ReadOnlyCollection<RawData>)) as ReadOnlyCollection<RawData>);
		//    int i = 0;
		//    if (_rawDatas.Value.Count > 2)
		//        SecsItem = Decode(_rawDatas.Value.Skip(2).SelectMany(arr => arr.Bytes).ToArray(), ref i);
		//}

		//[SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
		//void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) {
		//    info.AddValue(nameof(S), S);
		//    info.AddValue(nameof(F), F);
		//    info.AddValue(nameof(ReplyExpected), ReplyExpected);
		//    info.AddValue(nameof(Name), Name);
		//    info.AddValue(nameof(_rawDatas), _rawDatas.Value);
		//}
		#endregion
	}
}
