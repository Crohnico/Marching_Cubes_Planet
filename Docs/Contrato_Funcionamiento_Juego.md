# Contrato de funcionamiento del juego
Este documento define como debe funcionar el juego. Es el contrato de diseno y comportamiento: cuando cambiemos codigo, arquitectura o contenido, el cambio debe respetar lo escrito aqui o actualizar este documento de forma explicita.

## Proposito

Evitar que las reglas del juego cambien de forma accidental cada vez que tocamos una parte tecnica.

Este documento responde a preguntas como:

- Que espera el juego del mundo.
- Que reglas nunca deben romperse.


## Como usar este documento

- Cada regla escrita aqui se considera intencional.
- Si para hacer algo hay que modificar algo escrito aqui, no se hace hasta avisar que eso va a ocurrir.
- No se puede eliminar nada que este escrito en la documentacion.
- Si una linea documentada entra en conflicto con una peticion nueva o con un cambio de codigo, el agente debe pedir que una persona la elimine o la cambie manualmente.
- Mientras ese conflicto siga escrito en la documentacion, el agente no puede atacar el problema por fuera ni hacer un workaround para evitar la regla documentada.


## Reglas confirmadas

### StellarSystem

`StellarSystem` es el sistema responsable de descubrir, ordenar e inicializar los planetas que pertenecen a un sistema estelar.

#### Datos propios

`StellarSystem` tiene:

- Una lista privada de los planetas del sistema estelar.
- La lista de planetas esta oculta en el inspector.
- Un `string ID` con el nombre propio del sistema estelar.
- Un boton en el inspector para borrar la carpeta de archivos usada por funcionalidades de debug.

La carpeta debug del sistema se resuelve como:

```text
StellarSystems/{SystemId}
```

Dentro de `Application.persistentDataPath`.

#### Descubrimiento de planetas

En `Start`, `StellarSystem` busca todos los `PlanetManager` presentes en la escena.

Cuando los encuentra, los ordena de mas cerca a mas lejos respecto al `Player`.


#### Nombre de planeta

Cuando `StellarSystem` ya tiene todos los planetas ordenados, asigna a cada planeta un nombre basado en su posicion `XYZ` en el mundo.

El formato del identificador de planeta es:

```text
Planet_X{x}_Y{y}_Z{z}
```


#### Inicializacion secuencial

`StellarSystem` inicializa los planetas uno a uno usando `Task`.

El flujo es:

1. Inicializa el planeta A.
2. Espera a que el `Task` del planeta A termine.
3. El `Task` del planeta A se completa cuando `PlanetManager.Initialize(...)` termina.
4. `StellarSystem` inicializa el planeta B.
5. El proceso continua hasta que todos los planetas del sistema estelar han sido inicializados.

No se inicializan varios planetas en paralelo dentro de este flujo.

`StellarSystem` llama al flujo nuevo:

```csharp
await planet.Initialize(stellarID);
```

No llama al flujo antiguo basado en coroutine ni a presupuestos por frame de arranque.

### PlanetManager

`PlanetManager` es el script de consulta de estados y necesidades del planeta.

No debe concentrar responsabilidades que pertenezcan a otros sistemas. Las responsabilidades se definiran poco a poco en este contrato antes de mover o cambiar codigo.

#### Datos propios

`PlanetManager` tiene:

- Un valor `Radius`.
- Un valor `Seed`.
- Un valor `ActionAreaRadiusPadding`.
- Un valor `AtmosphereRadius`.

`Radius` define el radio base del planeta.

`Seed` define la semilla usada para generar el planeta.

`ActionAreaRadiusPadding` define cuanto se suma al `Radius` para calcular la esfera de radio de accion del planeta.

El radio de accion se usa para decidir si el planeta debe mantenerse activo para actualizaciones y carga cercana.

La atmosfera no decide carga de chunks ni LOD. Solo representa la zona en la que el jugador se considera dentro de la gravedad/atmosfera del planeta.

`AtmosphereRadius` se inicializa como `Radius * 2`.

#### Funciones debug

`PlanetManager` expone funciones debug para visualizar estados del planeta en escena.

Tiene:

- Un bool `DrawGizmosSegments`.
- Un bool `DrawActionAreaGizmo`.
- Un bool `DrawAtmosphereGizmo`.
- Un boton `Clear Planet Data`.

`DrawGizmosSegments` activa o desactiva los gizmos de los segmentos del planeta.

`DrawActionAreaGizmo` activa o desactiva el gizmo de la esfera del radio de accion del planeta.

`DrawAtmosphereGizmo` activa o desactiva el gizmo de la atmosfera del planeta.

`Clear Planet Data` borra de disco la carpeta persistente del planeta.

#### Atmosfera

La atmosfera lanza una señal cuando el jugador entra o sale de la atmosfera del planeta. SignalBus.

#### Inicializacion

`PlanetManager` inicializa el planeta con este flujo:

```csharp
Task Initialize(string stellarID)
{
    InitializePlanetData();
    InitializeFarMesh();
    InitializePlanet();
}
```

La inicializacion se divide en tres pasos:

- `InitializePlanetData()`
- `InitializeFarMesh()`
- `InitializePlanet()`

`InitializePlanetData()` obtiene el `PlanetData` del planeta.

Si existe `PlanetData` en disco, se usa ese dato.

Si no existe, `PlanetInitializer` crea el `PlanetData`, lo devuelve nutrido y se guarda en disco.

`InitializeFarMesh()` obtiene la far mesh del planeta.

Si existe far mesh en disco, se usa esa mesh.

Si no existe, `MeshCrafter` crea la far mesh usando `PlanetData`, se guarda la far mesh en disco y se guarda tambien `PlanetData`.

`InitializePlanet()` queda pendiente de definir.

`PlanetManager` no debe ser quien construye directamente `PlanetData` ni far mesh. Solo coordina y conserva estado.

`InitializePlanet()` hidrata el estado runtime del planeta desde `PlanetData`.

Los chunks runtime se reconstruyen desde `PlanetData.chunks`, no desde una generacion implicita dentro de `PlanetManager`.

Este flujo nuevo es el flujo normal de arranque del planeta.

El flujo antiguo de arranque por coroutine no forma parte de la inicializacion normal usada por `StellarSystem`.

#### Separacion de responsabilidades

`PlanetManager` no debe funcionar como libreria de utilidades ni como fabrica directa de meshes/chunks.

`PlanetManager` puede:

- Consultar estado del planeta.
- Decidir que chunks necesita el planeta.
- Coordinar la inicializacion.
- Hidratar estado runtime desde `PlanetData`.
- Mantener referencias runtime necesarias para render, culling y visibilidad.

`PlanetManager` no debe contener:

- Metodos genericos de conversion entre `int3`, `float3` y `Vector3`.
- Metodos genericos para copiar `NativeList` a listas manejadas.
- Comparadores genericos de coordenadas.
- Logica interna de construccion de celdas de chunk.
- Logica interna de scheduling de jobs de generacion de chunk.
- Logica interna de subida de buffers nativos a `Mesh`.

Las responsabilidades quedan separadas asi:

- `VoxelRuntimeMath`: utilidades matematicas/conversiones de tipos runtime.
- `NativeListCopyUtility`: copias entre contenedores nativos y listas manejadas.
- `ChunkBuilder`: construye requests de celdas, normaliza cell size de chunk, lanza jobs de evaluacion/generacion y devuelve builds o datos de mesh.
- `PlanetChunkBehaviour`: fachada funcional de chunks que recibe `Tick` desde `PlanetManager.Update()`.
- `PlanetChunkRuntime`: posee el estado runtime de chunks declarados, deseados y activos del planeta.
- `PlanetCombinedMeshBehaviour`: posee el estado runtime de combined meshes, far mesh visible, near meshes por segmento y builds diferidos de LOD de segmentos.
- `MeshCrafter`: convierte datos de mesh en `Mesh` de Unity y construye far mesh/mesh data desde `PlanetData`.
- `PlanetInitializer`: crea `PlanetData` cuando no existe en disco, delegando la generacion tecnica de chunks en `ChunkBuilder`.

Regla importante:

Si aparece una utilidad generica o una fabrica de mesh/chunk dentro de `PlanetManager`, debe moverse a una clase dedicada salvo que el contrato defina explicitamente lo contrario.

Mover campos a propiedades proxy dentro de `PlanetManager` no cuenta como separar responsabilidades.

Un puente temporal puede existir solo para mantener el codigo compilando durante una migracion, pero la direccion correcta es mover funciones completas al behaviour o servicio propietario del estado.

#### Gestion runtime de chunks

`PlanetManager` no es el propietario directo de la gestion runtime de chunks.

La gestion funcional de chunks pertenece a `PlanetChunkBehaviour`.

`PlanetManager.Update()` ejecuta el tick de chunks:

```csharp
chunkBehaviour.Tick(...);
```

`PlanetManager` no debe implementar directamente el update de visibilidad de chunks ni el ciclo de vida de desired/active chunks.

`PlanetChunkBehaviour` se divide por responsabilidades:

- `PlanetChunkBehaviour.Tick`: update/tick de la funcionalidad de chunks.
- `PlanetChunkBehaviour.Declarations`: declaracion, liberacion e hidratacion de chunks.
- `PlanetChunkBehaviour.Build`: desired chunks y builds sincronas/budgeted.
- `PlanetChunkBehaviour.Lifecycle`: limpieza, dirty flags y conteos.

`PlanetChunkRuntime` queda como estado interno y operaciones base usadas por `PlanetChunkBehaviour`.

`PlanetChunkRuntime` tiene:

- Chunks declarados.
- Chunks deseados.
- Chunks activos.
- Estados `DesiredChunkState`.
- Estados `VoxelChunkState`.
- Buffer de requests de celdas usado para builds.
- Estado de visibilidad/culling de chunks.
- Planos de frustum usados por visibilidad de chunks.

`PlanetChunkRuntime` se encarga de:

- Declarar y liberar chunks.
- Aplicar los chunks declarados desde `PlanetData`.
- Hidratar chunks activos desde `PlanetData`.
- Completar un `ChunkBuild` y convertirlo en `VoxelChunkState`.
- Destruir meshes de chunks cuando dejan de ser necesarios.
- Marcar chunks como dirty.
- Eliminar chunks que ya no son deseados.
- Limpiar chunks activos/deseados.
- Actualizar visibilidad de chunks contra rango de camara y frustum.
- Contar chunks visibles.

`PlanetManager` puede consultar ese estado y coordinar sus consecuencias, por ejemplo:

- Marcar combined meshes como dirty cuando cambia un chunk.
- Actualizar renderer visibility cuando cambia la visibilidad de chunks.
- Pedir a `ChunkBuilder` que construya chunks necesarios.
- Pedir a `PlanetChunkBehaviour` que complete o aplique el resultado.

Regla importante:

La logica de ciclo de vida de chunks no debe volver a `PlanetManager`.

Si una funcion habla principalmente de `VoxelChunkState`, `DesiredChunkState`, declarar/liberar chunks, completar builds de chunks, visibilidad de chunks o destruccion de meshes de chunks, debe estar en `PlanetChunkBehaviour`, `PlanetChunkRuntime` o en otro script dedicado de chunks.

#### Gestion runtime de combined meshes y segment LOD

`PlanetManager` no debe ser el propietario directo del estado runtime de combined meshes.

La gestion de estado de combined meshes pertenece a `PlanetCombinedMeshBehaviour`.

`PlanetCombinedMeshBehaviour` contiene:

- Buffers de `CombineInstance`.
- Buckets de combined mesh cercanos.
- Bucket visible/far.
- Cola de builds diferidos de segment LOD.
- Flags dirty de combined mesh, layout y visibilidad.
- Estado de modo de render cercano/far.
- Estado de refresco de hemisferio far.

`PlanetManager` puede coordinar llamadas mientras dure la migracion, pero no debe acumular nuevos campos de combined mesh ni segment LOD.

Regla importante:

Si una funcion habla principalmente de buckets, combined meshes, far bridge, near combined rendering, segment LOD o builds diferidos de LOD, debe moverse progresivamente a `PlanetCombinedMeshBehaviour` o a otro script dedicado de render/mesh.

Las propiedades puente entre `PlanetManager` y `PlanetCombinedMeshBehaviour` son deuda temporal y deben reducirse, no crecer.

#### Colision de segmentos

Los segmentos cercanos usan colision por `MeshCollider` en su objeto `LOD_0`.

El `MeshCollider` de `LOD_0` usa la misma `Mesh` asignada al `MeshFilter` de `LOD_0`.

`VoxelSegmentLodMeshCache` es responsable de crear y sincronizar el `MeshCollider` de `LOD_0` cuando se asigna o retira la mesh del LOD.

`LOD_1` y `LOD_2` no crean colliders de segmento.

#### Costuras entre LOD de segmentos

Cuando dos segmentos cercanos vecinos estan visibles y usan LOD distinto, la union entre ellos se resuelve con una mesh independiente de costura.

La mesh de costura no pertenece a ninguno de los dos segmentos. Vive bajo el root de costuras de los near meshes.

Las costuras se crean solo para vecinos directos de la grid de segmentos en los ejes `X`, `Y` y `Z`.

Las combinaciones runtime esperadas son:

- `LOD_0 - LOD_1`
- `LOD_1 - LOD_0`
- `LOD_1 - LOD_2`
- `LOD_2 - LOD_1`

No se crea costura cuando los dos segmentos usan el mismo LOD.

Las costuras se cachean en disco por pareja de segmentos, eje compartido y combinacion de LOD.

La cache runtime de costuras se libera cuando el planeta deja de usar near meshes o cuando se destruyen/limpian las combined meshes del planeta.

`PlanetSegmentSeamBehaviour` es responsable del estado runtime de las costuras.

`SegmentLodSeamBuilder` es responsable de construir la mesh de costura.

#### PlanetData

`PlanetData` es la fuente de verdad serializable del planeta.

Debe contener todo lo necesario para que `MeshCrafter` pueda construir far mesh y near mesh sin depender de estado runtime de `PlanetManager`.

`PlanetData` no guarda objetos runtime de Unity:

- No guarda `GameObject`.
- No guarda `MeshFilter`.
- No guarda `MeshRenderer`.
- No guarda `Material`.
- No guarda `JobHandle`.
- No guarda `NativeArray`.
- No guarda flags temporales de culling/frustum.

`PlanetData` si guarda datos equivalentes y serializables:

- Identidad del planeta.
- Configuracion usada para generar el planeta.
- Layout de chunks.
- Layout de segmentos.
- Datos de mesh por chunk.
- Datos de mesh agregados/cacheados para far y near, si existen.

La estructura conceptual de `PlanetData` es:

```csharp
PlanetData
{
    PlanetIdentity identity;
    PlanetGenerationConfig generation;
    PlanetChunkLayout chunkLayout;
    PlanetSegmentLayout segmentLayout;
    List<PlanetChunkData> chunks;
    PlanetMeshCache meshCache;
}
```

`PlanetIdentity` contiene:

- `stellarID`.
- `planetID`.
- posicion de mundo.

`PlanetGenerationConfig` contiene:

- `Radius`.
- `Seed`.
- `chunkSize`.
- cell sizes usados por LOD.
- distancias/reglas usadas para LOD.
- datos necesarios del campo escalar.

`PlanetChunkLayout` contiene:

- chunks declarados.
- chunks activos para build inicial.
- origen de cada chunk.
- bounds de cada chunk.
- cell size elegido para cada chunk.
- `detailFocusKey` usado al crear el chunk.

`PlanetSegmentLayout` contiene:

- cantidad de segmentos.
- grid de segmentos.
- bounds de cada segmento.
- relacion entre segmento y chunks.
- LOD disponible por segmento.

`PlanetChunkData` contiene:

- coordenada del chunk.
- origen del chunk.
- bounds del chunk.
- cell size.
- segmento al que pertenece.
- datos de mesh del chunk.

Los datos de mesh del chunk son obligatorios para que el planeta pueda reconstruir far mesh y near mesh desde `PlanetData`.

Los datos de mesh del chunk son datos puros:

```csharp
PlanetMeshData
{
    vertices;
    normals;
    uvs;
    interiorIndices;
    transitionIndices;
    surfaceIndices;
    bounds;
}
```

`PlanetMeshCache` contiene datos derivados que se pueden reconstruir desde los chunks, pero se guardan para acelerar arranque:

- far mesh.
- near mesh por segmento.
- near mesh por segmento y LOD.
- version de cache.

Regla importante:

Si `PlanetMeshCache` no existe o esta obsoleto, `MeshCrafter` debe poder reconstruir far mesh y near mesh usando solo `PlanetData.chunks`.

Por tanto, `PlanetData.chunks` es obligatorio. `PlanetMeshCache` es acelerador, no fuente unica.

Decision confirmada:

- `PlanetData` es la fuente de verdad persistente del planeta.
- `PlanetInitializer` crea `PlanetData` cuando no existe en disco.
- `MeshCrafter` crea far mesh usando `PlanetData`.
- `PlanetManager` hidrata su estado runtime desde `PlanetData`.
- `StellarSystem` solo orquesta la inicializacion secuencial.
