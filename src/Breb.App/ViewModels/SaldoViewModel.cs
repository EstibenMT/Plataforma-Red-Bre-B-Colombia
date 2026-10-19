using Breb.App.Models;
using Breb.App.Services.Abstracciones;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Breb.App.ViewModels;

public partial class SaldoViewModel : ViewModelBase
{
    private readonly IBrebApi _api;
    private readonly IConfiguracionApi _configuracion;

    public SaldoViewModel(
        IBrebApi api,
        IAuthService auth,
        INavegacionService navegacion,
        IDialogoService dialogos,
        IConfiguracionApi configuracion)
        : base(auth, navegacion, dialogos)
    {
        _api = api;
        _configuracion = configuracion;
        _cuentaIdTexto = configuracion.UltimaCuentaId.ToString();
    }

    // La cuenta no se edita en pantalla: la API no relaciona usuarios con cuentas
    // y un GUID tecleado a mano solo produce errores. Se muestra como dato.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CuentaCorta))]
    private string _cuentaIdTexto;

    public string CuentaCorta => CuentaIdTexto.Length >= 13
        ? $"{CuentaIdTexto[..8]}…{CuentaIdTexto[^4..]}"
        : CuentaIdTexto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneSaldo), nameof(DisponibleTexto))]
    private decimal? _saldoDisponible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RetenidoTexto))]
    private decimal? _saldoRetenido;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActualizadoTexto))]
    private DateTimeOffset? _ultimaActualizacion;

    [ObservableProperty]
    private string _usuario = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayAvisoRol), nameof(AvisoRol), nameof(PuedeTransferir))]
    private string _rol = string.Empty;

    public bool TieneSaldo => SaldoDisponible.HasValue;

    public string DisponibleTexto => Formatear(SaldoDisponible);

    public string RetenidoTexto => Formatear(SaldoRetenido);

    public string ActualizadoTexto => UltimaActualizacion is { } t
        ? $"Actualizado a las {t.ToLocalTime():HH:mm:ss}"
        : "Aún sin consultar";

    /// <summary>Solo el rol operador retiene fondos; a los demás no se les ofrece el botón.</summary>
    public bool PuedeTransferir => string.Equals(Rol, "operador", StringComparison.OrdinalIgnoreCase);

    public bool HayAvisoRol => Rol.Length > 0 && !string.Equals(Rol, "operador", StringComparison.OrdinalIgnoreCase);

    public string AvisoRol => $"Su rol ({Rol}) no permite transferir; solo el rol operador mueve fondos.";

    /// <summary>Se llama cuando la pantalla aparece: valida la sesión y refresca el saldo.</summary>
    public Task AlAparecerAsync() => EjecutarAsync(async () =>
    {
        var sesion = await Auth.ObtenerSesionVigenteAsync();
        if (sesion is null)
        {
            await Navegacion.IrALoginAsync();
            return;
        }

        Usuario = sesion.Usuario;
        Rol = sesion.Rol;
        await ConsultarCoreAsync();
    });

    [RelayCommand]
    private Task ConsultarSaldoAsync() => EjecutarAsync(ConsultarCoreAsync);

    [RelayCommand]
    private Task IrATransferenciaAsync() => EjecutarAsync(async () =>
    {
        if (!Guid.TryParse(CuentaIdTexto, out var cuentaId))
        {
            MensajeError = "Identificador de cuenta inválido.";
            return;
        }

        _configuracion.UltimaCuentaId = cuentaId;
        await Navegacion.IrATransferenciaAsync();
    });

    [RelayCommand]
    private Task CerrarSesionAsync() => EjecutarAsync(async () =>
    {
        await Auth.CerrarSesionAsync();
        await Navegacion.IrALoginAsync();
    });

    private async Task ConsultarCoreAsync()
    {
        if (!Guid.TryParse(CuentaIdTexto, out var cuentaId))
        {
            MensajeError = "Identificador de cuenta inválido.";
            return;
        }

        var resultado = await _api.ObtenerSaldoAsync(cuentaId);
        if (!resultado.EsExito)
        {
            if (resultado.Error.Tipo == TipoError.NoEncontrado)
            {
                // No dejar a la vista los saldos de otra cuenta.
                SaldoDisponible = null;
                SaldoRetenido = null;
                UltimaActualizacion = null;
            }

            await ManejarErrorAsync(resultado.Error);
            return;
        }

        SaldoDisponible = resultado.Valor.SaldoDisponible;
        SaldoRetenido = resultado.Valor.SaldoRetenido;
        UltimaActualizacion = DateTimeOffset.Now;
        _configuracion.UltimaCuentaId = cuentaId;
    }

    private static string Formatear(decimal? valor) => valor is { } v ? $"{v:N2} UVB" : "—";
}
