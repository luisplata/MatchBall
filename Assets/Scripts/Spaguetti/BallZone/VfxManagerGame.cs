using Cysharp.Threading.Tasks;
using UnityEngine;

internal class VfxManagerGame : MonoBehaviour
{
    [SerializeField] private GameObject vfxCollision;
    [SerializeField] private GameObject vfxPointToInstantiate;

    public GameObject PointToBase => vfxPointToInstantiate;

    public async UniTask StartVfx()
    {
        var vfx = Instantiate(vfxCollision, vfxPointToInstantiate.transform.position, Quaternion.identity,
            vfxPointToInstantiate.transform);
        var particleSystem = vfx.GetComponent<ParticleSystem>();
        particleSystem.Stop();
        vfx.transform.position = vfxPointToInstantiate.transform.position;
        //_vfx.SetActive(true);
        particleSystem.Play();
        await UniTask.WaitForSeconds(.9f);
        if (vfx != null) Destroy(vfx);
    }

    public void SetVfx(GameObject vfxToDestroyBalls)
    {
        vfxCollision = vfxToDestroyBalls;
    }
}