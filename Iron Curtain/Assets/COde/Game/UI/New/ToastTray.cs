using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToastTray : MonoBehaviour
{
    public static ToastTray Instance;

    [Header("Layout")]
    public RectTransform trayRoot;    // empty RectTransform anchored bottom-right
    public ToastItem toastPrefab;     // simple prefab with CanvasGroup + TMP_Text
    public float verticalSpacing = 6f;
    public int maxVisible = 4;

    [Header("Timing")]
    public float showAnim = 0.18f;
    public float holdTime = 2.2f;
    public float hideAnim = 0.2f;

    private readonly Queue<(string, ToastKind)> _queue = new();
    private readonly List<ToastItem> _active = new();
    private readonly Stack<ToastItem> _pool = new();

    void Awake() => Instance = this;

    public void Enqueue(string message, ToastKind kind)
    {
        _queue.Enqueue((message, kind));
        TryPump();
    }

    private void TryPump()
    {
        // keep at most maxVisible
        while (_active.Count < maxVisible && _queue.Count > 0)
        {
            var (msg, kind) = _queue.Dequeue();
            var item = GetItem();
            item.Setup(msg, kind);

            // place at bottom; push older ones upward
            _active.Insert(0, item);
            LayoutActive();

            // play lifecycle
            StartCoroutine(ShowThenHide(item));
        }
    }

    private IEnumerator ShowThenHide(ToastItem item)
    {
        yield return item.PlayShow(showAnim);
        yield return new WaitForSeconds(holdTime);
        yield return item.PlayHide(hideAnim);

        _active.Remove(item);
        Return(item);
        LayoutActive();
        TryPump();
    }

    private ToastItem GetItem()
    {
        ToastItem item = _pool.Count > 0 ? _pool.Pop() : Instantiate(toastPrefab, trayRoot);
        item.gameObject.SetActive(true);
        return item;
    }

    private void Return(ToastItem t)
    {
        t.gameObject.SetActive(false);
        _pool.Push(t);
    }

    private void LayoutActive()
    {
        float y = 0f; // grows upward
        for (int i = 0; i < _active.Count; i++)
        {
            var rt = _active[i].Rect;
            rt.anchoredPosition = new Vector2(0f, y);
            y += rt.sizeDelta.y + verticalSpacing;
        }
    }
}
