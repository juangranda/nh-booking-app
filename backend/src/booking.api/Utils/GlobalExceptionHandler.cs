using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using booking.core.Constants;

namespace booking.api.Utils;

// Turns unhandled exceptions into the same { code, message } shape the controllers return.
public class GlobalExceptionHandler : IExceptionHandler
{
	private const int SqliteConstraintError = 19;

	private readonly ILogger<GlobalExceptionHandler> _logger;
	private readonly IHostEnvironment _env;

	public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
	{
		_logger = logger;
		_env = env;
	}

	public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
	{
		var (status, code, message) = exception switch
		{
			DbUpdateException { InnerException: SqliteException { SqliteErrorCode: SqliteConstraintError } } sqlite
				=> (StatusCodes.Status409Conflict, Codes.Conflict, $"The request conflicts with existing data: {sqlite.InnerException!.Message}"),
			FormatException or ArgumentException
				=> (StatusCodes.Status400BadRequest, Codes.BadRequest, exception.Message),
			_ => (StatusCodes.Status500InternalServerError, Codes.ServerError,
				_env.IsDevelopment() ? exception.Message : "An unexpected error occurred.")
		};

		if (status == StatusCodes.Status500InternalServerError)
			_logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
		else
			_logger.LogWarning(exception, "Request failed with {Status} for {Method} {Path}", status, context.Request.Method, context.Request.Path);

		context.Response.StatusCode = status;
		await context.Response.WriteAsJsonAsync(new { code, message }, ct);
		return true;
	}
}
