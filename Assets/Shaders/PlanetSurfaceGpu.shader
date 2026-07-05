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
            Cull Off
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

            StructuredBuffer<PlanetTriangleGpuVertex> _PlanetTriangleVertices;

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half4 color : COLOR;
                half fogFactor : TEXCOORD3;
                float active : TEXCOORD4;
            };

            Varyings vert(uint vertexID : SV_VertexID)
            {
                PlanetTriangleGpuVertex input = _PlanetTriangleVertices[vertexID];
                Varyings output;
                float3 positionWS = input.positionAndActive.xyz;
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = input.uvAndMaterial.xy;
                output.normalWS = NormalizeNormalPerVertex(input.normalAndFlags.xyz);
                output.color = half4(input.color);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                output.active = input.positionAndActive.w;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(input.active - 0.5);
                half4 albedoAlpha;
                if (_UsePlanetSurfaceAtlas > 0.5)
                {
                    float2 atlasUv = input.uv;
                    float atlasWidth = max(_PlanetSurfaceAtlas_TexelSize.z, 1.0);
                    atlasUv.x = (floor(saturate(atlasUv.x) * atlasWidth) + 0.5) / atlasWidth;
                    albedoAlpha = SAMPLE_TEXTURE2D(_PlanetSurfaceAtlas, sampler_PlanetSurfaceAtlas, atlasUv) * _BaseColor;
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
