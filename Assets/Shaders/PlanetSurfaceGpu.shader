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
            float _PlanetGridRadius;
            float _PlanetWorldScale;
            float _PlanetSeed;
            int _PlanetLayerCount;
            float4 _PlanetLayerColors[16];
            float4 _PlanetLayerHeights[16];
            float4 _PlanetLayerNoise[16];
            float4 _PlanetLayerFlags[16];
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
                float4 positionAndCase;
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
                float3 gridPosition : TEXCOORD5;
            };

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise3D(float3 p)
            {
                float3 cell = floor(p);
                float3 local = frac(p);
                float3 u = local * local * (3.0 - 2.0 * local);

                float n000 = Hash13(cell + float3(0.0, 0.0, 0.0));
                float n100 = Hash13(cell + float3(1.0, 0.0, 0.0));
                float n010 = Hash13(cell + float3(0.0, 1.0, 0.0));
                float n110 = Hash13(cell + float3(1.0, 1.0, 0.0));
                float n001 = Hash13(cell + float3(0.0, 0.0, 1.0));
                float n101 = Hash13(cell + float3(1.0, 0.0, 1.0));
                float n011 = Hash13(cell + float3(0.0, 1.0, 1.0));
                float n111 = Hash13(cell + float3(1.0, 1.0, 1.0));

                float n00 = lerp(n000, n100, u.x);
                float n10 = lerp(n010, n110, u.x);
                float n01 = lerp(n001, n101, u.x);
                float n11 = lerp(n011, n111, u.x);
                float n0 = lerp(n00, n10, u.y);
                float n1 = lerp(n01, n11, u.y);
                return lerp(n0, n1, u.z);
            }

            float HeightMask(float height01, float4 heightParams)
            {
                float minHeight = heightParams.x;
                float maxHeight = max(heightParams.y, minHeight);
                float minFalloff = max(0.00001, heightParams.z);
                float maxFalloff = max(0.00001, heightParams.w);
                float lower = smoothstep(minHeight, minHeight + minFalloff, height01);
                float upper = 1.0 - smoothstep(maxHeight - maxFalloff, maxHeight, height01);
                return saturate(lower * upper);
            }

            float AltitudeCoverage(float height01, float altitudeBias)
            {
                if (altitudeBias > 0.0)
                {
                    return lerp(1.0, saturate(height01), saturate(altitudeBias));
                }

                return lerp(1.0, 1.0 - saturate(height01), saturate(-altitudeBias));
            }

            float LayerMassMask(float3 gridPosition, float height01, int layerIndex)
            {
                float4 noiseParams = _PlanetLayerNoise[layerIndex];
                float4 heightParams = _PlanetLayerHeights[layerIndex];
                float4 flags = _PlanetLayerFlags[layerIndex];
                float coverage = saturate(noiseParams.x * AltitudeCoverage(height01, flags.w));
                float minScale = max(0.01, noiseParams.y);
                float maxScale = max(minScale, noiseParams.z);
                float coherence = saturate(noiseParams.w);
                float seedOffset = _PlanetLayerSeeds[layerIndex].x + _PlanetSeed;
                float3 worldMeters = gridPosition * max(0.0001, _PlanetWorldScale);
                float smallMass = ValueNoise3D(worldMeters / minScale + seedOffset);
                float largeMass = ValueNoise3D(worldMeters / maxScale + seedOffset * 1.37);
                float mass = lerp(smallMass, largeMass, coherence);
                float threshold = 1.0 - coverage;
                float materialMask = smoothstep(threshold, 1.0, mass);
                return materialMask * HeightMask(height01, heightParams) * saturate(flags.z);
            }

            float3 EvaluateLayerColor(float3 gridPosition)
            {
                float height01 = length(gridPosition) / max(0.0001, _PlanetGridRadius);
                float3 color = _PlanetLayerColors[0].rgb;
                int layerCount = clamp(_PlanetLayerCount, 1, 16);
                [loop]
                for (int i = 1; i < layerCount; i++)
                {
                    float4 flags = _PlanetLayerFlags[i];
                    if (flags.x > 0.5 && (abs(flags.y - 1.0) < 0.5 || abs(flags.y - 2.0) < 0.5))
                    {
                        float mask = LayerMassMask(gridPosition, height01, i);
                        if (abs(flags.y - 2.0) < 0.5)
                        {
                            float contactMask = smoothstep(0.02, 0.16, mask);
                            float coreMask = smoothstep(0.22, 0.42, mask);
                            float contactBand = saturate(contactMask - coreMask);
                            color = lerp(color, color * 0.72, contactBand);
                            color = lerp(color, _PlanetLayerColors[i].rgb, coreMask);
                            continue;
                        }

                        color = lerp(color, _PlanetLayerColors[i].rgb, mask);
                    }
                }

                return color;
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

                if (_PlanetGpuVertexLayout > 0.5)
                {
                    PlanetMarchingCubesVertex input = _PlanetMarchingCubesVertices[vertexID];
                    gridPosition = input.positionAndCase.xyz;
                    positionWS = mul(_PlanetGridToWorldMatrix, float4(gridPosition, 1.0)).xyz;
                    normalWS = normalize(mul((float3x3)_PlanetGridToWorldMatrix, input.normalAndDiagnostic.xyz));
                    uv = float2(0.5, 0.5);
                    color = half4(1.0h, 1.0h, 1.0h, 1.0h);
                    active = 1.0;
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
                }

                output.positionHCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = uv;
                output.normalWS = normalWS;
                output.color = color;
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                output.active = active;
                output.gridPosition = gridPosition;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(input.active - 0.5);
                half4 albedoAlpha;
                if (_UsePlanetSurfaceAtlas > 0.5)
                {
                    albedoAlpha = half4(EvaluateLayerColor(input.gridPosition), 1.0h) * _BaseColor;
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
