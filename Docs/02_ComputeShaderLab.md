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
05_Forma_Planeta_GPU
06_Proxy_Planeta_Lejano
07_Payload_Triangulos
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

La salida visible puede ser simple:

```text
RenderTexture con patron generado por compute.
Mesh debug con datos calculados por compute.
Buffer de valores mostrado parcialmente en Inspector.
```

La primera opcion preferida es una `RenderTexture` debug porque permite ver resultado sin depender todavia de geometria procedural.

## Componentes/scripts previstos

### PlanetComputeShaderLabModule

Modulo principal del laboratorio para esta fase.

Responsabilidad:

```text
Heredar de PlanetLabModule.
Validar soporte de Compute Shader.
Crear recursos de prueba.
Ejecutar dispatches.
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
```

### PlanetComputeShaderLabEditor

`CustomEditor` nativo de Unity para `PlanetComputeShaderLabModule`.

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

Puede empezar como campos serializados dentro del modulo. Si crece demasiado, se separara en `ScriptableObject`.

Responsabilidad:

```text
Definir tamaños.
Definir numero de dispatches.
Definir modo de buffer.
Definir salida visible.
Definir limites de seguridad.
```

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

### PlanetComputeBufferHandle

Dato o helper pequeño para guardar informacion de un buffer vivo.

Responsabilidad:

```text
Tipo de buffer.
Numero de elementos.
Stride.
Bytes estimados.
Nombre debug.
Estado vivo/liberado.
```

Este helper no sustituye al `PlanetLabResourceRegistry`; solo facilita que el modulo sepa que ha creado.

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
PlanetComputeShaderLabModule crea.
PlanetComputeShaderLabModule reutiliza.
PlanetComputeShaderLabModule libera o limpia referencias.
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
PlanetComputeShaderLabModule existe en la escena de laboratorio.
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
Formato exacto del Compute Shader minimo.
Si la salida visible inicial sera RenderTexture o mesh debug.
Tamaño inicial de presets Low/Medium/High/Extreme.
Si PlanetComputeLabSettings nace como campos serializados o ScriptableObject.
Uso de ProfilerRecorder.
Medicion GPU fiable en PC.
Medicion GPU fiable en Quest 3.
Exportar snapshots a archivo.
```

Decision inicial no bloqueante:

```text
Empezar con RenderTexture debug.
Usar GraphicsBuffer como camino principal.
Usar ComputeBuffer solo para comparativa.
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

No se pasa a `05_Forma_Planeta_GPU` si antes no sabemos crear, usar, medir y liberar recursos GPU con seguridad suficiente.
