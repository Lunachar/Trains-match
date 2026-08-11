#if UNITY_ANDROID
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEngine;

namespace SortingStation.EditorTools
{
    /// <summary>
    /// The game has no network features. Unity can still add INTERNET while
    /// scanning its assemblies, so remove that permission from the generated
    /// Gradle library immediately before packaging the APK.
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
            XElement[] internetPermissions = document.Root
                .Elements("uses-permission")
                .Where(element => (string)element.Attribute(android + "name") == "android.permission.INTERNET")
                .ToArray();
            foreach (XElement permission in internetPermissions) permission.Remove();

            if (internetPermissions.Length > 0)
            {
                document.Save(manifestPath);
                Debug.Log("Removed unused Android INTERNET permission for the offline game.");
            }
        }
    }
}
#endif
