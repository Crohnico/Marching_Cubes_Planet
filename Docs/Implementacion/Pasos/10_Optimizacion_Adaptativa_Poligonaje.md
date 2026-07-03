# 10 - Optimizacion adaptativa de poligonaje

## Estado

Documento redefinido.

Este documento sustituye la definicion anterior de 10.

La idea vigente ya no es pintar una mesh completa del planeta ni apoyarse en una
estructura compleja antes de tiempo.

10 empieza como sistema de generacion, cache y pintado por chunks.

## Objetivo inicial

10 debe hacer que el planeta se materialice por partes pequenas, persistibles y
recuperables, evitando generar o pintar la mesh completa como una sola unidad.

La unidad de trabajo inicial sera:

```text
chunk + LOD
```

Cada chunk se puede generar, guardar, cargar, pintar y liberar de forma
independiente.

Regla de validacion:

```text
Todo avance de 10 debe ser usable y testeable desde el boton Generate del Canvas.
Los botones de Inspector pueden existir como diagnostico o comparacion, pero no sustituyen la ruta de Canvas.
```

## Paso 1 - Pintado por chunk

En vez de pintar la mesh completa del planeta, 10 pintara cada chunk por separado.

Cada chunk tendra:

```text
chunkId
LOD
mesh de terreno
segmento de agua si aplica
```

Regla:

```text
El terreno de un chunk y su agua asociada forman la unidad visible inicial.
```

Si un chunk no tiene agua, solo publica su mesh de terreno.

Si un chunk tiene agua, publica tambien su segmento de agua.

Decision:

```text
Terreno y agua son partes del mismo chunk logico.
Terreno y agua se guardan y publican como dos meshes separadas.
El chunk mantiene la relacion entre ambas meshes.
```

## Paso 2 - Cache directa a disco

10 guardara y cargara directamente la informacion de los chunks desde disco.

Al inicializar un planeta:

```text
1. Crear una carpeta cache si no existe.
2. Crear un ID de planeta en funcion del seed y la recipe.
3. Comprobar el ID guardado dentro de la cache.
4. Si el ID guardado no coincide con el ID actual, borrar la cache anterior.
5. Crear una cache nueva para el planeta actual.
```

La cache pertenece al planeta generado por:

```text
seed + recipe
```

Si cambia el seed o cambia la recipe, la cache anterior deja de ser valida.

## Estructura inicial de cache

Formato conceptual:

```text
cache/
  planet_id
  chunks/
    chunkId/
      LOD0/
        mesh.pmesh
        chunk_data.pchunk
        water.pmesh
        water_data.pchunk
      LOD1/
        mesh.pmesh
        chunk_data.pchunk
        water.pmesh
        water_data.pchunk
      LOD2/
        mesh.pmesh
        chunk_data.pchunk
        water.pmesh
        water_data.pchunk
```

Por ahora se asume:

```text
El planeta puede abrirse inicialmente en LOD2 como fallback barato.
```

La primera ruta de implementacion guardara cada chunk por LOD:

```text
chunkId/LOD0/mesh.pmesh
chunkId/LOD0/chunk_data.pchunk
chunkId/LOD0/water.pmesh
chunkId/LOD0/water_data.pchunk
chunkId/LOD1/mesh.pmesh
chunkId/LOD1/chunk_data.pchunk
chunkId/LOD1/water.pmesh
chunkId/LOD1/water_data.pchunk
chunkId/LOD2/mesh.pmesh
chunkId/LOD2/chunk_data.pchunk
chunkId/LOD2/water.pmesh
chunkId/LOD2/water_data.pchunk
```

Decision:

```text
Los archivos de chunk seran binarios.
La extension .pmesh identifica datos listos para pintar/publicar.
La extension .pchunk identifica informacion interna de chunk.
Terreno y agua se guardan separados.
Cada LOD tiene sus propios archivos.
El agua usa el mismo LOD asignado al chunk.
```

### Contenido inicial de .pmesh y .pchunk

`.pmesh` contiene solo lo necesario para pintar/publicar el chunk.

Contenido inicial:

```text
vertexCount
indexCount
bounds local/world
vertices local
normals
indices
material/submesh info si aplica
```

Opcional si la ruta visual lo necesita:

```text
uvs
colors
tangents
materialIds por vertice o triangulo
```

Regla:

```text
.pmesh no guarda densidades, samples internos, marching cubes cases ni datos de vecinos.
```

`.pchunk` contiene la informacion minima necesaria para operar bordes y resolver
Transvoxel.

Contenido inicial:

```text
chunkId
LOD
grid origin
grid size
sample step
bounds
datos de borde de las 6 caras
densidades/samples de borde
materiales/sustancias de borde si aplican
info de agua de borde si aplica
edge crossings de borde si aplica
normales de borde o datos para recalcularlas
cell masks/cases de las celdas de borde si aplica
```

Regla:

```text
.pchunk empieza como border payload.
.pchunk no guarda todo el volumen interno del chunk salvo que se demuestre necesario.
```

Terraformado:

```text
La informacion completa para terraformar no se guarda en esta primera cache de 10.
Cuando haga falta terraformar, se recalculara la informacion necesaria desde seed, recipe y 06, y se documentara en el sistema correspondiente.
```

Decision:

```text
Para pintar, cargar .pmesh.
Para coser LODs o resolver Transvoxel, cargar .pchunk.
Para regenerar desde cero, usar seed + recipe + 06.
```

### Salida de Transvoxel

Transvoxel genera una mesh auxiliar de transicion.

No modifica el `.pmesh` base del terreno ni el `.pmesh` base del agua.

Decision:

```text
La mesh base del chunk permanece estable.
Las transiciones LOD se generan como meshes auxiliares por borde/cara.
Las meshes auxiliares se renderizan/publican solo cuando hay vecino con LOD distinto.
Si el vecino vuelve al mismo LOD, se deja de usar la mesh auxiliar sin reescribir la mesh base.
```

Motivo:

```text
La necesidad de Transvoxel depende del LOD del vecino.
Modificar el .pmesh base haria que el chunk dependiera del estado actual de sus vecinos.
Separar la transicion evita invalidar la cache base cada vez que cambia un vecino.
```

Cache derivada:

```text
La primera implementacion puede generar la mesh auxiliar bajo demanda desde .pchunk.
Si el coste lo justifica, se podra guardar como cache derivada por chunk, LOD, cara y LOD vecino.
```

## Paso 3 - Puntuacion y LOD por chunk

10 asignara una puntuacion a cada chunk.

La puntuacion inicial dependera de:

```text
70% cercania al jugador
30% distancia/alineacion con lo que mira la camara
```

Formula inicial:

```text
score = proximityScore * 0.70 + viewScore * 0.30
```

Donde:

```text
proximityScore = 1 cuando el chunk esta muy cerca del jugador.
proximityScore = 0 cuando el chunk esta fuera del rango de interes cercano.
viewScore = 1 cuando el chunk esta sobre la direccion de mirada.
viewScore = 0 cuando el chunk queda lejos de la direccion de mirada o detras de la camara.
```

Parametros necesarios:

```text
distancia minima/maxima para normalizar proximityScore.
distancia maxima al rayo de mirada para normalizar viewScore.
hysteresis para evitar cambios constantes de LOD.
```

Regla:

```text
No se anaden mas senales a la formula inicial.
Primero se valida cercania + mirada.
```

Con esa puntuacion, cada chunk recibira un LOD deseado:

```text
LOD0 -> chunk de maxima prioridad.
LOD1 -> chunk de prioridad media.
LOD2 -> chunk de baja prioridad o fallback lejano.
```

Thresholds iniciales por distancia:

```text
LOD0 -> hasta 3 chunks de distancia.
LOD1 -> hasta 6 chunks de distancia.
LOD2 -> el resto del planeta.
```

Hysteresis inicial:

```text
LOD0 entra hasta 3 chunks de distancia y sale al pasar de 4.
LOD1 entra hasta 6 chunks de distancia y sale al pasar de 7.
LOD2 cubre el resto.
```

Lectura:

```text
Entrar y salir de un LOD no usa exactamente la misma frontera.
Esto evita que un chunk cambie constantemente de LOD cuando el jugador esta justo en el limite.
```

Lectura:

```text
La puntuacion sigue ordenando prioridad dentro de cada zona.
La distancia define el LOD base.
La mirada puede subir prioridad de carga/publicacion, pero no abre mas LODs en esta primera regla.
```

Lectura inicial:

```text
Los chunks cercanos al jugador tienden a LOD0.
Los chunks que estan en la direccion de mirada suben prioridad.
Los chunks cercanos pero fuera de camara pueden bajar a LOD1 si hace falta.
Los chunks lejanos o poco relevantes tienden a LOD2.
```

Regla:

```text
10 decide el LOD deseado por chunk antes de cargar o generar su mesh.
```

### Cambio de LOD deseado

Si un chunk ya estaba visible con un LOD y mas tarde pasa a necesitar otro LOD,
10 no recalcula el planeta completo.

Solo resuelve ese chunk con el nuevo LOD deseado.

Regla critica:

```text
Generar o cambiar un chunk solo calcula ese chunk.
No se recalcula el planeta entero por cada chunk.
```

Al generar un chunk, 06/10 solo deben evaluar:

```text
el dominio del chunk
el halo/borde minimo que haga falta para mesh, agua o Transvoxel
```

No deben evaluar:

```text
todos los chunks del planeta
la mesh global completa
densidades globales fuera del area necesaria
```

Flujo:

```text
1. Detectar que chunkId pasa de LOD actual a LOD deseado.
2. Buscar chunkId/LODx en cache.
3. Si existe, cargar .pmesh del nuevo LOD.
4. Si no existe, generar ese chunk en el nuevo LOD y guardarlo.
5. Publicar el nuevo mesh del mismo chunk.
6. Liberar o dejar de publicar el mesh anterior de ese chunk.
7. Resolver mesh auxiliar Transvoxel si los vecinos quedan con LOD distinto.
```

Lectura:

```text
El chunk no cambia de identidad.
Solo cambia la representacion visible/cacheada que se usa para ese chunk.
```

Flujo:

```text
1. Calcular centro/bounds del chunk.
2. Calcular distancia del chunk al jugador.
3. Calcular alineacion del chunk con el forward de la camara.
4. Combinar ambas senales en una puntuacion.
5. Convertir la puntuacion en LOD0, LOD1 o LOD2.
6. Buscar en cache el chunk con ese LOD.
7. Si no existe, generarlo y guardarlo.
8. Pintar/publicar ese chunk con su LOD asignado.
```

## Paso 4 - Carga selectiva desde cache

Cuando 10 asigna `LODX` a un chunk, pregunta a disco si existe la cache para:

```text
chunkId/LODX
```

La cache del chunk debe poder resolver dos niveles de carga:

```text
*.pmesh  -> datos listos para pintar/publicar.
*.pchunk -> informacion interna minima necesaria para operaciones de borde.
```

Regla importante:

```text
10 solo debe guardar, cargar y trabajar chunks visibles.
```

No interesa iterar sobre chunks que no tienen mesh visible.

No interesa recorrer el planeta completo buscando chunks posibles.

El conjunto activo de trabajo sale de los chunks visibles o candidatos a ser
visibles segun jugador, camara y LOD asignado.

### Cuando cargar solo mesh

10 carga solo `mesh` cuando:

```text
El chunk ya existe en cache.
El chunk tiene el LOD deseado.
El chunk no necesita resolver una union Transvoxel con un vecino.
El chunk puede publicarse tal cual.
```

Lectura:

```text
Este es el camino barato.
Sirve para pintar rapido lo que ya esta cocinado.
Debe minimizar RAM, CPU y GC.
```

### Cuando cargar chunk_data

10 carga `chunk_data` cuando:

```text
El chunk necesita hacer Transvoxel con un vecino.
Hay cambio de LOD entre chunks vecinos.
Hace falta informacion de borde que no esta en el mesh final.
Hace falta reconstruir o ajustar la salida visible de ese chunk.
```

Lectura:

```text
El mesh cacheado sirve para pintar.
chunk_data sirve para poder razonar y operar sobre el chunk.
```

Regla:

```text
No cargar chunk_data si el mesh basta para pintar el chunk actual.
```

## Paso 5 - Trabajo por paquetes de chunks

10 no debe generar, cargar ni instanciar todos los chunks visibles de golpe.

Se trabajara por paquetes.

Un paquete representa un grupo limitado de operaciones de chunks:

```text
chunks a comprobar en cache
chunks a cargar como mesh
chunks a cargar como chunk_data
chunks a generar porque no existen
chunks a publicar/pintar
```

Objetivo:

```text
No saturar RAM.
No saturar CPU.
No saturar disco.
No provocar picos de instanciacion.
No bloquear el frame por intentar resolver demasiados chunks a la vez.
```

Regla:

```text
El sistema avanza por paquetes hasta completar el conjunto visible deseado.
```

Decision inicial:

```text
Cada paquete tendra 5 o 6 chunks.
```

Pendiente:

```text
Definir prioridad dentro del paquete.
```

Presupuestos:

```text
No se fija todavia presupuesto por frame, disco o RAM.
Primero se implementa la ruta simple y se mide.
```

## Paso 6 - Cocinado lento de LODs restantes

Si 10 no necesita cargar, generar o publicar ningun LOD urgente para el conjunto
visible actual, entra en modo de trabajo lento de fondo.

Ese modo se usa para ir descargando, generando y montando en disco los LODs que
falten del planeta.

Objetivo:

```text
Preparar LODs antes de que hagan falta.
Aprovechar momentos sin urgencia visible.
No competir con la carga/generacion necesaria para el frame actual.
No saturar RAM, CPU ni disco.
```

Regla:

```text
El cocinado lento solo trabaja cuando no hay trabajo visible prioritario.
```

El cocinado lento puede preparar:

```text
LODs restantes de chunks visibles actuales.
LODs restantes de chunks candidatos cercanos.
LODs restantes de zonas previsibles segun jugador y camara.
```

Pero sigue respetando esta regla:

```text
No recorrer el planeta entero de golpe.
No activar una generacion masiva.
No bloquear el sistema por completar cache.
```

Lectura:

```text
La cache se completa poco a poco.
Lo visible manda.
Lo no urgente cocina despacio.
```

Pendiente:

```text
Definir ritmo del cocinado lento.
Definir cuantos chunks/LODs puede preparar por paquete lento.
Definir si el cocinado lento puede tocar chunks no visibles pero previsibles.
Definir cuando se pausa inmediatamente por nueva demanda visible.
```

## Flujo inicial con 06

Cuando 06 cree la data del planeta, no debe crear toda la mesh a la vez.

El flujo inicial sera:

```text
1. 06/10 determinan los chunks necesarios para el planeta visible inicial.
2. Para cada chunk, 10 calcula su puntuacion segun jugador y camara.
3. 10 asigna LOD0, LOD1 o LOD2 al chunk.
4. 10 agrupa los chunks visibles en paquetes de trabajo.
5. Para cada paquete, 10 comprueba si cada chunk existe en disco con el LOD asignado.
6. Si el chunk existe y no necesita Transvoxel, 10 carga solo mesh.
7. Si el chunk existe y necesita Transvoxel, 10 carga chunk_data.
8. Si el chunk no existe, 10 pide/genera la data necesaria para ese chunk y LOD.
9. 10 construye la mesh del chunk si hace falta.
10. 10 guarda mesh y chunk_data en cache segun corresponda.
11. 10 pinta/publica ese chunk de forma independiente.
12. 10 pasa al siguiente paquete sin saturar el sistema.
```

Regla:

```text
No generar la mesh completa del planeta si los chunks pueden resolverse uno a uno.
Generar un chunk solo calcula ese chunk y su halo minimo necesario.
No recalcular el planeta entero por cada chunk.
```

## Relacion con 09

10 no necesita un contrato especial nuevo para que 09 entienda chunks.

Desde 10, la operacion visible es:

```text
pinta esto
```

09 debe saber, por su propia responsabilidad, que mesh esta modificando y si
necesita coger triangulos de paquetes lejanos para hacer sitio.

Regla:

```text
10 entrega meshes o paquetes de triangulos para publicar.
09 gestiona la residencia, sustitucion y confiscacion de triangulos.
10 no decide desde aqui que triangulos lejanos confisca 09.
```

## Decisiones cerradas por ahora

```text
10 trabaja por chunks.
10 no pinta una mesh completa unica del planeta.
Cada chunk puede tener terreno y segmento de agua.
Terreno y agua pertenecen al mismo chunk logico, pero son dos meshes separadas.
10 tendra cache en disco.
El ID de cache depende de seed + recipe.
Si el ID de cache no coincide, se invalida la cache anterior.
10 puntua chunks segun cercania al jugador y direccion de mirada de la camara.
La formula inicial de puntuacion sera 70% cercania y 30% mirada.
Los thresholds iniciales seran LOD0 hasta 3 chunks, LOD1 hasta 6 chunks y LOD2 para el resto del planeta.
La hysteresis inicial sera LOD0 sale al pasar de 4 chunks y LOD1 sale al pasar de 7 chunks.
10 asigna LOD0, LOD1 o LOD2 a cada chunk en funcion de esa puntuacion.
LOD2 puede usarse como fallback barato inicial.
Generar o cambiar un chunk solo calcula ese chunk y su halo minimo, nunca el planeta entero.
La primera ruta guarda mesh y chunk_data por chunkId/LOD.
10 solo trabaja sobre chunks visibles o candidatos visibles.
10 no itera sobre chunks sin mesh visible.
10 carga solo .pmesh cuando no necesita operar con datos internos del chunk.
10 carga .pchunk cuando necesita resolver Transvoxel o bordes por cambio de LOD.
10 procesa chunks por paquetes para no saturar el sistema.
El paquete inicial de trabajo tendra 5 o 6 chunks.
No se fija presupuesto por frame, disco o RAM hasta medir la primera ruta.
09 recibe la orden de pintar/publicar y gestiona internamente residencia y confiscacion.
Si no hay trabajo visible urgente, 10 cocina lentamente LODs restantes en disco.
06 no debe forzar la generacion de toda la mesh a la vez para que 10 pueda pintar.
```

## Pendientes

```text
Definir como se calcula exactamente el planetId.
Definir politica de invalidacion parcial si solo cambia una parte de la recipe.
Definir prioridad dentro del paquete de chunks.
Definir ritmo y limites del cocinado lento de LODs restantes.
```
