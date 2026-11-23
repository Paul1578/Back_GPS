#!/usr/bin/env bash
set -euo pipefail

base_url="${FLETFLOW_BASE_URL:-http://127.0.0.1:5048/api}"
admin_email="${FLETFLOW_ADMIN_EMAIL:-}"
admin_password="${FLETFLOW_ADMIN_PASSWORD:-}"

if [[ -z "$admin_email" || -z "$admin_password" ]]; then
  echo "Define FLETFLOW_ADMIN_EMAIL y FLETFLOW_ADMIN_PASSWORD para ejecutar el smoke test." >&2
  exit 2
fi

for command in curl jq; do
  command -v "$command" >/dev/null || { echo "Falta dependencia: $command" >&2; exit 2; }
done

work_dir="$(mktemp -d)"
vehicle_id=""
cleanup() {
  if [[ -n "$vehicle_id" && -n "${token:-}" ]]; then
    curl -sS -o /dev/null -X DELETE \
      -H "Authorization: Bearer $token" \
      "$base_url/Vehicles/$vehicle_id" || true
  fi
  rm -f -- "$work_dir/body.json" "$work_dir/login.json"
  rmdir -- "$work_dir"
}
trap cleanup EXIT

request() {
  local method="$1" path="$2" payload="${3:-}"
  local args=(-sS -o "$work_dir/body.json" -w '%{http_code}' -X "$method" \
    -H "Authorization: Bearer $token" -H 'Content-Type: application/json')
  if [[ -n "$payload" ]]; then args+=(-d "$payload"); fi
  http_status="$(curl "${args[@]}" "$base_url$path")"
}

login_status="$(curl -sS -o "$work_dir/login.json" -w '%{http_code}' \
  -H 'Content-Type: application/json' \
  -d "$(jq -nc --arg email "$admin_email" --arg password "$admin_password" '{email:$email,password:$password}')" \
  "$base_url/Auth/login")"
[[ "$login_status" == "200" ]] || { echo "Login falló: HTTP $login_status" >&2; exit 1; }
token="$(jq -er '.token' "$work_dir/login.json")"

unique="$(date -u +%H%M%S)${RANDOM}"
plate="${FLETFLOW_SMOKE_PLATE:-QA$unique}"
if [[ ! "$plate" =~ ^QA[A-Za-z0-9]{1,18}$ ]]; then
  echo "FLETFLOW_SMOKE_PLATE debe comenzar con QA y tener hasta 20 caracteres alfanuméricos." >&2
  exit 2
fi
request POST /Vehicles "$(jq -nc --arg plate "$plate" '{plate:$plate,brand:"QA",model:"Smoke",year:2025,description:"Automated HTTP smoke test"}')"
[[ "$http_status" == "201" ]] || { echo "Crear vehículo falló: HTTP $http_status" >&2; exit 1; }
vehicle_id="$(jq -er '.id' "$work_dir/body.json")"

request GET "/Vehicles/$vehicle_id"
[[ "$http_status" == "200" && "$(jq -r '.plate' "$work_dir/body.json")" == "$plate" ]] || {
  echo "Leer vehículo creado falló: HTTP $http_status" >&2; exit 1;
}

request PUT "/Vehicles/$vehicle_id" "$(jq -nc --arg plate "$plate" '{plate:$plate,brand:"QA editado",model:"Smoke",year:2025,description:"Updated by HTTP smoke test"}')"
[[ "$http_status" == "200" && "$(jq -r '.brand' "$work_dir/body.json")" == "QA editado" ]] || {
  echo "Actualizar vehículo falló: HTTP $http_status" >&2; exit 1;
}

request PUT "/Vehicles/$vehicle_id/status" '{"status":2}'
[[ "$http_status" == "204" ]] || { echo "Cambiar estado falló: HTTP $http_status" >&2; exit 1; }

request DELETE "/Vehicles/$vehicle_id"
[[ "$http_status" == "204" ]] || { echo "Eliminar vehículo falló: HTTP $http_status" >&2; exit 1; }
vehicle_id=""

echo "OK: login Admin y CRUD HTTP de vehículo (crear, leer, actualizar, estado y eliminar)."
