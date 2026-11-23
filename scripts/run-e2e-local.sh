#!/usr/bin/env bash
set -euo pipefail

backend_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
frontend_dir="${FLETFLOW_FRONTEND_DIR:-/home/leonardo/Front_gpsR}"
playwright_base_url="${PLAYWRIGHT_BASE_URL:-http://127.0.0.1:3000}"
if [[ -z "${DB_NAME:-}" && -f "$backend_dir/.env" ]]; then
  set -a
  # shellcheck disable=SC1091
  source "$backend_dir/.env"
  set +a
fi

frontend_env_file="$frontend_dir/.env.local"
api_origin="${FLETFLOW_API_URL:-}"
api_origin="${api_origin%/api}"
client_api_base="${NEXT_PUBLIC_API_BASE_URL:-}"

if [[ -f "$frontend_env_file" ]]; then
  [[ -n "$api_origin" ]] || api_origin="$(sed -n 's/^FLETFLOW_API_URL=//p' "$frontend_env_file" | tail -n 1 | tr -d '"')"
  [[ -n "$client_api_base" ]] || client_api_base="$(sed -n 's/^NEXT_PUBLIC_API_BASE_URL=//p' "$frontend_env_file" | tail -n 1 | tr -d '"')"
fi
api_origin="${api_origin:-http://localhost:5048}"
MYSQL_HOST="${MYSQL_HOST:-127.0.0.1}"
MYSQL_PORT="${MYSQL_PORT:-3306}"
MYSQL_USER="${MYSQL_USER:-${DB_USER:-root}}"
MYSQL_PWD="${MYSQL_PWD:-${MYSQL_ROOT_PASSWORD:-${DB_PASSWORD:-}}}"
export MYSQL_HOST MYSQL_PORT MYSQL_USER MYSQL_PWD

if [[ "${FLETFLOW_E2E_ALLOW_MUTATIONS:-}" != "true" ]]; then
  echo "Define FLETFLOW_E2E_ALLOW_MUTATIONS=true sólo para una base local/QA descartable." >&2
  exit 2
fi
if [[ ! "${FLETFLOW_E2E_ADMIN_EMAIL:-}" =~ ^fletflow\.e2e\.admin\..+@example\.test$ ]]; then
  echo "Usa una cuenta bootstrap temporal con correo fletflow.e2e.admin.<id>@example.test." >&2
  exit 2
fi
if [[ -z "${FLETFLOW_E2E_ADMIN_PASSWORD:-}" ]]; then
  echo "Falta FLETFLOW_E2E_ADMIN_PASSWORD." >&2
  exit 2
fi
if [[ -z "${DB_NAME:-}" || -z "${MYSQL_PWD:-}" ]]; then
  echo "Faltan DB_NAME o credenciales MYSQL para limpiar los fixtures." >&2
  exit 2
fi
if [[ "${CI:-}" == "true" && ! "$DB_NAME" =~ ^fletflow_ci([_][a-zA-Z0-9]+)*$ ]]; then
  echo "En CI sólo se permiten bases descartables fletflow_ci*." >&2
  exit 2
fi
if [[ ! -f "$frontend_dir/package.json" ]]; then
  echo "No se encontró el frontend en $frontend_dir." >&2
  exit 2
fi
if [[ "$playwright_base_url" != "http://127.0.0.1:3000" && "$playwright_base_url" != "http://localhost:3000" ]]; then
  echo "El runner con limpieza local sólo acepta el frontend local en puerto 3000." >&2
  exit 2
fi
if [[ "$api_origin" != "http://127.0.0.1:5048" && "$api_origin" != "http://localhost:5048" ]]; then
  echo "El runner con limpieza local sólo acepta la API local en puerto 5048." >&2
  exit 2
fi
if [[ -n "$client_api_base" && "$client_api_base" != "/api/backend" && \
  "$client_api_base" != "http://127.0.0.1:5048/api" && \
  "$client_api_base" != "http://localhost:5048/api" ]]; then
  echo "NEXT_PUBLIC_API_BASE_URL apunta fuera del endpoint API local permitido." >&2
  exit 2
fi
if ! curl -fsS "http://127.0.0.1:5048/swagger/index.html" >/dev/null; then
  echo "Inicia la API Development local antes de ejecutar el E2E." >&2
  exit 2
fi
if [[ "${CI:-}" != "true" ]] && ! curl -fsS "$playwright_base_url/login" >/dev/null; then
  echo "Inicia el frontend local antes de ejecutar el E2E; Playwright lo inicia en CI." >&2
  exit 2
fi

cleanup() {
  local status=$?
  local cleanup_output
  cleanup_output="$(mktemp)"
  trap - EXIT
  if ! mysql --protocol=TCP --host="$MYSQL_HOST" --port="$MYSQL_PORT" --user="$MYSQL_USER" "$DB_NAME" \
    < "$backend_dir/scripts/cleanup-e2e.sql" >"$cleanup_output" 2>&1; then
    echo "No se pudieron limpiar los fixtures E2E de MySQL." >&2
    cat "$cleanup_output" >&2
    status=1
  fi
  rm -f "$cleanup_output"
  exit "$status"
}
trap cleanup EXIT

user_suffix="$(date +%s%N | tail -c 10)"
export FLETFLOW_E2E_USER_EMAIL="fletflow.e2e.user.$user_suffix@example.test"
export FLETFLOW_E2E_USER_PASSWORD="$(openssl rand -hex 24)"
export FLETFLOW_E2E_DEACTIVATION_EMAIL="fletflow.e2e.user.deactivation.$user_suffix@example.test"
export FLETFLOW_E2E_DEACTIVATION_PASSWORD="$(openssl rand -hex 24)"
export FLETFLOW_E2E_RESET_EMAIL="fletflow.e2e.user.reset.$user_suffix@example.test"
export FLETFLOW_E2E_RESET_PASSWORD="$(openssl rand -hex 24)"
export FLETFLOW_E2E_RESET_TOKEN="$(openssl rand -hex 32)"
export FLETFLOW_E2E_ACTIVATE_EMAIL="fletflow.e2e.user.activate.$user_suffix@example.test"
export FLETFLOW_E2E_ACTIVATE_PASSWORD="$(openssl rand -hex 24)"
export FLETFLOW_E2E_ACTIVATE_TOKEN="$(openssl rand -hex 32)"
export FLETFLOW_E2E_DRIVER_EMAIL="fletflow.e2e.driver.$user_suffix@example.test"
export FLETFLOW_E2E_DRIVER_PASSWORD="$(openssl rand -hex 24)"
export FLETFLOW_E2E_ROUTE_DRIVER_EMAIL="fletflow.e2e.driver.route.$user_suffix@example.test"
export FLETFLOW_E2E_ROUTE_DRIVER_PASSWORD="$(openssl rand -hex 24)"
export FLETFLOW_E2E_MANAGER_MAP_EMAIL="fletflow.e2e.manager.map.$user_suffix@example.test"
export FLETFLOW_E2E_MANAGER_MAP_PASSWORD="$(openssl rand -hex 24)"
export FLETFLOW_E2E_MAP_FIXTURE_SUFFIX="$user_suffix"
export FLETFLOW_E2E_OTHER_TENANT_EMAIL="fletflow.e2e.admin.other.$user_suffix@example.test"
export FLETFLOW_E2E_OTHER_TENANT_PASSWORD="$(openssl rand -hex 24)"

FLETFLOW_E2E_DRIVER_IDS="$(node <<'NODE'
const api = "http://127.0.0.1:5048/api";
const email = process.env.FLETFLOW_E2E_ADMIN_EMAIL;
const password = process.env.FLETFLOW_E2E_ADMIN_PASSWORD;

const loginResponse = await fetch(`${api}/Auth/login`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ email, password }),
});
if (!loginResponse.ok) throw new Error(`Login QA Admin: HTTP ${loginResponse.status}`);
const login = await loginResponse.json();
const token = login.token ?? login.accessToken;
if (!token) throw new Error("La respuesta de login QA no incluyó token de acceso.");

const createUser = async (body) => {
  const response = await fetch(`${api}/Users`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body),
  });
  if (!response.ok) throw new Error(`Alta fixture ${body.roleName}: HTTP ${response.status}`);
  return response.json();
};

await createUser({
  username: "E2E QA User",
  email: process.env.FLETFLOW_E2E_USER_EMAIL,
  password: process.env.FLETFLOW_E2E_USER_PASSWORD,
  roleName: "User",
});

await createUser({
  username: "E2E QA Deactivation",
  email: process.env.FLETFLOW_E2E_DEACTIVATION_EMAIL,
  password: process.env.FLETFLOW_E2E_DEACTIVATION_PASSWORD,
  roleName: "User",
});

await createUser({
  username: "E2E QA Password Reset",
  email: process.env.FLETFLOW_E2E_RESET_EMAIL,
  password: process.env.FLETFLOW_E2E_RESET_PASSWORD,
  roleName: "User",
});

await createUser({
  username: "E2E QA Account Activation",
  email: process.env.FLETFLOW_E2E_ACTIVATE_EMAIL,
  password: process.env.FLETFLOW_E2E_ACTIVATE_PASSWORD,
  roleName: "User",
});

const otherTenantRegistration = await fetch(`${api}/Auth/register`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    username: "E2E QA Other Company",
    email: process.env.FLETFLOW_E2E_OTHER_TENANT_EMAIL,
    password: process.env.FLETFLOW_E2E_OTHER_TENANT_PASSWORD,
    roleName: "Admin",
  }),
});
if (!otherTenantRegistration.ok) throw new Error(`Registro de tenant QA: HTTP ${otherTenantRegistration.status}`);

const driver = await createUser({
  username: "E2E QA Driver",
  email: process.env.FLETFLOW_E2E_DRIVER_EMAIL,
  password: process.env.FLETFLOW_E2E_DRIVER_PASSWORD,
  roleName: "Driver",
  firstName: "E2E QA",
  lastName: "Driver",
  documentNumber: `E2EQA${Date.now()}`,
  phoneNumber: "0990000000",
});
if (!driver.driverId) throw new Error("El fixture Driver no devolvió DriverId.");
const routeDriver = await createUser({
  username: "E2E QA Route Driver",
  email: process.env.FLETFLOW_E2E_ROUTE_DRIVER_EMAIL,
  password: process.env.FLETFLOW_E2E_ROUTE_DRIVER_PASSWORD,
  roleName: "Driver",
  firstName: "E2E QA Route",
  lastName: "Driver",
  documentNumber: `E2EQAR${Date.now()}`,
  phoneNumber: "0990000001",
});
if (!routeDriver.driverId) throw new Error("El fixture Driver de operación no devolvió DriverId.");
const mapManager = await createUser({
  username: "E2E QA Map Manager",
  email: process.env.FLETFLOW_E2E_MANAGER_MAP_EMAIL,
  password: process.env.FLETFLOW_E2E_MANAGER_MAP_PASSWORD,
  roleName: "Manager",
  firstName: "E2E QA Map",
  lastName: "Manager",
});
const mapDriverResponse = await fetch(`${api}/Drivers`, {
  method: "POST",
  headers: {
    Authorization: `Bearer ${token}`,
    "Content-Type": "application/json",
  },
  body: JSON.stringify({
    firstName: "E2E QA Map",
    lastName: "Route Driver",
    documentNumber: `E2EQA${Date.now().toString().slice(-10)}M`,
    phoneNumber: "0990000002",
  }),
});
if (!mapDriverResponse.ok) throw new Error(`Alta Driver fixture del mapa: HTTP ${mapDriverResponse.status}`);
const mapDriver = await mapDriverResponse.json();
process.stdout.write(JSON.stringify({
  driverId: driver.driverId,
  driverUserId: driver.id,
  routeDriverId: routeDriver.driverId,
  mapManagerId: mapManager.id,
  mapRouteDriverId: mapDriver.id,
}));
NODE
)"
FLETFLOW_E2E_DRIVER_ID="$(node -e 'process.stdout.write(JSON.parse(process.argv[1]).driverId)' "$FLETFLOW_E2E_DRIVER_IDS")"
FLETFLOW_E2E_DRIVER_USER_ID="$(node -e 'process.stdout.write(JSON.parse(process.argv[1]).driverUserId)' "$FLETFLOW_E2E_DRIVER_IDS")"
FLETFLOW_E2E_ROUTE_DRIVER_ID="$(node -e 'process.stdout.write(JSON.parse(process.argv[1]).routeDriverId)' "$FLETFLOW_E2E_DRIVER_IDS")"
FLETFLOW_E2E_MAP_MANAGER_ID="$(node -e 'process.stdout.write(JSON.parse(process.argv[1]).mapManagerId)' "$FLETFLOW_E2E_DRIVER_IDS")"
FLETFLOW_E2E_MAP_ROUTE_DRIVER_ID="$(node -e 'process.stdout.write(JSON.parse(process.argv[1]).mapRouteDriverId)' "$FLETFLOW_E2E_DRIVER_IDS")"
export FLETFLOW_E2E_DRIVER_ID
export FLETFLOW_E2E_DRIVER_USER_ID
export FLETFLOW_E2E_ROUTE_DRIVER_ID
export FLETFLOW_E2E_MAP_MANAGER_ID
export FLETFLOW_E2E_MAP_ROUTE_DRIVER_ID

mysql --protocol=TCP --host="$MYSQL_HOST" --port="$MYSQL_PORT" --user="$MYSQL_USER" "$DB_NAME" \
  -e "UPDATE Users SET IsActive=1, MustChangePassword=0 WHERE Email='$FLETFLOW_E2E_OTHER_TENANT_EMAIL';" \
  >/dev/null 2>&1

mysql --protocol=TCP --host="$MYSQL_HOST" --port="$MYSQL_PORT" --user="$MYSQL_USER" "$DB_NAME" \
  -e "UPDATE Users SET IsActive=1, MustChangePassword=0 WHERE Email IN ('$FLETFLOW_E2E_USER_EMAIL', '$FLETFLOW_E2E_DEACTIVATION_EMAIL', '$FLETFLOW_E2E_DRIVER_EMAIL', '$FLETFLOW_E2E_ROUTE_DRIVER_EMAIL', '$FLETFLOW_E2E_MANAGER_MAP_EMAIL');" \
  >/dev/null 2>&1

mysql --protocol=TCP --host="$MYSQL_HOST" --port="$MYSQL_PORT" --user="$MYSQL_USER" "$DB_NAME" \
  -e "UPDATE Users SET IsActive=1, MustChangePassword=0 WHERE Email='$FLETFLOW_E2E_RESET_EMAIL'; UPDATE Users SET IsActive=1, MustChangePassword=1 WHERE Email='$FLETFLOW_E2E_ACTIVATE_EMAIL'; INSERT INTO PasswordResetTokens (Id, UserId, TokenHash, ExpiresAt, CreatedAt, UsedAt) SELECT UUID(), Id, UPPER(SHA2('$FLETFLOW_E2E_RESET_TOKEN', 256)), DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 30 MINUTE), UTC_TIMESTAMP(6), NULL FROM Users WHERE Email='$FLETFLOW_E2E_RESET_EMAIL'; INSERT INTO PasswordResetTokens (Id, UserId, TokenHash, ExpiresAt, CreatedAt, UsedAt) SELECT UUID(), Id, UPPER(SHA2('$FLETFLOW_E2E_ACTIVATE_TOKEN', 256)), DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 30 MINUTE), UTC_TIMESTAMP(6), NULL FROM Users WHERE Email='$FLETFLOW_E2E_ACTIVATE_EMAIL';" \
  >/dev/null 2>&1
token_fixture_count="$(mysql --protocol=TCP --host="$MYSQL_HOST" --port="$MYSQL_PORT" --user="$MYSQL_USER" "$DB_NAME" \
  --batch --skip-column-names -e "SELECT COUNT(*) FROM PasswordResetTokens WHERE TokenHash IN (UPPER(SHA2('$FLETFLOW_E2E_RESET_TOKEN', 256)), UPPER(SHA2('$FLETFLOW_E2E_ACTIVATE_TOKEN', 256)));" 2>/dev/null)"
[[ "$token_fixture_count" == "2" ]] || {
  echo "No se pudieron preparar los fixtures temporales de recuperación/activación." >&2
  exit 1
}

PLAYWRIGHT_BASE_URL="$playwright_base_url" \
PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH="${PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH:-/usr/bin/google-chrome}" \
  npm --prefix "$frontend_dir" run test:e2e -- "$@"
