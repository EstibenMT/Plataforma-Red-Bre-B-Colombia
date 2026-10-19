using System.Diagnostics.CodeAnalysis;

namespace Breb.App.Models;

/// <summary>Clasificación de los fallos que la app sabe explicar al usuario.</summary>
public enum TipoError
{
    SinConexion,
    ServidorNoDisponible,
    Timeout,
    CredencialesInvalidas,
    SesionExpirada,
    SinPermiso,
    NoEncontrado,
    SolicitudInvalida,
    SaldoInsuficiente,
    ErrorServidor,
    RespuestaInvalida,
    Desconocido
}

/// <summary>Error ya traducido a un mensaje en español listo para mostrar.</summary>
public sealed record ErrorApi(TipoError Tipo, string Mensaje, string? TraceId = null, int? CodigoHttp = null);

/// <summary>
/// Resultado de una operación de red: valor o error, nunca una excepción.
/// Obliga al ViewModel a tratar el fallo (en móvil la red que falla es lo normal).
/// </summary>
public sealed class Resultado<T> where T : class
{
    private Resultado(T? valor, ErrorApi? error)
    {
        Valor = valor;
        Error = error;
    }

    [MemberNotNullWhen(true, nameof(Valor))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool EsExito => Error is null;

    public T? Valor { get; }

    public ErrorApi? Error { get; }

    public static Resultado<T> Ok(T valor) => new(valor, null);

    public static Resultado<T> Fallo(ErrorApi error) => new(null, error);
}
