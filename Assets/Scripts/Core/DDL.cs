using UnityEngine;

namespace ETD.Core
{
    public class DDL : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
