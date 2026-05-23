#!/usr/bin/env bash
# Restauracion manual desde snapshot exportado.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

TAG="${1:-}"
if [ -z "$TAG" ]; then
  echo "Uso: $0 <tag>"
  echo "Ejemplo: $0 backup_20260523_120000"
  echo ""
  echo "Backups disponibles:"
  ls -1 "$MANUAL_DIR" 2>/dev/null || echo "  (ninguno)"
  exit 1
fi

DEST_DIR="${MANUAL_DIR}/${TAG}"
INNER_SCRIPT="${SCRIPT_DIR}/restore-node-inner.sh"

if [ ! -d "$DEST_DIR" ]; then
  echo "ERROR: No existe el backup '$TAG' en $DEST_DIR" >&2
  exit 1
fi

echo "=== Restauracion manual Cassandra ==="
echo "Tag:     $TAG"
echo "Origen:  $DEST_DIR"
echo ""
echo "ADVERTENCIA: Esto truncara las tablas del keyspace '$KEYSPACE' antes de restaurar."
read -r -p "Continuar? (s/N): " confirm
if [[ ! "$confirm" =~ ^[sS]$ ]]; then
  echo "Cancelado."
  exit 0
fi

require_cluster_healthy

if docker ps --format '{{.Names}}' | grep -qx "superstock-api"; then
  echo "Deteniendo superstock-api..."
  docker stop superstock-api >/dev/null
fi

for node in "${NODES[@]}"; do
  archive="${DEST_DIR}/${node}.tar.gz"
  if [ ! -f "$archive" ]; then
    echo "ERROR: Falta archivo $archive" >&2
    exit 1
  fi

  echo "[${node}] Restaurando..."
  docker cp "$archive" "${node}:/tmp/restore-${TAG}.tar.gz"
  docker cp "$INNER_SCRIPT" "${node}:/tmp/restore-node-inner.sh"
  docker exec "$node" bash -c "tr -d '\r' < /tmp/restore-node-inner.sh > /tmp/restore-node-fixed.sh && bash /tmp/restore-node-fixed.sh $KEYSPACE $TAG ${TABLES[*]}"
  docker exec "$node" rm -f /tmp/restore-node-inner.sh /tmp/restore-node-fixed.sh
done

echo ""
echo "Restauracion completada."
echo "Verifique: docker exec cassandra-seed cqlsh -e \"SELECT nombre, is_deleted FROM ${KEYSPACE}.productos;\""
