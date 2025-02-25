using UnityEngine;
using UnityEngine.InputSystem;

public class csDemoSceneCode : MonoBehaviour
{
    public string[] EffectNames;
    public string[] Effect2Names;
    public Transform[] Effect;
    private int i = 0;
    private int a = 0;

    private InputAction zAction;
    private InputAction xAction;
    private InputAction cAction;

    void Start()
    {
        Instantiate(Effect[i], new Vector3(0, 5, 0), Quaternion.identity);

        var inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
        var inputActionMap = inputActionAsset.AddActionMap("DemoActions");

        zAction = inputActionMap.AddAction("Z", binding: "<Keyboard>/z");
        xAction = inputActionMap.AddAction("X", binding: "<Keyboard>/x");
        cAction = inputActionMap.AddAction("C", binding: "<Keyboard>/c");

        zAction.performed += ctx => OnZPressed();
        xAction.performed += ctx => OnXPressed();
        cAction.performed += ctx => OnCPressed();

        inputActionAsset.Enable();
    }

    void OnDestroy()
    {
        zAction.Disable();
        xAction.Disable();
        cAction.Disable();
    }

    private void OnZPressed()
    {
        if (i <= 0)
            i = 99;
        else
            i--;

        for (a = 0; a < Effect2Names.Length; a++)
        {
            if (EffectNames[i] == Effect2Names[a])
            {
                Instantiate(Effect[i], new Vector3(0, 0.2f, 0), Quaternion.identity);
                break;
            }
        }
        if (a++ == Effect2Names.Length)
            Instantiate(Effect[i], new Vector3(0, 5, 0), Quaternion.identity);
    }

    private void OnXPressed()
    {
        if (i < 99)
            i++;
        else
            i = 0;

        for (a = 0; a < Effect2Names.Length; a++)
        {
            if (EffectNames[i] == Effect2Names[a])
            {
                Instantiate(Effect[i], new Vector3(0, 0.2f, 0), Quaternion.identity);
                break;
            }
        }
        if (a++ == Effect2Names.Length)
            Instantiate(Effect[i], new Vector3(0, 5, 0), Quaternion.identity);
    }

    private void OnCPressed()
    {
        for (a = 0; a < Effect2Names.Length; a++)
        {
            if (EffectNames[i] == Effect2Names[a])
            {
                Instantiate(Effect[i], new Vector3(0, 0.2f, 0), Quaternion.identity);
                break;
            }
        }
        if (a++ == Effect2Names.Length)
            Instantiate(Effect[i], new Vector3(0, 5, 0), Quaternion.identity);
    }
}