using UnityEngine;

namespace Commander
{
    public class CommandMono : MonoBehaviour
    {
        private void Awake()
        {
            if (FindObjectsOfType<CommandMono>().Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Instance.RegisterService<ICommandQueue>(new CommandPlayerQueue());
        }

        public void OnDestroy()
        {
            ServiceLocator.Instance.UnregisterService<ICommandQueue>();
        }
    }
}