namespace Shared;

public enum MessageType
{
    Join,
    Leave,
    Chat,
    Dm,
    Typing,
    UserList,
    History,
    File,
    Error
}

/// <summary>
/// The single wire format used by both the Server and Client. Every line
/// sent over the socket is one of these, serialized as JSON. The Type
/// field tells the receiver how to interpret the rest of the fields.
/// </summary>
public class ChatMessage
{
    public MessageType Type { get; set; }
    public string From { get; set; } = string.Empty;
    public string? To { get; set; }
    public string? Text { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>Populated only on UserList messages.</summary>
    public List<string>? Users { get; set; }

    /// <summary>Populated only on History messages (sent once, right after joining).</summary>
    public List<ChatMessage>? History { get; set; }

    /// <summary>Populated only on File messages.</summary>
    public string? FileName { get; set; }
    public string? FileData { get; set; } // Base64-encoded file bytes
    public long? FileSize { get; set; }
}
