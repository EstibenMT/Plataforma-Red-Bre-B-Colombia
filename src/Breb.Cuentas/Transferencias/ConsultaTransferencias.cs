using Microsoft.EntityFrameworkCore;
using Breb.Cuentas.Infraestructura;
using Breb.Cuentas.Seguridad;

namespace Breb.Cuentas.Transferencias;

/// <summary>
/// Seguimiento de una transferencia (Issue #28): solo lectura.
///
/// POST /retener solo aparta fondos y crea la saga; el desenlace es asíncrono
/// y la saga se borra al terminar. Lo que SÍ queda para siempre es la fila de
/// Retenciones, así que el resultado exacto se lee de ahí:
///   Liquidada            → Completada   (el dinero salió hacia el banco destino)
///   Liberada             → Compensada   (el dinero volvió a la cuenta)
///   pendiente + saga viva en Compensando → Compensando
///   pendiente (resto)    → EnProceso
/// Sin migraciones y sin tocar la saga ni MassTransit.
/// </summary>
public static class ConsultaTransferencias
{
    public static IEndpointRouteBuilder MapConsultaTransferencias(this IEndpointRouteBuilder app)
    {
        app.MapGet("/transferencias/{transferenciaId:guid}",
            async (Guid transferenciaId, CuentasDbContext db) =>
            {
                var retencion = await db.Retenciones.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.TransferenciaId == transferenciaId);

                // 404 sin cuerpo: nunca hubo retención con ese id.
                if (retencion is null) return Results.NotFound();

                string estado;
                string? motivo = null;
                DateTime? resueltaEn = null;

                if (retencion.Liquidada)
                {
                    estado = "Completada";
                    resueltaEn = retencion.LiquidadaEn;
                }
                else if (retencion.Liberada)
                {
                    // La saga ya se borró; si aún vive, el motivo sale de ella.
                    estado = "Compensada";
                    resueltaEn = retencion.LiberadaEn;
                }
                else
                {
                    estado = "EnProceso";
                }

                // La saga viva (si la hay) aporta el motivo y distingue Compensando.
                var saga = await db.TransferenciaSagas.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.CorrelationId == transferenciaId);

                if (saga is not null)
                {
                    motivo = saga.MotivoCompensacion;
                    if (estado == "EnProceso" && saga.CurrentState == "Compensando")
                        estado = "Compensando";
                }

                return Results.Ok(new
                {
                    transferenciaId,
                    cuentaId = retencion.CuentaId,
                    montoUVB = retencion.MontoUVB,
                    estado,
                    motivo,
                    creadaEn = retencion.CreadaEn,
                    resueltaEn
                });
            })
            .RequireAuthorization(Politicas.LecturaCuentas);

        return app;
    }
}
