namespace Client;

using System.IO;
using Shared;

public partial class ChatForm : Form
{
    private readonly ChatClient _client = new();
    private readonly System.Windows.Forms.Timer _typingClearTimer = new() { Interval = 3000 };
    private DateTime _lastTypingSentAt = DateTime.MinValue;
    private string? _myUsername;

    public ChatForm()
    {
        InitializeComponent();
        SetupUserListContextMenu();

        _client.MessageReceived += OnMessageReceived;
        _client.Reconnecting += OnReconnecting;
        _client.Reconnected += OnReconnected;
        _client.ReconnectFailed += OnReconnectFailed;

        _typingClearTimer.Tick += (_, _) =>
        {
            lblTyping.Text = string.Empty;
            _typingClearTimer.Stop();
        };

        UpdateConnectionState(connected: false);
    }

    // ---------- Connection ----------

    private async void btnConnect_Click(object sender, EventArgs e)
    {
        if (_client.IsConnected)
        {
            _client.Disconnect();
            UpdateConnectionState(connected: false);
            AppendSystem("Disconnected.");
            lstUsers.Items.Clear();
            return;
        }

        string host = txtServer.Text.Trim();
        string username = txtUsername.Text.Trim();

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("Please enter a server address and a username.", "Missing info",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!int.TryParse(txtPort.Text.Trim(), out int port))
        {
            MessageBox.Show("Port must be a number.", "Invalid port",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            btnConnect.Enabled = false;
            await _client.ConnectAsync(host, port, username);
            _myUsername = username;
            UpdateConnectionState(connected: true);
            AppendSystem($"Connected to {host}:{port} as {username}.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not connect: {ex.Message}", "Connection failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnConnect.Enabled = true;
        }
    }

    private void OnReconnecting() =>
        InvokeIfNeeded(() => AppendSystem("Connection lost. Attempting to reconnect..."));

    private void OnReconnected() =>
        InvokeIfNeeded(() =>
        {
            UpdateConnectionState(connected: true);
            AppendSystem("Reconnected.");
        });

    private void OnReconnectFailed() =>
        InvokeIfNeeded(() =>
        {
            UpdateConnectionState(connected: false);
            AppendSystem("Could not reconnect. Please connect manually.");
            lstUsers.Items.Clear();
        });

    // ---------- Sending chat / DMs ----------

    private async void btnSend_Click(object sender, EventArgs e) => await SendCurrentMessageAsync();

    private async void txtMessage_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true; // stop the "ding" sound
            await SendCurrentMessageAsync();
        }
    }

    private void txtMessage_TextChanged(object sender, EventArgs e)
    {
        if (!_client.IsConnected || string.IsNullOrEmpty(txtMessage.Text)) return;
        if ((DateTime.Now - _lastTypingSentAt).TotalMilliseconds < 1500) return;

        _lastTypingSentAt = DateTime.Now;
        _ = _client.SendTypingAsync();
    }

    private async Task SendCurrentMessageAsync()
    {
        string raw = txtMessage.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw) || !_client.IsConnected) return;

        if (raw.StartsWith("/msg ", StringComparison.OrdinalIgnoreCase))
        {
            string rest = raw[5..].Trim();
            int spaceIdx = rest.IndexOf(' ');
            if (spaceIdx > 0)
            {
                string target = rest[..spaceIdx];
                string text = rest[(spaceIdx + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    await _client.SendDmAsync(target, text);
                }
            }
            else
            {
                AppendSystem("Usage: /msg username your message");
            }
        }
        else
        {
            await _client.SendChatAsync(raw);
        }

        txtMessage.Clear();
        txtMessage.Focus();
    }

    // ---------- Online users panel: DM shortcut + file sharing ----------

    private void SetupUserListContextMenu()
    {
        var menu = new ContextMenuStrip();

        var dmItem = new ToolStripMenuItem("Direct Message");
        dmItem.Click += (_, _) =>
        {
            if (lstUsers.SelectedItem is string user && user != _myUsername)
            {
                txtMessage.Text = $"/msg {user} ";
                txtMessage.Focus();
                txtMessage.SelectionStart = txtMessage.Text.Length;
            }
        };

        var fileItem = new ToolStripMenuItem("Send File...");
        fileItem.Click += async (_, _) =>
        {
            if (lstUsers.SelectedItem is string user)
            {
                await SendFileDialogAsync(user);
            }
        };

        menu.Items.Add(dmItem);
        menu.Items.Add(fileItem);
        lstUsers.ContextMenuStrip = menu;
    }

    private async void btnSendFile_Click(object sender, EventArgs e) => await SendFileDialogAsync(targetUser: null);

    private async Task SendFileDialogAsync(string? targetUser)
    {
        if (!_client.IsConnected) return;

        using var dialog = new OpenFileDialog();
        if (dialog.ShowDialog() != DialogResult.OK) return;

        var info = new FileInfo(dialog.FileName);
        const long maxBytes = 5 * 1024 * 1024;
        if (info.Length > maxBytes)
        {
            MessageBox.Show("Please choose a file smaller than 5 MB.", "File too large",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        byte[] bytes = await File.ReadAllBytesAsync(dialog.FileName);
        string base64 = Convert.ToBase64String(bytes);
        string fileName = Path.GetFileName(dialog.FileName);

        await _client.SendFileAsync(targetUser, fileName, base64, bytes.LongLength);

        AppendSystem(targetUser == null
            ? $"You sent '{fileName}' to everyone."
            : $"You sent '{fileName}' to {targetUser}.");
    }

    private void ReceiveFile(ChatMessage message)
    {
        try
        {
            string folder = Path.Combine(AppContext.BaseDirectory, "ReceivedFiles");
            Directory.CreateDirectory(folder);

            string safeName = Path.GetFileName(message.FileName ?? "received_file");
            string path = Path.Combine(folder, safeName);
            byte[] bytes = Convert.FromBase64String(message.FileData ?? string.Empty);
            File.WriteAllBytes(path, bytes);

            string scope = message.To == null ? "shared with everyone" : "sent privately to you";
            AppendSystem($"Received file '{safeName}' from {message.From} ({scope}) - saved to {path}");
        }
        catch (Exception ex)
        {
            AppendSystem($"Failed to save an incoming file: {ex.Message}");
        }
    }

    // ---------- Receiving ----------

    private void OnMessageReceived(ChatMessage message) => InvokeIfNeeded(() => HandleMessage(message));

    private void HandleMessage(ChatMessage message)
    {
        switch (message.Type)
        {
            case MessageType.Chat:
                AppendChat(message.From, message.Text, message.Timestamp);
                break;

            case MessageType.Dm:
                AppendDm(message.From, message.To, message.Text, message.Timestamp);
                break;

            case MessageType.Join:
            case MessageType.Leave:
                AppendSystem(message.Text);
                break;

            case MessageType.UserList:
                UpdateUserList(message.Users);
                break;

            case MessageType.History:
                foreach (var historyMessage in message.History ?? new List<ChatMessage>())
                {
                    AppendChat(historyMessage.From, historyMessage.Text, historyMessage.Timestamp);
                }
                break;

            case MessageType.Typing:
                ShowTyping(message.From);
                break;

            case MessageType.File:
                ReceiveFile(message);
                break;

            case MessageType.Error:
                AppendSystem($"Error: {message.Text}");
                break;
        }
    }

    private void ShowTyping(string username)
    {
        lblTyping.Text = $"{username} is typing...";
        _typingClearTimer.Stop();
        _typingClearTimer.Start();
    }

    private void UpdateUserList(List<string>? users)
    {
        lstUsers.Items.Clear();
        if (users == null) return;

        foreach (string name in users)
        {
            lstUsers.Items.Add(name);
        }
    }

    // ---------- Helpers ----------

    private void InvokeIfNeeded(Action action)
    {
        if (InvokeRequired) Invoke(action);
        else action();
    }

    private void AppendMessage(string line) => txtChat.AppendText(line + Environment.NewLine);
    private void AppendSystem(string? text) => AppendMessage($"SERVER: {text}");
    private void AppendChat(string from, string? text, DateTime timestamp) =>
        AppendMessage($"[{timestamp:HH:mm:ss}] {from}: {text}");
    private void AppendDm(string from, string? to, string? text, DateTime timestamp) =>
        AppendMessage($"[{timestamp:HH:mm:ss}] (DM) {from} -> {to}: {text}");

    private void UpdateConnectionState(bool connected)
    {
        btnConnect.Text = connected ? "Disconnect" : "Connect";
        txtServer.Enabled = !connected;
        txtPort.Enabled = !connected;
        txtUsername.Enabled = !connected;
        btnSend.Enabled = connected;
        btnSendFile.Enabled = connected;
        txtMessage.Enabled = connected;
    }
}
