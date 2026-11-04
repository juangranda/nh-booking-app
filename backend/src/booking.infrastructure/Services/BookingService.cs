using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using booking.core.Constants;
using booking.core.DTOs;
using booking.core.Models;
using booking.core.Options;
using booking.infrastructure.Persistence;

namespace booking.infrastructure.Services;

public interface IBookingService
{
	Task<(bool ok, List<BookingModel> data, string code)> GetAllAsync(CancellationToken ct);
	Task<(bool ok, BookingModel? data, string code)> GetByIdAsync(int id, CancellationToken ct);
	Task<(bool ok, List<BookingRow> data, string code)> GetByServiceIdAsync(int serviceId, CancellationToken ct);
	Task<(bool ok, int id, string code, string? message)> CreateAsync(BookingUpsertDto dto, int employeeId, CancellationToken ct);
	Task<(bool ok, string code, string? message)> UpdateAsync(int id, BookingUpsertDto dto, int employeeId, bool isAdmin, CancellationToken ct);
	Task<(bool ok, string code, string? message)> DeleteAsync(int id, int employeeId, bool isAdmin, CancellationToken ct);
	Task<(bool ok, List<AvailabilityTimes> data, string? message)> GetAvailability(Availability dto, CancellationToken ct);
}

public class BookingService : IBookingService
{
	private readonly AppDbContext _db;
	private readonly BookingOptions _hours;

	public BookingService(AppDbContext db, IOptions<BookingOptions> hours)
	{
		_db = db;
		_hours = hours.Value;
	}

	public async Task<(bool ok, List<BookingModel> data, string code)> GetAllAsync(CancellationToken ct)
	{
		var items = await _db.Bookings
			.AsNoTracking()
			.OrderBy(s => s.Id)
			.Select(s => MapToModel(s))
			.ToListAsync(ct);

		return (true, items, Codes.Ok);
	}

	public async Task<(bool ok, BookingModel? data, string code)> GetByIdAsync(int id, CancellationToken ct)
	{
		var s = await _db.Bookings
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == id, ct);

		if (s is null) return (false, null, Codes.NotFound);
		return (true, MapToModel(s), Codes.Ok);
	}

	public async Task<(bool ok, List<BookingRow> data, string code)> GetByServiceIdAsync(
	int serviceId,
	CancellationToken ct)
	{
		var items = await _db.Bookings
			.AsNoTracking()
			.Where(b => b.ServiceDetail!.ServiceId == serviceId)
			.OrderBy(b => b.Id)
			.Select(b => new BookingRow
			{
				Id = b.Id,
				EmployeeName = b.Employee != null
					? (b.Employee.FirstName + " " + b.Employee.LastName)
					: string.Empty,
				ServiceDetailName = b.ServiceDetail != null
					? b.ServiceDetail.Name
					: string.Empty,

				StartDateTime = b.StartDatetime,
				EndDateTime = b.EndDatetime
			})
			.ToListAsync(ct);

		return (true, items, Codes.Ok);
	}

	public async Task<(bool ok, int id, string code, string? message)> CreateAsync(BookingUpsertDto dto, int employeeId, CancellationToken ct)
	{
		var start = ToLocal(dto.startDatetime);
		var end = ToLocal(dto.endDatetime);

		// SQLite transactions take the write lock at BEGIN, so the overlap check and insert can't interleave.
		await using var tx = await _db.Database.BeginTransactionAsync(ct);

		var (valid, code, message) = await ValidateAsync(dto.serviceDetailId, employeeId, start, end, currentId: null, ct);
		if (!valid)
		{
			return (false, 0, code, message);
		}

		var s = new Booking
		{
			ServiceDetailId = dto.serviceDetailId,
			EmployeeId = employeeId,
			StartDatetime = start,
			EndDatetime = end,
			CreatedAt = DateTime.Now
		};

		_db.Bookings.Add(s);
		await _db.SaveChangesAsync(ct);
		await tx.CommitAsync(ct);
		return (true, s.Id, Codes.Ok, null);
	}

	public async Task<(bool ok, string code, string? message)> UpdateAsync(int id, BookingUpsertDto dto, int employeeId, bool isAdmin, CancellationToken ct)
	{
		var start = ToLocal(dto.startDatetime);
		var end = ToLocal(dto.endDatetime);

		await using var tx = await _db.Database.BeginTransactionAsync(ct);

		var s = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == id, ct);
		if (s is null)
		{
			return (false, Codes.NotFound, "Booking not found.");
		}
		if (!isAdmin && s.EmployeeId != employeeId)
		{
			return (false, Codes.Forbidden, "You can only change your own bookings.");
		}

		// The booking stays with its original employee, even when an admin edits it.
		var (valid, code, message) = await ValidateAsync(dto.serviceDetailId, s.EmployeeId, start, end, currentId: id, ct);
		if (!valid) 
		{
			return (false, code, message);
		}

		s.ServiceDetailId = dto.serviceDetailId;
		s.StartDatetime = start;
		s.EndDatetime = end;

		await _db.SaveChangesAsync(ct);
		await tx.CommitAsync(ct);
		return (true, Codes.Ok, null);
	}

	public async Task<(bool ok, string code, string? message)> DeleteAsync(int id, int employeeId, bool isAdmin, CancellationToken ct)
	{
		var s = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == id, ct);
		if (s is null)
		{
			return (false, Codes.NotFound, "Booking not found.");
		}
		if (!isAdmin && s.EmployeeId != employeeId)
		{
			return (false, Codes.Forbidden, "You can only delete your own bookings.");
		}

		_db.Bookings.Remove(s);
		await _db.SaveChangesAsync(ct);
		return (true, Codes.Ok, null);
	}

	private async Task<(bool ok, string code, string? message)> ValidateAsync(
		int serviceDetailId, int employeeId, DateTime start, DateTime end, int? currentId, CancellationToken ct)
	{
		var openTime = TimeSpan.FromHours(_hours.OpenHour);
		var closeTime = TimeSpan.FromHours(_hours.CloseHour);

		if (end <= start)
		{
			return (false, Codes.BookingInvalid, "The end time must be after the start time.");
		}
		if (start < DateTime.Now)
		{
			return (false, Codes.BookingInvalid, "Bookings can't start in the past.");
		}
		if (start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
		{
			return (false, Codes.BookingInvalid, "Bookings are only available Monday to Friday.");
		}

		// The end may be exactly the closing time (e.g. 16:45–17:00), but not on another day.
		if (end.Date != start.Date || start.TimeOfDay < openTime || end.TimeOfDay > closeTime)
		{
			return (false, Codes.BookingInvalid, $"Bookings must be between {openTime:hh\\:mm} and {closeTime:hh\\:mm} on the same day.");
		}

		var detail = await _db.ServiceDetails
			.AsNoTracking()
			.Include(d => d.Service)
			.FirstOrDefaultAsync(d => d.Id == serviceDetailId, ct);

		if (detail is null)
		{
			return (false, Codes.BookingInvalid, "The selected service detail doesn't exist.");
		}

		var overlaps = await _db.Bookings.AnyAsync(b =>
			b.ServiceDetailId == serviceDetailId &&
			b.Id != currentId &&
			b.StartDatetime < end &&
			b.EndDatetime > start, ct);

		if (overlaps)
		{
			return (false, Codes.BookingConflict, "This time slot is already booked.");
		}

		var maxPerWeek = detail.Service?.MaxBookingsPerWeek;
		if (maxPerWeek is > 0)
		{
			var weekStart = start.Date.AddDays(-(((int)start.DayOfWeek + 6) % 7));
			var weekEnd = weekStart.AddDays(7);

			var bookingsThisWeek = await _db.Bookings.CountAsync(b =>
				b.EmployeeId == employeeId &&
				b.Id != currentId &&
				b.ServiceDetail!.ServiceId == detail.ServiceId &&
				b.StartDatetime >= weekStart &&
				b.StartDatetime < weekEnd, ct);
			if (bookingsThisWeek >= maxPerWeek)
			{
				return (false, Codes.BookingLimitReached, $"You've reached the limit of {maxPerWeek} bookings per week for {detail.Service!.Name}.");
			}
		}

		return (true, Codes.Ok, null);
	}

	private static DateTime ToLocal(DateTime value)
	{
		return value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
	}

	public async Task<(bool ok, List<AvailabilityTimes> data, string? message)> GetAvailability(Availability dto, CancellationToken ct)
	{
		string initialHour = "08:00";
		string finalHour = "15:00";
		var availabilityList = new List<AvailabilityTimes>();

		var date = DateTime.Parse(dto.date).Date;
		DateTime startTime = date.Add(TimeSpan.Parse(initialHour));
		DateTime endTime = date.Add(TimeSpan.Parse(finalHour));

		var dayStart = date.Date;
		var dayEnd = dayStart.AddDays(1);

		var bookingsOnDate = await _db.Bookings
			.AsNoTracking()
			.Where(x =>
				x.ServiceDetailId == dto.id &&
				x.StartDatetime >= dayStart &&
				x.StartDatetime < dayEnd
			)
			.ToListAsync(ct);

		for (var time = startTime; time < endTime; time = time.AddMinutes(int.Parse(dto.frequency)))
		{
			var isAvailable = true;

			foreach (var b in bookingsOnDate)
			{
				if (b.StartDatetime <= time && b.EndDatetime > time)
				{
					isAvailable = false;
					break;
				}
			}

			availabilityList.Add(new AvailabilityTimes(time.ToString("HH:mm"), isAvailable));
		}

		return (true, availabilityList, null);
	}

	private static BookingModel MapToModel(Booking s) =>
		new(
			s.Id,
			s.ServiceDetailId,
			s.EmployeeId,
			s.StartDatetime,
			s.EndDatetime,
			s.CreatedAt
		);
}