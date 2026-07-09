Shader "Game/Ghosts/BackgroundGhostBob"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)
        _EmissionStrength ("Emission Strength", Range(0, 10)) = 1
        _Alpha ("Alpha", Range(0, 1)) = 1

        [Header(Bob)]
        _BobAmplitude ("Bob Amplitude", Range(0, 1)) = 0.08
        _BobSpeed ("Bob Speed", Range(0, 10)) = 1.5
        _PhaseX ("Phase From World X", Float) = 2.13
        _PhaseZ ("Phase From World Z", Float) = 1.37
        _ManualPhase ("Manual Phase", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 baseUV : TEXCOORD0;
                float2 emissionUV : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EmissionMap_ST;
                float4 _EmissionColor;
                float _EmissionStrength;
                float _Alpha;
                float _BobAmplitude;
                float _BobSpeed;
                float _PhaseX;
                float _PhaseZ;
                float _ManualPhase;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 objectWorldPosition = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float phase = objectWorldPosition.x * _PhaseX
                    + objectWorldPosition.z * _PhaseZ
                    + _ManualPhase;

                float bobOffset = sin(_Time.y * _BobSpeed + phase) * _BobAmplitude;
                float3 positionOS = input.positionOS.xyz;
                positionOS.y += bobOffset;

                output.positionHCS = TransformObjectToHClip(positionOS);
                output.baseUV = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.emissionUV = input.uv * _EmissionMap_ST.xy + _EmissionMap_ST.zw;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
                half4 emissionSample = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.emissionUV);

                half3 baseColor = baseSample.rgb * _BaseColor.rgb;
                half3 emission = emissionSample.rgb * _EmissionColor.rgb * _EmissionStrength;
                half alpha = saturate(baseSample.a * _BaseColor.a * _Alpha);

                return half4(baseColor + emission, alpha);
            }
            ENDHLSL
        }
    }
}
