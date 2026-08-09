Shader "UI/FieryTextShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture / Font Alpha", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FireCoreColor ("Fire Core Color", Color) = (1.0, 0.95, 0.2, 1)  // Bright Yellow
        _FireMidColor ("Fire Flame Color", Color) = (1.0, 0.45, 0.05, 1) // Burning Orange
        _FireTopColor ("Fire Smoke Color", Color) = (0.8, 0.1, 0.02, 1)  // Dark Fiery Red
        _FlameSpeed ("Flame Speed", Float) = 3.5
        _FlameScale ("Flame Scale", Float) = 12.0

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
                half4 _FireCoreColor;
                half4 _FireMidColor;
                half4 _FireTopColor;
                float _FlameSpeed;
                float _FlameScale;
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

                // Animated flame noise texture lookup
                float2 fireUV = uv * float2(_FlameScale, _FlameScale * 0.5);
                fireUV.y -= _Time.y * _FlameSpeed * 0.5; // Upward flame movement

                float flameNoise = noise(fireUV);

                // Vertical flame gradient (Yellow at bottom, Orange in middle, Red at top)
                float gradFactor = saturate(uv.y + (flameNoise - 0.5) * 0.35);

                half4 flameColor;
                if (gradFactor < 0.45)
                {
                    flameColor = lerp(_FireCoreColor, _FireMidColor, gradFactor * 2.22);
                }
                else
                {
                    flameColor = lerp(_FireMidColor, _FireTopColor, (gradFactor - 0.45) * 1.81);
                }

                // Sample font / texture alpha mask
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half fontAlpha = texColor.a * input.color.a;

                half4 finalColor = flameColor * input.color;
                finalColor.a = fontAlpha;

                return finalColor;
            }
            ENDHLSL
        }
    }
}
