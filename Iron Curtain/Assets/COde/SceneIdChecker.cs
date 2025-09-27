using FishNet.Managing.Scened;
using UnityEngine;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

public class SceneChecker : MonoBehaviour
{
    void Start()
    {
        var active = SceneManager.GetActiveScene();
        var lookup = new SceneLookupData(active);
        
        Debug.Log($"Unity Scene: {active.name}, BuildIndex: {active.buildIndex}");
        Debug.Log($"FishNet Scene Handle: {lookup.Handle}");
    }
}