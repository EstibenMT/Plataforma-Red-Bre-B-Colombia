namespace Breb.App.Models;

/// <summary>Datos de la sesión que se guardan cifrados en SecureStorage.</summary>
public sealed record Sesion(string Token, DateTimeOffset ExpiraEn, string Rol, string Usuario);

/// <summary>Cuerpo de POST /token.</summary>
public sealed record SolicitudLogin(string Usuario, string Clave);

/// <summary>Respuesta de POST /token.</summary>
public sealed record RespuestaToken(string Token, DateTimeOffset Expira, string Tipo, string Rol, int DuracionMinutos);
