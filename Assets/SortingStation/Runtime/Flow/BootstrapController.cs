using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SortingStation
{
    public sealed class BootstrapController : MonoBehaviour
    {
        private IEnumerator Start()
        {
            AppServices services = AppServices.Ensure();
            string[] arguments = System.Environment.GetCommandLineArgs();
            bool smokeRequested = System.Array.Exists(arguments, arg => arg.IndexOf("sortingStationSmoke", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                  || string.Equals(System.Environment.GetEnvironmentVariable("SORTING_STATION_SMOKE"), "1", System.StringComparison.Ordinal);
            Debug.Log("SortingStation bootstrap. Smoke=" + smokeRequested + "; args=" + string.Join(" | ", arguments));
            if (smokeRequested && FindObjectOfType<AutomatedSmokeCapture>() == null)
            {
                services.gameObject.AddComponent<AutomatedSmokeCapture>();
            }
            yield return null;
            if (SceneManager.GetActiveScene().name == SceneNames.Bootstrap)
            {
                Debug.Log("SortingStation loading main menu.");
                SceneManager.LoadScene(SceneNames.MainMenu);
            }
        }
    }
}
