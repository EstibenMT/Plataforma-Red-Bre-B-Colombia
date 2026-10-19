using System.Diagnostics;
using System.Globalization;
using Breb.App.Models;
using Breb.App.Services.Abstracciones;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Breb.App.ViewModels;

public partial class TransferenciaViewModel : ViewModelBase
{
    /// <summary>Tope de negocio por transferencia (la API no lo exige; se valida en el cliente).</summary>
    public const decimal MontoMaximoUVB = 1000m;

    private readonly ITransferenciaService _transferencias;
    private readonly IBrebApi _api;
    private readonly IConfiguracionApi _configuracion;
    private CancellationTokenSource? _sondeo;
    private (Guid CuentaId, decimal Monto)? _solicitudClave;

    public TransferenciaViewModel(
        ITransferenciaService transferencias,
        IBrebApi api,
        IAuthService auth,
        INavegacionService navegacion,
        IDialogoService dialogos,
        IConfiguracionApi configuracion)
        : base(auth, navegacion, dialogos)
    {
        _transferencias = transferencias;
        _api = api;
        _configuracion = configuracion;
        _cuentaIdTexto = configuracion.UltimaCuentaId.ToString();
    }

    /// <summary>Cada cuánto se consulta el estado. Ajustable para pruebas.</summary>
    public TimeSpan IntervaloSondeo { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Tiempo máximo de seguimiento antes de declarar el resultado indeterminado.</summary>
    public TimeSpan DuracionMaximaSondeo { get; set; } = TimeSpan.FromSeconds(45);

    /// <summary>Espera antes de recargar el saldo cuando la API no ofrece seguimiento.</summary>
    public TimeSpan EsperaSinSeguimiento { get; set; } = TimeSpan.FromSeconds(17);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CuentaCorta))]
    private string _cuentaIdTexto;

    /// <summary>Forma abreviada para mostrar; el identificador completo solo se usa internamente.</summary>
    public string CuentaCorta => CuentaIdTexto.Length >= 13
        ? $"{CuentaIdTexto[..8]}…{CuentaIdTexto[^4..]}"
        : CuentaIdTexto;

    [ObservableProperty]
    private string _montoTexto = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarFormulario), nameof(MostrarResultado), nameof(EstaTrabajando),
        nameof(EsEnProceso), nameof(EsCompletada), nameof(EsCompensada), nameof(EsAdvertencia), nameof(TituloResultado))]
    [NotifyCanExecuteChangedFor(nameof(TransferirCommand), nameof(NuevaTransferenciaCommand))]
    private FaseTransferencia _fase = FaseTransferencia.Ninguna;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TransferenciaIdTexto))]
    private Guid? _transferenciaId;

    [ObservableProperty]
    private string _mensajeResultado = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsperaTexto))]
    private int _segundosTranscurridos;

    [ObservableProperty]
    private Guid? _claveIdempotenciaActual;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisponibleTexto))]
    private decimal? _saldoDisponible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RetenidoTexto))]
    private decimal? _saldoRetenido;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MontoEnviadoTexto))]
    private decimal _montoEnviado;

    public bool MostrarFormulario => Fase is FaseTransferencia.Ninguna or FaseTransferencia.Enviando;

    public bool MostrarResultado => !MostrarFormulario;

    public bool EstaTrabajando => Fase is FaseTransferencia.Enviando or FaseTransferencia.EnProceso;

    public bool EsEnProceso => Fase == FaseTransferencia.EnProceso;

    public bool EsCompletada => Fase == FaseTransferencia.Completada;

    public bool EsCompensada => Fase == FaseTransferencia.Compensada;

    public bool EsAdvertencia => Fase is FaseTransferencia.Indeterminada or FaseTransferencia.SinSeguimiento;

    public string TransferenciaIdTexto => TransferenciaId?.ToString() ?? string.Empty;

    public string TituloResultado => Fase switch
    {
        FaseTransferencia.Completada => "Transferencia completada",
        FaseTransferencia.Compensada => "Transferencia no completada",
        FaseTransferencia.Indeterminada or FaseTransferencia.SinSeguimiento => "Resultado sin confirmar",
        _ => "Transferencia en proceso"
    };

    public string EsperaTexto => $"Esperando al banco destino… {SegundosTranscurridos} s";

    public string DisponibleTexto => SaldoDisponible is { } v ? $"{v:N2} UVB" : "—";

    public string RetenidoTexto => SaldoRetenido is { } v ? $"{v:N2} UVB" : "—";

    public string MontoEnviadoTexto => $"{MontoEnviado:N2} UVB";

    /// <summary>
    /// Interpreta el monto con la cultura indicada y, si falla, con la invariante.
    /// Sin separador de miles: "10.5" en es-CO se lee como 10,5 y no como 105.
    /// </summary>
    public static bool TryParseMonto(string? texto, CultureInfo cultura, out decimal monto)
    {
        monto = 0;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        const NumberStyles estilo = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;
        var limpio = texto.Trim();
        return decimal.TryParse(limpio, estilo, cultura, out monto)
               || decimal.TryParse(limpio, estilo, CultureInfo.InvariantCulture, out monto);
    }

    /// <summary>Se llama al aparecer la pantalla: lee la cuenta elegida y el saldo conocido.</summary>
    public Task AlAparecerAsync() => EjecutarAsync(async () =>
    {
        if (await Auth.ObtenerSesionVigenteAsync() is null)
        {
            await Navegacion.IrALoginAsync();
            return;
        }

        CuentaIdTexto = _configuracion.UltimaCuentaId.ToString();
        if (!EstaTrabajando)
        {
            await RecargarSaldoAsync();
        }
    });

    [RelayCommand(CanExecute = nameof(PuedeTransferir))]
    private Task TransferirAsync() => EjecutarAsync(TransferirCoreAsync);

    private bool PuedeTransferir() => !EstaTrabajando;

    [RelayCommand(CanExecute = nameof(PuedeReiniciar))]
    private void NuevaTransferencia()
    {
        Fase = FaseTransferencia.Ninguna;
        TransferenciaId = null;
        ClaveIdempotenciaActual = null;
        MensajeResultado = string.Empty;
        MensajeError = null;
        MontoTexto = string.Empty;
        SegundosTranscurridos = 0;
    }

    private bool PuedeReiniciar() => !EstaTrabajando;

    [RelayCommand]
    private async Task VolverAsync()
    {
        DetenerSondeo();
        try
        {
            await Navegacion.VolverAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"No se pudo volver: {ex.GetType().Name}");
        }
    }

    /// <summary>Cancela el seguimiento (al salir de la pantalla o cerrar sesión).</summary>
    public void DetenerSondeo()
    {
        try
        {
            _sondeo?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // El sondeo ya terminó.
        }
    }

    private async Task TransferirCoreAsync()
    {
        if (!Guid.TryParse(CuentaIdTexto, out var cuentaId))
        {
            MensajeError = "No se pudo identificar la cuenta de origen. Vuelva a Saldo e intente de nuevo.";
            return;
        }

        if (!TryParseMonto(MontoTexto, CultureInfo.CurrentCulture, out var monto))
        {
            MensajeError = "Ingrese un monto numérico válido, por ejemplo 10 o 12,5.";
            return;
        }

        if (monto <= 0)
        {
            MensajeError = "El monto debe ser mayor que cero.";
            return;
        }

        if (monto > MontoMaximoUVB)
        {
            MensajeError = $"El monto máximo por transferencia es {MontoMaximoUVB:N0} UVB.";
            return;
        }

        if (SaldoDisponible is { } disponible && monto > disponible)
        {
            MensajeError = $"El monto supera su saldo disponible, que es de {disponible:N2} UVB.";
            return;
        }

        // La clave nace con la intención y se conserva si el resultado del envío es ambiguo,
        // pero solo para la misma cuenta y monto: otra intención necesita otra clave.
        if (ClaveIdempotenciaActual is null || _solicitudClave != (cuentaId, monto))
        {
            ClaveIdempotenciaActual = Guid.NewGuid();
            _solicitudClave = (cuentaId, monto);
        }

        // El token existe desde antes del POST: si el usuario sale mientras está en vuelo, no se sondea.
        var cts = new CancellationTokenSource();
        _sondeo = cts;
        var ct = cts.Token;
        Fase = FaseTransferencia.Enviando;
        try
        {
            var envio = await _transferencias.EnviarAsync(
                new SolicitudTransferencia(cuentaId, monto, ClaveIdempotenciaActual.Value), ct);

            if (!envio.EsExito)
            {
                ct.ThrowIfCancellationRequested();
                await ManejarFalloEnvioAsync(envio.Error);
                return;
            }

            MontoEnviado = envio.Valor.MontoUVB;
            TransferenciaId = envio.Valor.TransferenciaId;
            ClaveIdempotenciaActual = null;
            SegundosTranscurridos = 0;
            MensajeResultado = "Retuvimos el monto en su cuenta mientras el banco destino confirma. Esto toma 15 segundos como máximo.";
            Fase = FaseTransferencia.EnProceso;

            if (!ct.IsCancellationRequested)
            {
                await SondearAsync(envio.Valor.TransferenciaId, ct);
            }
        }
        finally
        {
            if (ReferenceEquals(_sondeo, cts))
            {
                _sondeo = null;
            }

            cts.Dispose();

            // Pase lo que pase, la pantalla no queda en un estado de "trabajando" sin tarea.
            if (Fase == FaseTransferencia.Enviando)
            {
                Fase = FaseTransferencia.Ninguna;
            }
            else if (Fase == FaseTransferencia.EnProceso)
            {
                Indeterminar();
            }
        }
    }

    private async Task ManejarFalloEnvioAsync(ErrorApi error)
    {
        Fase = FaseTransferencia.Ninguna;

        // Timeout o servidor caído: la retención pudo aplicarse. Se conserva la clave para reintentar.
        var ambiguo = error.Tipo is TipoError.Timeout or TipoError.ServidorNoDisponible or TipoError.RespuestaInvalida;
        if (ambiguo)
        {
            await ManejarErrorAsync(error with
            {
                Mensaje = error.Mensaje
                          + " Es posible que la transferencia sí se haya registrado; revise su saldo antes de intentar de nuevo."
            });
            return;
        }

        ClaveIdempotenciaActual = null;
        await ManejarErrorAsync(error);
    }

    private async Task SondearAsync(Guid transferenciaId, CancellationToken ct)
    {
        var reloj = Stopwatch.StartNew();
        while (true)
        {
            var consulta = await _transferencias.ConsultarEstadoAsync(transferenciaId, ct);
            SegundosTranscurridos = (int)reloj.Elapsed.TotalSeconds;

            if (consulta.EsExito)
            {
                MensajeError = null;
                switch (consulta.Valor.Estado)
                {
                    case EstadoTransferencia.Completada:
                        await FinalizarAsync(
                            FaseTransferencia.Completada,
                            "El banco destino confirmó el abono y su saldo disponible ya se actualizó.");
                        return;
                    case EstadoTransferencia.Compensada:
                        await FinalizarAsync(
                            FaseTransferencia.Compensada,
                            "El banco destino no confirmó a tiempo. Los fondos fueron reintegrados a su cuenta y no se descontó ningún monto.");
                        return;
                    case EstadoTransferencia.Compensando:
                        MensajeResultado = "El banco destino no confirmó a tiempo. Estamos reintegrando los fondos a su cuenta…";
                        break;
                }
            }
            else if (consulta.Error.Tipo == TipoError.NoEncontrado)
            {
                // El servidor aún no ofrece GET /transferencias/{id}.
                await SeguirSinSeguimientoAsync(ct);
                return;
            }
            else if (consulta.Error.Tipo == TipoError.SesionExpirada)
            {
                await ManejarErrorAsync(consulta.Error);
                return;
            }
            else
            {
                // Fallo transitorio de red: se avisa y se sigue intentando hasta agotar el plazo.
                MensajeError = consulta.Error.Mensaje;
            }

            if (reloj.Elapsed >= DuracionMaximaSondeo)
            {
                Indeterminar();
                await RecargarSaldoAsync();
                return;
            }

            await Task.Delay(IntervaloSondeo, ct);
        }
    }

    private async Task FinalizarAsync(FaseTransferencia fase, string mensaje)
    {
        MensajeResultado = mensaje;
        Fase = fase;
        await RecargarSaldoAsync();
    }

    private void Indeterminar()
    {
        MensajeResultado = "No pudimos confirmar el resultado de la transferencia. Revise su saldo antes de intentar de nuevo.";
        Fase = FaseTransferencia.Indeterminada;
    }

    private async Task SeguirSinSeguimientoAsync(CancellationToken ct)
    {
        Fase = FaseTransferencia.SinSeguimiento;
        MensajeResultado = "El monto quedó retenido. Estamos verificando el resultado; su saldo se actualizará en unos segundos.";
        await Task.Delay(EsperaSinSeguimiento, ct);
        await RecargarSaldoAsync();
        MensajeResultado = "Revise su saldo: si el banco confirmó, el monto retenido se descuenta; si no, vuelve a su saldo disponible.";
    }

    private async Task RecargarSaldoAsync()
    {
        if (!Guid.TryParse(CuentaIdTexto, out var cuentaId))
        {
            return;
        }

        var saldo = await _api.ObtenerSaldoAsync(cuentaId);
        if (saldo.EsExito)
        {
            SaldoDisponible = saldo.Valor.SaldoDisponible;
            SaldoRetenido = saldo.Valor.SaldoRetenido;
        }
        else
        {
            await ManejarErrorAsync(saldo.Error);
        }
    }
}
