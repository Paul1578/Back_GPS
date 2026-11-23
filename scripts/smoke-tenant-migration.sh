#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
pre_tenant_migration="20251124021054_LinkDriverUser"
tenant_migration="20261121031333_TenantFleetOwnership"
mysql_image="${FLETFLOW_MIGRATION_MYSQL_IMAGE:-mysql:8.0}"
container_name="fletflow-tenant-migration-${$}-${RANDOM}"
work_dir="$(mktemp -d)"
source_dir="$work_dir/source"
container_started=0

cleanup() {
  local status=$?
  trap - EXIT
  if (( container_started )); then
    docker rm --force "$container_name" >/dev/null 2>&1 || true
  fi
  rm -rf -- "$work_dir"
  exit "$status"
}
trap cleanup EXIT

for command in docker mysql openssl tar grep; do
  command -v "$command" >/dev/null || {
    echo "Falta dependencia: $command" >&2
    exit 2
  }
done

dotnet_bin="$(command -v dotnet || true)"
if [[ -n "$dotnet_bin" ]] && ! "$dotnet_bin" --version >/dev/null 2>&1; then
  dotnet_bin=""
fi
if [[ -z "$dotnet_bin" && -n "${DOTNET_ROOT:-}" && -x "$DOTNET_ROOT/dotnet" ]]; then
  dotnet_bin="$DOTNET_ROOT/dotnet"
  export PATH="$DOTNET_ROOT:$PATH"
fi
if [[ -z "$dotnet_bin" ]]; then
  echo "No se encontró dotnet en PATH ni en DOTNET_ROOT; instala .NET SDK 8 para generar/aplicar migraciones." >&2
  exit 2
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

if ! docker info >/dev/null 2>&1; then
  echo "Docker no está disponible; no se inició ningún contenedor." >&2
  exit 2
fi

mkdir -p "$source_dir"
# AppDbContextDesignTimeFactory loads .env and DotNetEnv overwrites process vars.
# Running from this temporary source copy keeps any developer .env out of scope.
tar -C "$repo_root" \
  --exclude=.git \
  --exclude='.env*' \
  --exclude='**/.env*' \
  --exclude=bin \
  --exclude=obj \
  -cf - . | tar -C "$source_dir" -xf -

"$dotnet_bin" tool install dotnet-ef \
  --tool-path "$work_dir/tools" \
  --version 9.0.10 \
  --disable-parallel >/dev/null

mysql_root_password="$(openssl rand -hex 24)"
docker run --detach --rm \
  --name "$container_name" \
  --publish 127.0.0.1::3306 \
  --env MYSQL_ROOT_PASSWORD="$mysql_root_password" \
  --env MYSQL_ROOT_HOST=% \
  "$mysql_image" \
  --default-authentication-plugin=mysql_native_password >/dev/null
container_started=1

mysql_port="$(docker inspect --format '{{(index (index .NetworkSettings.Ports "3306/tcp") 0).HostPort}}' "$container_name")"
export MYSQL_PWD="$mysql_root_password"

mysql_cmd=(mysql --protocol=TCP --host=127.0.0.1 --port="$mysql_port" --user=root --batch --skip-column-names)
ready=0
for _ in $(seq 1 60); do
  if "${mysql_cmd[@]}" -e 'SELECT 1' >/dev/null 2>&1; then
    ready=1
    break
  fi
  sleep 2
done
if (( ! ready )); then
  echo "MySQL efímero no estuvo listo; últimos logs del contenedor:" >&2
  docker logs "$container_name" 2>&1 | tail -40 >&2 || true
  exit 1
fi

apply_migration() {
  local database="$1"
  local target="$2"
  (
    cd "$source_dir"
    export DB_HOST=127.0.0.1
    export DB_PORT="$mysql_port"
    export DB_NAME="$database"
    export DB_USER=root
    export DB_PASSWORD="$mysql_root_password"
    "$work_dir/tools/dotnet-ef" database update "$target" \
      --project "$source_dir/BackEnd_Gps.csproj" \
      --startup-project "$source_dir/BackEnd_Gps.csproj"
  )
}

seed_root_admins() {
  local database="$1"
  "${mysql_cmd[@]}" "$database" <<'SQL'
INSERT INTO Roles (Id, Name)
VALUES ('00000000-0000-0000-0000-000000000001', 'Admin');

INSERT INTO Users (Id, Username, Email, PasswordHash, IsActive, CreatedAt, MustChangePassword, OwnerUserId)
VALUES
  ('00000000-0000-0000-0000-000000000011', 'legacy-root-oldest', 'legacy-root-oldest@example.test', 'unused', b'1', '2020-01-01 00:00:00.000000', b'0', NULL),
  ('00000000-0000-0000-0000-000000000012', 'legacy-root-newer', 'legacy-root-newer@example.test', 'unused', b'1', '2021-01-01 00:00:00.000000', b'0', NULL);

INSERT INTO UserRoles (UserId, RoleId)
VALUES
  ('00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000001'),
  ('00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000001');
SQL
}

assert_sql_value() {
  local database="$1"
  local query="$2"
  local expected="$3"
  local actual
  actual="$("${mysql_cmd[@]}" "$database" -e "$query")"
  if [[ "$actual" != "$expected" ]]; then
    echo "Aserción SQL fallida en $database: esperado '$expected', recibido '$actual'." >&2
    exit 1
  fi
}

normal_db="tenant_migration_success"
discordant_db="tenant_migration_discordant"
"${mysql_cmd[@]}" -e "CREATE DATABASE \`$normal_db\` CHARACTER SET utf8mb4; CREATE DATABASE \`$discordant_db\` CHARACTER SET utf8mb4;"

echo "Aplicando migraciones hasta $pre_tenant_migration (escenario válido)..."
apply_migration "$normal_db" "$pre_tenant_migration"
seed_root_admins "$normal_db"
"${mysql_cmd[@]}" "$normal_db" <<'SQL'
INSERT INTO Vehicles (Id, Plate, Brand, Model, Year, Description, IsActive)
VALUES ('00000000-0000-0000-0000-000000000021', 'MIG-EMPTY', 'Legacy', 'Unassigned', 2020, 'vehicle survives migration', b'1');

INSERT INTO Drivers (Id, FirstName, LastName, DocumentNumber, PhoneNumber, IsActive, VehicleId, UserId)
VALUES ('00000000-0000-0000-0000-000000000022', 'Legacy', 'Driver', 'MIG-DRIVER', '5550100', b'1', NULL, NULL);

INSERT INTO Routes (Id, Name, VehicleId, DriverId, Origin, Destination, CargoDescription, PlannedStart, PlannedEnd, Status, IsActive, PointsJson)
VALUES ('00000000-0000-0000-0000-000000000023', 'legacy migration route', '00000000-0000-0000-0000-000000000021', '00000000-0000-0000-0000-000000000022', 'Origin', 'Destination', 'route survives migration', NULL, NULL, 0, b'1', '[]');
SQL

echo "Aplicando $tenant_migration y comprobando asignación y supervivencia..."
apply_migration "$normal_db" "$tenant_migration"
assert_sql_value "$normal_db" \
  "SELECT OwnerUserId FROM Vehicles WHERE Id='00000000-0000-0000-0000-000000000021'" \
  "00000000-0000-0000-0000-000000000011"
assert_sql_value "$normal_db" \
  "SELECT CONCAT((SELECT COUNT(*) FROM Vehicles WHERE Id='00000000-0000-0000-0000-000000000021' AND OwnerUserId='00000000-0000-0000-0000-000000000011'), ':', (SELECT COUNT(*) FROM Drivers WHERE Id='00000000-0000-0000-0000-000000000022' AND OwnerUserId='00000000-0000-0000-0000-000000000011'), ':', (SELECT COUNT(*) FROM Routes WHERE Id='00000000-0000-0000-0000-000000000023' AND OwnerUserId='00000000-0000-0000-0000-000000000011'))" \
  "1:1:1"
assert_sql_value "$normal_db" \
  "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId='$tenant_migration'" \
  "1"
echo "OK: Vehicle sin vínculo Driver asignado al root Admin más antiguo; Vehicle, Driver y Route sobrevivieron con propietario."

echo "Preparando relación discordante en una segunda base desechable..."
apply_migration "$discordant_db" "$pre_tenant_migration"
seed_root_admins "$discordant_db"
"${mysql_cmd[@]}" "$discordant_db" <<'SQL'
INSERT INTO Vehicles (Id, Plate, Brand, Model, Year, Description, IsActive)
VALUES ('00000000-0000-0000-0000-000000000031', 'MIG-DISCORD', 'Legacy', 'Discordant', 2020, NULL, b'1');

INSERT INTO Drivers (Id, FirstName, LastName, DocumentNumber, PhoneNumber, IsActive, VehicleId, UserId)
VALUES
  ('00000000-0000-0000-0000-000000000032', 'Vehicle', 'Owner', 'MIG-VEHICLE-OWNER', '5550101', b'1', '00000000-0000-0000-0000-000000000031', '00000000-0000-0000-0000-000000000011'),
  ('00000000-0000-0000-0000-000000000033', 'Route', 'Owner', 'MIG-ROUTE-OWNER', '5550102', b'1', NULL, '00000000-0000-0000-0000-000000000012');

INSERT INTO Routes (Id, Name, VehicleId, DriverId, Origin, Destination, CargoDescription, PlannedStart, PlannedEnd, Status, IsActive, PointsJson)
VALUES ('00000000-0000-0000-0000-000000000034', 'discordant legacy route', '00000000-0000-0000-0000-000000000031', '00000000-0000-0000-0000-000000000033', 'Origin', 'Destination', NULL, NULL, NULL, 0, b'1', '[]');
SQL

if apply_migration "$discordant_db" "$tenant_migration" >"$work_dir/discordant-migration.log" 2>&1; then
  cat "$work_dir/discordant-migration.log" >&2
  echo "La migración aceptó una ruta con Driver y Vehicle de root owners distintos." >&2
  exit 1
fi
if ! grep -Fq 'TenantFleetOwnership stopped before schema changes: a route links resources from different owners' "$work_dir/discordant-migration.log"; then
  cat "$work_dir/discordant-migration.log" >&2
  echo "La migración discordante falló por un motivo distinto al guard esperado." >&2
  exit 1
fi
assert_sql_value "$discordant_db" \
  "SELECT CONCAT((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('Vehicles','Drivers','Routes') AND COLUMN_NAME='OwnerUserId'), ':', (SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId='$tenant_migration'), ':', (SELECT COUNT(*) FROM Vehicles WHERE Id='00000000-0000-0000-0000-000000000031'), ':', (SELECT COUNT(*) FROM Drivers WHERE Id IN ('00000000-0000-0000-0000-000000000032','00000000-0000-0000-0000-000000000033')), ':', (SELECT COUNT(*) FROM Routes WHERE Id='00000000-0000-0000-0000-000000000034'))" \
  "0:0:1:2:1"
"${mysql_cmd[@]}" "$discordant_db" -e 'DROP PROCEDURE IF EXISTS __fletflow_assert_route_owner_consistent;'
echo "OK: ruta discordante rechazada; sin columnas de ownership ni cambios en historial o registros legacy."
echo "Smoke de migración histórica TenantFleetOwnership completado en MySQL efímero."
