namespace Server;

using System.Net.Sockets;
using System.Text;
using Shared;

/// <summary>
/// Wraps a single connected client's socket, parses incoming JSON messages,
/// and routes each one (chat, DM, typing, file) to the right place.
/// </summary>
public class ClientHandler
{
    private readonly TcpClient _tcpClient;
    private readonly ChatServer _server;
    private readonly NetworkStream _stream;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    public Guid Id { get; }
    public string Username { get; private set; } = "Unknown";

    public ClientHandler(Guid id, TcpClient tcpClient, ChatServer server)
    {
        Id = id;
        _tcpClient = tcpClient;
        _server = server;
        _stream = _tcpClient.GetStream();
        _reader = new StreamReader(_stream, Encoding.UTF8);
        _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
    }

    public async Task HandleAsync()
    {
        try
        {
            // Protocol: the first line a client sends is a Join message carrying its username
            string? firstLine = await _reader.ReadLineAsync();
            var join = MessageSerializer.Deserialize(firstLine);
            Username = (join?.Type == MessageType.Join && !string.IsNullOrWhiteSpace(join.From))
                ? join.From
                : $"Guest{Id.ToString()[..4]}";

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {Username} connected.");

            // Send this new client the recent public chat history before anything else
            await SendAsync(new ChatMessage
            {
                Type = MessageType.History,
                From = "Server",
                History = _server.GetHistorySnapshot()
            });

            await _server.BroadcastAsync(new ChatMessage
            {
                Type = MessageType.Join,
                From = Username,
                Text = $"{Username} has joined the chat."
            }, Id);

            await _server.BroadcastUserListAsync();

            string? line;
            while ((line = await _reader.ReadLineAsync()) != null)
            {
                var incoming = MessageSerializer.Deserialize(line);
                if (incoming == null) continue;

                await RouteMessageAsync(incoming);
            }
        }
        catch (IOException)
        {
            // Client dropped the connection abruptly - treat as a normal disconnect
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client {Username}: {ex.Message}");
        }
        finally
        {
            _server.RemoveClient(Id);
            _reader.Dispose();
            _writer.Dispose();
            _tcpClient.Close();
        }
    }

    private async Task RouteMessageAsync(ChatMessage incoming)
    {
        switch (incoming.Type)
        {
            case MessageType.Chat:
                var chat = new ChatMessage { Type = MessageType.Chat, From = Username, Text = incoming.Text };
                _server.AddHistory(chat);
                await _server.BroadcastAsync(chat);
                break;

            case MessageType.Dm:
                await HandleDmAsync(incoming);
                break;

            case MessageType.Typing:
                await _server.BroadcastAsync(new ChatMessage { Type = MessageType.Typing, From = Username }, Id);
                break;

            case MessageType.File:
                await HandleFileAsync(incoming);
                break;

            default:
                // Join/Leave/UserList/History/Error are server-to-client only; ignore if a client sends one
                break;
        }
    }

    private async Task HandleDmAsync(ChatMessage incoming)
    {
        if (string.IsNullOrWhiteSpace(incoming.To))
        {
            await SendAsync(new ChatMessage
            {
                Type = MessageType.Error,
                From = "Server",
                Text = "Direct message needs a username: /msg username your text"
            });
            return;
        }

        var dm = new ChatMessage { Type = MessageType.Dm, From = Username, To = incoming.To, Text = incoming.Text };
        bool delivered = await _server.SendToUserAsync(incoming.To, dm);

        // Echo back to the sender too, so their own window shows the DM they sent
        if (!string.Equals(incoming.To, Username, StringComparison.OrdinalIgnoreCase))
        {
            await SendAsync(dm);
        }

        if (!delivered)
        {
            await SendAsync(new ChatMessage
            {
                Type = MessageType.Error,
                From = "Server",
                Text = $"'{incoming.To}' is not online."
            });
        }
    }

    private async Task HandleFileAsync(ChatMessage incoming)
    {
        var file = new ChatMessage
        {
            Type = MessageType.File,
            From = Username,
            To = incoming.To,
            FileName = incoming.FileName,
            FileData = incoming.FileData,
            FileSize = incoming.FileSize
        };

        if (string.IsNullOrWhiteSpace(incoming.To))
        {
            // No target specified = share with everyone
            await _server.BroadcastAsync(file);
            return;
        }

        bool delivered = await _server.SendToUserAsync(incoming.To, file);
        if (!string.Equals(incoming.To, Username, StringComparison.OrdinalIgnoreCase))
        {
            await SendAsync(file);
        }

        if (!delivered)
        {
            await SendAsync(new ChatMessage
            {
                Type = MessageType.Error,
                From = "Server",
                Text = $"'{incoming.To}' is not online."
            });
        }
    }

    public async Task SendAsync(ChatMessage message) => await SendRawAsync(MessageSerializer.Serialize(message));

    public async Task SendRawAsync(string line)
    {
        try
        {
            await _writer.WriteLineAsync(line);
        }
        catch
        {
            // Ignore write failures here; the client's own read loop will
            // detect the disconnect and clean up via RemoveClient.
        }
    }
}
