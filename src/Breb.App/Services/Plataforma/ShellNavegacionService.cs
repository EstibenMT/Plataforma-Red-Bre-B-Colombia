using Breb.App.Services.Abstracciones;

namespace Breb.App.Services.Plataforma;

public sealed class ShellNavegacionService : INavegacionService
{
    public Task IrALoginAsync() => IrAAsync("//login");

    // Ruta absoluta: no deja "atrás" hacia Login.
    public Task IrASaldoAsync() => IrAAsync("//saldo");

    public Task IrATransferenciaAsync() => IrAAsync("transferencia");

    public Task VolverAsync() => IrAAsync("..");

    private static Task IrAAsync(string ruta) =>
        MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(ruta));
}
