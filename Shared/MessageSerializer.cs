using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared;

/// <summary>
/// Converts ChatMessage objects to/from the single-line JSON strings that
/// actually go over the socket. Centralized here so Server and Client
/// always agree on the exact format.
/// </summary>
public static class MessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false
    };

    public static string Serialize(ChatMessage message) => JsonSerializer.Serialize(message, Options);

    public static ChatMessage? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<ChatMessage>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
