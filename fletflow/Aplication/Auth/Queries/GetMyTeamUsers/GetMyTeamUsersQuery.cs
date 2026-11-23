using fletflow.Application.DTOs.Users;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Application.Shared;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Application.Users.Queries
{
    public class GetMyTeamUsersQuery
    {
        private readonly AppDbContext _context;

        public GetMyTeamUsersQuery(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserDto>> Execute(Guid ownerUserId)
        {
            var users = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(u => u.OwnerUserId == ownerUserId)     // 👈 filtro por dueño/equipo
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    DriverId = _context.Drivers
                        .Where(d => d.UserId == u.Id)
                        .Select(d => (Guid?)d.Id)
                        .FirstOrDefault(),
                    Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return users;
        }

        public async Task<PagedResult<UserDto>> ExecutePage(
            Guid ownerUserId,
            int page = 1,
            int pageSize = PaginationLimits.DefaultPageSize,
            string? search = null,
            string? role = null,
            bool? isActive = null)
        {
            if (!PaginationLimits.IsValid(page, pageSize))
                throw new ArgumentOutOfRangeException(nameof(page), "La página debe ser >= 1 y pageSize debe estar entre 1 y 100.");

            IQueryable<UserEntity> usersQuery = _context.Users
                .AsNoTracking()
                .Where(user => user.OwnerUserId == ownerUserId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                usersQuery = usersQuery.Where(user =>
                    user.Username.Contains(term) ||
                    user.Email.Contains(term) ||
                    (user.FirstName ?? string.Empty).Contains(term) ||
                    (user.LastName ?? string.Empty).Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                var roleName = role.Trim();
                usersQuery = usersQuery.Where(user => user.UserRoles.Any(userRole =>
                    userRole.Role.Name == roleName));
            }

            if (isActive.HasValue)
                usersQuery = usersQuery.Where(user => user.IsActive == isActive.Value);

            var totalCount = await usersQuery.CountAsync();
            var items = await usersQuery
                .OrderBy(user => user.LastName)
                .ThenBy(user => user.FirstName)
                .ThenBy(user => user.Username)
                .ThenBy(user => user.Id)
                .Skip(PaginationLimits.GetOffset(page, pageSize))
                .Take(pageSize)
                .Select(user => new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    DriverId = _context.Drivers
                        .Where(driver => driver.UserId == user.Id)
                        .Select(driver => (Guid?)driver.Id)
                        .FirstOrDefault(),
                    Roles = user.UserRoles.Select(userRole => userRole.Role.Name).ToList(),
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<UserDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }
    }
}
