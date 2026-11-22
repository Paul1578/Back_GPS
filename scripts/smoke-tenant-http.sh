#!/usr/bin/env bash
set -euo pipefail

api_base="${FLETFLOW_API_URL:-http://127.0.0.1:5048/api}"
admin_email="${FLETFLOW_E2E_ADMIN_EMAIL:-}"
admin_password="${FLETFLOW_E2E_ADMIN_PASSWORD:-}"
db_name="${DB_NAME:-}"
mysql_host="${MYSQL_HOST:-127.0.0.1}"
mysql_port="${MYSQL_PORT:-3306}"
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
logistics_user_id=""
public_registration_email=""
route_name=""
plate=""
restricted_attempt_plate=""
document_number=""
manager_vehicle_id=""
manager_driver_id=""
manager_route_name=""
manager_route_id=""
logistics_route_id=""
logistics_route_name=""

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
    if [[ -n "$manager_route_id" ]]; then
      sql+=" DELETE FROM RoutePositions WHERE RouteId='$manager_route_id'; DELETE FROM Routes WHERE Id='$manager_route_id';"
    elif [[ -n "$manager_route_name" ]]; then
      sql+=" DELETE rp FROM RoutePositions rp JOIN Routes r ON r.Id=rp.RouteId WHERE r.Name='$manager_route_name'; DELETE FROM Routes WHERE Name='$manager_route_name';"
    fi
    if [[ -n "$logistics_route_id" ]]; then
      sql+=" DELETE FROM RoutePositions WHERE RouteId='$logistics_route_id'; DELETE FROM Routes WHERE Id='$logistics_route_id';"
    elif [[ -n "$logistics_route_name" ]]; then
      sql+=" DELETE rp FROM RoutePositions rp JOIN Routes r ON r.Id=rp.RouteId WHERE r.Name='$logistics_route_name'; DELETE FROM Routes WHERE Name='$logistics_route_name';"
    fi
    if [[ -n "$driver_id" ]]; then sql+=" DELETE FROM Drivers WHERE Id='$driver_id';"
    elif [[ -n "$document_number" ]]; then sql+=" DELETE FROM Drivers WHERE DocumentNumber='$document_number';"; fi
    if [[ -n "$manager_driver_id" ]]; then sql+=" DELETE FROM Drivers WHERE Id='$manager_driver_id';"; fi
    if [[ -n "$vehicle_id" ]]; then sql+=" DELETE FROM Vehicles WHERE Id='$vehicle_id';"
    elif [[ -n "$plate" ]]; then sql+=" DELETE FROM Vehicles WHERE Plate='$plate';"; fi
    if [[ -n "$manager_vehicle_id" ]]; then sql+=" UPDATE Vehicles SET Status=0 WHERE Id='$manager_vehicle_id'; DELETE FROM Vehicles WHERE Id='$manager_vehicle_id';"; fi
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
    if [[ -n "$public_registration_email" ]]; then
      sql+=" SET @qa_user_id=(SELECT Id FROM Users WHERE Email='$public_registration_email'); DELETE FROM RefreshTokens WHERE UserId=@qa_user_id; DELETE FROM PasswordResetTokens WHERE UserId=@qa_user_id; DELETE FROM UserRoles WHERE UserId=@qa_user_id; DELETE FROM Users WHERE Id=@qa_user_id;"
    fi
    sql+=" COMMIT;"
    mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" -e "$sql" >/dev/null || {
      echo "No se pudieron limpiar los fixtures creados por smoke-tenant-http.sh." >&2
      status=1
    }
    if [[ -n "$manager_vehicle_id" && (-n "$manager_route_id" || -n "$manager_route_name") && \
      (-n "$logistics_route_id" || -n "$logistics_route_name") ]]; then
      local remaining
      if remaining="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" \
        --batch --skip-column-names "$db_name" -e "SELECT CONCAT((SELECT COUNT(*) FROM Routes WHERE Id IN ('$manager_route_id','$logistics_route_id') OR Name IN ('$manager_route_name','$logistics_route_name')), ':', (SELECT COUNT(*) FROM RoutePositions rp JOIN Routes r ON r.Id=rp.RouteId WHERE r.Id IN ('$manager_route_id','$logistics_route_id') OR r.Name IN ('$manager_route_name','$logistics_route_name')), ':', (SELECT COUNT(*) FROM Vehicles WHERE Id='$manager_vehicle_id'));" \
        2>"$work_dir/cleanup_verify.stderr")"; then
        :
      else
        echo "No se pudo verificar la limpieza de rutas, GPS y vehículo del smoke:" >&2
        cat "$work_dir/cleanup_verify.stderr" >&2
        status=1
        remaining="unknown"
      fi
      if [[ "$remaining" != "0:0:0" && "$remaining" != "unknown" ]]; then
        echo "Cleanup dejó rutas/posiciones/vehículo QA (conteos: $remaining)." >&2
        status=1
      fi
      if [[ "$remaining" == "0:0:0" ]]; then
        echo "Cleanup verificado: 0 rutas, 0 posiciones GPS y 0 vehículos Logistics/Manager." >&2
      fi
    fi
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

public_registration_email="fletflow.e2e.register.ci.$(date +%s%N | tail -c 9)@example.test"
public_registration_password="$(openssl rand -hex 24)"
request POST /Auth/register "$(jq -nc --arg username "Public registration QA" --arg email "$public_registration_email" --arg password "$public_registration_password" '{username:$username,email:$email,password:$password,roleName:"Driver"}')" ""
expect_status 200 "Registro público crea empresa raíz ignorando el rol solicitado"
mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT CONCAT(COUNT(DISTINCT ur.RoleId), ':', COALESCE(GROUP_CONCAT(DISTINCT LOWER(r.Name)), 'none')) FROM Users u LEFT JOIN UserRoles ur ON ur.UserId=u.Id LEFT JOIN Roles r ON r.Id=ur.RoleId WHERE u.Email='$public_registration_email';" >"$work_dir/registration_role.txt"
registered_role_state="$(cat "$work_dir/registration_role.txt")"
[[ "$registered_role_state" == "1:admin" ]] || { echo "Registro público produjo roles inesperados ($registered_role_state)." >&2; exit 1; }
public_registration_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"
public_registration_user_id="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT Id FROM Users WHERE Email='$public_registration_email';")"
public_registration_is_root="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT IF(OwnerUserId IS NULL, 1, 0) FROM Users WHERE Email='$public_registration_email';")"
[[ "$public_registration_is_root" == "1" ]] || { echo "La cuenta raíz de registro recibió un OwnerUserId inesperado." >&2; exit 1; }
public_jwt_payload="$(printf '%s' "$public_registration_token" | cut -d. -f2 | tr '_-' '/+')"
while (( ${#public_jwt_payload} % 4 != 0 )); do public_jwt_payload+="="; done
public_jwt_owner_id="$(printf '%s' "$public_jwt_payload" | base64 --decode | jq -er '.owner_id')"
[[ "$public_jwt_owner_id" == "$public_registration_user_id" ]] || { echo "El JWT no limita el tenant al ID de la cuenta raíz." >&2; exit 1; }
request GET /Vehicles "" "$public_registration_token"
expect_status 200 "Admin raíz del nuevo tenant puede consultar su flota"
jq -e 'type == "array" and length == 0' "$work_dir/response.json" >/dev/null || {
  echo "La flota inicial del tenant recién registrado no está vacía o tiene formato inesperado." >&2
  exit 1
}

suffix="$(date +%s%N | tail -c 10)"
restricted_user_email="fletflow.e2e.user.ci.$suffix@example.test"
restricted_user_password="$(openssl rand -hex 24)"
request POST /Users "$(jq -nc --arg email "$restricted_user_email" --arg password "$restricted_user_password" --arg username "E2E CI restricted $suffix" '{username:$username,email:$email,password:$password,roleName:"User"}')"
expect_status 200 "Crear usuario de permisos limitados"
mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  -e "UPDATE Users SET IsActive=1, MustChangePassword=0 WHERE Email='$restricted_user_email';" >/dev/null
request POST /Auth/login "$(jq -nc --arg email "$restricted_user_email" --arg password "$restricted_user_password" '{email:$email,password:$password}')" ""
expect_status 200 "Login usuario de permisos limitados"
restricted_user_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"
restricted_user_refresh_token="$(jq -er '.refreshToken' "$work_dir/response.json")"
restricted_attempt_plate="E2EQA${suffix: -9}"
request GET /Vehicles "" "$restricted_user_token"; expect_status 403 "Listar vehículos como User"
request GET /Routes "" "$restricted_user_token"; expect_status 403 "Listar rutas como User"
request POST /Vehicles "$(jq -nc --arg plate "$restricted_attempt_plate" '{plate:$plate,brand:"Blocked",model:"Blocked",year:2025}')" "$restricted_user_token"; expect_status 403 "Crear vehículo como User"

restricted_user_new_password="$(openssl rand -hex 24)"
request POST /Auth/change-password "$(jq -nc --arg current "$restricted_user_password" --arg next "$restricted_user_new_password" '{currentPassword:$current,newPassword:$next,confirmNewPassword:$next}')" "$restricted_user_token"
expect_status 200 "Cambiar contraseña del usuario"
request GET /Auth/me "" "$restricted_user_token"
expect_status 401 "Invalidar access token anterior al cambiar contraseña"
request POST /Auth/refresh "$(jq -nc --arg refreshToken "$restricted_user_refresh_token" '{refreshToken:$refreshToken}')" ""
expect_status 401 "Invalidar refresh token al cambiar contraseña"
request POST /Auth/login "$(jq -nc --arg email "$restricted_user_email" --arg password "$restricted_user_password" '{email:$email,password:$password}')" ""
expect_status 401 "Rechazar contraseña anterior"
request POST /Auth/login "$(jq -nc --arg email "$restricted_user_email" --arg password "$restricted_user_new_password" '{email:$email,password:$password}')" ""
expect_status 200 "Aceptar contraseña nueva"

deactivated_user_email="fletflow.e2e.deactivated.ci.$suffix@example.test"
deactivated_user_password="$(openssl rand -hex 24)"
request POST /Users "$(jq -nc --arg email "$deactivated_user_email" --arg password "$deactivated_user_password" --arg username "E2E CI deactivated $suffix" '{username:$username,email:$email,password:$password,roleName:"Manager"}')"
expect_status 200 "Crear usuario para probar revocación"
deactivated_user_id="$(jq -er '.id' "$work_dir/response.json")"
mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  -e "UPDATE Users SET IsActive=1, MustChangePassword=0 WHERE Id='$deactivated_user_id';" >/dev/null
request POST /Auth/login "$(jq -nc --arg email "$deactivated_user_email" --arg password "$deactivated_user_password" '{email:$email,password:$password}')" ""
expect_status 200 "Login de usuario antes de desactivar"
deactivated_access_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"
deactivated_refresh_token="$(jq -er '.refreshToken' "$work_dir/response.json")"
request GET /Vehicles "" "$deactivated_access_token"; expect_status 200 "Listar vehículos como Manager"
request GET /Drivers "" "$deactivated_access_token"; expect_status 200 "Listar conductores como Manager"
request GET /Routes "" "$deactivated_access_token"; expect_status 200 "Listar rutas como Manager"
manager_plate="E2EM${suffix: -9}"
manager_document="E2ED${suffix: -9}"
role_points='[{"lat":-0.18,"lng":-78.46,"name":"Role origin"},{"lat":-0.185,"lng":-78.465,"name":"Role pickup and delivery","operation":3},{"lat":-0.19,"lng":-78.47,"name":"Role destination"}]'
request POST /Vehicles "$(jq -nc --arg plate "$manager_plate" '{plate:$plate,brand:"CI Manager",model:"Fleet",year:2025}')" "$deactivated_access_token"
expect_status 201 "Crear vehículo como Manager"
manager_vehicle_id="$(jq -er '.id' "$work_dir/response.json")"
request GET /Vehicles "" "$public_registration_token"; expect_status 200 "Flota del registro sigue aislada tras crear vehículo en otro tenant"
jq -e 'type == "array" and length == 0' "$work_dir/response.json" >/dev/null || { echo "El registro público vio flota de otro tenant." >&2; exit 1; }
request GET "/Vehicles/$manager_vehicle_id" "" "$public_registration_token"; expect_status 404 "Registro público no puede consultar vehículo de otro tenant"
request POST /Drivers "$(jq -nc --arg document "$manager_document" '{firstName:"CI Manager",lastName:"Driver",documentNumber:$document,phoneNumber:"0990000000"}')" "$deactivated_access_token"
expect_status 201 "Crear Driver como Manager"
manager_driver_id="$(jq -er '.id' "$work_dir/response.json")"
manager_route_name="E2E manager route $suffix"
manager_route_payload="$(jq -nc --arg name "$manager_route_name" --arg vehicleId "$manager_vehicle_id" --arg driverId "$manager_driver_id" --argjson points "$role_points" '{name:$name,cargoDescription:"Role matrix",vehicleId:$vehicleId,driverId:$driverId,points:$points}')"
request POST /Routes "$manager_route_payload" "$deactivated_access_token"
expect_status 201 "Crear ruta como Manager"
manager_route_id="$(jq -er '.id' "$work_dir/response.json")"
request PUT "/Routes/$manager_route_id" "$(jq -nc --arg name "$manager_route_name updated" --arg vehicleId "$manager_vehicle_id" --arg driverId "$manager_driver_id" --argjson points "$role_points" '{name:$name,cargoDescription:"Role matrix updated",vehicleId:$vehicleId,driverId:$driverId,points:$points}')" "$deactivated_access_token"
expect_status 200 "Editar ruta como Manager"
request DELETE "/Routes/$manager_route_id" "" "$deactivated_access_token"
expect_status 204 "Dar de baja ruta como Manager antes de reutilizar agenda"
request POST /Routes "$manager_route_payload" "$restricted_user_token"
expect_status 403 "Denegar creación de ruta a User"
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
logistics_user_id="$(jq -er '.id' "$work_dir/response.json")"
mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  -e "UPDATE Users SET IsActive=1, MustChangePassword=0 WHERE Email='$logistics_user_email';" >/dev/null
request POST /Auth/login "$(jq -nc --arg email "$logistics_user_email" --arg password "$logistics_user_password" '{email:$email,password:$password}')" ""
expect_status 200 "Login Logistics"
logistics_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"
logistics_refresh_token="$(jq -er '.refreshToken' "$work_dir/response.json")"
request GET /Routes "" "$logistics_token"; expect_status 200 "Listar rutas como Logistics"
request GET /Vehicles "" "$logistics_token"; expect_status 403 "Denegar vehículos a Logistics"
request GET /Drivers "" "$logistics_token"; expect_status 403 "Denegar conductores a Logistics"
request POST /Vehicles "$(jq -nc --arg plate "BLOCK${suffix: -8}" '{plate:$plate,brand:"Blocked",model:"Blocked",year:2025}')" "$logistics_token"
expect_status 403 "Denegar creación de vehículo a Logistics"
request POST /Drivers '{"firstName":"Blocked","lastName":"Blocked","documentNumber":"BLOCK00001","phoneNumber":"0990000000"}' "$logistics_token"
expect_status 403 "Denegar creación de Driver a Logistics"
request POST /Auth/logout-all '{}' "$logistics_token"
expect_status 200 "Cerrar todas las sesiones Logistics"
request POST /Auth/refresh "$(jq -nc --arg refreshToken "$logistics_refresh_token" '{refreshToken:$refreshToken}')" ""
expect_status 401 "Revocar refresh por logout-all"
request POST /Auth/login "$(jq -nc --arg email "$logistics_user_email" --arg password "$logistics_user_password" '{email:$email,password:$password}')" ""
expect_status 200 "Nueva sesión Logistics para probar cambio de rol"
logistics_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"
logistics_refresh_token="$(jq -er '.refreshToken' "$work_dir/response.json")"
logistics_route_name="${manager_route_name} logistics"
logistics_route_payload="$(jq -nc --arg name "$logistics_route_name" --arg vehicleId "$manager_vehicle_id" --arg driverId "$manager_driver_id" --argjson points "$role_points" '{name:$name,cargoDescription:"Logistics create",vehicleId:$vehicleId,driverId:$driverId,points:$points}')"
request POST /Routes "$logistics_route_payload" "$logistics_token"
expect_status 201 "Crear ruta como Logistics con agenda liberada por Manager"
logistics_route_id="$(jq -er '.id' "$work_dir/response.json")"
request GET "/Routes/$logistics_route_id" "" "$logistics_token"
expect_status 200 "Leer ruta creada por Logistics"
jq -e --arg id "$logistics_route_id" --arg name "$logistics_route_name" --arg vehicleId "$manager_vehicle_id" --arg driverId "$manager_driver_id" \
  '.id == $id and .name == $name and .vehicleId == $vehicleId and .driverId == $driverId and .isActive == true and (.points | length == 3) and .points[1].operation == 3 and .points[1].status == 0' \
  "$work_dir/response.json" >/dev/null || { echo "La ruta de Logistics no coincide con lo persistido." >&2; cat "$work_dir/response.json" >&2; exit 1; }
logistics_route_updated_name="${logistics_route_name} updated"
request PUT "/Routes/$logistics_route_id" "$(jq -nc --arg name "$logistics_route_updated_name" --arg vehicleId "$manager_vehicle_id" --arg driverId "$manager_driver_id" --argjson points "$role_points" '{name:$name,cargoDescription:"Logistics update",vehicleId:$vehicleId,driverId:$driverId,points:$points}')" "$logistics_token"
expect_status 200 "Editar ruta como Logistics"
request GET "/Routes/$logistics_route_id" "" "$logistics_token"
expect_status 200 "Confirmar edición de ruta Logistics persistida"
jq -e --arg id "$logistics_route_id" --arg name "$logistics_route_updated_name" --arg vehicleId "$manager_vehicle_id" --arg driverId "$manager_driver_id" \
  '.id == $id and .name == $name and .vehicleId == $vehicleId and .driverId == $driverId and .isActive == true and (.points | length == 3) and .points[1].operation == 3 and .points[1].status == 0' \
  "$work_dir/response.json" >/dev/null || { echo "La edición de Logistics no quedó persistida." >&2; cat "$work_dir/response.json" >&2; exit 1; }

request PUT "/Routes/$logistics_route_id/status" '{"status":1}' "$logistics_token"
expect_status 204 "Iniciar ruta como Logistics"
request GET "/Routes/$logistics_route_id" "" "$logistics_token"
expect_status 200 "Leer ruta iniciada como Logistics"
jq -e '.status == 1 and .startedAt != null and .points[1].status == 0' "$work_dir/response.json" >/dev/null \
  || { echo "La ruta/vehículo no reflejan correctamente el inicio." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request GET "/Vehicles/$manager_vehicle_id" "" "$token"
expect_status 200 "Leer vehículo en ruta como Admin QA"
jq -e '.status == 1 and .isActive == true' "$work_dir/response.json" >/dev/null \
  || { echo "El vehículo no pasó a OnRoute al iniciar la ruta." >&2; cat "$work_dir/response.json" >&2; exit 1; }

request PUT "/Routes/$logistics_route_id/points/1/status" '{"status":1}' "$logistics_token"
expect_status 200 "Iniciar parada operativa como Logistics"
request POST "/Routes/$logistics_route_id/points/1/evidence" \
  "$(jq -nc --arg content "E2E QA Logistics $suffix" '{type:"note",content:$content,description:"Evidencia de entrega/recogida de smoke"}')" "$logistics_token"
expect_status 200 "Registrar evidencia de parada como Logistics"
request PUT "/Routes/$logistics_route_id/points/1/status" '{"status":2}' "$logistics_token"
expect_status 200 "Completar parada operativa como Logistics"
jq -e '.points[1].operation == 3 and .points[1].status == 2 and (.points[1].evidence | length == 1) and .points[1].evidence[0].type == "note"' \
  "$work_dir/response.json" >/dev/null \
  || { echo "El estado/evidencia de la parada no quedó persistido." >&2; cat "$work_dir/response.json" >&2; exit 1; }

request POST "/Routes/$logistics_route_id/positions" \
  "$(jq -nc --argjson latitude -0.186 --argjson longitude -78.466 --argjson speed 32 --argjson heading 90 '{latitude:$latitude,longitude:$longitude,speedKmh:$speed,heading:$heading}')" "$logistics_token"
expect_status 200 "Registrar posición GPS con ruta en progreso como Logistics"
jq -e --arg routeId "$logistics_route_id" '.routeId == $routeId and .latitude == -0.186 and .longitude == -78.466 and .speedKmh == 32 and .heading == 90' \
  "$work_dir/response.json" >/dev/null \
  || { echo "La respuesta GPS no coincide con la posición enviada." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request GET "/Routes/$logistics_route_id/positions" "" "$logistics_token"
expect_status 200 "Consultar posición GPS persistida como Logistics"
jq -e 'length == 1 and .[0].latitude == -0.186 and .[0].longitude == -78.466 and .[0].speedKmh == 32 and .[0].heading == 90' \
  "$work_dir/response.json" >/dev/null \
  || { echo "La posición GPS no quedó persistida." >&2; cat "$work_dir/response.json" >&2; exit 1; }

request PUT "/Routes/$logistics_route_id/status" '{"status":2}' "$logistics_token"
expect_status 204 "Completar ruta como Logistics después de cerrar parada/evidencia"
request GET "/Routes/$logistics_route_id" "" "$logistics_token"
expect_status 200 "Confirmar ruta completada como Logistics"
jq -e '.status == 2 and .isActive == true and .startedAt != null and .endedAt != null and .points[1].status == 2 and (.points[1].evidence | length == 1)' \
  "$work_dir/response.json" >/dev/null \
  || { echo "La ruta completada no conserva estado, timestamps, parada y evidencia." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request GET "/Vehicles/$manager_vehicle_id" "" "$token"
expect_status 200 "Leer vehículo tras completar ruta como Admin QA"
jq -e '.status == 0 and .isActive == true' "$work_dir/response.json" >/dev/null \
  || { echo "El vehículo no volvió a Available al completar la ruta." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request DELETE "/Routes/$logistics_route_id" "" "$logistics_token"
expect_status 204 "Dar de baja ruta completada como Logistics"
request PUT "/Users/$logistics_user_id/role" '{"roleName":"User"}' "$token"
expect_status 200 "Cambiar rol Logistics a User"
request GET /Auth/me "" "$logistics_token"
expect_status 401 "Invalidar access token tras cambio de rol"
request POST /Auth/refresh "$(jq -nc --arg refreshToken "$logistics_refresh_token" '{refreshToken:$refreshToken}')" ""
expect_status 401 "Invalidar refresh token tras cambio de rol"
request PUT "/Users/$logistics_user_id/role" '{"roleName":"Logistics"}' "$token"
expect_status 200 "Restaurar rol Logistics para probar JWT antiguo"
request GET /Auth/me "" "$logistics_token"
expect_status 401 "Mantener invalidado access token tras restaurar el rol anterior"
request POST /Auth/refresh "$(jq -nc --arg refreshToken "$logistics_refresh_token" '{refreshToken:$refreshToken}')" ""
expect_status 401 "Mantener invalidado refresh token tras restaurar el rol anterior"

other_tenant_email="fletflow.e2e.admin.other.$suffix@example.test"
other_tenant_password="$(openssl rand -hex 24)"
plate="E2EQA${suffix: -9}"
document_number="E2EQA${suffix: -9}"
request POST /Auth/register "$(jq -nc --arg email "$other_tenant_email" --arg password "$other_tenant_password" --arg username "E2E tenant B $suffix" '{username:$username,email:$email,password:$password,roleName:"Admin"}')" ""
expect_status 200 "Registro tenant B"

# El registro público crea el tenant raíz Admin sin elevar manualmente el rol en SQL.

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

echo "OK: aislamiento HTTP/MySQL entre tenants; autorización User/Manager/Logistics; Logistics creó/editó/leyó ruta, inició operación pickup+delivery con evidencia, registró GPS, completó ruta y verificó vehículo Available; cleanup de Vehicles/Drivers/Routes/RoutePositions; revocación de sesiones."
