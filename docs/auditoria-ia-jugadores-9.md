# Novena auditoría — `ChaseBall pen`, revalidado sobre el baseline definitivo: **rechazado**

Cierra la rama abierta en `auditoria-ia-jugadores-5.md`, bloqueada desde entonces a la espera de un baseline
estable. Ese baseline existe desde la ADR 0110. **No se ha aplicado ningún cambio.**

Etiquetas: **OBSERVADO**, **INFERIDO**, **HIPÓTESIS**, **CONFIRMADO**.

## 1. Qué venía pendiente

La auditoría 5 dejó confirmado el diagnóstico causal (**CONFIRMADO**, y sigue siéndolo):

> Con el poseedor rival dentro del rango de entrada, `Tackle` tiene el **mejor contexto** de las cuatro
> acciones legales y aun así pierde, porque `ChaseBall` parte de una base mucho más alta y **no tiene
> precondición** que la invalide. El jugador corre hacia un balón que ya podría disputar.

Y proponía como arreglo una penalización blanda a `ChaseBall`, aplicada **solo** con un poseedor rival al
alcance y **solo** si la entrada es una opción real para ese jugador (sin cooldown), para no volverlo pasivo.
Quedó bloqueada con la advertencia de que **el rango de entrada es una distancia** y la geometría había
cambiado.

## 2. Instrumento y control

`Sim.Tests/Analysis/_A9.cs`, temporal, ya borrado, con **las definiciones exactas de `_A5`** para que las
cifras sean comparables entre vueltas. Conmutador `UL_CHASEPEN` en `Utility.cs`, temporal, ya retirado;
apagado reproduce el baseline byte a byte.

**El cambio de geometría y el de `Shoot` no han tocado esta situación** *(OBSERVADO)*:

| | auditoría 5 | **auditoría 9** |
|---|---|---|
| churn improductivo | 16,5 % | 16,3 % |
| aprovechamiento de oportunidades | 29,2 % | 29,1 % |
| `Tackle` en oportunidad | 20,0 % | 20,2 % |
| `ChaseBall` en oportunidad | 31,8 % | 32,3 % |

Así que el diagnóstico de A5 se mantiene entero sobre el baseline nuevo.

## 3. El barrido

200 partidos por semilla para la decisión, 1.000 para las bandas, dos semillas.

### Corrección de decisión — el criterio principal

| pen | aprovech. s1/s2 | `Tackle` en op. s1/s2 | `ChaseBall` en op. s1/s2 |
|---|---|---|---|
| 0 | 29,1 / 29,9 | 20,2 / 20,7 | 32,3 / 32,3 |
| 25 | 31,5 / 31,7 | 20,9 / 22,4 | 29,0 / 29,5 |
| 50 | 33,3 / 33,0 | 23,5 / 23,6 | 26,9 / 27,0 |
| 60 | 33,6 / 33,5 | 23,8 / 23,9 | 26,2 / 26,0 |
| 75 | 35,3 / 35,3 | 25,0 / 25,5 | 24,5 / 24,3 |

*(OBSERVADO, CONFIRMADO en dos semillas)* **Funciona exactamente como A5 midió**: monótono, con menos de
1,5 puntos de diferencia entre semillas en todos los niveles. La corrección de decisión no está en duda.

### Churn

| pen | improductiva s1/s2 |
|---|---|
| 0 | 16,3 / 15,5 |
| 25 | **19,5** / 16,6 |
| 50 | 17,0 / 16,0 |
| 60 | 16,5 / 15,8 |
| 75 | 14,9 / **16,6** |

*(CONFIRMADO)* Vuelve a salir no monótono y con el signo cambiando entre semillas, igual que en A5. La
métrica sigue sin servir para elegir, y lo vuelve a demostrar sola.

### Guardarraíles de partido

| pen | entradas s1/s2 | lesiones s1/s2 | cadena s1/s2 |
|---|---|---|---|
| 0 | 12,74 / 10,30 | 0,85 / 0,51 | **2,02** / 2,09 |
| 25 | 13,30 / 10,68 | 0,88 / 0,52 | **2,00** ❌ / 2,08 |
| 50 | 13,48 / 10,89 | 0,85 / 0,52 | **2,00** ❌ / 2,08 |
| 60 | 13,53 / 11,17 | 0,87 / 0,54 | **2,00** ❌ / 2,09 |
| 75 | **14,03** ❌ / 11,46 | **0,90** / 0,52 | 2,01 / 2,08 |

La cadena de pases cae al suelo de la banda con **cualquier** dosis: el jugador que deja de perseguir
disputa antes, y una posesión que se corta antes es una cadena más corta. *(INFERIDO)*

## 4. Lo que decide: las 43 puertas

| | baseline | pen=25 | pen=50 | pen=60 |
|---|---|---|---|---|
| **rojas** | **3** | **5** | **7** | **6** |
| `coherentBuildsBeatNone_orc_violence` (≥58) | verde | verde | **57,50** | **56,25** |
| `betterTeamWinRate` (70-90) | verde | verde | **66,27** | **68,67** |
| `buildsWinDifferently_injuries` (≥1,40) | verde | **1,19** | **1,29** | **1,25** |
| `passChainAvgLength` (≥2) | verde | **2,00** | **2,00** | verde |
| `badBuildsLoseToNone_elf_brawler` | 47,71 | **51,04** | 48,12 | 46,46 |

*(OBSERVADO)* **Toda dosis empeora las puertas.** Y no es una puerta aislada con ruido: son **métricas
distintas e independientes de diferenciación de builds** moviéndose a la vez y en la misma dirección.

- `buildsWinDifferently_injuries` pasa de verde a **1,19-1,29** con cualquier penalización. Mide si builds
  distintas producen perfiles de lesión distintos. Si todo el mundo entra más y de forma más uniforme,
  **dejan de diferenciarse**.
- `coherentBuildsBeatNone` cae por debajo de 58 a partir de pen=50 y **sigue bajando** con la dosis
  (57,50 → 56,25): construir bien vale menos.
- `betterTeamWinRate` cae a 66-69. En A5 lo descarté como ruido, y aislado lo sería. Acompañado de las otras
  dos, apunta a lo mismo.

**INFERIDO, con la causa:** más entradas es más disputa, y la disputa es en buena medida una tirada. Subir
la proporción de balones que se resuelven en un duelo sube la **varianza** del partido, y la varianza se come
la diferencia entre una build buena y una mala. El arreglo compra mejores decisiones individuales pagando
con **profundidad de build**, que es el núcleo del juego.

## 5. Decisión: **RECHAZADO**

`ChaseBall pen` queda rechazado en todas las dosis medidas. Motivo, en el vocabulario de A5:
**REJECT — parche que degrada la profundidad**. No es exceso de agresividad (las entradas se quedan dentro
hasta pen=60) ni inestabilidad entre semillas (la corrección replica). Es que **el precio se cobra en otro
sitio**, y ese sitio importa más.

Prioridad declarada del proyecto: *corrección de decisión > identidad del juego > profundidad de
posicionamiento > seguridad de banda*. Aquí la corrección de decisión mejora de verdad, pero la
**profundidad** —que va por delante de la seguridad de banda— se degrada de forma medible y reproducible.
No hay dosis que compre lo primero sin pagar lo segundo.

### Una corrección a la auditoría 5

A5 recomendó `pen=50` como candidato aceptado. Esa recomendación **era prematura**. A5 sí midió las puertas
y vio 7 rojas frente a 6 del baseline, pero atribuí la diferencia a ruido de `betterTeamWinRate` —lo
demostré con un barrido— y no me fijé en que había **otras** métricas de build moviéndose. Sobre un baseline
sucio (6 rojas por el trabajo de siete filas) el daño era difícil de ver; sobre uno de 3 rojas es
inequívoco: **3 → 5 → 7**. *(OBSERVADO)*

La lección, que va a `CLAUDE.md`: **cuando varias métricas independientes se mueven juntas en la misma
dirección, no es ruido aunque cada una por separado lo parezca.**

## 6. Lo que queda vivo del diagnóstico

El diagnóstico causal de A5 **no se retira**: `ChaseBall` sigue sin precondición y sigue ganando a `Tackle`
en una situación en la que no debería. Lo que se retira es **este arreglo**, no el problema.

Caminos que quedan abiertos, ninguno medido *(HIPÓTESIS)*:

- Atacar la **precondición** en vez del peso: que `ChaseBall` deje de ser legal cuando el jugador ya es el
  más cercano **y** el balón está en poder rival al alcance, en lugar de restarle puntos. A5 midió el
  descarte duro y se pasaba, pero no se ha probado una precondición más estrecha.
- Aceptar que el jugador persiga: quizá 20 % de aprovechamiento de entradas **es** el juego que queremos, y
  lo que sobra es la expectativa de que fuera mayor.
- Revisar `tackleDistanceMaxCells` (1,0) en vez de la utilidad: si el rango es generoso, hay «oportunidades»
  que no lo son.

Ninguno se toca aquí.

## 7. Estado del árbol

`Sim/Engine/Utility.cs` restaurado y ausente de `git status`. `data/ai/weights.json` con la ADR 0110
aplicada y sin nada más. `data/sim/` sin tocar. Instrumento `Sim.Tests/Analysis/_A9.cs` borrado. RT-024 en
verde (4/4). Los dos lotes de 1.000 partidos reproducen el baseline de la ADR 0110:

- semilla 1: `shotsPerMatch=7,60 · tacklesPerMatch=12,74 · injuriesPerMatch=0,85 · possessionChanges=20,21 ·
  passChainAvgLength=2,02 · goalsPerMatch=2,38`
- semilla 2: `shotsPerMatch=7,49 · tacklesPerMatch=10,30 · injuriesPerMatch=0,51 · possessionChanges=22,19 ·
  passChainAvgLength=2,09 · goalsPerMatch=2,21`

Las 3 puertas rojas siguen siendo las dos regresiones del trabajo de siete filas (`elf_brawler` y el reparto
de `passChain`) más su agregador.
