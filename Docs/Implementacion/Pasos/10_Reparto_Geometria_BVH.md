# 10 - Reparto adaptativo de geometria planetaria

## Objetivo

Definir el sistema que decide que zonas del planeta se generan con mas o menos resolucion antes de gastar triangulos.

10 toma como base el presupuesto gestionado por 09 y lo convierte en una seleccion adaptativa de paginas/chunks de superficie.

Contrato funcional:

```text
budget gestionado por 09
-> paginas LOD alrededor del player/camara
-> Marching Cubes multiresolucion por pagina
-> costura entre LODs con Transvoxel o transicion equivalente
-> publicacion de triangulos al artista Environment de 09
```

Decision central:

```text
10 no se disena como BVH de triangulos.
10 se disena como generacion adaptativa por paginas/chunks LOD sobre el campo density(point).
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
Planeta 130 grid / 61.5 scale:
- radio visual aproximado mantenido en 8k de diametro / 4k de radio efectivo.
- salida observada: ~2.7M tris.
- cabe en el socket temporal actual.
- tarda poco para validacion.
- parece adecuado como lectura de planeta completo a distancia, pero no para cercania extrema.

Planeta 20 grid / 400 scale:
- salida observada: ~27k tris.
- parece apropiado para muy lejos o para base de impostor/proxy.

Planeta 400 grid / 20 scale:
- resolucion visual deseable cerca.
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
Publicacion de triangulos generados al artista Environment de 09.
Metricas de paginas pedidas, generadas, canceladas, degradadas y publicadas.
Stress con presupuestos bajos/medios/altos del artista Environment.
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
Si una decision trata de cuantos tris totales caben en Environment, pertenece a 09.
Si una decision trata de que paginas del planeta se generan y a que resolucion, pertenece a 10.
Si una decision trata de no pedir/mantener paginas que la camara no puede ver, pertenece a 11.
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
08 aporta la ruta Mesh visible inicial.
09 aporta presupuesto y artista Environment.
10 decide paginas LOD y resolucion de muestreo.
11 reducira prioridad o liberara lo no visible.
```

Regla de frontera:

```text
10 no modifica density(point).
10 no reimplementa la formula de planeta.
10 no sustituye el pool de 09.
10 no asume visibilidad final de 11.
```

## Fuentes tecnicas usadas

Referencias que guian la decision:

```text
Transvoxel Algorithm:
https://transvoxel.org/

Geometry Clipmaps:
https://hhoppe.com/geomclipmap.pdf

CDLOD:
https://github.com/fstrugar/CDLOD

Dual Contouring of Hermite Data:
https://www.cs.rice.edu/~jwarren/papers/dualcontour.pdf
```

Lectura aplicada:

```text
Transvoxel encaja directamente con Marching Cubes multiresolucion y costura entre LODs.
Geometry Clipmaps y CDLOD refuerzan la idea de anillos/niveles centrados en observador.
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
...
```

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
Las transition cells consumen presupuesto del artista Environment.
Las transition cells se registran y liberan como recursos propios de la pagina o grupo de paginas.
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
hemisferio contrario -> baja o delegada a 11.
```

Regla:

```text
La distancia no basta.
La direccion de mirada y el movimiento deben influir desde 10.
```

Pero:

```text
Frustum culling fuerte, oclusion y liberacion por invisibilidad pertenecen a 11.
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
1. 09 inicializa el artista Environment.
2. 10 recibe receta, placement y referencia player/camara.
3. 10 calcula paginas candidatas alrededor del player/camara.
4. 10 asigna un lodLevel a cada pagina candidata.
5. 10 descarta o degrada paginas fuera de presupuesto estimado.
6. 10 encola paginas nuevas o cambios de LOD.
7. El scheduler procesa trabajo con presupuesto por frame.
8. Cada pagina ejecuta Marching Cubes usando sampleStepGrid.
9. Si hay borde con LOD distinto, se generan transition cells.
10. La pagina publica sus triangulos al artista Environment de 09.
11. Paginas obsoletas se liberan o degradan.
12. Metricas y diagnostico quedan visibles en el Lab.
```

Regla:

```text
10 no bloquea el frame esperando generar todo lo deseado.
10 trabaja con cola, prioridad y cancelacion.
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

Los vertices siguen quedando inicialmente en GridCoordinates y se convierten a WorldSpace para salida Mesh o publicacion.

## Relacion con 09

09 es la autoridad del presupuesto visible.

10 no posee el presupuesto global.

Flujo:

```text
10 decide que pagina generar.
10 genera triangulos candidatos de esa pagina.
10 pide publicar esos triangulos al artista Environment de 09.
09 concede, reclama o deniega slots segun su politica.
10 registra diagnostico de pagina publicada/parcial/denegada.
```

Regla:

```text
10 no pinta saltandose 09.
10 no aumenta el presupuesto de Environment.
10 no reclama triangulos de otros artistas.
```

## Relacion con 11

10 usa senales de mirada y movimiento para calidad.

11 decide visibilidad global.

Separacion:

```text
10:
    Que resolucion quiero para esta pagina si compite por geometria.

11:
    Esta pagina deberia competir ahora o esta fuera de frustum/oculta.
```

Regla:

```text
11 puede bajar prioridad, liberar o evitar que paginas invisibles sigan compitiendo.
10 no implementa oclusion global en esta fase.
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

TBD futuro:

```text
Crear documento propio de Biomas antes de introducir reglas de bioma en codigo.
```

## Ruta visible inicial y ruta GPU final

Decision inicial:

```text
Durante 09-10-11 la salida visible seguira usando Mesh runtime CPU gestionada por el artista, porque es mas facil de depurar y hacer andar.
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
Primero hacer funcionar 09, 10 y 11 con Mesh runtime medible.
Despues, como ultimo paso del bloque 09-10-11, evaluar migrar el backend visible a GPU-resident.
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
Presupuesto activo del artista Environment.
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

## Datos de salida

Salida real:

```text
Lista de paginas residentes.
Pagina -> lodLevel actual.
Pagina -> estado: Pending, Generating, Resident, Degrading, Releasing, Cancelled.
Triangulos candidatos por pagina.
Transition cells por borde si aplican.
Requests de publicacion al artista Environment.
Metricas de pagina y de conjunto.
Diagnostico de presupuesto/calidad.
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

Wrapper entre 10 y 09.

Responsabilidad:

```text
Preparar batches de triangulos por pagina.
Enviar requests al artista Environment.
Registrar si la pagina quedo publicada total, parcial o denegada.
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
Recursos visuales gestionados por el artista Environment de 09.
```

Reglas:

```text
Cada recurso propio se registra con owner claro.
Los buffers temporales se reutilizan.
Los buffers de 06 no se registran como owned por 10.
Los recursos visuales del artista Environment siguen siendo owned por 09.
10 no crea RenderTextures salvo debug documentado.
```

Ruta inicial:

```text
Generar y publicar hacia Mesh runtime CPU gestionada por 09.
```

Ruta futura:

```text
Evaluar backend GPU-resident al final del bloque 09-10-11.
```

## Liberacion de recursos

Release debe:

```text
Cancelar trabajos pendientes.
Liberar buffers propios de 10.
Liberar paginas residentes propias.
Pedir al artista Environment que libere lo publicado por 10 si aplica mediante owner/releaseGroup.
Marcar handles como liberados.
Limpiar referencias internas.
Dejar contadores propios a cero.
Permitir Release doble.
Permitir Init -> Release -> Init.
```

Regla:

```text
10 no libera recursos de 06.
10 no libera todo el artista Environment salvo que el Lab ejecute Release All global.
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
Generate Near Pages no lanza excepcion con presupuesto de test.
Release Surface LOD no lanza excepcion.
Release doble no lanza excepcion.
Init -> Release -> Init funciona.
```

Tests condicionados:

```text
Si Compute Shader no esta disponible, la ruta GPU da diagnostico claro.
Si el artista Environment de 09 no esta inicializado, 10 queda unavailable con diagnostico.
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
trianglesRequestedToEnvironment.
trianglesGrantedByEnvironment.
trianglesDeniedByEnvironment.
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
numero de paginas con diferencia LOD > 1 bloqueadas.
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
Duplicar density(point).
Saltar 09 y pintar directo.
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
Versionado/cancelacion de trabajos.
Publicacion obligatoria via artista Environment.
Buffers/pools preasignados.
Metricas por pagina y por LOD.
Ruta Mesh inicial para depurar.
GPU-resident como ultimo paso del bloque.
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
Los LOD vecinos deben diferir como maximo en 1 nivel cuando haya borde compartido.
Transvoxel o transicion equivalente sera obligatorio para cerrar grietas entre LODs.
10 mantiene Marching Cubes como extractor inicial.
Dual Contouring queda como alternativa futura, no entra en esta fase.
10 prioriza por distancia, mirada y movimiento/lookahead.
10 no implementa oclusion/frustum global.
10 publica al artista Environment de 09.
10 no pinta directo saltandose 09.
10 no modifica density(point).
Biomas quedan fuera y requieren documento propio.
La salida visible inicial sigue siendo Mesh runtime CPU gestionada por 09.
La ruta GPU-resident queda como evaluacion final del bloque 09-10-11, despues de tener 09/10/11 funcionando con Mesh.
```

## TBD

```text
Nombre final de archivo si se decide quitar `BVH` tambien de la ruta.
Tamano inicial exacto de pagina en GridCoordinates.
MaxLodLevel inicial.
Rangos iniciales de distancia por LOD.
Pesos exactos de distancia/mirada/movimiento.
Formato exacto de tablas Transvoxel y layout C#/HLSL.
Si la extraccion adaptativa comparte kernels con 07 o usa kernels separados.
Cuando se considera suficiente la ruta Mesh para empezar backend GPU-resident.
Documento futuro de Biomas.
```

Decision inicial no bloqueante:

```text
Empezar con selector de paginas y Marching Cubes adaptativo.
Mantener Mesh runtime para ver y medir.
Introducir Transvoxel antes de cerrar 10.
No tocar biomas todavia.
No migrar a GPU-resident hasta que 09/10/11 funcionen y se midan.
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
El sistema prioriza distancia, mirada y movimiento.
El sistema cancela trabajo obsoleto.
El sistema publica a 09.
El sistema mide paginas, tris, memoria, tiempos y releases.
El sistema mantiene Mesh runtime como backend visible inicial.
La migracion GPU-resident queda documentada como ultimo paso posterior a validar 09/10/11.
```
