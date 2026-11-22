#!/usr/bin/env bash
set -euo pipefail

api_base="${FLETFLOW_API_URL:-http://127.0.0.1:5048/api}"
admin_email="${FLETFLOW_E2E_ADMIN_EMAIL:-}"
admin_password="${FLETFLOW_E2E_ADMIN_PASSWORD:-}"
db_name="${DB_NAME:-}"
mysql_host="${MYSQL_HOST:-127.0.0.1}"
mysql_user="${MYSQL_USER:-root}"

if [[ "${FLETFLOW_E2E_ALLOW_MUTATIONS:-}" != "true" ]]; then
  echo "Define FLETFLOW_E2E_ALLOW_MUTATIONS=true sólo con una base local/QA descartable." >&2
  exit 2
fi
if [[ ! "$api_base" =~ ^http://(127\.0\.0\.1|localhost):[0-9]{2,5}/api$ ]]; then
  echo "Este smoke sólo acepta una API HTTP en loopback." >&2
  exit 2
fi
if [[ ! "$admin_email" =~ ^fletflow\.e2e\.admin\..+@example\.test$ || -z "$admin_password" ]]; then
  echo "Se requiere una cuenta Admin bootstrap temporal fletflow.e2e.admin.*@example.test." >&2
  exit 2
fi
if [[ ! "$db_name" =~ ^fletflow_ci([_][a-zA-Z0-9]+)*$ ]]; then
  echo "DB_NAME debe ser una base descartable fletflow_ci_*; se rechazó '$db_name'." >&2
  exit 2
fi
for command in curl jq mysql; do
  command -v "$command" >/dev/null || { echo "Falta dependencia: $command" >&2; exit 2; }
done
if [[ -z "${MYSQL_PWD:-}" ]]; then
  echo "Falta MYSQL_PWD para cleanup acotado de fixtures." >&2
  exit 2
fi

work_dir="$(mktemp -d)"
token=""
route_id=""
vehicle_id=""
driver_id=""
other_tenant_email=""
restricted_user_email=""
deactivated_user_email=""
logistics_user_email=""
route_name=""
plate=""
restricted_attempt_plate=""
document_number=""

cleanup() {
  local status=$?
  trap - EXIT
  if [[ -n "$token" ]]; then
    local sql="START TRANSACTION;"
    if [[ -n "$route_id" ]]; then
      sql+=" DELETE FROM RoutePositions WHERE RouteId='$route_id'; DELETE FROM Routes WHERE Id='$route_id';"
    elif [[ -n "$route_name" ]]; then
      sql+=" DELETE rp FROM RoutePositions rp JOIN Routes r ON r.Id=rp.RouteId WHERE r.Name='$route_name'; DELETE FROM Routes WHERE Name='$route_name';"
    fi
    if [[ -n "$driver_id" ]]; then sql+=" DELETE FROM Drivers WHERE Id='$driver_id';"
    elif [[ -n "$document_number" ]]; then sql+=" DELETE FROM Drivers WHERE DocumentNumber='$document_number';"; fi
    if [[ -n "$vehicle_id" ]]; then sql+=" DELETE FROM Vehicles WHERE Id='$vehicle_id';"
    elif [[ -n "$plate" ]]; then sql+=" DELETE FROM Vehicles WHERE Plate='$plate';"; fi
    if [[ -n "$restricted_attempt_plate" ]]; then sql+=" DELETE FROM Vehicles WHERE Plate='$restricted_attempt_plate';"; fi
    if [[ -n "$other_tenant_email" ]]; then
      sql+=" SET @qa_user_id=(SELECT Id FROM Users WHERE Email='$other_tenant_email'); DELETE FROM RefreshTokens WHERE UserId=@qa_user_id; DELETE FROM PasswordResetTokens WHERE UserId=@qa_user_id; DELETE FROM UserRoles WHERE UserId=@qa_user_id; DELETE FROM Users WHERE Id=@qa_user_id;"
    fi
    if [[ -n "$restricted_user_email" ]]; then
      sql+=" SET @qa_user_id=(SELECT Id FROM Users WHERE Email='$restricted_user_email'); DELETE FROM RefreshTokens WHERE UserId=@qa_user_id; DELETE FROM PasswordResetTokens WHERE UserId=@qa_user_id; DELETE FROM UserRoles WHERE UserId=@qa_user_id; DELETE FROM Users WHERE Id=@qa_user_id;"
    fi
    if [[ -n "$deactivated_user_email" ]]; then
      sql+=" SET @qa_user_id=(SELECT Id FROM Users WHERE Email='$deactivated_user_email'); DELETE FROM RefreshTokens WHERE UserId=@qa_user_id; DELETE FROM PasswordResetTokens WHERE UserId=@qa_user_id; DELETE FROM UserRoles WHERE UserId=@qa_user_id; DELETE FROM Users WHERE Id=@qa_user_id;"
    fi
    if [[ -n "$logistics_user_email" ]]; then
      sql+=" SET @qa_user_id=(SELECT Id FROM Users WHERE Email='$logistics_user_email'); DELETE FROM RefreshTokens WHERE UserId=@qa_user_id; DELETE FROM PasswordResetTokens WHERE UserId=@qa_user_id; DELETE FROM UserRoles WHERE UserId=@qa_user_id; DELETE FROM Users WHERE Id=@qa_user_id;"
    fi
    sql+=" COMMIT;"
    mysql --protocol=TCP --host="$mysql_host" --user="$mysql_user" "$db_name" -e "$sql" >/dev/null 2>&1 || {
      echo "No se pudieron limpiar los fixtures creados por smoke-tenant-http.sh." >&2
      status=1
    }
  fi
  rm -rf -- "$work_dir"
  exit "$status"
}
trap cleanup EXIT

request() {
  local method="$1" path="$2" payload="${3:-}" request_token="$token"
  if (( $# >= 4 )); then request_token="$4"; fi
  local args=(-sS --max-time 20 -o "$work_dir/response.json" -w '%{http_code}' -X "$method"
    -H 'Content-Type: application/json')
  if [[ -n "$request_token" ]]; then args+=(-H "Authorization: Bearer $request_token"); fi
  if [[ -n "$payload" ]]; then args+=(-d "$payload"); fi
  http_status="$(curl "${args[@]}" "$api_base$path")"
}

expect_status() {
  local expected="$1" description="$2"
  if [[ "$http_status" != "$expected" ]]; then
    echo "$description: esperado HTTP $expected, recibido $http_status" >&2
    cat "$work_dir/response.json" >&2
    exit 1
  fi
}

request POST /Auth/login "$(jq -nc --arg email "$admin_email" --arg password "$admin_password" '{email:$email,password:$password}')" ""
expect_status 200 "Login tenant A"
token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"

suffix="$(date +%s%N | tail -c 10)"
restricted_user_email="fletflow.e2e.user.ci.$suffix@example.test"
restricted_user_password="$(openssl rand -hex 24)"
request POST /Users "$(jq -nc --arg email "$restricted_user_email" --arg password "$restricted_user_password" --arg username "E2E CI restricted $suffix" '{username:$username,email:$email,password:$password,roleName:"User"}')"
expect_status 200 "Crear usuario de permisos limitados"
mysql --protocol=TCP --host="$mysql_host" --user="$mysql_user" "$db_name" \
  -e "UPDATE Users SET IsActive=1, MustChangePassword=0 WHERE Email='$restricted_user_email';" >/dev/null
request POST /Auth/login "$(jq -nc --arg email "$restricted_user_email" --arg password "$restricted_user_password" '{email:$email,password:$password}')" ""
expect_status 200 "Login usuario de permisos limitados"
restricted_user_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"
restricted_attempt_plate="E2EQA${suffix: -9}"
request GET /Vehicles "" "$restricted_user_token"; expect_status 403 "Listar vehículos como User"
request GET /Routes "" "$restricted_user_token"; expect_status 403 "Listar rutas como User"
request POST /Vehicles "$(jq -nc --arg plate "$restricted_attempt_plate" '{plate:$plate,brand:"Blocked",model:"Blocked",year:2025}')" "$restricted_user_token"; expect_status 403 "Crear vehículo como User"

deactivated_user_email="fletflow.e2e.deactivated.ci.$suffix@example.test"
deactivated_user_password="$(openssl rand -hex 24)"
request POST /Users "$(jq -nc --arg email "$deactivated_user_email" --arg password "$deactivated_user_password" --arg username "E2E CI deactivated $suffix" '{username:$username,email:$email,password:$password,roleName:"Manager"}')"
expect_status 200 "Crear usuario para probar revocación"
deactivated_user_id="$(jq -er '.id' "$work_dir/response.json")"
mysql --protocol=TCP --host="$mysql_host" --user="$mysql_user" "$db_name" \
  -e "UPDATE Users SET IsActive=1, MustChangePassword=0 WHERE Id='$deactivated_user_id';" >/dev/null
request POST /Auth/login "$(jq -nc --arg email "$deactivated_user_email" --arg password "$deactivated_user_password" '{email:$email,password:$password}')" ""
expect_status 200 "Login de usuario antes de desactivar"
deactivated_access_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"
deactivated_refresh_token="$(jq -er '.refreshToken' "$work_dir/response.json")"
request GET /Vehicles "" "$deactivated_access_token"; expect_status 200 "Listar vehículos como Manager"
request GET /Drivers "" "$deactivated_access_token"; expect_status 200 "Listar conductores como Manager"
request GET /Routes "" "$deactivated_access_token"; expect_status 200 "Listar rutas como Manager"
request PUT "/Users/$deactivated_user_id/status?isActive=false" "" "$token"
expect_status 200 "Desactivar usuario de prueba"
request GET /Auth/me "" "$deactivated_access_token"
expect_status 401 "Bloquear access token de cuenta desactivada"
request POST /Auth/refresh "$(jq -nc --arg refreshToken "$deactivated_refresh_token" '{refreshToken:$refreshToken}')" ""
expect_status 401 "Bloquear refresh token de cuenta desactivada"

logistics_user_email="fletflow.e2e.logistics.ci.$suffix@example.test"
logistics_user_password="$(openssl rand -hex 24)"
request POST /Users "$(jq -nc --arg email "$logistics_user_email" --arg password "$logistics_user_password" --arg username "E2E CI logistics $suffix" '{username:$username,email:$email,password:$password,roleName:"Logistics"}')"
expect_status 200 "Crear usuario Logistics"
mysql --protocol=TCP --host="$mysql_host" --user="$mysql_user" "$db_name" \
  -e "UPDATE Users SET IsActive=1, MustChangePassword=0 WHERE Email='$logistics_user_email';" >/dev/null
request POST /Auth/login "$(jq -nc --arg email "$logistics_user_email" --arg password "$logistics_user_password" '{email:$email,password:$password}')" ""
expect_status 200 "Login Logistics"
logistics_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"
request GET /Routes "" "$logistics_token"; expect_status 200 "Listar rutas como Logistics"
request GET /Vehicles "" "$logistics_token"; expect_status 403 "Denegar vehículos a Logistics"
request GET /Drivers "" "$logistics_token"; expect_status 403 "Denegar conductores a Logistics"

other_tenant_email="fletflow.e2e.admin.other.$suffix@example.test"
other_tenant_password="$(openssl rand -hex 24)"
plate="E2EQA${suffix: -9}"
document_number="E2EQA${suffix: -9}"
request POST /Auth/register "$(jq -nc --arg email "$other_tenant_email" --arg password "$other_tenant_password" --arg username "E2E tenant B $suffix" '{username:$username,email:$email,password:$password,roleName:"Admin"}')" ""
expect_status 200 "Registro tenant B"

# El registro público fuerza User. Este upgrade sólo afecta la cuenta QA temporal y
# permite verificar aislamiento entre dos usuarios Admin de empresas independientes.
mysql --protocol=TCP --host="$mysql_host" --user="$mysql_user" "$db_name" <<SQL
UPDATE Users SET IsActive=1, MustChangePassword=0 WHERE Email='$other_tenant_email';
DELETE FROM UserRoles WHERE UserId=(SELECT Id FROM Users WHERE Email='$other_tenant_email');
INSERT INTO UserRoles (UserId, RoleId)
SELECT u.Id, r.Id FROM Users u JOIN Roles r ON r.Name='Admin' WHERE u.Email='$other_tenant_email';
SQL

request POST /Auth/login "$(jq -nc --arg email "$other_tenant_email" --arg password "$other_tenant_password" '{email:$email,password:$password}')" ""
expect_status 200 "Login tenant B"
other_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"

request POST /Vehicles "$(jq -nc --arg plate "$plate" '{plate:$plate,brand:"CI QA",model:"Tenant",year:2025}')"
expect_status 201 "Crear vehículo tenant A"
vehicle_id="$(jq -er '.id' "$work_dir/response.json")"

request POST /Drivers "$(jq -nc --arg document "$document_number" '{firstName:"E2E QA",lastName:"Driver A",documentNumber:$document,phoneNumber:"0990000000"}')"
expect_status 201 "Crear Driver tenant A"
driver_id="$(jq -er '.id' "$work_dir/response.json")"

route_name="E2E QA ruta tenant ${suffix: -9}"
points='[{"lat":-0.18,"lng":-78.46,"name":"CI origin","operation":0,"status":0},{"lat":-0.19,"lng":-78.47,"name":"CI destination","operation":0,"status":0}]'
request POST /Routes "$(jq -nc --arg name "$route_name" --arg vehicleId "$vehicle_id" --arg driverId "$driver_id" --argjson points "$points" '{name:$name,cargoDescription:"CI tenant isolation",vehicleId:$vehicleId,driverId:$driverId,points:$points}')"
expect_status 201 "Crear ruta tenant A"
route_id="$(jq -er '.id' "$work_dir/response.json")"

request PUT "/Routes/$route_id/status" '{"status":1}'
expect_status 204 "Iniciar ruta tenant A"
request POST "/Routes/$route_id/positions" "$(jq -nc '{latitude:-0.185,longitude:-78.465,recordedAt:(now|todateiso8601),speedKmh:0,heading:0}')"
expect_status 200 "Guardar GPS tenant A"

request GET /Vehicles "" "$other_token"
expect_status 200 "Listar vehículos tenant B"
if jq -e --arg id "$vehicle_id" 'any(.[]; .id == $id)' "$work_dir/response.json" >/dev/null; then
  echo "Tenant B recibió vehículo de A en el listado." >&2
  exit 1
fi
request GET "/Vehicles/$vehicle_id" "" "$other_token"; expect_status 404 "Leer vehículo cruzado"
request PUT "/Vehicles/$vehicle_id" "$(jq -nc --arg plate "HACK${suffix: -9}" '{plate:$plate,brand:"Blocked",model:"Blocked",year:2025}')" "$other_token"; expect_status 404 "Editar vehículo cruzado"
request DELETE "/Vehicles/$vehicle_id" "" "$other_token"; expect_status 404 "Borrar vehículo cruzado"

request GET /Drivers "" "$other_token"
expect_status 200 "Listar Drivers tenant B"
if jq -e --arg id "$driver_id" 'any(.[]; .id == $id)' "$work_dir/response.json" >/dev/null; then
  echo "Tenant B recibió Driver de A en el listado." >&2
  exit 1
fi
request GET "/Drivers/$driver_id" "" "$other_token"; expect_status 404 "Leer Driver cruzado"
request PUT "/Drivers/$driver_id" '{"firstName":"Blocked","lastName":"Blocked","documentNumber":"HACK00001","phoneNumber":"0990000000"}' "$other_token"; expect_status 404 "Editar Driver cruzado"
request PUT "/Drivers/$driver_id/status" '{"isActive":false}' "$other_token"; expect_status 404 "Cambiar estado Driver cruzado"

request GET /Routes "" "$other_token"
expect_status 200 "Listar rutas tenant B"
if jq -e --arg id "$route_id" 'any(.[]; .id == $id)' "$work_dir/response.json" >/dev/null; then
  echo "Tenant B recibió ruta de A en el listado." >&2
  exit 1
fi
request GET "/Routes/$route_id" "" "$other_token"; expect_status 404 "Leer ruta cruzada"
request GET "/Routes/$route_id/positions" "" "$other_token"; expect_status 200 "Leer colección GPS cruzada"
jq -e 'length == 0' "$work_dir/response.json" >/dev/null || { echo "Tenant B recibió posiciones GPS de A." >&2; exit 1; }
request POST "/Routes/$route_id/positions" '{"latitude":-0.186,"longitude":-78.466}' "$other_token"; expect_status 404 "Escribir GPS cruzado"
request PUT "/Routes/$route_id" "$(jq -nc --arg name "Blocked $suffix" --arg vehicleId "$vehicle_id" --arg driverId "$driver_id" --argjson points "$points" '{name:$name,cargoDescription:"Blocked",vehicleId:$vehicleId,driverId:$driverId,points:$points}')" "$other_token"; expect_status 404 "Editar ruta cruzada"
request PUT "/Routes/$route_id/status" '{"status":3}' "$other_token"; expect_status 404 "Cambiar estado de ruta cruzada"
request DELETE "/Routes/$route_id" "" "$other_token"; expect_status 404 "Borrar ruta cruzada"
request POST /Routes "$(jq -nc --arg name "E2E QA ruta intento ${suffix: -9}" --arg vehicleId "$vehicle_id" --arg driverId "$driver_id" --argjson points "$points" '{name:$name,cargoDescription:"Blocked cross-tenant association",vehicleId:$vehicleId,driverId:$driverId,points:$points}')" "$other_token"; expect_status 404 "Crear ruta con recursos cruzados"

request GET "/Routes/$route_id/positions" "" "$token"
expect_status 200 "Leer GPS tenant A"
jq -e 'length == 1' "$work_dir/response.json" >/dev/null || { echo "Tenant A no encontró su posición GPS." >&2; exit 1; }
request GET "/Routes/$route_id" "" "$token"; expect_status 200 "Confirmar ruta tenant A intacta"
request GET "/Drivers/$driver_id" "" "$token"; expect_status 200 "Confirmar Driver tenant A intacto"
request GET "/Vehicles/$vehicle_id" "" "$token"; expect_status 200 "Confirmar vehículo tenant A intacto"

echo "OK: aislamiento HTTP/MySQL entre tenants; autorización User/Manager/Logistics; revocación de sesiones; Vehicles/Drivers/Routes/RoutePositions."
