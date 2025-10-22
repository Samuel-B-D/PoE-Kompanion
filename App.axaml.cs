using System;

namespace PoELogoutMacro;

using Avalonia;
using Avalonia.Markup.Xaml;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ExitAction(object? sender, EventArgs e)
    {
        Environment.Exit(0);
    }
}