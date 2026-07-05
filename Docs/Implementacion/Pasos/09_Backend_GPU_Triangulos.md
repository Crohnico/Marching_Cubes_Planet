# 09 - Backend GPU de triangulos

## Estado

Documento de alcance para cambiar la salida visible de 09.

09 deja de tener como objetivo final una `Mesh` CPU runtime para el artista
Environment y pasa a tener un backend visible residente en GPU.

Este documento sustituye la decision anterior de "Mesh runtime CPU gestionada
por el artista" dentro de 09.

## Decision

La salida visible de 09 sera:

```text
Triangle/vertex data -> GraphicsBuffer persistente -> shader procedural -> render
```

09 seguira siendo un sistema de presupuesto y residencia de triangulos. No pasa
a decidir LOD, visibilidad, frustum ni generacion.

La diferencia es que el resultado visible ya no se materializa como una `Mesh`
de Unity que se reescribe en runtime. El artista mantiene buffers GPU propios y
dibuja desde ellos.

## Correccion tecnica

Un compute shader no pinta con un Material por si mismo.

La separacion correcta es:

```text
Compute shader:
    prepara, compacta, copia o actualiza GraphicsBuffer.

Material/shader procedural:
    lee esos GraphicsBuffer durante el render y calcula la salida visual.
```

Por tanto, cuando decimos "09 pinta en GPU" significa:

```text
09 mantiene datos visibles en GraphicsBuffer.
09 puede usar compute para escribir/compactar esos datos.
09 llama a una ruta de render procedural/indexada.
El material visible usa una variante shader que lee buffers.
```

Referencias Unity:

```text
GraphicsBuffer:
https://docs.unity3d.com/ScriptReference/GraphicsBuffer.html

ComputeShader.SetBuffer:
https://docs.unity3d.com/ScriptReference/ComputeShader.SetBuffer.html

Material.SetBuffer:
https://docs.unity3d.com/ScriptReference/Material.SetBuffer.html

Graphics.RenderPrimitives:
https://docs.unity3d.com/ScriptReference/Graphics.RenderPrimitives.html

Graphics.RenderPrimitivesIndexed:
https://docs.unity3d.com/ScriptReference/Graphics.RenderPrimitivesIndexed.html
```

## Objetivo

Eliminar el coste de reescribir/subir `Mesh` gigantes en runtime para cambios de
LOD de chunks.

Objetivos:

```text
1. Mantener el contrato Draw(meshId, datos, priority) de 09.
2. Mantener el presupuesto de triangulos por artista.
3. Mantener ownership, release, metricas y diagnostico.
4. Cambiar solo la representacion visible interna del artista.
5. Conservar el look/material actual del planeta.
6. Evitar readback GPU -> CPU en runtime caliente.
7. Evitar crear/destruir Mesh/GameObjects por cambio de LOD.
```

## Fuera de alcance

No entra:

```text
Cambiar el algoritmo de Marching Cubes.
Cambiar desiredLOD de 10.
Arreglar Transvoxel.
Meter culling/frustum.
Meter oclusion.
Cambiar materiales artisticamente.
Hacer colision desde este backend.
Eliminar 08 como validacion historica de Mesh.
```

## Contrato publico de 09

El contrato externo no cambia:

```text
EnvironmentArtist.Draw(meshId, trianglePayload, priority)
ParticlesArtist.Draw(meshId, trianglePayload, priority)
Release(meshId / owner / artist)
CaptureMetrics()
```

El caller no sabe si el artista:

```text
Concedio todos los triangulos.
Concedio una parte.
Reclamo slots peores.
Denego la request.
```

La diferencia vive dentro del artista:

```text
Antes: actualizar Mesh CPU.
Ahora: actualizar buffers GPU y draw procedural.
```

## Datos visibles

La unidad visible minima sigue siendo triangulo.

Formato inicial recomendado:

```text
PlanetGpuTriangleVertex:
    float3 positionWorldOrLocal
    float3 normal
    float2 uv
    uint materialId
    uint flags
```

Ruta no indexada inicial:

```text
3 vertices por triangulo.
vertexCount = triangleCount * 3.
Graphics.RenderPrimitives(... MeshTopology.Triangles, vertexCount)
shader usa SV_VertexID para leer PlanetGpuTriangleVertex[vertexID].
```

Motivo:

```text
07 ya produce vertices no indexados en la ruta actual.
Reduce el primer cambio conceptual.
Evita tener que resolver deduplicacion de vertices ahora.
```

Ruta indexada futura:

```text
vertexBuffer + indexBuffer.
Graphics.RenderPrimitivesIndexed.
```

Regla:

```text
La ruta indexada no entra hasta que la no indexada funcione y se mida.
```

## Materiales

Queremos mantener el resultado visual integro.

Eso no significa reutilizar el shader de Mesh sin cambios. Significa crear una
variante equivalente que tenga la misma logica visual, pero lea el input desde
buffers.

Regla:

```text
El material artistico actual sigue siendo la referencia visual.
La variante GPU debe producir el mismo color/look para los mismos datos.
```

Direccion:

```text
Extraer la logica compartida de color/atlas a include HLSL si compensa.
Crear shader SurfaceGpu o variante equivalente.
Mantener las mismas propiedades artisticas: atlas, parametros de altura, colores,
agua/superficie segun corresponda.
Material.SetBuffer enlaza los GraphicsBuffer necesarios.
```

No se debe:

```text
Cambiar el look para que sea mas facil el backend.
Meter un material debug como sustituto permanente.
Hacer readback a CPU para alimentar una Mesh solo para conservar el shader viejo.
```

## Backend de artista

Cada artista con backend GPU mantiene:

```text
GraphicsBuffer vertexBuffer.
GraphicsBuffer slotMetadataBuffer.
GraphicsBuffer drawArgsBuffer si se usa indirect.
ComputeBuffer/GraphicsBuffer temporales solo si son necesarios.
Material o materiales buffer-aware.
Bounds por batch/material para render.
Metricas de memoria estimada.
```

Los buffers se crean teniendo en cuenta el presupuesto del pool de 09:

```text
totalTriangleBudget -> capacidad maxima de triangulos del artista.
maxVertices         -> totalTriangleBudget * 3 en la ruta no indexada.
```

Regla:

```text
Init del artista crea buffers con capacidad maxima.
Draw no crea buffers nuevos.
Release libera buffers completos.
```

Environment inicial:

```text
Un batch de superficie planetaria.
Un material buffer-aware equivalente al material de superficie actual.
Ruta no indexada.
Draw procedural directo.
```

Agua:

```text
Puede ser otro batch/material dentro de Environment.
No se mezcla con superficie si necesita shader/material distinto.
```

## Uso de compute

Compute entra para trabajo de datos:

```text
Compactar slots vivos.
Actualizar rangos de vertexBuffer.
Construir contadores por bucket.
Copiar requests concedidas a la zona visible.
Generar argumentos indirectos cuando toque.
```

No entra para:

```text
Hacer shading final del material.
Decidir LOD.
Hacer culling que pertenece a 11.
```

## Render

Primera ruta:

```text
Graphics.RenderPrimitives
MeshTopology.Triangles
vertexCount = liveTriangleCount * 3
RenderParams con material buffer-aware
worldBounds del artista o batch
```

Ruta posterior si hace falta:

```text
Graphics.RenderPrimitivesIndexed
Graphics.RenderPrimitivesIndirect / RenderPrimitivesIndexedIndirect
```

Decision:

```text
No empezar por indirect si no hace falta.
Primero cerrar que el render buffer-aware se ve igual y elimina el coste de Mesh.
```

## Presupuesto y residencia

El presupuesto de 09 sigue en triangulos.

El backend GPU reserva por artista:

```text
maxTriangles = totalTriangleBudget
maxVertices = totalTriangleBudget * 3 para ruta no indexada
```

Modelo de llenado:

```text
Cada slot de triangulo del pool corresponde a 3 posiciones de vertexBuffer.
slotId -> vertexStart = slotId * 3.
Si una request obtiene N slots, escribe N * 3 vertices en esos rangos.
Si una request se libera o es reclamada, esos slots vuelven al freelist.
```

Vaciar un chunk/publicacion:

```text
No destruye buffers.
No compacta todo el vertexBuffer por defecto.
Marca los slots como libres/inactivos.
Actualiza metadata para que esos triangulos no se dibujen.
```

Primera ruta aceptada:

```text
Mantener slotMetadataBuffer con estado activo/inactivo.
El shader descarta triangulos inactivos o el compute compacta una lista visible.
```

Preferencia inicial:

```text
Si descartar inactivos en shader cuesta demasiado, pasar a compactacion GPU.
Si compactar todo por frame cuesta demasiado, compactar solo cuando cambian slots.
```

Estimacion inicial por vertice:

```text
position float3 -> 12 bytes
normal float3   -> 12 bytes
uv float2       -> 8 bytes
materialId uint -> 4 bytes
flags uint      -> 4 bytes
total           -> 40 bytes por vertice
```

Estimacion no indexada:

```text
bytes = totalTriangleBudget * 3 * 40
```

Para 1M triangulos:

```text
120 MB aprox solo vertexBuffer.
```

Regla:

```text
Esto hay que medir contra Quest 3 antes de subir presupuestos.
La memoria baja si pasamos a formato comprimido, half precision, indexado o
separacion por atributos.
```

Regla de churn:

```text
El coste normal de un cambio de LOD debe ser escribir rangos ya reservados.
No debe ser crear buffer, destruir buffer ni reconstruir una Mesh.
```

## Cache y payload CPU

El cambio de backend visible no obliga a cambiar inmediatamente el formato de
cache `.pmesh`.

Primera fase aceptada:

```text
Cache .pmesh -> lectura CPU acotada -> upload a GraphicsBuffer del artista.
```

Lectura:

```text
Esto no elimina todo coste CPU/disk.
Si elimina el coste de construir/modificar Mesh de Unity.
```

Fase posterior:

```text
Cache binaria alineada al formato GPU.
Streaming directo a buffers persistentes.
Menos conversiones CPU.
```

## Integracion con 10

10 sigue decidiendo:

```text
desiredLOD.
requestedLOD.
prioridad de request.
cancelacion de request.
```

10 no debe saber si 09 pinta por Mesh o por GPU.

Regla:

```text
10 entrega payload de chunk/LOD al artista Environment.
09 actualiza su residencia y su backend visible.
```

## Integracion con 08

08 mantiene su rol como validacion historica de "puedo convertir salida de 07 en
Mesh".

Pero para runtime gestionado por 09:

```text
08 no es la salida final.
08 no obliga a que 09 use Mesh.
09 puede tener un writer propio desde los datos de 07/cache hacia GraphicsBuffer.
```

## Liberacion

Release de artista GPU debe:

```text
Liberar GraphicsBuffer/ComputeBuffer propios.
Soltar materiales runtime si fueron instanciados.
Limpiar contadores y ownership.
Marcar handles como liberados en registry.
Permitir Release doble.
Permitir Init -> Release -> Init.
```

## Instrumentacion minima

Medir:

```text
Tiempo de upload a GraphicsBuffer.
Bytes escritos por frame.
Triangulos vivos por artista.
Triangulos publicados por frame.
Draw calls por artista/material.
Vertex Buffer Upload In Frame Bytes si Unity lo expone.
Index Buffer Upload In Frame Bytes si se usa ruta indexada.
Memoria GPU estimada por artista.
```

## Orden de implementacion

1. Crear backend GPU minimo de Environment con `GraphicsBuffer` no indexado.
2. Crear shader/material buffer-aware equivalente al material de superficie.
3. Conectar un payload pequeño conocido y validar que se ve igual.
4. Conectar una publicacion de chunk desde 10/09 sin tocar LOD.
5. Sustituir el camino runtime de cambio LOD para que escriba en buffers GPU.
6. Mantener fallback Mesh solo como diagnostico/Lab, no como backend de 09.
7. Medir tirones frente a la ruta Mesh.
8. Optimizar formato, indexado o indirect solo si lo pide el profiler.

## Validacion minima

Desde Canvas Generate:

```text
1. La shell LOD2 se ve usando backend GPU de 09.
2. El material visual coincide con la ruta Mesh anterior.
3. Al cambiar LOD, no se crea ni modifica Mesh runtime visible.
4. Los cambios de chunk actualizan GraphicsBuffer del artista.
5. Release All libera buffers y materiales propios.
6. Las metricas muestran triangulos vivos y memoria GPU estimada.
```

## Decisiones cerradas

```text
09 cambia su salida visible inicial a GPU-resident.
El backend visible inicial usa GraphicsBuffer.
La primera ruta sera no indexada para reducir cambios.
El render usa shader/material procedural buffer-aware.
Compute se usa para preparar/copiar/compactar datos, no para shading final.
El look actual debe conservarse mediante una variante equivalente del shader.
08 puede conservar Mesh como validacion, pero no define el backend final de 09.
10 no debe depender del backend visible usado por 09.
```

## Pendientes

```text
Confirmar soporte/rendimiento exacto en Quest 3 con la version Unity del proyecto.
Decidir si la primera ruta usa RenderPrimitives o alternativa compatible si la
version exacta de Unity no lo soporta.
Definir layout final del vertex buffer.
Definir si agua comparte buffer o batch separado.
Definir cuando conviene pasar a indexado.
Definir politica de bounds por chunk/material para culling de Unity.
Definir formato de cache alineado a GPU si la conversion CPU sigue costando.
```
