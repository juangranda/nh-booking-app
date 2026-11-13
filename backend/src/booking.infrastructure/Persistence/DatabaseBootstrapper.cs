using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using booking.core.Constants;
using booking.core.Models;

namespace booking.infrastructure.Persistence;

public static class DatabaseBootstrapper
{
	// Password of the demo accounts. Only ever used when demo data is seeded.
	public const string DemoPassword = "P@ssw0rd!";
	private static readonly string[] DemoEmails =
	{
		"admin@demo.com", "sarah.johnson@company.com", "michael.chen@company.com", "emma.rodriguez@company.com",
	};

	public static async Task InitAsync(AppDbContext db, string schemaPath, bool seedDemoData,
		string? adminEmail, string? adminPassword, CancellationToken ct = default)
	{
		bool usersTableExists;
		await db.Database.OpenConnectionAsync(ct);
		try
		{
			await using var cmd = db.Database.GetDbConnection().CreateCommand();
			cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users';";
			var result = (long)(await cmd.ExecuteScalarAsync(ct) ?? 0L);
			usersTableExists = result > 0;
		}
		finally
		{
			await db.Database.CloseConnectionAsync();
		}

		if (!usersTableExists)
		{
			if (!File.Exists(schemaPath))
				throw new FileNotFoundException($"No se encuentra el schema en: {schemaPath}");

			var sql = await File.ReadAllTextAsync(schemaPath, ct);

			await using var tx = await db.Database.BeginTransactionAsync(ct);
			foreach (var stmt in SplitSqlStatements(sql))   // <-- ya no hay ambigüedad
			{
				if (string.IsNullOrWhiteSpace(stmt)) continue;
				await db.Database.ExecuteSqlRawAsync(stmt, ct);
			}
			await tx.CommitAsync(ct);
		}

		// Roles and permissions are needed in every environment.
		await SeedRolesAsync(db, ct);

		if (seedDemoData)
		{
			await SeedDemoDataAsync(db, ct);
		}

		if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
		{
			await EnsureAdminAsync(db, adminEmail.Trim(), adminPassword, ct);
		}
	}

	public static async Task<List<string>> FindDemoAccountsAsync(AppDbContext db, CancellationToken ct = default)
	{
		var hasher = new PasswordHasher<User>();
		var users = await db.Users.AsNoTracking().Where(u => DemoEmails.Contains(u.Email)).ToListAsync(ct);
		return users
			.Where(u => u.PasswordHash is not null &&
				hasher.VerifyHashedPassword(u, u.PasswordHash, DemoPassword) != PasswordVerificationResult.Failed)
			.Select(u => u.Email!)
			.ToList();
	}

	private static async Task EnsureAdminAsync(AppDbContext db, string email, string password, CancellationToken ct)
	{
		if (await db.Users.AnyAsync(u => u.Email == email, ct))
		{
			return;
		}
		if (password.Length < 12)
		{
			throw new InvalidOperationException("Seed:AdminPassword must be at least 12 characters.");
		}

		var user = new User { UserName = email.Split('@')[0], Email = email, EmailConfirmed = true };
		user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
		user.Roles.Add(new UserRole { User = user, RoleId = "r_admin" });
		db.Users.Add(user);
		db.Employees.Add(new Employee
		{
			User = user,
			FirstName = "Admin",
			LastName = "Admin",
			Email = email,
			CreatedAt = DateTime.Now
		});
		await db.SaveChangesAsync(ct);
	}

	private static async Task EnsureRoleAsync(AppDbContext db, string roleId, string name, CancellationToken ct)
	{
		if (!await db.Roles.AnyAsync(r => r.Id == roleId, ct))
			db.Roles.Add(new Role { Id = roleId, Name = name });
	}

	private static async Task EnsureUserRoleAsync(AppDbContext db, int userId, string roleId, CancellationToken ct)
	{
		if (!await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct))
			db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
	}

	private static async Task EnsureRoleClaimAsync(AppDbContext db, string roleId, string permission, CancellationToken ct)
	{
		if (!await db.RoleClaims.AnyAsync(rc =>
			rc.RoleId == roleId && rc.ClaimType == Permissions.ClaimType && rc.ClaimValue == permission, ct))
		{
			db.RoleClaims.Add(new RoleClaim { RoleId = roleId, ClaimType = Permissions.ClaimType, ClaimValue = permission });
		}
	}

	private static IEnumerable<string> SplitSqlStatements(string sql) =>
		sql.Replace("\r", "").Split(";\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	private static async Task SeedRolesAsync(AppDbContext db, CancellationToken ct)
	{
		await EnsureRoleAsync(db, roleId:"r_admin", name:"Admin", ct);
		await EnsureRoleAsync(db, roleId:"r_user", name:"User", ct);

		string[] adminPermissions =
		{
			Permissions.Bookings.Read, Permissions.Bookings.Create, Permissions.Bookings.Update, Permissions.Bookings.Delete,
			Permissions.Services.Read, Permissions.Services.Create, Permissions.Services.Update, Permissions.Services.Delete,
			Permissions.Offices.Read, Permissions.Offices.Create, Permissions.Offices.Update, Permissions.Offices.Delete,
		};

		string[] userPermissions =
		{
			Permissions.Bookings.Read, Permissions.Bookings.Create, Permissions.Bookings.Update, Permissions.Bookings.Delete,
			Permissions.Services.Read,
			Permissions.Offices.Read,
		};

		foreach (var permission in adminPermissions)
		{
			await EnsureRoleClaimAsync(db, "r_admin", permission, ct);
		}
		foreach (var permission in userPermissions)
		{
			await EnsureRoleClaimAsync(db, "r_user", permission, ct);
		}

		await db.SaveChangesAsync(ct);
	}

	// Demo accounts (all with DemoPassword), offices, services and a sample booking.
	private static async Task SeedDemoDataAsync(AppDbContext db, CancellationToken ct)
	{
		var existingEmployeesUsers = await db.Users
			.Where(u => u.Id >= 1 && u.Id <= 4)
			.ToListAsync(ct);

		if (!existingEmployeesUsers.Any())
		{
			var hasher = new PasswordHasher<User>();

			var users = new List<User>
			{
				new User
				{
					Id = 1,
					UserName = "admin",
					Email = "admin@demo.com",
					EmailConfirmed = true
				},
				new User
				{
					Id = 2,
					UserName = "sarah.johnson",
					Email = "sarah.johnson@company.com",
					EmailConfirmed = true
				},
				new User
				{
					Id = 3,
					UserName = "michael.chen",
					Email = "michael.chen@company.com",
					EmailConfirmed = true
				},
				new User
				{
					Id = 4,
					UserName = "emma.rodriguez",
					Email = "emma.rodriguez@company.com",
					EmailConfirmed = true
				}
			};

			foreach (var u in users)
				u.PasswordHash = hasher.HashPassword(u, DemoPassword);

			db.Users.AddRange(users);
		}

		await EnsureUserRoleAsync(db, 1, "r_admin", ct);
		foreach (var userId in new[] { 2, 3, 4 })
		{
			await EnsureUserRoleAsync(db, userId, "r_user", ct);
		}

		if (!await db.Offices.AnyAsync(ct))
		{
			db.Offices.AddRange(
				new Office { Id = 1, Name = "Main Office", Image = "https://placehold.co/150" },
				new Office { Id = 2, Name = "Remote Office", Image = "https://placehold.co/150" }
			);
		}

		var existingEmployees = await db.Employees.CountAsync(ct);

		if (existingEmployees == 0)
		{
			db.Employees.AddRange(
				new Employee
				{
					Id = 101,
					UserId = 1,
					FirstName = "Admin",
					LastName = "Admin",
					Email = "admin@demo.com",
					Phone = "+1-555-0124",
					CreatedAt = LocalDate("2023-06-20T14:30:00")
				},
				new Employee
				{
					Id = 102,
					UserId = 2,
					FirstName = "Sarah",
					LastName = "Johnson",
					Email = "sarah.johnson@company.com",
					Phone = "+1-555-0123",
					CreatedAt = LocalDate("2023-06-20T14:30:00")
				},
				new Employee
				{
					Id = 103,
					UserId = 3,
					FirstName = "Michael",
					LastName = "Chen",
					Email = "michael.chen@company.com",
					Phone = "+1-555-0124",
					CreatedAt = LocalDate("2023-08-10T10:15:00")
				},
				new Employee
				{
					Id = 104,
					UserId = 4,
					FirstName = "Emma",
					LastName = "Rodriguez",
					Email = "emma.rodriguez@company.com",
					Phone = "+1-555-0125",
					CreatedAt = LocalDate("2024-02-05T09:45:00")
				}
			);
		}

		if (!await db.Services.AnyAsync(ct))
		{
			var services = new List<Service>
			{
				new Service
				{
					Id = 1,
					OfficeId = 1,
					Name = "Room Booking",
					Description = "Book meeting rooms and workspaces",
					ImageUrl = "https://placehold.co/100",
					Quantity = 10,
					MaxBookingsPerWeek = 5,
					BookingPerTime = 2,
					MinMax = "15-45",
					ServiceDetails = new List<ServiceDetail>
					{
						new ServiceDetail
						{
							Id = 1,
							ServiceId = 1,
							Name = "Small Room",
							Description = "A small meeting room for up to 4 people",
							Capacity = 4,
							IconUrl = "https://placehold.co/50"
						},
						new ServiceDetail
						{
							Id = 2,
							ServiceId = 1,
							Name = "Large Room",
							Description = "A large meeting room for up to 10 people",
							Capacity = 10,
							IconUrl = "https://placehold.co/50"
						}
					}
				},
				new Service
				{
					Id = 2,
					OfficeId = 1,
					Name = "Equipment Rental",
					Description = "Rent office equipment and supplies",
					ImageUrl = "https://placehold.co/100",
					Quantity = 20,
					MaxBookingsPerWeek = 10,
					BookingPerTime = 5,
					MinMax = "30-120",
					ServiceDetails = new List<ServiceDetail>
					{
						new ServiceDetail
						{
							Id = 3,
							ServiceId = 2,
							Name = "Projector",
							Description = "High-quality projector for presentations",
							Capacity = 1,
							IconUrl = "https://placehold.co/50"
						},
						new ServiceDetail
						{
							Id = 4,
							ServiceId = 2,
							Name = "Laptop",
							Description = "Portable laptop for work on the go",
							Capacity = 1,
							IconUrl = "https://placehold.co/50"
						}
					}
				}
			};
			db.Services.AddRange(services);
		}

		await db.SaveChangesAsync(ct);

		var employee = await db.Employees.FirstOrDefaultAsync(e => e.Email == "sarah.johnson@company.com", ct);
		var serviceDetail = await db.ServiceDetails.FirstOrDefaultAsync(sd => sd.Name == "Small Room", ct);

		if (employee is not null && serviceDetail is not null && !await db.Bookings.AnyAsync(ct))
		{
			var day = NextWeekday(from:DateTime.Today);
			db.Bookings.Add(new Booking
			{
				ServiceDetailId = serviceDetail.Id,
				EmployeeId = employee.Id,
				StartDatetime = day.AddHours(10),
				EndDatetime = day.AddHours(10).AddMinutes(45),
				CreatedAt = DateTime.Now
			});
			await db.SaveChangesAsync(ct);
		}
	}

	private static DateTime NextWeekday(DateTime from)
	{
		var day = from.AddDays(1);
		while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
		{
			day = day.AddDays(1);
		}
		return day;
	}

	private static DateTime LocalDate(string value)
	{
		return DateTime.ParseExact(value, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
	}
}