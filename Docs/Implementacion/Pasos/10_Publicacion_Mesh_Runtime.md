# 10 - Publicacion visual runtime por chunk

## Estado

Documento vigente tras mover la ruta visual de preview a buffers GPU-resident.

10 es propietario de crear, publicar, reutilizar y liberar la representacion
visible runtime por chunk. Para visual, la ruta preferida ya no publica `Mesh`
CPU: Marching Cubes escribe vertices en un buffer GPU y se pinta mediante draw
procedural indirecto.

```text
07 calcula/extraccion GPU -> 10 publica/dibuja buffer GPU-resident
```

10 sigue decidiendo `desiredLOD`, `requestedLOD`, prioridad, cancelacion y
publicacion visible.

La ruta `Mesh` runtime queda como legacy/diagnostico y como posible base futura
para colision/physics si se necesita una representacion CPU separada. No es la
ruta visual caliente.

## Problema

El sistema anterior ya evitaba parte del Garbage Collector al usar listas
reutilizadas y reutilizar slots visibles en vez de destruir y recrear
GameObjects/Meshes para cada cambio.

Aun asi seguian apareciendo tirones fuertes al publicar meshes grandes en
runtime, incluso cuando la mesh venia de cache.

Lectura vigente:

```text
El coste principal sospechoso ya no es solo calcular cells.
El coste principal sospechoso esta en el bucle GPU -> CPU -> Mesh -> GPU y en la
sincronizacion que provoca publicar/subir/cambiar meshes grandes en runtime.
```

Sintoma observado:

```text
runtimeLodCellsPerFrame bajo  -> cambios mas dispersos, tirones constantes.
runtimeLodCellsPerFrame alto  -> varios cambios juntos, pico mas claro y luego calma.
```

Esto apunta a que conviene controlar la fase de publicacion visible, no solo la
fase de extraccion/calculo.

## Objetivo

Reducir tirones al generar y cambiar LOD de chunks grandes eliminando el readback
visual y la publicacion de `Mesh` para terreno visible.

Objetivos concretos:

```text
1. Mantener la shell LOD2 inicial como carga inmediata.
2. No destruir ni recrear GameObjects visibles durante swaps runtime.
3. Evitar que los vertices visuales vuelvan a CPU.
4. Dibujar desde buffers GPU mediante indirect args generados en GPU.
5. Mantener materiales editables desde un shader URP de superficie.
6. Evitar GC accidental en runtime caliente.
7. Poder medir donde se produce el tiron.
```

## Fuera de alcance

Este documento no cambia:

```text
Calculo de desiredLOD.
Tamanos canonicos de chunk.
Politica final de cache por chunk/LOD para datos no visuales.
Transvoxel.
Pool global de triangulos.
Colision/physics.
```

La cache `.pmesh` deja de aportar a la ruta visual si el terreno visible no se
publica como `Mesh`. Si se conserva, debe justificarse para colision,
diagnostico, bake offline o comparativa, no como requisito de render.


## Linea base actual

La linea base aceptada para seguir desde aqui es:

```text
Cada chunk visual tiene recursos GPU persistentes o reutilizables.
El slot visible no se destruye en cada cambio de LOD.
La ruta generada escribe vertices en GPU.
El draw indirect toma el vertex count desde un buffer de args generado en GPU.
```

Regla:

```text
Un cambio de LOD no debe crear/destruir GameObject visible.
Un cambio de LOD no debe forzar readback visual ni reconstruccion de Mesh CPU.
```

## PlanetGrid inicial

Antes de generar chunks concretos, el preview crea un `PlanetGrid` minimo.

Contrato actual:

```text
coordinates -> uint
0 = sin informacion
1 = chunk confirmado con al menos 1 triangulo
```

El grid inicial usa coordenadas logicas de chunk, no origins de un LOD concreto.
Ese nombre de chunk es comun para LOD0, LOD1 y LOD2; cada LOD lo traduce despues
a su `PlanetMarchingCubesChunkOrigin` usando su `chunkSize`.

El rango de shell solo se usa como lista conservadora de trabajo. No escribe `1`
por si mismo.

Cada candidato se confirma con la ruta de conteo de Marching Cubes. Solo se
marca `1` si el count pass del chunk devuelve `triangleCountAttempted > 0`.
Un chunk vacio no se marca nunca con `1`.

La ruta de conteo del grid no necesita capacidad de vertices de shell completa:
usa capacidad minima de conteo y escritura desactivada. El shader incrementa
`triangleCountAttempted` antes de comprobar overflow, asi que basta para
responder "este chunk contiene al menos 1 triangulo" sin reservar buffers grandes
por candidato.

`PlanetGrid` es un mapa `PlanetGridCoordinates -> uint`. No tiene semantica de
lista ni se consulta por indice.

Regla:

```text
Generate Grid conserva el `PlanetGrid` legacy/diagnostico.
Generate Shell visual no necesita `PlanetGrid`: despacha candidatos de shell en
GPU y dibuja lo escrito en el buffer.
Generate Chunk visual usa `PlanetGrid` siempre. Si no existe grid, el panel lo
crea antes de recorrer chunks. No usa candidatos conservadores en el flujo normal
de `Chunk + Generate`.
En el panel de preview, `Generate` con modo Chunk recorre candidatos uno a uno
dejando un frame entre chunks para pruebas manuales de FPS.
Generate Base visual usa `PlanetGrid` siempre. Si no existe grid, el panel lo
crea antes de generar. Base no es Shell ni Chunk: usa la misma cola de chunks
para cargar, uno a uno, todos los chunks confirmados con informacion para el LOD
pedido. Al terminar la cola emite el callback de completado.
```

Regla de memoria:

```text
GenerateChunk visual ajusta outputVertexCapacity a un budget redondeado por LOD:
LOD2 -> 8k vertices.
LOD1 -> 62k vertices.
LOD0 -> 500k vertices.
No arrastra la capacidad global por defecto de la shell completa para cocinar un
solo chunk.
La ruta visual conserva buffers GPU vivos mientras el componente esta vivo:
chunk origins, vertices, state, indirect args, edge table, tri table y shape
evaluator. Se machacan los datos sobre los mismos recursos y solo se recrean si
la nueva capacidad no cabe.
En modo Chunk del panel, las coordenadas confirmadas por `PlanetGrid` se agregan
en un unico slot visible GPU de chunks. No se reserva un buffer LOD0 por chunk:
el buffer agregado usa la misma escala de capacidad que la shell para que el modo
chunks no consuma mas memoria que generar la shell equivalente.
En modo Base del panel, las coordenadas confirmadas por `PlanetGrid` se agregan
en el mismo slot visible GPU agregado de chunks, pero recorriendo todos los
chunks confirmados. No crea un buffer por chunk.
GenerateShell conserva temporalmente la capacidad global por defecto de 3M como
deuda explicita hasta medir y cerrar la reduccion de shell LOD2.
La ruta legacy de Mesh puede mantener sus scratch buffers reutilizables mientras
exista para comparativas o diagnostico.
```

## Ruta GPU-resident aceptada

Contrato visual vigente:

```text
CPU ordena generacion.
CPU sube receta/cells/chunk origins.
GPU evalua densidad.
GPU ejecuta Marching Cubes.
GPU escribe vertices visuales.
GPU incrementa indirect args[0] con vertexCountWritten.
CPU emite DrawProceduralIndirect.
Los vertices visuales no vuelven a CPU.
```

En el panel de preview:

```text
Shell -> un slot GPU visible.
Chunk -> un slot GPU agregado para todos los chunks generados en la secuencia.
Base  -> una cola que carga todos los chunks confirmados en el slot agregado.
Runtime LOD inicial -> cola por UID que pide chunks por prioridad, todavia sobre
el slot agregado.
```

Motivo:

```text
LOD0 chunk budget = 500k vertices ~= 15.3 MiB.
Shell default = 3M vertices ~= 91.6 MiB.
Si cada chunk mantiene su propio buffer LOD0, 7 chunks ya superan la shell.
```

El material visible usa shader URP de superficie compatible con el atlas actual.
El shader recibe el buffer `PlanetMarchingCubesVertex`, transforma grid->world en
vertex shader y calcula la UV de atlas por altura igual que la ruta CPU previa.
Los vertices generados permanecen en coordenadas de grid. Mover o rotar el
GameObject del planeta solo actualiza la matriz grid->world del material y los
bounds del draw indirect; no dispara Marching Cubes ni reescribe buffers.

Regla:

```text
No usar readback de vertices, state o counts en la ruta visual caliente.
Si se necesita saber si un chunk esta vacio en CPU, eso pertenece a diagnostico,
streaming o fisica, no al render visual GPU-only.
La sustitucion fina por UID dentro del buffer GPU visible queda como paso
pendiente; la primera cola runtime valida identidad, prioridad y cancelacion.
```

## Estrategias aceptadas

### 1. Buffer visual GPU + draw indirect

Usar buffers GPU persistentes para vertices visuales y un buffer de argumentos
indirectos escrito por compute:

```text
ComputeBuffer vertices -> StructuredBuffer en shader de superficie.
ComputeBuffer indirect args -> DrawProceduralIndirect.
```

Uso previsto:

```text
Evitar readback de vertices visuales.
Evitar Mesh.SetVertices/SetTriangles/SetVertexBufferData en la ruta caliente.
Reutilizar capacidad GPU por LOD/chunk.
Mantener el material editable mediante shader URP compatible con atlas.
```

### 2. API low-level de Mesh legacy

Usar la ruta de buffers explicitos de Unity para reducir conversiones y
validaciones de las APIs comodas solo si se usa la ruta legacy/debug de Mesh:

```text
Mesh.SetVertexBufferParams
Mesh.SetVertexBufferData
Mesh.SetIndexBufferParams
Mesh.SetIndexBufferData
```

Uso previsto legacy:

```text
Subir vertices, normales, uvs, colores e indices con layout explicito.
Evitar conversiones intermedias cuando el payload ya esta en buffers reutilizados.
Mantener control de capacidades para no crecer memoria en caliente.
```

Referencia:

```text
https://docs.unity3d.com/ScriptReference/Mesh.SetVertexBufferData.html
```

### 3. MeshData + Jobs/Burst legacy

Usar `Mesh.AllocateWritableMeshData` para preparar datos de mesh fuera de la ruta
comoda y permitir poblar datos desde Jobs cuando tenga sentido para colision,
diagnostico o una ruta CPU no visual.

Uso previsto:

```text
Preparar datos CPU de mesh en una estructura escribible.
Mover trabajo repetitivo fuera del main thread si el profiler lo justifica.
Aplicar el resultado con Mesh.ApplyAndDisposeWritableMeshData.
```

Lectura:

```text
Esto no elimina el upload final a GPU.
Sirve para sacar preparacion CPU del main thread y reducir conversiones.
```

Referencias:

```text
https://docs.unity3d.com/ScriptReference/Mesh.AllocateWritableMeshData.html
https://docs.unity3d.com/ScriptReference/Mesh.ApplyAndDisposeWritableMeshData.html
```

### 4. MarkDynamic obligatorio en meshes runtime legacy

Toda Mesh runtime de chunk que vaya a recibir datos dinamicos debe marcarse como
dinamica antes del primer upload si se usa la ruta legacy.

Regla:

```text
Mesh.MarkDynamic() se llama al crear/preparar el slot runtime.
La ruta cache-into-slot no se salta MarkDynamic.
La ruta generada-into-slot no se salta MarkDynamic.
```

Referencia:

```text
https://docs.unity3d.com/ScriptReference/Mesh.MarkDynamic.html
```

### 5. Fase de publicacion controlada

La preparacion de un chunk y la publicacion visible no tienen por que ocurrir en
el mismo frame.

Direccion:

```text
chunk request -> generar GPU -> readyToSwap/readyToDraw -> publicar con presupuesto
```

La cola `readyToPublish` debe permitir:

```text
Limitar cuantos buffers/draws se activan por frame.
Meter cooldown despues de generar buffers grandes.
Priorizar LOD0 sobre LOD1 y LOD1 sobre LOD2.
Cancelar publicaciones que ya no coinciden con desiredLOD.
```

Regla:

```text
La shell LOD2 inicial no pasa por una publicacion lenta si todavia no existe el
planeta visible.
El throttling aplica a refinamientos/cambios runtime posteriores.
```

### 6. UploadMeshData en punto controlado legacy

Probar `Mesh.UploadMeshData(false)` como parte opcional de la fase de
publicacion solo en la ruta legacy de Mesh.

Lectura:

```text
No se llama a ciegas como arreglo permanente.
Se prueba medido, porque puede mover el coste de sitio pero no hacerlo
desaparecer.
```

Referencia:

```text
https://docs.unity3d.com/ScriptReference/Mesh.UploadMeshData.html
```

### 7. Doble buffer visual por chunk

Cada chunk puede tener dos buffers visuales:

```text
slot A visible
slot B oculto/preparandose
```

Flujo:

```text
1. Mantener A visible.
2. Escribir el nuevo LOD en B.
3. Cuando B este listo, alternar visibilidad/referencia.
4. Mantener A vivo como representacion anterior o buffer libre.
```

Motivo:

```text
Evitar reescribir el mismo buffer visible que Unity puede estar usando para render.
Reducir sincronizaciones raras entre main thread, render thread y driver.
```

Regla importante:

```text
Una vez cargado el LOD2 de un chunk, no se destruye por cambiar a LOD1/LOD0.
Se deja vivo y se activa/desactiva segun haga falta mientras el presupuesto de
memoria lo permita.
```

## Politica de residencia inicial

La direccion preferida para esta fase:

```text
LOD2 de cada chunk de shell -> residente mientras el planeta local exista.
LOD1/LOD0 -> residentes si ya se cargaron/generaron y no superan presupuesto.
```

Ventaja:

```text
Volver de LOD0 a LOD1 o LOD2 puede ser cambio de visibilidad, no nuevo upload.
```

Riesgo:

```text
Aumenta memoria/VRAM viva.
```

Por eso la residencia completa de multiples LODs queda condicionada a metricas
de memoria y puede necesitar una politica de eviction posterior.

## Instrumentacion obligatoria

Antes de dar por buena una optimizacion, hay que medir al menos:

```text
Tiempo de dispatch de Marching Cubes GPU.
Tiempo de inicializacion/crecimiento de buffers GPU.
Tiempo de escritura de indirect args.
Tiempo de DrawProceduralIndirect y coste de render.
Vertex count publicado en args indirectos.
Tiempo de lectura de cache .pmesh si se usa una ruta legacy.
Tiempo de escritura/aplicacion sobre Mesh si se usa una ruta legacy.
Tiempo de UploadMeshData si se usa una ruta legacy.
Tiempo de cambio visible entre buffers/slots.
LOD origen y LOD destino.
```

Regla:

```text
Los logs por chunk no deben estar activos por frame en Editor salvo diagnostico
explicito.
Para coste normal se prefieren profiler markers/metricas agregadas.
```

## Orden de implementacion propuesto

1. Generar shell/chunk visual en buffer GPU sin readback.
2. Dibujar con args indirectos escritos por compute.
3. Anadir metricas/profiler markers alrededor de dispatch, buffer growth y draw.
4. Separar generacion GPU y activacion visible con una cola `readyToPublish`.
5. Anadir presupuesto de publicacion visible configurable en el SO.
6. Probar doble buffer visual por chunk para evitar reescribir buffers visibles.
7. Mantener Mesh low-level/MeshData solo para legacy, diagnostico o colision.

## Parametros esperados en configuracion

Los valores concretos se cerraran al implementar, pero el SO de activacion LOD
debe poder exponer:

```text
maxRuntimeGpuPublishesPerFrame
runtimeGpuPublishCooldownSeconds
largeRuntimeGpuVertexThreshold
largeRuntimeGpuPublishCooldownSeconds
enableRuntimeGpuDoubleBuffer
```

Regla:

```text
Estos parametros regulan publicacion visible.
No sustituyen runtimeLodCellsPerFrame ni maxRuntimeLodRequestsPerUpdate.
```

## Validacion

Validacion minima desde Canvas Generate:

```text
1. Generate carga la shell LOD2 completa de forma inmediata como antes.
2. Al moverse el jugador, LOD0/LOD1 se preparan en paquetes.
3. La publicacion visible respeta el presupuesto configurado.
4. No se destruyen/recrean GameObjects visibles por swap de LOD.
5. No aparecen logs masivos por frame.
6. El profiler muestra menos picos o picos mas controlados al generar/cambiar
   visuales.
7. No aparecen readbacks de vertices/state/counts en la ruta visual caliente.
```

## Decisiones cerradas

```text
La shell LOD2 inicial sigue siendo inmediata.
Los swaps runtime no deben destruir/recrear GameObjects visibles.
La publicacion visible GPU necesita presupuesto propio.
El calculo de cells y la activacion visible son fases distintas.
El doble buffer visual por chunk es una estrategia valida para probar.
Mantener LOD2 residente tras cargarlo es valido si memoria/VRAM lo permite.
La ruta visual caliente no debe publicar Mesh CPU ni hacer readback GPU->CPU.
```

## Pendientes

```text
Medir coste real de DrawProceduralIndirect en Editor y Quest 3.
Medir si el crecimiento de buffers GPU provoca waits residuales.
Definir politica de eviction si se mantienen varios LODs residentes por chunk.
Definir si el doble buffer guarda dos buffers por LOD o dos slots reutilizables.
Definir la ruta separada de colision/physics si necesita datos CPU.
```
