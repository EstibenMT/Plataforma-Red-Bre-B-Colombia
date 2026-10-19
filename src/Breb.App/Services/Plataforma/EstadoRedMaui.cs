using Breb.App.Services.Abstracciones;

namespace Breb.App.Services.Plataforma;

public sealed class EstadoRedMaui : IEstadoRed
{
    public bool HayConexion => Connectivity.Current.NetworkAccess != NetworkAccess.None;
}
