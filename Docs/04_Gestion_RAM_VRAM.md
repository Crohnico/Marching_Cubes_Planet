# 04 - Gestion RAM/VRAM

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

## Objetivo

Definir la politica comun de memoria del motor antes de implementar forma GPU, proxy lejano, payload de triangulos, chunks, colisiones o terraformado.

Este documento es transversal. No genera planeta por si mismo. Define como cualquier sistema que cree recursos debe:

```text
registrarlos.
medirlos.
reutilizarlos cuando tenga sentido.
liberarlos de forma explicita.
demostrar que no deja fugas.
```

La prioridad sigue siendo Quest 3:

```text
RAM controlada.
Memoria GPU estimada controlada.
Sin basura accidental de GC en caminos calientes.
Sin recursos vivos sin propietario.
Sin picos duros por crear o liberar demasiado de golpe.
```

## Alcance de esta fase

Entra:

```text
Registro de recursos vivos.
Presupuestos iniciales de RAM y memoria GPU estimada.
Handles para recursos registrados.
Snapshots ligeros propios del Lab.
Snapshots oficiales de Unity Memory Profiler.
Diagnosticos legibles.
Reglas de ownership.
Reglas de Release.
Reglas de pools y buffers preasignados.
Botones de Inspector para estresar memoria.
Tests de registro/liberacion.
```

Recursos a cubrir:

```text
GraphicsBuffer.
ComputeBuffer.
RenderTexture.
Texture creada en runtime.
Mesh runtime.
NativeArray/NativeList persistente.
Arrays CPU grandes.
List<T> grandes con capacidad controlada.
Material instanciado en runtime.
```

## Fuera de alcance

No entra:

```text
Implementar forma del planeta.
Implementar Marching Cubes.
Implementar proxy lejano.
Implementar chunks.
Implementar colisiones.
Implementar persistencia final.
Cerrar medicion perfecta de VRAM real en Quest 3.
Optimizar todos los presupuestos finales.
```

Este documento define la politica de medicion, ownership, snapshots y liberacion. Los valores numericos finales de presupuesto se ajustaran cuando tengamos mediciones reales.

## Relacion con otros documentos

Documentos base:

```text
Docs/Definicion_Tecnica_Proyecto.md
Docs/Teoria_Implementacion.md
Docs/Implementacion_Funcional.md
Docs/01_PlanetImplementationLab.md
Docs/02_ComputeShaderLab.md
Docs/03_Coordenadas_Y_Receta.md
```

Este documento prepara:

```text
05_Quest3_Player_Setup
06_Forma_Planeta_GPU
07_Proxy_Planeta_Lejano
08_Payload_Triangulos
09_Estados_Planeta
10_Chunks_Locales
11_Streaming_Prioridades
14_Colisiones_Locales
15_Terraformado
```

## Datos de entrada

El sistema necesita:

```text
Presupuesto de RAM estimada.
Presupuesto de memoria GPU estimada.
Limites por tipo de recurso.
Nombre del modulo propietario.
Tipo de recurso.
Tamaño del recurso.
Politica de vida del recurso.
```

Presupuestos iniciales:

```text
La politica queda definida en este documento.

Hardware objetivo base:
- Meta Quest 3 / Quest 3S basica.
- 8 GiB de RAM LPDDR5.
- Memoria compartida/unificada entre CPU, GPU, sistema, runtime XR y aplicacion.
- No existe una VRAM dedicada que podamos presupuestar como en PC.

Presupuesto provisional exacto para el primer bloque del Lab:
- ownedCpuEstimatedBytes soft: 384 MiB.
- ownedCpuEstimatedBytes hard: 512 MiB.
- ownedGpuEstimatedBytes soft: 384 MiB.
- ownedGpuEstimatedBytes hard: 512 MiB.
- ownedCombinedEstimatedBytes soft: 768 MiB.
- ownedCombinedEstimatedBytes hard: 1024 MiB.
- singleResourceEstimatedBytes soft: 128 MiB.
- singleResourceEstimatedBytes hard: 256 MiB.
- liveGraphicsBuffers hard: 128.
- liveComputeBuffers hard: 64.
- liveRenderTextures hard: 16.
- liveRuntimeMeshes hard: 128.
- liveRuntimeTextures hard: 16.
- liveRuntimeMaterials hard: 32.

En la primera implementacion se usaran limites provisionales editables desde el Inspector del Lab.
Estos limites sirven como cortafuegos para detectar fugas, acumulacion accidental y casos extremos.
No se consideran presupuestos finales del motor.

Los presupuestos finales se ajustaran despues de medir en:
- Unity Editor/PC durante el primer bloque.
- Build real en Quest 3 cuando exista una escena representativa.
```

Regla:

```text
Todo presupuesto empieza editable desde Inspector del Lab.
Ningun limite inicial es dogma del motor hasta medirlo.
```

## Datos de salida

El sistema debe producir:

```text
Recursos vivos registrados.
RAM estimada propia.
Memoria GPU estimada propia.
Contadores por tipo de recurso.
Snapshots antes/despues.
Deltas entre snapshots.
Capturas oficiales .snap cuando se pida analisis profundo.
Diagnostico legible.
Ultimo modulo que creo/libero recursos.
Tiempo de creacion.
Tiempo de liberacion.
Allocations detectadas si se puede medir.
```

## Conceptos

### Memoria propia estimada

La metrica principal inicial sera nuestra estimacion por recurso.

Motivo:

```text
Quest 3 usa memoria compartida/unificada.
No vamos a tratar "VRAM real" como una cifra absoluta fiable.
Unity/driver pueden retener memoria internamente.
Necesitamos saber que recursos creamos nosotros y quien los posee.
```

Regla:

```text
La estimacion propia es la fuente principal de ownership.
Unity Profiler es validacion externa.
No esperamos que ambas cifras coincidan exactamente.
```

### Doble snapshot obligatorio

Desde el primer bloque se usaran dos tipos de snapshot.

```text
PlanetMemorySnapshot:
- captura ligera propia del Lab.
- guarda recursos registrados, estimaciones, contadores Unity disponibles y tiempos.
- sirve para deltas rapidos, stress tests, Release All y diagnosticos inmediatos.
- no se ejecuta por frame por defecto.

Unity Memory Profiler Snapshot:
- captura oficial de Unity.
- se guarda como archivo .snap.
- sirve para analisis profundo de memoria en Editor, Player y Quest 3.
- se usa como validacion externa desde el principio del proyecto.
```

Regla:

```text
PlanetMemorySnapshot no sustituye a Unity Memory Profiler.
Unity Memory Profiler no sustituye al registro propio de ownership.
Se usan los dos desde el minuto 0.
```

Regla de separacion:

```text
Las capturas de memoria no forman parte del sistema oficial de gameplay/render.
No deben permear al runtime caliente.
No deben condicionar la generacion, el render, el streaming, la fisica ni el gameplay.
No deben ejecutarse por frame.
No deben introducir trabajo extra en builds finales salvo modo diagnostico explicito.
```

El sistema real puede exponer contadores ligeros y registrar ownership, pero no debe depender de capturar snapshots:

```text
Sistema real:
- registra recursos grandes.
- libera recursos grandes.
- expone contadores ligeros si hace falta.

Lab / Editor / Dev diagnostics:
- captura PlanetMemorySnapshot.
- solicita Unity Memory Profiler Snapshot.
- compara deltas.
- genera diagnosticos pesados.
```

Motivo:

```text
El snapshot propio responde rapido: que creo el proyecto, quien lo posee y si Release lo solto.
El snapshot oficial responde profundo: que ve Unity realmente, que queda vivo y que pasa en Player/dispositivo.
Mantener las capturas fuera del runtime real evita computo innecesario en Quest 3.
```

Uso esperado:

```text
Cada stress o prueba importante:
- PlanetMemorySnapshot before.
- PlanetMemorySnapshot after.
- PlanetMemorySnapshot after Release.

> Nota arrastrada desde `Docs/02_ComputeShaderLab.md`: la matriz de stress incluye `GraphicsBuffer Stress`, `ComputeBuffer Stress`, `Stress Low`, `Stress Medium`, `Stress High`, `Stress VeryHigh` y `Stress Extreme`. En 02 la masa relevante es solo la masa de datos calculados del buffer `float4`: `bufferElementCount * 16 bytes`. Quedan fuera de esa lectura RenderTexture de debug, meshes, materiales, texturas runtime, heap global del Editor y VRAM real total. En Editor/PC, el 2026-06-27, todos los registros revisados terminaron OK y con `liveResourceCount=0`, `ownedGpuEstimatedBytes=0 bytes (0 MiB / 0 GiB)` y `ownedCpuEstimatedBytes=0 bytes (0 MiB / 0 GiB)` tras Release. Los valores `managedHeapBytes` de esos JSON no se deben usar como peso RAM/VRAM por stress porque salen de `GC.GetTotalMemory(false)` y miden heap gestionado global de la sesion.

Hitos o sospecha de fuga:
- Unity Memory Profiler Snapshot before.
- Unity Memory Profiler Snapshot after.
- Unity Memory Profiler Snapshot after Release si aporta valor.
```

Regla Quest 3:

```text
La validacion fuerte de memoria no se considera cerrada solo con Editor.
Cuando exista build representativa, se capturaran snapshots oficiales en Quest 3.
```

### ProfilerRecorder counters iniciales

El Lab intentara crear `ProfilerRecorder` para un set inicial cerrado de contadores.

Regla:

```text
El nombre del counter debe coincidir exactamente con el nombre que expone Unity.
Si un counter no existe en la version/plataforma/build actual, se marca como unavailable.
Un counter unavailable no rompe la prueba ni el sistema real.
```

Counters obligatorios a intentar para memoria de proceso:

```text
ProfilerCategory.Memory / "App Resident Memory"
ProfilerCategory.Memory / "App Committed Memory"
ProfilerCategory.Memory / "Total Used Memory"
ProfilerCategory.Memory / "Total Reserved Memory"
ProfilerCategory.Memory / "System Used Memory"
ProfilerCategory.Memory / "System Total Used Memory"
```

Uso:

```text
App Resident Memory      -> memoria residente del proceso segun el SO.
App Committed Memory     -> memoria comprometida del proceso segun el SO.
Total Used Memory        -> memoria usada por la aplicacion segun Unity.
Total Reserved Memory    -> memoria reservada por la aplicacion segun Unity.
System Used Memory       -> memoria residente segun SO; suele coincidir con App Resident Memory.
System Total Used Memory -> memoria usada total del dispositivo si la plataforma la expone.
```

Counters obligatorios a intentar para GC:

```text
ProfilerCategory.Memory / "GC Used Memory"
ProfilerCategory.Memory / "GC Reserved Memory"
ProfilerCategory.Memory / "GC Allocated In Frame"
ProfilerCategory.Memory / "GC Allocation In Frame Count"
```

Uso:

```text
GC Used Memory              -> heap gestionado usado.
GC Reserved Memory          -> heap gestionado reservado.
GC Allocated In Frame       -> bytes gestionados asignados en el frame.
GC Allocation In Frame Count -> numero de allocations gestionadas en el frame.
```

Nota:

```text
GC Allocated In Frame y GC Allocation In Frame Count pueden no estar disponibles en release players.
Son especialmente utiles en Editor y Development Player.
```

Counters obligatorios a intentar para memoria/render GPU relacionada:

```text
ProfilerCategory.Render / "Used Buffers Bytes"
ProfilerCategory.Render / "Used Buffers Count"
ProfilerCategory.Render / "Render Textures Bytes"
ProfilerCategory.Render / "Render Textures Count"
ProfilerCategory.Render / "Used Textures Bytes"
ProfilerCategory.Render / "Used Textures Count"
ProfilerCategory.Memory / "Gfx Used Memory"
ProfilerCategory.Memory / "Gfx Reserved Memory"
ProfilerCategory.Memory / "Texture Memory"
ProfilerCategory.Memory / "Mesh Memory"
```

Uso:

```text
Used Buffers Bytes     -> memoria usada por buffers GPU segun Unity.
Used Buffers Count     -> numero total de buffers GPU segun Unity.
Render Textures Bytes  -> memoria usada por RenderTextures.
Render Textures Count  -> numero de RenderTextures usadas en el frame.
Used Textures Bytes    -> memoria usada por texturas si la plataforma lo expone.
Used Textures Count    -> numero de texturas usadas si la plataforma lo expone.
Gfx Used Memory        -> estimacion Unity/driver de memoria grafica usada.
Gfx Reserved Memory    -> estimacion Unity/driver de memoria grafica reservada.
Texture Memory         -> memoria de texturas cargadas.
Mesh Memory            -> memoria de meshes cargadas.
```

Counters obligatorios a intentar para payload visible:

```text
ProfilerCategory.Render / "Draw Calls Count"
ProfilerCategory.Render / "SetPass Calls Count"
ProfilerCategory.Render / "Triangles Count"
ProfilerCategory.Render / "Vertices Count"
ProfilerCategory.Render / "Vertex Buffer Upload In Frame Bytes"
ProfilerCategory.Render / "Index Buffer Upload In Frame Bytes"
ProfilerCategory.Render / "Vertex Buffer Upload In Frame Count"
ProfilerCategory.Render / "Index Buffer Upload In Frame Count"
```

Uso:

```text
Draw Calls Count                    -> draw calls del frame.
SetPass Calls Count                  -> cambios de pass/material.
Triangles Count                      -> triangulos procesados.
Vertices Count                       -> vertices procesados.
Vertex Buffer Upload In Frame Bytes  -> bytes de vertices subidos a GPU en el frame.
Index Buffer Upload In Frame Bytes   -> bytes de indices subidos a GPU en el frame.
Vertex Buffer Upload In Frame Count  -> numero de subidas de vertices.
Index Buffer Upload In Frame Count   -> numero de subidas de indices.
```

Counters secundarios a intentar solo para diagnostico:

```text
ProfilerCategory.Memory / "Profiler Used Memory"
ProfilerCategory.Memory / "Profiler Reserved Memory"
ProfilerCategory.Memory / "Object Count"
ProfilerCategory.Memory / "Asset Count"
ProfilerCategory.Memory / "GameObject Count"
ProfilerCategory.Memory / "Scene Object Count"
ProfilerCategory.Memory / "Material Count"
ProfilerCategory.Memory / "Mesh Count"
ProfilerCategory.Memory / "Texture Count"
```

Regla:

```text
Los counters secundarios no participan en hard limits.
Sirven para explicar deltas raros y solo se muestran si estan disponibles.
```

Implementacion esperada:

```text
PlanetMemoryLab intenta registrar todos los counters al inicializar.
Cada counter guarda category, name, unit, isAvailable y lastValue.
Los snapshots guardan valor o unavailable.
Los hard limits del proyecto se basan primero en nuestra estimacion propia.
Los counters Unity sirven para validar tendencias, no para reemplazar ownership.
```

### Ownership

Cada recurso grande debe tener un propietario claro.

Campos minimos:

```text
resourceId
resourceName
resourceType
ownerModule
estimatedBytes
createdAtFrame
isAlive
```

Regla:

```text
Si un sistema crea un recurso grande, ese sistema es responsable de registrarlo y liberarlo.
```

Si un recurso cambia de propietario, debe quedar explicito:

```text
TransferOwnership(resourceId, newOwner)
```

No se permite:

```text
crear recurso grande sin registrar.
liberar recurso sin actualizar registro.
compartir recurso mutable sin propietario principal.
dejar recurso vivo "por si acaso".
```

### Presupuesto

El presupuesto no es solo memoria total. Tambien cuenta numero de recursos y coste de churn.

Presupuestos a controlar:

```text
RAM estimada total.
Memoria GPU estimada total.
Buffers GPU vivos.
RenderTextures vivas.
Meshes runtime vivas.
Texturas runtime vivas.
Materiales instanciados.
Bytes por modulo.
Tiempo maximo de creacion por operacion.
Tiempo maximo de release por operacion.
Allocations por camino caliente.
```

Regla:

```text
Si se supera un presupuesto, el sistema debe emitir diagnostico.
No debe aumentar el presupuesto silenciosamente.
```

### Presupuesto inicial Quest 3

El presupuesto inicial del Lab parte de la Quest 3 mas basica como restriccion practica.

Dato base:

```text
RAM fisica del dispositivo objetivo: 8 GiB.
Tipo de memoria: LPDDR5.
Modelo de memoria: compartida/unificada.
VRAM dedicada: no aplica.
```

Regla:

```text
No presupuestamos contra 8 GiB completos.
El sistema operativo, Horizon OS, Unity, XR runtime, compositor, tracking, assets base y gameplay futuro tambien usan memoria.
```

Por eso el primer bloque usara limites deliberadamente conservadores para memoria propiedad del proyecto:

```text
CPU propia estimada:
    soft: 384 MiB
    hard: 512 MiB

GPU propia estimada:
    soft: 384 MiB
    hard: 512 MiB

CPU + GPU propia estimada:
    soft: 768 MiB
    hard: 1024 MiB

Recurso individual:
    soft: 128 MiB
    hard: 256 MiB
```

Interpretacion:

```text
soft -> Warning. La prueba puede continuar, pero debe mostrar diagnostico.
hard -> Critical. La prueba no debe seguir creciendo sin confirmacion manual.
```

Regla:

```text
Estos limites son presupuesto del Lab del primer bloque, no promesa de memoria final del juego.
Se ajustaran con snapshots propios, Unity Profiler, Unity Memory Profiler y build real en Quest 3.
```

No se permite:

```text
Subir el limite porque un stress test falla.
Subir el limite sin anotar la medicion que lo justifica.
Usar el presupuesto como excusa para dejar recursos vivos tras Release.
```

### Reutilizacion

Reutilizar no significa acumular sin limite.

Se permite:

```text
pools con capacidad maxima.
buffers persistentes con tamaño maximo definido.
ring buffers.
List<T> preasignadas con capacidad estable.
NativeArray persistente con Dispose claro.
```

No se permite:

```text
pools que crecen sin techo.
listas que crecen por frame.
buffers temporales gigantes recreados constantemente.
caches que son la unica fuente de verdad.
```

Regla:

```text
Pool sin limite explicito es fuga lenta.
```

## Componentes/scripts previstos

### PlanetMemoryBudget

Dato de configuracion de presupuestos.

Responsabilidad:

```text
Definir limites de RAM estimada.
Definir limites de memoria GPU estimada.
Definir limites por tipo de recurso.
Definir limites de stress test.
Ser editable desde Inspector del Lab.
```

Decision:

```text
Empieza como datos serializados en el Lab.
No empieza como ScriptableObject.
```

Motivo:

```text
Es configuracion de pruebas de la escena tecnica.
No queremos assets de presupuesto antes de medir.
Si luego hace falta un perfil reutilizable real, se definira aparte.
```

Decision de configuracion:

```text
Los presupuestos finales/oficiales no viviran en un archivo externo de texto.
Viviran como un dato interno del proyecto, definido donde sea mas simple de editar y consultar desde codigo.

El archivo externo en Quest 3 sera solo un override de prueba para profiling.
La ejecucion siempre usa una copia runtime plana llamada PlanetMemoryBudget.
```

Motivo:

```text
Durante pruebas reales en Quest 3 no queremos generar una build nueva por cada ajuste pequeño de presupuesto.
Si subimos o bajamos un 1%, debe bastar con editar el archivo de configuracion y pulsar un boton de recarga de prueba.

Pero esa comodidad de prueba no debe convertirse en el formato final oficial del juego.
```

Separacion:

```text
PlanetMemoryBudgetDefaults:
- dato interno del proyecto.
- fuente oficial de valores por defecto.
- versionable con el codigo/proyecto.
- facil de consultar desde codigo.
- no depende de leer archivos externos en runtime.

PlanetMemoryBudgetTestOverrideFile:
- archivo externo de texto.
- editable fuera de Unity.
- vive en la carpeta persistente/configurable de la app en Quest 3.
- sirve solo para ajustar presupuestos durante profiling en dispositivo.
- no se lee por frame.
- no es el formato final oficial.

PlanetMemoryBudget:
- struct/clase de datos runtime plana.
- se crea leyendo PlanetMemoryBudgetDefaults y luego aplicando el override externo si existe.
- es la version que usan registry, diagnosticos y checks de presupuesto.
- no depende de UnityEngine.Object.
- no lee disco por frame.
```

Regla:

```text
El sistema oficial usa PlanetMemoryBudget ya resuelto.
El archivo externo solo puede modificar la copia runtime durante pruebas o profiling.
El archivo externo no reemplaza al dato oficial del proyecto.
```

Ubicacion:

```text
La ruta concreta debe resolverse con Application.persistentDataPath o un wrapper propio equivalente.
En Quest/Android esto debe apuntar a la carpeta persistente de la aplicacion, asociada al package debug actual `com.Perodry.debug`.
El Lab debe mostrar la ruta exacta en Inspector para poder copiar/editar el archivo desde fuera.
```

Formato inicial del archivo:

```text
JSON legible.
Extension sugerida: planet-memory-budget.json.
Nombre recomendado: planet-memory-budget.override.json.
```

Campos minimos:

```text
profileName
targetPlatform
ownedCpuSoftMiB
ownedCpuHardMiB
ownedGpuSoftMiB
ownedGpuHardMiB
ownedCombinedSoftMiB
ownedCombinedHardMiB
singleResourceSoftMiB
singleResourceHardMiB
liveGraphicsBuffersHard
liveComputeBuffersHard
liveRenderTexturesHard
liveRuntimeMeshesHard
liveRuntimeTexturesHard
liveRuntimeMaterialsHard
```

Flujo:

```text
1. El sistema carga PlanetMemoryBudgetDefaults.
2. Crea una copia runtime PlanetMemoryBudget.
3. En Lab/dev diagnostics, busca el archivo externo de override.
4. Si existe y valida, aplica sus valores sobre la copia runtime.
5. Si no existe, usa los valores por defecto.
6. Si existe pero es invalido, usa defaults y muestra diagnostico.
7. El Lab puede ejecutar Reload Test Budget para releer el archivo manualmente.
```

Boton esperado:

```text
Reload Test Budget
```

Regla:

```text
Reload Test Budget es herramienta de Lab/dev diagnostics.
No debe ejecutarse automaticamente en runtime oficial.
No debe convertirse en polling de archivo.
No debe tocar sistemas calientes mientras estan generando/renderizando.
```

No se permite:

```text
Leer el archivo de presupuesto cada frame.
Modificar el archivo automaticamente porque una prueba excedio presupuesto.
Usar el archivo externo como estado mutable de gameplay.
Usar el archivo externo como fuente oficial final.
Mezclar presets de stress del Lab con presupuestos oficiales del juego.
Fallar la aplicacion si el archivo no existe.
```

### PlanetResourceRegistry

Sistema real de registro de recursos.

Responsabilidad:

```text
Registrar recursos.
Marcar recursos liberados.
Sumar bytes estimados.
Agrupar por propietario.
Agrupar por tipo.
Detectar recursos vivos tras Release.
Generar snapshot.
```

Regla:

```text
El registry no crea recursos.
El registry no libera recursos directamente salvo que se diseñe un wrapper propietario.
El registry sabe que existe y quien deberia liberarlo.
```

### PlanetResourceHandle

Dato ligero que representa un recurso registrado.

Responsabilidad:

```text
Guardar resourceId.
Guardar tipo.
Guardar bytes estimados.
Guardar propietario.
Guardar estado vivo/liberado.
```

Uso:

```text
El modulo crea recurso.
El modulo registra recurso y recibe handle.
El modulo guarda handle.
Al liberar, el modulo usa handle para marcar liberado.
```

### PlanetGpuResourceScope

Helper opcional para recursos GPU creados por codigo real.

Responsabilidad:

```text
Agrupar buffers, render textures y meshes de un modulo.
Liberar todos los recursos del scope.
Reportar si queda algo vivo.
```

Regla:

```text
Un scope no sustituye a ReleaseModule.
Ayuda a que ReleaseModule sea corto y fiable.
```

### PlanetMemorySnapshot

Captura puntual de memoria.

Responsabilidad:

```text
Guardar contadores propios.
Guardar contadores Unity disponibles si existen.
Guardar timestamp/frame.
Guardar nombre de la operacion.
Permitir comparacion before/after.
```

Campos iniciales:

```text
ownedCpuEstimatedBytes
ownedGpuEstimatedBytes
liveGraphicsBuffers
liveComputeBuffers
liveRenderTextures
liveRuntimeMeshes
liveRuntimeTextures
liveRuntimeMaterials
unityTotalUsedMemoryBytes
unityTotalReservedMemoryBytes
unityAppResidentMemoryBytes
unityAppCommittedMemoryBytes
unitySystemUsedMemoryBytes
unitySystemTotalUsedMemoryBytes
unityGcUsedBytes
unityGcReservedBytes
unityGcAllocFrameBytes
unityGcAllocFrameCount
unityGraphicsDriverBytes
unityGfxReservedBytes
unityRenderUsedBuffersBytes
unityRenderUsedBuffersCount
unityRenderTextureBytes
unityRenderTextureCount
unityUsedTextureBytes
unityUsedTextureCount
unityTextureMemoryBytes
unityMeshMemoryBytes
unityDrawCalls
unitySetPassCalls
unityTriangles
unityVertices
unityVertexBufferUploadFrameBytes
unityIndexBufferUploadFrameBytes
operationMs
```

Los campos Unity pueden empezar vacios si aun no tenemos ProfilerRecorder.

Regla:

```text
PlanetMemorySnapshot es obligatorio para pruebas del Lab.
Debe ser barato, serializable/mostrable en Inspector y suficiente para comparar deltas.
No debe intentar guardar todo el grafo de objetos de Unity.
No debe ser dependencia de sistemas oficiales de gameplay, render, streaming o fisica.
No debe capturarse automaticamente en caminos calientes.
```

### Exportacion de snapshots propios

Los snapshots propios del Lab se podran exportar en un formato legible de dos partes.

Objetivo:

```text
Poder abrir un archivo y saber en pocos segundos si una prueba esta correcta, en aviso o en peligro.
Poder bajar despues al detalle completo sin repetir la prueba.
```

Formato:

```text
Parte 1 -> cabecera resumen.
Parte 2 -> datos completos.
```

La cabecera debe ser corta y facil de leer.

Campos iniciales de cabecera:

```text
status
riskLevel
operationName
budgetResult
releaseResult
gcResult
topIssue
recommendedAction
```

Significado:

```text
status            -> OK, Warning o Critical.
riskLevel         -> Low, Medium, High o Unknown.
operationName     -> prueba, stress o accion que genero el snapshot.
budgetResult      -> dentro de presupuesto, cerca del limite o excedido.
releaseResult     -> limpio, quedan recursos vivos o no aplica.
gcResult          -> sin GC relevante, GC detectado o unavailable.
topIssue          -> problema principal en una frase.
recommendedAction -> siguiente accion recomendada.
```

Regla:

```text
La cabecera debe poder leerse sin entender todos los contadores.
Si la cabecera dice OK, la prueba debe haber quedado dentro de presupuesto y sin recursos propios vivos inesperados.
Si la cabecera dice Warning o Critical, debe explicar por que.
```

La segunda parte guarda todos los datos disponibles:

```text
snapshot metadata.
presupuestos usados.
estimaciones propias.
recursos vivos por owner.
recursos vivos por tipo.
ProfilerRecorder disponibles.
ProfilerRecorder unavailable.
deltas before/after si aplica.
diagnosticos generados.
ruta del snapshot oficial .snap si existe.
version de Unity si esta disponible.
plataforma/build si esta disponible.
```

Formato de archivo inicial:

```text
JSON legible.
Indentado.
Una exportacion por operacion o comparacion.
Extension sugerida: .planet-memory.json.
```

Regla:

```text
La exportacion propia vive en Lab/Editor/dev diagnostics.
No forma parte del runtime oficial.
No se escribe por frame.
No debe ejecutarse automaticamente en builds finales salvo modo diagnostico explicito.
```

Estructura conceptual:

```json
{
  "summary": {
    "status": "Warning",
    "riskLevel": "Medium",
    "operationName": "Stress Medium",
    "budgetResult": "ownedCombinedEstimatedBytes near soft limit",
    "releaseResult": "clean",
    "gcResult": "GC Allocated In Frame detected",
    "topIssue": "Managed allocations detected during stress.",
    "recommendedAction": "Review hot path allocations before increasing budget."
  },
  "details": {
    "snapshot": {},
    "budgets": {},
    "ownedEstimates": {},
    "resourcesByOwner": [],
    "resourcesByType": [],
    "profilerCounters": [],
    "unavailableCounters": [],
    "diagnostics": []
  }
}
```

No se debe usar este JSON como formato de savegame ni como contrato de runtime. Es una herramienta de inspeccion del Lab.

### PlanetUnityMemoryProfilerCapture

Helper del Lab para solicitar capturas oficiales de Unity Memory Profiler.

Responsabilidad:

```text
Capturar snapshots oficiales .snap cuando el paquete este disponible.
Guardar o mostrar la ruta del ultimo snapshot.
Etiquetar la captura con operacion, modulo y momento.
Emitir diagnostico claro si el paquete o la plataforma no permite capturar.
No reemplazar PlanetMemorySnapshot.
```

Regla:

```text
Unity Memory Profiler forma parte de las herramientas obligatorias desde el primer bloque.
Las capturas oficiales se usan para analisis profundo, no para medir cada frame.
Este helper vive en Lab/Editor/dev diagnostics.
El runtime oficial no debe depender de este helper.
```

Rutas:

```text
Usar la ubicacion por defecto de Unity Memory Profiler cuando sea posible.
Si se define ruta propia, debe quedar documentada y visible en el Lab.
```

### PlanetMemoryDiagnostics

Traductor de snapshots a mensajes legibles.

Responsabilidad:

```text
Comparar snapshot antes/despues.
Detectar fugas probables.
Detectar presupuesto excedido.
Detectar recursos no registrados.
Detectar GC inesperado.
Generar mensaje para Inspector.
```

Reglas de interpretacion:

```text
Si nuestra estimacion sube y no baja tras Release All -> fuga propia probable.
Si Unity sube y no baja pero nuestra estimacion esta a cero -> recurso Unity/internal, driver o referencia no registrada.
Si ambas suben con un stress test -> revisar presupuesto.
Si hay pico de GC -> revisar allocations del camino de prueba.
Si un modulo supera su presupuesto -> revisar ownership o degradacion.
```

Cada diagnostico debe tener:

```text
severity
title
probableCause
recommendedAction
relatedMetrics
```

### PlanetMemoryLab

Script aditivo para probar memoria en `PlanetImplementationLab`.

Responsabilidad:

```text
Exponer presupuestos.
Mostrar registry.
Capturar snapshots.
Ejecutar stress de memoria.
Ejecutar Release All via controlador.
Mostrar diagnosticos.
No contener algoritmos de planeta.
```

### PlanetMemoryLabEditor

`CustomEditor` nativo para botones de memoria.

Regla:

```text
Usar Inspector de Unity.
No usar UI Toolkit para controles.
No usar assets externos de inspector.
```

## Flujo funcional

Flujo de registro:

```text
1. Un modulo crea un recurso.
2. Calcula o declara estimatedBytes.
3. Registra el recurso en PlanetResourceRegistry.
4. Guarda el PlanetResourceHandle.
5. Usa el recurso.
6. Libera el recurso.
7. Marca el handle como liberado.
8. El registry vuelve a cero si no quedan recursos.
```

Flujo de snapshot:

```text
1. Capture Own Before.
2. Ejecutar operacion.
3. Capture Own After.
4. Calcular delta.
5. Generar diagnostico.
```

Flujo de snapshot oficial:

```text
1. Capture Unity Memory Snapshot Before si la prueba lo requiere.
2. Ejecutar operacion o stress.
3. Capture Unity Memory Snapshot After.
4. Release All.
5. Capture Unity Memory Snapshot After Release si se investiga fuga.
6. Comparar en Unity Memory Profiler.
```

Regla:

```text
Los snapshots oficiales son mas pesados.
Se usan desde el principio, pero bajo boton o en hitos claros, no en bucles por frame.
```

Flujo de Release All:

```text
1. Capturar snapshot inicial.
2. Llamar Release en todos los modulos.
3. Forzar limpieza de scopes propios.
4. Capturar snapshot final.
5. Revisar recursos vivos.
6. Mostrar diagnostico.
```

Regla:

```text
Release All debe poder ejecutarse dos veces.
Init despues de Release All debe funcionar.
```

## Gestion de RAM

Reglas:

```text
No crear arrays grandes por frame.
No usar ToArray en caminos calientes.
No usar LINQ en caminos calientes.
No crear strings por frame.
No crear List<T> nuevas para resultados frecuentes.
No permitir crecimiento silencioso de List<T> grandes.
```

Patrones permitidos:

```text
buffer preasignado + count.
List<T> con capacidad fija y Clear controlado.
NativeArray persistente con Dispose.
pool con capacidad maxima.
ring buffer.
```

Para datos pequeños, editor o configuracion:

```text
Se puede usar List<T> normal.
Se puede priorizar claridad.
```

Regla:

```text
La obsesion es sobre runtime caliente y datos grandes, no sobre complicar codigo pequeño.
```

## Gestion de VRAM

La VRAM se medira inicialmente como memoria GPU estimada.

Estimaciones:

```text
GraphicsBuffer:
    elementCount * stride

ComputeBuffer:
    elementCount * stride

RenderTexture:
    width * height * bytesPerPixel * slices * mipFactor aproximado

Texture runtime:
    width * height * bytesPerPixel * mipFactor aproximado

Mesh runtime:
    vertexBufferBytes + indexBufferBytes
```

Reglas:

```text
GraphicsBuffer es el camino principal para datos GPU del motor.
ComputeBuffer queda para comparativas, fallback temporal o compute puro.
No duplicar el mismo dato en CPU y GPU salvo necesidad medida.
No hacer readback GPU bloqueante salvo debug controlado.
No crear RenderTexture runtime sin registrarla.
No instanciar Material sin registrar si vive mas de una operacion puntual.
```

Sobre Quest 3:

```text
No tratar VRAM real como cifra absoluta.
Quest 3 usa memoria compartida/unificada.
La estimacion propia controla ownership.
Unity Profiler valida tendencias.
```

## Liberacion de recursos

Todo recurso grande debe tener liberacion explicita.

Reglas por tipo:

```text
GraphicsBuffer -> Release/Dispose segun API usada.
ComputeBuffer -> Release/Dispose segun API usada.
RenderTexture runtime -> Release y Destroy si aplica.
Mesh runtime -> Destroy.
Texture runtime -> Destroy.
Material runtime -> Destroy.
NativeArray/NativeList -> Dispose.
Arrays/List<T> grandes -> soltar referencia o Clear si se reutilizan.
```

Regla:

```text
OnDisable/OnDestroy son red de seguridad.
No sustituyen a Release explicito.
```

Despues de Release:

```text
handles marcados como liberados.
referencias internas limpiadas.
contadores actualizados.
diagnostico visible.
```

## Botones de Inspector

Botones esperados en `PlanetMemoryLab`:

```text
Validate Memory Setup
Capture Snapshot
Capture Before
Capture After
Compare Last Snapshots
Capture Unity Memory Snapshot
Capture Unity Memory Before
Capture Unity Memory After
Export Last Own Snapshot
Export Last Own Snapshot Comparison
Reload Test Budget
Run Allocation Smoke Test
Run Registry Smoke Test
Run Release Smoke Test
Run Stress Low
Run Stress Medium
Run Stress High
Run Stress Extreme
Release All
Force GC Check
Show Live Resources
Reset Memory Lab State
```

Botones de diagnostico:

```text
Show Resources By Owner
Show Resources By Type
Show Budget Warnings
Show Last Diagnostic
```

Regla:

```text
Los botones deben mostrar resultado legible.
No basta con numeros crudos.
```

## Pruebas manuales

Pruebas minimas:

```text
Abrir PlanetImplementationLab.
Validate Memory Setup no da errores.
Capture Snapshot genera datos visibles.
Capture Unity Memory Snapshot genera un .snap o un diagnostico claro si no esta disponible.
Export Last Own Snapshot genera un JSON con cabecera resumen y detalle completo.
Reload Test Budget relee el archivo externo de presupuesto y muestra diagnostico.
Registrar recurso mock aumenta contador.
Liberar recurso mock baja contador.
Release All deja contadores propios a cero.
Release All dos veces no rompe.
Init -> Release -> Init mantiene contadores correctos.
Stress Low no deja recursos vivos.
Stress Extreme muestra aviso antes de ejecutarse.
```

Pruebas de diagnostico:

```text
Simular recurso vivo tras Release -> Critical.
Simular presupuesto superado -> Warning.
Simular GC alloc -> Warning.
Simular Unity sube y propios cero -> Warning informativo.
Simular todo correcto -> OK.
```

Pruebas de tiempo:

```text
Medir tiempo de registrar N recursos.
Medir tiempo de liberar N recursos.
Medir duracion completa de cada stress.
Mostrar ultimo operationMs.
```

## Tests automatizados

Tests EditMode esperados:

```text
El calculo de bytes de GraphicsBuffer es correcto.
El calculo de bytes de ComputeBuffer es correcto.
El calculo aproximado de RenderTexture es correcto.
PlanetResourceRegistry suma recursos vivos.
PlanetResourceRegistry resta recursos liberados.
Liberar dos veces no deja contador negativo.
TransferOwnership cambia propietario.
PlanetMemorySnapshot calcula deltas.
PlanetMemoryDiagnostics detecta fuga propia simulada.
PlanetMemoryDiagnostics detecta presupuesto superado.
PlanetUnityMemoryProfilerCapture informa unavailable sin romper si el paquete/plataforma no permite capturar.
La exportacion propia incluye summary legible y details completos.
```

Tests PlayMode esperados:

```text
PlanetMemoryLab existe en PlanetImplementationLab cuando se integre.
Capture Snapshot no lanza excepcion.
Release All no lanza excepcion.
Release All dos veces no lanza excepcion.
Stress Low termina sin recursos vivos.
Un modulo registrado responde a Release All.
Capture Unity Memory Snapshot queda disponible como prueba manual obligatoria.
```

Tests condicionados:

```text
Si ProfilerRecorder no esta disponible, no falla la logica propia.
Si un contador Unity no existe en una plataforma, queda como unavailable.
Si un counter cambia de nombre entre versiones de Unity, el Lab debe mostrar el nombre fallido.
```

## Metricas

Metricas iniciales:

```text
ownedCpuEstimatedBytes
ownedGpuEstimatedBytes
liveResourceCount
liveGraphicsBuffers
liveComputeBuffers
liveRenderTextures
liveRuntimeMeshes
liveRuntimeTextures
liveRuntimeMaterials
bytesByOwner
bytesByType
lastOperationMs
lastReleaseMs
lastStressMs
unityTotalUsedMemoryBytes si disponible
unityTotalReservedMemoryBytes si disponible
unityAppResidentMemoryBytes si disponible
unityAppCommittedMemoryBytes si disponible
unitySystemUsedMemoryBytes si disponible
unitySystemTotalUsedMemoryBytes si disponible
unityGraphicsDriverBytes si disponible
unityGfxReservedBytes si disponible
unityRenderUsedBuffersBytes si disponible
unityRenderUsedBuffersCount si disponible
unityRenderTextureBytes si disponible
unityRenderTextureCount si disponible
unityUsedTextureBytes si disponible
unityUsedTextureCount si disponible
unityGcUsedBytes si disponible
unityGcReservedBytes si disponible
unityGcAllocFrameBytes si disponible
unityGcAllocFrameCount si disponible
unityDrawCalls si disponible
unitySetPassCalls si disponible
unityTriangles si disponible
unityVertices si disponible
unityVertexBufferUploadFrameBytes si disponible
unityIndexBufferUploadFrameBytes si disponible
lastUnityMemorySnapshotPath si existe
lastUnityMemorySnapshotMs si existe
lastOwnSnapshotExportPath si existe
lastDiagnostic
```

Metricas no fiables como verdad unica:

```text
VRAM real absoluta en Quest 3.
Memoria retenida internamente por driver.
Memoria liberada asincronamente por Unity.
```

## Riesgos

Riesgos principales:

```text
Creer que Unity libero todo inmediatamente.
Confundir memoria estimada propia con memoria real total.
No registrar un recurso grande.
Registrar pero no liberar.
Liberar pero no marcar como liberado.
Pools sin limite.
Stress tests que generan GC y contaminan resultados.
Hacer readback GPU bloqueante para medir.
Duplicar datos CPU/GPU por comodidad.
Crear demasiadas abstracciones antes de medir.
```

Mitigaciones:

```text
Registro obligatorio de recursos grandes.
Ownership explicito.
Release All probado.
Snapshots before/after.
Snapshots oficiales Unity Memory Profiler en hitos y sospechas de fuga.
Diagnosticos legibles.
Presupuestos editables.
Pools con capacidad maxima.
Medicion por tendencias.
Profiler como validacion externa.
```

## TBD

Decisiones abiertas:

```text
Ajuste final de presupuestos RAM/GPU despues de profiling en Quest 3 real.
Disponibilidad real de cada ProfilerRecorder en la version exacta de Unity y en Quest 3.
```

Decision inicial no bloqueante:

```text
Medir primero con estimacion propia.
Usar Unity Profiler y Unity Memory Profiler como validacion externa desde el primer bloque.
Capturar snapshots oficiales .snap en hitos, stress importantes y sospechas de fuga.
Usar el set inicial cerrado de ProfilerRecorder definido en este documento.
Marcar counters no disponibles como unavailable.
Exportar snapshots propios como JSON legible con cabecera resumen y detalle completo.
Mantener presupuestos editables en Inspector del Lab.
No bloquear el primer deadline por no tener VRAM real perfecta.
Registrar todos los recursos grandes manualmente.
Implementar diagnosticos legibles desde el principio.
```

## Criterio de cierre

Este documento queda listo para implementar cuando aceptemos este contrato:

```text
Todo recurso grande tiene propietario.
Todo recurso grande se registra.
Todo recurso grande tiene liberacion explicita.
Release All vuelve los contadores propios a cero.
Los snapshots comparan before/after.
Los snapshots oficiales de Unity Memory Profiler pueden capturarse desde el Lab o dan diagnostico claro.
Los diagnosticos traducen numeros a accion.
Los stress tests miden tiempo, memoria y recursos vivos.
```

No se pasa fuerte a `05_Quest3_Player_Setup`, `06_Forma_Planeta_GPU`, `07_Proxy_Planeta_Lejano` o `08_Payload_Triangulos` sin esta base integrada en `PlanetImplementationLab`.
