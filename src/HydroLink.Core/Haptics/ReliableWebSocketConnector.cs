using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Buttplug.Client;
using Buttplug.Core;
using Buttplug.Core.Messages;

namespace VrcHapticsBridge.Core;

/// <summary>
/// Transport workaround for the published 5.0.1 connector's pending-task disposal and
/// fragmented-frame handling. Serialization, messages and device APIs remain Buttplug's.
/// One connector instance is used for exactly one connection.
/// </summary>
internal sealed class ReliableWebSocketConnector(Uri uri) : IButtplugClientConnector, IDisposable
{
    private readonly ClientWebSocket socket = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<ButtplugMessage>> pending = new();
    private readonly ButtplugConnectorJSONParser reader = new();
    private readonly ButtplugConnectorJSONParser writer = new();
    private Task? receiveLoop;
    private int messageId;
    private int ended;
    public bool Connected => socket.State == WebSocketState.Open && Volatile.Read(ref ended) == 0;
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<ButtplugExceptionEventArgs>? InvalidMessageReceived;
    public event EventHandler? Disconnected;

    public async Task ConnectAsync(CancellationToken token = default)
    {
        await socket.ConnectAsync(uri, token).ConfigureAwait(false);
        receiveLoop = Task.Run(ReceiveAsync);
    }

    public async Task<ButtplugMessage> SendAsync(ButtplugMessage msg, CancellationToken token = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        var promise = new TaskCompletionSource<ButtplugMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = checked((uint)Interlocked.Increment(ref messageId));
        msg.Id = id;
        pending[id] = promise;
        try
        {
            await sendGate.WaitAsync(deadline.Token).ConfigureAwait(false);
            try
            {
                if (!Connected) throw new IOException("Intiface-Verbindung geschlossen.");
                var data = Encoding.UTF8.GetBytes(writer.Serialize(msg));
                await socket.SendAsync(data, WebSocketMessageType.Text, true, deadline.Token).ConfigureAwait(false);
            }
            finally { sendGate.Release(); }
            return await promise.Task.WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        finally { pending.TryRemove(id, out _); }
    }

    private async Task ReceiveAsync()
    {
        var buffer = new byte[8192];
        try
        {
            while (Connected && !lifetime.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult part;
                do
                {
                    part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), lifetime.Token).ConfigureAwait(false);
                    if (part.MessageType == WebSocketMessageType.Close) return;
                    if (part.MessageType != WebSocketMessageType.Text || message.Length + part.Count > 1_048_576)
                        throw new IOException("Ungültiger oder zu großer Intiface-WebSocket-Frame.");
                    message.Write(buffer, 0, part.Count);
                } while (!part.EndOfMessage);
                foreach (var msg in reader.Deserialize(new UTF8Encoding(false, true).GetString(message.ToArray())))
                {
                    if (msg.Id == 0) MessageReceived?.Invoke(this, new(msg));
                    else if (pending.TryRemove(msg.Id, out var promise))
                    {
                        if (msg is Error error) promise.TrySetException(ButtplugException.FromError(error));
                        else promise.TrySetResult(msg);
                    }
                    // Late replies to timed-out requests have already been invalidated.
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            InvalidMessageReceived?.Invoke(this, new(new ButtplugClientConnectorException("WebSocket-Empfang fehlgeschlagen.", ex)));
        }
        finally { EndConnection(); }
    }

    private void EndConnection()
    {
        if (Interlocked.Exchange(ref ended, 1) != 0) return;
        lifetime.Cancel();
        socket.Abort();
        foreach (var pair in pending)
            if (pending.TryRemove(pair.Key, out var promise)) promise.TrySetCanceled();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public async Task DisconnectAsync(CancellationToken token = default)
    {
        EndConnection();
        if (receiveLoop != null) await receiveLoop.WaitAsync(token).ConfigureAwait(false);
    }

    public void Dispose()
    {
        EndConnection();
        socket.Dispose();
        // These are disposed only after DisconnectAsync has joined the receive loop.
        lifetime.Dispose();
        sendGate.Dispose();
    }
}
