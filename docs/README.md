# Documentación de Underleague

| Documento | Qué contiene |
|---|---|
| [requisitos.md](requisitos.md) | Documento de requisitos funcionales y técnicos v0.9. **Fuente de verdad.** No se edita a mano salvo para subir de versión; los cambios de diseño se registran primero en `pendientes.md` o en un ADR |
| [arquitectura.md](arquitectura.md) | Proyectos de la solución, reglas de dependencia, superficie pública de `/Sim`, bus de eventos, carga de datos, persistencia |
| [determinismo.md](determinismo.md) | Cómo se garantiza "misma semilla, mismo binario, mismo resultado" |
| [modelo-datos.md](modelo-datos.md) | Esquema versionado del estado de la run y formato de los ficheros de `/data` |
| [simulacion.md](simulacion.md) | Las tres capas de comportamiento, la IA de utilidad, portero, árbitro y turba |
| [balance.md](balance.md) | Métricas objetivo, herramienta `/Balance`, puertas de CI |
| [plan-fases.md](plan-fases.md) | Fases 0-4 con criterios de salida y estado actual |
| [pendientes.md](pendientes.md) | Decisiones abiertas e inconsistencias del documento de requisitos |
| [decisiones/](decisiones/README.md) | Registro de decisiones de arquitectura (ADR) |
| [entorno.md](entorno.md) | Puesta a punto de la máquina (WSL + Windows) |
| [fase0-diseno.md](fase0-diseno.md) | Especificación de implementación de la fase 0: tipos, firmas, fórmulas, paquetes de trabajo |
| [estilo-descripciones.md](estilo-descripciones.md) | Cómo se redacta lo que el jugador lee: una frase, efecto observable, sin implementación; convención de porcentajes |
| [analisis-formulas-roguelike.md](analisis-formulas-roguelike.md) | Comparación de fórmulas con Brogue, DCSS, Shattered PD y CDDA, y propuesta de ajuste |
| [curva-de-dificultad.md](curva-de-dificultad.md) | Investigación sobre dificultad, azar y retención en roguelikes, aplicada a Underleague |
| [referencia-rune-dice.md](referencia-rune-dice.md) | Comparación con Rune Dice: qué coincide, qué falta y qué reorienta |
| [perks-ejes.md](perks-ejes.md) | Taxonomía de ejes de activación de los perks y distribución objetivo del catálogo |
| [fase2-diseno.md](fase2-diseno.md) | Especificación de la fase 2: bucle de run completo sin gráficos |
| [fase1b-diseno.md](fase1b-diseno.md) | Especificación de implementación del bloque de rediseño: contrato de datos, motor y paquetes |
| [balance/fase1b-resultados.md](balance/fase1b-resultados.md) | Medición de cierre del bloque de rediseño espacial (paquete U): RT-056 antes y después, criterio de salida de fase 1, valor marginal por atributo, campaña, rareza y jefe final, y las conclusiones de diseño. **Línea base de balance vigente** |
| [balance/fase1-perks.md](balance/fase1-perks.md) | Medición del motor anterior al rediseño. Superada; se conserva por sus hallazgos y por el apéndice §12 |
| [rediseno-espacial.md](rediseno-espacial.md) | Plan del rediseño espacial: cuerpos con volumen, roles derivados de la colocación y adyacencia por pares (ADR 0020-0022) |
| [fase1-diseno.md](fase1-diseno.md) | Especificación de la fase 1: motor de efectos, formato de perk, descripciones generadas, progresión, catálogo de prueba, builds y modos de simulación |
| [glosario-identificadores.md](glosario-identificadores.md) | Correspondencia español -> identificador en código. Única fuente para nombrar conceptos |

Convención de referencias: los identificadores `RF-xxx`, `RT-xxx`, `RA-xxx` y `UI-xxx` apuntan siempre a `requisitos.md`. Los documentos derivados **no repiten** los requisitos: los organizan, los concretan y registran lo que el documento original deja abierto.
