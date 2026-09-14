using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Platform.SystemIntegration;
using MinkQuickLax.Services;

namespace MinkQuickLax.Settings;

public sealed record BackupChoice(BackupInfo Backup, string Text);

/// <summary>The Data page (SPEC 4.8): back up, restore, open the folder, reset everything, delete everything.</summary>
public sealed partial class DataViewModel(
    ConfigStore store,
    StartupRegistration startup,
    SettingsDialogs dialogs,
    Localizer text,
    Application application,
    ILogger<DataViewModel> logger) : ObservableObject
{
    public ObservableCollection<BackupChoice> Backups { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    private BackupChoice? _selectedBackup;

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private bool _messageIsError;

    public bool HasBackups => Backups.Count > 0;

    public void Refresh()
    {
        Backups.Clear();
        foreach (var backup in store.Backups.List())
        {
            Backups.Add(new BackupChoice(backup, Describe(backup)));
        }
        SelectedBackup = Backups.FirstOrDefault();
        OnPropertyChanged(nameof(HasBackups));
    }

    [RelayCommand]
    private void BackupNow()
    {
        var backup = store.BackupNow();
        Refresh();
        if (backup is not null)
        {
            Show(text.Format("Data_BackedUp", Describe(backup)), error: false);
        }
    }

    [RelayCommand(CanExecute = nameof(HasBackup))]
    private async Task RestoreAsync()
    {
        if (SelectedBackup is not { } choice
            || !await dialogs.ConfirmAsync(text["Data_Restore"], text.Format("Data_RestoreConfirm", choice.Text), text["Data_RestoreButton"].TrimEnd('…')))
        {
            return;
        }
        try
        {
            store.RestoreFrom(choice.Backup);
            Refresh();
            Show(text.Format("Data_Restored", choice.Text), error: false);
        }
        catch (Exception ex) when (ex is ConfigFormatException or IOException or UnauthorizedAccessException)
        {
            LogRestoreFailed(logger, ex, choice.Backup.Path);
            Show(text["Data_RestoreFailed"], error: true);
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        Directory.CreateDirectory(store.Paths.Directory);
        Process.Start(new ProcessStartInfo { FileName = store.Paths.Directory, UseShellExecute = true })?.Dispose();
    }

    [RelayCommand]
    private async Task ResetAllAsync()
    {
        if (await dialogs.ConfirmAsync(text["Data_ResetAll"].TrimEnd('…'), text["Data_ResetAllConfirm"], text["Settings_Reset"], danger: true))
        {
            store.ResetToDefaults();
            Refresh();
        }
    }

    [RelayCommand]
    private async Task DeleteAllAsync()
    {
        if (!await dialogs.ConfirmAsync(text["Data_DeleteAll"].TrimEnd('…'), text["Data_DeleteAllConfirm"], text["Common_Delete"], danger: true))
        {
            return;
        }
        LogDeletingAll(logger, store.Paths.Directory);
        startup.Disable();
        try
        {
            store.DeleteAllData();
            if (Directory.Exists(AppInfo.IconCacheDirectory))
            {
                Directory.Delete(AppInfo.IconCacheDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Whatever could not be removed stays; the app still exits as asked.
            LogDeleteFailed(logger, ex);
        }
        application.Shutdown();
    }

    private bool HasBackup => SelectedBackup is not null;

    private string Describe(BackupInfo backup) => backup.CreatedAt.LocalDateTime.ToString("g", text.Culture);

    private void Show(string message, bool error)
    {
        MessageIsError = error;
        Message = message;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not restore {Path}")]
    private static partial void LogRestoreFailed(ILogger logger, Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deleting all data in {Directory} and exiting")]
    private static partial void LogDeletingAll(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Some data could not be deleted")]
    private static partial void LogDeleteFailed(ILogger logger, Exception ex);
}
