Shader "MarchingCubesPlanet/Planet/SurfaceGpu"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.35
        _Metallic ("Metallic", Range(0, 1)) = 0
        _PlanetSurfaceAtlas ("Planet Surface Atlas", 2D) = "white" {}
        _UsePlanetSurfaceAtlas ("Use Planet Surface Atlas", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLitGpu"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Smoothness;
            half _Metallic;
            float _PlanetGpuVertexLayout;
            float4x4 _PlanetGridToWorldMatrix;
            int _PlanetLayerCount;
            float4 _PlanetLayerColors[16];
            float4 _PlanetLayerUnderwaterColors[16];
            float4 _PlanetLayerSurfaceColors[16];
            float4 _PlanetLayerSeeds[16];

            TEXTURE2D(_PlanetSurfaceAtlas);
            SAMPLER(sampler_PlanetSurfaceAtlas);
            float4 _PlanetSurfaceAtlas_TexelSize;
            float _UsePlanetSurfaceAtlas;

            struct PlanetTriangleGpuVertex
            {
                float4 positionAndActive;
                float4 normalAndFlags;
                float4 uvAndMaterial;
                float4 color;
            };

            struct PlanetMarchingCubesVertex
            {
                float4 positionAndMaterial;
                float4 normalAndDiagnostic;
            };

            StructuredBuffer<PlanetTriangleGpuVertex> _PlanetTriangleVertices;
            StructuredBuffer<PlanetMarchingCubesVertex> _PlanetMarchingCubesVertices;

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half4 color : COLOR;
                half fogFactor : TEXCOORD3;
                float active : TEXCOORD4;
                float materialId : TEXCOORD5;
            };

            float3 EvaluateMaterialColor(float packedMaterial)
            {
                int layerCount = clamp(_PlanetLayerCount, 1, 16);
                int packed = max(0, (int)round(packedMaterial));
                int layerIndex = 0;
                int appearanceState = 0;
                if (packed >= 16384)
                {
                    int encoded = packed - 16384;
                    layerIndex = clamp((encoded / 256) % 16, 0, layerCount - 1);
                    appearanceState = (encoded / 4096) % 4;
                }
                else
                {
                    int materialId = packed % 256;
                    [loop]
                    for (int i = 0; i < layerCount; i++)
                    {
                        if (abs(_PlanetLayerSeeds[i].y - (float)materialId) < 0.5)
                        {
                            layerIndex = i;
                        }
                    }
                }

                if (appearanceState == 1)
                {
                    return _PlanetLayerUnderwaterColors[layerIndex].rgb;
                }

                if (appearanceState == 2)
                {
                    return _PlanetLayerSurfaceColors[layerIndex].rgb;
                }

                return _PlanetLayerColors[layerIndex].rgb;
            }

            Varyings vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                float3 positionWS;
                float3 normalWS;
                float2 uv;
                half4 color;
                float active;
                float3 gridPosition;
                float materialId;

                if (_PlanetGpuVertexLayout > 0.5)
                {
                    PlanetMarchingCubesVertex input = _PlanetMarchingCubesVertices[vertexID];
                    gridPosition = input.positionAndMaterial.xyz;
                    positionWS = mul(_PlanetGridToWorldMatrix, float4(gridPosition, 1.0)).xyz;
                    normalWS = normalize(mul((float3x3)_PlanetGridToWorldMatrix, input.normalAndDiagnostic.xyz));
                    uv = float2(0.5, 0.5);
                    color = half4(1.0h, 1.0h, 1.0h, 1.0h);
                    active = 1.0;
                    materialId = input.positionAndMaterial.w;
                }
                else
                {
                    PlanetTriangleGpuVertex input = _PlanetTriangleVertices[vertexID];
                    positionWS = input.positionAndActive.xyz;
                    normalWS = NormalizeNormalPerVertex(input.normalAndFlags.xyz);
                    uv = input.uvAndMaterial.xy;
                    color = half4(input.color);
                    active = input.positionAndActive.w;
                    gridPosition = input.positionAndActive.xyz;
                    materialId = input.uvAndMaterial.z;
                }

                output.positionHCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = uv;
                output.normalWS = normalWS;
                output.color = color;
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                output.active = active;
                output.materialId = materialId;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(input.active - 0.5);
                half4 albedoAlpha;
                if (_UsePlanetSurfaceAtlas > 0.5)
                {
                    albedoAlpha = half4(EvaluateMaterialColor(input.materialId), 1.0h) * _BaseColor;
                }
                else
                {
                    float2 baseUv = TRANSFORM_TEX(input.uv, _BaseMap);
                    albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUv) * _BaseColor * input.color;
                }

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedoAlpha.rgb;
                surfaceData.specular = half3(0.16h, 0.16h, 0.16h);
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
                surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
                surfaceData.occlusion = 1.0h;
                surfaceData.alpha = albedoAlpha.a;
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionHCS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = half3(0.0h, 0.0h, 0.0h);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
