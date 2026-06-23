using HapticDrive.Asio.Audio.Devices;
using System.Collections.Concurrent;
using System.Linq;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class NativeAsioOutputBackendTests
{
    [Fact]
    [Trait("Category", "Performance")]
    public void NativeCallback_UnderrunWritesZeros()
    {
        var provider = new NativeAsioOutputBackend.QueuedAsioWaveProvider(48_000, 2, 64, 3);
        var buffer = new byte[64 * 2 * sizeof(float)];

        var bytesRead = provider.Read(buffer, 0, buffer.Length);

        Assert.Equal(buffer.Length, bytesRead);
        Assert.All(buffer, value => Assert.Equal((byte)0, value));
        Assert.Equal(1, provider.GetSnapshot().UnderrunCount);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void CallbackPathDoesNotAllocateAfterWarmup()
    {
        var provider = new NativeAsioOutputBackend.QueuedAsioWaveProvider(48_000, 2, 64, 3);
        var buffer = new byte[64 * 2 * sizeof(float)];

        for (var i = 0; i < 64; i++)
        {
            provider.Read(buffer, 0, buffer.Length);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            provider.Read(buffer, 0, buffer.Length);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.True(allocated <= 1_024, $"Expected <= 1024 allocated bytes after warmup, observed {allocated}.");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void NativeCallback_ProducerOverrunNeverBlocks()
    {
        var provider = new NativeAsioOutputBackend.QueuedAsioWaveProvider(48_000, 2, 64, 2);
        var samples = new float[64 * 2];
        var callbackBuffer = new byte[samples.Length * sizeof(float)];

        Assert.True(provider.TryEnqueue(samples));
        Assert.True(provider.TryEnqueue(samples));
        Assert.False(provider.TryEnqueue(samples));

        var started = DateTime.UtcNow;
        var bytesRead = provider.Read(callbackBuffer, 0, callbackBuffer.Length);
        var elapsed = DateTime.UtcNow - started;

        Assert.Equal(callbackBuffer.Length, bytesRead);
        Assert.True(elapsed < TimeSpan.FromMilliseconds(50), $"Expected callback read to complete quickly, observed {elapsed.TotalMilliseconds:0.###} ms.");
        Assert.True(provider.GetSnapshot().DroppedBufferCount > 0);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task NativeQueue_ConcurrentSubmitReadResetStopStress()
    {
        var provider = new NativeAsioOutputBackend.QueuedAsioWaveProvider(48_000, 2, 64, 3);
        var samples = new float[64 * 2];
        var callbackBuffer = new byte[samples.Length * sizeof(float)];
        var exceptions = new ConcurrentQueue<Exception>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        var producer = Task.Run(() =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    provider.TryEnqueue(samples);
                }
                catch (Exception ex)
                {
                    exceptions.Enqueue(ex);
                    return;
                }
            }
        });

        var consumer = Task.Run(() =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    provider.Read(callbackBuffer, 0, callbackBuffer.Length);
                }
                catch (Exception ex)
                {
                    exceptions.Enqueue(ex);
                    return;
                }
            }
        });

        var resetter = Task.Run(() =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    provider.Reset();
                    Thread.Yield();
                }
                catch (Exception ex)
                {
                    exceptions.Enqueue(ex);
                    return;
                }
            }
        });

        await Task.Delay(TimeSpan.FromMilliseconds(300));
        await cancellation.CancelAsync();
        await Task.WhenAll(producer, consumer, resetter);
        provider.Reset();

        Assert.True(exceptions.IsEmpty, string.Join(Environment.NewLine, exceptions.Select(ex => ex.ToString())));
        var snapshot = provider.GetSnapshot();
        Assert.InRange(snapshot.QueuedBufferCount, 0, snapshot.QueueCapacityBuffers);
    }
}
