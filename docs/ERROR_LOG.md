# Registro de errores y soluciones

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
