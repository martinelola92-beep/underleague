# 0027. Los legendarios son mejores: común de nivel 8 ≈ legendario de nivel 2

**Fecha:** 2026-09-04
**Estado:** Aceptada (decisión del revisor). **Modifica RF-024 y matiza RF-023b: exige subir `requisitos.md` de versión.**
**Requisitos afectados:** RF-023, RF-023b, RF-024, RF-001c, y el riesgo "espiral de muerte" del §8

## Contexto

El documento v0.9 dice en **RF-024**: *"Un jugador común de nivel máximo con buenos perks debe poder superar en rendimiento a un legendario de nivel bajo"*, y en **RF-023b**: *"Un jugador común que sobrevive toda la run debe seguir siendo competitivo ante el jefe final. Las decisiones tempranas no caducan por diseño."*

El revisor plantea lo contrario: en una run tocan **uno o dos legendarios como mucho**, así que tienen que notarse. Su criterio: el techo de un común de nivel 8 debe equivaler a un legendario de **nivel 2**.

El argumento es sólido: un premio que aparece dos veces por partida y no se nota no es un premio, y la rareza deja de significar nada.

## Decisión

**Se adopta el criterio del revisor**: el legendario es claramente superior en atributos, y un común de nivel máximo alcanza aproximadamente a un legendario de nivel 2. La métrica de `/Balance` cambia en consecuencia: un común de nivel 8 debe quedar entre el 45% y el 55% frente a un legendario de nivel 2 **en igualdad de perks**, y perder con claridad contra uno de nivel alto.

**Con dos contrapesos**, porque el requisito que se modifica protegía un riesgo real:

1. **La ventaja del legendario es de fábrica; la del común es de mérito.** El legendario nace mejor (presupuesto de atributos y slots de perk, RF-023). El común solo compite si el jugador **lo cuida**: sobrevive, sube de nivel, acumula contadores y forma vínculos. Así el legendario se nota al recibirlo y el común se gana su sitio jugando, que es la fantasía de un roguelite de desgaste.
2. **Los perks acumulativos y los vínculos son el canal del común**, no los atributos. Si un común de nivel 8 con dos perks acumulados no puede pelear con un legendario recién fichado, el jugador que invirtió en él siente que perdió el tiempo.

## Riesgo que hay que vigilar

Es el riesgo **"espiral de muerte: run perdida sin saberlo"** del §8, y esta decisión lo agrava. El jefe final tiene plantilla **íntegramente legendaria** (RF-001c). Si los legendarios son netamente superiores y no te ha tocado ninguno, la run puede volverse invencible sin que el jugador lo sepa a tiempo.

Mitigaciones ya previstas en el documento, que pasan a ser **obligatorias** y no opcionales: canteranos gratuitos en el mercado con experiencia acelerada (RF-114b/c), reroll de recompensas (RF-071b), venta de jugadores (RF-114f) y abandono con logros conservados (RF-007). A ellas se añade una comprobación nueva en `/Balance`: **un equipo sin ningún legendario debe poder ganar al jefe final** con una tasa razonable si el jugador ha jugado bien. Si esa métrica no se cumple, la decisión hay que revisarla.

## Consecuencias

- `requisitos.md` debe subir a v0.9.1 con RF-024 reescrito y RF-023b matizado. Hasta entonces, esta ADR es la referencia y queda anotado en `docs/pendientes.md`.
- La métrica de RF-024 en `/Balance` y en la puerta estadística cambia de umbral.
- El sesgo de rareza en atributos de la ADR 0025 deja de ser "pequeño y acotado": pasa a ser el mecanismo principal de diferenciación entre rarezas, junto con los slots de perk.
