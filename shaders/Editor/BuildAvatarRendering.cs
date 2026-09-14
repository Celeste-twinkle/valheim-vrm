using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildAvatarRendering
{
    public static void Build()
    {
        const string shaderPath = "Assets/AvatarRendering/MToon10/vrmc_materials_mtoon.shader";
        AssetDatabase.Refresh();
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Rendering shader has errors.");
        var depth = AssetDatabase.LoadAssetAtPath<Shader>("Assets/AvatarRendering/AvatarDepth.shader");
        if (depth == null || ShaderUtil.ShaderHasError(depth)) throw new Exception("Depth shader has errors.");
        const string legacyPath = "Assets/AvatarRendering/MToon/MToon.shader";
        var legacy = AssetDatabase.LoadAssetAtPath<Shader>(legacyPath);
        if (legacy == null || ShaderUtil.ShaderHasError(legacy)) throw new Exception("Legacy MToon shader has errors.");
        Directory.CreateDirectory("../Build");
        var manifest = BuildPipeline.BuildAssetBundles("../Build", new[] {
            new AssetBundleBuild { assetBundleName = "avatar_rendering", assetNames = new[] { shaderPath, legacyPath, "Assets/AvatarRendering/AvatarDepth.shader" } },
            new AssetBundleBuild { assetBundleName = "avatar_bloom", assetNames = new[] { "Assets/AlbedoLit/AvatarBloom.shader" } }
        }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        if (manifest == null || ShaderUtil.ShaderHasError(shader) || ShaderUtil.ShaderHasError(depth) || ShaderUtil.ShaderHasError(legacy)) throw new Exception("Rendering bundle build failed.");
        Debug.Log("AVATAR_RENDERING_BUNDLE_OK");
    }
}
