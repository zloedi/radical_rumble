Shader "GLSprites"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Texture", 2D) = "white" {}
        // GL calls don't need a _Color property to switch colors
        _Color ("Main Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            Lighting Off

            BindChannels {
                Bind "vertex", vertex 
                Bind "color", color 
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // vertex shader inputs
            struct appdata {
                float2 uv : TEXCOORD0; // texture coordinate
                fixed4 color : COLOR0;
                float4 vertex : POSITION; // vertex position
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR0;
                float4 vertex : SV_POSITION;
            };

            fixed4 _Color;

            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            sampler2D _MainTex;

            fixed4 frag(v2f i) : SV_Target {
                // work in gamma space, sqrt
                fixed4 col = sqrt(tex2D(_MainTex, i.uv));
                col *= i.color;
                return col;
            }

            ENDCG
        }
    }
}
