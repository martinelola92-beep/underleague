# ADR 0146 — `modifyUtility` deja de ser inalcanzable

**Fecha**: 24 sep 2026
**Estado**: aceptada — decisión del revisor (*Gameplay AI Foundations Pass*, bloque 16)
**Paquete**: P14
**Contexto del encargo**: `docs/plan-gameplay-ai-foundations.md`

## Contexto

Era el hallazgo más incómodo de la auditoría de identidad, y no por difícil sino por absurdo:

> `modifyUtility` (C1) está en el motor, pilotada y medida al 24 % con RT-056 en verde, y **no está en el
> enum de `perks.schema.json`**: ningún dato puede usarla.

Es el **único canal del motor que cambia lo que un jugador quiere hacer** —en vez de si le sale— y llevaba
meses construido, probado y **desconectado**. El diagnóstico CB-C del plan de evolución lo señala como la
causa de que los perks no cambien la intención: el 63 % de los efectos mueve una cuota o un contador.

## Decisión

### 1 · El efecto entra en el esquema con sus dos campos

`modifyUtility` se añade al enum de tipos de efecto, con:

- **`utilityAction`** (obligatorio): la acción cuya intención se modifica. Un bono de intención sin decir a
  **qué** intención no significa nada, así que su ausencia es un error de carga y no un perk inerte
  (RT-032).
- **`utilityZone`** (opcional): el tercio del campo en el que cuenta. Sin él, el bono vale siempre; con él
  es la variante con la que se pilotó la primitiva — *«quiere tirar, pero sólo cuando está arriba»*.

Los dos campos se rechazan en cualquier otro tipo de efecto, igual que `markBias` y `tackleBias`.

### 2 · El techo C10 sale de la **tabla real**, no de una lista escrita a mano

El plan lo nombraba así: *«un ×3 a `Shoot` en un portero tiene que ser un error de datos, no una
anécdota»*. Se cumple sin ninguna regla especial para porteros:

> Un `modifyUtility` sobre una acción cuyo **peso base es cero** para el puesto al que el perk está
> restringido es un error de carga: un porcentaje de cero es cero, y la letra muerta en `/data` es un error
> explícito.

Comprobarlo contra `data/ai/weights.json` y no contra una lista es lo que hace que la regla **siga valiendo
cuando la tabla cambie**. Vive donde el catálogo y los pesos están los dos delante, igual que
`ValidateArcs`.

### 3 · Y se puede **describir**, que es lo que lo hace usable de verdad

RT-035 no admite texto de efecto escrito a mano, así que un efecto que no se puede describir no se puede
publicar. Se añaden:

- una sección **`actions`** en `data/l10n/*/templates.json` con el nombre legible de las diecisiete
  acciones —«tirar», «proteger el balón», «buscar la espalda»—;
- **dos plantillas**, porque la cláusula de tercio cambia lo que el jugador tiene que entender: *«quiere
  tirar más»* y *«quiere tirar más en la zona rival»* no son la misma promesa.

Sin esto, el canal habría quedado técnicamente alcanzable y prácticamente inutilizable — que es
exactamente la situación de la que venimos.

## Lo que NO se ha hecho, y es deliberado

**No se ha añadido ningún perk al catálogo.** La ADR 0122 dice que no se toca el catálogo de 102 perks, y
el encargo dice *«no crear rasgos nuevos por crear contenido. Primero hacer que los sistemas existentes
puedan modificar decisiones reales»*. El canal queda **demostrado de extremo a extremo con perks escritos
en el test** —cargan, validan y describen—, que es lo que prueba que funciona sin gastar una decisión de
diseño que le toca al revisor.

## Consecuencias

- La **fase F3 del plan de evolución** («que dos jugadores del mismo puesto no jueguen igual») deja de
  estar bloqueada por una tubería cortada: lo que le queda es decidir el contenido, no arreglar el motor.
- Cualquier perk que use el canal pasa por el techo C10 al cargar, así que el fallo que el plan temía
  —descubrirlo en un lote de balance— se detecta al arrancar.
