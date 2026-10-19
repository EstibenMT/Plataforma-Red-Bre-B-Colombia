using System.Net;
using System.Net.Http.Headers;
using Breb.App.Services.Abstracciones;

namespace Breb.App.Services.Api;

/// <summary>
/// Adjunta Authorization: Bearer leyendo el token del almacén seguro EN CADA petición
/// (no hay copia del token en memoria). Si la sesión ya venció, responde 401 sin llamar a la red.
/// </summary>
public sealed class AutenticacionHandler : DelegatingHandler
{
    private readonly ISesionStore _store;
    private readonly TimeProvider _reloj;

    public AutenticacionHandler(ISesionStore store, TimeProvider reloj)
    {
        _store = store;
        _reloj = reloj;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sesion = await _store.LeerAsync().ConfigureAwait(false);
        if (sesion is not null)
        {
            if (sesion.ExpiraEn <= _reloj.GetUtcNow().AddSeconds(OpcionesHttp.MargenExpiracionSegundos))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sesion.Token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
