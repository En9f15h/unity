Shader "Combat/Energy Ribbon and Particle"
{
    Properties
    {
        _MainTex("Texture",2D)="white" {}
        _Color("Tint",Color)=(1,1,1,1)
        [HDR] _CoreColor("Core",Color)=(1.8,2.2,2.5,1)
        [HDR] _EdgeColor("Edge",Color)=(0.12,0.4,1.1,1)
        _Intensity("Intensity",Range(0,4))=1.2
        _FlowSpeed("Flow Speed",Float)=3
        [Toggle] _Particle("Radial Particle",Float)=0
        _Additive("Additive Contribution",Range(0,1))=0.85
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            // SRPDefaultUnlit is rendered by both 2D and forward renderers.
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CombatNoise.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color,_CoreColor,_EdgeColor;
                float _Intensity,_FlowSpeed,_Particle,_Additive;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o=(Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                o.uv=input.uv; o.color=input.color*_Color;
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float edge=abs(input.uv.y*2-1);
                if(_Particle>0.5) edge=length(input.uv*2-1);
                float coverage=pow(saturate(1-edge),1.4);
                float core=pow(saturate(1-edge),7);
                float flow=0.92+0.08*sin(input.uv.x*32-_CombatVisualTime*_FlowSpeed*8);
                half4 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
                half alpha=coverage*input.color.a*tex.a;
                half3 rgb=lerp(_EdgeColor.rgb,_CoreColor.rgb,core)*input.color.rgb*tex.rgb*_Intensity*flow;
                return half4(rgb*alpha,alpha*(1-_Additive));
            }
            ENDHLSL
        }
    }
}
