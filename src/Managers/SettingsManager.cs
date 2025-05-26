using ServerDeploymentAssistant.src.Managers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ServerDeploymentAssistant.src.Helpers
{
    public class SettingsManager
    {
        private string _filePath;
        private XDocument _xDocument;

        private static SettingsManager _instance;
        private static readonly object _lock = new object();

        public static SettingsManager Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new SettingsManager();
                    return _instance;
                }
            }
        }
        private SettingsManager()
        {
            
        }

        public void SetSettingsFile(string filePath)
        {
            try
            {
                _filePath = filePath;

                if (File.Exists(_filePath))
                {
                    _xDocument = XDocument.Load(_filePath);
                }
                else
                {
                    Logger.CreateError(
                        "Settings file not exist. " +
                        "Default settings will be used. <bool>:false, <string>:\"NaN\", <int>:0, etc.", ConsoleColor.Cyan
                    );
                    _xDocument = new XDocument(new XElement("Settings"));
                    _xDocument.Save(_filePath);
                }

                Logger.CreateLog($"Application settings is load now from path <{_filePath}>", ConsoleColor.Cyan);
            }
            catch (Exception ex)
            {
                Logger.CreateError(
                    "Unable to load settings. " +
                    "Default settings will be used. <bool>:false, <string>:\"NaN\", <int>:0, etc.", ConsoleColor.Cyan
                    );

            }
        }

        private T GetDefaultValue<T>()
        {
            if (typeof(T) == typeof(bool))
            {
                return (T)(object)false;
            }
            else if (typeof(T) == typeof(string))
            {
                return (T)(object)"NaN";
            }
            else if (typeof(T) == typeof(int))
            {
                return (T)(object)0;
            }
            else if (typeof(T) == typeof(double))
            {
                return (T)(object)10.0;
            }
            else
            {
                return (T)(object)default(T);
            }
        }
        public T GetValue<T>(string group, string key)
        {
            var settingElement = _xDocument.Root
                .Elements("Group")
                .FirstOrDefault(g => g.Attribute("Name")?.Value == group)?
                .Elements("Setting")
                .FirstOrDefault(s => s.Attribute("Key")?.Value == key);

            if (settingElement == null)
            {
                var defaultValue = GetDefaultValue<T>();

                SetValue(group, key, defaultValue);
                Debug.WriteLine($"Setting '{key}' in group '{group}' not found.");
                return defaultValue;
            }
            string value = settingElement.Attribute("Value")?.Value;
            string type = settingElement.Attribute("Type")?.Value;

            if (typeof(T) == typeof(int) && type == "int" && int.TryParse(value, out int intValue))
                return (T)(object)intValue;

            if (typeof(T) == typeof(double) && type == "double" && double.TryParse(value, out double doubleValue))
                return (T)(object)doubleValue;

            if (typeof(T) == typeof(bool) && type == "bool" && bool.TryParse(value, out bool boolValue))
                return (T)(object)boolValue;

            if (typeof(T) == typeof(string) && type == "string")
                return (T)(object)value;

            throw new Exception($"Type mismatch or unsupported type for setting '{key}' in group '{group}'.");
        }

        public void SetValue<T>(string group, string key, T value)
        {
            string type;
            string valueString;

            if (value is int)
            {
                type = "int";
                valueString = value.ToString();
            }
            else if (value is double)
            {
                type = "double";
                valueString = value.ToString();
            }
            else if (value is bool)
            {
                type = "bool";
                valueString = value.ToString().ToLower();
            }
            else if (value is string)
            {
                type = "string";
                valueString = value as string;
            }
            else
            {
                throw new Exception($"Unsupported value type. {typeof(T)}. Value is {value}");
            }

            var groupElement = _xDocument.Root
                .Elements("Group")
                .FirstOrDefault(g => g.Attribute("Name")?.Value == group);

            if (groupElement == null)
            {
                groupElement = new XElement("Group", new XAttribute("Name", group));
                _xDocument.Root.Add(groupElement);
            }

            var settingElement = groupElement
                .Elements("Setting")
                .FirstOrDefault(s => s.Attribute("Key")?.Value == key);

            if (settingElement != null)
            {
                settingElement.SetAttributeValue("Value", valueString);
                settingElement.SetAttributeValue("Type", type);
            }
            else
            {
                groupElement.Add(new XElement("Setting",
                    new XAttribute("Key", key),
                    new XAttribute("Type", type),
                    new XAttribute("Value", valueString)));
            }

            _xDocument.Save(_filePath);
        }

    }
}
