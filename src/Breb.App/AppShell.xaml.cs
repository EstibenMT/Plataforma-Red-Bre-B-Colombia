using Breb.App.Views;

namespace Breb.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("transferencia", typeof(TransferenciaPage));
	}
}
