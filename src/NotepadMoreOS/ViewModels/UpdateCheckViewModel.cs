using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotepadMoreOS.Resources;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.ViewModels;

public partial class UpdateCheckViewModel : ObservableObject
{
    private readonly CancellationTokenSource _cancellation = new();
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _updateAvailable;

    public void Cancel() => _cancellation.Cancel();

    [RelayCommand]
    private async Task CheckAsync()
    {
        UpdateAvailable = false;
        Status = Strings.About_CheckingUpdates;
        try
        {
            var latest = await new UpdateCheckService().GetLatestVersionAsync(_cancellation.Token);
            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            UpdateAvailable = latest > new Version(current.Major, current.Minor, Math.Max(0, current.Build));
            Status = UpdateAvailable ? string.Format(Strings.About_UpdateAvailable, latest) : Strings.About_UpToDate;
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or IOException)
        {
            Status = Strings.About_UpdateError;
        }
    }
}
