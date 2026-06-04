using System.Runtime.InteropServices;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;

namespace HapticDrive.Input.Windows;

public sealed class WindowsGameControllerButtonStateReader : IInputButtonStateReader
{
    private const uint JoyNoError = 0;
    private const uint JoyReturnButtons = 0x00000080;
    private uint? _joystickId;
    private int _buttonCount;

    public InputDiscoveryMethod Method => InputDiscoveryMethod.WindowsGameController;

    public ValueTask StartAsync(InputDeviceSelection selection, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(selection);

        if (selection.Method != InputDiscoveryMethod.WindowsGameController)
        {
            throw new InvalidOperationException("Stage 2E live listener supports the read-only Windows game-controller input method.");
        }

        if (selection.NativeDeviceIndex is not >= 0)
        {
            throw new InvalidOperationException("Selected Windows game-controller device is missing its native joystick index. Refresh input devices before starting the listener.");
        }

        _joystickId = checked((uint)selection.NativeDeviceIndex.Value);
        _buttonCount = Math.Clamp(selection.ButtonCount ?? 32, 1, 32);
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _joystickId = null;
        _buttonCount = 0;
        return ValueTask.CompletedTask;
    }

    public ValueTask<InputButtonStateSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_joystickId is null)
        {
            return ValueTask.FromResult(InputButtonStateSnapshot.Stopped);
        }

        if (!OperatingSystem.IsWindows())
        {
            return ValueTask.FromResult(new InputButtonStateSnapshot(
                InputListenerStatus.Disconnected,
                new Dictionary<int, InputButtonState>(),
                "Windows game-controller input is only available on Windows."));
        }

        var info = new JoyInfoEx
        {
            Size = (uint)Marshal.SizeOf<JoyInfoEx>(),
            Flags = JoyReturnButtons
        };
        var result = JoyGetPosEx(_joystickId.Value, ref info);
        if (result != JoyNoError)
        {
            return ValueTask.FromResult(new InputButtonStateSnapshot(
                InputListenerStatus.Disconnected,
                new Dictionary<int, InputButtonState>(),
                $"Windows game-controller read failed with winmm result {result}."));
        }

        var buttons = new Dictionary<int, InputButtonState>(_buttonCount);
        for (var buttonId = 1; buttonId <= _buttonCount; buttonId++)
        {
            var mask = 1u << (buttonId - 1);
            buttons[buttonId] = (info.Buttons & mask) != 0
                ? InputButtonState.Pressed
                : InputButtonState.Released;
        }

        return ValueTask.FromResult(new InputButtonStateSnapshot(InputListenerStatus.Listening, buttons));
    }

    [DllImport("winmm.dll")]
    private static extern uint JoyGetPosEx(uint joystickId, ref JoyInfoEx info);

    [StructLayout(LayoutKind.Sequential)]
    private struct JoyInfoEx
    {
        public uint Size;

        public uint Flags;

        public uint XPosition;

        public uint YPosition;

        public uint ZPosition;

        public uint RPosition;

        public uint UPosition;

        public uint VPosition;

        public uint Buttons;

        public uint ButtonNumber;

        public uint PointOfView;

        public uint Reserved1;

        public uint Reserved2;
    }
}
