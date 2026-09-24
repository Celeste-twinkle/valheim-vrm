using System;
using System.IO;
using System.Linq;
using ValheimVRM;

static class Program
{
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "ValheimVRM-Catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var catalog = new AvatarCatalog(root, root);
            catalog.Refresh();
            Require(catalog.Names.Length == 0, "An empty folder must be supported.");
            for (int i = 0; i < 24; i++) File.WriteAllText(Path.Combine(root, "Avatar " + i + ".vrm"), "fixture");
            File.WriteAllText(Path.Combine(root, "中文 空格.VRM"), "fixture");
            File.WriteAllText(Path.Combine(root, "readme.txt"), "ignore");
            Directory.CreateDirectory(Path.Combine(root, "Shared"));
            File.WriteAllText(Path.Combine(root, "Shared", "Peer.vrm"), "ignore");
            catalog.Refresh();
            Require(catalog.Names.Length == 25, "The list must not stop at eight or include shared downloads.");
            Require(catalog.TryGetPath("中文 空格", out var path) && Path.GetExtension(path) == ".VRM", "Preserve Unicode and extension casing.");
            Require(!catalog.TryGetPath("../outside", out _), "Reject directory traversal.");
            catalog.Select("First player", "中文 空格");
            catalog.Select("Second player", "Avatar 23");
            var reload = new AvatarCatalog(root, root);
            reload.Refresh();
            reload.LoadSelections();
            Require(reload.Resolve("First player") == "中文 空格", "The first selection must survive a restart.");
            Require(reload.Resolve("Second player") == "Avatar 23", "Selections must be independent per character.");
            reload.Select("First player", AvatarCatalog.OriginalModel);
            File.WriteAllText(Path.Combine(root, "___Default.vrm"), "fixture");
            File.WriteAllText(Path.Combine(root, "First player.vrm"), "fixture");
            reload.Refresh(); reload.LoadSelections();
            Require(reload.IsOriginalSelected("First player") && reload.Resolve("First player") == "",
                "Explicit native selection did not suppress model/default fallback after restart.");
            Require(reload.Resolve("Second player") == "Avatar 23", "Native selection changed another character.");
            reload.Select("First player", "中文 空格");
            Require(!reload.IsOriginalSelected("First player"), "Selecting VRM did not leave native mode.");
            File.Delete(path);
            Require(reload.Names.Contains("中文 空格"), "Fixture did not begin with a cached catalog entry.");
            Require(reload.RefreshAndReportChanges(), "Deleting a VRM was not detected as a catalog change.");
            Require(!reload.Names.Contains("中文 空格") && !reload.TryGetPath("中文 空格", out _),
                "A deleted VRM remained in the refreshed picker catalog.");
            Require(!reload.RefreshAndReportChanges(), "An unchanged folder was reported as changed.");
            Require(reload.Resolve("First player") == "First player", "Removed models must fall back to character matching.");
            File.WriteAllText(Path.Combine(root, "avatar_selections.json"), "{\"First player\":\"../outside\"}");
            reload.LoadSelections();
            Require(reload.Resolve("First player") == "First player", "Stored paths must not bypass the catalog.");
            File.Delete(Path.Combine(root, "avatar_selections.json"));
            VrmOnly(Path.Combine(root, "vrm-only"));
            Console.WriteLine("PASS: discovery, 25 models, Unicode, cached snapshot until manual refresh, persistence, boundaries, and separate configuration.");
        }
        finally
        {
            // This randomly named directory was created by this process.
            Directory.Delete(root, true);
        }
    }

    static void VrmOnly(string root)
    {
        var models = Path.Combine(root, "ValheimVRM");
        var config = Path.Combine(root, "BepInEx", "config", "ValheimVRM");
        Directory.CreateDirectory(models);
        File.WriteAllText(Path.Combine(models, "Only.vrm"), "model bytes");
        var catalog = new AvatarCatalog(models, config);
        catalog.Refresh(); catalog.LoadSelections(); catalog.Select("A", "Only");
        var reload = new AvatarCatalog(models, config);
        reload.Refresh(); reload.LoadSelections();
        Require(reload.Resolve("A") == "Only", "Selections must persist outside the model directory.");
        Require(Directory.GetFileSystemEntries(models).Length == 1, "Selecting must not write into the model library.");
        File.WriteAllText(Path.Combine(models, "avatar_selections.json"), "invalid old configuration");
        reload.LoadSelections();
        Require(reload.Resolve("A") == "Only", "Old model-side configuration must be ignored.");
        File.Delete(Path.Combine(config, "avatar_selections.json"));
        reload.LoadSelections();
        Require(reload.Resolve("A") == "A", "Missing new configuration must reset to defaults, without legacy fallback.");
        Console.WriteLine("PASS: VRM-only library, separate persistence, old model-side configuration ignored, missing configuration defaults.");
    }
}
