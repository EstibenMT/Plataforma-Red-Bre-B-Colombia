using System.Globalization;
using Breb.App.Models;
using Breb.App.Services.Abstracciones;

namespace Breb.App.Services.Api;

/// <summary>Cliente de la API Bre-B. El Bearer lo añade <see cref="AutenticacionHandler"/>.</summary>
public sealed class ClienteBrebApi : IBrebApi
{
    private readonly HttpClient _http;
    private readonly IConfiguracionApi _configuracion;
    private readonly IEstadoRed _red;

    public ClienteBrebApi(HttpClient http, IConfiguracionApi configuracion, IEstadoRed red)
    {
        _http = http;
        _configuracion = configuracion;
        _red = red;
    }

    public Task<Resultado<SaldoCuenta>> ObtenerSaldoAsync(Guid cuentaId, CancellationToken ct = default) =>
        EjecutarAsync<SaldoCuenta>(
            HttpMethod.Get, $"/cuentas/{cuentaId}/saldo", null,
            new ContextoError(), ct);

    public Task<Resultado<RespuestaRetencion>> RetenerAsync(SolicitudTransferencia solicitud, CancellationToken ct = default)
    {
        // InvariantCulture: con es-CO, 10.5 saldría "10,5" y el servidor respondería 400.
        var monto = solicitud.MontoUVB.ToString(CultureInfo.InvariantCulture);
        return EjecutarAsync<RespuestaRetencion>(
            HttpMethod.Post, $"/cuentas/{solicitud.CuentaId}/retener?montoUVB={monto}", solicitud.ClaveIdempotencia,
            new ContextoError(MensajeSinPermiso: "No tiene permisos para realizar transferencias. Si necesita hacerlo, comuníquese con el administrador."), ct);
    }

    public Task<Resultado<ConsultaTransferencia>> ConsultarEstadoAsync(Guid transferenciaId, CancellationToken ct = default) =>
        EjecutarAsync<ConsultaTransferencia>(
            HttpMethod.Get, $"/transferencias/{transferenciaId}", null,
            new ContextoError(MensajeNoEncontrado: "No se encontró la transferencia."), ct);

    private async Task<Resultado<T>> EjecutarAsync<T>(
        HttpMethod metodo, string rutaYConsulta, Guid? claveIdempotencia, ContextoError contexto, CancellationToken ct)
        where T : class
    {
        var baseUrl = _configuracion.BaseUrl;
        if (!EjecutorHttp.TryCrearUri(baseUrl, rutaYConsulta, out var uri))
        {
            return Resultado<T>.Fallo(TraductorErrores.UrlInvalida(baseUrl));
        }

        using var peticion = new HttpRequestMessage(metodo, uri);
        if (claveIdempotencia is { } clave)
        {
            peticion.Options.Set(OpcionesHttp.ClaveIdempotencia, clave);
        }

        return await EjecutorHttp.EnviarAsync<T>(
            _http, _red, baseUrl, peticion,
            r => TraductorErrores.DesdeRespuestaAsync(r, contexto, ct),
            ct).ConfigureAwait(false);
    }
}
