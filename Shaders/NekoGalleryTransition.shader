Shader "NekoSune/WorldGalleryTransition"
{
    Properties
    {
        _FromTex ("From", 2D) = "white" {}
        _ToTex ("To", 2D) = "white" {}
        _Progress ("Progress", Range(0,1)) = 0
        _Mode ("Mode", Float) = 0
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            sampler2D _FromTex;
            sampler2D _ToTex;
            float _Progress;
            float _Mode;
            float4 _Color;

            v2f vert(appdata v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color * _Color; return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 a = tex2D(_FromTex, i.uv);
                fixed4 b = tex2D(_ToTex, i.uv);
                float p = saturate(_Progress);
                float mask;

                if (_Mode < 0.5)
                {
                    // Soft horizontal wipe.
                    mask = smoothstep(p - 0.08, p + 0.08, i.uv.x);
                    return lerp(b, a, mask) * i.color;
                }
                else if (_Mode < 1.5)
                {
                    // Grainy dissolve.
                    float n = hash21(floor(i.uv * 220.0));
                    mask = step(n, p);
                    return lerp(a, b, mask) * i.color;
                }
                else
                {
                    // Radial reveal from center.
                    float d = distance(i.uv, float2(0.5, 0.5)) * 1.42;
                    mask = 1.0 - smoothstep(p - 0.08, p + 0.08, d);
                    return lerp(a, b, mask) * i.color;
                }
            }
            ENDCG
        }
    }
    FallBack Off
}
