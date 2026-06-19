# Arquitectura DOTS para motor voxel con Marching Cubes

Este documento sustituye la arquitectura anterior. La direccion del proyecto pasa a ser: datos lineales, Jobs, Burst, generacion bajo demanda y resolucion de malla a partir de un campo escalar.

## Objetivo

Construir primero un motor voxel independiente del mundo final.

El motor voxel no decide que es un planeta, una cueva, una roca o una estructura. Solo sabe preguntar a un proveedor de valores escalares:

```csharp
float value = Sample(x, y, z);
```

Con esos valores genera celdas, resuelve Marching Cubes y escribe una mesh. La parte del mundo/planeta vendra despues como una implementacion concreta de ese contrato.

## Alcance inmediato

Primero hacemos el motor voxel:

1. `VoxelCell` orientado a datos.
2. Chunks como regiones de celdas.
3. Archivo de configuracion del motor voxel.
4. Seguimiento de posicion del player/anchor para activar chunks.
5. Notificacion cuando el player/anchor cambia de chunk.
6. Tabla completa para `VoxelCell.size = 1`.
7. Resolvers runtime para `size > 1`.
8. Pipeline de Jobs compatible con Burst.
9. Escritura de buffers de mesh.

Todavia no hacemos:

- Ecuacion final del planeta.
- Ruido, continentes o biomas.
- Streaming planetario.
- Reglas de gameplay.

## Principios

- El `0,0,0` del universo es el `0,0,0` del mundo.
- Las coordenadas de generacion son coordenadas de mundo/universo, no coordenadas relativas arbitrarias.
- La unidad minima del juego es una celda de `1u x 1u x 1u`.
- Un `VoxelCell` puede tener `size` variable y agrupar varias celdas minimas.
- El motor voxel no sabe si genera planetas, arboles o cualquier otro contenido; eso pertenece al campo escalar.
- El motor voxel si sabe empaquetar chunks en funcion de una posicion activa.
- La posicion activa inicial es la posicion del player en mundo.
- El player/anchor debe notificar al motor voxel cuando cambia de chunk.
- No usar objetos de dominio por voxel, celda o cubo.
- No usar herencia para datos de generacion.
- No usar `Dictionary<TKey, TValue>` dentro de Jobs.
- Mantener los datos en bloques lineales compatibles con Burst.
- Separar claramente muestreo del campo escalar, resolucion de celdas, construccion de malla y gestion de chunks.
- Recalcular antes que almacenar cuando el dato sea barato de derivar.
- Guardar en RAM solo los chunks activos y sus mallas visibles.

## Celda orientada a datos

La unidad minima de trabajo es una celda compacta. Debe poder vivir dentro de un `NativeArray<VoxelCell>` sin referencias administradas.

La unidad base del mundo es `1u x 1u x 1u`, pero `VoxelCell` puede representar una supercelda. Su `size` es la longitud de arista en unidades base.

Ejemplos:

- `size = 1`: cubo de `1u x 1u x 1u`, agrupa `1` celda minima.
- `size = 2`: cubo de `2u x 2u x 2u`, agrupa `8` celdas minimas.
- `size = 4`: cubo de `4u x 4u x 4u`, agrupa `64` celdas minimas.

En general, una supercelda de `size N` agrupa `N * N * N` celdas minimas.

```csharp
using System.Runtime.InteropServices;
using Unity.Mathematics;

[StructLayout(LayoutKind.Sequential)]
public struct VoxelCell
{
    // Coordenada entera de mundo/universo.
    // Representa la esquina minima de la celda.
    public int3 origin;

    // Longitud de arista en unidades base.
    // size = 1, 2, 4, 8, 16...
    public int size;

    // Las 8 esquinas comprimidas en un byte.
    // Bit 0 = esquina 0, bit 1 = esquina 1, etc.
    public byte corners;

    // Mascara de lados si la celda toca borde de chunk.
    // X-, X+, Y-, Y+, Z-, Z+ usan un bit cada uno.
    public byte boundarySides;

    // Material o tipo de terreno: piedra, tierra, hierro, etc.
    public int type;
}
```

### Notas

- `origin` esta en el mismo sistema que el mundo: el centro del universo es `0,0,0`.
- `origin` siempre representa la esquina minima de la celda, no el centro.
- `size` define la longitud de arista del cubo en unidades base.
- La esquina opuesta de la celda es `origin + new int3(size, size, size)`.
- `corners` contiene el resultado binario del campo escalar para las 8 esquinas.
- `boundarySides` permite saber si la celda participa en una costura de chunk y por que lado.
- `type` identifica el material: roca, tierra, hielo, mineral, agua, etc.
- `type` usa `int` para cubrir al menos el rango `0..256`.

### Tipo de material

El `type` de una celda controla como se pinta la mesh en el atlas/material.

Regla:

- Una celda minima `size = 1` toma su `type` directamente del proveedor de material.
- Una supercelda `size > 1` calcula su `type` combinando los `type` de sus celdas minimas internas.
- El `type` resultante de una supercelda es el tipo mayoritario.
- Si hay empate entre varios tipos, por ahora se elige aleatoriamente entre los empatados.

La eleccion aleatoria debe ser determinista por coordenada y seed. No puede depender del frame ni del orden de ejecucion de Jobs, porque eso haria que el material parpadee o cambie al regenerar el mismo chunk.

Ejemplo:

```text
size = 2 -> 8 celdas minimas internas

types internos:
1, 1, 1, 2, 2, 3, 3, 3

Empate entre 1 y 3.
Resultado: elegir 1 o 3 usando random determinista basado en origin + seed.
```

### Esquinas de una celda

Las coordenadas enteras siempre apuntan a esquinas de la rejilla. Si una celda empieza en `origin`, sus 8 esquinas se calculan sumando `0` o `size` en cada eje:

```csharp
int3 c000 = origin;
int3 c100 = origin + new int3(size, 0, 0);
int3 c010 = origin + new int3(0, size, 0);
int3 c110 = origin + new int3(size, size, 0);
int3 c001 = origin + new int3(0, 0, size);
int3 c101 = origin + new int3(size, 0, size);
int3 c011 = origin + new int3(0, size, size);
int3 c111 = origin + new int3(size, size, size);
```

Por eso una celda con `origin = 0,0,0` y `size = 2` ocupa:

```text
X = 0..2
Y = 0..2
Z = 0..2
```

Y agrupa las celdas minimas:

```text
X = 0, X = +1
Y = 0, Y = +1
Z = 0, Z = +1
```

Total: `2 * 2 * 2 = 8` celdas de `1u x 1u x 1u`.

## Sistema de coordenadas

El origen absoluto del proyecto queda fijado:

```text
Universo 0,0,0 == Mundo 0,0,0
```

El motor voxel recibe posiciones en este sistema. No hay que convertir a un origen local mientras estemos construyendo el motor base.

Cuando mas adelante aparezca el planeta, podra usar tambien el centro `0,0,0`. Eso haria que la esfericidad sea directa:

```csharp
float distanceToCenter = math.length(worldPosition);
```

Si en el futuro un planeta se desplaza, su proveedor de campo escalar podra usar:

```csharp
float3 planetLocalPosition = worldPosition - planetCenter;
float distanceToCenter = math.length(planetLocalPosition);
```

## Tabla de Marching Cubes

Marching Cubes tiene 256 combinaciones posibles para las 8 esquinas.

En Unity clasico podria usarse:

```csharp
Dictionary<int, CaseData>
```

En Jobs no debe usarse ese enfoque. La tabla debe almacenarse como datos nativos:

- `NativeArray<int>` indexado por `caseIndex`.
- O `NativeHashMap<int, int>` si necesitamos una estructura dispersa.

La opcion preferida para los 256 casos es una tabla nativa indexada porque el indice ya es directo:

```csharp
NativeArray<int> marchingCaseOffsets;   // 256 entradas
NativeArray<int> marchingCaseLengths;   // 256 entradas
NativeArray<int> marchingTriangles;     // datos compactados
```

### Ciclo de vida

1. Crear la tabla una sola vez al arrancar.
2. Guardarla con `Allocator.Persistent`.
3. Compartirla como `[ReadOnly]` entre Jobs.
4. Liberarla en `OnDestroy` o al cerrar el sistema.

## Resolvers de Marching Cubes por size

Cada `VoxelCell` se resuelve con un resolver especializado segun su `size`.

Lista inicial:

- `MarchingCubes`: resolver basico para `size = 1`.
- `MarchingCubes2`: resolver para `size = 2`.
- `MarchingCubes4`: resolver para `size = 4`.
- `MarchingCubes8`: resolver para `size = 8`.
- `MarchingCubes16`: resolver para `size = 16`.
- `MarchingCubes32`: resolver para `size = 32`.

El resolver basico trabaja con 8 esquinas, una por cada vertice del cubo.

Convencion del motor:

- `corners = 0`: celda vacia, no genera mesh.
- `corners = 255`: celda solida completa, se resuelve como cubo solido.
- Cualquier caso intermedio: se resuelve con Marching Cubes.

Esto se separa de la convencion clasica de Marching Cubes donde el caso `255` no genera triangulos porque se asume que estas extrayendo solo una isosuperficie interior. En este motor, una celda con las 8 esquinas en `1` representa volumen material. Por ahora se pinta como cubo completo con sus 6 caras. El culling interno entre celdas solidas queda como optimizacion posterior, no como regla base.

La tabla estandar de Marching Cubes se puede reutilizar, pero el orden de los triangulos de los casos parciales se invierte al escribir la mesh. La razon es que nuestra convencion es `corner bit = 1` significa solido/material, y queremos que el winding mire de solido hacia vacio. Las celdas completas no pasan por esa inversion: usan su propio resolver de cubo solido.

### Regla de pintado de malla

Una celda solo escribe geometria si tiene superficie visible conocida:

- `corners = 0`: aire puro, no pinta.
- `corners` parcial: pinta con Marching Cubes, porque al menos una esquina es aire y al menos una es solida.
- `corners = 255`: solo pinta caras hacia vecinos directos, no diagonales, que sean aire puro `corners = 0` o chunks no declarados.

Los vecinos directos se leen solo si pertenecen a chunks declarados vivos. Si el chunk vecino no ha sido declarado, no se crea ni se lee nada: para la regla de pintado cuenta como aire.

Para que esta regla no sea cuadratica, el job de malla no busca vecinos recorriendo todas las celdas del chunk. Despues de evaluar las celdas se construye un indice denso por unidad minima del chunk:

```text
index = x + chunkSize.x * (y + chunkSize.y * z)
```

Cada entrada guarda el `corners` de la `VoxelCell` que cubre esa unidad minima. Una supercelda escribe el mismo valor en todas sus unidades internas. Al pintar una cara solida, el job mira las unidades directas de la cara vecina por indice. Asi una consulta de vecino es aritmetica de coordenadas y acceso directo a array, no un bucle sobre todas las celdas.

Los resolvers superiores no inventan otro espacio. Sus puntos de evaluacion siguen cayendo en coordenadas enteras del mundo. Una arista de una supercelda contiene los puntos enteros desde `origin` hasta `origin + size`, incluyendo ambos extremos.

Punto clave para LOD y costuras: una supercelda interior puede compactarse usando solo sus 8 esquinas, pero una supercelda que toca un borde de chunk contra un vecino de mayor resolucion no puede tirar la informacion lateral. En ese caso el chunk grueso conserva una franja refinada en el lado afectado, usando el tamano de celda compartido con el vecino fino. Asi el borde no se convierte en:

```text
11
11
```

cuando la rejilla real del lateral era:

```text
101
1 1
111
```

La decision se hace por lado de chunk. Cada `VoxelCell` guarda una mascara `boundarySides` para saber si toca `X-`, `X+`, `Y-`, `Y+`, `Z-` o `Z+`. El manager compara el `cellSize` del chunk con el `cellSize` de sus seis vecinos virtuales:

- Si el vecino tiene el mismo detalle, no se fuerza franja fina.
- Si el vecino es mas grueso, ese vecino sera quien conserve su borde.
- Si el vecino es mas fino, este chunk conserva ese lado con celdas del tamano comun necesario para coincidir con la rejilla del vecino.

Ejemplo:

- Un chunk `size = 8` contra un vecino `size = 4` conserva puntos de borde en `0, 4, 8`: las esquinas y el punto compartido.
- Un chunk `size = 4` contra un vecino `size = 1` conserva puntos de borde en `0, 1, 2, 3, 4`.
- Si el vecino es `size = 8` o `size = 16`, una celda `size = 4` usa solo sus esquinas; el vecino mas grueso se encarga de su propio lado si lo necesita.

Esto no es la optimizacion final de transiciones LOD; es la regla segura del motor para no perder la silueta de las costuras mientras seguimos desarrollando los resolvers especializados.

Regla de puntos por arista:

```text
puntosPorArista = size + 1
```

Ejemplos:

- `size = 1`: puntos por arista `2`, cubo basico con 8 esquinas.
- `size = 2`: puntos por arista `3`.
- `size = 4`: puntos por arista `5`.
- `size = 8`: puntos por arista `9`.
- `size = 16`: puntos por arista `17`.
- `size = 32`: puntos por arista `33`.

Si mas adelante decidimos que `MarchingCubes4` necesita 6 puntos por arista, eso implicaria una regla distinta a la rejilla entera inclusiva. De momento la regla documentada es `size + 1` porque coincide con `size = 2 -> 3`.

### Contrato del resolver

Cada resolver recibe una celda y consulta un campo escalar por coordenada:

```csharp
float value = scalarField.Sample(x, y, z);
```

O en forma Burst-friendly:

```csharp
float value = ScalarField.Sample(new int3(x, y, z), settings);
```

El campo escalar no devuelve una celda ni una malla. Devuelve el valor de densidad en esa coordenada. Con esos valores, el resolver decide donde cruza la superficie y que vertices escribir.

### Alineacion espacial

Como todo esta centrado en el mismo espacio:

- Las coordenadas enteras son coordenadas de mundo.
- Las aristas de las celdas coinciden con lineas de la rejilla mundial.
- Los puntos internos de una supercelda tambien caen en coordenadas enteras.
- No hace falta transformar a otro sistema para preguntar al campo escalar.

Ejemplo para `origin = 0,0,0` y `size = 2`, una arista en X consulta:

```text
0,0,0
1,0,0
2,0,0
```

Ejemplo para `origin = 0,0,0` y `size = 4`, una arista en X consulta:

```text
0,0,0
1,0,0
2,0,0
3,0,0
4,0,0
```

### Diccionarios y tablas de pintado

Queremos resolver como se pinta la mesh para cada configuracion con datos precomputados. Eso es correcto para runtime: una tabla de consulta es mas barata que tomar decisiones topologicas complejas en caliente.

Pero no debemos crear un diccionario gigante con todas las configuraciones posibles de una supercelda completa.

Si una celda de `size N` evaluase todos sus puntos internos como una unica configuracion binaria, tendria:

```text
puntos = (N + 1) * (N + 1) * (N + 1)
configuraciones = 2 ^ puntos
```

Ejemplos:

- `size = 1`: `(2^3) = 8` puntos, `2^8 = 256` configuraciones. Esto es perfecto.
- `size = 2`: `(3^3) = 27` puntos, `2^27 = 134.217.728` configuraciones. Ya es demasiado.
- `size = 4`: `(5^3) = 125` puntos, `2^125` configuraciones. Imposible.

Decision de arquitectura:

- `VoxelCell.size = 1` usa la tabla completa clasica de 256 casos.
- `VoxelCell.size > 1` se calcula en runtime con resolvers especializados.

El motivo es que el `size = 1` sera el caso caliente: se generaran muchas celdas minimas a la vez y ahi el lookup completo compensa. Para superceldas, una tabla completa explotaria en combinaciones, asi que se resuelven en runtime preguntando al campo escalar y usando tablas auxiliares pequenas.

La forma sana es usar tablas pequenas y especializadas:

- Tabla basica de Marching Cubes: 256 casos para una celda elemental.
- Tablas auxiliares por resolver para patrones de aristas, offsets e indices.
- Resolver especializado por `size` que sabe que puntos preguntar y como componer la malla.
- Nada de `Dictionary` administrado en Jobs; usar `NativeArray`, arrays estaticos Burst-friendly o tablas nativas persistentes.

El runtime debe hacer:

1. Consultar el campo escalar con `Sample(x, y, z)`.
2. Construir indices de caso pequenos.
3. Mirar tablas precomputadas.
4. Escribir vertices e indices.

No debe hacer:

- Buscar una configuracion global enorme de toda la supercelda.
- Resolver topologia arbitraria con ramas complejas.
- Usar diccionarios C# normales dentro de Jobs.

La regla general es: precomputar decisiones topologicas pequenas, no precomputar el universo entero de posibles superceldas.

## Campo escalar

La superficie no se guarda. Se pregunta a una funcion determinista que valor tiene una posicion.

El motor voxel no necesita saber de donde viene ese valor. Para probarlo podemos usar campos simples:

- Plano: `value = y - altura`.
- Esfera: `value = radius - length(position)`.
- Caja: distancia a un volumen de prueba.
- Ruido temporal o mock, mas adelante.

Ejemplo conceptual:

```csharp
float value = radius - math.length(pos);
bool occupied = value > 0f;
```

Mas adelante, el mundo/planeta podra implementar este mismo contrato combinando:

- Distancia al centro para esfericidad.
- Ruido 3D para montanas y valles.
- Capas de ruido para continentes.
- Curvas de altura.
- Mascaras de bioma.
- Reglas de material.

La condicion importante es que el campo sea determinista: la misma posicion debe devolver siempre el mismo valor.

## Pipeline de Jobs

La generacion de un chunk se divide en fases independientes.

### Job 1: EvaluateScalarFieldJob

Responsabilidad: evaluar posiciones del mundo y devolver ocupado/vacio.

Este Job no sabe nada de mallas, triangulos ni vecinos. Solo recibe posiciones y consulta el campo escalar.

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct EvaluateScalarFieldJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> targetPositions;
    [ReadOnly] public float isoLevel;

    public NativeArray<byte> outCornersResult;

    public void Execute(int index)
    {
        float3 pos = targetPositions[index];

        float value = SampleField(pos);

        outCornersResult[index] = value > isoLevel
            ? (byte)1
            : (byte)0;
    }

    private static float SampleField(float3 pos)
    {
        // Campo simple de prueba para validar el motor voxel.
        return 16f - math.length(pos);
    }
}
```

### Job 2: BuildVoxelCellsJob

Responsabilidad: construir `VoxelCell` a partir de los resultados de esquinas.

Entrada:

- Coordenada base del chunk.
- Tamano de chunk.
- `size` de las celdas que se van a construir.
- Resultados binarios de esquinas.

Salida:

- `NativeArray<VoxelCell>`.

Cada celda queda lista para que Marching Cubes resuelva su caso con `corners`.

### Job 3: MarchingCubesMeshJob

Responsabilidad: convertir celdas ocupadas/parciales en vertices e indices.

Entrada:

- `NativeArray<VoxelCell>`.
- Tabla nativa de casos.
- Resolver correspondiente al `size` de la celda.
- Datos de vertices por arista generados al consultar el campo escalar.

Salida:

- Buffer nativo de vertices.
- Buffer nativo de normales.
- Buffer nativo de indices.
- Conteos por chunk.

Despues del Job, el hilo principal copia esos buffers al `Mesh` de Unity.

### Render agregado

Cada chunk conserva su mesh individual como dato de runtime, pero no crea un `MeshRenderer` propio. El `VoxelChunkManager` mantiene un unico `MeshFilter`/`MeshRenderer` con un mesh combinado y un solo material.

Regla:

- El build de chunk produce una mesh local individual.
- El manager conserva esa mesh por chunk para poder cambiar LOD, hacer culling o reemplazar solo ese chunk.
- El renderer visible es un mesh combinado en el objeto padre.
- Cuando un chunk se genera, cambia de LOD, desaparece o cambia el cono de render, el mesh combinado se reconstruye con los chunks visibles.

Esto reduce batches y mantiene la individualidad logica de cada chunk.

### Cono de render

El manager puede filtrar que chunks entran en el mesh combinado usando un frustum/cono de vision:

- Si hay una `Camera` de culling asignada, se usan sus planos de frustum.
- Si no hay camara asignada pero existe `Camera.main`, se usa `Camera.main`.
- Si no hay camara, se usa un cono rectangular definido por un `Transform`, FOV vertical, aspect ratio y distancia maxima.

El chunk se testea con su `Bounds` de chunk completo. Esta prueba decide si la mesh individual del chunk entra o no en el mesh combinado. No destruye la mesh del chunk ni altera su estado de generacion.

## Configuracion del motor voxel

El motor voxel debe tener un archivo de configuracion propio. No queremos que valores estructurales como el tamano de chunk queden repartidos por scripts.

Configuracion inicial:

```csharp
using UnityEngine;
using Unity.Mathematics;

[CreateAssetMenu(menuName = "Voxel Engine/Voxel Engine Config")]
public sealed class VoxelEngineConfig : ScriptableObject
{
    [SerializeField] private Vector3Int chunkSize = new Vector3Int(32, 32, 32);

    public int3 ChunkSize => new int3(chunkSize.x, chunkSize.y, chunkSize.z);
}
```

Decision actual:

```text
chunkSize = 32 x 32 x 32
activeChunkRadius = 4
```

Eso significa que un chunk cubre 32 unidades base por eje. Como la unidad minima del juego es `1u x 1u x 1u`, un chunk base puede contener hasta:

```text
32 * 32 * 32 = 32.768 celdas minimas
```

Cuando se usen `VoxelCell.size > 1`, el mismo volumen de chunk se cubrira con menos celdas agregadas.

Los tamaños de celda usados por LOD deben dividir exactamente el tamano del chunk. Para chunk `32`, los tamaños principales son:

```text
1, 2, 4, 8, 16, 32
```

Esta es la lista base de resoluciones del motor. Si el chunk se baja a `16`, los tamaños validos pasan a ser `1, 2, 4, 8, 16`.

## Manager de chunks

La decision de que chunks existen vive en el hilo principal, dentro de un `MonoBehaviour` o un sistema equivalente.

Este manager no genera geometria directamente. Solo decide:

- Que chunks deberian existir.
- Que `size` usa cada grupo de celdas.
- Que chunks se encolan para generar.
- Que chunks se liberan.

El manager usa la posicion activa del player/anchor para calcular el chunk actual y decidir que paquetes de chunks deben existir.

El contenido concreto sigue siendo irrelevante para el motor. Lo importante es donde esta el player en coordenadas de mundo.

### PlayerChunkTracker

El player/anchor debe notificar al motor voxel cuando cambia de chunk. Esa notificacion evita recalcular el paquete de chunks cada frame sin necesidad.

Contrato conceptual:

```csharp
public event Action<int3> OnChunkChanged;
```

El `int3` enviado es el chunk actual del player/anchor.

Regla:

```text
Si GetChunkCoords(playerWorldPosition, config.ChunkSize) cambia, se dispara OnChunkChanged.
```

### Coordenada de chunk

```csharp
int3 GetChunkCoords(float3 worldPosition, int3 chunkSize)
{
    return (int3)math.floor(worldPosition / new float3(chunkSize.x, chunkSize.y, chunkSize.z));
}
```

Para chunks de `32 x 32 x 32`:

```text
world x 0..31   -> chunk x 0
world x 32..63  -> chunk x 1
world x -32..-1 -> chunk x -1
```

### LOD futuro por anillos

Cuando el motor voxel este validado, el mundo podra usar anillos alrededor del jugador. El entorno inmediato usara el LOD mas fino y los anillos posteriores aumentaran el `size` de las celdas generadas.

```csharp
int3 centerChunkCoords = GetChunkCoords(activeWorldPosition, config.ChunkSize);

for (int x = -maxDistance; x <= maxDistance; x++)
{
    for (int y = -maxDistance; y <= maxDistance; y++)
    {
        for (int z = -maxDistance; z <= maxDistance; z++)
        {
            int3 targetChunk = centerChunkCoords + new int3(x, y, z);
            int distance = math.max(math.abs(x), math.max(math.abs(y), math.abs(z)));

            int cellSize;
            if (distance <= 1)
            {
                cellSize = 1;
            }
            else if (distance <= 3)
            {
                cellSize = 4;
            }
            else
            {
                cellSize = 16;
            }

            EnsureChunkState(targetChunk, cellSize);
        }
    }
}
```

### Estado deseado vs estado actual

El manager debe comparar el estado deseado de cada chunk con el estado actual antes de lanzar trabajo.

Un chunk no debe hacer nada si:

- Ya existe.
- Ya esta en el `VoxelCell.size` que le corresponde.
- No esta marcado como dirty.
- No tiene un cambio pendiente de configuracion o campo escalar.

Regla:

```text
Si currentChunkState == desiredChunkState, no se encola ningun Job.
```

Contrato conceptual:

```csharp
void EnsureChunkState(int3 chunkCoord, int desiredCellSize)
{
    if (IsChunkReady(chunkCoord, desiredCellSize))
    {
        return;
    }

    QueueChunkGeneration(chunkCoord, desiredCellSize);
}
```

Esto es clave para ahorrar calculo: al cambiar de chunk el player/anchor, el manager puede recalcular el conjunto deseado, pero solo deben regenerarse los chunks nuevos, los chunks que cambien de LOD/`size`, o los chunks marcados como dirty. Todo lo demas se queda quieto.

### Propagacion de cambios de chunk

Los cambios de resolucion no deben aplicarse destruyendo y regenerando todos los chunks en el mismo frame. El manager mantiene:

- Chunks activos visibles.
- Estados deseados por chunk.
- Cola de builds pendientes.
- Builds en vuelo con sus `JobHandle`.

Cuando cambia el LOD deseado de un chunk, el chunk viejo sigue visible. El manager encola el nuevo estado y arranca un numero limitado de builds por frame. Cuando los Jobs del chunk terminan, el hilo principal construye la `Mesh`, cambia el chunk visible por el nuevo y destruye el anterior.

Si mientras un build esta en vuelo el estado deseado cambia otra vez, ese resultado se descarta al terminar y se encola el estado nuevo. Asi evitamos swaps obsoletos y tambien evitamos cortar la visibilidad de un chunk antes de tener su reemplazo preparado.

La cola de builds se ordena por cercania al chunk activo. El entorno inmediato del jugador se genera primero y los anillos exteriores se van propagando despues. A igualdad de distancia se prioriza el `cellSize` mas fino y luego el orden de llegada.

La propagacion trabaja en paquetes configurables:

- `maxChunkBuildsStartedPerFrame`: cuantos builds nuevos puede arrancar el manager por frame.
- `maxConcurrentChunkBuilds`: cuantos builds pueden estar en vuelo a la vez para no disparar memoria.
- `maxChunkSwapsPerFrame`: cuantas meshes terminadas puede hacer visibles por frame.

Los swaps terminados tambien se priorizan por cercania al jugador, para que el trabajo que se hace visible siga la misma logica que el trabajo que se arranca.

Al entrar en Play Mode, la primera generacion puede completarse de forma sincronica para evitar ver el mundo aparecer por paquetes desde cero. A partir de ahi, los cambios provocados por movimiento, LOD o dirty flags se propagan con la cola incremental.

En modo editor, los botones de generacion completan la cola de forma sincronica para conservar el flujo manual de inspector.

### Chunks declarados vivos

El radio activo no crea el universo. Solo filtra chunks ya declarados por objetos o sistemas que tienen datos en esa zona.

Regla:

- Un objeto que ocupa un chunk con datos debe reservar ese chunk en el manager.
- Un chunk reservado queda declarado vivo.
- Si nadie declara un chunk, ese chunk no existe para el motor.
- Acceder a un chunk no declarado o no generado devuelve `null`.
- Para pintado de malla, un chunk no declarado cuenta como aire.
- El manager no debe iterar todos los chunks posibles dentro del radio activo; debe iterar los chunks declarados y quedarse solo con los que caen dentro del radio.

Esto permite que un universo conceptual de `1000 x 10000 x 1000` chunks tenga solo 20 chunks reales si solo 20 han sido declarados. En ese caso, subir `activeChunkRadius` no debe provocar que se recorran billones de coordenadas.

### Regla inicial de LOD

El LOD se configura en `VoxelEngineConfig` como una tabla ordenada por distancia de chunk. Cada entrada define:

- `maxChunkDistance`: distancia maxima inclusiva desde el chunk activo.
- `cellSize`: tamano de `VoxelCell` para ese tramo.

Configuracion inicial para chunk `32`:

- `distance <= 1`: `VoxelCell.size = 1`.
- `distance <= 3`: `VoxelCell.size = 4`.
- `distance <= 5`: `VoxelCell.size = 8`.
- `distance <= 7`: `VoxelCell.size = 16`.
- `distance > 7`: `VoxelCell.size = 32`, o el ultimo `cellSize` configurado.

La tabla se normaliza para que cada `cellSize` divida exactamente el tamano de chunk. Para chunk `32`, los tamanos principales son `1, 2, 4, 8, 16, 32`; la configuracion por defecto usa `1, 4, 8, 16, 32`.

Importante: estos valores no cambian la unidad minima del juego. La unidad minima sigue siendo `1u x 1u x 1u`; el `size` de `VoxelCell` indica cuantas unidades minimas agrupa la celda generada.

## Memoria

El motor voxel debe poder trabajar con espacios muy grandes sin ocupar memoria proporcional al tamano total del mundo.

La memoria activa contiene solo:

- Tabla de Marching Cubes.
- Buffers temporales de Jobs.
- Chunks activos.
- Mallas actualmente renderizadas.

No se almacena:

- El mundo completo.
- Una matriz global de voxels.
- Todos los chunks visitables.
- Datos de terreno fuera del area activa.

Cuando cambia el conjunto de chunks activos:

1. Se detectan nuevos chunks necesarios.
2. Se encolan Jobs para generarlos.
3. Se crean o actualizan mallas.
4. Los chunks lejanos se destruyen.
5. Sus buffers se liberan.

## Estructura futura recomendada

Cuando se implemente, partir de una estructura pequena:

```text
Assets/
  Scripts/
    Data/
      VoxelCell.cs
      ChunkCoord.cs
      ScalarFieldSettings.cs
      VoxelEngineConfig.cs
    Jobs/
      EvaluateScalarFieldJob.cs
      BuildVoxelCellsJob.cs
      MarchingCubesMeshJob.cs
    MarchingCubes/
      MarchingCubesTables.cs
      MarchingCubesCaseTableBuilder.cs
      MarchingCubesResolver.cs
      MarchingCubes2Resolver.cs
      MarchingCubes4Resolver.cs
      MarchingCubes8Resolver.cs
      MarchingCubes16Resolver.cs
      MarchingCubes32Resolver.cs
    Runtime/
      VoxelChunkManager.cs
      VoxelChunkMesh.cs
      PlayerChunkTracker.cs
      VoxelEngineBootstrap.cs
```

## Primer hito tecnico

El primer hito es el motor voxel minimo, no el mundo:

1. Un unico chunk de `32 x 32 x 32`, tomado de `VoxelEngineConfig`.
2. Evaluacion de un campo escalar simple con Burst.
3. Generacion de `VoxelCell` con `corners` empaquetado.
4. Marching Cubes `size = 1` usando tabla nativa.
5. Mesh visible en escena.
6. Liberacion correcta de todos los `NativeArray`.

Cuando ese hito funcione, se anaden resolvers `size > 1`. Despues vendra el manager de anillos de LOD.

## Prueba manual actual

Para probar el motor voxel en escena:

1. Usar `GameObject > Voxel Engine > Complete Sphere Demo`.
2. Seleccionar `Voxel Sphere Engine`.
3. Ajustar el radio en `VoxelSphereGenerator`.
4. Pulsar `Generate` en el inspector.
5. Entrar en Play Mode y mover `Voxel Demo Player`.

Controles del player demo:

- `WASD`: mover en horizontal.
- `Q/E`: bajar/subir.

Al cambiar de chunk, `PlayerChunkTracker` dispara `OnChunkChanged`. El manager recalcula el conjunto deseado y solo regenera chunks nuevos, dirty, o con `VoxelCell.size` incorrecto. Los chunks que ya estan bien no hacen ningun trabajo.

La esfera de prueba se genera preguntando el campo escalar en las 8 esquinas de cada celda:

- Si todas las esquinas estan dentro del radio, la celda queda completamente solida.
- Una celda completamente solida se pinta como cubo completo.
- Si solo algunas esquinas estan dentro, Marching Cubes genera la superficie parcial.
- Si ninguna esquina esta dentro, no genera triangulos.

## Fuera de alcance por ahora

- Persistencia de terreno modificado.
- Biomas complejos.
- Streaming asincrono avanzado.
- Transiciones suaves entre LODs.
- Caves, minerales o estructuras.
- Guardado/carga de chunks.
- Ecuacion final del planeta.

Primero se valida la base: campo escalar determinista, Jobs, Burst, tabla nativa y memoria limpia.
