Shader "Game/Buildings/BuildingDanceLit"
{
    Properties
    {
        [Header(Surface)]
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0, 1)) = 0.35

        [Header(Normal Map)]
        [Toggle(_NORMALMAP)] _UseNormalMap ("Use Normal Map", Float) = 0
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1

        [Header(Emission)]
        [Toggle(_EMISSION)] _UseEmission ("Use Emission", Float) = 0
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionStrength ("Emission Strength", Range(0, 10)) = 1

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

            // SM 3.5 is the safe mobile floor for URP forward lighting (GLES3 / Vulkan / Metal).
            #pragma target 3.5

            #pragma vertex Vert
            #pragma fragment Frag

            // Lighting keywords. Soft-shadow + additional-light-shadow only touch the
            // fragment stage, so multi_compile_fragment keeps the vertex variant count
            // (and mobile shader memory) down.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            // Optional maps. _local keeps the keywords per-material (they don't eat
            // global keyword slots), and shader_feature only compiles the variants a
            // material actually uses — so a building with no normal/emission map pays
            // for neither the sample nor the interpolators.
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _EMISSION

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
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            #ifdef _NORMALMAP
                float4 tangentWS : TEXCOORD4;   // xyz = tangent, w = sign
                float2 normalUV : TEXCOORD5;
            #endif
            #ifdef _EMISSION
                float2 emissionUV : TEXCOORD6;
            #endif
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            // Every non-texture property must live here for the SRP Batcher to batch
            // the (many) buildings together — including the two toggle floats, even
            // though only their keywords are read in HLSL.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _EmissionMap_ST;
                float4 _EmissionColor;

                float _UseNormalMap;
                float _UseEmission;

                float _NormalStrength;
                float _Smoothness;
                float _EmissionStrength;

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
            #ifdef _NORMALMAP
                half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.normalUV);
                half3 normalTS = UnpackNormalScale(normalSample, _NormalStrength);

                half3 normalWS = normalize(input.normalWS);
                half3 tangentWS = normalize(input.tangentWS.xyz);
                half3 bitangentWS = cross(normalWS, tangentWS) * input.tangentWS.w;

                half3x3 tangentToWorld = half3x3(tangentWS, bitangentWS, normalWS);
                return normalize(mul(normalTS, tangentToWorld));
            #else
                // No normal map → use the interpolated geometric normal. Skips the
                // texture fetch and the whole tangent-space transform.
                return normalize(input.normalWS);
            #endif
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 positionOS = ApplyBuildingDance(input.positionOS.xyz);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = TransformWorldToShadowCoord(positionInputs.positionWS);

            #ifdef _NORMALMAP
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w);
                output.normalUV = TRANSFORM_TEX(input.uv, _BumpMap);
            #else
                // Normal only — no tangent/bitangent computed in the vertex stage.
                output.normalWS = GetVertexNormalInputs(input.normalOS).normalWS;
            #endif

            #ifdef _EMISSION
                output.emissionUV = TRANSFORM_TEX(input.uv, _EmissionMap);
            #endif

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 albedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                half3 emission = half3(0, 0, 0);
            #ifdef _EMISSION
                emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.emissionUV).rgb
                    * _EmissionColor.rgb
                    * _EmissionStrength;
            #endif

                half3 normalWS = GetNormalWS(input);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = 0;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedoSample.rgb * _BaseColor.rgb;
                surfaceData.alpha = albedoSample.a * _BaseColor.a;
                surfaceData.metallic = 0;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1;
                surfaceData.emission = emission;
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
