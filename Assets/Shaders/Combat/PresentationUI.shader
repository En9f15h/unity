Shader "Combat/Presentation UI"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white" {}
        _Color("Tint",Color)=(1,1,1,1)
        _AccentColor("Accent",Color)=(0.95,0.65,0.25,1)
        _Strength("Highlight Strength",Range(0,1))=0.25
        [Enum(Metal,0,Energy,1,Rune,2,Mist,3)] _Mode("Mode",Float)=0
        _Density("Mist Density",Range(0,0.3))=0.06
        [HideInInspector] _StateAmount("State",Float)=0
        [HideInInspector] _Persistent("Persistent",Float)=0
        [HideInInspector] _PulseProgress("Pulse",Float)=1
        [HideInInspector] _LocalRect("Local Rect",Vector)=(-50,-50,100,100)
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
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
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
            #include "CombatNoise.hlsl"
            struct Attributes { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 mask:TEXCOORD1; float2 localUV:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            float4 _Color,_AccentColor,_TextureSampleAdd,_ClipRect,_LocalRect;
            float _Strength,_Mode,_Density,_StateAmount,_Persistent,_PulseProgress,_UIMaskSoftnessX,_UIMaskSoftnessY;
            Varyings Vert(Attributes input)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(input.vertex); o.uv=input.uv; o.color=input.color*_Color;
                o.localUV=(input.vertex.xy-_LocalRect.xy)/max(_LocalRect.zw,0.001);
                float2 pixelSize=o.vertex.w/abs(mul((float2x2)UNITY_MATRIX_P,_ScreenParams.xy));
                float4 rect=clamp(_ClipRect,-2e10,2e10);
                o.mask=float4(input.vertex.xy*2-rect.xy-rect.zw,0.25/(0.25*float2(_UIMaskSoftnessX,_UIMaskSoftnessY)+abs(pixelSize)));
                return o;
            }
            float4 Frag(Varyings input):SV_Target
            {
                float4 color=(tex2D(_MainTex,input.uv)+_TextureSampleAdd)*input.color;
                float2 uv=input.localUV;
                if(_Mode<2.5)
                {
                    float progress=saturate(_PulseProgress);
                    float scan=exp(-pow((uv.x+uv.y*0.25-lerp(-0.3,1.55,progress))*9,2))*sin(progress*3.14159);
                    float loop=pow(saturate(0.5+0.5*sin(_CombatVisualTime*2.2)),3)*_Persistent;
                    float detail=saturate(max(color.r,max(color.g,color.b))*1.8+0.1);
                    float light=scan+loop*0.32*_StateAmount;
                    if(_Mode>0.5 && _Mode<1.5) light*=0.75+0.25*sin(uv.x*14-_CombatVisualTime*1.5);
                    if(_Mode>1.5) light*=0.7+0.3*cos(atan2(uv.y-0.5,uv.x-0.5)*3-_CombatVisualTime);
                    color.rgb+=_AccentColor.rgb*light*_Strength*detail;
                }
                else
                {
                    float band=smoothstep(0,0.12,uv.y)*(1-smoothstep(0.2,0.58,uv.y));
                    float sides=smoothstep(0,0.1,uv.x)*(1-smoothstep(0.9,1,uv.x));
                    float fog=CombatNoise(uv*float2(7,5)+float2(_CombatVisualTime*0.025,0));
                    color.rgb=_AccentColor.rgb;
                    color.a*=band*sides*(0.35+fog*0.65)*_Density;
                }
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
