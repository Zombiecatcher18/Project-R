Shader "Custom/UnlitToonWatercolor_Full_URP"
{
    Properties
    {
        _BaseMap("Base Map (Albedo)", 2D) = "white" {}
        _Color("Color Tint", Color) = (1,1,1,1)

        _RampTex("Ramp (1D)", 2D) = "white" {}
        _NoiseTex("Noise (tileable)", 2D) = "white" {}
        _PaperTex("Paper (grayscale)", 2D) = "white" {}
        _StainTex("Stain (RGBA)", 2D) = "white" {}

        _NoiseScale("Noise Scale", Float) = 4.0
        _NoiseStrength("Noise Strength", Range(0,1)) = 0.08
        _BleedAmount("Soft Bleed Amount", Range(0,1)) = 0.35
        _BleedSamples("Bleed Samples", Range(1,6)) = 3

        _PaperStrength("Paper Strength", Range(0,1)) = 0.35
        _PaperTiling("Paper Tiling", Float) = 2.0

        _Vibrance("Vibrance", Range(0,2)) = 1.15
        _Saturation("Saturation", Range(0,2)) = 1.1

        _CelSteps("Cel Steps", Range(2,4)) = 3
        _HardStep("Hard Step (1=hard)", Range(0,1)) = 0.0

        _RimColor("Rim Color", Color) = (1,0.9,0.8,1)
        _RimStrength("Rim Strength", Range(0,2)) = 0.7
        _RimPower("Rim Power", Range(0.1,8)) = 2.0

        _DistanceFadeStart("Fade Start", Float) = 20.0
        _DistanceFadeEnd("Fade End", Float) = 60.0
        _DistanceDesaturate("Desaturate Far", Range(0,1)) = 0.6
        _DistanceDarken("Darken Far", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "FORWARD"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 posWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
            };

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            float4 _Color;

            sampler2D _RampTex;
            sampler2D _NoiseTex;
            sampler2D _PaperTex;
            sampler2D _StainTex;

            float _NoiseScale;
            float _NoiseStrength;
            float _BleedAmount;
            float _BleedSamples;

            float _PaperStrength;
            float _PaperTiling;

            float _Vibrance;
            float _Saturation;

            float _CelSteps;
            float _HardStep;

            float4 _RimColor;
            float _RimStrength;
            float _RimPower;

            float _DistanceFadeStart;
            float _DistanceFadeEnd;
            float _DistanceDesaturate;
            float _DistanceDarken;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                float4 worldPos4 = mul(unity_ObjectToWorld, v.vertex);
                o.posWS = worldPos4.xyz;
                o.viewDirWS = normalize(_WorldSpaceCameraPos - o.posWS);
                return o;
            }

            // helper: sample ramp with NdotL and steps/hard
            float3 SampleCelRamp(float NdotL, int steps, int hard)
            {
                if (hard != 0)
                {
                    float q = floor(NdotL * steps) / max(1, (steps - 1));
                    return tex2D(_RampTex, float2(q,0.5)).rgb;
                }
                else
                {
                    float band = NdotL * (steps - 1);
                    int i = (int)floor(band);
                    float local = band - i;
                    float leftX = (float)i / max(1, (steps - 1));
                    float rightX = (float)min(i+1, steps-1) / max(1, (steps-1));
                    float3 leftC = tex2D(_RampTex, float2(leftX,0.5)).rgb;
                    float3 rightC = tex2D(_RampTex, float2(rightX,0.5)).rgb;
                    return lerp(leftC, rightC, smoothstep(0,1,local));
                }
            }

            // vibrance/saturation helper
            float3 AdjustVibrance(float3 color, float vibrance, float saturation)
            {
                float lum = dot(color, float3(0.2126,0.7152,0.0722));
                float3 diff = color - lum.xxx;
                color = lum.xxx + diff * saturation;
                if (vibrance > 1.0)
                {
                    float v = saturate((vibrance - 1.0)/1.0);
                    float3 gamma = pow(max(color,0.0), float3(1.0/2.2,1.0/2.2,1.0/2.2));
                    color = lerp(color, gamma, v);
                }
                return color;
            }

            // soft bleed sampling
            float3 ApplyBleed(float2 uv, float bleedAmount, int samples)
            {
                float samplesF = max(1.0, (float)samples);
                float angleStep = 6.2831853 / samplesF;
                float3 accum = float3(0,0,0);
                float total = 0.0;

                float3 center = tex2D(_BaseMap, uv).rgb;
                accum += center * 1.2;
                total += 1.2;

                for (int s=1; s<=samples; s++)
                {
                    float a = s * angleStep;
                    float2 off = float2(cos(a), sin(a)) * (bleedAmount * s * 0.012);
                    float3 c = tex2D(_BaseMap, uv + off).rgb;
                    float n = tex2D(_NoiseTex, (uv + off) * _NoiseScale).r;
                    float3 stain = tex2D(_StainTex, (uv + off) + (n - 0.5)*0.05).rgb;
                    c = lerp(c, c * stain, 0.5);
                    accum += c;
                    total += 1.0;
                }

                return accum / max(0.0001, total);
            }

            float frag (v2f i) : SV_Target
            {
                int bleedSamples = max(1, (int)round(_BleedSamples));
                float3 baseCol = ApplyBleed(i.uv, _BleedAmount, bleedSamples);
                baseCol *= _Color.rgb;

                // noise warp for stain
                float noise = tex2D(_NoiseTex, i.uv * _NoiseScale).r;
                float2 warpedUV = i.uv + (noise - 0.5) * _NoiseStrength;
                float4 stainSample = tex2D(_StainTex, warpedUV);
                baseCol = lerp(baseCol, baseCol * stainSample.rgb, stainSample.a * 0.85);

                // simple directional light (main directional)
                float3 L = normalize(-_WorldSpaceLightPos0.xyz); // _WorldSpaceLightPos0.xyz is -dir for directional, but some builtin setups vary. We'll normalize -vector to get light dir.
                float3 N = normalize(i.normalWS);
                float NdotL = saturate(dot(N, L));

                int steps = max(2, (int)round(_CelSteps));
                int hard = (int)round(_HardStep);
                float3 toon = SampleCelRamp(NdotL, steps, hard);

                float3 lit = baseCol * toon;

                // rim
                float ndv = saturate(dot(N, normalize(i.viewDirWS)));
                float rim = pow(saturate(1.0 - ndv), _RimPower) * _RimStrength;
                lit += _RimColor.rgb * rim;

                // vibrance/saturation
                lit = AdjustVibrance(lit, _Vibrance, _Saturation);

                // paper grain
                float paper = tex2D(_PaperTex, i.uv * _PaperTiling).r;
                lit = lerp(lit, lit * paper, _PaperStrength);

                // distance fade
                float dist = distance(i.posWS, _WorldSpaceCameraPos);
                float fade = saturate((dist - _DistanceFadeStart) / max(0.001, (_DistanceFadeEnd - _DistanceFadeStart)));
                float grey = dot(lit, float3(0.2126,0.7152,0.0722));
                float3 grey3 = float3(grey,grey,grey);
                float3 darkened = lit * (1.0 - _DistanceDarken);
                float3 desat = lerp(darkened, grey3, _DistanceDesaturate);
                lit = lerp(lit, desat, fade);

                return float4(lit, 1.0);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
