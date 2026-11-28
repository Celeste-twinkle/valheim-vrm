#if UNITY_EDITOR
using UnityEditor;
public static class BuildDepthConsumer
{
    public static void Build()
    {
        BuildPipeline.BuildAssetBundles("../Build",new[]{new AssetBundleBuild{
            assetBundleName="render_test",assetNames=new[]{"Assets/RenderingTests/DepthConsumer.shader","Assets/RenderingTests/UniUnlit.shader"}
        }},BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
    }
}
#endif
