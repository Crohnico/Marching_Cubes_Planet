# 09 - Pool global de triangulos

## Objetivo

Crear un presupuesto fijo de triangulos para una familia de geometria gestionada.

La intencion de 09 no es reducir, simplificar ni recalcular geometria. La intencion es controlar cuantos triangulos pueden estar vivos/pintados al mismo tiempo.

## Implementacion incremental actual

La primera reparacion de 09 se divide en dos niveles:

```text
Nivel A -> residencia real de paquetes/publicaciones y confiscacion.
Nivel B -> salida visible Mesh runtime poseida directamente por el artista.
```

El nivel A es obligatorio antes de seguir:

```text
Cada publicacion concedida ocupa un paquete residente del artista.
Cada paquete guarda ownerId, meshId, allocationId, triangleCount, score, bucket y version.
Una publicacion nueva con mejor priorityScore puede confiscar paquetes residentes peores.
Una publicacion peor que lo residente se deniega sin borrar lo ya ocupado.
Una publicacion con el mismo ownerId + meshId reemplaza su publicacion anterior.
El storage de paquetes se reserva de forma perezosa en el primer Draw real, no al entrar en Play.
ReleaseAllSlots suelta las referencias para recuperar RAM.
```

Estado aceptado temporalmente:

```text
08 sigue construyendo la Mesh visible usando la seleccion devuelta por 09.
09 ya no es solo un filtro efimero: conserva residencia de paquetes y puede confiscar.
09 todavia no posee por completo la Mesh runtime visible del artista.
```

Pendiente para cerrar el nivel B:

```text
Mover la salida visible Mesh runtime al artista.
Hacer que las confiscaciones invaliden o retiren visualmente triangulos ya pintados por publicaciones anteriores.
Agrupar publicaciones por material/render batch dentro del artista.
```

Regla:

```text
No se avanza a reparto adaptativo real de 10 sin que 09 conserve al menos residencia/confiscacion de slots.
La posesion completa de la Mesh por 09 queda como siguiente cierre de 09 si la ruta actual necesita pintar varias publicaciones simultaneas.
```

Presupuestos iniciales:

```text
EnvironmentTriangleBudget = 1_000_000 tris
ParticlesTriangleBudget = 100_000 tris
EnvironmentArtistId = 0
ParticlesArtistId = 1
```

El primer presupuesto representa el artista/pool de Environment: planeta, terreno, agua y geometria de mundo que decidamos meter en este dominio.

El segundo presupuesto representa el artista/pool de Particles: niebla, humo, explosiones, sangre u otros efectos que queramos controlar con un budget separado.

09 debe permitir que en el futuro existan varios artistas/pools independientes:

```text
Environment: terreno, agua, rocas, meshes de mundo, props que entren en este sistema.
VFX/Fog: niebla volumetrica, humo, explosiones, sangre u otros efectos con tris.
Otros dominios futuros si tienen presupuestos y politicas distintas.
```

Cada artista tiene su propio presupuesto, sus propios slots, sus propios recursos visuales y su propia politica de reclamacion.

Crear otro artista debe ser barato:

```text
Duplicar o crear otro PlanetTriangleBudgetProfile.
Asignarle un artistId uint nuevo.
Asignarle un presupuesto, por ejemplo 10k, 29k, 200k o lo que toque.
Registrarlo en el bootstrap de pools.
Usarlo desde los painters indicando ese artistId.
```

Contrato funcional:

```text
sistema quiere pintar tris -> llama a Draw(meshId, datos[], priority) del artista -> 09 decide que entra en su presupuesto -> 09 pinta/actualiza su salida visible gestionada
```

Regla central:

```text
09 no cambia el poligonaje de ninguna geometria.
09 no decide como se generan los triangulos.
09 no decide donde una geometria debe tener mas o menos detalle.
09 no calcula vision, frustum, mirada ni LOD de planeta.
09 define artistas/pools que poseen y reparten slots dentro de su presupuesto fijo.
Dentros de un artista, los triangulos gestionados solo se publican a traves de ese artista.
Un artista/painter tiene un presupuesto.
Todo lo que use ese artista compite por el mismo presupuesto.
El ownerId no da prioridad ni reserva capacidad.
El materialId/renderBatch no da prioridad ni reserva capacidad.
El meshId identifica una publicacion estable dentro del artista; no autoriza pintar por fuera de 09.
```

## Modelo mental

Todo lo que quiera mantener triangulos gestionados por un dominio pasa por el artista de ese dominio.

Ejemplos:

```text
terreno -> Environment.
agua -> Environment.
rocas -> Environment si queremos que compitan con el mundo.
arboles -> Environment o un artista propio si lo decidimos.
niebla -> VFX/Fog.
humo de hoguera -> VFX/Fog.
explosion -> VFX/Fog.
```

Un sistema no crea triangulos visibles ilimitados por su cuenta dentro de un dominio gestionado.

Un sistema dice:

```text
Oye, artista Environment, necesito publicar X tris en esta zona.
```

09 actua internamente:

```text
internamente pinta X slots.
internamente reclama slots de peor prioridad si necesita sitio.
internamente deniega o deja sin pintar lo que no mejora el estado actual.
```

El sistema que llama no recibe una respuesta obligatoria ni usa el resultado interno de 09 para decidir gameplay, LOD o correccion de geometria.

Contrato mental:

```text
El caller dice: pintame esto en meshId X.
El artista recibe la peticion como una orden de dibujo gestionado.
Si el meshId ya existe, el artista actualiza/reemplaza/anade su contenido gestionado.
Si el meshId no existe, el artista crea una entrada gestionada para ese meshId.
El artista procesa presupuesto, prioridad recibida y reclamacion.
El artista pinta todos, algunos o ninguno.
El caller principal no necesita saber si quedo visible.
```

La idea practica:

```text
Hay un pool fijo de slots de triangulo.
Cada slot puede tener un releaseGroup/owner opcional para diagnostico y liberacion agrupada.
Cada slot tiene meshId, posicion representativa opcional y score de prioridad recibido.
Cuando el pool se llena, los slots de peor prioridad son los primeros candidatos a ser reutilizados.
El pool conoce que slots estan pintados y que recursos visuales propios ocupan.
El pool puede liberar, invalidar o sobrescribir los slots que posee.
Si una pagina planetaria nueva llega con mejor prioridad que geometria residente peor, el artista puede reclamar esos slots.
Si el agua de una pagina activa compite con terreno mas viejo o de menor prioridad dentro del mismo artista, el artista puede reclamar segun politica interna.
El tipo de cosa que pide no importa; solo importa que compite dentro del mismo artista y con la misma politica.
```

## Alcance de esta fase

Entra:

```text
Presupuesto configurable por artista/pool.
ScriptableObject simple de presupuesto cargado desde Resources.
Controlador que reserva/prepara el pool de un artista al arrancar.
Pool fijo de slots de triangulo.
Freelist de slots libres.
Release group opcional por sistema/emisor.
Wrapper de pintado para que 08 publique triangulos a traves de 09.
Retirada del cortafuegos local de pintado/generacion usado antes de 09.
Autoridad sobre que triangulos gestionados estan pintados dentro de ese artista.
Liberacion, invalidacion o sobrescritura de recursos visuales propios del artista.
Reclamacion de slots de peor prioridad si el pool esta lleno.
PriorityScore plano recibido en cada request como criterio inicial.
Buckets/anillos de prioridad para evitar busquedas caras.
Early-out por peor score residente cuando el pool esta lleno.
Invalidacion de slots reclamados.
Metricas de concedidos, denegados, reclamados y vivos.
Release opcional por releaseGroup/owner.
Pruebas manuales de saturacion del pool.
Tests de asignacion, release y reclamacion.
```

## Fuera de alcance

No entra:

```text
Cambiar el poligonaje de una mesh.
Reducir la geometria generada por 07.
Robar presupuesto entre artistas distintos.
Reparto adaptativo de detalle interno.
Reparto por direccion de mirada.
Oclusion.
Frustum culling.
LOD natural de props.
Decidir cuando un prop debe volver a publicar geometria por distancia.
Decidir cuando el mundo debe recalcular poligonaje.
Disparar actualizaciones por reparto adaptativo, oclusion o frustum.
Generar triangulos.
Marching Cubes.
Materiales finales.
Colisiones.
Persistencia.
Streaming final.
```

Regla:

```text
Si una decision trata de generar triangulos, pertenece a 07 u otro generador.
Si una decision trata de pintar una Mesh de validacion sin presupuesto, pertenece a 08.
Si una decision trata de conceder/reclamar slots de un artista, pertenece a 09.
Si una decision trata de poner mas detalle en una zona de una geometria adaptable, pertenece a 10.
Si una decision trata de no generar o no publicar paginas de planeta por mirada/frustum local, pertenece a 10.
Si una decision trata de visibilidad auxiliar reutilizable para otros productores, pertenece a 11 como senal de entrada, no como pintor.
Si una decision trata de cuando volver a publicar geometria por distancia/visibilidad, no pertenece a 09.
```

## Relacion con 08, 10 y 11

08 actual:

```text
07 genera triangulos.
08 los convierte en Mesh visible de validacion.
```

09 cambia el camino de publicacion:

```text
07 genera triangulos.
08 quiere publicar/pintar triangulos.
08 no escribe directo a una Mesh ilimitada.
08 llama al wrapper del artista Environment de 09.
09 decide internamente que slots de Environment usa.
09 actualiza la representacion visible propia del artista con lo que decida pintar.
08 no inspecciona que parte quedo visible como contrato de funcionamiento.
```

Cambio importante al entrar 09:

```text
El cortafuegos local que antes limitaba el pintado/generacion deja de ser la autoridad.
El generador/painter puede pedir publicar todos los triangulos que haya producido.
El wrapper/artista decide internamente cuantos entran realmente en su presupuesto.
Si no hay hueco y la nueva peticion no mejora lo que ya existe, 09 deja esa parte sin pintar y lo registra como diagnostico interno.
```

Propiedad de lo visible:

```text
Dentro de un artista, 09 es la autoridad de lo que esta pintado.
09 sabe que slots estan vivos, de quien son, donde estan y que recursos visuales propios ocupan.
09 puede liberar, invalidar o sobrescribir esos recursos cuando reclama slots.
09 no libera recursos que pertenezcan a otro artista o a otro sistema salvo transferencia explicita de ownership.
```

Regla:

```text
El generador genera.
El painter pide publicar.
El artista de 09 acepta, reclama, deniega y pinta su salida visible internamente.
El caller principal no cambia su flujo segun el resultado.
```

## Limite funcional de 09

El contrato publico de 09 se reduce a:

```text
Te paso datos de triangulos para este artista, este meshId y esta zona.
El artista intenta pintarlos dentro de su presupuesto.
Si caben, los pinta.
Si no caben, pinta lo que pueda/reclama lo que toque/deniega segun politica.
La decision queda dentro del artista.
```

09 no decide cuando se debe volver a pedir.

API mental:

```text
EnvironmentArtist.Draw(meshId, datos[], priority)
ParticlesArtist.Draw(meshId, datos[], priority)
```

Regla:

```text
El caller principal no necesita saber si finalmente se pinto todo.
El planeta pide pintar y continua.
El artista mantiene internamente que slots quedaron vivos, reclamados o denegados.
El planeta, agua, roca, prop o particula no poseen la verdad de visibilidad.
El planeta si posee la verdad de que geometria quiere publicar y con que resolucion.
```

Ejemplos:

```text
Props: publicaran geometria al crearse o cuando otro sistema decida que su distancia/prioridad ha cambiado.
Mundo/terreno: publicara lotes cuando el planeta activo/10 rehaga su shell adaptativo por distancia, mirada, movimiento, frustum local o ciclo de vida.
Particulas/VFX: publicaran geometria cuando nazcan, crezcan, mueran o cambie su prioridad externa.
```

Regla:

```text
09 no observa el mundo para iniciar actualizaciones.
09 no recalcula poligonaje.
09 no decide que geometria debe existir.
09 no decide que esta en vision.
09 solo arbitra capacidad dentro del artista solicitado.
```

10:

```text
No recibe un budget concedido por 09.
Decide de forma independiente como repartir detalle dentro de la geometria planetaria virtual.
Publica lo que decide mediante Draw(meshId, datos[], priority) o ruta equivalente del artista.
No ajusta su reparto por si 09 pinto todo, parte o nada.
No posee el millon de Environment ni ningun presupuesto de artista.
```

11:

```text
No cambia la frontera 09/10.
No pinta, no libera slots directamente y no decide LOD del planeta.
Si existe, aporta senales auxiliares o diagnostico que los productores pueden usar antes de publicar.
```

## Presupuesto configurable

Cada artista tiene un presupuesto que vive en un asset simple.

Decision inicial:

```text
PlanetTriangleBudgetProfile : ScriptableObject
Ruta inicial Environment: Assets/Resources/TrianglePools/EnvironmentTriangleBudgetProfile.asset
Ruta inicial Particles: Assets/Resources/TrianglePools/ParticlesTriangleBudgetProfile.asset
```

Campos iniciales:

```text
artistId = 0
artistName = Environment
totalTriangleBudget = 1_000_000
priorityBucketCount = 32
maxTrackedOwners = 64
maxAllocationsPerOwner = 8192
```

Perfiles iniciales:

```text
Environment -> 1_000_000 tris.
Particles -> 100_000 tris.
```

Perfiles adicionales:

```text
Cualquier artista nuevo debe poder crearse como otro asset en Resources/TrianglePools.
Un artista pequeno de 10k, 29k o similar no requiere codigo nuevo si usa la misma politica base.
Solo requiere artistId uint unico, nombre debug, presupuesto y registro en el bootstrap.
```

Reglas:

```text
El asset es fuente de valores por defecto.
El controlador carga el perfil de su artista desde Resources al arrancar.
El runtime copia esos valores a una estructura plana.
No se lee Resources por frame.
No se modifica el asset automaticamente en runtime.
Los presupuestos de artistas distintos no se mezclan salvo decision futura documentada.
Crear un artista nuevo debe ser una operacion de configuracion, no una nueva arquitectura.
```

Motivo:

```text
Queremos ajustar el presupuesto sin hardcodearlo.
No queremos montar todavia un sistema de config complejo.
Resources es suficiente para esta fase.
Si mas adelante hay perfiles por plataforma o calidad, se documenta aparte.
```

## Controlador de artista

### PlanetTrianglePoolController

Sistema real.

Responsabilidad:

```text
Cargar PlanetTriangleBudgetProfile.
Crear la copia runtime de presupuesto.
Inicializar el pool del artista.
Reservar/preparar memoria para totalTriangleBudget slots.
Exponer API de request/release.
Exponer metricas.
Liberar recursos.
```

Regla:

```text
El controlador no genera triangulos.
El controlador no sabe Marching Cubes.
El controlador no sabe materiales.
El controlador solo reparte slots, mantiene ownership y controla lo pintado por su artista.
```

Al arrancar:

```text
1. Cargar perfil.
2. Validar presupuesto.
3. Preasignar arrays/buffers de slots.
4. Inicializar freelist con todos los slots libres.
5. Inicializar buckets de prioridad vacios.
6. Quedar listo para recibir requests.
```

## Bootstrap de artistas

El arranque crea tantos pools como perfiles activos haya configurados.

Decision inicial:

```text
Environment activo.
Particles activo.
```

Regla:

```text
El core no debe asumir que solo existen dos artistas.
Los dos iniciales son la configuracion de esta fase, no una limitacion del sistema.
```

Flujo para crear otro artista:

```text
1. Crear asset PlanetTriangleBudgetProfile en Resources/TrianglePools.
2. Poner artistId uint unico.
3. Poner totalTriangleBudget.
4. Marcarlo como activo en el bootstrap/configuracion.
5. Usarlo desde un painter indicando ese artistId.
```

Ejemplos:

```text
MiniFog_10k.
Debris_29k.
TemporaryDebug_5k.
```

## Prioridad de publicacion

Decision inicial:

```text
La prioridad principal llega con cada request.
El productor calcula la prioridad segun su dominio.
Para planeta activo, 10 calcula prioridad desde distancia, mirada, movimiento/lookahead, frustum local y ciclo de vida.
09 no recalcula esa prioridad ni consulta camara/mirada para decidir LOD.
09 solo compara scores recibidos para arbitrar capacidad.
```

Contrato:

```text
requestPriorityScore -> float.
menor score = mejor candidato inicial.
mayor score = peor candidato inicial.
representativeWorldPosition -> opcional para diagnostico, bounds y fallback de Lab.
meshId -> identidad estable de publicacion.
```

Fallback de Lab:

```text
PlanetTrianglePriorityReferenceLab puede existir como herramienta de Lab.
Sirve para calcular un score simple por distancia cuando una request de prueba no trae prioridad.
No es autoridad del runtime del planeta.
No convierte a 09 en sistema de camara, player, vision o LOD.
```

Referencia player/camara:

```text
PlanetLodAgent vive en el jugador o en el objeto que represente su vista.
Su unica funcion es empujar a 09 un snapshot plano: playerPositionWorld, cameraForwardWorld y version.
El push ocurre solo cuando hay movimiento, giro o vence un intervalo maximo configurado.
09 guarda el ultimo snapshot para que 10 y los flujos de prueba tengan una fuente comun de posicion/mirada.
09 no usa ese snapshot para decidir LOD, frustum, visibilidad ni regeneracion.
El fallback de Lab no cuenta como player/camara real para 10.
```

Reglas:

```text
La prioridad es un dato plano.
09 puede guardar una senal plana de player/camara, pero no depende de locomocion ni XR.
La camara empuja datos simples; 09 no consulta ni gobierna el rig.
Si una request real llega sin prioridad valida, 09 debe fallar con diagnostico claro.
Si una request de Lab llega sin prioridad valida, puede usar una referencia explicita de fallback configurada en Lab.
Actualizar prioridades o requests no crea GC.
```

## Unidad de asignacion

La unidad conceptual es un slot de triangulo.

Un slot representa:

```text
1 triangulo visible gestionado por el pool.
meshId.
ownerId.
allocationId.
posicion representativa.
priorityBucket.
priorityScore.
estado libre/ocupado.
version.
```

Formato conceptual:

```text
TriangleSlot:
    uint meshId
    uint ownerId
    int allocationId
    int nextFreeOrBucket
    int priorityBucket
    float score
    uint version
    Vector3 representativeWorldPosition
```

Regla:

```text
El score recibido se usa para prioridad/reclamacion.
La posicion representativa no sustituye al score del productor.
No es una fuente de verdad geometrica.
La geometria de entrada viene del sistema que pide pintar.
La geometria visible gestionada vive en la salida del artista.
```

Aunque el slot sea un triangulo, las peticiones normales deben agruparse en lotes:

```text
RequestTriangles(owner, triangleCount, bounds/position)
```

Motivo:

```text
Pedir de uno en uno seria demasiado caro para publicar geometria grande.
El pool debe soportar slots individuales internamente, pero la API normal trabaja por batch.
```

## Wrapper de pintado

### PlanetTrianglePoolWriter

Sistema real/wrapper entre los generadores/painters y el pool.

Responsabilidad:

```text
Recibir triangulos que un sistema quiere publicar.
Llamar al artista correspondiente con Draw(meshId, datos[], priority).
Pedir slots al PlanetTrianglePoolController de forma interna.
Escribir o actualizar la representacion visible solo en slots concedidos.
Guardar diagnostico interno si no caben.
Invalidar triangulos cuyo slot fue reclamado.
Ocultar al caller principal si se pinto todo, parte o nada.
```

Uso desde 08 antes de 10:

```text
08 recibe resultado de 07.
08 crea batches de triangulos a publicar.
08 llama a PlanetTrianglePoolWriter.
PlanetTrianglePoolWriter llama a Draw(meshId, datos[], priority) del artista Environment.
El artista decide internamente que slots acepta/reclama/deniega.
El artista actualiza su salida visible.
08 no gestiona el resultado como owner de una asignacion.
08 no decide que hacer si solo entro una parte.
```

Uso desde 10:

```text
10 decide paginas, LOD y geometria virtual deseada.
10 usa la informacion/formato de 08 para preparar lotes publicables.
10 llama a PlanetTrianglePoolWriter como backend de Draw gestionado.
El artista decide internamente que pinta.
10 no recibe ni usa una respuesta de 09 para recalcular su reparto.
```

Regla:

```text
08 deja de ser el dueño ilimitado de todos los triangulos visibles.
09 pasa a ser el dueño del presupuesto de slots.
08 sigue siendo responsable de preparar el dato publicable.
09 es responsable de convertir lo aceptado internamente por el presupuesto en representacion visible.
09 sigue siendo responsable de saber que quedo realmente pintado.
```

## Politica de asignacion

Entrada minima de una request:

```text
meshId.
ownerId.
requestedTriangleCount.
representativeWorldPosition o bounds.
priorityScore calculado por el productor.
```

Primera regla:

```text
Si hay suficientes slots libres, concederlos desde freelist.
```

Segunda regla:

```text
Si no hay slots libres, buscar slots ocupados que esten en un bucket de prioridad peor que la request.
```

Tercera regla:

```text
Si existen slots peores, reclamarlos, invalidar su owner anterior y concederlos a la nueva request.
```

Cuarta regla:

```text
Si no existen slots peores suficientes, conceder parcial solo si el modo de la request lo permite; si no, denegar con diagnostico.
```

Decision inicial:

```text
Requests de 08 para superficies grandes pueden aceptar asignacion parcial.
Requests criticas futuras podran marcarse como all-or-nothing.
```

Regla:

```text
Una asignacion parcial debe estar diagnosticada.
No se puede confundir con geometria completa.
```

## Reclamacion barata por prioridad

No se permite escanear 1M slots por cada request.

Score inicial:

```text
requestScore = priorityScore recibido en la request
menor score = mejor candidato
mayor score = peor candidato
```

Algoritmo conceptual:

```text
Si tengo capacidad libre:
    concedo slots desde freelist.
    registro la asignacion.
    si el nuevo score es peor que el peor residente actual, actualizo peor residente.

Si no tengo capacidad libre:
    comparo requestScore contra worstResidentScore.
    si requestScore >= worstResidentScore:
        rechazo o dejo pendiente sin buscar mas.
    si requestScore < worstResidentScore:
        busco slots peores en buckets de peor prioridad.
        reclamo esos slots y asigno los nuevos.
```

Regla:

```text
El pool solo pregunta a estructuras internas cuando la peticion puede mejorar lo ya residente.
Una peticion mas lejana que el peor triangulo vivo no merece recorrer buckets.
```

Decision inicial:

```text
Usar buckets/anillos de prioridad.
```

Funcionamiento:

```text
1. Recibir priorityScore desde el productor.
2. Convertir priorityScore a priorityBucket.
3. Guardar cada slot ocupado dentro de su bucket.
4. Para una nueva request, usar su bucket.
5. Si no hay libres, buscar buckets peores que el bucket de la request.
6. Reusar slots sacados de los buckets de peor prioridad.
```

Coste esperado:

```text
O(1) para coger slots libres.
O(bucketCount) para encontrar candidatos peores.
O(reclaimedCount) para mover los slots reclamados.
```

Regla:

```text
bucketCount empieza bajo y fijo.
No se hace ordenacion global por score exacto.
No se hace busqueda lineal sobre todos los triangulos en runtime caliente.
```

## Peor residente cacheado

El pool mantiene un resumen barato del peor candidato vivo.

Nombre conceptual:

```text
worstResidentBucket
worstResidentScore
```

Significado:

```text
worstResidentBucket -> bucket de peor prioridad que contiene slots ocupados.
worstResidentScore -> score aproximado derivado del rango del worstResidentBucket.
```

Uso:

```text
Si el pool no esta lleno, worstResident solo se actualiza como metrica/cache.
Si el pool esta lleno, worstResident permite rechazar barato peticiones peores.
```

Reglas:

```text
worstResidentScore no es exacto por slot en la ruta caliente.
No se mantiene un maximo global exacto por triangulo.
Se deriva del peor bucket ocupado y se usa como early-out aproximado.
Puede ser conservador.
Si queda dudoso o stale, se recalcula desde buckets compactados, no escaneando todos los slots en CPU.
No se usa para cambiar poligonaje, solo para aceptar/rechazar/reclamar slots.
```

Decision:

```text
09 usa worstResident aproximado por bucket.
No usa worstResident exacto por slot en la primera version.
```

Motivo:

```text
Exacto por slot obliga a mantener o recalcular el peor triangulo global.
En GPU eso implica reducciones globales, heaps/priority queues o mantenimiento incremental complejo.
Para el contrato de 09 basta con saber que una request esta mejor/peor que un rango de prioridad.
Los productores pueden recalcular prioridad y generar nuevas requests; 09 no necesita precision fina.
```

Opcion futura si hiciera falta:

```text
Hibrido:
    early-out por worstResidentBucket.
    si hay empate o demasiada rotacion en el peor bucket, hacer reduccion solo dentro de ese bucket compacto.

No hacer:
    reduccion global exacta de todos los slots por request.
    heap global GPU como primera version.
```

Sobre actualizacion de prioridades:

```text
La prioridad de un slot no se recalcula cada frame obligatoriamente dentro de 09.
Refresh Priority Buckets significa reconstruir buckets desde los scores que el productor haya publicado o actualizado.
Si el productor no republica o no actualiza prioridad, 09 conserva el score anterior como dato interno de arbitraje.
```

Decision inicial:

```text
Los buckets se refrescan en GPU.
No se refrescan cada frame por defecto.
Se refrescan cuando entra una tanda de requests con scores nuevos o cuando el Lab lo fuerce.
Si llega una request con el pool lleno y los buckets estan stale, se refrescan antes de reclamar.
La unica prioridad de 09 es el score plano recibido.
```

Regla:

```text
No hay prioridad especial de gameplay en 09.
No hay prioridad por mirada en 09.
No hay prioridad por frustum/oclusion en 09.
El planeta/10 generara nuevas requests si quiere cambiar que geometria compite.
09 solo compara scores recibidos.
```

Opciones descartadas para la primera version:

```text
Exacto global por slot:
    Pros: siempre identifica el peor triangulo residente.
    Contras: requiere reduccion global, heap/priority queue o mantenimiento incremental caro; peor para requests frecuentes.

Exacto por bucket cada request:
    Pros: mas preciso que el rango de bucket.
    Contras: requiere reduccion dentro del bucket en cada decision dudosa; puede entrar mas adelante como hibrido.

Aproximado por bucket:
    Pros: barato, estable, compatible con buffers compactos y acceso contiguo.
    Contras: puede no elegir el peor slot absoluto dentro del bucket.
    Decision: usar esta opcion en 09.
```

## Invalidacion

Cuando 09 reclama un slot:

```text
El slot cambia de owner.
La version del slot aumenta.
El owner anterior queda marcado como invalidado para ese allocationId/slot.
La representacion visual debe dejar de usar el triangulo anterior.
```

El handle de asignacion es interno del artista.

No es el contrato mental principal para el generador de mundo.

Uso interno:

```text
Relacionar slots vivos con owner/allocation.
Invalidar slots reclamados.
Liberar por owner o por artista.
Construir metricas y diagnostico.
Permitir tests/labs que inspeccionen que ocurrio.
```

Formato conceptual interno:

```text
TriangleAllocationHandle:
    uint artistId
    uint ownerId
    uint allocationId
    uint slotListOffset
    uint slotCount
    uint version
```

Reglas:

```text
Un owner no puede asumir que sus slots viven para siempre.
El artista puede invalidar slots sin que el caller principal tenga que reaccionar al instante.
Si un sistema necesita persistencia fuerte sobre una asignacion, eso sera un modo explicito futuro.
En 09 base, el flujo normal es Draw(meshId, datos[], priority) y diagnostico opcional.
```

Decision de invalidacion:

```text
No usar callbacks por slot/triangulo.
No hacer polling masivo desde CPU.
Usar versionado por slot/allocation como verdad interna.
Usar cola compacta de eventos de invalidacion para diagnostico/labs/owners interesados.
La cola vive en buffer preasignado del artista.
La cola se rellena desde compute cuando se reclaman slots.
```

Formato conceptual de evento:

```text
TriangleInvalidationEvent:
    uint artistId
    uint previousOwnerId
    uint allocationId
    uint slotListOffset
    uint slotCount
    uint newVersion
```

Motivo:

```text
Callback por elemento escala fatal con millones.
Polling CPU de millones de versiones tambien escala fatal.
Una cola compacta solo contiene lo que cambio.
El versionado evita usar handles/eventos viejos.
```

## Datos de entrada

Datos minimos:

```text
PlanetTriangleBudgetProfile.
ArtistId o writer asociado a un artista.
Player/camera Transform o posicion world.
Requests de triangulos.
OwnerId del sistema solicitante.
Bounds o posicion representativa por request.
TriangleCount solicitado.
Modo de request: allowPartial / allOrNothing.
```

## Datos de salida

Salida publica:

```text
DrawResult opcional solo para Lab/debug.
diagnostico opcional.
Metricas consultables por artista.
Metricas consultables por owner.
```

Regla:

```text
El flujo normal puede ignorar el DrawResult.
Los productores principales no deben depender de DrawResult para decidir geometria, LOD, gameplay ni persistencia.
El planeta no necesita retener un handle para funcionar.
Labs, tests y diagnostico si pueden leer que paso con la peticion.
```

Estado interno:

```text
TriangleAllocationHandle interno.
Lista/rango de slotIds concedidos.
triangleCountGranted.
triangleCountDenied.
triangleCountReclaimed.
Eventos o estado de invalidacion.
```

No debe producir:

```text
Nueva geometria.
Nueva triangulacion.
Nuevos materiales finales.
Nueva density(point).
Nueva estructura principal de LOD.
```

## Componentes/scripts previstos

Namespace inicial:

```text
MarchingCubesPlanet.TrianglePools
```

Nombres cerrados:

```text
PlanetTriangleBudgetProfile
PlanetTrianglePoolBootstrap
PlanetTrianglePoolRegistry
PlanetTriangleBudget
PlanetTrianglePoolController
PlanetTrianglePoolWriter
PlanetPlayerViewReference
PlanetTrianglePriorityReferenceLab
PlanetTriangleOwnerId
PlanetTriangleArtistId
PlanetTriangleAllocationHandle
PlanetTrianglePoolLab
PlanetTrianglePoolLabEditor
```

### PlanetTriangleBudgetProfile

`ScriptableObject` en Resources.

Responsabilidad:

```text
Guardar artistId uint escrito a mano.
Guardar artistName para inspector/debug.
Guardar presupuesto inicial.
Guardar parametros simples de buckets.
Ser editable desde Unity.
No contener estado runtime.
```

### PlanetTriangleBudget

Dato runtime plano.

Responsabilidad:

```text
Copiar valores del profile.
Validar limites.
Mantener artistId uint runtime.
Ser usado por el controlador sin depender del asset.
```

### PlanetTrianglePoolController

Sistema real.

Responsabilidad:

```text
Inicializar pool.
Exponer artistId.
Mantener freelist.
Mantener buckets de prioridad.
Mantener worstResidentBucket/worstResidentScore.
Procesar requests.
Procesar releases.
Procesar reclamaciones.
Exponer metricas.
Liberar recursos.
```

### PlanetTrianglePoolBootstrap

Sistema real.

Responsabilidad:

```text
Localizar perfiles activos.
Crear un PlanetTrianglePoolController por artista activo.
Registrar cada controller en PlanetTrianglePoolRegistry.
Inicializar Environment y Particles en esta fase.
Permitir anadir artistas nuevos por configuracion.
```

### PlanetTrianglePoolRegistry

Sistema real.

Responsabilidad:

```text
Resolver artistId -> PlanetTrianglePoolController.
Permitir que un painter pida el writer/controller del artista que necesita.
Evitar referencias sueltas a pools concretos por toda la escena.
Exponer diagnostico de artistas activos.
Exponer el ultimo snapshot plano de player/camara para productores que lo necesiten.
Separar player/camara real de fallback de Lab.
No calcular LOD, vision, frustum ni mirada.
```

### PlanetPlayerViewReference

Componente runtime pequeno colocado en la camara del jugador.

Responsabilidad:

```text
Leer posicion world desde la camara o un Transform positionSource.
Leer forward world desde la camara o un Transform viewSource.
Empujar el snapshot a PlanetTrianglePoolRegistry cuando cambie lo suficiente.
Forzar un push inicial al activarse.
No calcular priorityScore.
No decidir paginas, LOD, frustum ni visibilidad.
No depender del Lab.
```

Contrato:

```text
PlayerPositionWorld -> Vector3 world.
PlayerForwardWorld -> Vector3 world normalizado.
PlayerViewVersion -> int monotono por referencia activa.
```

### PlanetTrianglePoolWriter

Wrapper usado por painters/generadores.

Responsabilidad:

```text
Traducir "quiero pintar estos tris" a requests de slots.
Seleccionar el artista correcto o estar ligado a uno.
Pedir slots internamente.
Actualizar la representacion visible del artista.
Guardar diagnosticos internos.
No obligar al caller principal a reaccionar al resultado.
```

### PlanetTrianglePriorityReferenceLab

Componente minimo opcional para pruebas de Lab.

Responsabilidad:

```text
Exponer una posicion world usada solo para calcular priorityScore simple por distancia en pruebas.
No contener logica de locomocion.
No contener logica XR.
No conocer artistas ni presupuestos.
No crear GC al consultar la posicion.
No ser requisito del runtime del planeta.
```

Contrato:

```text
Position -> Vector3 world.
El Lab puede usarlo para fabricar requests con priorityScore.
Los artistas reciben priorityScore, no el componente.
```

### PlanetTriangleOwnerId

Identificador opcional de release/diagnostico.

Responsabilidad:

```text
Agrupar triangulos para operaciones secundarias.
Borrar todo lo que pidio un emisor.
Liberar todo lo de un chunk/lote/sistema si hace falta.
Mostrar metricas de cuanto pidio Water/Terrain/Props u otros emisores.
Quitar tris de un efecto muerto sin esperar a que otra request los robe.
```

Formato:

```text
PlanetTriangleOwnerId = uint.
0 = Anonymous/Untracked.
El ownerId puede venir definido de forma explicita por el sistema que publica.
Si una llamada a Draw no trae ownerId, entra como owner 0.
No se usan strings ni hashes en la ruta caliente.
```

Ejemplo inicial orientativo:

```text
1 = PlanetSurface.
2 = PlanetWater.
3 = EnvironmentProps.
4 = ParticlesFog.
5 = ParticlesBurst.
```

Regla:

```text
Los ownerId solo necesitan ser unicos dentro del artista que los usa.
Si queremos unicidad global para debug, se documentara aparte.
Nadie necesita registrarse previamente para poder llamar a Draw.
maxTrackedOwners limita cuantos owners con nombre/diagnostico se siguen de forma comoda.
Si un owner no esta registrado o se supera el limite de tracking, sus tris pueden entrar como Anonymous/Untracked.
El ownerId no da permisos.
El ownerId no decide material.
El ownerId no crea meshes.
El propietario real de la VRAM es el artista.
```

### PlanetTriangleArtistId

Identificador estable de artista/pool.

Formato:

```text
PlanetTriangleArtistId = uint.
0 = Environment.
1 = Particles.
2 = reservado para el siguiente artista si aparece.
```

Regla:

```text
El artistId se escribe a mano en PlanetTriangleBudgetProfile.
El bootstrap valida que no existan dos profiles activos con el mismo artistId.
El nombre humano del artista es solo debug/inspector.
GPU, buffers y requests usan siempre uint.
```

### PlanetTrianglePoolLab

Modulo aditivo para `PlanetImplementationLab`.

Responsabilidad:

```text
Exponer el profile cargado.
Inicializar/liberar el pool.
Simular requests cerca/lejos.
Saturar el pool.
Mostrar metricas.
Probar reclamacion.
Probar release por owner.
No implementar la logica del pool.
```

### PlanetTrianglePoolLabEditor

CustomEditor nativo.

Responsabilidad:

```text
Botones de Init, Request, Stress, Release y diagnostico.
Mostrar ocupacion por bucket.
Mostrar ocupacion por owner.
Mostrar reclamaciones.
```

## Flujo funcional

Flujo de arranque:

```text
1. El bootstrap localiza perfiles activos en Resources/TrianglePools.
2. Crea un PlanetTrianglePoolController por profile activo.
3. Cada controller copia su PlanetTriangleBudget runtime.
4. Cada controller valida presupuesto.
5. Cada controller preasigna slots.
6. Cada controller inicializa freelist y buckets de prioridad.
7. Cada artista queda ready.
```

Flujo de pintado desde 08:

```text
1. 07 genera triangulos.
2. 08 prepara lote de triangulos a publicar.
3. 08 decide o recibe que esos tris consumen Environment.
4. 08 llama al PlanetTrianglePoolWriter de Environment.
5. Writer pide slots al artista Environment.
6. Environment concede libres o reclama slots de peor prioridad.
7. Writer escribe/pinta solo los slots concedidos.
8. 09 actualiza metricas del artista.
```

Flujo de pool lleno:

```text
1. Request nueva llega sin slots libres suficientes.
2. Calcular score y bucket de la request.
3. Si requestScore es peor o igual que worstResidentScore, denegar/pending inmediato.
4. Buscar buckets de peor prioridad.
5. Reclamar slots desde los buckets peores.
6. Invalidar owner anterior.
7. Reasignar slots al nuevo owner.
8. Si no alcanza, conceder parcial o denegar segun modo.
```

Flujo de release:

```text
1. Owner libera allocation o todos sus slots.
2. 09 retira slots de buckets.
3. 09 los mete en freelist.
4. Version/estado queda actualizado.
5. Metricas bajan.
```

## Gestion de RAM

Reglas:

```text
No crear listas nuevas por request.
No usar LINQ en caminos calientes.
No ordenar el millon de slots por request.
No escanear todos los slots para buscar candidatos.
Preasignar arrays de slots, freelist, buckets y metricas.
Los buckets tienen capacidad/estructura controlada.
```

Datos CPU previstos:

```text
Array de TriangleSlot con totalTriangleBudget elementos por artista.
Freelist preasignada.
Espejo ligero de buckets preasignado para diagnostico/tests si hace falta.
Tabla de owners/allocations.
Cache de worstResidentBucket/worstResidentScore.
Metricas.
```

Datos GPU previstos por artista:

```text
TriangleSlotBuffer.
bucketCountsBuffer.
bucketOffsetsBuffer.
bucketWriteCountersBuffer.
compactedSlotIdsBuffer.
RequestBuffer.
ResultBuffer.
```

Decision inicial de bucket interno:

```text
Usar bucket compactado por GPU cuando la ruta de pintado sea compute.
Patron base: histograma por bucket -> prefix sum/scan -> scatter a arrays contiguos.
Mantener solo un fallback CPU preasignado para tests/validacion si hace falta.
No usar listas enlazadas para la ruta GPU caliente.
No usar heaps en la primera version GPU.
No usar List<T> que crezcan en runtime caliente.
```

Estructura conceptual:

```text
bucketCounts[priorityBucketCount] -> numero de slots ocupados por bucket.
bucketOffsets[priorityBucketCount] -> offset inicial de cada bucket dentro de compactedSlotIds.
bucketWriteCounters[priorityBucketCount] -> contador temporal de escritura por bucket.
compactedSlotIds[totalTriangleBudget] -> slotIds agrupados de forma contigua por bucket.

TriangleSlot.priorityBucket -> bucket actual del slot.
```

Motivo:

```text
Los slots de un mismo bucket quedan contiguos.
Leer candidatos peores desde compactedSlotIds favorece acceso coalesced.
Grid-stride loops permiten procesar buffers grandes sin depender del limite exacto de grupos.
Tree-based reduction / prefix sum encaja con calcular offsets de buckets.
Warp/subgroup shuffle puede optimizar reducciones locales si la plataforma lo soporta bien.
No se recorre el millon de slots en CPU para saber que se puede reclamar.
No se crean objetos ni colecciones por request.
```

Regla:

```text
El bucket es una estructura de prioridad aproximada por score recibido.
No ordena todos los triangulos individualmente.
Para 09 nos basta con saber que un bucket es peor que otro.
La precision fina queda en el productor que calcula priorityScore.
La ruta GPU prioriza memoria contigua/coalesced sobre punteros o listas enlazadas.
```

Pipeline conceptual GPU:

```text
1. Clear bucketCounts/bucketWriteCounters.
2. Kernel BuildBucketHistogram:
   - grid-stride sobre slots vivos.
   - leer priorityBucket.
   - atomic add en bucketCounts[bucket].
3. Kernel PrefixSumBuckets:
   - scan/reduction sobre bucketCounts para producir bucketOffsets.
4. Kernel ScatterSlotsToBuckets:
   - grid-stride sobre slots vivos.
   - bucket = TriangleSlot.priorityBucket.
   - writeIndex = bucketOffsets[bucket] + atomic add(bucketWriteCounters[bucket], 1).
   - compactedSlotIds[writeIndex] = slotId.
5. Reclamacion:
   - empezar por worstResidentBucket.
   - leer rangos contiguos de compactedSlotIds para buckets peores.
```

Regla anti-GC:

```text
Todos los buffers se crean en Init del artista.
Las requests reutilizan buffers persistentes.
C# no crea List<T>, arrays, lambdas, LINQ ni closures por request.
Los contadores temporales se limpian/reusan, no se reallocan.
```

Tecnicas GPU usadas como referencia:

```text
Grid-stride loops para recorrer buffers grandes con kernels flexibles.
Coalesced memory access usando compactedSlotIds contiguo por bucket.
Tree-based reduction / prefix sum para generar bucketOffsets.
Warp/subgroup shuffle solo como optimizacion opcional de reducciones locales.
```

Regla:

```text
Estas tecnicas no cambian el contrato de 09.
Solo definen como hacer barato el bucket/reclaim internamente.
Si una optimizacion complica ownership, invalidacion o release, no entra en la primera version.
```

Regla:

```text
La primera implementacion debe priorizar coste predecible y cero GC en requests repetidas.
```

## Gestion de GPU, CPU y salida visible

09 puede mantener estado CPU ligero para ownership, metricas y diagnostico.

La decision actual de backend visible es:

```text
09 arbitra el presupuesto.
09 puede apoyarse en GPU/compute para procesar buckets, reclamacion y datos pesados cuando toque.
La geometria puede venir de un pipeline GPU/compute anterior.
La salida visible de esta fase es Mesh runtime CPU gestionada por el artista.
El output final que consume Unity para render/colision/debug es CPU Mesh.
El caller no escribe esa Mesh directamente.
```

Separacion esperada:

```text
C# orquesta profiles, artistas, owners, requests y metricas.
GPU/compute puede producir o transformar datos pesados.
El artista decide que triangulos pasan a su Mesh visible.
CPU no debe ordenar ni decidir prioridad recorriendo millones de triangulos por request.
```

Decision inicial:

```text
09 define presupuesto, ownership y estado de slots por artista.
Cada artista posee su salida visible Mesh runtime.
EnvironmentArtist -> Mesh runtime propia de Environment.
ParticlesArtist -> Mesh runtime propia de Particles si este artista necesita salida mesh.
GraphicsBuffer no es el backend visual obligatorio de 09 en esta fase.
```

Regla:

```text
Los recursos visuales de Environment no se mezclan con los de Particles salvo decision futura documentada.
Un artista puede liberar, invalidar o sobrescribir la Mesh/recursos que pertenecen a sus slots.
Un artista no libera recursos de otro artista.
El planeta, agua, rocas o props no escriben directamente en la Mesh gestionada.
```

Implicacion para 08:

```text
08 actual preparaba una Mesh visible de validacion.
Al entrar 09, 08 pasa a emitir datos hacia Draw(meshId, datos[], priority) del artista Environment.
El artista Environment decide que triangulos pinta dentro de su presupuesto.
El artista Environment actualiza su Mesh runtime CPU.
```

Ruta futura posible:

```text
Mover el backend visible de un artista a GraphicsBuffer sigue siendo posible.
No entra como requisito de esta version.
Si se hace, debe mantener el mismo contrato Draw(meshId, datos[], priority) y el mismo control de presupuesto.
No puede obligar al caller a saber si sus tris quedaron visibles.
```

## Materiales y shader de artista

Como la salida visible de esta fase es Mesh runtime CPU, el artista puede usar materiales normales de Unity sobre Mesh.

Regla:

```text
Se puede reutilizar el material/look actual del planeta si espera vertices de Mesh.
No se crea una variante buffer-aware obligatoria en 09.
El material no decide presupuesto.
El material no decide prioridad.
```

Material no es owner:

```text
ownerId responde a quien pidio pintar.
materialId/renderBatch responde a con que material se dibuja.
Un arbol, una roca o la luna pueden escribir en el mismo artista si consumen el mismo presupuesto.
Si necesitan materiales distintos, el artista los agrupa por materialId/render batch.
No se crea un owner distinto solo por cambiar de material.
```

Regla:

```text
Un artista puede tener varios batches de render sobre su salida visible.
Cada batch puede usar su Material normal de Unity.
El presupuesto de tris sigue siendo comun al artista.
```

## Liberacion de recursos

Release debe:

```text
Liberar todos los slots.
Vaciar freelist/buckets.
Limpiar owners/allocations.
Liberar recursos visuales propios del artista.
Marcar estado como no inicializado.
Dejar metricas a cero.
Permitir Release doble.
Permitir Init -> Release -> Init.
```

Regla:

```text
Release por owner no libera todo el pool.
Release por artista libera el pool entero de ese artista.
Release global del sistema libera todos los artistas activos.
```

## Botones de Inspector

Botones esperados en `PlanetTrianglePoolLab`:

```text
Validate Triangle Pool Setup
Init Triangle Pool
Request High Priority Batch
Request Low Priority Batch
Fill Pool
Request Better Than Existing
Refresh Priority Buckets
Show Buckets
Show Owners
Release Owner
Release All Triangle Slots
Capture Pool Metrics
Release Triangle Pool
```

## Pruebas manuales

Pruebas minimas:

```text
Validate detecta profile ausente.
Init crea tantos slots como indique el profile del artista.
Environment inicia con 1M slots si usa el profile inicial.
Particles inicia con su presupuesto configurado.
Request High Priority Batch concede desde freelist.
Request Low Priority Batch concede desde freelist.
Fill Pool deja freeSlotCount = 0.
Request Better Than Existing reclama slots de peor prioridad.
Una request peor que lo existente se deniega o queda parcial con diagnostico.
Release Owner devuelve sus slots a freelist.
Release Triangle Pool deja contadores a cero.
Release doble no rompe.
Init -> Release -> Init funciona.
```

Pruebas de integracion con 08:

```text
08 no pinta directo saltandose el wrapper.
El panel/lab muestra diagnostico si 09 no pinta todo.
Al saturar el pool, un lote de mejor prioridad reemplaza slots peores.
Los triangulos reclamados desaparecen o quedan invalidos visualmente.
```

Pruebas de integracion con el canvas actual de la escena:

```text
Los botones Apply Payload actuales aplican el presupuesto del artista Environment de 09.
Apply Payload 126k deja Environment con 126k tris maximos vivos.
Apply Payload 250k deja Environment con 250k tris maximos vivos.
Apply Payload 500k deja Environment con 500k tris maximos vivos.
Apply Payload 1M deja Environment con 1M tris maximos vivos.
Apply Payload 2M deja Environment con 2M tris maximos vivos si la plataforma/memoria lo permite.
El boton Generate genera el planeta usando 09 como unica via de pintado gestionado.
Generate se ciñe al presupuesto activo del artista Environment.
Si 07/08 producen mas triangulos que el presupuesto activo, 09 pinta como maximo el presupuesto y reclama/deniega segun priorityScore.
El planeta/generador no cambia su geometria ni sabe cuantos triangulos acabaron visibles.
```

## Tests automatizados

Tests EditMode esperados:

```text
PlanetTriangleBudget valida totalTriangleBudget > 0.
PlanetTriangleBudget valida priorityBucketCount > 0.
Init crea exactamente `totalTriangleBudget` slots libres para el artista del profile.
Request con slots libres concede el conteo pedido.
Release devuelve slots a freelist.
Release doble de una allocation no rompe.
Pool lleno + request de mejor prioridad reclama slots peores.
Pool lleno + request de peor prioridad no reclama slots mejores.
Las metricas de granted/denied/reclaimed son correctas.
No hay allocations gestionadas en requests repetidas si se puede medir.
```

Tests PlayMode esperados:

```text
PlanetTrianglePoolLab existe en PlanetImplementationLab cuando se integre.
Validate Triangle Pool Setup no lanza excepcion con profile valido.
Init Triangle Pool no lanza excepcion.
Fill Pool no lanza excepcion con presupuesto de test pequeno.
Request Better Than Existing reclama slots esperados.
Release Triangle Pool no lanza excepcion.
Release doble no lanza excepcion.
```

Regla:

```text
Los tests automaticos no necesitan usar 1M slots.
Pueden usar presupuestos pequeños de test para validar la politica.
```

## Metricas

Metricas iniciales:

```text
artistId.
totalTriangleBudget.
freeTriangleSlots.
usedTriangleSlots.
requestedTriangleCount.
grantedTriangleCount.
deniedTriangleCount.
reclaimedTriangleCount.
allocationCount.
ownerCount.
priorityBucketCount.
slotsByBucket.
slotsByOwner.
worstResidentBucket.
worstResidentScore.
lastRequestMs.
lastReleaseMs.
lastBucketRefreshMs.
lastDiagnostic.
```

Metricas diferidas a 10/11:

```text
Calidad visual por reparto interno.
Triangulos redistribuidos por paginas LOD adaptativas.
Triangulos no publicados porque el productor los descarta por frustum/mirada.
Triangulos no publicados porque el productor descarta u oculta zonas por oclusion.
Coste de calcular priorityScore avanzado en productores como 10.
```

## Riesgos

Riesgos principales:

```text
Convertir 09 en un sistema de LOD.
Convertir 09 en el sistema que decide cuando actualizar props, mundo o VFX.
Escanear 1M slots cada vez que llega una request.
Actualizar el peor residente recorriendo todos los slots por request.
Pedir triangulos de uno en uno desde 08.
No invalidar correctamente los slots reclamados.
Dejar que 08 pinte por fuera del pool.
Mezclar presupuestos de artistas distintos sin querer.
Hardcodear Environment y Particles de forma que crear otro pool requiera tocar core.
Confundir capacidad de Mesh temporal de 08 con presupuesto real de un artista.
Crear GC en requests frecuentes.
Hacer depender el pool de Labs o de escena debug.
Copiar millones de triangulos por CPU en vez de publicar mediante recursos controlados por artista.
```

Mitigaciones:

```text
Buckets de prioridad.
Early-out con worstResidentBucket/worstResidentScore.
Requests por batch.
Handles con version.
Owners claros.
Metricas por owner y bucket.
Wrapper obligatorio para pintar.
artistId explicito en profile/request/writer.
Perfiles clonables en Resources/TrianglePools.
Tests de pool lleno.
Separar 09 de 10/11.
Diagnostico interno cerrado: X solicitados, Y pintados, Y denegados/reclamados.
CPU solo como espejo ligero de control.
```

## Decisiones cerradas

```text
09 define artistas/pools de triangulos configurables.
Cada artista tiene su presupuesto independiente.
Los dos artistas iniciales son Environment y Particles.
Environment arranca con 1M tris.
Particles arranca con 100k tris.
Crear artistas pequenos adicionales debe ser una operacion de configuracion.
El presupuesto se configura en ScriptableObjects simples cargados desde Resources/TrianglePools.
Al arrancar, el bootstrap reserva/prepara un pool por profile activo.
El namespace inicial es MarchingCubesPlanet.TrianglePools.
El profile se llama PlanetTriangleBudgetProfile.
El bootstrap se llama PlanetTrianglePoolBootstrap.
El registry se llama PlanetTrianglePoolRegistry.
La referencia de prioridad de Lab se llama PlanetTrianglePriorityReferenceLab.
PlanetTrianglePriorityReferenceLab puede vivir en el Player de la escena solo para fabricar priorityScore de pruebas.
La referencia runtime de player/camara para 10 se llama PlanetLodAgent.
PlanetLodAgent vive preferentemente en el jugador, con fuente de posicion en el jugador y fuente de mirada en la camara.
PlanetPlayerViewReference queda como fallback legacy mientras existan escenas antiguas.
PlanetTrianglePoolRegistry puede exponer ese snapshot plano a productores como 10.
El snapshot de player/camara no convierte a 09 en sistema de LOD, frustum, vision ni mirada.
Las requests de pintado a 09 reciben priorityScore como dato plano, no camara, XR Rig, Transform ni GameObject en GPU.
meshId identifica la publicacion gestionada que el artista debe crear, actualizar o reemplazar.
artistId es uint escrito a mano en el SO; 0 = Environment, 1 = Particles, 2 queda para el siguiente artista.
ownerId es uint opcional para release/diagnostico; 0 = Anonymous/Untracked.
Nadie necesita ownerId registrado para llamar a Draw.
El propietario real de slots, buffers y VRAM es el artista, no el ownerId.
El bucket interno usa histograma + prefix sum/scan + scatter a arrays contiguos en la ruta GPU.
La ruta caliente no genera GC.
worstResidentScore es aproximado por bucket en 09.
La unica prioridad de 09 es el priorityScore recibido.
Refresh Priority Buckets reconstruye buckets en GPU cuando entran scores nuevos o antes de reclaim si estan stale.
La invalidacion usa versionado interno y cola compacta de eventos; no callbacks por triangulo ni polling CPU masivo.
La salida visible inicial de 09 es Mesh runtime CPU gestionada por el artista.
El material del artista puede usar el shader/material actual de Mesh.
GraphicsBuffer queda como backend visual futuro opcional, no como requisito de esta fase.
09 no cambia el poligonaje de ninguna geometria.
09 solo reparte slots dentro de cada artista.
09 no decide cuando una geometria debe volver a publicar.
09 se limita a Draw(meshId, datos[], priority): intenta pintar dentro del presupuesto y guarda diagnostico interno.
09 no calcula vision, mirada, frustum, oclusion ni LOD del planeta.
El planeta/generador no necesita retener handles para saber si algo se pinto.
El handle de asignacion es interno del artista: artistId, ownerId, allocationId, slotListOffset, slotCount, version.
08 debe publicar/pintar a traves del wrapper del artista correspondiente.
Si hay slots libres, se conceden.
Si no hay slots libres, se buscan slots de peor prioridad que la nueva request.
La busqueda de reclamacion no escanea todo el pool: usa buckets/anillos de prioridad.
El pool mantiene worstResidentBucket/worstResidentScore para rechazar peticiones peores sin recorrer buckets.
Los slots reclamados invalidan al owner anterior.
Cada artista puede liberar, invalidar o sobrescribir los recursos visuales propios de sus slots.
10 queda reservado para reparto adaptativo de detalle interno, mirada, frustum local del planeta y ciclo de vida activo.
11 queda reservado para senales auxiliares de visibilidad/oclusion reutilizables, sin autoridad directa sobre slots ni pintado.
```

## TBD

```text
Sin TBD abiertos para empezar implementacion de 09.
```

## Criterio de cierre

09 queda listo para implementar cuando aceptemos este contrato:

```text
Existen presupuestos fijos configurables por artista.
Environment y Particles pueden arrancar como pools separados.
Crear un artista pequeno adicional no requiere cambiar el core.
Cada controlador reserva/prepara su pool al arrancar.
Existe priorityScore en cada request real o una referencia explicita equivalente solo para labs.
Los artistas reciben priorityScore como dato plano para arbitrar capacidad.
08 publica triangulos llamando a Draw(meshId, datos[], priority) del artista Environment.
Los botones Apply Payload del canvas actual aplican el presupuesto real de 09.
El boton Generate del canvas actual genera el planeta usando 09.
Generate no puede pintar mas triangulos que el presupuesto activo del artista Environment.
Un painter de particulas publica triangulos llamando a Draw(meshId, datos[], priority) del artista Particles.
El pool concede slots libres.
Cuando esta lleno, puede reclamar slots de peor prioridad.
La reclamacion usa buckets/anillos de prioridad y no escanea 1M slots por request.
Las peticiones peores que el peor residente se rechazan con early-out.
Los owners pierden slots de forma detectable mediante invalidacion/version.
Release por owner, Release por artista y Release global funcionan.
Metricas muestran libres, usados, concedidos, denegados y reclamados.
09 no genera triangulos, no optimiza detalle, no inicia actualizaciones y no hace visibilidad.
El handle de asignacion queda como estructura interna/lab/debug, no como obligacion del caller principal.
```
