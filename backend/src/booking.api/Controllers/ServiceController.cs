using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using booking.core.Constants;
using booking.core.DTOs;
using booking.infrastructure.Services;

namespace booking.api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
	private readonly IServicesService _svc;
	public ServicesController(IServicesService svc) => _svc = svc;

	[Authorize(Policy = Permissions.Services.Read)]
	[HttpGet]
	public async Task<IActionResult> GetAll([FromQuery] int? officeId, CancellationToken ct)
	{
		var (ok, data, code) = await _svc.GetAllAsync(officeId, ct);
		return Ok(new { code, data });
	}

	[Authorize(Policy = Permissions.Services.Read)]
	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetById(int id, CancellationToken ct)
	{
		var (ok, data, code) = await _svc.GetByIdAsync(id, ct);
		if (!ok || data is null) return NotFound(new { code = Codes.NotFound, message = "Service not found." });
		return Ok(new { code, data });
	}

	[Authorize(Policy = Permissions.Services.Create)]
	[HttpPost]
	public async Task<IActionResult> Create([FromBody] ServiceUpsertDto body, CancellationToken ct)
	{
		var (ok, id, code, message) = await _svc.CreateAsync(body, ct);
		if (!ok) return BadRequest(new { code, message });
		return CreatedAtAction(nameof(GetById), new { id }, new { code = Codes.Ok, data = new { id } });
	}

	[Authorize(Policy = Permissions.Services.Update)]
	[HttpPut("{id:int}")]
	public async Task<IActionResult> Update(int id, [FromBody] ServiceUpsertDto body, CancellationToken ct)
	{
		var (ok, code, message) = await _svc.UpdateAsync(id, body, ct);
		if (!ok && code == Codes.NotFound) return NotFound(new { code, message });
		if (!ok && code == Codes.InUse) return Conflict(new { code, message });
		if (!ok) return BadRequest(new { code, message });
		return Ok(new { code = Codes.Ok });
	}

	[Authorize(Policy = Permissions.Services.Delete)]
	[HttpDelete("{id:int}")]
	public async Task<IActionResult> Delete(int id, CancellationToken ct)
	{
		var (ok, code, message) = await _svc.DeleteAsync(id, ct);
		if (!ok && code == Codes.NotFound) return NotFound(new { code, message });
		if (!ok && code == Codes.InUse) return Conflict(new { code, message });
		if (!ok) return BadRequest(new { code, message });
		return Ok(new { code = Codes.Ok });
	}
}