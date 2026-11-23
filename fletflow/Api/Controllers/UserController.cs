using System;
using System.Linq;
using System.Security.Claims;
using fletflow.Application.Users.Queries;
using fletflow.Application.Users.Commands;
using fletflow.Application.Auth.Commands;
using fletflow.Api.Contracts.Users;
using fletflow.Api.Security;
using fletflow.Domain.Fleet.Entities;
using fletflow.Infrastructure.Services;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Application.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")] // "Gerente" en el front
    public class UsersController : ControllerBase
    {
        private readonly UpdateUserCommand _updateUserCommand;
        private readonly UpdateUserProfileCommand _updateUserProfileCommand;
        private readonly ChangeUserRoleCommand _changeUserRoleCommand;
        private readonly RegisterUserCommand _registerUserCommand;
        private readonly GetMyTeamUsersQuery _getMyTeamUsersQuery;
        private readonly PasswordResetTokenFactory _passwordResetTokenFactory;
        private readonly IEmailSender _emailSender;
        private readonly EmailSettings _emailSettings;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            GetMyTeamUsersQuery getMyTeamUsersQuery,
            UpdateUserCommand updateUserCommand,
            UpdateUserProfileCommand updateUserProfileCommand,
            ChangeUserRoleCommand changeUserRoleCommand,
            RegisterUserCommand registerUserCommand,
            PasswordResetTokenFactory passwordResetTokenFactory,
            IEmailSender emailSender,
            IOptions<EmailSettings> emailOptions,
            IUnitOfWork unitOfWork,
            ILogger<UsersController> logger)
        {
            _updateUserCommand = updateUserCommand;
            _updateUserProfileCommand = updateUserProfileCommand;
            _changeUserRoleCommand = changeUserRoleCommand;
            _registerUserCommand = registerUserCommand;
            _getMyTeamUsersQuery = getMyTeamUsersQuery;
            _passwordResetTokenFactory = passwordResetTokenFactory;
            _emailSender = emailSender;
            _emailSettings = emailOptions.Value;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        private Guid GetCurrentOwnerId()
        {
            var claim = User.FindFirstValue("owner_id");

            if (claim == null || !Guid.TryParse(claim, out var userId))
                throw new InvalidOperationException("No se pudo resolver la empresa del usuario autenticado.");

            return userId;
        }

        private bool IsCurrentAccountRoot()
        {
            var currentUserClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                   User.FindFirstValue("nameid");
            return Guid.TryParse(currentUserClaim, out var currentUserId) &&
                   currentUserId == GetCurrentOwnerId();
        }

        private async Task<bool> BelongsToCurrentOwner(Guid userId)
        {
            var ownerId = GetCurrentOwnerId();
            var target = await _unitOfWork.Users.GetByIdAsync(userId);
            return target is not null && (target.Id == ownerId || target.OwnerUserId == ownerId);
        }

        /// <summary>
        /// Obtiene los usuarios del equipo del usuario autenticado.
        /// </summary>
        /// <response code="200">Listado de usuarios del equipo.</response>
        [HttpGet("my-team")]
        public async Task<IActionResult> GetMyTeam()
        {
            var ownerId = GetCurrentOwnerId();

            var users = await _getMyTeamUsersQuery.Execute(ownerId);

            return Ok(users);
        }

        [HttpGet("my-team/page")]
        public async Task<IActionResult> GetMyTeamPage(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = PaginationLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? role = null,
            [FromQuery] bool? isActive = null)
        {
            if (!PaginationLimits.IsValid(page, pageSize))
                return BadRequest("page debe ser >= 1 y pageSize debe estar entre 1 y 100.");

            var result = await _getMyTeamUsersQuery.ExecutePage(
                GetCurrentOwnerId(), page, pageSize, search, role, isActive);
            return Ok(result);
        }

        /// <summary>
        /// Obtiene todos los usuarios.
        /// </summary>
        /// <response code="200">Listado de usuarios.</response>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _getMyTeamUsersQuery.Execute(GetCurrentOwnerId());
            return Ok(users);
        }

        /// <summary>
        /// Cambia el estado de un usuario.
        /// </summary>
        /// <param name="id">ID del usuario.</param>
        /// <param name="isActive">Nuevo estado.</param>
        /// <response code="200">Estado actualizado correctamente.</response>
        /// <response code="404">Usuario no encontrado.</response>
        [HttpPut("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] bool isActive)
        {
            if (id == GetCurrentOwnerId())
                return BadRequest("No se puede desactivar al propietario de la empresa.");
            if (!await BelongsToCurrentOwner(id)) return NotFound();
            await _updateUserCommand.Execute(id, isActive);
            return Ok(new { Message = "Estado del usuario actualizado." });
        }

        [HttpPut("{id:guid}/profile")]
        public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] UpdateUserProfileRequest request)
        {
            if (id == GetCurrentOwnerId())
                return BadRequest("No se puede editar el perfil raíz desde la gestión del equipo.");
            if (!await BelongsToCurrentOwner(id)) return NotFound();

            var username = request.Username?.Trim();
            var firstName = request.FirstName?.Trim();
            var lastName = request.LastName?.Trim();
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName))
                return BadRequest("Usuario, nombres y apellidos son obligatorios.");
            if (username.Length > 100 || firstName.Length > 100 || lastName.Length > 100)
                return BadRequest("Usuario, nombres y apellidos no pueden exceder 100 caracteres.");

            await _updateUserProfileCommand.Execute(id, username, firstName, lastName);
            var updated = (await _getMyTeamUsersQuery.Execute(GetCurrentOwnerId()))
                .SingleOrDefault(user => user.Id == id);
            return updated is null ? NotFound() : Ok(updated);
        }

        [HttpPut("{id:guid}/role")]
        public async Task<IActionResult> ChangeRole(Guid id, [FromBody] ChangeUserRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RoleName))
                return BadRequest("El rol es obligatorio.");
            var roleName = request.RoleName.Trim();
            if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !IsCurrentAccountRoot())
                return Forbid();
            if (id == GetCurrentOwnerId())
                return BadRequest("No se puede cambiar el rol del propietario de la empresa.");
            if (!await BelongsToCurrentOwner(id)) return NotFound();
            await _changeUserRoleCommand.Execute(id, roleName);
            return Ok(new { message = "Rol actualizado", userId = id, role = roleName });
        }

        /// <summary>
        /// Cambia el rol único de un usuario. Se conserva la ruta antigua como alias.
        /// </summary>
        /// <param name="id">ID del usuario.</param>
        /// <param name="name">Nombre del rol.</param>
        /// <response code="200">Rol asignado correctamente.</response>
        /// <response code="404">Usuario o rol no encontrado.</response>
        [HttpPost("{id:guid}/roles/{name}")]
        public async Task<IActionResult> AssignRole(Guid id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("El rol es obligatorio.");
            var roleName = name.Trim();
            if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !IsCurrentAccountRoot())
                return Forbid();
            if (id == GetCurrentOwnerId())
                return BadRequest("No se pueden modificar los roles del propietario de la empresa.");
            if (!await BelongsToCurrentOwner(id)) return NotFound();
            await _changeUserRoleCommand.Execute(id, roleName);
            return Ok(new { message = "Rol actualizado", userId = id, role = roleName });
        }

        /// <summary>
        /// La retirada aislada de roles está deshabilitada para evitar cuentas sin rol.
        /// </summary>
        /// <param name="id">ID del usuario.</param>
        /// <param name="name">Nombre del rol.</param>
        /// <response code="200">Rol removido correctamente.</response>
        /// <response code="404">Usuario o rol no encontrado.</response>
        [HttpDelete("{id:guid}/roles/{name}")]
        public IActionResult RemoveRole(Guid id, string name)
        {
            return Conflict(new { message = "No se puede quitar un rol por separado. Usa PUT /api/Users/{id}/role para reemplazarlo." });
        }

        // POST /api/users
        /// <summary>
        /// Crea un usuario con rol y envía invitación por email.
        /// </summary>
        /// <param name="request">Datos del usuario.</param>
        /// <response code="200">Usuario creado correctamente.</response>
        /// <response code="400">Datos inválidos.</response>
        /// <response code="409">El documento de conductor ya pertenece a otro conductor de la empresa.</response>
        [HttpPost]
        public async Task<IActionResult> CreateUserWithRole([FromBody] CreateUserWithRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
                return BadRequest("El nombre de usuario es obligatorio.");

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest("El email es obligatorio.");

            if (request.Username.Trim().Length > 100)
                return BadRequest("El usuario no puede exceder 100 caracteres.");
            if (request.Email.Trim().Length > 200)
                return BadRequest("El correo no puede exceder 200 caracteres.");
            if ((request.FirstName?.Trim().Length ?? 0) > 100 || (request.LastName?.Trim().Length ?? 0) > 100)
                return BadRequest("Nombres y apellidos no pueden exceder 100 caracteres.");

            // El flujo de invitación establece una clave aleatoria temporal que el usuario
            // reemplaza al activar su cuenta. Nunca usar una contraseña fija o predecible.
            var temporaryPassword = string.IsNullOrWhiteSpace(request.Password)
                ? TemporaryPasswordGenerator.Generate()
                : request.Password;

            if (string.IsNullOrWhiteSpace(request.RoleName))
                return BadRequest("El rol es obligatorio.");
            var allowedRoles = new[] { "Admin", "Driver", "Manager", "User", "Logistics" };
            if (!allowedRoles.Contains(request.RoleName.Trim(), StringComparer.OrdinalIgnoreCase))
                return BadRequest("El rol solicitado no está permitido.");

            var ownerId = GetCurrentOwnerId();
            if (request.RoleName.Trim().Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
                !IsCurrentAccountRoot())
                return Forbid();
            var isDriver = request.RoleName.Trim().Equals("Driver", StringComparison.OrdinalIgnoreCase);
            if (isDriver && (string.IsNullOrWhiteSpace(request.DocumentNumber) ||
                             string.IsNullOrWhiteSpace(request.PhoneNumber)))
                return BadRequest("Documento y teléfono son obligatorios para crear un conductor.");
            if (isDriver && (request.DocumentNumber!.Trim().Length > 20 || request.PhoneNumber!.Trim().Length > 20))
                return BadRequest("Documento y teléfono no pueden exceder 20 caracteres.");
            if (isDriver)
            {
                var existingDriver = await _unitOfWork.Drivers.GetByDocumentAsync(request.DocumentNumber!.Trim());
                if (existingDriver is not null)
                    return Conflict(new { message = "Ya existe un conductor con ese documento en la empresa." });
            }

            var user = await _registerUserCommand.Execute(
                request.Username,
                request.Email,
                temporaryPassword,
                request.RoleName,
                ownerId,
                mustChangePassword: true,
                saveChanges: false,
                firstName: request.FirstName,
                lastName: request.LastName
            );

            // Auto-crear Driver si el rol es Driver
            Guid? driverId = null;
            if (isDriver)
            {
                var existingDriver = await _unitOfWork.Drivers.GetByUserIdAsync(user.Id);
                if (existingDriver is null)
                {
                    var driver = Driver.Create(
                        string.IsNullOrWhiteSpace(request.FirstName) ? user.Username : request.FirstName.Trim(),
                        string.IsNullOrWhiteSpace(request.LastName) ? "" : request.LastName.Trim(),
                        request.DocumentNumber!.Trim(),
                        request.PhoneNumber!.Trim(),
                        user.Id);
                    await _unitOfWork.Drivers.AddAsync(driver);
                    driverId = driver.Id;
                }
                else driverId = existingDriver.Id;
            }

            var (token, plainToken) = _passwordResetTokenFactory.Create(user.Id, _emailSettings.ResetTokenMinutes);
            await _unitOfWork.PasswordResetTokens.AddAsync(token);

            var activationBase = string.IsNullOrWhiteSpace(_emailSettings.ActivationUrl)
                ? _emailSettings.ResetPasswordUrl
                : _emailSettings.ActivationUrl;
            var encoded = Uri.EscapeDataString(plainToken);
            var activationLink = $"{activationBase}?token={encoded}";

            try
            {
                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateException)
            {
                // A competing request may pass the pre-check first. SaveChanges is
                // atomic for user, Driver and activation token; map a duplicate index
                // race to the same conflict as the normal pre-check path.
                if (await _unitOfWork.Users.GetByEmailAsync(request.Email.Trim()) is not null)
                    throw new fletflow.Domain.Fleet.Exceptions.BusinessConflictException(
                        "El correo ya fue registrado.");
                if (isDriver && await _unitOfWork.Drivers.GetByDocumentAsync(request.DocumentNumber!.Trim()) is not null)
                    throw new fletflow.Domain.Fleet.Exceptions.BusinessConflictException(
                        "Ya existe un conductor con ese documento en la empresa.");
                throw;
            }

            var invitationSent = true;
            try
            {
                await _emailSender.SendUserInvitationEmailAsync(
                    user.Email,
                    activationLink);
            }
            catch (Exception exception)
            {
                invitationSent = false;
                _logger.LogWarning(
                    "No se pudo enviar invitación para usuario {UserId}; tipo de fallo {FailureType}. La cuenta quedó guardada.",
                    user.Id,
                    exception.GetType().Name);
            }

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                Role = request.RoleName,
                user.OwnerUserId,
                DriverId = driverId,
                InvitationSent = invitationSent,
                Message = invitationSent
                    ? "Usuario creado y la invitación fue enviada."
                    : "Usuario creado, pero no se pudo enviar la invitación por correo."
            });
        }

    }
}
