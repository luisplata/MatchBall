using UnityEngine;

public class MusicController : MonoBehaviour, IMusicController
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] clips;

    private void Awake()
    {
        if (FindObjectsOfType<MusicController>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        if (ServiceLocator.Instance.RegisterService<IMusicController>(this))
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    public void PlayMusic(string musicName)
    {
        if(audioSource.clip && audioSource.clip.name == musicName) return;
        StopMusic();
        audioSource.clip = SearchClip(musicName);
        audioSource.loop = true;
        audioSource.Play();
    }

    private AudioClip SearchClip(string musicName)
    {
        foreach (var clip in clips)
        {
            if (clip.name == musicName)
            {
                return clip;
            }
        }

        throw new System.Exception("Clip not found");
    }

    public void PlaySfx(string sfName)
    {
        audioSource.PlayOneShot(SearchClip(sfName));
    }

    public void StopMusic()
    {
        audioSource.Stop();
    }
}

public interface IMusicController
{
    void PlayMusic(string musicName);
    void PlaySfx(string sfName);
    void StopMusic();
}