# 0056. Objetivos de separación entre perfiles de jugador

**Fecha:** 2026-09-05
**Estado:** Aceptada (directriz del revisor). **Tras la ADR 0058 siguen sin alcanzarse, y el paquete mide
por qué los dos primeros no pueden alcanzarse a la vez** (`fase2-diseno.md` §27.6): "buena al 60%" exige un
rival ordinario más débil y "mediocre al 42-45%" lo exige más fuerte, y la capa de build del rival es un
solo número que mueve a las dos en el mismo sentido. Medido: buena 52,4/42,1, mediocre 46,1/34,4, la mala
completa la run el 9,92% y el hueco del acto 2 pasa de 6,81 a 6,28 puntos. Hace falta una palanca que
aumente el **recorrido del catálogo** entre construir bien y construir regular; la que se probó —que la
rareza compre cuota— no lo consigue porque las dos builds llevan casi la misma mezcla de rarezas (AK-B).
*(Estado anterior)* **Los cuatro objetivos seguían sin alcanzarse tras la P1** y dos empeoran (`fase2-diseno.md` §26.7): buena 56,8/53,0 (meta 60), mediocre 50,0/45,1 (meta 42-45), mala completa la run el 14,34% (meta <2%), buena la completa el 18,00%. El hueco entre buena y mediocre en el acto 2 se estrecha de 9,8 a 6,8 puntos. La causa es la misma que falsifica la ADR 0057 y está en AJ-B
**Requisitos:** RT-055, RT-056, RF-032
**Depende de:** ADR 0054 (banda revisada) · **implementa** las P1 y P3 de la ADR 0050

## El problema, medido

Sobre 900 runs, partidos **ordinarios** (perder uno no termina la run, RF-002c):

| Perfil | Acto 1 | Acto 2 | Acto 3 | Run completada |
|---|---|---|---|---|
| Mediocre | 74,4% | 50,6% | 47,1% | **12,7%** |
| Buena | 77,5% | 53,2% | 50,5% | 19-22% |

**La separación en partidos es de tres puntos.** Construir bien casi no se nota partido a partido: se nota solo al acumularse en las puertas. Y una build mediocre completa la run una de cada ocho veces, que es demasiado para algo que no debería llegar.

## Objetivos

| Métrica | Hoy | Objetivo |
|---|---|---|
| Build **buena**, victoria en partidos de los actos 2 y 3 | 50-53% | **60%** |
| Build **mediocre**, victoria en partidos de los actos 2 y 3 | 50,6% / 47,1% | **claramente por debajo**, en torno al 42-45% |
| Build **mala**, completar la run | 12,7% | **menos del 2%** |
| Build buena, completar la run | 19-22% | **sin cambio**: 20-30% |

El motivo del primero, en palabras del revisor: *"es frustrante tenerlo todo planeado y no ganar"*. Con 60% en partidos, una build bien construida **domina el partido a partido** aunque la run siga siendo difícil — la tensión se traslada a las puertas, que es donde debe estar.

Y el último no cambia a propósito: la run sigue ganándose entre el 20% y el 30%, que es la banda coherente con la curva de la ADR 0033 y con lo que gana Slay the Spire de media sobre 240 millones de sesiones. **Lo que sube es cuánto se nota construir bien, no cuánto se gana.**

## Cómo: las dos correcciones que quedaban

No hace falta inventar nada. Las dos piezas pendientes de la ADR 0050 son exactamente palancas de "cuánto pesa la habilidad frente al azar":

- **P1, perks multiplicativos sobre cuotas.** Hoy un perk suma puntos porcentuales y su efecto depende de la base del canal; con cuotas, un perk vale lo mismo en cualquier canal y **el conjunto de la build pesa más y de forma predecible**. Es la palanca principal.
- **P3, curva de nivel más agresiva.** Del +22% actual entre el nivel 1 y el 8 al +39%. Premia sobrevivir y cuidar la plantilla, que es parte de construir bien.

La ADR 0054 ya subió la banda de `betterTeamWinRate` a 70-88 precisamente porque estas dos la habrían roto por hacer justo lo que se pretende. **Ese bloqueo ya está levantado.**

Para el objetivo de la build mala hace falta además que **los jefes de los actos 2 y 3 castiguen la falta de sinergia**, no solo la falta de piezas: sus modificadores ya invalidan ejes de construcción (ADR 0033), y una build sin línea clara debería quedarse sin respuesta ante ellos.

## Lo que hay que vigilar

- **La curva de puertas de la ADR 0033 no puede romperse**: hoy están las doce celdas en banda sin margen. Si al subir el peso de la habilidad se salen por arriba, se recalibran los jefes, nunca la tabla.
- **P1 y P3 no se aplican a la vez** (ADR 0050): juntas harían imposible atribuir un desajuste a su causa.
- Si `betterTeamWinRate` supera **88**, la habilidad domina y el azar deja de dar partidos: esa es la señal de que se ha ido demasiado lejos.
