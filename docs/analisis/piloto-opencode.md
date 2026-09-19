# Piloto: OpenCode como ejecutor

Pregunta: ¿delegar la ejecución en OpenCode (modelos gratuitos) reduce el consumo de Claude sin subir las
vueltas de corrección frente a `fast-worker`? Skill: `opencode-worker`. Decisión tras 3–5 encargos reales:
extender a `/Sim`, quedarse en `/data`/tests/docs, o abandonar.

## Preparación (19 sep 2026)

- OpenCode 1.18.31 en WSL (`~/.npm-global/bin/opencode`), sin credenciales: solo modelos gratuitos de Zen.
- `AGENTS.md` tiene precedencia sobre `CLAUDE.md` para OpenCode — CONFIRMED (fichero de prueba con ambos).
- `opencode.json`: las reglas `deny` de bash y `external_directory` se aplican sin interfaz — CONFIRMED.
- El proceso puede no salir tras escribir el informe — CONFIRMED; el script espera la línea `NOTAS:`.
- Cinco ejecuciones en paralelo se colgaron sin salida; dos funcionan — causa no aislada (LIKELY: límite de
  concurrencia del plan gratuito).
- Prueba de humo en el repo: encargo con una tentación fuera de alcance (`README.md`); el ejecutor la rechazó
  citando `AGENTS.md`. 16 s de principio a fin.

## Consultas de documentación (19 sep 2026)

`tools/opencode-consulta.sh`, agente `lector`. Tres preguntas de prueba, `big-pickle`:

| Pregunta | Tiempo | Citas | Resultado |
|---|---|---|---|
| RF-012d y las cinco condiciones de la ADR 0048 | 16 s | 4 ✓, 2 ✗ | las ✗ eran elisiones con "…", no invenciones; el verificador las admite desde entonces |
| Tamaño de plantilla y multijugador | 32 s | 6 ✓ | correcta; señaló por su cuenta la tensión entre la Copa en Steam y "sin online" |
| ADR de cooperativo local (no existe) | 20 s | 2 ✓ | "No consta", con la búsqueda que hizo; no inventó |

## Registro de encargos

| Fecha | Encargo | Ámbito | Modelo | Tiempo | Veredicto del script | Vueltas | Defectos hallados en revisión | Comentario |
|---|---|---|---|---|---|---|---|---|
