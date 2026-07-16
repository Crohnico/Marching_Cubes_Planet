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
03 Colisiones             -> permite locomocion e interaccion fisica real
04 Agua                   -> sistema volumetrico avanzado todavia abierto
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
Catalogo topologico derivado del campo final por chunk canonico. Cada chunk
registra sus componentes de aire, que caras conecta y con que vecinos directos
existe paso. La admision empieza por una esfera alrededor del jugador alcanzable
en linea recta y continua por coste de camino sobre el catalogo. La oclusion
jerarquica se aplica despues y solo a trabajo opcional.

Coste esperado:
Un bake incremental y presupuestado mientras el jugador esta en el planeta,
amortizado una sola vez mediante cache. En runtime, consultas baratas sobre
componentes y conexiones ya resueltas, con el mismo limite maximo de vertices.

Riesgo:
Catalogar como transitable una grieta sin espacio jugable, unir aperturas que no
coinciden entre chunks, consumir demasiado tiempo de GPU durante el bake, usar
datos obsoletos tras terraformado o activar la seleccion topologica antes de que
el entorno inmediato este catalogado.

Por que esta solucion y no otra:
La distancia euclidea no representa el coste de recorrer una cueva. El catalogo
convierte el campo procedural en informacion de movimiento reutilizable sin
guardar el volumen planetario ni regenerar Marching Cubes. Aumentar buffers solo
ocultaria la seleccion incorrecta y elevaria VRAM.

Que se medira para validarla:
Chunks catalogados por segundo, coste CPU/GPU por frame, bytes de cache, numero
de componentes y portales, falsos enlaces entre vecinos, progreso del anillo
local, coste de camino de los chunks admitidos, tiempo de reconstruccion,
overflow, VRAM maxima y distancia minima del jugador al borde cargado.
```

## Responsabilidades separadas

El sistema se divide conceptualmente en cuatro decisiones:

```text
Topologia       -> por donde se puede pasar dentro y entre chunks.
Representacion -> Shell o Base segun estado del jugador.
Admision        -> que chunks pueden ocupar presupuesto y en que orden.
Oclusion        -> que trabajo opcional puede evitarse por no ser visible.
```

No deben volver a resolverse mediante una unica lista ordenada solo por distancia.

## Catalogo topologico por chunk

El catalogo se construye despues de resolver todo lo que pueda modificar la
densidad: forma, operaciones de composicion con efecto volumetrico, cuevas y
modificaciones base aplicables. Colores y propiedades visuales que no cambian
solido/aire no invalidan esta topologia.

Cada chunk canonico es un grid 3D. El bake estudia sus seis caras y descubre los
componentes de aire conectados:

```text
1. Escoger una cara con aire sin catalogar.
2. Elegir una celda de aire de esa cara, priorizando las cercanas al centro.
3. Recorrer todo el aire alcanzable desde ella dentro del chunk.
4. Registrar que otras caras alcanza el mismo recorrido.
5. Repetir desde el aire fronterizo que siga sin pertenecer a un componente.
```

La implementacion puede usar A* para esas consultas. Como no existe un destino
unico y hay que descubrir el componente completo, un flood fill/BFS uniforme
produce el mismo resultado con menos expansiones. Esta es una decision de
implementacion; el contrato es la conectividad obtenida, no el algoritmo concreto.

Ejemplo:

```text
Componente 0 -> -X, +X
Componente 1 -> -Y, +Y, -Z, +Z
```

Ese chunk tiene dos regiones transitables independientes. Entrar por `-X` no
permite salir por `+Y` aunque ambas caras contengan aire.

Una cara puede contener varias aperturas desconectadas. Por ello el resultado no
se reduce a seis booleanos. Cada componente conserva una mascara de caras y la
mascara de celdas transitables que ocupa en cada cara. Dos chunks vecinos solo
crean una conexion si las aperturas de las caras opuestas se solapan:

```text
portal A en +X
AND portal B en -X
AND interseccion transitable de sus mascaras distinta de cero
```

La identidad de navegacion es `(chunk, componentId)`. La admision visual sigue
siendo por chunk: si cualquiera de sus componentes necesarios entra en la
seleccion, se genera el chunk una sola vez.

El catalogo minimo cacheado contiene:

```text
coordenada canonica del chunk
Solid / Air / Mixed / Unknown
componentes transitables locales
mascara de caras por componente
portales y mascaras de celdas de cara
conexiones confirmadas con los seis vecinos directos
version y hash de todo parametro que afecte a densidad o transitabilidad
```

`Surface / Entrance / Interior` no forman parte del criterio de admision. Una
buena consulta sobre el grafo ya expresa si existe camino desde el jugador y
cuanto cuesta alcanzarlo. Esas etiquetas solo se derivaran en el futuro si una
mecanica o una visualizacion necesita nombrar regiones; no se guardan como una
segunda autoridad de conectividad.

## Bake incremental y cache

El catalogo empieza a construirse bajo presupuesto desde que el jugador entra en
el planeta. No bloquea la aproximacion ni ocupa todo el ancho de CPU/GPU:

```text
1. Chunk/componente actual del jugador y anillo inmediato.
2. Esfera de seguridad y direccion prevista de movimiento.
3. Componentes conectados de menor coste alrededor de la zona catalogada.
4. Resto del planeta de forma incremental.
```

Los resultados terminados se guardan en cache para no repetir el bake en cargas
posteriores. La cache admite progreso parcial y se invalida por version o por un
hash de la receta topologica. `Unknown` significa pendiente, nunca bloqueado. El
sistema anterior conserva cobertura hasta que el entorno local necesario este
catalogado.

El terraformado invalida el chunk modificado, sus componentes y las conexiones
con sus seis vecinos. La cache base permanece inmutable y las modificaciones de
partida se aplican como un overlay local reconstruible.

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

### P0: esfera alcanzable en linea recta

Primero se construye una esfera alrededor de la posicion actual del jugador. No
entran automaticamente todos sus chunks: entran los que pueden alcanzarse
mediante un segmento recto transitable desde el componente actual.

Un DDA recorre los chunks cruzados por el segmento. En cada frontera consulta el
portal cacheado correspondiente y exige que entrada y salida pertenezcan al mismo
componente local. No es una prueba de frustum ni una linea de vision grafica; es
una prueba de movimiento recto sobre la topologia horneada.

```text
No se ocluye.
No se elimina por frustum.
Se genera primero.
Debe cubrir todo desplazamiento recto inmediato permitido dentro de su radio.
```

P0 es una garantia. Su radio debe elegirse de modo que el peor caso validado quepa
en el presupuesto. Si no cabe, se reduce radio o LOD de forma explicita; no se
recorta un subconjunto arbitrario de la esfera recta.

### P1: expansion por coste de camino

Despues de P0, el resto de componentes alcanzables se expande sobre las
conexiones cacheadas. Como se desea obtener un orden creciente y no llegar a un
destino unico, la consulta runtime usa una cola de prioridad tipo Dijkstra. A*
queda disponible para consultas con objetivo concreto.

El coste acumulado parte del componente actual. La primera heuristica es
deliberadamente sencilla, uniforme en los seis vecinos y sin diagonales:

```text
edgeCost = stepCost + (neighbor.isCompletelyAir ? airCost : mixedCost)
pathCost = parentPathCost + edgeCost
```

El valor inicial de `airCost` es `0.1`: atravesar vacio es muy barato, pero no
gratuito, de modo que el gradiente conserva informacion de distancia. No existe
penalizacion especial para Y, ascenso o descenso en esta fase. Los tres costes se
exponen en Inspector para observar el efecto antes de cerrar la heuristica. El
`stepCost` inicial es `0`, por lo que cruzar un chunk completamente vacio cuesta
exactamente `0.1`.

`Max Path Cost` es un limite duro adicional al numero de chunks. Dijkstra deja de
expandir cuando el menor nodo pendiente lo supera y no publica ningun candidato
con coste mayor. El mismo valor define el extremo rojo del gradiente de gizmos:

```text
coste 0             -> verde
coste intermedio    -> interpolacion verde/rojo
coste Max Path Cost -> rojo
coste superior      -> no admitido
```

```text
No se ocluye.
No se elimina por frustum.
Se genera despues de completar P0.
Se consume en orden creciente de pathCost hasta agotar presupuesto.
```

### Prototipo visual de coste

La primera implementacion es solo diagnostica y no modifica el buffer de Base.
En Editor o Development Build:

```text
se muestrea la densidad final con cuevas del chunk canonico 16^3
se descubren componentes de aire mediante seis vecinos
al catalogar vecinos se comparan sus caras una sola vez y se cachean los enlaces
Dijkstra recorre enlaces compactos; nunca vuelve a escanear las caras 16x16
se selecciona primero P0 recto y despues el menor pathCost
los chunks Air participan como transiciones pero no son candidatos de render
se detiene al alcanzar el numero de chunks Mixed o Max Path Cost
se colorean volumen y aristas verde -> rojo usando Max Path Cost
```

El origen se recalcula cuando el jugador cambia de chunk. El dibujo tambien se
actualiza al terminar nuevos chunks del catalogo o cambiar un coste del Inspector.
Cuando la seleccion alcanza el limite de chunks queda congelada hasta que cambie
el chunk del jugador o la configuracion. `Async Catalogue Enabled` permite pausar
el catalogado y mantiene como maximo una peticion GPU en vuelo. `Air Density
Threshold`, `Air Chunk Cost`, `Mixed Chunk Cost`, `Step Cost`, `Max Path Cost`, el
radio P0 y el limite de expansion son parametros de diagnostico, no decisiones
finales de gameplay.

La lectura de densidad GPU -> CPU es asincrona y no bloquea el frame esperando el
resultado. `Max Path Cost` fija admision y el coste que se representa como rojo;
no se renormaliza el gradiente cada vez que crece el catalogo. Mientras la seleccion no
esta llena, `Catalogue Batch / Rebuild` agrupa varios resultados antes de repetir
Dijkstra. Al alcanzar el limite, nuevos resultados del catalogo no alteran la
seleccion publicada hasta que el jugador cambia de chunk. Mientras se completa,
los chunks ya publicados conservan coste, color y orden; cada lote solo puede
anadir candidatos nuevos. El catalogo se conserva solo en memoria. Persistencia,
clearance del jugador y compresion quedan para la fase de bake/cache; no se
consideran resueltos por estos gizmos.

El Profiler separa `Planet.Topology.CompleteChunk` del coste de
`Planet.Topology.SearchRebuild`. Esto permite decidir con datos si el siguiente
cuello esta en el flood fill/catalogado o en la navegacion, sin recurrir a punteros
antes de conocer la ruta dominante.

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
resolver el componente actual del jugador
-> construir la esfera P0
-> admitir los chunks de P0 alcanzables en linea recta mediante DDA y portales
-> iniciar expansion Dijkstra sobre las conexiones cacheadas
-> ordenar el resto por coste acumulado de camino
-> aplicar P2..P4 solo como responsabilidades y desempates posteriores
-> deduplicar chunks alcanzados por varios componentes/fuentes
-> generar hasta agotar presupuesto
```

La fuente del candidato tambien se conserva como diagnostico:

```text
surface-confirmed
straight-sphere
path-cost
topology-unknown-fallback
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
Velocidad pequena          -> sin sesgo adicional dentro del mismo pathCost.
Velocidad alta             -> priorizar catalogo y desempates hacia el avance.
Cambio brusco de direccion -> P0 recto sigue protegiendo mientras cambia el sesgo.
```

La velocidad no crea conexiones ni sustituye el coste de camino. Solo ordena
trabajo equivalente y prioriza el bake todavia pendiente. La velocidad maxima
jugable y el radio P0 quedan `TBD` hasta medir la locomocion real en grid/segundo.

## Ciclo de reconstruccion

La Base deja de ser una cola indivisible.

```text
1. Capturar foco actual, velocidad y foco predicho.
2. Resolver componente actual y construir la esfera recta P0.
3. Expandir el resto por coste de camino sobre el catalogo disponible.
4. Aplicar responsabilidades P2..P4 y deduplicar chunks.
5. Mantener la Base anterior visible.
6. Cocinar P0 y el prefijo de menor coste en el slot trasero.
7. Publicar el prefijo seguro cuando este completo.
8. Continuar anexando el resto de forma presupuestada.
9. Finalizar al agotar candidatos o presupuesto.
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
progreso total y local del catalogo topologico
chunks catalogados por segundo
componentes y portales producidos
tiempo CPU/GPU consumido por el bake en el frame
bytes de cache escritos y residentes
chunks Unknown usados mediante fallback
chunks P0 aceptados/rechazados por la prueba recta
coste minimo/maximo de camino admitido
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

## Clasificacion topologica y coarse

El catalogo exacto por chunk produce:

```text
Air      -> volumen transitable sin superficie conocida.
Solid    -> no contiene volumen transitable.
Mixed    -> contiene solido y aire; publica componentes y portales.
Unknown  -> todavia no catalogado; se trata de forma conservadora.
```

No se puede declarar `Solid` o `Air` muestreando pocos puntos si una galeria
estrecha puede cruzar entre ellos. El bake usa el grid 3D canonico y la misma
densidad final que la geometria. Marching Cubes consume esa densidad, pero no es
necesario generar triangulos para catalogar conectividad.

La transitabilidad debe usar una regla de clearance medible. Aire matematico no
implica que quepa el jugador; grietas diagonales o pasos inferiores al perfil
minimo no crean portales de movimiento.

## Oclusion jerarquica

La oclusion se introduce despues de estabilizar el catalogo topologico, P0 recto
y la admision por coste de camino.

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
se recalculan las conexiones con los seis vecinos
Unknown se usa durante la reconstruccion
P0/P1 siguen sin depender de oclusion
```

El catalogo de receta es una estructura de conectividad estatica y compacta, no
un volumen global de cuevas ni la fuente de su forma. El overlay de terraformado
mantiene correctas solo las regiones modificadas.

## Relacion con LOD

La prioridad decide primero si el chunk existe. El LOD decide despues con que
resolucion se representa.

```text
P0 -> LOD necesario para contacto/lectura inmediata.
P1 -> LOD suficiente para la expansion admitida por coste de camino.
P2 -> LOD exterior que garantice silueta/horizonte.
P3/P4 -> degradable por tamano aparente y presupuesto.
```

Un LOD anterior valido se conserva si no hay presupuesto para refinar. Nunca se
elimina cobertura valida solo para intentar una version mas detallada que todavia
no esta lista.

## Fases de implementacion

### Fase A: contrato topologico y medicion

```text
Publicar contadores de vertices y overflow de Base.
Medir duracion de cola y movimiento durante carga.
Definir grid transitable, clearance y formato de componentes/portales.
Medir coste de catalogar un chunk sin generar Marching Cubes.
```

### Fase B: bake incremental y cache

```text
Descubrir componentes locales y mascaras de cara.
Enlazar solo portales coincidentes de los seis vecinos.
Presupuestar el trabajo desde el entorno local hacia el resto del planeta.
Guardar progreso y validar version/hash de cache.
```

### Fase C: admision por esfera recta y coste

```text
Resolver el componente actual del jugador.
Construir P0 mediante esfera + DDA recto sobre portales.
Expandir el resto mediante Dijkstra y costes uniformes configurables.
Rellenar el buffer hasta sus limites sin ampliar VRAM.
```

### Fase D: representacion, estados y memoria

```text
Cerrar Exterior/Transicion/Interior con hysteresis.
Repartir la misma envolvente de VRAM entre Shell y slots Base.
Publicar el prefijo seguro y reaccionar si el jugador abandona la guard band.
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

Topologia:
Cada apertura pertenece a un componente local. Dos vecinos solo se conectan si
sus portales opuestos se solapan. Una bolsa de aire enterrada solo resulta
alcanzable si el grafo contiene un camino real hasta ella.

Interior:
Todo chunk de la esfera P0 alcanzable en linea recta se publica antes del resto.
El jugador no alcanza el borde seguro a la velocidad maxima validada y no ve el
skybox por geometria pendiente o descartada.

Buffer:
Todo overflow queda registrado. P0/P1 se escriben antes que prioridades
opcionales. El limite de VRAM no aumenta.

Streaming:
Una cola obsoleta no bloquea indefinidamente una reconstruccion urgente. No hay
asignaciones GC por frame. Fuera de P0, el orden de admision es monotono respecto
al coste acumulado de camino salvo responsabilidades superiores documentadas.

Cache:
Una receta sin cambios reutiliza el catalogo. Un cambio de version/densidad lo
invalida. El terraformado invalida localmente el chunk afectado y sus seis aristas.

Oclusion:
Nunca descarta P0/P1 y nunca usa Mixed/Unknown como oclusor definitivo.
```

## Decisiones abiertas

```text
TBD: velocidad maxima jugable en GridCoordinates/segundo.
TBD: radio minimo de P0.
TBD: clearance minimo usado para declarar una celda transitable.
TBD: presupuesto CPU/GPU por frame para el bake topologico.
TBD: formato comprimido y version de la cache de componentes/portales.
TBD: valores finales de airCost, mixedCost y stepCost tras validar los gizmos.
TBD: valor final de Max Path Cost por estado de locomocion y presupuesto.
TBD: criterio exacto Exterior/Transicion/Interior.
TBD: presupuesto GPU por frame para la cola.
TBD: representacion compacta de Solid/Air/Mixed/Unknown.
TBD: si la oclusion final necesita paginas/indirect args por chunk o basta con
     admision conservadora.
```

Estas decisiones se cierran con mediciones, no aumentando buffers ni añadiendo
estados invisibles sin diagnostico.
