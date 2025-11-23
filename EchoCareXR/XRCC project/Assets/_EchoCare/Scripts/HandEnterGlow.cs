using Oculus.Interaction;
using UnityEngine;

namespace ECHO
{
    public class HandEnterGlow : MonoBehaviour
    {
        private ParticleSystem glowOnTouch;

        private void Awake()
        {
            glowOnTouch = GetComponent<ParticleSystem>();
            if (glowOnTouch != null)
            {
                glowOnTouch.gameObject.SetActive(false);
            }
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void OnTriggerEnter(Collider other)
        {
            //print("-- Trigger entered by: " + other.name);

            //Debug.Log("OVRHand has entered the trigger!");
            if (glowOnTouch == null)
                return;

            glowOnTouch.gameObject.SetActive(true);
        }

        private void OnTriggerExit(Collider other)
        {
            //print("-- Trigger entered by: " + other.name);

            //Debug.Log("OVRHand has entered the trigger!");
            if (glowOnTouch == null)
                return;

            glowOnTouch.gameObject.SetActive(false);
        }
    }
}
