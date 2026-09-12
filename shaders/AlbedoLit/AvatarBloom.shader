Shader "Hidden/ValheimVRM/AvatarBloom"
{
    Properties
    {
        _MainTex ("Coverage", 2D) = "white" {}
        _Opacity ("Opacity", Float) = 1
        _Cull ("Cull", Float) = 2
        _ZTest ("Depth test", Float) = 3
        _BloomCutoff ("Bloom coverage cutoff", Float) = 0.5
        _Transparent ("Partial coverage", Float) = 0
    }
    SubShader
    {
        Pass
        {
            Name "BLOOM_COVERAGE"
            Cull [_Cull]
            ZTest [_ZTest]
            ZWrite Off
            Blend One OneMinusSrcAlpha
            ColorMask R
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Opacity, _BloomCutoff, _Transparent;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Output vert(Input input)
            {
                Output output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            float4 frag(Output input) : SV_Target
            {
                float alpha = tex2D(_MainTex, input.uv).a * _Opacity;
                clip(alpha - _BloomCutoff);
                float coverage = lerp(1, saturate(alpha), _Transparent);
                return float4(coverage, 0, 0, coverage);
            }
            ENDCG
        }
        Pass
        {
            Name "BLOOM_INPUT_ONLY"
            Cull Off
            ZTest Always
            ZWrite Off
            Blend Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _AvatarBloomMask;
            float2 _AvatarBloomJitter;
            float4 frag(v2f_img input) : SV_Target
            {
                float2 maskUv = input.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) maskUv.y = 1 - maskUv.y;
                #endif
                maskUv -= _AvatarBloomJitter;
                float4 color = tex2D(_MainTex, input.uv);
                color.rgb *= 1 - tex2D(_AvatarBloomMask, maskUv).r;
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
