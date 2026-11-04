using UnityEngine;

namespace Bootstrap
{
    public class GameInstaller : MonoBehaviour
    {
        [SerializeField] private string url, uri, interstitialId, rewardedId;

        private void Start()
        {
            #if UNITY_EDITOR
                url = "http://localhost";
            #endif
            //TODO: Falta refactorizar el pase de batalla para darle manejo.
            DontDestroyOnLoad(gameObject);
            GameCompositionRoot.Install(url, uri);
        }
    }
}