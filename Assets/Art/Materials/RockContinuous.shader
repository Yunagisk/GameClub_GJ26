Shader "Heptagram/Continuous Rock Tilemap"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _RockTex ("Rock Strip", 2D) = "white" {}
        _WorldSize ("Repeat Size in World Units", Vector) = (4.5,1.5,0,0)
        _Contrast ("Texture Contrast", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _RockTex;
            float4 _WorldSize;
            float _Contrast;
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 world : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld,v.vertex).xy;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // Two offset samples per axis. The sample crossing a repeat seam
                // receives zero weight there, avoiding both a hard seam and mirrored motifs.
                float2 uv = frac(i.world / max(_WorldSize.xy,float2(.01,.01)));
                float2 weight = sin(uv * 3.14159265);
                weight *= weight;
                fixed4 a = tex2D(_RockTex,uv);
                fixed4 b = tex2D(_RockTex,frac(uv+float2(.5,0)));
                fixed4 c = tex2D(_RockTex,frac(uv+float2(0,.5)));
                fixed4 d = tex2D(_RockTex,frac(uv+float2(.5,.5)));
                fixed4 col = lerp(lerp(d,c,weight.x),lerp(b,a,weight.x),weight.y);
                col.rgb = lerp(fixed3(.32,.30,.34),col.rgb,_Contrast);
                col *= i.color;
                col.rgb *= col.a;
                return col;
            }
            ENDCG
        }
    }
}
