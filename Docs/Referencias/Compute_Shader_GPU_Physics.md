# Compute Shader GPU Physics

## Estado

Referencia guardada para exploracion futura.

No es una decision de arquitectura cerrada y no se integra todavia en runtime.
Sirve como recordatorio tecnico por si mas adelante se decide mover alguna parte
de simulacion fisica, interaccion o queries a compute shaders.

Fuente inmediata:

```text
Adjunto de conversacion: "Physics simulation on GPU with compute shader in Unity3D, tutorial"
```

## Idea Principal

La referencia explica una simulacion fisica sencilla ejecutada en GPU mediante
compute shaders en Unity. El ejemplo usa pelo como conjunto de puntos conectados
por fuerzas tipo muelle, pero la parte util para el proyecto no es el pelo: es
el patron general de trabajo GPU.

Patron:

```text
CPU prepara buffers y parametros.
GPU ejecuta kernels sobre muchos elementos en paralelo.
CPU evita leer resultados salvo que sea estrictamente necesario.
GPU puede escribir visualizacion o estados intermedios en buffers/texturas.
```

## Puntos Utiles Para El Proyecto

### Dispatch y numthreads

La cantidad real de threads sale de:

```text
Dispatch(x, y, z) * [numthreads(tx, ty, tz)]
```

Ejemplo:

```text
Dispatch(16, 16, 1)
[numthreads(16, 16, 1)]
=> 256 x 256 threads
```

Esto encaja con nuestras rutas de Marching Cubes, conteos, rasterizaciones o
queries donde cada thread procesa una celda, vertice, pixel, chunk o sample.

### Kernels En Fases

El tutorial estructura la simulacion como varios kernels secuenciales:

```text
calcular interacciones
compartir velocidades
resolver colisiones
aplicar movimiento
visualizar
limpiar buffers
```

Lectura para nosotros:

```text
No hace falta meter toda la logica en un kernel enorme.
Tiene sentido dividir en fases pequenas si cada fase escribe/lee buffers claros.
```

### Buffers Estructurados

Usa `RWStructuredBuffer<T>` para almacenar estado editable en GPU.

En CPU se crea un `ComputeBuffer` con stride explicito:

```text
new ComputeBuffer(elementCount, strideBytes)
SetData(initialData)
SetBuffer(kernel, name, buffer)
Dispatch(...)
```

Punto importante:

```text
El layout CPU/GPU debe coincidir exactamente.
El stride debe ser explicito y revisado.
```

Para Quest 3 conviene mantener structs alineados y pequenos. El tutorial habla
de alinear a 128 bits como recomendacion de rendimiento, pero no queda como regla
cerrada para el proyecto.

### Escrituras Concurrentes

Cuando muchos threads escriben sobre el mismo elemento se necesitan operaciones
atomicas/protegidas.

Ejemplo conceptual:

```text
InterlockedAdd(...)
```

Limitacion relevante:

```text
Las operaciones atomicas comunes trabajan sobre int/uint.
Si se necesita acumular floats, se puede cuantizar float -> int con un factor.
```

Esto puede ser util para:

```text
acumular fuerzas
acumular impactos
contadores
histogramas
conteos de visibilidad
conteos de triangulos o vertices
```

Pero tambien introduce coste y riesgo de precision/rango. No se debe usar como
atajo automatico.

### Evitar GetData

La referencia recalca que `ComputeBuffer.GetData()` bloquea/stallea la pipeline.

Regla compatible con nuestra arquitectura:

```text
GPU -> CPU readback no pertenece al camino caliente visual.
Si se necesita leer datos, debe ser diagnostico, streaming controlado o query
asincrona con presupuesto.
```

Esto refuerza la direccion actual:

```text
Marching Cubes visual escribe y dibuja en GPU.
No vuelve a CPU para publicar Mesh.
```

### SetData Frente A GetData

La referencia distingue:

```text
SetData suele ser aceptable para subir datos pequenos/controlados.
GetData puede provocar stalls fuertes.
```

Para el proyecto:

```text
Subir receta, parametros, origins o listas pequenas puede ser aceptable.
Bajar vertices/state/counts en runtime caliente no.
```

### Update Frente A FixedUpdate

La referencia recomienda lanzar compute desde `Update`, no `FixedUpdate`, porque
la pipeline grafica se sincroniza con el flujo de render.

Para nosotros queda como nota, no regla absoluta:

```text
Render/compute visual -> Update o fase render-controlada.
Fisica de contacto -> necesitara contrato propio si alguna vez depende de GPU.
```

## Riesgos Para Quest 3

La referencia menciona diferencias entre APIs:

```text
Metal puede limitar threads por eje.
Android puede limitar numero de buffers por kernel.
DX permite mas buffers que algunas APIs moviles.
```

Para Quest 3:

```text
No asumir limites de DX como si fueran universales.
Mantener kernels con pocos buffers vinculados.
Medir en Android/Quest, no solo Editor/DX.
```

## Posibles Usos Futuros

Ideas donde esta referencia podria servir:

```text
contact queries cercanas en GPU
rayos/escaneos diferidos
occlusion voxel jerarquica
acumulacion de impactos de herramientas
simulacion simple de particulas/sustancias
precalculo de campos auxiliares por chunk
```

No usar todavia para:

```text
fisica inmediata del player
colision necesaria para locomotion
respuestas que exijan GPU -> CPU bloqueante cada frame
```

## Lectura Para Nuestro Motor

Esta referencia encaja con una idea ya documentada:

```text
Visual Terrain       -> GPU y async siempre que se pueda.
Interaction Queries  -> GPU posible con retardo aceptable.
Contact Physics      -> respuesta inmediata, no depender de readback bloqueante.
```

Si nos envalentonamos con fisica GPU, el primer experimento deberia ser una
query no critica, medible y sin bloqueo:

```text
CPU sube N consultas.
GPU procesa.
CPU lee resultado mas tarde o lo usa directamente en GPU.
No afecta locomotion ni contacto inmediato.
```

## Pendiente

```text
TBD: buscar la fuente original publica del tutorial si se quiere citar enlace.
TBD: decidir si alguna prueba GPU physics entra como prototipo aislado.
TBD: definir limites Quest 3 para numero de buffers por kernel y numthreads.
```
