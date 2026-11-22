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
duplicate_email_race=""
deactivated_user_email=""
inactive_driver_user_email=""
inactive_linked_driver_id=""
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
logistics_gps_recorded_at=""
restricted_user_id=""
root_user_id=""
driver_race_email_a=""
driver_race_email_b=""
driver_race_document=""

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
    if [[ -n "$driver_race_document" ]]; then sql+=" DELETE FROM Drivers WHERE DocumentNumber='$driver_race_document';"; fi
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
    if [[ -n "$duplicate_email_race" ]]; then
      sql+=" SET @qa_user_id=(SELECT Id FROM Users WHERE Email='$duplicate_email_race'); DELETE FROM RefreshTokens WHERE UserId=@qa_user_id; DELETE FROM PasswordResetTokens WHERE UserId=@qa_user_id; DELETE FROM UserRoles WHERE UserId=@qa_user_id; DELETE FROM Users WHERE Id=@qa_user_id;"
    fi
    if [[ -n "$driver_race_email_a" ]]; then
      sql+=" SET @qa_user_id=(SELECT Id FROM Users WHERE Email='$driver_race_email_a'); DELETE FROM RefreshTokens WHERE UserId=@qa_user_id; DELETE FROM PasswordResetTokens WHERE UserId=@qa_user_id; DELETE FROM UserRoles WHERE UserId=@qa_user_id; DELETE FROM Users WHERE Id=@qa_user_id;"
    fi
    if [[ -n "$driver_race_email_b" ]]; then
      sql+=" SET @qa_user_id=(SELECT Id FROM Users WHERE Email='$driver_race_email_b'); DELETE FROM RefreshTokens WHERE UserId=@qa_user_id; DELETE FROM PasswordResetTokens WHERE UserId=@qa_user_id; DELETE FROM UserRoles WHERE UserId=@qa_user_id; DELETE FROM Users WHERE Id=@qa_user_id;"
    fi
    if [[ -n "$deactivated_user_email" ]]; then
      sql+=" SET @qa_user_id=(SELECT Id FROM Users WHERE Email='$deactivated_user_email'); DELETE FROM RefreshTokens WHERE UserId=@qa_user_id; DELETE FROM PasswordResetTokens WHERE UserId=@qa_user_id; DELETE FROM UserRoles WHERE UserId=@qa_user_id; DELETE FROM Users WHERE Id=@qa_user_id;"
    fi
    if [[ -n "$inactive_driver_user_email" ]]; then
      sql+=" SET @qa_user_id=(SELECT Id FROM Users WHERE Email='$inactive_driver_user_email'); DELETE FROM Drivers WHERE Id='$inactive_linked_driver_id'; DELETE FROM RefreshTokens WHERE UserId=@qa_user_id; DELETE FROM PasswordResetTokens WHERE UserId=@qa_user_id; DELETE FROM UserRoles WHERE UserId=@qa_user_id; DELETE FROM Users WHERE Id=@qa_user_id;"
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
    if [[ -n "$duplicate_email_race" ]]; then
      duplicate_email_remaining="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" \
        --batch --skip-column-names "$db_name" \
        -e "SELECT COUNT(*) FROM Users WHERE Email='$duplicate_email_race';" 2>"$work_dir/email_race_cleanup_verify.stderr")" || {
        echo "No se pudo verificar cleanup de la carrera de email." >&2
        cat "$work_dir/email_race_cleanup_verify.stderr" >&2
        status=1
        duplicate_email_remaining="unknown"
      }
      if [[ "$duplicate_email_remaining" != "0" && "$duplicate_email_remaining" != "unknown" ]]; then
        echo "Cleanup dejó $duplicate_email_remaining cuenta(s) de la carrera de email." >&2
        status=1
      fi
    fi
    if [[ -n "$inactive_driver_user_email" ]]; then
      local inactive_driver_remaining
      if inactive_driver_remaining="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" \
        --batch --skip-column-names "$db_name" \
        -e "SELECT CONCAT((SELECT COUNT(*) FROM Drivers WHERE Id='$inactive_linked_driver_id'), ':', (SELECT COUNT(*) FROM Users WHERE Email='$inactive_driver_user_email'));" 2>"$work_dir/inactive_cleanup_verify.stderr")"; then
        if [[ "$inactive_driver_remaining" != "0:0" ]]; then
          echo "Cleanup dejó Driver/User vinculado de QA (conteos: $inactive_driver_remaining)." >&2
          status=1
        fi
      else
        echo "No se pudo verificar la limpieza del Driver/User de QA:" >&2
        cat "$work_dir/inactive_cleanup_verify.stderr" >&2
        status=1
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
request GET /Roles "" "$token"; expect_status 200 "Consultar catálogo global de roles como Admin de tenant"
request POST /Roles '{"name":"Admin"}' "$token"; expect_status 403 "Impedir modificar catálogo global de roles desde un tenant"
request DELETE "/Roles/E2EQA_missing" "" "$token"; expect_status 403 "Impedir eliminar roles globales desde un tenant"

known_recovery_metrics="$(curl --silent --show-error --output "$work_dir/forgot-known.json" --write-out '%{http_code} %{time_total}' \
  --header 'Content-Type: application/json' --data "$(jq -nc --arg email "$admin_email" '{email:$email}')" \
  "$api_base/Auth/forgot-password")"
unknown_recovery_metrics="$(curl --silent --show-error --output "$work_dir/forgot-unknown.json" --write-out '%{http_code} %{time_total}' \
  --header 'Content-Type: application/json' --data "$(jq -nc --arg email "unknown.$(date +%s%N)@example.test" '{email:$email}')" \
  "$api_base/Auth/forgot-password")"
read -r known_recovery_status known_recovery_seconds <<<"$known_recovery_metrics"
read -r unknown_recovery_status unknown_recovery_seconds <<<"$unknown_recovery_metrics"
[[ "$known_recovery_status" == "200" && "$unknown_recovery_status" == "200" ]] || {
  echo "Forgot-password devolvió estados diferentes/inesperados: conocido=$known_recovery_status desconocido=$unknown_recovery_status." >&2
  exit 1
}
jq -S . "$work_dir/forgot-known.json" >"$work_dir/forgot-known.sorted.json"
jq -S . "$work_dir/forgot-unknown.json" >"$work_dir/forgot-unknown.sorted.json"
cmp -s "$work_dir/forgot-known.sorted.json" "$work_dir/forgot-unknown.sorted.json" || {
  echo "Forgot-password expuso respuestas diferentes para correo existente e inexistente." >&2
  exit 1
}
awk -v known="$known_recovery_seconds" -v unknown="$unknown_recovery_seconds" \
  'BEGIN { if (known < 0.20 || unknown < 0.20) exit 1 }' || {
  echo "Forgot-password no respetó la duración mínima uniforme (conocido=$known_recovery_seconds desconocido=$unknown_recovery_seconds)." >&2
  exit 1
}

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
email_local_prefix="fletflow$suffix"
email_local_padding=$((187 - ${#email_local_prefix}))
restricted_user_email="$(printf '%*s' "$email_local_padding" '' | tr ' ' a)${email_local_prefix}@example.test"
[[ ${#restricted_user_email} -eq 200 ]] || { echo "El fixture de correo límite no tiene 200 caracteres." >&2; exit 1; }
restricted_user_password="$(openssl rand -hex 24)"
max_user_name="$(printf '%100s' '' | tr ' ' u)"
max_person_name="$(printf '%100s' '' | tr ' ' n)"
too_long_name="$(printf '%101s' '' | tr ' ' n)"
too_long_email="$(printf '%188s' '' | tr ' ' x)@example.test"
too_long_username="$(printf '%101s' '' | tr ' ' u)"
request POST /Users "$(jq -nc --arg email "length.username.$suffix@example.test" --arg username "$too_long_username" '{username:$username,email:$email,roleName:"User"}')"
expect_status 400 "Rechazar username mayor a 100 caracteres"
request POST /Users "$(jq -nc --arg email "$too_long_email" --arg username "Length email $suffix" '{username:$username,email:$email,roleName:"User"}')"
expect_status 400 "Rechazar email mayor a 200 caracteres"
request POST /Users "$(jq -nc --arg email "length.first.$suffix@example.test" --arg username "Length first $suffix" --arg firstName "$too_long_name" '{username:$username,email:$email,firstName:$firstName,roleName:"User"}')"
expect_status 400 "Rechazar firstName mayor a 100 caracteres"
request POST /Users "$(jq -nc --arg email "length.last.$suffix@example.test" --arg username "Length last $suffix" --arg lastName "$too_long_name" '{username:$username,email:$email,lastName:$lastName,roleName:"User"}')"
expect_status 400 "Rechazar lastName mayor a 100 caracteres"
invalid_length_user_count="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT COUNT(*) FROM Users WHERE Email IN ('length.username.$suffix@example.test','length.first.$suffix@example.test','length.last.$suffix@example.test','$too_long_email');")"
[[ "$invalid_length_user_count" == "0" ]] || { echo "Una solicitud de longitud inválida dejó $invalid_length_user_count usuarios persistidos." >&2; exit 1; }
request POST /Users "$(jq -nc --arg email "$restricted_user_email" --arg password "$restricted_user_password" --arg username "$max_user_name" --arg firstName "$max_person_name" --arg lastName "$max_person_name" '{username:$username,email:$email,password:$password,roleName:"User",firstName:$firstName,lastName:$lastName}')"
expect_status 200 "Crear usuario de permisos limitados"
mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  -e "UPDATE Users SET IsActive=1, MustChangePassword=0 WHERE Email='$restricted_user_email';" >/dev/null
request POST /Auth/login "$(jq -nc --arg email "$restricted_user_email" --arg password "$restricted_user_password" '{email:$email,password:$password}')" ""
expect_status 200 "Login usuario de permisos limitados"
restricted_user_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"
restricted_user_refresh_token="$(jq -er '.refreshToken' "$work_dir/response.json")"

duplicate_email_race="fletflow.e2e.email.race.$suffix@example.test"
duplicate_email_payload="$(jq -nc --arg email "$duplicate_email_race" --arg password "$(openssl rand -hex 24)" --arg username "Email race A $suffix" '{username:$username,email:$email,password:$password,roleName:"User"}')"
duplicate_email_payload_b="$(jq -nc --arg email "$duplicate_email_race" --arg password "$(openssl rand -hex 24)" --arg username "Email race B $suffix" '{username:$username,email:$email,password:$password,roleName:"User"}')"
curl --silent --show-error --output "$work_dir/email_race_a.json" --write-out '%{http_code}' --request POST "$api_base/Users" \
  --header 'Content-Type: application/json' --header "Authorization: Bearer $token" --data "$duplicate_email_payload" >"$work_dir/email_race_a.status" &
email_race_a_pid=$!
curl --silent --show-error --output "$work_dir/email_race_b.json" --write-out '%{http_code}' --request POST "$api_base/Users" \
  --header 'Content-Type: application/json' --header "Authorization: Bearer $token" --data "$duplicate_email_payload_b" >"$work_dir/email_race_b.status" &
email_race_b_pid=$!
wait "$email_race_a_pid"
wait "$email_race_b_pid"
email_race_a_status="$(cat "$work_dir/email_race_a.status")"
email_race_b_status="$(cat "$work_dir/email_race_b.status")"
[[ "$email_race_a_status:$email_race_b_status" == "200:409" || "$email_race_a_status:$email_race_b_status" == "409:200" ]] || {
  echo "Alta concurrente con email duplicado debía resultar 200/409, recibió $email_race_a_status/$email_race_b_status." >&2
  cat "$work_dir/email_race_a.json" "$work_dir/email_race_b.json" >&2
  exit 1
}
duplicate_email_rows="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT CONCAT((SELECT COUNT(*) FROM Users WHERE Email='$duplicate_email_race'), ':', (SELECT COUNT(*) FROM UserRoles ur JOIN Users u ON u.Id=ur.UserId WHERE u.Email='$duplicate_email_race'), ':', (SELECT COUNT(*) FROM PasswordResetTokens pr JOIN Users u ON u.Id=pr.UserId WHERE u.Email='$duplicate_email_race'));")"
[[ "$duplicate_email_rows" == "1:1:1" ]] || { echo "La carrera de email dejó persistencia parcial/inconsistente ($duplicate_email_rows)." >&2; exit 1; }
restricted_attempt_plate="E2EQA${suffix: -9}"
request GET /Vehicles "" "$restricted_user_token"; expect_status 403 "Listar vehículos como User"
request GET /Routes "" "$restricted_user_token"; expect_status 403 "Listar rutas como User"
request GET /Routes/assignment-options "" "$restricted_user_token"; expect_status 403 "Denegar opciones de asignación a User"
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
restricted_user_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"
restricted_user_refresh_token="$(jq -er '.refreshToken' "$work_dir/response.json")"
request POST /Auth/logout-all "" "$restricted_user_token"
expect_status 200 "Cerrar todas las sesiones del usuario"
request GET /Auth/me "" "$restricted_user_token"
expect_status 401 "Invalidar access JWT tras logout-all"
request POST /Auth/refresh "$(jq -nc --arg refreshToken "$restricted_user_refresh_token" '{refreshToken:$refreshToken}')" ""
expect_status 401 "Revocar refresh token tras logout-all"
request POST /Auth/login "$(jq -nc --arg email "$restricted_user_email" --arg password "$restricted_user_new_password" '{email:$email,password:$password}')" ""
expect_status 200 "Permitir sesión nueva después de logout-all"
restricted_user_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"

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
manager_route_created_at="$(jq -er '.createdAtUtc | select(type == "string" and endswith("Z"))' "$work_dir/response.json")"
[[ -n "$manager_route_created_at" ]] || { echo "La API no devolvió CreatedAtUtc al crear la ruta." >&2; exit 1; }
manager_route_created_at_count="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT COUNT(*) FROM Routes WHERE Id='$manager_route_id' AND CreatedAtUtc IS NOT NULL;")"
[[ "$manager_route_created_at_count" == "1" ]] || { echo "CreatedAtUtc no quedó persistido en MySQL para la ruta Manager." >&2; exit 1; }
manager_route_created_at_db="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT DATE_FORMAT(CreatedAtUtc, '%Y-%m-%dT%H:%i:%s.%fZ') FROM Routes WHERE Id='$manager_route_id';")"
normalize_mysql_datetime() { sed -E 's/(\.[0-9]{6})[0-9]*Z$/\1Z/' <<<"$1"; }
[[ "$(normalize_mysql_datetime "$manager_route_created_at")" == "$manager_route_created_at_db" ]] || { echo "CreatedAtUtc de API no coincide con el valor MySQL." >&2; exit 1; }
request PUT "/Routes/$manager_route_id" "$(jq -nc --arg name "$manager_route_name updated" --arg vehicleId "$manager_vehicle_id" --arg driverId "$manager_driver_id" --argjson points "$role_points" '{name:$name,cargoDescription:"Role matrix updated",vehicleId:$vehicleId,driverId:$driverId,points:$points}')" "$deactivated_access_token"
expect_status 200 "Editar ruta como Manager"
manager_route_updated_created_at="$(jq -er '.createdAtUtc' "$work_dir/response.json")"
manager_route_updated_created_at_db="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT DATE_FORMAT(CreatedAtUtc, '%Y-%m-%dT%H:%i:%s.%fZ') FROM Routes WHERE Id='$manager_route_id';")"
[[ "$manager_route_updated_created_at_db" == "$manager_route_created_at_db" && \
   "$(normalize_mysql_datetime "$manager_route_updated_created_at")" == "$manager_route_created_at_db" ]] || {
  echo "Editar la ruta cambió CreatedAtUtc o la API no refleja el valor MySQL." >&2; exit 1;
}
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
request PUT "/Users/$deactivated_user_id/status?isActive=true" "" "$token"
expect_status 200 "Reactivar usuario de prueba"
request GET /Auth/me "" "$deactivated_access_token"
expect_status 401 "Mantener revocado access token anterior después de reactivar"
request POST /Auth/login "$(jq -nc --arg email "$deactivated_user_email" --arg password "$deactivated_user_password" '{email:$email,password:$password}')" ""
expect_status 200 "Permitir sesión nueva después de reactivar"
request GET /Auth/me "" "$(jq -er '.token // .accessToken' "$work_dir/response.json")"
expect_status 200 "Validar nuevo access token después de reactivar"

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

inactive_driver_user_email="fletflow.e2e.driver.inactive.$suffix@example.test"
inactive_driver_password="$(openssl rand -hex 24)"
inactive_driver_document="E2EQA${suffix: -9}I"
request POST /Users "$(jq -nc --arg email "$inactive_driver_user_email" --arg password "$inactive_driver_password" --arg username "E2E inactive Driver $suffix" --arg document "$inactive_driver_document" '{username:$username,email:$email,password:$password,roleName:"Driver",firstName:"Inactive",lastName:"Linked",documentNumber:$document,phoneNumber:"0990000099"}')" "$token"
expect_status 200 "Crear Driver vinculado para probar cuenta inactiva"
inactive_driver_user_id="$(jq -er '.id' "$work_dir/response.json")"
inactive_linked_driver_id="$(jq -er '.driverId' "$work_dir/response.json")"
mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  -e "UPDATE Users SET IsActive=1, MustChangePassword=0 WHERE Id='$inactive_driver_user_id';" >/dev/null
request POST /Auth/login "$(jq -nc --arg email "$inactive_driver_user_email" --arg password "$inactive_driver_password" '{email:$email,password:$password}')" ""
expect_status 200 "Login Driver para matriz negativa de opciones de asignación"
inactive_driver_access_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"
request GET /Routes/assignment-options "" "$inactive_driver_access_token"
expect_status 403 "Denegar opciones de asignación a Driver"
request PUT "/Users/$inactive_driver_user_id/status?isActive=false" "" "$token"
expect_status 200 "Desactivar cuenta de Driver enlazado"

request PUT "/Vehicles/$manager_vehicle_id/status" '{"status":2}' "$token"
expect_status 204 "Poner vehículo en mantenimiento para filtrar opciones"
request GET /Routes/assignment-options "" "$logistics_token"
expect_status 200 "Consultar opciones mínimas de asignación como Logistics"
jq -e --arg vehicleId "$manager_vehicle_id" --arg driverId "$manager_driver_id" --arg inactiveDriverId "$inactive_linked_driver_id" \
  '(.vehicles | all(.[]; .status == 0)) and (.vehicles | any(.[]; .id == $vehicleId) | not) and (.drivers | any(.[]; .id == $driverId and .firstName != "" and has("userId"))) and (.drivers | any(.[]; .id == $inactiveDriverId) | not) and ((.drivers | all(.[]; (has("documentNumber") or has("phoneNumber")) | not)))' \
  "$work_dir/response.json" >/dev/null \
  || { echo "Las opciones incluyen recursos no asignables, omiten recursos viables o exponen campos personales." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request PUT "/Vehicles/$manager_vehicle_id/status" '{"status":0}' "$token"
expect_status 204 "Restaurar vehículo disponible para opciones Logistics"
request GET /Routes/assignment-options "" "$logistics_token"
expect_status 200 "Reconsultar opciones disponibles como Logistics"
jq -e --arg vehicleId "$manager_vehicle_id" --arg driverId "$manager_driver_id" --arg inactiveDriverId "$inactive_linked_driver_id" \
  '(.vehicles | any(.[]; .id == $vehicleId and .status == 0)) and (.drivers | any(.[]; .id == $driverId)) and (.drivers | any(.[]; .id == $inactiveDriverId) | not)' \
  "$work_dir/response.json" >/dev/null \
  || { echo "No se devolvieron recursos asignables o apareció el Driver con cuenta inactiva." >&2; cat "$work_dir/response.json" >&2; exit 1; }
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

logistics_gps_recorded_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
request POST "/Routes/$logistics_route_id/positions" \
  "$(jq -nc --argjson latitude -0.186 --argjson longitude -78.466 --argjson speed 32 --argjson heading 90 --arg recordedAt "$logistics_gps_recorded_at" '{latitude:$latitude,longitude:$longitude,speedKmh:$speed,heading:$heading,recordedAt:$recordedAt}')" "$logistics_token"
expect_status 200 "Registrar posición GPS con ruta en progreso como Logistics"
jq -e --arg routeId "$logistics_route_id" '.routeId == $routeId and .latitude == -0.186 and .longitude == -78.466 and .speedKmh == 32 and .heading == 90' \
  "$work_dir/response.json" >/dev/null \
  || { echo "La respuesta GPS no coincide con la posición enviada." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request GET "/Routes/$logistics_route_id/positions" "" "$logistics_token"
expect_status 200 "Consultar posición GPS persistida como Logistics"
jq -e 'length == 1 and .[0].latitude == -0.186 and .[0].longitude == -78.466 and .[0].speedKmh == 32 and .[0].heading == 90' \
  "$work_dir/response.json" >/dev/null \
  || { echo "La posición GPS no quedó persistida." >&2; cat "$work_dir/response.json" >&2; exit 1; }
gps_after="$(date -u -d "$logistics_gps_recorded_at + 1 second" +%Y-%m-%dT%H:%M:%SZ)"
gps_before="$(date -u -d "$logistics_gps_recorded_at - 1 second" +%Y-%m-%dT%H:%M:%SZ)"
request GET "/Routes/$logistics_route_id/positions?from=$logistics_gps_recorded_at&to=$logistics_gps_recorded_at" "" "$logistics_token"
expect_status 200 "Consultar GPS dentro de límites inclusivos"
jq -e 'length == 1 and .[0].recordedAt != null' "$work_dir/response.json" >/dev/null \
  || { echo "El intervalo inclusivo no incluyó el GPS en el límite." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request GET "/Routes/$logistics_route_id/positions?from=$gps_after" "" "$logistics_token"
expect_status 200 "Consultar GPS desde después de la posición"
jq -e 'length == 0' "$work_dir/response.json" >/dev/null \
  || { echo "El filtro from no excluyó la posición previa." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request GET "/Routes/$logistics_route_id/positions?to=$gps_before" "" "$logistics_token"
expect_status 200 "Consultar GPS hasta antes de la posición"
jq -e 'length == 0' "$work_dir/response.json" >/dev/null \
  || { echo "El filtro to no excluyó la posición posterior." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request GET "/Routes/$logistics_route_id/positions?from=$gps_after&to=$gps_before" "" "$logistics_token"
expect_status 200 "Consultar GPS con intervalo invertido"
jq -e 'length == 0' "$work_dir/response.json" >/dev/null \
  || { echo "El intervalo invertido debía devolver una colección vacía." >&2; cat "$work_dir/response.json" >&2; exit 1; }

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
request GET /Routes/assignment-options "" "$other_token"
expect_status 200 "Opciones de asignación limitadas al tenant B"
jq -e '(.vehicles | length == 0) and (.drivers | length == 0)' "$work_dir/response.json" >/dev/null \
  || { echo "Tenant B recibió opciones de vehículos o Drivers del tenant A." >&2; cat "$work_dir/response.json" >&2; exit 1; }
restricted_user_id="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT Id FROM Users WHERE Email='$restricted_user_email';")"
root_user_id="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT Id FROM Users WHERE Email='$admin_email';")"
[[ -n "$restricted_user_id" && -n "$root_user_id" ]] || { echo "No se encontraron las cuentas QA para verificar el límite de usuarios." >&2; exit 1; }
root_username_before="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT Username FROM Users WHERE Id='$root_user_id';")"

request PUT "/Users/$restricted_user_id/profile" '{"username":"","firstName":"Nombre válido","lastName":"Apellido válido"}' "$token"
expect_status 400 "Rechazar username vacío al editar perfil"
long_username="$(printf '%101s' '' | tr ' ' 'u')"
request PUT "/Users/$restricted_user_id/profile" "$(jq -nc --arg username "$long_username" '{username:$username,firstName:"Nombre válido",lastName:"Apellido válido"}')" "$token"
expect_status 400 "Rechazar campos de perfil mayores de 100 caracteres"
long_first_name="$(printf '%101s' '' | tr ' ' 'n')"
request PUT "/Users/$restricted_user_id/profile" "$(jq -nc --arg firstName "$long_first_name" '{username:"usuario válido",firstName:$firstName,lastName:"Apellido válido"}')" "$token"
expect_status 400 "Rechazar nombre mayor de 100 caracteres"
long_last_name="$(printf '%101s' '' | tr ' ' 'a')"
request PUT "/Users/$restricted_user_id/profile" "$(jq -nc --arg lastName "$long_last_name" '{username:"usuario válido",firstName:"Nombre válido",lastName:$lastName}')" "$token"
expect_status 400 "Rechazar apellido mayor de 100 caracteres"

request PUT "/Users/$restricted_user_id/status?isActive=false" "" "$other_token"
expect_status 404 "Tenant B no puede desactivar un miembro de tenant A"
request PUT "/Users/$restricted_user_id/role" '{"roleName":"Admin"}' "$other_token"
expect_status 404 "Tenant B no puede cambiar el rol de un miembro de tenant A"
request PUT "/Users/$restricted_user_id/profile" '{"username":"altered-cross-tenant","firstName":"Alterado","lastName":"Ajeno"}' "$other_token"
expect_status 404 "Tenant B no puede editar el perfil de un miembro de tenant A"
request POST "/Users/$restricted_user_id/roles/Admin" "" "$other_token"
expect_status 404 "Alias de roles no cruza tenants"
user_state="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT CONCAT(CASE WHEN u.IsActive THEN '1' ELSE '0' END, ':', LOWER(r.Name), ':', u.Username, ':', COALESCE(u.FirstName,''), ':', COALESCE(u.LastName,'')) FROM Users u JOIN UserRoles ur ON ur.UserId=u.Id JOIN Roles r ON r.Id=ur.RoleId WHERE u.Id='$restricted_user_id';")"
[[ "$user_state" == "1:user:$max_user_name:$max_person_name:$max_person_name" ]] || { echo "Los intentos cross-tenant alteraron estado, rol o perfil del miembro ($user_state)." >&2; exit 1; }

request PUT "/Users/$root_user_id/status?isActive=false" "" "$token"
expect_status 400 "No se puede desactivar el propietario raíz"
request PUT "/Users/$root_user_id/role" '{"roleName":"User"}' "$token"
expect_status 400 "No se puede cambiar el rol del propietario raíz"
request PUT "/Users/$root_user_id/profile" '{"username":"altered-root","firstName":"Alterado","lastName":"Propietario"}' "$token"
expect_status 400 "No se puede editar el perfil raíz desde Gestión de Equipo"
request POST "/Users/$root_user_id/roles/User" "" "$token"
expect_status 400 "Alias de roles tampoco modifica al propietario raíz"
root_state="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT CONCAT(CASE WHEN u.IsActive THEN '1' ELSE '0' END, ':', LOWER(r.Name), ':', u.Username) FROM Users u JOIN UserRoles ur ON ur.UserId=u.Id JOIN Roles r ON r.Id=ur.RoleId WHERE u.Id='$root_user_id';")"
[[ "$root_state" == "1:admin:$root_username_before" ]] || { echo "Los intentos de cambiar propietario alteraron estado, rol o perfil ($root_state)." >&2; exit 1; }

driver_race_email_a="fletflow.e2e.driver.race.a.$suffix@example.test"
driver_race_email_b="fletflow.e2e.driver.race.b.$suffix@example.test"
driver_race_document="RACE${suffix: -8}"
driver_race_password="$(openssl rand -hex 24)"
driver_race_payload_a="$(jq -nc --arg username "E2E Driver race A $suffix" --arg email "$driver_race_email_a" --arg password "$driver_race_password" --arg document "$driver_race_document" \
  '{username:$username,email:$email,password:$password,roleName:"Driver",firstName:"Race A",lastName:"Driver",documentNumber:$document,phoneNumber:"0990000001"}')"
driver_race_payload_b="$(jq -nc --arg username "E2E Driver race B $suffix" --arg email "$driver_race_email_b" --arg password "$driver_race_password" --arg document "$driver_race_document" \
  '{username:$username,email:$email,password:$password,roleName:"Driver",firstName:"Race B",lastName:"Driver",documentNumber:$document,phoneNumber:"0990000002"}')"
curl -sS --max-time 60 -o "$work_dir/driver_race_a.json" -w '%{http_code}' -X POST \
  -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "$driver_race_payload_a" \
  "$api_base/Users" >"$work_dir/driver_race_a.status" & driver_race_pid_a=$!
curl -sS --max-time 60 -o "$work_dir/driver_race_b.json" -w '%{http_code}' -X POST \
  -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "$driver_race_payload_b" \
  "$api_base/Users" >"$work_dir/driver_race_b.status" & driver_race_pid_b=$!
wait "$driver_race_pid_a"
wait "$driver_race_pid_b"
driver_race_status_a="$(cat "$work_dir/driver_race_a.status")"
driver_race_status_b="$(cat "$work_dir/driver_race_b.status")"
if ! { [[ "$driver_race_status_a" =~ ^(200|201)$ && "$driver_race_status_b" == 409 ]] || [[ "$driver_race_status_a" == 409 && "$driver_race_status_b" =~ ^(200|201)$ ]]; }; then
  echo "Alta Driver concurrente esperaba 200/201 y 409; recibió $driver_race_status_a y $driver_race_status_b." >&2
  cat "$work_dir/driver_race_a.json" "$work_dir/driver_race_b.json" >&2
  exit 1
fi
driver_race_counts="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT CONCAT((SELECT COUNT(*) FROM Users WHERE Email IN ('$driver_race_email_a','$driver_race_email_b')), ':', (SELECT COUNT(*) FROM Drivers WHERE DocumentNumber='$driver_race_document'), ':', (SELECT COUNT(*) FROM PasswordResetTokens WHERE UserId IN (SELECT Id FROM Users WHERE Email IN ('$driver_race_email_a','$driver_race_email_b'))));")"
[[ "$driver_race_counts" == "1:1:1" ]] || { echo "Alta Driver concurrente dejó persistencia parcial (Users:Drivers:ActivationTokens=$driver_race_counts)." >&2; exit 1; }

request POST /Vehicles "$(jq -nc --arg plate "$plate" '{plate:$plate,brand:"CI QA",model:"Tenant",year:2025}')"
expect_status 201 "Crear vehículo tenant A"
vehicle_id="$(jq -er '.id' "$work_dir/response.json")"

request POST /Drivers "$(jq -nc --arg document "$document_number" '{firstName:"E2E QA",lastName:"Driver A",documentNumber:$document,phoneNumber:"0990000000"}')"
expect_status 201 "Crear Driver tenant A"
driver_id="$(jq -er '.id' "$work_dir/response.json")"
request GET "/Drivers/$driver_id"
expect_status 200 "Consultar Driver recién creado"
jq -e --arg id "$driver_id" --arg document "$document_number" '.id == $id and .documentNumber == $document and .isActive == true and .vehicleId == null' \
  "$work_dir/response.json" >/dev/null || { echo "El Driver inicial no coincide con los datos persistidos." >&2; cat "$work_dir/response.json" >&2; exit 1; }
updated_document="E2EU${suffix: -9}"
request PUT "/Drivers/$driver_id" "$(jq -nc --arg document "$updated_document" '{firstName:"E2E QA editado",lastName:"Driver A",documentNumber:$document,phoneNumber:"0990000009"}')"
expect_status 200 "Editar Driver propio"
request GET "/Drivers/$driver_id"
expect_status 200 "Confirmar edición persistida de Driver"
jq -e --arg document "$updated_document" '.firstName == "E2E QA editado" and .documentNumber == $document and .phoneNumber == "0990000009"' \
  "$work_dir/response.json" >/dev/null || { echo "La edición del Driver no quedó persistida." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request PUT "/Drivers/$driver_id/status" '{"isActive":false}'
expect_status 204 "Desactivar Driver propio"
request GET /Drivers?onlyActive=false
expect_status 200 "Consultar Drivers inactivos"
jq -e --arg id "$driver_id" 'any(.[]; .id == $id and .isActive == false)' "$work_dir/response.json" >/dev/null \
  || { echo "El filtro onlyActive=false no incluyó el Driver desactivado." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request PUT "/Drivers/$driver_id/status" '{"isActive":true}'
expect_status 204 "Reactivar Driver propio"
request GET "/Drivers/$driver_id"
expect_status 200 "Confirmar reactivación persistida de Driver"
jq -e '.isActive == true' "$work_dir/response.json" >/dev/null || { echo "El Driver no volvió a estar activo." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request PUT "/Drivers/$driver_id/assign-vehicle" "$(jq -nc --arg vehicleId "$vehicle_id" '{vehicleId:$vehicleId}')"
expect_status 204 "Asignar vehículo al Driver propio"
request GET "/Drivers/$driver_id"
expect_status 200 "Confirmar asignación de vehículo"
jq -e --arg vehicleId "$vehicle_id" '.vehicleId == $vehicleId' "$work_dir/response.json" >/dev/null \
  || { echo "La asignación del vehículo no quedó persistida." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request PUT "/Drivers/$driver_id/unassign-vehicle"
expect_status 204 "Desasignar vehículo del Driver propio"
request GET "/Drivers/$driver_id"
expect_status 200 "Confirmar desasignación de vehículo"
jq -e '.vehicleId == null' "$work_dir/response.json" >/dev/null || { echo "La desasignación del vehículo no quedó persistida." >&2; cat "$work_dir/response.json" >&2; exit 1; }

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
request PUT "/Routes/$route_id/points/0/status" '{"status":1}' "$other_token"
expect_status 404 "Cambiar estado de parada cruzada"
request POST "/Routes/$route_id/points/0/evidence" \
  "$(jq -nc --arg content "Tenant B blocked $suffix" '{type:"note",content:$content,description:"Intento cross-tenant"}')" "$other_token"
expect_status 404 "Agregar evidencia a parada cruzada"
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
request GET "/Routes/$route_id" "" "$token"; expect_status 200 "Confirmar ruta tenant A intacta tras intentos cruzados"
jq -e '.points[0].status == 0 and (.points[0].evidence | length == 0)' "$work_dir/response.json" >/dev/null \
  || { echo "Un intento cross-tenant modificó estado/evidencia de la parada." >&2; cat "$work_dir/response.json" >&2; exit 1; }
request GET "/Drivers/$driver_id" "" "$token"; expect_status 200 "Confirmar Driver tenant A intacto"
request GET "/Vehicles/$vehicle_id" "" "$token"; expect_status 200 "Confirmar vehículo tenant A intacto"

request POST /Auth/login "$(jq -nc --arg email "$admin_email" --arg password "$admin_password" '{email:$email,password:$password}')" ""
expect_status 200 "Login Admin para prueba concurrente de refresh"
race_refresh_token="$(jq -er '.refreshToken' "$work_dir/response.json")"
race_refresh_body="$(jq -nc --arg refreshToken "$race_refresh_token" '{refreshToken:$refreshToken}')"
curl --silent --show-error --output "$work_dir/refresh_a.json" --write-out '%{http_code}' \
  --request POST "$api_base/Auth/refresh" --header 'Content-Type: application/json' --data "$race_refresh_body" \
  >"$work_dir/refresh_a.status" &
refresh_a_pid=$!
curl --silent --show-error --output "$work_dir/refresh_b.json" --write-out '%{http_code}' \
  --request POST "$api_base/Auth/refresh" --header 'Content-Type: application/json' --data "$race_refresh_body" \
  >"$work_dir/refresh_b.status" &
refresh_b_pid=$!
wait "$refresh_a_pid"
wait "$refresh_b_pid"
refresh_a_status="$(cat "$work_dir/refresh_a.status")"
refresh_b_status="$(cat "$work_dir/refresh_b.status")"
[[ "$refresh_a_status:$refresh_b_status" == "200:401" || "$refresh_a_status:$refresh_b_status" == "401:200" ]] || {
  echo "Rotación concurrente debía producir un solo 200 y un 401; obtuvo $refresh_a_status y $refresh_b_status." >&2
  exit 1
}
if [[ "$refresh_a_status" == "200" ]]; then
  successful_refresh_response="$work_dir/refresh_a.json"
else
  successful_refresh_response="$work_dir/refresh_b.json"
fi
rotated_refresh_token="$(jq -er '.refreshToken' "$successful_refresh_response")"
old_refresh_hash="$(printf '%s' "$race_refresh_token" | sha256sum | awk '{print toupper($1)}')"
new_refresh_hash="$(printf '%s' "$rotated_refresh_token" | sha256sum | awk '{print toupper($1)}')"
race_refresh_db_state="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT CONCAT((SELECT COUNT(*) FROM RefreshTokens WHERE TokenHash='$old_refresh_hash' AND RevokedAt IS NOT NULL), ':', (SELECT COUNT(*) FROM RefreshTokens WHERE TokenHash='$new_refresh_hash' AND RevokedAt IS NULL));")"
[[ "$race_refresh_db_state" == "1:1" ]] || { echo "La rotación no persistió exactamente un token sucesor (estado $race_refresh_db_state)." >&2; exit 1; }
request POST /Auth/refresh "$(jq -nc --arg refreshToken "$race_refresh_token" '{refreshToken:$refreshToken}')" ""
expect_status 401 "Denegar reutilización del refresh token rotado concurrentemente"
request POST /Auth/refresh "$(jq -nc --arg refreshToken "$rotated_refresh_token" '{refreshToken:$refreshToken}')" ""
expect_status 200 "Aceptar únicamente el refresh token sucesor ganador"

request POST /Auth/login "$(jq -nc --arg email "$admin_email" --arg password "$admin_password" '{email:$email,password:$password}')" ""
expect_status 200 "Login Admin para carrera refresh/logout-all"
logout_race_token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"
logout_race_refresh="$(jq -er '.refreshToken' "$work_dir/response.json")"
logout_race_user_id="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT Id FROM Users WHERE Email='$admin_email';")"
# Mantener bloqueada la fila de usuario mientras ambos requests llegan al API fuerza
# que compitan por el mismo lock de sesión, en vez de depender sólo del scheduler.
mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  -e "SET SESSION innodb_lock_wait_timeout=10; START TRANSACTION; SELECT Id FROM Users WHERE Id='$logout_race_user_id' FOR UPDATE; DO SLEEP(2); COMMIT;" \
  >"$work_dir/session_lock.out" 2>"$work_dir/session_lock.err" &
session_lock_pid=$!
sleep 0.2
curl --silent --show-error --output "$work_dir/logout_race.json" --write-out '%{http_code}' \
  --request POST "$api_base/Auth/logout-all" --header "Authorization: Bearer $logout_race_token" \
  >"$work_dir/logout_race.status" &
logout_race_pid=$!
curl --silent --show-error --output "$work_dir/refresh_logout_race.json" --write-out '%{http_code}' \
  --request POST "$api_base/Auth/refresh" --header 'Content-Type: application/json' \
  --data "$(jq -nc --arg refreshToken "$logout_race_refresh" '{refreshToken:$refreshToken}')" \
  >"$work_dir/refresh_logout_race.status" &
refresh_logout_race_pid=$!
wait "$logout_race_pid"
wait "$refresh_logout_race_pid"
wait "$session_lock_pid"
logout_race_status="$(cat "$work_dir/logout_race.status")"
refresh_logout_race_status="$(cat "$work_dir/refresh_logout_race.status")"
[[ "$logout_race_status" == "200" && ( "$refresh_logout_race_status" == "200" || "$refresh_logout_race_status" == "401" ) ]] || {
  echo "Carrera logout-all/refresh produjo estados inesperados: logout=$logout_race_status refresh=$refresh_logout_race_status." >&2
  exit 1
}
active_refresh_after_logout="$(mysql --protocol=TCP --host="$mysql_host" --port="$mysql_port" --user="$mysql_user" "$db_name" \
  --batch --skip-column-names -e "SELECT COUNT(*) FROM RefreshTokens WHERE UserId='$logout_race_user_id' AND RevokedAt IS NULL AND ExpiresAt > UTC_TIMESTAMP(6);")"
[[ "$active_refresh_after_logout" == "0" ]] || { echo "Logout-all concurrente dejó $active_refresh_after_logout refresh tokens activos." >&2; exit 1; }
if [[ "$refresh_logout_race_status" == "200" ]]; then
  race_successor_token="$(jq -er '.refreshToken' "$work_dir/refresh_logout_race.json")"
  request POST /Auth/refresh "$(jq -nc --arg refreshToken "$race_successor_token" '{refreshToken:$refreshToken}')" ""
  expect_status 401 "Logout-all revoca el sucesor de refresh concurrente"
fi

echo "OK: aislamiento HTTP/MySQL entre tenants; autorización User/Driver/Manager/Logistics; altas con longitudes límite/rechazo y email duplicado concurrente 200/409 sin persistencia parcial; Logistics creó/editó/leyó ruta, inició operación pickup+delivery con evidencia, registró GPS, completó ruta y verificó vehículo Available; cleanup de Vehicles/Drivers/Routes/RoutePositions/usuarios QA; revocación de sesiones."
