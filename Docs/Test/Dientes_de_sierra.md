# Dientes de sierra

## Objetivo

Definir una investigacion y bateria de pruebas para resolver el problema de dientes de sierra visible en:

```text
PlanetRecipePayloadPreview con planeta de diametro 8000.
Esfera nativa de Unity con escala 1.
```

Lectura inicial:

```text
Si una esfera nativa de Unity escala 1 tambien muestra dientes de sierra, el bloqueo no se trata como problema del generador de planeta.
Se trata como problema de configuracion de proyecto, URP, camara, resolucion, XR, render path, postproceso, sombras o entorno de visualizacion.
```

Estado del proyecto:

```text
Estamos en el paso _merge-01-02-03.
No se avanza a 04 como siguiente trabajo mientras este problema siga abierto.
Las pruebas de dientes de sierra quedan pausadas hasta probar en Quest 3 o en la pantalla objetivo real.
El Editor/PC no se considera entorno fiable para decidir cambios de render mientras el problema dependa del monitor.
Todavia no se esta probando en Quest, Quest Link, APK, player VR, doble camara ni XR runtime real.
```

Este documento no modifica configuracion por si mismo. Define como probar cambios aislados sin acumular ruido.

## Fuentes investigadas

Fuentes oficiales consultadas:

```text
Unity Manual - URP anti-aliasing:
https://docs.unity3d.com/6000.1/Documentation/Manual/urp/anti-aliasing.html

Unity Manual - URP Asset:
https://docs.unity3d.com/6000.1/Documentation/Manual/urp/universalrp-asset.html

Unity Manual - URP camera component reference:
https://docs.unity3d.com/6000.1/Documentation/Manual/urp/camera-component-reference.html

Unity Manual - URP deferred rendering path:
https://docs.unity3d.com/6000.1/Documentation/Manual/urp/rendering/deferred-rendering-path.html

Unity Scripting API - XRSettings.eyeTextureResolutionScale:
https://docs.unity3d.com/6000.1/Documentation/ScriptReference/XR.XRSettings-eyeTextureResolutionScale.html

Unity OpenXR package - Foveated Rendering Feature:
https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.17/manual/features/foveatedrendering.html
```

Conclusiones utiles de la investigacion:

```text
URP tiene anti-aliasing por URP Asset y por camara.
MSAA se configura en el URP Asset y la camara debe permitirlo.
FXAA, SMAA y TAA se configuran por camara en URP.
TAA tiene incompatibilidades y no debe mezclarse a ciegas con MSAA, camera stacking o dynamic resolution.
Render Scale cambia la resolucion interna de render antes de presentar la imagen.
En XR, la escala de textura de ojo puede cambiar la resolucion efectiva.
Foveated rendering puede reducir resolucion fuera del centro y debe descartarse si aparece aliasing periferico en Quest.
Las sombras tienen su propia resolucion, distancia, cascadas y bias, y pueden parecer dientes de sierra aunque la geometria este bien.
```

## Configuracion encontrada en el proyecto

Revision local realizada sobre:

```text
Unity: 6000.3.11f1
URP package: com.unity.render-pipelines.universal 17.3.0
OpenXR package: com.unity.xr.openxr 1.17.1
Escena principal: Assets/Scenes/PlanetImplementationLab.unity
```

Valores relevantes encontrados:

```text
ProjectSettings/QualitySettings.asset:
- Mobile antiAliasing: 0
- PC antiAliasing: 0
- Android usa calidad Mobile
- Standalone usa calidad PC

Assets/Settings/PC_RPAsset.asset:
- m_MSAA: 1
- m_RenderScale: 1
- m_RequireDepthTexture: 1
- m_RequireOpaqueTexture: 1
- m_SupportsHDR: 1
- m_MainLightShadowmapResolution: 2048
- m_ShadowDistance: 50
- m_ShadowCascadeCount: 4
- m_ShadowAtlasResolution: 256

Assets/Settings/Mobile_RPAsset.asset:
- m_MSAA: 1
- m_RenderScale: 0.8
- m_RequireDepthTexture: 0
- m_RequireOpaqueTexture: 0
- m_SupportsHDR: 1
- m_MainLightShadowmapResolution: 1024
- m_ShadowDistance: 50
- m_ShadowCascadeCount: 1
- m_ShadowAtlasResolution: 256

PlanetLabCameraRig:
- near clip plane: 0.01
- far clip plane: 20000
- field of view: 60
- HDR: 1
- Allow MSAA: 1
- Allow Dynamic Resolution: 0
- Render Post Processing: 1
- Camera anti-aliasing: 0
- Camera stack: vacio

PC_Renderer:
- Renderer Feature ScreenSpaceAmbientOcclusion activo
- Native Render Pass activo
- Rendering mode serializado como m_RenderingMode: 2

Mobile_Renderer:
- Sin Renderer Features
- Native Render Pass activo
```

Lectura inicial de riesgo:

```text
Riesgo muy alto:
- MSAA efectivo probablemente desactivado en URP Asset.
- Anti-aliasing de camara desactivado.

Riesgo medio:
- PC_Renderer podria estar en modo que no nos convenga para MSAA o para diagnostico simple.
- Postproceso/SSAO puede endurecer bordes o crear ruido, sobre todo si ya falta AA.
- Camara con near 0.01 y far 20000 reduce precision de profundidad, pero el far largo es una necesidad del proyecto y no se debe recortar como solucion general.

Riesgo bajo para este caso concreto:
- Sombras, porque el problema tambien aparece en esfera simple y puede verse en contorno; aun asi se prueban.
- XR, foveated rendering, Quest Link, APK y doble camara quedan fuera de la bateria actual porque todavia no estamos en ese entorno.

Riesgo futuro para Quest/Android:
- Mobile Render Scale a 0.8 reduce resolucion interna, pero no participa en la prueba actual de Editor/PC.
```

## Restricciones actuales de entorno

La bateria actual se ejecuta en:

```text
Unity Editor.
PC/Standalone.
Una sola camara de escena/lab.
Sin Quest.
Sin Quest Link.
Sin APK.
Sin player VR.
Sin doble camara XR.
```

Regla:

```text
Las pruebas XR/Quest se mantienen documentadas como fase futura, pero no participan en el diagnostico actual.
Si un cambio solo afecta a Quest, Link, foveated rendering, eye texture scale o APK, se marca como pendiente y no bloquea la investigacion de Editor.
```

Regla de far clip:

```text
El far clip extenso es un requisito del proyecto.
far = 20000 se considera correcto para la fase actual e incluso podria quedarse corto mas adelante.
No se debe proponer reducir far como solucion permanente al aliasing.
Solo se permite tocar far en una prueba temporal de diagnostico de precision de profundidad, y siempre se revierte salvo que la persona pida explicitamente redefinir esta decision.
```

## Hallazgo observado

Resultado reportado durante la investigacion:

```text
En el segundo monitor se ven dientes de sierra muy grandes.
Al mover la misma vista al primer monitor para hacer una foto, el problema no ocurre en ese monitor.
```

Lectura:

```text
Este hallazgo sube la prioridad de las causas externas a Unity.
Antes de tocar URP, MSAA, camara o sombras, hay que descartar reescalado del monitor, resolucion no nativa, escala de Windows, configuracion de GPU, cable/salida, nitidez del monitor o Game View escalado distinto entre pantallas.
Como el defecto depende del monitor, se pausa la bateria activa en Editor.
```

Regla:

```text
No se empieza la prueba de MSAA hasta confirmar si el aliasing depende del monitor.
Si el defecto solo existe en un monitor, la solucion no debe asumirse como cambio de render del proyecto.
No se hacen mas pruebas de configuracion de proyecto en Editor para cerrar este bloqueo.
La investigacion se retoma en Quest 3 o pantalla objetivo real.
```

## Regla de pruebas

Estado actual:

```text
Pausada.
No ejecutar la bateria en Editor/PC como criterio de decision.
No tocar URP, camara, sombras, postproceso ni ProjectSettings por este bloqueo hasta probar en Quest 3 o pantalla objetivo real.
```

No se acumulan cambios.

Flujo obligatorio de cada prueba:

```text
1. Capturar estado base.
2. Codex aplica un unico cambio pequeno.
3. La persona prueba visualmente la esfera Unity escala 1 y el planeta diametro 8000.
4. Se apunta resultado: mejora clara, mejora parcial, sin cambio o peor.
5. Si no arregla o no aporta, Codex revierte exactamente el cambio.
6. Solo despues se pasa a la siguiente prueba.
```

Regla de resultado:

```text
Si una prueba arregla el problema, se para la bateria.
No se mezclan mas cambios hasta documentar cual fue el causante.
```

Regla de reversa:

```text
Cada prueba debe dejar un diff pequeno y revisable.
Si la prueba no sirve, se revierte el diff completo antes de continuar.
No se usa git reset --hard.
No se revierten cambios del usuario.
```

## Escena de control

Antes de tocar configuracion, preparar o confirmar una escena visual minima dentro de `PlanetImplementationLab`:

```text
Objeto A: esfera Unity nativa, escala 1, material opaco simple.
Objeto B: PlanetRecipePayloadPreview con diametro 8000.
Camara: PlanetLabCameraRig.
Fondo: color liso oscuro o neutro.
Luz: una directional light.
```

Prueba visual de cada paso:

```text
Mirar contorno de la esfera escala 1.
Mirar contorno del planeta diametro 8000.
Mirar borde iluminado/sombra si hay sombras.
Mirar captura en Game View 1x, no escalada.
```

Resultado que se debe apuntar:

```text
Fecha:
Plataforma: Editor PC
Cambio probado:
Resultado esfera escala 1:
Resultado planeta diametro 8000:
FPS/frame time si se mide:
Decision: conservar / revertir / repetir en Editor
Notas:
```

## Lista de posibles causantes

### Antialiasing y resolucion

```text
Segundo monitor trabajando fuera de resolucion nativa.
Escala de Windows distinta entre monitores.
Game View reescalada de forma distinta al moverla entre pantallas.
Monitor secundario con sharpening/super resolution/imagen mejorada activada.
Panel secundario con resolucion fisica menor o densidad de pixeles inferior.
Salida/cable/adaptador limitando resolucion, color o frecuencia.
Configuracion de escalado de GPU distinta por monitor.
URP Asset con MSAA a 1x o desactivado.
Camara con Anti-aliasing = None.
Camara con Allow MSAA desactivado.
QualitySettings antiAliasing a 0 y confusion con URP.
Render Scale menor que 1.
XR eye texture resolution scale menor que 1.
Dynamic resolution reduciendo resolucion interna.
Game View escalado a menos de 1x o ventana pequena.
Captura/streaming/Quest Link reescalando imagen.
Filtro de upscaling demasiado agresivo o sharpen excesivo.
Foveated rendering reduciendo resolucion periferica.
```

### URP y render path

```text
Renderer en modo no adecuado para diagnosticar MSAA.
Deferred path o GBuffer interactuando mal con el tipo de AA que se prueba.
Native Render Pass cambiando ruta interna en movil.
Intermediate Texture auto/off afectando resolucion o postproceso.
Opaque Texture obligando copias/resolves y ruta distinta en movil.
Depth Texture activada o desactivada cambiando postprocesos.
HDR activado cambiando formato/resolucion/coste.
Post Processing activado sin AA real.
SSAO activo generando ruido o bordes en geometria.
Camera stacking si se introduce mas adelante.
```

### Camara y profundidad

```text
Near clip demasiado bajo.
Far clip demasiado alto.
Relacion far/near demasiado grande.
Z-fighting por precision de profundidad.
FOV o distancia de camara que exagera pixeles de borde.
Objeto demasiado lejos respecto al rango de camara.
Planeta grande ocupando muchos valores de profundidad con camara local.
```

### Sombras e iluminacion

```text
Shadow map resolution baja.
Shadow atlas resolution baja.
Shadow distance demasiado alta para la resolucion usada.
Pocas cascadas o splits no adecuados.
Bias o normal bias creando bordes dentados.
Soft shadows desactivadas.
Sombras de directional light proyectadas sobre esfera/planeta.
SSAO o contacto visual pareciendo sombra dentada.
```

### Material, shader y geometria

```text
Shader sin iluminacion o sin normal smoothing no causa el borde de silueta, pero puede hacer mas visibles cambios de color por triangulo.
Normales duras o vertices duplicados pueden parecer facetas, no aliasing de pantalla.
Triangulos grandes o payload bajo pueden parecer poligonizacion, no diente de sierra de pantalla.
LOD cross fade con dithering puede crear patron de puntos o bordes.
Texturas con mipmaps/filtro incorrecto si hubiera texturas.
Alpha clipping o transparencias si se introducen mas adelante.
```

### XR, Quest y entorno externo

```text
Quest Link con resolucion de encode baja.
Runtime OpenXR usando resolucion menor que la esperada.
Foveated rendering activado en Quest.
Fixed Foveated Rendering o eye tracked foveation con periferia visible.
Vulkan/OpenGLES con diferencias de MSAA o resolves.
Multiview/single pass cambiando ruta de render.
APK usando calidad Mobile con Render Scale 0.8 mientras Editor usa PC.
Android usando URP Asset Mobile distinto al PC.
Headset o mirror view mostrando imagen reescalada.
```

Nota:

```text
Este bloque no se prueba todavia.
Queda guardado para cuando entren 05_Quest3_Player_Setup, Quest Link, APK o XR real.
```

## Bateria de pruebas ordenada

Estado:

```text
Pausada hasta Quest 3 / pantalla objetivo real.
Los tests se conservan como lista de diagnostico futura.
No se ejecutan ahora en Editor/PC porque el resultado queda contaminado por el monitor.
```

### Test 00 - Baseline sin cambios

Cambio:

```text
Ninguno.
```

Accion:

```text
Abrir PlanetImplementationLab.
Confirmar esfera Unity escala 1 visible.
Confirmar PlanetRecipePayloadPreview visible.
Capturar screenshot o descripcion desde Game View 1x.
Anotar si el diente aparece en contorno, sombras, interior o todo.
```

Exito:

```text
Queda una referencia visual antes de tocar nada.
```

Reversa:

```text
No aplica.
```

### Test 01 - Descartar monitor, Game View o captura reescalada

Cambio:

```text
Ninguno en archivos.
```

Accion para la persona:

```text
Poner Game View a escala 1x.
Evitar "Fit" si reduce la imagen.
Probar una resolucion fija alta.
Comparar con maximizar Game View.
Mover la misma ventana de Unity entre monitor principal y segundo monitor.
Comprobar que ambos monitores estan en su resolucion nativa.
Comprobar escala de Windows por monitor.
Comprobar si el segundo monitor tiene sharpening, super resolution, modo juego, nitidez artificial o escalado propio activado.
Comprobar en panel NVIDIA/AMD/Intel si el escalado lo hace GPU o pantalla.
Probar una captura de pantalla real: si la captura se ve bien en el monitor principal pero mal al verla en el segundo, el problema es presentacion del monitor.
```

Nota futura:

```text
Cuando exista Quest Link, comparar vista en gafas contra mirror en PC como prueba separada.
```

Exito:

```text
Si el problema solo ocurre en el segundo monitor, el bloqueo principal no es URP.
Si al ver 1x desaparece o baja mucho, el problema era reescalado de visualizacion/captura.
```

Reversa:

```text
No aplica.
```

### Test 02 - Activar MSAA 4x en URP Asset PC

Cambio:

```text
Assets/Settings/PC_RPAsset.asset:
- m_MSAA de 1 a 4.
```

Motivo:

```text
El proyecto PC tiene MSAA efectivo en 1x.
Si la esfera escala 1 mejora claramente, el causante principal es falta de multisampling.
```

Accion de prueba:

```text
Probar en Editor PC.
Mirar contorno de esfera escala 1.
Mirar contorno del planeta diametro 8000.
```

Exito:

```text
Contornos mucho mas suaves sin tocar geometria.
```

Si funciona:

```text
Probar despues 2x, 4x y 8x como subpruebas separadas para decidir coste/calidad.
No pasar aun a Quest sin medir.
```

Reversa si no funciona:

```text
Volver m_MSAA a 1.
```

### Test 03 - Activar MSAA 4x en URP Asset Mobile

Estado actual:

```text
Pendiente. No se ejecuta mientras la investigacion sea solo Editor/PC.
```

Cambio:

```text
Assets/Settings/Mobile_RPAsset.asset:
- m_MSAA de 1 a 4.
```

Motivo:

```text
Android usa la calidad Mobile.
Aunque el Editor PC mejore, Quest puede seguir dentado si Mobile_RPAsset queda a 1x.
```

Accion de prueba:

```text
Probar en Quest Link si usa la ruta XR esperada.
Probar APK Quest cuando exista flujo.
```

Exito:

```text
Mejora visible en Quest/Android.
```

Reversa si no funciona:

```text
Volver m_MSAA a 1.
```

### Test 04 - Subir Mobile Render Scale a 1.0

Estado actual:

```text
Pendiente. No se ejecuta mientras la investigacion sea solo Editor/PC.
```

Cambio:

```text
Assets/Settings/Mobile_RPAsset.asset:
- m_RenderScale de 0.8 a 1.0.
```

Motivo:

```text
Render Scale 0.8 reduce resolucion interna y puede causar dientes incluso con geometria simple.
```

Accion de prueba:

```text
Probar la misma escena en ruta Mobile/Android/Quest.
Comparar contorno y rendimiento.
```

Exito:

```text
El diente baja claramente al subir resolucion interna.
```

Reversa si no funciona:

```text
Volver m_RenderScale a 0.8.
```

### Test 05 - Probar Render Scale 1.25 en PC

Cambio:

```text
Assets/Settings/PC_RPAsset.asset:
- m_RenderScale de 1.0 a 1.25.
```

Motivo:

```text
Si mejora sin tocar MSAA, el problema esta muy ligado a resolucion interna o presentacion.
```

Accion de prueba:

```text
Probar solo en Editor PC.
No mezclar con MSAA si Test 02 fue revertido.
```

Exito:

```text
El borde mejora por supersampling.
```

Reversa si no funciona:

```text
Volver m_RenderScale a 1.0.
```

### Test 06 - Activar FXAA por camara

Cambio:

```text
PlanetLabCameraRig / UniversalAdditionalCameraData:
- m_Antialiasing de 0 a FXAA.
```

Motivo:

```text
La camara tiene anti-aliasing None.
FXAA es barato y puede suavizar bordes de pantalla, aunque puede emborronar.
```

Accion de prueba:

```text
Probar esfera y planeta.
Mirar si suaviza pero pierde nitidez.
```

Exito:

```text
Borde aceptable con coste bajo.
```

Reversa si no funciona:

```text
Volver m_Antialiasing a 0.
```

### Test 07 - Activar SMAA por camara

Cambio:

```text
PlanetLabCameraRig / UniversalAdditionalCameraData:
- m_Antialiasing de 0 a SMAA.
- Mantener una calidad concreta y documentarla.
```

Motivo:

```text
SMAA suele conservar mas nitidez que FXAA y puede funcionar bien para bordes geometricos.
```

Accion de prueba:

```text
Probar esfera y planeta.
Comparar contra FXAA solo si FXAA se revierte antes.
```

Exito:

```text
Borde mejor que None y menos borroso que FXAA.
```

Reversa si no funciona:

```text
Volver m_Antialiasing a 0.
```

### Test 08 - Probar TAA solo en PC

Cambio:

```text
PlanetLabCameraRig / UniversalAdditionalCameraData:
- m_Antialiasing de 0 a TAA.
```

Condiciones:

```text
No mezclar con MSAA.
No probar como solucion Quest hasta medir coste y compatibilidad.
No usar si introduce ghosting o borrosidad en movimiento.
```

Motivo:

```text
TAA puede suavizar mucho contorno, pero en VR y movimiento puede ser mala solucion.
```

Accion de prueba:

```text
Probar en Editor PC.
Mover camara.
Mirar ghosting, shimmering y borrosidad.
```

Exito:

```text
Mejora clara sin ghosting molesto.
```

Reversa si no funciona:

```text
Volver m_Antialiasing a 0.
```

### Test 09 - Cambiar PC_Renderer a Forward para diagnostico MSAA

Cambio:

```text
Assets/Settings/PC_Renderer.asset:
- Cambiar Rendering Mode a Forward.
```

Motivo:

```text
Si el renderer PC esta en una ruta deferred o equivalente, conviene aislar si la ruta de render impide o empeora el AA esperado.
```

Accion de prueba:

```text
Probar solo en Editor PC.
Idealmente combinar despues con MSAA 4x como subprueba controlada, pero primero Forward solo.
```

Exito:

```text
El contorno mejora o MSAA empieza a tener efecto.
```

Reversa si no funciona:

```text
Volver al Rendering Mode anterior.
```

### Test 10 - Desactivar postproceso de camara

Cambio:

```text
PlanetLabCameraRig / UniversalAdditionalCameraData:
- m_RenderPostProcessing de 1 a 0.
```

Motivo:

```text
El postproceso puede cambiar la ruta final de imagen o resaltar bordes. Queremos saber si el problema existe antes de postprocesar.
```

Accion de prueba:

```text
Probar esfera y planeta.
Mirar contorno y sombras.
```

Exito:

```text
Si mejora, investigar volumen/postprocesos concretos.
```

Reversa si no funciona:

```text
Volver m_RenderPostProcessing a 1.
```

### Test 11 - Desactivar SSAO en PC_Renderer

Cambio:

```text
Assets/Settings/PC_Renderer.asset:
- Desactivar Renderer Feature ScreenSpaceAmbientOcclusion.
```

Motivo:

```text
SSAO puede crear ruido o bordes oscuros dentados, especialmente sin AA.
```

Accion de prueba:

```text
Probar esfera Unity escala 1 y planeta.
Mirar si los dientes estaban en contacto/sombras, no solo en silueta.
```

Exito:

```text
Los bordes oscuros o interiores mejoran.
```

Reversa si no funciona:

```text
Reactivar SSAO.
```

### Test 12 - Desactivar Opaque Texture en PC_RPAsset

Cambio:

```text
Assets/Settings/PC_RPAsset.asset:
- m_RequireOpaqueTexture de 1 a 0.
```

Motivo:

```text
Opaque Texture puede introducir copias de color y rutas de resolve. En movil, Unity documenta restricciones alrededor de MSAA y StoreAndResolve.
```

Accion de prueba:

```text
Probar PC.
Si mejora, despues se repite una variante equivalente en Mobile.
```

Exito:

```text
Mejora visible en bordes o desaparece artefacto de copia.
```

Reversa si no funciona:

```text
Volver m_RequireOpaqueTexture a 1.
```

### Test 13 - Desactivar HDR

Cambio:

```text
PC_RPAsset o Mobile_RPAsset segun plataforma:
- m_SupportsHDR de 1 a 0.
Camara:
- m_HDR de 1 a 0 si hace falta para aislar.
```

Motivo:

```text
HDR cambia formato de color y coste. No deberia ser causa principal de dientes, pero puede cambiar ruta de postproceso y resolucion.
```

Accion de prueba:

```text
Probar contorno y brillo.
```

Exito:

```text
Mejora clara atribuible a ruta HDR/postproceso.
```

Reversa si no funciona:

```text
Volver HDR al estado anterior.
```

### Test 14 - Ajustar near/far clip de camara

Cambio:

```text
PlanetLabCameraRig:
- near clip plane de 0.01 a 0.1.
- far clip plane se mantiene en 20000 salvo que se haga una subprueba temporal estrictamente diagnostica.
```

Motivo:

```text
La relacion actual far/near es grande. La prueba aceptada aqui es subir near para mejorar precision sin romper el requisito de far extenso.
```

Nota:

```text
far = 20000 es una decision practica actual y no se considera sospechoso prioritario del diente de sierra de contorno.
Si se prueba un far menor, sera solo para confirmar o descartar precision de profundidad, no como propuesta de solucion.
```

Accion de prueba:

```text
Probar esfera y planeta.
Mirar si mejora interior, sombras o parpadeos.
```

Exito:

```text
Mejora en artefactos de profundidad, no necesariamente en silueta pura.
```

Reversa si no funciona:

```text
Volver near a 0.01.
Si se toco far en una subprueba temporal, volver far a 20000.
```

### Test 15 - Desactivar sombras temporalmente

Cambio:

```text
PlanetLabDirectionalLight:
- Shadows Off.
```

Motivo:

```text
Sirve para separar dientes de silueta real de dientes de shadow map.
```

Accion de prueba:

```text
Probar esfera y planeta.
Mirar si los dientes solo estaban en frontera luz/sombra.
```

Exito:

```text
Desaparecen dientes asociados a sombra.
```

Reversa si no funciona:

```text
Volver sombras al estado anterior.
```

### Test 16 - Subir resolucion/cascadas de sombras

Cambio:

```text
PC_RPAsset o Mobile_RPAsset segun plataforma:
- Aumentar shadow map/atlas resolution.
- Probar mas cascadas si aplica.
- Reducir Shadow Distance si procede.
```

Motivo:

```text
Si Test 15 confirma que el problema son sombras, se prueba calidad de shadow map de forma aislada.
```

Accion de prueba:

```text
Probar solo despues de confirmar que sombras eran el origen.
```

Exito:

```text
Sombras menos dentadas sin tocar contorno de geometria.
```

Reversa si no funciona:

```text
Volver valores de sombras previos.
```

### Test 17 - Desactivar Native Render Pass

Cambio:

```text
PC_Renderer o Mobile_Renderer:
- m_UseNativeRenderPass de 1 a 0.
```

Motivo:

```text
En algunas rutas de plataforma, native render pass puede cambiar resolves, stores o intermedios. Se prueba solo si MSAA/render scale/postproceso no explican el problema.
```

Accion de prueba:

```text
Probar plataforma afectada.
```

Exito:

```text
Mejora atribuible a ruta interna de render.
```

Reversa si no funciona:

```text
Volver m_UseNativeRenderPass a 1.
```

### Test 18 - Forzar Intermediate Texture

Cambio:

```text
PC_Renderer o Mobile_Renderer:
- Cambiar Intermediate Texture Mode a Always.
```

Motivo:

```text
Permite comprobar si la ruta directa al backbuffer o la ruta con textura intermedia afecta al resolve final.
```

Accion de prueba:

```text
Probar contorno en plataforma afectada.
```

Exito:

```text
Mejora o cambio claro en el patron de dientes.
```

Reversa si no funciona:

```text
Volver Intermediate Texture Mode al valor previo.
```

### Test 19 - Revisar XR eye texture scale

Estado actual:

```text
Pendiente. No se ejecuta antes de tener player VR, Quest Link o APK.
```

Cambio:

```text
Crear o usar una herramienta de Lab solo si hace falta para fijar XRSettings.eyeTextureResolutionScale a 1.0 temporalmente.
```

Motivo:

```text
En XR la resolucion por ojo puede ser menor que la esperada. Si esta baja, todo tendra dientes aunque URP parezca correcto.
```

Accion de prueba:

```text
Probar en Quest Link o APK.
Mostrar valor actual antes de tocarlo.
Fijar 1.0.
Repetir visual.
```

Exito:

```text
Mejora clara en headset.
```

Reversa si no funciona:

```text
Eliminar herramienta temporal o volver valor anterior.
```

### Test 20 - Confirmar foveated rendering desactivado

Estado actual:

```text
Pendiente. No se ejecuta antes de tener player VR, Quest Link o APK.
```

Cambio:

```text
Ninguno si sigue desactivado.
Si esta activo en una build, desactivarlo para prueba.
```

Motivo:

```text
Foveated rendering reduce calidad fuera del centro. En VR puede parecer aliasing periferico.
```

Accion de prueba:

```text
Mirar objeto en centro y periferia.
Comparar con foveation desactivada.
```

Exito:

```text
La periferia mejora y el centro queda igual.
```

Reversa si no funciona:

```text
Volver al estado anterior.
```

### Test 21 - Comparar Vulkan/OpenGLES en Android

Estado actual:

```text
Pendiente. No se ejecuta antes de tener APK/Quest.
```

Cambio:

```text
ProjectSettings Android Graphics APIs:
- Probar una API cada vez.
```

Motivo:

```text
MSAA, resolve, native render pass, multiview y XR pueden variar entre APIs graficas.
```

Accion de prueba:

```text
Solo despues de tener APK/Quest.
Probar la misma escena y anotar API.
```

Exito:

```text
Una API muestra menos dientes o respeta mejor MSAA.
```

Reversa si no funciona:

```text
Volver a la API anterior.
```

### Test 22 - Descartar geometria/facetas del preview

Cambio:

```text
Ninguno o solo cambiar payload del PlanetRecipePayloadPreview.
```

Motivo:

```text
Si el problema fuera solo el planeta, podria ser poligonizacion o normales. Pero la esfera Unity escala 1 tambien falla, asi que este test queda despues.
```

Accion de prueba:

```text
Subir payload del preview.
Comparar esfera Unity escala 1.
Mirar si el diente de contorno sigue identico.
```

Exito:

```text
Si solo mejora el planeta, habia facetas.
Si esfera sigue igual, el problema principal sigue siendo AA/resolucion/configuracion.
```

Reversa:

```text
Volver payload previo si se cambio.
```

## Orden recomendado inicial

Estado:

```text
Suspendido.
No se ejecuta en Editor/PC.
```

Orden corto para atacar primero lo mas probable:

```text
00 Baseline.
01 Monitor / Game View 1x / captura sin reescalado.
02 MSAA 4x en PC_RPAsset.
06 FXAA por camara.
07 SMAA por camara.
05 Render Scale 1.25 PC.
09 Forward renderer para diagnostico.
10 Postproceso off.
11 SSAO off.
14 near clip.
15 sombras off.
```

Orden Quest/Android:

```text
Pendiente hasta entrar en 05_Quest3_Player_Setup o pruebas reales de Quest.

03 MSAA 4x Mobile.
04 Mobile Render Scale 1.0.
19 XR eye texture scale.
20 Foveated rendering.
21 Vulkan/OpenGLES.
```

## Registro de ejecucion

Se rellena durante las pruebas.

Estado actual:

```text
Pausado hasta prueba en Quest 3 / pantalla objetivo real.
```

```text
Test:
Fecha:
Plataforma: Editor PC
Cambio aplicado:
Resultado esfera escala 1:
Resultado planeta diametro 8000:
Rendimiento:
Decision:
Revertido:
Notas:
```

## TBD

```text
Confirmar visualmente si los dientes estan en contorno, sombras, interiores o todos.
Confirmar si el problema se observa igual en Game View 1x.
Confirmar si el problema depende del monitor, resolucion nativa, escala de Windows o escalado de GPU/pantalla.
Cuando exista player VR, confirmar de forma separada si el problema se reproduce en Quest Link y APK.
Confirmar el valor real de MSAA mostrado por Inspector para m_MSAA en URP 17.3.
Definir si se permitira una herramienta temporal de Lab para leer/aplicar XRSettings.eyeTextureResolutionScale.
Definir presupuesto aceptable para MSAA/Render Scale en Quest 3 una vez se encuentre la causa.
```
