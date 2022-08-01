using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace websocket_playground
{
    public interface IWebSocket : IDisposable
    {
        event EventHandler<string> OnMessage;

        Task ConnectAsync(Uri uri);
        Task BeginReceiveAsync();
        Task SendAsync(string message);
    }

    public class WebSocket : IWebSocket
    {
        public event EventHandler<string> OnMessage;
        private readonly ClientWebSocket client;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationToken cancellationToken;

        public WebSocket()
        {
            client = new ClientWebSocket();

            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
        }

        public Task ConnectAsync(Uri uri)
        {
            return client.ConnectAsync(uri, cancellationToken);
        }

        public Task BeginReceiveAsync()
        {
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string message = await ReceiveAsync().ConfigureAwait(false);
                    OnMessage?.Invoke(this, message);
                }
            }, cancellationToken);

            return Task.CompletedTask;
        }

        private async Task<string> ReceiveAsync()
        {
            const int chunkSize = 1024 * 4;
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[chunkSize]);

            byte[] resultArrayWithTrailing = null;
            int resultArraySize = 0;
            bool isResultArrayCloned = false;
            MemoryStream ms = null;
            bool endOfMessage = false;

            while (!endOfMessage)
            {
                WebSocketReceiveResult result = await client.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

                byte[] currentChunk = buffer.Array;
                int currentChunkSize = result.Count;

                bool isFirstChunk = resultArrayWithTrailing == null;
                if (isFirstChunk)
                {
                    resultArraySize += currentChunkSize;
                    resultArrayWithTrailing = currentChunk;
                    isResultArrayCloned = false;
                }
                else
                {
                    if (ms == null)
                    {
                        ms = new MemoryStream();
                        ms.Write(resultArrayWithTrailing, 0, resultArraySize);
                    }

                    if (currentChunk != null)
                    {
                        ms.Write(currentChunk, buffer.Offset, currentChunkSize);
                    }
                }

                endOfMessage = result.EndOfMessage;

                if (!isResultArrayCloned)
                {
                    resultArrayWithTrailing = resultArrayWithTrailing?.ToArray();
                    isResultArrayCloned = true;
                }
            }

            string message = ms != null ?
                Encoding.UTF8.GetString(ms.ToArray()) :
                resultArrayWithTrailing != null ?
                    Encoding.UTF8.GetString(resultArrayWithTrailing, 0, resultArraySize) :
                    null;

            return message;
        }

        public Task SendAsync(string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            var messageSegment = new ArraySegment<byte>(buffer);

            return client.SendAsync(messageSegment, WebSocketMessageType.Text, true, cancellationToken);
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            client?.Dispose();
        }
    }

    internal class Program
    {
        private static IWebSocket provisioningWebSocket;
        private static IWebSocket agentVersionWebSocket;
        // 1. BeginReceiveAsync
        // 2. SendAsync

        public static async Task Main()
        {
            provisioningWebSocket = new WebSocket();
            await provisioningWebSocket.ConnectAsync(new Uri("ws://127.0.0.1:5002/provisioning")).ConfigureAwait(false);

            agentVersionWebSocket = new WebSocket();
            await agentVersionWebSocket.ConnectAsync(new Uri("ws://127.0.0.1:5002/agentVersion")).ConfigureAwait(false);

            try
            {
                await BeginReceiveAsync(provisioningWebSocket, OnProvisioning).ConfigureAwait(false);
                await BeginReceiveAsync(agentVersionWebSocket, OnAgentVersion).ConfigureAwait(false);

                await provisioningWebSocket.SendAsync("Provisioning data from client.");
                await agentVersionWebSocket.SendAsync("Agent version data from client.");

                Console.ReadKey();
            }
            finally
            {
                provisioningWebSocket?.Dispose();
                agentVersionWebSocket?.Dispose();
            }

        }

        private static async Task BeginReceiveAsync(IWebSocket webSocket, EventHandler<string> onNewMessage)
        {
            webSocket.OnMessage += onNewMessage;

            await webSocket.BeginReceiveAsync().ConfigureAwait(false);
        }

        private static void OnProvisioning(object sender, string e)
        {
            Console.WriteLine($"Provisioning received: {e}");
        }

        private static void OnAgentVersion(object sender, string e)
        {
            Console.WriteLine($"Agent version received: {e}");
        }
    }
}
