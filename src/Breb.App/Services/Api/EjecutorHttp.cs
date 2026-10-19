using System.Net.Http.Json;
using System.Text.Json;
using Breb.App.Models;
using Breb.App.Services.Abstracciones;

namespace Breb.App.Services.Api;

/// <summary>
/// Envía una petición y la convierte en <see cref="Resultado{T}"/>. Aquí (y solo aquí) viven
/// los catch de HttpRequestException/timeout: ni ViewModels ni vistas los ven.
/// </summary>
internal static class EjecutorHttp
{
    public static bool TryCrearUri(string baseUrl, string rutaYConsulta, out Uri uri)
    {
        var texto = $"{baseUrl.Trim().TrimEnd('/')}{rutaYConsulta}";
        if (Uri.TryCreate(texto, UriKind.Absolute, out var creada)
            && (creada.Scheme == Uri.UriSchemeHttp || creada.Scheme == Uri.UriSchemeHttps))
        {
            uri = creada;
            return true;
        }

        uri = null!;
        return false;
    }

    public static async Task<Resultado<T>> EnviarAsync<T>(
        HttpClient http,
        IEstadoRed red,
        string baseUrl,
        HttpRequestMessage peticion,
        Func<HttpResponseMessage, Task<ErrorApi>> traducirError,
        CancellationToken ct) where T : class
    {
        if (!red.HayConexion)
        {
            return Resultado<T>.Fallo(TraductorErrores.SinConexion());
        }

        try
        {
            using var respuesta = await http.SendAsync(peticion, ct).ConfigureAwait(false);
            if (!respuesta.IsSuccessStatusCode)
            {
                return Resultado<T>.Fallo(await traducirError(respuesta).ConfigureAwait(false));
            }

            var valor = await respuesta.Content
                .ReadFromJsonAsync<T>(OpcionesHttp.Json, ct)
                .ConfigureAwait(false);
            return valor is null
                ? Resultado<T>.Fallo(TraductorErrores.RespuestaInvalida())
                : Resultado<T>.Ok(valor);
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException
                                   || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // OperationCanceledException sin cancelación del usuario = timeout del HttpClient.
            // Si el usuario canceló, la excepción se propaga y el llamador la trata.
            System.Diagnostics.Debug.WriteLine($"[BrebHttp] {peticion.Method} {peticion.RequestUri} -> {ex}");
            return Resultado<T>.Fallo(TraductorErrores.DesdeExcepcion(ex, baseUrl));
        }
    }
}
