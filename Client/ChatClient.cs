namespace Client;

using System.Net.Sockets;
using System.Text;
using Shared;

/// <summary>
/// Handles the TCP connection to the chat server: connecting, sending
/// typed messages, listening for incoming ones, and automatically
/// retrying the connection if it drops unexpectedly.
/// Kept independent of any UI code.
/// </summary>
public class ChatClient
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    private string? _username;
    private string? _lastHost;
    private int _lastPort;
    private bool _manualDisconnect;

    public bool IsConnected => _tcpClient?.Connected ?? false;

    public event Action<ChatMessage>? MessageReceived;
    public event Action? Reconnecting;
    public event Action? Reconnected;
    public event Action? ReconnectFailed;

    public async Task ConnectAsync(string host, int port, string username)
    {
        _manualDisconnect = false;
        _username = username;
        _lastHost = host;
        _lastPort = port;

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(host, port);

        _stream = _tcpClient.GetStream();
        _reader = new StreamReader(_stream, Encoding.UTF8);
        _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };

        // First line sent to the server is always a Join message (see server protocol)
        var join = new ChatMessage { Type = MessageType.Join, From = username };
        await SendRawAsync(MessageSerializer.Serialize(join));

        _ = ListenAsync();
    }

    private async Task ListenAsync()
    {
        try
        {
            string? line;
            while (_reader != null && (line = await _reader.ReadLineAsync()) != null)
            {
                var message = MessageSerializer.Deserialize(line);
                if (message != null)
                {
                    MessageReceived?.Invoke(message);
                }
            }
        }
        catch
        {
            // Connection was lost
        }
        finally
        {
            if (!_manualDisconnect)
            {
                _ = TryAutoReconnectAsync();
            }
        }
    }

    private async Task TryAutoReconnectAsync()
    {
        Reconnecting?.Invoke();

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            if (_manualDisconnect) return;
            await Task.Delay(attempt * 1500);

            if (_lastHost == null || _username == null) break;

            try
            {
                await ConnectAsync(_lastHost, _lastPort, _username);
                Reconnected?.Invoke();
                return;
            }
            catch
            {
                // keep retrying
            }
        }

        ReconnectFailed?.Invoke();
    }

    public async Task SendChatAsync(string text) =>
        await SendRawAsync(MessageSerializer.Serialize(new ChatMessage
        {
            Type = MessageType.Chat,
            From = _username ?? "",
            Text = text
        }));

    public async Task SendDmAsync(string to, string text) =>
        await SendRawAsync(MessageSerializer.Serialize(new ChatMessage
        {
            Type = MessageType.Dm,
            From = _username ?? "",
            To = to,
            Text = text
        }));

    public async Task SendTypingAsync() =>
        await SendRawAsync(MessageSerializer.Serialize(new ChatMessage
        {
            Type = MessageType.Typing,
            From = _username ?? ""
        }));

    public async Task SendFileAsync(string? to, string fileName, string base64Data, long size) =>
        await SendRawAsync(MessageSerializer.Serialize(new ChatMessage
        {
            Type = MessageType.File,
            From = _username ?? "",
            To = to,
            FileName = fileName,
            FileData = base64Data,
            FileSize = size
        }));

    private async Task SendRawAsync(string line)
    {
        if (_writer != null)
        {
            await _writer.WriteLineAsync(line);
        }
    }

    public void Disconnect()
    {
        _manualDisconnect = true;
        _reader?.Dispose();
        _writer?.Dispose();
        _stream?.Dispose();
        _tcpClient?.Close();
    }
}
