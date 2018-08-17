using Secs4Net;
using static Secs4Net.Item;
using Secs4Net.Sml;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Xunit;

namespace Sml.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void CanReadSmlFromString()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
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
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var sml = @"S1F14EstablishCommunicationsRequestAck_Host_Ack:'S1F14' 
    <L[2]
        <B[1] 0x0 >
        <L[0]
        >
    >
.";

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(sml));

            var msgs = await  stream.ToSecsMessagesAsync();

            Assert.True(msgs.Any());

            var expected = new SecsMessage(1, 14, item:
                L(
                    B(0),
                    L()));

            Assert.True(msgs[0].IsMatch(expected));

        }
    }
}
