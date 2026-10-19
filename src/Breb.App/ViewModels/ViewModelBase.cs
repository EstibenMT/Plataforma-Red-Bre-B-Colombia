using System.Diagnostics;
using Breb.App.Models;
using Breb.App.Services.Abstracciones;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Breb.App.ViewModels;

/// <summary>
/// Base de los ViewModels: estado ocupado, banner de error y la reacción única ante los errores de red.
/// No usa ningún tipo de MAUI; navegación y diálogos pasan por interfaces.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    protected ViewModelBase(IAuthService auth, INavegacionService navegacion, IDialogoService dialogos)
    {
        Auth = auth;
        Navegacion = navegacion;
        Dialogos = dialogos;
    }

    protected IAuthService Auth { get; }

    protected INavegacionService Navegacion { get; }

    protected IDialogoService Dialogos { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoOcupado))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayError))]
    private string? _mensajeError;

    public bool NoOcupado => !IsBusy;

    public bool HayError => !string.IsNullOrEmpty(MensajeError);

    /// <summary>
    /// 401 por sesión vencida: cierra sesión y va a Login. Cualquier otro error: banner.
    /// El 403 no cierra la sesión (el usuario sigue siendo válido, solo no tiene permiso).
    /// </summary>
    protected async Task ManejarErrorAsync(ErrorApi error)
    {
        if (error.Tipo == TipoError.SesionExpirada)
        {
            await Auth.CerrarSesionAsync();
            await Navegacion.IrALoginAsync();
            await Dialogos.MostrarAsync("Sesión expirada", error.Mensaje);
            return;
        }

        MensajeError = error.Mensaje;
    }

    /// <summary>Ejecuta una acción con IsBusy garantizado y sin dejar escapar excepciones a la UI.</summary>
    protected async Task EjecutarAsync(Func<Task> accion)
    {
        IsBusy = true;
        MensajeError = null;
        try
        {
            await accion();
        }
        catch (OperationCanceledException)
        {
            // Cancelación solicitada por la propia pantalla: no es un error.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error no controlado en {GetType().Name}: {ex.GetType().Name}");
            MensajeError = "Ocurrió un error inesperado. Intente de nuevo.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
