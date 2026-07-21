#!/bin/bash
set -euo pipefail

ADMIN_TOKEN_FILE=/var/lib/influxdb3/admin-token.json

cat > "$ADMIN_TOKEN_FILE" <<EOF
{
  "token": "$INFLUXDB_TOKEN",
  "name": "_admin",
  "description": "Admin token for VitalBand"
}
EOF

export INFLUXDB3_AUTH_TOKEN=$INFLUXDB_TOKEN

if [ ! -f /var/lib/influxdb3/.setup-done ]; then
  echo "Running first-time setup..."

  /usr/bin/entrypoint.sh influxdb3 serve \
    --node-id=node0 \
    --object-store=file \
    --data-dir=/var/lib/influxdb3/data \
    --plugin-dir=/var/lib/influxdb3/plugins \
    --admin-token-file="$ADMIN_TOKEN_FILE" &
  SERVER_PID=$!

  for i in $(seq 1 30); do
    if influxdb3 query --database _system "SELECT 1" &>/dev/null; then
      break
    fi
    sleep 1
  done

  influxdb3 create database sensores 2>/dev/null || true
  touch /var/lib/influxdb3/.setup-done

  kill $SERVER_PID 2>/dev/null || true
  wait $SERVER_PID 2>/dev/null || true
  echo "Setup complete."
fi

exec /usr/bin/entrypoint.sh "$@"
