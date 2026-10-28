using Breb.App.Services.Abstracciones;
using Breb.App.Services.Api;
using Breb.App.Services.Plataforma;
using Breb.App.ViewModels;
using Breb.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Breb.App.Servicios;
using Breb.App.ViewModels;

namespace Breb.App;

public static class MauiProgram
{
	private static readonly TimeSpan TiempoMaximoHttp = TimeSpan.FromSeconds(15);

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		RegistrarServicios(builder.Services);

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	private static void RegistrarServicios(IServiceCollection servicios)
	{
		// Plataforma (dependen de MAUI)
		servicios.AddSingleton(TimeProvider.System);
		servicios.AddSingleton<ISesionStore, SecureStorageSesionStore>();
		servicios.AddSingleton<IConfiguracionApi, ConfiguracionApi>();
		servicios.AddSingleton<IEstadoRed, EstadoRedMaui>();
		servicios.AddSingleton<INavegacionService, ShellNavegacionService>();
		servicios.AddSingleton<IDialogoService, DialogoService>();

		// HTTP: el login no lleva Bearer; la API sí, más la clave de idempotencia
		servicios.AddTransient<AutenticacionHandler>();
		servicios.AddTransient<IdempotenciaHandler>();
		servicios.AddHttpClient<IAuthService, AuthService>(c => c.Timeout = TiempoMaximoHttp);
		servicios.AddHttpClient<IBrebApi, ClienteBrebApi>(c => c.Timeout = TiempoMaximoHttp)
			.AddHttpMessageHandler<IdempotenciaHandler>()
			.AddHttpMessageHandler<AutenticacionHandler>();
		servicios.AddTransient<ITransferenciaService, TransferenciaDirectaService>();

		// ViewModels y páginas
		servicios.AddTransient<LoginViewModel>();
		servicios.AddTransient<SaldoViewModel>();
		servicios.AddTransient<TransferenciaViewModel>();
		servicios.AddTransient<LoginPage>();
		servicios.AddTransient<SaldoPage>();
		servicios.AddTransient<TransferenciaPage>();
	}
}
