# Backup manual: nodetool snapshot en 2 nodos + copia al PC.
# Uso: powershell -ExecutionPolicy Bypass -File .\scripts\backup-manual.ps1

param([string]$Tag = "")

$ErrorActionPreference = "Stop"
$Keyspace = "superstock"
$Nodes = @("cassandra-seed", "cassandra-node2")

if (-not $Tag) { $Tag = "backup_{0:yyyyMMdd_HHmmss}" -f (Get-Date) }
$DestDir = Join-Path $PSScriptRoot "..\backups\manual\$Tag"
New-Item -ItemType Directory -Force -Path $DestDir | Out-Null

Write-Host "=== BACKUP ===" -ForegroundColor Cyan
Write-Host "Tag: $Tag"
Write-Host ""

foreach ($node in $Nodes) {
    Write-Host "[$node] snapshot..."
    docker exec $node nodetool snapshot $Keyspace -t $Tag

    Write-Host "[$node] exportar a PC..."
    $tar = "/tmp/$Tag-$node.tar.gz"
    $out = Join-Path $DestDir "$node.tar.gz"
    docker exec $node bash -c "find /var/lib/cassandra/data/$Keyspace -path '*/snapshots/$Tag/*' -type f -print0 | tar czf $tar --null -T -"
    docker cp "${node}:$tar" $out
    docker exec $node rm -f $tar
    Write-Host "  OK: $out"
}

Write-Host ""
Write-Host "Backup listo. Para restaurar: " -ForegroundColor Green
Write-Host "  powershell -ExecutionPolicy Bypass -File .\scripts\restore-manual.ps1 -Tag $Tag"
