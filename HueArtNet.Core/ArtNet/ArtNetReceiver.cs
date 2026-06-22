using System.Net;
using System.Net.Sockets;

namespace HueArtNet.Core.ArtNet;

public sealed class ArtNetReceiver : IArtNetFrameSource
{
  private readonly ArtNetReceiverOptions options;
  private CancellationTokenSource? cts;
  private UdpClient? udpClient;
  private Task? receiveTask;

  public ArtNetReceiver(ArtNetReceiverOptions options)
  {
    this.options = options;
    FrameBuffer = new ArtNetFrameBuffer(options.Universes);
  }

  public ArtNetFrameBuffer FrameBuffer { get; }
  public int BoundPort { get; private set; }
  public bool IsRunning { get; private set; }

  public Task StartAsync(CancellationToken cancellationToken)
  {
    if (IsRunning)
      return Task.CompletedTask;

    var receiver = new UdpClient(AddressFamily.InterNetwork);
    receiver.EnableBroadcast = true;
    receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
    receiver.Client.Bind(new IPEndPoint(options.BindAddress, options.Port));

    udpClient = receiver;
    BoundPort = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;
    cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    receiveTask = Task.Run(() => ReceiveLoopAsync(receiver, cts.Token), CancellationToken.None);
    IsRunning = true;

    return Task.CompletedTask;
  }

  public async Task StopAsync()
  {
    var localCts = cts;
    var localClient = udpClient;
    var localTask = receiveTask;

    cts = null;
    udpClient = null;
    receiveTask = null;
    IsRunning = false;

    if (localCts == null)
      return;

    try
    {
      localCts.Cancel();
      localClient?.Close();
      if (localTask != null)
        await localTask;
    }
    catch (OperationCanceledException)
    {
    }
    catch (ObjectDisposedException)
    {
    }
    finally
    {
      localClient?.Dispose();
      localCts.Dispose();
    }
  }

  public async ValueTask DisposeAsync()
  {
    await StopAsync();
  }

  private async Task ReceiveLoopAsync(UdpClient receiver, CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        var result = await receiver.ReceiveAsync(cancellationToken);
        if (ArtDmxPacketParser.TryParse(result.Buffer, out var frame, out _) && frame != null)
          FrameBuffer.AddFrame(frame, DateTimeOffset.UtcNow);
      }
      catch (OperationCanceledException)
      {
        return;
      }
      catch (ObjectDisposedException)
      {
        return;
      }
    }
  }
}
