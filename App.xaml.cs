using System.Globalization;
using System.Windows;

namespace LosKrotos.Desktop;

public partial class App : Application
{
    public App()
    {
        var culture = CultureInfo.GetCultureInfo("es-CR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}