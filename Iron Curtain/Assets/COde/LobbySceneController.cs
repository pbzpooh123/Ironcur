using UnityEngine;
using FishNet.Object;
using FishNet.Managing.Scened;
using FishNet;

public class LobbySceneController : NetworkBehaviour
{
    public static LobbySceneController Instance;

    [SerializeField] private string mainGameSceneName = "MainGameScene";

    private void Awake()
    {
        Instance = this;
    }

    [Server]
    public void ServerStartGameWithFade()
    {
        if (!NetworkManagerLobby.Instance.AllPlayersReady())
            return;
        RpcFadeOutAll();
        StartCoroutine(CoLoadAfterFade());
    }

    [ObserversRpc]
    private void RpcFadeOutAll()
    {
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.FadeOut();
    }

    private System.Collections.IEnumerator CoLoadAfterFade()
    {
        yield return new WaitForSeconds(0.6f);

        var loadData = new SceneLoadData(mainGameSceneName)
        {
            ReplaceScenes = ReplaceOption.All
        };

        InstanceFinder.SceneManager.LoadGlobalScenes(loadData);
    }
}
