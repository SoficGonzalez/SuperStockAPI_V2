#!/bin/bash
BACKUP_NAME="superstock-backup-$(date +%Y%m%d-%H%M%S)"
echo "[$(date)] Iniciando backup: $BACKUP_NAME"
nodetool snapshot -t "$BACKUP_NAME" superstock
echo "[$(date)] Backup completado: $BACKUP_NAME"
nodetool listsnapshots