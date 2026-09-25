#if UNITY_ANDROID
using System.IO;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEngine;

namespace SortingStation.EditorTools
{
    /// <summary>
    /// Online radio requires network access. Keep the permission explicit even
    /// when Unity's automatic assembly scan does not detect the Android player.
    /// </summary>
    public sealed class AndroidManifestPostprocessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string basePath)
        {
            string manifestPath = Path.Combine(basePath, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            XDocument document = XDocument.Load(manifestPath);
            if (document.Root == null) return;

            XNamespace android = "http://schemas.android.com/apk/res/android";
            bool hasInternet = false;
            foreach (XElement permission in document.Root.Elements("uses-permission"))
            {
                if ((string)permission.Attribute(android + "name") == "android.permission.INTERNET")
                {
                    hasInternet = true;
                    break;
                }
            }
            if (!hasInternet)
            {
                document.Root.AddFirst(new XElement("uses-permission",
                    new XAttribute(android + "name", "android.permission.INTERNET")));
                document.Save(manifestPath);
                Debug.Log("Added Android INTERNET permission for online radio.");
            }
        }
    }
}
#endif
