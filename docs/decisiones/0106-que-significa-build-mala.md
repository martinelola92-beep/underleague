# ADR 0106 · Qué significa «build mala» cuando ningún perk es negativo

**Fecha:** 13 de septiembre de 2026 · **Estado:** aceptada, **pendiente de medir** · **Decisión del revisor**
**Cierra:** CAT-E · **Toca:** `data/balance/builds/*_incoherent.json`, `human_scattered.json`, RT-055

## El problema, en una frase

Desde la **ADR 0088** —«ningún perk es negativo», la rama `else` no hace nada— **una build mala no puede
ser mala, solo neutra**, y el criterio de fase 1 «las malas pierden, ≤ 45 %» se volvió **insatisfacible por
construcción** sin que nadie lo notara.

Las builds «malas» del catálogo estaban hechas de **catorce perks que dependen de `linked()` colocados para
que ninguno resuelva**. Eso era malo cuando un perk que no cumplía su condición aplicaba `elseEffects`
negativos: catorce maluses. Desde la ADR 0088 son catorce perks que **no hacen nada**, que es otra forma de
decir «una plantilla normal». El campo de seis filas (ADR 0103) no creó el problema: movió un número que
llevaba desde el paquete AY apoyado en el borde.

## Dos decisiones del revisor

**1. Una plantilla con cero perks no existe.** Las recompensas **solo** dan perks desde la ADR 0055
(`rewardItemWeight` 0), así que toda run acumula perks quiera o no. Medirse contra `<raza>_none` era medirse
contra un estado **inalcanzable del juego** — lo que D-34 llevaba años diciendo y la ADR 0104 formalizó al
poner `<raza>_neutral` de referencia.

**2. Mala es «sin sentido, sin sinergias, con objetos mal colocados».**

## La definición nueva

Una build mala lleva **los mismos catorce perks que la referencia neutra** —se disparan, pero no se suman—
y se diferencia de ella en **una sola cosa medible**: los **objetos malditos en el portador equivocado**.

Formación 2-3-1, slot 0 portero · 1-2 centrales · 3-5 medios · 6 delantero:

| slot | rol | objeto | qué le resta |
|---|---|---|---|
| 0 | portero | `brutes_pauldron` | **−20 velocidad**, y la necesita para cubrir |
| 1, 2 | centrales | `martyrs_relic` | **−20 fuerza**, y es con lo que entran |
| 3, 4, 5 | medios | `berserker_totem` | **−20 técnica**, y es con lo que pasan |
| 6 | delantero | `brutes_pauldron` | **−20 velocidad** |

Es **exactamente la jugada de `human_excellent` al revés**: allí el `berserker_totem` (−20 técnica) va a
centrales, que no la necesitan. Aquí cada maldito cae sobre el atributo que su portador más usa.

**Por qué los objetos y no los perks.** Un maldito resta **atributos**, no efectos con rama `else`, así que
la ADR 0088 no lo toca. Desde aquella decisión, **el equipamiento mal colocado es la única forma que queda
de construir en contra**. Eso convierte la puerta de fase 1 en algo que antes no medía: *¿cuesta equipar
mal?* — y de paso le da trabajo a la ADR 0036, que puso los objetos a valer.

## Lo bueno del diseño: aísla una variable

Neutra y mala llevan **los mismos perks, la misma formación y la misma calidad**. La única diferencia son
los cuatro objetos. Así que el número que salga **no se puede confundir** con el valor del catálogo de
perks, que es justo el vicio que D-34 denunciaba.

`*_incoherent` lleva además su alineación incoherente y `human_scattered` la dispersa, de modo que las dos
acumulan mala colocación de **jugadores** sobre mala colocación de **equipo**.

## Medición, en tres iteraciones, y las dos primeras fallaron

| | `badBuildsLoseToNone_human_scattered` | techo |
|---|---|---|
| definición vieja (14 perks inertes) | 59,58 | 45 |
| **1.** neutra sin objetos contra mala con malditos | **66,25** | 45 |
| **2.** mismos objetos en las dos, permutados | **66,67** | 45 |
| **3.** + alineación amontonada en vez de dispersa | **dentro** | 45 |

**Por qué falló la primera.** Un objeto maldito da **+20 en tres atributos y −20 en uno**: es un **buff
neto**. Comparar «neutra sin objetos» contra «mala con objetos» medía la **presencia** del objeto, no su
colocación, y la build mala ganaba por llevar equipo.

**Por qué falló la segunda, que es el hallazgo.** Corregido con un experimento controlado —el **mismo
multiconjunto** de objetos en las dos builds, dos `brutes_pauldron`, tres `berserker_totem` y dos
`martyrs_relic`, solo que permutados, de modo que los totales de atributo son idénticos y la única variable
es a quién se le da— el número **no se movió**: 66,67. Es decir: **colocar mal el equipamiento no cuesta
nada medible**. Las cinco `*_incoherent` sí pasaron a perder con esta definición, así que el problema era
solo `human_scattered`.

**Lo que de verdad la hacía ganar.** Su alineación. Con cinco filas, repartir gente a las filas 0 y 4 era
irse a los dos bordes; con **seis filas** (ADR 0103) ese mismo reparto es **abrir el campo a lo ancho**, que
es jugar bien. La ADR 0103 convirtió una colocación mala en una buena sin que nadie lo notara. La mala
colocación en un campo más ancho es la contraria: **amontonarse** —siete jugadores apiñados en el cuarto
defensivo, estorbándose, sin anchura y sin nadie arriba—. Con esa alineación, **las 43 puertas en verde**.

**Consecuencia que hay que dejar escrita:** de las tres cosas que el revisor nombró como «build mala» —sin
sentido, sin sinergias, con objetos mal colocados—, la tercera **no se mide**. Los perks inertes tampoco,
desde la ADR 0088. Lo único que hoy separa una build mala de una normal es **dónde pones a los jugadores**.
Si se quiere que equipar mal cueste, hay que hacer que el objeto maldito reste más de lo que suma, y eso es
tocar la ADR 0036, con su ADR y su medición.

