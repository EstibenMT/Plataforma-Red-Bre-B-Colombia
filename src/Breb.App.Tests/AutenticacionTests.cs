using System.Net;
using Breb.App.Models;
using Breb.App.Services.Api;

namespace Breb.App.Tests;

public class AutenticacionHandlerTests
{
    private static (HttpClient Cliente, FakeHttpMessageHandler Interno) Crear(FakeSesionStore store)
    {
        var interno = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var handler = new AutenticacionHandler(store, TimeProvider.System) { InnerHandler = interno };
        return (new HttpClient(handler), interno);
    }

    [Fact]
    public async Task ConSesion_AgregaBearer()
    {
        var (cliente, interno) = Crear(new FakeSesionStore { Sesion = Datos.SesionOperador() });

        await cliente.GetAsync("http://x.test/saldo");

        var auth = interno.Peticiones.Single().Headers.Authorization;
        Assert.Equal("Bearer", auth!.Scheme);
        Assert.Equal("jwt-de-prueba", auth.Parameter);
    }

    [Fact]
    public async Task SinSesion_NoAgregaCabecera()
    {
        var (cliente, interno) = Crear(new FakeSesionStore());

        await cliente.GetAsync("http://x.test/saldo");

        Assert.Null(interno.Peticiones.Single().Headers.Authorization);
    }

    [Fact]
    public async Task SesionVencida_Responde401SinLlamarALaRed()
    {
        var store = new FakeSesionStore { Sesion = Datos.SesionOperador(DateTimeOffset.UtcNow.AddSeconds(3)) };
        var (cliente, interno) = Crear(store);

        var respuesta = await cliente.GetAsync("http://x.test/saldo");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Empty(interno.Peticiones);
    }
}

public class AuthServiceTests
{
    private readonly FakeSesionStore _store = new();

    private AuthService Crear(FakeHttpMessageHandler handler, FakeEstadoRed? red = null) =>
        new(new HttpClient(handler), new FakeConfiguracion(), red ?? new FakeEstadoRed(), _store, TimeProvider.System);

    [Fact]
    public async Task LoginExitoso_GuardaSesion()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK,
            """{"token":"abc.def","expira":"2099-01-01T00:00:00Z","tipo":"Bearer","rol":"operador","duracionMinutos":15}""");

        var r = await Crear(handler).IniciarSesionAsync("ana.operadora", "Operadora-2026");

        Assert.True(r.EsExito);
        Assert.Equal(1, _store.Guardados);
        Assert.Equal("abc.def", _store.Sesion!.Token);
        Assert.Equal("operador", _store.Sesion.Rol);
        Assert.Equal("ana.operadora", _store.Sesion.Usuario);
        var peticion = handler.Peticiones.Single();
        Assert.Equal("/token", peticion.RequestUri!.AbsolutePath);
        var cuerpo = handler.Cuerpos.Single();
        Assert.Contains("\"usuario\":\"ana.operadora\"", cuerpo);
    }

    [Fact]
    public async Task Login401_NoGuardaYEsCredencialesInvalidas()
    {
        var r = await Crear(new FakeHttpMessageHandler(HttpStatusCode.Unauthorized)).IniciarSesionAsync("x", "y");

        Assert.Equal(TipoError.CredencialesInvalidas, r.Error!.Tipo);
        Assert.Equal(0, _store.Guardados);
    }

    [Fact]
    public async Task LoginSinRed_NoGuarda()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");

        var r = await Crear(handler, new FakeEstadoRed { HayConexion = false }).IniciarSesionAsync("x", "y");

        Assert.Equal(TipoError.SinConexion, r.Error!.Tipo);
        Assert.Empty(handler.Peticiones);
    }

    [Fact]
    public async Task SesionVigente_BorraLaVencida()
    {
        _store.Sesion = Datos.SesionOperador(DateTimeOffset.UtcNow.AddMinutes(-1));
        var servicio = Crear(new FakeHttpMessageHandler(HttpStatusCode.OK, "{}"));

        Assert.Null(await servicio.ObtenerSesionVigenteAsync());
        Assert.Null(_store.Sesion);
    }
}
