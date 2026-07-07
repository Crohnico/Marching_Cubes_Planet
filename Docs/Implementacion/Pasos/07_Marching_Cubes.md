# 07 - Marching Cubes

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

## Objetivo

Convertir el campo de densidad definido en `06_Forma_Planeta_GPU` en triangulos reales mediante Marching Cubes sobre un grid cartesiano bruto.

Este paso no busca una version optimizada para Quest 3.

Este paso busca la verdad geometrica inicial del planeta:

```text
density(point) -> grid cartesiano 1x1x1 -> Marching Cubes -> triangulos brutos
```

Contrato funcional:

```text
06 define density(point).
07 coloca chunks cartesianos de 64x64x64 celdas sobre el grid del planeta.
07 evalua las 8 esquinas reales de cada celda.
07 genera triangulos reales de Marching Cubes.
08 pinta esos triangulos.
```

Regla central:

```text
07 no reimplementa density(point).
07 incluye el HLSL compartido de 06.
07 no usa cubemap, esfera parametrica, cáscara radial ni proxy angular.
07 trabaja con celdas cartesianas reales de 1x1x1 en GridCoordinates.
07 no contiene reglas especiales para Voronoi, altura, ruido, biomas, cuevas ni terraformado.
```

Contrato invariable:

```text
for each cell 1x1x1:
    sample density(point) en sus 8 esquinas
    corner solid = density > 0
    construir caseIndex
    resolver triangulos con edgeTable/triTable
```

Regla de depuracion:

```text
07 debe funcionar igual con density esfera pura que con la formula completa de 06.
Si density esfera pura no cierra una mesh, 07/08 estan mal.
Si density esfera pura cierra y la formula completa no, 07 no se cambia salvo que falten chunks candidatos.
```

## Modelo mental

06 no crea una nube volumetrica persistente del planeta.

06 define una funcion:

```text
density(point)
```

Esa funcion responde, para cualquier punto del grid local del planeta:

```text
positivo -> dentro/solido
cero     -> superficie
negativo -> fuera/aire
```

07 no debe inventar otra representacion del planeta.

07 hace lo mas literal:

```text
1. Toma un chunk cartesiano.
2. Recorre sus celdas 1x1x1.
3. Evalua density(point) en las 8 esquinas de cada celda.
4. Construye caseIndex.
5. Usa edgeTable/triTable.
6. Emite triangulos.
```

La rejilla de 07 no es el resultado visual.

La rejilla de 07 es la herramienta de muestreo.

El resultado de 07 son triangulos reales de superficie, interpolados en las aristas donde el campo cruza el isoLevel.

08 no decide como unir los 8 vertices de cada celda.

Eso lo decide Marching Cubes en 07 mediante `triTable`.

08 solo decide como pintar los triangulos ya generados:

```text
como se convierten en Mesh visible.
que material/color diagnostico usan.
cuantos se pintan como cortafuegos de validacion.
como se miden sus vertices/triangulos/bytes.
como se liberan sus recursos visuales.
```

10 y 11 empiezan despues.

```text
10 = reparto interno de detalle, cache y publicacion de meshes por chunk.
11 = visibilidad, oclusion y frustum.
```

Regla importante:

```text
No meter en 07 una optimizacion que pertenece a 10 u 11.
No usar 07 para hacer una version ligera del planeta.
No usar 07 para resolver presupuesto de triangulos.
```

## Alcance de esta fase

Entra:

```text
Marching Cubes sobre GPU.
Tabla estandar de 256 casos.
Muestreo de density(point) desde el HLSL de 06.
Chunks cartesianos de 64x64x64 celdas.
Cell size fijo de 1x1x1 en GridCoordinates.
Seleccion inicial de chunks que intersectan la banda posible de superficie.
Extraccion de triangulos reales de Marching Cubes.
Normales geometricas iniciales.
Buffer GPU de vertices no indexados.
Readback acotado para que 08 pueda construir una Mesh de Unity.
Resultado visual no final.
Metricas de chunks, celdas, triangulos, vertices, overflow y memoria.
Registro y liberacion de recursos.
Botones de Lab para generar/liberar la superficie.
```

La fase debe validar visualmente por primera vez que la forma de 06 produce una superficie con grid real.

## Fuera de alcance

No entra:

```text
Cubemap extractor.
Cáscara radial.
Proxy angular.
Payload final de triangulos.
Asignacion de presupuesto global.
LOD final.
BVH.
Frustum/occlusion.
Chunks jugables finales.
Streaming.
Deduplicacion avanzada de vertices.
Persistencia.
Colisiones.
Materiales finales.
Cuevas.
Minerales/sustancias.
Terraformado.
```

Regla:

```text
Si una decision trata de pintar triangulos generados, pertenece a 08.
Si una decision trata de repartir detalle dentro de una geometria adaptable, pertenece a 10.
Si una decision trata de no gastar tris en lo que no se ve, pertenece a 11.
Si una decision trata de una mesh low-res final del planeta entero, pertenece a 12.
Si una decision trata de chunks jugables alrededor del player, pertenece a 14.
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
Docs/Implementacion/Pasos/05_Quest3_Player_Setup.md
Docs/Implementacion/Pasos/06_Forma_Planeta_GPU.md
```

Este documento prepara directamente:

```text
08_Pintado_Resultado_Marching_Cubes
10_Optimizacion_Adaptativa_Poligonaje
11_Visibilidad_Oclusion_Frustum
_deadline_06-08
12_Proxy_Planeta_Lejano
14_Chunks_Locales
```

Dependencias cerradas:

```text
PlanetRecipe aporta GridRadius, WorldScale, Seed, IsoLevel, VoronoiDivision y ContinentCells.
PlanetGpuShapeEvaluator aporta parametros/celdas GPU vivos.
PlanetShapeDensity.hlsl aporta density(point).
PlanetResourceRegistry registra recursos grandes.
PlanetImplementationLab ejecuta botones, metricas y Release All.
```

## Regla de grid heredado

07 no define otro tamaño logico de grid.

```text
Micro cell logica = 1x1x1 en GridCoordinates.
GridRadius viene de PlanetRecipe.
GridDiameter = GridRadius * 2.
Con GridRadius = 1000, el diametro logico del planeta es 2000 cells.
```

Marching Cubes trabaja sobre cubos reales de una micro cell logica:

```text
cubeSizeGrid = 1
```

Regla:

```text
07 decide que chunks de ese grid se muestrean.
07 no cambia la resolucion logica del planeta.
07 no reparametriza el grid a cubemap.
```

## Chunks cartesianos

Decision cerrada:

```text
ChunkSize = 64
```

Significado:

```text
Cada chunk contiene 64x64x64 celdas Marching Cubes.
Cada celda mide 1x1x1 en GridCoordinates.
Cada chunk cubre 64x64x64 unidades de grid.
Cada chunk necesita muestras de esquina en una reticula de 65x65x65 puntos.
```

Coordenadas:

```text
chunkCoord = int3(cx, cy, cz)
chunkOriginGrid = chunkCoord * 64
localCell = int3(x, y, z), con x/y/z en 0..63
cellOriginGrid = chunkOriginGrid + localCell
```

Esquinas:

```text
samplePosition = cellOriginGrid + cornerOffset
```

Regla:

```text
Las posiciones de muestra son posiciones cartesianas reales.
No se normalizan para construir una cáscara.
No se derivan de faceIndex/u/v/radialIndex.
```

## Seleccion inicial de chunks

07 bruto no significa recorrer todo el cubo global siempre.

07 bruto significa que, cuando un chunk se procesa, se procesa con grid cartesiano real.

Para evitar evaluar un volumen entero imposible, la seleccion inicial de chunks puede usar una banda aproximada de superficie.

Rango radial de superficie posible:

```text
maxOutwardOffset =
    GridRadius * MaxLandElevation * MaxHeightModifier
    + GridRadius * MountainBiomeHeight
    + GridRadius * SurfaceNoiseAmplitude
    + max(0, -IsoLevel)

maxInwardOffset =
    max(GridRadius * OceanDepth, GridRadius * MinimumOceanDepth) * MaxHeightModifier
    + GridRadius * SurfaceNoiseAmplitude
    + max(0, IsoLevel)

innerRadius = GridRadius - maxInwardOffset - safetyMargin
outerRadius = GridRadius + maxOutwardOffset + safetyMargin
```

Nota:

```text
SurfaceNoiseOctaves, SurfaceNoiseLacunarity y SurfaceNoisePersistence cambian el detalle interno del ruido de 06.
No amplian por si solas la banda radial de 07 porque el fBm de 06 se normaliza antes de aplicar SurfaceNoiseAmplitude.
MountainBiomeCells, MountainBiomeMinPeaks, MountainBiomeMaxPeaks, MountainBiomePeakRadius, MountainBiomePeakSpread, MountainBiomeEdgeBlend y MountainBiomePeakFalloff cambian donde y como aparecen las montanas.
No amplian por si solos la banda radial de 07 porque la cota maxima la pone MountainBiomeHeight.
```

Un chunk se considera candidato si su AABB cartesiano puede intersectar la cascara:

```text
chunkAabbMin = chunkOriginGrid
chunkAabbMax = chunkOriginGrid + int3(64, 64, 64)

minDistanceToAabb <= outerRadius
maxDistanceToAabb >= innerRadius
```

Regla:

```text
Esta seleccion no simplifica el grid.
Solo evita lanzar chunks que no pueden contener superficie.
La seleccion puede ser conservadora.
Si incluye chunks de mas, solo aumenta coste.
Si excluye chunks que contienen superficie, es bug de 07.
```

Para pruebas iniciales:

```text
Se permite bajar GridRadius en la receta, por ejemplo 500u, para validar el pipeline bruto.
No se baja la resolucion de celda.
No se sustituye el grid cartesiano por otro sistema.
```

## Escala y coste esperado

Este paso puede producir muchos triangulos.

Eso es intencionado.

Ejemplo:

```text
GridRadius = 1000
WorldScale = 4
diametro visual = 8000 metros
```

Con pocos cientos de miles de triangulos, el planeta completo seria un proxy.

Para 06-07-08 se acepta que la salida bruta pueda estar en millones de triangulos.

Valores de validacion esperados:

```text
temporaryOutputTriangleCapacity inicial alto para PC/editor.
10M triangulos es aceptable como objetivo bruto de validacion si la maquina lo soporta.
Overflow no es fallo de Marching Cubes; es capacidad temporal insuficiente del buffer de salida de 07 para esa prueba.
```

Regla:

```text
El limite de triangulos de 07 es capacidad temporal de extraccion, no presupuesto de poligonaje.
Si no cabe la extraccion completa, 07 no debe exponer una malla parcial como resultado valido.
07 debe reportar overflow/capacidad insuficiente.
La politica de calidad empieza en 10/11.
```

## Decision de extraccion inicial

La extraccion inicial corre en GPU.

```text
CPU prepara lista de chunks candidatos.
CPU sube chunk origins o despacha por chunk/lote.
GPU evalua density(point).
GPU calcula mascaras de ocupacion.
GPU aplica Marching Cubes.
GPU escribe vertices no indexados.
CPU hace readback acotado para que 08 construya una Mesh de Unity.
```

Motivo:

```text
La funcion density(point) vive en GPU.
Evita crear una ruta CPU paralela de forma.
Evita duplicar el algoritmo de forma.
Prepara la ruta real de chunks.
Mantiene el readback como herramienta de Lab, no como contrato final.
```

Regla:

```text
No implementar Marching Cubes CPU como ruta alternativa de validacion.
Tests CPU solo pueden comprobar tablas, indices, bounds y formulas pequeñas sin sustituir la ruta GPU.
```

## Ocupacion de celda

Cada celda tiene 8 esquinas.

Orden inicial de esquinas:

```text
0 = (0, 0, 0)
1 = (1, 0, 0)
2 = (1, 1, 0)
3 = (0, 1, 0)
4 = (0, 0, 1)
5 = (1, 0, 1)
6 = (1, 1, 1)
7 = (0, 1, 1)
```

Mascara:

```text
cornerMask = 0
si density(cornerN) > 0:
    cornerMask |= 1 << N
```

Con:

```text
PlanetShapeEvaluateDensity ya devuelve el campo firmado con PlanetRecipe.IsoLevel aplicado.
density > 0  -> solido
density <= 0 -> aire
```

Casos triviales:

```text
cornerMask == 0   -> todo aire, no escribe triangulos.
cornerMask == 255 -> todo solido, no escribe triangulos.
```

## Orden de aristas

Orden inicial de aristas:

```text
0  = 0-1
1  = 1-2
2  = 2-3
3  = 3-0
4  = 4-5
5  = 5-6
6  = 6-7
7  = 7-4
8  = 0-4
9  = 1-5
10 = 2-6
11 = 3-7
```

Regla:

```text
El orden de esquinas, aristas, edgeTable y triTable debe coincidir exactamente.
```

## Interpolacion de vertices

Cuando una arista cruza el cero del campo firmado:

```text
t = valueA / (valueA - valueB)
t = saturate(t)
vertex = lerp(positionA, positionB, t)
```

Caso degenerado:

```text
Si abs(valueB - valueA) es casi cero, usar t = 0.5.
```

Regla:

```text
La posicion de vertex queda en GridCoordinates.
La conversion a WorldSpace se hace al construir/mostrar la Mesh visual.
```

## Tablas de Marching Cubes

Archivos previstos:

```text
Assets/Shaders/Resources/Compute/PlanetMarchingCubesTables.hlsl
PlanetMarchingCubesLookupTables en C#
Assets/Shaders/Resources/Compute/PlanetMarchingCubes.compute
```

Tablas:

```text
edgeTable[256] subido a buffer GPU
triTable[256][16] subido a buffer GPU
```

Reglas:

```text
triTable usa -1 como final de lista.
Cada caso genera como maximo 5 triangulos.
Cada triangulo usa 3 indices de arista.
Las tablas grandes viven en C# como datos estaticos y se suben a buffers GPU antes del dispatch.
El shader lee edgeTable/triTable desde StructuredBuffer para evitar constantes HLSL grandes en Quest/GLES/Vulkan.
El include HLSL mantiene solo constantes pequeñas de topologia, como offsets de esquinas y pares de aristas.
Los tests EditMode pueden validar una copia CPU pequeña o generada, pero no definen la ruta runtime.
```

## Normales

La tabla se lee con el orden estandar de `triTable`, pero la emision aplica un flip global de winding para la convencion del proyecto:

```text
density > 0  -> solido
density <= 0 -> aire
```

Orden de emision:

```text
edgeA = triTable[row + 0]
edgeB = triTable[row + 1]
edgeC = triTable[row + 2]

a = edgeVertex[edgeA]
b = edgeVertex[edgeC]
c = edgeVertex[edgeB]
```

La normal sale del orden emitido.

```text
geometricNormal = normalize(cross(b - a, c - a))
```

Regla:

```text
La orientacion no se corrige por direccion radial al centro del planeta.
La orientacion no se corrige por gradiente estimado de density(point).
El winding lo define la combinacion caseIndex + edgeTable + triTable.
El flip global de B/C pertenece a la convencion density > 0 = solido.
Si aparecen triangulos aislados invertidos, el bug esta en la convencion caseIndex/triTable o en el orden de esquinas/aristas.
La normal se duplica en los 3 vertices del triangulo.
No se calculan normales suaves compartidas en 07.
```

## Salida de geometria

Decision inicial:

```text
07 escribe vertices no indexados.
```

Formato conceptual de vertice:

```text
positionGrid.xyz
normalGrid.xyz
diagnosticData.x = caseIndex opcional
diagnosticData.y = chunkIndex opcional
diagnosticData.z = reserved
diagnosticData.w = reserved
```

Layout recomendado:

```text
float4 positionAndCase:
    x,y,z = posicion en GridCoordinates.
    w     = caseIndex.

float4 normalAndDiagnostic:
    x,y,z = normal.
    w     = chunk/celda/debug.
```

Stride:

```text
32 bytes por vertice.
```

Indices:

```text
La Mesh visual usa indices lineales generados en CPU:
0, 1, 2, 3, 4, 5...
```

Regla:

```text
No hay deduplicacion de vertices en 07.
No hay indexacion compartida real en 07.
08 pintara el resultado.
10 decidira reparto interno/compactacion para geometria adaptable.
11 decidira visibilidad/occlusion/frustum.
```

## Buffers GPU

Recursos previstos:

```text
GraphicsBuffer de chunk origins o parametros de chunk.
GraphicsBuffer de vertices.
GraphicsBuffer de contador/estado.
Buffers de forma de 06 ya vivos.
```

Estado/contador:

```text
chunkCountCandidate.
chunkCountProcessed.
cellCountProcessed.
triangleCountAttempted.
triangleCountWritten.
vertexCountWritten.
overflowFlag.
invalidCaseFlag.
```

Layout conceptual:

```text
PlanetMarchingCubesStateGpu:
    uint chunkCountCandidate;
    uint chunkCountProcessed;
    uint cellCountProcessed;
    uint triangleCountAttempted;
    uint triangleCountWritten;
    uint vertexCountWritten;
    uint overflowFlag;
    uint invalidCaseFlag;
```

Reglas:

```text
triangleCountAttempted cuenta los triangulos que Marching Cubes quiso generar.
triangleCountWritten cuenta los triangulos que entraron en el buffer.
vertexCountWritten siempre debe ser triangleCountWritten * 3.
overflowFlag se activa si temporaryOutputTriangleCapacity no alcanza.
invalidCaseFlag se activa si se detecta una lectura invalida de tabla o caso imposible.
cellCountProcessed cuenta cuantas celdas reales proceso el dispatch.
```

Reglas:

```text
GraphicsBuffer es el camino principal.
ComputeBuffer solo se usa si una API concreta lo exige y queda documentado.
No se crea buffer de densidad global.
No se crea volumen 3D global persistente.
Todos los recursos se registran con owner y estimatedBytes.
```

## Compute Shader previsto

Archivo:

```text
Assets/Shaders/Resources/Compute/PlanetMarchingCubes.compute
```

Include obligatorio:

```text
Assets/Shaders/Resources/Compute/PlanetShapeDensity.hlsl
Assets/Shaders/Resources/Compute/PlanetMarchingCubesTables.hlsl
```

Kernel canonico de 07:

```text
CS_ExtractChunkedCartesianSurface
```

Thread group inicial:

```text
[numthreads(64, 1, 1)]
```

Dispatch:

```text
cellCount = chunkCount * 64 * 64 * 64
groupCountX = ceil(cellCount / 64)
```

Conversion de indice lineal:

```text
globalCellIndex -> chunkIndex + localCellIndex
localCellIndex -> local x/y/z en 0..63
cellOrigin = chunkOriginGrid + int3(x, y, z)
```

Regla:

```text
Un thread procesa una celda 1x1x1 real.
Si groupCountX supera el limite de Unity, 07 debe partir la extraccion en varios dispatch con `cellStartIndex`.
Los dispatch parciales comparten el mismo buffer de vertices y el mismo estado GPU.
El estado GPU solo se limpia una vez antes del primer dispatch del lote completo.
```

## Flujo funcional

Flujo minimo:

```text
1. Validar que 06 esta inicializado.
2. Validar settings de Marching Cubes.
3. Construir lista CPU de chunks candidatos.
4. Subir chunk origins a GPU.
5. Crear buffers GPU de salida y estado.
6. Resetear contador/estado.
7. Configurar buffers de density(point) desde 06.
8. Dispatch CS_ExtractChunkedCartesianSurface.
9. Leer contador/estado de forma acotada para diagnostico.
10. Leer vertices escritos de forma acotada para 08.
11. Dejar el resultado disponible para pintado.
12. Mostrar diagnostico.
13. Liberar recursos cuando se pida.
```

Regla:

```text
El readback de vertices pertenece al Lab/visualizacion actual.
La ruta final de payload no queda obligada a leer todos los vertices a CPU.
```

## Componentes/scripts previstos

### PlanetMarchingCubesSettings

Dato serializable de configuracion.

Responsabilidad:

```text
Guardar chunkSize.
Guardar temporaryOutputTriangleCapacity.
Guardar safetyMargin.
Guardar limite opcional de chunks para pruebas.
Guardar flags de diagnostico.
Validar limites.
No contener buffers GPU.
No depender del Lab.
```

Valores iniciales:

```text
chunkSize = 64
cellSizeGrid = 1
safetyMargin = 4
temporaryOutputTriangleCapacity = configurable alto para validacion bruta
```

### PlanetMarchingCubesChunkRange

Dato pequeño para describir los chunks candidatos.

Responsabilidad:

```text
Calcular shell radial posible desde PlanetRecipe.
Enumerar chunkCoord candidates.
Calcular chunkAabb.
Validar interseccion AABB-shell.
No reparametrizar el planeta.
```

Nota:

```text
PlanetMarchingCubesSurfaceRange basado en cubemap/radial no es el contrato canonico de 07.
Si existe codigo previo con ese nombre, se considera prototipo descartado o pendiente de reemplazo.
```

### PlanetMarchingCubesVertex

Dato compacto de vertice.

Responsabilidad:

```text
Coincidir con el layout HLSL.
Transportar posicion Grid y normal.
Mantener stride de 32 bytes.
No transportar payload final.
```

### PlanetMarchingCubesExtractionResult

Dato de intercambio entre 07 y 08.

Responsabilidad:

```text
Exponer resultado valido de la extraccion.
Exponer conteos ya leidos del estado GPU.
Exponer si hubo overflow.
Exponer vertices no indexados para readback/pintado de validacion.
No contener density samples.
No contener celdas de Marching Cubes.
No contener una Mesh final.
```

### PlanetMarchingCubesExtractor

Sistema real de extraccion.

Responsabilidad:

```text
Gestionar buffers GPU de Marching Cubes.
Configurar ComputeShader.
Conectar con PlanetGpuShapeEvaluator.
Ejecutar dispatch por chunks cartesianos.
Gestionar readback acotado.
Registrar y liberar recursos.
No contener parametros de forma de 06.
```

Regla:

```text
No basarlo en el extractor cubemap/radial actual.
No hacer cambios quirurgicos sobre la base equivocada.
La implementacion canonica debe ser chunked/cartesian desde el diseño.
```

### PlanetMarchingCubesLab

Modulo aditivo para `PlanetImplementationLab`.

Responsabilidad:

```text
Exponer settings de 07.
Validar que 06 esta listo.
Ejecutar extraccion cartesiana por chunks.
Mostrar metricas.
Ejecutar stress pequeño/medio.
Liberar recursos propios.
Integrarse con Release All.
```

Regla:

```text
El Lab no implementa Marching Cubes.
El Lab solo orquesta PlanetMarchingCubesExtractor.
Debe heredar de PlanetLabModule y respetar InitModule, ReleaseModule, ValidateModule y CaptureMetrics.
```

## Botones de Inspector

Botones esperados:

```text
Validate Marching Cubes Setup
Reset Demo Settings
Init Marching Cubes GPU
Build Candidate Chunks
Extract Cartesian Planet Surface
Run Marching Cubes Smoke Test
Run Stress Low
Run Stress Medium
Capture Snapshot
Release Marching Cubes GPU
Release All
```

Reglas:

```text
Extract Cartesian Planet Surface exige que 06 este inicializado.
Release Marching Cubes GPU no libera recursos cuyo owner sea 06.
Release All libera 07 y despues puede liberar 06 segun el orden del Lab.
```

## Pruebas manuales

Pruebas minimas:

```text
Validate Marching Cubes Setup detecta referencias nulas.
Build Candidate Chunks produce conteo de chunks.
Extract Cartesian Planet Surface ejecuta sin excepcion.
Extract Cartesian Planet Surface produce diagnostico con chunkCount y cellCount.
Release Marching Cubes GPU libera buffers propios.
Release doble no rompe.
Init -> Release -> Init funciona.
Release All deja recursos propios de 07 a cero.
```

Pruebas visuales:

```text
La superficie del planeta aparece con triangulos si los chunks candidatos cruzan la superficie.
Las celdas son cartesianas, sin costuras de cubemap.
Las normales apuntan hacia fuera.
Cambiar seed cambia la silueta.
Cambiar parametros de 06 cambia la superficie extraida.
Cambiar temporaryOutputTriangleCapacity puede provocar overflow diagnosticado.
```

Regla:

```text
Si la superficie no aparece por parametros extremos, se revisa el calculo de chunks candidatos.
No se añade una ruta alternativa de forma.
```

## Tests automatizados

Tests EditMode esperados:

```text
PlanetMarchingCubesSettings valida chunkSize = 64.
PlanetMarchingCubesSettings valida cellSizeGrid = 1.
PlanetMarchingCubesSettings valida temporaryOutputTriangleCapacity > 0.
PlanetMarchingCubesChunkRange calcula shell inner/outer desde PlanetRecipe.
PlanetMarchingCubesChunkRange detecta interseccion AABB-shell.
PlanetMarchingCubesChunkRange no devuelve chunks fuera del volumen candidato salvo margen documentado.
El orden de esquinas coincide con el documentado.
El orden de aristas coincide con el documentado.
El stride C# de PlanetMarchingCubesVertex coincide con 32 bytes.
El calculo de dispatch groups usa ceil(cellCount / 64).
Release simulado no deja handles vivos.
```

Tests PlayMode esperados:

```text
PlanetMarchingCubesLab existe en PlanetImplementationLab cuando se integre.
Validate Marching Cubes Setup no lanza excepcion con referencias validas.
Init Marching Cubes GPU no lanza excepcion si hay soporte compute.
Extract Cartesian Planet Surface no lanza excepcion si 06 esta listo.
Release Marching Cubes GPU no lanza excepcion.
Release doble no lanza excepcion.
Init -> Release -> Init funciona.
```

Tests condicionados:

```text
Si SystemInfo.supportsComputeShaders es false, el modulo da diagnostico claro.
Si AsyncGPUReadback no esta disponible, el pintado CPU queda unavailable y la extraccion GPU sigue siendo diagnosticable.
Si overflowFlag se activa, el test comprueba que no hay escritura fuera de buffer.
```

Regla:

```text
No intentar generar un planeta completo gigante en tests automaticos.
Los tests prueban el pipeline con receta pequeña.
```

## Metricas

Metricas iniciales:

```text
chunkSize.
cellSizeGrid.
chunkCountCandidate.
chunkCountProcessed.
cellCountProcessed.
temporaryOutputTriangleCapacity.
maxPlanetSurfaceVertices.
triangleCountAttempted.
triangleCountWritten.
vertexCountWritten.
overflowFlag.
invalidCaseFlag.
vertexBufferBytes.
ownedGpuEstimatedBytes.
ownedCpuEstimatedBytes.
dispatchThreadGroupSize.
dispatchGroupCount.
lastChunkBuildMs.
lastDispatchRequestMs.
lastReadbackRequestMs.
lastReleaseMs.
liveGraphicsBuffers.
lastDiagnostic.
```

Metricas diferidas a 08:

```text
Triangulos pintados.
Vertices pintados.
Mesh memory de validacion.
Material/color mode de validacion.
Frame time visual con geometria pintada.
```

Metricas diferidas a 10/11:

```text
Triangulos redistribuidos/degradados por geometria adaptable.
Triangulos descartados o no pedidos por visibilidad.
Payload final por planeta/estado/distancia.
Coste de compactacion.
Politica de indices compartidos.
Memoria final de mesh/payload.
```

## Gestion de RAM

Reglas:

```text
No crear arrays nuevos por frame.
No usar LINQ en caminos calientes.
No guardar densidades de todo el planeta.
Preasignar buffers CPU solo para chunk origins y readback acotado.
Liberar Mesh runtime explicitamente.
```

Datos CPU esperados:

```text
Settings serializados.
Lista de chunk origins candidatos.
Estado/diagnostico.
Array/lista acotada para vertices leidos.
Indices lineales de Mesh.
Mesh runtime.
```

Regla:

```text
El tamaño CPU crece con chunkCount candidato y temporaryOutputTriangleCapacity de validacion, no con el volumen completo del planeta.
```

## Gestion de VRAM

Recursos GPU previstos:

```text
GraphicsBuffer de chunk origins.
GraphicsBuffer de vertices.
GraphicsBuffer de estado/contador.
GraphicsBuffer de tablas.
Buffers de 06 referenciados, no owned.
```

Reglas:

```text
Cada recurso propio se registra con owner `07_Marching_Cubes`.
Los buffers de 06 no se registran de nuevo como owned por 07.
No se crea RenderTexture si no se usa.
No se crea volumen 3D global.
No se crea buffer de densidades global.
```

## Liberacion de recursos

`Release` debe:

```text
Liberar buffers GPU propios.
Liberar Mesh runtime si 07 posee una de validacion.
Cancelar/ignorar readbacks pendientes de forma segura.
Marcar handles como liberados.
Limpiar referencias internas.
Dejar contadores propios a cero.
Permitir Release doble.
Permitir Init -> Release -> Init.
```

Regla:

```text
OnDisable/OnDestroy son red de seguridad.
No sustituyen al Release explicito.
```

## Riesgos

Riesgos principales:

```text
Duplicar density(point) en 07.
Crear un Marching Cubes CPU paralelo como validacion principal.
Volver a introducir cubemap/cáscara radial como atajo.
Confundir chunk candidato con chunk jugable final.
Crear un buffer de densidades global.
Crear vertices sin limite y romper memoria.
Readback masivo o bloqueante.
Winding invertido por convencion de tabla.
Mezclar payload final con Mesh visual temporal.
Liberar recursos de 06 desde 07.
```

Mitigaciones:

```text
Include obligatorio de PlanetShapeDensity.hlsl.
Ruta GPU como fuente real.
Chunks cartesianos de 64x64x64.
Cell size fijo de 1x1x1.
Lista de chunks candidatos documentada.
temporaryOutputTriangleCapacity obligatorio.
overflowFlag obligatorio.
No exponer un resultado parcial como superficie valida.
Readback acotado y solo para visualizacion actual.
Flip de normal hacia fuera.
Owner de recursos separado.
08 pinta la salida de 07.
10 decide reparto adaptativo de paginas LOD.
11 queda como senales auxiliares de visibilidad/oclusion, no como autoridad de pintado o LOD del planeta.
```

## Decisiones cerradas

```text
No quedan decisiones abiertas para empezar la implementacion de 07.
La tabla de Marching Cubes es la tabla estandar de 256 casos.
edgeTable[256] y triTable[256][16] se guardan en C# y se suben a buffers GPU de 07.
PlanetMarchingCubesTables.hlsl conserva solo constantes pequeñas de topologia.
caseIndex es la mascara de 8 bits de las esquinas solidas del cubo.
caseIndex = 0 y caseIndex = 255 generan 0 triangulos.
El orden de esquinas/aristas documentado debe coincidir con la tabla.
El resultado expuesto a 08 es PlanetMarchingCubesExtractionResult.
07 canonico usa chunks cartesianos de 64x64x64 celdas.
La cell logica es 1x1x1.
La extraccion inicial corre en GPU.
La Mesh visual usa vertices no indexados.
Los indices son lineales y se generan en CPU solo para la Mesh visual.
Las normales son geometricas por triangulo y salen del winding de triTable.
El readback es acotado y solo para visualizacion actual.
El pintado queda para 08.
El reparto de detalle queda para 10.
La visibilidad queda para 11.
El extractor cubemap/radial no forma parte del 07 canonico.
```

## Criterio de cierre

Este documento queda listo para implementar cuando aceptemos este contrato:

```text
07 convierte density(point) de 06 en triangulos reales de Marching Cubes.
07 no redefine el grid ni la cell logica.
07 no reimplementa la forma del planeta.
07 ejecuta Marching Cubes en GPU.
07 usa chunks cartesianos reales de 64x64x64.
07 genera vertices acotados para que 08 pinte una Mesh visual.
07 registra y libera sus recursos.
07 mide chunks, celdas, triangulos, vertices, overflow, memoria y tiempos.
07 no define pintado final ni payload final.
07 no usa cubemap/radial shell como atajo.
```

El cierre real del bloque ocurre en:

```text
_deadline_06-08
```

Ese deadline valida conjuntamente:

```text
Forma GPU.
Marching Cubes cartesiano bruto.
Pintado del resultado de Marching Cubes.
Reparto de detalle y visibilidad quedan reservados para 10 y 11.
```
