namespace Client;

partial class ChatForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private Label lblServer;
    private TextBox txtServer;
    private Label lblPort;
    private TextBox txtPort;
    private Label lblUsername;
    private TextBox txtUsername;
    private Button btnConnect;
    private TextBox txtChat;
    private Label lblTyping;
    private TextBox txtMessage;
    private Button btnSend;
    private Label lblUsers;
    private ListBox lstUsers;
    private Button btnSendFile;

    private void InitializeComponent()
    {
        lblServer = new Label();
        txtServer = new TextBox();
        lblPort = new Label();
        txtPort = new TextBox();
        lblUsername = new Label();
        txtUsername = new TextBox();
        btnConnect = new Button();
        txtChat = new TextBox();
        lblTyping = new Label();
        txtMessage = new TextBox();
        btnSend = new Button();
        lblUsers = new Label();
        lstUsers = new ListBox();
        btnSendFile = new Button();
        SuspendLayout();

        // lblServer
        lblServer.AutoSize = true;
        lblServer.Location = new Point(12, 15);
        lblServer.Text = "Server IP:";

        // txtServer
        txtServer.Location = new Point(80, 12);
        txtServer.Size = new Size(120, 23);
        txtServer.Text = "127.0.0.1";

        // lblPort
        lblPort.AutoSize = true;
        lblPort.Location = new Point(212, 15);
        lblPort.Text = "Port:";

        // txtPort
        txtPort.Location = new Point(252, 12);
        txtPort.Size = new Size(60, 23);
        txtPort.Text = "5000";

        // lblUsername
        lblUsername.AutoSize = true;
        lblUsername.Location = new Point(324, 15);
        lblUsername.Text = "Username:";

        // txtUsername
        txtUsername.Location = new Point(400, 12);
        txtUsername.Size = new Size(120, 23);

        // btnConnect
        btnConnect.Location = new Point(532, 11);
        btnConnect.Size = new Size(90, 25);
        btnConnect.Text = "Connect";
        btnConnect.UseVisualStyleBackColor = true;
        btnConnect.Click += btnConnect_Click;

        // txtChat
        txtChat.Location = new Point(12, 48);
        txtChat.Multiline = true;
        txtChat.ReadOnly = true;
        txtChat.ScrollBars = ScrollBars.Vertical;
        txtChat.Size = new Size(590, 310);
        txtChat.TabStop = false;

        // lblTyping
        lblTyping.AutoSize = true;
        lblTyping.ForeColor = SystemColors.GrayText;
        lblTyping.Location = new Point(12, 362);
        lblTyping.Size = new Size(0, 15);
        lblTyping.Text = "";

        // txtMessage
        txtMessage.Location = new Point(12, 384);
        txtMessage.Size = new Size(470, 23);
        txtMessage.KeyDown += txtMessage_KeyDown;
        txtMessage.TextChanged += txtMessage_TextChanged;

        // btnSend
        btnSend.Location = new Point(490, 383);
        btnSend.Size = new Size(112, 25);
        btnSend.Text = "Send";
        btnSend.UseVisualStyleBackColor = true;
        btnSend.Click += btnSend_Click;

        // lblUsers
        lblUsers.AutoSize = true;
        lblUsers.Location = new Point(614, 48);
        lblUsers.Text = "Online:";

        // lstUsers
        lstUsers.Location = new Point(614, 68);
        lstUsers.Size = new Size(140, 270);
        lstUsers.IntegralHeight = false;

        // btnSendFile
        btnSendFile.Location = new Point(614, 344);
        btnSendFile.Size = new Size(140, 25);
        btnSendFile.Text = "Send File to All";
        btnSendFile.UseVisualStyleBackColor = true;
        btnSendFile.Click += btnSendFile_Click;

        // ChatForm
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(766, 420);
        Controls.Add(lblServer);
        Controls.Add(txtServer);
        Controls.Add(lblPort);
        Controls.Add(txtPort);
        Controls.Add(lblUsername);
        Controls.Add(txtUsername);
        Controls.Add(btnConnect);
        Controls.Add(txtChat);
        Controls.Add(lblTyping);
        Controls.Add(txtMessage);
        Controls.Add(btnSend);
        Controls.Add(lblUsers);
        Controls.Add(lstUsers);
        Controls.Add(btnSendFile);
        MinimumSize = new Size(640, 380);
        Text = "C# Network Chat - Client";
        ResumeLayout(false);
        PerformLayout();
    }
}
