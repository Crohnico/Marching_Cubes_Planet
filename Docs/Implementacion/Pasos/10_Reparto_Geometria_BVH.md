# 10 - Reparto de geometria y detalle espacial

## Objetivo

Definir el sistema que decide donde merece la pena gastar mas triangulos dentro de una geometria que puede adaptarse.

10 toma como base el presupuesto asignado por 09 y lo reparte con mas inteligencia dentro de cada geometria compatible.

Contrato funcional:

```text
budget concedido por 09 -> estructura espacial -> mas detalle donde importa -> menos detalle al alejarse
```

## Modelo mental

09 decide cuantos tris puede usar un sistema.

10 decide donde ponerlos dentro de una geometria adaptable.

Ejemplo principal:

```text
terreno planetario / chunks / superficie Marching Cubes.
```

El sistema debe tender a:

```text
mas triangulos donde estoy mirando o cerca del player.
menos triangulos conforme se aleja.
menos detalle en zonas de baja prioridad visual.
```

## Alcance

Entra:

```text
BVH o estructura espacial equivalente.
Reparto interno de triangulos por distancia.
Reparto interno de triangulos por direccion de mirada.
Metrica de calidad por zona.
Seleccion/degradacion de triangulos para geometria adaptable.
Stress con presupuestos bajos/medios/altos concedidos por 09.
```

No entra:

```text
Pool global de tris.
Oclusion/frustum global.
Reclamar tris entre sistemas.
LOD natural de props.
Materiales.
Colisiones.
Persistencia.
```

## Regla para props

10 no tiene por que aplicarse a todo.

Para props como arboles, rocas pequeñas u objetos con silueta clara, probablemente convenga usar LODs naturales o sistemas especificos.

Decision inicial:

```text
10 se diseña pensando sobre todo en terreno/geometria adaptable.
Props quedan fuera salvo que un documento futuro los incorpore explicitamente.
```

## Decisiones cerradas

```text
10 no sustituye a 09.
10 consume un presupuesto ya concedido.
10 es especialmente relevante para terreno.
10 puede usar BVH u otra estructura espacial si cumple el objetivo.
```

## TBD

```text
Tipo exacto de BVH o estructura espacial.
CPU, GPU o ruta hibrida.
Unidad de nodo.
Como se integra con triangulos generados por Marching Cubes.
Como se combina con chunks locales.
```

