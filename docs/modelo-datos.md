# Modelo de datos

Concreta RT-030 a RT-035, RT-060, RT-061b. El esquema del estado de la run se define **antes** de implementar sistemas (RT-030) y está versionado. Versión actual: **0** (borrador, sin código).

## Estado de la run (`Run`)

Corrige tres desajustes del bloque de RT-030 respecto al resto del documento (ver `pendientes.md` I-1 a I-3): cinco atributos, un único slot de equipo, vínculos solo positivos.

```
Run
  versionEsquema        int
  semilla               ulong
  division              Tercera | Segunda | Primera | Continental | Mundial   (RF-128)
  club                  id de club inicial (RF-004)
  acto                  1..3
  nodoActual            id
  oro                   int
  historialNodos[]      (idNodo, tipo, resultado)
  mapa                  grafo del acto (nodos, aristas, rival asignado, modificador de jefe oculto/revelado)
  arbitros[]            6-8 árbitros de la run: id, nombre, rasgo, sobornosRecibidos (RF-061b, RF-064c)
  rerollsUsados         int  (RF-071b, coste creciente)
  snapshotData          copia de /data congelada al empezar (RT-061b)
  Plantilla
    Jugador
      id                int, asignado en orden de creación
      nombre            generado por raza (RF-020b)
      raza              id
      posicion          portero | defensa | centrocampista | delantero   (RF-022b)
      rareza            comun | raro | legendario                          (RF-023)
      nivel             1..8 (resurrección: máximo -2, RF-096)
      experiencia       int
      atributos         { fuerza, velocidad, tecnica, resistencia, correa }  1..99  (RF-022)
      rasgos[]          1..3 ids  (RF-022c)
      etiquetas[]       raza + posicion + rasgos + adquiridas (Chatarra, Automata, Descompuesto, Extraño)  (RF-022d)
      perks[]           ids, tamaño máximo = slots por rareza
      equipo            id de objeto o null  (RF-076: un único objeto)
      estadoFisico      sano | leve | grave | muerto  (RF-090)
      lesionesLeves     int (acumulables, RF-091)
      protesis[]        (slot, efecto)  (RF-095)
      salario           int, 0 salvo mercenarios  (RF-111)
      esMercenario      bool
      esCanterano       bool  (+33% experiencia, RF-114c)
      partidosSinJugar  int  (mercenarios, RF-111)
      vinculos[]        (idOtroJugador, tipo: sociedad | deuda_de_sangre | muro)  máximo 2  (RF-101, RF-102)
      duelo             partidos restantes, 0 si no aplica  (RF-104)
      contadores{}      acumuladores de perks entre partidos  (RF-070)
      progresoVinculos{} contadores parciales (asistencias A->B, partidos sin encajar como pareja)
  Alineacion
    asignaciones[]      (idJugador, columna, fila)  con portero en casilla fija  (RF-041)
    tamañoDoble[]       jugadores que ocupan 2 casillas  (RF-033)
  Consumibles
    equipados[]         (id, modo: manual | condicional, disparador)  máximo 3, mínimo 1 manual  (RF-080..082)
  Logros
    progreso{}          contadores de logros de desbloqueo (RF-125b)
```

Fuera de la run, en el perfil del jugador: razas desbloqueadas, divisiones ganadas por raza (RF-128b), perks/objetos/consumibles desbloqueados (RF-126), compendio de modificadores de jefe descubiertos (RF-014b), memorial acumulado.

## Estado del partido (`EstadoPartido`)

Es lo que recibe `Simulador.Ejecutar`. Se construye desde `Run` y no vuelve a ella salvo a través de los eventos.

```
EstadoPartido
  equipos[2]            plantilla en campo, alineación, consumibles equipados, es local
  arbitro               id, rasgo, criterio inicial (0 salvo objetos como "Amigo de la federación")
  modificadoresRegla[]  0..3 (jefes, RF-001b/c, RF-128)
  campo                 16x5 (RF-040); en turba 16x3 útil, filas invadidas fijas (RF-055b)
  activacionesManuales[] (idConsumible, tick)   ver arquitectura.md
```

## Ficheros de `/data`

Todos con esquema JSON en `/data/esquemas/` y validados por `tools/DataValidator` (RT-032, RT-083).

| Directorio | Contenido | Requisitos |
|---|---|---|
| `data/perks/` | Un perk por fichero | RF-065..072, RT-033 |
| `data/objetos/` | Equipamiento, con arquetipo maldito/fragil/restringido | RF-075..078 |
| `data/consumibles/` | Familias medico/tactico/sucio/sobrenatural, con tabla de resultados para sobornos | RF-080..085, RF-064b |
| `data/razas/` | Sesgo poblacional, etiqueta, regla exclusiva, dimensiones de sprite, generador de nombres | RF-030..035, RF-020b |
| `data/clubes/` | Raza, plantilla inicial, oro, regla especial | RF-004 |
| `data/rasgos/` | Rasgos de jugador y de portero, con modificadores de pesos de IA | RF-022c, RF-057e, RT-094 |
| `data/arbitros/` | Rasgos de árbitro y sus efectos sobre criterio y sobornos | RF-061, RF-064 |
| `data/ia/` | Pesos base por posición y por estado táctico | RT-093, RT-096 |
| `data/rivales/` | Equipos rivales diseñados a mano por acto y división | RF-015 |
| `data/jefes/` | Modificadores de regla | RF-001b, RF-014 |
| `data/economia/` | Oro por acto, multiplicadores, precios, objetivos de partido excelente | RF-114g..k |
| `data/balance/` | Configuraciones de equipos de referencia para `/Balance` | RT-052 |
| `data/l10n/` | Plantillas de descripción y textos, es/en | RT-035, RT-073 |

## Formato de perk (RT-033)

```json
{
  "id": "sed_de_sangre",
  "nombre": { "es": "Sed de sangre", "en": "Bloodlust" },
  "rareza": "raro",
  "tipo": "condicional",
  "disparador": "ENTRADA",
  "condicion": "tiene(ejecutor, 'Bruto') && criterio() < 0",
  "efecto": { "tipo": "modificar_atributo", "objetivo": "ejecutor", "atributo": "fuerza", "valor": 3, "duracion": "jugada" },
  "limite": { "por": "partido", "veces": 2 },
  "acumulaEntrePartidos": false,
  "letal": false,
  "soloPosicion": null
}
```

- `tipo`: `relleno` (60%), `condicional` (30%), `rompe_reglas` (10%) (RF-069). `/Balance` informa de la distribución real del catálogo.
- `disparador`: uno del catálogo RF-066.
- `condicion`: expresión NCalc. Vacía = siempre.
- `efecto.tipo`: catálogo cerrado, cada tipo con su plantilla de descripción. Conjunto inicial de fase 1: `modificar_atributo`, `modificar_correa`, `modificar_criterio`, `modificar_probabilidad` (falta, tarjeta, lesion, parada), `anular_evento`, `repetir_evento`, `curar`, `lesionar`, `sumar_contador`, `oro`. Se amplía por ADR ligero (entrada en `decisiones/` de una línea).
- `efecto.objetivo`: `ejecutor`, `receptor`, `rival`, `adyacentes`, `equipo`, `equipo_rival`, `con_etiqueta:<Etiqueta>`.
- `efecto.duracion`: `instantanea`, `jugada`, `partido`, `run`.
- `limite.por`: `jugada`, `partido`, `turba`, `run`. (El ejemplo original decía `parte`; no existen partes, RF-055.)
- `letal`: `true` obliga a destacar el perk en el informe de ojeo (RF-013) y es la única vía, junto a lesión grave sin tratar, de muerte (RF-093).

## Funciones NCalc propias (RT-034)

| Función | Devuelve |
|---|---|
| `tiene(quien, 'Etiqueta')` | bool. `quien` es `ejecutor`, `receptor` o `rival` |
| `turba()` | bool, partido en gol de oro |
| `criterio()` | int, -100..100 |
| `zona(quien)` | `'propia'`, `'centro'`, `'rival'` |
| `adyacente(quien, 'Etiqueta')` | bool, algún compañero con esa etiqueta en casilla-hogar adyacente (RF-044) |
| `distancia_porteria()` | int, casillas |
| `marcador()` | int, diferencia de goles desde el punto de vista del equipo del ejecutor |
| `tick()` | int |
| `contador('nombre')` | int, contador del ejecutor (RF-070) |

Las expresiones se compilan una vez al cargar. Un identificador desconocido es error de validación, no error en partido.

## Descripciones generadas (RT-035)

Cada `efecto.tipo` tiene una plantilla en `data/l10n/<idioma>/plantillas.json`, con parámetros del efecto y de la condición:

```
modificar_atributo: "{objetivo} gana {valor:+} de {atributo} durante {duracion}{condicion_texto}{limite_texto}"
```

Las condiciones NCalc se traducen con un pequeño *pretty-printer* por función (`tiene(ejecutor,'Bruto')` -> "si es Bruto"). Nunca hay campo `descripcion` en el JSON; si aparece, el validador lo rechaza.

## Objetos y consumibles

Comparten `efecto`, `condicion` y `limite` con los perks. Diferencias:

- Objeto: `arquetipo` (`maldito` con `contrapartida`, `fragil` con `usos` o `seRompeAlLesionarse`, `restringido` con `requiereEtiqueta`), `rareza`, `valorVenta` (RF-076b, RF-077).
- Consumible: `familia`, `disparadoresPermitidos` (RF-083), `esSoborno` con `tablaResultados[]` de `(resultado, probabilidadBase)` ajustada por rasgo del árbitro (RF-064b).

## Versionado

- `Run.versionEsquema` y `data/esquemas/version.json` suben con cualquier cambio de forma. Una run guardada con versión anterior carga con su snapshot de `/data` y una migración explícita, o se rechaza con mensaje claro. Nunca se migra en silencio.
