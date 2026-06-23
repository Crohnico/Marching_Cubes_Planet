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


## Reglas confirmadas

### StellarSystem

`StellarSystem` es el sistema responsable de descubrir, ordenar e inicializar los planetas que pertenecen a un sistema estelar.

#### Datos propios

`StellarSystem` tiene:

- Una lista privada de los planetas del sistema estelar.
- La lista de planetas esta oculta en el inspector.
- Un `string ID` con el nombre propio del sistema estelar.
- Un valor `startupFarChunkBuildsPerFrame`.
- Un valor `segmentLodChunksBuiltPerFrame`.
- Un boton en el inspector para borrar la carpeta de archivos usada por funcionalidades de debug.

La carpeta debug del sistema se resuelve como:

```text
StellarSystems/{SystemId}
```

Dentro de `Application.persistentDataPath`.

#### Presupuesto de carga

`startupFarChunkBuildsPerFrame` y `segmentLodChunksBuiltPerFrame` definen el estres que `StellarSystem` permite durante la carga de datos de los planetas.

- `startupFarChunkBuildsPerFrame` limita cuantos chunks lejanos/base se construyen por frame en el arranque.
- `segmentLodChunksBuiltPerFrame` limita cuantos chunks de LOD por segmentos se construyen por frame.


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
