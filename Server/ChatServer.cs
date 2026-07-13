namespace Server;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Shared;

/// <summary>
/// Listens for incoming TCP connections and coordinates broadcasting,
/// private routing, and history for all connected clients.
/// </summary>
public class ChatServer
{
    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<Guid, ClientHandler> _clients = new();
    private readonly ConcurrentQueue<ChatMessage> _history = new();
    private const int MaxHistory = 50;
    private readonly int _port;

    public ChatServer(int port)
    {
        _port = port;
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public async Task StartAsync()
    {
        _listener.Start();
        Console.WriteLine($"Server started on port {_port}. Waiting for clients...");
        Console.WriteLine("Press Ctrl+C to stop.\n");

        while (true)
        {
            TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
            var id = Guid.NewGuid();
            var handler = new ClientHandler(id, tcpClient, this);
            _clients[id] = handler;

            // Fire and forget: each client is handled on its own async loop
            _ = handler.HandleAsync();
        }
    }

    public void RemoveClient(Guid id)
    {
        if (_clients.TryRemove(id, out var handler))
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {handler.Username} disconnected.");
            _ = BroadcastAsync(new ChatMessage
            {
                Type = MessageType.Leave,
                From = handler.Username,
                Text = $"{handler.Username} has left the chat."
            });
            _ = BroadcastUserListAsync();
        }
    }

    public async Task BroadcastAsync(ChatMessage message, Guid? excludeId = null)
    {
        Console.WriteLine(DescribeForConsole(message));
        string line = MessageSerializer.Serialize(message);

        var sendTasks = _clients
            .Where(kvp => !excludeId.HasValue || kvp.Key != excludeId.Value)
            .Select(kvp => kvp.Value.SendRawAsync(line));

        await Task.WhenAll(sendTasks);
    }

    /// <summary>Sends a message to a single connected user by username. Returns false if not found.</summary>
    public async Task<bool> SendToUserAsync(string username, ChatMessage message)
    {
        var target = _clients.Values.FirstOrDefault(c =>
            string.Equals(c.Username, username, StringComparison.OrdinalIgnoreCase));

        if (target == null) return false;

        await target.SendAsync(message);
        return true;
    }

    public async Task BroadcastUserListAsync()
    {
        var message = new ChatMessage
        {
            Type = MessageType.UserList,
            From = "Server",
            Users = GetConnectedUsernames().ToList()
        };
        await BroadcastAsync(message);
    }

    /// <summary>Only public Chat messages go into history - DMs stay private.</summary>
    public void AddHistory(ChatMessage message)
    {
        _history.Enqueue(message);
        while (_history.Count > MaxHistory)
        {
            _history.TryDequeue(out _);
        }
    }

    public List<ChatMessage> GetHistorySnapshot() => _history.ToList();

    public IEnumerable<string> GetConnectedUsernames() => _clients.Values.Select(c => c.Username);

    private static string DescribeForConsole(ChatMessage m) => m.Type switch
    {
        MessageType.Chat => $"[{m.Timestamp:HH:mm:ss}] {m.From}: {m.Text}",
        MessageType.Dm => $"[{m.Timestamp:HH:mm:ss}] (DM) {m.From} -> {m.To}: {m.Text}",
        MessageType.Join or MessageType.Leave => $"SERVER: {m.Text}",
        MessageType.File => $"[{m.Timestamp:HH:mm:ss}] {m.From} sent a file: {m.FileName}",
        _ => $"[{m.Type}] {m.From}"
    };
}
