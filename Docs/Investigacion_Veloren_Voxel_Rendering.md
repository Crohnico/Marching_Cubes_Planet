# Investigacion: Veloren y render voxel para planetas Marching Cubes

Fecha: 2026-06-20

## Resumen ejecutivo

El problema actual no es el numero bruto de triangulos del demo. El problema es que el renderer no distingue entre:

- superficie exterior que puede aportar imagen desde lejos;
- volumen interior/cuevas que solo importa cerca, dentro, o mirando una entrada;
- cuerpos celestes completos que ni siquiera deberian entrar al presupuesto cuando estan fuera de camara;
- chunks declarados, chunks generados, chunks visibles y chunks renderizados.

La conclusion principal de Veloren es esta: el chunk es una unidad logica de terreno, pero el render se decide con otro estado: visibilidad, bounds ajustados, cola de mesh, datos GPU, LOD/instancias y pases separados. No hay una unica regla de "chunk existe => chunk renderiza".

Para nuestro motor, la direccion sana es:

1. Mantener mesh combinado o una cantidad muy baja de draw calls. No volver a un `MeshRenderer` por chunk.
2. Restaurar culling, pero no como barrido caro de todos los chunks cada frame. Hacer culling jerarquico por cuerpo celeste y despues por chunks visibles/candidatos, con bounds ajustados y cache.
3. Separar exterior e interior/cuevas como dominios de render. Desde lejos, el cuerpo celeste renderiza solo su envolvente exterior o proxy de LOD; las cuevas no entran en la malla visible.
4. Generar y guardar metadatos de visibilidad por chunk: `tightBounds`, `surfaceClass`, `hasExteriorSurface`, `hasInteriorSurface`, `lastFrustumPlane`, `visible`, `meshVersion`.
5. Usar el LOD dinamico como decision de diseno, pero gobernado por presupuesto: distancia, tamano en pantalla, cuerpo celeste activo, visibilidad y cercania al jugador.

## Fuentes revisadas

- Medium, Adam/Game Effects Studio:
  - Part 1, Voxels & Chunks: https://medium.com/@adamy1558/building-a-high-performance-voxel-engine-in-unity-a-step-by-step-guide-part-1-voxels-chunks-86275c079fb8
  - Part 2, Mesh Generation: https://medium.com/@adamy1558/building-a-high-performance-voxel-engine-in-unity-a-step-by-step-guide-part-2-mesh-generation-bcf1401a5b4b
  - Part 3, Noise: https://medium.com/@adamy1558/building-a-high-performance-voxel-engine-in-unity-a-step-by-step-guide-part-3-noise-98e0c91fee59
  - Part 4, Infinite Terrain: https://medium.com/@adamy1558/building-a-high-performance-voxel-engine-in-unity-a-step-by-step-guide-part-4-infinite-terrain-7b7ec2063a80
- Veloren:
  - Repo: https://github.com/veloren/veloren
  - Terrain meshing: https://github.com/veloren/veloren/blob/master/voxygen/src/mesh/terrain.rs
  - Greedy meshing: https://github.com/veloren/veloren/blob/master/voxygen/src/mesh/greedy.rs
  - Terrain render state/culling: https://github.com/veloren/veloren/blob/master/voxygen/src/scene/terrain/mod.rs
  - Terrain chunk/chonk storage: https://github.com/veloren/veloren/blob/master/common/src/terrain/chonk.rs
  - Terrain chunk size/conversions: https://github.com/veloren/veloren/blob/master/common/src/terrain/mod.rs
  - View distances: https://github.com/veloren/veloren/blob/master/common/src/view_distances.rs

Nota de licencia: Veloren esta bajo GPL-3.0. Este documento extrae ideas arquitectonicas. No conviene copiar codigo de Veloren al proyecto salvo que se acepte el impacto de licencia.

## Lo util de los articulos de Medium

Los articulos son una base didactica, no una solucion de produccion para nuestro caso.

Ideas aprovechables:

- Chunks como unidad de gestion.
- No generar caras ocultas entre voxeles solidos.
- Generacion alrededor del jugador.
- Ruido/campo determinista para reproducibilidad.
- LOD, multithreading y estructuras eficientes como objetivos.

Lo que no nos sirve directamente:

- Usa voxeles cubicos, no Marching Cubes.
- Plantea arrays 3D y GameObjects de chunk como tutorial, no como arquitectura final.
- El greedy meshing de cubos no arregla una isosuperficie suave. En Marching Cubes no tenemos grandes caras coplanares rectangulares que fusionar.
- La parte de terreno infinito es XZ plano; nuestro caso es planetario, multi-cuerpo y con cuevas.

Conclusion: Medium confirma principios, pero no cambia nuestra arquitectura.

## Lo que Veloren hace bien

## Como resuelve Veloren exactamente las meshes lejanas

Veloren tiene dos sistemas separados:

1. `Terrain`: chunks voxel cercanos, con mesh real por chunk, greedy meshing, workers, bounds, visibilidad y render normal.
2. `Lod`: terreno lejano, no basado en meshes de chunks. Es una malla global/procedural que se deforma en shader usando texturas globales generadas desde worldgen.

La parte importante esta en:

- `voxygen/src/scene/lod.rs`
- `voxygen/src/render/pipelines/lod_terrain.rs`
- `assets/voxygen/shaders/lod-terrain-vert.glsl`
- `assets/voxygen/shaders/lod-terrain-frag.glsl`
- `assets/voxygen/shaders/include/lod.glsl`
- `client/src/lib.rs`

### La malla lejana no son chunks combinados

`create_lod_terrain_mesh(detail)` crea una reticula de quads alrededor del centro. Usa una espiral 2D, salta el centro y genera quads en un cuadrado normalizado de `-1..1`.

Conceptualmente:

```text
LOD mesh = grid/anillo centrado en camara
vertices = posiciones 2D normalizadas
shader:
    splay(vertex2D) -> posicion mundo XY
    alt_at_real(XY) -> altura desde textura
    color desde textura
    normal desde diferencias de altura
```

No esta sumando meshes de chunks lejanos. Esta dibujando un proxy continuo del mundo lejano.

### Splay: concentrar detalle cerca y abrirlo lejos

En `assets/voxygen/shaders/include/lod.glsl`, `splay(pos)` convierte la posicion normalizada del quad en una distancia real desde el foco. La funcion no reparte los vertices de forma lineal: concentra detalle cerca del borde de la zona cargada y estira cada vez mas hacia lejos.

Idea:

```text
pos normalizada cerca del centro -> distancia cercana a viewDistance
pos normalizada hacia bordes     -> distancia enorme
```

Esto permite que una malla de pocos miles de vertices cubra muchisima distancia. La densidad de vertices baja con la distancia, que es justo lo que queremos para planetas/lunas lejanas.

### La altura viene de una textura global

En `client/src/lib.rs`, `WorldData` guarda:

- `lod_base`: color/base map.
- `lod_alt`: altura.
- `lod_horizon`: datos de horizonte/sombras lejanas.

`LodData::new` sube esas capas a GPU como texturas:

- `map`: `Rgba8UnormSrgb`
- `alt`: `Rgba8Unorm`
- `horizon`: `Rgba8Unorm`
- `weather`: otra textura separada.

El shader reconstruye altura con algo equivalente a:

```text
alt = decodedTextureAlt * maxHeight + minHeight - focusOffsetZ
```

Para nuestro caso planetario, esto se traduce a:

```text
radius = baseRadius + decodedHeight
position = planetCenter + normalFromPatch * radius
```

### El color tambien viene de una textura global

`lod_col(pos)` samplea `t_map`, la textura de color del mapa. No pinta materiales voxel reales lejanos. Pinta una representacion agregada del terreno.

Para nuestro motor:

- desde lejos no necesitamos materiales/cuevas voxel exactos;
- necesitamos un color/material macro: roca, hielo, vegetacion, arena, agua, etc.;
- la textura puede venir del mismo campo escalar/material que genera los chunks cercanos.

### El LOD lejano convive con terrain cercano

En `scene/mod.rs`, Veloren renderiza el terrain normal y tambien el `lod.render`. No hace un swap perfecto chunk-a-chunk. El depth buffer resuelve el solape.

Esto es importante:

```text
near terrain real: chunks voxel con mesh completa
far terrain proxy: heightmap LOD
zona de solape: profundidad / bias / pull-down para evitar z-fighting
```

En el vertex shader de LOD hay un pequeno `pull_down` para bajar un poco el proxy y evitar pelearse con el terreno real cerca del borde.

### Objetos lejanos van instanciados por zonas

Veloren tambien tiene `lod::Zone`: zonas de `32 x 32` chunks. El servidor/cliente carga objetos resumidos por zona: arboles, casas, grandes estructuras. Esos objetos usan modelos `.obj` muy simples e instancing.

Esto no resuelve el terreno, pero si resuelve "cosas visibles lejanas":

```text
terrain lejano = proxy heightmap
objetos lejanos = instancias por zona
terrain cercano = chunks reales
objetos cercanos = entidades/meshes normales
```

Para nosotros:

- planeta/luna lejana: proxy de superficie.
- bases/estructuras/arboles gigantes lejanos: instancias/proxies por zona.
- cuevas/tuneles lejanos: nada.

## Traduccion directa a nuestro motor

Si vamos a resolverlo como Veloren, el sistema deberia quedar asi:

```text
CelestialBodyRenderer
  FarSurfaceProxy
    patch mesh / cube-sphere mesh / ico-sphere subdividida
    height texture o height cache
    color/material texture
    optional horizon/occlusion texture

  NearVoxelTerrain
    chunks Marching Cubes reales
    LOD dinamico actual
    cuevas solo cerca/interior
```

### Para planetas, no usar grid plano: usar patches esfericos

Veloren puede usar XY porque su mundo es plano. Nosotros necesitamos una version esferica:

```text
6 caras de cube-sphere por cuerpo celeste
cada cara tiene una reticula LOD
vertex shader / CPU:
    uv de cara -> direccion normalizada desde centro
    sample height(face, uv)
    pos = center + direction * (radius + height)
```

La idea equivalente a `splay`:

- Si el player esta lejos del cuerpo, usar una reticula uniforme baja.
- Si se acerca, concentrar detalle alrededor del punto de impacto/closest point en la esfera.
- Si esta en superficie, el proxy puede bajar prioridad y los chunks MC toman el primer plano.

### Niveles recomendados

```text
FarBodyProxy:
    siempre maximo 1 renderer por cuerpo
    mesh de patches, sin cuevas, sin chunks interiores
    height/color macro

MidSurfaceChunks:
    chunks MC exteriores visibles por frustum
    LOD grueso/intermedio
    sin cuevas profundas

NearVoxelChunks:
    chunks MC con LOD dinamico fino
    terraformacion
    cuevas/tuneles en radio local
```

Esto es basicamente Veloren aplicado a esfera:

```text
Veloren terrain LOD global    -> nuestro FarBodyProxy
Veloren terrain chunks reales -> nuestros chunks Marching Cubes
Veloren lod zones objects     -> nuestros proxies/instancias por zona planetaria
```

### Datos que necesitamos generar por cuerpo

Primera version:

```csharp
struct FarBodyLodData
{
    public Texture2D heightAtlas;   // 6 caras o atlas por patches
    public Texture2D colorAtlas;    // color/material macro
    public float radius;
    public float minHeight;
    public float maxHeight;
}
```

No hace falta que esto sea perfecto al principio. Puede generarse desde el mismo `ScalarFieldSettings`/proveedor de planeta:

```text
for each face pixel:
    direction = CubeSphereDirection(face, uv)
    height = SamplePlanetHeight(direction)
    color = SamplePlanetColor(direction, height)
```

Despues el renderer lejano usa esto en vez de una mesh MC completa.

### Como mezclar proxy y chunks reales

Igual que Veloren:

1. Renderizar chunks reales donde existan y sean visibles.
2. Renderizar proxy lejano del cuerpo.
3. Evitar z-fighting:
   - bajar un poco el proxy cerca de la zona de chunks reales;
   - o recortar un agujero circular/esferico alrededor del player;
   - o usar depth bias/material separado.

Para Unity, la opcion mas limpia al principio:

```text
si player esta cerca del cuerpo:
    FarBodyProxy sigue activo pero con agujero/radio de exclusion alrededor del player
    NearVoxelChunks cubren ese agujero
```

Si hacer agujero en shader tarda, se puede empezar con `proxyRadiusOffset = -0.5f` o similar y medir.

### Que hacemos con cuevas

Veloren no mete cuevas en el LOD lejano. Su LOD es superficie/altura macro. Para nosotros debe ser igual:

```text
FarBodyProxy: 0 cuevas
MidSurfaceChunks: 0 cuevas profundas
NearVoxelChunks: cuevas locales
InteriorMode: cuevas alrededor del player
```

Esto resuelve el problema de duplicar tris "por la cara": desde fuera del planeta, la cueva no existe para render salvo entrada visible/cercana.

### Decision

La solucion estilo Veloren para nuestro motor no es "optimizar el mesh combinado actual hasta que aguante todo". Es introducir un segundo renderer:

```text
Renderer lejano agregado por cuerpo celeste
+
Renderer voxel cercano por chunks Marching Cubes
```

El mesh combinado actual sigue vivo, pero deja de ser responsable de representar un planeta entero desde lejos.

### 1. Chunk logico separado de render

Veloren conserva estado por chunk, pero cada chunk tiene datos GPU, visibilidad, bounds, mapas de luz, instancias, estado de sombras y referencias de textura. El render itera chunks visibles, no "todo chunk existente".

Aplicacion aqui:

- Seguir guardando `VoxelChunkState` por chunk.
- Anadir `VoxelChunkRenderState` explicito:
  - `Bounds tightBounds`
  - `bool hasExteriorSurface`
  - `bool hasInteriorSurface`
  - `bool visible`
  - `int lastFrustumPlane`
  - `int meshVersion`
  - `SurfaceDomain domainMask`
- El mesh combinado debe construirse desde los chunks render-visibles, no desde todos los chunks activos.

### 2. Frustum culling coherente y con bounds ajustados

Veloren construye un frustum por frame/cambio de camara, testea AABBs de chunks y guarda el ultimo plano que fallo/paso para acelerar tests coherentes. Tambien usa bounds verticales derivados del mesh (`z_bounds`, `sun_occluder_z_bounds`) en vez de asumir un volumen completo.

Aplicacion aqui:

- No usar bounds de chunk completo si el mesh real ocupa una banda fina.
- Guardar `tightBounds` al terminar `BuildMesh`.
- Testear el `tightBounds`, no siempre `chunkOrigin + chunkSize`.
- Cachear `lastPlaneIndex` por chunk para que el culling sea coherente entre frames.
- Recalcular culling solo si:
  - la camara se mueve/rota por encima de umbral;
  - cambia el set de chunks;
  - cambia el mesh/bounds de un chunk;
  - cambia el cuerpo celeste activo.

El `CullingGroup` de Unity puede ser util, pero en nuestras pruebas ha sido caro. Una alternativa mas controlada es un culling propio con arrays lineales:

```text
camera frustum planes
visible bodies = test sphere/AABB por cuerpo celeste
for each visible body:
    traverse body chunk nodes
    test tightBounds against planes
    update chunk.visible
```

### 3. Culling jerarquico antes de culling de chunk

Veloren trabaja con un mundo plano, pero el patron escala: antes de decidir chunks, decide distancia de vista, zona cargada, estado visible y render pass.

Para planetas y lunas necesitamos una jerarquia:

```text
UniverseRender
  CelestialBodyRenderState
    bodyBounds / bodySphere
    exteriorChunkTree
    interiorChunkTree
    visibleChunkList
```

Pipeline recomendado:

1. Testear cada cuerpo celeste contra frustum.
2. Si el cuerpo no es visible, no tocar sus chunks para render.
3. Si es visible pero lejano, usar proxy/LOD exterior y no cuevas.
4. Si esta cerca, recorrer chunks visibles del exterior.
5. Si el player esta cerca de superficie, dentro del cuerpo, o mirando una entrada, activar dominio interior/cuevas.

Esto ataca directamente el caso de "planeta + 2 lunas": la decision cara no puede empezar en los 3 x N chunks. Tiene que empezar en 3 bounds de cuerpo.

### 4. Render por dominios: exterior, interior, fluidos/sprites

Veloren separa terreno opaco, fluidos, sprites, sombras, lluvia/occlusion e incluso bandas de altitud subterranea/superficie. La leccion no es copiar sus pases, sino separar lo que tiene reglas de visibilidad diferentes.

Para nosotros:

- `ExteriorSurface`: superficie planetaria visible desde lejos.
- `InteriorSurface`: cuevas/tuneles.
- `NearEditSurface`: zona de terraformacion/refinamiento.
- `ProxyFarBody`: mesh/LOD de cuerpo completo desde lejos.

Regla de diseno:

```text
Lejos del cuerpo:
    render ProxyFarBody o ExteriorSurface grueso
    no render InteriorSurface

Cerca de superficie:
    render ExteriorSurface segun LOD dinamico
    activar InteriorSurface solo en radio local o si hay entrada visible

Dentro del cuerpo/cueva:
    render InteriorSurface alrededor del jugador
    exterior puede pasar a proxy o desactivarse si no aporta imagen
```

Esto evita duplicar tris por cuevas que estan encerradas dentro del planeta.

### 5. Meshing solo con vecinos listos y borde extendido

Veloren no malla un chunk como isla ciega: cuando necesita el borde, espera a tener vecinos y muestrea una region con margen. Esto es clave para no crear caras falsas, AO roto o costuras.

Aplicacion aqui:

- Para Marching Cubes, todo chunk deberia construir su malla con una franja de muestras vecinas o con datos de borde declarados.
- Si el vecino no existe porque el cuerpo no declara ese chunk, cuenta como aire.
- Si el vecino existe pero no esta listo, mantener mesh viejo o encolar despues; no crear una frontera incorrecta.
- Vuestro `BoundaryRefinement` ya apunta a esto. Falta que el desired LOD del vecino sea real, no siempre `CoarsestCellSize`.

### 6. No recorrer volumenes homogeneos

Veloren usa una estructura tipo `Chonk` con:

- valor por defecto debajo;
- valor por defecto encima;
- subchunks solo donde hay cambios;
- defragmentacion de subchunks homogeneos;
- iteracion solo por voxeles cambiados cuando interesa.

Para nuestro planeta:

- Un chunk completamente vacio no debe tener mesh.
- Un chunk completamente solido y sin frontera/cueva tampoco deberia tener mesh.
- Un chunk con solo interior profundo no deberia entrar al exterior pass.
- Las cuevas deberian declararse como regiones con superficie interior, no como "todo el volumen del planeta".

Metadatos recomendados al evaluar chunk:

```csharp
struct ChunkSurfaceSummary
{
    public bool hasAnySurface;
    public bool hasExteriorSurface;
    public bool hasInteriorSurface;
    public bool isHomogeneousSolid;
    public bool isHomogeneousAir;
    public Bounds tightBounds;
    public int minSurfaceDepth;
    public int maxSurfaceDepth;
}
```

El objetivo es poder decir "este chunk existe para datos, pero no pinta nada ahora".

### 7. Greedy meshing como patron, no como tecnica directa

Veloren crea una mascara de caras candidatas con `should_draw`, recorre secciones por ejes y fusiona rectangulos compatibles. En Marching Cubes no podemos fusionar igual porque la superficie es triangulada e interpolada.

Pero si se puede copiar el patron:

- construir una mascara barata de celdas candidatas antes de emitir triangulos;
- saltar celdas con `corners == 0` y `corners == 255` sin frontera visible;
- separar "deteccion de superficie" de "emision de vertices";
- tener metadatos por superficie para decidir dominio/material/render pass;
- compactar rangos activos antes del job caro.

Para cuevas, la mascara importa mucho mas que el greedy. Si una cueva esta a 500m de la camara y encerrada, ni se evalua para render.

### 8. Presupuesto de jobs y swaps

Veloren limita trabajadores de mesh segun CPU, prioriza cercania al foco, descarta resultados obsoletos y limita cuantas respuestas sube a GPU por tiempo.

Vuestro manager ya tiene:

- `maxChunkBuildsStartedPerFrame`
- `maxConcurrentChunkBuilds`
- `maxChunkSwapsPerFrame`
- descarte si el desired state ya no coincide.

Recomendacion: mantenerlo y ampliar la prioridad:

```text
priority =
    bodyVisibleScore
    + screenSpaceError
    + distanceToPlayer
    + isNearEditFocus
    + wasVisibleLastFrame
    + LODUrgency
```

No todos los chunks declarados de un cuerpo visible deben competir igual.

## Diagnostico de nuestro estado actual

Lo bueno:

- Ya hay mesh combinado, asi que los batches estan bajos.
- Ya existe estado deseado vs estado actual.
- Ya existe cola incremental.
- Ya existe LOD dinamico intra-chunk por octree.
- Ya existe base de culling opcional con camara del player.

Lo peligroso:

- `RebuildDesiredChunkSet` asigna `config.CoarsestCellSize` a todos los chunks declarados antes de construir requests. Eso puede estar ocultando el LOD real por chunk en la capa de estado.
- `EnqueueVisibleDesiredChunks` encola todos los `desiredChunkStates`; el nombre dice visible, pero ahora no filtra por visibilidad.
- `GetBoundaryRefinement` compara contra `config.CoarsestCellSize`, no contra el LOD deseado real del vecino.
- El culling actual reconstruye esferas/bounds para todo `desiredChunkStates` y ademas usa `GeometryUtility.TestPlanesAABB` en ese rebuild. Si se llama demasiado, se come CPU.
- El mesh combinado se reconstruye completo cuando cambia visibilidad. Eso esta bien para pocos chunks, pero puede doler con muchos cambios de camara.

## Plan recomendado

### Fase 1: visibilidad barata sin cambiar el meshing

Objetivo: dejar de renderizar todo el planeta sin volver a 2000 batches.

1. Mantener un unico renderer combinado.
2. Anadir `tightBounds` al estado de chunk usando bounds real del mesh generado.
3. Implementar culling propio por frustum con cache por chunk:
   - arrays/listas lineales;
   - test AABB contra 6 planos;
   - `lastPlaneIndex`;
   - recalculo por umbral de camara.
4. Filtrar `UpdateCombinedMesh` por `chunk.visible`.
5. No encolar build de chunks invisibles salvo:
   - primera generacion cercana;
   - chunks necesarios para costuras;
   - chunks que ya eran visibles y necesitan reemplazo.

Resultado esperado: batches siguen bajos; CPU de culling baja porque no usa GameObjects por chunk ni reconstruye todo cada frame.

### Fase 2: cuerpo celeste como nivel superior de culling

Objetivo: planeta + lunas sin pagar todos los chunks de todos los cuerpos.

Crear:

```csharp
sealed class CelestialBodyRenderState
{
    public int bodyId;
    public float3 center;
    public float radius;
    public Bounds bounds;
    public bool visible;
    public bool nearPlayer;
    public List<int3> declaredChunks;
    public List<int3> visibleChunks;
}
```

Reglas:

- Si `body.visible == false`, no se combinan sus chunks.
- Si `body.visible == true` pero esta lejos, se usa LOD exterior grueso/proxy.
- Solo el cuerpo cercano al player puede activar cuevas salvo herramientas especiales.

### Fase 3: separar exterior e interior

Objetivo: no duplicar triangulos de cuevas encerradas.

Durante evaluacion de celdas/chunk, clasificar superficie:

```text
Exterior: conectada con fuera del planeta / cerca de radio exterior
Interior: cavidad/tunel no visible desde fuera
NearEdit: zona donde el player puede terraformar con detalle
```

Primera version pragmatica:

- No intentar conectividad perfecta global.
- Clasificar por distancia radial:
  - superficie cerca de `planetRadius + heightField` => exterior;
  - superficie bastante bajo la corteza => interior;
  - cuevas solo se renderizan si `distance(player, chunkBounds) < caveRenderDistance`.

Con esto ya se elimina la mayoria del coste de cuevas desde lejos.

### Fase 4: jerarquia de chunks por cuerpo

Objetivo: no testear miles de chunks planos.

Crear un arbol simple por cuerpo:

```text
BodyNode bounds
  child nodes
  chunk refs
```

Puede empezar como octree o como buckets por anillos. El culling hace:

```text
if body bounds outside frustum: skip body
else traverse nodes:
    if node bounds outside frustum: skip subtree
    if node screen size tiny: use coarse/proxy
    else test chunks
```

Esto es especialmente importante para lunas y planetas grandes.

## Reglas concretas de render propuestas

```text
Distancia muy lejana:
    cuerpo visible como proxy esferico / mesh exterior ultra grueso
    0 cuevas

Distancia media:
    exterior chunks visibles por frustum + LOD grueso
    0 cuevas

Cerca de superficie:
    exterior visible por frustum + LOD dinamico
    cuevas solo en radio local / entradas cercanas

Dentro de cueva:
    interior chunks alrededor del player
    exterior solo si hay linea de salida o esta cerca
```

## Que no haria ahora

- No volver a `MeshFilter + MeshRenderer` por chunk.
- No copiar el greedy meshing de cubos como solucion para Marching Cubes.
- No hacer occlusion culling complejo todavia.
- No meter GPU-driven indirect rendering antes de agotar culling jerarquico y dominios exterior/interior.
- No generar malla de cuevas lejanas "por si acaso".

## Siguiente paso mas rentable

Implementar `ChunkSurfaceSummary + tightBounds + culling propio coherente` y cambiar el combinado para incluir solo chunks visibles.

Despues, introducir `SurfaceDomain`:

```csharp
[Flags]
public enum SurfaceDomain : byte
{
    None = 0,
    Exterior = 1 << 0,
    Interior = 1 << 1,
    NearEdit = 1 << 2
}
```

Y que `UpdateCombinedMesh` reciba una mascara:

```text
visibleDomains = Exterior
visibleDomains = Exterior | InteriorLocal
visibleDomains = Interior
```

Ese cambio ataca el problema real: no es solo cuantos triangulos hay, sino que triangulos tienen derecho a existir en el frame actual.
