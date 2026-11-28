// Test-only screen-space depth consumer, not shipped in the plugin bundles.
Shader "Hidden/ValheimVRMTests/DepthConsumer"
{
    Properties { _MainTex ("Scene color", 2D) = "white" {} }
    SubShader { Pass {
        Cull Off ZWrite Off ZTest Always
        CGPROGRAM
        #pragma vertex vert_img
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        float _EffectDepth;
        float4 _Tint;
        float4 frag(v2f_img input) : SV_Target
        {
            float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, input.uv));
            float4 color = tex2D(_MainTex, input.uv);
            // Like a cloud/water depth consumer, affect only pixels behind its
            // surface, leaving nearer solids intact. Partial fabric keeps the
            // background's depth and is composited later by the real renderer.
            return color + _Tint * step(_EffectDepth, depth);
        }
        ENDCG
    } }
}
