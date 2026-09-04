# 0023. Perks universales por defecto, con un núcleo exclusivo por raza

**Fecha:** 2026-09-04
**Estado:** Aceptada (decisión del revisor)
**Requisitos:** RF-004, RF-004b, RF-024b, RF-031, RF-032, RF-071, RF-110, RF-126

## Contexto

El catálogo de prueba de la fase 1 condiciona muchos perks por raza (`hasTag(owner,'Brute')`, `tagsRequired: ['Fine']`). En un juego de clubes **monoraza** (RF-004) eso no produce ninguna decisión: la condición se cumple siempre o no se cumple nunca, según el club con el que hayas empezado la run. Peor aún, un perk racial puede aparecer como recompensa (RF-071) en una run de otra raza, donde es basura garantizada: el jugador recibe una opción muerta de tres.

La sinergia real, según RF-004b, no viene de la raza sino de las **etiquetas que portan individuos de una misma raza**, que varían entre ellos (RF-024b): dos orcos del mismo club pueden tener perfiles opuestos.

## Decisión

1. **El 90% del catálogo es universal**: sin condiciones de raza. Sus condiciones usan los demás ejes (`docs/perks-ejes.md`): rasgo, posición, alineación, zona de inicio, acumulación, estado del partido, proximidad.
2. **El 10% son perks exclusivos de raza**, con un campo nuevo `race` que es una **restricción de aparición**, no de asignación: un perk con `race: "Orc"` solo entra en el pool de recompensas, mercado y desbloqueos de una run cuyo club sea orco. Es distinto de `tagsRequired`, que restringe a qué jugador puede asignarse.
3. **Rareza alta**: los exclusivos son `rare` o `legendary`. Se encuentran poco y, cuando salen, definen la partida.
4. **Aplicación**: además, exigen la etiqueta racial para surtir efecto, de modo que asignárselos a un **mercenario** de otra raza no funciona. Refuerza RF-111 (el mercenario rompe las sinergias raciales) sin mecánica adicional.
5. **Dos o tres exclusivos por raza, cada uno apuntando a una build distinta de esa raza.** Esto es lo que impide que los exclusivos se conviertan en "gana más": si los tres perks orcos empujan hacia la violencia, la raza colapsa en un solo estilo y se incumple **RF-032**, que exige tres builds viables por raza. Ejemplo de reparto para orcos: uno de violencia, uno de resistencia y bloque, y uno inesperado (juego de pase con jugadores enormes).
6. **Toda run empieza con sabor racial**: el jugador de rareza superior del club inicial (RF-005) arranca con uno de los perks exclusivos de su raza entre sus perks iniciales (RF-023). Así la identidad se percibe desde el primer partido aunque el resto de exclusivos sean difíciles de encontrar.

## Alternativas descartadas

- **Condicionar por raza los perks universales**: el defecto que motiva esta ADR.
- **Sin exclusivos, identidad solo por sesgo de atributos**: las razas se distinguirían por números y no por reglas, y RF-031 pide explícitamente "una regla o afinidad exclusiva" por raza.
- **Muchos exclusivos por raza (8-10)**: multiplica el coste de diseño y balanceo por 9 razas (riesgo ya identificado en §8 del documento de requisitos) y hace que el catálogo universal se quede corto.

## Consecuencias

- El generador de recompensas y el surtido del mercado (fase 2) filtran por `race`. Hasta entonces, `/Balance` debe respetar el filtro al construir builds, o medirá cosas imposibles en el juego real.
- El catálogo de prueba de la fase 1 se rehace: los perks con condición racial pasan a universales (quitando la condición y reajustando el valor) o a exclusivos (subiendo su rareza).
- Las builds "malas" del conjunto de pruebas ya no pueden basarse en poner perks de otra raza, porque el juego no los ofrecería. Su incoherencia debe venir de los otros ejes: colocación que rompe vínculos, perks de acumulación en jugadores que no realizan esa acción, perks de posición mal asignados, o efectos con `elseEffects` negativos.
- Los exclusivos son buen material para los desbloqueos de RF-126 y para el compendio, porque son memorables.
