<#
.SYNOPSIS
  Confirma el abono de una transferencia con la identidad del banco destino.

.DESCRIPTION
  POST /retener solo aparta fondos. Si en 15 s nadie confirma el abono, la saga
  compensa. Confirmar es trabajo del core bancario (rol "banco"), no de un
  operador; este script usa esa identidad de servicio para la demo.

  Sin -TransferenciaId confirma la ultima retencion pendiente (consulta la
  base con docker exec; requiere el contenedor de PostgreSQL en marcha).

.EXAMPLE
  .\scripts\confirmar-abono-demo.ps1
  .\scripts\confirmar-abono-demo.ps1 -TransferenciaId 3f2c1a9e-0000-0000-0000-000000000000
#>
param(
    [string]$TransferenciaId,
    [string]$Api = "http://localhost:5080",
    [string]$ContenedorPg = "breb-postgres"
)

$ErrorActionPreference = "Stop"

# Usuario de laboratorio documentado (no es un secreto).
$cuerpo = @{ usuario = "core-bancario"; clave = "CoreBancario-2026" } | ConvertTo-Json
$token = (Invoke-RestMethod -Method Post -Uri "$Api/token" -ContentType "application/json" -Body $cuerpo).token

if (-not $TransferenciaId) {
    $sql = 'SELECT "TransferenciaId" FROM "Retenciones" WHERE NOT "Liberada" AND NOT "Liquidada" ORDER BY "CreadaEn" DESC LIMIT 1'
    # La consulta entra por stdin: pasarla como argumento pierde las comillas dobles en Windows PowerShell 5.1.
    $TransferenciaId = ($sql | docker exec -i $ContenedorPg psql -U postgres -d brebcuentas -t -A | Select-Object -First 1)
    if (-not $TransferenciaId) { Write-Host "No hay retenciones pendientes."; exit 1 }
    $TransferenciaId = $TransferenciaId.Trim()
}

$r = Invoke-WebRequest -Method Post -Uri "$Api/transferencias/$TransferenciaId/confirmar-abono" `
        -Headers @{ Authorization = "Bearer $token" } -UseBasicParsing
Write-Host "Abono confirmado para $TransferenciaId (HTTP $($r.StatusCode))"
