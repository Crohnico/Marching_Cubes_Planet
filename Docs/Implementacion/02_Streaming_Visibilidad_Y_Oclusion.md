# Streaming, visibilidad y oclusion

## Prioridad del sistema

Este documento ocupa la prioridad `02` porque el campo volumetrico definido en
`01_Grutas_Y_Cuevas.md` convierte el interior en espacio jugable y hace necesario
decidir que geometria entra en memoria, cuando se publica y que puede descartarse.

El orden de dependencias queda:

```text
00 Laminas de composicion -> define el campo compuesto
01 Grutas y cuevas        -> crea interior explorable
02 Streaming/visibilidad  -> mantiene visible la zona necesaria con VRAM fija
```

## Objetivo

Mantener una representacion continua y jugable del planeta con un presupuesto
fijo de RAM/VRAM, tanto desde el exterior como durante desplazamientos rapidos por
el interior.

El sistema debe impedir dos fallos criticos:

```text
Desde el exterior:
La Base volumetrica desplaza chunks de superficie y el planeta deja de cubrir el
horizonte.

Desde el interior:
El jugador alcanza el borde de la ventana cargada y ve el skybox a traves de
geometria que aun no existe o que no entro en el buffer.
```

El objetivo no es generar todo el planeta ni ocultar agujeros con niebla. Es
admitir, cocinar y publicar primero la geometria que protege la experiencia.

## Diagnostico del runtime actual

La Base parte de los chunks confirmados por la Shell y añade una esfera de
candidatos volumetricos alrededor del foco. Despues:

```text
mezcla superficie e interior
-> ordena solo por distancia al foco
-> recorta por baseOctreeMaxChunks
-> genera secuencialmente en un buffer agregado
```

Esto crea tres problemas diferentes.

### Competencia entre horizonte e interior

Los chunks subterraneos cercanos compiten con los chunks de superficie. Al estar
mas cerca del foco pueden expulsar del limite de chunks a la superficie necesaria
para cerrar el horizonte.

Cuando la Base termina, la Shell se libera. Una ventana local pasa entonces a ser
responsable tanto del detalle cercano como de representar el planeta exterior
completo, responsabilidad que no puede cumplir con su presupuesto actual.

### Ventana centrada en una posicion antigua

El foco se captura al iniciar una reconstruccion. La cola genera un chunk y cede
un frame. Mientras `isBaseLoading` esta activo no se programa otra reconstruccion.

Un jugador rapido puede recorrer varios chunks antes de que termine una cola de
hasta `baseOctreeMaxChunks`. La Base anterior permanece visible, pero protege una
posicion que el jugador ya abandono.

### Saturacion silenciosa del buffer de vertices

`baseOctreeMaxChunks` limita candidatos, pero el limite fisico final es
`baseOctreeOutputVertexCapacity`.

Los chunks tienen costes de vertices muy distintos. Se escriben en el orden de la
cola hasta alcanzar el limite global. El shader dispone de `overflowFlag`, pero la
ruta visual agregada no publica actualmente esa informacion como diagnostico de
cada reconstruccion.

Por tanto, un hueco visible puede significar:

```text
el chunk fue recortado antes de cocinar
el chunk sigue pendiente
el chunk fue cocinado para un foco obsoleto
el buffer se lleno antes de que llegase su turno
```

La instrumentacion debe distinguir estos casos antes de introducir oclusion.

## Parada de decision tecnica

```text
Objetivo real:
Garantizar horizonte exterior y volumen interior seguro mientras se prioriza el
resto de geometria dentro de un presupuesto fijo.

Restriccion dura:
Meta Quest 3, 90 FPS, VRAM sin ampliar, buffers GPU-resident, sin volumen global,
sin readback bloqueante en runtime caliente y sin GC accidental.

Solucion elegida:
Planificador de admision por prioridades, foco predictivo por velocidad, estados
Exterior/Transicion/Interior y publicacion temprana de un prefijo seguro. La
oclusion jerarquica se aplica despues y solo a trabajo opcional.

Coste esperado:
Mas clasificacion CPU barata por reconstruccion, el mismo limite maximo de
vertices y trabajo GPU desplazado desde candidatos inutiles hacia prioridades
jugables.

Riesgo:
Reinicios continuos de cola, doble representacion demasiado prolongada, popping,
datos de oclusion obsoletos tras terraformado o una zona segura insuficiente para
la velocidad real del jugador.

Por que esta solucion y no otra:
La oclusion no puede arreglar un chunk prioritario que nunca se genero. Aumentar
buffers esconderia el problema y elevaria VRAM. La admision ordenada garantiza
primero continuidad; la oclusion reduce despues trabajo opcional.

Que se medira para validarla:
Tiempo de reconstruccion, distancia recorrida durante la cola, vertices
intentados/escritos, overflow, candidatos por prioridad, tiempo hasta publicar la
zona segura, VRAM maxima y distancia minima del jugador al borde cargado.
```

## Responsabilidades separadas

El sistema se divide conceptualmente en tres decisiones:

```text
Representacion -> Shell o Base segun estado del jugador.
Admision        -> que chunks pueden ocupar presupuesto y en que orden.
Oclusion        -> que trabajo opcional puede evitarse por no ser visible.
```

No deben volver a resolverse mediante una unica lista ordenada solo por distancia.

## Estados de representacion

### Exterior

```text
Shell global visible.
Base local pequena para detalle cercano si hace falta.
La ventana de cuevas no sustituye la cobertura del horizonte.
```

La Shell es la autoridad de cobertura exterior. No evalua el interior completo.

### Transicion

```text
Shell visible como respaldo.
Base cocina el volumen seguro alrededor del jugador y de su trayectoria.
Cuando el prefijo seguro esta listo puede comenzar el cambio de autoridad.
```

La transicion debe tener hysteresis para no alternar al cruzar repetidamente una
entrada, costa o relieve irregular.

### Interior

```text
Base volumetrica como autoridad.
Shell liberada cuando ya no aporta cobertura util.
El presupuesto recuperado se usa dentro del mismo limite de VRAM.
```

La decision Exterior/Transicion/Interior no se basara solo en distancia al centro
del planeta. Debe combinar profundidad radial conservadora, estado anterior e
hysteresis. El criterio exacto queda `TBD` hasta medir entradas y montanas reales.

## Envolvente fija de memoria

No se aumenta la VRAM maxima respecto al pico ya permitido por el doble slot.

Distribucion conceptual:

```text
Exterior:
Shell + un slot Base local reducido.

Transicion:
Shell + slot de construccion del volumen seguro.

Interior estable:
Base visible + presupuesto libre para recarga.

Interior recargando:
Base anterior + Base nueva dentro del doble slot existente; Shell liberada.
```

El presupuesto es una envolvente compartida, no una suma de capacidades
independientes que puedan crecer simultaneamente.

## Prioridades de admision

Cada candidato recibe una prioridad funcional antes de ordenar por distancia.

### P0: volumen de seguridad

Esfera completa alrededor de la posicion actual del jugador.

```text
No se ocluye.
No se elimina por frustum.
Se genera primero.
Debe cubrir movimiento, giro y contacto inmediato en cualquier direccion.
```

### P1: lookahead de movimiento

Capsula entre la posicion actual y una posicion predicha:

```text
predictedFocus = currentFocus + filteredGridVelocity * preloadSeconds
```

La longitud se limita a un maximo de chunks para evitar que un pico de velocidad
consuma todo el planeta local.

```text
No se ocluye.
No se elimina por frustum.
Se genera despues de P0.
```

### P2: cobertura exterior y transicion

Responsabilidad de cerrar el planeta visible desde fuera y conservar continuidad
entre Shell y Base.

Mientras la Shell sea visible, P2 no duplica sin limite la superficie. Solo
mantiene el detalle local y las zonas necesarias para el cambio de autoridad.

### P3: camara y frustum

Chunks cercanos que pueden ser visibles desde la camara actual, priorizados por:

```text
interseccion con frustum
tamano aparente
distancia
direccion de mirada
```

P3 puede usar oclusion conservadora cuando exista clasificacion fiable.

### P4: expansion opcional

Chunks laterales, traseros, lejanos o actualmente ocluidos. Son los primeros que
se descartan al agotar chunks, vertices, tiempo de GPU o ventana de carga.

## Orden de seleccion

La seleccion no usa porcentajes fijos de chunks por prioridad. El coste real son
vertices y tiempo, no cantidad de coordenadas.

Orden obligatorio:

```text
deduplicar candidatos globales
-> clasificar P0..P4
-> ordenar por prioridad
-> ordenar dentro de cada prioridad por distancia/avance
-> generar hasta agotar presupuesto
```

La fuente del candidato tambien se conserva como diagnostico:

```text
surface-confirmed
safety-volume
movement-lookahead
frustum
optional-expansion
```

Un chunk presente en varias fuentes adopta la prioridad mas alta.

## Foco y velocidad

`PlanetDirector` recibe actualmente un `Transform`, no puede exigir un
`Rigidbody`. La velocidad se calcula sin asignaciones desde posiciones de grid:

```text
rawVelocity = (currentGridPosition - previousGridPosition) / deltaTime
filteredVelocity = smooth(rawVelocity)
```

Reglas:

```text
Teleport o salto imposible -> reinicio de historial y reconstruccion urgente.
Velocidad pequena          -> lookahead minimo o nulo.
Velocidad alta             -> capsula mas larga, dentro del limite configurado.
Cambio brusco de direccion -> P0 sigue protegiendo mientras cambia P1.
```

`preloadSeconds`, velocidad maxima jugable y radio P0 quedan `TBD` hasta medir la
locomocion real en grid/segundo.

## Ciclo de reconstruccion

La Base deja de ser una cola indivisible.

```text
1. Capturar foco actual, velocidad y foco predicho.
2. Construir y ordenar candidatos P0..P4.
3. Mantener la Base anterior visible.
4. Cocinar P0 y P1 en el slot trasero.
5. Publicar el prefijo seguro cuando este completo.
6. Continuar anexando P2, P3 y P4 de forma presupuestada.
7. Finalizar al agotar candidatos o presupuesto.
```

La publicacion temprana reutiliza el doble slot existente. No crea un tercer
buffer. Tras publicar P0/P1, el mismo slot puede seguir recibiendo geometria antes
del render de frames posteriores.

La geometria opcional puede aparecer progresivamente. La zona inmediata no.

## Movimiento durante una carga

`isBaseLoading` no puede bloquear cualquier reconsideracion del foco.

Mientras existe una cola se evalua una guard band:

```text
Si jugador permanece dentro de P0/P1 objetivo -> continuar cola.
Si se acerca al borde seguro                 -> elevar trabajo pendiente cercano.
Si sale del volumen objetivo                 -> cancelar cola opcional y crear
                                                un nuevo prefijo seguro.
```

No se reinicia por cada pequeno desplazamiento. La cancelacion urgente depende de
la cobertura restante, no solo de superar `baseRebuildDistanceChunks`.

## Instrumentacion obligatoria

Antes de cambiar oclusion se exponen por reconstruccion:

```text
focus inicial y predicho
velocidad filtrada
candidatos por P0..P4
chunks intentados, con geometria y vacios
vertices intentados y escritos
overflowFlag
primera prioridad afectada por overflow
frames y milisegundos hasta P0/P1
frames y milisegundos hasta completar
distancia recorrida durante la carga
distancia minima al borde seguro
bytes de Shell y slots Base residentes
```

El readback de contadores debe ser asincrono o ejecutarse como diagnostico fuera
del camino caliente. La colision y la continuidad visual no pueden esperar una
respuesta GPU bloqueante.

## Clasificacion coarse

La clasificacion futura usa macro bloques:

```text
Air   -> no contiene superficie conocida.
Solid -> bloque completamente oclusor.
Mixed -> contiene o puede contener frontera.
Unknown -> no evaluado; se trata de forma conservadora.
```

No se puede declarar `Solid` o `Air` muestreando pocos puntos si una galeria
estrecha puede cruzar entre ellos. Un clasificador aproximado puede aumentar
prioridad, pero no excluir hasta disponer de una cota conservadora o una
clasificacion exacta reutilizable.

La primera implementacion puede usar resultados ya producidos por Marching Cubes
para futuras reconstrucciones. No se duplica inmediatamente todo el coste de
density(point) con un pase exacto previo sin medirlo.

## Oclusion jerarquica

La oclusion se introduce despues de estabilizar admision y lookahead.

Capas previstas:

```text
Frustum culling.
Culling por horizonte/hemisferio en exterior.
Clasificacion macro Solid/Air/Mixed/Unknown.
DDA 3D conservador desde camara para P3/P4.
Oclusion GPU/Hi-Z solo si el profiler justifica su complejidad.
```

Reglas conservadoras:

```text
P0 y P1 nunca se ocluyen.
Unknown no ocluye.
Mixed no ocluye por si solo.
Solo Solid confirmado puede bloquear candidatos posteriores.
```

La Base actual se dibuja como un agregado indirecto. Puede evitarse que un chunk
oculto entre en el agregado, pero no retirarlo individualmente despues sin
introducir segmentacion por chunk, paginas o una compactacion GPU. Esa evolucion
queda `TBD` y solo se autoriza si reduce coste medido sin ampliar VRAM.

## Terraformado e invalidacion

El terraformado puede convertir:

```text
Solid -> Mixed/Air
Mixed -> Solid/Air
Air   -> Mixed/Solid
```

Una clasificacion de oclusion obsoleta puede ocultar un camino abierto por el
jugador. Por ello:

```text
los patches invalidan clasificacion local
Unknown se usa durante la reconstruccion
P0/P1 siguen sin depender de oclusion
```

No se precalcula una estructura de portales estatica para todo el planeta.

## Relacion con LOD

La prioridad decide primero si el chunk existe. El LOD decide despues con que
resolucion se representa.

```text
P0 -> LOD necesario para contacto/lectura inmediata.
P1 -> LOD suficiente para movimiento predicho.
P2 -> LOD exterior que garantice silueta/horizonte.
P3/P4 -> degradable por tamano aparente y presupuesto.
```

Un LOD anterior valido se conserva si no hay presupuesto para refinar. Nunca se
elimina cobertura valida solo para intentar una version mas detallada que todavia
no esta lista.

## Fases de implementacion

### Fase A: medir

```text
Publicar contadores de vertices y overflow de Base.
Medir duracion de cola y movimiento durante carga.
Etiquetar fuente/prioridad de candidatos.
```

### Fase B: admision y horizonte

```text
Separar superficie y volumen.
Introducir P0..P4.
Mantener Shell como autoridad exterior.
Ordenar escritura por prioridad funcional.
```

### Fase C: streaming predictivo

```text
Calcular velocidad filtrada.
Construir P1 como capsula de lookahead.
Publicar P0/P1 antes de completar la cola.
Reaccionar si el jugador abandona la guard band.
```

### Fase D: estados y memoria

```text
Cerrar Exterior/Transicion/Interior con hysteresis.
Repartir la misma envolvente de VRAM entre Shell y slots Base.
Medir transiciones en superficie, entrada y salida de cuevas.
```

### Fase E: oclusion

```text
Clasificacion coarse conservadora.
DDA jerarquico para P3/P4.
Evaluar segmentacion/indirect args por paginas solo con datos de profiler.
```

## Criterios de aceptacion

```text
Exterior:
La superficie visible cubre el horizonte mientras el jugador permanezca en el
estado Exterior o Transicion.

Interior:
El jugador no alcanza el borde de P0/P1 a la velocidad maxima validada y no ve el
skybox por geometria pendiente o descartada.

Buffer:
Todo overflow queda registrado. P0/P1 se escriben antes que prioridades
opcionales. El limite de VRAM no aumenta.

Streaming:
Una cola obsoleta no bloquea indefinidamente una reconstruccion urgente. No hay
asignaciones GC por frame.

Oclusion:
Nunca descarta P0/P1 y nunca usa Mixed/Unknown como oclusor definitivo.
```

## Decisiones abiertas

```text
TBD: velocidad maxima jugable en GridCoordinates/segundo.
TBD: radio minimo de P0.
TBD: preloadSeconds y maximo de la capsula P1.
TBD: criterio exacto Exterior/Transicion/Interior.
TBD: presupuesto GPU por frame para la cola.
TBD: representacion compacta de Solid/Air/Mixed/Unknown.
TBD: si la oclusion final necesita paginas/indirect args por chunk o basta con
     admision conservadora.
```

Estas decisiones se cierran con mediciones, no aumentando buffers ni añadiendo
estados invisibles sin diagnostico.
