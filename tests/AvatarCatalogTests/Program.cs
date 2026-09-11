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
            var catalog = new AvatarCatalog(root);
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
            var reload = new AvatarCatalog(root);
            reload.Refresh();
            reload.LoadSelections();
            Require(reload.Resolve("First player") == "中文 空格", "The first selection must survive a restart.");
            Require(reload.Resolve("Second player") == "Avatar 23", "Selections must be independent per character.");
            File.Delete(path);
            Require(reload.Resolve("First player") == "First player", "Removed models must fall back to character matching.");
            File.WriteAllText(Path.Combine(root, "avatar_selections.json"), "{\"First player\":\"../outside\"}");
            reload.LoadSelections();
            Require(reload.Resolve("First player") == "First player", "Stored paths must not bypass the catalog.");
            File.Delete(Path.Combine(root, "avatar_selections.json"));
            File.WriteAllText(Path.Combine(root, "Costume_Summer.vrm"), "fixture");
            File.WriteAllText(Path.Combine(root, "selected_models.json"), "{\"Legacy player\":\"Summer\"}");
            var migrated = new AvatarCatalog(root);
            migrated.Refresh();
            migrated.LoadSelections();
            Require(migrated.Resolve("Legacy player") == "Costume_Summer", "Keep existing local-build selections.");
            File.WriteAllText(Path.Combine(root, "Other_Summer.vrm"), "fixture");
            var ambiguous = new AvatarCatalog(root);
            ambiguous.Refresh();
            ambiguous.LoadSelections();
            Require(ambiguous.Resolve("Legacy player") == "Legacy player", "Do not guess ambiguous old identifiers.");
            Console.WriteLine("PASS: discovery, 25 models, Unicode, refresh, persistence, boundaries, and legacy migration.");
        }
        finally
        {
            // This randomly named directory was created by this process.
            Directory.Delete(root, true);
        }
    }
}
