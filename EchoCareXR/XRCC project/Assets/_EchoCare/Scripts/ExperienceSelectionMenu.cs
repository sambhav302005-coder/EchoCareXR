using DG.Tweening;
using UnityEngine;

namespace ECHO
{
    public class ExperienceSelectionMenu : MonoBehaviour
    {
        private EchoManager echoManager;

        private void Awake()
        {
            echoManager = EchoManager.Instance;
        }

        private void OnEnable()
        {
            transform.DOScale(Vector3.one, 0.5f).From(Vector3.zero).SetEase(Ease.OutBack);
        }

        public void SelectDelayed()
        {
            //print("-- Delayed Recall selected.");
            gameObject.SetActive(false);
            echoManager.StartDelayedRecallTask();
        }

        public void SelectTask2()
        {
            gameObject.SetActive(false);
            echoManager.StartTask2();
        }
    }
}
