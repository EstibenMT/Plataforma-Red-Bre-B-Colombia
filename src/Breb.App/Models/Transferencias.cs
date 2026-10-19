namespace Breb.App.Models;

/// <summary>Respuesta de GET /cuentas/{id}/saldo.</summary>
public sealed record SaldoCuenta(Guid CuentaId, decimal SaldoDisponible, decimal SaldoRetenido);

/// <summary>Respuesta de POST /cuentas/{id}/retener.</summary>
public sealed record RespuestaRetencion(Guid TransferenciaId, decimal MontoUVB);

/// <summary>
/// Intención de transferir. Su <see cref="ClaveIdempotencia"/> nace al expresar la intención,
/// no en cada intento: es lo que el Issue #29 encolará.
/// </summary>
public sealed record SolicitudTransferencia(Guid CuentaId, decimal MontoUVB, Guid ClaveIdempotencia);

/// <summary>Estado de la transferencia según GET /transferencias/{id}.</summary>
public enum EstadoTransferencia
{
    EnProceso,
    Compensando,
    Completada,
    Compensada
}

/// <summary>Respuesta de GET /transferencias/{id}.</summary>
public sealed record ConsultaTransferencia(
    Guid TransferenciaId,
    Guid CuentaId,
    decimal MontoUVB,
    EstadoTransferencia Estado,
    string? Motivo,
    DateTimeOffset? CreadaEn,
    DateTimeOffset? ResueltaEn);

/// <summary>Fase que la pantalla de Transferencia muestra al usuario.</summary>
public enum FaseTransferencia
{
    Ninguna,
    Enviando,
    EnProceso,
    Completada,
    Compensada,
    Indeterminada,
    SinSeguimiento
}
