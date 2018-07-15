using Microsoft.VisualStudio.TestTools.UnitTesting;
using Secs4Net;
using static Secs4Net.Item;
using Secs4Net.Sml;
using System;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace Sml.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
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
            Assert.IsTrue(msg.IsMatch(expected));

        }

        [TestMethod]
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

            var stream = new MemoryStream(Encoding.ASCII.GetBytes(sml));

            var msgs = await  stream.ToSecsMessagesAsync();

            Assert.IsTrue(msgs.Any());


            var expected = new SecsMessage(1, 14, item:
                L(
                    B(0),
                    L()));

            Assert.IsTrue(msgs[0].IsMatch(expected));

        }
    }
}
