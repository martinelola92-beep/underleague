# BI-C — Las animaciones se llevaban al modelo fuera de su anillo (root motion)

Hermana de [BI-B](./BI-B.md): misma pantalla, misma sesión y el mismo tipo de causa —un dato que viaja
bien y se usa mal en el último metro—.

Estado: **CERRADA** (23 sep 2026). Reportada por el revisor jugando: *«estas animaciones no se producen en
el sitio, por lo que cuando le das la orden de correr, el modelo va hacia adelante y sale de su
aro/aura/anillo y cuando decide hacer otra cosa el modelo se teletransporta otra vez a su anillo»*.

## Causa

Los clips de Mixamo traen el desplazamiento **horneado en el hueso raíz** (`mixamorig_Hips`): es *root
motion*, la animación mueve al personaje. En Underleague eso está mal **por construcción** —la posición de
cada jugador la manda `/Sim` tick a tick y el render no mueve a nadie (RT-014)—, así que el clip y la traza
se peleaban por la misma cosa: el clip empujaba al modelo y el siguiente `ApplyTrace` lo devolvía.

## Medido, no estimado

Deriva horizontal máxima del hueso raíz por clip, en metros del modelo y en casillas del campo (escala
0,501 = alto de la raza humana 0,907 ÷ alto natural del personaje 1,809):

| clip | metros | **casillas** |
|---|---|---|
| `trip` (trompicón) | 4,26 | **2,14** |
| `tackle` (entrada) | 4,03 | **2,02** |
| `gk_save` (estirada) | 3,38 | 1,69 |
| `run` (carrera) | 3,06 | **1,53** |
| `jog` (trote) | 2,15 | 1,08 |
| `idle` (espera) | 0,25 | 0,13 |

Corriendo se salía **una casilla y media** de su anillo, y hasta las esperas creían un poco. Catorce de
los quince clips tenían deriva.

## Arreglado

`PlayerModel.PinInPlace` fija a su valor inicial el desplazamiento **horizontal** del hueso raíz al cargar
la biblioteca y **conserva el vertical**, que es el balanceo al correr y el bajar al suelo del trompicón
—quitarlo dejaría un muñeco deslizándose—. Equivale a la casilla «In Place» de Mixamo, aplicada al cargar
**para no depender de cómo se descargó cada fichero**. Cada clip corregido deja su línea en consola con la
deriva que tenía, así que si mañana entra un pack nuevo se ve en el arranque.

De paso, las posturas pasan a **fundirse en 0,15 s** en vez de cortarse en seco: pasar de correr a chutar
saltaba en un fotograma.

## Lo que esto enseña para el siguiente pack

**Cualquier clip de una biblioteca genérica es sospechoso de traer root motion**, y el síntoma —el modelo
se va y vuelve— parece un fallo de interpolación o de la cámara y no lo es. La comprobación cuesta una
línea de consola y ya está puesta.
