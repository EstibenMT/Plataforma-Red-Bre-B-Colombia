namespace Breb.App.Services.Api;

/// <summary>Añade Idempotency-Key cuando la petición lo pide (la API actual la ignora; prepara el Issue #29).</summary>
public sealed class IdempotenciaHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Options.TryGetValue(OpcionesHttp.ClaveIdempotencia, out var clave))
        {
            request.Headers.Remove("Idempotency-Key");
            request.Headers.Add("Idempotency-Key", clave.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
