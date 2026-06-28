# 07 - Marching Cubes

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

## Objetivo

Convertir el campo de densidad definido en `06_Forma_Planeta_GPU` en triangulos reales mediante Marching Cubes, dentro de una malla de validacion acotada.

Este documento no define la forma del planeta. Consume:

```text
PlanetShapeDensity.hlsl
density(point)
PlanetRecipe
PlanetGpuShapeEvaluator
Buffers GPU de parametros/celdas Voronoi vivos
```

Contrato funcional:

```text
campo escalar -> muestras de cubo -> mascara de ocupacion -> triangulos reales
```

Regla central:

```text
07 no reimplementa density(point).
07 incluye el HLSL compartido de 06.
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

Por tanto, la "masa" del planeta existe como campo escalar implicito, no como millones de celdas guardadas en RAM/VRAM.

07 coloca una rejilla temporal sobre una zona de ese campo y pregunta por las 8 esquinas de cada cubo:

```text
corner solid/air -> caseIndex -> triangulos de Marching Cubes
```

La rejilla de 07 no es el resultado visual. Es la herramienta de medicion.

El resultado de 07 son triangulos reales de superficie, interpolados en las aristas donde el campo cruza el isoLevel.

08 parte 1 no decide como unir los 8 vertices de cada cubo. Eso ya lo decide Marching Cubes en 07 mediante `triTable`.

08 parte 1 decide como pintar los triangulos ya generados:

```text
como se convierten en Mesh visible.
que material/color diagnostico usan.
cuantos se pintan como cortafuegos de validacion.
como se miden sus vertices/triangulos/bytes.
como se liberan sus recursos visuales.
```

08-2 decide la optimizacion:

```text
BVH.
distancia de camara.
presupuesto de poligonaje.
prioridad visual.
triangulos conservados/descartados por budget.
```

Resumen:

```text
06 = campo escalar implicito.
07 = muestreo + triangulacion local de Marching Cubes.
08 = pintado del resultado.
08-2 = BVH + presupuesto/seleccion/organizacion del resultado.
```

## Alcance de esta fase

Entra:

```text
Marching Cubes sobre GPU.
Tabla de casos de Marching Cubes.
Muestreo de density(point) desde el HLSL de 06.
Rango inicial exacto de muestreo sobre el grid de receta.
Extraccion de triangulos reales de Marching Cubes.
Normales geometricas iniciales.
Buffer GPU de vertices no indexados.
Readback pequeno/acotado para Mesh de Unity.
Mesh de validacion no final.
Metricas de cubos, triangulos, vertices, overflow y memoria.
Registro y liberacion de recursos.
Botones de Lab para generar/liberar la extraccion.
```

La fase debe validar visualmente por primera vez que la forma de 06 produce superficie.

## Fuera de alcance

No entra:

```text
Payload final de triangulos.
Asignacion de presupuesto global.
LOD final.
Chunks locales reales.
Proxy lejano final.
Deduplicacion avanzada de vertices.
Persistencia.
Colisiones.
Materiales finales.
Cuevas.
Minerales/sustancias.
Terraformado.
Streaming.
BVH.
Mesh planetaria completa.
```

Regla:

```text
Si una decision trata de pintar los triangulos generados, pertenece a 08.
Si una decision trata de conservar, repartir, degradar o priorizar triangulos por camara/presupuesto, pertenece a 08-2.
Si una decision trata de una mesh low-res final del planeta entero, pertenece a 09.
Si una decision trata de chunks jugables alrededor del player, pertenece a 11.
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
08-2_BVH_y_Presupuesto_Poligonaje
_deadline_06-08
09_Proxy_Planeta_Lejano
11_Chunks_Locales
```

Dependencias cerradas:

```text
PlanetRecipe aporta GridRadius, WorldScale, Seed, IsoLevel, VoronoiDivision y ContinentCells.
PlanetGpuShapeEvaluator aporta parametros/celdas GPU vivos.
PlanetShapeDensity.hlsl aporta density(point).
PlanetResourceRegistry registra recursos grandes.
PlanetImplementationLab ejecuta botones, metricas y Release All.
```

Estado del codigo existente antes de implementar 07:

```text
PlanetRecipe existe en `Assets/Scripts/Planet/Coordinates/Runtime/PlanetRecipe.cs`.
PlanetRecipe actual todavia no tiene VoronoiDivision ni ContinentCells hasta que 06 lo amplie.
PlanetRecipeValidator ya existe y debe validar los nuevos campos cuando 06 los añada.
PlanetLabModule ya define InitModule, ReleaseModule, ValidateModule y CaptureMetrics.
PlanetLabResourceRegistry ya registra recursos por owner, type, elementCount, stride y estimatedBytes.
PlanetGpuBufferHandle ya encapsula GraphicsBuffer/ComputeBuffer estructurados.
PlanetComputeShaderRunner existe, pero es un adaptador especifico de `PlanetComputeDebug.compute`.
PlanetRecipePayloadPreview ya genera una esfera de payload preview, pero no usa density(point).
```

Reglas de integracion con codigo existente:

```text
07 no debe modificar PlanetComputeShaderRunner para convertirlo implicitamente en API generica si eso rompe el objetivo del paso 02.
07 puede crear un extractor propio o una API compute nueva y explicita.
07 debe seguir el patron PlanetLabModule para su modulo de Lab.
07 debe usar PlanetLabResourceRegistry con owner propio.
07 no debe reutilizar PlanetSpherePayloadMeshBuilder como fuente geometrica de Marching Cubes.
```

Ensamblados previstos:

```text
Crear MarchingCubesPlanet.Shape para 06 si no existe como asmdef real.
Crear MarchingCubesPlanet.MarchingCubes para 07 si conviene separar extractor y datos.
Actualizar MarchingCubesPlanet.Lab para referenciar los ensamblados reales de 06/07.
Actualizar MarchingCubesPlanet.Lab.Editor si se añaden editores nativos.
Actualizar asmdefs de tests para referenciar los nuevos ensamblados.
```

Regla:

```text
No meter el algoritmo real de Marching Cubes directamente en MarchingCubesPlanet.Lab.
El Lab invoca codigo real y mide, pero no es la implementacion del algoritmo.
```

## Regla de grid heredado

07 no define otro tamaño logico de grid.

```text
Micro cell logica = 1x1x1 en GridCoordinates.
GridRadius viene de PlanetRecipe.
GridDiameter = GridRadius * 2.
Con GridRadius = 1000, el diametro logico del planeta es 2000 cells.
```

Marching Cubes trabaja sobre cubos de una micro cell logica:

```text
cubeSizeGrid = 1
```

Regla:

```text
07 solo decide que volumen/rango inicial de ese grid se muestrea.
07 no cambia la resolucion logica del planeta.
```

## Decision de extraccion inicial

La extraccion inicial corre en GPU.

```text
CPU prepara parametros y dispatch.
GPU evalua density(point).
GPU calcula mascaras de ocupacion.
GPU aplica Marching Cubes.
GPU escribe vertices no indexados.
CPU hace readback acotado solo para construir una Mesh de validacion de Unity.
```

Motivo:

```text
La funcion density(point) vive en GPU.
Evita crear una ruta CPU paralela de forma.
Evita duplicar el algoritmo de forma.
Prepara la ruta real de chunks/payload.
Mantiene el readback como herramienta de Lab, no como contrato final.
```

Regla:

```text
No implementar Marching Cubes CPU como ruta alternativa de validacion.
Tests CPU solo pueden comprobar tablas, indices y formulas pequeñas sin sustituir la ruta GPU.
```

## Rango inicial de muestreo

El primer rango no intenta mallar el planeta completo.

Decision inicial:

```text
validationPatchDirection = +X
validationPatchTangentU = +Y
validationPatchTangentV = +Z
radialStartOffset = -512
radialCubeCount = 1024
tangentHalfExtent = 8
tangentCubeCount = 16
cubeSizeGrid = 1
```

Posicion de una esquina de muestra:

```text
radial = GridRadius + radialStartOffset + x
u = -tangentHalfExtent + y
v = -tangentHalfExtent + z

point =
    validationPatchDirection * radial +
    validationPatchTangentU * u +
    validationPatchTangentV * v
```

Con los valores iniciales:

```text
x = 0..1024
y = 0..16
z = 0..16
```

Numero de cubos:

```text
1024 * 16 * 16 = 262144 cubos
```

Motivo:

```text
El slab cruza la superficie esperada alrededor del radio base.
El rango radial cubre deformaciones fuertes de 06 sin mallar un volumen enorme.
El rango tangencial es pequeño para mantener controlado el primer coste.
La cell logica sigue siendo 1x1x1.
```

Regla:

```text
Este rango es un patch de validacion inicial.
No es el chunk final.
No es el proxy final.
No es el grid global del planeta.
```

Parametros editables de Lab:

```text
validationPatchDirection.
radialStartOffset.
radialCubeCount.
tangentHalfExtent.
tangentCubeCount.
maxValidationTriangles.
```

Valores de seguridad iniciales:

```text
maxValidationTriangles = 65536
maxValidationVertices = maxValidationTriangles * 3
```

Si se supera el limite:

```text
Se marca overflow.
No se escribe fuera del buffer.
El resultado visual puede quedar truncado.
El diagnostico debe indicar cuantos triangulos se intentaron escribir.
```

## Ocupacion de cubo

Cada cubo tiene 8 esquinas.

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
si density(cornerN) > isoLevel:
    cornerMask |= 1 << N
```

Con:

```text
isoLevel = PlanetRecipe.IsoLevel
valor inicial = 0
density > isoLevel  -> solido
density <= isoLevel -> aire
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

Cuando una arista cruza el isoLevel:

```text
t = (isoLevel - valueA) / (valueB - valueA)
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
La conversion a WorldSpace se hace al construir/mostrar la Mesh de validacion.
```

## Tablas de Marching Cubes

Archivos previstos:

```text
Assets/Shaders/Compute/PlanetMarchingCubesTables.hlsl
Assets/Shaders/Compute/PlanetMarchingCubes.compute
```

Tablas:

```text
edgeTable[256]
triTable[256][16]
```

Reglas:

```text
triTable usa -1 como final de lista.
Cada caso genera como maximo 5 triangulos.
Cada triangulo usa 3 indices de arista.
La tabla vive en HLSL para que la ruta GPU sea la fuente real.
Los tests EditMode pueden validar una copia CPU pequeña o generada, pero no definen la ruta runtime.
```

## Normales

La normal inicial es geometrica por triangulo.

```text
normal = normalize(cross(b - a, c - a))
```

Como la densidad es positiva dentro del planeta, se fuerza orientacion exterior:

```text
triangleCenter = (a + b + c) / 3
outward = normalize(triangleCenter - planetCenter)

si dot(normal, outward) < 0:
    swap(b, c)
    normal = -normal
```

Regla:

```text
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
diagnosticData.x = density/height opcional
diagnosticData.y = caseIndex opcional
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
    w     = valor diagnostico/reservado.
```

Stride:

```text
32 bytes por vertice.
```

Indices:

```text
La Mesh de validacion usa indices lineales generados en CPU:
0, 1, 2, 3, 4, 5...
```

Regla:

```text
No hay deduplicacion de vertices en 07.
No hay indexacion compartida real en 07.
08 pintara el resultado.
08-2 decidira BVH, compactacion y presupuesto de triangulos.
```

## Buffers GPU

Recursos previstos:

```text
GraphicsBuffer de parametros de Marching Cubes.
GraphicsBuffer de vertices.
GraphicsBuffer de contador/estado.
Buffers de forma de 06 ya vivos.
Mesh runtime de validacion.
```

Estado/contador:

```text
triangleCountAttempted.
triangleCountWritten.
vertexCountWritten.
overflowFlag.
invalidCaseFlag.
processedCubeCount.
```

Layout cerrado:

```text
PlanetMarchingCubesStateGpu:
    uint triangleCountAttempted;
    uint triangleCountWritten;
    uint vertexCountWritten;
    uint overflowFlag;
    uint invalidCaseFlag;
    uint processedCubeCount;
    uint reserved0;
    uint reserved1;
```

Stride:

```text
32 bytes.
```

Reglas:

```text
triangleCountAttempted cuenta los triangulos que Marching Cubes quiso generar.
triangleCountWritten cuenta los triangulos que entraron en el buffer.
vertexCountWritten siempre debe ser triangleCountWritten * 3.
overflowFlag se activa si maxValidationTriangles no alcanza.
invalidCaseFlag se activa si se detecta una lectura invalida de tabla o caso imposible.
processedCubeCount cuenta cuantos cubos proceso el dispatch.
```

Reglas:

```text
GraphicsBuffer es el camino principal.
ComputeBuffer solo se usa si una API concreta lo exige y queda documentado.
No se crea buffer de densidad global.
No se crea volumen 3D global.
No se crea una Mesh del planeta completo.
Todos los recursos se registran con owner y estimatedBytes.
```

Estimacion inicial:

```text
vertexBufferBytes = maxValidationVertices * 32
stateBufferBytes = strideState
meshCpuBytes estimado = vertexCountWritten * datos de Mesh de validacion
```

Con los valores iniciales:

```text
maxValidationTriangles = 65536
maxValidationVertices = 196608
vertexBufferBytes = 6291456 bytes
vertexBufferBytes ~= 6 MiB
```

## Compute Shader previsto

Archivo:

```text
Assets/Shaders/Compute/PlanetMarchingCubes.compute
```

Include obligatorio:

```text
Assets/Shaders/Compute/PlanetShapeDensity.hlsl
Assets/Shaders/Compute/PlanetMarchingCubesTables.hlsl
```

Kernel inicial:

```text
CS_ExtractValidationPatch
```

Thread group inicial:

```text
[numthreads(64, 1, 1)]
```

Dispatch:

```text
cubeCount = radialCubeCount * tangentCubeCount * tangentCubeCount
groupCountX = ceil(cubeCount / 64)
```

Regla:

```text
Un thread procesa un cubo.
El indice lineal se convierte a x/y/z dentro del rango de validacion.
```

## Flujo funcional

Flujo minimo:

```text
1. Validar que 06 esta inicializado.
2. Validar settings de Marching Cubes.
3. Crear buffers GPU de salida y estado.
4. Resetear contador/estado.
5. Configurar parametros de rango.
6. Configurar includes/buffers de density(point) desde 06.
7. Dispatch CS_ExtractValidationPatch.
8. Leer contador/estado de forma acotada para diagnostico.
9. Leer vertices escritos de forma acotada para Mesh de validacion.
10. Construir Mesh de validacion no indexada.
11. Registrar Mesh runtime.
12. Mostrar diagnostico.
13. Liberar recursos cuando se pida.
```

Regla:

```text
El readback de vertices pertenece al Lab/validacion.
La ruta final de payload no queda obligada a leer todos los vertices a CPU.
```

## Componentes/scripts previstos

### PlanetMarchingCubesSettings

Dato serializable de configuracion.

Responsabilidad:

```text
Guardar rango de validacion.
Guardar maxValidationTriangles.
Guardar flags de diagnostico.
Validar limites.
No contener buffers GPU.
No depender del Lab.
```

### PlanetMarchingCubesRange

Dato pequeño de rango de muestreo.

Responsabilidad:

```text
Representar direccion radial, tangentes, offsets y counts.
Calcular cubeCount.
Calcular bounds aproximados.
No redefinir cell size.
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

Layout cerrado:

```text
PlanetMarchingCubesVertex:
    float4 positionAndCase;
        xyz = posicion en GridCoordinates.
        w   = caseIndex.

    float4 normalAndDiagnostic;
        xyz = normal geometrica en GridCoordinates.
        w   = dato diagnostico/reservado.
```

Stride:

```text
32 bytes.
```

### PlanetMarchingCubesExtractionResult

Dato de intercambio entre 07 y 08.

Responsabilidad:

```text
Exponer el resultado valido de la extraccion.
Exponer conteos ya leidos del estado GPU.
Exponer si hubo overflow.
Exponer vertices no indexados para readback/pintado de validacion.
No contener density samples.
No contener celdas de Marching Cubes.
No contener una Mesh final.
```

Contenido conceptual:

```text
vertexBuffer.
triangleCountAttempted.
triangleCountWritten.
vertexCountWritten.
overflowFlag.
invalidCaseFlag.
processedCubeCount.
hasValidResult.
lastDiagnostic.
```

Regla:

```text
08 consume triangulos ya resueltos.
08 no recibe el grid, no recibe densidades y no consulta triTable.
```

### PlanetMarchingCubesExtractor

Sistema real de extraccion.

Responsabilidad:

```text
Gestionar buffers GPU de Marching Cubes.
Configurar ComputeShader.
Conectar con PlanetGpuShapeEvaluator.
Ejecutar dispatch.
Gestionar readback acotado.
Construir Mesh de validacion si se solicita.
Registrar y liberar recursos.
No contener parametros de forma de 06.
```

Regla de implementacion:

```text
No basarlo directamente en PlanetComputeShaderRunner actual.
Ese runner esta documentado en codigo como adaptador del paso 02 para PlanetComputeDebug.compute.
Si se extrae una utilidad comun de dispatch, debe ser una clase nueva o una ampliacion documentada sin romper el runner debug existente.
```

### PlanetMarchingCubesLab

Modulo aditivo para `PlanetImplementationLab`.

Responsabilidad:

```text
Exponer settings de 07.
Validar que 06 esta listo.
Ejecutar extraccion de validacion.
Mostrar Mesh de validacion.
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

Integracion con escena:

```text
PlanetImplementationLabSceneBuilder debe crear el GameObject/modulo de 06 y 07 cuando existan.
El orden de modulos debe permitir inicializar 06 antes de extraer 07.
Release All debe dejar el registry a cero como los tests actuales esperan.
```

### PlanetMarchingCubesLabEditor

CustomEditor nativo para botones de 07.

Responsabilidad:

```text
Mostrar parametros.
Mostrar recursos vivos.
Mostrar diagnostico.
Exponer botones de Validate, Extract, Build Validation Mesh, Stress y Release.
```

## Botones de Inspector

Botones esperados:

```text
Validate Marching Cubes Setup
Reset Validation Patch Settings
Init Marching Cubes GPU
Extract Validation Patch
Build Validation Mesh
Run Marching Cubes Smoke Test
Run Stress Low
Run Stress Medium
Capture Snapshot
Release Marching Cubes GPU
Release Validation Mesh
Release All
```

Reglas:

```text
Extract Validation Patch exige que 06 este inicializado.
Build Validation Mesh usa solo vertices ya extraidos o ejecuta la extraccion documentada.
Release Marching Cubes GPU no libera recursos cuyo owner sea 06.
Release All libera 07 y despues puede liberar 06 segun el orden del Lab.
```

## Pruebas manuales

Pruebas minimas:

```text
Validate Marching Cubes Setup detecta referencias nulas.
Init Marching Cubes GPU crea buffers y los registra.
Extract Validation Patch ejecuta sin excepcion.
Extract Validation Patch produce diagnostico con cubeCount.
Build Validation Mesh crea una Mesh runtime si triangleCountWritten > 0.
Release Marching Cubes GPU libera buffers propios.
Release Validation Mesh libera Mesh runtime.
Release doble no rompe.
Init -> Release -> Init funciona.
Release All deja recursos propios de 07 a cero.
```

Pruebas visuales:

```text
El patch +X muestra triangulos si el rango cruza la superficie.
Las normales apuntan hacia fuera.
Cambiar seed cambia la silueta local.
Cambiar parametros de 06 cambia la superficie extraida.
Cambiar maxValidationTriangles puede provocar overflow diagnosticado.
```

Regla:

```text
Si el patch no cruza la superficie por parametros extremos, se cambia el rango desde settings y se documenta el diagnostico.
No se añade una ruta alternativa de forma.
```

## Tests automatizados

Tests EditMode esperados:

```text
PlanetRecipe conserva WorldRadius derivado y añade VoronoiDivision/ContinentCells sin guardar WorldRadius.
PlanetMarchingCubesSettings valida maxValidationTriangles > 0.
PlanetMarchingCubesRange calcula cubeCount correctamente.
PlanetMarchingCubesRange mantiene cubeSizeGrid = 1.
El orden de esquinas coincide con el documentado.
El orden de aristas coincide con el documentado.
El stride C# de PlanetMarchingCubesVertex coincide con 32 bytes.
El calculo de vertexBufferBytes es correcto.
El calculo de dispatch groups usa ceil(cubeCount / 64).
Release simulado no deja handles vivos.
```

Tests PlayMode esperados:

```text
PlanetMarchingCubesLab existe en PlanetImplementationLab cuando se integre.
PlanetImplementationLabSceneBuilder crea el modulo 07 con referencias validas.
Validate Marching Cubes Setup no lanza excepcion con referencias validas.
Init Marching Cubes GPU no lanza excepcion si hay soporte compute.
Extract Validation Patch no lanza excepcion si 06 esta listo.
Build Validation Mesh no lanza excepcion con readback valido.
Release Marching Cubes GPU no lanza excepcion.
Release doble no lanza excepcion.
Init -> Release -> Init funciona.
```

Tests condicionados:

```text
Si SystemInfo.supportsComputeShaders es false, el modulo da diagnostico claro.
Si AsyncGPUReadback no esta disponible, Build Validation Mesh queda unavailable y la extraccion GPU sigue siendo diagnosticable.
Si overflowFlag se activa, el test comprueba que no hay escritura fuera de buffer.
```

Regla:

```text
No hacer readback masivo.
No intentar mallar el planeta completo en tests.
```

## Metricas

Metricas iniciales:

```text
radialCubeCount.
tangentCubeCount.
cubeCount.
maxValidationTriangles.
maxValidationVertices.
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
lastDispatchRequestMs.
lastReadbackRequestMs.
lastMeshBuildMs.
lastReleaseMs.
liveGraphicsBuffers.
liveRuntimeMeshes.
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

Metricas diferidas a 08-2:

```text
Triangulos conservados por presupuesto.
Triangulos descartados/degradados.
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
No guardar todos los vertices de un planeta completo.
Preasignar buffers CPU solo para readback acotado.
Reutilizar arrays/listas de Mesh de validacion cuando sea razonable.
Liberar Mesh runtime explicitamente.
```

Datos CPU esperados:

```text
Settings serializados.
Estado/diagnostico.
Array/lista acotada para vertices leidos.
Indices lineales de Mesh de validacion.
Mesh runtime de validacion.
```

Regla:

```text
El tamaño CPU crece con maxValidationTriangles, no con el volumen del planeta.
```

## Gestion de VRAM

Recursos GPU previstos:

```text
GraphicsBuffer de vertices.
GraphicsBuffer de estado/contador.
GraphicsBuffer de parametros si aplica.
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
Liberar Mesh runtime de validacion.
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
Mallar el planeta completo por accidente.
Confundir patch de validacion con chunk final.
Crear un buffer de densidades global.
Crear vertices sin limite y romper memoria.
Readback masivo o bloqueante.
Winding invertido por convencion de tabla.
Mezclar payload final con Mesh de validacion.
Liberar recursos de 06 desde 07.
```

Mitigaciones:

```text
Include obligatorio de PlanetShapeDensity.hlsl.
Ruta GPU como fuente real.
Rango inicial pequeño y documentado.
maxValidationTriangles obligatorio.
overflowFlag obligatorio.
Readback acotado y solo para validacion.
Flip de normal hacia fuera.
Owner de recursos separado.
08 pinta la salida de 07.
08-2 decide payload final/BVH/presupuesto.
```

## Decisiones cerradas

```text
No quedan decisiones abiertas para empezar la implementacion de 07.
La tabla de Marching Cubes es la tabla estandar de 256 casos portada a HLSL.
edgeTable[256] y triTable[256][16] viven en PlanetMarchingCubesTables.hlsl.
caseIndex es la mascara de 8 bits de las esquinas solidas del cubo.
caseIndex = 0 y caseIndex = 255 generan 0 triangulos.
El orden de esquinas/aristas documentado debe coincidir con la tabla.
El estado GPU usa PlanetMarchingCubesStateGpu de 32 bytes.
El resultado expuesto a 08 es PlanetMarchingCubesExtractionResult.
El primer rango es un patch radial +X.
El rango inicial usa 1024 x 16 x 16 cubos.
La cell logica sigue siendo 1x1x1.
La extraccion inicial corre en GPU.
La Mesh de validacion usa vertices no indexados.
Los indices son lineales y se generan en CPU solo para Mesh de validacion.
Las normales son geometricas por triangulo.
El readback es acotado y solo para validacion.
El pintado queda para 08.
El payload final queda para 08-2.
```

## Criterio de cierre

Este documento queda listo para implementar cuando aceptemos este contrato:

```text
07 convierte density(point) de 06 en triangulos reales de Marching Cubes.
07 no redefine el grid ni la cell logica.
07 no reimplementa la forma del planeta.
07 ejecuta Marching Cubes en GPU.
07 genera una Mesh de validacion acotada.
07 registra y libera sus recursos.
07 mide cubos, triangulos, vertices, overflow, memoria y tiempos.
07 no define pintado final ni payload final.
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
BVH/presupuesto de poligonaje queda reservado para 08-2.
```
