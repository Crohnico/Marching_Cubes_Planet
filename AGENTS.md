# Instrucciones para agentes

## Lectura obligatoria

Antes de tocar codigo, arquitectura, runtime, inicializacion, datos, render, memoria, persistencia, editor tooling o comportamiento jugable, hay que leer la documentacion existente en `Docs/`.

Regla:

```text
Leer los documentos:
- El documento general de definicion tecnica.
- El documento especifico del sistema que se vaya a tocar.
- Cualquier documento enlazado desde esos documentos.
```


## Documento estanco

`Docs/Definicion_Tecnica_Proyecto.md` queda estanco.

No se debe modificar salvo que la persona lo pida explicitamente o que haya que revisitar una decision de arquitectura y se indique de forma clara.

## Regla practica

- No introducir reglas de funcionamiento implicitas.
- No asumir decisiones abiertas como cerradas.
- Si una decision sigue abierta, documentarla como `TBD` o pendiente en el documento correspondiente.
- Si un cambio contradice una documentacion existente, no hacer workaround silencioso: actualizar la documentacion en el mismo cambio o pedir confirmacion.
- Cada sistema importante debe tener su documento propio antes de bajar a codigo.

## Enfoque del proyecto

El target principal es Meta Quest 3. Cualquier decision tecnica debe considerar rendimiento, RAM, VRAM, Garbage Collector, streaming, uso de disco, CPU y GPU desde el principio.

La prioridad es construir una base medible, ligera y ampliable antes de implementar gameplay encima.
