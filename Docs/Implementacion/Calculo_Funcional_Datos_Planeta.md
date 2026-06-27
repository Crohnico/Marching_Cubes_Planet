# Calculo funcional del planeta

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

Este documento describe la teoria de como se calcula la forma del planeta. No describe arquitectura, persistencia, render, objetos de escena, rutas de archivos ni clases concretas del proyecto.

## Idea central

El planeta no se guarda como una esfera modelada a mano. Se define como un campo escalar determinista:

```text
density(worldPosition) = radioDelPlaneta + desplazamientoDeSuperficie - distanciaAlCentro
```

Luego se muestrea ese campo en una rejilla voxel y se extrae la superficie con Marching Cubes.

La regla de ocupacion es:

```text
density > isoLevel => solido
density <= isoLevel => aire
```

El `isoLevel` actual es `0`.

Por eso la superficie visible conceptual del planeta aparece donde:

```text
radioDelPlaneta + desplazamientoDeSuperficie == distanciaAlCentro
```

El aspecto que gusta sale principalmente de `desplazamientoDeSuperficie`: continentes por Voronoi esferico, oceanos hundidos, ruido fino de relieve y una clasificacion por altura/capa.

## Campo escalar

La base es un campo escalar de densidad:

```text
density = radius + surfaceOffset - distance(position, center)
```

Donde:

- `center` es el centro del planeta.
- `radius` es el radio base.
- `distance(position, center)` es la distancia del punto al centro.
- `surfaceOffset` es cuanto sube o baja la superficie en esa direccion.

La regla es:

```text
density > 0  => solido
density <= 0 => aire
```

Si no hay deformacion:

```text
surfaceOffset = 0
density = radius - distance(position, center)
```

Eso produce una esfera perfecta.

Si hay deformacion, el radio efectivo cambia por direccion:

```text
effectiveRadius(direction) = radius + surfaceOffset(direction)
```

La superficie real aparece donde:

```text
distance(position, center) == effectiveRadius(direction)
```

Esto permite que el planeta tenga silueta irregular, masas de tierra, cuencas oceanicas y relieve sin guardar una malla fija como fuente de verdad.

## Direccion radial

Para calcular la deformacion de un punto, primero se convierte su posicion en una direccion desde el centro:

```text
direction = normalize(position - center)
```

Esa direccion representa "en que punto de la esfera" estamos mirando.

El truco importante es que el continente no se calcula como ruido plano en `x/y/z`, sino sobre direcciones de una esfera. Asi las formas continentales envuelven el planeta de manera natural.

## Voronoi esferico

La forma continental principal sale de un Voronoi distribuido sobre la esfera.

Proceso:

1. Se generan muchos puntos/direcciones repartidos por la esfera.
2. Para una direccion dada, se buscan las dos celdas Voronoi mas cercanas.
3. La cercania se mide con producto punto:

```text
proximity = dot(direction, cellDirection)
```

Cuanto mayor es el producto punto, mas alineada esta la direccion con esa celda.

Se conserva:

```text
nearestCell
secondNearestCell
nearestDot
secondNearestDot
```

La celda mas cercana decide el tipo principal de esa zona: tierra u oceano.

## Seleccion determinista de continentes

Algunas celdas Voronoi se marcan como tierra y otras como oceano.

La seleccion es determinista con una semilla:

```text
landCellCount = deterministicRange(seed, minLandCells, maxLandCells)
```

Luego se eligen `landCellCount` celdas como tierra usando la misma semilla.

La proporcion entre celdas de tierra y oceano controla el caracter general del planeta: mas celdas de tierra producen masas continentales dominantes; menos celdas de tierra producen un planeta mas oceanico.

## Elevacion de tierra

Cada celda de tierra recibe una altura base.

La altura se calcula de forma determinista:

```text
t = random01(seed, cellIndex)
t = elevationCurve(t)
landElevation = lerp(minElevation, maxElevation, t)
```

Despues se aplica un modificador por celda:

```text
heightModifier = randomRange(seed, cellIndex, minHeightModifier, maxHeightModifier)
landOffset = radius * landElevation * heightModifier
```

En la version que nos gusta:

```text
minElevation = 1.5% del radio
maxElevation = 12.7% del radio
heightModifier = 0.3..1.5
```

Esto hace que dos regiones de tierra no tengan la misma altura. Algunas quedan como plataformas bajas, otras suben mucho mas, y eso ayuda a que el planeta no parezca una esfera con manchas suaves.

## Oceanos

Las celdas que no son tierra reciben una altura negativa:

```text
oceanOffset = -radius * oceanDepth
```

En la version que nos gusta:

```text
oceanDepth = 16% del radio
minimumOceanDepth = 3% del radio
```

Ademas, si una celda es oceano, se fuerza una profundidad minima:

```text
offset <= -radius * minimumOceanDepth
```

Esto evita que las zonas oceanicas queden casi al mismo nivel que la tierra. La separacion entre continente y cuenca queda clara.

## Mezcla de bordes continentales

Si solo se usara la celda Voronoi mas cercana, las fronteras entre regiones serian demasiado duras.

Para suavizarlas se compara la cercania de la celda mas cercana y la segunda mas cercana:

```text
dotDelta = nearestDot - secondNearestDot
rawBlend = saturate(dotDelta / continentEdgeBlend)
interiorBlend = blendCurve(rawBlend)
```

Cuando `dotDelta` es pequeno, estamos cerca de una frontera. Cuando es grande, estamos dentro de una celda.

El offset se mezcla asi:

```text
nearestOffset = offset de la celda mas cercana
secondOffset = offset de la segunda celda
boundaryOffset = (nearestOffset + secondOffset) * 0.5
offset = lerp(boundaryOffset, nearestOffset, interiorBlend)
```

En la version que nos gusta:

```text
continentEdgeBlend = 0.16
```

Esto crea transiciones continentales mas organicas. Las costas y cambios de altura no son cortes completamente matematicos.

## Ruido fino de superficie

Despues de calcular el offset continental/oceanico, se suma ruido 3D coherente.

La idea es:

```text
localPosition = position - center
normalizedPosition = localPosition / radius
noiseValue = coherentNoise(normalizedPosition * frequency * roughnessModifier)
offset += noiseValue * radius * amplitude
```

El modificador de rugosidad tambien cambia por celda Voronoi:

```text
roughnessModifier = randomRange(seed, cellIndex, minRoughness, maxRoughness)
```

En la version que nos gusta:

```text
amplitude = 30.8% del radio
frequency = 7
roughnessModifier = 0.6..0.8
```

Este valor de amplitud es fuerte. Es una de las claves del aspecto: rompe bastante la esfera, crea montanas y crestas visibles, y hace que las masas de tierra no parezcan simples parches lisos.

Si en un reinicio se baja mucho esta amplitud, el planeta sera mas limpio y controlable, pero probablemente perdera parte del caracter visual actual.

## Offset final de superficie

La deformacion final puede entenderse asi:

```text
baseOffset = offset continental u oceanico mezclado por Voronoi
fineOffset = ruido coherente 3D
surfaceOffset = baseOffset + fineOffset
```

Y por tanto:

```text
effectiveRadius(direction) = radius + surfaceOffset
```

La forma del planeta es el conjunto de puntos cuya distancia al centro coincide con ese radio efectivo.

## Extraccion de superficie

Una vez existe el campo escalar, la superficie se puede extraer muestreando el espacio en celdas.

Cada celda evalua sus 8 esquinas:

```text
cornerSolid = density(cornerPosition) > 0
```

Con esas 8 respuestas se forma una mascara:

```text
corners = 8 bits de ocupacion
```

Reglas:

```text
corners = 0   => todo aire
corners = 255 => todo solido
otro valor    => la superficie cruza la celda
```

Para los casos parciales se aplica Marching Cubes. Marching Cubes convierte la mascara de esquinas en triangulos que aproximan donde la densidad cruza el nivel `0`.

En terminos funcionales:

```text
Campo escalar -> muestras de celdas -> mascara de ocupacion -> triangulos
```

## Interpolacion de vertices

Cuando una arista de la celda tiene una esquina solida y otra de aire, la superficie cruza esa arista.

El punto exacto se interpola usando los valores de densidad:

```text
t = (0 - valueA) / (valueB - valueA)
vertex = lerp(positionA, positionB, t)
```

Asi la malla no queda limitada a las esquinas de la rejilla. La rejilla decide donde buscar, pero la superficie puede caer en posiciones intermedias.

## Normales

La normal de cada triangulo se calcula a partir de sus vertices:

```text
normal = normalize(cross(b - a, c - a))
```

Esto da una normal geometrica basica. No es una normal analitica del campo escalar, pero sirve para representar la superficie generada por los triangulos.

## Capas semanticas de la malla

Esto no define la forma, pero puede ser util conservarlo como dato.

Cada punto puede clasificarse por profundidad respecto a la superficie:

```text
depth = radius + surfaceOffset - distance(position, center)
```

La idea es separar:

```text
surface    => zona exterior/cerca de la piel del planeta
transition => zona intermedia
interior   => zona profunda
```

En la version actual se usaba una separacion equivalente a:

```text
depth <= 64  => surface
depth <= 192 => transition
depth > 192  => interior
```

Esta informacion puede servir despues para materiales, excavacion, lectura de terreno o reglas jugables. No hace falta que dependa del render.

## Altura normalizada

Tambien se puede derivar un valor de altura para colorear o clasificar la superficie mas adelante.

La altura radial relativa es:

```text
heightOffset = distance(position, center) - radius
```

Si `heightOffset` es negativo, el punto esta por debajo del radio base. Si es positivo, esta por encima.

Conceptualmente se puede separar:

```text
underwater01 => profundidad normalizada bajo el nivel base
landHeight01 => altura normalizada sobre el nivel base
```

En la version que nos gusta, el corte visual entre agua y tierra estaba alrededor de:

```text
seaLevel = 0.337 dentro del gradiente de altura
```

Lo importante no es el atlas ni el material usado, sino conservar el dato de altura relativa y si el punto pertenece a tierra, oceano o transicion.

## Resumen

El planeta se calcula como una esfera de densidad cuyo radio cambia segun la direccion.

La deformacion principal sale de Voronoi esferico:

- unas celdas son tierra y suben el radio;
- otras son oceano y lo hunden;
- los bordes se mezclan con la segunda celda mas cercana;
- encima se suma ruido 3D coherente para romper la suavidad.

Luego se muestrea ese campo en celdas y Marching Cubes extrae la superficie.

La parte visual que conviene recordar no es una clase, una cache o una ruta de datos. Es esta funcion:

```text
effectiveRadius(direction) =
    radius
    + voronoiContinentOffset(direction)
    + coherentSurfaceNoise(direction)
```

Todo lo demas puede cambiar.
