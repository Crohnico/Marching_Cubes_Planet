# 02 - ComputeShaderLab

## Objetivo

Definir el primer modulo tecnico para aprender, probar y cerrar el camino minimo de trabajo con Compute Shaders dentro de `PlanetImplementationLab`.

Este documento no define el planeta. Define la base que despues usaran el planeta, Marching Cubes, proxies, queries y culling:

```text
CPU -> datos -> GPU buffer -> Dispatch -> resultado -> medicion -> Release
```

El objetivo real es demostrar que sabemos crear recursos GPU, usarlos, medirlos, reutilizarlos y liberarlos sin fugas evidentes ni basura accidental.

## Alcance de esta fase

Entra:

```text
Modulo de laboratorio para Compute Shader.
Compute Shader minimo.
Kernel de prueba simple.
Buffers GPU de prueba.
RenderTexture o salida visible simple.
Comparativa basica GraphicsBuffer vs ComputeBuffer.
Registro de recursos vivos.
Estimacion de memoria GPU.
Stress test de crear/usar/liberar.
Botones de Inspector con CustomEditor nativo.
Tests basicos de vida de recursos.
```

La prueba debe vivir dentro de:

```text
PlanetImplementationLab
```

Puede haber assets auxiliares, pero el cierre ocurre cuando el modulo queda integrado en el laboratorio comun.

## Fuera de alcance

No entra:

```text
Forma real del planeta.
Marching Cubes.
Dual Contouring.
Chunks.
Cuevas.
Minerales/sustancias.
Colisiones.
Terraformado.
Streaming.
Shader final del juego.
Optimizacion final de Quest 3.
```

Tampoco entra cerrar todavia una medicion perfecta de VRAM real. En esta fase se trabaja con estimacion propia y observacion desde herramientas de Unity.

## Relacion con otros documentos

Documentos base:

```text
Docs/Definicion_Tecnica_Proyecto.md
Docs/Teoria_Implementacion.md
Docs/Implementacion_Funcional.md
Docs/01_PlanetImplementationLab.md
```

Este documento prepara:

```text
03_Coordenadas_Y_Receta
04_Gestion_RAM_VRAM
05_Quest3_Player_Setup
06_Forma_Planeta_GPU
07_Proxy_Planeta_Lejano
08_Payload_Triangulos
```

## Datos de entrada

El modulo necesita:

```text
ComputeShader de prueba.
Kernel seleccionado.
Tamaño de buffer.
Tamaño de textura de salida si aplica.
Numero de dispatches por prueba.
Preset de stress.
Tipo de buffer a probar.
ResourceRegistry del laboratorio.
```

Parametros iniciales recomendados:

```text
bufferElementCount
dispatchRepeatCount
outputTextureWidth
outputTextureHeight
useGraphicsBuffer
useComputeBufferComparison
releaseBetweenCycles
```

## Datos de salida

El modulo debe producir:

```text
Resultado visible de que la GPU ha trabajado.
Ultimo tiempo CPU de preparacion.
Ultimo tiempo CPU de dispatch solicitado.
Memoria GPU estimada.
Numero de buffers vivos.
Numero de RenderTextures vivas si aplica.
Estado de soporte runtime.
Diagnostico legible del ultimo test.
```

La salida visible tendra dos versiones iniciales:

```text
Version 1 -> Mesh debug.
Version 2 -> RenderTexture debug.
```

Motivo:

```text
Mesh es una via critica futura para proxies, chunks, colisiones y debug.
RenderTexture es una via critica futura para impostores y debug GPU directo.
Queremos dejar vivas las dos rutas desde el inicio.
```

La `Mesh debug` de esta fase no valida todavia la generacion real de geometria desde GPU. Sirve para confirmar que el laboratorio puede mostrar geometria controlada, registrar meshes runtime y liberarlas.

La `RenderTexture debug` valida la escritura directa desde Compute Shader y prepara el camino conceptual para impostores.

## Formato exacto del Compute Shader minimo

El primer Compute Shader del proyecto sera una prueba minima de pipeline GPU.

No se considera una prueba real de carga del motor porque no tiene suficiente "grosor" tecnico:

```text
No genera geometria.
No evalua campo escalar.
No trabaja sobre grid 3D.
No compacta datos.
No hace Marching Cubes.
No valida payload de triangulos.
No simula chunks reales.
```

Sirve para empezar la implementacion porque valida lo basico:

```text
Unity carga un ComputeShader.
C# crea y registra un GraphicsBuffer.
C# crea y registra una RenderTexture escribible.
GPU escribe en buffer y textura.
El resultado se puede ver en RenderTexture.
El Lab puede mostrar tambien una Mesh debug registrada y liberable.
El dispatch se puede repetir.
Los recursos se pueden liberar.
El stress inicial no deja fugas evidentes.
```

Archivo propuesto:

```text
Assets/Shaders/Compute/PlanetComputeDebug.compute
```

Kernel inicial:

```text
CS_DebugWrite
```

Recursos del shader:

```text
RWStructuredBuffer<float4> _DebugSamples
RWTexture2D<float4> _DebugTexture

int _BufferCount
int _TextureWidth
int _TextureHeight
uint _Seed
uint _DispatchIndex
```

Thread group inicial:

```text
[numthreads(64, 1, 1)]
```

Motivo:

```text
64,1,1 es simple.
Encaja bien con un buffer lineal.
Permite aprender el flujo base sin mezclar todavia grids 2D/3D.
Nos obliga a calcular correctamente el numero de grupos desde C#.
```

Regla:

```text
El tamaño del grupo se declara en el shader.
C# debe consultarlo con GetKernelThreadGroupSizes.
C# no debe asumir a mano que siempre sera 64.
```

El kernel debe usar un indice lineal:

```text
index = SV_DispatchThreadID.x
```

Debe escribir:

```text
_DebugSamples[index] si index < _BufferCount.
_DebugTexture[x, y] si index < _TextureWidth * _TextureHeight.
```

Regla obligatoria:

```text
Todo acceso a buffer o textura debe comprobar limites.
```

Motivo:

```text
El dispatch se lanza por grupos.
Normalmente habra mas threads lanzados que elementos reales.
En DX11 algunos accesos fuera de rango pueden parecer inocuos.
En mobile/Vulkan/GLES/Metal pueden romper o producir comportamiento indefinido.
```

Salida esperada:

```text
Un patron de color visible en RenderTexture.
Valores float4 escritos en el buffer.
Una Mesh debug visible, creada y liberada por el Lab.
Misma seed + mismo dispatchIndex produce mismo resultado.
Cambiar seed o dispatchIndex cambia el patron.
```

Formato de dato inicial:

```text
float4
stride = 16 bytes
```

Motivo:

```text
Es facil de alinear.
Es facil de inspeccionar.
Evita empezar con structs complejos antes de dominar el pipeline.
```

Este paso debe quedar bien cerrado antes de pasar a pruebas de mas grosor.

### Version 1 - Mesh debug

La primera via visible sera una Mesh debug pequeña y controlada.

Objetivo:

```text
Validar que el Lab puede crear, mostrar, registrar y liberar una Mesh runtime.
Validar materiales debug.
Validar conteo de vertices/indices.
Validar Release de Mesh.
```

No objetivo:

```text
No validar Marching Cubes.
No validar triangulacion GPU.
No hacer readback de GPU para rellenar la Mesh.
No usar SetVertices como prueba de rendimiento principal de Compute Shader.
```

Regla:

```text
La Mesh debug de esta fase puede ser CPU/simple.
No se traen datos GPU a CPU solo para mostrar esta Mesh.
```

La optimizacion de Mesh sin GC queda documentada en `Docs/Teoria_Implementacion.md`.

En esta fase no se usa como prueba principal porque el objetivo del documento 02 es validar el pipeline Compute Shader y la vida de recursos GPU. La ruta sin GC de Mesh se usara cuando haya geometria CPU real, proxies CPU, colision local o fallback CPU.

### Version 2 - RenderTexture debug

La segunda via visible sera una RenderTexture escrita por el Compute Shader.

Objetivo:

```text
Validar escritura directa GPU -> RenderTexture.
Mostrar resultado sin readback.
Preparar el camino para impostores.
Validar enableRandomWrite.
Registrar y liberar RenderTexture runtime.
```

Uso futuro:

```text
Impostores astronomicos.
Capturas de planeta lejano.
Texturas cacheadas de representaciones baratas.
Debug visual de kernels.
```

Regla:

```text
La RenderTexture es la salida principal del kernel minimo.
La Mesh debug acompaña al Lab, pero no demuestra que el compute genere geometria real.
```

## Ampliacion posterior del Compute Shader Lab

El kernel minimo no es suficiente para validar el motor. Despues de cerrarlo, el Lab necesitara al menos una segunda fase de Compute Shader con mas cuerpo.

La segunda prueba se llamara conceptualmente:

```text
VoxelDensityDebug
```

Objetivo:

```text
Validar un grid 3D de densidad en GPU sin entrar todavia en Marching Cubes.
```

Esta prueba ya se parece mas al problema real porque trabaja con:

```text
Grid 3D.
Indexado 3D -> lineal.
Campo escalar simple.
Clasificacion aire/solido.
Volumen en GPU.
Visualizacion barata por slice.
```

No entra:

```text
Marching Cubes.
Triangulos.
Normales.
Materiales finales.
Chunks reales.
Cuevas.
Ruido continental.
Voronoi.
```

Archivo propuesto:

```text
Assets/Shaders/Compute/PlanetVoxelDensityDebug.compute
```

Kernels propuestos:

```text
CS_WriteSphereDensityGrid
CS_WriteDensitySlice
```

`CS_WriteSphereDensityGrid` calcula un volumen 3D de densidad usando una esfera perfecta:

```text
density = radius - distance(position, center)
```

Clasificacion:

```text
density > 0  -> solido
density <= 0 -> aire
```

Buffers iniciales:

```text
RWStructuredBuffer<float> _DensityBuffer
RWStructuredBuffer<uint> _VoxelStateBuffer
```

`CS_WriteDensitySlice` pinta una seccion 2D del volumen a una `RenderTexture` para debug:

```text
sliceAxis
sliceIndex
DensityBuffer -> RenderTexture debug
```

Desde Inspector se debe poder:

```text
Cambiar tamaño del grid.
Cambiar radio de la esfera.
Cambiar slice visible.
Cambiar eje de slice.
Regenerar densidad.
Repintar slice.
Liberar buffers.
Ejecutar stress.
```

Presets iniciales de grid:

```text
Low:
    gridSize: 32x32x32

Medium:
    gridSize: 64x64x64

High:
    gridSize: 96x96x96

VeryHigh:
    gridSize: 128x128x128

Extreme:
    gridSize: 160x160x160 o 192x192x192
    requiere aviso en Inspector
```

La prueba debe medir:

```text
Elementos totales del grid.
Bytes estimados de DensityBuffer.
Bytes estimados de VoxelStateBuffer.
Tiempo CPU de dispatch.
Buffers vivos.
RenderTextures vivas.
Resultado visual de slice.
Release correcto.
```

Regla:

```text
La segunda prueba no sustituye al documento 06_Forma_Planeta_GPU.
Solo valida la base volumetrica minima.
La forma procedural real del planeta se define e implementa despues.
```

Despues de `VoxelDensityDebug`, las siguientes ampliaciones deberan acercarse mas a cargas reales:

```text
Kernels 2D y/o 3D.
Varios buffers agrupados con criterio.
Pruebas de tamaño parecido a chunks.
Patrones de acceso parecidos a grid voxel.
Escritura de datos estructurados.
Dispatches encadenados si aporta valor.
Medicion de coste al subir volumen de trabajo.
Comparacion GraphicsBuffer vs ComputeBuffer solo donde tenga sentido.
```

La ampliacion no debe colarse dentro del primer kernel minimo. El primer paso debe quedar pequeño, limpio y medible.

## Componentes/scripts previstos

### PlanetComputeShaderRunner

Sistema real minimo para ejecutar un Compute Shader.

Responsabilidad:

```text
Recibir ComputeShader y kernel.
Crear o recibir buffers necesarios.
Configurar parametros.
Ejecutar Dispatch.
Exponer estado minimo de ejecucion.
No depender del laboratorio.
```

Este componente/clase es codigo aprovechable por el motor. El Lab lo usa, pero no lo define.

No debe:

```text
Tener botones de stress.
Conocer presets de laboratorio.
Conocer PlanetImplementationLab.
Mostrar UI.
```

### PlanetGpuBufferHandle

Dato/helper real para guardar informacion de un buffer vivo.

Responsabilidad:

```text
Tipo de buffer.
Numero de elementos.
Stride.
Bytes estimados.
Nombre debug.
Estado vivo/liberado.
Referencia al recurso GPU.
```

Este helper debe poder usarse fuera del Lab.

### PlanetComputeShaderRunnerLab

Modulo principal del laboratorio para esta fase.

Responsabilidad:

```text
Heredar de PlanetLabModule.
Validar soporte de Compute Shader.
Crear recursos de prueba usando codigo real.
Ejecutar dispatches usando PlanetComputeShaderRunner.
Registrar recursos en PlanetLabResourceRegistry.
Capturar metricas.
Liberar recursos.
Exponer botones mediante CustomEditor.
```

No debe:

```text
Implementar planeta.
Implementar Marching Cubes.
Guardar datos persistentes.
Conocer estados del planeta.
Convertirse en manager global.
Contener logica que deba vivir en PlanetComputeShaderRunner.
```

### PlanetComputeShaderLabEditor

`CustomEditor` nativo de Unity para `PlanetComputeShaderRunnerLab`.

Responsabilidad:

```text
Mostrar botones de prueba.
Mostrar estado de soporte.
Mostrar recursos vivos.
Mostrar ultimo diagnostico.
Mostrar memoria estimada.
```

Regla:

```text
No usar assets de terceros para botones de Inspector.
No crear UI en Game View para controlar este modulo.
```

### PlanetComputeLabSettings

Configuracion serializada del modulo.

Debe vivir en el Inspector de `PlanetComputeShaderRunnerLab`.

No debe empezar como `ScriptableObject`, porque es configuracion del arnes de pruebas, no del sistema real.

Responsabilidad:

```text
Definir tamaños.
Definir numero de dispatches.
Definir modo de buffer.
Definir salida visible.
Definir limites de seguridad.
```

Si crece demasiado, se separara en una clase serializable o drawer/editor propio, manteniendolo dentro del Inspector del Lab.

Si aparece una configuracion necesaria para runtime real, se definira aparte como dato del sistema real.

### PlanetComputeLabResultView

Objeto o componente simple para mostrar el resultado visual.

Responsabilidad:

```text
Mostrar RenderTexture debug si existe.
Mostrar mesh/material debug si se usa.
No contener logica de compute.
No crear recursos sin registrarlos.
```

Puede ser un plano en la escena con un material debug o un objeto tecnico equivalente.

Regla de nombres:

```text
PlanetComputeShaderRunner    -> codigo real.
PlanetComputeShaderRunnerLab -> pruebas y stress del codigo real.
PlanetComputeShaderLabEditor -> botones del Lab en Inspector.
```

Si durante la implementacion aparece un nombre mas claro, se puede ajustar, pero debe conservarse la separacion:

```text
Real.
Editor.
Lab.
```

## Flujo funcional

Flujo minimo:

```text
1. Validate Module.
2. Crear recursos GPU.
3. Registrar recursos.
4. Enviar datos iniciales si aplica.
5. Ejecutar Dispatch.
6. Ver resultado visible.
7. Capturar metricas.
8. Ejecutar Release Module.
9. Comprobar que no quedan recursos vivos del modulo.
```

Flujo de stress:

```text
1. Capturar snapshot inicial.
2. Repetir N ciclos:
   - Crear recursos.
   - Ejecutar Dispatch varias veces.
   - Capturar metricas si el preset lo pide.
   - Liberar recursos si el preset lo pide.
3. Release Module.
4. Capturar snapshot final.
5. Generar diagnostico legible.
```

Regla importante:

```text
El modulo debe soportar Init -> Release -> Init.
El modulo debe soportar Release -> Release.
El modulo debe soportar Stress -> Release All.
```

## Gestion de RAM

La fase debe evitar allocations accidentales en caminos repetidos.

Reglas:

```text
No crear arrays nuevos por dispatch.
No usar LINQ en pruebas repetidas.
No crear strings por frame.
No hacer ToArray para pasar datos si se puede evitar.
Usar buffers preasignados para datos CPU grandes.
```

Se permite codigo simple en botones de Inspector y preparacion no caliente, pero los stress tests deben servir para detectar basura.

Los datos CPU de prueba deben tener dueño claro:

```text
PlanetComputeShaderRunnerLab solicita.
PlanetComputeShaderRunner crea/reutiliza cuando aplique.
PlanetComputeShaderRunnerLab libera o pide liberar al sistema real.
PlanetLabResourceRegistry registra estimacion si el dato es grande.
```

## Gestion de VRAM

Decision inicial:

```text
GraphicsBuffer sera el camino principal.
ComputeBuffer se probara como comparativa y fallback temporal.
```

Motivo:

```text
El proyecto apunta a datos GPU-resident que podran acabar conectados a render, vertex/index buffers o draw indirect.
GraphicsBuffer encaja mejor como base general.
ComputeBuffer sigue siendo util para pruebas simples y comparativas.
```

Prueba obligatoria:

```text
Mismo tamaño.
Mismo stride.
Mismo kernel si es posible.
Mismo numero de dispatches.
Medir creacion.
Medir SetData si aplica.
Medir Dispatch.
Medir Release.
Comparar estimacion de memoria.
```

Registro minimo por recurso GPU:

```text
resourceName
resourceType
ownerModule
elementCount
stride
estimatedBytes
isAlive
```

La estimacion inicial:

```text
bufferBytes = elementCount * stride
renderTextureBytes = width * height * bytesPerPixel
```

TBD:

```text
Uso de ProfilerRecorder.
Medicion GPU fiable en PC.
Medicion GPU fiable en Quest 3.
Exportar snapshots a archivo.
```

De momento asumimos que el set de herramientas de Unity sera suficiente para inspeccionar manualmente cuando haga falta.

## Liberacion de recursos

El modulo debe tener liberacion explicita.

Recursos a liberar:

```text
GraphicsBuffer.
ComputeBuffer.
RenderTexture creada en runtime.
Material instanciado si se crea.
Mesh runtime si se crea.
Arrays CPU grandes si quedan retenidos sin necesidad.
```

Reglas:

```text
ReleaseModule puede llamarse dos veces.
ReleaseModule deja el estado interno en conocido.
ReleaseModule actualiza el ResourceRegistry.
OnDisable y OnDestroy llaman ReleaseModule como red de seguridad.
Release All del laboratorio debe liberar este modulo.
```

No se debe depender de cerrar la escena para recuperar memoria.

## Botones de Inspector

Botones del modulo:

```text
Validate Module
Init Module
Create GraphicsBuffer Test
Create ComputeBuffer Test
Dispatch Once
Dispatch 100x
Run GraphicsBuffer Stress
Run ComputeBuffer Stress
Run Stress Low
Run Stress Medium
Run Stress High
Run Stress VeryHigh
Run Stress Extreme
Run Comparison
Capture Module Metrics
Release Module
Reset Module State
```

Botones opcionales:

```text
Create Output Texture
Clear Output Texture
Randomize Input Data
Force Large Buffer
Force Small Buffer
```

Reglas:

```text
Los botones viven en un CustomEditor nativo.
Cada boton deja resultado legible.
Cada boton que cree recursos debe registrarlos.
Cada boton de stress debe terminar con diagnostico.
```

## Pruebas manuales

Pruebas minimas:

```text
Abrir PlanetImplementationLab.
Validate Module detecta si falta ComputeShader.
Init Module crea estado valido.
Dispatch Once genera salida visible.
Dispatch 100x no genera basura evidente.
Release Module libera recursos.
Release Module dos veces no rompe.
Init -> Release -> Init funciona.
GraphicsBuffer Stress no deja recursos vivos.
ComputeBuffer Stress no deja recursos vivos.
Run Comparison muestra resultado legible.
```

Presets iniciales:

```text
Low:
    bufferElementCount: 16k
    outputTexture: 256x256
    dispatchRepeatCount: 1
    cycleCount: 10
    releaseBetweenCycles: true

Medium:
    bufferElementCount: 128k
    outputTexture: 512x512
    dispatchRepeatCount: 10
    cycleCount: 25
    releaseBetweenCycles: true

High:
    bufferElementCount: 512k
    outputTexture: 1024x1024
    dispatchRepeatCount: 50
    cycleCount: 50
    releaseBetweenCycles: true

VeryHigh:
    bufferElementCount: 1M
    outputTexture: 1024x1024
    dispatchRepeatCount: 75
    cycleCount: 75
    releaseBetweenCycles: true

Extreme:
    bufferElementCount: 2M
    outputTexture: 2048x2048
    dispatchRepeatCount: 100
    cycleCount: 100
    releaseBetweenCycles: configurable
```

Intencion de cada preset:

```text
Low      -> smoke test rapido.
Medium   -> carga normal de desarrollo.
High     -> presion seria.
VeryHigh -> prueba pesada controlada de 1M elementos.
Extreme  -> prueba para intentar romper limites.
```

Reglas de cortafuegos:

```text
VeryHigh y Extreme solo se ejecutan con boton manual.
Extreme debe mostrar aviso claro en Inspector.
Ningun stress pesado se ejecuta automaticamente al entrar en Play.
Debe capturar metricas antes y despues.
Debe ejecutar Release al terminar o permitir Release manual inmediato.
Debe existir limite duro configurable desde Inspector.
```

Limites iniciales:

```text
maxBufferElementCount = 2_000_000
maxTextureSize = 2048
maxDispatchRepeatCount = 100
maxCycleCount = 100
```

Estos valores son editables desde el Inspector del Lab. No son constantes del motor.

Pruebas de limite:

```text
Buffer pequeño.
Buffer medio.
Buffer grande.
Dispatch repeat alto.
Crear y liberar en bucle.
Cambiar tipo de buffer tras liberar.
Intentar ejecutar sin Init y obtener diagnostico claro.
```

## Tests automatizados

Tests EditMode esperados:

```text
El calculo de bytes de buffer es correcto.
Los settings rechazan tamaños invalidos.
El handle de buffer cambia a liberado al liberar.
El diagnostico detecta recurso vivo tras Release simulado.
```

Tests PlayMode esperados:

```text
PlanetComputeShaderRunnerLab existe en la escena de laboratorio.
ValidateModule no lanza excepciones.
InitModule no lanza excepciones si hay soporte.
ReleaseModule no lanza excepciones.
ReleaseModule dos veces no lanza excepciones.
InitModule -> ReleaseModule -> InitModule funciona.
```

Tests condicionados:

```text
Si SystemInfo.supportsComputeShaders es false, el modulo debe quedar desactivado con diagnostico claro.
Si el entorno de test no permite Compute Shader real, no se marca como fallo de logica del modulo.
```

## Metricas

Metricas iniciales:

```text
Soporte de Compute Shader.
Soporte de Instancing.
Tipo de buffer usado.
Element count.
Stride.
Bytes estimados.
Buffers vivos.
RenderTextures vivas.
Tiempo CPU de creacion.
Tiempo CPU de SetData.
Tiempo CPU de Dispatch solicitado.
Tiempo CPU de Release.
Allocations detectadas si se puede medir.
Ultimo diagnostico.
```

Metricas que quedan en TBD:

```text
Tiempo GPU real.
Uso de ProfilerRecorder.
Medicion GPU fiable en PC.
Medicion GPU fiable en Quest 3.
Exportar snapshots a archivo.
```

## Validacion de plataforma

El modulo de Compute Shader puede cerrarse primero en PC/Editor para validar flujo basico, recursos, botones, release y stress.

Pero el primer deadline global del proyecto no se cierra hasta tener preparado el setup de player/Quest Link definido en:

```text
Docs/05_Quest3_Player_Setup.md
```

Ese setup permite ejecutar la bateria completa de pruebas del deadline desde Inspector o UI VR.

Las decisiones de este documento siguen considerando Quest 3 desde el principio:

```text
Buffers limitados.
Pocos recursos por kernel.
Sin readback bloqueante.
Sin allocations en caminos calientes.
Release explicito.
Stress con cortafuegos.
```

## Riesgos

Riesgos principales:

```text
Creer que Dispatch es gratis porque solo vemos coste CPU.
Hacer readbacks bloqueantes para comprobar resultados.
No liberar buffers en todos los caminos.
Duplicar datos en CPU y GPU sin necesidad.
Medir VRAM como verdad absoluta cuando solo tenemos estimacion.
Escribir una arquitectura grande antes de dominar el caso minimo.
Contaminar runtime con codigo editor.
```

Mitigaciones:

```text
Mantener el modulo pequeño.
Registrar cada recurso creado.
Separar runtime y editor.
Evitar readback salvo debug controlado.
Comparar tendencias, no solo numeros absolutos.
Stress test antes de construir sistemas encima.
```

## TBD

Decisiones abiertas:

```text
Uso de ProfilerRecorder.
Medicion GPU fiable en PC.
Medicion GPU fiable en Quest 3.
Exportar snapshots a archivo.
```

Decision inicial no bloqueante:

```text
Empezar con Mesh debug y RenderTexture debug.
Usar GraphicsBuffer como camino principal.
Usar ComputeBuffer solo para comparativa.
Usar VoxelDensityDebug como segunda prueba con mas grosor.
Medir memoria GPU por estimacion propia.
Validar manualmente con herramientas de Unity cuando haga falta.
```

## Criterio de cierre

Este documento queda listo para implementar cuando aceptemos este contrato:

```text
El modulo no sabe nada de planetas.
El modulo demuestra Compute Shader basico.
El modulo registra recursos.
El modulo libera recursos.
El modulo tiene botones de Inspector.
El modulo tiene stress tests.
El modulo deja diagnostico legible.
```

No se pasa a `05_Quest3_Player_Setup` ni a `06_Forma_Planeta_GPU` si antes no sabemos crear, usar, medir y liberar recursos GPU con seguridad suficiente.
