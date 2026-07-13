# Estado actual del proyecto

Este documento sustituye a los documentos funcionales cerrados de los pasos 01 a 12.

La documentacion historica de esos pasos se ha retirado para evitar que el siguiente bloque
arrastre decisiones viejas, rutas de validacion obsoletas o nombres que ya no representan el
runtime actual.

La definicion tecnica general sigue siendo:

```text
Docs/Definicion_Tecnica_Proyecto.md
```

Ese documento no se modifica salvo peticion explicita.

## Objetivo vigente

Construir un planeta procedural usable en Meta Quest 3, con generacion visual GPU-resident,
memoria controlada y sin Garbage Collector accidental en runtime caliente.

El planeta no es una mesh guardada ni un volumen completo en memoria.

La fuente de verdad sigue siendo:

```text
PlanetRecipe + density(point)
```

La representacion visible se genera cuando hace falta.

## Estado tecnico resumido

El bloque 01-12 deja cerrada esta base:

```text
Receta procedural
-> forma GPU
-> Marching Cubes GPU cartesiano
-> render GPU-resident
-> Shell LOD2
-> Base local mixta con ventana octree
-> agua temporal por esfera
-> Transvoxel inicial experimental
```

El runtime visual caliente ya no depende de publicar `Mesh` CPU para terreno.

La direccion vigente es:

```text
CPU ordena trabajo
GPU evalua density(point)
GPU genera vertices Marching Cubes
GPU escribe indirect args
CPU dibuja con DrawProceduralIndirect
```

## Coordenadas y receta

Decisiones vigentes:

```text
GridCoordinates -> espacio logico del planeta.
WorldSpaceCoordinates -> espacio Unity local.
WorldScale -> escala visual aplicada al grid.
WorldRadius -> derivado, no fuente de verdad.
PlanetRecipe -> define como es el planeta.
PlanetPlacement -> define donde y como se coloca.
```

Reglas:

```text
El terreno se evalua en coordenadas locales/grid del planeta.
No se evalua density(point) directamente en WorldSpace.
Mover o rotar el GameObject del planeta actualiza placement, matriz y bounds.
Mover o rotar no recalcula la geometria ya cocinada.
```

## Forma procedural

La forma vive en GPU y se comparte por HLSL:

```text
Assets/Shaders/Resources/Compute/PlanetShapeDensity.hlsl
```

La formula conceptual sigue siendo:

```text
density(point) = radius + surfaceOffset(point) - distance(point, center)
```

Componentes principales de `surfaceOffset`:

```text
Voronoi esferico.
Seleccion determinista tierra/oceano.
Elevacion por celda.
Profundidad oceanica.
Mezcla de bordes continentales.
Biomas Meadow/Mountain.
Ruido Perlin/fBm de detalle.
```

La receta conserva parametros de radio, escala, seed, isoLevel, Voronoi, ruido,
oceano, continente y montanas.

## Laminas de composicion

La siguiente evolucion del calculo del terreno se documenta en:

```text
Docs/Implementacion/04_Laminas_Composicion_Terreno.md
```

La idea vigente es no crear caminos de render especiales para cuevas, minerales o
modificadores. En su lugar, el terreno local se calculara como composicion de
laminas:

```text
Lamina Superficie
Lamina Cavidades
Laminas de minerales / sustancias / modificaciones futuras
```

El render GPU-resident actual debe seguir consumiendo chunks renderizables como
hasta ahora. Las laminas modifican el campo compuesto que se entrega a Marching
Cubes, no el contrato de render.

## Marching Cubes

La ruta canonica es cartesiana:

```text
Chunk canonico = 64 x 64 x 64 celdas.
Micro cell logica = 1 x 1 x 1 en GridCoordinates.
Cada celda evalua 8 esquinas reales.
Marching Cubes usa edgeTable/triTable estandar.
```

El shader principal es:

```text
Assets/Shaders/Resources/Compute/PlanetMarchingCubes.compute
```

El kernel normal:

```text
CS_ExtractChunkedCartesianSurface
```

Reglas:

```text
Marching Cubes no reimplementa la forma.
Incluye PlanetShapeDensity.hlsl.
No usa cubemap, shell radial ni esfera parametrica.
Los vertices salen no indexados.
El stride de vertice visible es 32 bytes.
```

## PlanetGrid

`PlanetGrid` queda como mapa de informacion:

```text
PlanetGridCoordinates -> uint
0 = sin informacion
1 = chunk confirmado con al menos 1 triangulo
```

Reglas:

```text
No es una lista.
No se consulta por indice como autoridad logica.
Un chunk vacio nunca se marca con 1.
Las coordenadas son coordenadas de chunk canonico compartidas entre LODs.
```

Para iteracion manual o carga secuencial puede existir una vista ordenada de las celdas
confirmadas, pero esa vista no cambia la semantica del grid.

## Render visual GPU-resident

`PlanetGpuMarchingCubesSurface` es la ruta visual actual.

Responsabilidades:

```text
Gestionar buffers GPU de shell y base.
Mantener shape evaluator reutilizable.
Generar chunks en buffers agregados.
Dibujar con DrawProceduralIndirect.
Actualizar placement/bounds sin recalcular geometria.
Mantener doble slot para recargas de Base.
```

La ruta visual caliente no debe hacer:

```text
Readback de vertices.
Mesh.SetVertices/SetTriangles para terreno visible.
Recrear GameObjects por chunk.
Liberar y reservar buffers por chunk.
```

## Shell y Base

`PlanetDirector` es el orquestador runtime vigente del planeta activo.

Estado inicial:

```text
Start:
  sincroniza placement desde transform.
  obtiene PlanetGpuMarchingCubesSurface.
  genera PlanetGrid.
  espera una breve ventana de arranque.
  carga Shell LOD2.
```

Estado al entrar en rango:

```text
Mantiene Shell visible.
Carga Base.
Cuando Base termina, libera Shell.
Marca isSetUp = true.
```

Estado al salir de rango:

```text
Vuelve a cargar Shell LOD2.
Libera Base cuando Shell ya esta lista.
```

Si el jugador sale mientras Base carga:

```text
Cancela cola.
Libera Base parcial.
Conserva Shell.
```

## Base actual

La Base ya no significa "todo el planeta completo en LOD0".

La Base vigente usa una ventana octree inicial:

```text
Ordena chunks confirmados por distancia al foco del player.
Recorta por baseOctreeMaxChunks.
Asigna LOD0 al radio cercano.
Asigna LOD1 al anillo medio.
Asigna LOD2 a extremos.
Genera todo dentro de un slot agregado de Base.
```

La Base se recalcula cuando el foco del jugador se mueve lo suficiente:

```text
baseRebuildDistanceChunks
```

Para evitar parpadeo:

```text
La recarga se cocina en slot trasero.
La Base anterior sigue visible.
Al terminar la cola se publica el nuevo slot.
El slot anterior se libera despues.
```

La primera Base tambien se cocina oculta mientras Shell sigue visible.

## LOD dinamico

El gestor de LOD por GameObjects y UID queda retirado.

No existe como ruta vigente:

```text
ChunkLODGestor.
LODGestor en escena.
Cola runtime por UID para cambiar chunks sueltos.
GameObject fisico por chunk para decidir LOD.
```

Motivo:

```text
No reducia memoria visible de forma clara.
Acumulaba estados y buffers.
No sustituyo contenido de forma limpia.
```

La decision actual:

```text
Mantener Shell LOD2.
Mantener Base como ventana activa.
Recentrar/recalcular Base completa de forma presupuestada.
Mas adelante, estudiar sustitucion fina por segmentos si aporta memoria real.
```

## Agua

El agua actual es temporal.

`PlanetDirector` crea una esfera hija:

```text
Ocean
```

Usa:

```text
Resources/PlanetOcean
```

Decisiones:

```text
La esfera de agua no participa en Marching Cubes.
No es el sistema final de oceano.
No tiene colision.
No representa sustancias ni volumen.
Sirve para visualizacion y presentacion del planeta actual.
```

La malla de agua se genera como UV sphere runtime con mas poligonaje que la primitive
basica de Unity.

## Transvoxel

Hay una primera integracion experimental de Transvoxel.

Datos:

```text
Las tablas oficiales viven en ThirdParty/Transvoxel y Docs/Referencias/Transvoxel.
La copia C# generada vive en PlanetTransvoxelLookupTables.
Las caras a coser se describen con PlanetTransvoxelFaceDescriptor.
```

Contrato actual:

```text
Shell no usa Transvoxel.
Base puede generar transiciones en caras 2:1.
El owner de la transicion es el chunk coarse.
LOD1 cose contra LOD0.
LOD2 cose contra LOD1.
LOD2 contra LOD0 no se cose directamente.
```

Estado real:

```text
Transvoxel esta cerca, pero no esta cerrado.
La transicion se dibuja como capa adicional.
No se retira la capa regular de Marching Cubes.
Retirar la capa regular abria zanjas visibles.
```

Por ahora se acepta:

```text
Puede haber solapes o costuras imperfectas.
No se considera solucion final.
```

Pendiente:

```text
Soldado correcto con secondary positions o retirada selectiva sin huecos.
Casos de aristas/esquinas.
Medicion en Quest 3.
```

## Memoria y rendimiento

Reglas vigentes:

```text
Quest 3 manda.
Evitar GC accidental en runtime caliente.
Reutilizar buffers.
No reservar/liberar buffers grandes por chunk.
No hacer GPU -> CPU -> Mesh -> GPU para terreno visual caliente.
No aumentar memoria para resolver LOD si no reduce memoria real.
```

Decisiones practicas tomadas:

```text
Shape evaluator reutilizable.
Buffers persistentes mientras vive la surface.
Budgets por LOD para chunks individuales.
Shell mantiene su presupuesto propio.
Base usa presupuesto recortado y ventana activa.
Doble slot de Base solo durante recarga.
```

## Player y pruebas VR

La prueba actual usa:

```text
PlanetMinimalXrRig.
DebugMinimalLocomotion.
Canvas/rayos de Lab.
```

El rig es tecnico, no gameplay final.

La locomocion vigente de Lab permite inspeccionar el planeta en Quest Link/Quest 3.

## Assets runtime importantes

Los compute shaders usados en runtime deben estar bajo:

```text
Assets/Shaders/Resources/Compute
```

Motivo:

```text
En Quest/Android no se puede depender de AssetDatabase.
```

El shader visual GPU debe estar disponible en build:

```text
MarchingCubesPlanet/Planet/SurfaceGpu
```

Regla:

```text
No asumir que Shader.Find encontrara un shader strippeado.
Debe estar referenciado o incluido.
```

## Documentos conservados

Se conservan:

```text
Docs/Definicion_Tecnica_Proyecto.md
Docs/Teoria_Implementacion.md
Docs/Implementacion/Calculo_Funcional_Datos_Planeta.md
Docs/Implementacion/Estado_Actual_Proyecto.md
Docs/Referencias/*
Docs/Test/*
```

Los pasos 01-12 quedan cerrados y retirados como documentos vivos.

## Estado abierto para el siguiente bloque

Pendientes tecnicos principales:

```text
Cerrar Transvoxel de verdad o decidir abandonarlo si no aporta suficiente.
Definir el siguiente sistema antes de tocar codigo.
Definir ruta de chunks locales/interactuables.
Definir colision local real.
Definir sustancias/material logico si entra.
Definir terraformado/persistencia si toca.
Definir oclusion/visibilidad real si se ataca antes que interaccion.
Medir en Quest 3 cada cambio grande.
```

Regla para lo siguiente:

```text
Antes de escribir codigo nuevo, crear o actualizar el documento especifico del siguiente sistema.
Ese documento debe partir de este resumen, no de los pasos 01-12 retirados.
```
