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

Primera estructura funcional:

```text
enabled
operation
material
atlasColor
heightMin01 / heightMax01
falloffMin01 / falloffMax01
coverage01
massScaleMinMeters / massScaleMaxMeters
massCoherence01
strength01
altitudeBias
seedOffset
```

Operaciones iniciales:

```text
BaseSurface     -> lamina 0 obligatoria, material base de todo solido.
PaintMaterial   -> tinta/mezcla sobre lo anterior, para cesped y arena.
OverlayMaterial -> sobrescribe visualmente sobre lo anterior, para masas tipo roca.
SubtractDensity -> resta densidad, reservado para cavidades/cuevas.
```

`heightMin01` y `heightMax01` usan radio normalizado:

```text
0 = centro del planeta
1 = radio de grid de la receta
```

Las laminas 1+ no crean mesh propia y no crean solido nuevo.

En esta fase inicial solo tintan el material visual final sobre la superficie
generada por Marching Cubes.

Ejemplos iniciales en escena:

```text
Lamina 0: tierra basica rosa, cobertura total.
Lamina 1: cesped verde, manto general de superficie.
Lamina 2: arena, orillas y zonas bajo/cerca del agua, masas grandes conectadas.
Lamina 3: roca gris, aparece por masas, aumenta con altura y usa OverlayMaterial.
```

## Lamina Cavidades

La Lamina Cavidades no es un render especial.

Responsabilidad:

```text
Tomar solido existente.
Convertir partes de ese solido en aire.
Crear grutas, cuevas y posibles entradas naturales.
```

Primera version funcional:

```text
Voronoi 3D determinista.
8 regiones por chunk canonico.
Porcentaje de cavidades por receta.
Radio + falloff smoothstep.
Nucleo solido intocable.
```

La lamina solo debe restar densidad si el estado acumulado ya era solido.

```text
si estado anterior es aire -> no hace nada
si estado anterior es solido y region hueca afecta -> puede convertir a aire
```

Las entradas no se fuerzan. Si una cavidad intersecta superficie, aparece una
entrada. Si no intersecta, no aparece.

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
Formato exacto de cache en disco.
Hash exacto de receta + version de laminas.
API C# de Lamina.
Representacion GPU de material compuesto.
Como se invalida cache si cambia una lamina.
Como se integran patches de terraformado con prioridad superior.
```

## Primer paso de implementacion recomendado

Antes de tocar render:

```text
Crear un evaluador conceptual de composicion:
  LaminaSuperficie
  LaminaCavidades

Hacer que PlanetShapeDensity tenga una funcion clara:
  EvaluateComposedPlanet(point)

Mantener el render actual consumiendo el resultado igual que ahora.
```

No se debe introducir una cola/render/buffer especial para cuevas.
