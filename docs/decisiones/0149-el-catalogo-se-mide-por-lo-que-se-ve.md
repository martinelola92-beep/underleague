# ADR 0149 — El catálogo se mide por lo que se ve, no sólo por lo que rompe

Fecha: 25 sep 2026 · Estado: **aceptada, decisión del revisor**
Reescribe **RF-069** y sube `docs/requisitos.md` a **v0.10**. Cierra [BK-A](../pendientes/BK-A.md).
Auditorías que la sostienen: [13B](../analisis/perks-auditoria-potencial-visual.md),
[13C](../analisis/perks-auditoria-situaciones.md). Diseño derivado:
[`perks-de-cuota-a-acto.md`](../analisis/perks-de-cuota-a-acto.md).

## Por qué

RF-069 exigía que el **60 %** del catálogo fuera *«modificadores numéricos condicionados»*. El revisor
enunció el producto en dirección contraria:

> No: *«tengo 100 perks y el 60 % modifica estadísticas con condiciones»*. Sino: *«tengo un sistema
> estadístico que permite que 100 perks creen situaciones diferentes durante un partido»*. […] Las
> estadísticas son el soporte que determina si esas situaciones salen bien o mal.

**El catálogo actual no incumplía RF-069: lo cumplía.** *(MEDIDO, censo de los 102 ficheros de
`data/perks/`: **70,6 %** de los perks no producen ni un suceso ni un cambio observable de conducta —son
desplazamientos de probabilidad, atributo o contador—; 14,7 % cambian conducta; 14,7 % producen un suceso.)*
Por eso ninguna auditoría de catálogo podía arreglarlo: **el documento que producía el resultado estaba por
encima del catálogo.**

**El diagnóstico exacto no es que RF-069 estuviera entera mal.** Sus tres filas miden **potencia** —cuánto
rompe un perk las reglas del simulador— y esa escala sigue siendo buena y sigue estando bien capada. Lo que
faltaba era el otro eje: **visibilidad**. Al no existir como requisito, nadie lo comprobaba, y el eje que sí
existía **premiaba explícitamente lo invisible**, porque el efecto barato y el efecto invisible son el mismo
efecto: una suma entera que no necesita orden, ni traza, ni plantilla, ni pantalla.

## Qué se decide

**RF-069 pasa a tener dos distribuciones ortogonales** —visibilidad y potencia— y una regla de composición
entre ellas. Texto normativo completo en `docs/requisitos.md` §RF-069. Resumen:

### a) Visibilidad — el eje nuevo

| grado | qué es | cuota |
|---|---|---|
| **Acto** | Produce un **suceso nombrado**, reconocible en el instante en que ocurre y atribuible a su portador. Lleva **enfriamiento** (RF-069c) | **≥ 50 %** |
| **Conducta** | Cambia de forma observable **lo que el jugador hace**: dónde se coloca, a quién marca, qué decide | ≥ 25 % |
| **Soporte** | Ajusta magnitudes: probabilidad, atributo, escalar, contador | ≤ 25 % |

### b) La regla que hace que las estadísticas sean soporte y no contenido

> **Ningún perk tiene efectos exclusivamente de soporte.** Un efecto de soporte modula el acto o la conducta
> **del mismo perk**.

Es la traducción literal de *«las estadísticas son el soporte que determina si esas situaciones salen bien o
mal»*, y es **comprobable al cargar** (RT-032): un perk cuyos efectos son todos de soporte es un error de
datos, no un perk flojo. Hoy fallarían **72 de 102** *(MEDIDO)*.

### c) Potencia — el eje viejo, con la primera fila corregida

Se conserva 60/30/10. Lo único que cambia es la **descripción** de la primera fila, que era la que
contradecía el producto: *«modificadores numéricos condicionados»* → **«actos y conductas acotados:
ocurren a menudo y cambian poco el partido»**. Un perk de relleno **debe** ser visible; lo que lo hace
relleno es que su acto sea pequeño, no que sea un número.

### d) Enfriamiento

> Un perk de grado **Acto** declara cada cuánto puede repetirse. **Un acto sin enfriamiento no es un acto:
> es una regla nueva.**

### e) La excepción declarada

Los perks que **no tocan el partido** —economía, herencia, experiencia— declaran grado `economy` y quedan
fuera de las cuotas de visibilidad. *(MEDIDO: hoy son 6 de 102: `ad_machine`, `box_office`, `inheritance`,
`life_insurance`, `loan`, `local_idol`.)* Sin esta excepción, el suelo de visibilidad obligaría a convertir
en espectáculo cosas que **deben** ser invisibles porque ocurren entre partidos. Queda anotado, y no es una
conclusión de esta ADR, que un perk de economía **compite por un slot de perk sin usar el partido**: si eso
es correcto es otra decisión.

## Qué NO se decide aquí

- **Las magnitudes.** Decisión explícita del revisor: *«no te ciñas a los números. Ya lo balancearemos
  después»*. Las cuotas de arriba son de **reparto**, no de potencia numérica, y ningún número de
  `tuning`, `weights.json` ni de los ficheros de perk se toca con esta ADR.
- **El catálogo final.** La conversión de los 102 actuales se diseña en
  [`perks-de-cuota-a-acto.md`](../analisis/perks-de-cuota-a-acto.md) y se ejecutará por tandas.
- **Las primitivas de motor** que la conversión necesita (variante de acción y enfriamiento). Se especifican
  en ese mismo documento y cada una traerá su ADR cuando se escriba, porque son tipo de efecto nuevo
  (RT-057).

## Consecuencias

1. **`/data` queda fuera de requisitos el día que esto se implante**: 72 de 102 perks incumplen la regla (b).
   Es intencionado y es el trabajo que la ADR abre. **No se retira ni se reescribe ningún perk con esta
   ADR**; la conversión va por tandas y cada tanda se mide.
2. **El cargador gana dos validaciones** (RT-032): grado declarado por perk, y la regla de composición.
   Mientras el catálogo no esté convertido, la validación entra **en modo aviso** y se endurece a error al
   cerrar la conversión — o la primera validación tiraría el arranque entero.
3. **`perks-design-bible.md` §5.3** ya pedía que cada ficha declarara su registro; ahora además declara su
   grado, y la comprobación que ese epígrafe dejaba «pendiente y hoy imposible» pasa a ser posible.
4. **La hoja de ruta se reordena** (13C §6): la atribución en pantalla deja de ser una mejora de
   presentación y pasa a ser **el requisito que hace verificable el grado Acto**. Un acto que no se puede
   nombrar no se puede declarar acto.
5. **Riesgo asumido, y hay que escribirlo**: un catálogo con ≥ 50 % de actos con enfriamiento es un partido
   con **más sucesos por minuto** que el actual. La gramática de momentos tiene cola de una plaza y
   caducidad de 1,5 s *(MEDIDO, `PresentationDirector`)*: el techo de cuántos actos caben antes de que se
   pisen entre ellos **no está medido**, y es la primera medición de la conversión (13C §9).

## Alternativas descartadas

- **Dejar RF-069 y arreglar el catálogo**: imposible por construcción — el catálogo lo estaba cumpliendo.
- **Borrar el 60/30/10 y sustituirlo por el eje de visibilidad**: perdería el techo del 10 % de
  rompe-reglas, que es lo único que hoy impide que el simulador deje de tener reglas. Los dos ejes miden
  cosas distintas y los dos hacen falta.
- **Prohibir `modifyProbability`**: es tirar el soporte con el contenido. La probabilidad es exactamente lo
  que decide si el cañonazo entra o se va a la grada, y el revisor lo pide así.
