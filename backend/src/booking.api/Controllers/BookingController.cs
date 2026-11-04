using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using booking.core.Constants;
using booking.core.DTOs;
using booking.infrastructure.Services;

namespace booking.api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class BookingController : ControllerBase
{
	private readonly IBookingService _svc;
	public BookingController(IBookingService svc)
	{
		_svc = svc;
	}

	[Authorize(Policy = Permissions.Bookings.Read)]
	[HttpGet]
	public async Task<IActionResult> GetAll(CancellationToken ct)
	{
		var (ok, data, code) = await _svc.GetAllAsync(ct);
		return Ok(new { code, data });
	}

	[Authorize(Policy = Permissions.Bookings.Read)]
	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetById(int id, CancellationToken ct)
	{
		var (ok, data, code) = await _svc.GetByIdAsync(id, ct);
		if (!ok || data is null) return NotFound(new { code = Codes.NotFound, message = "Booking not found." });
		return Ok(new { code, data });
	}

	[Authorize(Policy = Permissions.Bookings.Read)]
	[HttpGet("Service")]
	public async Task<IActionResult> GetByServiceId(
		[FromQuery] int? serviceId,
		CancellationToken ct)
	{
		if (!serviceId.HasValue)
			return BadRequest(new { code = Codes.NotFound, message = "serviceId is required." });

		var (ok, data, code) = await _svc.GetByServiceIdAsync(serviceId.Value, ct);

		if (!ok || data is null || !data.Any())
			return NotFound(new { code = Codes.NotFound, message = "Bookings not found." });

		return Ok(new { code, data });
	}

	[Authorize(Policy = Permissions.Bookings.Create)]
	[HttpPost]
	public async Task<IActionResult> Create([FromBody] BookingUpsertDto body, CancellationToken ct)
	{
		if (CurrentEmployeeId() is not int employeeId) return NoEmployee();

		var (ok, id, code, message) = await _svc.CreateAsync(body, employeeId, ct);
		if (!ok) return Failure(code, message);
		return CreatedAtAction(nameof(GetById), new { id }, new { code = Codes.Ok, data = new { id } });
	}

	[Authorize(Policy = Permissions.Bookings.Update)]
	[HttpPut("{id:int}")]
	public async Task<IActionResult> Update(int id, [FromBody] BookingUpsertDto body, CancellationToken ct)
	{
		if (CurrentEmployeeId() is not int employeeId) return NoEmployee();

		var (ok, code, message) = await _svc.UpdateAsync(id, body, employeeId, User.IsInRole("Admin"), ct);
		if (!ok) return Failure(code, message);
		return Ok(new { code = Codes.Ok });
	}

	[Authorize(Policy = Permissions.Bookings.Delete)]
	[HttpDelete("{id:int}")]
	public async Task<IActionResult> Delete(int id, CancellationToken ct)
	{
		if (CurrentEmployeeId() is not int employeeId) return NoEmployee();

		var (ok, code, message) = await _svc.DeleteAsync(id, employeeId, User.IsInRole("Admin"), ct);
		if (!ok) return Failure(code, message);
		return Ok(new { code = Codes.Ok });
	}

	// Leveraging the middleware logic that sets the JWT.
	// "User" is set by AuthService.CreateJwt for users when they have an Employee record.
	private int? CurrentEmployeeId()
	{
		return int.TryParse(User.FindFirst("employee_id")?.Value, out var id) ? id : null;
	}

	private ObjectResult NoEmployee()
	{
		return StatusCode(StatusCodes.Status403Forbidden,
			new { code = Codes.Forbidden, message = "Your account isn't linked to an employee, so it can't make bookings." });
	}

	private IActionResult Failure(string code, string? message)
	{
		return code switch
		{
			Codes.NotFound => NotFound(new { code, message }),
			Codes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { code, message }),
			Codes.BookingConflict => Conflict(new { code, message }),
			_ => BadRequest(new { code, message })
		};
	}

	[Authorize(Policy = Permissions.Bookings.Read)]
	[HttpPost("availability")]
	public async Task<IActionResult> GetAvailability([FromBody] Availability body, CancellationToken ct)
	{
		var (ok, data, message) = await _svc.GetAvailability(body, ct);

		if (!ok || data is null)
		{
			return NotFound(new
			{
				code = Codes.NotFound,
				message = message ?? "Availability not found."
			});
		}

		return Ok(new { code = Codes.Ok, data });
	}

}