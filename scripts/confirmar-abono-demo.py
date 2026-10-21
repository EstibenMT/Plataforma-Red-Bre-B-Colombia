#!/usr/bin/env python3
"""
Alternativa multiplataforma a confirmar-abono-demo.ps1: hace de CORE BANCARIO.

POST /retener solo aparta fondos. Si en 15 s nadie confirma el abono, la saga
compensa. Confirmar es trabajo del banco destino (rol "banco"); este script usa
la identidad de servicio `core-bancario`. Solo libreria estandar de Python 3.

Uso:
  python scripts/confirmar-abono-demo.py                       # confirma la ultima retencion pendiente
  python scripts/confirmar-abono-demo.py <transferenciaId>     # confirma una concreta
  python scripts/confirmar-abono-demo.py --vigilar             # MODO DEMO: queda escuchando y
                                                               # confirma cada transferencia nueva
  python scripts/confirmar-abono-demo.py --vigilar --demora 5  # espera 5 s antes de confirmar
                                                               # (para que se vea "en proceso")

Sin id, busca la retencion pendiente con `docker exec ... psql` (requiere el
contenedor breb-postgres en marcha). Con id no necesita Docker.
"""
import argparse
import json
import subprocess
import sys
import time
import urllib.error
import urllib.request

USUARIO, CLAVE = "core-bancario", "CoreBancario-2026"   # laboratorio, no es secreto
SQL = ('SELECT "TransferenciaId" FROM "Retenciones" '
       'WHERE NOT "Liberada" AND NOT "Liquidada" ORDER BY "CreadaEn" DESC LIMIT 1')


def token(api):
    req = urllib.request.Request(
        f"{api}/token", data=json.dumps({"usuario": USUARIO, "clave": CLAVE}).encode(),
        headers={"Content-Type": "application/json"}, method="POST")
    with urllib.request.urlopen(req, timeout=10) as r:
        return json.load(r)["token"]


def pendiente(contenedor):
    try:
        salida = subprocess.run(
            ["docker", "exec", "-i", contenedor, "psql", "-U", "postgres", "-d", "brebcuentas", "-t", "-A"],
            input=SQL, capture_output=True, text=True, timeout=15)
    except (FileNotFoundError, subprocess.TimeoutExpired):
        sys.exit("No pude ejecutar docker. Pase el id: python confirmar-abono-demo.py <transferenciaId>")
    lineas = salida.stdout.strip().splitlines()
    return lineas[0].strip() if lineas else None


def confirmar(api, tid):
    try:
        req = urllib.request.Request(
            f"{api}/transferencias/{tid}/confirmar-abono", data=b"", method="POST",
            headers={"Authorization": f"Bearer {token(api)}"})
        with urllib.request.urlopen(req, timeout=10) as r:
            print(f"Abono confirmado para {tid} (HTTP {r.status})")
    except urllib.error.HTTPError as e:
        print(f"La API rechazo la confirmacion: HTTP {e.code}")
    except (urllib.error.URLError, OSError) as e:
        # API apagada o sin red: no tumbar el vigilante, avisar y seguir.
        print(f"No se pudo contactar la API ({getattr(e, 'reason', e)}). Reintente cuando este arriba.")


def main():
    # Con '| Tee-Object' la salida va a una tuberia y Python la guarda en un bufer:
    # sin esto no se veria nada hasta cerrar el script. Una linea, un volcado.
    sys.stdout.reconfigure(line_buffering=True)
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("transferencia_id", nargs="?")
    p.add_argument("--api", default="http://localhost:5080")
    p.add_argument("--contenedor", default="breb-postgres")
    p.add_argument("--vigilar", action="store_true", help="confirma toda transferencia nueva hasta Ctrl+C")
    p.add_argument("--demora", type=float, default=2.0, help="segundos antes de confirmar en --vigilar (max 10)")
    a = p.parse_args()

    if a.transferencia_id:
        return confirmar(a.api, a.transferencia_id)

    if not a.vigilar:
        tid = pendiente(a.contenedor)
        if not tid:
            sys.exit("No hay retenciones pendientes.")
        return confirmar(a.api, tid)

    print(f"[core-bancario] vigilando retenciones pendientes (demora {a.demora}s). Ctrl+C para salir.")
    vistas = set()
    try:
        while True:
            tid = pendiente(a.contenedor)
            if tid and tid not in vistas:
                vistas.add(tid)
                print(f"[core-bancario] retencion nueva {tid}")
                time.sleep(max(0.0, min(a.demora, 10.0)))   # la saga compensa a los 15 s
                confirmar(a.api, tid)
            time.sleep(1)
    except KeyboardInterrupt:
        print("\n[core-bancario] detenido.")


if __name__ == "__main__":
    main()
