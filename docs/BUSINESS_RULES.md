# Reglas de negocio

## Usuarios

- Los roles reales son los definidos por backend; las etiquetas del frontend no los reemplazan.
- El registro público no puede elegir roles privilegiados.
- Los usuarios y recursos deben pertenecer a un propietario/empresa.
- Crear usuarios de equipo persiste la cuenta y los datos de conductor antes del intento SMTP; si el correo falla, la API devuelve `invitationSent=false` para que la interfaz no afirme que se entregó.
- Un usuario con rol Driver recibe un único registro de conductor enlazado, con documento y teléfono proporcionados; los documentos duplicados se rechazan antes de crear usuario.

## Vehículos y conductores

- Estados de vehículo: disponible, en ruta y mantenimiento.
- Mantenimiento bloquea nuevas asignaciones.
- Un conductor puede tener cero o un vehículo asignado.
- No debe haber asignaciones contradictorias.

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
# Aislamiento por empresa

- La claim JWT `owner_id` representa al usuario raíz de la empresa; para cuentas miembro se deriva de `OwnerUserId`, para cuentas raíz de su propio `Id`.
- Vehículos, conductores, rutas y posiciones se consultan bajo filtros de propietario en EF Core.
- Las altas de recursos reciben propietario del contexto autenticado en `SaveChanges`; el cliente no puede elegirlo.
- Placas y documentos son únicos dentro de una empresa.
- La migración de datos históricos requiere resolver recursos sin relaciones que permitan inferir el propietario.
