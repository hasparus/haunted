// The office drawn on paper, Don't Starve-style: a toon-banded light, paper fibre and blotches in the colour,
// ink crosshatching wherever the light doesn't reach, and (for the floor) carpet tiles sketched in pencil.
Shader "Haunted/Paper"
{
    Properties
    {
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Hatch("Hatching in shade", Range(0, 1)) = 0.7
        _Tiles("Floor tiles", Range(0, 1)) = 0
        _TileSize("Tile size", Float) = 1.6
        _Outline("Ink outline (px)", Range(0, 4)) = 1.8
        _PoolRadius("Light pool radius", Float) = 9
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Hatch, _Tiles;
                float _TileSize, _Outline, _PoolRadius;
            CBUFFER_END
            float4 _PoolCenter; // xz of the floor point under the middle of the board, set by MindFx

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.uv = v.uv;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            // Wobbly pencil lines: a stripe pattern whose phase drifts with noise so no line is perfectly straight.
            half Lines(float coord, float2 wobble, float width)
            {
                float u = coord + (Noise(wobble) - 0.5) * 0.6;
                return 1 - smoothstep(0, width, abs(frac(u) - 0.5) * 2 - (1 - width * 2));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                // Planar coordinates on whichever face this is, so the paper doesn't smear on walls.
                float2 plane = abs(n.y) > 0.5 ? i.positionWS.xz : (abs(n.x) > 0.5 ? i.positionWS.zy : i.positionWS.xy);

                half3 albedo = _BaseColor.rgb;
                albedo *= 0.93 + 0.14 * Noise(plane * 1.3) * Noise(plane * 0.37 + 7.1);
                albedo *= 0.97 + 0.06 * Hash(floor(plane * 90));

                if (_Tiles > 0)
                {
                    float2 t = plane / _TileSize;
                    half grid = max(Lines(t.x, plane * 2.3, 0.06), Lines(t.y, plane.yx * 2.3 + 3, 0.06));
                    albedo *= 1 - grid * 0.16 * _Tiles;
                    albedo *= 0.96 + 0.08 * Hash(floor(t));
                }

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light sun = GetMainLight(shadowCoord);
                half ndl = saturate(dot(n, sun.direction) * 0.5 + 0.5);
                half lit = ndl * sun.shadowAttenuation;
                // Three soft-edged bands instead of a smooth ramp.
                half band = 0.42 + 0.33 * smoothstep(0.3, 0.36, lit) + 0.25 * smoothstep(0.62, 0.68, lit);
                half3 light = sun.color * band;

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                half4 shadowMask = half4(1, 1, 1, 1);
                uint count = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                    half a = saturate(dot(n, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                    light += l.color * smoothstep(0.02, 0.2, a) * a * 1.6;
                LIGHT_LOOP_END

                light += SampleSH(n) * 0.6;
                // Don't Starve's light falloff (a stretched exponential: soft core, long tail, no edge) around the room.
                half pool = exp(-0.9 * pow(length(i.positionWS.xz - _PoolCenter.xy) / _PoolRadius, 2.6));
                light *= 0.45 + 0.55 * pool;
                half3 col = albedo * light;

                // Ink crosshatching in the shade: one direction where it dims, a second across it where it's dark.
                float2 screen = i.positionCS.xy / 7.0;
                half shade = 1 - saturate(Luminance(light) / 1.1);
                half hatchA = Lines(screen.x + screen.y, plane * 3.1, 0.12) * smoothstep(0.35, 0.5, shade);
                half hatchB = Lines(screen.x - screen.y, plane * 2.7 + 5, 0.12) * smoothstep(0.55, 0.7, shade);
                col *= 1 - _Hatch * 0.35 * max(hatchA, hatchB);

                // Ink along the face's edges, the same width in pixels however far away it is.
                float2 edge = min(i.uv, 1 - i.uv) / max(fwidth(i.uv), 1e-5);
                half wobble = 0.7 + 0.6 * Noise(plane * 6);
                half ink = 1 - smoothstep(_Outline * wobble - 0.75, _Outline * wobble + 0.75, min(edge.x, edge.y));
                col = lerp(col, half3(0.06, 0.05, 0.08), ink * 0.85);

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
