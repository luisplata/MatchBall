using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class AssetFactory<T> where T : UnityEngine.Object
{
    private static Dictionary<string, List<AsyncOperationHandle>> groupHandles = new();
    private const string DEFAULT_GROUP = "__ungrouped__";

    /// Carga un asset por clave (address) y lo asocia a un grupo lógico.
    public static async UniTask<T> LoadAsync(string groupLabel, string assetKey)
    {
        var handle = Addressables.LoadAssetAsync<T>(assetKey);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            RegisterHandle(groupLabel, handle);
            return handle.Result;
        }
        else
        {
            Debug.LogError($"[AssetFactory<{typeof(T).Name}>] No se pudo cargar '{assetKey}' en grupo '{groupLabel}'");
            return null;
        }
    }

    /// ✅ Nueva función: carga asset por clave sin grupo
    public static async UniTask<T> LoadByKeyAsync(string assetKey)
    {
        var handle = Addressables.LoadAssetAsync<T>(assetKey);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            RegisterHandle(DEFAULT_GROUP, handle);
            return handle.Result;
        }
        else
        {
            Debug.LogError($"[AssetFactory<{typeof(T).Name}] No se pudo cargar '{assetKey}' (sin grupo)");
            return null;
        }
    }


    /// Carga todos los assets con la etiqueta indicada.
    public static async UniTask<List<T>> LoadGroupAsync(string groupLabel)
    {
        var handle = Addressables.LoadAssetsAsync<T>(groupLabel, null);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            RegisterHandle(groupLabel, handle);
            return new List<T>(handle.Result);
        }
        else
        {
            Debug.LogError($"[AssetFactory<{typeof(T).Name}>] No se pudo cargar grupo '{groupLabel}'");
            return null;
        }
    }

    /// Verifica si un asset con la clave indicada existe.
    public static async UniTask<bool> KeyExistsAsync(string assetKey)
    {
        var handle = Addressables.LoadResourceLocationsAsync(assetKey);

        await handle.Task;

        bool exists = handle.Status == AsyncOperationStatus.Succeeded && handle.Result.Count > 0;

        Addressables.Release(handle);
        return exists;
    }

    /// Libera todos los assets de un grupo.
    public static void UnloadGroup(string groupLabel)
    {
        if (groupHandles.TryGetValue(groupLabel, out var handles))
        {
            foreach (var handle in handles)
                Addressables.Release(handle);

            groupHandles.Remove(groupLabel);
        }
    }

    /// Libera todos los assets registrados por la fábrica.
    public static void UnloadAll()
    {
        foreach (var handles in groupHandles.Values)
        {
            foreach (var handle in handles)
                Addressables.Release(handle);
        }

        groupHandles.Clear();
    }

    private static void RegisterHandle(string groupLabel, AsyncOperationHandle handle)
    {
        if (!groupHandles.ContainsKey(groupLabel))
            groupHandles[groupLabel] = new List<AsyncOperationHandle>();

        groupHandles[groupLabel].Add(handle);
    }
}