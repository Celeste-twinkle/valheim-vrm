#ifndef VALHEIM_VRM_FUR_LIGHTING_INCLUDED
#define VALHEIM_VRM_FUR_LIGHTING_INCLUDED
// Native VRM 0.x MToon response, kept in sync with MToon/MToonCore.cginc
// (UniVRM, MIT). Mapping its shade shift onto MToon 1.0 is not equivalent:
// legacy MToon also caps base lighting to albedo and has shading-grade maps.
sampler2D _ReceiveShadowTexture, _ShadingGradeTexture;
half _ReceiveShadowRate, _ShadingGradeRate, _LightColorAttenuation;

half4 GetFurLegacyLighting(UnityLighting light, MToonInput surface)
{
    half3 emission = MTOON_SAMPLE_TEXTURE2D(_EmissionMap, surface.uv).rgb * _EmissionColor.rgb;
    if (_AvatarSceneLighting < .5)
        return half4(MToon_IsForwardBasePass() ? surface.litColor + emission : 0, surface.alpha);
    half dotNL = dot(light.directLightDirection, surface.normalWS);
    half attenuation = light.directLightAttenuation.x;
    half shadow = MToon_IsForwardBasePass() ? attenuation * lerp(1, attenuation,
        _ReceiveShadowRate * tex2D(_ReceiveShadowTexture, surface.uv).r) : 1;
    half grade = 1 - _ShadingGradeRate * (1 - tex2D(_ShadingGradeTexture, surface.uv).r);
    half intensity = ((dotNL * .5 + .5) * shadow * grade) * 2 - 1;
    half upper = lerp(1, _ShadingShiftFactor, _ShadingToonyFactor);
    intensity = saturate((intensity - _ShadingShiftFactor) / max(EPSILON_FP16, upper - _ShadingShiftFactor));
    half3 direct = lerp(light.directLightColor,
        max(EPSILON_FP16, max(light.directLightColor.r, max(light.directLightColor.g, light.directLightColor.b))), _LightColorAttenuation);
    if (!MToon_IsForwardBasePass())
    {
        if (_FurBaseAlphaMode > 1.5) direct *= step(0, dotNL);
        direct *= .5 * (min(0, dotNL) + 1) * attenuation;
    }
    half3 color = lerp(MTOON_SAMPLE_TEXTURE2D(_ShadeTex, surface.uv).rgb * _ShadeColor.rgb, surface.litColor, intensity) * direct;
    half3 indirect = 0;
    if (MToon_IsForwardBasePass())
    {
        indirect = lerp(light.indirectLight, light.indirectLightEqualized, _GiEqualization);
        indirect = lerp(indirect, max(EPSILON_FP16, max(indirect.r, max(indirect.g, indirect.b))), _LightColorAttenuation);
        color = min(color + indirect * surface.litColor, surface.litColor);
    }
    half3 rimLight = lerp(MToon_IsForwardBasePass() ? 1 : 0, direct + indirect, _RimLightingMix);
    color += pow(saturate(1 - dot(surface.normalWS, surface.viewDirWS) + _RimLift), max(_RimFresnelPower, EPSILON_FP16)) *
        _RimColor.rgb * MTOON_SAMPLE_TEXTURE2D(_RimTex, surface.uv).rgb * rimLight;
    if (MToon_IsForwardBasePass())
    {
        half3 up = normalize(UNITY_MATRIX_V[1].xyz);
        up = normalize(up - surface.viewDirWS * dot(surface.viewDirWS, up));
        half3 right = normalize(cross(surface.viewDirWS, up));
        half2 uv = half2(dot(right, surface.normalWS), dot(up, surface.normalWS)) * .5 + .5;
        color += MTOON_SAMPLE_TEXTURE2D(_MatcapTex, uv).rgb + emission;
    }
    return half4(color, surface.alpha);
}
#endif
