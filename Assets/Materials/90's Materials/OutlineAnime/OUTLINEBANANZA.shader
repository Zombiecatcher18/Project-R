Shader "Custom/ToonWithOutlineURP"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _LightSteps ("Light Steps", Range(1,5)) = 2

        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness ("Outline Thickness", Range(0,0.2)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        /////////////////////////////////////////////////////////////
        // PASS 1 — OUTLINE (inverted hull)
        /////////////////////////////////////////////////////////////
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="UniversalForward" }

            Cull Front  // render backfaces only

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _OutlineColor;
            float  _OutlineThickness;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;

                // Inflate mesh uniformly (perfect corners)
                float3 scaled = v.positionOS.xyz * (1.0 + _OutlineThickness);

                o.positionHCS = TransformObjectToHClip(scaled);
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                return _OutlineColor;
            }

            ENDHLSL
        }

        /////////////////////////////////////////////////////////////
        // PASS 2 — TOON LIGHTING
        /////////////////////////////////////////////////////////////
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _BaseColor;
            float _LightSteps;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float3 lightDir = normalize(_MainLightPosition.xyz);
                float NdotL = saturate(dot(i.normalWS, lightDir));

                // Toon banding
                float stepped = floor(NdotL * _LightSteps) / _LightSteps;

                return float4(_BaseColor.rgb * stepped, 1);
            }

            ENDHLSL
        }
    }
}
