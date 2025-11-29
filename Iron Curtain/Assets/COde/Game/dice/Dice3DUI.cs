using UnityEngine;
using System.Collections;
using DG.Tweening;

public class Dice3DUI : MonoBehaviour
{
    public static Dice3DUI Instance;

    [Header("Dice roots (position / tilt)")]
    public Transform die1Root;
    public Transform die2Root;

    [Header("Dice meshes (rotated per value)")]
    public Transform die1Mesh;
    public Transform die2Mesh;

    [Header("Animation settings")]
    public float rollDuration = 1.2f;
    public float snapDuration = 0.25f;

    [Header("Optional: parent for enabling/disabling UI")]
    public GameObject root;

    [Header("Debug (Editor only)")]
    public bool debugSnapInEditor = false;
    [Range(1, 6)] public int debugValue1 = 1;
    [Range(1, 6)] public int debugValue2 = 1;

    void Awake()
    {
        Instance = this;
        if (die1Root != null)
        {
            var e = die1Root.eulerAngles;
            e.x = -90f;
            die1Root.eulerAngles = e;
        }

        if (die2Root != null)
        {
            var e = die2Root.eulerAngles;
            e.x = -90f;
            die2Root.eulerAngles = e;
        }
        HideImmediate();
    }

    public void ShowDiceRollingWithCallback(int d1, int d2, System.Action onDone)
    {
        StopAllCoroutines();
        StartCoroutine(CoRoll(d1, d2, onDone));
    }

    private IEnumerator CoRoll(int d1, int d2, System.Action onDone)
    {
        ShowImmediate();

        if (die1Mesh != null) die1Mesh.localRotation = Random.rotation;
        if (die2Mesh != null) die2Mesh.localRotation = Random.rotation;

        float elapsed = 0f;
        while (elapsed < rollDuration)
        {
            elapsed += Time.deltaTime;

            if (die1Mesh != null)
                die1Mesh.Rotate(Vector3.up * 720f * Time.deltaTime, Space.Self);
            if (die2Mesh != null)
                die2Mesh.Rotate(Vector3.up * 720f * Time.deltaTime, Space.Self);

            yield return null;
        }

        if (die1Mesh != null)
        {
            die1Mesh.DOKill();
            die1Mesh.DOLocalRotate(GetRotationForValue(d1), snapDuration, RotateMode.Fast)
                    .SetEase(Ease.OutQuad);
        }

        if (die2Mesh != null)
        {
            die2Mesh.DOKill();
            die2Mesh.DOLocalRotate(GetRotationForValue(d2), snapDuration, RotateMode.Fast)
                    .SetEase(Ease.OutQuad);
        }

        yield return new WaitForSeconds(snapDuration + 0.1f);

        HideImmediate();
        onDone?.Invoke();
    }

    private void ShowImmediate()
    {
        if (root != null) root.SetActive(true);
    }

    private void HideImmediate()
    {
        if (root != null) root.SetActive(false);
    }

    
    private Vector3 GetRotationForValue(int value)
    {
        switch (value)
        {
            case 1: return new Vector3(-90f, 0f, 0f); 
            case 2: return new Vector3(-180f, 0f, 0f); 
            case 3: return new Vector3(-90f, 0f, 90f); 
            case 4: return new Vector3(90f, 0f, 90f); 
            case 5: return new Vector3(0f, 0f, 90f); 
            case 6: return new Vector3(0f, 90f, 90f);
            default: return Vector3.zero;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!debugSnapInEditor) return;

        if (die1Mesh != null)
            die1Mesh.localRotation = Quaternion.Euler(GetRotationForValue(debugValue1));
        if (die2Mesh != null)
            die2Mesh.localRotation = Quaternion.Euler(GetRotationForValue(debugValue2));
    }
#endif
}
