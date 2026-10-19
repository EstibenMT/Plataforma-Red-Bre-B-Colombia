using Breb.App.Services.Abstracciones;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Breb.App.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IConfiguracionApi _configuracion;

    public LoginViewModel(
        IAuthService auth,
        INavegacionService navegacion,
        IDialogoService dialogos,
        IConfiguracionApi configuracion)
        : base(auth, navegacion, dialogos)
    {
        _configuracion = configuracion;
        _servidorUrl = configuracion.BaseUrl;
    }

    [ObservableProperty]
    private string _usuario = string.Empty;

    [ObservableProperty]
    private string _clave = string.Empty;

    [ObservableProperty]
    private string _servidorUrl;

    /// <summary>La conexión al servidor es una opción avanzada: plegada de forma predeterminada.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoConexion))]
    private bool _mostrarConexion;

    public string TextoConexion => MostrarConexion ? "Ocultar conexión" : "Configurar conexión";

    [RelayCommand]
    private void AlternarConexion() => MostrarConexion = !MostrarConexion;

    /// <summary>Con una sesión vigente guardada se salta directo a Saldo.</summary>
    public async Task InicializarAsync()
    {
        try
        {
            if (await Auth.ObtenerSesionVigenteAsync() is not null)
            {
                await Navegacion.IrASaldoAsync();
            }
        }
        catch (Exception)
        {
            // Si algo falla al leer la sesión, el usuario simplemente inicia sesión de nuevo.
        }
    }

    [RelayCommand]
    private Task IngresarAsync() => EjecutarAsync(IngresarCoreAsync);

    private async Task IngresarCoreAsync()
    {
        var usuario = Usuario.Trim();
        if (usuario.Length == 0)
        {
            MensajeError = "Ingrese su usuario.";
            return;
        }

        if (string.IsNullOrEmpty(Clave))
        {
            MensajeError = "Ingrese su contraseña.";
            return;
        }

        var url = ServidorUrl.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MostrarConexion = true;
            MensajeError = "La dirección del servidor no es válida. Corríjala en «Configurar conexión». Ejemplo: http://10.0.2.2:5080";
            return;
        }

        var baseUrl = url.TrimEnd('/');
        var resultado = await Auth.IniciarSesionAsync(usuario, Clave, baseUrl: baseUrl);
        if (!resultado.EsExito)
        {
            await ManejarErrorAsync(resultado.Error);
            return;
        }

        // Solo una URL con la que el login funcionó se conserva para las siguientes llamadas.
        _configuracion.BaseUrl = baseUrl;

        Clave = string.Empty;
        await Navegacion.IrASaldoAsync();
    }
}
