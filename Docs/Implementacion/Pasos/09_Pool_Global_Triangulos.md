# 09 - Pool global de triangulos

## Objetivo

Crear un controlador global de presupuesto de triangulos para todo el universo.

El presupuesto inicial es fijo:

```text
TotalTriangleBudget = 1_000_000 tris
```

09 no cambia el poligonaje de ninguna geometria.

09 solo reparte slots de triangulos ya presupuestados.

Contrato funcional:

```text
sistema pide X tris -> pool concede/deniega/roba slots -> owner usa esos tris
```

## Modelo mental

Todo lo que quiera consumir triangulos visibles pasa por el mismo controlador:

```text
terreno.
agua.
rocas.
arboles.
props que entren en el sistema.
otros elementos con geometria gestionada.
```

Un sistema puede decir:

```text
Oye, necesito generar X tris.
```

El controlador responde segun el presupuesto global disponible y la prioridad espacial respecto al player/camara.

## Regla central

Si el pool aun no ha repartido el millon de triangulos, concede slots.

Si el pool ya repartio el millon, busca slots asignados a geometria mas lejana del player.

Si la nueva peticion esta mas cerca del player que esos slots lejanos, el pool reclama esos slots y los reasigna.

Si no existe geometria peor candidata, la peticion queda pendiente o denegada con diagnostico.

## Alcance

Entra:

```text
Pool global de slots de triangulos.
Presupuesto fijo inicial de 1M tris.
Owner por asignacion.
Distancia al player como criterio inicial.
Reclamacion de slots lejanos.
Metricas de concedidos/denegados/reclamados.
Debug de consumo por sistema.
Release de slots por owner.
```

No entra:

```text
Cambiar poligonaje de una mesh.
BVH de detalle.
Reparto por direccion de mirada.
Oclusion.
Frustum culling.
LOD natural de props.
Generar triangulos.
Marching Cubes.
Materiales.
Colisiones.
```

## Decisiones cerradas

```text
El presupuesto de triangulos es global para todo el universo.
El valor inicial es 1M tris.
09 no optimiza geometria: solo asigna slots.
La primera prioridad de robo/reasignacion es distancia al player.
El pool es compartido por terreno, agua, rocas, arboles y sistemas futuros que consuman tris gestionados.
```

## TBD

```text
Nombre exacto de las clases.
Si el pool vive en CPU puro o expone estado GPU.
Formato de handle de asignacion.
Como se notifican invalidaciones a sistemas que pierden slots.
Politica de prioridades especiales por gameplay.
```

