# Restaura todas las tablas del keyspace al estado del backup.
# Uso: powershell -ExecutionPolicy Bypass -File .\scripts\restore-manual.ps1 -Tag backup_YYYYMMDD_HHMMSS


param(
    [Parameter(Mandatory = $true)]
    [string]$Tag
)

$ErrorActionPreference = "Stop"
$Keyspace = "superstock"
$ScriptSh = Join-Path $PSScriptRoot "restore-simple.sh"

Write-Host "=== RESTORE keyspace $Keyspace ===" -ForegroundColor Cyan
Write-Host "Tag: $Tag"
Write-Host ""
Write-Host "Detenga Visual Studio antes de continuar." -ForegroundColor Yellow
$ok = Read-Host "Continuar? (s/N)"
if ($ok -notmatch "^[sS]$") { exit }

# Copiar script al contenedor (quitar CRLF de Windows)
docker cp $ScriptSh cassandra-seed:/tmp/restore-simple.sh
docker exec cassandra-seed bash -c "tr -d '\r' < /tmp/restore-simple.sh > /tmp/r.sh && bash /tmp/r.sh $Tag"

# Sincronizar el keyspace completo al segundo nodo
Write-Host ""
Write-Host "Sincronizando nodo 2 (repair keyspace completo)..."
docker exec cassandra-seed nodetool repair $Keyspace

Write-Host ""
Write-Host "Listo. Restaurado completo de la base de datos" -ForegroundColor Green
