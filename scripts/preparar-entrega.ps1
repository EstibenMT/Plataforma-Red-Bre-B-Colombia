<#
.SYNOPSIS
  Revisa que todo este listo para la entrega del Issue #28 y arma el comentario.

.DESCRIPTION
  1. Comprueba que la API responde y que /token y /saldo funcionan.
  2. Lee rama, commit y estado de git.
  3. Extrae del log de la API (evidencias\log-api.txt) las lineas de POST /token y GET /saldo.
  4. Revisa que existan las capturas esperadas en la carpeta de evidencias.
  5. Escribe evidencias\comentario-issue-28.md listo para pegar en el Issue.

  No sube nada ni modifica el repositorio: solo lee y escribe en la carpeta de evidencias.
  Lo unico que usted debe escribir es la linea de "que nos fallo" (a proposito: tiene que ser real).

.EXAMPLE
  .\scripts\preparar-entrega.ps1
  .\scripts\preparar-entrega.ps1 -Falla "La app usaba localhost y no conectaba; se corrigio a 10.0.2.2"
#>
param(
    [string]$Api = "http://localhost:5080",
    [string]$CuentaId = "11111111-1111-1111-1111-111111111111",
    [string]$Evidencias = "C:\ITM\p-d\evidencias",
    [string]$UrlRepo = "",
    [string]$Falla = "<ESCRIBA AQUI UNA LINEA REAL: que fallo y como salieron>"
)

$ErrorActionPreference = "Continue"
$script:ok = 0; $script:mal = 0
function Revisa($condicion, $texto, $ayuda = "") {
    if ($condicion) { Write-Host "[ OK ] $texto" -ForegroundColor Green; $script:ok++ }
    else { Write-Host "[FALTA] $texto  $ayuda" -ForegroundColor Yellow; $script:mal++ }
}

# --- 1. API ---------------------------------------------------------------
$saldoTxt = "<saldo>"
try {
    $r = Invoke-RestMethod -Method Post "$Api/token" -ContentType "application/json" `
         -Body (@{ usuario = "ana.operadora"; clave = "Operadora-2026" } | ConvertTo-Json) -TimeoutSec 10
    Revisa ($r.rol -eq "operador") "POST /token devuelve rol operador"
    $s = Invoke-RestMethod "$Api/cuentas/$CuentaId/saldo" -Headers @{ Authorization = "Bearer $($r.token)" } -TimeoutSec 10
    $saldoTxt = "disponible $($s.saldoDisponible) / retenido $($s.saldoRetenido) UVB"
    Revisa $true "GET /saldo de la cuenta de demo: $saldoTxt"
} catch {
    Revisa $false "La API responde en $Api" "(¿esta corriendo 'dotnet run --launch-profile emulador'? ¿existe la cuenta?)"
}

# --- 2. git ---------------------------------------------------------------
$repo = Split-Path -Parent $PSScriptRoot
$rama = (git -C $repo branch --show-current) 2>$null
$hash = (git -C $repo log -1 --format=%h) 2>$null
$sucio = (git -C $repo status --porcelain) 2>$null | Where-Object { $_ -notmatch "\.vs/|/bin/|/obj/|__pycache__" }
Revisa ([bool]$hash) "git: rama '$rama', commit '$hash'"
Revisa (-not $sucio) "git: no hay cambios sin commit (lo que sustenta es lo que esta subido)" "(haga commit y push; vea la fase 2 de la guia)"
$remoto = (git -C $repo remote -v) 2>$null | Select-Object -First 1
if ($remoto) { Write-Host "       remoto: $remoto" }
Write-Host "       (confirme con el squad a que remoto se hace push; hoy solo existe 'docente')" -ForegroundColor DarkGray

# --- 3. log del servidor --------------------------------------------------
$log = Join-Path $Evidencias "log-api.txt"
$fragmento = @()
if (Test-Path $log) {
    $fragmento = @(Select-String -Path $log -Pattern "Request finished HTTP/1.1 (POST|GET) http://\S+/(token|cuentas/\S+/saldo) - [0-9]{3}" | ForEach-Object Line | Select-Object -Last 4)
}
Revisa (@($fragmento | Where-Object { $_ -match "POST .*/token - 200" }).Count -gt 0) "log-api.txt contiene POST /token" "(arranque la API con Tee-Object, fase 3.2)"
Revisa (@($fragmento | Where-Object { $_ -match "GET .*/saldo - 200" }).Count -gt 0) "log-api.txt contiene GET /saldo (token USADO por la app)"
$core = Join-Path $Evidencias "core-bancario.txt"
Revisa ((Test-Path $core) -and (Select-String -Path $core -Pattern "Abono confirmado" -Quiet)) "core-bancario.txt registra 'Abono confirmado'" "(fase 5)"

# --- 4. capturas ----------------------------------------------------------
foreach ($n in "saldo-app", "error-clave", "api-apagada", "transferencia-completada", "transferencia-compensada", "rol-consulta") {
    Revisa (Test-Path (Join-Path $Evidencias "$n.png")) "captura $n.png"
}
Revisa (Test-Path (Join-Path $Evidencias "mediciones-api.md")) "mediciones propias (medir-api-demo.ps1)" "(opcional)"

# --- 5. comentario --------------------------------------------------------
$url = if ($UrlRepo) { $UrlRepo } else { "<URL del repo del squad>" }
$frag = if ($fragmento.Count -gt 0) { ($fragmento -join "`n") } else { "<pegue aqui las lineas POST /token y GET /saldo del log>" }
$fence = '```'
$comentario = @"
## Squad 6 — Evaluable 3

**Repo / rama / commit:** $url · $rama · $hash

**Saldo real en la app:** (captura saldo-app.png adjunta). Coincide con GET /cuentas/{id}/saldo de la API: $saldoTxt.

**Log del servidor** (POST /token y GET /saldo, trazas distintas):
$fence
$frag
$fence

**Errores sin caerse:** clave mala -> "Usuario o contraseña incorrectos." (sin decir si fallo usuario o clave) · API apagada -> "No pudimos conectarnos con Bre-B…" y la app no se cierra.

**Core bancario:** el abono se confirma con la identidad de servicio core-bancario (rol banco); la transferencia queda Completada, y sin confirmar a los 15 s queda Compensada.

**Que nos fallo y como salimos:** $Falla
"@
New-Item -ItemType Directory -Force -Path $Evidencias | Out-Null
$destino = Join-Path $Evidencias "comentario-issue-28.md"
$comentario | Set-Content -Path $destino -Encoding UTF8

Write-Host "`n$($script:ok) comprobaciones OK, $($script:mal) pendientes." -ForegroundColor Cyan
Write-Host "Comentario generado: $destino"
if ($Falla -like "<ESCRIBA*") { Write-Host "Falta su linea de 'que nos fallo' (use -Falla o editela en el archivo)." -ForegroundColor Yellow }
