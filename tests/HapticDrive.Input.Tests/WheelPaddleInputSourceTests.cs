using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Input.Windows;

namespace HapticDrive.Input.Tests;

public sealed class WheelPaddleInputSourceTests
{
    [Fact]
    public async Task PollingSource_StartsStopsAndTracksSelectedDevice()
    {
        var reader = new FakeButtonStateReader();
        await using var source = new PollingWheelPaddleInputSource(reader);
        var selection = CreateSelection();

        await source.StartAsync(selection, CreateMapping());

        Assert.Equal(selection.DeviceId, source.SelectedDeviceId);
        Assert.Equal(InputListenerStatus.Listening, source.GetPaddleSnapshot().Status);

        await source.StopAsync();

        Assert.Equal(InputListenerStatus.Stopped, source.GetPaddleSnapshot().Status);
    }

    [Fact]
    public async Task WindowsGameControllerReader_BlocksZeroButtonSelectedDevice()
    {
        var reader = new WindowsGameControllerButtonStateReader();
        var selection = CreateSelection(buttonCount: 0);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.StartAsync(selection));

        Assert.Contains("0 usable buttons", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WindowsGameControllerReader_StartsOnThirtyTwoButtonSelectedDevice()
    {
        var reader = new WindowsGameControllerButtonStateReader();
        var selection = CreateSelection(buttonCount: 32);

        await reader.StartAsync(selection);

        await reader.StopAsync();
    }

    [Fact]
    public void Processor_MapsLeftAndRightPaddlePresses()
    {
        var processor = CreateProcessor();
        var events = new List<WheelPaddleInputEvent>();
        processor.PaddleInputReceived += (_, e) => events.Add(e);
        processor.RefreshSelection(CreateSelection(), InputListenerStatus.Listening);
        processor.RefreshMapping(CreateMapping());

        var left = processor.ProcessButtonState(4, InputButtonState.Pressed);
        processor.ProcessButtonState(4, InputButtonState.Released);
        var right = processor.ProcessButtonState(5, InputButtonState.Pressed);

        Assert.NotNull(left);
        Assert.NotNull(right);
        Assert.Equal(PaddleSide.Left, left.PaddleSide);
        Assert.Equal(PaddleSide.Right, right.PaddleSide);
        Assert.Equal([PaddleSide.Left, PaddleSide.Right], events.Select(item => item.PaddleSide));
        Assert.Equal(InputButtonState.Pressed, processor.GetSnapshot().RightPaddleState);
    }

    [Fact]
    public void Processor_DoesNotMapUnmappedButtons()
    {
        var processor = CreateProcessor();
        processor.RefreshSelection(CreateSelection(), InputListenerStatus.Listening);
        processor.RefreshMapping(CreateMapping());

        var mapped = processor.ProcessButtonState(7, InputButtonState.Pressed);

        Assert.Null(mapped);
        Assert.Equal(0, processor.GetSnapshot().PaddlePressCount);
        Assert.Equal(7, processor.GetSnapshot().LastChangedButtonId);
    }

    [Fact]
    public void Processor_FiresOnceForRisingEdgeAndDoesNotRepeatWhileHeld()
    {
        var processor = CreateProcessor();
        processor.RefreshSelection(CreateSelection(), InputListenerStatus.Listening);
        processor.RefreshMapping(CreateMapping());

        var first = processor.ProcessButtonState(4, InputButtonState.Pressed);
        var held = processor.ProcessButtonState(4, InputButtonState.Pressed);
        var stillHeld = processor.ProcessButtonState(4, InputButtonState.Pressed);

        Assert.NotNull(first);
        Assert.Null(held);
        Assert.Null(stillHeld);
        Assert.Equal(1, processor.GetSnapshot().PaddlePressCount);
    }

    [Fact]
    public void Processor_ReleaseResetsStateAndRepeatedPressFiresAgain()
    {
        var clock = new FakeInputEventClock();
        var processor = CreateProcessor(clock);
        processor.RefreshSelection(CreateSelection(), InputListenerStatus.Listening);
        processor.RefreshMapping(CreateMapping() with { DebounceDuration = TimeSpan.FromMilliseconds(5) });

        var first = processor.ProcessButtonState(4, InputButtonState.Pressed);
        processor.ProcessButtonState(4, InputButtonState.Released);
        clock.Advance(TimeSpan.FromMilliseconds(6));
        var second = processor.ProcessButtonState(4, InputButtonState.Pressed);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(2, processor.GetSnapshot().PaddlePressCount);
    }

    [Fact]
    public void Processor_PopulatesUtcAndStopwatchTimestamps()
    {
        var clock = new FakeInputEventClock();
        clock.Advance(TimeSpan.FromMilliseconds(12));
        var processor = CreateProcessor(clock);
        processor.RefreshSelection(CreateSelection(), InputListenerStatus.Listening);
        processor.RefreshMapping(CreateMapping());

        var mapped = processor.ProcessButtonState(5, InputButtonState.Pressed);

        Assert.NotNull(mapped);
        Assert.Equal(clock.UtcNow, mapped.TimestampUtc);
        Assert.Equal(clock.StopwatchTicks, mapped.StopwatchTicks);
    }

    [Fact]
    public void Processor_DebounceSuppressesNoisyDuplicatePresses()
    {
        var clock = new FakeInputEventClock();
        var processor = CreateProcessor(clock);
        processor.RefreshSelection(CreateSelection(), InputListenerStatus.Listening);
        processor.RefreshMapping(CreateMapping() with { DebounceDuration = TimeSpan.FromMilliseconds(20) });

        var first = processor.ProcessButtonState(4, InputButtonState.Pressed);
        processor.ProcessButtonState(4, InputButtonState.Released);
        clock.Advance(TimeSpan.FromMilliseconds(5));
        var noisy = processor.ProcessButtonState(4, InputButtonState.Pressed);
        processor.ProcessButtonState(4, InputButtonState.Released);
        clock.Advance(TimeSpan.FromMilliseconds(20));
        var later = processor.ProcessButtonState(4, InputButtonState.Pressed);

        Assert.NotNull(first);
        Assert.Null(noisy);
        Assert.NotNull(later);
        Assert.Equal(2, processor.GetSnapshot().PaddlePressCount);
        Assert.Equal(1, processor.GetSnapshot().DebounceSuppressedCount);
    }

    [Fact]
    public void Processor_DebounceIsPerMappedPaddleButton()
    {
        var clock = new FakeInputEventClock();
        var processor = CreateProcessor(clock);
        processor.RefreshSelection(CreateSelection(), InputListenerStatus.Listening);
        processor.RefreshMapping(CreateMapping() with { DebounceDuration = TimeSpan.FromMilliseconds(20) });

        var right = processor.ProcessButtonState(4, InputButtonState.Pressed);
        processor.ProcessButtonState(4, InputButtonState.Released);
        clock.Advance(TimeSpan.FromMilliseconds(5));
        var left = processor.ProcessButtonState(5, InputButtonState.Pressed);
        processor.ProcessButtonState(5, InputButtonState.Released);
        var noisyRight = processor.ProcessButtonState(4, InputButtonState.Pressed);

        Assert.NotNull(right);
        Assert.NotNull(left);
        Assert.Null(noisyRight);
        Assert.Equal(2, processor.GetSnapshot().PaddlePressCount);
        Assert.Equal(1, processor.GetSnapshot().DebounceSuppressedCount);
    }

    [Fact]
    public async Task PollingSource_CapturesListenerErrorsSafely()
    {
        var reader = new FakeButtonStateReader
        {
            ThrowOnRead = true
        };
        await using var source = new PollingWheelPaddleInputSource(
            reader,
            new WheelPaddleInputSourceOptions { PollInterval = TimeSpan.FromMilliseconds(1) });

        await source.StartAsync(CreateSelection(), CreateMapping());
        await WaitUntilAsync(() => source.GetPaddleSnapshot().Status == InputListenerStatus.Error);

        var snapshot = source.GetPaddleSnapshot();
        Assert.Equal(InputListenerStatus.Error, snapshot.Status);
        Assert.Contains("synthetic read failure", snapshot.LastErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PollingSource_RepresentsDisconnectSafely()
    {
        var reader = new FakeButtonStateReader
        {
            Snapshot = new InputButtonStateSnapshot(
                InputListenerStatus.Disconnected,
                new Dictionary<int, InputButtonState>(),
                "synthetic disconnect")
        };
        await using var source = new PollingWheelPaddleInputSource(
            reader,
            new WheelPaddleInputSourceOptions { PollInterval = TimeSpan.FromMilliseconds(1) });

        await source.StartAsync(CreateSelection(), CreateMapping());
        await WaitUntilAsync(() => source.GetPaddleSnapshot().Status == InputListenerStatus.Disconnected);

        var snapshot = source.GetPaddleSnapshot();
        Assert.Equal(InputListenerStatus.Disconnected, snapshot.Status);
        Assert.Contains("synthetic disconnect", snapshot.LastErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PollingSource_RaisesMappedDiagnosticsButNoShiftIntentInStage2E()
    {
        var reader = new FakeButtonStateReader
        {
            Snapshot = new InputButtonStateSnapshot(
                InputListenerStatus.Listening,
                new Dictionary<int, InputButtonState>
                {
                    [4] = InputButtonState.Pressed,
                    [5] = InputButtonState.Released
                })
        };
        await using var source = new PollingWheelPaddleInputSource(
            reader,
            new WheelPaddleInputSourceOptions { PollInterval = TimeSpan.FromMilliseconds(1) });
        var paddleEvents = 0;
        var shiftIntentEvents = 0;
        source.PaddleInputReceived += (_, _) => paddleEvents++;
        source.ShiftIntentReceived += (_, _) => shiftIntentEvents++;

        await source.StartAsync(CreateSelection(), CreateMapping());
        await WaitUntilAsync(() => source.GetPaddleSnapshot().PaddlePressCount == 1);

        Assert.Equal(1, paddleEvents);
        Assert.Equal(0, shiftIntentEvents);
    }

    [Fact]
    public async Task PollingSource_ShiftIntentEventSubscriptionAndUnsubscriptionBehaveCorrectly()
    {
        var reader = new FakeButtonStateReader
        {
            Snapshot = new InputButtonStateSnapshot(
                InputListenerStatus.Listening,
                new Dictionary<int, InputButtonState>
                {
                    [4] = InputButtonState.Pressed
                })
        };
        await using var source = new PollingWheelPaddleInputSource(
            reader,
            new WheelPaddleInputSourceOptions { PollInterval = TimeSpan.FromMilliseconds(1) });
        var shiftIntentEvents = 0;
        EventHandler<ShiftIntentEvent> handler = (_, _) => shiftIntentEvents++;
        source.ShiftIntentReceived += handler;
        source.ShiftIntentReceived -= handler;

        await source.StartAsync(CreateSelection(), CreateMapping());
        await WaitUntilAsync(() => source.GetPaddleSnapshot().PaddlePressCount == 1);

        Assert.Equal(0, shiftIntentEvents);
    }

    [Fact]
    public void ListenerAbstractions_DoNotExposeUsbWriteCapableApi()
    {
        var forbiddenTerms = new[] { "Write", "Send", "Output", "Feature", "Vibrate", "Command", "Report" };
        var methodNames = new[]
            {
                typeof(IWheelPaddleInputSource),
                typeof(IInputButtonStateReader),
                typeof(PollingWheelPaddleInputSource),
                typeof(WheelPaddleInputProcessor),
                typeof(WindowsGameControllerButtonStateReader)
            }
            .SelectMany(type => type.GetMethods())
            .Where(method => method.DeclaringType != typeof(object))
            .Select(method => method.Name)
            .ToArray();

        foreach (var methodName in methodNames)
        {
            Assert.DoesNotContain(forbiddenTerms, term => methodName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static WheelPaddleInputProcessor CreateProcessor(IInputEventClock? clock = null)
    {
        return new WheelPaddleInputProcessor(clock);
    }

    private static InputDeviceSelection CreateSelection(int buttonCount = 12)
    {
        return new InputDeviceSelection(
            "windowsgamecontroller:test",
            "Synthetic GT Neo wheel input",
            InputDiscoveryMethod.WindowsGameController,
            NativeDeviceIndex: 0,
            ButtonCount: buttonCount);
    }

    private static WheelPaddleMapping CreateMapping()
    {
        return new WheelPaddleMapping
        {
            SelectedDeviceId = "windowsgamecontroller:test",
            SelectedMethod = InputDiscoveryMethod.WindowsGameController,
            LeftPaddleButtonId = 4,
            RightPaddleButtonId = 5,
            DebounceDuration = TimeSpan.FromMilliseconds(20)
        };
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class FakeInputEventClock : IInputEventClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

        public long StopwatchTicks { get; private set; } = 1_000;

        public InputEventTimestamp GetTimestamp()
        {
            return new InputEventTimestamp(UtcNow, StopwatchTicks);
        }

        public void Advance(TimeSpan duration)
        {
            UtcNow += duration;
            StopwatchTicks += (long)duration.TotalMilliseconds * 1_000;
        }
    }

    private sealed class FakeButtonStateReader : IInputButtonStateReader
    {
        public bool ThrowOnRead { get; init; }

        public bool Started { get; private set; }

        public InputButtonStateSnapshot Snapshot { get; init; } = new(
            InputListenerStatus.Listening,
            new Dictionary<int, InputButtonState>
            {
                [4] = InputButtonState.Released,
                [5] = InputButtonState.Released
            });

        public InputDiscoveryMethod Method => InputDiscoveryMethod.WindowsGameController;

        public ValueTask StartAsync(InputDeviceSelection selection, CancellationToken cancellationToken = default)
        {
            Started = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            Started = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask<InputButtonStateSnapshot> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("synthetic read failure");
            }

            return ValueTask.FromResult(Snapshot);
        }
    }
}
