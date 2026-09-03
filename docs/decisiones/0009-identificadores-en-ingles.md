# 0009. Identificadores en inglés, documentación en español

**Fecha:** 2026-09-03
**Estado:** Aceptada
**Requisitos:** RT-073 (localización desde el primer día)

## Contexto

El documento de requisitos usa términos españoles (`Puede(estado, accion)`, `INICIO_PARTIDO`, `disparador`). Había que decidir si el código los seguía.

## Decisión

Código C#, claves JSON, ids de datos, eventos y etiquetas en **inglés**. Documentación, comentarios de diseño y commits en español. Todo texto visible por el jugador pasa por `data/l10n/` (es/en); el español es una traducción más, no el idioma del código. La correspondencia está en `docs/glosario-identificadores.md`, que es la única fuente para nombrar conceptos nuevos.

## Alternativas descartadas

- Español en el código: coherente con el documento, pero choca con el framework, las librerías y las herramientas, y complica cualquier colaboración futura.

## Consecuencias

Los documentos derivados citan el término español y, entre paréntesis o en código, el identificador. Los ejemplos de código del documento de requisitos se leen como pseudocódigo.
