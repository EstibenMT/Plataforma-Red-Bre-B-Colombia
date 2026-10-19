using System.Text.Json;
using Breb.App.Models;
using Breb.App.Services.Abstracciones;

namespace Breb.App.Services.Plataforma;

/// <summary>
/// Guarda la sesión (con el JWT) en SecureStorage (Keystore en Android).
/// No hay copia en memoria: cada lectura va al almacén.
/// </summary>
public sealed class SecureStorageSesionStore : ISesionStore
{
    private const string Clave = "breb.sesion";

    public async Task<Sesion?> LeerAsync()
    {
        try
        {
            var json = await SecureStorage.Default.GetAsync(Clave);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<Sesion>(json);
        }
        catch (Exception)
        {
            // Caso conocido en Android tras restaurar un backup: la clave del Keystore ya no sirve.
            SecureStorage.Default.Remove(Clave);
            return null;
        }
    }

    public Task GuardarAsync(Sesion sesion) =>
        SecureStorage.Default.SetAsync(Clave, JsonSerializer.Serialize(sesion));

    public Task BorrarAsync()
    {
        SecureStorage.Default.Remove(Clave);
        return Task.CompletedTask;
    }
}
