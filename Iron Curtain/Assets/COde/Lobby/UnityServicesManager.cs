// UnityServicesManager.cs
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using System.Threading.Tasks;

public class UnityServicesManager : MonoBehaviour
{
    public static bool IsInitialized = false;

    private async void Awake()
    {
        if (!IsInitialized)
        {
            await InitializeUnity();
            IsInitialized = true;
        }
    }

    private async Task InitializeUnity()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Signed in with player ID: " + AuthenticationService.Instance.PlayerId);
        }
    }
}