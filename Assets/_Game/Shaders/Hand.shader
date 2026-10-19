Shader "Haunted/Hand"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        _InkColor("Charcoal ink", Color) = (0.06,0.05,0.08,1)
        _ShadeColor("Painted shade", Color) = (0.56,0.52,0.58,1)
        _Outline("Silhouette ink", Range(0,0.5)) = 0.28
        _Grain("Paper grain", Range(0,1)) = 0.3
        _Hatch("Shadow hatching", Range(0,1)) = 0.35
        _Creases("Baked crease ink", Range(0,1)) = 0.7
        _NailColor("Natural nail", Color) = (0.87,0.78,0.73,1)
        _CuticleColor("Cuticle", Color) = (0.48,0.37,0.35,1)
        _WarmColor("Knuckle and fingertip warmth", Color) = (0.84,0.64,0.60,1)
        _Nails("Nail definition", Range(0,1)) = 0.9
        _Warmth("Localized warmth", Range(0,1)) = 0.25
        _ShadeSoftness("Softer painted planes", Range(0,1)) = 0.5
        _Smoothness("Smoothness", Range(0,1)) = 0
        _Metallic("Metallic", Range(0,1)) = 0
        _SpecColor("Specular", Color) = (0.2,0.2,0.2,1)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Float) = 1
        _EmissionMap("Emission", 2D) = "white" {}
        _EmissionColor("Emission Color", Color) = (0,0,0,0)
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
        [HideInInspector] _HandSortAnchor("Sort Anchor", Vector) = (0,0,0,0)
        [HideInInspector] _HandSortFrom("Sort Origin", Vector) = (0,0,0,0)
        [HideInInspector] _HandSortRadius("Orb Radius", Float) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
        #include "Sorting.hlsl"
        float4 _HandSortAnchor, _HandSortFrom;
        float _HandSortRadius;
        half4 _InkColor, _ShadeColor;
        half _Outline, _Grain, _Hatch, _Creases;
        half4 _NailColor, _CuticleColor, _WarmColor;
        half _Nails, _Warmth, _ShadeSoftness;
        float _MindTime;

        float4 HandSortedPosition(float4 clip, float3 world, half2 fingers)
        {
            if (_HandSortAnchor.w < 0.5) return clip;
            float3 at = _HandSortAnchor.xyz + (world - _HandSortFrom.xyz) * 0.1;
            float3 forward = GetViewForwardDir();
            float3 fromOrb = world - _HandSortFrom.xyz;
            float radial = length(fromOrb - forward * dot(fromOrb, forward));
            float influence = saturate(max(fingers.r, fingers.g) * 2) * (1 - smoothstep(_HandSortRadius, _HandSortRadius + 0.08, radial));
            float plane = (_HandSortRadius + 0.05) * 0.1;
            float fingerDepth = plane * (fingers.r - fingers.g) / max(fingers.r + fingers.g, 0.001);
            at += forward * (fingerDepth - dot(at - _HandSortAnchor.xyz, forward)) * influence;
            clip.z = SortedZ(clip, at);
            return clip;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex HandVertex
            #pragma fragment HandFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #define LitPassVertex UnsortedLitVertex
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #undef LitPassVertex
            struct HandAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 detail : TEXCOORD1;
                half4 fingers : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct HandVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half crease : TEXCOORD3;
                half fog : TEXCOORD4;
                float4 detail : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            HandVaryings HandVertex(HandAttributes input)
            {
                HandVaryings output = (HandVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 clip = TransformWorldToHClip(output.positionWS);
                output.fog = ComputeFogFactor(clip.z);
                output.positionCS = HandSortedPosition(clip, output.positionWS, input.fingers.rg);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.crease = input.fingers.b;
                output.detail = input.detail;
                return output;
            }

            float HandHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float HandNoise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(HandHash(cell), HandHash(cell + float2(1, 0)), f.x),
                    lerp(HandHash(cell + float2(0, 1)), HandHash(cell + 1), f.x), f.y);
            }

            half4 HandFragment(HandVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight();
                half lit = saturate(dot(normal, sun.direction) * 0.5 + 0.5);
                // Painted planes, not flesh/specular: the same soft three-band treatment as the paper props.
                half band = 0.35 + 0.4 * smoothstep(0.3, 0.36, lit) + 0.25 * smoothstep(0.62, 0.68, lit);
                band = lerp(band, 0.4 + 0.6 * smoothstep(0.05, 0.95, lit), _ShadeSoftness);
                half3 skin = lerp(_BaseColor.rgb, _WarmColor.rgb, saturate(input.detail.w) * _Warmth);
                float2 nailPoint = float2(input.detail.x, (input.detail.y - 0.55) * 2);
                float2 nailBox = abs(nailPoint) - float2(0.41, 0.40);
                float nailDistance = length(max(nailBox, 0)) + min(max(nailBox.x, nailBox.y), 0) - 0.24;
                float aa = max(fwidth(nailDistance), 0.012);
                half dorsal = saturate(input.detail.z) * _Nails;
                half nail = (1 - smoothstep(-aa, aa, nailDistance)) * dorsal;
                half tip = smoothstep(0.80, 0.85, input.detail.y);
                half3 nailColor = lerp(_NailColor.rgb, _NailColor.rgb * 0.96 + 0.04, tip);
                skin = lerp(skin, nailColor, nail);
                half cuticle = (1 - smoothstep(0.012, 0.035 + aa, abs(nailDistance)))
                    * (1 - smoothstep(0.15, 0.45, nailPoint.y)) * dorsal;
                skin = lerp(skin, _CuticleColor.rgb, cuticle * 0.45);
                half3 paper = skin * lerp(_ShadeColor.rgb, half3(1, 1, 1), band);
                half3 illumination = clamp(sun.color * 0.7 + SampleSH(normal) * 0.6, 0.65, 1.15);
                paper *= illumination;
                // UV-locked grain follows the skin instead of swimming as the hand descends.
                paper *= 1 + _Grain * ((HandNoise(input.uv * 85) - 0.5) * 0.12
                    + (HandHash(floor(input.uv * 700)) - 0.5) * 0.055);
                float boil = floor(_MindTime * 2.5) * 7.13;
                float2 screen = input.positionCS.xy / 7;
                float hatchPhase = screen.x + screen.y + (HandNoise(input.uv * 45 + boil) - 0.5) * 0.3;
                float stripe = abs(frac(hatchPhase) - 0.5);
                half hatch = 1 - smoothstep(0.04, 0.12, stripe);
                paper *= 1 - _Hatch * hatch * (1 - band) * 0.3;
                // No UV-border ink: those seams are an unwrap, not anatomical creases.
                half crease = saturate(input.crease * _Creases);
                paper = lerp(paper, _InkColor.rgb, crease);
                half facing = saturate(dot(normal, view));
                float wobble = 0.9 + HandNoise(input.uv * 110 + boil) * 0.2;
                float width = max(fwidth(facing) * 0.65, 0.015);
                half ink = 1 - smoothstep(_Outline * wobble - width, _Outline * wobble + width, facing);
                paper = lerp(paper, _InkColor.rgb, ink);
                return half4(MixFog(paper, input.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex HandDepthVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #define DepthOnlyVertex UnsortedDepthVertex
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            #undef DepthOnlyVertex
            Varyings HandDepthVertex(Attributes input, half4 fingers : COLOR)
            {
                Varyings output = UnsortedDepthVertex(input);
                output.positionCS = HandSortedPosition(output.positionCS, TransformObjectToWorld(input.position.xyz), fingers.rg);
                return output;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex HandNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #define DepthNormalsVertex UnsortedNormalsVertex
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            #undef DepthNormalsVertex
            Varyings HandNormalsVertex(Attributes input, half4 fingers : COLOR)
            {
                Varyings output = UnsortedNormalsVertex(input);
                output.positionCS = HandSortedPosition(output.positionCS, TransformObjectToWorld(input.positionOS.xyz), fingers.rg);
                return output;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
