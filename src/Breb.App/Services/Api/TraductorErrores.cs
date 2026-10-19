using System.Net;
using System.Text.Json;
using Breb.App.Models;

namespace Breb.App.Services.Api;

/// <summary>Datos que cambian el mensaje de un mismo código HTTP.</summary>
public sealed record ContextoError(
    bool EsLogin = false,
    string? Rol = null,
    string MensajeNoEncontrado = "No encontramos la cuenta. Verifique el identificador.");

/// <summary>Convierte respuestas HTTP y excepciones en <see cref="ErrorApi"/> con mensajes en español.</summary>
public static class TraductorErrores
{
    public static ErrorApi SinConexion() =>
        new(TipoError.SinConexion, "No hay conexión a internet. Revise su red e intente de nuevo.");

    public static ErrorApi RespuestaInvalida() =>
        new(TipoError.RespuestaInvalida, "Recibimos una respuesta que no pudimos interpretar. Intente de nuevo.");

    public static ErrorApi UrlInvalida(string baseUrl) =>
        new(TipoError.ServidorNoDisponible, "La dirección del servidor no es válida. Corríjala en «Configurar conexión».");

    public static ErrorApi DesdeExcepcion(Exception ex, string baseUrl) => ex switch
    {
        JsonException => RespuestaInvalida(),
        HttpRequestException => new ErrorApi(
            TipoError.ServidorNoDisponible,
            "No pudimos conectarnos con Bre-B. Revise su conexión e intente de nuevo en unos momentos."),
        OperationCanceledException => new ErrorApi(
            TipoError.Timeout, "Bre-B tardó demasiado en responder. Intente de nuevo."),
        _ => new ErrorApi(TipoError.Desconocido, "Ocurrió un error inesperado. Intente de nuevo.")
    };

    public static async Task<ErrorApi> DesdeRespuestaAsync(
        HttpResponseMessage respuesta, ContextoError contexto, CancellationToken ct = default)
    {
        var traceId = respuesta.Headers.TryGetValues("X-Trace-Id", out var valores)
            ? valores.FirstOrDefault()
            : null;
        var detalle = respuesta.StatusCode == HttpStatusCode.BadRequest
            ? await LeerDetalleAsync(respuesta, ct).ConfigureAwait(false)
            : null;
        return DesdeStatus(respuesta.StatusCode, contexto, traceId, detalle);
    }

    public static ErrorApi DesdeStatus(HttpStatusCode status, ContextoError contexto, string? traceId, string? detalle)
    {
        var codigo = (int)status;
        var referencia = string.IsNullOrWhiteSpace(traceId) ? "" : $" Referencia para soporte: {traceId}.";

        if (status == HttpStatusCode.Unauthorized)
        {
            return contexto.EsLogin
                ? new ErrorApi(TipoError.CredencialesInvalidas, "Usuario o contraseña incorrectos.", traceId, codigo)
                : new ErrorApi(TipoError.SesionExpirada, "Su sesión venció por seguridad. Ingrese de nuevo.", traceId, codigo);
        }

        return status switch
        {
            HttpStatusCode.Forbidden => new ErrorApi(
                TipoError.SinPermiso,
                $"Su perfil (rol {contexto.Rol ?? "desconocido"}) no tiene permiso para realizar esta operación.",
                traceId, codigo),
            HttpStatusCode.NotFound => new ErrorApi(TipoError.NoEncontrado, contexto.MensajeNoEncontrado, traceId, codigo),
            HttpStatusCode.BadRequest => new ErrorApi(
                TipoError.SolicitudInvalida,
                string.IsNullOrWhiteSpace(detalle) ? "Revise los datos ingresados." : $"Revise los datos ingresados. {detalle}",
                traceId, codigo),
            HttpStatusCode.Conflict => new ErrorApi(TipoError.SaldoInsuficiente, "Saldo insuficiente para realizar esta transferencia.", traceId, codigo),
            >= HttpStatusCode.InternalServerError => new ErrorApi(
                TipoError.ErrorServidor,
                $"Bre-B tuvo un problema de nuestro lado. Intente de nuevo en unos minutos.{referencia}",
                traceId, codigo),
            _ => new ErrorApi(TipoError.RespuestaInvalida, $"No pudimos completar la operación (código {codigo}). Intente de nuevo.", traceId, codigo)
        };
    }

    /// <summary>Results.Problem(texto) pone el texto en "detail" y deja "title" genérico: se prefiere detail.</summary>
    private static async Task<string?> LeerDetalleAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        try
        {
            var cuerpo = await respuesta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var documento = JsonDocument.Parse(cuerpo);
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return LeerTexto(documento.RootElement, "detail") ?? LeerTexto(documento.RootElement, "title");
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException)
        {
            return null;
        }
    }

    private static string? LeerTexto(JsonElement objeto, string propiedad) =>
        objeto.TryGetProperty(propiedad, out var valor)
        && valor.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(valor.GetString())
            ? valor.GetString()
            : null;
}
