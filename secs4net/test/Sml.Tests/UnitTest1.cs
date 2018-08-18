using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Secs4Net;
using Secs4Net.Sml;
using Xunit;
using static Secs4Net.Item;

namespace Sml.Tests
{
    public class UnitTest1
    {
        static UnitTest1()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public void CanReadSmlFromString()
        {
            var sml = @"S1F14EstablishCommunicationsRequestAck_Host_Ack:'S1F14' 
    <L[2]
        <B[1] 0x0 >
        <L[0]
        >
    >
.";

            var msg = sml.ToSecsMessage();
            var expected = new SecsMessage(1, 14, item:
                L(
                    B(0),
                    L()));

            Assert.True(msg.IsMatch(expected));
        }

        [Fact]
        public async Task CanReadSmlFromStream()
        {
            var sml = @"S1F14EstablishCommunicationsRequestAck_Host_Ack:'S1F14' 
    <L[2]
        <B[1] 0x0 >
        <L[0]
        >
    >
.";

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(sml));

            var msgs = await stream.ToSecsMessagesAsync();

            Assert.True(msgs.Any());

            var expected = new SecsMessage(1, 14, item:
                L(
                    B(0),
                    L()));

            Assert.True(msgs[0].IsMatch(expected));
        }

        [Fact]
        public void CanWriteSmlAsString()
        {
            var msg = new SecsMessage(1, 14, item:
                L(
                    B(0),
                    L()));

            var sml = msg.ToSml();

            var expected = sml.ToSecsMessage();

            Assert.True(msg.IsMatch(expected));
        }
    }
}
