Shader "Game/Buildings/BuildingDanceLit"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1

        _Smoothness ("Smoothness", Range(0, 1)) = 0.35

        [Header(Building Height)]
        _BottomY ("Bottom Y", Float) = 0
        _TopY ("Top Y", Float) = 5

        [Header(Dance)]
        _DanceIntensity ("Dance Intensity", Range(0, 1)) = 1
        _DanceAmount ("Side Bend Amount", Range(0, 1)) = 0.15
        _DanceSpeed ("Dance Speed", Range(0, 10)) = 2
        _DanceOffset ("Dance Offset", Float) = 0

        [Header(Squash And Stretch)]
        _SquashAmount ("Squash Amount", Range(0, 0.5)) = 0.08
        _WidthBoost ("Width Boost", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;

                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;

                float2 uv : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NormalMap_ST;

                float _NormalStrength;
                float _Smoothness;

                float _BottomY;
                float _TopY;

                float _DanceIntensity;
                float _DanceAmount;
                float _DanceSpeed;
                float _DanceOffset;

                float _SquashAmount;
                float _WidthBoost;
            CBUFFER_END

            float GetHeightMask(float positionY)
            {
                float heightRange = max(0.0001, _TopY - _BottomY);
                float heightMask = saturate((positionY - _BottomY) / heightRange);

                return heightMask;
            }

            float3 ApplyBuildingDance(float3 positionOS)
            {
                float heightMask = GetHeightMask(positionOS.y);

                float phase = _Time.y * _DanceSpeed + _DanceOffset;

                float sideWave = sin(phase);
                float squashWave = sin(phase * 2.0) * 0.5 + 0.5;

                float squash = squashWave * _SquashAmount * _DanceIntensity;

                float distanceFromBottom = positionOS.y - _BottomY;
                positionOS.y = _BottomY + distanceFromBottom * (1.0 - squash);

                float widthScale = 1.0 + squash * _WidthBoost;

                positionOS.x *= widthScale;
                positionOS.z *= widthScale;

                positionOS.x += sideWave * _DanceAmount * heightMask * _DanceIntensity;

                return positionOS;
            }

            half3 GetNormalWS(Varyings input)
            {
                half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                half3 normalTS = UnpackNormalScale(normalSample, _NormalStrength);

                half3 normalWS = normalize(input.normalWS);
                half3 tangentWS = normalize(input.tangentWS.xyz);
                half3 bitangentWS = cross(normalWS, tangentWS) * input.tangentWS.w;

                half3x3 tangentToWorld = half3x3(tangentWS, bitangentWS, normalWS);
                normalWS = normalize(mul(normalTS, tangentToWorld));

                return normalWS;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionOS = ApplyBuildingDance(input.positionOS.xyz);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;

                output.normalWS = normalInputs.normalWS;
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w);

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = TransformWorldToShadowCoord(positionInputs.positionWS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 albedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                half3 normalWS = GetNormalWS(input);

                InputData inputData;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = 0;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData;
                surfaceData.albedo = albedoSample.rgb;
                surfaceData.alpha = 1;

                surfaceData.metallic = 0;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1;
                surfaceData.emission = half3(0, 0, 0);

                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;
                surfaceData.normalTS = half3(0, 0, 1);

                return UniversalFragmentPBR(inputData, surfaceData);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}