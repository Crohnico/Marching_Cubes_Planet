# Teoria de implementacion

## Nota de estado

Los documentos funcionales de los pasos 01 a 12 se han cerrado y consolidado.

El mapa vivo para continuar el proyecto es:

```text
Docs/Implementacion/Estado_Actual_Proyecto.md
```

Las referencias historicas de este documento a pasos concretos del bloque 01-12
se conservan solo como contexto de origen, no como plan vigente.

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

Este documento esta escrito como guia de trabajo para implementar el proyecto paso a paso sin perder el foco.

No es un documento de deseo. Es una escalera tecnica. Cada paso debe dejar una demo, una medicion y una decision clara antes de pasar al siguiente.

La prioridad del proyecto sigue siendo:

```text
Meta Quest 3
90 FPS estables
RAM controlada
VRAM controlada
sin basura accidental de GC en runtime caliente
sin sistemas gigantes antes de tener metricas
```

## Regla principal

No se avanza al siguiente paso si el paso actual no tiene:

```text
Demo tecnica visible.
Botones de prueba en Inspector.
Pruebas automatizables cuando tenga sentido.
Medicion de RAM.
Medicion de VRAM o memoria GPU estimada.
Medicion de CPU/GPU frame time.
Politica de liberacion probada.
Lista de decisiones TBD actualizada.
```

Cada sistema debe poder forzarse, romperse y medirse desde el editor. Necesitamos botones para darle caña a los casos extremos sin depender de gameplay.

## Escena tecnica acumulativa

Los deadlines no deben resolverse como escenas separadas que luego haya que juntar.

La implementacion debe crecer sobre una misma escena tecnica principal:

```text
PlanetImplementationLab
```

Esa escena sera el banco de pruebas del motor. Cada paso añade una capacidad nueva encima de lo anterior, manteniendo los botones, metricas y sistemas de liberacion ya existentes.

Regla:

```text
No se valida un deadline en una demo aislada si luego no vive dentro de la escena tecnica acumulativa.
```

Se pueden crear escenas auxiliares pequeñas para investigar algo concreto, pero el cierre real de cada paso ocurre cuando esa capacidad esta integrada en `PlanetImplementationLab`.

La escena debe permitir:

```text
Probar el planeta desde lejos.
Forzar estados.
Cambiar presupuesto.
Generar y liberar recursos.
Capturar metricas.
Estresar casos extremos.
Validar que lo anterior no se ha roto.
```

El objetivo es que cada deadline construya el proyecto real, no una coleccion de pruebas que despues haya que reconciliar.

## Regla de Lab aditivo

El laboratorio no debe ser una version temporal del sistema real.

La regla de implementacion es:

```text
ClaseReal      -> sistema definitivo o reutilizable por el juego.
ClaseRealEditor -> inspector/editor nativo si hace falta.
ClaseRealLab   -> script aditivo para probar, medir y estresar ClaseReal.
```

Ejemplo conceptual:

```text
PlanetGpuBuffer
PlanetGpuBufferEditor
PlanetGpuBufferLab
```

El Lab puede tener botones, presets de stress, diagnosticos y casos extremos. La logica importante debe vivir en la clase real, no en el Lab.

Reglas:

```text
El juego no depende de los Labs.
Los Labs dependen del codigo real.
Si se borra un Lab, no se pierde funcionalidad del motor.
Ningun algoritmo importante vive solo dentro de un Lab.
El Lab solo orquesta pruebas sobre sistemas reales.
```

## Regla de codigo

El codigo debe ser facil de leer, corregir y seguir.

Siempre que sea razonable:

```text
scripts de 60 a 400 lineas
nombres claros
responsabilidad unica
minimos comentarios
sin capas innecesarias
sin abstracciones prematuras
```

El rango de lineas no es una meta artificial. Es una alarma de complejidad.

Si un script necesita ser mas largo por trabajar con DOTS, Jobs, Burst, Compute Shaders, tablas de Marching Cubes o integracion tecnica pesada, se permite. Pero debe tener una razon clara.

Preferencia:

```text
Codigo directo.
Nombres que expliquen la intencion.
Funciones pequeñas cuando reduzcan ruido real.
Comentarios solo si aclaran una decision no obvia.
```

Si un comentario es necesario porque hay contexto de arquitectura, debe apuntar al documento correspondiente:

```text
// Ver Docs/Teoria_Implementacion.md - Paso 4
// Ver Docs/Implementacion/Calculo_Funcional_Datos_Planeta.md - Campo escalar
```

No queremos codigo críptico para ahorrar lineas. Tampoco queremos codigo inflado con defensas, wrappers o comentarios que no aportan.

## Regla de memoria

Cada paso que cree datos en CPU o GPU debe definir:

```text
Quien crea el recurso.
Quien es dueño del recurso.
Cuando se reutiliza.
Cuando se libera.
Como se comprueba que se ha liberado.
Que pasa si se pide mas memoria de la permitida.
```

Nada debe quedarse vivo "porque luego igual hace falta".

En caminos calientes:

```text
No List<T> creciendo sin control.
No ToArray.
No LINQ.
No strings por frame.
No new por frame.
No readback GPU bloqueante.
```

Para datos grandes o repetidos:

```text
buffer preasignado + count
pool
ring buffer
presupuesto maximo explicito
```

## Consultas espaciales sin GC

Los sistemas no deben pedir terreno cercano creando listas nuevas ni recorriendo objetos de escena.

La forma base de consultar el mundo sera pedir celdas del grid por volumen:

```text
Dame todas las cells contenidas o intersectadas por una esfera de centro X y radio Y.
```

Regla:

```text
La consulta se resuelve en GridCoordinates.
Si el origen viene de mundo Unity, primero se convierte WorldSpacePosition -> GridPosition.
La salida escribe en un buffer preasignado y devuelve count.
No se crea List<T> nueva en caminos calientes.
```

Esto aplica a:

```text
colisiones cercanas.
terraformado.
herramientas del jugador.
rayos/escaneos.
streaming local.
cache de chunks.
minerales/sustancias consultadas por zona.
```

La consulta debe declarar su intencion:

```text
CenterInside   -> la celda cuenta si su centro cae dentro del volumen.
Intersects     -> la celda cuenta si su volumen toca el volumen consultado.
FullyContained -> la celda cuenta si esta completamente dentro del volumen.
```

Para una celda 1x1x1, su punto representativo es:

```text
cellCenter = cellCoordinates + 0.5
```

`CenterInside` es barato y sirve para muchas herramientas. `Intersects` es mas conservador y sirve para no perder terreno cercano en colision, streaming o terraformado. `FullyContained` es mas estricto y solo debe usarse cuando el borde no importe.

Regla:

```text
Si ocultar o ignorar una celda puede romper gameplay o hacer desaparecer algo visible, usar Intersects.
```

La consulta puede hacer una primera pasada por AABB para limitar el rango de celdas y despues filtrar por esfera. La version vigente de coordenadas y estado del proyecto se resume en `Docs/Implementacion/Estado_Actual_Proyecto.md`.

## Mesh sin GC

Cuando una malla se construye o modifica desde CPU, se debe evitar generar basura accidental.

La direccion preferida es:

```text
List<T> preasignada o NativeArray persistente.
Capacidad maxima conocida.
Clear + rellenado manual.
Uso de overloads con start/length cuando aplique.
Mesh.MarkDynamic si la malla cambia con frecuencia.
Evitar propiedades antiguas que devuelven arrays y generan allocations.
```

Ejemplo conceptual:

```text
vertices = List<Vector3>(maxVertices)
uvs      = List<Vector4>(maxVertices)
indices  = List<int>(maxIndices)

mesh.SetVertices(vertices, 0, vertexCount, flags)
mesh.SetUVs(0, uvs, 0, vertexCount, flags)
mesh.SetTriangles(indices, 0, indexCount, submesh, calculateBounds: false, baseVertex: 0)
```

Esta optimizacion aplica cuando:

```text
La geometria nace o se modifica en CPU.
Hay meshes debug actualizadas a menudo.
Hay proxies CPU.
Hay meshes de colision locales.
Hay fallback CPU.
Hay chunks que por decision concreta se bajan a Mesh clasica.
```

No aplica como solucion principal cuando:

```text
El dato nace y vive en GPU.
El render lee directamente de GraphicsBuffer.
El resultado es una RenderTexture.
El objetivo del test es validar Compute Shader, no actualizar Mesh desde CPU.
La malla es estatica o se crea una vez sin coste relevante.
```

Regla:

```text
No traer datos de GPU a CPU solo para poder usar SetVertices.
Si el camino natural es GPU -> GraphicsBuffer -> render, se prueba ese camino.
Si el camino natural es CPU -> Mesh, se usa la ruta sin GC.
```

## Herramientas comunes desde el principio

Antes de construir sistemas grandes, necesitamos herramientas pequeñas que acompañen todo el desarrollo.

Scripts/ventanas tecnicas esperadas:

```text
PlanetDebugRunner
GpuBufferDebugger
MemoryBudgetDebugger
PlanetStressTester
PlanetProfileSnapshot
```

Botones de Inspector esperados:

```text
Init
Generate
Release
Regenerate
Stress Low
Stress Medium
Stress High
Stress Extreme
Capture Metrics
Force GC Check
Force GPU Release
Validate Determinism
```

Los nombres exactos pueden cambiar, pero la intencion no:

```text
Poder probar cada sistema a mano, rapido y a lo bestia.
```

Tests esperados:

```text
EditMode tests para datos puros.
PlayMode tests para vida de componentes y escenas tecnicas.
Tests de determinismo por seed.
Tests de conversion GridCoordinates <-> WorldSpaceCoordinates.
Tests de limites de buffers.
Tests de liberar y volver a crear recursos.
```

Los tests no sustituyen al profiler, pero evitan romper contratos basicos mientras avanzamos.

## Paso 0 - Laboratorio minimo de Compute Shader

Objetivo:

```text
Aprender y cerrar el camino minimo CPU -> GPU -> resultado visible.
```

Que se implementa:

```text
Una escena tecnica limpia.
Un Compute Shader minimo.
Un script controlador.
Creacion y liberacion explicita de buffers.
Un resultado visible simple.
Medicion basica.
```

Pruebas desde Inspector:

```text
Crear buffers.
Ejecutar Dispatch una vez.
Ejecutar Dispatch muchas veces.
Cambiar tamaño de buffer.
Liberar buffers.
Crear/liberar en bucle.
Forzar caso extremo.
Capturar metricas.
```

Tests automatizables:

```text
Crear y destruir controlador sin excepciones.
Inicializar dos veces no duplica recursos vivos.
Release dos veces no rompe.
Los tamaños de buffer coinciden con lo solicitado.
```

Metricas:

```text
CPU ms del Dispatch.
GPU ms si se puede medir.
RAM antes/despues.
Memoria GPU estimada por buffers.
Allocations por frame.
```

Criterio de cierre:

```text
Sabemos crear, usar, reutilizar y liberar buffers GPU.
Sabemos evitar readbacks bloqueantes.
Tenemos un patron minimo para el resto del proyecto.
```

No entra:

```text
Planeta real.
Marching Cubes.
Cuevas.
Materiales.
Streaming.
```

## Paso 1 - Coordenadas, escala y receta minima

Objetivo:

```text
Cerrar la base de datos minima antes de generar nada serio.
```

Que se implementa:

```text
PlanetRecipe
GridCoordinates
WorldSpaceCoordinates
WorldScale
conversiones basicas
validaciones de rango en herramientas/editor
```

Valores iniciales:

```text
GridRadius = 1000
WorldScale = 4
WorldRadius = GridRadius * WorldScale
```

Regla:

```text
El planeta se calcula en GridCoordinates.
Se monta/renderiza en WorldSpaceCoordinates.
WorldRadius no se guarda como verdad independiente.
```

Pruebas desde Inspector:

```text
Cambiar GridRadius.
Cambiar WorldScale.
Mostrar WorldRadius derivado.
Convertir punto Grid -> World.
Convertir punto World -> Grid.
Reset a valores demo.
```

Tests automatizables:

```text
Grid -> World -> Grid conserva valor dentro de tolerancia.
WorldRadius siempre deriva de GridRadius * WorldScale.
GridRadius invalido se rechaza en herramientas.
WorldScale invalido se rechaza en herramientas.
```

Criterio de cierre:

```text
No hay ambiguedad entre coordenada logica y coordenada visual.
El resto de sistemas puede depender de esta base.
```

## Presets procedurales de planeta

Los planetas seran procedurales. La fuente de verdad no sera una malla guardada a mano ni un volumen completo persistido.

Ademas de la receta concreta de un planeta, existiran presets o perfiles procedurales que definiran familias de planetas:

```text
Forma tipo luna.
Planeta con crateres.
Planeta sin agua.
Planeta sin atmosfera.
Planeta mas o menos acuoso.
Planeta solo agua.
Planeta gaseoso.
Planeta toxico.
Otros perfiles futuros.
```

Regla:

```text
PlanetPreset define una familia o intencion procedural.
PlanetRecipe define un planeta concreto instanciado desde seed, preset y parametros.
```

Los presets no se definen en detalle en este documento. Se documentaran cuando empecemos a bajar la generacion de planetas y sus perfiles.

## Paso 2 - Funcion del planeta en GPU

Objetivo:

```text
Implementar en GPU la funcion que define la forma exterior del planeta.
```

Fuente teorica:

```text
Docs/Implementacion/Calculo_Funcional_Datos_Planeta.md
```

Funciones a evaluar:

```text
surfaceOffset(point)
effectiveRadius(point)
density(point)
```

Que se implementa:

```text
Parametros de superficie.
Seed.
Voronoi esferico inicial.
Ruido fino inicial.
Kernel de evaluacion de puntos.
Salida debug visible.
```

Pruebas desde Inspector:

```text
Generar con seed fija.
Generar con seed aleatoria.
Comparar dos generaciones con la misma seed.
Cambiar radio.
Cambiar amplitud de ruido.
Cambiar frecuencia.
Mostrar muestras de densidad.
Liberar buffers.
Regenerar 100 veces.
```

Tests automatizables:

```text
Misma seed y mismos parametros producen mismos datos de debug.
Cambiar seed cambia el resultado.
density en centro es solido.
density muy lejos es aire.
WorldScale no cambia la forma logica, solo la escala visual.
```

Metricas:

```text
Tiempo de generacion.
Tamaño de buffers.
Allocations.
Coste de regenerar.
Coste de liberar y volver a crear.
```

Criterio de cierre:

```text
La forma exterior existe en GPU.
La semilla es determinista.
No se necesita un volumen completo del planeta en memoria.
La liberacion de recursos esta probada.
```

No entra:

```text
Cuevas.
Minerales.
Terraformado.
Colision.
Persistencia.
```

## Paso 3 - Primera visualizacion exterior

Objetivo:

```text
Ver el planeta como objeto exterior barato.
```

Salida aceptada para esta fase:

```text
mesh low-res
vertex buffer procedural
puntos debug
render texture
impostor simple
```

La salida exacta puede cambiar. Lo importante es ver el resultado sin calcular el volumen completo.

Pruebas desde Inspector:

```text
Generar proxy.
Cambiar resolucion.
Cambiar seed.
Cambiar gradiente.
Cambiar material de agua.
Liberar proxy.
Regenerar proxy en bucle.
Forzar resolucion absurda y comprobar limite.
```

Tests automatizables:

```text
El proxy no supera el presupuesto maximo configurado.
Release elimina referencias a buffers/meshes.
Regenerate no acumula recursos.
Los materiales requeridos estan asignados.
```

Metricas:

```text
Triangulos/vertices generados.
VRAM estimada del proxy.
RAM de datos auxiliares.
Draw calls.
Frame time.
```

Criterio de cierre:

```text
El planeta se ve desde lejos.
La silueta conserva el caracter procedural.
Se puede destruir y reconstruir sin fuga evidente.
```

## Paso 4 - Payload de triangulos

Objetivo:

```text
Probar como responde el sistema con muchos y pocos triangulos.
```

Presupuestos de prueba:

```text
Payload bajo.
Payload medio.
Payload alto.
Payload extremo.
```

Que se implementa:

```text
Configuracion de presupuesto.
Aplicacion del presupuesto al proxy.
Degradacion visual controlada.
Panel/debug de triangulos usados.
```

Pruebas desde Inspector:

```text
Aplicar payload bajo.
Aplicar payload medio.
Aplicar payload alto.
Aplicar payload extremo.
Oscilar payload en bucle.
Cambiar payload mientras el planeta esta visible.
Capturar metricas por payload.
```

Tests automatizables:

```text
El numero de triangulos no supera el presupuesto.
Payload bajo no genera cero visible salvo que se pida.
Cambiar payload no deja buffers antiguos vivos.
Payload invalido se limita o rechaza en herramientas.
```

Metricas:

```text
Triangulos.
Vertices.
Buffers creados.
VRAM estimada.
FPS/frame time.
Tiempo de regeneracion.
```

Criterio de cierre:

```text
Con pocos triangulos el planeta sigue siendo legible.
Con muchos triangulos mejora sin romper memoria ni FPS.
El sistema degrada en vez de explotar.
```

## Paso 4.5 - Setup de player Quest 3

Objetivo:

```text
Preparar el proyecto para probar PlanetImplementationLab en VR con Quest 3, Quest Link, camara estereo, movimiento basico y UI clicable con rayo.
```

Estado consolidado:

```text
Docs/Implementacion/Estado_Actual_Proyecto.md
```

Que se implementa:

```text
Configuracion XR para Quest 3.
Play en Editor con Quest Link.
Configuracion para APK/Player Android.
Camara/player VR basico.
Movimiento normal de camara/player en el Lab.
Interaccion por rayo.
UI de pruebas para lanzar botones de Labs.
```

Pruebas desde Lab:

```text
Validate Quest Player Setup.
Show XR Status.
Show Build Target Status.
Show VR UI References.
Focus VR Control Panel.
Reset Player Rig Pose.
```

Criterio de cierre:

```text
PlanetImplementationLab se puede usar en Play Mode con Quest Link.
La camara VR estereo funciona.
La camara/player se puede mover de forma basica.
Hay UI de pruebas clicable con rayo.
Los comandos principales de Lab se pueden lanzar desde esa UI.
El proyecto queda preparado para generar APK/Player de Quest 3.
```

Regla:

```text
Este paso prepara como se prueban los sistemas.
La validacion completa de memoria, compute, snapshots, release y stress se hace en el deadline ejecutando todas las pruebas definidas.
```

## Paso 5 - Estados lejanos del planeta

Objetivo:

```text
Implementar la transicion conceptual Dormant -> Astronomical -> Far -> Approach.
```

Que se implementa:

```text
Estado Dormant.
Estado Astronomical con impostor.
Estado Far con proxy low-res.
Estado Approach sin chunks locales reales todavia.
Hysteresis entre estados.
```

Pruebas desde Inspector:

```text
Forzar Dormant.
Forzar Astronomical.
Forzar Far.
Forzar Approach.
Simular distancia.
Simular tamaño angular.
Simular acercamiento rapido.
Simular alejamiento rapido.
Liberar estado actual.
```

Tests automatizables:

```text
Cada estado crea solo sus recursos esperados.
Salir de un estado libera recursos no necesarios.
Hysteresis evita cambios infinitos cerca del umbral.
Dormant no mantiene buffers visuales vivos.
```

Criterio de cierre:

```text
El planeta puede cambiar de representacion sin saltos de arquitectura.
Cada estado tiene memoria y recursos bajo control.
```

## Paso 6 - Chunks locales minimos

Objetivo:

```text
Generar terreno real solo cerca del jugador.
```

Que se implementa:

```text
Definicion minima de chunk.
Generacion local pequeña.
Marching Cubes local basico.
Mesh local visual.
Liberacion de chunks.
```

No entra todavia:

```text
Cuevas.
Minerales complejos.
Terraformado.
Colision final.
Streaming completo.
```

Pruebas desde Inspector:

```text
Generar chunk en coordenada.
Generar bloque 3x3x3.
Generar bloque 10x10x10 si el presupuesto lo permite.
Liberar todos los chunks.
Regenerar alrededor de posicion simulada.
Mover posicion simulada.
Stress de creacion/destruccion.
```

Tests automatizables:

```text
Chunk generado respeta coordenadas.
Chunk fuera de presupuesto no se crea.
Release limpia mesh/buffers.
Regenerar mismo chunk con misma seed produce resultado equivalente.
```

Metricas:

```text
Tiempo por chunk.
Triangulos por chunk.
VRAM por chunk.
RAM por chunk.
Allocations por generacion.
```

Criterio de cierre:

```text
Existe terreno local real.
Se puede crear y destruir sin fuga evidente.
La generacion local no exige materializar el planeta completo.
```

## Paso 7 - Streaming, prioridades y cancelacion

Objetivo:

```text
Preparar terreno por delante del jugador sin picos duros.
```

Que se implementa:

```text
Zona de interes.
Lookahead por velocidad.
Prioridad por camara.
Prioridad por distancia/contacto.
Cola de trabajo con presupuesto por frame.
Cancelacion de trabajo obsoleto.
```

Pruebas desde Inspector:

```text
Simular jugador quieto.
Simular vuelo lento.
Simular vuelo rapido.
Simular giro brusco.
Simular cambio de trayectoria.
Forzar presupuesto bajo.
Forzar presupuesto alto.
Mostrar cola de trabajo.
Cancelar todo.
```

Tests automatizables:

```text
La cola no supera capacidad.
Los trabajos cancelados no escriben resultados.
La prioridad favorece contacto y direccion de movimiento.
Presupuesto por frame se respeta.
```

Criterio de cierre:

```text
El sistema prepara lo importante y suelta lo que deja de importar.
No hay picos grandes por intentar hacerlo todo de golpe.
```

## Paso 8 - Auditoria de RAM, VRAM y GC

Objetivo:

```text
Parar antes de añadir features y comprobar que la base respira.
```

Que se revisa:

```text
Buffers CPU.
Buffers GPU.
Meshes.
Material instances.
Texturas/render textures.
Pools.
Listas grandes.
Allocations por frame.
AsyncGPUReadback.
```

Pruebas desde Inspector:

```text
Crear todo.
Liberar todo.
Crear/liberar 100 veces.
Cambiar de estado 100 veces.
Generar chunks y soltarlos.
Capturar snapshot antes/despues.
```

Tests automatizables:

```text
Despues de Release no quedan recursos registrados como vivos.
Los contadores internos vuelven a cero.
Los pools no crecen por encima del limite.
No hay allocations en updates calientes conocidos.
```

Criterio de cierre:

```text
Tenemos una politica de memoria real.
Sabemos que recursos existen y por que.
Sabemos liberar sin depender de cerrar la escena.
```

Este paso es obligatorio antes de cuevas, minerales, colisiones o terraformado.

## Paso 9 - Cuevas

Objetivo:

```text
Añadir cuevas sin convertir el planeta entero en un volumen precalculado.
```

Que se implementa:

```text
Red procedural inicial.
Voronoi 3D o variante para estructura organica.
Evaluacion por chunk/zona.
Entradas a superficie.
Debug visual de red.
```

Pruebas desde Inspector:

```text
Generar red de cuevas.
Mostrar nodos.
Mostrar tuneles.
Forzar muchas cuevas.
Forzar pocas cuevas.
Generar chunk con cueva.
Generar entrada a superficie.
```

Tests automatizables:

```text
Misma seed produce misma red.
La evaluacion de cueva no exige volumen global.
Los radios quedan dentro de rango.
La red no genera datos infinitos.
```

Criterio de cierre:

```text
Las cuevas aparecen en chunks locales.
Tienen formas interesantes.
No obligan a almacenar todo el interior del planeta.
```

## Paso 10 - Minerales y sustancias

Objetivo:

```text
Separar contenido logico de material visual.
```

Que se implementa:

```text
SustanciaId.
Funcion procedural de sustancia por posicion.
Masas/vetas iniciales.
Color debug por sustancia.
```

Pruebas desde Inspector:

```text
Mostrar sustancia en chunk.
Forzar mineral concreto.
Cambiar seed.
Cambiar frecuencia/tamaño de veta.
Stress con muchas sustancias.
```

Tests automatizables:

```text
Misma seed produce mismas sustancias.
Sustancia aire no se confunde con agua.
Agua conserva id propio.
IDs fuera de rango se detectan en herramientas.
```

Criterio de cierre:

```text
El terreno tiene sustancias logicas consultables.
El render puede seguir usando material visual simple.
```

## Paso 11 - Colisiones locales

Objetivo:

```text
Dar fisica solo donde el jugador o vehiculo la necesita.
```

Que se implementa:

```text
Zona de contacto 2-3 metros.
Cache local de colision.
Pool de colliders/meshes de colision.
Lookahead para vehiculos.
Queries async separadas para rayos/lejanos.
```

Pruebas desde Inspector:

```text
Forzar zona de contacto.
Mover jugador simulado.
Mover vehiculo simulado.
Activar/desactivar colliders.
Stress de pool.
Mostrar colliders vivos.
Liberar colisiones.
```

Tests automatizables:

```text
La zona de contacto no supera presupuesto.
Los colliders vuelven al pool.
No se crean colliders para terreno lejano.
Las queries async no bloquean movimiento.
```

Criterio de cierre:

```text
El contacto inmediato no depende de readback GPU bloqueante.
La colision existe solo donde aporta gameplay.
```

## Paso 12 - Terraformado

Objetivo:

```text
Editar terreno local sin convertir todo el planeta a resolucion fina.
```

Que se implementa:

```text
Macro cell 4x4x4.
Micro cell 1x1x1.
Patch local.
Descomposicion macro -> micro.
Compactacion micro -> macro si se puede.
Persistencia inicial de patch.
```

Pruebas desde Inspector:

```text
Vaciar macro cell.
Crear micro cells.
Editar micro cell.
Compactar patch.
Forzar patch no compactable.
Guardar patch.
Cargar patch.
Stress de muchas ediciones locales.
```

Tests automatizables:

```text
Macro 4x4x4 contiene 64 micro cells.
Patch igual a procedural se elimina.
Patch homogeneo compacta.
Patch complejo se conserva.
Guardar/cargar mantiene datos.
```

Criterio de cierre:

```text
El jugador puede modificar una zona.
La modificacion persiste.
El planeta base sigue siendo procedural.
```

## Bloque 01-12 cerrado

El bloque de documentos 01-12 queda cerrado y consolidado en:

```text
Docs/Implementacion/Estado_Actual_Proyecto.md
```

Los deadlines y merges antiguos ya no son documentacion viva. El siguiente sistema
importante debe abrir su propio documento funcional partiendo del estado actual.

## Regla final

Cada vez que una fase quiera meter una feature nueva, hay que preguntar:

```text
Ayuda a validar el siguiente riesgo tecnico?
Tiene prueba?
Tiene medicion?
Tiene liberacion?
Funciona con presupuesto?
Puede esperar?
```

Si puede esperar, espera.
