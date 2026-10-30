using System.Data;
using Microsoft.AspNetCore.Identity;              // <-- para PasswordHasher<T>
using Microsoft.EntityFrameworkCore;
using booking.core.Constants;
using booking.core.Models;

namespace booking.infrastructure.Persistence;

public static class DatabaseBootstrapper
{
	public static async Task InitAsync(AppDbContext db, string infraRootPath, CancellationToken ct = default)
	{
		// 1) ¿Existe la tabla Users?
		bool hasUsersTable;
		await using (var conn = db.Database.GetDbConnection())
		{
			if (conn.State != ConnectionState.Open)
				await conn.OpenAsync(ct);

			await using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users';";
			var result = (long)(await cmd.ExecuteScalarAsync(ct) ?? 0L);
			hasUsersTable = result > 0;
		}

		// 2) Si NO existe, ejecutar schema.sql una sola vez
		if (!hasUsersTable)
		{
			var schemaPath = Path.Combine(infraRootPath, "Persistence", "Sql", "schema.sql");
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

		// 3) Seed idempotente
		await SeedAsync(db, ct);
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

	private static async Task SeedAsync(AppDbContext db, CancellationToken ct)
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
				u.PasswordHash = hasher.HashPassword(u, "P@ssw0rd!");

			db.Users.AddRange(users);
		}

		await EnsureRoleAsync(db, roleId:"r_admin", name:"Admin", ct);
		await EnsureRoleAsync(db, roleId:"r_user", name:"User", ct);

		await EnsureUserRoleAsync(db, 1, "r_admin", ct);
		foreach (var userId in new[] { 2, 3, 4 })
		{
			await EnsureUserRoleAsync(db, userId, "r_user", ct);
		}

		// Admins can do everything.
		string[] adminPermissions =
		{
			Permissions.Bookings.Read, Permissions.Bookings.Create, Permissions.Bookings.Update, Permissions.Bookings.Delete,
			Permissions.Services.Read, Permissions.Services.Create, Permissions.Services.Update, Permissions.Services.Delete,
			Permissions.Offices.Read, Permissions.Offices.Create, Permissions.Offices.Update, Permissions.Offices.Delete,
		};
		// Users can browse offices and services and manage bookings (limited to their own in BookingService).
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
					CreatedAt = DateTime.Parse("2023-06-20T14:30:00Z")
				},
				new Employee
				{
					Id = 102,
					UserId = 2,
					FirstName = "Sarah",
					LastName = "Johnson",
					Email = "sarah.johnson@company.com",
					Phone = "+1-555-0123",
					CreatedAt = DateTime.Parse("2023-06-20T14:30:00Z")
				},
				new Employee
				{
					Id = 103,
					UserId = 3,
					FirstName = "Michael",
					LastName = "Chen",
					Email = "michael.chen@company.com",
					Phone = "+1-555-0124",
					CreatedAt = DateTime.Parse("2023-08-10T10:15:00Z")
				},
				new Employee
				{
					Id = 104,
					UserId = 4,
					FirstName = "Emma",
					LastName = "Rodriguez",
					Email = "emma.rodriguez@company.com",
					Phone = "+1-555-0125",
					CreatedAt = DateTime.Parse("2024-02-05T09:45:00Z")
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

		var employee = await db.Employees.FirstOrDefaultAsync(e => e.Email == "sarah.johnson@company.com", ct);
		var serviceDetail = await db.ServiceDetails.FirstOrDefaultAsync(sd => sd.Name == "Small Room", ct);

		if (employee is not null && serviceDetail is not null)
		{
			if (!await db.Bookings.AnyAsync(ct))
			{
				db.Bookings.Add(new Booking
				{
					ServiceDetailId = serviceDetail.Id,
					EmployeeId = employee.Id,
					StartDatetime = DateTime.Parse("2024-10-18T10:00:00Z"),
					EndDatetime = DateTime.Parse("2024-10-18T10:45:00Z"),
					CreatedAt = DateTime.Parse("2024-10-15T16:45:00Z")
				});
			}
		}

		await db.SaveChangesAsync(ct);
	}
}