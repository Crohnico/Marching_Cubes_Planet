# 10 - Publicacion runtime de meshes por chunk

## Estado

Documento superseded por el cambio de decision de 09 hacia backend visible GPU.

Este documento queda como registro de la hipotesis anterior: optimizar la
publicacion de `Mesh` runtime. La direccion vigente pasa a ser:

```text
Docs/Implementacion/Pasos/09_Backend_GPU_Triangulos.md
```

10 sigue decidiendo `desiredLOD`, `requestedLOD`, prioridad y cancelacion. 09
pasa a decidir como publica visualmente los triangulos aceptados.

## Problema

El sistema actual ya evita parte del Garbage Collector al usar listas
reutilizadas y ya reutiliza slots visibles en vez de destruir y recrear
GameObjects/Meshes para cada cambio.

Aun asi siguen apareciendo tirones fuertes cuando cambian meshes grandes en
runtime, incluso cuando la mesh viene de cache.

Lectura vigente:

```text
El coste principal sospechoso ya no es solo calcular cells.
El coste principal sospechoso esta en publicar/subir/cambiar meshes grandes en runtime.
```

Sintoma observado:

```text
runtimeLodCellsPerFrame bajo  -> cambios mas dispersos, tirones constantes.
runtimeLodCellsPerFrame alto  -> varios cambios juntos, pico mas claro y luego calma.
```

Esto apunta a que conviene controlar la fase de publicacion visible, no solo la
fase de extraccion/calculo.

## Objetivo

Reducir tirones al cambiar LOD de chunks grandes sin romper la shell inicial
LOD2 inmediata.

Objetivos concretos:

```text
1. Mantener la shell LOD2 inicial como carga inmediata.
2. No destruir ni recrear GameObjects/Meshes visibles durante swaps runtime.
3. Separar preparacion de datos y publicacion visible.
4. Controlar cuando se suben/aplican meshes grandes a Unity.
5. Evitar GC accidental en runtime caliente.
6. Poder medir donde se produce el tiron.
```

## Fuera de alcance

Este documento no cambia:

```text
Calculo de desiredLOD.
Tamanos canonicos de chunk.
Politica de cache por chunk/LOD.
Transvoxel.
Presupuesto de triangulos de 09.
```

La pieza GPU-resident ya ha dejado de estar fuera de alcance. Vive en 09:

```text
Docs/Implementacion/Pasos/09_Backend_GPU_Triangulos.md
```

## Linea base actual

La linea base aceptada para seguir desde aqui es:

```text
Cada chunk tiene slot visible persistente.
El slot visible no se destruye en cada cambio de LOD.
La Mesh del slot se reutiliza.
La ruta de cache puede cargar dentro de la Mesh existente.
La ruta generada puede pintar dentro de la Mesh existente.
```

Regla:

```text
Un cambio de LOD no debe crear/destruir GameObject visible.
Un cambio de LOD no debe destruir la Mesh visible si puede reescribirse o
alternarse mediante doble buffer.
```

## Estrategias aceptadas

### 1. API low-level de Mesh

Usar la ruta de buffers explicitos de Unity para reducir conversiones y
validaciones de las APIs comodas:

```text
Mesh.SetVertexBufferParams
Mesh.SetVertexBufferData
Mesh.SetIndexBufferParams
Mesh.SetIndexBufferData
```

Uso previsto:

```text
Subir vertices, normales, uvs, colores e indices con layout explicito.
Evitar conversiones intermedias cuando el payload ya esta en buffers reutilizados.
Mantener control de capacidades para no crecer memoria en caliente.
```

Referencia:

```text
https://docs.unity3d.com/ScriptReference/Mesh.SetVertexBufferData.html
```

### 2. MeshData + Jobs/Burst

Usar `Mesh.AllocateWritableMeshData` para preparar datos de mesh fuera de la ruta
comoda y permitir poblar datos desde Jobs cuando tenga sentido.

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

### 3. MarkDynamic obligatorio en meshes runtime

Toda Mesh runtime de chunk que vaya a recibir datos dinamicos debe marcarse como
dinamica antes del primer upload.

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

### 4. Fase de publicacion controlada

La preparacion de un chunk y la publicacion visible no tienen por que ocurrir en
el mismo frame.

Direccion:

```text
chunk request -> preparar/cargar/generar payload -> readyToPublish -> publicar con presupuesto
```

La cola `readyToPublish` debe permitir:

```text
Limitar cuantas meshes se aplican por frame.
Meter cooldown despues de publicar meshes grandes.
Priorizar LOD0 sobre LOD1 y LOD1 sobre LOD2.
Cancelar publicaciones que ya no coinciden con desiredLOD.
```

Regla:

```text
La shell LOD2 inicial no pasa por una publicacion lenta si todavia no existe el
planeta visible.
El throttling aplica a refinamientos/cambios runtime posteriores.
```

### 5. UploadMeshData en punto controlado

Probar `Mesh.UploadMeshData(false)` como parte opcional de la fase de
publicacion para decidir si conviene forzar el upload en un punto conocido del
frame.

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

### 6. Doble buffer visual por chunk

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
Evitar reescribir la misma Mesh visible que Unity puede estar usando para render.
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
Tiempo de lectura de cache .pmesh.
Tiempo de preparacion de buffers CPU.
Tiempo de escritura/aplicacion sobre Mesh.
Tiempo de UploadMeshData si se usa.
Tiempo de cambio visible entre buffers/slots.
Vertex count e index count publicados.
LOD origen y LOD destino.
```

Regla:

```text
Los logs por chunk no deben estar activos por frame en Editor salvo diagnostico
explicito.
Para coste normal se prefieren profiler markers/metricas agregadas.
```

## Orden de implementacion propuesto

1. Verificar que todas las meshes runtime/cache into slot pasan por
   `MarkDynamic`.
2. Anadir metricas/profiler markers alrededor de la publicacion de mesh.
3. Separar preparacion de payload y publicacion visible con una cola
   `readyToPublish`.
4. Anadir presupuesto de publicacion visible configurable en el SO.
5. Probar publicacion con `UploadMeshData(false)` controlado.
6. Cambiar a API low-level de Mesh si el profiler confirma coste alto en las
   APIs comodas.
7. Probar doble buffer visual por chunk para evitar reescribir la mesh visible.
8. Considerar MeshData + Jobs/Burst solo si queda coste CPU medible en la
   preparacion de datos.

## Parametros esperados en configuracion

Los valores concretos se cerraran al implementar, pero el SO de activacion LOD
debe poder exponer:

```text
maxRuntimeMeshPublishesPerFrame
runtimeMeshPublishCooldownSeconds
largeRuntimeMeshVertexThreshold
largeRuntimeMeshPublishCooldownSeconds
forceRuntimeMeshUploadOnPublish
enableRuntimeMeshDoubleBuffer
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
6. El profiler muestra menos picos o picos mas controlados en cambios de mesh.
```

## Decisiones cerradas

```text
La shell LOD2 inicial sigue siendo inmediata.
Los swaps runtime no deben destruir/recrear GameObjects visibles.
La publicacion visible de meshes necesita presupuesto propio.
El calculo de cells y la publicacion de mesh son fases distintas.
El doble buffer visual por chunk es una estrategia valida para probar.
Mantener LOD2 residente tras cargarlo es valido si memoria/VRAM lo permite.
```

## Pendientes

```text
Medir coste real de SetVertices/SetTriangles frente a SetVertexBufferData.
Medir si UploadMeshData(false) ayuda o solo mueve el pico.
Definir politica de eviction si se mantienen varios LODs residentes por chunk.
Definir si el doble buffer guarda dos Meshes por LOD o dos slots reutilizables.
Definir si MeshData + Jobs/Burst compensa la complejidad en Quest 3.
```
