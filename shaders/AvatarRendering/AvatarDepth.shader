Shader "Hidden/ValheimVRM/AvatarDepth"
{
    Properties
    {
        _MainTex ("Alpha coverage", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Float) = -1
        _Cull ("Cull", Float) = 2
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal scale", Float) = 1
        _UvAnimMaskTex ("UV animation mask", 2D) = "white" {}
        _UvAnimScrollXSpeed ("UV scroll X", Float) = 0
        _UvAnimScrollYSpeed ("UV scroll Y", Float) = 0
        _UvAnimRotationSpeed ("UV rotation", Float) = 0
        _LegacyMToon ("Legacy UV mask channel", Float) = 0
        _VertexColorAlpha ("Multiply vertex alpha", Float) = 0
    }
    SubShader
    {
        Pass
        {
            Name "DEFERRED_SURFACE"
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Blend Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ _NORMALMAP
            #pragma multi_compile __ _MTOON_PARAMETERMAP
            #include "UnityCG.cginc"
            #include "UnityStandardUtils.cginc"
            #include "./AvatarUv.cginc"
            sampler2D _MainTex, _BumpMap;
            float4 _MainTex_ST, _Color;
            float _Cutoff, _BumpScale, _VertexColorAlpha;
            struct Input { float4 vertex : POSITION; float3 normal : NORMAL; float4 tangent : TANGENT; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float3 normal : TEXCOORD1; float3 tangent : TEXCOORD2; float3 bitangent : TEXCOORD3; float vertexAlpha : TEXCOORD4; };
            Varyings vert(Input input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.vertexAlpha = lerp(1, input.color.a, _VertexColorAlpha);
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.tangent = UnityObjectToWorldDir(input.tangent.xyz);
                output.bitangent = cross(output.normal, output.tangent) * input.tangent.w * unity_WorldTransformParams.w;
                return output;
            }
            struct Surface { half4 albedo : SV_Target0; half4 specular : SV_Target1; half4 normal : SV_Target2; };
            Surface frag(Varyings input)
            {
                // Match MToon's animated UV coverage, including cutout hair.
                float2 uv = AvatarUv(input.uv);
                clip(tex2D(_MainTex, uv).a * _Color.a * input.vertexAlpha - _Cutoff);
                float3 normal = normalize(input.normal);
                #if defined(_NORMALMAP)
                float3 mapped = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale);
                normal = normalize(input.tangent * mapped.x + input.bitangent * mapped.y + normal * mapped.z);
                #endif
                Surface output;
                // MToon still supplies all visible lighting in its forward pass.
                // Remove background material data and expose the actual surface
                // to deferred depth/normal consumers such as Amplify Occlusion.
                output.albedo = half4(0,0,0,1);
                output.specular = 0;
                output.normal = half4(normal * .5 + .5, 1);
                return output;
            }
            ENDCG
        }
    }
    Fallback Off
}
