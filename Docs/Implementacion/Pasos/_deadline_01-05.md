# Deadline 01-05 - Validacion de base para empezar 06

## Regla de validacion y workarounds

Cada validacion ejecutable debe correr solo en el contexto definido por este documento.

No se deben añadir `if/else` defensivos, ramas alternativas, fallbacks o workarounds para ejecutar una validacion fuera de su contexto definido.

Si una validacion falla por contexto incorrecto, debe fallar de forma directa y diagnostica. Si existe una alternativa tecnica para rodear el fallo, primero se pregunta si ese workaround es deseado y despues se documenta la decision.

## Objetivo

Definir la prueba de cierre del primer bloque tecnico antes de empezar:

```text
Docs/Implementacion/Pasos/06_Forma_Planeta_GPU.md
```

Este documento no define un sistema nuevo del motor. Define el gate de validacion que decide si la base comun esta lista.

El deadline cubre:

```text
01_PlanetImplementationLab
02_ComputeShaderLab
03_Coordenadas_Y_Receta
04_Gestion_RAM_VRAM
05_Quest3_Player_Setup
```

No valida todavia:

```text
Forma procedural real del planeta.
Proxy lejano final.
Payload final de triangulos.
Estados lejanos del planeta.
Chunks locales.
Gameplay.
```

Esos sistemas empiezan despues, en sus documentos propios.

## Regla de bloqueo

```text
No se empieza `06_Forma_Planeta_GPU` si este deadline no esta en verde.
```

El objetivo no es "tenerlo casi". El objetivo es saber que la base no esta disfuncional.

Si una prueba falla, no se avanza y se corrige antes de construir encima.

## Lugar de validacion

La validacion se hace dentro de la escena tecnica acumulativa:

```text
PlanetImplementationLab
```

No se acepta cerrar el deadline con demos aisladas que luego haya que reconciliar.

La prueba debe poder ejecutarse desde:

```text
Inspector / CustomEditor.
UI VR con rayo cuando el setup 05 este disponible.
```

## Estados posibles de cada check

Cada check debe quedar marcado como:

```text
OK
Warning aceptado con diagnostico
Blocked
```

Un `Warning` solo permite avanzar si cumple todo esto:

```text
La causa esta entendida.
La accion futura esta escrita.
No contradice una regla base de memoria, release, XR o arquitectura.
No oculta una fuga, allocation caliente o recurso sin owner.
```

## Checklist de visto bueno

### 1. Documentacion

```text
[ ] Los documentos 01, 02, 03, 04 y 05 estan escritos y alineados.
[ ] No hay contradicciones conocidas entre documentos.
[ ] Los TBD restantes son no bloqueantes para empezar el 06.
[ ] `Docs/Definicion_Tecnica_Proyecto.md` no se ha modificado sin peticion explicita.
[ ] Las instrucciones para implementar estan escritas como contrato para Codex, no como notas vagas.
[ ] Queda claro que los Labs son scripts aditivos sobre codigo real.
[ ] Queda claro que las capturas/snapshots no forman parte del runtime oficial caliente.
```

### 2. Proyecto y paquetes

```text
[ ] El proyecto abre en la version de Unity fijada sin errores de compilacion.
[ ] El package id Android debug es `com.Perodry.debug`.
[ ] OpenXR esta instalado como paquete explicito del proyecto.
[ ] No se usa prerelease de OpenXR salvo decision explicita.
[ ] El build target Android esta preparado o el diagnostico indica exactamente que falta.
[ ] La escena `PlanetImplementationLab` esta incluida en el flujo de build o se documenta como cargarla.
```

### 3. PlanetImplementationLab

```text
[ ] Existe una escena tecnica acumulativa `PlanetImplementationLab`.
[ ] Existe un controlador raiz del Lab.
[ ] Los modulos del Lab siguen el patron ClaseReal / ClaseRealEditor / ClaseRealLab.
[ ] `Validate Scene` detecta referencias nulas importantes.
[ ] `Init Lab` funciona.
[ ] `Init Lab` ejecutado dos veces no duplica estado ni recursos.
[ ] `Release All` funciona.
[ ] `Release All` ejecutado dos veces no rompe.
[ ] `Init -> Release All -> Init` funciona.
[ ] Los diagnosticos muestran causa probable y accion recomendada, no solo numeros.
```

### 4. Compute Shader minimo

```text
[ ] El proyecto carga un Compute Shader minimo.
[ ] El kernel minimo se puede ejecutar con `Dispatch Once`.
[ ] C# consulta el tamano de thread group con `GetKernelThreadGroupSizes`.
[ ] Todo acceso a buffer o textura comprueba limites.
[ ] GraphicsBuffer es el camino principal de la prueba.
[ ] ComputeBuffer existe solo como comparativa/fallback.
[ ] La RenderTexture debug muestra resultado visible sin readback bloqueante.
[ ] La Mesh debug se crea, registra y libera si existe en la prueba.
[ ] `Dispatch 100x` no deja recursos vivos inesperados.
[ ] `GraphicsBuffer Stress` termina con diagnostico legible.
[ ] `ComputeBuffer Stress` termina con diagnostico legible.
[ ] `Run Comparison` muestra diferencias de coste/estado de forma legible.
```

### 5. Coordenadas y receta

```text
[ ] `PlanetRecipe` existe como dato de receta, separado de placement.
[ ] `WorldRadius` se deriva de `GridRadius * WorldScale`.
[ ] `WorldRadius` no existe como dato editable independiente.
[ ] `GridRadius = 1000` y `WorldScale = 4` producen `WorldRadius = 4000`.
[ ] `Grid -> World -> Grid` conserva valores dentro de tolerancia.
[ ] `GridPosition -> GridCellCoordinates` usa floor matematico, tambien en negativos.
[ ] Las consultas espaciales escriben en buffer preasignado y devuelven count.
[ ] Un overflow de buffer de query se diagnostica sin crear memoria nueva.
[ ] Las pruebas de receta no crean recursos GPU ni recursos pesados artificiales.
[ ] Queda apuntado revisitar `03_Coordenadas_Y_Receta` para incorporar orbitas planetarias y su efecto sobre placement, conversiones y calculos derivados.
```

### 6. RAM, memoria GPU estimada y GC

```text
[ ] Todo recurso grande creado por pruebas queda registrado con owner.
[ ] Todo recurso registrado tiene estimatedBytes.
[ ] Los presupuestos iniciales Quest 3 estan cargados en el Lab.
[ ] Los presupuestos son editables desde Inspector del Lab.
[ ] El archivo externo de override de presupuesto solo se lee al arrancar o con `Reload Test Budget`.
[ ] No hay polling de archivo por frame.
[ ] `Capture Snapshot` genera snapshot propio.
[ ] `Export Last Own Snapshot` genera JSON con summary y details.
[ ] `Capture Unity Memory Snapshot` genera .snap o diagnostico claro de unavailable.
[ ] Los ProfilerRecorder definidos se intentan registrar.
[ ] Los counters no disponibles quedan marcados como unavailable.
[ ] `Release All` deja contadores propios a cero.
[ ] `Release All` dos veces no deja contadores negativos ni excepciones.
[ ] Un recurso vivo tras Release produce diagnostico Critical.
[ ] Un presupuesto superado produce diagnostico Warning o Critical.
[ ] Una allocation GC relevante produce diagnostico accionable.
```

### 7. UI de Inspector

```text
[ ] Los botones principales existen en CustomEditor nativo.
[ ] No se usan assets externos de inspector.
[ ] Cada boton que crea recursos tiene boton o flujo de release claro.
[ ] Cada stress pesado requiere accion manual.
[ ] `Stress Extreme` muestra aviso antes de ejecutarse.
[ ] Los resultados de botones quedan visibles en Inspector.
```

### 8. Quest Link, rig y UI VR

```text
[ ] Quest 3 conecta por Link al Editor.
[ ] Play Mode muestra `PlanetImplementationLab` en las gafas.
[ ] La camara VR renderiza estereo.
[ ] La camara responde al movimiento del HMD.
[ ] El rig permite movimiento basico de prueba.
[ ] Existe un Canvas fisico delante del player.
[ ] El Canvas no tapa ni interfiere con el sistema bajo prueba.
[ ] El rayo apunta a la UI.
[ ] El rayo pulsa botones de la UI.
[ ] La UI VR replica los comandos importantes del Inspector.
[ ] Cada pantalla de UI VR muestra diagnostico lateral equivalente al Inspector.
[ ] `Release All` se puede ejecutar desde UI VR.
```

### 9. APK / Player Quest 3

```text
[ ] El proyecto esta preparado para generar APK/Player Android.
[ ] Se puede generar un APK de prueba o queda un diagnostico exacto de que falta.
[ ] El APK usa package id `com.Perodry.debug`.
[ ] El APK se instala con SideQuest cuando toque probar en dispositivo.
[ ] La build de dispositivo no ejecuta snapshots, stress ni polling automaticamente al arrancar.
```

### 10. Tests automatizados

```text
[ ] Pasan los EditMode tests de datos puros disponibles.
[ ] Pasan los PlayMode tests de vida de componentes disponibles.
[ ] Hay tests para Release doble donde aplique.
[ ] Hay tests para Init -> Release -> Init donde aplique.
[ ] Hay tests para calculo de bytes estimados.
[ ] Hay tests para conversion Grid <-> World.
[ ] Los tests condicionados por soporte de plataforma no fallan falsamente si el soporte no existe.
```

### 11. Evidencias minimas

```text
[ ] Ultimo resultado de Validate Scene.
[ ] Ultimo resultado de Validate Memory Setup.
[ ] Ultimo snapshot propio exportado.
[ ] Ultimo diagnostico de Release All.
[ ] Resultado de stress Low y al menos un stress pesado elegido manualmente.
[ ] Resultado de prueba Quest Link.
[ ] Resultado de click con rayo en UI VR.
[ ] Estado de generacion APK/Player.
```

## Bloqueantes automaticos

Si aparece cualquiera de estos casos, el deadline queda bloqueado:

```text
Errores de compilacion.
PlanetImplementationLab no abre.
Un modulo no responde a Release All.
Release All no vuelve los contadores propios a cero.
Release All rompe al ejecutarse dos veces.
Un recurso grande se crea sin owner.
Un recurso grande se crea sin estimatedBytes.
Hay crecimiento de memoria propio no explicado tras Release.
Hay GC recurrente en un camino que deberia ser caliente.
Una prueba supera hard budget sin confirmacion manual y diagnostico.
La UI VR ejecuta comandos distintos a los del Inspector.
El player rig impide probar el Lab.
Quest Link no permite validar camara/player/UI y no hay diagnostico claro.
El proyecto no puede preparar APK/Player Android y no hay diagnostico claro.
Un TBD bloqueante queda sin escribir.
```

## Definition of Done

El deadline se considera aprobado cuando:

```text
Todos los checks obligatorios estan OK.
Los Warning aceptados tienen diagnostico y accion futura escrita.
No queda ningun bloqueante automatico.
Release All queda probado desde Inspector y UI VR.
Los snapshots propios y oficiales quedan disponibles o con diagnostico claro.
Quest Link permite usar el Lab con gafas, movimiento y rayo.
El proyecto queda listo para empezar `Docs/Implementacion/Pasos/06_Forma_Planeta_GPU.md`.
```

## Resultado de ejecucion

Este apartado se rellena cuando se ejecute la validacion real.

```text
Fecha:
Unity version:
Rama/commit:
Plataforma Editor:
Quest Link probado:
APK generado:
APK instalado:
Resultado global: TBD
Bloqueantes encontrados:
Warnings aceptados:
Snapshots propios:
Snapshots oficiales .snap:
Notas:
```

## TBD

```text
Fecha del deadline.
Formato final del registro historico de resultados si queremos conservar varias ejecuciones.
Revisitar `03_Coordenadas_Y_Receta` para modelar que los planetas orbitan: el centro del planeta no sera siempre estatico y eso afecta a PlanetPlacement, conversiones Grid/World/Stellar, floating origin y calculos dependientes del tiempo.
```
