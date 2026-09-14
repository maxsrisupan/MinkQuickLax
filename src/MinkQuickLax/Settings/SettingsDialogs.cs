namespace MinkQuickLax.Settings;

/// <summary>Shows a confirmation inside the settings window.</summary>
public interface ISettingsDialogHost
{
    /// <returns>True when the user chose <paramref name="confirmText"/>.</returns>
    Task<bool> ConfirmAsync(string? title, string message, string confirmText, bool danger);
}

/// <summary>Confirmations for the settings view models, shown inside the settings window so they share its style.</summary>
public sealed class SettingsDialogs(ISettingsDialogHost host)
{
    public Task<bool> ConfirmAsync(string? title, string message, string confirmText, bool danger = false) =>
        host.ConfirmAsync(title, message, confirmText, danger);
}
