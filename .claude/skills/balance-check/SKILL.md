---
name: balance-check
description: Ejecutar el lote de /Balance y contrastar los resultados con los rangos de RT-056 y las métricas obligatorias de docs/balance.md. Usar tras cualquier cambio en /Sim o /data, al cerrar una fase, o cuando el usuario pregunte si algo está balanceado.
---

# Comprobar balance

Fuente: `docs/balance.md`. Los rangos que hay allí son los vigentes; no uses otros.

## Procedimiento

1. Asegúrate de que compila y pasan los tests: `dotnet build Underleague.sln && dotnet test Sim.Tests`. Si el test estadístico de 1.000 partidos falla, ya tienes la respuesta; no sigas hasta entenderlo.
2. Lanza el lote de referencia con la **misma semilla base** que la última medición registrada (búscala en el último commit que tocó `Balance/out/` o en `docs/balance.md`):
   ```bash
   dotnet run --project Balance -c Release -- --runs 10000 --seed 1 --teams data/balance/referencia.json --out out/$(date +%F)/
   ```
3. Lee `out/<fecha>/resumen.csv`. Presenta una tabla con: métrica, valor, rango, DENTRO/FUERA, y la diferencia respecto a la medición anterior si existe.
4. Comprueba el tiempo total: si supera 60 s para 10.000 partidos, es un hallazgo (RT-051).
5. Para cada build catalogada, tasa de victoria contra la referencia: cualquier valor > 70% o < 30% rompe RT-055 y bloquea el commit.
6. Si se tocó el catálogo de perks, incluye la distribución relleno/condicional/rompe-reglas (RF-069) y el número de perks con `acumulaEntrePartidos` (RF-070, >= 15).

## Interpretación

- Una métrica fuera de rango tras un cambio en `/data` -> se ajusta el dato, se vuelve a medir. Nunca se ajusta el rango.
- Si crees que el rango está mal: escribe un ADR con los datos (RT-057). Cambiar un rango sin ADR es un ajuste silencioso y está prohibido.
- Métricas de sensación de fútbol interrelacionadas: más entradas suele bajar la longitud de cadena de pases y subir lesiones. Cambia un dato cada vez y mide.
- No des por bueno un lote con menos de 1.000 partidos por configuración: la varianza engaña.

## Qué entregar

Tabla resumen, lista de métricas fuera de rango con la causa probable (qué dato cambió), y la recomendación concreta (qué valor tocar y en qué fichero). Si todo está dentro, dilo en una línea y adjunta el tiempo de ejecución.
