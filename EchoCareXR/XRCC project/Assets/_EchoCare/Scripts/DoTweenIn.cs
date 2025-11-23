using DG.Tweening;
using UnityEngine;

namespace ECHO
{
    public class DoTweenIn : MonoBehaviour
    {
        private bool hasTweenedIn = false;

        private void OnEnable()
        {
            if (hasTweenedIn)
                return;

            transform.DOScale(Vector3.one, 0.5f).From(Vector3.zero).SetEase(Ease.OutBack);
        }

        private void OnTriggerEnter(Collider other)
        {
            //print("-- DoTweenIn Triggered by: " + other.name);
            hasTweenedIn = true;
            transform.localScale = Vector3.one;
        }

        private void OnTriggerExit(Collider other)
        {
            transform.localScale = Vector3.one;
        }
    }
}
