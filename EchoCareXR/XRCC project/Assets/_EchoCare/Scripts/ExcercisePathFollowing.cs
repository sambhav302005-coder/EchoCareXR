using UnityEngine;

namespace ECHO
{
    public class ExcercisePathFollowing : MonoBehaviour
    {
        private EchoManager echoManager;

        internal void StartExcercise()
        {
            // show
            echoManager = EchoManager.Instance;
            echoManager.testTableHandler.ShowPathFollowing();
        }

        private void Awake() { }
    }
}
