// The day's photo seen through the host's eyes: the photo, desaturated, scaled to cover the eye shape then shifted and
// zoomed as set in EyePhotos, cut out by the shared eye mask.
Shader "Haunted/EyePhoto"
{
    Properties
    {
        _Photo("Photo", 2D) = "black" {}
        _Mask("Eye mask", 2D) = "white" {}
        _Offset("Offset (eye widths, eye heights)", Vector) = (0, 0, 0, 0)
        _Zoom("Zoom", Float) = 1
        _PhotoAspect("Photo aspect", Float) = 2.5
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Photo);
            SAMPLER(sampler_Photo);
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);

            CBUFFER_START(UnityPerMaterial)
                float4 _Offset;
                float _Zoom, _PhotoAspect;
            CBUFFER_END

            // The eye quad is 2.5 times as wide as it is tall (EyeView.Aspect).
            static const float EyeAspect = 2.5;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // Cover: the photo's shorter side spans the eyes, the longer one runs past them.
                float2 cover = _PhotoAspect > EyeAspect ? float2(EyeAspect / _PhotoAspect, 1) : float2(1, _PhotoAspect / EyeAspect);
                float2 uv = (i.uv - 0.5 - _Offset.xy) * cover / max(_Zoom, 0.01) + 0.5;
                half3 photo = SAMPLE_TEXTURE2D(_Photo, sampler_Photo, uv).rgb;
                half grey = dot(photo, half3(0.299, 0.587, 0.114));
                half inside = all(uv >= 0) && all(uv <= 1);
                half mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, i.uv).a;
                return half4(grey.xxx * inside, mask);
            }
            ENDHLSL
        }
    }
}
