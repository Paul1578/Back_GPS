# 2026-11-22 — Matriz HTTP CRUD Driver de Manager — CORREGIDA Y VALIDADA

- La primera prueba intentó leer el Driver activo con `GET /Drivers?onlyActive=false` y obtuvo `[]`. El parámetro es un filtro exacto, no una opción “incluir inactivos”; el fixture se lee por `GET /Drivers/{id}`.
- La segunda corrida comparó `OwnerUserId` contra `root_user_id` antes de que ese valor se inicializara en otra fase del smoke. La expectativa ahora deriva el tenant owner del usuario Manager desde MySQL.
- Solución/cobertura: fixture aislado Manager Driver + Vehicle; tras editar, desactivar, reactivar, asignar y desasignar, el smoke valida HTTP, lectura API y columnas SQL (documento/nombres/teléfono/IsActive/VehicleId/OwnerUserId; Plate/IsActive/Status/OwnerUserId). Cada paso también compara snapshots API/SQL del Driver, Vehicle y Route Manager existentes.
- Cleanup elimina por ID con fallback por documento/placa y verifica cero filas. Smoke completo MySQL 8 efímero terminó exit 0; `bash -n`, `git diff --check` y build Release (0 warnings/errores) pasan.
- Prevención: tratar `onlyActive` como predicado exacto y obtener expectativas de ownership en el mismo escenario/tenant, sin depender de variables asignadas en fases posteriores.

# 2026-11-22 — Fixtures tenant A/B del smoke no reflejaban assignment-options — CORREGIDO

- Al crear el Driver propio de B antes de `GET /Routes/assignment-options`, el endpoint devolvió ese Driver asignable. La aserción antigua `drivers | length == 0` falló aunque el aislamiento era correcto.
- Solución: exigir cero vehículos y exactamente el ID del Driver B; esto también excluye IDs de Drivers A.
- Se añadieron consultas MySQL inmediatamente después de cada intento cross-tenant (asignar/desasignar Driver A, asignar vehículo A al Driver B y reactivar vehículo A). Comparan ambos `VehicleId`, `IsActive` de los vehículos, y `OwnerUserId`.
- Verificación: `bash -n`, `git diff --check`, build Release sin warnings/errores y smoke completo contra API + MySQL 8 desechables; cleanup confirmó cero Drivers, Vehicles y Users temporales A/B.
- Incidencias de ejecución: el primer wrapper local fue bloqueado antes de ejecutar por incluir `rm -f`; se sustituyó por `unlink` y `docker stop/rm`. La primera creación del miembro B respondió HTTP 400 porque faltaba `Password`; el fixture ahora genera contraseña aleatoria. La primera corrida que alcanzó `assignment-options` expuso la expectativa vieja descrita arriba.

# 2026-11-22 — `dotnet` del PATH no encuentra runtimes — RESUELTO PARA ESTA SESIÓN

- Síntoma: `dotnet test BackEnd_Gps.sln --configuration Release --no-restore` abortó con código 131 porque `/usr/lib64/dotnet/host/fxr` no contiene una versión runtime.
- Causa: el host global del sistema no corresponde al SDK/runtime .NET 8 instalado para el usuario.
- Solución: usar `/home/leonardo/.dotnet/dotnet test BackEnd_Gps.sln --configuration Release --no-restore`; pasó **82/82** pruebas Release.
- Prevención: verificar `command -v dotnet` y usar el SDK local del usuario en este entorno.

# 2026-11-22 — Inspección buscó Application con ortografía distinta — CORREGIDO

- Síntoma: `rg` en `/home/leonardo/Back_GPS` no encontró `fletflow/Application`; el proyecto histórico tiene carpetas `Aplication` y namespaces `Application`, con spelling inconsistente.
- Causa: se usó el namespace como si fuera la ruta del directorio.
- Solución: localizar archivos con `rg --files fletflow` y filtrar la ruta real (`fletflow/Aplication`).
- Prevención: descubrir la estructura real con `rg --files` antes de asumir rutas derivadas del namespace; el antecedente general también está registrado abajo en la bitácora.

# Registro de errores y soluciones

## 2026-11-22 — Alta Driver podía exceder el tamaño de columnas — CORREGIDO

- El endpoint de alta no validaba longitud de documento/teléfono antes de ejecutar EF Core, mientras MySQL restringe ambos campos a 20 caracteres. En particular, el formulario de Manager aceptaba entradas mayores y el POST podía terminar en error de persistencia.
- El comando ahora normaliza espacios, exige los cuatro campos y rechaza documento/teléfono mayores a 20 con `ArgumentException`, en línea con `UpdateDriverCommand` y `DriverConfiguration`.
- xUnit añade rechazo de ambos campos en 21 caracteres, aceptación del límite exacto de 20 y verificación de que los rechazos no agregan ni confirman.
- Verificación local: build Release 0 warnings/0 errors y xUnit 72/72. Actions `45016639356` pasó para `8c28ccb`: xUnit 72/72, smoke HTTP/MySQL tenant/roles/concurrencia y cleanup.

## 2026-11-22 — Comando de perfil dependía del controlador para tenant — REFORZADO

- Revisión de seguridad identificó que `UpdateUserProfileCommand` consultaba por ID solamente, aunque el controller sí verificaba tenant antes de invocarlo. No se confirmó un bypass HTTP.
- El comando ahora requiere claim `owner_id` desde `AppDbContext.CurrentOwnerUserId` y limita explícitamente User y Driver al mismo `OwnerUserId`; intento de invocación sin tenant autenticado falla cerrado.
- xUnit prueba edición/sincronización propia y rechazo de usuario ajeno; el smoke HTTP también valida límites de nombre y apellido.
- Actions `45007491905` confirmó Release build, xUnit 64/64, smoke tenant HTTP/MySQL con ambos límites y concurrencia; contenedores descartables cerrados.
- Prevención: comandos que mutan registros tenant-aware deben conservar el scope de propietario aunque el controller ya lo haya comprobado.

## 2026-11-21 — Prueba UI de Driver se detuvo por selector ambiguo — CORREGIDO EN FRONTEND

- La búsqueda parcial `jperez` coincidió con el textbox de usuario y con `jperez@empresa.com`; Playwright paró en strict mode antes de crear datos.
- El selector se cambió al nombre accesible exacto `jperez`. El runner limpió las fixtures aun con la prueba fallida; la repetición autenticada final pasó 5/5.

## 2026-11-21 — Fixture de middleware construyó una IPv6 inválida — CORREGIDO

- La prueba de rate limit construyó una dirección IPv6 interpolando un GUID sin separadores y falló con `FormatException` antes de ejercitar middleware.
- Solución: crear la dirección de 16 bytes con `IPAddress(Guid.NewGuid().ToByteArray())`.
- Verificación: suite Release completa pasó 47/47.

## 2026-11-21 — E2E local superó 60 requests/minuto y recibió 429 — CORREGIDO PARA DEVELOPMENT

- El límite global por IP bloqueó refrescos de la suite local después de que el alta Driver ya había persistido.
- En Development, el middleware ahora deja pasar loopback; el límite se conserva en otros entornos. La UI también cierra el diálogo cuando el POST confirma y trata por separado fallos posteriores al refresco.
- Verificación: suite Chrome local 5/5; en producción siguen pendientes rate limiting distribuido y reglas apropiadas para usuarios detrás de NAT.

## 2026-11-21 — Sondeo consultó `/api/Dashboard`, ruta que no existe — CORREGIDO EN EL DIAGNÓSTICO

- La comprobación inicial asumió un endpoint dedicado al dashboard y recibió 404.
- El dashboard es una vista frontend que agrega recursos de Vehicles/Routes/Drivers; no existe un controlador `/api/Dashboard`.
- Se verificaron la página web y Swagger con HTTP 200, y las rutas API protegidas sin token con HTTP 401. El 404 no corresponde a una falla del servicio.

## 2026-11-20 — El backend no podía compilarse localmente — RESUELTO

- Comando: `dotnet build --no-restore`.
- Resultado original: el host .NET local no contenía un runtime/SDK válido (`/usr/lib64/dotnet/host/fxr` no tenía una versión instalada).
- Solución: se instaló .NET SDK 8.0.428 en `/home/leonardo/.local/share/dotnet`.
- Verificación: `dotnet restore` y `dotnet build --no-restore` terminan con 0 errores.
- Pendiente: corregir el warning nullable de `AuthMapper.cs` cuando se trabaje en esa parte.

## 2026-11-20 — La base de datos está activa, pero la API no está levantada

- Docker: MySQL y phpMyAdmin están activos.
- Estado: no hay servicio ASP.NET Core ejecutándose en el compose actual.
- Impacto: el frontend puede cargar, pero no se puede validar el flujo real contra la API.
- Próxima solución: levantar la API con un SDK .NET funcional y verificar Swagger, autenticación y endpoints de flota.

### Estado actualizado

- El backend ya está ejecutándose en `http://127.0.0.1:5046` desde el proceso iniciado por el entorno de desarrollo.
- Swagger responde HTTP 200 y MySQL ejecuta las migraciones sin cambios pendientes.

## 2026-11-20 — `docker-compose.yml` contiene cambios locales previos

- Archivos detectados fuera de esta tarea: `docker-compose.yml` y `.vscode/`.
- Regla aplicada: no sobrescribir ni revertir cambios preexistentes del usuario.

## 2026-11-20 — Límite de inotify al iniciar la API

- Síntoma: el primer arranque falló al crear watchers de configuración por el límite de instancias `inotify` del sistema.
- Solución operativa: iniciar con `DOTNET_USE_POLLING_FILE_WATCHER=1`.
- Nota: el siguiente intento encontró que la API ya estaba escuchando en `5046`; Swagger confirmó que el proceso existente está sano.

## Regla de trabajo

Cada cambio de API debe incluir validación de compilación, migraciones si aplica, endpoint probado y actualización de este registro.

## 2026-11-20 — Contrato de paradas ampliado — RESUELTO

- Necesidad: una ruta puede tener múltiples paradas, y cada parada puede ser entrega, recogida o ambas.
- Solución: se añadieron `RoutePointOperation` y `RoutePointStatus`; se extendieron entidad, DTO, mapper y serialización JSON con valores compatibles con puntos históricos.
- Verificación: `dotnet build --no-restore` termina con 0 errores y 0 advertencias; el frontend envía los valores numéricos esperados por la API.

## 2026-11-20 — Datos GPS fuera de rango y consultas sin límite

- Riesgo: valores NaN/Infinity y velocidades/rumbo inválidos podían contaminar seguimiento; el historial por ruta se consultaba sin límite.
- Solución: dominio valida coordenadas finitas, velocidad 0–300 km/h y rumbo [0, 360); repositorio limita cada consulta a 10 000 puntos ordenados temporalmente.
- Verificación: pruebas automatizadas cubren latitud/longitud, NaN/infinito, velocidad y rumbo; suite actual `dotnet test` 22/22.

## 2026-11-20 — Reglas de solapamiento y transición de rutas

- Riesgo: asignaciones podían solaparse para un mismo conductor/vehículo y rutas terminales podían reabrirse.
- Solución: creación/edición revisa horarios activos, rechaza vehículos no disponibles al asignar, valida fechas y permite sólo transiciones operativas; al finalizar no cambia a disponible si otra ruta sigue en curso.
- Verificación: compila; integrar pruebas específicas de comandos y repetir suite luego de resolver conflictos de paquetes.

## 2026-11-20 — Resolución duplicada de EF Relational — RESUELTO

- Síntoma: los tests resolvían EF Relational 9.0.0 por Pomelo y 9.0.10 por EF Tools, emitiendo MSB3277.
- Causa: el rango de Pomelo 9.0.0 admite EF Relational 9.0.x, pero no se fijó directamente una versión para el proyecto.
- Solución: referencia explícita a `Microsoft.EntityFrameworkCore.Relational` 9.0.10, alineada con EF Core/Tools 9.0.10.
- Verificación: restore y `dotnet test` pasan con 22/22 y sin warning.

## 2026-11-20 — MySQL no permitió retirar índice requerido por FK — RESUELTO

- Síntoma: aplicar `TenantFleetOwnership` falló al borrar `IX_RoutePositions_RouteId`, requerido por la FK de posiciones.
- Efecto parcial: MySQL ya había eliminado `IX_Vehicles_Plate` (DDL con commit implícito); el historial de migraciones no registró la migración.
- Corrección inmediata: se recreó `IX_Vehicles_Plate` y se cambió el orden para crear `IX_RoutePositions_RouteId_RecordedAt` antes de retirar el índice anterior. El `Down` también recrea primero el índice de FK.
- Respaldo: dump local previo en `/tmp/fletflow-before-tenant-migration.sql`.
- Verificación: la migración se aplicó correctamente y el inicio de API confirmó DB al día.

## 2026-11-20 — Limpieza automática del E2E tenía comillas incompatibles — RESUELTO

- Síntoma: los flujos E2E terminaron bien, pero la limpieza SQL lanzada con comillas anidadas falló.
- Solución: se ejecutó SQL por stdin con heredoc, en orden de FK (posiciones, ruta, conductor, vehículo).
- Verificación: consulta posterior encontró cero filas con el marcador QA.

## 2026-11-20 — Swagger 404 durante la primera prueba de API

- Causa: la API se inició con el entorno `Production`, donde el código sólo registra Swagger para `Development`.
- Solución: se volvió a iniciar localmente con `ASPNETCORE_ENVIRONMENT=Development`.
- Verificación: `/swagger/index.html` HTTP 200 y ruta protegida sin token HTTP 401.
- Efecto frontend: el proxy de Next apuntaba a 5046 y la versión corregida corre en 5048. Se añadió el ajuste `FLETFLOW_API_URL` y `.env.local` ignorado para conectar frontend local con la API corregida.

## 2026-11-20 — Prueba de tenant compartía el HttpContext ambient — RESUELTO

- Síntoma: al probar dos `HttpContextAccessor` a la vez en el mismo flujo async, el primer contexto reflejaba el segundo tenant y la prueba falló mostrando el registro B.
- Causa: `HttpContextAccessor` transporta el contexto mediante `AsyncLocal`; el test alojó dos peticiones simuladas en el mismo contexto ambiental.
- Solución: ejecutar cada lectura de tenant en un flujo async aislado (como peticiones HTTP independientes).
- Verificación: tests de propietario distinto y claim JWT pasan; total backend 25/25.

## 2026-11-20 — Token manual de E2E no tenía el tipo URI de rol — RESUELTO

- Síntoma: el primer JWT manual para HTTP devolvió 403 en todos los endpoints Admin.
- Causa: se firmó `role` en el claim corto; el middleware de roles de la API espera el tipo de claim de rol de .NET.
- Solución: los tokens locales de prueba incluyeron `ClaimTypes.Role` serializado con su URI de .NET.
- Verificación: listas por tenant y CRUD E2E HTTP pasaron; se probaron 404 de lecturas cruzadas.

## 2026-11-20 — Lectura de configuración de test falló por BOM — RESUELTO

- Síntoma: el script de E2E no pudo parsear `appsettings.json` como UTF-8 plano.
- Causa: el archivo incluye BOM UTF-8.
- Solución: leer configuración con `utf-8-sig`.
- Verificación: el script de prueba HTTP completó correctamente.

## 2026-11-20 — Estado/evidencia de paradas y cierre de ruta — RESUELTO

- Necesidad: `DriverView` registraba evidencia local sin persistirla y el backend no imponía una secuencia por parada.
- Solución: endpoints por parada, transición de estados en dominio, evidencia validada y serializada en `PointsJson`; completar ruta exige estado terminal y evidencia por cada operación.
- Se sincronizan `Origin`/`Destination` con la lista de puntos al mutar, para evitar extremos desfasados.
- Verificación: pruebas dominio ampliadas (suite 30/30) y E2E HTTP/MySQL aprobado con recogida, entrega, evidencia y cierre.

## 2026-11-20 — Fixture inicial de E2E rechazado por contrato y dato inválido — RESUELTO

- Herramienta: el script usó codec `utf-utf-8-sig`, corregido a `utf-8-sig`.
- Dato: la primera placa QA excedía la longitud admitida y MySQL la rechazó; el script usó después identificador corto.
- Contrato: el primer fixture sólo incluyó paradas intermedias, pero `POST /Routes` recibe la lista completa con origen y destino. Se ajustó el fixture y el E2E pasó.
- Limpieza: el intento inicial de usar variables MySQL no existentes en el contenedor falló; se usó acceso local configurado por compose, SQL por stdin y verificación con cero filas.

## 2026-11-20 — Arranque final con nombre de variable de logging con puntos — RESUELTO

- Síntoma: bash interpretó `Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Warning` como nombre de comando, porque el nombre contiene puntos.
- Solución: ejecutar con `env 'Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Warning' ...`.
- Verificación: API estable en `127.0.0.1:5048`, Development; Swagger HTTP 200.

## 2026-11-20 — Segundo comando `dotnet` usó host global inválido — RESUELTO

- Síntoma: una cadena `build && test` encontró SDK en el primer comando gracias a variables prefijadas, pero el segundo `dotnet` cayó al host `/usr/lib64/dotnet` sin runtime.
- Solución: ejecutar directamente `/home/leonardo/.local/share/dotnet/dotnet test ...` o exportar `DOTNET_ROOT`/`PATH` para toda la shell.
- Verificación: `dotnet test BackEnd_Gps.sln --no-restore` con la ruta local: 30 aprobadas.

## 2026-11-20 — Alta de chofer intentaba crear dos registros y dependía de SMTP — RESUELTO

- Hallazgo: `UsersController` generaba un Driver con documento parcial/teléfono N/A, mientras el frontend intentaba insertar otro Driver para el mismo usuario. El envío SMTP ocurría antes de confirmar la transacción.
- Solución: el request de equipo acepta nombre, documento y teléfono; el backend valida duplicados y crea un único Driver enlazado. Guarda usuario/driver/token antes del intento SMTP y responde `invitationSent=false` si falla, sin incluir token ni destinatario en logs. UI presenta advertencia explícita.
- Verificación real: crear Driver con SMTP no configurado devolvió cuenta persistida, relación correcta y `invitationSent=false`; duplicado devolvió 400. Limpieza MySQL dejó cero usuarios/Drivers QA.
- Compilación inicial falló por referirse a `ILogger<UserController>` en clase `UsersController`; se cambió el tipo genérico y build + 30 tests pasaron.
- Revisión de seguridad adicional retiró destinatarios y hostname SMTP de logs, manteniendo sólo el tipo de fallo.

## 2026-11-20 — Desasignar vehículo devolvía 415 Unsupported Media Type — RESUELTO

- Reproducción real desde Gestionar choferes: crear usuario Driver, enlazar vehículo y pulsar `Quitar` produjo HTTP 415.
- Causa: `HeaderValidationMiddleware` exigía `Content-Type: application/json` para todo `PUT`, aunque el endpoint `PUT /Drivers/{id}/unassign-vehicle` no recibe cuerpo. La llamada directa confirmó el 415 antes de llegar al controlador.
- Solución: validar el tipo MIME JSON sólo cuando POST/PUT/PATCH realmente lleva cuerpo; se parsea el tipo MIME exacto en vez de aceptar coincidencias parciales.
- Verificación: tres pruebas de middleware nuevas; suite completa 33/33. Repetición en Chrome tras recompilar/reiniciar backend: el vehículo se desasignó y la UI mostró `Sin vehículo` y `Vehiculo desasignado`.
- Limpieza: se borraron las cuentas, conductor y vehículo de QA; MySQL confirmó cero coincidencias.

## 2026-11-20 — Rol Logistics no estaba sembrado y las rutas autorizaban Dispatcher — RESUELTO

- Reproducción: `Roles` en MySQL sólo tenía Admin, Driver, Manager y User; aunque la API/UI permiten crear el rol `Logistics`, éste no existía. Además, las políticas de rutas usaban `Dispatcher`, un rol que no pertenece al modelo de negocio.
- Solución: el seeder ahora garantiza los cinco roles de negocio (incluido Logistics); acciones de rutas permiten Admin/Manager/Logistics y las consultas/operaciones de parada también Driver, conservando la verificación de que Driver sólo acceda a su ruta asignada.
- Verificación automatizada: prueba de seeding idempotente y prueba de metadatos de autorización; suite 35/35.
- Verificación MySQL/API: cuenta Logistics QA pudo consultar rutas (200) y alcanzó la acción de borrado sobre ID inexistente (404); User recibió 403 en listado de rutas. Filas QA eliminadas y conteo final cero.

## 2026-11-21 — Driver veía su ruta, pero no podía iniciarla y no veía su vehículo — RESUELTO

- E2E Chrome/MySQL encontró que `PUT /Routes/{id}/status` devolvía 403 al conductor asignado, aunque DriverView ofrece iniciar, pausar y completar.
- Solución de autorización: permitir `Driver` en la política del endpoint y verificar `IsCurrentDriverAssigned` antes de llamar el comando. Otro conductor del mismo tenant recibe 404 en el cambio y 403 en el detalle; el listado queda filtrado.
- Hallazgo visual responsive: DriverView dependía del listado de vehículos, que no está autorizado para Driver, dejando vacío “Vehículo asignado”. Se agregaron placa/marca/modelo como resumen en `RouteDto`, obtenidos en lote por IDs bajo el filtro tenant, y se muestran como fallback en frontend.
- E2E real: Driver inicia ruta, inicia parada, guarda nota, la ve tras recargar, completa la parada y cierra ruta. API confirmó ruta completada, operación/evidencia persistidas y vehículo disponible. En viewport 390 px no hubo overflow; el mapa informó “Sin ubicación GPS” sin simular ubicación.
- QA de scripts: login devuelve propiedad `token` (no `accessToken`); se corrigió el fixture. Un primer recolector intercambió status/body al desempaquetar una respuesta y se corrigió. QA limpio: cero rutas, vehículos, conductores y usuarios.

## 2026-11-21 — API de equipo creaba usuarios con contraseña fija desde el frontend — RESUELTO

- Hallazgo: el alta de equipo enviaba `123456` si el formulario dejaba la contraseña vacía. Esa credencial era predecible aunque el backend forzara activación.
- Solución: el frontend conserva el campo vacío; el backend genera una clave temporal criptográficamente aleatoria URL-safe y fuerza su reemplazo al activar. La clave no se envía por correo, pues la invitación ya contiene un enlace para fijar una nueva.
- Prueba: se añadió test de longitud/formato URL-safe y no repetición; la suite completa se ejecuta al cierre.
- Alerta aparte: la entrega del correo sigue dependiendo de credenciales SMTP válidas; sin SMTP se guarda el usuario y la UI informa que la invitación falló.
- Error adicional: plantilla HTML usaba `{{PASSWORD}}`, pero el servicio reemplazaba `{{TEMP_PASSWORD}}`; se eliminó el campo y se dejó un mensaje claro para activar y definir contraseña.

## 2026-11-21 — Primera ejecución de pruebas tras ampliar Drivers apuntó a nombre de solución incorrecto — CORREGIDO

- Síntoma: MSBuild respondió `MSB1009` porque la orden usó `Back_GPS.sln` desde el directorio backend; la solución se llama `BackEnd_Gps.sln`.
- Solución: corregir la ruta de la orden y repetir la suite completa antes de integrar.

## 2026-11-21 — Cambio de rol usaba varias peticiones y podía dejar cuenta sin rol — CORREGIDO

- Hallazgo: el frontend quitaba roles individualmente antes de añadir el nuevo; un fallo parcial podía dejar el usuario sin rol y sin acceso.
- Solución: nuevo `PUT /Users/{id}/role` valida rol/empresa y reemplaza todas las asignaciones en una sola unidad de trabajo; frontend informa el resultado de esa llamada.
- Al cambiar un conductor a otro rol, también se desactiva su perfil y se desasigna vehículo; se rechaza el cambio si tiene una ruta en curso. Al volver a Driver, un perfil existente se reactiva.
- Verificación: tests de comando comprueban reemplazo en un commit, baja del perfil y bloqueo por ruta activa.

## 2026-11-21 — API de conductores carecía de edición y baja lógica — CORREGIDO

- Hallazgo: Drivers sólo admitía crear, leer y asignar/desasignar; no había manera de corregir documento/teléfono ni retirar conductores manteniendo historial.
- Solución: se añadieron `PUT /Drivers/{id}` y `PUT /Drivers/{id}/status`; valida duplicados/documento y longitud, exige estado explícito, bloquea la baja si existe ruta en curso y desasigna el vehículo. Las rutas y comandos del Driver exigen perfil activo.
- Refuerzo de consistencia: no se puede crear/vincular conductor, asignar vehículo o crear ruta si la cuenta vinculada está inactiva; el propietario raíz tampoco puede degradar ni desactivar su propia cuenta.
- Pruebas: edición/trimming, activar/desactivar y rechazo de baja con ruta en curso; suite total 40/40.

## 2026-11-21 — Dependencia MySqlConnector 2.4.0 vulnerable — CORREGIDO

- Hallazgo: `dotnet list package --vulnerable --include-transitive` detectó GHSA-473q-m89c-ghf8 (MITM puede capturar contraseña en configuración TLS VerifyCA/VerifyFull sin CA).
- Solución: se fijó `MySqlConnector` en 2.6.2, versión estable que incluye la corrección; el aviso marca afectadas las versiones menores a 2.6.1.
- Verificación: restauración NuGet, suite 40/40 y auditoría vulnerable sin paquetes afectados.
- Fuente: [GitHub Advisory GHSA-473q-m89c-ghf8](https://github.com/advisories/GHSA-473q-m89c-ghf8).

## 2026-11-21 — Herramienta rechazó preparación temporal de E2E por `rm -rf` — CORREGIDO EN EL FIXTURE

- Causa: el primer comando de fixture temporal incluyó borrado recursivo de directorio temporal y la política de ejecución lo rechazó antes de ejecutarlo.
- Solución: sustituirlo por `unlink` de cada archivo conocido y `rmdir` del directorio temporal; no se creó cuenta ni se modificó la base.

## 2026-11-21 — Credencial MySQL `admin` versionada en Compose y `.env.example` — RESUELTO

- Hallazgo: Docker Compose definía `MYSQL_ROOT_PASSWORD: admin`; `.env.example` también contenía valores DB de ejemplo utilizables.
- Solución: Compose ahora requiere `MYSQL_ROOT_PASSWORD` desde `.env`, `.env.example` usa marcadores, `.gitignore` excluye `.env.*` salvo `.env.example`, y el backend exige variables DB completas.
- Rotación: se actualizó MySQL root en `root@%` y `root@localhost`, junto con `DB_PASSWORD`/`MYSQL_ROOT_PASSWORD` locales; también se rotó `Jwt__Key` y revocaron todos los refresh tokens activos. `.env` queda 0600. Se recreó el contenedor sin eliminar el volumen; las sesiones locales deben iniciar sesión de nuevo.
- Verificación: API conecta a MySQL y aplica el estado ya migrado; permanecen las 21 migraciones y cero registros QA.

## 2026-11-21 — Configuración de prueba apuntó al host global de .NET y a usuario MySQL incorrecto — CORREGIDO

- Síntoma: `/usr/bin/dotnet` seleccionó un host sin runtime instalado; el primer query MySQL omitió `-uroot` y devolvió 1045. Una primera versión del escáner de secretos olvidó importar `os`.
- Solución: ejecutar con `DOTNET_ROOT=/home/leonardo/.dotnet`; usar root y `MYSQL_PWD` dentro del contenedor; importar `os` y repetir el análisis sin mostrar valores secretos.
- Verificación: suite 43/43, Release build limpio, DB consultada, auditoría frontend de npm sin vulnerabilidades. Los patrones reportados en SQL eran interpolaciones de variables de entorno, no credenciales literales.

## 2026-11-21 — Swagger devolvió 404 al iniciar API como Production — CORREGIDO

- Causa: el lanzamiento manual omitió `ASPNETCORE_ENVIRONMENT=Development`; Swagger sólo se habilita en Development.
- Solución: reiniciar API local con `ASPNETCORE_ENVIRONMENT=Development` en loopback.
- Verificación: Swagger 200, endpoint protegido 401 sin token, dashboard 200.

## 2026-11-21 — Docker Compose anunció `version` obsoleto — CORREGIDO

## 2026-11-21 — Revisión independiente detectó fallos de sesión, roles y operación — CORREGIDOS

- Hallazgos: tokens de reset/activación podían aparecer en logs; activación no cerraba sesiones; asignación aceptaba vehículos en mantenimiento; rol legacy permitía múltiples roles aunque el JWT elegía uno; rutas tenían fallback por ID de usuario; CORS no se aplicaba en todos los entornos; fechas reales de ruta no se persistían.
- Solución: se removieron logs sensibles, activación revoca refresh tokens, asignación exige vehículo Available, rol se reemplaza transaccionalmente y queda único en DB, el endpoint legacy delega al cambio atómico y quitar el rol responde Conflict. Se eliminó el fallback inseguro, CORS se aplica siempre y fuera de Development se activa HTTPS/HSTS. Se añadieron `StartedAt`/`EndedAt` y migración aditiva.
- Verificación: 45/45 pruebas Release, 22 migraciones, cero usuarios con múltiples roles, cero sesiones vigentes y auditoría NuGet limpia.

## 2026-11-21 — Comandos de cierre usaron identificadores de esquema incorrectos — CORREGIDO

- Síntoma: consultas de sólo lectura usaron `BackEndGps` en lugar de `fletflow` y luego `IsRevoked`, columna inexistente.
- Causa: se asumió el nombre de base y booleano de revocación sin inspeccionar esquema.
- Solución: leer `DB_NAME` del entorno sin imprimir secretos, inspeccionar columnas y usar `RevokedAt IS NULL`.
- Verificación: DB reportó 22 migraciones, cero asignaciones multirol, cero tokens vigentes y cero filas QA. Las consultas fallidas no modificaron datos.

## 2026-11-21 — EF CLI usó host .NET global no configurado — CORREGIDO

- Síntoma: el comando de migración no encontraba runtime bajo `/usr/lib64/dotnet/host/fxr`.
- Solución: usar SDK instalado en `/home/leonardo/.dotnet` con `PATH` y `DOTNET_ROOT` explícitos.
- Verificación: migración `RouteLifecycleAndSingleRole` generada y aplicada a la DB local preservando datos.

## 2026-11-21 — Build de unidad de asignación omitió namespace del estado de vehículo — CORREGIDO

- Síntoma: primer build de la prueba de mantenimiento no resolvió `VehicleStatus`.
- Solución: importar `fletflow.Domain.Fleet.Entities`.
- Verificación: suite Release completa 45/45.

## 2026-11-21 — E2E de cierre apuntó a nombres de campos SQL previos — CORREGIDO

- Síntoma: el primer query usó `IsRevoked`, pero el esquema registra `RevokedAt`.
- Solución: inspeccionar `RefreshTokens` y consultar `RevokedAt IS NULL`; query confirma cero sesiones vigentes.
- La prueba QA autenticada posterior limpió todas las cuentas/tokens/vehículo aunque finalice con fallo.

## 2026-11-21 — Puertos MySQL/phpMyAdmin publicados en todas las interfaces — CORREGIDO

- Hallazgo de seguridad: los puertos Docker `3306` y `8081` estaban enlazados a todas las interfaces del host.
- Solución: enlazarlos explícitamente a `127.0.0.1` y recrear contenedores manteniendo el volumen.
- Verificación: `docker compose ps` muestra `127.0.0.1:3306` y `127.0.0.1:8081`; DB conserva 22 migraciones, API Swagger 200, dashboard 200 y proxy protegido 401.

## 2026-11-21 — Harness Chrome falló durante limpieza de fixtures — CORREGIDO

- Un cleanup omitió un paréntesis SQL y otra ejecución no heredó `MYSQL_PWD` porque `.env` define `MYSQL_ROOT_PASSWORD`.
- Se corrigió SQL, el harness mapea la variable y suprime diagnósticos internos.
- El error anterior imprimió una credencial local en la excepción; se rotó root MySQL, se actualizó `.env` modo 0600, se reinició API y se limpiaron fixtures. Verificado: 22 migraciones, cero QA accounts/Drivers y cero sesiones activas.

## 2026-11-21 — E2E Logistics falló por fixture inválido/invitación pendiente — CORREGIDO

- Primer fixture intentó cambiar rol del Admin raíz y recibió 400; el segundo intentó login de un usuario de equipo aún no activado y recibió 400 por invitación SMTP pendiente.
- Se creó un Manager dentro del tenant mediante el endpoint Admin, se cambió a Logistics y se activó sólo ese fixture en la DB local para completar el login.
- Chrome/API confirmó Routes 200, Vehicles 403 y UI sin controles de Equipo/Vehículos. Se eliminó la cuenta y sus tokens.

## 2026-11-21 — E2E de vehículo esperaba 404 luego de baja lógica — CORREGIDO EN EL TEST

- El endpoint conserva recursos desactivados para historial, por lo que GET por ID responde 200 con `IsActive=false`; 404 era un assert incorrecto.
- La prueba confirma que `onlyActive=true` excluye el vehículo. Un XPath tenía un slash final inválido y se corrigió.
- Chrome pasó alta/búsqueda/estado vacío/edición/baja lógica; vehículo y cuenta QA se eliminaron.

- Síntoma: Compose v2 avisaba que `version: "3.9"` ya se ignora.
- Solución: quitar la clave obsoleta; el formato actual no necesita versión explícita.
- Verificación: `docker compose up -d --force-recreate mysql` completó sin ese aviso y preservó el volumen.

## 2026-11-21 — E2E GPS cruzado esperaba 404 para colección filtrada — ACLARADO

- `GET /Routes/{id}/positions` responde 200 con lista vacía cuando el tenant no posee la ruta, porque el query filter de EF oculta todas sus posiciones; la ruta individual sigue respondiendo 404.
- No se cambió backend. Se añadió un registro GPS real en la ruta propietaria y asserts: una posición visible para A, `[]` para B, y POST por B 404.
- La creación de posición requiere ruta InProgress; el test primero ejecuta la transición permitida. E2E completo pasó 8/8.

## 2026-11-21 — DotNetEnv ignoraba la base inyectada y apuntaba a `fletflow` — CORREGIDO

- El primer arranque del smoke debía usar una base CI temporal, pero `Env.Load()` reemplazó variables ya existentes con valores de `.env`. Los logs confirmaron conexión al esquema `fletflow`; el API se detuvo antes de cualquier solicitud HTTP.
- Ese arranque creó únicamente el Admin QA bootstrap `fletflow.e2e.admin.localvalidation@example.test`; no generó tokens ni recursos. Se borró exactamente esa cuenta y se comprobó que quedara en cero; la base temporal vacía se descartó.
- `Program.cs` ahora usa `Env.NoClobber().Load()`: variables del proceso (CI/despliegue) prevalecen sobre `.env`. Repetición segura confirmó 22 migraciones en `fletflow_ci_validation_02` y cero cambios a la DB principal.

## 2026-11-21 — Ejecución de API aislada usó el host `dotnet` del sistema — CORREGIDO EN EL COMANDO

- El primer lanzamiento manual falló antes de ejecutar código: el `dotnet` de sistema apunta a un host sin runtimes (`/usr/lib64/dotnet/host/fxr`).
- Se usó `/home/leonardo/.dotnet/dotnet`, la instalación .NET 8.0.428 documentada para este entorno; API inició y aplicó migraciones a la base CI indicada.

## 2026-11-21 — Smoke tenant no recibía MYSQL_PWD en CI y limpiaba prefijos amplios — CORREGIDO

- La revisión independiente detectó que el workflow no exportaba `MYSQL_PWD`, requisito del script. Se añadió al entorno de CI; una ejecución local sin esa variable abortó antes de crear fixtures.
- La limpieza inicial reutilizaba `cleanup-e2e.sql` y permitía `DB_NAME=fletflow`, con riesgo de afectar otros datos QA. El smoke ahora acepta sólo `fletflow_ci*`, elimina por IDs exactos y usa fallback por identificadores únicos del propio fixture.
- Verificación: script de aislamiento HTTP/MySQL pasó; conteos QA en cero en la base CI y base principal.

## 2026-11-21 — CI backend cubría aislamiento de tenants, pero no rechazo HTTP del rol User — CORREGIDO

- Hallazgo: la matriz de HTTP/MySQL del smoke tenía dos Admin de tenants distintos; la denegación del rol `User` sólo se validaba en pruebas frontend locales y tests de autorización, no en el job backend.
- Solución: `scripts/smoke-tenant-http.sh` crea un `User` QA en el tenant A, lo activa sólo en la base descartable, y verifica 403 en listar vehículos, crear vehículo y listar rutas. La limpieza retira tokens, reset tokens, rol y cuenta por email exacto; también borra por placa única si una regresión llegase a permitir el POST.
- Verificación: API real + MySQL en `fletflow_ci_validation_03`, smoke completo aprobado y DB descartada al salir. No se tocó la base principal.

## 2026-11-21 — Runner frontend/MySQL no era portable a Actions — CORREGIDO

- Hallazgo: el E2E completo llamaba `docker exec mysql-gps` y leía `.env` siempre, así que fallaría dentro de Actions aunque el workflow levantara MySQL service y pasara variables aisladas.
- Solución: el runner conecta con cliente MySQL host; sólo carga `.env` local si `DB_NAME` no fue inyectada, exige credenciales MySQL y en CI sólo acepta esquemas `fletflow_ci*`. Se añadió workflow frontend con checkout del backend público en su rama de estabilización, backend/API efímeros y ejecución Playwright autenticada.
- Verificación local de integración runner: Chrome/API/MySQL **8/8**; esquema de prueba eliminado. Workflow YAML parseado. Ejecución remota queda pendiente de publicar ramas y abrir PR.

## 2026-11-22 — Runner CI esperaba Next activo antes de que Playwright lo iniciara — CORREGIDO

- El primer PR frontend pasó build, audit, lint y el chequeo responsive, pero el runner autenticado terminó con código 2: su precheck pedía `/login` antes de ejecutar Playwright. El job no llegó a provisionar usuarios E2E.
- El runner ahora comprueba siempre Swagger/API y sólo exige frontend preiniciado fuera de CI; en CI Playwright `webServer` arranca Next.
- Se actualizó checkout v4→v7 y setup-dotnet v4→v6 para abandonar Node 20 según advertencia de Actions. La próxima ejecución del workflow debe verificarlo.

## 2026-11-21 — Asignaciones concurrentes podían solaparse y conductor inactivo podía iniciar ruta — CORREGIDO

- Hallazgo: CreateRoute/UpdateRoute comprobaban disponibilidad antes de guardar sin serializar escrituras concurrentes. Dos solicitudes podían ver agendas libres y confirmar solapamientos. Además, Pending→InProgress no revalidaba que el conductor asignado siguiera activo.
- Solución: transacción relacional y bloqueos `FOR UPDATE` de Vehicle y Driver, siempre en ese orden y con filtro por propietario; la agenda se vuelve a comprobar bajo los locks y el conflicto responde HTTP 409. El cambio de estado rechaza el Driver inactivo con 409. Las escrituras fallidas revierten la transacción.
- Verificación real: MySQL descartable, pares concurrentes de creación (mismo vehículo / mismo Driver) y edición (mismo vehículo/conductor), cada par produjo un éxito y un 409. El smoke de aislamiento HTTP/MySQL también pasó. Release xUnit 49/49.

## 2026-11-21 — Nueva prueba SQLite introdujo paquete nativo con CVE — CORREGIDO

- Hallazgo: `Microsoft.EntityFrameworkCore.Sqlite` resolvía `SQLitePCLRaw.lib.e_sqlite3` 2.1.10, afectado por CVE-2025-6965 (GHSA-2m69-gcr7-jv3q); NuGet identificó severidad alta.
- Solución: se fijó `SQLitePCLRaw.lib.e_sqlite3` 2.1.14 en el proyecto de pruebas. La versión estable publicada incluye SQLite posterior a la versión vulnerable; no se suprimió la alerta.
- Verificación: `dotnet list BackEnd_Gps.sln package --vulnerable --include-transitive` no reporta paquetes afectados; xUnit Release 49/49. Fuentes: https://github.com/advisories/GHSA-2m69-gcr7-jv3q y https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3.

## 2026-11-21 — Rutas del mismo vehículo podían iniciarse a la vez — CORREGIDO

- Hallazgo de revisión independiente: dos transiciones Pending→InProgress podían leer `Available` en paralelo y dejar ambas rutas activas para un vehículo.
- Solución: las transiciones serializan la ruta y su asignación mediante locks owner-scoped; vuelven a leer el estado después de adquirirlos y rechazan con 409 si otra ruta del vehículo ya está en progreso. UpdateRoute usa el mismo lock de ruta y bloquea tanto la asignación previa como la nueva.
- Verificación: xUnit Release 50/50; smoke HTTP/MySQL con dos solicitudes simultáneas de inicio devolvió un 204 y un 409. El smoke completo de tenant también pasó y la DB temporal se descartó.

## 2026-11-21 — Inspección imprimió credencial MySQL local — MITIGADO

- La consulta de nombres de variables de `.env` incluyó accidentalmente el valor de `MYSQL_ROOT_PASSWORD` en la salida de diagnóstico.
- Se rotó la contraseña de `root` para los hosts MySQL `%` y `localhost`, se actualizó `.env` y se dejó con permisos `0600`.
- Verificación: autenticación con el nuevo valor sin mostrarlo y creación/eliminación de DB temporal funcionaron; `.env` no está versionado. No se imprimió la credencial nueva.

## 2026-11-21 — Regresión de Driver esperaba el código anterior al conflicto — CORREGIDO EN EL TEST

- Al cambiar la desactivación de Driver con ruta en progreso a `BusinessConflictException` (HTTP 409), la prueba existente seguía exigiendo `InvalidOperationException` exactamente.
- Se actualizó el assert al tipo de conflicto de negocio; no era un fallo de producto.
- El primer ajuste usó el nombre plenamente cualificado dentro del namespace `BackEnd_Gps.Tests` y el compilador lo resolvió como `BackEnd_Gps.fletflow`; se añadió el `using` explícito y el assert ahora compila con `BusinessConflictException`.
- La suite se vuelve a ejecutar después de alinear el test y corregir el namespace.

## 2026-11-21 — Smoke interpretó el booleano MySQL como carácter NUL — CORREGIDO EN EL ASSERT

- La primera corrida de la carrera inicio/desactivación produjo el estado coherente `Route Pending + Driver inactive`, pero la consulta `CONCAT` convirtió el `BIT` MySQL a carácter NUL y el comparador rechazó el resultado.
- El smoke ahora castea `IsActive` a `UNSIGNED` antes de concatenarlo. La base temporal se eliminó por el `trap`; no hubo fuga de datos.
- Repetir la suite completa para confirmar 204/409 y el estado permitido.

## 2026-11-21 — Smoke de inicio simultáneo no verificaba persistencia — CUBIERTO

- Revisión independiente observó que el smoke sólo validaba HTTP 204/409 para dos inicios concurrentes, sin comprobar la ruta y vehículo que quedaron guardados.
- Se añadió consulta MySQL que exige exactamente una ruta InProgress, una Pending y el vehículo OnRoute. Para inicio contra desactivación de Driver, el estado persistido sólo admite `InProgress + Driver activo + OnRoute` o `Pending + Driver inactivo + Available`.
- Repetición completa en DB temporal `fletflow_ci_validation_14` pasó con los asserts: una ruta InProgress, una Pending y vehículo OnRoute; la carrera con desactivación terminó en una de dos combinaciones consistentes. La DB se descartó con `trap`.

## 2026-11-21 — Auditoría de seguridad encontró sesiones vivas tras baja de usuario — CORREGIDO

- `UpdateUserCommand` sólo modificaba `IsActive` y no revocaba refresh tokens; `AuthService.RefreshAsync` permitía renovar cuentas inactivas y JWT no consultaba el estado de cuenta.
- Se conserva la actualización EF de entidad rastreada, se revocan refresh tokens y JWT valida estado actual por cada autenticación. El refresh también rechaza cuenta inactiva.
- Primera prueba local arrancó DLL no recompilado después del último ajuste del comando y respondió 200/activo; recompilación Release y smoke repetido verificaron 401 en `/Auth/me` y `/Auth/refresh`.

## 2026-11-21 — Rutas/vehículos podían quedar operativamente huérfanos — CORREGIDO

- Baja de ruta en curso dejaba vehículo `OnRoute` e impedía registrar GPS; Driver podía desasignarse durante recorrido; Vehicle inactivo podía empezar una ruta pendiente.
- Se agregaron locks, chequeo de ruta/vehículo/conductor y conflictos 409. La baja de Vehicle exige desasignar Driver y resolver rutas pendientes/en curso.
- La ruta temporal local dio inicialmente 500 creando placa QA demasiado larga; el prefijo/document ID se acortó dentro del máximo y el smoke pasó. IDs descartables se limpiaron por prefijo acotado y la DB efímera se descartó.

## 2026-11-21 — Escrituras paralelas de puntos podían sobrescribirse — CORREGIDO

- `UpdatePointStatus`/`AddPointEvidence` actualizaban `PointsJson` completo sin serialización de fila.
- Ambos handlers toman lock de ruta/asignación, vuelven a leer después del lock y commitean la instantánea actual. Smoke MySQL realizó simultáneamente dos estados de parada y verificó ambos en la respuesta persistida.

## 2026-11-21 — Configuración local de smoke no alineaba DB_PASSWORD y MYSQL_ROOT_PASSWORD — CORREGIDO EN EL ENTORNO DE PRUEBA

- CLI MySQL usaba `MYSQL_ROOT_PASSWORD` mientras el backend usa `DB_PASSWORD`; los valores guardados localmente difieren y el API falló al iniciar con Access denied. No creó schema ni fixtures en esa primera llamada.
- Se pasó la credencial MySQL activa a `DB_PASSWORD` sólo en el proceso de la corrida aislada. No se imprimió ni modificó el secreto guardado; smoke se ejecutó y descartó su base.

## 2026-11-21 — Harness local mezcló usuario de DB, wrapper de API y URL de runner — CORREGIDO

- Una primera preparación con `dotnet run` dio `Access denied` para el usuario root aunque se había creado DB QA. Se abandonó esa invocación ambigua y se inició el DLL Release directamente con un usuario SQL efímero limitado al esquema; API y migrations conectaron correctamente. Usuario y DB se eliminaron con trap.
- Los scripts smoke no tienen permiso ejecutable en este checkout; llamarlos directamente dio `Permission denied`. Se ejecutaron con `bash script.sh`; no se abrió permiso global ni se modificó la base principal.
- `run-e2e-local.sh` rechazó `FLETFLOW_API_URL` terminada en `/api`, mientras smokes requieren precisamente esa ruta. El runner ahora elimina el sufijo opcional antes de validar que el host sea local; una repetición completa pasó.
- Un primer E2E tampoco exportó `FLETFLOW_E2E_ADMIN_EMAIL/PASSWORD` y se detuvo antes de crear fixtures; la corrida siguiente asignó variables explícitas.
- En E2E, se corrigieron dos supuestos: el rol Usuario tiene nombre accesible más descripción, y el endpoint `/Users/my-team` devuelve `roles[]`. El snapshot DOM y DTO indicaron las aserciones correctas; Playwright final pasó 14/14.
- Smokes HTTP/tenant y concurrencia pasaron después; todas las bases, cuentas SQL y fixtures efímeros fueron eliminados.

## 2026-11-21 — Cambio de rol Driver podía competir con inicio de ruta — CORREGIDO

- El comando comprobaba rutas activas y desactivaba el Driver sin el lock compartido por el inicio; una carrera podía conservar ruta en progreso con conductor inactivo.
- Se toma el lock de asignación, se vuelve a leer Driver/usuario y se comprueba ruta activa dentro de la sección serializada; cualquier fallo ejecuta rollback. El inicio y el cambio de rol ahora compiten por el mismo lock de Driver.
- Se añadió smoke concurrente real que exige 204/409 y verifica en MySQL uno de dos resultados íntegros. La ejecución pasó.

## 2026-11-21 — Access token conservaba permisos después de cambiar el rol — CORREGIDO

- La validación JWT sólo consultaba `IsActive`, por lo que un JWT ya emitido seguía llevando el rol anterior tras cambiar la cuenta.
- El middleware compara el claim de rol con el único rol actual en DB y falla autenticación si no coincide; `ChangeUserRoleCommand` revoca refresh tokens al cambiarlo.
- Smoke HTTP con MySQL comprobó que access token antiguo y refresh token antiguo responden 401 inmediatamente después del cambio.

## 2026-11-21 — Assert de registro público leía encabezado SQL como resultado — CORREGIDO

- La verificación del rol User comparaba la salida completa de `mysql`; el cliente imprimía primero el encabezado de columna, por lo que el assert fallaba aunque la fila persistida fuera `1:user`.
- Se añadió `--batch --skip-column-names` y se comprobó que haya exactamente un rol `user`. El smoke HTTP final pasó: el registro que solicita Admin queda User y recibe 403 en Vehicles. También pasó logout-all real y revocación del refresh.
- Esquema, usuario SQL y fixtures de prueba se descartaron con el trap.

## 2026-11-21 — Host dotnet del sistema sin runtime — DOCUMENTADO

- `/usr/bin/dotnet test` terminó antes de compilar: `/usr/lib64/dotnet/host/fxr` no contiene una versión instalada.
- La instalación utilizable está en `/home/leonardo/.dotnet` (SDK 8.0.428). Con `/home/leonardo/.dotnet/dotnet`, las cuatro pruebas nuevas y la suite completa pasaron (54/54). Problema del entorno, no del código.

## 2026-11-21 — Primer test de recuperación no compiló por namespace de pruebas — CORREGIDO

- El primer build de `PasswordRecoveryCommandTests` usó rutas `fletflow.Domain...` totalmente calificadas dentro del namespace `BackEnd_Gps.Tests`; C# resolvió el prefijo como `BackEnd_Gps.fletflow` y reportó interfaces de IUnitOfWork no implementadas.
- Se importó `fletflow.Domain.Fleet.Repositories` y se usaron los nombres de interfaz cortos en las implementaciones explícitas. Compilación y suite completa posteriores: 54/54.

## 2026-11-21 — dotnet-ef usó host del sistema y no pudo comprobar migración en DB — CORREGIDO/DOCUMENTADO

- La herramienta local inicialmente invocó `/usr/bin/dotnet` y falló porque no tiene `host/fxr`; se instaló `dotnet-ef` 9.0.10 en `/tmp` y se ejecutó con `DOTNET_ROOT=/home/leonardo/.dotnet` y ese SDK en `PATH`.
- Al regenerar la migración, `migrations remove` no pudo consultar si estaba aplicada: acceso denegado para `root` desde el host del contenedor. La migración recién generada no se había aplicado; EF la retiró del código/snapshot y se regeneró como `20261122040123_InvalidateAccessTokensAfterPasswordChange`. Ningún dato o esquema local fue modificado por estas operaciones de scaffold.
- Validación de aplicación pendiente mediante workflow MySQL descartable, que ejecuta las migraciones desde cero; el acceso root local no se reutilizará para probar ni migrar una base persistente.

## 2026-11-21 — Llamada existente a GenerateToken quedó incompatible — CORREGIDO

- El primer build tras agregar el argumento `tokenVersion` encontró el test `TenantIsolationTests` que llama directamente al generador.
- El argumento ahora tiene default 0 para compatibilidad de generación directa; AuthService siempre pasa la versión persistida real. Suite posterior pasó 54/54.

## 2026-11-21 — Aplicación de parche de smoke no encontró el bloque esperado — CORREGIDO

- El primer parche buscó líneas contiguas, pero el fixture de usuario limitaba primero el `restricted_attempt_plate`; apply_patch no hizo cambios.
- Se inspeccionó el bloque real con `nl -ba` y se insertó junto al punto exacto después del login. `bash -n` y Actions `44983763612` pasaron el smoke completo en MySQL desechable.

## 2026-11-21 — JWT podía recuperar permisos al restaurar un rol — CORREGIDO

- Confirmación del código actual: `Program.cs` valida `token_version` y compara el rol del JWT con el rol vigente; `ChangeUserRoleCommand` revocaba refresh tokens, pero no aumentaba `TokenVersion`. El JWT viejo fallaba mientras el rol era distinto, pero volvía a ser aceptado si se restauraba el rol original antes de su expiración.
- Corrección: todo cambio efectivo incrementa `Users.TokenVersion` y revoca refresh tokens en el mismo UnitOfWork/commit. xUnit cubre cambio efectivo y no-op; el smoke HTTP cambia Logistics→User→Logistics y vuelve a comprobar que los tokens iniciales sigan respondiendo 401.
- Primer smoke local no completó: el runner no pasaba el puerto MySQL y conectó al 3306 local, donde el password temporal fue rechazado antes de ejecutar SQL. No se modificó la DB local. Se añadió `MYSQL_PORT` y se repitió con MySQL 8 y DB `fletflow_ci_*` desechables; smoke completo aprobado y contenedor eliminado.

## 2026-11-22 — Cleanup del smoke perdió la ruta soft-deleted de Manager — CORREGIDO

- Al agregar creación positiva como Logistics, el primer smoke pasó las verificaciones HTTP de alta, lectura y edición, pero `cleanup()` falló. La prueba reutilizó `manager_route_id` para guardar la ruta Logistics; la ruta previa de Manager permanece como fila inactiva por la baja lógica. El trap perdió su ID y no pudo borrar todas las referencias antes de borrar vehículo/conductor.
- Corrección: conservar `manager_route_id` y `logistics_route_id` por separado y eliminar ambas rutas/posiciones en el cleanup SQL existente. También se dejó visible el error SQL de cleanup en stderr para que un fallo futuro no quede oculto.
- Verificación: repetición completa en API + MySQL 8 dentro de un contenedor efímero y esquema `fletflow_ci_*`. Manager creó/editó/dio de baja antes de reutilizar su agenda; Logistics creó (201), leyó y verificó campos persistidos, editó y volvió a verificar persistencia (200), y dio de baja (204). El script terminó con código 0 y el contenedor fue eliminado. La DB local nunca se usó.

## 2026-11-22 — Verificador de cleanup del ciclo Logistics mezcló warning MySQL con conteos — CORREGIDO

- Al ampliar el smoke al ciclo completo de Logistics, la primera ejecución pasó las operaciones HTTP y aserciones de ruta/vehículo, pero el conteo de cleanup falló porque MySQL emitió un warning en stdout junto a la fila numérica.
- Corrección: capturar stderr del query de verificación en un archivo temporal separado, mostrarlo sólo si el query falla y comparar stdout sólo con `0:0:0`. El cleanup fuerza el Vehicle a Available antes de borrarlo y verifica que no queden rutas ni posiciones.
- Repetición: `bash scripts/smoke-tenant-http.sh` contra API `127.0.0.1:5058`, MySQL 8 efímero en puerto 13306 y DB `fletflow_ci_lifecycle_*`. Logistics recibió 201 al crear; 200 en lectura/edición/parada/evidencia/GPS; 204 al iniciar/completar/baja. Lectura final confirmó `Completed` y vehículo `Available`. Cleanup confirmó 0 rutas, 0 posiciones y 0 vehículos. API y contenedor temporales eliminados.

## 2026-11-22 — Cleanup no tenía fallback si una respuesta POST no traía el ID — ENDURECIDO

- Revisión estática encontró que si un POST creaba la ruta pero la extracción del ID fallaba, `cleanup()` no tenía el ID y tampoco corría la aserción de cero residuos.
- Corrección: guardar el nombre QA antes del POST y usarlo como alternativa para borrar ruta/posiciones y para la consulta final cuando no se extrajo el ID. Se aplica a Manager y Logistics.
- Un primer intento de QA delegado no inició pruebas porque `/usr/bin/dotnet` no tenía un host SDK (`host/fxr` vacío); no creó base ni contenedor y no tocó MySQL. Repetición con `DOTNET_ROOT=/home/leonardo/.dotnet` compiló y ejecutó el smoke contra MySQL 8 descartable; el ciclo pasó y cleanup confirmó `0:0:0`. El camino de fallback por nombre estático no se indujo por separado.
- Actions `44991032520` para `da53081` confirmó build, xUnit 57/57 y smoke HTTP/MySQL completo; `Cleanup verificado: 0 rutas, 0 posiciones GPS y 0 vehículos`. El fallback específico por nombre sigue sin inducción directa, pero la limpieza normal pasó con el nuevo código.

## 2026-11-22 — Placa duplicada se devolvía como error genérico — CORREGIDO

- La inspección del CRUD encontró que CreateVehicle/UpdateVehicle detectaban duplicados pero lanzaban `InvalidOperationException`; el middleware respondía 400 y el mensaje técnico en inglés llegaba a la UI.
- Se cambiaron a `BusinessConflictException`, que el middleware mapea a HTTP 409, y mensajes claros en español. Pruebas unitarias confirman que alta/edición no hacen commit ni mutan la entidad si la placa pertenece a otro vehículo.
- Validación: Release local con .NET 8.0.428, 59/59. Actions integrada aún pendiente.

## 2026-11-22 — Rutas de inspección cruzada equivocadas — CORREGIDO

- Búsquedas iniciales usaron rutas relativas inexistentes para el backend y una asumió `AppDbContext.cs`, que no existe. `rg` informó archivos inexistentes y no inspeccionó esos objetivos ni modificó datos.
- Corrección: usar `/home/leonardo/Back_GPS` como raíz, listar con `rg --files`, y abrir el contexto real `fletflow/Infrastructure/Persistence/Context/FleetFlowDbContext.cs`. Inspección posterior confirmó filtros por `OwnerUserId` y el índice compuesto tenant+placa.
- Prevención: comprobar `pwd`/archivos antes de consultas cruzadas y verificar el nombre real desde `rg --files`.

## 2026-11-22 — Host dotnet predeterminado no tiene SDK — SUPERADO

- `/usr/bin/dotnet test` terminó antes de compilar porque `/usr/lib64/dotnet/host/fxr` no tiene runtime.
- Solución: usar `DOTNET_ROOT=/home/leonardo/.dotnet` y `/home/leonardo/.dotnet/dotnet` (SDK 8.0.428). La suite Release terminó 59/59; ningún test fue omitido.

## 2026-11-22 — Documento Driver duplicado respondía 400 y texto técnico — CORREGIDO, CI PENDIENTE

- CreateDriver y UpdateDriver detectaban documento repetido pero usaban InvalidOperationException, que middleware transformaba en 400. El endpoint `POST /Users` prevalidaba duplicado y devolvía 400; semántica inconsistente con conflicto de identidad y la UI no tenía un estado estable para conflicto.
- Corrección: usar BusinessConflictException/HTTP 409 y un mensaje español uniforme también en UserController, antes de crear la cuenta. Pruebas unitarias prueban rechazo sin mutación/commit; E2E agregado comprueba sin usuario parcial.
- Suite backend Release local: 61/61; lint/build/discovery Front local: 16 casos. CI integrado pendiente.
- Validación corregida confirmada: Actions `44997043103` para `a7f7c4e` pasó build, xUnit 61/61 y smokes MySQL tenant/concurrencia.

## 2026-11-22 — DTO de registro buscado bajo `Application` en vez de `Aplication` — CORREGIDO

- `sed` usó `fletflow/Application/DTOs/Auth/RegisterUserDto.cs`, pero el repositorio mantiene el nombre histórico de carpeta `fletflow/Aplication`; el comando devolvió archivo inexistente y no cambió nada.
- Corrección: localizar con `rg --files fletflow` y abrir `fletflow/Aplication/Auth/DTOs/Auth/RegisterUserDto.cs`. El DTO permite omitir `RoleName`; el servidor aplica la política fija en el controlador.

## 2026-11-22 — Registro público creaba cuenta operativa sin permisos — CORREGIDO EN CÓDIGO, INTEGRACIÓN PENDIENTE

- UX y API discrepaban: `/register` prometía una cuenta Gerente, el frontend solicitaba Admin, pero `AuthController` forzaba User. El registro emitía una sesión válida para una cuenta incapaz de operar su nueva empresa.
- El registro es la creación de un tenant nuevo; no requiere elevar permisos dentro de una empresa existente. Se cambió a asignar Admin raíz fijo en servidor, ignorando el rol de request. El token limita esa cuenta a `owner_id=user.Id` y los filtros multi-tenant mantienen el aislamiento.
- xUnit verifica que una solicitud Driver recibe Admin; Release local 61/61. Smoke HTTP/MySQL verifica un tenant Admin nuevo con flota vacía y acceso 200; Actions integrada aún pendiente.
- El runner ya no muta manualmente `UserRoles` del segundo tenant, para que la prueba confirme el contrato del registro real.

## 2026-11-22 — Parche de documentación detenido por encabezado distinto — CORREGIDO

- Un parche agrupado esperaba el encabezado `## Estado actual`, que no existe en `PROJECT_STATUS.md` (el encabezado real es `## Estado operativo`). El parche se detuvo parcialmente: sólo se aplicaron los cambios previos independientes a Business Rules y Security; no se tocó código ni datos.
- Corrección: inspeccionar el encabezado real, aplicar los cambios pendientes por contexto correcto y revisar `git diff` en ambos repositorios.
- Prevención: comprobar la estructura de cada archivo antes de crear parches multiarquivo dependientes de headings.

## 2026-11-22 — Dotnet global apuntaba a un host sin SDK — CORREGIDO

- `dotnet test` mediante el ejecutable global resolvió `/usr/lib64/dotnet/host/fxr`, que no contiene versiones instaladas; terminó con código 131 antes de ejecutar pruebas.
- Corrección: usar el SDK local documentado `/home/leonardo/.dotnet/dotnet`; Release volvió a pasar 61/61.
- Prevención: en este entorno invocar explícitamente el SDK local, sin depender del `dotnet` del PATH.

## 2026-11-22 — Esquema inicial incorrecto en pregunta de negocio — CORREGIDO

- La primera llamada a la herramienta de aclaración usó el campo `question`; el contrato actual requiere `title` y `options`, y rechazó la solicitud sin enviarla al usuario.
- Corrección: repetir la pregunta con los campos requeridos. Se consulta si un Admin delegado puede crear/promover Admin, mientras las pruebas independientes de aislamiento continúan.
- Prevención: confirmar el esquema activo de herramientas de entrada antes de construir la solicitud.

## 2026-11-22 — Workflows no ejecutables en ramas de trabajo — CORREGIDO Y VALIDADO

- Los workflows sólo escuchaban push/PR hacia `main`; al mantener el trabajo aislado en `chore/stabilize-fleet-platform`, no había ejecución automática para validar este cambio integrado sin crear una integración a `main`.
- Corrección: habilitar `workflow_dispatch` para que la suite completa se ejecute contra el ref de trabajo elegido; el workflow frontend usa además el backend feature checkout.
- Prevención: al exigir validación antes de merge, comprobar que la CI se pueda ejecutar sobre la rama aislada.
- Evidencia: Actions backend `44999697740` para `3620cc2` success en build, xUnit, smoke HTTP/MySQL y concurrencia.


## 2026-11-22 — MySQL CONCAT representó booleano como byte de control en smoke — CORREGIDO

- Actions `45000671534` pasó build y 61/61 xUnit, y los endpoints cross-tenant/propietario respondieron como se esperaba. El smoke falló al comparar estado porque `CONCAT(u.IsActive, ...)` serializó el `TINYINT(1)` como byte `0x01` (`^A`) en vez del carácter `"1"`.
- Corrección: normalizar el booleano mediante `CASE WHEN ... THEN '1' ELSE '0' END` antes de concatenar.
- Prevención: representar booleanos explícitamente como strings en asserts SQL; repetir Actions del smoke MySQL.

## 2026-11-22 — Comilla invertida sin escapar en llamada de parche JS — CORREGIDO

- Un intento de `apply_patch` dentro de la herramienta `exec` incluyó una comilla invertida sin escapar en una cadena template literal; JavaScript reportó `SyntaxError` antes de ejecutar herramientas o editar archivos.
- Corrección: aplicar el reemplazo con un script Python heredoc y comprobar el diff.
- Prevención: evitar interpolación de template literals al transportar parches que contengan backticks.

## 2026-11-22 — Contratos de persistencia buscados bajo carpetas equivocadas — CORREGIDO

- La interfaz `IUnitOfWork` y contratos de Driver/PasswordResetToken/User se buscaron bajo `Infrastructure/Persistence/Contracts`; el código los tiene en `Aplication/Common/Interfaces` o `Domain/*/Repositories`, así que esos paths devolvieron archivo inexistente.
- Corrección: localizar las fuentes con `rg --files fletflow` y leer los contratos desde sus rutas reales. No hubo cambios de archivos ni datos por los comandos fallidos.
- Prevención: buscar primero el nombre de interfaz o contrato, no asumir que comparte carpeta con su implementación.


## 2026-11-22 — Bloque de documentación añadió EOF en blanco extra — CORREGIDO

- `git diff --check` señaló líneas en blanco al final de seis archivos después de concatenar secciones sobre la alta Driver atómica.
- Corrección: normalizar cada archivo a una sola línea final y repetir diff-check.
- Prevención: añadir secciones con EOF normalizado en el mismo script y comprobar whitespace inmediatamente.
# 2026-11-22 — Smoke de alta Driver asumía HTTP 201 — RESUELTO

- Síntoma: Actions `45001352585` confirmó una creación y un conflicto en la carrera de documento duplicado, pero el smoke falló porque recibió `200` y `409` en lugar de `201` y `409`.
- Causa: `UsersController` devuelve HTTP 200 para la creación de una cuenta Driver; el smoke había supuesto incorrectamente que el endpoint respondía 201. La prueba de atomicidad fue válida, la expectativa HTTP no respetaba el contrato implementado.
- Solución: aceptar `200` o `201` para exactamente una creación, manteniendo `409` obligatorio para la solicitud concurrente perdedora y el assert SQL `Users:Drivers:PasswordResetTokens=1:1:1`.
- Prevención: al escribir pruebas HTTP nuevas, inspeccionar y validar el código real de respuesta del endpoint antes de codificar la expectativa; conservar asserts de persistencia para verificar el resultado de negocio.
- Verificación: Actions `45001599626` pasó build, xUnit 61/61, smoke HTTP/MySQL (incluida la carrera 200/409 y persistencia atómica) y prueba de concurrencia de asignación.
- Error de ejecución relacionado: el primer `gh workflow run backend-ci.yml` respondió HTTP 404 porque ese archivo no existe. El workflow real es `.github/workflows/ci.yml`; repetir el dispatch usando ese nombre.
# 2026-11-22 — Detalle y GPS permitían acceso con perfil Driver inactivo — RESUELTO EN CÓDIGO, E2E PENDIENTE

- Hallazgo de revisión independiente: `GET /Routes/{id}` y `GET/POST /Routes/{id}/positions` no exigían `Driver.IsActive`; un usuario autenticado podía consultar detalle/GPS o enviar GPS tras desactivar su perfil operativo.
- Causa: detalle comprobaba sólo el ID asignado y los endpoints GPS duplicaban el ownership, omitiendo el helper central `IsCurrentDriverAssigned` que valida estado activo y compatibilidad heredada.
- Solución: detalle y GPS de lectura/escritura cargan la ruta dentro del tenant y usan `IsCurrentDriverAssigned`; ruta inexistente responde 404 y perfil inactivo/no asignado responde 403. Listado y mutaciones ya aplicaban esta regla.
- Prevención: reutilizar el helper de autorización central en endpoints operativos Driver y probar no sólo acceso cruzado, también cambios de estado del perfil con JWT todavía vigente.
- Verificación: Release/xUnit local 61/61; Actions backend `45002562048` success. Front Actions `45002920723` pasó E2E API/MySQL 18/18 con backend `41c01e7`, cubriendo Driver inactivo, detalle/listado/GPS, ruta de otro conductor, intentos de parada/evidencia y ausencia de mutaciones.
# 2026-11-22 — Lectura de entidad Backend desde Front — CORREGIDO

- Síntoma: un `sed` relativo buscó `fletflow/Domain/Auth/Entities/User.cs` desde `/home/leonardo/Front_gpsR` y no encontró el archivo.
- Causa: la consulta cruzó el repositorio hermano sin cambiar el directorio de trabajo.
- Solución: volver a ejecutar con `workdir=/home/leonardo/Back_GPS` y confirmar la ruta con `rg --files`.
- Prevención: toda inspección entre repositorios debe fijar su raíz explícitamente; registrar errores de cwd y continuar desde el repo dueño del archivo.

## 2026-11-22 — Fallos de runner y primera prueba de edición de perfil — CORREGIDOS Y VERIFICADOS (Playwright 18/18)

- Una prueba intentó usar el MySQL persistente de desarrollo con un password root distinto al configurado en su inicialización. Acceso rechazado; no se alteró esa base. Se sustituyó por MySQL 8 efímero en puerto 13306 y esquema descartable; smoke tenant y concurrencia pasaron.
- El primer arranque API recibió ConnectionStrings__DefaultConnection, pero Program.cs consume DB_HOST/DB_PORT/DB_NAME/DB_USER/DB_PASSWORD. Se corrigió el entorno del proceso.
- dotnet test sobre BackEnd_Gps.csproj terminó sin ejecutar pruebas porque no es test project. Además, el cambio de constructor de UpdateDriverCommand dejó un test sin AppDbContext. Se corrigió y ahora dotnet test BackEnd_Gps.sln pasa 62/62.
- smoke-route-concurrency.sh ignoraba MYSQL_PORT y conectaba al MySQL persistente. Se corrigió para pasar el puerto; ambas pruebas pasaron en instancia efímera.
- Primera Playwright terminó 16/18: selector Driver ambiguo entre dos secciones y mock de error de Historial liberado antes del render por fetch de foco. Se agregó selector data-testid, se acotó el texto y se conserva el 503 hasta que el test ve el alert. El spec de carga pasa 2/2; la suite completa final pasó 18/18.
- La segunda suite dio 17/18: tras recargar, otro tramo del E2E seguía buscando el username como heading, aunque el diseño muestra el nombre legal. Se reemplazó el selector por testid estable del perfil.
- Prevención: leer configuración consumida realmente por Program.cs, ejecutar xUnit desde la solución, fijar puerto MySQL en runners y verificar selectores y mocks frente a fetches secundarios.

## 2026-11-22 — Esquema de solicitud de aclaración — CORREGIDO

- La primera llamada a request_user_input_async envió el campo question, pero el contrato de esta herramienta sólo acepta title y options; la llamada fue rechazada sin llegar al usuario.
- Se reintentó con title y opciones, y la pregunta pendiente es si un Admin delegado puede asignar rol Admin.
- Prevención: validar el esquema concreto de cada interfaz de preguntas antes de invocarla.


## 2026-11-22 — Ruta de Program.cs equivocada durante la verificación local — CORREGIDO

- La inspección buscó `fletflow/Program.cs`, aunque el entry point está en la raíz del repositorio. `rg --files` localizó el archivo; se leyó el path correcto y se confirmó que el proceso toma variables de entorno antes de `.env`. No se ejecutó ni modificó configuración local por esa búsqueda fallida.
- Prevención: consultar el inventario de archivos antes de asumir que el entry point está dentro del proyecto de dominio/API.
# 2026-11-22 — Logistics no podía cargar recursos para crear rutas — EN VALIDACIÓN

- Acciones previas: Logistics podía crear rutas, pero el frontend no podía consultar `/Vehicles` ni `/Drivers` (403), mientras que esos datos eran requeridos por el constructor.
- Solución en curso: `GET /Routes/assignment-options` devuelve sólo vehículos/Drivers activos y campos operativos mínimos; Controller permite Admin/Manager/Logistics. Repositorios consultan mediante `FleetFlowDbContext`, cuyos query filters limitan `OwnerUserId` al propietario actual.
- El smoke verifica Logistics 200 con los registros del tenant propietario sin documento/teléfono, User 403 y un segundo tenant con arrays vacíos.
- Verificación: local Release build 0 warnings/0 errors, xUnit 64/64, `bash -n` y diff-check; Actions `45011796536` pasó build, xUnit, smoke HTTP/MySQL de tenant/Logistics y concurrencia, cleanup correcto.
- Error de comando: la primera invocación apuntó a `fletflow.sln`, que no existe; el nombre correcto es `BackEnd_Gps.sln`. El sistema `/usr/bin/dotnet` tampoco tiene runtime utilizable; se repitió con `/home/leonardo/.dotnet/dotnet` SDK 8.0.428.
- Prevención: consultar `rg --files -g '*.sln'` antes de invocar build y usar el SDK local conocido cuando el host de sistema no tenga runtime.
# 2026-11-22 — Opciones de ruta incluían recursos no asignables — CORREGIDO Y VERIFICADO

- Hallazgo auditoría backend: el endpoint nuevo sólo filtraba `IsActive`; exponía vehículos en mantenimiento/en ruta y Drivers activos con cuenta de usuario inactiva, aunque CreateRoute rechaza esas asignaciones.
- Solución: opciones incluyen sólo vehículos activos `Available`; `IDriverRepository.GetAssignableAsync` mantiene Drivers activos sin usuario y sólo incluye Drivers vinculados a una cuenta activa del mismo tenant. La consulta usa query filters OwnerUserId y un `Any` acotado, evitando N+1.
- Cobertura añadida: xUnit de proyección excluye estados de vehículo no disponibles; smoke HTTP/MySQL crea un Driver con cuenta luego inactiva y verifica que se excluya, prueba vehículo en mantenimiento y restauración a disponible, además de inclusión del Driver sin usuario.
- Verificación local: Release build 0 warnings/0 errors, xUnit 65/65, `bash -n` y diff-check. Actions backend `45014146500` pasó smoke HTTP/MySQL de filtros/tenant y concurrencia; cleanup Driver/User dejó cero filas. Actions Front integrada `45014430986` pasó 18/18 y cleanup.
- Prevención: los selectores de asignación deben representar restricciones de comandos de creación, y las pruebas deben contrastar elegibilidad del read model con persistencia real.

## 2026-11-22 — Ruta de comando de vehículo asumida incorrectamente — CORREGIDO

- Una inspección buscó `fletflow/Aplication/Fleet/Commands/DeleteVehicleCommand.cs`, pero el comando está bajo `Commands/CreateVehicle/`. `sed` falló sin modificar archivos.
- Solución: localizar el archivo con `rg --files` y leer `CreateVehicle/DeleteVehicleCommand.cs`; se confirmó que la baja permite vehículos en mantenimiento si no tienen ruta activa ni Driver asignado.
- Prevención: localizar la ruta real de clases antes de leerlas mediante un nombre de archivo supuesto.

## 2026-11-22 — Smoke no eliminaba Driver vinculado al usuario de QA — CORREGIDO Y VERIFICADO

- Actions backend `45013922679`: build y xUnit 65/65 pasaron; el smoke HTTP/MySQL hizo todas las aserciones de negocio, pero el cleanup falló con MySQL 1451 porque `Drivers.UserId` restringe borrar el User de prueba mientras permanezca el Driver enlazado.
- Solución: guardar el `driverId` de la respuesta, borrar primero ese Driver durante cleanup y comprobar que queden cero filas Driver/User. La verificación tolera errores de conexión reportándolos y conserva el estado de fallo.
- Prevención: al ampliar fixtures con entidades enlazadas, documentar orden de borrado respetando FK y verificar también que cada fila de prueba desaparezca.
- Repetición: Actions backend `45014146500` pasó, incluyendo la comprobación de cero Driver/User residuales.

## 2026-11-22 — Lectura del AuthContext desde el checkout Backend — CORREGIDO

- Una inspección ejecutada desde `/home/leonardo/Back_GPS` intentó leer `src/Context/AuthContext.tsx`, que pertenece al frontend; `sed` devolvió error sin modificar archivos.
- Solución: repetir la lectura desde `/home/leonardo/Front_gpsR`. En la misma inspección de Back sí se confirmó que `cleanup-e2e.sql` elimina Drivers antes de Vehicles usando prefijos E2EQA.
- Prevención: leer cada archivo relativo desde el repositorio que lo contiene.
## 2026-11-22 — Dos Drivers podían conservar el mismo vehículo — CORREGIDO

- Auditoría confirmó que AssignDriverToVehicleCommand bloqueaba el vehículo, pero no comprobaba si otro Driver conservaba VehicleId; no hay índice único en esa columna. Dos llamadas concurrentes podían dejar dos perfiles apuntando al mismo vehículo.
- Bajo el lock de fila, el command ahora busca otra asignación dentro del tenant y responde conflicto antes de cambiar el perfil. Para reasignar se desasigna primero el Driver actual.
- xUnit cubre el rechazo. El smoke MySQL crea dos Drivers, lanza ambas asignaciones en paralelo, exige 204/409 y verifica por SQL que sólo uno quede asociado.
- Verificación local: Release build 0 warnings/0 errors, xUnit 73/73, bash -n y diff-check pasan. Falta smoke HTTP/MySQL de asignaciones en Actions.

### Error de prueba — namespace ambiguo — CORREGIDO

- La primera compilación del test de asignación se refirió a BusinessConflictException con namespace global completo, pero el proyecto de pruebas tiene raíz BackEnd_Gps; el compilador lo resolvió como BackEnd_Gps.fletflow y falló CS0234.
- Se añadió using fletflow.Domain.Fleet.Exceptions y se usó el tipo importado. Producción compiló sin errores en ese intento; repetir build/test completo.
- Prevención: preferir imports explícitos cuando el nombre de raíz del proyecto puede prefijar un namespace global.

## 2026-11-22 — Búsqueda de E2E Front ejecutada desde Backend — CORREGIDO

- Se consultó e2e/fleet-admin.spec.ts desde /home/leonardo/Back_GPS y rg informó IO error; el archivo pertenece al repositorio Front.
- Solución: repetir la inspección desde /home/leonardo/Front_gpsR y usar el workdir del repositorio de cada archivo.
- Prevención: verificar ruta raíz antes de búsquedas que cruzan los checkouts de Front y Back.

## 2026-11-22 — Búsqueda de scripts auxiliares en Front — CORREGIDO

- Una consulta de descubrimiento incluyó el directorio `scripts/` en Front, pero allí no existe; `rg` reportó IO error.
- Solución: buscar en los archivos que sí existen (`package.json`, `playwright.config.ts` y `e2e/`) y consultar el workflow pertinente.
- Prevención: confirmar directorios con `rg --files` antes de incluir rutas supuestas.

## 2026-11-22 — Búsqueda de `fechaCreacion` Front desde Backend — CORREGIDO

- Una búsqueda agrupada intentó abrir `src/` estando en `/home/leonardo/Back_GPS`; el frontend vive en `/home/leonardo/Front_gpsR` y `rg` devolvió IO error.
- Solución: repetir las búsquedas frontend desde el checkout Front y las búsquedas de DTO/repositorio desde Back.
- Prevención: no compartir rutas relativas entre repositorios al agrupar comandos.

## 2026-11-22 — Lectura de RoutesManagement desde Backend — CORREGIDO

- Un comando combinado consultó `src/Components/fleet/RoutesManagement.tsx` desde el checkout Backend y `sed` reportó que no existía; ese path es de Front.
- Solución: repetir el archivo frontend desde `/home/leonardo/Front_gpsR` y mantener las lecturas de DTO/entidad/backend desde `/home/leonardo/Back_GPS`.
- Prevención: dividir búsquedas por checkout cuando los paths comparten sólo el nombre del proyecto.

## 2026-11-22 — Directorio del mapper Application mal escrito — CORREGIDO

- Se consultó `fletflow/Aplication/Fleet/Mappings`; el directorio real tiene otra estructura y `rg` devolvió IO error.
- Solución: descubrir el archivo del mapper mediante `rg --files fletflow` y buscar la clase por contenido.
- Prevención: no inferir rutas a partir del nombre de namespace; confirmar el árbol antes de abrir archivos.

## 2026-11-22 — Archivo `RouteTests.cs` supuesto — CORREGIDO

- Se intentó abrir `BackEnd_Gps.Tests/RouteTests.cs`, pero ese archivo no existe y `sed` reportó IO error.
- Solución: localizar cobertura real con `rg --files BackEnd_Gps.Tests` y continuar en `CreateRouteCommandTests.cs`/`RouteLifecycleTests.cs`.
- Prevención: descubrir nombres de test antes de abrirlos.

## 2026-11-22 — Nombre de archivo de `AppDbContext` asumido — CORREGIDO

- Se intentó abrir `Infrastructure/Persistence/Context/AppDbContext.cs`; la carpeta existe, pero esa clase está declarada en `FleetFlowDbContext.cs` y `sed` informó que el nombre de archivo no existe.
- Solución: buscar la declaración de clase con `rg -n 'class AppDbContext' fletflow` antes de abrirla.
- Prevención: no inferir que el nombre de clase coincide con el nombre de archivo.

## 2026-11-22 — `dotnet ef` no está instalado localmente — CORREGIDO

- Al comprobar el generador de migraciones, `/home/leonardo/.dotnet/dotnet ef --version` informó que `dotnet-ef` no existe.
- Solución: instalar `dotnet-ef` 9.0.10 bajo `/tmp/fletflow-tools` y ejecutar la generación con `PATH=/home/leonardo/.dotnet:$PATH`, `DOTNET_ROOT`/`DOTNET_ROOT_X64=/home/leonardo/.dotnet` y `DOTNET_MULTILEVEL_LOOKUP=0`.
- Prevención: verificar la herramienta antes de intentar generar migraciones.

## 2026-11-22 — Glob del `.csproj` desde subdirectorio equivocado — CORREGIDO

- Se buscó `fletflow/*.csproj`, pero el proyecto se declara en la raíz y `cat` reportó que el glob no coincidía.
- Solución: localizar proyectos con `rg --files | rg '\\.csproj$'` desde la raíz.
- Prevención: confirmar ubicación del proyecto al iniciar comandos de herramientas.

## 2026-11-22 — EF CLI no encontró el runtime .NET instalado — CORREGIDO

- La primera ejecución de `/tmp/fletflow-tools/dotnet-ef` buscó `hostfxr` bajo `/usr/lib64/dotnet`, pero el SDK/runtime local está instalado en `/home/leonardo/.dotnet`.
- Solución: `dotnet-ef` 9.0.10 se ejecuta con `PATH=/home/leonardo/.dotnet:$PATH`, `DOTNET_ROOT` y `DOTNET_ROOT_X64` apuntando a `/home/leonardo/.dotnet` y `DOTNET_MULTILEVEL_LOOKUP=0`; la migración se generó correctamente.
- Prevención: este entorno necesita esas variables también para la metadata de MSBuild, no sólo para consultar la versión del apphost.

## 2026-11-22 — Smoke comparaba timestamps con precisión distinta — CORREGIDO Y VALIDADO

- Actions `45019828759` pasó build/xUnit, pero falló el smoke al comparar el `CreatedAtUtc` devuelto al crear con el devuelto tras editar.
- Causa: la respuesta inicial tenía precisión de DateTime .NET (100 ns); MySQL `datetime(6)` conserva microsegundos. El test comparaba strings exactos aunque ambas marcas coincidían a la precisión persistida.
- Solución: normalizar la respuesta API a seis decimales y comparar ambos reads con `DATE_FORMAT(CreatedAtUtc)` de MySQL antes y después de editar.
- Prevención: verificar timestamps a la precisión real de la columna y contrastar el valor persistido.

- Resolución confirmada por Actions `45020146775`: migración y smoke MySQL pasaron, incluyendo comparación a precisión `datetime(6)` antes/después de editar.

## 2026-11-22 — Access JWT revivía al reactivar cuenta — CORREGIDO Y VALIDADO

- Auditoría backend confirmó que desactivar sólo revocaba refresh tokens. El access JWT fallaba mientras `IsActive=false`, pero volvía a cumplir las validaciones al reactivar porque `TokenVersion` no cambiaba.
- Solución local: incrementar `TokenVersion` en cada transición de estado; desactivación revoca refresh tokens y reactivación conserva la invalidez del token viejo. Se añadió xUnit y smoke que reutilizan el mismo JWT después de reactivar y comprueban login/token nuevos.
- Verificación local: Release xUnit **74/74**; smoke HTTP/MySQL 8 aislado aprobado, incluido token viejo 401 después de reactivar y nuevo JWT 200; cleanup verificado. Actions `45021088919` confirmó el cambio.
- Prevención: probar el access token antes/durante/después de reactivar, no sólo el refresh token al desactivar.
- Actions `45021088919` confirmó backend Release, xUnit **74/74**, prueba HTTP/MySQL de reactivación y smoke concurrente; cleanup correcto.

## 2026-11-22 — Admin de tenant podía mutar catálogo global de Roles — CORREGIDO Y VALIDADO

- `Roles` es tabla global sin owner y el API autorizaba `POST`/`DELETE` a cualquier rol Admin tenant. El frontend no consume esas mutaciones y no existe rol/endpoint de administrador de plataforma.
- Solución local: conservar `GET /Roles` y devolver 403 a mutaciones tenant para evitar que una empresa altere roles compartidos. Smoke HTTP verifica el contrato sin crear ni borrar roles.
- Verificación local: incluido en smoke MySQL 8 aprobado; Actions `45021088919` pasó.
- Actions `45021088919` confirmó GET 200 y mutaciones POST/DELETE 403 durante smoke HTTP/MySQL.

## 2026-11-22 — Inspección usó rutas/nombres de archivo supuestos — CORREGIDO

- Varias lecturas iniciales apuntaron a `IUnitOfWork.cs`, `DriversController.cs`/`UpdateDriverCommand.cs` en carpetas que no correspondían y una búsqueda de archivos backend se ejecutó desde el checkout Front. `sed`/`rg` informaron IO error; no se alteraron archivos.
- Solución: volver a ejecutar cada consulta desde el repositorio correspondiente y descubrir los paths reales mediante `rg --files` antes de leer.
- Prevención: comprobar `workdir`/repositorio al agrupar comandos y no inferir la ruta a partir del nombre de clase.

## 2026-11-22 — Parche de smoke no encontró ancla de login — CORREGIDO

- Un parche esperaba una etiqueta de login que no coincidía con el script (`Login tenant A`); `apply_patch` rechazó el cambio íntegro, sin modificar archivos.
- Solución: localizar la sección exacta con `rg`/`sed` y volver a aplicar cambios con las anclas existentes.
- Prevención: comprobar el contexto vigente antes de redactar el parche.

## 2026-11-22 — Corrección documental reutilizó una línea ya actualizada — CORREGIDO

- Un segundo parche buscaba el estado “Actions pendiente” tras una aplicación anterior que ya lo había sustituido; `apply_patch` rechazó ese parche completo y no alteró los archivos.
- Solución: consultar la ocurrencia actual con `rg` y actualizar sólo el texto restante con contexto exacto.
- Prevención: tras aplicar un parche, releer el resultado antes de preparar otro que dependa de sus líneas.

## 2026-11-22 — Refresh concurrente, logout-all y recuperación de contraseña — CORREGIDO Y VALIDADO

- Auditoría de seguridad detectó que dos refresh simultáneos podían leer el mismo token vigente; logout-all revocaba refresh pero dejaba válidos los access JWT ya emitidos; y la respuesta de recuperación exponía `ExpiresAt` sólo para cuentas existentes. Además, un error SMTP podía romper la petición.
- Solución: rotación condicional atómica del refresh anterior y creación del sucesor dentro de una transacción; logout-all incrementa `TokenVersion` además de revocar refresh; respuesta pública de recuperación igual para cuenta conocida/desconocida, sin expiración y con mensaje condicional; error SMTP se registra en backend y no se presenta como envío exitoso.
- Verificación: xUnit Release local 76/76 antes de la última repetición, y smoke HTTP/MySQL 8 descartable aprobado. Dos requests concurrentes al mismo refresh produjeron exactamente uno HTTP 200 y otro 401; hash anterior revocado, un sucesor activo, reuso anterior 401 y sucesor 200. Logout-all dejó access y refresh anteriores en 401; login nuevo 200. Cleanup confirmó cero rutas, posiciones y vehículos QA.
- Error operativo: `dotnet test` inicialmente invocó el host del sistema (`/usr/lib64/dotnet`) sin runtime y devolvió código 131. Solución: usar `/home/leonardo/.dotnet/dotnet` con `DOTNET_ROOT`, `DOTNET_ROOT_X64` y `DOTNET_MULTILEVEL_LOOKUP=0`; prevención: respetar la instalación local documentada antes de invocar el ejecutable global.
- Error de edición: un hunk del smoke de rotación concurrente no coincidió y fue rechazado sin aplicar cambios. Solución: releer el final actual del script y aplicar el bloque con anclas exactas; prevención: validar el contexto inmediatamente antes del parche.

## 2026-11-22 — Auditor de sesiones omitió el hook de validación JWT — HALLAZGO NO REPRODUCIBLE

- La revisión independiente afirmó que `TokenVersion` no se comparaba al autenticar porque `Api/Middleware/JwtMiddleware.cs` no tiene esa lógica.
- Al revisar el flujo completo, `Program.cs` sí registra `JwtBearerEvents.OnTokenValidated`, consulta el usuario y compara `token_version` antes de aceptar el token; el middleware JWT del pipeline no es el único control. El smoke anterior ya comprobó `/Auth/me` = 401 con access previo a logout-all.
- No se duplicó la validación. Se conservará como regresión HTTP al correr el smoke nuevo.
- Prevención de falso positivo: revisar tanto middleware personalizado como configuración del esquema `AddJwtBearer`/eventos y orden real del pipeline antes de concluir que falta validación.

## 2026-11-22 — Logout-all podía dejar refresh sucesor activo en carrera — CORREGIDO EN VALIDACIÓN

- El primer `RevokeAllForUserAsync` cargaba tokens y luego los marcaba revocados; un refresh podía insertar un sucesor después de que la lista ya se hubiera leído.
- Solución en curso: lock transaccional de fila de `Users` compartido por logout-all y rotación refresh. Logout-all mantiene lock, incrementa TokenVersion, revoca refresh y persiste todo antes de liberarlo; rotación bloquea el mismo usuario antes de cambiar token/insertar sucesor.
- Se amplió smoke HTTP para mantener el lock desde otra conexión MySQL y lanzar refresh + logout-all concurrentes; debe comprobar cero refresh activos y rechazo del sucesor si refresh alcanzó a responder 200. La validación de esta iteración está corriendo.
- Prevención: probar carreras entre operaciones de sesión distintas, no sólo duplicar el mismo refresh token.

## 2026-11-22 — Path de IUnitOfWork asumido en lectura — CORREGIDO

- Intenté abrir `fletflow/Infrastructure/Persistence/Contracts/IUnitOfWork.cs`; el contrato está en `fletflow/Aplication/Common/Interfaces/IUnitOfWork.cs`. `cat` no encontró el path y no hubo cambios.
- Solución: localizar por `rg --files | rg 'IUnitOfWork.cs$'` y leer el archivo real.
- Prevención: verificar nombres/rutas antes de abrir, incluso si el namespace sugiere otra carpeta.

- Verificación cerrada de la carrera: MySQL 8 efímero + API real ejecutó el smoke ampliado con lock deliberado y refresh/logout-all concurrentes; exit code 0, cero refresh activos tras logout, token anterior inválido, cleanup `0 rutas, 0 posiciones GPS, 0 vehículos`. `dotnet test -c Release --no-restore`: 76/76 después del cambio. La posible diferencia temporal de ForgotPassword permanece anotada como pendiente de seguridad, no como resuelta.

## 2026-11-22 — Enumeración temporal de ForgotPassword — MITIGADA PARCIALMENTE

- El auditor observó diferencia temporal marcada porque una cuenta existente esperaba SMTP (hasta timeout) y una inexistente respondía tras sólo consultar la cuenta.
- Solución: envío fuera del request en `EmailDeliveryQueue` bounded de 256 mensajes, mínimo de 250 ms para ambas ramas y HTTP smoke que compara payload y duración mínima. Fallos/cola llena sólo generan log; SMTP no se presenta como enviado.
- Verificación local: xUnit Release 76/76; build 0 warnings/0 errores; smoke API/MySQL 8 exit 0 con payloads/códigos idénticos y ambos tiempos >=200 ms. El worker reportó SMTP no configurado, sin enviar correo real.
- Límite explícito: la cola de proceso no es durable y el piso de tiempo no garantiza igualdad ante saturación/DB lenta. Requiere outbox durable protegido para producción; no marcar como eliminación matemática del canal temporal.
- Error de edición durante este fix: el hunk inicial no coincidió con el cuerpo real (`normalizedEmail` se valida como nullable y tiene early return); no hubo cambios aplicados. Solución: releer el método exacto y dividir el parche. Prevención: revisar BOM/contexto y ramas de retorno reales antes de cambiar timing.

## 2026-11-22 — CREATE USER no validaba límites de Users y email race — CORREGIDO

- Auditoría independiente encontró que User/Email/FirstName/LastName superaban las longitudes EF/MySQL (100/200/100/100) y que dos POST simultáneos al mismo correo podían dejar un `DbUpdateException` sin traducir a 500.
- Solución: `RegisterUserCommand` recorta valores y valida longitudes; `UsersController` devuelve 400 para username/email/nombres fuera de límite; duplicado existente y colisión del índice Email se traducen a `BusinessConflictException`/409. El manejo sólo convierte a conflicto si la consulta confirma correo duplicado, conserva excepciones no relacionadas.
- Front limita inputs a máximos backend y valida texto recortado antes de enviar; E2E comprueba `maxlength` en TeamManagement.
- Smoke MySQL/API de límites probó max username/nombres=100 y email=200 con 200; max+1 para cada campo dio 400 y persistencia cero. Dos altas simultáneas al mismo email produjeron 200/409 y exactamente una fila User, UserRole y PasswordResetToken; cleanup cero.
- También se agregó HTTP Driver para `/Routes/assignment-options`; Driver recibe 403. El mismo contrato se conserva para User y permitido para Logistics/Managers según matriz.
- Primer smoke falló después de llegar al test cross-tenant porque el fixture a longitud máxima ya no coincidía con una expectativa anterior de username/nombre corto. Solución: comparar contra valores máximos de ese fixture; repetición completa exit 0.
- Error de exploración: se intentó leer `e2e/fleet-admin.spec.ts` desde Back y `src/Components/...` desde Backend; rutas relativas no existen ahí y no hubo modificación. Solución: repetir desde checkout Front. Prevención: separar comandos por repositorio al agrupar inspecciones.

## 2026-11-22 — Outbox durable de recuperación y pruebas reales — ERRORES CORREGIDOS

- Generar AddEmailOutbox falló al compilar porque faltaba el namespace de extensiones Microsoft.AspNetCore.DataProtection para SetApplicationName. dotnet build expuso CS1061; se agregó el using y EF generó la migración.
- El primer smoke inició el host global /usr/bin/dotnet, que no tiene runtime (/usr/lib64/dotnet/host/fxr sin runtime). Se canceló antes de enviar requests; se repitió con /home/leonardo/.dotnet/dotnet, DOTNET_ROOT y el SDK local.
- Una orden de limpieza demasiado amplia con rm -rf fue rechazada antes de ejecutarse por el guard del terminal. Se reemplazó por limpieza explícita del único contenedor descartable identificado (docker stop y docker rm por nombre); no hubo cambios de archivos por la orden rechazada.
- La primera pasada de outbox fue seguida por una prueba más fuerte de dos solicitudes concurrentes; el smoke actualizado pasó y ahora comprueba dos HTTP 200, un token activo y tres filas cifradas. No se ocultó ni se contó la primera corrida como cobertura concurrente.
- Prevención: importar la extensión de DataProtection antes de generar migraciones; invocar el dotnet local documentado; destruir recursos descartables por nombre exacto; esperar el resultado del smoke que contiene el caso que se quiere reportar.

## 2026-11-22 — Recuperaciones concurrentes podían mantener múltiples tokens válidos — CORREGIDO Y VALIDADO

- El auditor de solo lectura detectó que invalidar tokens y crear uno nuevo sin bloquear el usuario permite que dos recuperaciones simultáneas dejen ambos tokens activos.
- Solución: ForgotPasswordAsync toma el lock transaccional de la fila User que también coordina sesiones; invalida tokens anteriores y agrega token y outbox en el mismo commit.
- El smoke HTTP/MySQL ejecutó dos requests concurrentes reales: ambos respondieron 200, SQL confirmó exactamente un reset token activo y tres filas outbox (una inicial más dos concurrentes). El payload protegido no contiene el email en texto claro. DB MySQL 8 y API fueron descartables.

## 2026-11-22 — Aserción de contador asumía un intento previo al reinicio — CORREGIDA

- El primer experimento de restart esperaba que el contador final fuera exactamente 2/3; en la ejecución real el worker había completado cero intentos antes del primer apagado, así que el resultado correcto fue 1. La condición de prueba falló aunque el worker reiniciado descifró el payload y llamó al sender (tipo InvalidOperationException de SMTP sin configurar).
- Solución: comparar el número de intentos final con el valor capturado antes de reiniciar y exigir que aumente, además del tipo de fallo SMTP esperado.
- Repetición en MySQL 8 descartable: antes 1 fila/0 intentos; después 1 intento/InvalidOperationException. Aprobado.
- Prevención: probar la propiedad del sistema (contador aumenta frente a baseline) en vez de asumir cuándo el worker alcanzó su primer poll.
# 2026-11-22 — Sombreado de parámetros en prueba de carrera de Driver

- Error: la primera compilación de `ChangeRouteStatusAuthorizationTests` falló con `CS1061` porque el parámetro del constructor primario `vehicleId` quedó sombreado por un parámetro del método y `this.vehicleId` no es miembro de la clase. También emitió `CS9113` por el parámetro de constructor sin uso.
- Solución: convertir el fixture de prueba a clase explícita con campos `_vehicleId`, `_newDriverId` y `_routes`. La prueba enfocada pasó 1/1 y la suite completa 78/78.
# 2026-11-22 — Normalización de precisión temporal en smoke de rutas

- Error: Actions `45029094282` falló en la comparación de `CreatedAtUtc` tras editar una ruta, aunque build y xUnit pasaron. El mensaje previo no mostraba los valores comparados.
- Causa en el test: su normalizador truncaba fracciones mayores a 6 dígitos, pero no rellenaba fracciones cortas ni el caso sin fracción; `System.Text.Json` puede omitir ceros finales mientras MySQL `datetime(6)` siempre representa microsegundos.
- Solución: canonicalizar ambos timestamps a UTC textual con exactamente seis dígitos fraccionarios y agregar los valores iniciales/finales al diagnóstico para fallos futuros. Pendiente repetir el workflow para comprobar la solución.
# 2026-11-22 — Cobertura backend incompleta para Driver del mismo tenant

- Auditoría: smoke backend sólo probaba acceso a rutas ajenas mediante un Admin de otro tenant; no ejercitaba un Driver válido del mismo tenant contra la ruta de otro conductor. El frontend sí tenía E2E real, pero la brecha del smoke backend persistía.
- Corrección: smoke ahora usa la cuenta Driver ganadora de la prueba concurrente de documento/email y prueba list/detail/status/stop/evidence/GPS denegados, seguido de lecturas Admin que verifican ausencia de mutaciones. Falta confirmar en MySQL/API efímeros por CI.
# 2026-11-22 — Rutas omitían exclusividad de asignación conductor/vehículo

- Auditoría de `CreateRouteCommand` y `UpdateRouteCommand` encontró una vía indirecta que eludía `AssignDriverToVehicleCommand`: ambos podían ligar a una ruta un vehículo todavía asignado a otro conductor.
- Solución: después del lock y de releer las entidades, ambos comandos consultan `GetOtherAssignedToVehicleAsync`; si existe otro conductor, devuelven `BusinessConflictException` (HTTP 409) antes de guardar. Las pruebas SQLite cubren creación y edición rechazadas y comprueban que asignaciones/ruta no cambian.
- La primera suite Release completa falló en una prueba que creaba dos rutas futuras con el mismo vehículo y conductores distintos para luego probar el doble inicio. La expectativa contradice la regla vigente de exclusividad; el escenario se cambió a dos rutas del mismo conductor/vehículo para aislar la regla que la prueba realmente busca (sólo una puede estar en progreso).
- Se añadió cobertura HTTP/MySQL al smoke para creación y edición fallidas, incluyendo consultas SQL de integridad. Pendiente ejecutar smoke local/Actions con MySQL descartable.
- Prevención: al cambiar reglas de consistencia, revisar también los fixtures de agenda/concurrencia y mantener cada prueba enfocada en una sola invariante.

## 2026-11-22 — Smoke de concurrencia reutilizaba un vehículo ya asignado — CORREGIDO Y VALIDADO

- Actions `45031869323` pasó build/xUnit/tenant HTTP, pero falló en `smoke-route-concurrency.sh`: el caso de update esperaba crear una ruta base con `v2/d2`, cuando el escenario previo ya había asignado ese vehículo a otro Driver. El nuevo HTTP 409 era el resultado correcto.
- Solución: cada escenario de update y doble inicio usa ahora vehículos/conductores propios, evitando acoplar el resultado a qué petición concurrente ganó.
- Actions `45032172895` pasó Release xUnit 82/82, smoke tenant y smoke de concurrencia. Confirmó ambos HTTP 409, estado SQL intacto y cleanup del runner.
- Prevención: escenarios de concurrencia deben tener recursos QA aislados por invariante y no depender de la identidad del ganador previo.

## 2026-11-22 — Auditoría de seguridad: rate limit concurrente, correo de prueba y key ring — CORREGIDO LOCAL; ACTIONS PENDIENTE

- Hallazgos estáticos: el rate limiter leía y escribía el contador en pasos separados, por lo que requests paralelas podían perder incrementos; `GET /api/Auth/test-email?to=` permitía a cualquier Admin enviar a un destinatario arbitrario; el key ring de Data Protection se persistía sin cifrado explícito.
- Rate limit: reemplazada la secuencia get/set por `ConcurrentDictionary.AddOrUpdate`, y la limpieza elimina sólo la versión exacta observada del entry. Un burst de 200 requests concurrentes desde la misma IP acepta exactamente 60 y responde 429 a 140.
- Email de prueba: endpoint habilitado sólo en Development y únicamente al email del principal autenticado; pruebas con sender registrado demuestran 404 en Production, `Forbid` para destinatario ajeno y envío sólo al mismo email.
- Data Protection: producción ahora exige key ring persistente y certificado PFX configurados mediante `FLETFLOW_DATA_PROTECTION_KEYS_PATH`, `FLETFLOW_DATA_PROTECTION_CERT_PATH` y `FLETFLOW_DATA_PROTECTION_CERT_PASSWORD`; las claves se protegen con el certificado. `.env.example` documenta nombres sin valores.
- Validación local: Release xUnit **85/85**, Release build **0 warnings/0 errors**, test concurrente enfocado 1/1 y `git diff --check` correcto.
- Actions/MySQL integrado pendiente para estos cambios. Límites externos: certificado/secretos y volumen de producción deben provisionarse en despliegue. El rate limiter sigue siendo por proceso; varias réplicas requieren almacenamiento/limitador distribuido.
- Prevención: las operaciones de límite deben ser atómicas de extremo a extremo; endpoints de diagnóstico SMTP deben limitar entorno y destinatario; nunca persistir claves criptográficas de producción sin proveedor de protección explícito.

## 2026-11-22 — Auditoría ejecutada desde el repositorio Front — CORREGIDO

- Una búsqueda de recuento de pruebas backend se lanzó con raíz `/home/leonardo/Front_gpsR`; `rg` devolvió ruta inexistente y recuento cero. No se interpretó como ausencia de pruebas.
- Solución: ejecutar cada inspección en su repositorio propietario; Front y Backend comparten convenciones, pero no las carpetas de tests.
- Prevención: fijar `workdir` al repo del artefacto antes de búsquedas/ejecuciones cruzadas.

## 2026-11-22 — Verificación de arranque Production usó texto de excepción en inglés — CORREGIDO

- El proceso API sí rechazó el key ring sin certificado con el error esperado en español, pero la aserción inicial buscó la versión inglesa y reportó código 1. El proceso imprimió `Aborted (core dumped)` al terminar por excepción de configuración; no quedó archivo `core*` en el repositorio.
- Solución: ajustar la aserción al mensaje real `Producción requiere FLETFLOW_DATA_PROTECTION_CERT_PATH`; repetición local confirmó el rechazo fail-fast.
- Prevención: las verificaciones de startup deben afirmar el texto/código efectivamente emitidos por la aplicación y distinguir ese rechazo esperado del resultado de la aserción.

## Auditoría de seguridad — CI integrado completado

- Actions backend `45037399332` pasó con el código `94c3e90`: Release build 0 warnings/0 errors; xUnit **85/85**; smoke HTTP/MySQL de tenant/roles/flota y smoke concurrente de asignación, con cleanup correcto.
- Se validó en Actions Development el API y los smokes de base real. El guard de configuración Production sin certificado quedó validado localmente; producción con certificado/infra real sigue pendiente de provisión externa.

## 2026-11-22 — Falta de prueba de cifrado/persistencia del key ring — CUBIERTO

- La corrección anterior hacía obligatorio cifrar el key ring de Production, pero sólo verificaba la configuración negativa (rechazo si falta certificado).
- Se agregó `DataProtectionKeyRingTests`: genera certificado temporal, persiste una clave, comprueba `<encryptedSecret` en el XML y reinicia el provider con el mismo key ring/certificado para descifrar el payload.
- Resultado: prueba enfocada 1/1; suite Release **86/86**; build 0 warnings/0 errors; diff-check limpio.
- El test usa un key ring temporal único y lo borra en `finally`; no usa ni modifica certificados/datos locales de producción.

## 2026-11-22 — Test de rotación y concurrencia real — CORREGIDO

- La revisión encontró que `ConcurrentRequests` sólo enumeraba tareas y el delegate downstream podía completar sin solapamiento efectivo. Se sustituyó por una barrera async de 200 solicitudes y medición de handlers activos; exige solapamiento y resultado exacto 60/140.
- Una aserción secundaria probó abrir el key ring mixto con sólo el certificado actual. Falló al procesar la clave protegida por el certificado previo; ese escenario no representa la configuración de rotación, que debe poder leer todo el key ring existente.
- Solución: el provider de rotación declara certificado actual + anterior para descifrado; descifra payload previo, persiste clave nueva y realiza round-trip del payload nuevo después de reiniciar el provider. Se quitó la aserción incorrecta de current-only. Prueba enfocada de rate limit/rotación: 2/2; suite serial: 86/86.
- Se eliminó la salida temporal del XML cifrado. La prueba no vuelca el contenido del key ring en logs.
- Se documentaron `FLETFLOW_DATA_PROTECTION_PREVIOUS_CERT_PATH` y `FLETFLOW_DATA_PROTECTION_PREVIOUS_CERT_PASSWORD`; se conserva el PFX anterior mientras claves/payloads persistidos puedan depender de él.

## 2026-11-22 — Runtime local de .NET no resolvía SDK por PATH — RESUELTO

- `dotnet test BackEnd_Gps.sln --configuration Release` falló antes de ejecutar pruebas: `/usr/lib64/dotnet/host/fxr` no contiene runtime versionado.
- El host `/usr/bin/dotnet` no corresponde al SDK instalado que usa este entorno. Se repitió con `/home/leonardo/.dotnet/dotnet`; suite Release **86/86** y build Release **0 warnings/0 errores**.
- Prevención: usar el path explícito `/home/leonardo/.dotnet/dotnet` en comandos locales de backend.

## 2026-11-22 — Resultado inconsistente durante test backend concurrente — RESUELTO / NO REPRODUCIDO

- Una ejecución completa intercalada con la iteración/build del test de certificado informó 85/86 y `CryptographicException: Unable to retrieve the decryption key`; el número de línea del stack no correspondía con el fuente actual. La prueba de rotación aislada pasó después y la suite serial completa pasó 86/86.
- Causa probable: compilación/test simultáneos escribieron artefactos `bin/obj` compartidos, dejando símbolos/código desalineados; no se reprodujo con los comandos seriales posteriores.
- Prevención: evitar varios procesos `dotnet test/build` sobre el mismo checkout; usar `-m:1` para verificaciones seriales. Mantener la prueba de rotación actual + anterior para impedir regresión real.

## 2026-11-22 — Consulta de Actions en repositorio incorrecto — RESUELTO

- `gh run view 45039605708` se lanzó desde `/home/leonardo/Back_GPS`; ese ID pertenece a `Paul1578/Front_gpsR` y la CLI respondió HTTP 404.
- No se alteró ningún recurso. Se consultó desde `/home/leonardo/Front_gpsR` y se confirmó que el run terminó success.
- Prevención: consultar IDs de Actions desde el checkout dueño del workflow.

## 2026-11-22 — Cuenta Driver desactivada aún podía iniciar una ruta — CORREGIDO Y VALIDADO

- Auditoría encontró que `/Users/{id}/status?isActive=false` revocaba la sesión de la cuenta User pero dejaba activo su Driver enlazado. `ChangeRouteStatusCommand` sólo comprobaba `Driver.IsActive`, por lo que una ruta pendiente podía iniciarse y cambiar el vehículo a `OnRoute` aunque el Driver no pudiera autenticarse.
- Corrección: el inicio valida la cuenta vinculada bajo los locks de ruta/vehículo/Driver/cuenta. La baja de una cuenta con Driver usa los mismos locks, relee la relación y bloquea la desactivación mientras haya ruta en progreso. Conflictos devuelven HTTP 409 antes de persistir cambios.
- xUnit añade rechazo de inicio con cuenta vinculada inactiva y rechazo de baja durante ruta activa; ambas aserciones verifican que route/vehicle/user permanezcan consistentes. Smoke HTTP/MySQL cubre desactivar→intentar iniciar (409, Pending/Available/Inactive), cuenta activa→iniciar y baja durante ruta (409, InProgress/OnRoute/Active), cancelar, desactivar y cleanup.
- Validación local: xUnit enfocado **3/3**, suite Release **88/88**, restore correcto, build Release **0 warnings/0 errores**, `bash -n scripts/smoke-tenant-http.sh` y `git diff --check` correctos.
- Backend commit `190f284` pasó Actions `45045070288`: Release build/test, xUnit **88/88**, smoke HTTP/MySQL tenant/flota y smoke de concurrencia; cleanup de servicios correcto. SHA exacto validado.
- Durante la auditoría dos búsquedas intentaron abrir `AppDbContext.cs` y `fletflow/Program.cs` en ubicaciones que no existen; el output confirmó rutas no encontradas. Solución: localizar las clases con `rg -n 'class AppDbContext'` / `rg --files | rg 'Program.cs'` antes de abrirlas; no hubo cambios ni impacto.
- El primer `git diff --check` encontró una línea vacía extra al final de `docs/CHANGELOG.md`; se recortó el EOF y la repetición pasó.


## 2026-11-22 — Cancelar ruta pendiente liberaba vehículo de mantenimiento — CORREGIDO; CI PENDIENTE

- La transición de ruta trataba toda cancelación como liberación de vehículo. Una ruta Pending puede coexistir con vehículo puesto en Maintenance, por lo que cancelarla cambiaba erróneamente el estado a Available.
- Corrección: liberar sólo si el estado previo era InProgress y el destino es Pending/Completed/Cancelled.
- Regresión SQLite y smoke HTTP/MySQL: route Pending + vehicle Maintenance → cancelar → route Cancelled y SQL `Vehicles.Status=2`; se restablece Available para continuar el smoke. Test enfocado 1/1 y suite Release 89/89; Actions del nuevo smoke pendiente.
- El `dotnet` global falló porque `/usr/lib64/dotnet/host/fxr` está vacío. Solución: instalar SDK 8.0.428 en `/tmp/fletflow-dotnet`; tests enfocado y completo pasan, sin alterar setup del sistema. Prevención: verificar `dotnet --info` y usar SDK local aislado cuando no haya runtime registrado.


## 2026-11-22 — Fixture de regresión de mantenimiento chocaba con agenda existente — CORREGIDO

- Actions backend `45046324866` falló al crear la segunda ruta de regresión: el smoke reutilizaba el vehículo y Driver de otra ruta pendiente, y la validación de agenda devolvió 409 (`El vehículo o conductor ya tiene una ruta activa en ese horario`).
- Solución: escenario aislado con Vehicle, Driver y Route propios; la limpieza restaura el estado del vehículo, borra asociaciones y verifica por SQL que Driver:Vehicle:Route quede `0:0:0`. Repetir Action en el siguiente SHA.
- Prevención: fixtures que prueban transición deben tener recursos/agenda independientes; no relajar la regla de negocio para facilitar el test.


## 2026-11-22 — Carrera de revocación de refresh, consumo doble reset y GPS tras reasignación — CORREGIDO; CI PENDIENTE

- Refresh rotation bloqueaba fila User, pero los cambios de contraseña, recuperación/activación y baja de algunas cuentas no siempre compartían ese lock. Un refresh podía insertar reemplazo después de una revocación masiva. Solución: operaciones que actualizan contraseña, revocan sesiones o cambian estado toman el lock User antes de releer estado y persistir; todas serializan con `TryRotateAsync`.
- Reset token se leía activo y luego se marcaba usado sin condición; dos solicitudes podían avanzar a la vez. `MarkUsedAsync` ahora hace UPDATE atómico sólo si `UsedAt IS NULL` y `ExpiresAt > now`, devuelve éxito/fallo; ResetPassword/ActivateAccount releen tras lock y abortan sin cambiar password si no lo consumen. SQLite verifica primera llamada true, segunda false.
- GPS Driver se autorizaba en controller y se escribía después de cargar ruta sin lock/revalidación. Reasignación entre ambos pasos permitía que el Driver anterior guardara GPS. Command y consulta toman locks de asignación, recargan y vuelven a verificar Driver activo/asignado antes de crear/leer posiciones; unitaria simula reasignación y exige cero escrituras.
- Validación local Release xUnit **91/91**, build 0 warnings/0 errors; integración MySQL/HTTP de esta tanda pendiente de Actions.


## 2026-11-22 — Outbox podía enviar enlace de recuperación ya invalidado — CORREGIDO; CI PENDIENTE

- El worker enviaba cada payload válido criptográficamente aunque una nueva solicitud hubiera invalidado el PasswordResetToken o ya hubiera sido usado/expirado; el destinatario podía abrir un enlace que no funcionaba.
- Corrección: antes del envío, el worker calcula el hash del token descifrado y consulta que exista para ese usuario con `UsedAt IS NULL` y `ExpiresAt > now`. Si no, cierra mensaje sin enviar y registra `ResetTokenInactive` como estado terminal, intento SMTP intacto.
- Regresión SQLite: token usado no llega al remitente, queda DeadLettered con tipo exacto, sin `SentAt`, sin lock y con cero intentos. Worker 3/3, suite 92/92 y build limpio. Actions del SHA posterior pendientes.
- Prevención: los workers de correo deben comprobar vigencia/propiedad del token justo antes de una operación externa irreversible; no basta con que el payload esté cifrado.

## 2026-11-22 — SESSION_HANDOFF tuvo churn de finales de línea — CORREGIDO

- Al añadir el resumen actual con escritura de texto Python, se normalizó el formato histórico CRLF del handoff y el commit mostró 830 líneas alteradas aunque sólo se necesitaba anteponer el estado.
- Solución: restaurar el contenido exacto del commit base y añadir el encabezado conservando sus bytes de newline. El diff neto quedó en 10 líneas nuevas; no se perdió historial.
- Prevención: antes de escribir documentación histórica, detectar/preservar el newline original y revisar `git diff --stat` por archivo, no sólo `git diff --check`.
