# Definicion tecnica del proyecto

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

Este documento define como vamos a programar el "motor" de nuestro juego: el alcance tecnico, el acercamiento a cada tarea y las decisiones que van a guiar la arquitectura.

Aunque el proyecto tambien saldra para PC, su target principal son las Meta Quest 3. Esa restriccion marca desde el principio el nivel de complejidad de la arquitectura: necesitamos construir sistemas capaces de generar, mantener y renderizar mundos ambiciosos sin olvidar que el hardware objetivo principal es VR standalone.

Como el target principal es Quest 3, vamos a mantener una obsesion por el rendimiento y la optimizacion desde el segundo uno.

La RAM y la VRAM deben controlarse y descargarse de forma deliberada. No podemos permitir que los sistemas acumulen memoria sin una politica clara de carga, descarga y reutilizacion. El Garbage Collector debe evitarse como coste accidental: las asignaciones runtime deben estar justificadas, localizadas y, siempre que sea posible, sustituidas por reutilizacion de buffers, pools o datos persistentes controlados.

Tambien vamos a apoyarnos en disco, CPU y GPU de forma simultanea. Ningun subsistema debe convertirse en el unico cuello de botella. El trabajo debe repartirse para mantener una experiencia estable a 90 FPS, especialmente en VR, donde una caida de rendimiento no es solo un problema visual sino tambien de comodidad.

Lo que definimos aqui es la base del juego. Encima de esta base todavia tiene que vivir todo el juego real: gameplay, interaccion, UI, contenido, progresion, sonido y cualquier sistema futuro. Por eso cada apartado debe tratarse con una mentalidad estricta.

La comunicacion entre sistemas debe ser limpia. El codigo debe ser corto, directo y medible. Cada linea extra tiene un coste: mas lectura, mas mantenimiento y, en runtime, potencialmente mas ciclos ocupados. No se trata de escribir codigo críptico, sino de evitar capas, estados y pasos que no aporten valor claro.

## Primeros pasos y definicion

En este juego vamos a crear un universo completo para Quest 3.

Para evitar la sobrecarga, no vamos a cargar el universo entero a la vez. La unidad grande de carga sera el sistema estelar. Cargaremos sistema estelar a sistema estelar, y aun asi cada sistema estelar ya debe tratarse como una carga grande y compleja.

Cada sistema estelar tendra al menos un sol o cuerpo central equivalente. Los planetas del sistema orbitan alrededor de ese sol, y los planetas podran tener lunas orbitando alrededor de ellos.

Regla conceptual:

```text
Sistema estelar
-> sol / cuerpo central
-> planetas orbitando el sol
-> lunas orbitando planetas
```

Esto significa que la posicion de un planeta no debe asumirse como estatica para siempre. El centro de cada planeta y de cada luna debe poder derivarse de su estado orbital en un tiempo dado. Las conversiones de coordenadas, el placement, el floating origin, el streaming y las representaciones visibles deben prepararse para trabajar con cuerpos cuya posicion cambia dentro del sistema estelar.

El flujo inicial sera:

1. El jugador ejecuta el juego.
2. El juego identifica el sistema estelar en el que se encuentra el jugador.
3. Se entra en ese sistema estelar.
4. Un manager carga el sistema paso a paso.
5. La carga se hace como una descarga prolongada y controlada de trabajo.

El contrato con el jugador es claro:

```text
Aceptamos una carga inicial mas larga a cambio de evitar picos duros de carga y caidas fuertes de FPS durante la experiencia.
```

La prioridad es que, una vez dentro del sistema estelar, el jugador no perciba pausas duras, pantallas de carga constantes ni bajadas fuertes de FPS provocadas por cargas pesadas. Despues de la carga inicial seguira existiendo streaming, precarga, descarga, regeneracion y movimiento de datos, pero debe hacerse de forma ligera y presupuestada.

## Approach general

El motor no debe intentar mantener un planeta completo como volumen vivo. Un planeta completo existe como receta procedural, no como una malla o buffer gigante permanentemente cargado.

La arquitectura se apoyara en varias representaciones del mismo planeta:

```text
Receta procedural   -> fuente de verdad
Impostor astronomico -> cuerpo muy lejano, tipo luna en el cielo
Modelo low-res      -> cuerpo lejano con silueta visible
Approach            -> planeta en aproximacion o vuelo alto
Chunks locales      -> terreno real cercano/interactuable
Patches editados    -> excepciones persistentes creadas por terraformado
```

La receta define como se calcula el planeta: radio, escala, semilla, superficie, cuevas, materiales y modificaciones. La mesh visible y los datos de colision se generan desde esa receta solo cuando hacen falta.

La idea clave es:

```text
El planeta completo es una receta procedural.
Solo hacemos real lo que el jugador puede ver, tocar o modificar.
```

## Presupuesto de geometria

El sistema estelar tendra un presupuesto de geometria visible. No todos los planetas reciben el mismo detalle al mismo tiempo.

La prioridad depende de la situacion del jugador:

```text
Contacto/interaccion inmediata     -> prioridad maxima
Direccion de movimiento            -> prioridad alta
Centro de camara                   -> prioridad alta
Periferia de camara                -> prioridad media/baja
Planeta del que se aleja           -> prioridad temporal
Otros planetas del sistema estelar -> impostor/proxy barato
```

El objetivo no es que cada planeta tenga siempre el maximo detalle, sino que el sistema use su presupuesto para construir lo que el jugador puede percibir o tocar.

La distancia no sera la unica metrica. El detalle debe depender tambien del tamano aparente en pantalla y del coste de mantener 90 FPS.

```text
Mas pantalla ocupada -> mas detalle permitido
Menos pantalla ocupada -> menos detalle o impostor
```

## Estados de un planeta

Cada planeta puede pasar por varios estados de presencia:

```text
Dormant      -> solo receta o datos minimos
Astronomical -> impostor direccional muy lejano
Far          -> modelo low-res/proxy lejano
Approach     -> proxy refinado y zonas de interes preparandose
Local        -> chunks voxel activos alrededor del jugador
Departing    -> descarga progresiva de chunks y vuelta a proxy/impostor
```

El cambio entre estados debe ser progresivo. El jugador puede acercarse volando, entrar en el planeta, salir de el e ir a otro. Por tanto, no puede existir un salto brusco entre "planeta lejano" y "planeta local".

## Planeta lejano e impostor

La mesh lejana del planeta no debe generarse calculando todo el volumen voxel.

Cuando el planeta esta extremadamente lejos y ocupa poco en pantalla, puede renderizarse como impostor. Este caso representa cuerpos tipo luna en el cielo: ocupan poco tamano aparente, no tienen parallax perceptible, no se interactua con ellos y no necesitan geometria real.

El impostor no debe vivir necesariamente a distancia real dentro del Z-buffer. Puede tratarse como objeto astronomico renderizado por direccion, tamano angular y capa/pasada especifica.

```text
directionToBody -> impostor
angularSize     -> escala visual
texture/cache   -> apariencia
```

El cambio entre impostor y modelo low-res no debe depender solo de distancia. Debe considerar distancia y tamano aparente:

```text
angularSize = 2 * atan(radius / distance)
```

Regla conceptual:

```text
Muy lejos + tamano aparente pequeno -> impostor
Lejos + silueta visible             -> modelo low-res
Acercandose                         -> approach
Cerca                               -> chunks locales
```

Debe existir hysteresis para evitar cambios constantes cerca del umbral:

```text
Entrar a impostor con un umbral.
Salir de impostor con un umbral un poco mayor.
```

La version low-res se genera como una superficie esferica deformada:

```text
direction -> surfaceOffset -> vertex = direction * (radius + surfaceOffset)
```

Esta representacion solo necesita conocer la superficie exterior. No calcula el interior completo ni todas las cuevas. Si una cueva tiene una entrada grande en superficie, se podra representar como informacion superficial adicional, pero el sistema no debe exigir resolver toda la red interior para ver el planeta desde lejos.

## Planeta en aproximacion

Cuando el jugador se acerca a un planeta, no podemos asumir que entra en caida libre ni que existe un unico punto de entrada.

El jugador puede caer, orbitar, volar alto, ir rasante, despegar desde superficie o salir hacia otro planeta. Por eso el sistema debe preparar zonas de interes, no un punto fijo.

Las zonas de interes se calculan usando varias senales:

```text
Cercania al planeta
Direccion de velocidad
Frustum/camara
Altitud respecto a superficie
Velocidad actual
Intencion/ruta si existe
```

La referencia basica sigue siendo el punto mas cercano del planeta:

```text
closestDirection = normalize(playerPosition - planetCenter)
closestSurfacePoint = planetCenter + closestDirection * surfaceRadius(closestDirection)
```

Pero tambien debe existir prediccion por movimiento:

```text
futurePosition = playerPosition + velocity * preloadTime
futureDirection = normalize(futurePosition - planetCenter)
futureSurfacePoint = planetCenter + futureDirection * surfaceRadius(futureDirection)
```

Durante la aproximacion o vuelo alto:

```text
El proxy global sigue existiendo.
Las zonas de interes suben de detalle.
Los chunks o patches se preparan por delante del jugador.
El resto del planeta sigue barato o como proxy.
```

Si el jugador cambia de trayectoria, las zonas de interes deben moverse y cancelar trabajo que ya no sea prioritario.

La idea principal:

```text
Al subir, aumenta el area cubierta y baja la densidad de detalle.
Al bajar, disminuye el area cubierta y sube la densidad de detalle.
```

La zona de interes no tiene por que ser una esfera perfecta. Puede ser una esfera, una capsula, un cono o una elipse orientada por velocidad y camara.

## Chunks locales

El terreno real solo se materializa cerca del jugador.

Los chunks locales se generan desde la receta procedural y las modificaciones guardadas. Estos chunks son los que pueden tener mesh detallada, colision, sustancias editables y datos de terraformado.

La carga local debe tener lookahead:

```text
lookahead = velocidad del jugador * tiempo de preparacion objetivo
```

Volar por la superficie no es lo mismo que caminar. Un jugador o vehiculo rapido necesita preparar terreno por delante, no solo alrededor.

## Culling y visibilidad

El motor no debe pintar ni generar geometria que el jugador no puede ver.

La visibilidad debe resolverse por capas, de barato a caro:

```text
Frustum culling
Culling por horizonte/hemisferio planetario
LOD por tamano aparente en pantalla
Occlusion culling jerarquico
Voxel queries con DDA 3D cuando toque
```

Tiene sentido usar Fast Voxel Traversal / DDA 3D / Hierarchical Voxel Raymarching para recorrer grids voxel de forma eficiente, pero no como una solucion bruta sobre todo el planeta.

La direccion preferida es jerarquica:

```text
Macro bloques:
- aire
- solido
- mixto

Si es aire   -> el rayo avanza rapido
Si es solido -> puede ocultar lo que hay detras
Si es mixto  -> se baja de nivel o se marca candidato
```

Este sistema puede servir para queries lejanas, laser, escaneo y occlusion voxel. La colision inmediata no debe depender de una respuesta GPU-CPU bloqueante.

## Cuevas

Las cuevas no deben ser un volumen global precalculado.

La intencion es usar Voronoi 3D y/o una red procedural para crear cuevas naturales:

```text
nodos      -> camaras
conexiones -> tuneles
radio      -> zonas anchas y estrechas
ruido      -> irregularidad organica
salidas    -> conexiones con superficie
```

El resultado debe permitir grutas largas, ramificaciones, zonas estrechas, cavidades grandes y entradas naturales a superficie.

TBD: cerrar el algoritmo exacto. La idea inicial es que Voronoi 3D ayude a construir la estructura de cueva, pero la evaluacion debe poder hacerse por zona/chunk, no como un planeta completo ya excavado en memoria.

## Materiales visuales y sustancias

Vamos a separar material visual y sustancia logica.

```text
Material visual -> shader, atlas, gradiente y parametros de render
Sustancia       -> id logico del contenido voxel
```

Las sustancias tambien deben generarse de forma procedural y consultable por posicion.

No queremos llenar un buffer global de marmol, hierro, cobre o cualquier otra sustancia para todo el planeta. Las masas de sustancia deben poder evaluarse cuando un chunk se genera.

La idea base:

```text
sustancia(point) = funcion procedural + modificaciones
```

Las sustancias pueden venir de regiones, vetas, blobs, capas por profundidad, ruido o combinaciones de esos sistemas.

Visualmente, la direccion inicial es usar un material compartido para todo el sistema estelar y un atlas/gradiente de color. Marching Cubes ya empuja el resultado hacia un aspecto limpio y algo cartoon, asi que no necesitamos texturas elaboradas para la primera version.

La referencia visual inicial es mas cercana a un estilo tipo Astroneer que a terreno realista con texturas complejas.

Para la demo inicial se usara un material con gradiente de colores para pintar el mundo por altura/profundidad/material:

```text
Blanco  -> picos de montanas
Marron  -> roca/tierra alta
Verde   -> superficie habitable
Arena   -> costa o terreno bajo
Rosa    -> agua
Oscuro  -> bajo tierra/interior
```

El agua se tratara como una sustancia volumetrica transparente. No es aire. Para render tendra un material visual propio, aunque pueda tomar su color base del atlas/gradiente.

En la primera version asumimos que el agua puede renderizarse como mesh propia o submesh separada. Los efectos submarinos, distorsion, niebla, volumen interior o comportamiento avanzado del agua quedan para documentacion futura.

TBD: definir el formato exacto del atlas y como se asigna el indice/color desde la densidad, altura, sustancia y profundidad.

## Coordenadas y escala

La generacion del planeta trabaja en coordenadas de grid. La escala visual se aplica despues al montar o renderizar el resultado en mundo.

Nombres base:

```text
GridCoordinates       -> coordenadas logicas de generacion
WorldSpaceCoordinates -> coordenadas de Unity/mundo
WorldScale            -> escala aplicada de grid a mundo
```

Valores iniciales de autoria:

```text
GridRadius = 1000
WorldScale = 4
```

Valores derivados:

```text
WorldPosition = GridPosition * WorldScale
WorldRadius = GridRadius * WorldScale
```

`WorldRadius` no es fuente de verdad independiente. Sale de `GridRadius * WorldScale`.

La escala visual no cambia el tamano logico de la celda:

```text
Micro cell logica = 1x1x1 en GridCoordinates
Macro cell logica = 4x4x4 en GridCoordinates
```

Por tanto, para un planeta con `GridRadius = 1000` y `WorldScale = 4`:

```text
Diametro logico = 2000 cells
Radio visual    = 4000 unidades de mundo
Diametro visual = 8000 unidades de mundo
```

TBD: desarrollar esta seccion en un documento propio de coordenadas, escala, floating origin y conversiones.

## Terraformado

El planeta base usara celdas macro. La escala visual del planeta puede subir, pero la edicion fina se hara con una trampa local.

La idea inicial:

```text
GridRadius         -> 1000
WorldScale         -> 4
Celda macro logica -> 4x4x4 GridCoordinates
Edicion fina local -> 1x1x1 GridCoordinates
```

Cuando el jugador terraforma una zona:

```text
1. Se localiza la celda macro afectada.
2. Esa celda macro se descompone en subceldas 1x1x1.
3. Se editan las subceldas.
4. Se guarda la modificacion como patch local.
```

Una celda macro 4x4x4 contiene:

```text
4 * 4 * 4 = 64 subceldas 1x1x1
```

Cuando el jugador se aleja, el sistema intenta compactar el patch:

```text
Si todo queda aire              -> guardar como macro vacia
Si todo queda solido homogeneo  -> guardar como macro solida/material
Si coincide con lo procedural   -> eliminar modificacion
Si conserva forma compleja      -> mantener patch refinado
```

Los patches refinados son excepciones. El planeta no debe convertirse entero a resolucion fina.

El objetivo no es que la trampa sea invisible a nivel de datos, sino que sea estable, barata y suficientemente precisa donde el jugador edita.

## Colisiones

La colision real solo existira donde el jugador, sus manos, herramientas o vehiculos puedan tocar de forma inmediata.

El sistema se divide en capas:

```text
Contact Physics      -> colision inmediata, sin retardo
Interaction Queries  -> rayos/laser/escaneos con respuesta async
Visual Terrain       -> solo render, sin fisica
```

La zona de contacto sera pequeña, inicialmente del orden de 2-3 metros alrededor del jugador. Para vehiculos se necesitara lookahead hacia la direccion de movimiento.

Las queries lejanas podran preguntarse a GPU con retardo aceptable. La colision que afecta al movimiento inmediato no puede depender de una respuesta GPU-CPU bloqueante.

Ejemplos:

```text
Caminar / tocar / conducir -> Contact Physics, sin retardo
Laser / escaneo / rayos largos -> Interaction Queries, async GPU
Terreno lejano -> Visual Terrain, sin fisica
```

Para minimizar llamadas y objetos activos, la direccion preferida es usar pools y colliders por patches o mini-chunks, no colision global.

TBD: definir si los colliders se construyen por celda, por patch o por mini-chunk.

## Memoria y GC

En runtime caliente no se deben crear estructuras nuevas de forma accidental.

Usaremos codigo normal donde no duela, y buffers explicitos donde si duela.

```text
List<T> normal:
- datos pequenos
- editor
- carga
- configuracion
- codigo no critico

Buffer preasignado + count:
- render
- fisica
- streaming
- colisiones
- generacion
- queries
- cualquier camino por frame
```

La regla no es "nunca usar List". La regla es:

```text
No asignar memoria accidentalmente en runtime caliente.
```

Se evitaran patrones como crear listas nuevas, convertir a arrays, usar LINQ en caminos calientes, construir strings por frame o crecer colecciones sin capacidad predefinida.

Para buffers grandes y caminos calientes se prefiere el patron:

```text
buffer preasignado + count
```

No se trata de prohibir `List<T>`, sino de usarla donde no meta ruido ni basura. Para datos grandes o modificados por frame, cambiar elementos a mano dentro de un buffer preasignado sera preferible a crear colecciones nuevas.

## DOTS, Jobs y Burst

DOTS, Jobs y Burst se usaran solo cuando aporten una mejora clara y medible.

El proyecto ya es tecnicamente complejo. No queremos anadir mas complejidad si no resuelve un problema real de rendimiento, memoria, paralelismo o determinismo.

Regla general:

```text
Si una solucion simple funciona y no aparece en profiler, no se complica.
Si una ruta CPU es pesada, repetitiva, paralelizable y medible, se considera Jobs/Burst.
Si el trabajo pertenece claramente a render o computo masivo visual, se prioriza GPU/Compute Shader.
```

Uso previsto:

```text
Jobs/Burst:
- contact cache de fisica cercana
- streaming y priorizacion de trabajo
- compresion/descompresion de modificaciones
- terraformado local
- preparacion de datos auxiliares
- evaluaciones CPU que gameplay/fisica necesiten sin retardo GPU

Compute Shaders:
- generacion visual masiva
- Marching Cubes visual
- culling/queries masivas
- proxies y LOD visuales
```

ECS completo queda como decision abierta. No se introduce por defecto. Se considerara solo si aparece una necesidad clara de miles de entidades o sistemas uniformes donde ECS reduzca complejidad real en vez de aumentarla.

## Trabajo pendiente

Este documento solo define el approach inicial. Cada bloque tendra su propia documentacion extendida cuando bajemos la decision a tierra.

Documentos pendientes:

```text
Sistema estelar y flujo de carga
Estados del planeta
Proxy lejano y approach
Generacion local de chunks
Cuevas
Materiales
Terraformado
Colisiones
Presupuesto de geometria
Memoria, buffers y GC
DOTS, Jobs y Burst
Persistencia de modificaciones
```
