Shader "Custom/WatercolorAnimeProp"
{
    Properties
    {
        _LightColor      ("Light Tone", Color) = (0.65, 0.18, 0.18, 1)
        _MidColor        ("Mid Tone",   Color) = (0.45, 0.08, 0.08, 1)
        _ShadowColor     ("Shadow Tone",Color) = (0.20, 0.02, 0.02, 1)

        _RimColor        ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower        ("Rim Power", Float) = 2.0

        _ContactShadowTint ("Contact Shadow Tint", Color) = (0.1, 0.0, 0.0, 1)
        _ContactShadowStrength ("Contact Shadow Strength", Range(0,1)) = 0.5

        _Smoothness ("Shading Smoothness", Range(0.1, 2)) = 0.7
        _DesaturateShadows ("Shadow Desaturation", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            float4 _LightColor;
            float4 _MidColor;
            float4 _ShadowColor;

            float4 _RimColor;
            float  _RimPower;

            float4 _ContactShadowTint;
            float  _ContactShadowStrength;

            float _Smoothness;
            float _DesaturateShadows;

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                return o;
            }

            float3 DesaturateWC(float3 color, float amount)
            {
                float grey = dot(color, float3(0.3, 0.59, 0.11));
                return lerp(color, grey.xxx, amount);
            }

            float4 frag (Varyings i) : SV_Target
            {
                Light light = GetMainLight();
                float3 L = normalize(-light.direction);
                float3 N = normalize(i.normalWS);

                float NdotL = saturate(dot(N, L));

                // Smooth watercolor-like ramp
                float t = pow(NdotL, _Smoothness);

                // Base toon blending
                float3 shadow = _ShadowColor.rgb;
                float3 mid    = _MidColor.rgb;
                float3 lightC = _LightColor.rgb;

                float3 sm = lerp(shadow, mid, t);
                float3 lit = lerp(sm, lightC, t);

                // Soft desaturation in shadow areas
                float shadowAmount = 1.0 - NdotL;
                float3 shaded = DesaturateWC(lit, shadowAmount * _DesaturateShadows);

                float3 finalColor = shaded;

                // --- Soft Rim Light ---
                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
                float rim = 1.0 - saturate(dot(V, N));
                rim = pow(rim, _RimPower);
                finalColor += rim * _RimColor.rgb;

                // --- Contact Shadow Tint ---
                float contact = (1.0 - NdotL);
                finalColor -= (_ContactShadowTint.rgb * contact * _ContactShadowStrength);

                return float4(finalColor, 1);
            }
            ENDHLSL
        }
    }
}
