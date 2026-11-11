// NekoSuneVR eye shader.
//
// Built to match the UVs that neko_eye_tool.py generates: a planar projection along the
// facing direction, iris dead centre at (0.5, 0.5). That means the iris can be masked
// radially from UV centre instead of relying on a hand-authored eye texture layout.
//
// The parallax offset fakes corneal refraction - the iris appears to sit behind a clear
// dome, so the eye reads as having depth from off-angle instead of looking like a decal.
Shader "NekoSuneVR/Eye"
{
    Properties
    {
        [Header(Iris)]
        _IrisTex ("Iris Texture", 2D) = "white" {}
        _IrisColor ("Iris Tint", Color) = (1, 1, 1, 1)
        _IrisSize ("Iris Size", Range(0.05, 1.0)) = 0.42
        _IrisSoftness ("Iris Edge Softness", Range(0.001, 0.3)) = 0.03
        _Parallax ("Iris Depth", Range(0.0, 0.2)) = 0.045

        [Header(Pupil)]
        _PupilSize ("Pupil Size", Range(0.0, 0.9)) = 0.30
        _PupilColor ("Pupil Color", Color) = (0.03, 0.03, 0.05, 1)

        [Header(Sclera)]
        _ScleraColor ("Sclera Color", Color) = (0.92, 0.90, 0.90, 1)
        _ScleraTex ("Sclera Texture", 2D) = "white" {}

        [Header(Highlight)]
        _HighlightTex ("Highlight Texture", 2D) = "black" {}
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _HighlightPower ("Highlight Strength", Range(0, 4)) = 1.0

        [Header(Lighting)]
        _MinBrightness ("Minimum Brightness", Range(0, 1)) = 0.35
        _Emission ("Emission", Range(0, 2)) = 0.0
        _Smoothness ("Specular Sharpness", Range(1, 256)) = 48
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200
        Cull Back

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _IrisTex;
            float4 _IrisTex_ST;
            sampler2D _ScleraTex;
            sampler2D _HighlightTex;

            float4 _IrisColor;
            float4 _PupilColor;
            float4 _ScleraColor;
            float4 _HighlightColor;

            float _IrisSize;
            float _IrisSoftness;
            float _Parallax;
            float _PupilSize;
            float _HighlightPower;
            float _MinBrightness;
            float _Emission;
            float _Smoothness;
            float _SpecularStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 tangentViewDir : TEXCOORD3;
                SHADOW_COORDS(4)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _IrisTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);

                // Object-space tangent basis, used to push the iris UV against the view
                // direction so the iris appears to sit under the surface.
                float3 objectViewDir = ObjSpaceViewDir(v.vertex);
                float3 binormal = cross(v.normal, v.tangent.xyz) * v.tangent.w;
                o.tangentViewDir = float3(
                    dot(objectViewDir, v.tangent.xyz),
                    dot(objectViewDir, binormal),
                    dot(objectViewDir, v.normal));

                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(i.tangentViewDir);

                // Shift the iris lookup opposite the view so it lags behind the cornea.
                float2 parallaxOffset = viewDir.xy / max(viewDir.z, 0.15) * _Parallax;
                float2 irisUv = i.uv - parallaxOffset;

                // Everything is measured from the UV centre, which is where the generated
                // mesh puts the front of the eye.
                float2 centred = irisUv - 0.5;
                float radius = length(centred) * 2.0;

                float irisMask = 1.0 - smoothstep(
                    _IrisSize - _IrisSoftness,
                    _IrisSize + _IrisSoftness,
                    radius);

                float pupilMask = 1.0 - smoothstep(
                    _IrisSize * _PupilSize - _IrisSoftness,
                    _IrisSize * _PupilSize + _IrisSoftness,
                    radius);

                // Remap the iris disc to fill the whole texture, so any square iris art works.
                float2 irisSampleUv = centred / max(_IrisSize, 0.001) * 0.5 + 0.5;

                fixed4 iris = tex2D(_IrisTex, saturate(irisSampleUv)) * _IrisColor;
                fixed4 sclera = tex2D(_ScleraTex, i.uv) * _ScleraColor;

                fixed3 albedo = lerp(sclera.rgb, iris.rgb, irisMask);
                albedo = lerp(albedo, _PupilColor.rgb, pupilMask);

                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float3 worldView = normalize(_WorldSpaceCameraPos - i.worldPos);

                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);

                // Eyes going pitch black in dark worlds is the usual complaint, so the
                // diffuse term is floored rather than allowed to reach zero.
                float ndotl = saturate(dot(normal, lightDir));
                float3 lighting = _LightColor0.rgb * ndotl * atten;
                lighting += ShadeSH9(float4(normal, 1));
                lighting = max(lighting, _MinBrightness.xxx);

                float3 halfDir = normalize(lightDir + worldView);
                float specular = pow(saturate(dot(normal, halfDir)), _Smoothness) * _SpecularStrength;

                fixed4 highlight = tex2D(_HighlightTex, i.uv) * _HighlightColor;

                float3 color = albedo * lighting;
                color += _LightColor0.rgb * specular * atten;
                color += highlight.rgb * highlight.a * _HighlightPower;
                color += albedo * _Emission;

                return fixed4(color, 1.0);
            }
            ENDCG
        }

        // Keeps the eyes lit by additional point/spot lights in busy worlds.
        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _IrisTex;
            float4 _IrisTex_ST;
            sampler2D _ScleraTex;
            float4 _IrisColor;
            float4 _ScleraColor;
            float _IrisSize;
            float _IrisSoftness;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                SHADOW_COORDS(3)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _IrisTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float radius = length(i.uv - 0.5) * 2.0;
                float irisMask = 1.0 - smoothstep(
                    _IrisSize - _IrisSoftness,
                    _IrisSize + _IrisSoftness,
                    radius);

                fixed3 albedo = lerp(
                    (tex2D(_ScleraTex, i.uv) * _ScleraColor).rgb,
                    (tex2D(_IrisTex, i.uv) * _IrisColor).rgb,
                    irisMask);

                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));

                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);

                float ndotl = saturate(dot(normal, lightDir));
                return fixed4(albedo * _LightColor0.rgb * ndotl * atten, 1.0);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
