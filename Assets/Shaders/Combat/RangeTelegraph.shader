Shader "Combat/Range Telegraph"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1,1,1,1)
        _Layer("Fill / Border", Float) = 0
        _EnemyStyle("Enemy Pattern", Float) = 0
        _Progress("Warning Progress / Negative for Hover", Float) = -1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Layer, _EnemyStyle, _Progress;
            CBUFFER_END
            struct Attributes { COMMON_2D_INPUTS half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                Varyings o = (Varyings)0; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv; o.color = input.color * unity_SpriteColor;
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p = abs(input.uv - .5);
                float edgeDistance = .48 - max(p.x,p.y);
                float aa = max(fwidth(edgeDistance), .001);
                float inside = smoothstep(-aa,aa,edgeDistance);
                float edge = inside * (1-smoothstep(.012-aa,.012+aa,edgeDistance));
                float corners = smoothstep(.25,.3,min(p.x,p.y));
                float dash = step(.35,frac((input.uv.x+input.uv.y)*12));
                float border = edge * lerp(.3,1,corners) * lerp(1,lerp(dash,1,corners),_EnemyStyle);
                float hatch = 1-smoothstep(.1,.2,abs(frac((input.uv.x+input.uv.y)*7)-.5));
                float fill = inside * (.18 + .22*smoothstep(.2,.48,max(p.x,p.y)) + _EnemyStyle*hatch*.12);
                float progress = saturate(_Progress);
                float cueX = lerp(.42,0,progress);
                float cue = (1-smoothstep(.009,.009+aa,abs(p.x-cueX))) * (1-smoothstep(.035,.05,abs(input.uv.y-.08)));
                cue *= step(0,_Progress);
                float alpha = _Layer>.5 ? max(border,cue*.85) : fill;
                half luminance = dot(input.color.rgb,half3(.2126,.7152,.0722));
                half3 tint = lerp(luminance.xxx,input.color.rgb,.75);
                return half4(tint,alpha*input.color.a);
            }
            ENDHLSL
        }
    }
}
