using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace websocket_playground
{
    public interface IWebSocket<TMessage> : IDisposable
    {
        event EventHandler<TMessage> OnMessage;

        Task ConnectAsync();
        Task BeginReceiveAsync();
        Task SendAsync(string message);
    }

    public class WebSocket<TMessage> : IWebSocket<TMessage>
    {
        public event EventHandler<TMessage> OnMessage;
        private ClientWebSocket client;
        private readonly Uri uri;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationToken cancellationToken;

        public WebSocket(Uri uri)
        {
            this.uri = uri;

            client = new ClientWebSocket();

            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
        }

        public async Task ConnectAsync()
        {
            try
            {
                await client.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception while connecting. " +
                                  $"Waiting 2 sec before next reconnection try. Error: '{e.Message}'");
                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                await ReconnectAsync().ConfigureAwait(false);
            }
        }

        private async Task ReconnectAsync()
        {
            client = new ClientWebSocket();
            await ConnectAsync().ConfigureAwait(false);
        }

        public Task BeginReceiveAsync()
        {
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await ReceiveAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception: {ex.Message}");
                    }
                }
            }, cancellationToken);

            return Task.CompletedTask;
        }

        private async Task ReceiveAsync()
        {
            const int chunkSize = 1024 * 4;
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[chunkSize]);

            byte[] resultArrayWithTrailing = null;
            int resultArraySize = 0;
            bool isResultArrayCloned = false;
            MemoryStream ms = null;
            bool endOfMessage = false;

            try
            {
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

                    Console.WriteLine($"Message type {result.MessageType}");

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

                OnMessage?.Invoke(this, JsonConvert.DeserializeObject<TMessage>(message));
            }
            catch (Exception)
            {
                client?.Dispose();
                await ReconnectAsync().ConfigureAwait(false);
            }
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
        private static IWebSocket<string[]> adapterWebSocket;

        public static async Task Main()
        {
            adapterWebSocket = new WebSocket<string[]>(new Uri("ws://127.0.0.1:5002"));
            await adapterWebSocket.ConnectAsync();

            try
            {
                adapterWebSocket.OnMessage += OnProvisioning;

                _ = adapterWebSocket.BeginReceiveAsync();

                //_ = adapterWebSocket.SendAsync("Provisioning data from client.");

                Console.ReadKey();
            }
            finally
            {
                adapterWebSocket?.Dispose();

                Console.WriteLine("Done");
            }

        }

        private static void OnProvisioning(object sender, string[] e)
        {
            Console.WriteLine($"Provisioning received: {string.Join(", ", e)}");
        }
    }
}
