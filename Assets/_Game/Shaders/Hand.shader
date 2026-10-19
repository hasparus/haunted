Shader "Haunted/Hand"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        _Smoothness("Smoothness", Range(0,1)) = 0.35
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
            #pragma fragment LitPassFragment
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
            Varyings HandVertex(Attributes input, half4 fingers : COLOR)
            {
                Varyings output = UnsortedLitVertex(input);
                output.positionCS = HandSortedPosition(output.positionCS, TransformObjectToWorld(input.positionOS.xyz), fingers.rg);
                return output;
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
