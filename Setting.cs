using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace NavigationView
{
    [FileLocation(nameof(NavigationView))]
    public class Setting : ModSetting
    {

        public Setting(IMod mod) : base(mod)
        {

        }

        [SettingsUISlider(min = 0, max = 180, step = 1, scalarMultiplier = 1, unit = Unit.kScreenFrequency)]
        public int RefreshFrequency { get; set; } = 2;

        public override void SetDefaults()
        {
            RefreshFrequency = 2;
        }
    }
}
