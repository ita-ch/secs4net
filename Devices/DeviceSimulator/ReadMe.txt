This sample program is the demo of Secs4Net.You can simulate a device/host to communicate with other HSMS-SS/SECS-II device.
Try to send/reply message with following SML format, notice the end of angle brackets of the list item.(or refer to received message textbox)
This just a simple demo,expand it by yourself for fun.

S6F11ReadyToLoad: 'S6F11' W 
    <L [3]
        <U4 [1] 320 >
        <U2 [1] 114 > 
        <L [1]
            <L [2]
                <U2 [1] 500 >
                <L [1]
                    <U1 [1] 1 > 
                 >
             >
         >
     >
.

S6F12: 'S6F12'
    <B [1] 0x00 > 
.

S1F4ReadyToLoad: 'S1F4' R 
    <L [1]
        <U4 [1] 320 >
     >
.

S1F13Test: 'S1F13' W 
    <L [0]
     >
.

ResponseS14F1:'S14F2'
     < L[2]
          < L[1]
               < L[2]
                    < A [5] 'Meuh' >
                    < L[5]
                         < L[2]
                              < A [14] 'OriginLocation'>
                              < U4 [1] 3>
                         >
                          < L[2]
                              < A [4] 'Rows'>
                              < U4 [1] 3>
                         >
                          < L[2]
                              < A [7] 'Columns'>
                              < U4 [1] 5>
                         >
                          < L[2]
                              < A [10] 'CellStatus'>
                              < U1 [15] 0 1 0 0 1 1 0 0 0 1 1 1 0 0 0>
                         >
                          < L[2]
                              < A [10] 'DefectCode'>
                              < U1 [15] 0 1 0 0 1 1 0 0 0 1 1 1 0 0 0>
                         >
                         < L[2]
                              < A [5] 'LotID'>
                              < A [9] 'MeuhLotID'>
                         >
                    >
               >
          >
          < L [2]
               < U1 [1] 0>
               < L [0]
               >
          >
     >