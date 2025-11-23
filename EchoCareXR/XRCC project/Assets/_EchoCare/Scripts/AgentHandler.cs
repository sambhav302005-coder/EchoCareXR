using UnityEngine;

namespace ECHO
{
    public class CompanionHandler : MonoBehaviour
    {
        private const float genDist = 1.5f;

        [Header("Task Clips")]
        public AudioClip taskIntroduction;

        private AudioClip agentAudioClip;
        private AudioSource audioSource;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            Hide();
        }

        private void PlayVoice()
        {
            if (agentAudioClip == null)
                return;
            // player after a short delay

            audioSource.clip = agentAudioClip;
            audioSource.Play();
        }

        public void PositionInfrontOfUser()
        {
            // Position the companion 2 meters in front of the user
            var userTransform = Camera.main.transform;
            var forward = new Vector3(
                userTransform.forward.x,
                0,
                userTransform.forward.z
            ).normalized;
            var targetPosition = userTransform.position + forward * genDist; // 2 meters in front

            targetPosition.y = 0; //plane the companion on the ground level

            transform.position = targetPosition;
            transform.LookAt(
                new Vector3(
                    userTransform.position.x,
                    transform.position.y,
                    userTransform.position.z
                )
            );
        }

        internal void RevealCompanion()
        {
            //print("-- Revealing Companion...");
            gameObject.SetActive(true);
            PositionInfrontOfUser();
        }

        internal void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
