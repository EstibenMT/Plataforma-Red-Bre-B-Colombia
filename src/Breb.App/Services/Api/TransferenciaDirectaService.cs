using Breb.App.Models;
using Breb.App.Services.Abstracciones;

namespace Breb.App.Services.Api;

/// <summary>Envío directo (sin cola): llama a la API en el momento. El Issue #29 puede sustituirlo por una cola.</summary>
public sealed class TransferenciaDirectaService : ITransferenciaService
{
    private readonly IBrebApi _api;

    public TransferenciaDirectaService(IBrebApi api) => _api = api;

    public Task<Resultado<RespuestaRetencion>> EnviarAsync(SolicitudTransferencia solicitud, CancellationToken ct = default) =>
        _api.RetenerAsync(solicitud, ct);

    public Task<Resultado<ConsultaTransferencia>> ConsultarEstadoAsync(Guid transferenciaId, CancellationToken ct = default) =>
        _api.ConsultarEstadoAsync(transferenciaId, ct);
}
