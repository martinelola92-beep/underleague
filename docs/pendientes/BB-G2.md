# BB-G2 — Riesgo latente encontrado de camino, sin evidencia de que se dispare

**Estado:** Abierta

## Observación

**Riesgo latente encontrado de camino, sin evidencia de que se dispare**

## Análisis / estado actual

**Abierta.** `UpdateContextCaches` nombra perseguidor designado al más cercano al balón **incluyendo al portero**, y `EvaluateChaseBall` descarta al portero si el balón está fuera de su área. Si el portero es el más cercano fuera de su área, se produce un **abrazo mortal**: él no puede perseguir por ser portero, y los diez de campo no pueden por no ser el designado (precondición dura de AW-S). No he encontrado ningún caso real en 40 partidos, así que **no se ha tocado**; se anota para que exista, y el arreglo —excluir al portero de la designación cuando el balón está fuera de su área— es de una línea

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
