#ifndef VALHEIM_VRM_FUR_INCLUDED
#define VALHEIM_VRM_FUR_INCLUDED
#if defined(AVATAR_FUR_ON)
#define AVATAR_FUR 1
#define _ALPHABLEND_ON 1
#include "MToon10/vrmc_materials_mtoon_forward_vertex.hlsl"
#include "MToon10/vrmc_materials_mtoon_lighting_mtoon.hlsl"

sampler2D _FurLengthMask, _FurNoise, _FurMask;
float4 _FurNoise_ST, _FurDirection;
float _FurLength, _FurDensity, _FurRandomness, _FurRootOffset;
float _FurBaseAlphaMode, _AvatarReceiveShadows, _LegacyMToon;

struct FurInput
{
    float4 vertex : POSITION; // object coordinates, consumed by the geometry stage
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float2 uv : TEXCOORD0;
};
FurInput FurVertex(FurInput input) { return input; }

float2 FurUv(float2 uv)
{
    uv = uv * _MainTex_ST.xy + _MainTex_ST.zw;
    float time = _Time.y;
    #if defined(_MTOON_PARAMETERMAP)
    float4 mask = MTOON_SAMPLE_TEXTURE2D(_UvAnimMaskTex, uv);
    time *= lerp(mask.b, mask.r, _LegacyMToon);
    #endif
    float turns = time * _UvAnimRotationSpeed;
    float angle = (_LegacyMToon > .5 ? turns : frac(turns)) * UNITY_TWO_PI;
    uv += time * float2(_UvAnimScrollXSpeed, _UvAnimScrollYSpeed) - .5;
    return mul(float2x2(cos(angle), -sin(angle), sin(angle), cos(angle)), uv) + .5;
}

// A fixed seed in mesh space; camera jitter and elapsed time never randomize fins.
float3 FurRandom(float seed)
{
    return normalize(frac(sin(seed + float3(17.13, 79.71, 41.37)) * 43758.5453) * 2 - 1 + .00001);
}

[maxvertexcount(12)]
void FurGeometry(triangle FurInput input[3], uint triangleId : SV_PrimitiveID, inout TriangleStream<Varyings> stream)
{
    if (_FurLength <= 0) return;
    float3 center = (input[0].vertex.xyz + input[1].vertex.xyz + input[2].vertex.xyz) / 3;
    float distanceToCamera = distance(mul(unity_ObjectToWorld, float4(center, 1)).xyz, _WorldSpaceCameraPos);
    float distanceFade = saturate((20 - distanceToCamera) / 8);
    if (distanceFade <= 0) return;
    float density = lerp(1, clamp(_FurDensity, 1, 3), saturate((12 - distanceToCamera) / 8));
    [unroll] for (int fin = 0; fin < 3; fin++)
    {
        float densityFade = saturate(density - fin);
        if (densityFade <= 0) continue;
        [unroll] for (int endpoint = 0; endpoint < 2; endpoint++)
        {
            float3 start = fin == 0 ? float3(.8,.1,.1) : fin == 1 ? float3(.1,.8,.1) : float3(.1,.1,.8);
            float3 end = fin == 0 ? float3(.1,.8,.1) : fin == 1 ? float3(.1,.1,.8) : float3(.8,.1,.1);
            float3 factors = endpoint == 0 ? start : end;
            float3 p = input[0].vertex.xyz * factors.x + input[1].vertex.xyz * factors.y + input[2].vertex.xyz * factors.z;
            float3 n = normalize(input[0].normal * factors.x + input[1].normal * factors.y + input[2].normal * factors.z);
            float4 tangent = input[0].tangent * factors.x + input[1].tangent * factors.y + input[2].tangent * factors.z;
            float3 t = normalize(tangent.xyz + .00001);
            float3 b = cross(n, t) * sign(tangent.w);
            float2 uv = input[0].uv * factors.x + input[1].uv * factors.y + input[2].uv * factors.z;
            float lengthMask = tex2Dlod(_FurLengthMask, float4(uv * _MainTex_ST.xy + _MainTex_ST.zw, 0, 0)).r;
            float3 direction = normalize(_FurDirection.xyz + float3(0, 0, .0001));
            float3 delta = (t * direction.x + b * direction.y + n * direction.z +
                FurRandom(triangleId * 3 + fin) * _FurRandomness) * _FurLength * lengthMask;
            [unroll] for (int tip = 0; tip < 2; tip++)
            {
                Attributes vertex = (Attributes)0;
                vertex.vertex = float4(p + delta * tip, 1);
                vertex.normalOS = n;
                vertex.texcoord0 = uv;
                Varyings output = MToonVertex(vertex);
                output.fur = float3(tip, densityFade, distanceFade);
                stream.Append(output);
            }
        }
        stream.RestartStrip();
    }
}

float FurAlpha(Varyings input, float2 uv)
{
    float baseAlpha = (MTOON_SAMPLE_TEXTURE2D(_MainTex, uv) * _Color).a;
    if (_FurBaseAlphaMode < .5) baseAlpha = 1; // Opaque ignores stored alpha.
    else if (_FurBaseAlphaMode < 1.5) { clip(baseAlpha - _Cutoff); baseAlpha = 1; }
    float noise = tex2D(_FurNoise, uv * _FurNoise_ST.xy + _FurNoise_ST.zw).r;
    float mask = tex2D(_FurMask, uv).r;
    float shift = input.fur.x * (1 - _FurRootOffset) + _FurRootOffset;
    return baseAlpha * mask * saturate(noise - shift * abs(shift) * abs(shift)) * input.fur.y * input.fur.z;
}

half4 FurFragment(Varyings input) : SV_Target
{
    float2 uv = FurUv(input.uv);
    float alpha = FurAlpha(input, uv);
    clip(alpha - .001);
    #if defined(FUR_BLOOM_MASK)
    return alpha;
    #else
    half3 normal = normalize(input.normalWS); // Both fin faces share the cloth normal.
    UnityLighting light = GetUnityLighting(input, normal);
    MToonInput surface;
    surface.uv = uv; surface.normalWS = normal;
    surface.viewDirWS = normalize(input.viewDirWS);
    surface.litColor = (MTOON_SAMPLE_TEXTURE2D(_MainTex, uv) * _Color).rgb;
    surface.alpha = alpha;
    half4 color = GetMToonLighting(light, surface);
    #if defined(UNITY_PASS_FORWARDADD)
    color.rgb *= alpha;
    UNITY_APPLY_FOG_COLOR(input.fogCoord, color, half4(0, 0, 0, 0));
    #else
    UNITY_APPLY_FOG(input.fogCoord, color);
    #endif
    return color;
    #endif
}
#else
// The disabled variant contains no fur samplers, lighting, or generated geometry.
struct FurInput { float4 vertex : POSITION; };
struct FurDisabled { float4 position : SV_POSITION; };
FurInput FurVertex(FurInput input) { return input; }
[maxvertexcount(1)]
void FurGeometry(triangle FurInput input[3], inout TriangleStream<FurDisabled> stream) { }
half4 FurFragment(FurDisabled input) : SV_Target { return 0; }
#endif
#endif
