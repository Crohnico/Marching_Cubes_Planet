# 04 - Gestion RAM/VRAM

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
Snapshots de memoria.
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

Este documento define la base. Los presupuestos finales se ajustaran con profiling real.

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
05_Forma_Planeta_GPU
06_Proxy_Planeta_Lejano
07_Payload_Triangulos
08_Estados_Planeta
09_Chunks_Locales
10_Streaming_Prioridades
13_Colisiones_Locales
14_Terraformado
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
TBD con profiling real.
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
unityGcUsedBytes
unityGcAllocFrameBytes
unityGraphicsDriverBytes
operationMs
```

Los campos Unity pueden empezar vacios si aun no tenemos ProfilerRecorder.

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
1. Capture Before.
2. Ejecutar operacion.
3. Capture After.
4. Calcular delta.
5. Generar diagnostico.
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
```

Tests PlayMode esperados:

```text
PlanetMemoryLab existe en PlanetImplementationLab cuando se integre.
Capture Snapshot no lanza excepcion.
Release All no lanza excepcion.
Release All dos veces no lanza excepcion.
Stress Low termina sin recursos vivos.
Un modulo registrado responde a Release All.
```

Tests condicionados:

```text
Si ProfilerRecorder no esta disponible, no falla la logica propia.
Si un contador Unity no existe en una plataforma, queda como unavailable.
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
unityGraphicsDriverBytes si disponible
unityGcUsedBytes si disponible
unityGcAllocFrameBytes si disponible
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
Diagnosticos legibles.
Presupuestos editables.
Pools con capacidad maxima.
Medicion por tendencias.
Profiler como validacion externa.
```

## TBD

Decisiones abiertas:

```text
Valores iniciales exactos de presupuesto RAM/GPU.
Contadores exactos de ProfilerRecorder que usaremos.
Formato final de exportacion de snapshots si hace falta.
Si los presupuestos finales seran assets o datos runtime.
```

Decision inicial no bloqueante:

```text
Medir primero con estimacion propia.
Usar Unity Profiler como validacion externa.
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
Los diagnosticos traducen numeros a accion.
Los stress tests miden tiempo, memoria y recursos vivos.
```

No se pasa fuerte a `05_Forma_Planeta_GPU`, `06_Proxy_Planeta_Lejano` o `07_Payload_Triangulos` sin esta base integrada en `PlanetImplementationLab`.
