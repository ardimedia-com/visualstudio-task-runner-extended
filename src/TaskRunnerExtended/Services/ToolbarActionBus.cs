namespace TaskRunnerExtended.Services;

/// <summary>
/// Simple event bus for toolbar command → ViewModel communication.
/// Toolbar commands fire events, the ViewModel subscribes.
/// </summary>
public static class ToolbarActionBus
{
    public static event Action? RefreshRequested;
    public static event Action? StopAllRequested;
    public static event Action? CollapseAllRequested;
    public static event Action<string>? TabChanged;

    public static void RequestRefresh() => RefreshRequested?.Invoke();
    public static void RequestStopAll() => StopAllRequested?.Invoke();
    public static void RequestCollapseAll() => CollapseAllRequested?.Invoke();
    public static void RequestTabChange(string tab) => TabChanged?.Invoke(tab);
}
