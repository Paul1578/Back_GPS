# Changelog

## 2026-11-21 — Rate limit Development y prueba E2E extendida

- Loopback en Development se exime del rate limit para que QA local no bloquee flujos válidos; fuera de Development se conserva 60/min por IP.
- xUnit añadió pruebas para el bypass de Development y el límite de Production; suite Release queda 47/47.
- Chrome E2E frontend/API/MySQL pasó 5/5, incluyendo creación/edición/baja/reactivación de Driver, transición Driver→Logistics→Driver, cancelación y consulta de Historial de ruta en 390×844.
- Cleanup confirmó 22 migraciones, cero datos de fixture y cero sesiones refresh activas. SMTP sigue sin credenciales.

## 2026-11-21 — Consistencia de rutas y endurecimiento de configuración

- CreateRoute/UpdateRoute guardan asignación de vehículo del conductor y ruta en una sola unidad de trabajo; test de integración EF InMemory añadido.
- Se eliminó la contraseña `admin` de Docker Compose y `.env.example`; el backend exige variables de conexión y JWT válidos.
- Se rotaron MySQL root y clave JWT locales; Compose requiere valor desde `.env`, excluido de Git y con modo 0600.
- El contenedor MySQL se recreó preservando su volumen; 21 migraciones continúan aplicadas.
- Verificación actual: 43/43 tests, Release sin warnings, cero paquetes NuGet vulnerables; Swagger y endpoints locales respondieron correctamente.

## 2026-11-21 — Revisión independiente de seguridad y consistencia

- Se dejaron de registrar tokens de activación/restablecimiento; activar cuenta también revoca sesiones.
- Cada cuenta conserva un rol único, con índice MySQL y actualización atómica; asignación a vehículo en mantenimiento bloqueada.
- Se quitó el acceso legacy a rutas usando el ID de usuario; CORS aplica en todos los entornos y producción activa HTTPS/HSTS.
- Rutas conservan `StartedAt` y `EndedAt`; migración aditiva `RouteLifecycleAndSingleRole` aplicada (22 migraciones).
- Se añadió limpieza periódica al rate limiter local y prueba para ciclo de vida/vehículos en mantenimiento.
- Resultado: Release 45/45; NuGet y npm sin vulnerabilidades; frontend lint/build correctos.
- MySQL y phpMyAdmin ahora exponen sus puertos sólo en `127.0.0.1`; Compose recreó ambos servicios y preservó el volumen.
- Chrome E2E completó flujos representativos de Vehicle, Driver, User y Logistics; fixtures eliminados y resultado registrado en `docs/TESTING.md`.

## 2026-11-20

- Se instaló .NET SDK 8.0.428 localmente.
- Se verificó compilación del backend y Swagger.
- Se documentaron hallazgos de seguridad y arquitectura.
- Se confirmó que las paradas pueden ser recogida, entrega o ambas.
- Se bloqueó la asignación de roles desde el registro público.
- Se protegió el endpoint de prueba de correo.
- Se eliminaron tokens de recuperación de los logs.
- Se revocan refresh tokens después de resetear contraseña.
- Se restringió CORS a orígenes configurados.
- Se eliminó la contraseña fija del seeder admin.
- Se añadió operación y estado a cada punto de ruta, manteniendo compatibilidad con rutas existentes.
- Se persistió correctamente el nombre en la actualización de rutas.
- Se verificó la API corregida en `5047`: Swagger HTTP 200, rutas sin token HTTP 401 y prueba de correo sin token HTTP 401.
# Cambios 2026-11-20 — Estabilización de backend

- Registro público ya no puede autoasignarse roles privilegiados; endpoint de prueba SMTP requiere Admin.
- Se quitaron tokens de recuperación de logs, reset revoca refresh tokens y el seeder no incluye contraseña fija.
- CORS toma orígenes de configuración; secretos locales pasan por `.env`; errores 500 ocultan detalles internos.
- Rutas validan fechas, vehículo disponible, solapamiento y transiciones de estado; el nombre de actualización persiste.
- Paradas admiten operación y estado entrega/recogida/ambas; ubicaciones GPS validan rangos y limitan lecturas.
- Añadida suite xUnit con 25 pruebas; compilación/tests limpios después de alinear EF Relational.
- Se implementó aislamiento de recursos por `owner_id`, stamping del propietario, restricciones únicas por empresa y migración aditiva `TenantFleetOwnership`; tests de aislamiento y claim JWT incluidos (25/25). Migración aplicada tras asignar el vehículo legado al Admin raíz más antiguo con aprobación del usuario.
- E2E local sobre MySQL pasó: vehículo, conductor, ruta de recogida/entrega, GPS, estados, limpieza de datos y verificación IDOR cross-tenant.
- Pendientes de producto/seguridad están listados en `PROJECT_STATUS.md` y `SECURITY.md`.

## 2026-11-20 — Operación y evidencia por parada

- Se añadieron endpoints para actualizar el estado de una parada y guardar evidencia asociada a ella.
- Las rutas sólo se completan cuando todas sus operaciones están completadas u omitidas y cada una tiene evidencia; el cliente no puede crear paradas ya completadas.
- La vista Driver ahora muestra el tipo y estado de cada parada, permite iniciarla, omitirla, reintentarla, adjuntar notas/fotos y completar la ruta sólo cuando corresponde.
- El estado operativo de ruta usa el endpoint de cambio de estado, no el endpoint de edición; pausar libera el vehículo cuando no hay otra ruta activa.
- Se corrigió el mapeo de operaciones al editar una ruta y se conservan evidencias incluso en origen/destino operativos.
- Verificación: frontend lint/build correctos; backend 30/30; E2E HTTP/MySQL de recogida, entrega, evidencia, posición y cierre aprobado; registros QA eliminados.
- E2E adicional verificó pausa, disponibilidad de vehículo y reanudación; todos los recursos temporales se eliminaron.
- Gestión de equipo ahora crea el conductor una sola vez con sus datos reales; SMTP fallido deja cuenta/driver persistidos y responde `invitationSent=false`.
- Logs SMTP se redujeron para no incluir emails destinatarios, host SMTP ni cuerpos de correo.


## 2026-11-21 — Regresiones de aislamiento y policies

- EF InMemory prueba aislamiento de Vehicles, Drivers, Routes y RoutePositions entre dos tenants.
- Se añadieron asserts de roles para controllers Vehicles, Drivers y Users.
- Suite Release: 48/48; siguen pendientes las pruebas de matriz HTTP/MySQL cross-tenant automatizadas en CI.

## 2026-11-21 — IDOR validado con dos tenants reales

- E2E sobre API/MySQL confirma aislamiento y 404 en operaciones individuales de Vehicles, Drivers y Routes; bloquea asociar IDs ajenos en una nueva ruta.
- La ruta propietaria persiste GPS; el tenant cruzado sólo recibe colección vacía y no puede escribir posiciones.
- Chrome/API/MySQL: 8/8; limpieza QA verificada en cero.

## 2026-11-21 — E2E API/MySQL integrado al workflow backend

- CI crea servicio MySQL 8, inicia API con credenciales efímeras y ejecuta smoke HTTP cross-tenant de Vehicles, Drivers, Routes y GPS.
- El smoke limita destino/DB, prueba operaciones cruzadas y limpia sus fixtures exactos.
- DotNetEnv ahora preserva variables de CI sobre `.env` mediante `NoClobber`; test local equivalente pasó en DB aislada con 22 migraciones.
- El smoke valida HTTP 403 para el rol User en lectura/escritura de flota; comprobado en la DB descartable `fletflow_ci_validation_03`.
- Frontend CI ahora provisiona MySQL y checkout del backend feature, ejecuta la API y los ocho flujos Playwright autenticados. Runner generalizado a MySQL host y variables CI; repetición local 8/8.
- Primera ejecución remota backend pasó. Frontend encontró precheck prematuro de Next; se corrigió el runner para que Playwright inicie frontend en CI. Actions subieron a runtimes Node 24.
# 2026-11-21 — Estabilización de concurrencia de rutas

- Se serializan creación, edición, inicio y cambios relacionados de rutas por tenant usando transacciones MySQL y locks ordenados de ruta, vehículo y conductor. Conflictos operativos responden 409.
- Iniciar ruta valida Driver activo y que el vehículo no tenga ya otra ruta en progreso.
- Desactivación de Driver, asignación manual y cambios de estado de Vehicle comparten locks para evitar estados contradictorios con una ruta en progreso.
- Smoke MySQL real para create/update/start y start/deactivate simultáneos; los asserts revisan estados persistidos, no sólo códigos HTTP.
- Se fijó SQLitePCLRaw.lib.e_sqlite3 2.1.14 tras detectar CVE-2025-6965 en el artefacto de pruebas 2.1.10.

## 2026-11-21 — Protección de sesiones y ciclo de vida de flota

- Baja de usuario revoca refresh tokens; JWT y refresh rechazan cuentas inactivas.
- Inicio con vehículo inactivo rechazado; se protege la ruta en curso de bajas y la desasignación de conductor.
- Baja de vehículo valida relaciones operativas y se agrega endpoint de reactivación.
- Escrituras concurrentes de estado/evidencia de paradas se serializan por Route.
- Smokes HTTP/MySQL ampliados para sesión revocada, conflictos operativos, reactivación y pérdida de actualizaciones concurrentes.

## 2026-11-21 — Roles y locks de ruta

- Cambio de rol serializado con inicio de ruta y revocación de refresh tokens.
- JWT rechaza roles obsoletos; smokes MySQL validan access/refresh revocados y consistencia concurrente.
- Runner E2E normaliza URL local con/sin `/api`; suite frontend Chrome/API/MySQL: 14/14.
- Smoke HTTP amplía permisos de escritura para Manager, Logistics, User y Driver; se verificó persistencia/denegación en MySQL temporal y se eliminó la base.

## 2026-11-21 — Pruebas de recovery y CI

- Se agregaron pruebas de reset/activación y revocación de sesiones.
- Suite backend Release 54/54; Actions backend y frontend de los commits publicados finalizaron correctamente.

## 2026-11-21 — Invalidación inmediata de JWT de password anterior

- Se agregó versión de credenciales por usuario y validación del claim en cada request; cualquier cambio de password invalida inmediatamente access y refresh previos.
- Migración EF Core aditiva `TokenVersion` default 0; smoke HTTP verifica cambio de contraseña, tokens revocados y login con password nueva.

- Backend Actions `44983337062` aprobó migración y smoke HTTP/MySQL del cambio de contraseña; access/refresh anteriores rechazados inmediatamente.

- Pruebas unitarias cubren cambio de contraseña exitoso y rechazo sin efectos cuando falla la contraseña actual; suite local ampliada a 56/56.

- Backend Actions `44983763612` pasó 56/56 pruebas y los smokes HTTP/MySQL tenant, sesiones y concurrencia.

## 2026-11-21 — Invalidación de access JWT al cambiar roles

- Un cambio efectivo de rol incrementa `TokenVersion` junto al reemplazo de rol y revoca todos los refresh tokens; un JWT anterior no puede volver a ser válido aunque se restaure su rol.
- xUnit cubre cambio efectivo y no-op. El smoke tenant prueba Logistics→User→Logistics y verifica 401 persistente para access/refresh originales.
- El smoke HTTP acepta `MYSQL_PORT` para usar una instancia MySQL desechable sin tocar la DB local.
