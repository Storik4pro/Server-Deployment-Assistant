using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ServerDeploymentAssistant
{
    public static class JavaScriptHelper
    {
        public static string LoadEmbeddedScript(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            string[] names = assembly.GetManifestResourceNames();
            if (!Array.Exists(names, n => n == resourceName))
                throw new ArgumentException($"{resourceName} is not exist");

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException($"{resourceName} is not exist");

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public readonly static string script = LoadEmbeddedScript("ServerDeploymentAssistant.src.JavaScript.GetTextElement.js");
        public readonly static string SetFullPageSize = LoadEmbeddedScript("ServerDeploymentAssistant.src.JavaScript.SetFullPageSize.js");
        public readonly static string GetActiveElementText = LoadEmbeddedScript("ServerDeploymentAssistant.src.JavaScript.GetActiveElementText.js");
        public readonly static string GetFocusActiveElementText = LoadEmbeddedScript("ServerDeploymentAssistant.src.JavaScript.GetFocusActiveElementText.js");
        public readonly static string SetCursorInInputField = LoadEmbeddedScript("ServerDeploymentAssistant.src.JavaScript.SetCursorInInputField.js");
    }
}
