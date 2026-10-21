using System.Net.Http.Json;
using Breb.App.Models;
using Breb.App.Services.Abstracciones;

namespace Breb.App.Services.Api;

public sealed class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly IConfiguracionApi _configuracion;
    private readonly IEstadoRed _red;
    private readonly ISesionStore _store;
    private readonly TimeProvider _reloj;

    public AuthService(HttpClient http, IConfiguracionApi configuracion, IEstadoRed red, ISesionStore store, TimeProvider reloj)
    {
        _http = http;
        _configuracion = configuracion;
        _red = red;
        _store = store;
        _reloj = reloj;
    }

    public async Task<Resultado<Sesion>> IniciarSesionAsync(string usuario, string clave, CancellationToken ct = default, string? baseUrl = null)
    {
        baseUrl ??= _configuracion.BaseUrl;
        if (!EjecutorHttp.TryCrearUri(baseUrl, "/token", out var uri))
        {
            return Resultado<Sesion>.Fallo(TraductorErrores.UrlInvalida(baseUrl));
        }

        using var peticion = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(new SolicitudLogin(usuario, clave), options: OpcionesHttp.Json)
        };

        var resultado = await EjecutorHttp.EnviarAsync<RespuestaToken>(
            _http, _red, baseUrl, peticion,
            r => TraductorErrores.DesdeRespuestaAsync(r, new ContextoError(EsLogin: true), ct),
            ct).ConfigureAwait(false);

        if (!resultado.EsExito)
        {
            return Resultado<Sesion>.Fallo(resultado.Error);
        }

        var token = resultado.Valor;
        if (string.IsNullOrWhiteSpace(token.Token))
        {
            return Resultado<Sesion>.Fallo(TraductorErrores.RespuestaInvalida());
        }
        //Aqui se guarda la secion en scurity storage
        var sesion = new Sesion(token.Token, token.Expira, token.Rol, usuario);
        try
        {
            await _store.GuardarAsync(sesion).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return Resultado<Sesion>.Fallo(new ErrorApi(
                TipoError.Desconocido, "No se pudo guardar la sesión de forma segura en el dispositivo."));
        }

        return Resultado<Sesion>.Ok(sesion);
    }

    public async Task<Sesion?> ObtenerSesionVigenteAsync()
    {
        var sesion = await _store.LeerAsync().ConfigureAwait(false);
        if (sesion is null)
        {
            return null;
        }

        if (sesion.ExpiraEn <= _reloj.GetUtcNow().AddSeconds(OpcionesHttp.MargenExpiracionSegundos))
        {
            await _store.BorrarAsync().ConfigureAwait(false);
            return null;
        }

        return sesion;
    }

    public Task CerrarSesionAsync() => _store.BorrarAsync();
}
