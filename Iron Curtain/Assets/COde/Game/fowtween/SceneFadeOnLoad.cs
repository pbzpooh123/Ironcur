using UnityEngine;

public class SceneFadeOnLoad : MonoBehaviour
{
    void Start()
    {
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.FadeIn();
    }
}
