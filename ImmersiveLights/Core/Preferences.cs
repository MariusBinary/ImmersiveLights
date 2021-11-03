using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace ImmersiveLights.Core
{
    public static class Preferences
    {
		public static string storageCurrentFile = null;

		static Preferences()
		{
			storageCurrentFile = Path.Combine(Environment.GetFolderPath(
				Environment.SpecialFolder.ApplicationData), @"ImmersiveLights\user_settings.json");
		}

        /// <summary>
        /// Ritorna un'array JSON di tutte le preferenze presenti in memoria.
        /// </summary>
        private static JObject GetAllPreferences()
        {
            return JObject.Parse(File.ReadAllText(storageCurrentFile));
        }

		/// <summary>
		/// Salva l'array JSON di tutte le preferenze in memoria.
		/// </summary>
		private static void SaveAllPreferences(JObject preferences)
		{
			File.WriteAllText(storageCurrentFile, preferences.ToString(Newtonsoft.Json.Formatting.None));
		}

		/// <summary>
		/// Salva in memoria le preferenze fornite.
		/// </summary>
		public static void SetPreference<T>(string group, string key, T value)
		{
			JObject preferences = GetAllPreferences();
			if (preferences.ContainsKey(group)) {
				if (preferences[group].ToObject<JObject>().ContainsKey(key)) {
					preferences[group][key] = JToken.FromObject(value);
				} else {
					JObject obj = preferences[group].ToObject<JObject>();
					obj.Add(key, JToken.FromObject(value));
					preferences[group] = JToken.FromObject(obj);
				}
			} else {
				JObject obj = new JObject();
				obj.Add(key, JToken.FromObject(value));
				preferences.Add(group, obj);
			}
			SaveAllPreferences(preferences);
		}

		/// <summary>
		/// Restituisce una preferenza dalla memoria.
		/// </summary>
		public static T GetPreference<T>(string group, string key)
		{
			JObject preferences = GetAllPreferences();
			if (preferences.Count > 0) {
				if (preferences.ContainsKey(group)) {
					if (preferences[group].ToObject<JObject>().ContainsKey(key)) {
						return (preferences[group][key]).ToObject<T>();
					}
				}
			}
			return default;
		}
	}
}
