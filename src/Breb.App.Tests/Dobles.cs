using System.Net;
using Breb.App.Models;
using Breb.App.Services.Abstracciones;

namespace Breb.App.Tests;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _respuesta;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respuesta) => _respuesta = respuesta;

    public FakeHttpMessageHandler(HttpStatusCode status, string? json = null, string? traceId = null)
        : this(_ =>
        {
            var r = new HttpResponseMessage(status);
            if (json is not null)
            {
                r.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            }

            if (traceId is not null)
            {
                r.Headers.Add("X-Trace-Id", traceId);
            }

            return Task.FromResult(r);
        })
    {
    }

    public List<HttpRequestMessage> Peticiones { get; } = new();

    public List<string> Cuerpos { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Peticiones.Add(request);
        Cuerpos.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        return await _respuesta(request);
    }
}

public sealed class FakeSesionStore : ISesionStore
{
    public Sesion? Sesion { get; set; }
    public int Guardados { get; private set; }
    public int Borrados { get; private set; }

    public Task<Sesion?> LeerAsync() => Task.FromResult(Sesion);

    public Task GuardarAsync(Sesion sesion)
    {
        Sesion = sesion;
        Guardados++;
        return Task.CompletedTask;
    }

    public Task BorrarAsync()
    {
        Sesion = null;
        Borrados++;
        return Task.CompletedTask;
    }
}

public sealed class FakeEstadoRed : IEstadoRed
{
    public bool HayConexion { get; set; } = true;
}

public sealed class FakeConfiguracion : IConfiguracionApi
{
    public string BaseUrl { get; set; } = "http://servidor.test:5080";
    public Guid UltimaCuentaId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
}

public sealed class FakeNavegacion : INavegacionService
{
    public List<string> Destinos { get; } = new();
    public Task IrALoginAsync() { Destinos.Add("login"); return Task.CompletedTask; }
    public Task IrASaldoAsync() { Destinos.Add("saldo"); return Task.CompletedTask; }
    public Task IrATransferenciaAsync() { Destinos.Add("transferencia"); return Task.CompletedTask; }
    public Task VolverAsync() { Destinos.Add("volver"); return Task.CompletedTask; }
}

public sealed class FakeDialogos : IDialogoService
{
    public List<string> Mensajes { get; } = new();
    public Task MostrarAsync(string titulo, string mensaje) { Mensajes.Add(mensaje); return Task.CompletedTask; }
}

public sealed class FakeAuth : IAuthService
{
    public Resultado<Sesion>? Respuesta { get; set; }
    public Sesion? SesionVigente { get; set; }
    public int Intentos { get; private set; }
    public int Cierres { get; private set; }
    public Func<Task<Resultado<Sesion>>>? Comportamiento { get; set; }
    public string? UltimaBaseUrl { get; private set; }

    public Task<Resultado<Sesion>> IniciarSesionAsync(string usuario, string clave, CancellationToken ct = default, string? baseUrl = null)
    {
        Intentos++;
        UltimaBaseUrl = baseUrl;
        return Comportamiento is not null ? Comportamiento() : Task.FromResult(Respuesta!);
    }

    public Task<Sesion?> ObtenerSesionVigenteAsync() => Task.FromResult(SesionVigente);

    public Task CerrarSesionAsync()
    {
        Cierres++;
        SesionVigente = null;
        return Task.CompletedTask;
    }
}

public sealed class FakeBrebApi : IBrebApi
{
    public Func<Guid, Resultado<SaldoCuenta>> Saldo { get; set; } =
        id => Resultado<SaldoCuenta>.Ok(new SaldoCuenta(id, 100m, 0m));

    public Func<SolicitudTransferencia, Resultado<RespuestaRetencion>> Retener { get; set; } =
        s => Resultado<RespuestaRetencion>.Ok(new RespuestaRetencion(Guid.NewGuid(), s.MontoUVB));

    /// <summary>Si se define, sustituye a <see cref="Retener"/> y permite dejar el POST en vuelo.</summary>
    public Func<SolicitudTransferencia, CancellationToken, Task<Resultado<RespuestaRetencion>>>? RetenerAsincrono { get; set; }

    public Queue<Resultado<ConsultaTransferencia>> Estados { get; } = new();
    public Resultado<ConsultaTransferencia>? EstadoRepetido { get; set; }

    public int ConsultasSaldo { get; private set; }
    public List<SolicitudTransferencia> Solicitudes { get; } = new();
    public int ConsultasEstado { get; private set; }

    public Task<Resultado<SaldoCuenta>> ObtenerSaldoAsync(Guid cuentaId, CancellationToken ct = default)
    {
        ConsultasSaldo++;
        return Task.FromResult(Saldo(cuentaId));
    }

    public Task<Resultado<RespuestaRetencion>> RetenerAsync(SolicitudTransferencia solicitud, CancellationToken ct = default)
    {
        Solicitudes.Add(solicitud);
        return RetenerAsincrono is not null
            ? RetenerAsincrono(solicitud, ct)
            : Task.FromResult(Retener(solicitud));
    }

    public async Task<Resultado<ConsultaTransferencia>> ConsultarEstadoAsync(Guid transferenciaId, CancellationToken ct = default)
    {
        ConsultasEstado++;
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        if (Estados.Count > 0)
        {
            return Estados.Dequeue();
        }

        return EstadoRepetido ?? Resultado<ConsultaTransferencia>.Fallo(
            new ErrorApi(TipoError.NoEncontrado, "No se encontró la transferencia."));
    }
}

public static class Datos
{
    public static ErrorApi Error(TipoError tipo, string mensaje = "error") => new(tipo, mensaje);

    public static Resultado<ConsultaTransferencia> Estado(EstadoTransferencia estado) =>
        Resultado<ConsultaTransferencia>.Ok(new ConsultaTransferencia(
            Guid.NewGuid(), Guid.NewGuid(), 10m, estado, null, null, null));

    public static Sesion SesionOperador(DateTimeOffset? expira = null) =>
        new("jwt-de-prueba", expira ?? DateTimeOffset.UtcNow.AddMinutes(15), "operador", "ana.operadora");
}
