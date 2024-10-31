using Colossal;
using Colossal.Json;
using Game.SceneFlow;
using System;
using System.Collections.Generic;
using System.IO;

namespace NavigationView
{
    public class Locale : IDictionarySource
    {
        private readonly Dictionary<string, string> m_LocaleDict;
        public Locale(Dictionary<string, string> localeDict)
        {
            m_LocaleDict = localeDict;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return m_LocaleDict;
        }

        public void Unload()
        {

        }
    }

    public class I18n
    {
        public static void LoadAll(string localesPath)
        {
            var directoryInfo = new DirectoryInfo(localesPath);
            Mod.log.Info($"Loading locales from directory: {directoryInfo.FullName}");
            var files = directoryInfo.GetFiles("*.json", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                Mod.log.Info($"Loading {file.Name}");
                try
                {
                    var dict = JSON.Load(File.ReadAllText(file.FullName));
                    var locale = file.Name.Replace(file.Extension, "");
                    GameManager.instance.localizationManager.AddSource(locale, new Locale(dict.Make<Dictionary<string, string>>()));
                }
                catch (Exception ex)
                {
                    Mod.log.Error(ex, $"Failed to load locale file {file.Name}");
                }
            }
        }
    }
}
