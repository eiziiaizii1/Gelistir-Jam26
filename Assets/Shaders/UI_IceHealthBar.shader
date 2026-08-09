Shader "UI/IceHealthBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _IceColor ("Ice Deep Color", Color) = (0.1, 0.65, 0.98, 1)
        _FrostColor ("Frost Sparkle Color", Color) = (0.85, 0.96, 1.0, 1)
        _MeltEdgeColor ("Melt Thermal Glow", Color) = (1.0, 0.4, 0.1, 1)
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _ShimmerSpeed ("Shimmer Speed", Float) = 1.5

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _IceColor;
                half4 _FrostColor;
                half4 _MeltEdgeColor;
                float _FillAmount;
                float _ShimmerSpeed;
                float4 _ClipRect;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.worldPosition = input.positionOS;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(123.39, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Fill Amount mask
                if (uv.x > _FillAmount)
                {
                    discard;
                }

                // Procedural ice crystal noise pattern
                float icePattern = noise(uv * float2(18.0, 6.0));
                float crystalSparkle = pow(icePattern, 2.2);

                // Base Ice Color blend
                half4 baseColor = lerp(_IceColor, _FrostColor, crystalSparkle * 0.7);

                // Animated Shimmer Light Streak across bar
                float shimmerPos = frac(_Time.y * _ShimmerSpeed * 0.4);
                float shimmer = smoothstep(0.0, 0.08, 0.08 - abs(uv.x - shimmerPos - (uv.y * 0.15)));
                baseColor.rgb += _FrostColor.rgb * shimmer * 0.6;

                // Thermal Melt Edge Glow right at the fill boundary
                float edgeDist = abs(uv.x - _FillAmount);
                if (edgeDist < 0.04 && _FillAmount < 0.98)
                {
                    float glowIntensity = smoothstep(0.04, 0.0, edgeDist);
                    baseColor.rgb = lerp(baseColor.rgb, _MeltEdgeColor.rgb * 1.8, glowIntensity);
                }

                half4 finalColor = baseColor * input.color;
                return finalColor;
            }
            ENDHLSL
        }
    }
}
