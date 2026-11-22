using System.Windows.Controls;

namespace LuauGhirda.UI;

/// <summary>
/// Single-window overlay: a full-size panel layered above the whole UI where
/// transient things (dropdown menus) render, so they never get clipped by
/// scroll viewers or page layouts.
/// </summary>
public static class OverlayService
{
    private static Panel? _host;

    public static void Register(Panel host) => _host = host;

    public static Panel? Resolve(object requester) => _host;
}
