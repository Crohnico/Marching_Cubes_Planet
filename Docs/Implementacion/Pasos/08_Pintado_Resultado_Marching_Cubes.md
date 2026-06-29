# 08 - Pintado resultado Marching Cubes

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

## Objetivo

Pintar y mostrar el resultado generado por `07_Marching_Cubes`.

08 parte 1 no optimiza todavia la geometria.

08 parte 1 no decide el presupuesto final de poligonaje.

08 parte 1 no construye BVH.

El objetivo es convertir la salida de 07 en una representacion visual clara, medible y liberable dentro de `PlanetImplementationLab`.

Contrato funcional:

```text
resultado completo de 07 -> Mesh/material de validacion -> render visible -> metricas/release
```

Regla central:

```text
08 no interpreta la formula de 06.
08 no consulta density(point).
08 no ejecuta Marching Cubes.
08 no decide cuantos triangulos se conservan.
08 pinta todos los triangulos validos que 07 expone o falla con diagnostico.
```

## Modelo mental

06 define la masa como campo escalar implicito:

```text
density(point)
```

07 pone una rejilla temporal, consulta las 8 esquinas de cada cubo y genera triangulos reales de Marching Cubes:

```text
campo escalar -> cubos -> caseIndex -> triTable -> triangulos
```

08 parte 1 pinta esos triangulos.

08 parte 1 no puede pintar "los primeros N triangulos" como si eso fuese el planeta.

Si el resultado de 07 no cabe en la Mesh temporal de validacion:

```text
08 bloquea el pintado.
08 reporta capacidad insuficiente.
08 no muestra una malla parcial.
```

09, 10 y 11, en documentos separados, definiran:

```text
pool global fijo de triangulos.
reparto interno de detalle para geometria adaptable.
visibilidad, oclusion y frustum.
```

Regla:

```text
Si el problema es "no se ve lo que genera 07", pertenece a 08 parte 1.
Si el problema es "quien recibe triangulos del millon global", pertenece a 09.
Si el problema es "donde pongo mas/menos detalle dentro de una geometria adaptable", pertenece a 10.
Si el problema es "no deberia haber tris porque no lo veo", pertenece a 11.
```

## Alcance de esta fase

Entra:

```text
Construir Mesh de Unity desde los triangulos generados por 07.
Construir Mesh visual de agua desde las zonas de esa superficie que quedan bajo el nivel de mar.
Asignar material de validacion.
Asignar material de oceano inicial.
Pintar por color plano, normal, altura, caseIndex o dato diagnostico simple.
Mostrar la mesh en PlanetImplementationLab.
Registrar Mesh runtime y Material runtime si aplica.
Medir vertices, triangulos, indices, bytes estimados y tiempos.
Liberar recursos propios.
Botones de Inspector para pintar/liberar.
Pruebas visuales de que 07 produce superficie legible.
```

La fase debe permitir mirar el resultado de 07 sin confundirlo con el sistema final de LOD.

## Fuera de alcance

No entra:

```text
Generar density(point).
Ejecutar Marching Cubes.
Consultar las 8 esquinas de cubo.
Usar edgeTable/triTable.
Generar volumen/simulacion final de agua.
Optimizar por distancia de camara.
BVH.
Culling espacial.
Reparto global de presupuesto.
LOD final.
Chunks locales reales.
Proxy lejano final.
Materiales finales.
Colisiones.
Persistencia.
Terraformado.
```

Regla:

```text
08 parte 1 no descarta triangulos por presupuesto.
Si hay un limite de seguridad de Mesh temporal y el resultado no cabe, se bloquea el pintado con diagnostico.
El presupuesto real queda para 09.
La optimizacion interna de geometria queda para 10.
La visibilidad/occlusion/frustum queda para 11.
```

## Relacion con otros documentos

Documentos base:

```text
Docs/Definicion_Tecnica_Proyecto.md
Docs/Teoria_Implementacion.md
Docs/Implementacion/Pasos_de_Implementacion.md
Docs/Implementacion/Calculo_Funcional_Datos_Planeta.md
Docs/Implementacion/Pasos/01_PlanetImplementationLab.md
Docs/Implementacion/Pasos/02_ComputeShaderLab.md
Docs/Implementacion/Pasos/03_Coordenadas_Y_Receta.md
Docs/Implementacion/Pasos/04_Gestion_RAM_VRAM.md
Docs/Implementacion/Pasos/06_Forma_Planeta_GPU.md
Docs/Implementacion/Pasos/07_Marching_Cubes.md
```

Este documento prepara directamente:

```text
09_Pool_Global_Triangulos
10_Reparto_Geometria_BVH
11_Visibilidad_Oclusion_Frustum
_deadline_06-08
12_Proxy_Planeta_Lejano
```

Dependencias cerradas:

```text
07 produce triangulos reales de Marching Cubes.
07 expone conteos de triangulos/vertices generados.
07 puede exponer vertices no indexados para Mesh visual.
PlanetLabResourceRegistry registra Mesh runtime y Material runtime.
PlanetImplementationLab ejecuta botones, metricas y Release All.
```

Estado del codigo existente antes de implementar 08:

```text
PlanetRecipePayloadPreview ya existe como preview historico de isosfera por payload.
PlanetSpherePayloadMeshBuilder ya existe para isosfera CPU.
Ese preview no es el pintado de Marching Cubes.
08 debe pintar la salida de 07, no una esfera generada aparte.
```

Reglas de integracion:

```text
08 no reutiliza PlanetSpherePayloadMeshBuilder como fuente geometrica.
08 reutiliza la ruta visual ya validada por PlanetRecipePayloadPreview: Mesh runtime + MeshRenderer + material runtime.
08 debe seguir el patron ClaseReal / ClaseRealLab / ClaseRealEditor.
El boton Generate del panel preview usa PlanetRecipePayloadPreview como fuente de receta, posicion/rotacion y payload solicitado.
Los objetos Lab calculan/orquestan, pero no son el ancla espacial del planeta visible.
```

## Datos de entrada

Datos minimos:

```text
Resultado valido de 07.
Vertices generados por 07.
Normales generadas por 07.
triangleCountWritten.
vertexCountWritten.
overflowFlag de 07.
PlanetRecipe.
PlanetPlacement si hace falta convertir Grid -> World.
Material de validacion.
ResourceRegistry.
```

Datos por vertice esperados:

```text
positionGrid.
normalGrid.
caseIndex o dato diagnostico opcional.
```

## Datos de salida

Salida real:

```text
Mesh runtime visible.
Mesh runtime visible de agua si existen triangulos bajo el nivel de mar.
Material runtime o material compartido asignado.
GameObject/ResultView de validacion.
Conteo de triangulos visibles.
Conteo de vertices visibles.
Conteo de triangulos/vertices de agua.
Bytes estimados de Mesh.
Diagnostico visual/metricas.
```

No debe producir:

```text
Nueva density(point).
Nueva triangulacion Marching Cubes.
Nuevo grid global.
Volumen/simulacion final de agua.
BVH.
LOD.
Payload final optimizado.
Colision.
Persistencia.
```

## Politica visual inicial

Decision inicial:

```text
Pintar vertices con atlas de superficie planetaria por defecto.
Usar Mesh runtime de Unity.
Usar vertices no indexados tal como salen de 07.
Generar indices lineales si Unity los necesita.
Generar vertex colors solo para modos diagnosticos.
Generar UV.y por altura normalizada de cada vertice.
Generar UV.x por celda `PlanetGpuShapeCell` mas cercana al triangulo.
Usar un atlas runtime 2D de superficie planetaria: columnas por celda, filas por altura.
Mantener vertex colors como fallback y para modos diagnosticos.
Usar el shader/material MarchingCubesPlanet/Planet/Surface como material por defecto.
El material por defecto no debe ser un material de vertex color.
El material de superficie debe ser opaco.
Durante la validacion 06-08 se usa doble cara para no ocultar triangulos por winding/culling mientras se revisa la orientacion final.
El material asset de mundo inicial es `Resources/PlanetWorld_Surface`, recuperado del antiguo `Planet Height Atlas`.
El material asset de oceano inicial es `Resources/PlanetOcean`, recuperado del antiguo `Ocean`.
La mesh inicial de agua se deriva de la salida de 07: los triangulos bajo `GridRadius` se recortan contra el nivel de mar y se proyectan a `GridRadius`.
08 no reevalua `density(point)` para generar agua.
08 no genera volumen de agua ni simulacion; solo una lamina visual superior para cuencas bajo el nivel de mar.
```

Decision cerrada:

```text
La primera ruta de pintado de 08 es la misma familia tecnica que PlanetRecipePayloadPreview.
No se usa render GPU-resident directo en 08 parte 1.
El render visible inicial es MeshFilter + MeshRenderer + Mesh runtime + material de superficie.
```

Modos de color iniciales:

```text
FlatNormalColor.
HeightColor.
CaseIndexPalette.
TrianglePalette.
SolidDebugColor.
PlanetSurfaceAtlas.
```

Regla:

```text
El modo recomendado para leer el planeta completo es PlanetSurfaceAtlas.
El modo recomendado para ver si la triangulacion existe es TrianglePalette o CaseIndexPalette.
El modo recomendado para ver orientacion es FlatNormalColor.
El modo recomendado para leer forma planetaria es HeightColor.
```

Atlas inicial:

```text
UV.x = 0.5 porque el atlas recuperado es una tira vertical de altura.
UV.y = altura respecto al radio de la receta y al nivel de mar, no min/max de la mesh pintada.
UV.y = 0.5 es el nivel de mar/costa para el atlas actual.
UV.y 0.5..1.0 representa superficie emergida.
UV.y 0.0..0.5 representa superficie bajo el agua.
El tramo emergido usa una compresion del 60% del maximo teorico de altura de la receta para que marron, gris y nieve sean alcanzables visualmente.
Cada triangulo usa una sola coordenada UV calculada en su centro para evitar interpolacion suave entre bandas de altura.
La paleta base se decide por altura, no por flag continente/oceano de la celda.
El gradiente inicial de abajo a arriba es rosa submarino, salmon, rojo/marron oscuro, arena, amarillo palido, verde claro, verde oscuro, marron, gris y blanco nieve.
El material de superficie usa el `PlanetHeightAtlas.png` recuperado de la historia anterior al reinicio del proyecto.
```

No objetivo:

```text
Material final.
Agua final.
Atlas final de biomas.
```

## Capacidad temporal de Mesh

08 parte 1 puede tener una capacidad temporal de seguridad para no construir una Mesh demasiado grande.

Decision inicial:

```text
meshTriangleCapacity = 1000000
```

Regla:

```text
Este limite no es el presupuesto final del juego.
Es un cortafuegos de validacion.
Si 07 produce mas triangulos de los que caben, 08 parte 1 no pinta una malla parcial.
08 debe fallar con diagnostico de capacidad insuficiente.
El pool real de triangulos queda para 09.
El reparto por distancia/camara queda para 10 y 11.
```

## Componentes/scripts previstos

### PlanetMarchingCubesPaintSettings

Dato serializable de pintado.

Responsabilidad:

```text
Guardar modo de color.
Guardar meshTriangleCapacity.
Guardar material de validacion si aplica.
Validar limites.
No contener buffers GPU.
No depender del Lab.
```

### PlanetMarchingCubesPaintResult

Dato de resultado.

Responsabilidad:

```text
Guardar triangleCountSource.
Guardar triangleCountPainted.
Guardar vertexCountPainted.
Guardar waterTriangleCount.
Guardar waterVertexCount.
Guardar meshEstimatedBytes.
Guardar waterMeshEstimatedBytes.
Guardar diagnostico corto.
```

### PlanetMarchingCubesMeshPainter

Sistema real para construir/actualizar Mesh visual.

Responsabilidad:

```text
Recibir salida de 07.
Construir Mesh runtime visible.
Aplicar colores diagnosticos.
Asignar normales.
Generar indices lineales.
Calcular bytes estimados.
No ejecutar Marching Cubes.
No evaluar density(point).
No decidir BVH ni LOD.
```

### PlanetMarchingCubesPaintLab

Modulo aditivo para `PlanetImplementationLab`.

Responsabilidad:

```text
Exponer settings de 08.
Validar que 07 tiene resultado.
Construir Mesh visual.
Mostrar metricas.
Registrar Mesh runtime y Material runtime si aplica.
Liberar recursos propios.
Integrarse con Release All.
```

Regla:

```text
El Lab no implementa el pintado.
El Lab orquesta PlanetMarchingCubesMeshPainter.
Debe heredar de PlanetLabModule.
```

### PlanetMarchingCubesPaintLabEditor

CustomEditor nativo para botones de 08.

Responsabilidad:

```text
Mostrar modo de color.
Mostrar recursos vivos.
Mostrar diagnostico.
Exponer botones de Validate, Paint, Change Color Mode, Capture Metrics y Release.
```

## Flujo funcional

Flujo minimo:

```text
1. Validar que 07 genero triangulos.
2. Validar settings de pintado.
3. Leer vertices/normales/conteos de 07.
4. Validar que la capacidad temporal de Mesh puede pintar todo el resultado.
5. Construir Mesh runtime.
6. Asignar material.
7. Mostrar Mesh en PlanetImplementationLab.
8. Registrar recursos propios.
9. Capturar metricas.
10. Liberar recursos cuando se pida.
```

Regla:

```text
Si 07 se regenera, 08 parte 1 debe invalidar o reconstruir la Mesh visible.
08 parte 1 no debe pintar datos obsoletos como si fueran actuales.
```

## Gestion de RAM

Reglas:

```text
No crear listas nuevas por frame.
No usar LINQ en caminos calientes.
No copiar una malla parcial.
Reutilizar listas/arrays de Mesh si se repinta.
No guardar datos obsoletos tras regenerar 07.
```

Datos CPU esperados:

```text
Settings.
Resultado/diagnostico.
Listas/arrays acotados de vertices/normales/colores/indices.
Mesh runtime.
```

Regla:

```text
El tamaño CPU crece con los triangulos expuestos por 07 para esta validacion, no con el volumen del planeta.
```

## Gestion de VRAM

Recursos previstos:

```text
Mesh runtime de validacion.
Mesh runtime de agua si hay zonas bajo el nivel de mar.
Material runtime solo si se instancia.
```

Estimacion inicial:

```text
vertexBytes = vertexCountPainted * (position + normal + color + uv)
indexBytes = triangleCountPainted * 3 * indexStride
meshEstimatedBytes = vertexBytes + indexBytes
waterMeshEstimatedBytes usa la misma regla con waterVertexCount y waterTriangleCount
```

Valores iniciales:

```text
position = 12 bytes
normal = 12 bytes
color = 4 bytes
uv = 8 bytes
indexStride = 4 si vertexCountPainted > 65535, si no 2
```

Reglas:

```text
Registrar Mesh runtime.
Registrar Mesh runtime de agua si existe.
Registrar Material runtime si se instancia.
No registrar como propios los buffers de 07.
No mantener viva la Mesh anterior al repintar.
```

## Liberacion de recursos

`Release` debe:

```text
Liberar Mesh runtime propia.
Liberar Material runtime propio si existe.
Marcar handles como liberados.
Limpiar referencias internas.
Dejar conteos propios a cero.
Permitir Release doble.
Permitir Paint -> Release -> Paint.
```

Regla:

```text
08 parte 1 no libera recursos cuyo owner sea 07.
```

## Botones de Inspector

Botones esperados:

```text
Validate Paint Setup
Reset Paint Settings
Paint Marching Cubes Result
Cycle Color Mode
Paint FlatNormalColor
Paint HeightColor
Paint CaseIndexPalette
Paint TrianglePalette
Capture Paint Metrics
Release Painted Mesh
Release All
```

Reglas:

```text
Paint exige resultado valido de 07.
Release Painted Mesh no libera 07.
Release All debe dejar recursos propios de 08 parte 1 a cero.
```

## Pruebas manuales

Pruebas minimas:

```text
Validate Paint Setup detecta ausencia de resultado 07.
Paint Marching Cubes Result crea Mesh visible si 07 genero triangulos.
Cycle Color Mode cambia el color sin regenerar density ni Marching Cubes.
TrianglePalette permite ver triangulacion.
HeightColor permite leer silueta/relieve.
Release Painted Mesh libera recursos propios.
Release doble no rompe.
Paint -> Release -> Paint funciona.
Si hay superficie bajo `GridRadius`, Paint crea una mesh de agua con `PlanetOcean`.
Release Painted Mesh libera tambien mesh/material de agua.
Release All deja recursos propios de 08 parte 1 a cero.
```

Pruebas visuales:

```text
La Mesh pintada coincide con la zona extraida por 07.
Las normales no parecen invertidas.
Cambiar seed en 06 y regenerar 07 cambia el resultado pintado.
Si la capacidad temporal de Mesh no alcanza, 08 bloquea el pintado y el diagnostico lo muestra.
```

## Tests automatizados

Tests EditMode esperados:

```text
PlanetMarchingCubesPaintSettings valida meshTriangleCapacity > 0.
PlanetMarchingCubesPaintResult no marca truncado visual porque 08 no pinta parciales.
El calculo de meshEstimatedBytes es correcto.
El modo de color se valida.
Release simulado no deja handles vivos.
08 parte 1 no llama a density(point).
08 parte 1 no depende de edgeTable/triTable.
```

Tests PlayMode esperados:

```text
PlanetMarchingCubesPaintLab existe en PlanetImplementationLab cuando se integre.
Validate Paint Setup no lanza excepcion con resultado 07 valido.
Paint Marching Cubes Result no lanza excepcion.
Release Painted Mesh no lanza excepcion.
Release doble no lanza excepcion.
Paint -> Release -> Paint funciona.
```

Tests condicionados:

```text
Si no hay resultado de 07, el modulo queda unavailable con diagnostico claro.
Si el material de validacion no existe, el modulo da diagnostico claro.
```

## Metricas

Metricas iniciales:

```text
triangleCountSource.
triangleCountPainted.
vertexCountPainted.
waterTriangleCount.
waterVertexCount.
meshTriangleCapacity.
meshEstimatedBytes.
waterMeshEstimatedBytes.
ownedCpuEstimatedBytes.
ownedGpuEstimatedBytes.
liveRuntimeMeshes.
liveRuntimeMaterials.
lastPaintMs.
lastReleaseMs.
colorMode.
lastDiagnostic.
```

Metricas diferidas a 09/10/11:

```text
Triangulos concedidos/reclamados por el pool global.
Triangulos redistribuidos por geometria adaptable.
Triangulos descartados por oclusion/frustum.
Coste de estructura espacial.
Calidad visual por reparto de detalle.
```

## Riesgos

Riesgos principales:

```text
Confundir pintar con optimizar.
Convertir meshTriangleCapacity en presupuesto final.
Usar una isosfera preview en vez de la salida de 07.
Regenerar Marching Cubes al cambiar color.
Mantener Mesh vieja viva al repintar.
Pintar una malla parcial sin diagnostico.
Liberar recursos de 07 desde 08.
```

Mitigaciones:

```text
Frontera clara: 07 extrae, 08 parte 1 pinta, 09 reparte slots, 10 adapta detalle, 11 filtra visibilidad.
meshTriangleCapacity solo como cortafuegos.
Owner de recursos separado.
Diagnostico de capacidad insuficiente obligatorio.
Release probado.
Color modes solo cambian representacion visual.
```

## Decisiones cerradas

```text
No quedan decisiones abiertas para empezar la implementacion de 08 parte 1.
08 parte 1 pinta la salida de 07.
08 parte 1 no optimiza por distancia ni camara.
08 parte 1 usa Mesh runtime de Unity.
08 parte 1 usa material de superficie por defecto.
Los vertex colors quedan reservados a modos de diagnostico.
08 parte 1 usa MeshFilter + MeshRenderer.
08 parte 1 mantiene vertices no indexados.
meshTriangleCapacity es cortafuegos de validacion, no presupuesto final.
08 no pinta parciales.
El budget global de poligonaje queda para 09.
BVH/reparto interno queda para 10.
Oclusion/frustum queda para 11.
```

## Criterio de cierre

Este documento queda listo para implementar cuando aceptemos este contrato:

```text
08 parte 1 pinta triangulos reales generados por 07.
08 parte 1 no genera densidad.
08 parte 1 no ejecuta Marching Cubes.
08 parte 1 no optimiza por distancia/camara.
08 parte 1 registra y libera su Mesh/material.
08 parte 1 mide triangulos pintados, vertices, bytes y tiempos.
09, 10 y 11 quedan reservados para presupuesto global, reparto interno y visibilidad.
```

El cierre real del bloque ocurre en:

```text
_deadline_06-08
```

Ese deadline valida conjuntamente:

```text
Forma GPU.
Marching Cubes.
Pintado del resultado de Marching Cubes.
```
