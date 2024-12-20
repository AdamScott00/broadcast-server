using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.CommandLineUtils;

const string URL = "localhost:5000/";
const string httpPrefix = "http://";
const string wsPrefix = "ws://";
HttpListener httpListener = new();
ConcurrentDictionary<Guid, WebSocket> webClients = new();


var app = new CommandLineApplication();

app.HelpOption("-? | -h | --help");

app.Command("start", command =>
{
    command.Description = "Starts the server";

    command.OnExecute(async () =>
    {
        await StartServer(httpPrefix + URL);
        return 0;
    });
});

app.Command("connect", command =>
{
    command.Description = "Connects to the server";
    command.OnExecute(async () =>
    {
        await ConnectAsync(wsPrefix + URL);
        return 0;
    });
});

async Task StartServer(string URL)
{
    httpListener.Prefixes.Add(URL);
    httpListener.Start();
    Console.WriteLine($"Listening on {URL}");

    while (true)
    {
        var context = await httpListener.GetContextAsync();
        if (context.Request.IsWebSocketRequest)
        {
            var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            var clientId = Guid.NewGuid();
            webClients[clientId] = socket;
            Console.WriteLine($"Client {clientId} connected");
            _ = HandleClientAsync(clientId, socket);
        }
    }
}

async Task HandleClientAsync(Guid guid, WebSocket socket)
{
    var buffer = new byte[1024 * 4];
    while (socket.State == WebSocketState.Open)
    {
        try
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                webClients.TryRemove(guid, out _);
                Console.WriteLine($"Client {guid} disconnected");
            }
            else
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine($"Received: {message}");
                await BroadcastMessageAsync(message);
            }
        }
        catch
        {
            webClients.TryRemove(guid, out _);
            Console.WriteLine($"Error with client {guid}");
            break;
        }
    }
}

async Task BroadcastMessageAsync(string message)
{
    var data = Encoding.UTF8.GetBytes(message);
    var tasks = webClients.Values
        .Select(socket => socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None));
    await Task.WhenAll(tasks);
}

//CLIENT

async Task ConnectAsync(string url)
{
    using var socket = new ClientWebSocket();
    await socket.ConnectAsync(new Uri(url), CancellationToken.None);
    Console.WriteLine($"Connected to {url}");

    _ = RecieveMessageAsync(socket);

    while (true)
    {
        var message = Console.ReadLine();
        if (string.IsNullOrEmpty(message)) break;
        
        var data = Encoding.UTF8.GetBytes(message);
        await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
}

async Task RecieveMessageAsync(ClientWebSocket socket)
{
    var buffer = new byte[1024 * 4];
    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Text)
        {
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"Recieved: {message}");
        }
    }
    
}

app.Execute(args);