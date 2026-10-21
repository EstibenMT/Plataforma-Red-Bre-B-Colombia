<#
.SYNOPSIS
  Mide cuanto tarda POST /token y GET /cuentas/{id}/saldo, y guarda el resultado.

.DESCRIPTION
  Cronometra con System.Diagnostics.Stopwatch alrededor de cada llamada HTTP,
  desde ESTE equipo (no desde el emulador). Descarta una corrida de calentamiento
  y reporta las N siguientes con su mediana.

  LO QUE MIDE: el tiempo de las dos llamadas de la API, visto desde Windows.
  LO QUE NO MIDE: el tiempo "toque el boton -> numero en pantalla" de la app.
  Ese no se puede medir bien con herramientas mas lentas que el fenomeno
  (el docente lo descarto en la Semana 12). No lo reporte como si lo fuera.

.EXAMPLE
  .\scripts\medir-api-demo.ps1
  .\scripts\medir-api-demo.ps1 -Corridas 10 -Salida C:\ITM\p-d\evidencias\mediciones-api.md
#>
param(
    [string]$Api = "http://localhost:5080",
    [string]$CuentaId = "11111111-1111-1111-1111-111111111111",
    [string]$Usuario = "ana.operadora",
    [string]$Clave = "Operadora-2026",
    [int]$Corridas = 5,
    [string]$Salida = "C:\ITM\p-d\evidencias\mediciones-api.md"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
$http = New-Object System.Net.Http.HttpClient
$http.Timeout = [TimeSpan]::FromSeconds(15)

function Mediana($valores) {
    $o = @($valores | Sort-Object)
    $n = $o.Count
    if ($n % 2) { return $o[[int](($n - 1) / 2)] }
    return ($o[$n / 2 - 1] + $o[$n / 2]) / 2
}

function Una-Corrida {
    $cuerpo = New-Object System.Net.Http.StringContent(
        (@{ usuario = $Usuario; clave = $Clave } | ConvertTo-Json), [Text.Encoding]::UTF8, "application/json")

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $r = $http.PostAsync("$Api/token", $cuerpo).GetAwaiter().GetResult()
    $txt = $r.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $sw.Stop()
    if (-not $r.IsSuccessStatusCode) { throw "POST /token respondio HTTP $([int]$r.StatusCode)" }
    $tToken = $sw.Elapsed.TotalMilliseconds
    $token = ($txt | ConvertFrom-Json).token

    $req = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Get, "$Api/cuentas/$CuentaId/saldo")
    $req.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $token)
    $sw.Restart()
    $r2 = $http.SendAsync($req).GetAwaiter().GetResult()
    $null = $r2.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $sw.Stop()
    if (-not $r2.IsSuccessStatusCode) { throw "GET /saldo respondio HTTP $([int]$r2.StatusCode)" }

    [pscustomobject]@{ Token = [math]::Round($tToken); Saldo = [math]::Round($sw.Elapsed.TotalMilliseconds) }
}

try {
    Write-Host "Calentamiento (no cuenta)..."
    $null = Una-Corrida

    $filas = @()
    for ($i = 1; $i -le $Corridas; $i++) {
        $f = Una-Corrida
        $filas += [pscustomobject]@{ Corrida = $i; Token = $f.Token; Saldo = $f.Saldo }
        Write-Host ("Corrida {0}: POST /token {1} ms | GET /saldo {2} ms" -f $i, $f.Token, $f.Saldo)
    }
}
catch {
    Write-Host "No se pudo medir: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Compruebe que la API esta arriba en $Api y que la cuenta $CuentaId existe."
    exit 1
}

$mT = Mediana ($filas | ForEach-Object Token)
$mS = Mediana ($filas | ForEach-Object Saldo)
Write-Host ("`nMEDIANA ({0} corridas): POST /token {1} ms | GET /saldo {2} ms" -f $Corridas, $mT, $mS) -ForegroundColor Green

$md = @()
$md += "# Mediciones de la API (propias)"
$md += ""
$md += "- Fecha: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
$md += "- Equipo: $env:COMPUTERNAME · API: $Api · cuenta: $CuentaId"
$md += "- Metodo: Stopwatch alrededor de cada llamada HTTP, desde Windows (no desde el emulador). 1 corrida de calentamiento descartada, $Corridas corridas contadas."
$md += ""
$md += "| Corrida | POST /token (ms) | GET /saldo (ms) |"
$md += "|---|---|---|"
foreach ($f in $filas) { $md += "| $($f.Corrida) | $($f.Token) | $($f.Saldo) |" }
$md += "| **Mediana** | **$mT** | **$mS** |"
$md += ""
$md += "**Limites:** son cifras de una sola maquina y sirven como orden de magnitud. No incluyen el tiempo de pantalla de la app ni la red del emulador; ese tiempo **no se midio**."
New-Item -ItemType Directory -Force -Path (Split-Path $Salida) | Out-Null
$md | Set-Content -Path $Salida -Encoding UTF8
Write-Host "Guardado en $Salida"
