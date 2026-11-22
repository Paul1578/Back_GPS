# Reglas de negocio

## Usuarios

- Los roles reales son los definidos por backend; las etiquetas del frontend no los reemplazan.
- El registro público crea la cuenta raíz `Admin` de una empresa nueva; el servidor asigna este rol fijo e ignora cualquier rol solicitado por el cliente.
- La cuenta raíz es propietaria de su tenant (`owner_id` igual a su propio ID); todos sus recursos quedan aislados de otras empresas.
- Los usuarios y recursos deben pertenecer a un propietario/empresa.
- Crear usuarios de equipo persiste la cuenta y los datos de conductor antes del intento SMTP; si el correo falla, la API devuelve `invitationSent=false` para que la interfaz no afirme que se entregó.
- Un usuario con rol Driver recibe un único registro de conductor enlazado, con documento y teléfono proporcionados; los documentos duplicados se rechazan antes de crear usuario.

## Vehículos y conductores

- Estados de vehículo: disponible, en ruta y mantenimiento.
- Mantenimiento bloquea nuevas asignaciones.
- Un conductor puede tener cero o un vehículo asignado.
- Un vehículo puede tener como máximo un conductor asignado. Para cambiarlo, se desasigna primero el conductor actual; las asignaciones concurrentes se serializan y una de ellas recibe conflicto.
- Crear o editar una ruta respeta la misma exclusividad: no puede asociar un vehículo que siga asignado a otro conductor. La operación responde conflicto sin alterar ruta ni asignaciones.

## Rutas

- Estados: pendiente, en progreso, completada y cancelada.
- Una ruta puede contener múltiples paradas.
- Cada parada puede ser recogida, entrega o ambas.
- El orden de las paradas debe conservarse.
- Las paradas necesitan estado y evidencia para cerrar entregas de forma auditable.
- Transiciones de parada: pendiente→en curso/omitida; en curso→completada/fallida; fallida→en curso. Una parada completada u omitida es terminal.
- Toda parada operativa completada u omitida debe conservar al menos una evidencia; sólo se puede cerrar una ruta cuando todas las paradas operativas están cerradas y documentadas.
- Crear o editar una ruta no permite al cliente inyectar estados previos de paradas: todas comienzan pendientes.
- El conductor asignado sólo puede mutar las paradas de su ruta.
- Al iniciar una ruta, el vehículo pasa a en ruta; al cerrar, vuelve a disponible si no existe otra operación activa.
- Pausar una ruta la devuelve a pendiente y libera el vehículo si ninguna otra ruta sigue en curso; reanudar exige que el vehículo esté disponible.
- Un vehículo sólo puede tener una ruta activa en progreso a la vez; cambios concurrentes de ruta se serializan y el perdedor recibe HTTP 409.
- El inicio exige Driver activo. La baja del Driver, la asignación manual y el cambio de estado del vehículo se serializan con inicio/edición para evitar estados incompatibles.
- Si el Driver está vinculado a una cuenta User, esa cuenta también debe estar activa para iniciar la ruta. La baja de la cuenta se serializa con el inicio usando los locks de vehículo/Driver/cuenta; no se puede desactivar mientras tenga ruta en progreso. Si la cuenta ya está inactiva, el inicio devuelve 409 y no cambia la ruta ni el vehículo.
- `CreatedAtUtc` registra en UTC cuándo se crea la ruta y la API lo serializa con zona UTC. Es nullable sólo para rutas anteriores a la migración; la edición no altera la fecha original. Las consultas recientes ordenan por creación y usan cierre/inicio planificado para ordenar los registros históricos sin fecha.
# Aislamiento por empresa

- La claim JWT `owner_id` representa al usuario raíz de la empresa; para cuentas miembro se deriva de `OwnerUserId`, para cuentas raíz de su propio `Id`.
- Vehículos, conductores, rutas y posiciones se consultan bajo filtros de propietario en EF Core.
- Las altas de recursos reciben propietario del contexto autenticado en `SaveChanges`; el cliente no puede elegirlo.
- Placas y documentos son únicos dentro de una empresa.
- La migración de datos históricos requiere resolver recursos sin relaciones que permitan inferir el propietario.

## Protecciones operativas añadidas — 2026-11-21

- Una ruta no se puede dar de baja mientras está en progreso; primero debe completarse o cancelarse. API responde 409.
- No se puede desasignar el vehículo de un Driver mientras tiene una ruta en progreso.
- No se puede dar de baja un vehículo asignado a un Driver activo o vinculado a ruta pendiente/en curso. El inicio de una ruta también valida que Vehicle siga activo.
- Un vehículo dado de baja se conserva para historial y puede reactivarse por `POST /api/Vehicles/{id}/reactivate`; reactivación bloquea vehículos con ruta en progreso. El estado operativo `OnRoute` huérfano vuelve a `Available` sólo al reactivar sin ruta en curso.
- Las actualizaciones de estado/evidencia de paradas adquieren el lock de Route y vuelven a leer el JSON de puntos antes de guardar para evitar perder cambios concurrentes.
- La baja de cuenta revoca refresh tokens y JWT no permite solicitudes nuevas de cuentas inactivas.

## Matriz HTTP de rol verificada — 2026-11-21

- Admin: smokes de CRUD, tenant e invariantes.
- Manager: GET de Vehicles, Drivers y Routes devuelve 200.
- Logistics: GET de Routes devuelve 200; Vehicles y Drivers devuelven 403.
- User: Vehicles/Routes y creación de Vehicle devuelven 403.
- Driver: Playwright autenticado verifica alcance de ruta propia y denegaciones de recursos administrativos.

## Cambio de rol y rutas activas

- Cambiar una cuenta desde Driver a otro rol desactiva y desasigna su perfil Driver, salvo que exista ruta en progreso; ese conflicto devuelve 409.
- El cambio de rol y el inicio de ruta comparten lock de la asignación para preservar el estado si llegan simultáneamente.
- Al cambiar el rol, access tokens con el rol anterior dejan de autenticar y refresh tokens previos se revocan.
- Verificación en MySQL: la carrera sólo puede dejar ruta en progreso con Driver activo y Vehicle OnRoute, o ruta pendiente con Driver inactivo, rol Logistics y Vehicle Available.

## Sesiones después de cambiar contraseña — 2026-11-21

Cambiar, restablecer o activar una contraseña incrementa `User.TokenVersion`, revoca refresh tokens y hace que JWT previos sean rechazados en la siguiente solicitud autenticada. El usuario debe volver a iniciar sesión. Se persiste en `Users.TokenVersion`, default 0 para conservar compatibilidad de registros existentes; la migración aditiva se aplica antes de usar la versión nueva de la API.

- El alta administrativa de una cuenta Driver, su entidad Driver y token de activación se persisten en una única llamada de guardado; si el índice único del documento detecta una carrera, no queda usuario parcial y la API devuelve HTTP 409.

## Perfil de miembros — 2026-11-22

- Admin puede editar nombre, apellido y username de miembros de su tenant. Email no se modifica desde Gestión de Equipo.
- La cuenta raíz no se edita desde gestión del equipo; recursos de otra empresa responden 404.
- User y Driver enlazados mantienen nombres sincronizados en cada sentido dentro de una única operación EF.
- Al migrar, usuarios Driver existentes reciben nombres del perfil Driver sólo cuando los campos nuevos están vacíos.

## Sesiones y catálogo de roles

- Cada transición real de estado de usuario incrementa `TokenVersion`; desactivar también revoca refresh tokens. Reactivar no rehabilita access JWT emitidos previamente; el usuario debe iniciar sesión de nuevo.
- El catálogo de Roles es global y actualmente no tiene propietario ni rol de administración de plataforma. Admin tenant puede consultarlo, pero no crear ni eliminar roles (403).

## Sesiones y recuperación — 2026-11-22

- Rotar un refresh token es una operación de un solo ganador: la revocación del token actual y la inserción del sucesor ocurren condicionalmente y en una única transacción. Reutilizar el token anterior devuelve no autorizado.
- Cerrar todas las sesiones invalida refresh tokens y access JWT existentes mediante `TokenVersion`; el usuario puede volver a iniciar sesión después.
- Recuperación de contraseña mantiene el mismo mensaje público para cuentas existentes e inexistentes y no revela expiración. La disponibilidad SMTP no implica que se haya enviado un mensaje.
- `logout-all` y la rotación de refresh bloquean la fila del usuario dentro de transacciones MySQL. Así, si ambas operaciones compiten, el logout revoca cualquier sucesor producido antes de terminar y una rotación posterior ve revocado el token anterior.

## Recuperación de contraseña y entrega de correo — 2026-11-22

- La API devuelve contenido idéntico para correo conocido/desconocido y un mínimo de 250 ms de respuesta; el envío SMTP se realiza fuera del request en worker bounded.
- Error SMTP se registra como fallo y no se comunica entrega exitosa. La nota histórica de cola en memoria fue sustituida el 2026-11-22 por EmailOutbox cifrado y persistente; revisar la sección de recuperación de cuenta al cambiar esa política.

## Altas de miembros — límites y duplicados concurrentes (2026-11-22)

- Máximos de persistencia y API: Username 100, FirstName 100, LastName 100 y Email 200 caracteres; recortar espacios periféricos antes de guardar.
- Correo ya existente, incluida carrera concurrente protegida por índice único global, produce conflicto 409; una carrera debe persistir una sola cuenta y un único conjunto de UserRole/token de activación.
- Driver no puede consultar `/Routes/assignment-options`; Admin, Manager y Logistics tienen acceso según sus permisos operativos; User también recibe 403.

## Recuperación de cuenta y entrega de correo — 2026-11-22

- Solicitudes de recuperación conocidas e inexistentes conservan la misma respuesta pública. Para un usuario existente, las solicitudes se serializan por lock de su fila; se invalida cualquier token anterior y sólo queda un token activo.
- El token y el email cifrado de outbox se confirman juntos. La respuesta HTTP no afirma que el proveedor ya haya entregado el correo.
- El worker reintenta fallos y deja registro dead-letter después de 12 intentos. La entrega SMTP externa requiere configuración válida; el almacenamiento durable no equivale a entrega exactamente una vez.

### Consistencia de asignaciones activas

- Un Driver con una ruta activa Pending o InProgress conserva el vehículo relacionado a esa operación: no puede reasignarse a otro vehículo hasta que las rutas activas terminen o se cancelen.
- Antes de pasar a InProgress, la ruta, el Vehicle y la asignación actual del Driver deben referirse al mismo vehículo. La regla se verifica bajo los locks de asignación.
