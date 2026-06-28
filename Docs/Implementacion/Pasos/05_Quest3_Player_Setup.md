# 05 - Quest3 Player Setup

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

## Objetivo

Preparar el proyecto y `PlanetImplementationLab` para poder ejecutarse como experiencia VR basica en Meta Quest 3.

Esta fase no valida todos los sistemas tecnicos. Solo crea la base para poder probarlos bien:

```text
Play en Editor con Quest Link.
Camara VR estereo funcional.
Movimiento basico de camara/player para recorrer el Lab.
UI de pruebas clicable con rayo.
Proyecto preparado para generar APK/Player para Quest 3.
```

Regla:

```text
El documento 05 prepara el player y la interaccion de pruebas.
La validacion completa de memoria, compute, release, snapshots y stress se hace en el deadline ejecutando todas las pruebas definidas.
```

## Alcance de esta fase

Entra:

```text
Configurar XR para Quest 3.
Configurar Play en Editor con Quest Link.
Usar el stack Meta/OpenXR como base XR.
Configurar una camara/player VR basico para el Lab.
Permitir mover la camara de forma normal dentro de PlanetImplementationLab.
Preparar interaccion por rayo para pulsar UI de pruebas.
Crear una UI sencilla para lanzar botones de Lab sin depender solo del Inspector.
Preparar el proyecto para generar APK/Player Android.
Cambiar el proyecto a Android como plataforma de build.
Configurar el package id debug temporal `com.Perodry.debug`.
Documentar el flujo minimo para probar en Quest 3.
```

La UI de pruebas debe permitir ejecutar de forma sencilla los botones importantes de los Labs ya definidos:

```text
Validate.
Init.
Run test.
Run stress.
Capture snapshot/metrics.
Reload Test Budget.
Release.
Release All.
Export snapshot si aplica.
```

## Fuera de alcance

No entra:

```text
Gameplay.
Locomocion final.
Manos finales.
UI final de juego.
Arte final de UI.
Optimizacion final de render.
Forma procedural completa del planeta.
Marching Cubes.
Proxy lejano final.
Streaming.
Colisiones finales.
Terraformado.
Validar todos los presupuestos de memoria.
Cerrar disponibilidad real de todos los ProfilerRecorder.
Capturar todos los snapshots finales de memoria.
```

Este documento no sustituye a `Docs/Implementacion/Pasos/04_Gestion_RAM_VRAM.md`. Solo da una forma usable de ejecutar sus pruebas en VR/Quest.

## Relacion con otros documentos

Documentos base:

```text
Docs/Definicion_Tecnica_Proyecto.md
Docs/Teoria_Implementacion.md
Docs/Implementacion/Pasos_de_Implementacion.md
Docs/Implementacion/Pasos/01_PlanetImplementationLab.md
Docs/Implementacion/Pasos/02_ComputeShaderLab.md
Docs/Implementacion/Pasos/03_Coordenadas_Y_Receta.md
Docs/Implementacion/Pasos/04_Gestion_RAM_VRAM.md
```

Este documento desbloquea la ejecucion comoda de pruebas en:

```text
PlanetImplementationLab.
Deadline del primer bloque.
```

No desbloquea por si solo:

```text
06_Forma_Planeta_GPU.
07_Proxy_Planeta_Lejano.
08_Payload_Triangulos.
```

Esos pasos se desbloquean cuando el deadline completo pase todas las pruebas definidas.

## Datos de entrada

El sistema necesita:

```text
Escena PlanetImplementationLab.
Configuracion XR del proyecto.
Quest 3 con Link para probar desde Editor.
Configuracion Android preparada para APK/Player.
Lista de modulos Lab disponibles.
Botones/acciones expuestos por los Labs.
Presupuesto de memoria por defecto si el Lab de memoria ya esta integrado.
Override externo opcional para pruebas de presupuesto si el Lab de memoria ya esta integrado.
```

## Datos de salida

Debe producir:

```text
PlanetImplementationLab usable desde Quest Link en Play Mode.
Camara/player VR basico funcional.
Movimiento basico de camara/player.
UI de pruebas visible y clicable con rayo.
Proyecto preparado para generar APK/Player Android.
Build target Android configurado.
Package id debug temporal configurado.
Checklist de setup Quest 3 visible en Lab.
Diagnostico claro si falta XR, camara, input, UI o build target.
```

No debe producir:

```text
Datos finales de memoria.
Validacion final de stress.
Gameplay.
Sistemas de planeta.
```

## Componentes/scripts previstos

### PlanetMinimalXrRig

Rig basico de VR para el Lab.

Responsabilidad:

```text
Contener camara XR.
Permitir orientacion normal con el HMD.
Actualizar posicion/rotacion de camara desde CenterEye.
Actualizar posicion/rotacion de marcadores de mano desde LeftHand y RightHand.
Mostrar una esfera primitiva por mano.
Mostrar un rayo por mano.
Permitir hover y click sobre UI de Unity con el gatillo.
No ser controlador final de gameplay.
No contener logica de planeta.
```

Regla:

```text
Este rig existe para probar el Lab.
Si mas adelante se crea un player real, no se reutiliza automaticamente como player final.
```

Decision inicial:

```text
Usar Unity OpenXR Plugin como base XR.
Version inicial instalada en el proyecto: com.unity.xr.openxr 1.17.1.
Unity 6000.3.11f1 permite esta version porque el paquete declara compatibilidad minima Unity 2022.3.
No usar prerelease de OpenXR salvo decision explicita.
Mantener OpenXR como direccion base para no cerrar la puerta a paquetes OpenXR futuros.
Usar un rig minimo propio para el Lab en vez de OVRComprehensiveInteractionRig.
No meter building blocks completos de Meta si anaden objetos o sistemas que no son necesarios para esta fase.
```

Decision de limpieza de arranque en Editor:

```text
MetaXRFeature queda desactivado solo para Standalone/Editor en OpenXRPackageSettings.
MetaXRFeature Android queda activado para la ruta de APK/Quest.
```

Motivo:

```text
El rig minimo del Lab no depende de OVRPlugin ni de building blocks de Meta.
En Quest Link/Editor, MetaXRFeature Standalone inicializa OVRPlugin y puede emitir warnings de audio output driver y funciones espaciales no soportadas como xrRequestSceneCaptureFB.
Para este paso se prioriza un Play Mode limpio con Unity OpenXR, HMD, mandos, rayos y UI.
Si mas adelante se necesita una API especifica de Meta en Standalone, se reactivara esa feature y se documentara el nuevo requisito.
```

Nota:

```text
El nombre exacto del paquete puede variar por version de Unity/Meta.
Al implementar se debe usar el paquete oficial vigente de Meta para Quest/OpenXR y documentar el nombre exacto instalado.
```

### PlanetMinimalXrRig Ray UI

Interaccion por rayo para pulsar UI de pruebas.

Responsabilidad:

```text
Emitir rayo desde cada mando/mano trackeada.
Detener el rayo visual en la UI apuntada.
Interactuar con botones UI del Lab.
Ejecutar pointer enter/exit para hover.
Ejecutar pointer down/up/click con gatillo.
No contener logica de los tests.
```

Decision inicial:

```text
El mecanismo principal sera propio y minimo sobre UnityEngine.XR, GraphicRaycaster y EventSystem.
No se usara OVRComprehensiveInteractionRig para esta validacion.
Si mas adelante se necesita interaccion avanzada de manos/controladores, se documentara como cambio de alcance.
```

### PlanetLabVRControlPanel

Panel UI de pruebas para VR.

Responsabilidad:

```text
Mostrar acciones principales de los Labs.
Agrupar botones por modulo.
Mostrar ultimo diagnostico breve.
Permitir ejecutar pruebas sin Inspector.
Ser simple, legible y facil de pulsar con rayo.
```

Regla:

```text
El panel llama a sistemas/Labs existentes.
El panel no implementa la logica de memoria, compute, snapshots ni release.
```

Decision de forma:

```text
El panel sera un Canvas fisico en mundo.
Estara delante del player.
Debe colocarse de forma que no interfiera con las pruebas visuales ni tape el objeto/sistema bajo test.
El panel vive como GameObject normal de escena en PlanetImplementationLab.
No se crea en runtime al entrar en Play, porque debe poder moverse y ajustarse comodamente desde Scene View antes de probar con gafas.
```

Estructura:

```text
Panel izquierdo:
- botones grandes para pruebas manuales.
- para el deadline de payload, replica la forma del Inspector de PlanetRecipePayloadPreview:
  Apply Payload 126k, Apply Payload 250k, Apply Payload 500k, Apply Payload 1M, Apply Payload 2M, Apply Payload 5M.
  Before Snapshot, After Snapshot.
  Generate, Release.

Zona central:
- queda libre para ver el objeto/sistema bajo test.
- la esfera generada debe aparecer delante del player, sin quedar tapada por el Canvas.

Panel derecho:
- muestra informacion tecnica importante que no se puede leer directamente en la grafica de Quest 3.
- para el deadline de payload, muestra requested triangles, triangles reales, vertices, indices, frecuencia geodesica, GridRadius, WorldScale, WorldRadius, surfaceRadius, IsoLevel, estado de mesh vivo, ultimo operationMs si existe, bytes CPU/GPU estimados si existen y ultimo diagnostico.
- tambien muestra el modo de color debug usado por PlanetRecipePayloadPreview.
- para ver mejor la triangulacion en Quest, el modo recomendado del deadline es TrianglePalette: cada triangulo se pinta con un color plano elegido de una paleta corta, asumiendo el coste extra de duplicar vertices por triangulo.
```

Regla:

```text
La UI VR no inventa logica distinta.
Puede agrupar comandos existentes en una accion compuesta cuando el deadline lo documenta de forma explicita.
Cada pantalla replica la intencion del CustomEditor correspondiente.
Si un boton existe en Inspector para una prueba importante, debe poder existir tambien en la pantalla VR de ese modulo.
Los botones Apply Payload de VR solo seleccionan el payload, igual que el Inspector.
El boton Generate de VR llama a PlanetRecipePayloadPreview.Generate, que libera la prueba previa antes de generar.
El boton Release de VR llama a PlanetRecipePayloadPreview.Release.
El panel derecho lee el diagnostico y snapshots del mismo PlanetRecipePayloadPreview.
```

### PlanetQuestPlayerSetupLab

Script aditivo del Lab para validar setup de player.

Responsabilidad:

```text
Mostrar estado de XR.
Mostrar estado de Quest Link/Play Mode si se puede detectar.
Mostrar estado de build target Android.
Mostrar referencias del rig, camara, ray interactor y panel UI.
Ejecutar Validate Quest Player Setup.
No ejecutar stress ni validaciones completas de memoria.
```

### PlanetQuestPlayerSetupLabEditor

`CustomEditor` nativo para validar el setup desde Inspector.

Responsabilidad:

```text
Mostrar botones de setup.
Mostrar checklist.
Mostrar errores accionables.
No usar assets externos de inspector.
```

## Flujo funcional

Flujo en Editor con Quest Link:

```text
1. Conectar Quest 3 por Link.
2. Abrir PlanetImplementationLab.
3. Pulsar Validate Quest Player Setup.
4. Entrar en Play Mode.
5. Ponerse las gafas.
6. Confirmar render estereo.
7. Mover la camara/player por el Lab.
8. Apuntar con el rayo a la UI de pruebas.
9. Pulsar botones simples del Lab.
10. Confirmar que la UI responde y muestra diagnosticos.
```

Flujo de preparacion APK:

```text
1. Cambiar build target a Android.
2. Configurar package id debug temporal `com.Perodry.debug`.
3. Validar configuracion XR para Quest 3.
4. Incluir PlanetImplementationLab en escenas de build o flujo equivalente.
5. Generar APK/Player de prueba.
6. Instalar y abrir en Quest 3 cuando toque ejecutar el deadline.
```

Package id inicial:

```text
com.Perodry.debug
Temporal, debug y no final.
```

Instalacion inicial:

```text
Por ahora se usara SideQuest para instalar el APK.
```

Posible herramienta futura:

```text
Se podra crear una consola externa PowerShell/ADB o batch auxiliar para instalar/lanzar APK.
Esa herramienta sera side project fuera del runtime del juego y no formara parte del motor.
```

Regla:

```text
El setup de player debe estar listo antes de ejecutar el deadline completo en Quest 3.
```

## Gestion de RAM

Esta fase debe ser ligera.

Reglas:

```text
No crear paneles o listas UI nuevas por frame.
No hacer polling de archivo de presupuesto.
No capturar snapshots automaticamente por frame.
No ejecutar stress automaticamente al entrar en Play.
```

La UI puede mostrar diagnosticos recientes, pero debe hacerlo bajo demanda o con datos ya existentes.

## Gestion de VRAM

La UI y el rig deben tener coste minimo.

Reglas:

```text
No crear RenderTextures nuevas para el panel salvo necesidad documentada.
No instanciar materiales por frame.
No cargar assets pesados para una UI de pruebas.
No convertir el panel en una escena de showcase.
```

## Liberacion de recursos

El rig y la UI deben poder desactivarse sin dejar recursos propios vivos.

Reglas:

```text
Si el panel crea recursos runtime, los registra y libera.
Si solo usa componentes/asset references de escena, no inventa Release artificial.
Release All de los Labs no debe depender del player rig.
El player rig no debe impedir Release All.
```

## Botones de Inspector

Botones esperados en `PlanetQuestPlayerSetupLab`:

```text
Validate Quest Player Setup
Show XR Status
Show Build Target Status
Show VR UI References
Focus VR Control Panel
Reset Player Rig Pose
```

Botones esperados en UI VR, agrupados por modulo disponible:

```text
Validate
Init
Run Test
Run Stress Low
Capture Metrics/Snapshot
Reload Test Budget
Export Snapshot
Release
Release All
```

Botones esperados para el panel de payload del deadline:

```text
Apply Payload 126k
Apply Payload 250k
Apply Payload 500k
Apply Payload 1M
Apply Payload 2M
Apply Payload 5M
Before Snapshot
After Snapshot
Generate
Release
```

Regla:

```text
La UI VR debe exponer los mismos comandos o acciones compuestas documentadas desde el Inspector/sistema del modulo.
No debe explicar el sistema con texto largo.
Los diagnosticos e informacion lateral si pueden mostrarse como resumen corto.
Generate ejecuta Release de la prueba activa antes de crear la nueva esfera porque delega en PlanetRecipePayloadPreview.Generate.
```

## Pruebas manuales

Pruebas minimas:

```text
Quest Link conecta con el Editor.
Play Mode muestra PlanetImplementationLab en las gafas.
La vista es estereo y estable.
La camara responde al movimiento de cabeza.
El movimiento basico del rig funciona.
El rayo apunta a la UI.
El rayo pulsa botones de la UI.
Un boton Validate de algun Lab responde.
Un boton Release All responde.
Los botones Apply Payload 126k, Apply Payload 250k, Apply Payload 500k, Apply Payload 1M, Apply Payload 2M y Apply Payload 5M son visibles en el panel izquierdo del modo payload.
Los botones Before Snapshot, After Snapshot, Generate y Release son visibles en el panel izquierdo del modo payload.
Aplicar un payload y pulsar Generate libera la esfera anterior antes de crear la nueva.
La esfera generada aparece delante del player y no queda tapada por el Canvas.
El panel derecho actualiza requested triangles, triangles reales, vertices/indices, memoria estimada si existe y ultimo diagnostico.
La UI muestra un diagnostico corto.
El proyecto queda preparado para generar APK/Player.
El package id debug temporal queda configurado.
El APK se puede instalar con SideQuest.
```

## Tests automatizados

Tests EditMode esperados:

```text
PlanetQuestPlayerSetupLab detecta referencias nulas.
PlanetLabVRControlPanel puede registrar acciones sin duplicarlas.
PlanetLabVRControlPanel puede limpiar acciones.
```

Tests PlayMode esperados:

```text
PlanetQuestPlayerSetupLab existe en PlanetImplementationLab cuando se integre.
Validate Quest Player Setup no lanza excepcion.
Reset Player Rig Pose no lanza excepcion.
```

Tests en Quest 3:

```text
Inicialmente seran pruebas manuales.
La automatizacion en dispositivo queda TBD.
```

## Metricas

Metricas iniciales:

```text
Estado XR.
Estado build target.
Estado de referencias de rig/UI/rayo.
Ultimo comando ejecutado desde UI VR.
Ultimo diagnostico corto.
Allocations visibles si se puede medir durante interaccion simple.
```

No se miden aqui como criterio de cierre del documento:

```text
Presupuestos finales de RAM/GPU.
Disponibilidad completa de ProfilerRecorder.
Resultado final de stress.
```

Eso se valida en el deadline ejecutando todas las pruebas definidas.

## Riesgos

Riesgos principales:

```text
Confundir setup de player con gameplay.
Meter la logica de los Labs dentro de la UI.
Depender solo del Inspector y no poder probar comodamente con gafas.
Crear una UI pesada que contamine las pruebas.
Ejecutar stress automaticamente al entrar en Play.
Pensar que este documento valida memoria por si solo.
```

Mitigaciones:

```text
El rig es tecnico y temporal.
La UI solo invoca comandos existentes.
La validacion completa vive en el deadline.
La UI se mantiene simple y ligera.
Los stress se ejecutan solo por boton.
```

## TBD

Decisiones abiertas:

```text
Automatizacion futura de pruebas en Quest 3.
Nombre exacto/version del paquete Meta SDK instalado para building blocks de player/rayo.
```

Decision inicial no bloqueante:

```text
Priorizar Quest Link + Play Mode para iterar rapido.
Preparar APK/Player pero no convertir este documento en validacion completa de memoria.
Usar Meta XR / OpenXR como stack base.
Usar un rig minimo propio para player/ray interactor de Lab.
Usar un Canvas fisico delante del player.
Replicar en cada pantalla VR los botones e informacion del Inspector correspondiente.
Instalar APK con SideQuest por ahora.
Dejar posible herramienta ADB/PowerShell como auxiliar externa futura.
Mantener todo como Lab/setup, no gameplay final.
```

## Criterio de cierre

Este documento queda listo para implementar cuando aceptemos este contrato:

```text
PlanetImplementationLab se puede usar en Play Mode con Quest Link.
La camara VR estereo funciona.
La camara/player se puede mover de forma basica.
Hay UI de pruebas clicable con rayo.
Los comandos principales de Lab se pueden lanzar desde esa UI.
El proyecto queda preparado para generar APK/Player de Quest 3.
La validacion completa de sistemas queda para el deadline, no para este documento.
```
