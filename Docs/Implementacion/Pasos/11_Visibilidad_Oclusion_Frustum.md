# 11 - Senales auxiliares de visibilidad, oclusion y frustum

## Objetivo

Definir senales reutilizables de visibilidad que puedan ayudar a los sistemas que generan geometria, sin crear un segundo camino de pintado ni un segundo cerebro de vision para el planeta activo.

Contrato funcional:

```text
estado de camara / volumen / bounds
-> senales auxiliares de visibilidad
-> productores deciden si las usan antes de publicar a 09
```

Regla central:

```text
11 no pinta.
11 no libera slots de 09 directamente.
11 no decide LOD del planeta.
11 no decide que paginas del planeta activo existen.
11 no sustituye el calculo de mirada/frustum local de 10.
```

## Modelo mental

El flujo principal del planeta activo queda:

```text
06 define density(point).
10 decide shell/paginas/resolucion por distancia, mirada, movimiento, frustum local y ciclo de vida.
10 publica lotes con meshId y priorityScore.
09 es el unico backend que pinta y arbitra capacidad.
```

11 puede existir para aportar informacion adicional:

```text
Esta zona parece fuera de frustum global.
Esta zona parece ocluida por un bloque/hemisferio.
Esta zona tiene baja relevancia visual segun una query compartida.
```

Pero esa informacion no actua sola sobre el render.

```text
La senal vuelve al productor.
El productor decide si deja de generar, si baja prioridad o si republica.
La publicacion visible sigue pasando por 09.
```

## Alcance

Entra:

```text
Helpers de frustum/bounds reutilizables.
Queries de oclusion iniciales y medibles.
Senales de visibilidad como datos planos.
Debug visual de bounds/zones marcadas.
Metricas de senales calculadas, no de slots liberados.
Integracion futura como entrada opcional de productores.
```

No entra:

```text
Pintar geometria.
Liberar slots de 09.
Degradar Meshes gestionadas por 09.
Cambiar poligonaje.
Decidir paginas LOD del planeta activo.
Recalcular el shell del planeta.
Sustituir la prioridad de 10.
Pool global base.
Materiales.
Colisiones.
Persistencia.
Gameplay.
```

## Relacion con 09 y 10

09:

```text
Es el unico backend de pintado gestionado.
Recibe Draw(meshId, datos, priority).
Arbitra presupuesto y slots.
No consulta 11 para decidir vision.
```

10:

```text
Es el dueño de la representacion adaptativa del planeta activo.
Calcula paginas, LOD, mirada, frustum local, movimiento/lookahead y priorityScore.
Puede usar senales de 11 como entrada futura si aportan valor medido.
No delega su decision de vision/interes en 11.
```

11:

```text
Aporta senales auxiliares.
No tiene autoridad sobre slots.
No llama a Release de 09.
No llama a Draw.
No inicia regeneracion de planeta por su cuenta.
```

## Flujo permitido

```text
1. 11 calcula una senal auxiliar para bounds/zonas.
2. 10 u otro productor lee esa senal cuando construye su priorityScore o decide publicar.
3. El productor genera o no genera geometria.
4. El productor publica mediante 09.
5. 09 pinta o reclama segun su presupuesto interno.
```

Flujo prohibido:

```text
11 detecta invisible.
11 libera slots de 09.
11 cambia meshes gestionadas.
11 repinta o borra geometria visible.
```

## Decisiones cerradas

```text
11 deja de ser autoridad global de visibilidad para el planeta activo.
La visibilidad/interes del planeta activo pertenece a 10.
09 sigue siendo el unico backend de pintado.
Las senales de 11 son opcionales hasta que un productor las use explicitamente.
Si una senal de 11 afecta al planeta, se documenta como entrada de 10.
```

## TBD

```text
Nivel inicial de oclusion reutilizable.
Si la oclusion empieza por CPU, GPU o aproximacion por bounds.
Frecuencia de actualizacion.
Formato de senal plana para productores.
Como medir que una senal de 11 mejora coste sin introducir popping.
```
