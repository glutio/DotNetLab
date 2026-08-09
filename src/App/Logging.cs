using System.ComponentModel;
using DotNetLab.Lab;

namespace DotNetLab;

internal sealed class Logging : IDisposable
{
    private readonly SettingsService settingsService;

    public Logging(SettingsService settingsService)
    {
        this.settingsService = settingsService;

        UpdateLogLevel();

        settingsService.PropertyChanged += OnSettingsPropertyChanged;
    }

    public LogLevel LogLevel { get; private set; }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsService.DebugLogs) or nameof(SettingsService.TraceLogs))
        {
            UpdateLogLevel();
        }
    }

    private void UpdateLogLevel()
    {
        LogLevel = settingsService.TraceLogs
            ? LogLevel.Trace
            : settingsService.DebugLogs
            ? LogLevel.Debug
            : LogLevel.Information;

        if (!settingsService.DebugLogs)
        {
            settingsService.TraceLogs = false;
        }
        else if (settingsService.TraceLogs)
        {
            settingsService.DebugLogs = true;
        }
    }

    public void Dispose()
    {
        settingsService.PropertyChanged -= OnSettingsPropertyChanged;
    }
}

internal static class LoggingUtil
{
    extension(ILogger logger)
    {
        public void LogErrorAndAssert(string message)
        {
            logger.LogError(message);
            Debug.Fail(message);
        }
    }
}
