Shader "MarchingCubesPlanet/Planet/WaterGpu"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.08, 0.42, 0.72, 0.78)
        _SpecColor ("Specular Color", Color) = (0.18, 0.26, 0.30, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.72
        _Metallic ("Metallic", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardLitGpuWater"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 _BaseColor;
            half4 _SpecColor;
            half _Smoothness;
            half _Metallic;

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
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
                float active : TEXCOORD3;
            };

            Varyings vert(uint vertexID : SV_VertexID)
            {
                PlanetTriangleGpuVertex input = _PlanetTriangleVertices[vertexID];
                Varyings output;
                output.positionWS = input.positionAndActive.xyz;
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = NormalizeNormalPerVertex(input.normalAndFlags.xyz);
                output.color = half4(input.color);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                output.active = input.positionAndActive.w;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(input.active - 0.5);

                half4 albedoAlpha = _BaseColor * input.color;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedoAlpha.rgb;
                surfaceData.specular = _SpecColor.rgb;
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
                color.a = albedoAlpha.a;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
