# 06 - Forma planeta GPU

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

## Objetivo

Implementar en GPU la funcion que define la forma exterior del planeta.

Este documento no intenta cerrar el render final. Define el campo escalar que el documento `07_Marching_Cubes` consumira casi de corrido para extraer triangulos.

La idea central viene de:

```text
Docs/Implementacion/Calculo_Funcional_Datos_Planeta.md
```

Contrato funcional:

```text
point -> surfaceOffset(point)
point -> effectiveRadius(point)
point -> density(point)
```

Formula base:

```text
density(planetLocalGridPosition) =
    radius
    + surfaceOffset(planetLocalGridPosition)
    - distance(planetLocalGridPosition, center)
```

Con:

```text
direction = normalize(planetLocalGridPosition - center)
isoLevel = 0
density > isoLevel  -> solido
density <= isoLevel -> aire
```

Regla de cierre provisional:

```text
06 puede quedar asumido como correcto de forma provisional.
La validacion visual fuerte ocurre al encadenarlo con 07_Marching_Cubes.
```

Esto es intencionado. No queremos construir un sistema de visualizacion paralelo solo para probar 06 si el siguiente paso va a convertir exactamente ese campo en triangulos.

## Modelo mental

06 no crea una nube de datos volumetrica del planeta.

No crea:

```text
Un grid global de densidades.
Una lista de voxels del planeta entero.
Una nube persistente de puntos solidos.
Una malla.
Triangulos.
```

06 crea la funcion que permite preguntar por cualquier punto:

```text
density(point)
```

Esa funcion representa la masa del planeta de forma implicita:

```text
density(point) > 0  -> el punto esta dentro de la masa.
density(point) == 0 -> el punto cae en la superficie.
density(point) < 0  -> el punto esta fuera de la masa.
```

La "masa" existe porque la receta y los parametros permiten evaluarla bajo demanda.

Regla:

```text
El planeta base es campo escalar implicito, no volumen materializado.
07 decide que zona muestrear con una rejilla temporal.
07 convierte ese muestreo en triangulos reales de superficie.
08 parte 1 pinta esos triangulos.
10 decide despues el reparto y la publicacion de meshes por chunk.
10 decide despues el shell adaptativo del planeta activo por distancia, mirada, movimiento y frustum local.
11 queda despues como senales auxiliares de visibilidad/oclusion, no como pintor ni como LOD del planeta.
```

Frontera obligatoria:

```text
Solo 06 cambia la formula de density(point).
07 no sabe si density(point) viene de esfera pura, Voronoi, Perlin, cuevas o terraformado.
07 siempre ejecuta el mismo algoritmo: celdas 1x1x1, 8 esquinas, caseIndex y triTable.
08 siempre ejecuta el mismo algoritmo: convertir los triangulos completos de 07 en Mesh visible.
Si una esfera pura falla, el bug esta en 07/08.
Si una esfera pura funciona y una formula compleja falla, el bug esta en 06 o en la cobertura de chunks necesaria para esa formula.
```

## Alcance de esta fase

Entra:

```text
Parametros de forma del planeta.
Seed determinista.
Voronoi esferico inicial.
Seleccion determinista de celdas tierra/oceano.
Elevacion por celda de tierra.
Profundidad oceanica.
Mezcla de bordes continentales.
Ruido fino inicial de superficie.
Biomas iniciales Meadow/Mountain.
Mountain aplica picos suaves internos por celda Voronoi.
surfaceOffset(point).
effectiveRadius(point).
density(point).
Kernel GPU de evaluacion de puntos.
Buffers GPU necesarios para parametros y celdas Voronoi.
Salida de muestras de densidad solo para debug pequeno.
Salida debug minima para comprobar que el kernel corre.
Registro y liberacion de recursos.
```

La fase debe dejar preparado un evaluador que pueda usarse por:

```text
07_Marching_Cubes.
10_Optimizacion_Adaptativa_Poligonaje.
10_Optimizacion_Adaptativa_Poligonaje.
11_Visibilidad_Oclusion_Frustum.
12_Proxy_Planeta_Lejano.
14_Chunks_Locales.
13_Cuevas.
14_Minerales_Sustancias.
16_Terraformado.
```

## Fuera de alcance

No entra:

```text
Marching Cubes.
Extraccion de triangulos.
Tabla de casos de Marching Cubes.
Mesh final del planeta.
Proxy lejano final.
Chunks locales reales.
Cuevas.
Minerales/sustancias.
Terraformado.
Colisiones.
Persistencia.
Streaming.
BVH.
Payload final de triangulos.
```

Tampoco entra construir una visualizacion completa alternativa para 06.

Regla:

```text
Si una prueba visual exige triangulos, pertenece a 07.
Si una prueba visual exige una mesh low-res final, pertenece a 10 o a un documento futuro de proxy.
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
Docs/Implementacion/Pasos/_deadline_01-05.md
```

Este documento prepara directamente:

```text
07_Marching_Cubes
08_Pintado_Resultado_Marching_Cubes
10_Optimizacion_Adaptativa_Poligonaje
11_Visibilidad_Oclusion_Frustum
_deadline_06-08
```

Dependencias cerradas:

```text
PlanetRecipe aporta GridRadius, WorldScale, Seed, IsoLevel, VoronoiDivision y ContinentCells.
PlanetPlacement/CoordinateFrame aporta conversion World -> Grid si se evalua desde mundo.
PlanetResourceRegistry registra los recursos grandes.
PlanetImplementationLab ejecuta botones, metricas y Release All.
```

Estado del codigo existente antes de implementar 06:

```text
PlanetRecipe ya existe en `Assets/Scripts/Planet/Coordinates/Runtime/PlanetRecipe.cs`.
06 amplia PlanetRecipe con los parametros de forma: VoronoiDivision, ContinentCells, elevacion, oceano, ruido y roughness.
PlanetRecipeValidator valida sus rangos.
PlanetRecipe.Default inicializa VoronoiDivision = 100 y ContinentCells = 84.
PlanetRecipeLab muestra estos valores en diagnostico si forman parte de la receta.
```

Regla:

```text
No crear una segunda receta paralela para forma.
Los parametros que definen la identidad procedural del planeta viven en PlanetRecipe.
```

Regla de coordenadas:

```text
El campo se evalua en coordenadas locales/grid del planeta.
No se evalua density directamente en WorldSpacePosition.
```

Regla de grid heredada:

```text
El tamaño logico de la micro cell ya esta definido en 03_Coordenadas_Y_Receta.
Micro cell logica = 1x1x1 en GridCoordinates.
GridRadius viene de PlanetRecipe.
GridDiameter = GridRadius * 2.
Con GridRadius = 1000, el diametro logico del planeta es 2000 cells.
```

Regla:

```text
06 no define otro tamaño de grid.
07 tampoco redefine el tamaño logico de cell.
07 solo define que volumen/rango de ese grid se muestrea para la primera extraccion Marching Cubes.
```

## Datos de entrada

Datos minimos:

```text
PlanetRecipe.
GridRadius.
WorldScale solo para derivaciones visuales o conversion previa.
Seed.
IsoLevel = 0.
VoronoiDivision.
ContinentCells.
PlanetPlacement si se parte de puntos World/Stellar.
Parametros de superficie.
Parametros de mezcla de borde Voronoi.
Debug sample count.
```

Parametros iniciales de forma:

```text
VoronoiDivision
ContinentCells
continentEdgeBlend
continentEdgeWidthMin
continentEdgeWidthMax
continentEdgeShiftStrength
minLandElevation
maxLandElevation
minHeightModifier
maxHeightModifier
oceanDepth
minimumOceanDepth
surfaceNoiseAmplitude
surfaceNoiseFrequency
surfaceNoiseOctaves
surfaceNoiseLacunarity
surfaceNoisePersistence
surfaceNoiseResponsePower
mountainBiomeCells
mountainBiomeMinPeaks
mountainBiomeMaxPeaks
mountainBiomeHeight
mountainBiomePeakRadius
mountainBiomePeakSpread
mountainBiomeEdgeBlend
mountainBiomePeakFalloff
minRoughness
maxRoughness
```

Valores teoricos iniciales heredados del calculo funcional:

```text
VoronoiDivision = 100
ContinentCells = 84
minLandElevation = 1.5% del radio
maxLandElevation = 12.7% del radio
heightModifier = 0.3..1.5
oceanDepth = 16% del radio
minimumOceanDepth = 3% del radio
continentEdgeBlend = 0.16
continentEdgeWidthMin = 0.65
continentEdgeWidthMax = 1.75
continentEdgeShiftStrength = 0.75
surfaceNoiseAmplitude = 8% del radio
surfaceNoiseFrequency = 7
surfaceNoiseOctaves = 4
surfaceNoiseLacunarity = 2
surfaceNoisePersistence = 0.5
surfaceNoiseResponsePower = 3.5
mountainBiomeCells = 10
mountainBiomeMinPeaks = 1
mountainBiomeMaxPeaks = 4
mountainBiomeHeight = 18% del radio
mountainBiomePeakRadius = 0.055
mountainBiomePeakSpread = 0.45
mountainBiomeEdgeBlend = 0.18
mountainBiomePeakFalloff = 2.25
roughnessModifier = 0.6..0.8
```

Regla:

```text
Estos valores son punto de partida.
Si se ajustan por rendimiento o aspecto, se documenta el motivo.
VoronoiDivision define cuantas celdas Voronoi esfericas existen.
ContinentCells define cuantas de esas celdas son continente.
ContinentCells debe ser mayor o igual que 0 y menor o igual que VoronoiDivision.
```

## Datos de salida

Salida real:

```text
Buffers GPU con parametros de forma.
Buffer GPU con celdas Voronoi esfericas.
Kernel capaz de evaluar density(point).
Buffer de muestras debug pequeñas si se necesita diagnostico.
Diagnostico de soporte y recursos.
```

Salida consumida por 07:

```text
PlanetShapeDensity.hlsl con density(point).
Parametros GPU de forma vivos.
Buffer GPU de celdas Voronoi vivo.
```

Regla importante:

```text
07 no debe reimplementar otra version distinta de density(point).
07 incluye el HLSL compartido de 06 y evalua density(point) en su propio kernel.
```

No debe producir:

```text
Mesh.
Indices.
Triangulos.
Colliders.
Texturas finales.
Datos persistidos.
```

## Modelo funcional

### surfaceOffset(point)

Responsabilidad:

```text
Calcular cuanto sube o baja la superficie para un punto evaluado.
```

Componentes:

```text
Voronoi esferico.
Clasificacion tierra/oceano.
Elevacion por celda.
Profundidad oceanica.
Mezcla de borde continental.
Biomas Meadow/Mountain.
Ruido fino.
```

Regla:

```text
La base continental se calcula por direccion.
El ruido fino se calcula con `localPosition / radius`, igual que la formula funcional previa.
No requiere materializar un volumen global.
```

### effectiveRadius(point)

Formula:

```text
effectiveRadius(point) =
    radius + surfaceOffset(point)
```

Responsabilidad:

```text
Dar el radio real de superficie para el punto evaluado.
```

### density(point)

Formula:

```text
density(point) =
    effectiveRadius(point) - distance(point, center)
```

Donde:

```text
direction = normalize(point - center)
```

Reglas:

```text
density > 0  -> solido.
density <= 0 -> aire.
isoLevel actual = 0.
```

Caso especial:

```text
point == center no tiene direccion radial unica.
Para density(center), se considera solido y se puede devolver radius positivo sin calcular direction.
```

Esta excepcion debe estar documentada en codigo porque evita un normalize indefinido, no porque cambie la regla conceptual.

## Voronoi esferico

Las celdas Voronoi viven como direcciones normalizadas.

Decision de receta:

```text
PlanetRecipe incorpora:
- VoronoiDivision.
- ContinentCells.
```

Significado:

```text
VoronoiDivision -> numero total de divisiones/celdas Voronoi sobre la esfera.
ContinentCells  -> numero de esas celdas que se marcan como continente.
```

Valores iniciales:

```text
VoronoiDivision = 100
ContinentCells = 84
```

Lectura:

```text
100 celdas da una lectura directa de division porcentual.
84 celdas continente deja una proporcion inicial de tierra de 84%.
La intencion es partir de un planeta con mucha masa continental y cuencas oceanicas claras.
```

Reglas:

```text
VoronoiDivision debe ser mayor que 0.
ContinentCells debe estar entre 0 y VoronoiDivision.
Cambiar cualquiera de los dos valores cambia la identidad procedural del planeta.
Estos valores viven en receta porque forman parte de como es el planeta, no del Lab.
```

Datos por celda previstos:

```text
direction.xyz
isLand
baseOffset
roughnessModifier
```

Proceso:

```text
1. Generar VoronoiDivision direcciones aleatorias deterministas desde seed.
2. Elegir ContinentCells indices con shuffle determinista desde seed.
3. Marcar celdas tierra/oceano.
4. Calcular offset base por celda.
5. Calcular roughnessModifier por celda.
6. Calcular heightModifier por celda.
7. Subir estos datos a GPU.
```

Distribucion de direcciones:

```text
Secuencia aleatoria determinista heredada del perfil funcional previo.
```

Motivo:

```text
Replica la identidad procedural previa antes de optimizar distribucion.
No necesita assets precalculados.
Es estable por indice y reproducible con seed.
```

Regla:

```text
La distribucion base usa la secuencia determinista previa.
La seed cambia direcciones, seleccion de continentes y alturas.
```

Decision inicial:

```text
Las celdas Voronoi se preparan en CPU y se suben a GPU.
```

Motivo:

```text
La lista de celdas es pequeña frente al volumen de muestras.
Permite controlar determinismo y debug sin generar un planeta completo.
Evita cerrar todavia una arquitectura de generacion procedural 100% GPU.
```

Regla:

```text
El campo de densidad se evalua en GPU.
La preparacion CPU de parametros no convierte el planeta en datos volumetricos.
```

Decision de elevacion inicial:

```text
t = random01(secuenciaElevacion)
landElevation = lerp(minLandElevation, maxLandElevation, t)
landOffset = radius * landElevation
heightModifier = randomRange(seed, cellIndex, minHeightModifier, maxHeightModifier)
nearestSurfaceOffset = nearestBaseOffset * nearestHeightModifier
secondSurfaceOffset = secondBaseOffset * secondHeightModifier
boundaryOffset = (nearestSurfaceOffset + secondSurfaceOffset) / 2
surfaceOffset = lerp(boundaryOffset, nearestSurfaceOffset, interiorBlend)
```

Decision de oceano inicial:

```text
oceanOffset = -radius * oceanDepth
oceanOffset = min(oceanOffset, -radius * minimumOceanDepth)
```

Decision de borde continental inicial:

```text
dotDelta = nearestDot - secondNearestDot
edgeWidthFactor = random determinista por pareja Voronoi entre continentEdgeWidthMin y continentEdgeWidthMax
edgeWidth = continentEdgeBlend * edgeWidthFactor
edgeShift = random determinista por pareja Voronoi entre -continentEdgeBlend * continentEdgeShiftStrength y +continentEdgeBlend * continentEdgeShiftStrength
firstCell, secondCell = pareja Voronoi ordenada por indice estable
signedDotDelta = firstCellDot - secondCellDot
edgeT = saturate((signedDotDelta - edgeShift) / edgeWidth + 0.5)
firstCellBlend = smootherstep(edgeT)
offset = lerp(secondCellOffset, firstCellOffset, firstCellBlend)
```

Regla:

```text
La mezcla pertenece a 06 porque forma parte de la funcion de densidad.
07 y 08 no conocen Voronoi ni suavizado: solo consumen density(point).
El borde no se centra siempre en la frontera matematica entre dos celdas.
Cada pareja Voronoi tiene un ancho y un foco deterministas a partir de seed e indices de celda.
Esto permite que el suavizado invada mas una region u otra sin reducir ni simplificar geometria.
La mezcla se calcula con distancia firmada entre la pareja ordenada para mantener continuidad en la frontera.
```

Decision no bloqueante:

```text
Empezar con VoronoiDivision = 100 y ContinentCells = 84.
Usar la distribucion determinista heredada del perfil funcional previo.
Mantener los parametros visibles en Inspector.
Medir coste antes de aumentar complejidad.
```

## Biomas

Objetivo:

```text
Crear montanas altas y localizadas sin depender solo de ruido uniforme y sin cordilleras que partan el planeta.
```

Modelo:

```text
La celda Voronoi continental puede ser Meadow o Mountain.
Meadow no modifica el offset.
Mountain elige 1..4 puntos internos deterministas.
Cada punto levanta una zona suave con mascara gaussiana.
El aporte del bioma se desvanece a cero en el borde Voronoi.
```

Formula conceptual:

```text
nearestCell, secondCell = sphericalVoronoi(direction)
edgeMask = fade(saturate((nearestDot - secondDot) / MountainBiomeEdgeBlend))
peakMask = max(gaussianDistanceToEachPeak)
surfaceOffset += biome.apply(nearestBiome, direction, nearestCell) * landMask
```

Reglas:

```text
El id de bioma viaja en PlanetGpuShapeCell.offsetRoughnessHash.w.
Mountain solo se asigna a celdas continentales.
MountainBiomeHeight define la cota maxima que debe considerar 07.
El patron `biome.apply` debe permitir anadir biomas sin reescribir la densidad base.
```

## Ruido fino

Antes del ruido fino se aplica el bioma de la celda.

Objetivo:

```text
Mantener la rugosidad pequena agradable sin usarla para crear toda la silueta montanosa.
```

Reglas:

```text
El ruido fino se aplica despues del offset continental/oceanico y despues del bioma.
La amplitud del ruido fino no debe sustituir la altura de Mountain.
```

El ruido fino se aplica despues del offset continental/oceanico.

Formula conceptual:

```text
normalizedPosition = localPosition / radius
boundaryRoughness = (nearestRoughness + secondRoughness) / 2
effectiveRoughness = lerp(boundaryRoughness, nearestRoughness, interiorBlend)
noisePosition = normalizedPosition * frequency * effectiveRoughness
noiseValue = fBmPerlin3D(noisePosition, octaves, lacunarity, persistence)
shapedNoiseValue = sign(noiseValue) * abs(noiseValue) ^ responsePower
offset += shapedNoiseValue * radius * amplitude
```

Reglas:

```text
El ruido debe ser determinista por seed y parametros.
El ruido no debe depender de WorldSpacePosition.
El ruido no debe requerir textura global del planeta.
El ruido no entra linealmente en altura: responsePower aplasta valores pequenos y medios.
responsePower = 1 equivale al comportamiento lineal anterior.
```

Decision inicial:

```text
fBm sobre Perlin3D en GPU.
```

Implementacion inicial:

```text
Perlin3D procedural sin texturas.
Hash determinista entero basado en celda 3D y seed.
Gradientes derivados del hash.
Fade quintico por eje: t * t * t * (t * (t * 6 - 15) + 10).
Interpolacion trilineal de las 8 esquinas.
Salida normalizada esperada en rango aproximado [-1, 1].
fBm normalizado por suma de amplitudes para mantener el rango estable al cambiar octavas.
```

Reglas:

```text
Perlin3D recibe posicion local normalizada por radio, frecuencia, roughness efectivo mezclado en borde Voronoi y seed.
La receta controla SurfaceNoiseOctaves, SurfaceNoiseLacunarity, SurfaceNoisePersistence y SurfaceNoiseResponsePower.
Esta regla replica la formula funcional previa: `normalizedPosition = localPosition / radius`.
El ruido de superficie puede variar dentro de la banda radial evaluada por Marching Cubes.
El seed entra como offset/hash determinista, no como dependencia de tiempo.
Si Perlin3D resulta caro en Quest 3, se mide y se documenta antes de sustituirlo.
```

## Componentes/scripts previstos

### PlanetGpuShapeRecipeRuntime

Adaptador runtime derivado de `PlanetRecipe`.

Responsabilidad:

```text
Leer desde PlanetRecipe los parametros de forma.
Validar rangos mediante PlanetRecipeValidator.
Preparar el paquete de parametros GPU.
No contener buffers GPU.
No depender del Lab.
No convertirse en una fuente paralela de identidad del planeta.
```

Regla:

```text
PlanetRecipe es la fuente de verdad de la forma.
Los parametros de elevacion, oceano, ruido, VoronoiDivision y ContinentCells viven en PlanetRecipe.
Si el Inspector muestra parametros de forma, edita PlanetRecipe.
No existe un PlanetGpuShapeSettings paralelo que pueda contradecir a PlanetRecipe.
```

### PlanetGpuShapeParameters

Dato compacto de parametros globales de forma enviado a GPU.

Layout actual:

```text
float4 radiusIsoSeedCellCount
float4 elevation
float4 oceanBlend
float4 noise
float4 noiseFractal
float4 continentEdgeShape
float4 biomeShape:
    x = mountainHeight.
    y = mountainPeakRadius.
    z = mountainEdgeBlend.
    w = mountainPeakSpread.
float4 mountainBiome:
    x = minPeaks.
    y = maxPeaks.
    z = peakFalloff.
    w = reserved.
```

Stride:

```text
128 bytes.
```

Regla:

```text
El layout C# y HLSL de PlanetGpuShapeParameters debe coincidir.
Los parametros de Mountain viven aqui, no en PlanetGpuShapeCell, porque son globales de receta.
El id de bioma por celda vive en PlanetGpuShapeCell.
```

### PlanetGpuShapeCell

Dato compacto para una celda Voronoi subida a GPU.

Responsabilidad:

```text
Representar una division Voronoi ya resuelta como dato GPU.
Permitir que density(point) encuentre la celda mas cercana y la segunda mas cercana.
Evitar recalcular en GPU la clasificacion continente/oceano de cada celda.
```

Contenido inicial:

```text
direction.xyz        -> direccion normalizada de la celda sobre la esfera.
continentFlag        -> 1 si es continente, 0 si es oceano.
baseOffset           -> offset radial base de esa celda, ya sea tierra u oceano.
roughnessModifier    -> modificador de rugosidad para Perlin3D, mezclado con la segunda celda en borde Voronoi.
heightModifier       -> modificador de altura aplicado por celda antes de mezclar el borde Voronoi.
padding              -> relleno para alinear.
```

Layout inicial recomendado:

```text
float4 directionAndFlag:
    x,y,z = direction normalizada.
    w     = continentFlag como 0.0 o 1.0.

float4 offsetRoughnessHash:
    x = baseOffset.
    y = roughnessModifier.
    z = heightModifier.
    w = padding/reservado.
```

Stride:

```text
32 bytes por celda.
```

Ejemplo de masa:

```text
VoronoiDivision 100 * 32 bytes = 3200 bytes.
```

Regla:

```text
El layout C# y HLSL debe coincidir en stride y alineacion.
No meter aqui datos que se puedan derivar baratos por muestra salvo que eviten coste real.
No guardar vertices ni triangulos en PlanetGpuShapeCell.
```

### PlanetGpuShapeCellBuilder

Sistema real para preparar las celdas desde seed.

Responsabilidad:

```text
Generar direcciones deterministas.
Elegir celdas tierra con shuffle determinista.
Asignar offset base.
Asignar roughness.
Asignar heightModifier.
Rellenar buffer CPU preasignado o array controlado.
No crear recursos GPU.
```

### PlanetGpuShapeEvaluator

Sistema real para gestionar recursos GPU de forma.

Responsabilidad:

```text
Crear buffers GPU de parametros/celdas.
Subir datos.
Configurar ComputeShader.
Ejecutar evaluacion de puntos.
Exponer handles registrados.
Liberar recursos.
No depender del Lab.
```

### PlanetGpuShapeLab

Modulo aditivo para `PlanetImplementationLab`.

Responsabilidad:

```text
Heredar de PlanetLabModule si encaja con la base existente.
Exponer receta y settings demo.
Crear/evaluar/liberar PlanetGpuShapeEvaluator.
Ejecutar smoke test.
Ejecutar muestras debug pequeñas si hace falta.
Mostrar ultimo diagnostico.
Registrar recursos en PlanetResourceRegistry.
```

Regla:

```text
El Lab no implementa density.
El Lab solo orquesta pruebas sobre PlanetGpuShapeEvaluator.
```

### PlanetGpuShapeLabEditor

CustomEditor nativo para botones de 06.

Responsabilidad:

```text
Mostrar parametros.
Mostrar recursos vivos.
Mostrar diagnostico.
Exponer botones de Init, Generate, Evaluate Samples, Release y Stress.
```

### PlanetShapeDensity.compute

Compute Shader de forma.

Kernels previstos:

```text
CS_EvaluateDensitySamples
CS_DebugWriteShapeTexture opcional
```

Regla:

```text
La funcion density debe vivir en HLSL compartible por 07.
Si se usa include HLSL, 07 debe incluir el mismo archivo.
```

Archivo HLSL compartido previsto:

```text
Assets/Shaders/Compute/PlanetShapeDensity.hlsl
```

Archivo compute previsto:

```text
Assets/Shaders/Compute/PlanetShapeDensity.compute
```

## Flujo funcional

Flujo minimo de 06:

```text
1. Validar PlanetRecipe.
2. Generar celdas Voronoi deterministas en CPU.
3. Crear buffers GPU.
4. Subir parametros y celdas.
5. Ejecutar CS_EvaluateDensitySamples sobre muestras pequeñas de debug si hace falta.
6. Guardar resultado en buffer GPU debug si se solicita.
7. Capturar diagnostico.
8. Liberar recursos.
```

Flujo encadenado con 07:

```text
1. 06 inicializa parametros y celdas.
2. 07 incluye la funcion HLSL compartida de density(point).
3. 07 evalua density en las esquinas de sus celdas dentro de su propio kernel.
4. 07 construye mascaras de ocupacion.
5. 07 aplica Marching Cubes.
6. 07 genera triangulos.
7. _deadline_06-08 valida forma + triangulos + pintado de resultado.
```

Decision:

```text
07 no consume por defecto un buffer de densidades precalculado por 06.
07 llama a la funcion HLSL compartida de density(point) dentro de su propio kernel.
```

Motivo:

```text
Evita un buffer intermedio obligatorio.
Evita almacenar densidades que solo sirven para una pasada concreta.
Evita duplicar memoria en Quest 3.
Permite que 07 calcule solo las esquinas/celdas que necesita.
Mantiene una unica fuente de verdad para density(point).
```

Uso permitido de buffer de densidad:

```text
Debug.
Tests pequenos.
Cache local de chunk si 07 lo documenta.
Comparativas de rendimiento.
```

Regla:

```text
Si 07 introduce un buffer de densidades persistente, debe justificar ownership, memoria, reutilizacion y release.
Ese buffer no se convierte en contrato obligatorio de 06.
```

Flujo conceptual:

```text
06:
    PlanetShapeDensity.hlsl define density(point).

07:
    MarchingCubes.compute incluye PlanetShapeDensity.hlsl.
    MarchingCubes.compute evalua density en sus esquinas.
    MarchingCubes.compute genera mascaras y triangulos.
```

## Gestion de RAM

Reglas:

```text
No crear volumen completo del planeta en CPU.
No crear listas nuevas por dispatch.
No usar LINQ en caminos calientes.
No usar ToArray para subir datos grandes si se puede evitar.
Usar buffers/arrays preasignados para celdas y muestras.
```

Datos CPU esperados:

```text
Array/buffer de celdas Voronoi.
Array/buffer de muestras debug pequeno si hace falta.
Settings serializados.
Diagnostico puntual.
```

Regla:

```text
El tamaño CPU crece con numero de celdas Voronoi y muestras debug, no con el volumen del planeta.
```

## Gestion de VRAM

Recursos GPU previstos:

```text
GraphicsBuffer de parametros si aplica.
GraphicsBuffer de celdas Voronoi.
GraphicsBuffer de posiciones de muestra debug.
GraphicsBuffer de salida de densidad debug.
RenderTexture debug opcional.
```

Reglas:

```text
GraphicsBuffer es el camino principal.
ComputeBuffer solo se usa si una API concreta lo exige y queda documentado.
Cada recurso GPU se registra con owner y estimatedBytes.
No se crea RenderTexture debug si no se usa.
No se crea un volumen 3D global del planeta.
```

Estimacion:

```text
cellBufferBytes = cellCount * strideCell
sampleInputBytes = sampleCount * stridePosition
sampleOutputBytes = sampleCount * strideDensitySample
```

Regla:

```text
Los buffers de muestras son debug o pruebas pequeñas.
No representan el grid global del planeta.
No se usan para decidir el tamaño de cell ni el rango de Marching Cubes.
```

## Liberacion de recursos

`Release` debe:

```text
Liberar buffers GPU.
Liberar RenderTexture debug si existe.
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

## Botones de Inspector

Botones esperados:

```text
Validate Shape Setup
Reset Demo Settings
Init Shape GPU
Generate Voronoi Cells
Upload Shape Data
Evaluate Debug Samples
Run Shape Smoke Test
Run Stress Low
Run Stress Medium
Capture Snapshot
Release Shape GPU
Release All
```

Botones opcionales:

```text
Randomize Seed
Show Cell Summary
Show Last Sample Summary
Create Debug Texture
Release Debug Texture
```

Regla:

```text
Los botones de 06 no generan mesh.
Si un boton termina mostrando triangulos, ese boton pertenece a 07.
```

## Pruebas manuales

Pruebas minimas:

```text
Validate Shape Setup detecta referencias nulas.
Generate Voronoi Cells crea el mismo resumen con misma seed.
Cambiar seed cambia el resumen.
Upload Shape Data registra buffers GPU.
Evaluate Debug Samples ejecuta el kernel sin excepcion.
Release Shape GPU libera recursos.
Release Shape GPU dos veces no rompe.
Init -> Release -> Init funciona.
Release All deja recursos propios de 06 a cero.
```

Pruebas de sentido funcional:

```text
density(center) se considera solido.
density(point muy lejos) se considera aire.
effectiveRadius(point) queda dentro de rangos esperados.
surfaceOffset contiene valores negativos para oceano y positivos para tierra.
La misma seed y parametros producen el mismo resumen de celdas.
Otra seed produce otra distribucion.
```

Regla de validacion visual:

```text
La visualizacion fuerte de la forma se aplaza a 07.
06 no queda bloqueado por no tener triangulos visibles.
```

## Tests automatizados

Tests EditMode esperados:

```text
PlanetRecipeValidator valida rangos de forma.
VoronoiDivision > 0.
ContinentCells >= 0.
ContinentCells <= VoronoiDivision.
PlanetGpuShapeCellBuilder produce mismas celdas con misma seed.
Cambiar seed cambia al menos parte de las celdas.
El numero de celdas tierra coincide con ContinentCells.
La distribucion heredada genera direcciones normalizadas.
El stride C# esperado para PlanetGpuShapeCell coincide con el documentado.
El calculo de bytes estimados es correcto.
Release simulado no deja handles vivos.
```

Tests PlayMode esperados:

```text
PlanetGpuShapeLab existe en PlanetImplementationLab cuando se integre.
Validate Shape Setup no lanza excepcion.
Init Shape GPU no lanza excepcion si hay soporte compute.
Evaluate Debug Samples no lanza excepcion.
Release Shape GPU no lanza excepcion.
Release Shape GPU dos veces no lanza excepcion.
Init -> Release -> Init funciona.
```

Tests condicionados:

```text
Si SystemInfo.supportsComputeShaders es false, el modulo da diagnostico claro.
Si AsyncGPUReadback se usa para una muestra pequeña y no esta disponible, queda como unavailable sin bloquear la ruta 07.
```

Regla:

```text
No hacer readback masivo para validar 06.
El test fuerte de forma visible se cierra con 07.
```

## Metricas

Metricas iniciales:

```text
VoronoiDivision.
ContinentCells.
sampleCount.
cellBufferBytes.
sampleInputBytes.
sampleOutputBytes.
ownedGpuEstimatedBytes.
ownedCpuEstimatedBytes.
dispatchThreadGroupSize.
dispatchGroupCount.
lastUploadMs.
lastDispatchRequestMs.
lastReleaseMs.
liveGraphicsBuffers.
lastDiagnostic.
```

Metricas diferidas a 07:

```text
Triangulos generados.
Vertices generados.
Indices generados.
Coste por celda de Marching Cubes.
Mesh memory.
Draw calls.
Frame time visual con geometria.
```

## Riesgos

Riesgos principales:

```text
Duplicar la funcion density entre 06 y 07.
Convertir el buffer debug de densidades en contrato obligatorio.
Evaluar densidad en WorldSpacePosition y provocar terreno nadando.
Crear un volumen completo del planeta.
Usar readback GPU bloqueante para validar.
Construir una visualizacion paralela que luego se descarte.
Cerrar parametros visuales demasiado pronto antes de ver Marching Cubes.
Subir demasiadas celdas Voronoi sin medir coste en Quest 3.
Usar ruido caro por sample sin presupuesto.
Mezclar el preview de payload de esfera con la forma real GPU.
```

Mitigaciones:

```text
HLSL compartido para density.
07 evalua density en su propio kernel.
Buffers de densidad solo debug/cache justificada.
Conversion World -> Grid antes de evaluar.
Buffers por muestras/chunks, no planeta global.
Readback solo debug pequeño y opcional.
Validacion visual fuerte en 07.
Parametros editables y medibles.
Registro obligatorio de recursos.
Release probado.
```

## Decisiones cerradas

```text
No quedan decisiones abiertas para empezar la implementacion de 06.
06 y 07 se implementan casi de corrido.
06 se considera correcto provisionalmente si compila, crea/libera recursos y produce muestras sin excepcion.
La primera validacion visual fuerte de forma ocurre en 07.
No se crea visualizador alternativo de triangulos en 06.
PlanetRecipe incorpora todos los parametros de forma.
No existe una fuente paralela tipo ShapeSettings para definir la identidad del planeta.
VoronoiDivision inicial = 100.
ContinentCells inicial = 84.
La distribucion inicial de direcciones Voronoi replica el perfil funcional previo.
El ruido coherente inicial usa fBm sobre Perlin3D.
Perlin3D inicial es procedural sin texturas, con hash determinista por seed.
PlanetRecipe expone amplitud, frecuencia, octavas, lacunaridad y persistencia del ruido de superficie.
PlanetRecipe expone MountainBiomeCells, MountainBiomeMinPeaks, MountainBiomeMaxPeaks, MountainBiomeHeight, MountainBiomePeakRadius, MountainBiomePeakSpread, MountainBiomeEdgeBlend y MountainBiomePeakFalloff.
PlanetGpuShapeParameters usa 8 float4 y stride de 128 bytes.
PlanetGpuShapeCell usa 2 float4 y stride de 32 bytes.
07 llama a density(point) desde HLSL compartido en su propio kernel.
```

## Criterio de cierre

Este documento queda listo para implementar cuando aceptemos este contrato:

```text
06 define el campo escalar del planeta en GPU.
06 no genera triangulos.
06 no materializa el planeta completo.
06 prepara parametros, celdas Voronoi y density(point).
06 registra y libera recursos GPU.
06 expone una funcion HLSL compartida para 07.
06 puede generar muestras de densidad solo para debug o pruebas pequeñas.
07 es el primer consumidor visual fuerte mediante Marching Cubes.
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
Reparto de detalle y visibilidad quedan reservados para 10 y 11.
```
