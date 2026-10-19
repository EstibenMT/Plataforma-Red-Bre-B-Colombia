using System.Text.Json;
using System.Text.Json.Serialization;

namespace Breb.App.Services.Api;

/// <summary>Opciones compartidas por los servicios HTTP.</summary>
public static class OpcionesHttp
{
    /// <summary>Opción de petición con la que se pide al IdempotenciaHandler que añada Idempotency-Key.</summary>
    public static readonly HttpRequestOptionsKey<Guid> ClaveIdempotencia = new("breb.idempotency-key");

    /// <summary>Segundos de margen: un token que vence en menos de esto se considera vencido.</summary>
    public const int MargenExpiracionSegundos = 10;

    public static readonly JsonSerializerOptions Json = Crear();

    private static JsonSerializerOptions Crear()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opciones.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return opciones;
    }
}
