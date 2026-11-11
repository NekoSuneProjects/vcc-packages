// NekoSuneVR particle shader.
//
// Deliberately tiny. The blend mode is driven from C# through _SrcBlend/_DstBlend rather than
// keywords, so one shader and one variant covers both the additive presets (sparks, embers,
// stars) and the alpha-blended ones (hearts, petals, snow) with no shader stripping surprises
// on upload.
Shader "NekoSuneVR/Particle"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _TintColor ("Tint", Color) = (1, 1, 1, 1)
        _Brightness ("Brightness", Range(0, 4)) = 1.0
        _SoftParticleFade ("Soft Edge", Range(0, 1)) = 0.0

        [HideInInspector] _SrcBlend ("__src", Float) = 5   // SrcAlpha
        [HideInInspector] _DstBlend ("__dst", Float) = 10  // OneMinusSrcAlpha
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off
        Lighting Off
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;
            half _Brightness;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // Particle start colour and colour-over-lifetime both arrive as vertex colour.
                o.color = v.color * _TintColor;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 texel = tex2D(_MainTex, i.uv);
                fixed4 result = texel * i.color;
                result.rgb *= _Brightness;

                // No premultiply here on purpose: the source factor is SrcAlpha for both blend
                // modes, so the hardware already scales rgb by alpha. Doing it here as well
                // would square the alpha and make every sprite edge too dark.
                return result;
            }
            ENDCG
        }
    }

    FallBack "Sprites/Default"
}
