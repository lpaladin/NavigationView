using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Input;
using Game.Modding;
using Game.SceneFlow;
using System.IO;
using UnityEngine;

namespace NavigationView
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(NavigationView)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public static Mod Instance;
        public Setting Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Instance = this;
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                log.Info($"Current mod asset at {asset.path}");
                I18n.LoadAll(Path.Combine(Path.GetDirectoryName(asset.path), "Locales"));
            }

            Setting = new Setting(this);
            Setting.RegisterInOptionsUI();

            Setting.RegisterKeyBindings();

            AssetDatabase.global.LoadSettings(nameof(NavigationView), Setting, new Setting(this));

            updateSystem.UpdateBefore<NavigationRouteListSystem>(SystemUpdatePhase.Rendering);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (Setting != null)
            {
                Setting.UnregisterInOptionsUI();
                Setting = null;
            }
            Instance = null;
        }
    }
}
