using Microsoft.EntityFrameworkCore;
using booking.core.Constants;
using booking.core.DTOs;
using booking.core.Models;
using booking.infrastructure.Persistence;

namespace booking.infrastructure.Services;

public interface IServicesService
{
	Task<(bool ok, List<ServiceModel> data, string code)> GetAllAsync(int? officeId, CancellationToken ct);
	Task<(bool ok, ServiceModel? data, string code)> GetByIdAsync(int id, CancellationToken ct);
	Task<(bool ok, int id, string code, string? message)> CreateAsync(ServiceUpsertDto dto, CancellationToken ct);
	Task<(bool ok, string code, string? message)> UpdateAsync(int id, ServiceUpsertDto dto, CancellationToken ct);
	Task<(bool ok, string code, string? message)> DeleteAsync(int id, CancellationToken ct);
}

public class ServicesService : IServicesService
{
	private readonly AppDbContext _db;
	public ServicesService(AppDbContext db) => _db = db;

	// officeId is optional: without it, every office's services are returned.
	public async Task<(bool ok, List<ServiceModel> data, string code)> GetAllAsync(int? officeId, CancellationToken ct)
	{
		var items = await _db.Services
			.AsNoTracking()
			.Include(s => s.ServiceDetails)
			.Where(s => officeId == null || s.OfficeId == officeId)
			.OrderBy(s => s.Id)
			.Select(s => MapToModel(s))
			.ToListAsync(ct);

		return (true, items, Codes.Ok);
	}

	public async Task<(bool ok, ServiceModel? data, string code)> GetByIdAsync(int id, CancellationToken ct)
	{
		var s = await _db.Services
			.AsNoTracking()
			.Include(x => x.ServiceDetails)
			.FirstOrDefaultAsync(x => x.Id == id, ct);

		if (s is null) return (false, null, Codes.NotFound);
		return (true, MapToModel(s), Codes.Ok);
	}

	public async Task<(bool ok, int id, string code, string? message)> CreateAsync(ServiceUpsertDto dto, CancellationToken ct)
	{
		var s = new Service
		{
			OfficeId = dto.officeId,
			Name = dto.name,
			Description = dto.description,
			Quantity = dto.quantity,
			MaxBookingsPerWeek = dto.maxBookingsPerWeek,
			BookingPerTime = dto.bookingPerTime,
			MinMax = dto.minMax,
			ImageUrl = dto.imageUrl,
			CreatedAt = DateTime.UtcNow
		};

		if (dto.serviceDetails is { Count: > 0 })
		{
			s.ServiceDetails = dto.serviceDetails.Select(d => new ServiceDetail
			{
				Name = d.name,
				Description = d.description,
				Capacity = d.capacity,
				IconUrl = d.iconUrl,
				CreatedAt = DateTime.UtcNow
			}).ToList();
		}

		_db.Services.Add(s);
		await _db.SaveChangesAsync(ct);
		return (true, s.Id, Codes.Ok, null);
	}

	public async Task<(bool ok, string code, string? message)> UpdateAsync(int id, ServiceUpsertDto dto, CancellationToken ct)
	{
		var s = await _db.Services.Include(x => x.ServiceDetails).FirstOrDefaultAsync(x => x.Id == id, ct);
		if (s is null) return (false, Codes.NotFound, "Service not found.");

		s.OfficeId = dto.officeId;
		s.Name = dto.name;
		s.Description = dto.description;
		s.Quantity = dto.quantity;
		s.MaxBookingsPerWeek = dto.maxBookingsPerWeek;
		s.BookingPerTime = dto.bookingPerTime;
		s.MinMax = dto.minMax;
		s.ImageUrl = dto.imageUrl;

		// Match details by id so existing ones (and the bookings that point at them) are kept.
		var requested = dto.serviceDetails ?? [];
		var existingById = s.ServiceDetails.ToDictionary(d => d.Id);

		var unknownId = requested.FirstOrDefault(d => d.id is int detailId && !existingById.ContainsKey(detailId));
		if (unknownId is not null)
		{
			return (false, Codes.BadRequest, $"Service detail {unknownId.id} doesn't belong to this service.");
		}

		var keptIds = requested.Where(d => d.id.HasValue).Select(d => d.id!.Value).ToHashSet();
		var removed = s.ServiceDetails.Where(d => !keptIds.Contains(d.Id)).ToList();
		if (removed.Count > 0)
		{
			var removedIds = removed.Select(d => d.Id).ToList();
			var bookedNames = await _db.Bookings
				.Where(b => removedIds.Contains(b.ServiceDetailId))
				.Select(b => b.ServiceDetail!.Name)
				.Distinct()
				.ToListAsync(ct);
			if (bookedNames.Count > 0)
			{
				return (false, Codes.InUse, $"Can't remove {string.Join(", ", bookedNames)} because it has bookings.");
			}
			_db.ServiceDetails.RemoveRange(removed);
		}

		foreach (var d in requested)
		{
			var detail = d.id is int detailId ? existingById[detailId] : new ServiceDetail { CreatedAt = DateTime.UtcNow };
			detail.Name = d.name;
			detail.Description = d.description;
			detail.Capacity = d.capacity;
			detail.IconUrl = d.iconUrl;
			if (!d.id.HasValue)
			{
				s.ServiceDetails.Add(detail);
			}
		}

		await _db.SaveChangesAsync(ct);
		return (true, Codes.Ok, null);
	}

	public async Task<(bool ok, string code, string? message)> DeleteAsync(int id, CancellationToken ct)
	{
		var s = await _db.Services.Include(x => x.ServiceDetails).FirstOrDefaultAsync(x => x.Id == id, ct);
		if (s is null) return (false, Codes.NotFound, "Service not found.");

		if (await _db.Bookings.AnyAsync(b => b.ServiceDetail!.ServiceId == id, ct))
		{
			return (false, Codes.InUse, "This service has bookings, so it can't be deleted.");
		}

		// The details reference the service, so they go first.
		_db.ServiceDetails.RemoveRange(s.ServiceDetails);
		_db.Services.Remove(s);
		await _db.SaveChangesAsync(ct);
		return (true, Codes.Ok, null);
	}

	private static ServiceModel MapToModel(Service s) =>
		new(
			s.Id,
			s.OfficeId,
			s.Name,
			s.Description,
			s.Quantity,
			s.MaxBookingsPerWeek,
			s.BookingPerTime,
			s.MinMax,
			s.ImageUrl,
			s.ServiceDetails
				.OrderBy(d => d.Id)
				.Select(d => new ServiceDetailModel(d.Id, d.ServiceId, d.Name, d.Description, d.Capacity, d.IconUrl))
				.ToList()
		);
}