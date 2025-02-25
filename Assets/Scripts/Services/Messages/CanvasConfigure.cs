using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CanvasConfigure : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera _camera;

    private void Reset()
    {
        canvas = GetComponent<Canvas>();
    }

    private void Start()
    {
        //SetUpCanvas();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        SetUpCanvas();
    }

    private async void SetUpCanvas()
    {
        var identifies = FindObjectsOfType<IdentifyComponent>();
        if (identifies.Length <= 0) return;

        foreach (var identify in identifies)
        {
            if (identify.GetComponentName().Equals("Camera"))
            {
                _camera = identify.GetComponent<Camera>();
                if (_camera == null)
                {
                    continue;
                }

                //Debug.Log("Main Camera not found! Waiting...");
                await UniTask.Delay(100);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _camera = identify.GetComponent<Camera>();
                canvas.worldCamera = _camera;
                return;
            }
        }

        Debug.LogError("Main Camera not found!");
    }
}