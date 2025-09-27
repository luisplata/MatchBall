using UnityEngine;

public class InstallerServiceLocator : MonoBehaviour
{
    private void Awake()
    {
        if (FindObjectsOfType<InstallerServiceLocator>().Length > 1)
        {
            Destroy(gameObject);
        }
        else
        {
            ServiceLocator.Instance.RegisterService<IGlobalParameters>(new Global());
            ServiceLocator.Instance.RegisterService<IClaimRewardService>(new ClaimRewardService());
            DontDestroyOnLoad(gameObject);
        }
    }
}