using System.Collections.Generic;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Connection;
using UnityEngine;

public class MainGameStarter : MonoBehaviour
{
    [Header("Start Points (server order)")]
    public List<Transform> startPoints = new();

    private void OnEnable()
    {
        InstanceFinder.SceneManager.OnLoadEnd += HandleLoadEnd;
    }

    private void OnDisable()
    {
        if (InstanceFinder.SceneManager != null)
            InstanceFinder.SceneManager.OnLoadEnd -= HandleLoadEnd;
    }

    private void HandleLoadEnd(SceneLoadEndEventArgs args)
    {
        
        if (!InstanceFinder.IsServer)
            return;

   
        bool loadedMain = false;
        foreach (var s in args.LoadedScenes) if (s.name == "MainGameScene") { loadedMain = true; break; }
        if (!loadedMain) return;

       
        int index = 0;
        foreach (var kvp in InstanceFinder.ServerManager.Clients)
        {
            NetworkConnection conn = kvp.Value;
            if (conn == null || conn.FirstObject == null) continue;

         
            if (conn.FirstObject.TryGetComponent(out PlayerPawn pawn))
            {
                Transform spawn = startPoints.Count > 0 ? startPoints[index % startPoints.Count] : null;
                if (spawn != null)
                {
                   
                    pawn.transform.position = spawn.position;
                    pawn.transform.rotation = spawn.rotation;
                }
               
            }

           
            if (conn.FirstObject.TryGetComponent(out NetworkLobbyPlayer lobbyPlayer))
            {
                
                if (!lobbyPlayer.HudSpawned)
                {
                    lobbyPlayer.HudSpawned = true;
                    lobbyPlayer.TargetSetHUD(
                        conn,
                        index,                                 
                        lobbyPlayer.playerName.Value,          
                        0                        
                    );
                }
            }

            index++;
        }
    }
}
