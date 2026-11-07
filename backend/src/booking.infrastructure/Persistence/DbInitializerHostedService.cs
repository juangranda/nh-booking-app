using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace booking.infrastructure.Persistence;

public class DbInitializerHostedService : IHostedService
{
	private readonly IServiceProvider _sp;
	private readonly IHostEnvironment _env;

	public DbInitializerHostedService(IServiceProvider sp, IHostEnvironment env)
	{
		_sp = sp;
		_env = env;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		using var scope = _sp.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

		var schemaPath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Sql", "schema.sql");
		await DatabaseBootstrapper.InitAsync(db, schemaPath, cancellationToken);
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}