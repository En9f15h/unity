Shader "Combat/Impact Accent"
{
    Properties { [HDR] _Color("Tint",Color)=(1.5,0.95,0.4,1) _Progress("Progress",Range(0,1))=1 _Mode("Heavy / Parry",Float)=0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color; float _Progress,_Mode;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=(i.uv-.5)*2; float r=length(p);
                float age=saturate(_Progress), fade=pow(1-age,1.5);
                float angle=atan2(p.y,p.x);
                float rays=pow(saturate(cos(angle*(_Mode>1.5?4:3)+.4)),22);
                float lengthMask=smoothstep(.04,.18,r)*(1-smoothstep(.25+age*.3,.65+age*.28,r));
                float core=exp(-r*r*140)*(1-age);
                float ring=exp(-pow((r-(.18+age*.55))*36,2))*(_Mode>.5?.24:0);
                float alpha=saturate(core+rays*lengthMask+ring)*fade*_Color.a;
                return half4(_Color.rgb*alpha,alpha*.4);
            }
            ENDHLSL
        }
    }
}
