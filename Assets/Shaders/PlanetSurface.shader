Shader "MarchingCubesPlanet/Planet/Surface"
{
    Properties
    {
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
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_PlanetSurfaceAtlas);
            SAMPLER(sampler_PlanetSurfaceAtlas);
            float4 _PlanetSurfaceAtlas_TexelSize;
            float _UsePlanetSurfaceAtlas;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_UsePlanetSurfaceAtlas > 0.5)
                {
                    float2 atlasUv = input.uv;
                    float atlasWidth = max(_PlanetSurfaceAtlas_TexelSize.z, 1.0);
                    atlasUv.x = (floor(saturate(atlasUv.x) * atlasWidth) + 0.5) / atlasWidth;
                    return SAMPLE_TEXTURE2D(_PlanetSurfaceAtlas, sampler_PlanetSurfaceAtlas, atlasUv);
                }

                return input.color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
