# Implementacion funcional

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

Este documento organiza la bajada a codigo del proyecto.

No sustituye a `Docs/Teoria_Implementacion.md`. Lo complementa. La teoria dice en que orden avanzar; este documento define como vamos a escribir los documentos funcionales de cada sistema antes de implementarlo.

La idea es que cada sistema importante tenga su propio documento tecnico-funcional antes de tocar codigo.

## Objetivo

Cada documento funcional debe responder:

```text
Que problema resuelve.
Que entra en esta fase.
Que no entra.
Que datos usa.
Que scripts/clases podrian existir.
Que recursos de RAM/VRAM crea.
Como se liberan esos recursos.
Que botones de Inspector necesita.
Que tests necesita.
Que dudas quedan abiertas.
```

La regla importante:

```text
Primero se baja a documento funcional.
Luego se implementa.
```

Si durante la implementacion aparece una decision nueva, se vuelve al documento y se deja escrita.

## Documentos como instrucciones para el agente

Los documentos funcionales se escriben como instrucciones de implementacion para el agente que vaya a bajar el sistema a codigo.

No son notas generales ni literatura de diseño. Deben permitir implementar sin adivinar intenciones, sin avanzar de mas y sin cerrar decisiones abiertas por accidente.

Regla:

```text
Escribir cada paso como si Codex tuviera que implementarlo despues leyendo solo la documentacion.
```

Esto implica:

```text
El alcance debe decir claramente que entra.
El fuera de alcance debe decir claramente que no se toca todavia.
Los componentes previstos deben separar codigo real, Lab y Editor.
Los flujos deben marcar el orden esperado de uso.
Las reglas de RAM/VRAM deben dejar claro que vive en runtime y que vive solo en Lab/debug.
Los TBD deben quedar escritos y no resolverse implicitamente en codigo.
```

Si una parte no esta escrita, no se debe asumir como cerrada. Se documenta como `TBD`, se propone una decision no bloqueante o se pide confirmacion antes de implementarla.

Objetivo practico:

```text
Evitar que el agente implemente una arquitectura mas grande de la necesaria.
Evitar que mezcle sistemas reales con herramientas de Lab.
Evitar que convierta pruebas, snapshots, diagnosticos o botones en dependencias del runtime oficial.
Evitar que avance al siguiente paso sin haber cerrado medicion, liberacion y pruebas del paso actual.
```

## Formato de cada documento funcional

Cada documento funcional debe seguir esta estructura base.

```text
# Nombre del sistema

## Objetivo
## Alcance de esta fase
## Fuera de alcance
## Relacion con otros documentos
## Datos de entrada
## Datos de salida
## Componentes/scripts previstos
## Flujo funcional
## Gestion de RAM
## Gestion de VRAM
## Liberacion de recursos
## Botones de Inspector
## Pruebas manuales
## Tests automatizados
## Metricas
## Riesgos
## TBD
```

No todos los apartados tienen que ser largos. Si algo no aplica, se marca como `No aplica` o `TBD`, pero no se borra el apartado.

## Nivel de detalle esperado

El documento funcional no debe escribir codigo completo.

Debe llegar hasta este nivel:

```text
Nombre claro de componentes.
Responsabilidad de cada componente.
Datos que entran y salen.
Vida de recursos.
Pruebas necesarias.
Decisiones abiertas.
```

Ejemplo de nivel correcto:

```text
PlanetComputeLabController:
- vive en la escena PlanetImplementationLab.
- crea y libera buffers de prueba.
- expone botones de Init, Dispatch, Release y Stress.
- no conoce Marching Cubes.
```

Ejemplo de nivel demasiado bajo:

```text
Escribir aqui toda la clase con sus metodos.
```

Eso va en codigo, no en documento.

## Escena principal

Todos los sistemas del primer bloque se integran en:

```text
PlanetImplementationLab
```

Los documentos funcionales pueden proponer escenas auxiliares para investigar, pero el cierre real ocurre al integrar la capacidad en esa escena.

## Regla de Labs

Los documentos con nombre `Lab` no definen sistemas temporales que luego haya que rehacer.

Definen un arnes de pruebas alrededor de codigo real.

Patron esperado:

```text
SistemaReal
SistemaRealEditor
SistemaRealLab
```

Donde:

```text
SistemaReal    -> codigo que puede llegar al juego.
SistemaRealEditor -> inspector/editor nativo si aporta claridad.
SistemaRealLab -> botones, stress tests, metricas y diagnosticos.
```

Regla:

```text
El Lab no contiene la logica principal.
El Lab no es la fuente de verdad.
El Lab llama al sistema real y lo fuerza.
El sistema real no debe depender del Lab.
```

## Orden de documentos funcionales

El orden inicial sera:

```text
01_PlanetImplementationLab
02_ComputeShaderLab
03_Coordenadas_Y_Receta
04_Gestion_RAM_VRAM
05_Quest3_Player_Setup
Deadline_01_05_Base_Validation
06_Forma_Planeta_GPU
07_Proxy_Planeta_Lejano
08_Payload_Triangulos
09_Estados_Planeta
10_Chunks_Locales
11_Streaming_Prioridades
12_Cuevas
13_Minerales_Sustancias
14_Colisiones_Locales
15_Terraformado
16_Persistencia
```

El orden puede cambiar si aparece un bloqueo tecnico, pero no se debe saltar a codigo de un sistema importante sin su documento funcional.

## 01 - PlanetImplementationLab

Documento para definir la escena tecnica acumulativa.

Debe concretar:

```text
Objetos principales de la escena.
Componentes debug.
Paneles o inspectors necesarios.
Botones comunes.
Como se capturan metricas.
Como se fuerza limpieza de recursos.
Como se evita que la escena se convierta en un caos.
```

Preguntas a resolver:

```text
Usamos solo Inspector o tambien una ventana Editor?
Como mostramos contadores de RAM/VRAM estimada?
Como registramos recursos vivos?
Donde vive el estado del laboratorio?
```

## 02 - ComputeShaderLab

Documento para cerrar el primer contacto con Compute Shaders.

Debe concretar:

```text
Buffers minimos.
Kernel minimo.
Dispatch.
Salida visible.
AsyncGPUReadback solo para debug.
Liberacion segura.
Stress test de crear/liberar.
```

Preguntas a resolver:

```text
ComputeBuffer o GraphicsBuffer para cada caso?
Como estimamos VRAM ocupada?
Como registramos buffers vivos?
Como evitamos readback bloqueante?
```

## 03 - Coordenadas y receta

Documento para definir datos base de planeta.

Debe concretar:

```text
PlanetRecipe.
GridCoordinates.
WorldSpaceCoordinates.
WorldScale.
GridRadius.
WorldRadius derivado.
Conversiones.
Validaciones de editor.
```

Preguntas a resolver:

```text
Usamos int, float, double o combinacion?
Donde vive la semilla?
Como se prepara esto para floating origin futuro?
```

## 04 - Gestion RAM/VRAM

Documento transversal obligatorio.

Debe concretar:

```text
Registro de recursos vivos.
Buffers CPU.
Buffers GPU.
Meshes.
Render textures.
Pools.
Presupuestos.
Liberacion.
Metricas.
```

Preguntas a resolver:

```text
Como medimos VRAM real o estimada?
Que recursos se registran manualmente?
Que pasa si se supera presupuesto?
Como se prueba que Release funciona?
```

## 05 - Quest3 Player Setup

Documento para preparar el player VR basico, Quest Link, UI de pruebas con rayo y build Android/Quest antes de ejecutar el deadline completo.

Debe concretar:

```text
Configuracion Android/XR.
Generacion de APK/Player.
Play en Editor con Quest Link.
Camara/player VR basico.
Movimiento normal de camara/player en el Lab.
UI de pruebas clicable con rayo.
Botones sencillos para lanzar pruebas de los Labs.
```

Preguntas a resolver:

```text
Que paquete/componentes XR usamos para el rig?
Como movemos la camara/player en el Lab?
Como se implementa el rayo de interaccion?
Como agrupamos los botones de pruebas sin duplicar logica de los Labs?
```

Regla:

```text
El documento 05 prepara la forma de probar.
El deadline ejecuta todas las pruebas y queda bloqueado hasta que pasen.
```

## Deadline 01-05 - Validacion de base

Documento para cerrar el gate entre los documentos `01` a `05` y el inicio de `06_Forma_Planeta_GPU`.

Debe concretar:

```text
Lista completa de checks.
Bloqueantes automaticos.
Evidencias minimas.
Definition of Done.
Resultado de ejecucion del deadline.
```

Regla:

```text
Este documento no define un sistema nuevo del motor.
Define la prueba que decide si se puede empezar el documento 06.
```

## 06 - Forma planeta GPU

Documento para bajar a codigo la funcion de forma exterior.

Debe concretar:

```text
Parametros enviados a GPU.
surfaceOffset(direction).
effectiveRadius(direction).
density(point).
Debug de muestras.
Determinismo por seed.
```

Preguntas a resolver:

```text
Que parte se calcula CPU y que parte GPU?
Como se pasa Voronoi esferico a GPU?
Como validamos que la forma coincide con la teoria?
```

## 07 - Proxy planeta lejano

Documento para render lejano barato.

Debe concretar:

```text
Impostor.
Low-res mesh.
Gradiente/atlas.
Material solido.
Material agua.
Umbrales por tamaño angular.
```

Preguntas a resolver:

```text
Primero hacemos mesh low-res o impostor?
Como evitamos problemas de Z-buffer?
Como cacheamos la representacion?
```

## 08 - Payload triangulos

Documento para presupuesto de geometria.

Debe concretar:

```text
Unidad de presupuesto.
Triangulos maximos.
Vertices maximos.
Degradacion.
Stress tests.
Metricas por payload.
```

Preguntas a resolver:

```text
El presupuesto se reparte por planeta, estado o sistema estelar?
Como evitamos saltos visuales?
Como se prioriza lo visible?
```

## Regla de cierre

Un documento funcional esta listo para implementar cuando:

```text
Tiene objetivo claro.
Tiene alcance cerrado.
Tiene fuera de alcance.
Tiene componentes previstos.
Tiene pruebas manuales.
Tiene tests automatizados.
Tiene politica de RAM/VRAM.
Tiene TBD explicitos.
```

Si no se puede escribir alguno de esos puntos, la tarea todavia esta verde.

## Regla de actualizacion

Cuando el codigo contradiga este documento o un documento funcional:

```text
Se actualiza la documentacion.
O se cambia el codigo.
Pero no se deja contradiccion silenciosa.
```

La documentacion no debe ser literatura muerta. Tiene que ser el mapa que usamos para programar.

