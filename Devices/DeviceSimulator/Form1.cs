using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using Secs4Net;
using System.Net;
using System.Drawing;
using Secs4Net.Sml;

namespace SecsDevice
{
    public partial class Form1 : Form {
        SecsGem _secsGem;
        readonly ISecsGemLogger _logger;
        readonly BindingList<PrimaryMessageWrapper> recvBuffer = new BindingList<PrimaryMessageWrapper>();

        public Form1() {
            InitializeComponent();
            Settings1.Default.Reload();
            txtReplySeconary.Text = Settings1.Default.Response;
            txtSendPrimary.Text = Settings1.Default.Message;

            radioActiveMode.DataBindings.Add("Enabled", btnEnable, "Enabled");
            radioPassiveMode.DataBindings.Add("Enabled", btnEnable, "Enabled");
            txtAddress.DataBindings.Add("Enabled", btnEnable, "Enabled");
            numPort.DataBindings.Add("Enabled", btnEnable, "Enabled");
            numDeviceId.DataBindings.Add("Enabled", btnEnable, "Enabled");
            numBufferSize.DataBindings.Add("Enabled", btnEnable, "Enabled");
            recvMessageBindingSource.DataSource = recvBuffer;
            txtAddress.Text = Settings1.Default.IPAdress;
            numPort.Value = Settings1.Default.Port;
            radioActiveMode.Checked = Settings1.Default.ActiveMode;
            radioPassiveMode.Checked = !Settings1.Default.ActiveMode;
            Application.ThreadException += (sender, e) => MessageBox.Show(e.Exception.ToString());
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => MessageBox.Show(e.ExceptionObject.ToString());
            _logger = new SecsLogger(this);
        }

        private void btnEnable_Click(object sender, EventArgs e)
        {
            _secsGem?.Dispose();
            _secsGem = new SecsGem(
                radioActiveMode.Checked,
                radioActiveMode.Checked?IPAddress.Parse(txtAddress.Text):IPAddress.Any,
                (int)numPort.Value,
                (int)numBufferSize.Value)
            { Logger = _logger, DeviceId = (ushort)numDeviceId.Value };

            _secsGem.ConnectionChanged += delegate
            {
                this.Invoke((MethodInvoker)delegate
                {
                    lbStatus.Text = _secsGem.State.ToString();
                    if (_secsGem.State == ConnectionState.Selected && _secsGem.IsActive)
	                    _secsGem.SendAsync(new SecsMessage(1, 13,"Online Request",Item.L()));
                });
            };

            Settings1.Default.ActiveMode = radioActiveMode.Checked;
            Settings1.Default.Save();

            _secsGem.PrimaryMessageReceived += PrimaryMessageReceived;

            btnEnable.Enabled = false;
            _secsGem.Start();
            btnDisable.Enabled = true;


        }

        private void PrimaryMessageReceived(object sender, PrimaryMessageWrapper e)
        {
	        if (CheckBoxAutoResponse.Checked)
	        {
		        AutoReply(e);
	        }
	        else
	        {
		        this.Invoke(new MethodInvoker(() => recvBuffer.Add(e)));
	        }
        }

        private void btnDisable_Click(object sender, EventArgs e)
        {
            _secsGem?.Dispose();
            _secsGem = null;
            btnEnable.Enabled = true;
            btnDisable.Enabled = false;
            lbStatus.Text = "Disable";
            recvBuffer.Clear();
            richTextBox1.Clear();
        }

        private async void btnSendPrimary_Click(object sender, EventArgs e)
        {
            if (_secsGem.State != ConnectionState.Selected)
                return;
            if (string.IsNullOrWhiteSpace(txtSendPrimary.Text))
                return;

            try
            {
                var reply = await _secsGem.SendAsync(txtSendPrimary.Text.ToSecsMessage());
                txtRecvSecondary.Text = reply.ToSml();
            }
            catch (SecsException ex)
            {
                txtRecvSecondary.Text = ex.Message;
            }
        }

        private void lstUnreplyMsg_SelectedIndexChanged(object sender, EventArgs e) {
            var receivedMessage = lstUnreplyMsg.SelectedItem as PrimaryMessageWrapper;
            txtRecvPrimary.Text = receivedMessage?.Message.ToSml();
            
        }

        private async void btnReplySecondary_Click(object sender, EventArgs e)
        {
            if (!(lstUnreplyMsg.SelectedItem is PrimaryMessageWrapper recv))
                return;

            if (string.IsNullOrWhiteSpace(txtReplySeconary.Text))
                return;

            await recv.ReplyAsync(txtReplySeconary.Text.ToSecsMessage());
            recvBuffer.Remove(recv);
            txtRecvPrimary.Clear();
        }

       

        private async void btnReplyS9F7_Click(object sender, EventArgs e)
        {
            var recv = lstUnreplyMsg.SelectedItem as PrimaryMessageWrapper;
            if (recv == null)
                return;

            await recv.ReplyAsync(null);

            recvBuffer.Remove(recv);
            txtRecvPrimary.Clear();
        }

        class SecsLogger : ISecsGemLogger
        {
            readonly Form1 _form;
            internal SecsLogger(Form1 form)
            {
                _form = form;
            }
            public void MessageIn(SecsMessage msg, int systembyte)
            {
                _form.Invoke((MethodInvoker)delegate {
                    _form.richTextBox1.SelectionColor = Color.Black;
                    _form.richTextBox1.AppendText($"<-- [0x{systembyte:X8}] {msg.ToSml()}\n");
                });
            }

            public void MessageOut(SecsMessage msg, int systembyte)
            {
                _form.Invoke((MethodInvoker)delegate {
                    _form.richTextBox1.SelectionColor = Color.Black;
                    _form.richTextBox1.AppendText($"--> [0x{systembyte:X8}] {msg.ToSml()}\n");
                });
            }

            public void Info(string msg)
            {
                _form.Invoke((MethodInvoker)delegate {
                    _form.richTextBox1.SelectionColor = Color.Blue;
                    _form.richTextBox1.AppendText($"{msg}\n");
                });
            }

            public void Warning(string msg)
            {
                _form.Invoke((MethodInvoker)delegate {
                    _form.richTextBox1.SelectionColor = Color.Green;
                    _form.richTextBox1.AppendText($"{msg}\n");
                });
            }

            public void Error(string msg, Exception ex = null)
            {
                _form.Invoke((MethodInvoker)delegate {
                    _form.richTextBox1.SelectionColor = Color.Red;
                    _form.richTextBox1.AppendText($"{msg}\n");
                    _form.richTextBox1.SelectionColor = Color.Gray;
                    _form.richTextBox1.AppendText($"{ex}\n");
                });
            }

            public void Debug(string msg)
            {
                _form.Invoke((MethodInvoker)delegate {
                    _form.richTextBox1.SelectionColor = Color.Yellow;
                    _form.richTextBox1.AppendText($"{msg}\n");
                });
            }
        }

        private void txtReplySeconary_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty( txtReplySeconary.Text))
            { 
	            Settings1.Default.Response = txtReplySeconary.Text;
            
	            Settings1.Default.Save();
            }
        }

        private void txtSendPrimary_TextChanged(object sender, EventArgs e)
        {
	        if (!string.IsNullOrEmpty(txtSendPrimary.Text))
	        {
		        Settings1.Default.Message = txtSendPrimary.Text;

				Settings1.Default.Save();
	        }
        }

        private void txtAddress_TextChanged(object sender, EventArgs e)
        {
	        if (!string.IsNullOrEmpty(txtAddress.Text))
	        {
		        Settings1.Default.IPAdress = txtAddress.Text;

		        Settings1.Default.Save();
	        }
        }

        private void numPort_ValueChanged(object sender, EventArgs e)
        {
	        if (!string.IsNullOrEmpty(txtSendPrimary.Text))
	        {
		        Settings1.Default.Port = numPort.Value;

		        Settings1.Default.Save();
	        }
        }

        private void richTextBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
	        richTextBox1.Text = "";
        }

        private async void buttonOnlineRequest_Click(object sender, EventArgs e)
        {
	        if (_secsGem.State != ConnectionState.Selected)
		        return;
	        
	        try
	        {
                var item = Item.L();
		        var message = new SecsMessage(1, 13, "Online Request",item, true);
                
		        var reply = await _secsGem.SendAsync(message);
		        txtRecvSecondary.Text = reply.ToSml();
	        }
	        catch (SecsException ex)
	        {
		        txtRecvSecondary.Text = ex.Message;
	        }
        }

        private async void AutoReply(PrimaryMessageWrapper pmw)
        {
	        // Get the Stream
	        switch (pmw.Message.S)
	        {
		        case 1:
		        {
			        switch (pmw.Message.F)
			        {
				        case 1:
					        await pmw.ReplyAsync(new SecsMessage(1, 14, "Establish Communications Request Acknowledge",
						        Item.L()));
					        return;
				        case 13:
					        await pmw.ReplyAsync(new SecsMessage(1, 14, "Establish Communications Request Acknowledge",
						        Item.L()));
					        return;
			        }

			        break;
		        }
		        case 14:
		        {

			        if (pmw.Message.F == 1)
			        {
				        await pmw.ReplyAsync(txtReplySeconary.Text.ToSecsMessage());
				        return;
			        }

			        break;
		        }




	        }

	        await pmw.ReplyAsync(new SecsMessage(pmw.Message.S, (byte) ((int) pmw.Message.F + 1),
		        "SuperResponse", Item.B(0)));
        }

        private void txtRecvSecondary_DoubleClick(object sender, EventArgs e)
        {
	        txtRecvSecondary.Text = "";
        }
    }
}
