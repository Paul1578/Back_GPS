# Arquitectura backend

La API mantiene una arquitectura por capas dentro de un proyecto .NET:

`Controller → Command/Query → Domain → Repository/UnitOfWork → EF Core/MySQL`

JWT autentica la solicitud. La claim firmada `owner_id` define el tenant; filtros EF globales aíslan vehículos, conductores, rutas y posiciones, y las altas sellan el propietario desde la solicitud autenticada.

Las rutas almacenan puntos ordenados como JSON en `Routes.PointsJson`. Cada punto mantiene operación (`None`, `Pickup`, `Delivery`, `PickupAndDelivery`), estado y evidencias. Las mutaciones por parada validan el estado de la ruta y se vuelven a persistir en el JSON. `RoutePositions` guarda posiciones GPS históricas enlazadas con la ruta.

`POST /api/Routes` y `PUT /api/Routes/{id}` reciben `points` con origen, paradas intermedias y destino en ese orden; también reciben `origin` y `destination` explícitos para compatibilidad. El mapper de frontend conserva los extremos y excluye sólo esos dos al mostrar la lista intermedia. El estado de una parada enviado por el cliente al crear/editar se ignora y se inicia como `Pending`.

El conductor cambia estado con `PUT /api/Routes/{id}/points/{index}/status` y adjunta notas/fotos con `POST /api/Routes/{id}/points/{index}/evidence`. Completar una operación requiere evidencia y todos los puntos operativos deben quedar completados u omitidos para cerrar la ruta. La evidencia de imagen se conserva como data URL en el JSON (JPEG/PNG/WebP, máximo 4 MB); para cargas de producción a escala se recomienda mover binarios a object storage.

## Concurrencia de operaciones y revocación de sesión — 2026-11-21

- `UsersController` delega la baja de estado a `UpdateUserCommand`, que persiste `IsActive=false` y revoca refresh tokens mediante el mismo `AppDbContext`/unit of work.
- El evento de validación JWT consulta el estado actual de la cuenta en cada autenticación de bearer. `AuthService.RefreshAsync` rechaza cuentas inactivas antes de rotar tokens.
- Mutaciones de puntos de ruta (`RoutesController`) toman lock de asignación/ruta usando `IUnitOfWork.LockRouteAssignmentAsync`, releen `RouteE` y guardan la colección completa serializada. Las colisiones operativas siguen respondiendo 409.
- Las operaciones de baja de Vehicle/Route y desasignación de Driver comparten transacciones/locks antes de validar rutas/asignaciones.
- `ChangeUserRoleCommand` bloquea la asignación Driver/Vehicle al cambiar el rol; `UserRepository.GetByIdAsync` usa `AsNoTracking` para que la relectura posterior al lock refleje el estado ya confirmado por una solicitud concurrente.
- `JwtBearerEvents.OnTokenValidated` compara el único rol persistido con el claim de rol firmado. El cambio de rol también revoca refresh tokens en la misma transacción de persistencia.

## Perfil de usuario de equipo — 2026-11-22

PUT /api/Users/{id}/profile valida rol, propietario, raíz, campos y longitudes en UsersController, y ejecuta UpdateUserProfileCommand para cambiar User y Driver enlazado en el mismo AppDbContext/commit. El correo no forma parte del contrato. Al actualizar un Driver, UpdateDriverCommand sincroniza el User vinculado sólo si ambos registros comparten OwnerUserId.
