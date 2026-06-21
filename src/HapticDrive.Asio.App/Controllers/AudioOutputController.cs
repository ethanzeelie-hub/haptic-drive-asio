using HapticDrive.Asio.App.ViewModels;

namespace HapticDrive.Asio.App.Controllers;

internal sealed class AudioOutputController
{
    public AudioOutputController(OutputDeviceStatusViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public OutputDeviceStatusViewModel ViewModel { get; }

    public void Publish(
        string selectedOutputId,
        string statusText,
        int sampleRate,
        int bufferSize)
    {
        ViewModel.SelectedOutputId = selectedOutputId;
        ViewModel.StatusText = statusText;
        ViewModel.SampleRate = sampleRate;
        ViewModel.BufferSize = bufferSize;
    }
}
