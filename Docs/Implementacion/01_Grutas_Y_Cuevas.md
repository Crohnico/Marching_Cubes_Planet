# Grutas y cuevas

## Objetivo

Crear un interior planetario determinista, organico y explorable en el que las
grutas sean un espacio principal de juego y no una coleccion de salas conectadas
por pasillos reconocibles.

El jugador debe poder descender aprovechando redes naturales durante periodos
largos. Excavar sirve principalmente para comunicar redes, abrir atajos y acceder
a recursos, no como unica forma de locomocion subterranea.

El campo debe producir combinaciones como:

```text
grandes cavernas irregulares
galerias nudosas y ramificadas
gargantas y pasos estrechos
fallas y laminas erosionadas
columnas y puentes de roca residual
entradas naturales a superficie
redes que atraviesan cualquier profundidad
```

## Parada de decision tecnica

```text
Objetivo real:
Hacer de las cuevas un espacio principal de exploracion, con continuidad y
variedad suficientes para soportar el descenso como bucle jugable.

Restriccion dura:
Meta Quest 3, VRAM fija, evaluacion local por chunk, determinismo global y ningun
volumen planetario precalculado.

Solucion elegida:
Campo continuo de ruido Simplex 3D deformado en dominio. Se combinan una familia
de cavernas, una familia de galerias formada por la interseccion de dos bandas de
ruido y una familia de fracturas.

Coste esperado:
Numero fijo y acotado de evaluaciones de ruido por sample. No se recorren grafos,
nodos ni listas de primitivas cercanas y no existe coste dependiente del tamano
total del planeta.

Riesgo:
Demasiada geometria de alta frecuencia, cavidades desconectadas, aliasing entre
LOD o saturacion del buffer agregado de vertices.

Por que esta solucion y no otra:
Un grafo implicito de nodos, capsulas y elipsoides revela una gramatica de
habitaciones y pasillos aunque se deforme. Un unico umbral de ruido genera queso
suizo. La composicion de varias familias continuas permite topologia organica sin
materializar una red global.

Que se medira para validarla:
GPU ms por chunk, vertices generados, overflow, VRAM residente, continuidad entre
chunks/LOD, porcentaje conectado del vacio y profundidad recorrible.
```

Esta decision sustituye el planteamiento anterior basado en grafo/SDF. Las
primitivas `Tunnel`, `Chamber`, `Fault`, `Shaft` y `Column` dejan de ser la base
del sistema.

## Sistema propio dentro de PlanetRecipe

Las cuevas no son una `Material Layer`.

```text
Planet Recipe
|- Shape / Terrain
|- Composition / Material Layers
|- Cave System
|- Water
`- Atmosphere
```

`Air` no es una sustancia ni un material. El sistema de cuevas modifica el campo
de densidad antes de Marching Cubes. La composicion solo asigna sustancia al
solido que permanece.

```text
forma base del planeta
-> Cave System modifica density(point)
-> composicion asigna sustancia al solido restante
-> Marching Cubes extrae superficies exteriores e interiores
```

## Campo continuo global

La topologia se evalua directamente desde:

```text
seed del planeta
seed offset de cuevas
coordenada global de grid
parametros de Cave System
```

No depende del chunk que realiza la consulta:

```text
misma receta + misma seed + misma coordenada -> misma densidad
```

Esto garantiza continuidad entre chunks sin almacenar nodos, descriptores ni un
buffer global de cuevas.

### Deformacion de dominio

Primero se calcula un desplazamiento 3D de baja frecuencia y se aplica a la
posicion de muestreo:

```text
warp = simplex3D(position / warpScale, seeds independientes XYZ)
warpedPosition = position + warp * Tortuosity
```

Las tres componentes usan seeds diferentes. La deformacion rompe ejes y evita
que las formaciones se lean como bandas o tubos matematicos.

### Cavernas

Las cavernas usan Simplex de baja frecuencia con un segundo nivel de detalle
compartiendo la posicion deformada:

```text
cavernField = simplex(warped / CavernScale)
             + detalle de menor amplitud
```

`Porosity` y `Cavern Abundance` desplazan el umbral. El resultado no representa
una camara individual: es una region continua capaz de formar salas, recovecos,
puentes y masas de roca residuales.

### Galerias

Una unica banda cercana al cero de un ruido forma laminas. La interseccion de dos
bandas independientes forma curvas volumetricas semejantes a raices:

```text
passageField = max(
    abs(simplexA(warped / PassageScale)) - width,
    abs(simplexB(warped / PassageScale)) - width)
```

El interior aparece donde ambas expresiones son negativas. `Connectivity`
controla el ancho de la banda y, por tanto, la facilidad con la que estas galerias
se cruzan y conectan con cavernas.

### Fracturas

Las fracturas conservan una sola banda de ruido a una escala intermedia:

```text
fractureField = abs(simplex(warped / fractureScale)) - width
```

Generan grietas, gargantas y laminas erosionadas. Al unirse con cavernas y
galerias rompen su silueta y crean conexiones secundarias.

### Union y detalle de pared

Las familias se unen antes de excavar:

```text
caveField = min(cavernField, passageField, fractureField)
finalDensity = min(baseDensity, caveField)
```

`Wall Detail` solo añade una perturbacion de alta frecuencia cerca de la frontera
del vacio. No decide la topologia y no se paga lejos de una pared candidata.

Las columnas, arcos y puentes no se añaden como objetos. Son el solido que queda
entre campos de erosion solapados.

## Parametros de receta

La seccion `Cave System` expone:

```text
Enabled

Min Appearance          0..255
Max Appearance          0..255

Porosity                0..100%
Connectivity            0..100%

Cavern Scale            GridCoordinates en LOD1
Passage Scale           GridCoordinates en LOD1
Tortuosity              0..100%

Cavern Abundance        0..100%
Passage Abundance       0..100%
Fracture Abundance      0..100%

Entrance Abundance      0..100%
Wall Detail             0..100%
Seed Offset
```

Semantica:

```text
Min/Max Appearance -> rango radial: 0 centro, 255 extremo exterior.
Porosity            -> cantidad global de roca candidata a erosionarse.
Connectivity        -> anchura y percolacion de la red de galerias.
Cavern Scale        -> escala de las grandes masas vacias.
Passage Scale       -> escala de la red nudosa de pasos.
Tortuosity          -> intensidad de la deformacion de dominio.
Abundance           -> peso de cada familia en la union.
Entrance Abundance  -> resistencia gradual de la banda superficial.
Wall Detail         -> detalle local de frontera, no topologia.
```

`Porosity = 0` produce un planeta sin cuevas aunque las abundancias sean mayores
que cero. Una abundancia a cero desactiva funcionalmente su familia.

Las recetas de la version anterior conservan `Enabled`, el rango de aparicion y
`Seed Offset`, pero reciben los valores morfologicos por defecto del campo de
ruido. Los antiguos radios y abundancias de primitivas no se reinterpretan porque
no tienen una equivalencia tecnica honesta en el nuevo algoritmo.

## Entradas a superficie

El mismo campo interior llega hasta la superficie. No se colocan bocas ni se
seleccionan sectores especiales.

Cerca de la piel exterior se suma una resistencia gradual al campo de cueva. Las
ramas mas fuertes pueden atravesarla y las debiles quedan enterradas:

```text
surfaceWeight = 1 - saturate(depth / surfaceBand)
caveField += surfaceWeight * resistance(EntranceAbundance)
```

Con abundancia alta aparecen mas bocas, simas y cortes abiertos. Con abundancia
baja la mayor parte de la red queda enterrada, pero la topologia interior no
cambia de algoritmo.

## Shell, Base y LOD

La Shell no evalua interiores. Desde lejos solo necesita la superficie exterior.

La Base/local evalua el campo completo. Todos los LOD consultan la misma funcion
en coordenadas globales; las escalas se convierten para conservar su tamano en
mundo.

Cuando las cuevas estan activas, la Base combina los chunks superficiales con una
ventana volumetrica 3D alrededor del foco:

```text
fuera de cueva -> superficie cercana + volumen local candidato
dentro de cueva -> volumen local centrado en jugador + banda de transicion
```

La ventana respeta `baseOctreeMaxChunks`, los anillos LOD y el doble slot. El
interior completo nunca se hace residente.

## Geometria, buffer y visibilidad

Una topologia mas porosa convierte mas chunks en mixtos y puede aumentar los
vertices aunque la funcion de densidad no almacene datos.

El buffer Base mantiene un limite duro. No se amplia automaticamente para ocultar
un problema de contenido. Deben registrarse como minimo:

```text
vertices intentados y escritos
overflow
chunks solidos / aire / mixtos
GPU ms de clasificacion y extraccion
VRAM de slots activos
```

La oclusion no evita un overflow si la geometria ya se genero. La evolucion
correcta del streaming interior queda definida asi:

```text
clasificacion coarse: solido / aire / mixto
-> prioridad y presupuesto local
-> Marching Cubes solo para candidatos
-> frustum y oclusion jerarquica por chunks
```

La clasificacion previa y la oclusion interior avanzada quedan `TBD` despues de
medir el vertical slice. No se incrementara VRAM antes de disponer de esas
medidas.

## Profundidad extrema

No existe un nucleo geometrico artificialmente solido. El campo puede actuar en
cualquier profundidad, incluido el centro. El acceso se limitara mediante
mecanicas como temperatura, que pertenecen a su futuro sistema y quedan `TBD`.

## Restricciones

```text
No volumen global precalculado.
No buffer global de cuevas.
No grafo de nodos como topologia principal.
No dependencia de CPU readback para decidir la forma.
Evaluacion por coordenada global y chunk local.
Coste de ruido fijo y acotado por sample.
Shell ligera y sin interiores.
Base/local con ventana volumetrica.
Sin Air dentro de Material Layers.
Sin nucleo geometrico artificial.
Sin ampliar VRAM como primera respuesta.
```

## Validacion

Pruebas automatizables:

```text
Porosity 0 no modifica densidad.
La misma seed y posicion producen la misma densidad.
Cambiar seed modifica el campo.
Cada familia puede excavar de forma independiente.
Los parametros se empaquetan sin perder precision.
El escalado LOD conserva escalas en mundo.
Shell desactiva cuevas y Base las activa.
El shader compila sin errores ni overflow en muestras controladas.
```

Validacion visual y de gameplay:

```text
No reconocer un patron repetido de sala + pasillo.
Encontrar cavernas, redes nudosas, fracturas y gargantas en la misma receta.
Recorrer varios chunks sin cortes en fronteras.
Comprobar entradas naturales sin perforar toda la superficie.
Medir componentes conectados y profundidad alcanzable.
Medir GPU, vertices y memoria en Quest 3.
```

## Pendiente despues del vertical slice

```text
Clasificacion coarse previa a Marching Cubes.
Oclusion interior jerarquica y priorizacion por visibilidad.
Herramienta de analisis de componentes conectados.
Presets Sparse, Network, Caverns, Fractured, Hollow y Custom.
Integracion con calor, gameplay, colisiones y terraformado.
Persistencia de modificaciones realizadas dentro de cuevas.
```
