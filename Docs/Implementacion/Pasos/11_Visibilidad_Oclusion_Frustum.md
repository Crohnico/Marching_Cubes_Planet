# 11 - Visibilidad, oclusion y frustum

## Objetivo

Evitar gastar triangulos en geometria que la camara no puede ver.

11 afecta al sistema global de triangulos y debe poder beneficiar a cualquier geometria gestionada.

Contrato funcional:

```text
estado de camara -> zonas visibles/no visibles -> liberar o no pedir tris invisibles
```

## Modelo mental

El sistema debe dejar de pedir o mantener triangulos donde no miro.

Tambien debe provocar de forma natural que el hemisferio contrario a la camara pierda prioridad.

Regla conceptual:

```text
Lo que esta fuera de frustum, oculto u orientado al hemisferio contrario no debe competir igual por tris.
```

## Alcance

Entra:

```text
Frustum culling global.
Oclusion inicial.
Prioridad por direccion de camara.
Liberacion o degradacion de slots invisibles en 09.
Metricas de tris visibles, ocultos, liberados y evitados.
Debug visual de zonas descartadas.
```

No entra:

```text
Pool global base.
BVH de detalle interno.
Cambiar poligonaje por distancia.
Materiales.
Colisiones.
Persistencia.
```

## Relacion con 09 y 10

09:

```text
posee el millon de tris y los reparte.
```

10:

```text
redistribuye detalle dentro de geometria adaptable.
```

11:

```text
decide que zonas no deberian competir por tris porque no son visibles.
```

## Decisiones cerradas

```text
11 afecta tambien a todo el universo gestionado por tris.
11 debe reducir naturalmente el hemisferio contrario a la mirada de la camara.
La palabra tecnica sera Frustum, aunque en notas se pueda escribir frustrum.
```

## TBD

```text
Nivel inicial de oclusion.
Si la oclusion empieza por CPU, GPU o aproximacion por bounds.
Frecuencia de actualizacion.
Como se evita popping visible al liberar tris.
```

