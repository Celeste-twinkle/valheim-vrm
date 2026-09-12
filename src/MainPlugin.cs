using System.Collections;
using System.Globalization;
using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

#if DEBUG

using System.Diagnostics;
using System.Threading;

#endif

namespace ValheimVRM
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    public class MainPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.yoship1639.plugins.valheimvrm";
        public const string PluginName = "ValheimVRM";
        public const string PluginVersion = VersionInfo.PluginVersion;

        private static Harmony _harmony = new Harmony("com.yoship1639.plugins.valheimvrm.patch");

        void Awake()
        {
            // avoid float parsing error on computers with different cultures
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            Settings.ReloadGlobalSettings();
            AvatarRendering.Initialize();
            AvatarPhysics.Initialize();
            gameObject.AddComponent<AvatarSyncClient>().Initialize(Config.Bind("AvatarSync", "Enabled", true,
                "Sync each player's selected local VRM when the server runs ValheimVRM.Server. Without it, selections remain local."));
            gameObject.AddComponent<OutfitSwitcher>();
            gameObject.AddComponent<AvatarResidency>();
            gameObject.AddComponent<AvatarBloomController>();
            gameObject.AddComponent<AvatarDepthController>();

            // a semi hacky way of loading a default character, no one can name a character with and underscore as far as i am aware.
            Settings.AddSettingsFromFile("___Default", false);

            // Apply Harmony patches after the FejdStartup, this is needed because textures load way later now.
            PatchFejdStartup.Apply(_harmony);

        }


        internal static void PatchAll()
        {
            if (Settings.globalSettings.EnableProfileCode) PatchAllUpdateMethods.ApplyPatches(_harmony);

            _harmony.PatchAll();
            VRMShaders.Initialize();

        }
    }
}
