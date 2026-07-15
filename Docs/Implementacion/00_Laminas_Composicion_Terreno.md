# Laminas de composicion de terreno

## Objetivo

Definir como se compone el terreno real del planeta a partir de varias capas
procedurales ordenadas.

El sistema actual de render es funcional y debe conservarse:

```text
lista de chunks renderizables -> buffer GPU -> draw
```

Las laminas no son una ruta de render paralela. Son una forma de calcular que
hay dentro de cada punto/chunk antes de generar la geometria visible.

## Decision vigente

Fuera del planeta se mantiene la `Shell` actual.

Dentro del planeta, el terreno se entiende como una composicion de laminas:

```text
Lamina 0: forma base del planeta
Lamina 1: cavidades / grutas / cuevas
Lamina 2+: minerales, sustancias, modificaciones futuras
```

Cada lamina puede sobreescribir lo que habia debajo.

La composicion de todas las laminas produce el campo final:

```text
planetDensity(point)
planetMaterial(point)
```

Ese campo compuesto es lo que se usa para decidir si hay superficie, material,
colision y chunks renderizables.

Decision cerrada:

```text
El material de una lamina es sustancia logica del volumen.
No es un color calculado despues de Marching Cubes.
```

La autoria inicial de un planeta debe ser sencilla:

```text
Lamina 0 -> sustancia principal de todo el solido.
Lamina 1 -> sustituye sustancia dentro de un rango de altura.
Lamina N -> vuelve a sustituir sustancia dentro de su rango y mascara.
```

La autoria visible de cada material queda limitada a:

```text
name / enabled
operation
material
atlasColor
minAppearance / maxAppearance: 0..255
abundance: 0..100 %
coherence: 0..100 %
underwaterBehaviour / underwaterAtlasColor / underwaterProbability / underwaterCoherence
surfaceBehaviour / surfaceAtlasColor / surfaceProbability / surfaceCoherence
surfaceMinAppearance / surfaceMaxAppearance: 0..255
```

No se exponen escalas de masa, falloff, strength, bias ni seed offset. La escala
espacial se deriva de `coherence` y del radio de la receta. La seed de cada capa
se deriva de la seed del planeta y de su indice estable.

Ejemplos de sustancia principal:

```text
tierra
roca
hierro
silicio
hielo
```

El color, atlas y parametros de shader son la presentacion visual de una
sustancia. Cambiar la presentacion no cambia las cells ni invalida geometria.

## Metafora operativa

Las laminas son como papeles invisibles colocados unos sobre otros.

Cada papel contiene una informacion concreta:

```text
forma base       -> solido / aire de la forma principal
cavidades        -> quita solido y crea aire interior
mineral de hierro -> sustituye material donde exista masa de hierro
```

Al poner todos los papeles juntos se obtiene el resultado final del planeta.

## Regla de composicion

La evaluacion debe ocurrir en orden fijo:

```text
estado = vacio
estado = aplicar Lamina Superficie sobre estado
estado = aplicar Lamina Cavidades sobre estado
estado = aplicar Lamina Mineral X sobre estado
estado = aplicar Lamina Modificador Y sobre estado
```

Cada lamina recibe:

```text
posicion local/grid del planeta
estado acumulado hasta ahora
receta del planeta
seed
```

Y devuelve:

```text
estado acumulado modificado
```

## Estado compuesto minimo

El resultado minimo por punto debe representar:

```text
density  -> campo escalar para Marching Cubes
material -> sustancia/material dominante
flags    -> informacion barata de clasificacion
```

Reglas iniciales:

```text
density > 0  -> solido
density <= 0 -> aire
```

El material solo tiene sentido si el punto sigue siendo solido despues de aplicar
las laminas anteriores.

Ejemplo:

```text
Superficie crea roca
Cavidad convierte esa zona en aire
Mineral no debe pintar hierro dentro del aire salvo que sea una regla explicita
```

## Lamina Superficie

La Lamina Superficie equivale a la forma actual del planeta.

Responsabilidad:

```text
Crear el cascaron/volumen solido base.
Definir continentes, oceanos, montanas, ruido fino y biomas de superficie.
```

Esta lamina produce la forma que actualmente calcula:

```text
PlanetShapeDensity.hlsl
```

Conceptualmente:

```text
density = radius + surfaceOffset(point) - distance(point, center)
material = tierra basica
```

Decision inicial:

```text
Toda geometria creada por Lamina 0 tiene material asignado por capa 0.
La capa 0 es obligatoria y cubre todo solido de la forma base.
La capa 0 puede ser tierra, roca, oro, hielo u otro material base de receta.
Ahora se usa tierra basica rosa como placeholder.
El material final ya no se asigna por altura de forma implicita.
La altura sigue afectando a la forma, no al material.
```

Las laminas de material viven en `PlanetRecipe`.

Estructura funcional:

```text
enabled
operation
material
atlasColor
minAppearance / maxAppearance
abundance
coherence
underwaterBehaviour / underwaterAtlasColor / underwaterProbability / underwaterCoherence
surfaceBehaviour / surfaceAtlasColor / surfaceProbability / surfaceCoherence
surfaceMinAppearance / surfaceMaxAppearance
```

Operaciones de material:

```text
BaseMaterial    -> lamina 0 obligatoria, material fallback de todo solido.
ReplaceMaterial -> sustituye sustancia sobre lo anterior usando rango y mascara.
```

`minAppearance` y `maxAppearance` usan radio normalizado a byte:

```text
0 = centro del planeta
255 = radio exterior maximo que puede generar la receta, incluido su margen de altura
```

El rango no crea solido en atmosfera. Solo clasifica cells que el campo de
densidad ya ha considerado solidas.

`abundance` es la probabilidad espacial de que una capa candidata reemplace el
material acumulado. No se llama `density` porque ese termino queda reservado al
campo SDF solido/aire.

`coherence` controla agrupacion sin exponer metros:

```text
0   -> decisiones practicamente por cell
100 -> grandes masas conectadas
```

El dominio procedural evita alinearse con Marching Cubes:

```text
material volumetrico -> posicion 3D rotada respecto al grid cartesiano
behaviour exterior   -> direccion esferica proyectada al radio exterior
```

Una deformacion determinista barata rompe contornos demasiado regulares. Los
behaviours no muestrean profundidad radial, evitando bandas y flechas al cortar
ruido cartesiano con pendientes o con la esfera del planeta.

Las capas se evaluan en orden y una coincidencia posterior reemplaza la anterior.
La capa cero siempre es el fallback y se normaliza a rango completo y abundancia
total.

Las laminas 1+ no crean mesh propia y no crean solido nuevo.

Las laminas de material se evaluan antes de escribir los triangulos de Marching
Cubes. El triangulo conserva la sustancia dominante calculada para la cell.

No se materializa un buffer de sustancias para el planeta completo. La receta
define `planetMaterial(point)` y el dato solo se calcula para chunks visibles,
interactuables o consultados.

`Air` no es una sustancia de pintado. Una caverna cambia `density` y pertenece a
la composicion de cavidades, fuera de la lista de materiales.

## Estados visuales por exposicion

La sustancia base no cambia cuando reacciona visualmente al entorno. Tierra con
cesped sigue siendo tierra; cobre oxidado sigue siendo cobre. El vertice conserva
la sustancia, la capa que la eligio y un estado visual compacto.

Estados iniciales:

```text
Base       -> atlasColor
Underwater -> underwaterAtlasColor si el behaviour y su probabilidad aplican
Surface    -> surfaceAtlasColor si el behaviour y su probabilidad aplican
```

`Surface` aplica ademas su propio rango radial `0..255`. Esto permite detener
cesped, oxidacion u otras reacciones antes de las cotas extremas sin modificar el
rango donde existe la sustancia base. Fuera del rango se conserva `atlasColor`.

Clasificacion:

```text
Surface    -> superficie exterior expuesta y a nivel o por encima del oceano
Underwater -> superficie exterior expuesta por debajo de la esfera del oceano
Cueva      -> no es Surface ni Underwater por el mero hecho de limitar con aire
```

La exposicion exterior se contrasta con la superficie base procedural. Asi una
futura resta de densidad de cueva no convierte automaticamente sus paredes en
suelo con cesped. La probabilidad de cada behaviour es independiente de
`abundance` y cada behaviour tiene su propia coherencia para controlar el tamano
de sus manchas sin depender de la distribucion del material base.

Ejemplos conceptuales:

```text
Lamina 0: roca como material base.
Lamina 1: tierra en un rango radial, con abundancia y masas coherentes;
          su estado Surface usa color de cesped.
Lamina 2: cobre en profundidad; su estado Surface usa color oxidado.
Lamina 3: arena en cotas altas/bajas definidas por receta;
          su estado Underwater usa color mojado.
```

## Lamina Cavidades

La Lamina Cavidades no es un render especial.

Responsabilidad:

```text
Tomar solido existente.
Convertir partes de ese solido en aire.
Crear grutas, cuevas y posibles entradas naturales.
```

La topologia, autoria y evaluacion vigente de cavidades se definen exclusivamente
en:

```text
Docs/Implementacion/01_Grutas_Y_Cuevas.md
```

Actualmente se usa un campo continuo de Simplex 3D deformado. Este documento no
duplica sus reglas: `00` define el orden de composicion y `01` define como la
operacion de cavidades modifica `density(point)`.

La lamina solo debe restar densidad si el estado acumulado ya era solido.

```text
si estado anterior es aire -> no hace nada
si estado anterior es solido y region hueca afecta -> puede convertir a aire
```

Las entradas, el acceso a cualquier profundidad y la ausencia de un nucleo
geometrico artificial pertenecen al contrato de `01`.

## Laminas de minerales

Las laminas de minerales sustituyen material, no necesariamente geometria.

Ejemplo:

```text
MineralHierro:
  porcentaje = 0.02
  genera masas deterministas
  si el punto sigue siendo solido:
      material = hierro
```

Regla importante:

```text
Una lamina superior puede sustituir lo que haya debajo.
```

Por tanto, una masa de hierro puede aparecer sobre roca base o sobre otra
sustancia anterior si su prioridad es mayor.

Si una lamina quiere rellenar aire o tapar una cueva, eso debe ser una regla
explicita de esa lamina, no comportamiento implicito.

## Relacion con Shell

La Shell actual se mantiene por ahora como representacion exterior.

Pero hay una diferencia conceptual importante:

```text
Shell de forma base != Shell compuesta final
```

La suma de laminas puede cambiar el cascaron visible:

```text
Superficie + Cavidades -> superficie con mordidas/entradas
Superficie + Minerales -> mismo volumen, otro material visible si aflora
```

A futuro debe existir un proceso costoso que:

```text
1. Evalua laminas relevantes.
2. Compone el campo final.
3. Genera una Shell compuesta.
4. Guarda en disco el resultado precalculado.
```

La Shell cargada en runtime debe poder venir de cache para no repetir el coste.

## Cache en disco

La composicion global puede ser costosa.

Por tanto, el resultado pesado debe cachearse por:

```text
planetSeed
recipeHash
version del algoritmo de laminas
resolucion / LOD de cache
```

Decision practica inicial:

```text
El sistema estelar controla la vida de la cache de Shell.

StellarSystemSeedId cambia -> se borra la cache de shells antes de cargar.
PlanetSeed = StellarSystemSeedId + indicePlaneta.
```

Regla de arranque:

```text
PlanetDirector no debe cargar Shell por si mismo en runtime normal.
El arranque de Shell lo ordena StellarSystemDirector.
```

Solo puede activarse carga standalone de un planeta de forma explicita para
pruebas controladas.

Formato inicial de Shell:

```text
buffer GPU de vertices completo 1:1 con la capacidad reservada
drawArgs indirectos
LOD de Shell
hash de receta
version del formato
```

Se guarda la capacidad completa del buffer, no solo los vertices usados.

Motivo:

```text
La recarga debe alimentar a la GPU de forma directa y barata,
sin recomponer geometria ni reconstruir listas intermedias.
```

Coste aceptado:

```text
mas disco que un formato compacto
readback sincronico al crear cache por primera vez
```

La carga desde cache debe hidratar el mismo slot GPU de Shell que usa el render
actual. No debe crear una ruta de render paralela.

## Relacion con PlanetGrid

`PlanetGrid` sigue siendo un mapa:

```text
PlanetGridCoordinates -> uint
```

Pero el significado de `hasInfo` pasa a depender del campo compuesto:

```text
hasInfo = 1 si el chunk tiene al menos 1 triangulo tras componer laminas
hasInfo = 0 si el chunk queda vacio tras componer laminas
```

El grid no debe decidir por indice.

La lista ordenada de chunks renderizables es una vista derivada del grid y del
presupuesto actual, no la fuente de verdad.

## Flujo compartido de Shell y LOD

La Shell no debe clasificar el planeta por una ruta distinta a Base/LOD.

Cada chunk conserva una identidad canonica independiente de su resolucion:

```text
recipeHash + chunkCoordinates + LOD + compositionVersion
```

La generacion de Shell LOD2 produce tambien la ocupacion por chunk. Ese resultado
alimenta `PlanetGrid` y evita un barrido anterior con un readback GPU->CPU por
chunk. Los LOD mas finos reutilizan identidad, clasificacion y cache; no intentan
reutilizar vertices porque la topologia de Marching Cubes cambia con el LOD.

Regla de publicacion:

```text
Mantener el LOD anterior visible.
Generar solo los chunks que requieren refinamiento.
Publicar el reemplazo cuando este completo.
Liberar despues el contenido sustituido.
```

Los planetas lejanos no fuerzan una Shell completa durante la entrada al sistema
estelar. Usan impostor/proxy y la generacion se agenda por prioridad y presupuesto.

## Presupuesto de VRAM

El sistema no aumenta buffers automaticamente para incorporar sustancias.

Decision inicial:

```text
PlanetMarchingCubesVertex mantiene stride de 32 bytes.
substanceId reutiliza/compacta un canal diagnostico existente.
No existe buffer volumetrico global de materiales.
Cada celda de superficie evalua una vez su material logico en el centro y todos
sus triangulos heredan ese identificador. Las celdas interiores o vacias no
materializan ni conservan material en memoria.
La Shell hace pase de conteo y reserva solo los vertices que necesita hasta el presupuesto maximo.
La cache de Shell guarda vertices usados, no capacidad vacia.
```

La evolucion a streaming por paginas debe usar un presupuesto fijo compartido
entre Shell y chunks locales. Si no hay espacio para un refinamiento, se conserva
el LOD anterior y se expulsa primero trabajo invisible o de menor prioridad.

Cada carga de Shell informa como minimo:

```text
chunks con informacion
vertices usados
capacidad real reservada
bytes de vertex buffer
```

## Relacion con el render actual

El render no debe saber cuantas laminas existen.

El render recibe:

```text
chunks a generar
LOD solicitado
receta / estado compuesto necesario
buffer destino
```

La composicion ocurre antes o durante la evaluacion de densidad, pero el contrato
visible sigue siendo:

```text
chunk -> Marching Cubes -> buffer GPU
```

Esto evita crear caminos especiales para cuevas, minerales o futuras sustancias.

## Prioridad de chunks dentro del planeta

Dentro del planeta no se debe intentar cargar todo el interior.

La seleccion de chunks renderizables debe seguir presupuesto:

```text
1. chunks visibles/externos cercanos
2. chunks alrededor del jugador
3. chunks en direccion de movimiento/camara
4. chunks interiores solo si el jugador entra o se acerca a ellos
```

Las cavidades no autorizan por si solas a llenar el buffer con todo el interior.

## Pendiente

Queda por definir:

```text
Como se integran patches de terraformado con prioridad superior.
Politica exacta del pool/paginas GPU y dibujo indirecto por segmentos.
```

## Primer paso de implementacion recomendado

Antes de ampliar operaciones de laminas:

```text
Eliminar la clasificacion con readback sincronico por chunk.
Hacer que Shell produzca ocupacion reutilizable por PlanetGrid/LOD.

Crear un evaluador de composicion:
  EvaluateComposedDensity(point)
  EvaluateComposedMaterial(point)

Propagar substanceId en el vertice actual sin aumentar su stride.

Mantener el render actual consumiendo el resultado igual que ahora.
```

No se debe introducir una cola/render/buffer especial para cuevas.
