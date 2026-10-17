// The office drawn on paper, Don't Starve-style: a toon-banded light, paper fibre and blotches in the colour,
// ink crosshatching wherever the light doesn't reach, and (for the floor) carpet tiles sketched in pencil.
Shader "Haunted/Paper"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Hatch("Hatching in shade", Range(0, 1)) = 0.7
        _Tiles("Floor tiles", Range(0, 1)) = 0
        _TileSize("Tile size", Float) = 1.6
        _Outline("Ink outline (px)", Range(0, 4)) = 1.0
        _PoolRadius("Light pool radius", Float) = 9
        _Slats("Belt slats", Range(0, 1)) = 0
        _Silhouette("Silhouette ink (for round things)", Range(0, 1)) = 0
        _DepthPull("Depth pulled towards the camera (m)", Float) = 0
        _Anchor("Depth taken from this world point (xyz) when w is 1", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // Every pass declares the same UnityPerMaterial, so the SRP Batcher can take these materials.
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Hatch, _Tiles;
                float _TileSize, _Outline, _PoolRadius, _Slats, _Silhouette, _DepthPull;
                float4 _Anchor;
            CBUFFER_END
        ENDHLSL

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _PoolCenter; // xz of the floor point under the middle of the board, set by MindFx
            float _BeltTravel; // how far the belt has moved, set by Belt
            float _MindTime; // unscaled seconds, set by MindFx
            // Light spilling from open cracks (xz, unused, strength), set by MindFx: warm daylight, Don't Starve's falloff.
            float4 _CrackGlows[5];
            half3 CrackLight(float2 xz)
            {
                half sum = 0;
                for (int g = 0; g < 5; g++)
                    sum += _CrackGlows[g].w * exp(-0.9 * pow(length(xz - _CrackGlows[g].xy) / 3.8, 2.2));
                return half3(1, 0.91, 0.69) * sum;
            }

            TEXTURE2D(_Stains); SAMPLER(sampler_Stains); // coloured ink stains on the floor (rgb ink, a coverage), stamped by Fx
            float4 _StainRect; // xz min, 1 / size

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
                // Thoughts are pulled forward in depth only, so one rolling against a wall isn't cut by it. A thought can
                // also take its whole depth from a point on the floor, the way the cards stand at their feet, so it hides
                // and is hidden by the drawings like the desk, belt or demon it is on.
                float3 depthAt = _Anchor.w > 0 ? _Anchor.xyz : o.positionWS;
                float4 pulled = TransformWorldToHClip(depthAt + normalize(GetCameraPositionWS() - depthAt) * _DepthPull);
                o.positionCS.z = pulled.z / pulled.w * o.positionCS.w;
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
                float u = coord + (Noise(wobble) - 0.5) * 0.35;
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
                    half4 stain = SAMPLE_TEXTURE2D(_Stains, sampler_Stains, (i.positionWS.xz - _StainRect.xy) * _StainRect.zw);
                    albedo = lerp(albedo, stain.rgb, smoothstep(0.4, 0.6, stain.a) * 0.9);
                }

                // The belt's slats, drawn as pale ridges on its top that roll along with the thoughts.
                if (_Slats > 0 && n.y > 0.5)
                    albedo = albedo * (1 - 0.3 * _Slats) + _Slats * 0.12 * (1 - smoothstep(0.02, 0.07, abs(frac((i.positionWS.x - _BeltTravel) / 0.45) - 0.5) - 0.38));

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
                light += CrackLight(i.positionWS.xz) * 1.7;
                half3 col = albedo * light;

                // Ink crosshatching in the shade: one direction where it dims, a second across it where it's dark.
                float2 screen = i.positionCS.xy / 7.0;
                half shade = 1 - saturate(Luminance(light) / 1.1);
                // Line boil: the ink is redrawn a few times a second, like a slow hand-drawn boil.
                float boil = floor(_MindTime * 2.5) * 7.13;
                half hatchA = Lines(screen.x + screen.y, plane * 3.1 + boil, 0.12) * smoothstep(0.35, 0.5, shade);
                half hatchB = Lines(screen.x - screen.y, plane * 2.7 + 5 + boil, 0.12) * smoothstep(0.55, 0.7, shade);
                col *= 1 - _Hatch * 0.35 * max(hatchA, hatchB);

                // Ink along the face's edges, the same width in pixels however far away it is.
                float2 edge = min(i.uv, 1 - i.uv) / max(fwidth(i.uv), 1e-5);
                half wobble = 0.7 + 0.6 * Noise(plane * 6 + boil);
                half ink = 1 - smoothstep(_Outline * wobble - 0.75, _Outline * wobble + 0.75, min(edge.x, edge.y));
                col = lerp(col, half3(0.06, 0.05, 0.08), ink * 0.85);

                // Round things have no face edges to ink, so outline where the surface turns away from the camera.
                half facing = dot(n, normalize(GetWorldSpaceViewDir(i.positionWS)));
                col = lerp(col, half3(0.06, 0.05, 0.08), _Silhouette * (1 - smoothstep(0.35, 0.5, facing + (Noise(plane * 9 + boil) - 0.5) * 0.08)));

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // Lit's ShadowCaster and DepthOnly, minus the material keywords these materials never set.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}
