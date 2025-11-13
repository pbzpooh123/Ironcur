using UnityEngine;

public class CharacterLibrary : MonoBehaviour
{
    public static CharacterLibrary Instance;

    [Tooltip("One sprite per character slot. Index must match lobby slotIndex.")]
    public Sprite[] characterSprites;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public Sprite GetSprite(int idx)
    {
        if (characterSprites == null) return null;
        if (idx < 0 || idx >= characterSprites.Length) return null;
        return characterSprites[idx];
    }
}
