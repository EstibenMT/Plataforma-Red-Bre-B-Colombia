using Breb.App.Models;

namespace Breb.App.Services.Abstracciones;

public interface IAuthService
{
    /// <summary>
    /// Hace POST /token y, si sale bien, guarda la sesión en el almacén seguro.
    /// <paramref name="baseUrl"/> (opcional) sustituye a la URL configurada sin persistirla.
    /// </summary>
    Task<Resultado<Sesion>> IniciarSesionAsync(string usuario, string clave, CancellationToken ct = default, string? baseUrl = null);

    /// <summary>Devuelve la sesión guardada si no ha vencido; si venció la borra y devuelve null.</summary>
    Task<Sesion?> ObtenerSesionVigenteAsync();

    Task CerrarSesionAsync();
}

public interface ISesionStore
{
    Task<Sesion?> LeerAsync();
    Task GuardarAsync(Sesion sesion);
    Task BorrarAsync();
}

public interface IBrebApi
{
    Task<Resultado<SaldoCuenta>> ObtenerSaldoAsync(Guid cuentaId, CancellationToken ct = default);
    Task<Resultado<RespuestaRetencion>> RetenerAsync(SolicitudTransferencia solicitud, CancellationToken ct = default);
    Task<Resultado<ConsultaTransferencia>> ConsultarEstadoAsync(Guid transferenciaId, CancellationToken ct = default);
}

/// <summary>Punto único de envío de una transferencia (el Issue #29 lo sustituirá por una cola).</summary>
public interface ITransferenciaService
{
    Task<Resultado<RespuestaRetencion>> EnviarAsync(SolicitudTransferencia solicitud, CancellationToken ct = default);
    Task<Resultado<ConsultaTransferencia>> ConsultarEstadoAsync(Guid transferenciaId, CancellationToken ct = default);
}

public interface INavegacionService
{
    Task IrALoginAsync();
    Task IrASaldoAsync();
    Task IrATransferenciaAsync();
    Task VolverAsync();
}

public interface IDialogoService
{
    Task MostrarAsync(string titulo, string mensaje);
}

public interface IConfiguracionApi
{
    string BaseUrl { get; set; }
    Guid UltimaCuentaId { get; set; }
}

public interface IEstadoRed
{
    bool HayConexion { get; }
}
