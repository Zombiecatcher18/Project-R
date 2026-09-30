Shader "Custom/ToonWatercolor_URP_Full_Fixed"
{
    Properties
    {
        // Base
        _BaseMap("Base Map (Albedo)", 2D) = "white" {}
        _Color("Color Tint", Color) = (1,1,1,1)

        // Lookups / detail
        _RampTex("Ramp (1D)", 2D) = "white" {}
        _NoiseTex("Noise (tileable)", 2D) = "white" {}
        _PaperTex("Paper (grayscale)", 2D) = "white" {}
        _StainTex("Stain (RGBA)", 2D) = "white" {}

        // Artistic controls
        _NoiseScale("Noise Scale", Float) = 4.0
        _NoiseStrength("Noise Strength", Range(0,1)) = 0.08
        _BleedAmount("Soft Bleed Amount", Range(0,1)) = 0.35
        _BleedSamples("Bleed Samples (1-6)", Range(1,6)) = 3

        _PaperStrength("Paper Strength", Range(0,1)) = 0.35
        _PaperTiling("Paper Tiling", Float) = 2.0

        _Vibrance("Vibrance (color pop)", Range(0,2)) = 1.15
        _Saturation("Saturation", Range(0,2)) = 1.1

        // Cel / toon controls
        _CelSteps("Cel Steps (2-4)", Range(2,4)) = 3
        _HardStep("Hard Steps (1 = hard)", Range(0,1)) = 0.0

        // Rim / outline
        _RimColor("Rim Color", Color) = (1,0.9,0.8,1)
        _RimStrength("Rim Strength", Range(0,2)) = 0.7
        _RimPower("Rim Power", Range(0.1,8)) = 2.0

        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width (object units)", Range(0,0.05)) = 0.01

        // Distance fade
        _DistanceFadeStart("Fade Start (world units)", Float) = 20.0
        _DistanceFadeEnd("Fade End (world units)", Float) = 60.0
        _DistanceDesaturate("Desaturate Far", Range(0,1)) = 0.6
        _DistanceDarken("Darken Far", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalRenderPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        // ---------- OUTLINE PASS (inverted hull) ----------
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode" = "Always" }
            Cull Front
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata_outline
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct v2f_outline
            {
                float4 posH : SV_POSITION;
            };

            float _OutlineWidth;
            float4 _OutlineColor;

            v2f_outline vertOutline(appdata_outline v)
            {
                v2f_outline o;

                // Expand in object space along normal
                float3 pos = v.positionOS + v.normalOS * _OutlineWidth;

                // URP transform to clip space
                o.posH = TransformObjectToHClip(pos);

                return o;
            }

            float4 fragOutline(v2f_outline i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ---------- MAIN PAINT PASS ----------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ATTRIBUTES / VARYINGS
            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 posWS       : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
            };

            // TEXTURES & UNIFORMS
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            float4 _Color;

            TEXTURE2D(_RampTex);
            SAMPLER(sampler_RampTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_PaperTex);
            SAMPLER(sampler_PaperTex);
            TEXTURE2D(_StainTex);
            SAMPLER(sampler_StainTex);

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

            float4 _OutlineColor; // kept for UI parity
            float _OutlineWidth;  // kept for UI parity

            float _DistanceFadeStart;
            float _DistanceFadeEnd;
            float _DistanceDesaturate;
            float _DistanceDarken;

            // TRANSFORMS
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.posWS = TransformObjectToWorld(IN.positionOS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                // view direction (world space)
                OUT.viewDirWS = normalize(_WorldSpaceCameraPos - OUT.posWS);
                return OUT;
            }

            // Helpers
            float3 GetMainLightDirWS()
            {
                Light main = GetMainLight();
                return main.direction; // normalized
            }

            // quantize lighting into N steps, hard or soft
            float3 SampleCelRamp(float NdotL, int steps, int hard)
            {
                if (hard != 0)
                {
                    // hard quantize
                    float q = floor(NdotL * steps) / max(1, (steps - 1));
                    return SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(q, 0.5)).rgb;
                }
                else
                {
                    // soft stepped bands: blend between adjacent ramp samples
                    float band = NdotL * (steps - 1.0);
                    int i = (int)floor(band);
                    float local = band - i; // 0..1
                    float leftX = (float)i / max(1, (steps - 1));
                    float rightX = (float)min(i + 1, steps - 1) / max(1, (steps - 1));
                    float3 leftC = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(leftX, 0.5)).rgb;
                    float3 rightC = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(rightX, 0.5)).rgb;
                    return lerp(leftC, rightC, smoothstep(0.0, 1.0, local));
                }
            }

            // vibrant color boost (simple)
            float3 AdjustVibrance(float3 color, float vibrance, float saturation)
            {
                float lum = dot(color, float3(0.2126, 0.7152, 0.0722));
                float3 diff = color - lum.xxx;
                color = lum.xxx + diff * saturation;
                // small vibrance: push midtones slightly by gamma tweak
                if (vibrance > 1.0)
                {
                    float v = saturate((vibrance - 1.0) / 1.0);
                    // Ensure non-negative base for pow to avoid warnings on negative inputs
                    float3 gamma = pow(max(color, 0.0), float3(1.0 / 2.2, 1.0 / 2.2, 1.0 / 2.2));
                    color = lerp(color, gamma, v);
                }
                return color;
            }

            // soft watercolor bleed: multiple samples around UV
            float3 ApplyBleed(float2 uv, float bleedAmount, int samples)
            {
                float samplesF = max(1.0, (float)samples);
                float angleStep = 6.2831853 / samplesF;
                float3 accum = float3(0.0, 0.0, 0.0);
                float total = 0.0;

                // center sample slightly stronger
                float3 center = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                accum += center * 1.2;
                total += 1.2;

                for (int s = 1; s <= samples; s++)
                {
                    float a = s * angleStep;
                    float2 off = float2(cos(a), sin(a)) * (bleedAmount * s * 0.012);
                    float3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + off).rgb;

                    // vary with noise and stain
                    float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, (uv + off) * _NoiseScale).r;
                    float3 stain = SAMPLE_TEXTURE2D(_StainTex, sampler_StainTex, (uv + off) + (n - 0.5) * 0.05).rgb;
                    c = lerp(c, c * stain, 0.5);

                    accum += c;
                    total += 1.0;
                }

                return accum / max(0.0001, total);
            }

            // subtle paper grain multiply
            float ApplyPaper(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_PaperTex, sampler_PaperTex, uv * _PaperTiling).r;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // base color with soft bleed
                int bleedSamples = max(1, (int)round(_BleedSamples));
                float3 baseCol = ApplyBleed(IN.uv, _BleedAmount, bleedSamples);
                baseCol *= _Color.rgb;

                // noise warp uv for stain sampling
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, IN.uv * _NoiseScale).r;
                float2 warpedUV = IN.uv + (noise - 0.5) * _NoiseStrength;

                // stain overlay (alpha encoded stain assumed)
                float4 stainSample = SAMPLE_TEXTURE2D(_StainTex, sampler_StainTex, warpedUV);
                baseCol = lerp(baseCol, baseCol * stainSample.rgb, stainSample.a * 0.85);

                // lighting: main directional simple
                float3 L = GetMainLightDirWS();
                float3 N = normalize(IN.normalWS);
                float NdotL = saturate(dot(N, L));

                // cel-step shading using ramp
                int steps = max(2, (int)round(_CelSteps));
                int hard = (int)round(_HardStep);
                float3 toonLit = SampleCelRamp(NdotL, steps, hard);

                float3 lit = baseCol * toonLit;

                // rim glow (view dependent)
                float ndv = saturate(dot(N, normalize(IN.viewDirWS)));
                float rim = pow(saturate(1.0 - ndv), _RimPower) * _RimStrength;
                lit += _RimColor.rgb * rim;

                // vibrance / saturation
                lit = AdjustVibrance(lit, _Vibrance, _Saturation);

                // paper grain multiplicative
                float paper = ApplyPaper(IN.uv);
                lit = lerp(lit, lit * paper, _PaperStrength);

                // distance fade (Ghibli-style atmospheric fade)
                float dist = distance(IN.posWS, _WorldSpaceCameraPos);
                float fade = saturate((dist - _DistanceFadeStart) / max(0.001, (_DistanceFadeEnd - _DistanceFadeStart)));

                // desaturate / darken far objects
                float grey = dot(lit, float3(0.2126, 0.7152, 0.0722));
                float3 grey3 = float3(grey, grey, grey);
                float3 darkened = lit * (1.0 - _DistanceDarken);
                float3 desat = lerp(darkened, grey3, _DistanceDesaturate);

                lit = lerp(lit, desat, fade);

                return float4(lit, 1.0);
            }

            ENDHLSL
        } // End main pass
    } // End SubShader

    // No explicit fallback; URP-only
}
