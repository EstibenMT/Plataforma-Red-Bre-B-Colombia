using Breb.App.Services.Abstracciones;

namespace Breb.App.Services.Plataforma;

public sealed class DialogoService : IDialogoService
{
    public Task MostrarAsync(string titulo, string mensaje) =>
        MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync(titulo, mensaje, "Aceptar"));
}
