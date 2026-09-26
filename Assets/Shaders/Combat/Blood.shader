Shader "Combat/Blood"
{
    Properties
    {
        [PerRendererData] _MainTex("Blood texture",2D)="white" {}
        _Color("Tint",Color)=(1,1,1,1)
        _DryColor("Dry multiplier",Color)=(0.46,0.33,0.26,1)
        [Toggle] _Particle("Particle",Float)=0
        [HideInInspector] _Dryness("Dryness",Float)=0
        [HideInInspector] _Erosion("Erosion",Float)=0
        [HideInInspector] _SpriteUVRect("Sprite UV",Vector)=(0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "CombatNoise.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color,_DryColor;
                float4 _SpriteUVRect;
                float _Dryness,_Erosion,_Particle;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o=(Varyings)0; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                half4 tint=input.color*_Color;
                if(_Particle<0.5)
                {
                    SetUpSpriteInstanceProperties();
                    input.positionOS=UnityFlipSprite(input.positionOS,unity_SpriteProps.xy);
                    tint*=unity_SpriteColor;
                }
                o.positionCS=TransformObjectToHClip(input.positionOS); o.uv=input.uv; o.color=tint; return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv)*input.color;
                float2 uv=(input.uv-_SpriteUVRect.xy)/max(_SpriteUVRect.zw-_SpriteUVRect.xy,0.00001);
                float field=CombatNoise(uv*30)*0.65+CombatNoise(uv*67)*0.35;
                float edgeBias=(1-color.a)*0.16;
                float threshold=lerp(-0.25,1.1,saturate(_Erosion));
                color.a*=smoothstep(threshold,threshold+0.08,field-edgeBias);
                if(_Particle>0.5) color.a*=pow(saturate(1-length(input.uv*2-1)),0.65);
                color.rgb*=lerp(half3(1,1,1),_DryColor.rgb,saturate(_Dryness));
                return color;
            }
            ENDHLSL
        }
    }
}
