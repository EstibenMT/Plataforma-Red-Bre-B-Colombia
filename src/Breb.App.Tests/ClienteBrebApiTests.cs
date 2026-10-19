using System.Globalization;
using System.Net;
using Breb.App.Models;
using Breb.App.Services.Api;

namespace Breb.App.Tests;

public class ClienteBrebApiTests
{
    private static readonly Guid Cuenta = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeEstadoRed _red = new();
    private readonly FakeSesionStore _store = new() { Sesion = Datos.SesionOperador() };

    private ClienteBrebApi Crear(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), new FakeConfiguracion(), _red, _store);

    [Fact]
    public async Task Saldo_DeserializaCamelCase()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK,
            $$"""{"cuentaId":"{{Cuenta}}","saldoDisponible":1000.5,"saldoRetenido":25}""");

        var r = await Crear(handler).ObtenerSaldoAsync(Cuenta);

        Assert.True(r.EsExito);
        Assert.Equal(1000.5m, r.Valor.SaldoDisponible);
        Assert.Equal(25m, r.Valor.SaldoRetenido);
        Assert.Equal($"/cuentas/{Cuenta}/saldo", handler.Peticiones.Single().RequestUri!.AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, TipoError.SesionExpirada, "sesión venció")]
    [InlineData(HttpStatusCode.Forbidden, TipoError.SinPermiso, "rol operador")]
    [InlineData(HttpStatusCode.NotFound, TipoError.NoEncontrado, "No encontramos la cuenta")]
    [InlineData(HttpStatusCode.Conflict, TipoError.SaldoInsuficiente, "Saldo insuficiente")]
    [InlineData(HttpStatusCode.InternalServerError, TipoError.ErrorServidor, "Referencia para soporte: abc123")]
    [InlineData(HttpStatusCode.BadRequest, TipoError.SolicitudInvalida, "Revise los datos")]
    public async Task Saldo_TraduceCodigosHttp(HttpStatusCode status, TipoError esperado, string fragmento)
    {
        var handler = new FakeHttpMessageHandler(status, traceId: "abc123");

        var r = await Crear(handler).ObtenerSaldoAsync(Cuenta);

        Assert.False(r.EsExito);
        Assert.Equal(esperado, r.Error.Tipo);
        Assert.Contains(fragmento, r.Error.Mensaje);
    }

    [Fact]
    public async Task Retener_500_EsErrorDelServidorConReferencia()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, traceId: "t1");

        var r = await Crear(handler).RetenerAsync(new SolicitudTransferencia(Cuenta, 5m, Guid.NewGuid()));

        Assert.Equal(TipoError.ErrorServidor, r.Error!.Tipo);
        Assert.Equal("Bre-B tuvo un problema de nuestro lado. Intente de nuevo en unos minutos. Referencia para soporte: t1.", r.Error.Mensaje);
        Assert.DoesNotContain("saldo", r.Error.Mensaje);
        Assert.Equal("t1", r.Error.TraceId);
    }

    [Fact]
    public async Task HttpRequestException_EsServidorNoDisponible()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("rechazada"));

        var r = await Crear(handler).ObtenerSaldoAsync(Cuenta);

        Assert.Equal(TipoError.ServidorNoDisponible, r.Error!.Tipo);
        Assert.Contains("No pudimos conectarnos", r.Error.Mensaje);
        Assert.DoesNotContain("servidor.test", r.Error.Mensaje);
    }

    [Fact]
    public async Task TimeoutDelHttpClient_EsTimeout()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new TaskCanceledException("timeout"));

        var r = await Crear(handler).ObtenerSaldoAsync(Cuenta);

        Assert.Equal(TipoError.Timeout, r.Error!.Tipo);
    }

    [Fact]
    public async Task CancelacionDelUsuario_Propaga()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new FakeHttpMessageHandler(_ => throw new TaskCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Crear(handler).ObtenerSaldoAsync(Cuenta, cts.Token));
    }

    [Fact]
    public async Task JsonMalformado_EsRespuestaInvalida()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{esto no es json");

        var r = await Crear(handler).ObtenerSaldoAsync(Cuenta);

        Assert.Equal(TipoError.RespuestaInvalida, r.Error!.Tipo);
    }

    [Fact]
    public async Task CuerpoNulo_EsRespuestaInvalida()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "null");

        var r = await Crear(handler).ObtenerSaldoAsync(Cuenta);

        Assert.Equal(TipoError.RespuestaInvalida, r.Error!.Tipo);
    }

    [Fact]
    public async Task SinRed_NoLlamaAlHandler()
    {
        _red.HayConexion = false;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");

        var r = await Crear(handler).ObtenerSaldoAsync(Cuenta);

        Assert.Equal(TipoError.SinConexion, r.Error!.Tipo);
        Assert.Empty(handler.Peticiones);
    }

    [Fact]
    public async Task Retener_EnviaMontoConCulturaInvariante()
    {
        var anterior = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("es-CO");
        try
        {
            var id = Guid.NewGuid();
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, $$"""{"transferenciaId":"{{id}}","montoUVB":10.5}""");

            var r = await Crear(handler).RetenerAsync(new SolicitudTransferencia(Cuenta, 10.5m, Guid.NewGuid()));

            Assert.True(r.EsExito);
            Assert.Equal(id, r.Valor.TransferenciaId);
            var peticion = handler.Peticiones.Single();
            Assert.Equal(HttpMethod.Post, peticion.Method);
            Assert.Equal("?montoUVB=10.5", peticion.RequestUri!.Query);
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Fact]
    public async Task ConsultarEstado_DeserializaEstadoTexto()
    {
        var id = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK,
            $$"""{"transferenciaId":"{{id}}","cuentaId":"{{Cuenta}}","montoUVB":10,"estado":"Compensada","motivo":"Plazo vencido","creadaEn":"2026-10-18T10:00:00Z","resueltaEn":"2026-10-18T10:00:15Z"}""");

        var r = await Crear(handler).ConsultarEstadoAsync(id);

        Assert.True(r.EsExito);
        Assert.Equal(EstadoTransferencia.Compensada, r.Valor.Estado);
        Assert.Equal("Plazo vencido", r.Valor.Motivo);
    }

    [Fact]
    public async Task ConsultarEstado_404_EsNoEncontradoDeTransferencia()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound);

        var r = await Crear(handler).ConsultarEstadoAsync(Guid.NewGuid());

        Assert.Equal(TipoError.NoEncontrado, r.Error!.Tipo);
        Assert.Contains("transferencia", r.Error.Mensaje);
    }

    [Fact]
    public async Task Idempotency_Key_ViajaCuandoSePide()
    {
        var clave = Guid.NewGuid();
        var interno = new FakeHttpMessageHandler(HttpStatusCode.OK, $$"""{"transferenciaId":"{{Guid.NewGuid()}}","montoUVB":1}""");
        var cadena = new IdempotenciaHandler { InnerHandler = interno };
        var cliente = new ClienteBrebApi(new HttpClient(cadena), new FakeConfiguracion(), _red, _store);

        await cliente.RetenerAsync(new SolicitudTransferencia(Cuenta, 1m, clave));
        await cliente.ObtenerSaldoAsync(Cuenta);

        Assert.Equal(clave.ToString(), interno.Peticiones[0].Headers.GetValues("Idempotency-Key").Single());
        Assert.False(interno.Peticiones[1].Headers.Contains("Idempotency-Key"));
    }
}
