using UnityEngine;

namespace ECHO
{
    public class ExcerciseDelayedRecall : MonoBehaviour
    {
        [SerializeField]
        private AudioClip[] clips;

        private EchoManager echoManager;

        //get echo manager instance
        private EchoManager EM => EchoManager.Instance;

        private void Awake() { }

        internal void StartExercise()
        {
            echoManager = EchoManager.Instance;

            echoManager.RevealCompanion();
            echoManager.testTableHandler.ShowDelayedRecall();
        }
    }
}
