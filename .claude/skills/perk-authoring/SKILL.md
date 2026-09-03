---
name: perk-authoring
description: Crear o revisar un perk, objeto de equipamiento o consumible en /data cumpliendo el formato de RT-033, las funciones NCalc, los límites y la distribución 60/30/10 de RF-069. Usar cuando el usuario pida añadir, diseñar, revisar o balancear perks, objetos o consumibles.
---

# Crear o revisar un perk, objeto o consumible

Fuentes: `docs/modelo-datos.md` (formato, tipos de efecto, funciones NCalc), `docs/requisitos.md` §3.7-3.9 y §4.4-4.5.

## Antes de escribir

1. Lee el formato vigente en `docs/modelo-datos.md`. No inventes campos: si el efecto que necesitas no existe en el catálogo de `efecto.tipo`, para y propón añadirlo con una línea en `docs/decisiones/README.md`; no lo metas como condición NCalc rebuscada.
2. Comprueba que el id no existe: `ls data/perks data/objetos data/consumibles | grep <id>`.
3. Decide el `tipo` y comprueba la distribución actual del catálogo (RF-069: 60% relleno, 30% condicional, 10% rompe-reglas). Si el tipo que añades ya está por encima de su cuota, dilo.

## Reglas que el validador rechaza

- Campo `descripcion`: las descripciones se generan (RT-035). Nunca lo escribas.
- Referencia a un jugador concreto o a un nombre: los perks consultan **etiquetas** (RF-068).
- `disparador` fuera del catálogo RF-066.
- `limite.por` fuera de `jugada | partido | turba | run`.
- Función NCalc no listada en `docs/modelo-datos.md`.
- `letal: true` sin que el efecto pueda causar `MUERTE`; o un efecto que pueda causarla sin `letal: true` (RF-013, RF-093).

## Reglas de diseño

- Un jugador sano nunca muere (RF-093). Un efecto letal solo actúa sobre un receptor con lesión grave arrastrada, o está marcado `letal` y se destaca en el ojeo.
- Todo lo que el perk haga debe ser previsible desde el informe de ojeo (RF-012d). Si el efecto depende de algo que el jugador no puede ver antes del partido, está mal diseñado.
- Los perks que acumulan entre partidos (`acumulaEntrePartidos: true`) usan `contador('nombre')` y `sumar_contador`; hacen falta al menos 15 en el catálogo (RF-070).
- Objetos: exactamente un arquetipo (`maldito`, `fragil`, `restringido`) o ninguno; un jugador lleva un único objeto (RF-076).
- Consumibles: familia obligatoria; si `esSoborno`, la `tablaResultados` suma 100 y la denuncia existe (RF-064b).
- Escribe el efecto en español, con los valores en enteros. Sin decimales (RT-023).

## Después de escribir

1. `dotnet run --project tools/DataValidator -- data/` debe pasar.
2. Genera la descripción (es y en) y pégala en la respuesta: si no se lee bien, el problema está en la plantilla del tipo de efecto o en el diseño del perk, no se arregla con texto a mano.
3. Si el perk es `condicional` o `rompe_reglas`, añade o actualiza una build en `data/balance/builds/` que lo use y ejecuta la skill `balance-check`.
4. Añade un test en `Sim.Tests/Perks/` que dispare el evento con contexto mínimo y compruebe el efecto y el límite, con semilla fija.

## Plantilla mínima

```json
{
  "id": "",
  "nombre": { "es": "", "en": "" },
  "rareza": "comun",
  "tipo": "relleno",
  "disparador": "",
  "condicion": "",
  "efecto": { "tipo": "", "objetivo": "ejecutor", "duracion": "jugada" },
  "limite": { "por": "partido", "veces": 1 },
  "acumulaEntrePartidos": false,
  "letal": false,
  "soloPosicion": null
}
```
