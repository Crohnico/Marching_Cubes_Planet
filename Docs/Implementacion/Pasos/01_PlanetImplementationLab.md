# 01 - PlanetImplementationLab

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

## Objetivo

Definir la escena tecnica acumulativa donde vamos a construir, probar y medir la base del motor del planeta.

Esta escena es el laboratorio principal del proyecto:

```text
PlanetImplementationLab
```

Todos los deadlines del primer bloque deben cerrarse dentro de esta escena. Puede haber pruebas auxiliares, pero el cierre real ocurre aqui.

La escena debe permitir:

```text
Inicializar sistemas.
Ejecutar pruebas manuales.
Estresar casos extremos.
Capturar metricas.
Liberar RAM y VRAM.
Validar que lo anterior sigue funcionando.
```

## Alcance de esta fase

Esta fase define la estructura del laboratorio, no la implementacion completa de los sistemas futuros.

Entra:

```text
Escena tecnica principal.
Objeto raiz del laboratorio.
Controlador principal de laboratorio.
Botones comunes de Inspector.
Registro inicial de recursos vivos.
Captura basica de metricas.
Sistema de presets de stress.
Punto unico para Release All.
Contratos para conectar futuros modulos.
```

La escena debe poder crecer con:

```text
ComputeShaderLab.
Coordenadas y receta.
Forma de planeta en GPU.
Proxy lejano.
Payload de triangulos.
Estados del planeta.
Chunks locales.
```

## Fuera de alcance

No entra en este documento:

```text
Implementar Marching Cubes.
Implementar forma real del planeta.
Implementar shaders finales.
Implementar streaming.
Implementar cuevas.
Implementar colisiones.
Implementar terraformado.
Crear UI final de juego.
Crear gameplay.
```

El laboratorio no debe convertirse en el motor entero. Solo coordina pruebas y expone controles.

Regla de diseño:

```text
El laboratorio es aditivo al codigo real.
No es una version temporal del motor.
```

Patron esperado:

```text
ClaseReal
ClaseRealEditor
ClaseRealLab
```

Donde:

```text
ClaseReal      -> sistema definitivo o reutilizable por gameplay/runtime.
ClaseRealEditor -> inspector/editor nativo si hace falta.
ClaseRealLab   -> botones, stress tests, metricas y diagnosticos.
```

Regla:

```text
Si se borra ClaseRealLab, ClaseReal sigue existiendo y funcionando.
Ningun algoritmo importante debe vivir solo en un Lab.
El Lab solo invoca, mide, fuerza y valida codigo real.
```

## Relacion con otros documentos

Documentos base:

```text
Docs/Definicion_Tecnica_Proyecto.md
Docs/Teoria_Implementacion.md
Docs/Implementacion/Pasos_de_Implementacion.md
Docs/Implementacion/Calculo_Funcional_Datos_Planeta.md
```

Documentos que dependen de este:

```text
02_ComputeShaderLab
03_Coordenadas_Y_Receta
04_Gestion_RAM_VRAM
05_Quest3_Player_Setup
06_Forma_Planeta_GPU
07_Proxy_Planeta_Lejano
08_Payload_Triangulos
```

## Datos de entrada

La escena debe recibir o contener:

```text
Configuracion del laboratorio.
Presets de stress.
Presupuesto inicial de RAM/VRAM estimada.
Presupuesto inicial de triangulos.
Referencias a camara y luces.
Referencias a materiales debug.
Referencias a modulos conectados.
```

En fases posteriores tambien recibira:

```text
PlanetRecipe.
GridRadius.
WorldScale.
Seed.
Parametros de superficie.
```

## Datos de salida

La escena debe producir informacion medible:

```text
Estado actual del laboratorio.
Metricas de frame.
Metricas de recursos vivos.
RAM estimada usada por sistemas registrados.
VRAM estimada usada por buffers/meshes/texturas registrados.
Numero de buffers vivos.
Numero de meshes vivas.
Numero de render textures vivas.
Triangulos visibles.
Resultado de la ultima prueba manual.
Resultado del ultimo stress test.
```

No es obligatorio medir VRAM real en esta fase. Si Unity no da un dato fiable, se registra estimacion por recurso.

## Componentes/scripts previstos

### PlanetImplementationLabController

Objeto principal de la escena.

Responsabilidad:

```text
Coordinar botones globales.
Inicializar el laboratorio.
Ejecutar validaciones generales.
Capturar metricas.
Llamar a Release All.
No implementar logica interna de cada sistema.
```

No debe ser un manager gigante. Debe delegar en modulos especializados.

### PlanetLabModule

Clase abstracta base para modulos del laboratorio.

Responsabilidad:

```text
Exponer Init.
Exponer Release.
Exponer CaptureMetrics si aplica.
Exponer Validate si aplica.
Declarar si tiene recursos vivos.
```

Decision inicial:

```csharp
public abstract class PlanetLabModule : MonoBehaviour
{
    public abstract string ModuleName { get; }
    public abstract bool HasLiveResources { get; }

    public abstract void InitModule();
    public abstract void ReleaseModule();
    public abstract bool ValidateModule();
    public abstract PlanetLabMetricsSnapshot CaptureMetrics();

    public virtual void RunModuleTest() { }
    public virtual void RunModuleStress() { }
}
```

Motivo:

```text
Estos modulos viven en una escena Unity.
Necesitan referencias de Inspector.
Necesitan botones de editor.
El controlador puede agruparlos como PlanetLabModule[].
No tenemos todavia un caso real de reutilizacion fuera del laboratorio.
```

Regla:

```text
PlanetLabModule debe ser fino.
No debe convertirse en una base pesada.
No debe contener logica especifica de Compute, planetas, chunks o render.
RunModuleTest y RunModuleStress solo coordinan acciones manuales genericas y pueden ser sobrescritos por cada modulo.
```

Si mas adelante estos contratos se necesitan fuera del laboratorio, en clases puras o en sistemas que no sean `MonoBehaviour`, se extraera una interfaz:

```csharp
public interface IPlanetLabModule
{
    string ModuleName { get; }
    bool HasLiveResources { get; }

    void InitModule();
    void ReleaseModule();
    bool ValidateModule();
    PlanetLabMetricsSnapshot CaptureMetrics();
}
```

En ese caso, `PlanetLabModule : MonoBehaviour, IPlanetLabModule`.

### PlanetLabResourceRegistry

Registro temporal de recursos vivos.

Responsabilidad:

```text
Registrar buffers CPU.
Registrar buffers GPU.
Registrar meshes.
Registrar render textures.
Registrar materiales instanciados si existen.
Estimar RAM/VRAM.
Mostrar contadores.
Ayudar a detectar fugas.
```

Este componente sera refinado en `04_Gestion_RAM_VRAM`.

### PlanetLabMetricsSnapshot

Dato simple de metricas capturadas.

Responsabilidad:

```text
Guardar una captura puntual.
Permitir comparar antes/despues.
Mostrar ultimo resultado en Inspector.
```

Debe ser pequeño y claro.

### PlanetLabDiagnostics

Traductor de metricas a diagnostico legible.

Responsabilidad:

```text
Comparar snapshots antes/despues.
Aplicar reglas de interpretacion.
Generar mensajes legibles para Inspector/debug view.
Separar numeros crudos de conclusion accionable.
```

La salida debe tener:

```text
severity
title
probableCause
recommendedAction
relatedMetrics
```

Severidades iniciales:

```text
OK
Info
Warning
Critical
```

Ejemplos de traduccion:

```text
Regla:
Si nuestra estimacion sube y no baja tras Release All.

Inspector:
Critical - Posible fuga propia de recursos
Causa probable: hay buffers/meshes/render textures registrados que no se liberaron.
Accion: revisar ReleaseModule del ultimo modulo ejecutado y el ResourceRegistry.
```

```text
Regla:
Si Unity sube y no baja pero nuestra estimacion esta a cero.

Inspector:
Warning - Memoria no atribuida a recursos registrados
Causa probable: recurso interno de Unity, driver, asset no registrado o referencia perdida.
Accion: abrir Unity Profiler/Memory Profiler y revisar objetos vivos.
```

```text
Regla:
Si ambas suben con un stress test.

Inspector:
Warning - Presupuesto excedido durante stress
Causa probable: el preset fuerza mas memoria de la permitida o no libera entre ciclos.
Accion: revisar PlanetLabStressPreset y presupuesto del modulo.
```

```text
Regla:
Si hay pico de GC.

Inspector:
Warning - Allocations detectadas en camino de prueba
Causa probable: listas, strings, LINQ, ToArray o crecimiento de colecciones.
Accion: revisar el modulo ejecutado y mover datos calientes a buffers preasignados.
```

Regla:

```text
Las pruebas de la escena deben mostrar diagnostico legible, no solo numeros.
```

### PlanetLabStressPreset

Dato serializado de configuracion para stress tests dentro del Inspector del Lab.

Responsabilidad:

```text
Definir intensidad.
Definir numero de ciclos.
Definir tamaños de buffer.
Definir payload de triangulos futuro.
Definir si se fuerza liberar entre ciclos.
```

Decision:

```text
PlanetLabStressPreset no sera un ScriptableObject.
Vivira como dato serializado dentro del Lab que lo usa.
```

Motivo:

```text
Los presets de stress pertenecen al arnes de pruebas.
No son parte del sistema real del juego.
No queremos generar assets sueltos para configuracion que solo existe para machacar el Lab.
Queremos editar Low/Medium/High/Extreme directamente en el Inspector del Lab.
```

Presets iniciales esperados:

```text
Stress Low
Stress Medium
Stress High
Stress Extreme
```

Campos minimos previstos:

```text
presetName
whatItTests
expectedResult
riskCovered
cycleCount
bufferElementCount
dispatchRepeatCount
trianglePayloadLimit
releaseBetweenCycles
captureMetricsEachCycle
```

Los campos descriptivos deben verse en el Inspector para saber que se esta probando sin abrir codigo ni documentacion:

```text
whatItTests    -> que intenta comprobar este preset.
expectedResult -> que deberia pasar si todo va bien.
riskCovered    -> que riesgo tecnico intenta detectar.
```

Ejemplo:

```text
presetName: Stress Extreme
whatItTests: crear, usar y liberar buffers grandes repetidamente.
expectedResult: no quedan buffers vivos tras Release All y no hay pico inesperado de memoria.
riskCovered: fuga de VRAM o duplicacion accidental de buffers.
```

En codigo, estos campos deberian tener ayuda de Inspector:

```text
[TextArea] para descripciones largas.
[Tooltip] para explicar cada campo numerico.
```

Regla:

```text
El preset define datos.
No ejecuta pruebas.
No crea recursos.
No libera recursos.
No vive como asset.
```

La ejecucion vive en el Lab o controlador que recibe el preset.

Si en el futuro aparece una configuracion reutilizable por el juego real, se creara otro tipo de dato separado. No se reutilizara automaticamente el preset del Lab como configuracion runtime.

### PlanetLabCameraRig

Camara tecnica de laboratorio.

Responsabilidad:

```text
Dar una vista estable para pruebas.
Permitir mover o simular distancia.
Preparar pruebas de planeta lejano.
```

No es controlador final de jugador.

### PlanetLabDebugView

Vista debug opcional.

Responsabilidad:

```text
Mostrar texto o gizmos con estado basico.
Mostrar recursos vivos.
Mostrar metricas recientes.
```

Decision:

```text
No habra UI en escena para controlar el laboratorio.
La vista principal sera el Inspector de Unity.
Los controles se haran con CustomEditor nativo.
Los Gizmos quedan permitidos solo para visualizacion espacial si hace falta.
```

## Flujo funcional

Flujo basico esperado:

```text
1. Abrir PlanetImplementationLab.
2. Pulsar Validate Scene.
3. Pulsar Init Lab.
4. Ejecutar una prueba manual.
5. Capturar metricas.
6. Ejecutar stress test.
7. Capturar metricas.
8. Pulsar Release All.
9. Capturar metricas.
10. Repetir el ciclo sin acumulacion inesperada.
```

Flujo de cierre de cada deadline:

```text
1. Integrar el nuevo modulo en la escena.
2. Exponer sus botones.
3. Registrar sus recursos.
4. Añadir sus pruebas manuales.
5. Añadir sus tests automatizados.
6. Verificar Release All.
7. Capturar metricas.
```

## Gestion de RAM

El laboratorio no debe crear datos grandes sin registrarlos.

Reglas:

```text
Todo buffer CPU grande se registra.
Toda lista grande debe tener capacidad controlada.
Los stress tests no deben crear basura por frame.
Las capturas de metricas deben ser puntuales, no por frame si generan allocations.
```

Inicialmente se permite medir RAM de forma aproximada:

```text
bytes estimados por arrays.
bytes estimados por buffers propios.
contadores de objetos vivos.
```

TBD:

```text
Medicion exacta con ProfilerRecorder.
Que metricas de memoria se exponen en runtime.
Que metricas solo se consultan en editor.
```

## Gestion de VRAM

La VRAM se gestionara primero por estimacion.

Cada recurso GPU registrado debe declarar:

```text
tipo de recurso.
numero de elementos.
stride.
bytes estimados.
modulo propietario.
estado vivo/liberado.
```

Recursos a registrar:

```text
ComputeBuffer.
GraphicsBuffer.
Mesh.
RenderTexture.
Texture creada en runtime.
```

Decision inicial de buffers GPU:

```text
GraphicsBuffer sera el buffer principal para datos GPU del motor.
ComputeBuffer queda permitido para pruebas simples, comparativas, fallback temporal o casos puramente compute donde no toque render.
```

Motivo:

```text
El proyecto quiere datos GPU-resident.
Muchos buffers podran acabar participando en render, geometria, vertex/index data o draw indirect.
GraphicsBuffer cubre compute y tambien casos de geometria/render.
ComputeBuffer es mas especifico para compute.
```

Regla:

```text
No duplicar el mismo dato en ComputeBuffer y GraphicsBuffer salvo prueba medida.
No usar ComputeBuffer por comodidad si ese dato va a terminar alimentando render.
No convertir esta decision en dogma sin profiler en Quest 3.
```

El hilo de Unity "Buffers and instancing" refuerza la idea conceptual:

```text
ComputeBuffer puede entenderse como caso especifico dentro del espacio de buffers graficos.
GraphicsBuffer tambien puede representar buffers de vertices o indices.
Indirect permite dibujar mas alla del limite clasico de DrawMeshInstanced.
En mobile hay que comprobar soporte runtime.
```

Comprobaciones runtime obligatorias:

```text
SystemInfo.supportsComputeShaders
SystemInfo.supportsInstancing
```

Benchmark obligatorio en `02_ComputeShaderLab`:

```text
Misma prueba con ComputeBuffer.
Misma prueba con GraphicsBuffer.
Medir creacion.
Medir SetData.
Medir Dispatch.
Medir Release.
Medir memoria estimada.
Confirmar en Quest 3.
```

Decision de medicion de VRAM/memoria grafica:

```text
No vamos a tratar "VRAM real" como una cifra absoluta fiable en Quest 3.
Quest 3 usa memoria compartida/unificada, no una VRAM dedicada tipo PC.
La metrica principal del proyecto sera memoria GPU estimada y registrada por nosotros.
Unity Profiler se usara como validacion externa, no como unica fuente de verdad.
```

Capas de medicion:

```text
1. Estimacion propia por recursos que creamos.
2. Contadores de Unity Profiler/ProfilerRecorder.
3. Capturas manuales con Unity Profiler y Memory Profiler cuando haga falta.
4. Validacion en Quest 3 con build real.
```

La estimacion propia registra:

```text
GraphicsBuffer:
    elementCount * stride

ComputeBuffer:
    elementCount * stride

RenderTexture:
    width * height * bytesPerPixel * slices/mips aproximados

Texture runtime:
    width * height * bytesPerPixel * mips aproximados

Mesh runtime:
    vertexBufferBytes + indexBufferBytes
```

Contadores Unity que nos interesan:

```text
Rendering / Used Buffers
Rendering / Render Textures
Rendering / Triangles
Rendering / Vertices
Rendering / Draw Calls
Memory / Graphics & Graphics Driver
Memory / Texture Memory
Memory / Mesh Memory
Memory / Total Used Memory
Memory / GC Used Memory
Memory / GC Allocated In Frame
```

Regla de sincronizacion:

```text
Cada PlanetLabMetricsSnapshot guarda nuestra estimacion y los contadores Unity disponibles en el mismo momento.
No esperamos que los numeros coincidan exactamente.
Comparamos tendencias y deltas antes/despues de cada operacion.
```

Ejemplo de snapshot:

```text
ownedGpuEstimatedBytes
ownedCpuEstimatedBytes
unityRenderUsedBuffersBytes
unityRenderTextureBytes
unityGraphicsDriverBytes
unityTextureMemoryBytes
unityMeshMemoryBytes
unityTotalUsedMemoryBytes
unityGcUsedBytes
unityGcAllocFrameBytes
```

Regla de interpretacion:

```text
Si nuestra estimacion sube y no baja tras Release All -> fuga propia probable.
Si Unity sube y no baja pero nuestra estimacion esta a cero -> recurso Unity/internal, driver o referencia no registrada.
Si ambas suben con un stress test -> revisar presupuesto.
Si hay pico de GC -> revisar allocations del camino de prueba.
```

Estas reglas no deben quedarse solo como texto de documentacion. Deben implementarse como diagnosticos visibles en el `CustomEditor`, `PlanetLabDebugView` o sistema equivalente.

Cada diagnostico debe mostrar:

```text
Estado.
Causa probable.
Accion recomendada.
Metricas relacionadas.
```

Esta decision se refinara en `04_Gestion_RAM_VRAM`.

## Liberacion de recursos

La escena debe tener un boton global:

```text
Release All
```

Este boton debe:

```text
Llamar Release en todos los modulos registrados.
Liberar buffers GPU.
Liberar meshes creadas en runtime.
Liberar render textures.
Limpiar referencias internas.
Actualizar contadores.
Capturar metricas despues de liberar.
```

Reglas:

```text
Release debe poder llamarse dos veces sin romper.
Init despues de Release debe funcionar.
Cambiar de modo o stress test no debe dejar recursos viejos vivos.
OnDisable/OnDestroy pueden ser red de seguridad, pero no sustituyen al Release explicito.
```

## Botones de Inspector

Botones globales esperados:

```text
Validate Scene
Init Lab
Release All
Capture Metrics
Run Smoke Test
Run Stress Low
Run Stress Medium
Run Stress High
Run Stress Extreme
Force GC Check
Force GPU Release
Reset Lab State
```

Botones por modulo:

```text
Init Module
Run Module Test
Run Module Stress
Capture Module Metrics
Release Module
Validate Module
```

Decision:

```text
Usar Inspector de Unity.
Usar CustomEditor nativo y simple.
No usar assets/librerias externas de inspector.
No crear UI en Game View para estos controles.
```

Nota:

```text
Cuando se hablaba de "libreria" se referia a assets de terceros tipo Odin Inspector o NaughtyAttributes.
Quedan descartados para el laboratorio.
```

## Pruebas manuales

Pruebas minimas de esta fase:

```text
Abrir escena sin errores.
Validate Scene detecta referencias nulas importantes.
Init Lab puede ejecutarse.
Init Lab dos veces no duplica estado.
Capture Metrics genera una captura visible.
Release All puede ejecutarse.
Release All dos veces no rompe.
Stress Low ejecuta varios ciclos.
Stress Extreme no deja recursos vivos fuera de presupuesto.
Reset Lab State deja la escena en estado conocido.
```

Pruebas de integracion futura:

```text
Cada modulo nuevo aparece en el laboratorio.
Cada modulo nuevo responde a Release All.
Cada modulo nuevo reporta metricas.
Cada modulo nuevo tiene al menos un stress test manual.
```

## Tests automatizados

Tests PlayMode esperados:

```text
La escena PlanetImplementationLab carga.
Existe PlanetImplementationLabController.
Validate Scene devuelve ok en escena bien configurada.
Init Lab no lanza excepcion.
Release All no lanza excepcion.
Init -> Release -> Init funciona.
Release -> Release funciona.
```

Tests EditMode esperados:

```text
PlanetLabMetricsSnapshot calcula bytes correctamente.
PlanetLabResourceRegistry suma recursos correctamente.
PlanetLabResourceRegistry queda a cero tras liberar registros mock.
StressPreset valida valores minimos/maximos.
```

Decision aplicada:

```text
Usar Unity Test Framework.
Los tests de escena viven en Assets/Tests/PlayMode.
Los tests puros viven en Assets/Tests/EditMode.
```

Test de evidencia aplicado:

```text
PlanetImplementationLabEvidencePlayModeTests ejecuta Validate Scene, Smoke Test, Stress Low/Medium/High/Extreme, Force GC Check, Force GPU Release y Release/Reset.
El resultado se guarda como JSON en Assets/Resources/PlanetLabReports/PlanetImplementationLab_SmokeEvidence.json.
El test valida que cada paso termina sin excepcion, con diagnostico OK y sin recursos registrados vivos.
Esta exportacion pertenece al arnes de validacion del Lab, no al runtime final del juego.
```

## Metricas

Metricas iniciales:

```text
Frame time aproximado.
FPS aproximado.
RAM estimada registrada.
VRAM estimada registrada.
Buffers GPU vivos.
Meshes runtime vivas.
RenderTextures vivas.
Triangulos visibles.
Ultimo tiempo de operacion manual.
Allocations detectadas si se puede medir.
Diagnostico legible del ultimo snapshot.
```

TBD:

```text
Uso de ProfilerRecorder.
Medicion GPU fiable en PC.
Medicion GPU fiable en Quest 3.
Exportar historicos comparables de snapshots a archivo.
```

## Riesgos

Riesgos principales:

```text
Que PlanetImplementationLab se convierta en un manager dios.
Que cada prueba cree su propia mini-arquitectura aislada.
Que las metricas de VRAM sean falsas o incompletas.
Que Release All parezca funcionar pero queden recursos vivos.
Que el codigo editor se mezcle con runtime.
Que los stress tests generen basura y contaminen las mediciones.
```

Mitigaciones:

```text
El controlador principal solo coordina.
Cada sistema vive en su modulo.
Cada recurso grande se registra.
Cada modulo implementa Release.
Cada deadline se valida en esta escena.
Los datos editor-only se separan del runtime cuando toque.
```

## TBD

Decisiones abiertas:

```text
Estructura exacta de carpetas para scripts runtime/editor/tests.
Como mediremos VRAM real en Quest 3.
Como se exportaran historicos comparables de metricas si hace falta compararlas.
```

Decision inicial no bloqueante:

```text
Empezar simple con Inspector y CustomEditor.
Medir VRAM por estimacion propia.
Registrar todos los recursos grandes manualmente.
Separar logica runtime y botones editor desde el principio.
```
