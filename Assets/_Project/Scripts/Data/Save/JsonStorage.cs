using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Wendao.Data
{
    internal static class JsonStorage
    {
        private static readonly JsonSerializerSettings Settings = CreateSettings();
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static bool TryRead<T>(string path, out T value, out string error)
            where T : class
        {
            value = null;
            error = null;

            try
            {
                if (!File.Exists(path))
                {
                    error = $"JSON file does not exist: {path}";
                    return false;
                }

                string json = File.ReadAllText(path, Utf8WithoutBom);
                value = JsonConvert.DeserializeObject<T>(json, Settings);
                if (value == null)
                {
                    error = $"JSON root is null: {path}";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = $"Failed to read JSON '{path}': {exception.Message}";
                return false;
            }
        }

        public static bool TryWriteAtomic<T>(string path, T value, out string error)
            where T : class
        {
            error = null;
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";

            try
            {
                if (value == null)
                {
                    error = $"Cannot write a null JSON root: {path}";
                    return false;
                }

                string directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                {
                    error = $"JSON path has no directory: {path}";
                    return false;
                }

                Directory.CreateDirectory(directory);
                string json = JsonConvert.SerializeObject(value, Settings);
                File.WriteAllText(tempPath, json, Utf8WithoutBom);

                if (!File.Exists(path))
                {
                    File.Move(tempPath, path);
                    return true;
                }

                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                try
                {
                    File.Replace(tempPath, path, backupPath, true);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(path, backupPath, true);
                    File.Delete(path);
                    File.Move(tempPath, path);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = $"Failed to write JSON '{path}': {exception.Message}";
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // A stale temp file is harmless and can be replaced on the next save.
                }
            }
        }

        private static JsonSerializerSettings CreateSettings()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Culture = CultureInfo.InvariantCulture,
                DateParseHandling = DateParseHandling.None,
                Formatting = Formatting.Indented,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
            settings.Converters.Add(new StringEnumConverter());
            return settings;
        }
    }
}
