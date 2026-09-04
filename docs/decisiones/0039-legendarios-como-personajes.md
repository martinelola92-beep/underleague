# 0039. Los legendarios dejan de generarse: son personajes que se desbloquean

**Fecha:** 2026-09-05
**Estado:** Aceptada (idea del revisor)
**Modifica:** RF-023, RF-125..128, y **RF-127 de forma sustancial**
**Sustituye:** la salvaguarda de la ADR 0027

## Decisión

1. **Las rarezas generables pasan a ser tres**: común, poco común y raro. El legendario **desaparece de la generación aleatoria**.
2. **Cada raza tiene tres legendarios únicos**, diseñados a mano: nombre, retrato, atributos y perks fijos. No se generan, no se compran, no salen en el mercado.
3. **Se desbloquean ganando runs.** Al completar una división se obtiene uno, y la run siguiente —en la división superior, con rivales más duros— **empieza con él en la plantilla**. Al llegar a la división más alta, el jugador arranca con los tres.
4. **Son parodias de futbolistas reales cruzadas con su raza**, y material natural para cromos coleccionables.

## Por qué esto arregla algo, además de añadir

La ADR 0027 fijó que los legendarios deben ser netamente superiores, y para evitar que no encontrar ninguno condenara la run hubo que añadir una salvaguarda obligatoria: *"un equipo sin ningún legendario debe poder ganar al jefe final"*. Era una tensión incómoda: el premio tenía que notarse mucho y a la vez no ser necesario.

**Con esta decisión la tensión desaparece.** El juego base se equilibra entre común, poco común y raro, que es lo único que la generación produce; los legendarios son **progresión meta** y su presencia está garantizada de antemano, así que no hay lotería que compensar. La salvaguarda de la ADR 0027 deja de hacer falta.

Y hay un segundo beneficio: un legendario deja de ser "un jugador con más presupuesto" y pasa a ser **un personaje**. Es infinitamente más memorable perder a alguien con nombre y cara que perder una rareza.

## El conflicto con RF-127, y cómo se resuelve

RF-127 dice: *"No hay progresión meta que otorgue poder puro acumulativo. Los desbloqueos añaden variedad, no ventaja."* Empezar con un legendario **es** ventaja acumulativa, así que el requisito cambia.

La condición para que el cambio no rompa el juego es que **la dificultad de la división suba más de lo que aporta el legendario**. La progresión meta no facilita el juego: lo hace **distinto y más difícil**, y te da una herramienta con la que afrontarlo. Es medible y pasa a ser una métrica obligatoria: la tasa de victoria en la división N con sus legendarios debe ser **igual o menor** que en la división N−1 sin ellos.

## Los legendarios mueren

Un legendario desbloqueado está disponible **al empezar cada run** de esa división, pero dentro de la run es un jugador más: se lesiona, se puede quedar sin tratar y **puede morir** (RF-093). Si muere, se pierde **para esa run**, no para siempre.

Es la decisión que más valor añade: el desgaste de plantilla es el recurso central del juego, y un legendario inmortal sería la única pieza a la que ese recurso no afecta. Perder a un personaje con nombre y cara en el acto 2 es exactamente la historia que el juego quiere producir, y hace que la ceremonia de muerte y el memorial (RF-122) tengan a quién llorar.

## Criterio para los nombres

**El nombre tiene que funcionar aunque no captes la referencia.** Un chiste que solo entiende quien conoce al futbolista deja fuera a media audiencia; el nombre debe sonar bien como nombre de criatura fantástica **y además** guiñar. "Paolo Maldito" funciona como no-muerto aunque no sepas quién es Maldini.

**Riesgo a tener en cuenta**: los nombres deben ser guiños **transformados**, no el nombre real con una letra cambiada. Cuanto más reconocible sea la persona concreta, más conviene deformar apellido y nombre a la vez, y evitar retratos parecidos a la persona real. No es asesoramiento legal, pero es un riesgo conocido del sector y sale más barato tenerlo en cuenta ahora que después.

## Consecuencias

- **RF-023 se reescribe**: tres rarezas generables con sus slots (común 2, poco común 3, raro 4) y los legendarios como categoría aparte, con 4 o 5 slots fijos por diseño.
- **Toda la generación, el mercado y las recompensas** dejan de producir legendarios. `data/balance/builds/*_excellent` se rehace: "muy buena" ya no puede apoyarse en rareza legendaria.
- **RF-128 (divisiones)** gana su recompensa: hoy subir de división solo añade reglas; ahora también da un personaje.
- Los legendarios son contenido de **arte** (retrato, cromo) y de **escritura**, no solo de datos: entran en el presupuesto de la fase 3 en adelante.
- Se necesita una métrica nueva: dificultad neta por división, para comprobar que la ventaja no supera al aumento de exigencia.
