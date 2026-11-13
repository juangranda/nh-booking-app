using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using booking.core.Options;

namespace booking.infrastructure.Persistence;

public class DbInitializerHostedService : IHostedService
{
	private readonly IServiceProvider _sp;
	private readonly IHostEnvironment _env;
	private readonly SeedOptions _seed;
	private readonly ILogger<DbInitializerHostedService> _logger;

	public DbInitializerHostedService(IServiceProvider sp, IHostEnvironment env,
		IOptions<SeedOptions> seed, ILogger<DbInitializerHostedService> logger)
	{
		_sp = sp;
		_env = env;
		_seed = seed.Value;
		_logger = logger;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		using var scope = _sp.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

		var seedDemoData = _seed.DemoData ?? _env.IsDevelopment();

		var schemaPath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Sql", "schema.sql");
		await DatabaseBootstrapper.InitAsync(db, schemaPath, seedDemoData,
			_seed.AdminEmail, _seed.AdminPassword, cancellationToken);

		if (!_env.IsDevelopment())
		{
			var demoAccounts = await DatabaseBootstrapper.FindDemoAccountsAsync(db, cancellationToken);
			if (demoAccounts.Count > 0)
			{
				_logger.LogWarning(
					"These accounts still use the demo password from the repository and can be logged into by anyone: {Accounts}. Change their passwords or delete them.",
					string.Join(", ", demoAccounts));
			}
		}
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
