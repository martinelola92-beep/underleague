# BB-J — «Mentalidad de manada» no sirve jugando con enanos

**Estado:** Abierta

## Observación

**«Mentalidad de manada» no sirve jugando con enanos**, porque no habrá brutos. «Igual hay que tener en cuenta los estilos más que la raza»

## Análisis / estado actual

**Confirmado, y con número.** El perk ya usa **estilo**, no raza (`teammatesWithTag(owner,'Brute') > 2`), pero los enanos son **75 % Bulwark y solo 8 % Brute**: juntar tres brutos es casi imposible. La solución elegante es que cuente **la etiqueta del propio portador** («cuantos más como yo»), que es lo que dice la ficha de Manada del catálogo — pero **hoy no existe** esa función de condición: `teammatesWithTag` exige etiqueta literal

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
