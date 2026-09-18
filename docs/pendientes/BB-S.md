# BB-S — `build-neutral-reference.py` dice que escribe las referencias y no escribe nada

**Estado:** Arreglada a medias (19 sep 2026): la herramienta ya puede escribir, tras `--write`, y el
docstring dice la verdad. **Queda abierto un desajuste de reparto por slot** que salió al probarlo, y que
es más interesante que el defecto original.

## Observación

`tools/build-neutral-reference.py` empieza con este docstring:

> *"La regla vive aquí y no en la cabeza de nadie: si el catálogo de perks o la tabla de valores cambian,
> se vuelve a ejecutar y las referencias se recalculan solas. […] **Escribe**
> `data/balance/builds/<raza>_neutral.json` y apunta `baselineByRace` de `data/balance/groups.json` a
> ellas."*

Su `__main__` hace exactamente esto y nada más:

```python
median, chosen = select()
print(f'mediana del catálogo elegible: ...')
for pid, value, family in chosen:
    print(f'  {pid:24} {value:5}  {family}')
```

**No abre ningún fichero para escribir.** Ni las cinco builds, ni `groups.json`. Es una calculadora que
imprime; el docstring describe un generador.

## Cómo se detectó

Al borrar `pack_mentality`, que estaba en las cinco referencias `*_neutral`, se ejecutó la herramienta
esperando que las regenerase. Imprimió la selección nueva —correcta: sustituye `pack_mentality` por
`bruised_knuckles`, todo lo demás igual— y los cinco ficheros **quedaron intactos**, todavía con el perk
borrado dentro. Se detectó porque `grep -c pack_mentality data/balance/builds/*_neutral.json` seguía
devolviendo 1 en los cinco.

La sustitución acabó aplicándose a mano, copiando la salida de la herramienta, que es exactamente lo que
el docstring dice que no debería hacer falta.

## Por qué importa

La promesa del docstring —"la regla vive aquí y no en la cabeza de nadie"— es justo lo que NO se cumple:
hoy la regla vive en el script y el RESULTADO vive a mano en cinco JSON que nadie garantiza que estén
sincronizados con ella. Si alguien cambia `perk-values.json` y ejecuta la herramienta, verá una selección
distinta de la que hay en disco y no se enterará de que no se ha aplicado.

## Cuarto caso del mismo patrón en un día

Es la misma forma de fallo que:

- **BB-Q** — `steamroller`: el `_doc` dice "quedó en el suelo", el dato dice `Injured || Dead`.
- **`shadow`** (`docs/analisis/informe-decision-catalogo.md`) — el `_doc` promete altura relativa al
  vinculado, el efecto es absoluto.
- **BB-R** — `blood_tithe` y `first_touch_school` dicen ser maestros y declaran `requires: null`.

Los cuatro salieron mirando datos y código, ninguno jugando. **El texto de diseño y lo que el proyecto
hace de verdad divergen con frecuencia suficiente como para que merezca una pasada sistemática**, no
cuatro arreglos sueltos.

## Qué hay que decidir

1. **Que escriba**, como dice su docstring: cinco builds + `baselineByRace` de `groups.json`. Ojo: eso
   convierte cualquier cambio de `perk-values.json` en un movimiento automático de la línea base de las
   43 puertas, que es potente y peligroso a la vez.
2. **O que no lo diga**: corregir el docstring a "imprime la selección; aplícala a mano", que es la
   verdad de hoy y deja el control en manos de quien mide.

No se toca hasta que se decida.


## 19 sep 2026 — arreglo, y lo que el arreglo destapa

**Arreglo**: la herramienta acepta `--write`. Sin la bandera **solo imprime**, como hacía; con ella
reescribe las cinco referencias. El docstring ya describe lo que el código hace.

Escribir va detrás de una bandera **a propósito**, no por comodidad: esas cinco builds son la línea base
contra la que miden las 43 puertas, así que reescribirlas mueve el baseline de todo el balance. Eso se
hace mirando, no de pasada. La herramienta lo recuerda por pantalla al terminar.

## El desajuste que salió al probarlo

Al ejecutar `--write` sobre el árbol actual, **cambian 25 líneas en los cinco ficheros**. Comprobado: el
**conjunto de 14 perks es idéntico** en los cinco; lo que cambia es **a qué slot va cada uno**.

- La aplicación a mano (la de siempre, y la que se hizo hoy al borrar `pack_mentality`) sustituye el perk
  **conservando su slot**.
- La herramienta reparte la selección en **su propio orden** (el de `select()`: por distancia a la
  mediana, desempatado por id).

O sea que los ficheros en disco llevaban ya tiempo desviados del orden que la regla produce, porque cada
edición histórica se hizo a mano. **El slot importa**: decide qué jugador lleva el perk, y eso cambia el
partido.

**Decisión (19 sep 2026): NO se adopta el orden de la herramienta.** Se revierte y se conserva el reparto
con el que se midió todo lo que hay medido —CAT-J, la ADR 0118, las bandas de RT-056—. Adoptar el orden
nuevo movería la línea base de las 43 puertas por un desempate arbitrario, sin ninguna decisión de diseño
detrás. Eso es justo lo que la bandera `--write` existe para no hacer de pasada.

**Lo que queda por decidir**: si la regla canónica es "el orden que produce `select()`" —y entonces hay que
adoptarlo de una vez, midiendo el salto de las puertas y anotándolo— o si el reparto por slot es
información independiente de la regla y la herramienta no debería tocarlo. Hasta entonces, `--write` está
disponible pero **no es idempotente contra el árbol actual**, y eso hay que saberlo antes de usarlo.
