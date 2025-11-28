using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    void Awake()
    {
       
        Instance = this;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        // Start fully black, fade in on first scene
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        FadeIn();
    }

    public void FadeIn(System.Action onComplete = null)
    {
        canvasGroup.DOKill();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        canvasGroup.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            onComplete?.Invoke();
        });
    }

    public void FadeOut(System.Action onComplete = null)
    {
        canvasGroup.DOKill();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        canvasGroup.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            onComplete?.Invoke();
        });
    }
}
