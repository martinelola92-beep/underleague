# BB-S — `build-neutral-reference.py` dice que escribe las referencias y no escribe nada

**Estado:** Abierta. Detectada al borrar `pack_mentality` (19 sep 2026). No arreglada: hay que decidir si
la herramienta escribe o si se documenta como calculadora.

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
