#ifndef VALHEIM_VRM_AVATAR_UV_INCLUDED
#define VALHEIM_VRM_AVATAR_UV_INCLUDED
sampler2D _UvAnimMaskTex;
float _UvAnimScrollXSpeed, _UvAnimScrollYSpeed, _UvAnimRotationSpeed, _LegacyMToon;
float2 AvatarUv(float2 uv)
{
    float time = _Time.y;
    #if defined(_MTOON_PARAMETERMAP)
    float4 mask = tex2D(_UvAnimMaskTex, uv);
    time *= lerp(mask.b, mask.r, _LegacyMToon);
    #endif
    float turns = time * _UvAnimRotationSpeed;
    float angle = (_LegacyMToon > .5 ? turns : frac(turns)) * UNITY_TWO_PI;
    uv += time * float2(_UvAnimScrollXSpeed, _UvAnimScrollYSpeed) - .5;
    return mul(float2x2(cos(angle), -sin(angle), sin(angle), cos(angle)), uv) + .5;
}
#endif
