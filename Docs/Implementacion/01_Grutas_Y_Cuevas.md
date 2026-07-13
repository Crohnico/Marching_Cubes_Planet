# Grutas y cuevas

## Objetivo

Crear grutas y cuevas deterministas sin precalcular ni almacenar un volumen global
del planeta.

Las cuevas forman parte del planeta local/interactuable, no de la Shell lejana.

## Decision vigente

La Shell no carga cuevas interiores.

Como mucho, la Shell puede representar entradas grandes a cueva como informacion
visual superficial, por ejemplo pintandolas en negro o con una mascara simple.
La Shell no debe resolver la red interior de cuevas.

Las cuevas empiezan a evaluarse cuando el planeta entra en modo Base/local, donde
ya se generan chunks con mas detalle.

## Modelo procedural cerrado

Las cuevas usaran un Voronoi 3D propio, independiente del Voronoi superficial de
continentes/oceanos.

El volumen del planeta se divide en regiones irregulares tipo Voronoi 3D. Cada
region decide de forma determinista si es hueca o solida.

La decision de si una region es hueca o solida se calcula con random basado en
seed, por lo que:

```text
misma receta + misma seed + misma coordenada -> misma respuesta
```

No hace falta precalcular la red completa ni guardarla en memoria.

## Parametros de receta

La receta del planeta necesitara al menos:

```text
porcentajeDeCavidades -> porcentaje de regiones Voronoi que se consideran huecas
radioMedioCavidad     -> radio base de cavidad dentro de su region Voronoi
variacionRadioCavidad -> variacion determinista del radio por region
```

Este porcentaje controla cuantas regiones candidatas se convierten en cueva.

Defaults iniciales:

```text
porcentajeDeCavidades = TBD por preset
radioMedioCavidad = 0.45 * tamanoRegionVoronoi
variacionRadioCavidad = 0.20 * tamanoRegionVoronoi
```

La escala inicial del Voronoi 3D sera:

```text
8 regiones Voronoi por chunk canonico
```

La seleccion de cavidad se calcula con un random determinista `0..1`.

```text
si random <= porcentajeDeCavidades -> region hueca
si random >  porcentajeDeCavidades -> region solida
```

Ejemplo:

```text
porcentajeDeCavidades = 0.8
random = 0.72 -> hueca
random = 0.91 -> solida
```

Esto no garantiza exactamente el porcentaje en una muestra pequena, pero tiende
al valor configurado en poblaciones grandes.

La seed de cada region debe derivarse de:

```text
seed del planeta
x del chunk
y del chunk
z del chunk
indice de Voronoi dentro del chunk: 1..8
```

Entrada conceptual:

```text
seedPlaneta + chunkX + chunkY + chunkZ + voronoiIndex
```

Implementacion recomendada:

```text
hash = seedPlaneta
hash = mix(hash, chunkX)
hash = mix(hash, chunkY)
hash = mix(hash, chunkZ)
hash = mix(hash, voronoiIndex)
random01 = hash normalizado a 0..1
```

`mix` debe ser una mezcla entera barata y determinista. No se busca seguridad ni
hash criptografico; solo evitar patrones obvios y colisiones tontas de una suma
plana.

Importante: aunque la seed use coordenadas de chunk, la evaluacion de densidad
debe ser estable en coordenadas canonicas/globales. Dos chunks vecinos no pueden
obtener respuestas distintas para la misma zona del borde.

El radio final de cada region hueca se calcula de forma determinista con la seed
de la region, aplicando la variacion sobre el radio medio.

## Funcion de densidad

Las cuevas se aplican como modificacion de densidad sobre el campo del planeta.

La forma base de una cavidad debe seguir una formula de densidad donde:

```text
centro de la cavidad -> vacio
laterales            -> solido
```

Esto permite que una region Voronoi no sea simplemente un booleano duro, sino un
volumen con transicion desde el centro hueco hacia paredes solidas.

La cueva resta densidad antes de Marching Cubes. No es geometria aparte.

La Shell puede ignorar esta capa de densidad. Base/local puede activarla.

La curva de transicion de vacio a solido usa `smoothstep`.

Las cuevas no tienen una formula de ruido de pared separada. Usan la misma
formula procedural de la receta del planeta y aplican una resta de densidad sobre
segmentos solidos.

## Union entre regiones huecas

El Voronoi no debe producir aspecto de panel de abeja.

Las fronteras matematicas entre regiones Voronoi no son paredes por defecto. Si
dos regiones contiguas son huecas, la pared entre ambas no debe pintarse.

La densidad de cueva debe evaluarse como campo combinado de cavidades cercanas,
no como "celda actual vacia contra celda vecina solida" de forma dura.

Regla:

```text
region hueca + region hueca -> continuidad de vacio
region hueca + region solida -> pared/transicion
region solida + region solida -> solido
```

Para evitar costuras, cada sample de densidad debe tener en cuenta la region
Voronoi propietaria y las regiones vecinas necesarias. La excavacion resultante
debe usar union/blend de influencias huecas, no paredes en las caras Voronoi.

La forma preferida es radio + falloff por region hueca:

```text
centro de cavidad -> vacio fuerte
radio interno     -> vacio
falloff           -> transicion smoothstep hacia solido
exterior          -> solido salvo influencia de otra cavidad hueca
```

## Capas de generacion local

El presupuesto de Base/local no se reparte con porcentajes fijos entre
superficie, intermedio e interior.

La regla es llenar el buffer por prioridad espacial: desde lo mas cercano al
jugador hacia lo mas lejano, hasta donde quepa.

No se debe asumir que todo el buffer disponible pertenece siempre a terreno
exterior.

Se definen tres zonas de generacion:

```text
Superficie -> terreno exterior visible y caminable
Intermedio -> entradas a cuevas y chunks superficiales de cueva
Interior   -> grutas/cuevas ya por debajo de la superficie
```

Cuando el jugador esta fuera de cuevas se genera:

```text
Superficie + Intermedio
```

Cuando el jugador esta mas cerca del interior que de la superficie se genera:

```text
Interior + Intermedio
```

El Intermedio existe para evitar saltos duros entre mundo exterior e interior.

Solo se evalua la zona cubierta por la ventana local hasta LOD2. Las cuevas fuera
de esa ventana no se precalculan ni se mantienen vivas.

Si el buffer no alcanza para toda la ventana deseada, se conserva lo cercano y se
descarta lo lejano.

La zona Intermedio tiene una profundidad inicial de 2 chunks en Y desde la
superficie.

Tomando como referencia:

```text
y = 0  -> chunk inicial de superficie
y = -1 -> primer chunk interior/intermedio
y = -2 -> segundo chunk interior
```

Cuando el jugador cruza de `y = -1` a `y = -2`, el sistema pasa de generar
`Superficie + Intermedio` a generar `Interior + Intermedio`.

## Nucleo solido

El centro del planeta queda reservado como una esfera perfecta de radio amplio y
siempre solida.

Dentro de esa esfera no pueden generarse cuevas ni grutas.

Esta restriccion tambien debe aplicarse al terraformado: el jugador no debe poder
excavar en direccion al nucleo hasta atravesar esta zona. La forma prevista de
bloquearlo es introducir un sistema de calor que haga inviable terraformar hacia
el centro.

El radio exacto del nucleo solido es:

```text
radioNucleoSolido = radioPlaneta * 0.2
```

## Entradas a superficie

Las entradas a cuevas son conexiones entre cavidades interiores y superficie.

En Shell solo deben aparecer como senal visual si son relevantes desde lejos.
En Base/local deben afectar a la geometria real del chunk.

No hay un generador especial de bocas.

Las entradas aparecen de forma natural cuando una cavidad hueca intersecta un
chunk de superficie donde conviven aire exterior y terreno solido.

Si se forman, se forman. Si no se forman, no se fuerzan.

## Relacion con Voronoi existente

El Voronoi de cuevas es 3D y propio.

El Voronoi de continentes/oceanos puede reutilizar utilidades de hash, ruido o
seed si encajan, pero no debe ser el contrato espacial de cuevas si esta definido
solo sobre superficie esferica.

## Restricciones

```text
No volumen global precalculado.
No buffer global de cuevas.
No dependencia de CPU readback para saber si existe cueva.
Evaluacion por chunk/zona.
Determinismo por seed.
Shell ligera.
Base/local con cuevas reales.
Voronoi 3D propio para cuevas.
Buffer local repartido entre superficie/intermedio/interior.
Sin modo debug como contrato de funcionalidad.
```

## Validacion

No se creara un modo debug de juego como dependencia del sistema.

La validacion debe hacerse con escenas/pruebas controladas, recetas forzadas y
tests automatizables cuando aplique. El debug puede existir como herramienta
temporal de desarrollo, pero no como contrato funcional ni como parte necesaria
del runtime.

Validacion manual aceptada para este bloque:

```text
Jugar/cargar el mismo planeta varias veces con la misma seed.
Comprobar que las mismas coordenadas generan las mismas cavidades.
Comprobar que al moverse entre chunks no aparecen discrepancias visibles en bordes.
```

## Pendiente por definir

```text
Tests de determinismo.
```
