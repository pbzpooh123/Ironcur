using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LobbyCharacterAnim : MonoBehaviour
{
    Vector3 _baseScale;
    Vector2 _baseAnchoredPos;

    Image image;
    RectTransform rectTransform;

    void Awake()
    {
        rectTransform   = GetComponent<RectTransform>();
        image           = GetComponent<Image>();

        _baseScale      = transform.localScale;
        _baseAnchoredPos = rectTransform.anchoredPosition;
    }

    public void PlaySelectAnim()
    {
        transform.DOKill();
        rectTransform.DOKill();
        image.DOKill();

        transform.localScale = _baseScale;
        rectTransform.anchoredPosition = _baseAnchoredPos;


        transform
            .DOPunchScale(Vector3.one * 0.2f, 0.3f, 1, 0.5f)
            .SetEase(Ease.OutQuad);

        rectTransform
            .DOJumpAnchorPos(
                _baseAnchoredPos, 
                5f,                
                3,                 
                1f              
            )
            .SetEase(Ease.OutQuad);
        image
            .DOColor(Color.black, 0.15f)
            .SetEase(Ease.OutQuad);
    }

    public void PlayDeselectAnim()
    {
        transform.DOKill();
        rectTransform.DOKill();
        image.DOKill();

        transform.localScale = _baseScale;

        rectTransform
            .DOAnchorPos(_baseAnchoredPos, 0.2f)
            .SetEase(Ease.OutQuad);

        image
            .DOColor(Color.white, 0.15f)
            .SetEase(Ease.OutQuad);
    }
}
