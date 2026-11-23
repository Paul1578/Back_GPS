#!/usr/bin/env bash
set -euo pipefail

api_origin="${FLETFLOW_API_URL:-http://127.0.0.1:5048/api}"
api_origin="${api_origin%/api}"
api_port="${FLETFLOW_RATE_LIMIT_PORT:-5048}"
admin_email="${FLETFLOW_E2E_ADMIN_EMAIL:-}"
admin_password="${FLETFLOW_E2E_ADMIN_PASSWORD:-}"
database_name="${DB_NAME:-}"
runner_temp="${RUNNER_TEMP:-/tmp}"
api_pid_file="$runner_temp/fletflow-api.pid"
api_log="$runner_temp/fletflow-rate-limit-api.log"
response_file="$(mktemp)"
headers_file="$(mktemp)"
secrets_dir="$(mktemp -d)"
new_api_pid=""

cleanup_files() {
  if [[ -n "$new_api_pid" ]]; then
    kill "$new_api_pid" 2>/dev/null || true
    wait "$new_api_pid" 2>/dev/null || true
    rm -f "$api_pid_file"
  fi
  rm -f "$response_file" "$headers_file"
  find "$secrets_dir" -mindepth 1 -delete
  rmdir "$secrets_dir"
}
trap cleanup_files EXIT

if [[ "${CI:-}" != "true" || ! "$database_name" =~ ^fletflow_ci([_][a-zA-Z0-9]+)*$ ]]; then
  echo "Este smoke sólo se permite en CI contra una base descartable fletflow_ci*." >&2
  exit 2
fi
if [[ "$api_origin" != "http://127.0.0.1:$api_port" && "$api_origin" != "http://localhost:$api_port" ]]; then
  echo "FLETFLOW_API_URL debe apuntar a la API local permitida." >&2
  exit 2
fi
if [[ -z "$admin_email" || -z "$admin_password" || ! -f "$api_pid_file" ]]; then
  echo "Falta fixture Admin temporal o PID de API CI." >&2
  exit 2
fi

# El Development loopback está exento por diseño; reiniciar el mismo binario en
# Staging ejercita el middleware y el pipeline HTTP con las protecciones activas.
old_api_pid="$(cat "$api_pid_file")"
kill "$old_api_pid" 2>/dev/null || true
for attempt in $(seq 1 30); do
  if ! curl --silent --max-time 1 "$api_origin/swagger/index.html" >/dev/null; then
    break
  fi
  sleep 1
done

certificate_password="fletflow-rate-limit-${RANDOM}-${RANDOM}"
openssl req -x509 -newkey rsa:2048 -nodes -days 1 \
  -subj "/CN=FletFlow CI Data Protection" \
  -keyout "$secrets_dir/data-protection.key" \
  -out "$secrets_dir/data-protection.crt" >/dev/null 2>&1
openssl pkcs12 -export \
  -out "$secrets_dir/data-protection.pfx" \
  -inkey "$secrets_dir/data-protection.key" \
  -in "$secrets_dir/data-protection.crt" \
  -passout "pass:$certificate_password" >/dev/null 2>&1

ASPNETCORE_ENVIRONMENT=Staging \
ASPNETCORE_URLS="http://127.0.0.1:$api_port" \
Email__ResetPasswordUrl="https://localhost/reset-password" \
Email__ActivationUrl="https://localhost/activate-account" \
ForwardedHeaders__KnownProxies__0=127.0.0.2 \
ForwardedHeaders__KnownProxies__1=127.0.0.4 \
FLETFLOW_DATA_PROTECTION_KEYS_PATH="$secrets_dir/key-ring" \
FLETFLOW_DATA_PROTECTION_CERT_PATH="$secrets_dir/data-protection.pfx" \
FLETFLOW_DATA_PROTECTION_CERT_PASSWORD="$certificate_password" \
dotnet ./bin/Release/net8.0/BackEnd_Gps.dll >"$api_log" 2>&1 &
new_api_pid=$!
echo "$new_api_pid" > "$api_pid_file"

ready=false
for attempt in $(seq 1 60); do
  status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
    --interface 127.0.0.2 --max-time 2 "$api_origin/api/Auth/me" || true)"
  if [[ "$status" == "401" ]]; then
    ready=true
    break
  fi
  if ! kill -0 "$new_api_pid" 2>/dev/null; then
    break
  fi
  sleep 1
done
if [[ "$ready" != "true" ]]; then
  echo "La API Staging no quedó lista para la prueba HTTP." >&2
  cat "$api_log" >&2
  exit 1
fi

wrong_password="rate-limit-invalid-$(date +%s)"
login_payload="$(jq -cn --arg email "$admin_email" --arg password "$wrong_password" \
  '{email:$email,password:$password}')"

client_ip="198.51.100.25"
untrusted_status="$(curl --silent --show-error --output "$response_file" --write-out '%{http_code}' \
  --interface 127.0.0.3 --max-time 10 \
  --header 'Content-Type: application/json' \
  --header "X-Forwarded-For: $client_ip" --header 'X-Forwarded-Proto: http' \
  --data "$login_payload" "$api_origin/api/Auth/login")"
if [[ "$untrusted_status" != "401" ]]; then
  echo "Origen no confiable con X-Forwarded-For falsificado devolvió $untrusted_status; se esperaba 401." >&2
  cat "$response_file" >&2
  exit 1
fi

for proxy_ip in 127.0.0.2 127.0.0.4; do
  for attempt in $(seq 1 5); do
    status="$(curl --silent --show-error --output "$response_file" --write-out '%{http_code}' \
      --interface "$proxy_ip" --max-time 10 \
      --header 'Content-Type: application/json' \
      --header "X-Forwarded-For: $client_ip" --header 'X-Forwarded-Proto: http' \
      --data "$login_payload" "$api_origin/api/Auth/login")"
    if [[ "$status" != "401" ]]; then
      echo "Login desde proxy confiable $proxy_ip devolvió $status antes del intento 11." >&2
      cat "$response_file" >&2
      exit 1
    fi
  done
done

status="$(curl --silent --show-error --output "$response_file" --dump-header "$headers_file" \
  --write-out '%{http_code}' --interface 127.0.0.2 --max-time 10 \
  --header 'Content-Type: application/json' \
  --header "X-Forwarded-For: $client_ip" --header 'X-Forwarded-Proto: http' \
  --data "$login_payload" "$api_origin/api/Auth/login")"
if [[ "$status" != "429" ]]; then
  echo "El intento 11 de login del cliente reenviado devolvió $status; se esperaba 429." >&2
  cat "$response_file" >&2
  exit 1
fi
retry_after="$(awk 'tolower($1)=="retry-after:" {gsub("\\r", "", $2); print $2}' "$headers_file")"
if [[ ! "$retry_after" =~ ^[0-9]+$ ]] || (( retry_after < 1 || retry_after > 60 )); then
  echo "Retry-After no está entre 1 y 60 segundos." >&2
  exit 1
fi

# Una IP cliente distinta, reenviada por un proxy confiable, no queda bloqueada.
valid_payload="$(jq -cn --arg email "$admin_email" --arg password "$admin_password" \
  '{email:$email,password:$password}')"
status="$(curl --silent --show-error --output "$response_file" --write-out '%{http_code}' \
  --interface 127.0.0.2 --max-time 15 \
  --header 'Content-Type: application/json' \
  --header 'X-Forwarded-For: 198.51.100.26' --header 'X-Forwarded-Proto: http' \
  --data "$valid_payload" "$api_origin/api/Auth/login")"
if [[ "$status" != "200" ]] || ! jq -e '(.token // .accessToken) | type == "string" and length > 0' \
  "$response_file" >/dev/null; then
  echo "La misma cuenta no pudo autenticarse desde otra IP de cliente detrás del proxy (HTTP $status)." >&2
  cat "$response_file" >&2
  exit 1
fi

echo "Proxy confiable verificado: X-Forwarded-For no confiable ignorado; 10 intentos 401 agregados entre proxies, 11.º 429 con Retry-After, misma cuenta desde otra IP 200."
