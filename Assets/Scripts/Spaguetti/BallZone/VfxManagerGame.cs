using Cysharp.Threading.Tasks;
using UnityEngine;

internal class VfxManagerGame : MonoBehaviour
{
    [SerializeField] private GameObject vfxCollision;
    [SerializeField] private GameObject vfxPointToInstantiate;
    private ParticleSystem _particleSystem;
    private GameObject _vfx;

    public GameObject PointToBase => vfxPointToInstantiate;

    public async UniTask StartVfx()
    {
        _vfx = Instantiate(vfxCollision, vfxPointToInstantiate.transform.position, Quaternion.identity,
            vfxPointToInstantiate.transform);
        _particleSystem = _vfx.GetComponent<ParticleSystem>();
        _particleSystem.Stop();
        _vfx.transform.position = vfxPointToInstantiate.transform.position;
        //_vfx.SetActive(true);
        _particleSystem.Play();
        await UniTask.WaitForSeconds(.9f);
        Destroy(_vfx.gameObject);
        Destroy(_particleSystem.gameObject);
    }

    public void SetVfx(GameObject vfxToDestroyBalls)
    {
        vfxCollision = vfxToDestroyBalls;
    }
}