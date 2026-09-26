Shader "Combat/Health Gauge"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white" {}
        _Color("Tint",Color)=(1,1,1,1)
        _HealthColor("Health",Color)=(0.18,0.58,0.5,1)
        _ChipColor("Recent Damage",Color)=(0.82,0.58,0.25,1)
        _FillAmount("Health Ratio",Range(0,1))=1
        _TrailAmount("Damage Trail",Range(0,1))=1
        _Reverse("Fill From Right",Float)=0
        _DamageFlash("Damage Flash",Range(0,1))=0
        [HideInInspector] _SpriteUVRect("Sprite UV",Vector)=(0,0,1,1)
        [HideInInspector] _RendererColor("Renderer Color",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Blend One OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "CombatNoise.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color:COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color:COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color,_HealthColor,_ChipColor;
                float4 _SpriteUVRect;
                float _FillAmount,_TrailAmount,_Reverse,_DamageFlash;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS=UnityFlipSprite(input.positionOS,unity_SpriteProps.xy);
                Varyings o=CommonUnlitVertex(input); o.color=input.color*_Color*unity_SpriteColor; return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half alpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a*input.color.a;
                float2 uv=(input.uv-_SpriteUVRect.xy)/max(_SpriteUVRect.zw-_SpriteUVRect.xy,0.00001);
                float x=lerp(uv.x,1-uv.x,step(.5,_Reverse));
                float fill=step(x,_FillAmount)*step(.00001,_FillAmount);
                float trail=step(x,max(_FillAmount,_TrailAmount))*step(.00001,_TrailAmount);
                float grain=CombatNoise(uv*float2(160,9))-.5;
                float bevel=smoothstep(0,.13,uv.y)*(1-smoothstep(.88,1,uv.y));
                half3 health=_HealthColor.rgb*(.68+.36*uv.y+grain*.07);
                health=lerp(health*.65,health,bevel)+pow(saturate(1-abs(uv.y-.78)*18),3)*.08;
                health=lerp(health,half3(.9,.81,.59),_DamageFlash*.2);
                half3 chip=_ChipColor.rgb*(.7+.3*uv.y);
                half3 well=half3(.028,.036,.043)*(0.65+bevel*.35);
                half3 rgb=lerp(well,chip,trail); rgb=lerp(rgb,health,fill);
                return half4(rgb*input.color.rgb*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
