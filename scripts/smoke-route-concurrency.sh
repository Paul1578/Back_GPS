#!/usr/bin/env bash
set -euo pipefail

api_base="${FLETFLOW_API_URL:-http://127.0.0.1:5048/api}"
admin_email="${FLETFLOW_E2E_ADMIN_EMAIL:-}"
admin_password="${FLETFLOW_E2E_ADMIN_PASSWORD:-}"
db_name="${DB_NAME:-}"
mysql_host="${MYSQL_HOST:-127.0.0.1}"
mysql_user="${MYSQL_USER:-root}"

if [[ "${FLETFLOW_E2E_ALLOW_MUTATIONS:-}" != "true" || ! "$api_base" =~ ^http://(127\.0\.0\.1|localhost):[0-9]{2,5}/api$ ]]; then
  echo "Este smoke requiere autorización explícita y una API HTTP en loopback." >&2
  exit 2
fi
if [[ ! "$admin_email" =~ ^fletflow\.e2e\.admin\..+@example\.test$ || -z "$admin_password" ]]; then
  echo "Se requiere una cuenta Admin bootstrap temporal fletflow.e2e.admin.*@example.test." >&2
  exit 2
fi
if [[ ! "$db_name" =~ ^fletflow_ci([_][a-zA-Z0-9]+)*$ || -z "${MYSQL_PWD:-}" ]]; then
  echo "DB_NAME debe ser descartable fletflow_ci_* y MYSQL_PWD debe estar definido." >&2
  exit 2
fi
for command in curl jq mysql; do
  command -v "$command" >/dev/null || { echo "Falta dependencia: $command" >&2; exit 2; }
done

work_dir="$(mktemp -d)"
token=""
prefix="CQ$(date +%s%N | tail -c 7)_"
cleanup() {
  local status=$?
  trap - EXIT
  if [[ -n "$token" ]]; then
    mysql --protocol=TCP --host="$mysql_host" --user="$mysql_user" "$db_name" <<SQL >/dev/null 2>&1 || status=1
START TRANSACTION;
DELETE rp FROM RoutePositions rp JOIN Routes r ON r.Id=rp.RouteId WHERE r.Name LIKE '${prefix}%';
DELETE FROM Routes WHERE Name LIKE '${prefix}%';
DELETE FROM Drivers WHERE DocumentNumber LIKE '${prefix}%';
DELETE FROM Vehicles WHERE Plate LIKE '${prefix}%';
COMMIT;
SQL
  fi
  rm -rf -- "$work_dir"
  exit "$status"
}
trap cleanup EXIT

request() {
  local method="$1" path="$2" payload="${3:-}"
  local args=(-sS --max-time 30 -o "$work_dir/response.json" -w '%{http_code}' -X "$method"
    -H 'Content-Type: application/json' -H "Authorization: Bearer $token")
  [[ -z "$payload" ]] || args+=(-d "$payload")
  http_status="$(curl "${args[@]}" "$api_base$path")"
}
expect() {
  local expected="$1" label="$2"
  if [[ "$http_status" != "$expected" ]]; then
    echo "$label: esperado HTTP $expected, recibido $http_status" >&2
    cat "$work_dir/response.json" >&2
    exit 1
  fi
}
create_vehicle() {
  request POST /Vehicles "$(jq -nc --arg plate "$1" '{plate:$plate,brand:"Concurrency QA",model:"Van",year:2025}')"
  expect 201 "Crear vehículo $1"
  jq -er '.id' "$work_dir/response.json"
}
create_driver() {
  request POST /Drivers "$(jq -nc --arg document "$1" '{firstName:"Concurrency",lastName:"QA",documentNumber:$document,phoneNumber:"0990000000"}')"
  expect 201 "Crear conductor $1"
  jq -er '.id' "$work_dir/response.json"
}
create_route() {
  local name="$1" vehicle="$2" driver="$3" start="$4" end="$5"
  local points='[{"lat":-0.18,"lng":-78.46,"name":"Origin"},{"lat":-0.19,"lng":-78.47,"name":"Destination"}]'
  request POST /Routes "$(jq -nc --arg name "$name" --arg vehicle "$vehicle" --arg driver "$driver" --arg start "$start" --arg end "$end" --argjson points "$points" '{name:$name,vehicleId:$vehicle,driverId:$driver,plannedStart:$start,plannedEnd:$end,points:$points}')"
  expect 201 "Crear ruta base $name"
  jq -er '.id' "$work_dir/response.json"
}
concurrent_request() {
  local method="$1" path="$2" payload="$3" tag="$4"
  curl -sS --max-time 60 -o "$work_dir/$tag.json" -w '%{http_code}' -X "$method" \
    -H 'Content-Type: application/json' -H "Authorization: Bearer $token" -d "$payload" \
    "$api_base$path" >"$work_dir/$tag.status"
}
assert_one_created_one_conflict() {
  local success_status="$4" first second
  first="$(cat "$work_dir/$1.status")"
  second="$(cat "$work_dir/$2.status")"
  if ! { [[ "$first" == "$success_status" && "$second" == 409 ]] || [[ "$first" == 409 && "$second" == "$success_status" ]]; }; then
    echo "$3: esperado un HTTP $success_status y un HTTP 409; recibidos $first y $second" >&2
    cat "$work_dir/$1.json" "$work_dir/$2.json" >&2
    exit 1
  fi
}

request POST /Auth/login "$(jq -nc --arg email "$admin_email" --arg password "$admin_password" '{email:$email,password:$password}')"
expect 200 "Login Admin de CI"
token="$(jq -er '.token // .accessToken' "$work_dir/response.json")"

v1="$(create_vehicle "${prefix}V1")"
d1="$(create_driver "${prefix}D1")"
v2="$(create_vehicle "${prefix}V2")"
d2="$(create_driver "${prefix}D2")"
v3="$(create_vehicle "${prefix}V3")"
d3="$(create_driver "${prefix}D3")"
v4="$(create_vehicle "${prefix}V4")"
d4="$(create_driver "${prefix}D4")"
start_create="2031-04-05T10:00:00Z"
end_create="2031-04-05T12:00:00Z"
points='[{"lat":-0.18,"lng":-78.46,"name":"Origin"},{"lat":-0.19,"lng":-78.47,"name":"Destination"}]'
payload_vehicle_a="$(jq -nc --arg name "${prefix}vehicle_a" --arg vehicle "$v1" --arg driver "$d1" --arg start "$start_create" --arg end "$end_create" --argjson points "$points" '{name:$name,vehicleId:$vehicle,driverId:$driver,plannedStart:$start,plannedEnd:$end,points:$points}')"
payload_vehicle_b="$(jq -nc --arg name "${prefix}vehicle_b" --arg vehicle "$v1" --arg driver "$d2" --arg start "$start_create" --arg end "$end_create" --argjson points "$points" '{name:$name,vehicleId:$vehicle,driverId:$driver,plannedStart:$start,plannedEnd:$end,points:$points}')"
concurrent_request POST /Routes "$payload_vehicle_a" create_vehicle_a & p1=$!
concurrent_request POST /Routes "$payload_vehicle_b" create_vehicle_b & p2=$!
wait "$p1"
wait "$p2"
assert_one_created_one_conflict create_vehicle_a create_vehicle_b "CreateRoute concurrente (mismo vehículo, conductores distintos)" 201

start_driver="2031-04-07T10:00:00Z"
end_driver="2031-04-07T12:00:00Z"
payload_driver_a="$(jq -nc --arg name "${prefix}driver_a" --arg vehicle "$v2" --arg driver "$d1" --arg start "$start_driver" --arg end "$end_driver" --argjson points "$points" '{name:$name,vehicleId:$vehicle,driverId:$driver,plannedStart:$start,plannedEnd:$end,points:$points}')"
payload_driver_b="$(jq -nc --arg name "${prefix}driver_b" --arg vehicle "$v3" --arg driver "$d1" --arg start "$start_driver" --arg end "$end_driver" --argjson points "$points" '{name:$name,vehicleId:$vehicle,driverId:$driver,plannedStart:$start,plannedEnd:$end,points:$points}')"
concurrent_request POST /Routes "$payload_driver_a" create_driver_a & p1=$!
concurrent_request POST /Routes "$payload_driver_b" create_driver_b & p2=$!
wait "$p1"
wait "$p2"
assert_one_created_one_conflict create_driver_a create_driver_b "CreateRoute concurrente (mismo conductor, vehículos distintos)" 201

start_source_a="2030-04-05T10:00:00Z"
end_source_a="2030-04-05T11:00:00Z"
start_source_b="2030-04-06T10:00:00Z"
end_source_b="2030-04-06T11:00:00Z"
route_a="$(create_route "${prefix}update_a" "$v2" "$d2" "$start_source_a" "$end_source_a")"
route_b="$(create_route "${prefix}update_b" "$v3" "$d3" "$start_source_b" "$end_source_b")"
start_update="2032-04-05T10:00:00Z"
end_update="2032-04-05T12:00:00Z"
update_payload_a="$(jq -nc --arg name "${prefix}update_a" --arg vehicle "$v1" --arg driver "$d1" --arg start "$start_update" --arg end "$end_update" '{name:$name,vehicleId:$vehicle,driverId:$driver,plannedStart:$start,plannedEnd:$end,points:[{lat:-0.18,lng:-78.46,name:"Origin"},{lat:-0.19,lng:-78.47,name:"Destination"}]}')"
update_payload_b="$(jq -nc --arg name "${prefix}update_b" --arg vehicle "$v1" --arg driver "$d1" --arg start "$start_update" --arg end "$end_update" '{name:$name,vehicleId:$vehicle,driverId:$driver,plannedStart:$start,plannedEnd:$end,points:[{lat:-0.18,lng:-78.46,name:"Origin"},{lat:-0.19,lng:-78.47,name:"Destination"}]}')"
concurrent_request PUT "/Routes/$route_a" "$update_payload_a" update_a & p1=$!
concurrent_request PUT "/Routes/$route_b" "$update_payload_b" update_b & p2=$!
wait "$p1"
wait "$p2"
assert_one_created_one_conflict update_a update_b "UpdateRoute concurrente (mismo vehículo y conductor)" 200

start_status_a="2033-04-05T10:00:00Z"
end_status_a="2033-04-05T11:00:00Z"
start_status_b="2034-04-05T10:00:00Z"
end_status_b="2034-04-05T11:00:00Z"
route_status_a="$(create_route "${prefix}status_a" "$v1" "$d2" "$start_status_a" "$end_status_a")"
route_status_b="$(create_route "${prefix}status_b" "$v1" "$d3" "$start_status_b" "$end_status_b")"
concurrent_request PUT "/Routes/$route_status_a/status" '{"status":1}' start_status_a & p1=$!
concurrent_request PUT "/Routes/$route_status_b/status" '{"status":1}' start_status_b & p2=$!
wait "$p1"
wait "$p2"
assert_one_created_one_conflict start_status_a start_status_b "Inicio concurrente de rutas del mismo vehículo" 204
started_routes="$(mysql --protocol=TCP --host="$mysql_host" --user="$mysql_user" --batch --skip-column-names "$db_name" \
  -e "SELECT CONCAT(SUM(r.Status=1), ':', SUM(r.Status=0), ':', CAST(v.Status AS UNSIGNED)) FROM Routes r JOIN Vehicles v ON v.Id=r.VehicleId WHERE r.Id IN ('${route_status_a}','${route_status_b}') GROUP BY v.Status;")"
if [[ "$started_routes" != "1:1:1" ]]; then
  echo "Inicio concurrente dejó estado incoherente (InProgress:Pending:VehicleStatus=$started_routes)." >&2
  exit 1
fi

route_deactivation="$(create_route "${prefix}driver_deactivation" "$v4" "$d4" "2036-04-05T10:00:00Z" "2036-04-05T11:00:00Z")"
concurrent_request PUT "/Routes/$route_deactivation/status" '{"status":1}' start_vs_deactivate & start_pid=$!
curl -sS --max-time 60 -o "$work_dir/deactivate_driver.json" -w '%{http_code}' -X PUT \
  -H 'Content-Type: application/json' -H "Authorization: Bearer $token" \
  -d '{"isActive":false}' "$api_base/Drivers/$d4/status" >"$work_dir/deactivate_driver.status" & deactivate_pid=$!
wait "$start_pid"
wait "$deactivate_pid"
deactivate_status="$(cat "$work_dir/deactivate_driver.status")"
start_status="$(cat "$work_dir/start_vs_deactivate.status")"
if ! { [[ "$start_status" == 204 && "$deactivate_status" == 409 ]] || [[ "$start_status" == 409 && "$deactivate_status" == 204 ]]; }; then
  echo "Inicio/desactivación concurrente: esperado un HTTP 204 y un HTTP 409; recibidos $start_status y $deactivate_status" >&2
  cat "$work_dir/start_vs_deactivate.json" "$work_dir/deactivate_driver.json" >&2
  exit 1
fi
route_state="$(mysql --protocol=TCP --host="$mysql_host" --user="$mysql_user" --batch --skip-column-names "$db_name" \
  -e "SELECT CONCAT(r.Status, ':', CAST(d.IsActive AS UNSIGNED), ':', CAST(v.Status AS UNSIGNED)) FROM Routes r JOIN Drivers d ON d.Id=r.DriverId JOIN Vehicles v ON v.Id=r.VehicleId WHERE r.Id='${route_deactivation}';")"
if [[ "$route_state" != "1:1:1" && "$route_state" != "0:0:0" ]]; then
  echo "Inicio/desactivación dejó estado incoherente (RouteStatus:DriverIsActive:VehicleStatus=$route_state)." >&2
  exit 1
fi

guard_vehicle="$(create_vehicle "${prefix}GV")"
guard_driver="$(create_driver "${prefix}GD")"
guard_route="$(create_route "${prefix}guard_in_progress" "$guard_vehicle" "$guard_driver" "2037-04-05T10:00:00Z" "2037-04-05T11:00:00Z")"
request PUT "/Drivers/$guard_driver/assign-vehicle" "$(jq -nc --arg vehicleId "$guard_vehicle" '{vehicleId:$vehicleId}')"
expect 204 "Asignar vehículo al Driver de protección"
request PUT "/Routes/$guard_route/status" '{"status":1}'
expect 204 "Iniciar ruta de protección"
request PUT "/Drivers/$guard_driver/unassign-vehicle" '{}'
expect 409 "Impedir desasignar Driver en ruta"
request DELETE "/Routes/$guard_route"
expect 409 "Impedir dar de baja ruta en progreso"
request PUT "/Routes/$guard_route/status" '{"status":3}'
expect 204 "Cancelar ruta antes de baja"
request DELETE "/Routes/$guard_route"
expect 204 "Dar de baja ruta cancelada"
request PUT "/Drivers/$guard_driver/unassign-vehicle" '{}'
expect 204 "Desasignar Driver sin ruta activa"
request DELETE "/Vehicles/$guard_vehicle"
expect 204 "Dar de baja vehículo sin asignaciones"
request GET "/Vehicles/$guard_vehicle"
expect 200 "Consultar vehículo dado de baja"
jq -e '.isActive == false' "$work_dir/response.json" >/dev/null || { echo "La baja de vehículo no se persistió." >&2; exit 1; }
request POST "/Vehicles/$guard_vehicle/reactivate"
expect 204 "Reactivar vehículo dado de baja"
request GET "/Vehicles/$guard_vehicle"
expect 200 "Consultar vehículo reactivado"
jq -e '.isActive == true' "$work_dir/response.json" >/dev/null || { echo "La reactivación de vehículo no se persistió." >&2; exit 1; }
request DELETE "/Vehicles/$guard_vehicle"
expect 204 "Limpiar baja del vehículo de protección"

inactive_vehicle="$(create_vehicle "${prefix}IV")"
inactive_driver="$(create_driver "${prefix}ID")"
inactive_route="$(create_route "${prefix}inactive_vehicle_route" "$inactive_vehicle" "$inactive_driver" "2038-04-05T10:00:00Z" "2038-04-05T11:00:00Z")"
mysql --protocol=TCP --host="$mysql_host" --user="$mysql_user" "$db_name" \
  -e "UPDATE Vehicles SET IsActive=0 WHERE Id='$inactive_vehicle';" >/dev/null
request PUT "/Routes/$inactive_route/status" '{"status":1}'
expect 409 "Impedir iniciar ruta con vehículo inactivo"
inactive_state="$(mysql --protocol=TCP --host="$mysql_host" --user="$mysql_user" --batch --skip-column-names "$db_name" \
  -e "SELECT CONCAT(r.Status, ':', CAST(v.IsActive AS UNSIGNED), ':', CAST(v.Status AS UNSIGNED)) FROM Routes r JOIN Vehicles v ON v.Id=r.VehicleId WHERE r.Id='${inactive_route}';")"
if [[ "$inactive_state" != "0:0:0" ]]; then
  echo "Intentar iniciar con vehículo inactivo alteró estado (RouteStatus:VehicleActive:VehicleStatus=$inactive_state)." >&2
  exit 1
fi

points_vehicle="$(create_vehicle "${prefix}PV")"
points_driver="$(create_driver "${prefix}PD")"
points_json='[{"lat":-0.18,"lng":-78.46,"name":"Origin","operation":0},{"lat":-0.185,"lng":-78.465,"name":"Pickup","operation":1},{"lat":-0.19,"lng":-78.47,"name":"Delivery","operation":2},{"lat":-0.195,"lng":-78.475,"name":"Destination","operation":0}]'
request POST /Routes "$(jq -nc --arg name "${prefix}point_writes" --arg vehicle "$points_vehicle" --arg driver "$points_driver" --argjson points "$points_json" '{name:$name,vehicleId:$vehicle,driverId:$driver,points:$points}')"
expect 201 "Crear ruta con dos paradas para concurrencia"
points_route="$(jq -er '.id' "$work_dir/response.json")"
request PUT "/Routes/$points_route/status" '{"status":1}'
expect 204 "Iniciar ruta con paradas para concurrencia"
concurrent_request PUT "/Routes/$points_route/points/1/status" '{"status":1}' point_one & point_one_pid=$!
concurrent_request PUT "/Routes/$points_route/points/2/status" '{"status":1}' point_two & point_two_pid=$!
wait "$point_one_pid"
wait "$point_two_pid"
point_one_status="$(cat "$work_dir/point_one.status")"
point_two_status="$(cat "$work_dir/point_two.status")"
if [[ "$point_one_status" != 200 || "$point_two_status" != 200 ]]; then
  echo "Escrituras concurrentes de paradas: esperado HTTP 200/200, recibido $point_one_status/$point_two_status." >&2
  cat "$work_dir/point_one.json" "$work_dir/point_two.json" >&2
  exit 1
fi
request GET "/Routes/$points_route"
expect 200 "Verificar paradas después de escrituras concurrentes"
jq -e '(.points[1].status == 1 or .points[1].status == "InProgress") and (.points[2].status == 1 or .points[2].status == "InProgress")' "$work_dir/response.json" >/dev/null || {
  echo "Se perdió una actualización concurrente de estado de parada." >&2
  jq '.points' "$work_dir/response.json" >&2
  exit 1
}

echo "OK: concurrencia de agenda y paradas, protección de rutas/Driver/vehículo, vehículo inactivo y ciclo baja/reactivación; MySQL/API conserva estados coherentes."
