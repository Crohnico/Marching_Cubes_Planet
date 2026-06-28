# 08-2 - BVH y presupuesto de poligonaje

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

## Objetivo

Reservar la segunda parte del paso 08 para optimizar el resultado pintado de Marching Cubes en funcion de:

```text
distancia a camara.
tamaño aparente.
presupuesto de poligonaje.
prioridad visual.
BVH o estructura espacial equivalente.
```

Este documento queda como continuacion futura de:

```text
Docs/Implementacion/Pasos/08_Pintado_Resultado_Marching_Cubes.md
```

## Frontera con 08 parte 1

08 parte 1:

```text
Pinta lo que sale de 07.
Construye Mesh de validacion.
Mide triangulos/vertices/bytes.
No optimiza.
No decide LOD final.
```

08-2:

```text
Organiza triangulos espacialmente.
Decide que conservar segun camara y presupuesto.
Construye o prueba BVH.
Prepara degradacion visual controlada.
Empieza a pensar en reparto de poligonaje real.
```

Regla:

```text
No implementar 08-2 antes de que 08 parte 1 pinte correctamente el resultado de 07.
```

## Alcance previsto

Entrara:

```text
BVH inicial o estructura espacial equivalente.
Presupuesto de triangulos por vista/estado.
Seleccion por distancia de camara.
Seleccion por tamaño aparente.
Conteos de triangulos conservados/descartados.
Degradacion controlada.
Stress de presupuestos bajos/medios/altos.
Metricas de coste de construccion y consulta.
```

No entrara:

```text
Reimplementar Marching Cubes.
Reimplementar density(point).
Corregir triangulos mal generados por 07.
Material final.
Colisiones.
Persistencia.
Terraformado.
```

## TBD

Decisiones abiertas:

```text
Tipo exacto de BVH.
Si el BVH vive en CPU, GPU o ruta hibrida.
Unidad final de presupuesto.
Criterio de prioridad visual.
Relacion con estados de planeta.
Relacion con chunks locales.
```

Decision cerrada:

```text
08-2 queda fuera del 08 parte 1.
```
