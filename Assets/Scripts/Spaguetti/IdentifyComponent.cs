using UnityEngine;

public class IdentifyComponent : MonoBehaviour
{
    [SerializeField] private string componentName;
    
    public string GetComponentName()
    {
        return componentName;
    }
}
