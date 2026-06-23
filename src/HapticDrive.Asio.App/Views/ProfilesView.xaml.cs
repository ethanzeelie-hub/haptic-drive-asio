using System.Windows;
using System.Windows.Controls;

namespace HapticDrive.Asio.App.Views;

public partial class ProfilesView : UserControl, IAudioProfileProfilesViewSync
{
    internal event RoutedEventHandler? ProfileNameLostFocus;
    internal event RoutedEventHandler? SaveProfileClicked;
    internal event RoutedEventHandler? LoadProfileClicked;
    internal event RoutedEventHandler? ResetProfileClicked;

    public ProfilesView()
    {
        InitializeComponent();
    }

    internal void Apply(ProfilesStatusPresentation presentation)
    {
        ProfileStatusText.Text = presentation.ProfileStatusText;
        ProfilePathText.Text = presentation.ProfilePathText;
        ProfilePhprStatusText.Text = presentation.ProfilePhprStatusText;
        ProfileValidationText.Text = presentation.ProfileValidationText;
    }

    internal void ApplyAudioProfileControlValues(AudioProfileControlValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        ProfileNameTextBox.Text = values.ProfileName;
    }

    internal string? BuildAudioProfileNameInput()
    {
        return ProfileNameTextBox.Text;
    }

    string? IAudioProfileProfilesViewSync.BuildAudioProfileNameInput()
    {
        return BuildAudioProfileNameInput();
    }

    void IAudioProfileProfilesViewSync.ApplyAudioProfileControlValues(AudioProfileControlValues values)
    {
        ApplyAudioProfileControlValues(values);
    }

    private void ProfileNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ProfileNameLostFocus?.Invoke(sender, e);
    }

    private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        SaveProfileClicked?.Invoke(sender, e);
    }

    private void LoadProfileButton_Click(object sender, RoutedEventArgs e)
    {
        LoadProfileClicked?.Invoke(sender, e);
    }

    private void ResetProfileButton_Click(object sender, RoutedEventArgs e)
    {
        ResetProfileClicked?.Invoke(sender, e);
    }
}
