#!/bin/bash
# Prueba de humo del flujo completo contra la API (local o desplegada).
# Uso: scripts/prueba-flujo.sh [url_base]   (por defecto http://localhost:5047)
# Requiere: curl y python3. Crea una solicitud nueva y la lleva hasta Aprobado.
B=${1:-http://localhost:5047}
set -e
export TMP=$(mktemp -d)
tok() { B1="{\"usuario\":\"$1\",\"password\":\"Demo123!\"}"; curl -s -X POST $B/api/auth/login -H 'Content-Type: application/json' -d "$B1" | python3 -c 'import sys,json;print(json.load(sys.stdin)["token"])'; }
ANA=$(tok ana); CARLA=$(tok carla); MALLAS=$(tok mallas); VALLE=$(tok valle); LUIS=$(tok luis)
req() { local t=$1; shift; curl -s -o $TMP/r.json -w "%{http_code}" -H "Authorization: Bearer $t" "$@"; }
FALLAS=0
chk() { local esperado=$1 obtenido=$2 desc=$3; if [ "$esperado" = "$obtenido" ]; then echo "OK    $desc ($obtenido)"; else echo "FALLA $desc: esperado $esperado, obtenido $obtenido"; cat $TMP/r.json; echo; FALLAS=$((FALLAS+1)); fi; }
PROV='{"proveedorId":2}'; J='Content-Type: application/json'
HOY=$(date +%F); MAN=$(python3 -c "import datetime;print(datetime.date.today()+datetime.timedelta(7))")

chk 401 "$(curl -s -o /dev/null -w '%{http_code}' -X POST $B/api/auth/login -H "$J" -d '{"usuario":"ana","password":"x"}')" "login con clave incorrecta"
chk 401 "$(curl -s -o /dev/null -w '%{http_code}' $B/api/solicitudes)" "sin token"
B2="{\"material\":\"Tela prueba\",\"tipo\":\"Tela\",\"descripcion\":\"desc\",\"fechaObjetivo\":\"$MAN\",\"prioridad\":\"Alta\"}"; chk 201 "$(req $ANA -X POST $B/api/solicitudes -H "$J" -d "$B2")" "ana crea"
ID=$(python3 -c 'import json,os;print(json.load(open(os.environ["TMP"]+"/r.json"))["id"])')
B3="{\"material\":\"x\",\"tipo\":\"Tela\",\"descripcion\":\"d\",\"fechaObjetivo\":\"$MAN\",\"prioridad\":\"Alta\"}"; chk 403 "$(req $CARLA -X POST $B/api/solicitudes -H "$J" -d "$B3")" "coordinador no crea"
B4="{\"material\":\" \",\"tipo\":\"Tela\",\"descripcion\":\"d\",\"fechaObjetivo\":\"$MAN\",\"prioridad\":\"Alta\"}"; chk 400 "$(req $ANA -X POST $B/api/solicitudes -H "$J" -d "$B4")" "material vacío"
B5="{\"material\":\"x\",\"tipo\":\"Nope\",\"descripcion\":\"d\",\"fechaObjetivo\":\"$MAN\",\"prioridad\":\"Alta\"}"; chk 400 "$(req $ANA -X POST $B/api/solicitudes -H "$J" -d "$B5")" "tipo inválido"
B6="{\"fechaCompromiso\":\"$MAN\"}"; chk 404 "$(req $MALLAS -X POST $B/api/solicitudes/$ID/aceptar -H "$J" -d "$B6")" "proveedor no actúa sobre una no asignada"
chk 404 "$(req $MALLAS $B/api/solicitudes/$ID)" "proveedor no ve detalle ajeno"
chk 204 "$(req $CARLA -X POST $B/api/solicitudes/$ID/asignar -H "$J" -d "$PROV")" "carla asigna a Mallas"
chk 409 "$(req $CARLA -X POST $B/api/solicitudes/$ID/asignar -H "$J" -d "$PROV")" "asignar dos veces"
B7="{\"fechaCompromiso\":\"$MAN\"}"; chk 404 "$(req $VALLE -X POST $B/api/solicitudes/$ID/aceptar -H "$J" -d "$B7")" "otro proveedor no acepta"
B8="{\"fechaEnvio\":\"$HOY\",\"transportista\":\"DHL\",\"numeroGuia\":\"G1\"}"; chk 409 "$(req $MALLAS -X POST $B/api/solicitudes/$ID/envio -H "$J" -d "$B8")" "enviar sin aceptar"
B9="{\"fechaCompromiso\":\"$MAN\"}"; chk 204 "$(req $MALLAS -X POST $B/api/solicitudes/$ID/aceptar -H "$J" -d "$B9")" "mallas acepta"
B10="{\"fechaEnvio\":\"$HOY\",\"transportista\":\"DHL\",\"numeroGuia\":\"G1\"}"; chk 204 "$(req $MALLAS -X POST $B/api/solicitudes/$ID/envio -H "$J" -d "$B10")" "mallas envía"
chk 400 "$(req $CARLA -X POST $B/api/solicitudes/$ID/evaluacion -F resultado=Cumple -F comentarios=ok)" "evaluación sin fotos"
printf 'no soy imagen' > $TMP/x.txt
chk 400 "$(req $CARLA -X POST $B/api/solicitudes/$ID/evaluacion -F resultado=Cumple -F comentarios=ok -F "fotos=@$TMP/x.txt;type=text/plain")" "foto con tipo inválido"
FOTO="$(cd "$(dirname "$0")/.." && pwd)/src/PortalMateriales.Infrastructure/Seed/muestra.png"
chk 204 "$(req $CARLA -X POST $B/api/solicitudes/$ID/evaluacion -F resultado=NoCumple -F comentarios='tono distinto' -F "fotos=@$FOTO;type=image/png" -F "fotos=@$FOTO;type=image/png")" "carla evalúa con 2 fotos"
chk 403 "$(req $LUIS -X POST $B/api/solicitudes/$ID/rechazar -H "$J" -d '{"comentario":"no"}' )" "otro solicitante no decide"
chk 400 "$(req $ANA -X POST $B/api/solicitudes/$ID/rechazar -H "$J" -d '{}' )" "rechazo sin comentario"
chk 204 "$(req $ANA -X POST $B/api/solicitudes/$ID/rechazar -H "$J" -d '{"comentario":"repetir"}' )" "ana rechaza"
B11="{\"fechaCompromiso\":\"$MAN\"}"; chk 204 "$(req $MALLAS -X POST $B/api/solicitudes/$ID/aceptar -H "$J" -d "$B11")" "mallas recompromete"
B12="{\"fechaCompromiso\":\"$MAN\"}"; chk 409 "$(req $MALLAS -X POST $B/api/solicitudes/$ID/aceptar -H "$J" -d "$B12")" "recomprometer con compromiso vigente"
B13="{\"fechaEnvio\":\"$HOY\",\"transportista\":\"FedEx\",\"numeroGuia\":\"G2\"}"; chk 204 "$(req $MALLAS -X POST $B/api/solicitudes/$ID/envio -H "$J" -d "$B13")" "mallas reenvía"
chk 204 "$(req $CARLA -X POST $B/api/solicitudes/$ID/evaluacion -F resultado=Cumple -F comentarios='ok' -F "fotos=@$FOTO;type=image/png")" "carla evalúa otra vez"
chk 204 "$(req $ANA -X POST $B/api/solicitudes/$ID/aprobar -H "$J" -d '{"comentario":"listo"}' )" "ana aprueba"
chk 409 "$(req $ANA -X POST $B/api/solicitudes/$ID/aprobar -H "$J" -d '{}' )" "aprobar dos veces"
chk 200 "$(req $MALLAS $B/api/solicitudes/$ID)" "detalle (proveedor asignado)"
python3 -c '
import json,os;d=json.load(open(os.environ["TMP"]+"/r.json"))
print("     estado:",d["estado"],"| envíos:",len(d["envios"]),"| evaluaciones:",len(d["evaluaciones"]),"| historial:",len(d["historial"]))
for h in d["historial"]: print("      ",h["fecha"][:16],h["usuario"],"-",h["accion"],h["estadoAnterior"],"->",h["estadoNuevo"],"|",h["detalle"])
open(os.environ["TMP"]+"/foto_url","w").write(d["evaluaciones"][0]["fotos"][0]["url"])'
chk 200 "$(req $ANA $B$(cat $TMP/foto_url))" "descargar foto"
file $TMP/r.json | sed 's/^/     /'
chk 404 "$(req $VALLE $B$(cat $TMP/foto_url))" "proveedor ajeno no ve la foto"
chk 200 "$(req $MALLAS "$B/api/solicitudes")" "listado proveedor"
python3 -c 'import json,os;d=json.load(open(os.environ["TMP"]+"/r.json"));print("     mallas ve",len(d),"solicitudes, proveedores:",{x["proveedor"] for x in d})'
chk 200 "$(req $ANA "$B/api/solicitudes?estado=EnProgreso")" "filtro por estado"
python3 -c 'import json,os;d=json.load(open(os.environ["TMP"]+"/r.json"));print("     EnProgreso:",[(x["id"],x["vencida"]) for x in d])'
# ---- Maestro de proveedores ----
RTN=$(python3 -c "import random;print(''.join(random.choice('0123456789') for _ in range(14)))")
P1="{\"nombre\":\"Proveedor Prueba\",\"tipo\":\"Nacional\",\"rtn\":\"$RTN\",\"moneda\":\"hnl\",\"idioma\":\"es\",\"correo\":\"prueba@prov.example\",\"direccion\":\"San Pedro Sula\",\"contactos\":[{\"nombre\":\"Luis\",\"puesto\":\"Ventas\",\"telefono\":\"+504 9999-0000\",\"correo\":\"luis@prov.example\"}]}"
chk 403 "$(req $ANA -X POST $B/api/proveedores -H "$J" -d "$P1")" "solicitante no crea proveedores"
chk 403 "$(req $MALLAS $B/api/proveedores)" "proveedor no ve el maestro"
chk 201 "$(req $CARLA -X POST $B/api/proveedores -H "$J" -d "$P1")" "carla crea proveedor nacional con contacto"
PID=$(python3 -c 'import json,os;print(json.load(open(os.environ["TMP"]+"/r.json"))["proveedor"]["id"])')
chk 400 "$(req $CARLA -X POST $B/api/proveedores -H "$J" -d "$P1")" "RTN repetido"
P2="{\"nombre\":\"Sin RTN\",\"tipo\":\"Nacional\",\"moneda\":\"HNL\",\"idioma\":\"es\",\"correo\":\"x@prov.example\",\"direccion\":\"SPS\"}"
chk 400 "$(req $CARLA -X POST $B/api/proveedores -H "$J" -d "$P2")" "nacional sin RTN"
P3="{\"nombre\":\"Moneda mala\",\"tipo\":\"Internacional\",\"moneda\":\"ZZZ\",\"idioma\":\"en\",\"correo\":\"x@prov.example\",\"direccion\":\"Miami\"}"
chk 400 "$(req $CARLA -X POST $B/api/proveedores -H "$J" -d "$P3")" "moneda no ISO"
C1="{\"nombre\":\"Rosa\",\"puesto\":\"Calidad\",\"telefono\":\"+504 9999-1111\",\"correo\":\"rosa@prov.example\"}"
chk 201 "$(req $CARLA -X POST $B/api/proveedores/$PID/contactos -H "$J" -d "$C1")" "agrega contacto"
CID=$(python3 -c 'import json,os;print(json.load(open(os.environ["TMP"]+"/r.json"))["id"])')
chk 400 "$(req $CARLA -X POST $B/api/proveedores/$PID/contactos -H "$J" -d "$C1")" "contacto con correo repetido"
C2="{\"nombre\":\"Rosa Paz\",\"puesto\":\"Jefa de calidad\",\"telefono\":\"+504 9999-1111\",\"correo\":\"rosa@prov.example\"}"
chk 204 "$(req $CARLA -X PUT $B/api/proveedores/$PID/contactos/$CID -H "$J" -d "$C2")" "actualiza contacto"
chk 204 "$(req $CARLA -X DELETE $B/api/proveedores/$PID/contactos/$CID)" "elimina contacto"
chk 200 "$(req $ANA $B/api/proveedores/$PID)" "solicitante ve detalle del proveedor"
python3 -c 'import json,os;d=json.load(open(os.environ["TMP"]+"/r.json"));p=d["proveedor"];print("     ",p["nombre"],p["tipo"],p["rtn"],p["moneda"],p["idioma"],"activo" if p["activo"] else "inactivo","| contactos:",[c["nombre"] for c in d["contactos"]])'
chk 204 "$(req $CARLA -X POST $B/api/proveedores/$PID/desactivar)" "desactiva proveedor"
B20="{\"material\":\"Prueba inactivo\",\"tipo\":\"Tela\",\"descripcion\":\"d\",\"fechaObjetivo\":\"$MAN\",\"prioridad\":\"Baja\"}"
req $ANA -X POST $B/api/solicitudes -H "$J" -d "$B20" >/dev/null
SID=$(python3 -c 'import json,os;print(json.load(open(os.environ["TMP"]+"/r.json"))["id"])')
B21="{\"proveedorId\":$PID}"
chk 400 "$(req $CARLA -X POST $B/api/solicitudes/$SID/asignar -H "$J" -d "$B21")" "no asigna a proveedor inactivo"
chk 200 "$(req $CARLA "$B/api/proveedores?activo=false")" "filtro de inactivos"
python3 -c 'import json,os;print("      inactivos:",[p["nombre"] for p in json.load(open(os.environ["TMP"]+"/r.json"))])'
chk 204 "$(req $CARLA -X POST $B/api/proveedores/$PID/activar)" "reactiva proveedor"
chk 204 "$(req $CARLA -X POST $B/api/solicitudes/$SID/asignar -H "$J" -d "$B21")" "asigna tras reactivar"
chk 403 "$(req $MALLAS $B/api/indicadores)" "proveedor no ve indicadores"
chk 200 "$(req $CARLA $B/api/indicadores)" "indicadores"
python3 -c 'import json,os;d=json.load(open(os.environ["TMP"]+"/r.json"));print("     ",d["porEstado"],"vencidas:",d["vencidas"]);[print("     ",c) for c in d["entregasATiempo"]]'
echo; [ $FALLAS = 0 ] && echo "Todo OK" || { echo "$FALLAS verificación(es) fallaron"; exit 1; }
