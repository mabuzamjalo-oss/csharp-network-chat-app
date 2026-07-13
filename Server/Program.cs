using Server;

Console.WriteLine("=== C# Network Chat Server ===");
Console.Write("Enter port to listen on (default 5000): ");
string? input = Console.ReadLine();
int port = string.IsNullOrWhiteSpace(input) ? 5000 : int.Parse(input);

var server = new ChatServer(port);
await server.StartAsync();
