# 0051. Perks maestros y profundidad nativa

**Fecha:** 2026-09-05
**Estado:** Aceptada
**Requisitos:** RF-023, RF-032, RF-069, RF-071, RF-114
**Origen:** análisis de DRL (DoomRL) y Angband, sobre la petición de mejorar la progresión de build

## De dónde sale

**DRL** resuelve la progresión de personaje con un sistema que nuestro catálogo no tiene: subir de nivel **no da nada innato**, solo un punto que se invierte en un *trait*; los ***master traits* exigen tres traits previos** para poder elegirse; y —lo más interesante— **bloquean otros traits**: tomar `Ammochain` cierra `Eagle Eye` para siempre.

**Angband** genera objetos con una **tabla de asignación por profundidad**: cada objeto tiene un valor de "commonness" (una daga 30, una espada de caos 1) y una profundidad nativa, con posibilidad ocasional de aparecer *fuera de profundidad* como sorpresa. En su versión 3.5 rebalancearon la curva para **aplanarla**: más objetos buenos pronto, proporcionalmente menos tarde.

## Lo que nos falta

**Nuestros 45 perks son todos independientes entre sí.** Cualquiera se puede coger en cualquier momento, ninguno exige nada y ninguno cierra nada. Consecuencias:

- **No hay arcos de build.** El jugador no persigue un objetivo a lo largo de la run: acumula piezas sueltas. Lo medimos sin entenderlo: construir bien resultó ser sobre todo **rechazar** lo que no encaja, porque no hay nada hacia lo que construir.
- **No hay compromiso.** Sin exclusiones, una build "coherente" es la que suma más, no la que elige. Dos builds distintas se diferencian por qué les tocó, no por qué decidieron.
- **El surtido no mejora con la run.** El pool pondera por valor (ADR 0038) pero no por acto: un perk que define una build puede salir en el primer nodo o no salir nunca, y eso no cambia entre el acto 1 y el 3.

## Decisión

### 1. Perks maestros: exigen y excluyen

Una capa nueva y **pequeña** sobre el catálogo: unos pocos **perks maestros** por familia, que

- **exigen** llevar ya dos o tres perks de su línea (`requiresPerks`), y
- **excluyen** perks o familias concretas (`blocksPerks`), de forma permanente en esa run.

Son el objetivo hacia el que se construye: cuando aparece uno en la recompensa o el mercado, el jugador sabe si su build lo puede sostener. Y su exclusión es lo que hace que comprometerse tenga precio.

**No sustituyen al catálogo**: son entre el 5% y el 10% de los perks, del orden de tres o cuatro por familia grande. Si crecen más, el catálogo se vuelve un árbol de talentos y deja de ser un roguelite de piezas sueltas.

### 2. Profundidad nativa por acto

Cada perk y cada objeto declara el **acto en el que empieza a aparecer** y una **frecuencia** (el "commonness" de Angband, que ya tenemos a medias en el peso por valor de la ADR 0038). Un perk maestro no sale en el acto 1; uno de relleno sale sobre todo pronto.

Con un margen deliberado: **una probabilidad pequeña de aparición fuera de acto**, para que encontrar algo de acto 3 en el acto 1 sea un momento memorable en vez de imposible. Es la sorpresa que Angband llama *out of depth*, y es barata de implementar.

Y la lección de su rebalanceo de la 3.5: **aplanar**. Que lo bueno aparezca antes de lo que la intuición pide; la escasez tardía se administra sola porque los slots se llenan.

## Lo que esto arregla, en nuestros propios términos

- Da **algo que perseguir** dentro de la run, que es justo lo que el revisor pide al decir que la build es el núcleo del juego.
- Hace que dos builds de la misma raza **se excluyan** entre sí, que es lo que RF-032 exige (tres builds viables y **distintas** por raza) y hoy no se cumple por diseño, sino por qué perks tocan.
- Da al mercado un papel que el trampolín le quitó: si te falta la tercera pieza de una línea para desbloquear su maestro, **la buscas y la pagas**.

## Riesgos

- **Bloqueos frustrantes.** Un perk que cierra una línea debe decirlo **antes** de aceptarlo, con la misma claridad que un perk letal se destaca en el ojeo (RF-013, RF-012d). Y como los perks no se pueden retirar (RF-072), un bloqueo es permanente: la advertencia no es opcional.
- **Builds ladrillo.** Si los maestros son muy fuertes, toda run converge a conseguir uno. La medición que lo vigila ya existe: ninguna build catalogada por encima del 70% (RT-055).
- **Complejidad**: es una capa más sobre perks, objetos, consumibles, habilidades raciales y vínculos. Por eso se acota al 5-10% del catálogo y se mide antes de crecer.

## Nota de Sil-Q, que confirma otra decisión

Sil resuelve cada acción con `1d10 + habilidad` contra `1d10 + dificultad`: **una tirada por lado**, no una contra un umbral. Es exactamente la P2 de la ADR 0050 (dos tiradas promediadas) y da la misma distribución triangular.

Su comunidad advierte además de algo que nos toca directamente: *"las mecánicas simples y elegantes pueden escalar de forma torpe"*, porque la brecha entre la habilidad del jugador y la del rival **no se ensancha linealmente** a lo largo de la partida. Nuestras fórmulas ya son relativas (ADR 0041), pero conviene medir el valor marginal de un atributo **por acto**, no solo en global: si en el acto 3 un punto vale el triple que en el 1, el balance del catálogo está mal repartido aunque el promedio parezca correcto.
