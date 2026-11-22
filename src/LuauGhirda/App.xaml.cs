using System.Windows;
using LuauGhirda.Core;

namespace LuauGhirda;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TweenManager.Initialize();
    }
}
