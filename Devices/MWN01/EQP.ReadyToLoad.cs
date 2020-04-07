﻿using Cim.Eap.Tx;
using Secs4Net;
namespace Eap.Driver.MWN {
    partial class Driver {
        void EQP_ReadyToLoad(SecsMessage msg) {
            EAP.Report(new ReadyToLoadReport {
                PortID = GetPortID((byte)msg.SecsItem.Items[2].Items[0].Items[1].Items[0])
            });
        }
    }
}