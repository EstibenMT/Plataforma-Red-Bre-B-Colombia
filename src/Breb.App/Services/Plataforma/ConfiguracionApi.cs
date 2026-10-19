using Breb.App.Services.Abstracciones;

namespace Breb.App.Services.Plataforma;

public sealed class ConfiguracionApi : IConfiguracionApi
{
    private const string ClaveUrl = "api_base_url";
    private const string ClaveCuenta = "ultima_cuenta_id";
    private const string CuentaBase = "11111111-1111-1111-1111-111111111111";

    // 10.0.2.2 es la máquina anfitriona vista desde el emulador de Android.
    private static string UrlPorDefecto =>
        DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5080" : "http://localhost:5080";

    public string BaseUrl
    {
        get => Preferences.Default.Get(ClaveUrl, UrlPorDefecto);
        set => Preferences.Default.Set(ClaveUrl, value);
    }

    public Guid UltimaCuentaId
    {
        get => Guid.TryParse(Preferences.Default.Get(ClaveCuenta, CuentaBase), out var id) ? id : Guid.Parse(CuentaBase);
        set => Preferences.Default.Set(ClaveCuenta, value.ToString());
    }
}
