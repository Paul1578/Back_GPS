# FletFlow Backend

## Propósito

API ASP.NET Core .NET 8 para una plataforma SaaS de gestión de flotas, rutas, recogidas, entregas y seguimiento.

## Arquitectura

- `Api`: controladores, contratos y middleware.
- `Aplication`: comandos, queries, DTOs y validación.
- `Domain`: entidades e invariantes.
- `Infrastructure`: EF Core/MySQL, repositorios, JWT y correo.

Mantener esta separación dentro del proyecto actual. No crear proyectos nuevos sin justificarlo.

## Reglas de negocio

- Una ruta puede tener múltiples paradas.
- Una parada puede ser recogida, entrega o ambas.
- Los recursos y datos deben aislarse por propietario/empresa.
- La autorización debe validarse en backend, no solo en frontend.
- Vehículos en mantenimiento no pueden asignarse a rutas nuevas.

## Trabajo seguro

- Trabajar siempre en una rama distinta de `main`.
- Leer `docs/SESSION_HANDOFF.md` y `docs/PROJECT_STATUS.md` antes de continuar.
- Nunca registrar tokens, contraseñas o secretos.
- Ejecutar `dotnet restore` y `dotnet build` después de cambios.
- Añadir pruebas para cada regla de seguridad o transición de estado.

## Correo de recuperación durable

- EmailDeliveryQueue debe seguir siendo scoped y escribir en el mismo AppDbContext que el token de reset; no ejecutar SaveChanges dentro del escritor.
- El worker hosted debe mantener SMTP fuera de la transacción y no registrar email, URL, token o payload. Proteger FLETFLOW_DATA_PROTECTION_KEYS_PATH con volumen persistente y acceso restringido.
- Mantener la cobertura HTTP/MySQL de requests concurrentes y leer docs/SECURITY.md antes de cambiar la política de reintentos/retención.
