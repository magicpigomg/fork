using System;
using System.IO;
using System.Xml.Serialization;
using MultiTool.Models;

namespace MultiTool.Services
{
    /// <summary>Хранение настроек в XML (без внешних библиотек): %AppData%\MultiTool\settings.xml.</summary>
    public static class SettingsStore
    {
        public static readonly string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiTool");

        public static readonly string FilePath = Path.Combine(Folder, "settings.xml");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    using (var stream = File.OpenRead(FilePath))
                    {
                        var loaded = new XmlSerializer(typeof(AppSettings)).Deserialize(stream) as AppSettings;
                        if (loaded != null) return loaded;
                    }
                }
            }
            catch
            {
                // битый файл настроек — используем значения по умолчанию
            }
            return new AppSettings();
        }

        /// <summary>Сохраняет настройки. Возвращает текст ошибки или null.</summary>
        public static string Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                using (var stream = File.Create(FilePath))
                {
                    new XmlSerializer(typeof(AppSettings)).Serialize(stream, settings);
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
