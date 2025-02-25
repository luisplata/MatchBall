using Cysharp.Threading.Tasks;
using UnityEngine;

internal class VfxManagerGame : MonoBehaviour
{
    [SerializeField] private GameObject vfxCollision;
    [SerializeField] private GameObject vfxPointToInstantiate;
    private ParticleSystem particleSystem;
    private GameObject _vfx;

    public async UniTask StartVfx()
    {
        _vfx = Instantiate(vfxCollision, vfxPointToInstantiate.transform.position, Quaternion.identity,
            vfxPointToInstantiate.transform);
        particleSystem = _vfx.GetComponent<ParticleSystem>();
        particleSystem.Stop();
        _vfx.transform.position = vfxPointToInstantiate.transform.position;
        //_vfx.SetActive(true);
        particleSystem.Play();
        await UniTask.WaitForSeconds(.9f);
        Destroy(_vfx.gameObject);
        Destroy(particleSystem.gameObject);
    }

    public void SetVfx(GameObject vfxToDestroyBalls)
    {
        vfxCollision = vfxToDestroyBalls;
    }
}