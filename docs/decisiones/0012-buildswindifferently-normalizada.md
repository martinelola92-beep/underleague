# 0012. `buildsWinDifferently` se mide normalizada contra la referencia de la propia raza

**Fecha:** 2026-09-04
**Estado:** Propuesta (requiere decisión del revisor: cambia una métrica del criterio de salida de la fase 1)
**Requisitos:** RT-056, RT-057, RF-044, RF-069

## Contexto

§8 de `docs/fase1-diseno.md` define `buildsWinDifferently` como: `orc_violence` produce ≥ 1,5× las lesiones
de `elf_tiki_taka`, y `elf_tiki_taka` ≥ 1,3× la cadena media de pases de `orc_violence`. Medido en valores
absolutos, las dos mitades están rotas, y por el mismo motivo: **comparan dos razas, no dos builds.**

- **Lesiones.** Con el catálogo de perks vacío, un equipo de orcos ya causa 0,47 lesiones por partido y uno
  de elfos 0,12 (3,9×, 5.000 partidos por celda). La mitad de lesiones de la métrica aprobaba con
  `orc_none` contra `elf_none`, sin un solo perk: no medía nada del diseño de las builds.
- **Cadena de pases.** Al revés. `orc_none` encadena 4,47 pases por posesión y `elf_none` 3,67: los orcos
  son lentos, tienen la correa corta y juegan juntos, así que la posesión se les va en pases cortos y
  seguros; los elfos son rápidos, tienen la correa larga y regatean, así que la posesión se les va antes.
  Ningún perk del vocabulario de efectos de §2 mueve esa magnitud lo bastante: el máximo medido para un
  bloque élfico entero es 4,75 y el mínimo para uno orco 4,25 (ratio 1,12), muy lejos de 1,3. No existe un
  tipo de efecto que toque la utilidad de las acciones, que es lo que decide si un jugador pasa o dispara,
  así que la cadena de pases no es una palanca de perk.

## Decisión propuesta

Las dos magnitudes se miden **relativas a la referencia sin perks de la propia raza, en los mismos
partidos**: el denominador de cada build es la otra cara de su celda de la matriz (la fila
`(referencia, build)`), y lo que se compara es cuánto multiplica cada build lo que ya hacía su raza.

```
lesiones(orc_violence) / lesiones(orc_none)   ≥ 1,5 ×   lesiones(elf_tiki_taka) / lesiones(elf_none)
cadena(elf_tiki_taka)  / cadena(elf_none)     ≥ 1,3 ×   cadena(orc_violence)    / cadena(orc_none)
```

Además, "lesiones que produce" se lee como las lesiones **causadas al rival** (`InjuriesAgainst`), no como
las sufridas: era lo que decía §8 ("produce") y no lo que calculaba el paquete H.

## Alternativas descartadas

- **Dejar la métrica en absoluto y aceptar que no se cumple**: deja la fase 1 sin criterio de salida por un
  defecto de la métrica, no del juego.
- **Comparar dos builds de la misma raza**: quita el confusor, pero §8 nombra explícitamente
  `orc_violence` y `elf_tiki_taka`, y perdería la idea de "dos razas, dos formas de ganar".
- **Añadir un tipo de efecto `modifyActionUtility`** (un perk que cambie el peso de `Pass`, `Shoot` o
  `Tackle` en la IA de utilidad) para que la cadena de pases sí sea una palanca de perk: es la solución
  de diseño correcta y la recomendación de este ADR para la fase 2, pero es un tipo de efecto nuevo
  (esquema, plantillas de descripción, motor y tests) fuera del encargo de cierre de la fase 1. Con él, la
  métrica podría volver a medirse en absoluto.

## Consecuencias

- `Sim/Analysis/BuildMetrics.BuildsWinDifferently` recibe el mapa de referencias y calcula los dos
  cocientes normalizados. Los umbrales (1,5 y 1,3) **no cambian**.
- Medido con el catálogo final: lesiones 3,0× (orc_violence multiplica por 2,1 las de `orc_none`;
  elf_tiki_taka por 0,71 las de `elf_none`) y cadena 1,35×. Los dos aprueban con margen.
- Se pierde la lectura directa "esta build lesiona el doble que aquella"; hay que leer la tabla de
  `docs/balance/fase1-perks.md`, que da los dos valores absolutos y el normalizado.
