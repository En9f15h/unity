Shader "Combat/Character UI"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white" {}
        _Color("Tint",Color)=(1,1,1,1)
        _RimColor("Edge Light",Color)=(0.4,0.6,0.8,1)
        _RimStrength("Edge Strength",Range(0,0.3))=0.06
        [HideInInspector] _SpriteUVRect("Sprite UV",Vector)=(0,0,1,1)
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct Attributes { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 mask:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize,_Color,_RimColor,_TextureSampleAdd,_ClipRect,_SpriteUVRect;
            float _RimStrength,_UIMaskSoftnessX,_UIMaskSoftnessY;
            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(input.vertex); o.uv=input.uv; o.color=input.color*_Color;
                float2 pixelSize=o.vertex.w/abs(mul((float2x2)UNITY_MATRIX_P,_ScreenParams.xy));
                float4 clipRect=clamp(_ClipRect,-2e10,2e10);
                o.mask=float4(input.vertex.xy*2-clipRect.xy-clipRect.zw,
                    0.25/(0.25*float2(_UIMaskSoftnessX,_UIMaskSoftnessY)+abs(pixelSize)));
                return o;
            }
            float Alpha(float2 uv)
            {
                float2 inside=step(_SpriteUVRect.xy,uv)*step(uv,_SpriteUVRect.zw);
                return tex2D(_MainTex,uv).a*inside.x*inside.y;
            }
            float4 Frag(Varyings input):SV_Target
            {
                float4 source=tex2D(_MainTex,input.uv)+_TextureSampleAdd;
                float2 pixel=_MainTex_TexelSize.xy;
                float neighbors=min(min(Alpha(input.uv+float2(pixel.x,0)),Alpha(input.uv-float2(pixel.x,0))),
                    min(Alpha(input.uv+float2(0,pixel.y)),Alpha(input.uv-float2(0,pixel.y))));
                source.rgb+=_RimColor.rgb*saturate(source.a-neighbors)*_RimStrength;
                float4 color=source*input.color;
                #ifdef UNITY_UI_CLIP_RECT
                    float2 mask=saturate((_ClipRect.zw-_ClipRect.xy-abs(input.mask.xy))*input.mask.zw);
                    color.a*=mask.x*mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a-0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
