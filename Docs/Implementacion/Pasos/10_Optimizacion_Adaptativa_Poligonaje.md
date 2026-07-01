# 10 - Optimizacion adaptativa de poligonaje

## Objetivo

Definir el sistema que decide que zonas del planeta se generan con mas o menos resolucion dentro de la representacion virtual deseada del planeta.

10 es independiente de 09.

10 trabaja sobre la forma virtual del planeta y toma el control de como se divide, genera y publica la superficie adaptable.

Cuando el jugador entra en el area de actividad de un planeta, 10 es el sistema que activa y mantiene su shell de representacion dinamica:

```text
Planeta activo
-> calcula resolucion deseada por distancia, mirada, movimiento y estado.
-> genera o actualiza paginas/shells desde density(point).
-> cose LODs con Transvoxel o transicion equivalente.
-> publica lotes identificados por meshId hacia 09.
```

09 queda tratado como backend de publicacion/dibujo:

```text
10 decide su reparto.
10 genera o prepara paginas.
10 publica lotes de triangulos con meshId estable y priorityScore.
09, por detras, decide si los pinta, los reclama, los sobrescribe o los descarta.
10 no cambia su reparto por una respuesta de 09.
```

Contrato funcional:

```text
density(point) de 06
-> extraccion/triangulacion de 07 adaptada por pagina
-> reglas de pintado/formato de 08 aplicadas por pagina
-> paginas LOD alrededor del player/camara
-> Marching Cubes multiresolucion por pagina
-> costura entre LODs con Transvoxel o transicion equivalente
-> Draw/SetMesh/publicacion con meshId hacia 09 como backend gestionado
```

Decision central:

```text
10 no se disena como BVH de triangulos.
10 se disena como generacion adaptativa por paginas/chunks LOD sobre el campo density(point).
10 no recibe autorizacion ni presupuesto de 09 para decidir su reparto.
10 es quien calcula que parte del planeta esta en vision/interes para el planeta activo.
```

El nombre de archivo conserva `BVH` por continuidad con el indice historico, pero el contrato tecnico de este documento elimina BVH como arquitectura principal.

## Modelo mental

El problema que resuelve 10 no es buscar rapido triangulos ya generados.

El problema real es:

```text
No cabe generar el planeta completo a resolucion cercana.
Hay que decidir que zonas muestrear antes de ejecutar Marching Cubes.
```

El planeta sigue siendo una receta procedural y un campo escalar:

```text
density(point)
```

10 decide donde consultar ese campo con mas densidad de muestras.

Ejemplo conceptual:

```text
Muy cerca del player      -> paginas pequenas, sampleStepGrid bajo, mucho detalle.
Direccion de mirada       -> mas prioridad.
Direccion de movimiento   -> precarga/lookahead.
Media distancia           -> paginas medianas, sampleStepGrid medio.
Lejos                     -> paginas grandes, sampleStepGrid alto.
Muy lejos                 -> proxy/impostor futuro, no terreno local denso.
```

La malla no se optimiza despues de existir.

La malla se evita o se genera con menor resolucion desde el principio.

Regla:

```text
10 no debe construir un planeta 400-20 completo para luego simplificarlo.
10 debe generar solo las paginas que importan con la resolucion que toca.
```

## Observaciones de pruebas actuales

Estas cifras son observaciones utiles para calibrar, no contratos finales:

```text
Nota de escala:
- En estas observaciones, "grid" describe el perfil/resolucion del shell de representacion usado en la prueba.
- No redefine la micro cell logica 1x1x1 ni rompe `WorldRadius = GridRadius * WorldScale`.
- Como el isoLevel actual es 0 y la superficie visible util aparece alrededor del corte del campo, se habla de diametro visible practico de ~8k.
- El objetivo visual es mantener un planeta legible de ~8k de diametro visible util mientras cambia la resolucion del shell.

Shell minimo / lejano:
- grid/profile: 20.
- world scale: 400.
- diametro visible practico objetivo: ~8k.
- salida observada: ~27k tris.
- parece apropiado para muy lejos o para base de impostor/proxy.

Shell intermedio de validacion:
- grid/profile: 130.
- world scale: 61.5.
- diametro visible practico objetivo: ~8k.
- salida observada: ~2.7M tris.
- cabe en el buffer/socket temporal actual.
- tarda poco para validacion.
- parece adecuado como lectura de planeta completo a distancia, pero no para cercania extrema.

Shell maxima resolucion local:
- grid/profile: 125.
- world scale: 64.
- diametro visible practico objetivo: ~8k.
- cabe en el buffer temporal.
- va bien de rendimiento en pruebas actuales.
- debe usarse como resolucion local/paginas activas, no como obligacion de planeta completo permanente.

Referencia anterior de alta resolucion:
- planeta 400 grid / 20 scale mostraba resolucion visual deseable cerca.
- no cabe como planeta completo en el socket temporal.
- solo debe existir como resolucion local por paginas, no como planeta entero.
```

Lectura:

```text
El sistema debe combinar resolucion lejana barata con resolucion cercana local.
El objetivo no es encontrar un unico grid global.
El objetivo es tener varios niveles de detalle activos a la vez.
```

## Alcance de esta fase

Entra:

```text
Estructura de paginas/chunks LOD para superficie planetaria.
Seleccion por distancia al player.
Seleccion por direccion de mirada.
Seleccion por direccion de movimiento/lookahead.
Prioridad por tamano aparente aproximado.
Generacion bajo demanda de paginas cercanas.
Degradacion de paginas lejanas.
Marching Cubes con sampleStepGrid variable por pagina.
Costura entre paginas de distinto LOD con Transvoxel o transicion equivalente.
Cache/pool de paginas LOD con capacidad maxima.
Cancelacion de paginas que dejan de ser prioritarias.
Publicacion de triangulos generados mediante la ruta de pintado/dibujo gestionada por 09.
Metricas de paginas deseadas, generadas, canceladas, degradadas y enviadas a publicacion.
Stress con diferentes calidades internas de 10 y diferentes backends de publicacion.
```

## Fuera de alcance

No entra:

```text
Pool global de triangulos.
Reclamar tris entre artistas.
BVH de triangulos como estructura principal de LOD.
Oclusion/frustum global.
Colisiones finales.
Terraformado persistente.
Biomas.
Sustancias/minerales.
Material final.
Proxy/impostor astronomico final.
LOD natural de props.
Sistema final GPU-resident de render.
Gameplay.
```

Regla:

```text
Si una decision trata de cuantos tris totales acaba pintando Environment, pertenece a 09.
Si una decision trata de que paginas del planeta se generan y a que resolucion, pertenece a 10.
Si una decision trata de no pedir/mantener paginas del planeta activo por mirada, frustum local o interes visual, pertenece a 10.
Si una decision trata de visibilidad auxiliar reutilizable para otros productores, pertenece a 11 como senal, no como autoridad del planeta.
Si una decision trata de biomas y cambios de ruido por region, pertenece a un documento futuro de biomas/receta.
```

## Relacion con otros documentos

Documentos base:

```text
Docs/Definicion_Tecnica_Proyecto.md
Docs/Teoria_Implementacion.md
Docs/Implementacion/Pasos_de_Implementacion.md
Docs/Implementacion/Calculo_Funcional_Datos_Planeta.md
Docs/Implementacion/Pasos/03_Coordenadas_Y_Receta.md
Docs/Implementacion/Pasos/04_Gestion_RAM_VRAM.md
Docs/Implementacion/Pasos/06_Forma_Planeta_GPU.md
Docs/Implementacion/Pasos/07_Marching_Cubes.md
Docs/Implementacion/Pasos/08_Pintado_Resultado_Marching_Cubes.md
Docs/Implementacion/Pasos/09_Pool_Global_Triangulos.md
Docs/Implementacion/Pasos/11_Visibilidad_Oclusion_Frustum.md
```

Relacion concreta:

```text
06 aporta density(point).
07 aporta Marching Cubes canonico sobre celdas cartesianas.
08 aporta la ruta de pintado/formato visual inicial.
09 aporta un backend gestionado de publicacion, equivalente conceptual a una herramienta `Draw(meshId, datos, priority)` / `SetMesh`.
10 decide paginas LOD y resolucion de muestreo.
11 queda como fuente futura de senales auxiliares de visibilidad/oclusion, sin liberar slots ni decidir LOD del planeta.
```

Regla de frontera:

```text
10 no modifica density(point).
10 no reimplementa la formula de planeta.
10 no depende de una respuesta de 09.
10 no delega en 11 la decision de vision/interes del planeta activo.
```

## Fuentes tecnicas usadas

Referencias que guian la decision:

```text
Transvoxel Algorithm:
https://transvoxel.org/

Lengyel, Voxel-Based Terrain for Real-Time Virtual Simulations:
https://transvoxel.org/Lengyel-VoxelTerrain.pdf

Geometry Clipmaps:
https://hhoppe.com/geomclipmap.pdf

CDLOD:
https://github.com/fstrugar/CDLOD

Unity LOD transitions:
https://docs.unity3d.com/Manual/LevelOfDetail.html

Dual Contouring of Hermite Data:
https://www.cs.rice.edu/~jwarren/papers/dualcontour.pdf
```

Lectura aplicada:

```text
Transvoxel encaja directamente con Marching Cubes multiresolucion y costura entre LODs.
Geometry Clipmaps y CDLOD refuerzan la idea de anillos/niveles centrados en observador.
Geometry Clipmaps refuerza la actualizacion incremental con presupuesto fijo y degradacion elegante cuando el observador se mueve rapido.
Unity LOD cross-fade confirma que suavizar por doble render puede eliminar pop, pero implica renderizar LOD actual y siguiente durante la transicion.
Dual Contouring adaptativo queda como alternativa futura si se decide abandonar Marching Cubes.
```

Decision:

```text
La primera ruta de 10 mantiene Marching Cubes y prueba Transvoxel.
No se cambia a Dual Contouring en esta fase.
```

## Decision sobre BVH

BVH queda descartado como estructura principal para las ideas de este documento.

Motivo:

```text
Un BVH acelera consultas sobre geometria o bounds ya existentes.
Nuestro problema principal es no generar geometria que no cabe.
```

Uso permitido futuro de BVH:

```text
Debug de bounds.
Rayos contra paginas ya generadas.
Queries sobre geometria residente.
Acelerador auxiliar para herramientas.
```

Uso no permitido en 10:

```text
No usar un BVH de triangulos como autoridad de LOD del terreno.
No generar malla densa completa para meterla en un BVH.
No hacer que 10 dependa de simplificar triangulos ya generados.
```

## Estructura principal

Decision inicial:

```text
Usar paginas de superficie LOD organizadas por una jerarquia tipo octree/clipmap.
```

Lectura:

```text
Octree -> permite subdividir mas donde importa.
Clipmap -> permite pensar en anillos de resolucion alrededor del player/camara.
Pagina LOD -> unidad real de generacion, cache, release y publicacion.
```

No se implementa un Sparse Voxel Octree global del planeta.

Regla:

```text
La estructura es un selector de paginas de superficie.
No es un volumen completo persistente.
No guarda todas las densidades del planeta.
```

### Pagina LOD

Unidad principal:

```text
PlanetSurfaceLodPage
```

Una pagina representa:

```text
bounds en GridCoordinates.
lodLevel.
sampleStepGrid.
estado de generacion.
prioridad actual.
conteo de triangulos generado.
referencia a recursos temporales o publicados.
```

`sampleStepGrid` define el paso de muestreo visual:

```text
LOD 0 -> sampleStepGrid = 1
LOD 1 -> sampleStepGrid = 2
LOD 2 -> sampleStepGrid = 4
LOD 3 -> sampleStepGrid = 8
LOD 4 -> sampleStepGrid = 16
```

En el perfil inicial `maxLodLevel = 4`.

Regla importante:

```text
La micro cell logica del planeta sigue siendo 1x1x1.
Un LOD visual con sampleStepGrid > 1 no cambia la identidad logica de las cells.
Terraformado, persistencia y datos finos siguen anclados a la micro cell.
```

Lectura:

```text
sampleStepGrid = 4 significa que la pagina visual muestrea cada 4 unidades de grid.
No significa que el planeta haya perdido su resolucion logica.
```

### Ratio entre LODs

Decision inicial:

```text
Los LODs visuales deben usar ratio 2:1 entre niveles vecinos.
```

Motivo:

```text
Transvoxel esta pensado para transiciones entre resoluciones que difieren por factor 2.
Los grids powers-of-two simplifican vecinos, costuras, cache y diagnostico.
```

Regla:

```text
Los presets observados 20/400, 130/61.5 y 400/20 sirven para calibrar aspecto.
No se convierten directamente en una escalera de LOD si rompen el ratio 2:1.
```

## Transvoxel

Transvoxel o una transicion equivalente sera la solucion inicial para evitar grietas entre paginas de distinto LOD.

Problema:

```text
Si una pagina fina toca una pagina gruesa, sus vertices de borde no coinciden siempre.
Eso crea cracks visibles.
```

Solucion:

```text
Generar transition cells en los bordes donde una pagina toca otra de LOD mas grueso.
```

Reglas:

```text
Las transiciones solo existen entre paginas vecinas con diferencia 1 de LOD.
Si hay diferencia mayor, se fuerza refinamiento intermedio o se bloquea la combinacion.
Las transition cells forman parte del resultado visual de 10.
Las transition cells forman parte de la malla virtual deseada de 10.
Las transition cells se envian a la misma ruta de publicacion que el resto de la pagina.
09 puede pintarlas o descartarlas por su politica interna, pero 10 no recalcula su reparto por esa respuesta.
Las transition cells se registran y liberan como recursos propios de la pagina o grupo de paginas hasta su publicacion.
```

No objetivo:

```text
No implementar toda la tabla de Transvoxel antes de tener selector de paginas funcionando.
No ocultar grietas con material, doble cara o overlap como solucion final.
```

Decision de implementacion:

```text
Primero validar paginas LOD sin costura en escenas controladas.
Despues introducir Transvoxel como cierre de calidad obligatorio antes de considerar 10 estable.
```

Tablas oficiales:

```text
Fuente: Eric Lengyel, Transvoxel Algorithm.
Web: https://transvoxel.org/
Repositorio: https://github.com/EricLengyel/Transvoxel
Licencia: MIT.
Referencia local: Docs/Referencias/Transvoxel/Transvoxel.cpp
Licencia local: Docs/Referencias/Transvoxel/LICENSE
```

Regla:

```text
No generar tablas Transvoxel en runtime.
No reinterpretar ni redisenar las tablas en esta fase.
Usar las tablas oficiales como fuente canonica.
Mantener la licencia MIT junto a cualquier copia sustancial de las tablas.
```

Tablas fuente que se usaran:

```text
regularCellClass[256]        -> byte
regularCellData[16]          -> geometryCounts + vertexIndex[15]
regularVertexData[256][12]   -> ushort
transitionCellClass[512]     -> byte
transitionCellData[56]       -> geometryCounts + vertexIndex[36]
transitionCornerData[13]     -> byte
transitionVertexData[512][12]-> ushort
```

Lectura de campos:

```text
geometryCounts high nibble -> vertex count.
geometryCounts low nibble  -> triangle count.
transitionCellClass bit alto -> invertir winding.
transitionCellClass low 7 bits -> indice de clase.
```

Layout C# previsto:

```text
TransvoxelTables.cs generado desde la referencia local.
byte[] para tablas de clase, counts e indices pequenos.
ushort[] para tablas de vertices.
Arrays planos, sin structs con padding implicito.
Validacion de longitudes contra los tamanos oficiales.
```

Layout GPU/HLSL previsto:

```text
Subir tablas como arrays uint empaquetados.
4 bytes por uint para datos byte.
2 ushorts por uint para datos ushort.
No depender de layout binario compartido entre struct C# y struct HLSL.
Leer con helpers HLSL ReadPackedByte(index) y ReadPackedUShort(index).
```

Buffers GPU previstos:

```text
StructuredBuffer<uint> _RegularCellClassPacked;
StructuredBuffer<uint> _RegularCellDataPacked;
StructuredBuffer<uint> _RegularVertexDataPacked;
StructuredBuffer<uint> _TransitionCellClassPacked;
StructuredBuffer<uint> _TransitionCellDataPacked;
StructuredBuffer<uint> _TransitionCornerDataPacked;
StructuredBuffer<uint> _TransitionVertexDataPacked;
```

Regla de implementacion:

```text
La primera implementacion puede generar los arrays C# a partir de Transvoxel.cpp mediante herramienta/editor script.
El runtime no parsea C++ ni ficheros externos.
Los buffers GPU se crean una vez y se registran en 04/09 como recurso vivo.
```

## Criterio de prioridad

Cada pagina obtiene un score.

Senales iniciales:

```text
distancia al player.
distancia a la direccion de mirada.
angulo respecto al forward de camara.
direccion de movimiento.
velocidad del player.
tamano aparente aproximado.
estado de carga actual.
coste estimado de generar la pagina.
```

Prioridad conceptual:

```text
contacto/interaccion inmediata -> maxima.
frente de camara -> alta.
direccion de movimiento -> alta.
periferia cercana -> media.
lejos visible -> baja.
hemisferio contrario del planeta activo -> baja o no publicado por 10.
```

Regla:

```text
La distancia no basta.
La direccion de mirada y el movimiento deben influir desde 10.
```

Pero:

```text
El frustum/interes del planeta activo se calcula aqui porque afecta a que paginas se generan y publican.
La oclusion avanzada reutilizable queda para 11 como senal auxiliar futura.
```

## Lookahead

10 debe preparar paginas por delante del jugador.

Formula conceptual:

```text
lookaheadDistance = playerSpeed * preloadTime
```

Uso:

```text
futurePosition = playerPosition + velocity * preloadTime
futureDirection = normalize(futurePosition - planetCenter)
```

Regla:

```text
Un jugador quieto prioriza camara y contacto.
Un jugador volando prioriza direccion de movimiento y zona futura.
Un giro brusco cancela o baja prioridad de paginas obsoletas.
```

## Flujo funcional

Flujo minimo:

```text
1. 10 recibe receta, placement y referencia player/camara.
2. 10 calcula paginas candidatas alrededor del player/camara.
3. 10 asigna un lodLevel a cada pagina candidata.
4. 10 decide su malla virtual deseada sin preguntar a 09.
5. 10 encola paginas nuevas o cambios de LOD.
6. El scheduler procesa trabajo con presupuesto de tiempo/trabajo por frame.
7. Cada pagina ejecuta Marching Cubes usando sampleStepGrid.
8. Si hay borde con LOD distinto, se generan transition cells.
9. 10 aplica o delega en 08 el formato de pintado/material/datos visuales por pagina.
10. 10 asigna meshId estable y priorityScore a la pagina/lote.
11. 10 envia la pagina a 09 como operacion de publicacion/dibujo.
12. 09 decide internamente que pinta, reclama o descarta.
13. 10 no ajusta su reparto por la respuesta de 09.
14. Paginas obsoletas se liberan o degradan por decision de 10, no por feedback de 09.
15. Metricas y diagnostico quedan visibles en el Lab.
```

Regla:

```text
10 no bloquea el frame esperando generar todo lo deseado.
10 trabaja con cola, prioridad y cancelacion.
```

## Cambio de LOD no perceptible

El cambio de LOD no debe ser un cambio atomico visible.

Regla central:

```text
La resolucion deseada puede cambiar instantaneamente.
La geometria visible solo cambia cuando la alternativa ya esta lista, completa y publicable.
```

10 separa tres conceptos:

```text
desiredLod -> lo que 10 querria tener segun distancia/mirada/movimiento.
residentLod -> lo que existe en cache o esta generandose.
publishedLod -> lo que se ha enviado a 09 como salida visible gestionada.
```

El jugador nunca debe esperar a que `desiredLod` termine de generarse.

Si una pagina de mayor resolucion todavia no esta lista, se mantiene publicada la pagina estable anterior.

### Estados de pagina para transicion

Cada pagina tiene estado explicito:

```text
StablePublished
WantedDifferentLod
Queued
GeneratingDensity
ExtractingSurface
GeneratingTransitions
ReadyToPublish
PublishedTo09
RetiringPrevious
Cancelled
Failed
```

Flujo:

```text
1. 10 detecta que una pagina deberia cambiar de LOD.
2. 10 no retira la pagina visible actual.
3. 10 encola una version candidata con pageCoord + targetLod + version.
4. El scheduler genera la candidata bajo presupuesto temporal.
5. Si la candidata queda obsoleta antes de terminar, se cancela.
6. Si termina, se generan sus transition cells necesarias.
7. Solo entonces se publica a 09 con meshId/version o releaseGroup estable.
8. La pagina previa queda marcada para retirada cuando la nueva queda publicada o cuando 10 decide que ya no hace falta.
```

Regla:

```text
No se publica media pagina.
No se retira una pagina estable para esperar a otra.
No se cambia LOD visible en el mismo frame en que se decide el target.
```

### Presupuesto temporal

La transicion de LOD no debe producir un pico de frame.

10 tiene un presupuesto propio por frame:

```text
maxLodWorkCpuMs.
maxLodDispatchesPerFrame.
maxPagesStartedPerFrame.
maxPagesCompletedPerFrame.
maxTemporaryTrianglesPerFrame.
maxTemporaryBufferBytes.
max09PublicationsPerFrame.
```

Orden recomendado:

```text
1. Mantener paginas ya visibles.
2. Completar paginas casi terminadas.
3. Generar costuras Transvoxel necesarias para candidatas listas.
4. Publicar paginas listas a 09.
5. Empezar nuevas paginas cercanas.
6. Empezar lookahead.
7. Degradar paginas lejanas.
```

Regla inspirada por Geometry Clipmaps:

```text
Si el jugador se mueve mas rapido de lo que 10 puede actualizar, 10 no intenta ponerse al dia en un unico frame.
Las paginas finas pueden ir con retraso.
La salida degrada de forma elegante manteniendo detalle grueso estable.
```

### Hysteresis

Las fronteras de LOD no deben producir vibracion.

Cada pagina mantiene hysteresis:

```text
refinar si visualError > refineThreshold.
degradar si visualError < degradeThreshold.
refineThreshold > degradeThreshold.
```

Perfil inicial:

```text
refineThreshold = 1.5 px de error aproximado.
degradeThreshold = 0.75 px de error aproximado.
lodDistanceMargin = 15% - 25%.
minLodStateLifetime = 0.25 s - 0.5 s.
```

Si se usa distancia en lugar de error de pantalla:

```text
enterHigherDetailDistance = lodDistance * 0.85
exitHigherDetailDistance = lodDistance * 1.15
```

Regla:

```text
Una pagina no puede alternar LOD cada frame por estar en la frontera.
Un cambio brusco de mirada o velocidad puede subir prioridad, pero no elimina hysteresis.
```

### Transvoxel y popping

Transvoxel resuelve continuidad geometrica entre paginas vecinas de distinto LOD.

Transvoxel no resuelve por si solo el popping de reemplazar una pagina completa.

Por eso 10 usa dos capas:

```text
Transvoxel -> evita grietas, agujeros y seams entre LODs vecinos.
Transicion temporal/espacial -> evita que el reemplazo de pagina sea perceptible.
```

Transicion visual inicial:

```text
Mantener pagina antigua hasta que la nueva este lista.
Publicar cambios por pagina, no por planeta completo.
Limitar paginas publicadas por frame.
Priorizar paginas cerca de la mirada y contacto.
```

Transicion visual futura si hace falta:

```text
Geomorph espacial de vertices dentro de una franja LOD.
Dither/cross-fade por pagina pequena.
Fade de material solo si se mide y no rompe Quest 3.
```

Decision:

```text
No usar cross-fade global como solucion base.
Renderizar dos LODs a la vez suaviza el pop, pero duplica coste durante la transicion.
En Quest 3 solo se permite como opcion puntual y medible para paginas pequenas, no como arquitectura principal.
```

### Publicacion hacia 09 durante transiciones

10 publica versiones completas.

Formato conceptual:

```text
meshId = planetId + pageCoord + logicalLayer
version = pageVersion
lodLevel = targetLod
priorityScore = score calculado por 10
releaseGroup = planetId + pageCoord
```

Regla:

```text
10 puede publicar una nueva version de la misma pagina.
09 decide internamente que queda pintado.
10 no usa la respuesta de 09 para decidir LOD.
10 no libera slots de 09 directamente.
10 solicita retirada de versiones obsoletas mediante la ruta gestionada acordada con 09.
```

### Cancelacion

El trabajo de una pagina candidata es cancelable.

Se cancela si:

```text
La pagina sale del area activa.
El targetLod cambia antes de terminar.
La receta/shapeHash cambia.
El presupuesto temporal prioriza otra pagina mas importante.
El planeta sale de ciclo activo.
```

Regla:

```text
Cancelar no debe generar GC ni liberar buffers globales.
Los buffers temporales vuelven a pools preasignados.
```

### Criterio de aceptacion de transicion

Una transicion de LOD se considera valida si:

```text
No hay frame hitch medible al cruzar umbral LOD.
No hay grietas visibles en bordes entre paginas vecinas.
No hay pagina que parpadee entre dos LODs por hysteresis insuficiente.
El numero de publicaciones a 09 por frame queda bajo limite.
El buffer temporal no crece en el frame de transicion.
La pagina vieja sigue visible hasta que la nueva esta lista o hasta que 10 decide retirarla por ciclo de vida.
```

## Relacion con Marching Cubes

07 sigue siendo el canon bruto:

```text
cellSizeGrid = 1
chunks cartesianos 64x64x64
validacion de forma completa sin LOD adaptativo
```

10 introduce una version adaptativa:

```text
pagina LOD.
sampleStepGrid variable.
bounds por pagina.
extraccion parcial bajo demanda.
transition cells.
```

Regla:

```text
10 no cambia la funcion density(point).
10 cambia donde y con que paso se muestrea esa funcion.
```

Decision de kernels/cache:

```text
10 comparte la base HLSL/codigo comun de 07 cuando sea posible.
10 puede usar kernels especializados si sampleStepGrid, layout por pagina, cancelacion o Transvoxel lo exigen.
10 no reutiliza la Mesh/cache de salida de 07 como fuente runtime.
La cache residente pertenece a 10.
La key de cache inicial sera planetId + pageCoord + lodLevel + recipeHash + shapeHash.
No se mantiene versionado historico de recetas durante prototipo; si cambia la receta, se invalida la cache y se reconstruye.
```

Se reutiliza de 07:

```text
density(point).
tablas Marching Cubes si aplican.
estructura de dispatch base.
AppendBuffer/conteo de triangulos.
debug y validacion.
release de buffers.
```

No se reutiliza de 07:

```text
Mesh de validacion.
cache de chunk completo como verdad runtime.
decision de bounds globales.
estado residente.
prioridad/cancelacion.
Transvoxel.
```

Los vertices siguen quedando inicialmente en GridCoordinates y se convierten a WorldSpace para salida Mesh o publicacion.

## Relacion con 08

08 no conserva el control de pintar el planeta completo cuando entra 10.

08 aporta:

```text
formato de Mesh inicial.
materiales/modos de color.
atlas/UV/altura.
agua visual inicial si aplica.
conversion de resultado geometrico a dato publicable.
```

10 toma el control del reparto y usa esa informacion de 08 por pagina.

Flujo:

```text
10 decide pagina y LOD.
10 extrae o pide extraer geometria adaptativa.
10 usa la logica/formato de 08 para preparar el lote visible.
10 envia el lote a publicacion.
```

Regla:

```text
08 no decide que paginas existen ni a que LOD.
08 no consulta presupuesto de 09 para 10.
08 no recupera el control global de pintado cuando 10 esta activo.
```

## Relacion con 09

09 es el backend gestionado de publicacion/dibujo.

10 no recibe presupuesto de 09 para decidir su reparto.

10 no espera respuesta de 09 para saber si su pagina existe conceptualmente.

09 puede entenderse como una herramienta parecida a:

```text
ManagedDraw(meshId, datos, priority)
SetManagedMesh(meshId, datos, priority)
```

Flujo:

```text
10 decide que pagina generar.
10 genera triangulos candidatos de esa pagina.
10 prepara datos de pintado usando la ruta/formato de 08.
10 llama a la ruta de publicacion gestionada por 09.
09 decide por detras si lo pinta, lo sobrescribe, lo reclama o lo descarta.
```

Regla:

```text
10 no pinta saltandose 09.
10 no pregunta a 09 si cabe.
10 no degrada paginas porque 09 no las haya pintado.
10 no aumenta ni reduce el presupuesto de Environment.
10 no reclama triangulos de otros artistas.
10 no usa granted/denied/reclaimed como entrada de calidad.
```

## Relacion con 11

10 usa distancia, mirada, movimiento y frustum local del planeta activo para decidir paginas y prioridad.

11 no decide la visibilidad del planeta activo en lugar de 10.

Separacion:

```text
10:
    Que paginas del planeta activo existen.
    Que resolucion tiene cada pagina.
    Que paginas se publican con meshId y priorityScore.
    Que paginas se degradan, cancelan o liberan por ciclo de vida del planeta.

11:
    Puede aportar senales auxiliares reutilizables de oclusion/visibilidad a productores futuros.
    No pinta.
    No libera slots de 09 directamente.
    No decide LOD del planeta.
```

Regla:

```text
Si una senal futura de 11 contradice la decision del planeta activo, se documenta como entrada a 10.
No se crea un segundo camino que calcule vision y pinte por fuera de 10 -> 09.
```

## Biomas

Los biomas quedan fuera de 10.

Nota para pasos futuros:

```text
Los biomas probablemente se definiran como regiones Voronoi u otra particion procedural.
Cada bioma podra modificar parametros de ruido, rugosidad, altura, color, sustancias o reglas locales.
```

Responsabilidad correcta:

```text
biome(point/direction) -> parametros de density/noise/material/sustancia
```

No:

```text
biome -> sistema de LOD
```

Regla:

```text
10 debe ser agnostico a biomas.
Si density(point) cambia por bioma en el futuro, 10 seguira muestreando density(point).
```

Pendiente futuro:

```text
Crear documento propio de Biomas antes de introducir reglas de bioma en codigo.
```

## Ruta visible inicial y ruta GPU final

Decision inicial:

```text
Durante 09-10 y las senales auxiliares que hagan falta, la salida visible seguira usando Mesh runtime CPU gestionada por el artista, porque es mas facil de depurar y hacer andar.
```

Motivo:

```text
Mesh permite validar forma, costuras, conteos, materiales, release, snapshots y bugs visuales con herramientas conocidas.
Saltarse demasiado pronto a render GPU-resident mezclaria demasiados riesgos a la vez.
```

Decision de direccion futura:

```text
Tarde o temprano la geometria adaptativa debera vivir y actualizarse mas directamente en GPU.
```

Motivo:

```text
Si el terreno cambia a menudo, subir Mesh CPU completa constantemente puede crear coste de CPU, copia, GC accidental y upload de vertices/indices.
La ruta natural a largo plazo es GPU-resident: GraphicsBuffer, draw procedural/indirect o backend equivalente.
```

Regla de orden:

```text
Primero hacer funcionar 09 y 10 con Mesh runtime medible.
Si 11 aporta senales auxiliares necesarias, integrarlas como entrada de productores sin cambiar la frontera 10 -> 09.
Cuando el trabajo con triangulos este resuelto, cambiar el backend visible para que deje de escupir a Mesh y pinte desde GPU.
```

Momento de GPU-resident:

```text
09 resuelve el backend gestionado de triangulos.
10 resuelve el reparto adaptativo de poligonaje.
11 puede aportar senales auxiliares de visibilidad/oclusion si hacen falta.
Despues de validar 09/10, la geometria ya existe como flujo de triangulos correcto.
El siguiente paso es cambiar la salida visible: de Mesh runtime a buffers/draw GPU-resident.
```

Decision:

```text
GPU-resident no es requisito para cerrar 10.
GPU-resident queda como ultimo paso posterior a validar 09/10 y las senales auxiliares necesarias, reutilizando el flujo de triangulos ya validado.
```

No se permite:

```text
Meter GPU mesh final antes de validar seleccion de paginas, Transvoxel, release y presupuesto.
Usar la necesidad futura de GPU como excusa para no medir la ruta Mesh inicial.
```

## Datos de entrada

Datos minimos:

```text
PlanetRecipe.
PlanetPlacement / frame de coordenadas.
PlanetGpuShapeEvaluator o datos GPU de 06.
Referencia player/camara.
Forward de camara.
Velocidad del player si existe.
Configuracion de LOD.
Estado de paginas residentes.
```

Configuracion inicial:

```text
maxResidentPages.
maxPageGenerationsPerFrame.
maxPageReleasesPerFrame.
basePageSizeGrid.
maxLodLevel.
preloadTime.
nearDetailRadius.
viewPriorityAngle.
movementPriorityWeight.
cameraPriorityWeight.
distancePriorityWeight.
```

Valores iniciales de prueba:

```text
basePageSizeGrid = 64 cells.
maxLodLevel = 4.
distancePriorityWeight = 0.55.
cameraPriorityWeight = 0.30.
movementPriorityWeight = 0.15.
LOD0 / alta resolucion: 0 m - 128 m aprox.
LOD1: 128 m - 256 m aprox.
LOD2: 256 m - 512 m aprox.
LOD3: 512 m - 1500 m aprox.
LOD4: 1500 m+ como geometria muy gruesa o candidato a proxy/impostor.
```

Notas:

```text
basePageSizeGrid vive en GridCoordinates.
Los rangos LOD son distancia mundo aproximada desde player/camara, no GridCoordinates.
maxLodLevel = 4 significa que 10 puede seleccionar LOD0, LOD1, LOD2, LOD3 y LOD4.
Niveles por encima de LOD4 quedan fuera del primer perfil y se decidiran tras medir.
Los pesos iniciales priorizan primero cercania, despues direccion de mirada y por ultimo movimiento/lookahead.
El perfil inicial asume juego lento y movimiento con vehiculos, pero debe medirse.
El radio de alta resolucion se mantiene corto; la precarga/lookahead puede crecer con la velocidad sin subir necesariamente LOD0.
La distancia util al horizonte depende de la altura sobre superficie, especialmente en un planeta de radio visual aproximado de 4 km.
```

## Datos de salida

Salida real:

```text
Lista de paginas residentes.
Pagina -> lodLevel actual.
Pagina -> estado: Pending, Generating, Resident, Degrading, Releasing, Cancelled.
Triangulos candidatos por pagina.
Transition cells por borde si aplican.
Lotes enviados a publicacion gestionada.
Metricas de pagina y de conjunto.
Diagnostico de calidad virtual y publicacion solicitada.
```

No debe producir:

```text
Nueva receta.
Nueva funcion density.
Biomas.
Colision final.
Persistencia.
Gameplay.
```

## Componentes/scripts previstos

### PlanetSurfaceLodSettings

Dato serializable.

Responsabilidad:

```text
Guardar tamaños de pagina.
Guardar maxLodLevel.
Guardar radios o rangos de LOD.
Guardar pesos de prioridad.
Guardar limites de generacion por frame.
Guardar limites de cache/pool.
No contener recursos GPU.
No depender del Lab.
```

### PlanetSurfaceLodPageKey

Identidad estable de pagina.

Responsabilidad:

```text
Identificar pagina por coordenada discreta y lodLevel.
Permitir comparar, cachear y liberar paginas.
No guardar datos visuales.
```

Campos conceptuales:

```text
int3 pageCoord.
int lodLevel.
int sampleStepGrid.
```

### PlanetSurfaceLodPage

Estado runtime de una pagina.

Responsabilidad:

```text
Guardar bounds Grid.
Guardar prioridad.
Guardar estado.
Guardar conteos de triangulos.
Guardar handles de recursos propios si existen.
Guardar version para cancelar trabajos obsoletos.
```

### PlanetSurfaceLodSelector

Sistema real de seleccion.

Responsabilidad:

```text
Calcular paginas candidatas.
Asignar lodLevel.
Calcular score de prioridad.
Detectar paginas nuevas, obsoletas o con LOD incorrecto.
No ejecutar Marching Cubes.
No publicar triangulos.
```

### PlanetSurfaceLodScheduler

Sistema real de cola de trabajo.

Responsabilidad:

```text
Ordenar trabajos por prioridad.
Aplicar presupuesto por frame.
Cancelar trabajos obsoletos.
Evitar picos de generacion/release.
No crear GC en runtime caliente.
```

### PlanetAdaptiveMarchingCubesExtractor

Sistema real de extraccion adaptativa.

Responsabilidad:

```text
Ejecutar Marching Cubes por pagina.
Usar density(point) de 06.
Aplicar sampleStepGrid de la pagina.
Generar vertices/triangulos candidatos.
No decidir prioridad global.
No pintar directo.
```

### PlanetTransvoxelStitcher

Sistema real de costura LOD.

Responsabilidad:

```text
Detectar bordes entre paginas de distinto LOD.
Generar transition cells.
Evitar cracks visibles.
Reportar coste y triangulos extra.
```

### PlanetSurfaceLodPublisher

Wrapper de publicacion gestionada.

Responsabilidad:

```text
Preparar batches de triangulos por pagina.
Enviar operaciones Draw/SetMesh al backend gestionado por 09.
Enviar meshId estable y priorityScore junto a cada publicacion.
Registrar que se solicito publicacion.
No interpretar granted/denied/reclaimed como decision de LOD.
No saltarse PlanetTrianglePoolWriter.
```

### PlanetSurfaceLodLab

Modulo aditivo para `PlanetImplementationLab`.

Responsabilidad:

```text
Exponer settings.
Forzar posicion/camara simulada.
Generar paginas cercanas.
Simular vuelo.
Mostrar paginas por LOD.
Mostrar grietas/transiciones.
Capturar metricas.
Liberar paginas.
No implementar los algoritmos reales dentro del Lab.
```

### PlanetSurfaceLodLabEditor

CustomEditor nativo.

Responsabilidad:

```text
Mostrar botones de prueba.
Mostrar pagina/resolucion/estado.
Mostrar diagnosticos.
No usar assets externos de inspector.
```

## Gestion de RAM

Reglas:

```text
No crear listas nuevas por frame.
No usar LINQ en caminos calientes.
No generar arrays nuevos por pagina si se puede reutilizar pool.
No guardar densidades de todo el planeta.
No mantener paginas obsoletas vivas sin limite.
No hacer caches sin capacidad maxima.
```

Patrones permitidos:

```text
Pool de paginas.
Buffers preasignados por generacion.
Cola/ring buffer de trabajos.
Arrays de resultados con capacity maxima.
Contadores + count.
```

Regla:

```text
El numero de paginas residentes debe tener limite duro.
El numero de paginas generandose a la vez debe tener limite duro.
```

## Gestion de VRAM

Recursos GPU previstos:

```text
Buffers temporales de extraccion por pagina o lote.
Buffers de estado de pagina.
Buffers de vertices/triangulos candidatos si se generan en GPU.
Buffers de transition cells.
Recursos visuales gestionados por el backend de publicacion de 09.
```

Reglas:

```text
Cada recurso propio se registra con owner claro.
Los buffers temporales se reutilizan.
Los buffers de 06 no se registran como owned por 10.
Los recursos visuales finales siguen siendo owned por 09.
10 no crea RenderTextures salvo debug documentado.
```

Ruta inicial:

```text
Generar y publicar hacia Mesh runtime CPU gestionada por 09 como backend de dibujo.
```

Ruta futura:

```text
Evaluar backend GPU-resident despues de validar 09/10 con Mesh runtime.
```

## Liberacion de recursos

Release debe:

```text
Cancelar trabajos pendientes.
Liberar buffers propios de 10.
Liberar paginas residentes propias.
Solicitar al backend de publicacion que libere lo publicado por 10 si aplica mediante owner/releaseGroup.
Marcar handles como liberados.
Limpiar referencias internas.
Dejar contadores propios a cero.
Permitir Release doble.
Permitir Init -> Release -> Init.
```

Regla:

```text
10 no libera recursos de 06.
10 no libera todo el backend/artista salvo que el Lab ejecute Release All global.
```

## Botones de Inspector

Botones esperados:

```text
Validate Surface LOD Setup
Init Surface LOD
Build Page Selection
Generate Near Pages
Generate View Cone Pages
Simulate Slow Flight
Simulate Fast Flight
Simulate Sharp Turn
Generate With LOD Transitions
Show Page Debug
Show Transition Debug
Capture LOD Metrics
Release Obsolete Pages
Release Surface LOD
Release All
```

Botones de stress:

```text
Run Stress Low
Run Stress Medium
Run Stress High
Run Stress Extreme
```

Reglas:

```text
Stress pesado solo por accion manual.
Cada boton que cree paginas debe tener diagnostico y camino de release.
```

## Pruebas manuales

Pruebas minimas:

```text
Validate detecta referencias nulas.
Init crea pools/colas sin generar planeta.
Build Page Selection muestra paginas candidatas sin ejecutar Marching Cubes.
Generate Near Pages genera detalle alto cerca del player.
Generate View Cone Pages da mas prioridad al centro de camara.
Simulate Slow Flight prepara paginas por delante.
Simulate Fast Flight aumenta lookahead o degrada periferia.
Simulate Sharp Turn cancela o baja prioridad de paginas obsoletas.
Release Obsolete Pages libera paginas fuera de prioridad.
Release Surface LOD deja contadores propios a cero.
Release doble no rompe.
Init -> Release -> Init funciona.
```

Pruebas visuales:

```text
Se ve mas detalle cerca que lejos.
La direccion de mirada recibe mejor resolucion que la espalda.
La transicion de LOD no deja grietas visibles cuando Transvoxel esta activo.
Al bajar hacia el planeta, el area cubierta disminuye y la densidad aumenta.
Al subir, el area cubierta aumenta y la densidad baja.
El sistema no intenta generar un planeta denso completo.
```

## Tests automatizados

Tests EditMode esperados:

```text
PlanetSurfaceLodSettings valida limites.
sampleStepGrid se deriva como potencia de dos.
lodLevel vecinos no difieren mas de 1 cuando Transvoxel es obligatorio.
PlanetSurfaceLodPageKey compara correctamente.
El selector devuelve resultados deterministas con la misma entrada.
El score mejora al acercarse al player.
El score mejora al alinearse con la camara.
El lookahead desplaza prioridad hacia futurePosition.
La cola cancela trabajos obsoletos por version.
Release simulado no deja handles vivos.
```

Tests PlayMode esperados:

```text
PlanetSurfaceLodLab existe en PlanetImplementationLab cuando se integre.
Validate Surface LOD Setup no lanza excepcion.
Init Surface LOD no lanza excepcion.
Generate Near Pages no lanza excepcion con configuracion de test.
Release Surface LOD no lanza excepcion.
Release doble no lanza excepcion.
Init -> Release -> Init funciona.
```

Tests condicionados:

```text
Si Compute Shader no esta disponible, la ruta GPU da diagnostico claro.
Si el backend de publicacion de 09 no esta inicializado, 10 puede seguir calculando seleccion virtual pero queda sin salida visible gestionada y debe mostrar diagnostico.
Si Transvoxel no esta implementado aun, las pruebas de costura quedan marcadas como pendiente bloqueante antes de cerrar 10.
```

## Metricas

Metricas iniciales:

```text
residentPageCount.
pendingPageCount.
generatingPageCount.
cancelledPageCount.
releasedPageCount.
pagesByLodLevel.
trianglesGeneratedByLod.
trianglesTransitionGenerated.
trianglesPreparedForPublish.
trianglesSentToManagedDraw.
publishCallsRequested.
averagePageGenerationMs.
maxPageGenerationMs.
lastSelectionMs.
lastScheduleMs.
lastReleaseMs.
ownedCpuEstimatedBytes.
ownedGpuEstimatedBytes.
liveGraphicsBuffers.
liveRuntimeMeshes si aplica.
pagePoolCapacity.
pagePoolUsed.
lastDiagnostic.
```

Metricas de calidad:

```text
distancia media de paginas LOD0 al player.
error visual aproximado por pagina.
numero de cambios de LOD por segundo.
numero de cambios de LOD publicados por frame.
numero de paginas en transicion.
numero de paginas candidatas canceladas por obsolescencia.
tiempo medio de generacion por pagina y LOD.
tiempo maximo de trabajo 10 por frame.
profundidad de cola de generacion.
profundidad de cola de publicacion hacia 09.
numero de paginas que mantienen LOD viejo esperando reemplazo listo.
numero de paginas con diferencia LOD > 1 bloqueadas.
numero de cambios evitados por hysteresis.
numero de frames en los que 10 agota su presupuesto temporal.
numero de cracks detectados visual/debug si existe test.
```

## Riesgos

Riesgos principales:

```text
Volver a generar demasiado planeta por frame.
Meter BVH de triangulos como solucion equivocada.
Romper la identidad de micro cell al usar sampleStepGrid alto.
Crear grietas entre LODs.
No respetar ratio 2:1 y complicar Transvoxel.
Hacer switch atomico de LOD y producir popping.
Generar la pagina nueva en el mismo frame de cruce de umbral.
Retirar una pagina estable antes de tener reemplazo listo.
Usar cross-fade global y duplicar coste durante transiciones.
No tener hysteresis y provocar oscilacion de LOD.
Duplicar density(point).
Saltar 09 y pintar directo en la ruta gestionada.
Hacer readback masivo para cada pagina.
Crear GC en seleccion o scheduler.
Mantener paginas obsoletas vivas.
Migrar a GPU-resident demasiado pronto.
Mezclar biomas con LOD antes de documentarlos.
```

Mitigaciones:

```text
Paginas con limite duro.
sampleStepGrid powers-of-two.
Transvoxel para costuras.
Pipeline de transicion por estados: deseado, encolado, generando, listo, publicado, retirando viejo.
Pagina antigua visible hasta que la candidata nueva este lista.
Presupuesto temporal fijo por frame para trabajo de 10.
Hysteresis de refinado/degradado.
Cooldown minimo por pagina antes de cambiar otra vez de LOD.
Publicacion limitada hacia 09 por frame.
Cross-fade o dither solo como opcion puntual medida, no como base.
Versionado/cancelacion de trabajos.
Publicacion gestionada obligatoria via 09 para la ruta visible gestionada.
Buffers/pools preasignados.
Metricas por pagina y por LOD.
Ruta Mesh inicial para depurar.
GPU-resident como ultimo paso despues de validar la publicacion adaptativa.
Biomas en documento futuro propio.
```

## Decisiones cerradas

```text
10 elimina BVH como arquitectura principal de reparto de detalle.
10 usa paginas/chunks LOD sobre el campo density(point).
La estructura sera tipo octree/clipmap, no Sparse Voxel Octree global.
La unidad runtime sera PlanetSurfaceLodPage.
La micro cell logica sigue siendo 1x1x1.
Los LOD visuales usan sampleStepGrid powers-of-two.
maxLodLevel inicial sera 4.
Los LOD vecinos deben diferir como maximo en 1 nivel cuando haya borde compartido.
Transvoxel o transicion equivalente sera obligatorio para cerrar grietas entre LODs.
Transvoxel no se considera solucion completa al popping de pagina; solo a costuras entre LODs.
El cambio de LOD visible se hace solo con reemplazo listo.
La pagina estable anterior se mantiene hasta que la nueva version esta completa y publicable.
10 usa hysteresis para evitar oscilacion en fronteras de LOD.
10 usa presupuesto temporal fijo para generar, completar y publicar cambios de LOD.
10 no usa cross-fade global como solucion base para Quest 3.
10 mantiene Marching Cubes como extractor inicial.
Dual Contouring queda como alternativa futura, no entra en esta fase.
10 prioriza por distancia, mirada y movimiento/lookahead.
10 implementa frustum/interes local del planeta activo como parte de su seleccion de paginas.
10 no implementa oclusion global reutilizable.
10 envia Draw/SetMesh/publicacion gestionada con meshId y priorityScore a 09.
10 no pinta directo saltandose 09.
10 no recibe ni usa respuesta de 09 para decidir LOD.
10 no modifica density(point).
Biomas quedan fuera y requieren documento propio.
La salida visible inicial sigue siendo Mesh runtime CPU gestionada por 09.
La ruta GPU-resident queda como evaluacion final despues de tener 09/10 funcionando con Mesh y las senales auxiliares necesarias integradas.
Las tablas Transvoxel oficiales de Eric Lengyel quedan descargadas como referencia local en Docs/Referencias/Transvoxel.
El layout inicial sera C# generado con arrays planos y GPU/HLSL con uints empaquetados.
10 compartira base HLSL/codigo comun con 07, pero su cache residente sera propia por pagina, LOD y version.
```

## Perfil inicial de prueba

```text
La primera prueba de 10 usara paginas base de 64 cells.
La primera prueba de 10 usara maxLodLevel = 4.
La primera prueba de 10 usara pesos de prioridad 0.55 distancia, 0.30 mirada y 0.15 movimiento/lookahead.
La primera prueba de 10 usara hysteresis de LOD con margen 15% - 25%.
La primera prueba de 10 no retirara paginas visibles hasta tener reemplazo listo.
La primera prueba de 10 limitara publicaciones a 09 por frame para evitar picos.
La alta resolucion se probara alrededor del jugador/camara hasta unos 128 m.
El detalle bajara progresivamente hasta unos 1.5 km.
Por encima de 1.5 km se evaluara geometria muy gruesa, proxy o impostor segun coste visual.
Estos valores no cierran el diseno final; sirven para medir coste, pop, costuras y sensacion de escala.
```

Motivo:

```text
64 cells encaja con el chunk canonico actual de 07.
128 m aproxima una zona cercana manejable para alta resolucion sin intentar rehacer el planeta visible completo.
1.5 km cubre vuelo bajo, laderas, vehiculos lentos y vistas elevadas iniciales sin prometer detalle fino hasta el horizonte.
```

## Pendientes de medicion/futuro

```text
Ajuste final de basePageSizeGrid y rangos LOD tras pruebas con jugador, vehiculos y altura sobre superficie.
Ajuste final de pesos de prioridad tras medir popping, retraso de generacion y coste por frame.
Ajuste final de hysteresis tras medir oscilacion de LOD y sensibilidad en VR.
Ajuste final de maxLodWorkCpuMs, maxPagesCompletedPerFrame y max09PublicationsPerFrame tras profiling en Quest 3.
Documento futuro de Biomas.
```

Nombre final del documento:

```text
Docs/Implementacion/Pasos/10_Optimizacion_Adaptativa_Poligonaje.md
```

Decision inicial no bloqueante:

```text
Empezar con selector de paginas y Marching Cubes adaptativo.
Mantener Mesh runtime para ver y medir.
Introducir Transvoxel antes de cerrar 10.
No tocar biomas todavia.
No migrar a GPU-resident hasta que 09/10 funcionen y se midan.
```

## Criterio de cierre

10 queda listo para implementar cuando aceptemos este contrato:

```text
El sistema reparte detalle generando paginas LOD bajo demanda.
El sistema no usa BVH de triangulos como solucion principal.
El sistema no materializa el planeta denso completo.
El sistema conserva micro cell logica 1x1x1.
El sistema usa sampleStepGrid powers-of-two para LOD visual.
El sistema cose LODs con Transvoxel o transicion equivalente.
El sistema no hace switch atomico perceptible de LOD.
El sistema mantiene pagina estable hasta que la candidata nueva esta lista.
El sistema usa hysteresis para cambios de LOD.
El sistema usa presupuesto temporal fijo para evitar picos de generacion/publicacion.
El sistema prioriza distancia, mirada y movimiento.
El sistema cancela trabajo obsoleto.
El sistema envia publicacion gestionada a 09.
El sistema mide paginas, tris, memoria, tiempos y releases.
El sistema mantiene Mesh runtime como backend visible inicial.
La migracion GPU-resident queda documentada como ultimo paso posterior a validar 09/10.
```
