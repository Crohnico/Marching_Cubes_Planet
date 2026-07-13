# 12 - Transvoxel Stitching

## Objetivo

Cerrar grietas visuales entre chunks vecinos de distinto LOD dentro de la Base GPU mixta.

Transvoxel no sustituye Marching Cubes. Anade una capa auxiliar de triangulos de transicion
en las caras donde un chunk coarse toca un chunk mas fino.

## Contrato runtime inicial

```text
Shell -> LOD2, sin Transvoxel.
Base  -> ventana mixta LOD0/LOD1/LOD2.
Transvoxel -> solo Base, solo caras con diferencia exacta de 1 LOD.
```

El owner de la transicion es siempre el chunk coarse:

```text
LOD1 genera transicion contra vecino LOD0.
LOD2 genera transicion contra vecino LOD1.
LOD2 contra LOD0 no se cose directamente; requiere anillo LOD1 o se deja pendiente.
```

Motivo:

```text
El algoritmo Transvoxel conecta una resolucion contra exactamente el doble de resolucion.
Permitir saltos LOD0 <-> LOD2 inventaria una transicion 4:1 fuera de las tablas oficiales.
```

## Forma de publicacion

La primera implementacion escribe los triangulos Transvoxel en el mismo slot GPU de Base,
despues del Marching Cubes normal del chunk.

No se crea un buffer por LOD.
No se crea un buffer por chunk.
No se duplica la Base por Transvoxel salvo el doble slot ya existente durante recenter.

## Datos GPU

Las tablas oficiales de `ThirdParty/Transvoxel/Transvoxel.cpp` se convierten a arrays C# en:

```text
Assets/Scripts/Planet/MarchingCubes/Runtime/PlanetTransvoxelLookupTables.cs
```

Se suben como `ComputeBuffer`, igual que `edgeTable` y `triTable` de Marching Cubes.

Cada cara a coser se describe con:

```text
chunk origin coarse
chunkSize coarse
axis
sign
outputChunkIndex
```

## Ancho de transicion inicial

La primera version runtime usa un ancho configurable en celdas coarse:

```text
PlanetDirector.baseTransvoxelWidthCells = 0.5
```

Las posiciones 0..8 viven en la cara full-resolution compartida con el vecino
mas fino. Las posiciones 9/A/B/C viven en una cara desplazada hacia dentro del
chunk coarse.

Esto genera una region de transicion real sin crear buffers por LOD/chunk. La
primera version no retira la capa regular de Marching Cubes: mantener esa capa
evita abrir zanjas visibles mientras la transicion no este soldada de forma
correcta.

Esta ruta deja la malla regular y la transicion coexistiendo. No usa todavia
secondary positions por vertice ni sustituye una capa completa de celdas coarse.

TBD:

```text
Evaluar secondary positions por vertice o una retirada selectiva de geometria
regular que no abra huecos en caras, aristas ni esquinas.
```

## Limitaciones conocidas

```text
Solo se generan transiciones por cara, no por arista/esquina especial.
No hay cache en disco de transiciones.
No se usa para colision.
Si el buffer de Base se queda corto, las transiciones compiten con los vertices normales.
```

## Criterio de exito

```text
Base mixta LOD0/LOD1/LOD2 no muestra grietas grandes en fronteras 2:1.
El frame time de Quest 3 no empeora de forma significativa respecto a Base mixta sin Transvoxel.
La VRAM residente no vuelve al modelo de planeta completo en multiples LODs.
```
