using UnityEngine;

public abstract class SfxController : MonoBehaviour
{
    [SerializeField] private string sfxName;

    protected void PlaySfx()
    {
        ServiceLocator.Instance.GetService<IMusicController>().PlaySfx(sfxName);
    }
}