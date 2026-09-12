using PaneShift.Windows;

namespace PaneShift.App.Settings;

public sealed class LoginStartupViewModel(LoginStartup service) : ObservableObject
{
    private LoginStartupState state = service.Read();
    private string error = "";
    public bool IsEnabled
    {
        get => state.IsEnabled;
        set
        {
            if (value == state.IsEnabled) return;
            error = service.SetEnabled(value) ?? "";
            Refresh();
        }
    }
    public bool CanChange => state.CanChange;
    public string Description => state.Description;
    public string Error => error;
    public RelayCommand ToggleCommand => new(() => IsEnabled = !IsEnabled);
    public void Refresh()
    {
        state = service.Read();
        Changed(nameof(IsEnabled)); Changed(nameof(CanChange)); Changed(nameof(Description)); Changed(nameof(Error));
    }
}
