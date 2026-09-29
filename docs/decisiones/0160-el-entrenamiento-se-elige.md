# ADR 0160 — El entrenamiento se elige: pachanga, especialización o cambio de puesto

Fecha: 27 sep 2026 · Estado: **aceptada**. **Decisión del revisor**: el entrenamiento *«es aburrido»*; de
la primera propuesta acepta la pachanga y el cambio de puesto y pide *«poder subir mucho una sola
estadística de un solo jugador»* (`docs/plan-diversion.md` §2).
**Cumple RF-026** («experiencia **dirigida** al jugador que el usuario elija»), que el código no cumplía.
**Enmienda RF-022b** (la posición deja de ser fija, sólo por esta vía).
**Requisitos:** RF-022, RF-022b, RF-026, RF-027, RT-031
**Relacionada:** ADR 0100 (la carta que se elige, de la que toma la forma), ADR 0159 (el efecto `attribute`,
compartido)

## Lo que había

`ServiceNodeSystem.Training` suma `economy.trainingExperience` (40) a **todos** los disponibles al entrar,
sin preguntar nada. No hay decisión, así que no hay juego.

## Decisión

El entrenamiento pasa a ser un nodo **abierto** (como la clínica y el evento) con **tres sesiones**
derivadas del nodo (flujo de recompensas, no se guardan):

| sesión | efecto | coste |
|---|---|---|
| **Pachanga** (siempre) | la experiencia de hoy para todos los disponibles, como antes | ninguno: es la opción segura |
| **Especialización: <atributo>** (dos, con atributos distintos) | un jugador señalado gana **+8** en ese atributo, permanente y hasta 99 | nadie gana experiencia |
| **Cambio de puesto** (sustituye a una especialización en uno de cada tres nodos) | el señalado pasa a otra posición de campo que el jugador elige | pierde un nivel |

- **+8, provisional, sin medir.** La referencia es `progression.attributesPerLevel`: la especialización
  tiene que valer claramente más que un nivel en ese atributo, o no compensa (la objeción del revisor a la
  «doble sesión»).
- **Pasa del techo del nivel**: el bonus es permanente y la subida de nivel suma encima (`LevelUp` es
  incremental), así que también vale para un jugador de nivel alto.
- **El portero no cambia de puesto** ni nadie pasa a portero: el mercado garantiza exactamente uno (ADR
  0080) y la cuadrícula lo trata aparte.
- El texto de cada sesión se compone de plantillas (RT-035).

## Las diez preguntas (`game-design-review`)

1. **Qué ve el jugador.** Tres sesiones con nombre; elige una y a quién.
2. **Qué decide.** Repartir (pachanga) o fabricar un especialista; recolocar a un jugador que no rinde en
   su puesto.
3. **Qué debería decidir.** Lo mismo. Si la especialización siempre gana a la pachanga o al revés, se mide y
   se ajusta el +8.
4. **Regla.** RF-026 cumplida; RF-022b enmendada.
5. **Sistemas.** `/Sim`: `ServiceNodeSystem.Training` → un `TrainingSystem` con carta y decisión
   (`ChooseTrainingSession`), el nodo se abre en `StandardRunSystems.OpenNode`, `RunPolicy` elige. `/Game`:
   `NodeScreen` (el entrenamiento se abre como el evento), `MapScreen` (sin enseñar antes de entrar).
6. **Alternativas.** Doble sesión, el maestro, el descanso y el ensayo de vínculo: descartados por el revisor.
7. **Trade-off.** La especialización cuesta la experiencia del resto; el cambio de puesto, un nivel.
8. **Estrategias.** Jugadores con identidad («el orco más rápido»), y una salida para el jugador bueno
   atrapado en el puesto que no toca.
9. **Degeneración.** Apilar especializaciones en un solo atributo de un jugador: el tope es 99 y el número de
   nodos de entrenamiento. Se mide el atributo máximo alcanzado por run.
10. **Cómo se demuestra.** Tests: la carta es estable (W-12), la pachanga equivale al entrenamiento de antes,
    la especialización persiste tras una subida de nivel, el cambio de puesto quita un nivel y no toca
    porteros. Lote de campaña con la política eligiendo.

## Revisión independiente (29 sep 2026)

**Corregido:**
- **Una sesión se elige una vez**: antes tres pachangas seguidas daban 3×40 de experiencia y diez
  especializaciones llevaban un atributo a 99. Elegir marca el nodo resuelto y una segunda elección lanza.
- **Cambio de puesto**: mantiene las etiquetas coherentes con la posición (como `RunLineup.Repositioned`),
  no ofrece la posición actual, no deja cambiar a una posición donde un perk del jugador no vale, no
  admite jugadores de nivel 1 y cuesta un nivel de verdad (`LevelLoss`, arreglado el desplazamiento de
  `Progression.MinExperienceForLevel`).
- **Especialización**: fuerza, velocidad, técnica y resistencia, sin la correa (disciplina, no nivel,
  RF-027); nadie ya en 99 es candidato; reparto de atributos por nodo sin el sesgo anterior.
- **Política**: pachanga y especialización se comparan en puntos de atributo equivalentes, con un factor de
  concentración provisional; el cambio de puesto se usa en uno de cada N nodos si alguien juega fuera de su
  posición. Antes siempre especializaba.

**Anotado sin corregir:** la cifra +8 y el factor de concentración siguen provisionales, sin lote de
campaña.
