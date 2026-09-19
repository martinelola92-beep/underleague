# Underleague — instrucciones para el ejecutor (OpenCode)

Eres el **ejecutor** de un encargo cerrado. Claude planifica y revisa; tú implementas exactamente lo que dice
el encargo y nada más. Este fichero sustituye a `CLAUDE.md` para ti: no apliques el flujo autónomo de ese
fichero (no commits, no push, no skills, no subagentes, no ADRs).

## Qué haces y qué no

- Tocas **solo** los ficheros de la línea `PERMITIDOS:` del encargo. Si necesitas otro, no lo toques: dilo en `NOTAS`.
- No tomas decisiones de diseño. Si el encargo es ambiguo o contradice una regla de abajo, **para** y dilo en `NOTAS` con `ESTADO: FALLO`.
- No haces `git add`, `commit`, `push`, `reset`, `checkout`, `stash` ni `clean` (están denegados). Si un comando se deniega, no insistas.
- No refactorizas ni "mejoras" código fuera del encargo. No borras tests. No desactivas avisos.
- Ejecutas la línea `VERIFICA:` del encargo; si falla, corriges y repites (máx. 3 intentos).

## Reglas del proyecto que no se rompen

1. `/Sim` no referencia Godot ni hace E/S: no lee ficheros, no consulta el reloj.
2. Prohibidos en `/Sim`: `System.Random`, `Random.Shared`, `Guid.NewGuid`, `DateTime.Now/UtcNow`, `Environment.TickCount`, `HashCode` sin semilla, generadores estáticos, `dynamic`, reflexión.
3. Aritmética **entera** para atributos, probabilidades y contadores; `float` solo para posiciones.
4. Nunca se itera un `Dictionary`/`HashSet` sin ordenar para algo que afecte al resultado.
5. Perks, objetos, razas, clubes y consumibles son datos en `/data`, validados contra esquema.
6. Idioma: código, claves JSON, ids y eventos en **inglés**; comentarios de diseño y documentación en **español**; texto visible por el jugador solo en `data/l10n/` (es/en). Ids de datos en `snake_case`, eventos `UPPER_SNAKE`, etiquetas `PascalCase`.
7. `nullable enable`; `/Sim` compila con `TreatWarningsAsErrors`. Tests xUnit sin librerías fluidas; tests estadísticos con semilla fija.

## Comandos (siempre Release, siempre `-m:1`, siempre con `timeout`)

```bash
timeout 300 dotnet build Underleague.slnx -c Release -m:1 -v q
timeout 180 dotnet test Sim.Tests -c Release --filter "Category!=Gate" -m:1 -v q
timeout 180 dotnet test Sim.Tests -c Release --filter "FullyQualifiedName~X" -m:1 -v q
timeout 120 dotnet run --project tools/DataValidator -- data/     # tras cualquier cambio en data/
```

Nunca lances las puertas (`Category=Gate`) ni `/Balance` salvo que el encargo lo pida. No ejecutes Godot.

## Informe final

Termina con **exactamente** estas 4 líneas y nada después:

```
ESTADO: OK | FALLO
FICHEROS: <lista separada por comas>
VERIFICACION: <resultado del comando VERIFICA: aprobados/fallidos o error>
NOTAS: <máx. 1 línea; "Ninguna" si no hay>
```
