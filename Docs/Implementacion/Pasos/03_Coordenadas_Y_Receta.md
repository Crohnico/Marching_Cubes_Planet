# 03 - Coordenadas y receta

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

## Objetivo

Definir la base de coordenadas y la receta minima del planeta antes de generar forma, proxy, chunks o persistencia.

Este documento existe para evitar ambiguedades entre:

```text
coordenadas logicas de generacion
coordenadas de mundo Unity
coordenadas globales del sistema estelar
escala visual
radio de grid
radio visual derivado
datos propios del planeta
posicion del planeta en el sistema estelar
```

La regla central:

```text
El planeta se calcula en GridCoordinates.
El planeta se monta/renderiza en WorldSpaceCoordinates.
WorldRadius no se guarda como verdad independiente.
WorldRadius = GridRadius * WorldScale.
```

## Alcance de esta fase

Entra:

```text
PlanetRecipe minima.
PlanetPlacement separado de PlanetRecipe.
GridCoordinates.
WorldSpaceCoordinates.
WorldScale.
GridRadius.
WorldRadius derivado.
Conversiones Grid <-> World.
Validaciones de Inspector.
Tests de conversion.
Tests de determinismo de datos base.
```

Este documento prepara el suelo para:

```text
Forma de planeta GPU.
Proxy lejano.
Payload de triangulos.
Chunks locales.
Terraformado.
Colisiones.
Persistencia.
```

## Fuera de alcance

No entra:

```text
Forma procedural completa.
Definir PlanetPreset final.
Definir familias de planeta.
Voronoi esferico.
Ruido de superficie.
Marching Cubes.
Cuevas.
Minerales/sustancias.
Streaming.
Floating origin completo.
Implementar FloatingOriginSystem.
Definir umbrales de recenter.
Definir fade/recenter VR.
Definir sincronizacion multiplayer del origin offset.
Coordenadas astronomicas finales.
Persistencia final en disco.
```

La receta no debe convertirse todavia en el sistema completo de planeta.

## Relacion con otros documentos

Documentos base:

```text
Docs/Definicion_Tecnica_Proyecto.md
Docs/Teoria_Implementacion.md
Docs/Implementacion/Pasos_de_Implementacion.md
Docs/Implementacion/Calculo_Funcional_Datos_Planeta.md
Docs/Implementacion/Pasos/01_PlanetImplementationLab.md
Docs/Implementacion/Pasos/02_ComputeShaderLab.md
```

Documentos que dependen de este:

```text
04_Gestion_RAM_VRAM
05_Quest3_Player_Setup
06_Forma_Planeta_GPU
07_Proxy_Planeta_Lejano
08_Payload_Triangulos
09_Estados_Planeta
10_Chunks_Locales
15_Terraformado
16_Persistencia
```

## Datos de entrada

Valores iniciales de autoria:

```text
GridRadius = 1000
WorldScale = 4
Seed = valor determinista
IsoLevel = 0
```

Valores derivados:

```text
WorldRadius = GridRadius * WorldScale
GridDiameter = GridRadius * 2
WorldDiameter = WorldRadius * 2
```

Ejemplo:

```text
GridRadius = 1000
WorldScale = 4

GridDiameter = 2000 cells
WorldRadius = 4000 unidades Unity
WorldDiameter = 8000 unidades Unity
```

## Datos de salida

El sistema debe producir:

```text
Receta valida.
Placement valido.
WorldRadius derivado.
Conversion Grid -> World.
Conversion World -> Grid.
Conversion GridCell -> World bounds.
Valores de debug legibles en Inspector.
Errores de validacion claros.
```

No debe producir:

```text
Meshes.
Buffers GPU.
RenderTextures.
Chunks.
Datos persistidos.
```

## Conceptos

### GridCoordinates

Coordenadas logicas de generacion.

Representan el espacio donde vive el planeta procedural.

```text
Micro cell logica = 1x1x1 en GridCoordinates.
Macro cell logica = 4x4x4 en GridCoordinates.
```

La escala visual no cambia el tamaño logico de una celda.

Con:

```text
GridRadius = 1000
WorldScale = 4
```

seguimos teniendo:

```text
Micro cell logica = 1 grid unit
Macro cell logica = 4 grid units por lado
```

Pero al montarlo en mundo:

```text
1 grid unit -> 4 unidades Unity
4 grid units -> 16 unidades Unity
```

Tipos iniciales:

```csharp
public readonly struct GridCellCoordinates
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;
}
```

`GridCellCoordinates` representa una celda discreta. Es el dato que se usa para:

```text
indices.
chunks.
patches.
terraformado.
persistencia.
consultas de celda.
```

Cuando una posicion continua se convierte a celda, se usa `floor`, no `round`:

```text
GridPosition(73.25, 99.99, 13.2) -> GridCellCoordinates(73, 99, 13)
```

Decision cerrada:

```text
GridPosition -> GridCellCoordinates siempre usa floor matematico.
No se permite cast directo a int para convertir posicion continua a celda.
```

Motivo:

```text
El cast directo a int trunca hacia cero.
En positivos parece funcionar, pero en negativos asigna mal las celdas cerca del cero.
```

Regla:

```text
La celda contiene el rango [X, X+1), [Y, Y+1), [Z, Z+1).
```

Convencion:

```text
GridCellCoordinates identifica la esquina minima de la celda.
El centro representativo de la celda se deriva como X+0.5, Y+0.5, Z+0.5.
El maximo de la celda se deriva como X+1, Y+1, Z+1.
```

No se almacena el centro de cada celda. Se calcula cuando hace falta.

Ejemplo:

```text
GridCellCoordinates = 10,33,65

gridMin    = 10,33,65
gridCenter = 10.5,33.5,65.5
gridMax    = 11,34,66
```

Con:

```text
WorldScale = 4
planetWorldCenter = 0,0,0
```

se deriva:

```text
worldMin    = 40,132,260
worldCenter = 42,134,262
worldMax    = 44,136,264
```

Por tanto:

```text
73.00 pertenece a la celda 73.
73.99 pertenece a la celda 73.
74.00 pertenece a la celda 74.
```

La misma regla aplica a coordenadas negativas:

```text
-0.01 pertenece a la celda -1.
-0.99 pertenece a la celda -1.
-1.00 pertenece a la celda -1.
-1.01 pertenece a la celda -2.
```

Esto mantiene rangos consistentes:

```text
cell -2 = [-2, -1)
cell -1 = [-1,  0)
cell  0 = [ 0,  1)
cell  1 = [ 1,  2)
```

### GridPosition

Posicion continua dentro del grid.

No es el dato base para persistencia ni para indexar celdas.

Se usa cuando un punto puede caer entre celdas:

```text
vertices interpolados.
sampling de densidad.
posicion del jugador convertida a grid.
rayos y queries.
normales.
interpolacion de Marching Cubes.
```

Regla:

```text
GridPosition se convierte a GridCellCoordinates con floor.
```

### WorldSpacePosition

Posicion continua en Unity/mundo.

No se almacena como dato base si se puede derivar.

```text
worldPosition = planetWorldCenter + gridPosition * WorldScale
```

Ejemplo:

```text
GridCellCoordinates = 10,33,65
WorldScale = 4
planetWorldCenter = 0,0,0

WorldSpacePosition minima de la celda = 40,132,260
WorldSpacePosition centro de la celda = 42,134,262
```

### WorldSpaceCoordinates

Coordenadas de Unity/mundo donde se renderiza, se coloca la escena y se mueve el jugador.

Son coordenadas locales al origen activo de Unity. No representan por si solas la escala astronomica del sistema estelar.

La conversion base:

```text
worldPosition = planetWorldCenter + gridPosition * WorldScale
```

La conversion inversa:

```text
gridPosition = (worldPosition - planetWorldCenter) / WorldScale
```

Decision cerrada para el primer bloque:

```text
WorldSpacePosition usa float / Vector3.
```

Motivo:

```text
Unity renderiza, transforma meshes y trabaja con Transform en float.
Compute Shader y GPU tambien trabajan principalmente en float.
El primer bloque se valida con el planeta cerca del origen activo.
```

### StellarSpaceCoordinates

Coordenadas globales del sistema estelar.

Representan posiciones grandes:

```text
centros de planetas.
naves lejos del origen activo.
orbitas.
distancias astronomicas.
```

Decision cerrada:

```text
StellarSpacePosition usa double / double3.
```

Motivo:

```text
Las distancias del sistema estelar pueden ser demasiado grandes para mantener precision estable en float.
Estas coordenadas no se usan directamente para render.
Primero pasan por un sistema de origen activo o floating origin.
```

Flujo conceptual futuro:

```text
StellarSpacePosition double
-> FloatingOrigin / origen activo
-> WorldSpacePosition float
-> GridPosition float si estamos dentro o cerca de un planeta
-> GridCellCoordinates int si necesitamos celda
```

Regla:

```text
WorldSpaceCoordinates no significa coordenadas astronomicas.
WorldSpaceCoordinates es mundo local Unity.
StellarSpaceCoordinates es espacio global del sistema estelar.
```

### FloatingOriginOffset

Desplazamiento entre el espacio global del sistema estelar y el mundo local de Unity.

El mundo debe tener conciencia explicita de este desplazamiento:

```text
WorldSpacePosition = StellarSpacePosition - FloatingOriginOffset
```

La primera forma prevista de exponer esta conversion sera una clase propia del proyecto:

```csharp
public static class UniversePositionConverter
{
    public static UniversePosition CurrentOriginOffset { get; private set; }

    public static UniversePosition LocalToUniversePosition(Vector3 localPosition)
    {
        return CurrentOriginOffset + localPosition;
    }

    public static Vector3 UniverseToLocalPosition(UniversePosition universePosition)
    {
        return (Vector3)(universePosition - CurrentOriginOffset);
    }

    public static void SetOrigin(UniversePosition newOrigin)
    {
        CurrentOriginOffset = newOrigin;
    }

    public static void RecenterAt(UniversePosition universePosition)
    {
        CurrentOriginOffset = universePosition;
    }
}
```

Reglas de esta clase:

```text
CurrentOriginOffset usa UniversePosition, no Vector3.
La clase no depende de Transform.
Se le puede pasar transform.position o cualquier Vector3 local crudo.
Devuelve coordenadas globales en precision double.
```

Los nombres exactos pueden cambiar al implementar, pero la idea no:

```text
Local Unity float -> Universe double.
Universe double -> Local Unity float.
```

El objetivo no es mover la verdad del universo. El objetivo es mantener al jugador cerca del `0,0,0` local de Unity para conservar precision en `float`.

Regla:

```text
Los datos persistentes, recetas, patches y coordenadas logicas no se reescriben cuando cambia el origen.
Solo se recalcula la posicion local de las representaciones vivas.
```

Esto implica:

```text
No mover buffers gigantes de datos logicos.
No regenerar el planeta entero por cambiar el origen.
No cambiar GridCellCoordinates por mover el origen activo.
No usar Transform.position como autoridad logica.
```

Los objetos visibles se colocan usando:

```text
worldPosition = stellarPosition - activeOrigin
```

Para un planeta:

```text
planetWorldCenter = planetStellarPosition - activeOrigin
worldPosition = stellarPosition - activeOrigin
gridPosition = (worldPosition - planetWorldCenter) / WorldScale
```

Conceptualmente el `activeOrigin` se cancela:

```text
gridPosition = (stellarPosition - planetStellarPosition) / WorldScale
```

Por tanto:

```text
GridPosition depende de la posicion relativa al planeta.
GridPosition no debe depender del origen activo de Unity.
```

En el primer bloque:

```text
FloatingOriginOffset = 0
El planeta vive cerca del origen.
No se implementa recenter real.
```

Para navegacion espacial futura:

```text
El jugador puede mantenerse cerca de 0,0,0 en Unity.
El universo visible se recoloca relativo al origen activo.
Los sistemas logicos siguen trabajando en StellarSpacePosition o coordenadas relativas.
```

Nota VR:

```text
Un recenter brusco puede marear o generar un salto perceptible en VR.
```

Si el cambio de origen produce artefactos visibles, debe ocultarse con una transicion controlada:

```text
parpadeo/fade corto.
transicion durante cambio de estado.
momento sin contacto fisico critico.
ventana donde no haya input delicado.
```

Regla VR:

```text
No hacer recenters frecuentes, continuos o visibles durante interaccion fina.
El recenter debe ser raro, presupuestado y testeado en dispositivo.
```

Multijugador:

```text
WorldSpacePosition local no sera autoridad de red.
Cada cliente puede tener su propio FloatingOriginOffset.
La autoridad debe vivir en StellarSpacePosition o coordenadas logicas equivalentes.
```

Documento futuro:

```text
FloatingOriginSystem / Sistema Estelar definira:
- umbrales de recenter.
- eventos de cambio de origen.
- objetos vivos registrados.
- fade o transicion VR.
- integracion multiplayer.
```

### PlanetRecipe

Define como es el planeta.

No define donde esta colocado.

Tampoco define por si sola una familia completa de planetas. La receta representa un planeta concreto.

Responsabilidad:

```text
GridRadius.
WorldScale.
Seed.
IsoLevel.
Parametros minimos de superficie cuando toque.
Version de receta.
```

Regla:

```text
PlanetRecipe es fuente de verdad procedural.
PlanetRecipe no guarda WorldRadius como dato independiente.
PlanetRecipe no guarda posicion en el sistema estelar.
PlanetRecipe no guarda mallas, buffers ni caches.
```

Relacion con presets:

```text
PlanetPreset define una familia o intencion procedural.
PlanetRecipe define un planeta concreto instanciado desde seed, preset y parametros.
```

Ejemplos futuros de `PlanetPreset`:

```text
Luna / roca con crateres.
Planeta sin agua.
Planeta sin atmosfera.
Planeta acuoso.
Planeta solo agua.
Planeta gaseoso.
Planeta toxico.
Otros perfiles futuros.
```

Regla:

```text
El detalle de PlanetPreset no se define en este documento.
Este documento solo reserva la separacion conceptual entre preset y receta.
```

El documento futuro de generacion de planetas definira:

```text
Campos de PlanetPreset.
Como un preset afecta a superficie, agua, atmosfera, cuevas y sustancias.
Como se combina preset + seed + parametros para crear PlanetRecipe.
```

### PlanetPlacement

Define donde esta colocado el planeta en el sistema o escena.

Responsabilidad:

```text
planetWorldCenter.
rotacion si aplica.
escala adicional no permitida por defecto.
estado de montaje en escena.
```

Regla:

```text
La escala del planeta viene de WorldScale.
No se debe añadir una escala de Transform adicional para cambiar el tamaño real del planeta.
```

Si se usa `Transform.localScale` por necesidad tecnica temporal, debe documentarse como excepcion y no como fuente de verdad.

## Componentes/scripts previstos

### PlanetRecipe

Dato real del motor.

Responsabilidad:

```text
Guardar valores de autoria del planeta.
Exponer valores derivados.
Validar rangos.
Ser serializable.
No depender de MonoBehaviour.
No depender del Lab.
```

Decision inicial:

```text
PlanetRecipe sera struct serializable de datos puros.
No sera ScriptableObject en esta fase.
```

Motivo:

```text
La receta debe poder venir de generacion procedural, guardado en disco o Inspector del Lab.
No queremos atarla todavia a assets Unity.
```

### PlanetPlacement

Dato real del motor para colocar una receta.

Responsabilidad:

```text
Guardar centro en WorldSpaceCoordinates.
Convertir puntos usando PlanetRecipe.
No modificar la receta.
```

### PlanetCoordinateConverter

Sistema real de conversion.

Responsabilidad:

```text
GridToWorld.
WorldToGrid.
GridCellToWorldBounds.
GridCellToGridCenter.
GridCellToWorldCenter.
WorldDistanceToGridDistance.
GridDistanceToWorldDistance.
```

Debe ser codigo puro y testeable.

Regla de precision:

```text
GridPosition continua usa float.
WorldSpacePosition local usa float / Vector3.
StellarSpacePosition global usa double / double3.
```

`PlanetCoordinateConverter` del primer bloque no necesita resolver `StellarSpacePosition`. Esa conversion quedara para el sistema de floating origin.

Cuando exista `FloatingOriginOffset`, cualquier conversion que parta de `WorldSpacePosition` debe saber si esa posicion ya esta desplazada a mundo local. No se debe mezclar una posicion global con una posicion local sin pasar por el convertidor correspondiente.

### GridCellQuery

Sistema real para pedir conjuntos de celdas por volumen.

Responsabilidad:

```text
Resolver consultas espaciales en GridCoordinates.
Recibir centro y radio en GridPosition/GridDistance.
Escribir resultados en un buffer preasignado.
Devolver count.
No crear listas nuevas en runtime caliente.
```

Consulta inicial:

```text
Sphere(center, radius, mode, resultBuffer) -> resultCount
```

Modos iniciales:

```text
CenterInside
Intersects
FullyContained
```

Regla:

```text
CenterInside usa el centro de celda.
Intersects usa test conservador celda/esfera.
FullyContained exige que la celda completa quede dentro de la esfera.
```

Primera pasada de rango:

```text
minCell = floor(center - radius)
maxCell = floor(center + radius)
```

Despues se recorre ese AABB discreto y se filtra segun el modo.

Para `CenterInside`:

```text
cellCenter = cell + 0.5
distance(cellCenter, center) <= radius
```

Para `Intersects`, la opcion inicial puede ser conservadora:

```text
distance(cellCenter, center) <= radius + cellHalfDiagonal
```

Para una micro cell 1x1x1:

```text
cellHalfDiagonal = sqrt(3) * 0.5
```

Mas adelante se podra cambiar por test exacto AABB/esfera si aporta precision:

```text
closestPoint = clamp(sphereCenter, cellMin, cellMax)
distance(closestPoint, sphereCenter) <= radius
```

Regla:

```text
En colision, streaming, terraformado y visibilidad conservadora, preferir Intersects.
En herramientas simples o debug, CenterInside puede ser suficiente.
```

Si el buffer de salida se queda corto:

```text
No se crea memoria nueva automaticamente.
Se devuelve count escrito.
Se marca overflow.
El Inspector/Lab muestra diagnostico claro.
```

### PlanetRecipeValidator

Validador de datos.

Responsabilidad:

```text
Detectar GridRadius invalido.
Detectar WorldScale invalido.
Detectar Seed no inicializada si se decide exigirlo.
Detectar IsoLevel invalido si aplica.
Generar mensaje legible para Inspector/Lab.
```

### PlanetRecipeLab

Script aditivo para probar `PlanetRecipe`, `PlanetPlacement` y `PlanetCoordinateConverter`.

Responsabilidad:

```text
Exponer receta de prueba en Inspector.
Mostrar valores derivados.
Probar conversiones.
Ejecutar validaciones.
Ejecutar tests manuales de rango.
No contener logica principal.
```

Regla:

```text
PlanetRecipeLab depende de PlanetRecipe.
PlanetRecipe no depende de PlanetRecipeLab.
```

### PlanetRecipeLabEditor

`CustomEditor` nativo para botones del Lab.

Responsabilidad:

```text
Validate Recipe.
Reset Demo Recipe.
Convert Grid To World.
Convert World To Grid.
Run Conversion Smoke Test.
Run Boundary Test.
```

## Flujo funcional

Flujo minimo:

```text
1. Crear PlanetRecipe demo.
2. Validar receta.
3. Crear PlanetPlacement demo.
4. Calcular WorldRadius derivado.
5. Convertir punto Grid -> World.
6. Convertir punto World -> Grid.
7. Validar que Grid -> World -> Grid conserva valor dentro de tolerancia.
8. Mostrar resultados en Inspector.
```

Flujo hacia sistemas futuros:

```text
PlanetRecipe + PlanetPlacement
-> PlanetCoordinateConverter
-> GridCellQuery
-> Forma GPU
-> Proxy
-> Chunks locales
```

## Gestion de RAM

Esta fase no debe crear datos grandes.

Reglas:

```text
Los datos son structs/clases pequeñas.
No crear listas grandes.
No crear buffers por frame.
No generar strings por frame.
Los mensajes de validacion se generan bajo demanda.
```

La receta debe ser barata de copiar o pasar por referencia controlada.

Decision:

```text
PlanetRecipe sera struct serializable editable para Inspector/Lab.
No sera readonly struct como tipo editable inicial.
En runtime, cuando se pase a funciones calientes, se preferira pasarla como in PlanetRecipe o ref readonly si aporta valor.
```

Motivo:

```text
Unity serializa y edita mejor datos simples y mutables en Inspector.
La receta es pequeña en esta fase.
La proteccion contra escrituras accidentales se hara por convencion, validadores y separacion entre Recipe y Placement.
Si mas adelante aparece una variante readonly para runtime puro, debe tener los mismos campos y no cambiar la fuente de verdad.
```

## Gestion de VRAM

No aplica en esta fase.

No se crean:

```text
GraphicsBuffer.
ComputeBuffer.
Mesh.
RenderTexture.
Texture runtime.
```

La receta se enviara a GPU en documentos posteriores, especialmente en:

```text
06_Forma_Planeta_GPU
```

## Liberacion de recursos

No hay recursos pesados que liberar.

Regla:

```text
No se debe introducir Release artificial si no hay recursos vivos.
```

El Lab puede tener `Reset` para limpiar valores de prueba, pero no se considera liberacion de RAM/VRAM.

## Botones de Inspector

Botones esperados en `PlanetRecipeLab`:

```text
Validate Recipe
Reset Demo Recipe
Show Derived Values
Convert Grid To World
Convert World To Grid
Run Conversion Smoke Test
Run Boundary Test
Run Invalid Values Test
```

Valores visibles:

```text
GridRadius
WorldScale
WorldRadius derivado
GridDiameter derivado
WorldDiameter derivado
Seed
IsoLevel
planetWorldCenter
ultimo resultado de conversion
ultimo resultado de query espacial
ultimo diagnostico
```

Botones adicionales para `GridCellQuery`:

```text
Run Sphere Cell Query
Change Query Mode
Show Query Bounds
Clear Query Buffer
Run Query Overflow Test
```

## Pruebas manuales

Pruebas minimas:

```text
Receta demo valida.
GridRadius 1000 y WorldScale 4 producen WorldRadius 4000.
GridPosition zero se convierte al centro del planeta.
GridPosition (1000,0,0) se convierte a center + (4000,0,0).
WorldPosition vuelve a GridPosition dentro de tolerancia.
GridRadius invalido se detecta.
WorldScale invalido se detecta.
IsoLevel demo es 0.
```

Pruebas de borde:

```text
Punto en superficie positiva X.
Punto en superficie negativa X.
Punto en centro.
Punto fuera del radio.
Conversion de distancia Grid -> World.
Conversion de distancia World -> Grid.
Conversion de GridPosition positivo a GridCellCoordinates con floor.
Conversion de GridPosition negativo a GridCellCoordinates con floor.
Cruce por cero: -0.01 -> cell -1 y 0.01 -> cell 0.
```

Pruebas futuras de origen activo:

```text
Con FloatingOriginOffset = 0, WorldSpacePosition coincide con StellarSpacePosition local.
Con FloatingOriginOffset != 0, WorldSpacePosition cambia pero GridPosition relativa al planeta se mantiene.
Cambiar activeOrigin no cambia GridCellCoordinates del jugador respecto al planeta.
Recenter no reescribe receta, patches ni datos persistentes.
```

Pruebas de query espacial:

```text
Consulta esfera con CenterInside devuelve celdas cuyo centro cae dentro.
Consulta esfera con Intersects no pierde celdas que tocan el volumen.
Consulta esfera con FullyContained solo devuelve celdas completas.
Consulta desde WorldSpacePosition se convierte primero a GridPosition.
Consulta con buffer pequeño marca overflow sin crear memoria nueva.
```

## Tests automatizados

Tests EditMode esperados:

```text
WorldRadius = GridRadius * WorldScale.
WorldDiameter = GridRadius * 2 * WorldScale.
GridToWorld y WorldToGrid son inversas dentro de tolerancia.
GridDistanceToWorldDistance multiplica por WorldScale.
WorldDistanceToGridDistance divide por WorldScale.
GridRadius <= 0 invalida receta.
WorldScale <= 0 invalida receta.
PlanetRecipe no guarda WorldRadius como campo editable.
PlanetPlacement no modifica PlanetRecipe.
GridPosition a GridCellCoordinates usa floor.
GridPosition negativa a GridCellCoordinates usa floor, no truncado hacia cero.
GridCellCoordinates deriva min, center y max correctamente.
GridCellQuery CenterInside devuelve resultados deterministas.
GridCellQuery Intersects es conservador.
GridCellQuery no asigna memoria nueva en camino caliente.
```

Tests PlayMode esperados:

```text
PlanetRecipeLab existe en PlanetImplementationLab cuando se integre.
Validate Recipe no lanza excepcion.
Reset Demo Recipe deja valores esperados.
Botones de conversion no crean recursos vivos.
```

## Metricas

Metricas iniciales:

```text
Tiempo de validacion.
Numero de errores de validacion.
Resultado de conversion.
Tiempo de conversion Grid <-> World.
Tiempo de conversion Stellar -> World cuando exista.
Tiempo de query espacial.
Numero de celdas devueltas por query.
Overflow de buffers de query.
Allocations durante pruebas si se puede medir.
```

No se miden en esta fase:

```text
VRAM.
Triangulos.
Buffers GPU.
Frame time de render.
```

## Riesgos

Riesgos principales:

```text
Confundir GridRadius con WorldRadius.
Guardar WorldRadius como dato editable.
Usar Transform.localScale como fuente de verdad.
Mezclar receta procedural con placement.
Usar coordenadas float para todo y bloquear floating origin futuro.
Meter persistencia antes de cerrar los datos base.
Usar CenterInside donde hace falta Intersects y perder celdas cercanas.
Crear listas nuevas para consultas espaciales frecuentes.
Que el cambio de origen activo cambie coordenadas logicas por error.
Mover o reescribir datos gigantes al hacer recenter.
Provocar salto perceptible o mareo en VR al recolocar el mundo.
Usar WorldSpacePosition local como autoridad de red en multijugador.
```

Mitigaciones:

```text
WorldRadius siempre derivado.
PlanetRecipe separado de PlanetPlacement.
Conversiones centralizadas.
Consultas espaciales centralizadas.
Salida de queries con buffer preasignado + count.
FloatingOriginOffset explicito.
Datos logicos separados de representaciones vivas.
Recenter raro, controlado y testeado en VR.
Autoridad de red en StellarSpacePosition o coordenadas logicas, no en WorldSpacePosition local.
Tests de ida/vuelta.
Floating origin completo separado a documento futuro.
```

## TBD

Decisiones abiertas:

```text
No quedan TBD bloqueantes para este documento.
```

Decisiones movidas a documentos futuros:

```text
FloatingOriginSystem / Sistema Estelar:
- formato final de floating origin.
- cuando entra StellarSpacePosition en runtime real.
- como se oculta o suaviza el recenter en VR.
- como se sincroniza floating origin con multijugador.

06_Forma_Planeta_GPU / Generacion de planetas:
- definicion final de PlanetPreset.
- familias de planeta.
- como seed + preset + parametros producen PlanetRecipe.

16_Persistencia:
- formato final de persistencia de PlanetRecipe.
- si PlanetRecipe vive en archivo propio, savegame o generador procedural.
```

Decision inicial no bloqueante:

```text
GridCellCoordinates discretas usan int.
GridCellCoordinates sera readonly struct.
GridPosition continua usa float para GPU y pruebas iniciales.
GridPosition a GridCellCoordinates usa floor matematico tambien en negativos.
WorldSpaceCoordinates en Unity usan Vector3/float durante el primer bloque.
StellarSpaceCoordinates usara double/double3 cuando entre el sistema estelar real.
WorldRadius siempre derivado.
PlanetRecipe empieza como dato puro serializable, no ScriptableObject.
PlanetRecipeLab solo prueba y visualiza.
```

## Criterio de cierre

Este documento queda listo para implementar cuando aceptemos este contrato:

```text
Tenemos nombres claros de coordenadas.
GridRadius y WorldScale estan cerrados como datos de autoria.
WorldRadius es derivado.
GridCellCoordinates tiene convencion clara de min/center/max.
PlanetRecipe no contiene placement.
PlanetPlacement no modifica receta.
Las conversiones son testeables.
Las consultas espaciales basicas son testeables y sin allocations.
El Lab solo prueba el sistema real.
```

No se pasa a `05_Quest3_Player_Setup` ni a `06_Forma_Planeta_GPU` si antes no esta clara la conversion entre grid y mundo.
