using System.Globalization;
using Breb.App.Models;
using Breb.App.Services.Api;
using Breb.App.ViewModels;

namespace Breb.App.Tests;

public class LoginViewModelTests
{
    private readonly FakeAuth _auth = new();
    private readonly FakeNavegacion _nav = new();
    private readonly FakeDialogos _dialogos = new();
    private readonly FakeConfiguracion _config = new();

    private LoginViewModel Crear() => new(_auth, _nav, _dialogos, _config);

    [Fact]
    public async Task CamposVacios_NoLlamanAlServicioYIndicanQueFalta()
    {
        var vm = Crear();

        await vm.IngresarCommand.ExecuteAsync(null);
        Assert.Equal("Ingrese su usuario.", vm.MensajeError);

        vm.Usuario = "ana";
        await vm.IngresarCommand.ExecuteAsync(null);
        Assert.Equal("Ingrese su contraseña.", vm.MensajeError);
        Assert.Equal(0, _auth.Intentos);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task LoginExitoso_VaASaldoYBorraLaClave()
    {
        _auth.Respuesta = Resultado<Sesion>.Ok(Datos.SesionOperador());
        var vm = Crear();
        vm.Usuario = "ana.operadora";
        vm.Clave = "secreta";

        await vm.IngresarCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "saldo" }, _nav.Destinos);
        Assert.Equal(string.Empty, vm.Clave);
        Assert.False(vm.HayError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task LoginFallido_NoCambiaLaBaseUrl_YElExitosoSi()
    {
        var urlAnterior = _config.BaseUrl;
        _auth.Respuesta = Resultado<Sesion>.Fallo(new ErrorApi(TipoError.CredencialesInvalidas, "mal"));
        var vm = Crear();
        vm.Usuario = "ana";
        vm.Clave = "mal";
        vm.ServidorUrl = "http://otro.test:9000/";

        await vm.IngresarCommand.ExecuteAsync(null);

        Assert.Equal(urlAnterior, _config.BaseUrl);
        Assert.Equal("http://otro.test:9000", _auth.UltimaBaseUrl);

        _auth.Respuesta = Resultado<Sesion>.Ok(Datos.SesionOperador());
        await vm.IngresarCommand.ExecuteAsync(null);

        Assert.Equal("http://otro.test:9000", _config.BaseUrl);
    }

    [Fact]
    public async Task Login401_MuestraBannerYNoNavega()
    {
        _auth.Respuesta = Resultado<Sesion>.Fallo(new ErrorApi(TipoError.CredencialesInvalidas, "Usuario o contraseña incorrectos."));
        var vm = Crear();
        vm.Usuario = "ana";
        vm.Clave = "mal";

        await vm.IngresarCommand.ExecuteAsync(null);

        Assert.Equal("Usuario o contraseña incorrectos.", vm.MensajeError);
        Assert.True(vm.HayError);
        Assert.Empty(_nav.Destinos);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ExcepcionInesperada_RestableceIsBusyYMuestraMensaje()
    {
        _auth.Comportamiento = () => throw new InvalidOperationException("boom");
        var vm = Crear();
        vm.Usuario = "ana";
        vm.Clave = "x";

        await vm.IngresarCommand.ExecuteAsync(null);

        Assert.False(vm.IsBusy);
        Assert.Contains("error inesperado", vm.MensajeError);
    }

    [Fact]
    public async Task MientrasEnvia_ElComandoNoSePuedeEjecutar()
    {
        var gate = new TaskCompletionSource<Resultado<Sesion>>();
        _auth.Comportamiento = () => gate.Task;
        var vm = Crear();
        vm.Usuario = "ana";
        vm.Clave = "x";

        var tarea = vm.IngresarCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        Assert.False(vm.IngresarCommand.CanExecute(null));

        gate.SetResult(Resultado<Sesion>.Ok(Datos.SesionOperador()));
        await tarea;
        Assert.False(vm.IsBusy);
        Assert.True(vm.IngresarCommand.CanExecute(null));
    }

    [Fact]
    public async Task UrlInvalida_NoLlamaAlServicio()
    {
        var vm = Crear();
        vm.Usuario = "ana";
        vm.Clave = "x";
        vm.ServidorUrl = "no es una url";

        await vm.IngresarCommand.ExecuteAsync(null);

        Assert.Contains("dirección del servidor", vm.MensajeError);
        Assert.Equal(0, _auth.Intentos);
    }

    [Fact]
    public async Task Inicializar_ConSesionVigenteSaltaASaldo()
    {
        _auth.SesionVigente = Datos.SesionOperador();

        await Crear().InicializarAsync();

        Assert.Equal(new[] { "saldo" }, _nav.Destinos);
    }

    [Fact]
    public async Task Inicializar_SinSesionSeQuedaEnLogin()
    {
        await Crear().InicializarAsync();

        Assert.Empty(_nav.Destinos);
    }
}

public class SaldoViewModelTests
{
    private readonly FakeBrebApi _api = new();
    private readonly FakeAuth _auth = new() { SesionVigente = Datos.SesionOperador() };
    private readonly FakeNavegacion _nav = new();
    private readonly FakeDialogos _dialogos = new();
    private readonly FakeConfiguracion _config = new();

    private SaldoViewModel Crear() => new(_api, _auth, _nav, _dialogos, _config);

    [Fact]
    public async Task AlAparecer_CargaSaldoYRol()
    {
        var vm = Crear();

        await vm.AlAparecerAsync();

        Assert.Equal(100m, vm.SaldoDisponible);
        Assert.Equal("ana.operadora", vm.Usuario);
        Assert.False(vm.HayAvisoRol);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task RolConsulta_MuestraAviso()
    {
        _auth.SesionVigente = Datos.SesionOperador() with { Rol = "consulta" };
        var vm = Crear();

        await vm.AlAparecerAsync();

        Assert.True(vm.HayAvisoRol);
    }

    [Fact]
    public async Task SinSesion_VaALogin()
    {
        _auth.SesionVigente = null;

        await Crear().AlAparecerAsync();

        Assert.Equal(new[] { "login" }, _nav.Destinos);
    }

    [Fact]
    public async Task IdInvalido_NoLlamaALaApi()
    {
        var vm = Crear();
        vm.CuentaIdTexto = "abc";

        await vm.ConsultarSaldoCommand.ExecuteAsync(null);

        Assert.Equal("Identificador de cuenta inválido.", vm.MensajeError);
        Assert.Equal(0, _api.ConsultasSaldo);
    }

    [Fact]
    public async Task Error401_CierraSesionYVaALogin()
    {
        _api.Saldo = _ => Resultado<SaldoCuenta>.Fallo(new ErrorApi(TipoError.SesionExpirada, "Su sesión expiró"));
        var vm = Crear();

        await vm.ConsultarSaldoCommand.ExecuteAsync(null);

        Assert.Equal(1, _auth.Cierres);
        Assert.Equal(new[] { "login" }, _nav.Destinos);
        Assert.Single(_dialogos.Mensajes);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Error403_MuestraBannerSinCerrarSesion()
    {
        _api.Saldo = _ => Resultado<SaldoCuenta>.Fallo(new ErrorApi(TipoError.SinPermiso, "sin permiso"));
        var vm = Crear();

        await vm.ConsultarSaldoCommand.ExecuteAsync(null);

        Assert.Equal("sin permiso", vm.MensajeError);
        Assert.Equal(0, _auth.Cierres);
        Assert.Empty(_nav.Destinos);
    }

    [Fact]
    public async Task Error404_LimpiaLosSaldosPrevios()
    {
        var vm = Crear();
        await vm.ConsultarSaldoCommand.ExecuteAsync(null);
        Assert.True(vm.TieneSaldo);

        _api.Saldo = _ => Resultado<SaldoCuenta>.Fallo(new ErrorApi(TipoError.NoEncontrado, "La cuenta no existe."));
        await vm.ConsultarSaldoCommand.ExecuteAsync(null);

        Assert.False(vm.TieneSaldo);
        Assert.Equal("La cuenta no existe.", vm.MensajeError);
    }

    [Fact]
    public async Task IrATransferencia_GuardaCuentaYNavega()
    {
        var vm = Crear();
        var otra = Guid.NewGuid();
        vm.CuentaIdTexto = otra.ToString();

        await vm.IrATransferenciaCommand.ExecuteAsync(null);

        Assert.Equal(otra, _config.UltimaCuentaId);
        Assert.Equal(new[] { "transferencia" }, _nav.Destinos);
    }

    [Fact]
    public async Task CerrarSesion_BorraYVaALogin()
    {
        var vm = Crear();

        await vm.CerrarSesionCommand.ExecuteAsync(null);

        Assert.Equal(1, _auth.Cierres);
        Assert.Equal(new[] { "login" }, _nav.Destinos);
    }
}

public class TransferenciaViewModelTests
{
    private readonly FakeBrebApi _api = new();
    private readonly FakeAuth _auth = new() { SesionVigente = Datos.SesionOperador() };
    private readonly FakeNavegacion _nav = new();
    private readonly FakeDialogos _dialogos = new();
    private readonly FakeConfiguracion _config = new();

    private TransferenciaViewModel Crear()
    {
        var servicio = new TransferenciaDirectaService(_api);
        return new TransferenciaViewModel(servicio, _api, _auth, _nav, _dialogos, _config)
        {
            IntervaloSondeo = TimeSpan.FromMilliseconds(1),
            DuracionMaximaSondeo = TimeSpan.FromSeconds(2),
            EsperaSinSeguimiento = TimeSpan.FromMilliseconds(5)
        };
    }

    [Theory]
    [InlineData("es-CO", "10,5", 10.5)]
    [InlineData("es-CO", "10.5", 10.5)]
    [InlineData("en-US", "10.5", 10.5)]
    [InlineData("es-CO", "12", 12)]
    public void TryParseMonto_AceptaCulturaActualEInvariante(string cultura, string texto, double esperado)
    {
        Assert.True(TransferenciaViewModel.TryParseMonto(texto, new CultureInfo(cultura), out var monto));
        Assert.Equal((decimal)esperado, monto);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1e3")]
    public void TryParseMonto_RechazaTextoInvalido(string texto)
    {
        Assert.False(TransferenciaViewModel.TryParseMonto(texto, CultureInfo.InvariantCulture, out _));
    }

    [Theory]
    [InlineData("0", "mayor que cero")]
    [InlineData("-5", "mayor que cero")]
    [InlineData("abc", "numérico")]
    [InlineData("1001", "máximo")]
    [InlineData("500", "saldo disponible")]
    public async Task MontoInvalido_NoLlamaALaApi(string monto, string fragmento)
    {
        var vm = Crear();
        await vm.AlAparecerAsync(); // conoce el saldo (100)
        vm.MontoTexto = monto;

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Contains(fragmento, vm.MensajeError);
        Assert.Empty(_api.Solicitudes);
        Assert.Equal(FaseTransferencia.Ninguna, vm.Fase);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task EnProceso_LuegoCompletada_RecargaSaldo()
    {
        _api.Estados.Enqueue(Datos.Estado(EstadoTransferencia.EnProceso));
        _api.Estados.Enqueue(Datos.Estado(EstadoTransferencia.EnProceso));
        _api.Estados.Enqueue(Datos.Estado(EstadoTransferencia.Completada));
        var vm = Crear();
        vm.MontoTexto = "10";

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Equal(FaseTransferencia.Completada, vm.Fase);
        Assert.True(vm.EsCompletada);
        Assert.Equal(3, _api.ConsultasEstado);
        Assert.True(_api.ConsultasSaldo >= 1);
        Assert.Null(vm.ClaveIdempotenciaActual);
        Assert.NotNull(vm.TransferenciaId);
        Assert.False(vm.IsBusy);
        Assert.True(vm.TransferirCommand.CanExecute(null));
    }

    [Fact]
    public async Task EnProceso_LuegoCompensada()
    {
        _api.Estados.Enqueue(Datos.Estado(EstadoTransferencia.EnProceso));
        _api.Estados.Enqueue(Datos.Estado(EstadoTransferencia.Compensando));
        _api.Estados.Enqueue(Datos.Estado(EstadoTransferencia.Compensada));
        var vm = Crear();
        vm.MontoTexto = "10";

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Equal(FaseTransferencia.Compensada, vm.Fase);
        Assert.Contains("reintegrados", vm.MensajeResultado);
    }

    [Fact]
    public async Task SinEstadoFinal_QuedaIndeterminada()
    {
        _api.EstadoRepetido = Datos.Estado(EstadoTransferencia.EnProceso);
        var vm = Crear();
        vm.DuracionMaximaSondeo = TimeSpan.FromMilliseconds(30);
        vm.MontoTexto = "10";

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Equal(FaseTransferencia.Indeterminada, vm.Fase);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Estado404_PasaASinSeguimientoYRecargaSaldo()
    {
        // FakeBrebApi devuelve NoEncontrado cuando no hay estados programados.
        var vm = Crear();
        vm.MontoTexto = "10";
        var saldoAntes = _api.ConsultasSaldo;

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Equal(FaseTransferencia.SinSeguimiento, vm.Fase);
        Assert.True(_api.ConsultasSaldo > saldoAntes);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Timeout_ConservaLaClaveDeIdempotenciaYElReintentoLaReusa()
    {
        var intentos = 0;
        _api.Retener = s => ++intentos == 1
            ? Resultado<RespuestaRetencion>.Fallo(Datos.Error(TipoError.Timeout, "El servidor no respondió a tiempo."))
            : Resultado<RespuestaRetencion>.Ok(new RespuestaRetencion(Guid.NewGuid(), s.MontoUVB));
        _api.EstadoRepetido = Datos.Estado(EstadoTransferencia.Completada);
        var vm = Crear();
        vm.MontoTexto = "10";

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ClaveIdempotenciaActual);
        Assert.Contains("revise su saldo", vm.MensajeError);
        Assert.Equal(FaseTransferencia.Ninguna, vm.Fase);

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Equal(2, _api.Solicitudes.Count);
        Assert.Equal(_api.Solicitudes[0].ClaveIdempotencia, _api.Solicitudes[1].ClaveIdempotencia);
        Assert.Equal(FaseTransferencia.Completada, vm.Fase);
    }

    [Fact]
    public async Task ErrorDefinitivo409_DescartaLaClave()
    {
        _api.Retener = _ => Resultado<RespuestaRetencion>.Fallo(Datos.Error(TipoError.SaldoInsuficiente, "Saldo insuficiente."));
        var vm = Crear();
        vm.MontoTexto = "10";

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Null(vm.ClaveIdempotenciaActual);
        Assert.Equal("Saldo insuficiente.", vm.MensajeError);
        Assert.Equal(FaseTransferencia.Ninguna, vm.Fase);
    }

    [Fact]
    public async Task Error403_MuestraMensajeSinSalirDeSesion()
    {
        _api.Retener = _ => Resultado<RespuestaRetencion>.Fallo(Datos.Error(TipoError.SinPermiso, "sin permiso"));
        var vm = Crear();
        vm.MontoTexto = "10";

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Equal("sin permiso", vm.MensajeError);
        Assert.Equal(0, _auth.Cierres);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Error401_AlEnviar_VaALogin()
    {
        _api.Retener = _ => Resultado<RespuestaRetencion>.Fallo(Datos.Error(TipoError.SesionExpirada, "Su sesión expiró"));
        var vm = Crear();
        vm.MontoTexto = "10";

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "login" }, _nav.Destinos);
        Assert.Equal(1, _auth.Cierres);
    }

    [Fact]
    public async Task EnviaElMontoParseadoConLaCulturaDelDispositivo()
    {
        var anterior = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("es-CO");
        try
        {
            _api.EstadoRepetido = Datos.Estado(EstadoTransferencia.Completada);
            var vm = Crear();
            vm.MontoTexto = "12,5";

            await vm.TransferirCommand.ExecuteAsync(null);

            Assert.Equal(12.5m, _api.Solicitudes.Single().MontoUVB);
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Fact]
    public async Task DetenerSondeo_CancelaYNoDejaLaPantallaTrabajando()
    {
        _api.EstadoRepetido = Datos.Estado(EstadoTransferencia.EnProceso);
        var vm = Crear();
        vm.IntervaloSondeo = TimeSpan.FromMilliseconds(20);
        vm.MontoTexto = "10";

        var tarea = vm.TransferirCommand.ExecuteAsync(null);
        await Task.Delay(60);
        Assert.Equal(FaseTransferencia.EnProceso, vm.Fase);

        vm.DetenerSondeo();
        await tarea;

        Assert.Equal(FaseTransferencia.Indeterminada, vm.Fase);
        Assert.False(vm.IsBusy);
        Assert.False(vm.EstaTrabajando);
    }

    [Fact]
    public async Task NuevaTransferencia_ReiniciaElFormulario()
    {
        _api.EstadoRepetido = Datos.Estado(EstadoTransferencia.Completada);
        var vm = Crear();
        vm.MontoTexto = "10";
        await vm.TransferirCommand.ExecuteAsync(null);

        vm.NuevaTransferenciaCommand.Execute(null);

        Assert.Equal(FaseTransferencia.Ninguna, vm.Fase);
        Assert.Equal(string.Empty, vm.MontoTexto);
        Assert.Null(vm.TransferenciaId);
        Assert.True(vm.MostrarFormulario);
    }

    [Fact]
    public async Task Volver_CancelaUnSondeoActivoYNavega()
    {
        _api.EstadoRepetido = Datos.Estado(EstadoTransferencia.EnProceso);
        var vm = Crear();
        vm.IntervaloSondeo = TimeSpan.FromMilliseconds(20);
        vm.MontoTexto = "10";

        var tarea = vm.TransferirCommand.ExecuteAsync(null);
        await Task.Delay(60);
        Assert.Equal(FaseTransferencia.EnProceso, vm.Fase);

        await vm.VolverCommand.ExecuteAsync(null);
        await tarea;

        Assert.Equal(new[] { "volver" }, _nav.Destinos);
        Assert.Equal(FaseTransferencia.Indeterminada, vm.Fase);
        Assert.False(vm.IsBusy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SalirConElEnvioEnVuelo_NoArrancaElSondeo(bool conVolver)
    {
        var liberar = new TaskCompletionSource();
        _api.RetenerAsincrono = async (s, _) =>
        {
            await liberar.Task;
            return Resultado<RespuestaRetencion>.Ok(new RespuestaRetencion(Guid.NewGuid(), s.MontoUVB));
        };
        _api.EstadoRepetido = Datos.Estado(EstadoTransferencia.EnProceso);
        var vm = Crear();
        vm.MontoTexto = "10";

        var tarea = vm.TransferirCommand.ExecuteAsync(null);
        Assert.Equal(FaseTransferencia.Enviando, vm.Fase);

        if (conVolver)
        {
            await vm.VolverCommand.ExecuteAsync(null);
        }
        else
        {
            vm.DetenerSondeo();
        }

        liberar.SetResult();
        await tarea;

        Assert.Equal(0, _api.ConsultasEstado);
        Assert.Equal(FaseTransferencia.Indeterminada, vm.Fase);
        Assert.False(vm.IsBusy);
        Assert.False(vm.EstaTrabajando);
        Assert.DoesNotContain("login", _nav.Destinos);
    }

    [Fact]
    public async Task ClaveIdempotencia_SeReusaConElMismoMontoYCambiaConOtro()
    {
        var intentos = 0;
        _api.Retener = s => ++intentos <= 2
            ? Resultado<RespuestaRetencion>.Fallo(Datos.Error(TipoError.Timeout, "El servidor no respondió a tiempo."))
            : Resultado<RespuestaRetencion>.Ok(new RespuestaRetencion(Guid.NewGuid(), s.MontoUVB));
        _api.EstadoRepetido = Datos.Estado(EstadoTransferencia.Completada);
        var vm = Crear();
        vm.MontoTexto = "10";
        await vm.TransferirCommand.ExecuteAsync(null);

        await vm.TransferirCommand.ExecuteAsync(null);
        Assert.Equal(_api.Solicitudes[0].ClaveIdempotencia, _api.Solicitudes[1].ClaveIdempotencia);

        vm.MontoTexto = "20";
        await vm.TransferirCommand.ExecuteAsync(null);
        Assert.NotEqual(_api.Solicitudes[1].ClaveIdempotencia, _api.Solicitudes[2].ClaveIdempotencia);
        Assert.Equal(20m, _api.Solicitudes[2].MontoUVB);
    }
}

public class TraductorErroresTests
{
    [Fact]
    public async Task Status400_MuestraElDetailDelProblemDetails()
    {
        // Forma real de Results.Problem("..."): el texto va en detail y title queda genérico.
        using var respuesta = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
        {
            Content = new System.Net.Http.StringContent(
                """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Bad Request","status":400,"detail":"El monto debe ser positivo."}""",
                System.Text.Encoding.UTF8, "application/json")
        };

        var e = await TraductorErrores.DesdeRespuestaAsync(respuesta, new ContextoError());

        Assert.Equal("Revise los datos ingresados. El monto debe ser positivo.", e.Mensaje);
        Assert.DoesNotContain("Bad Request", e.Mensaje);
    }

    [Fact]
    public async Task Status400_SinDetailUsaElTitle()
    {
        using var respuesta = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
        {
            Content = new System.Net.Http.StringContent(
                """{"title":"Uno o más errores de validación."}""", System.Text.Encoding.UTF8, "application/json")
        };

        var e = await TraductorErrores.DesdeRespuestaAsync(respuesta, new ContextoError());

        Assert.Equal("Revise los datos ingresados. Uno o más errores de validación.", e.Mensaje);
    }

    [Fact]
    public void Status401EnLogin_EsCredencialesInvalidas()
    {
        var e = TraductorErrores.DesdeStatus(System.Net.HttpStatusCode.Unauthorized, new ContextoError(EsLogin: true), null, null);

        Assert.Equal(TipoError.CredencialesInvalidas, e.Tipo);
    }
}
