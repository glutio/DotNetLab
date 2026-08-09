using System.ComponentModel;

namespace DotNetLab;

public abstract class CustomComponentBase : ComponentBase
{
    protected async Task RefreshAsync()
    {
        _ = InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    protected void StartWatching(INotifyPropertyChanged target)
    {
        target.PropertyChanged += OnWatchedPropertyChanged;
    }

    protected void StopWatching(INotifyPropertyChanged? target)
    {
        target?.PropertyChanged -= OnWatchedPropertyChanged;
    }

    protected void OnWatchedPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvokeAsync(StateHasChanged);
    }
}
