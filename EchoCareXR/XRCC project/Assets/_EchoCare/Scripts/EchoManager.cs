using System;
using UnityEngine;
using UnityEngine.Playables;

namespace ECHO
{
    public class EchoManager : MonoBehaviour
    {
        // Singleton instance
        public static EchoManager Instance { get; private set; }

        [SerializeField]
        private string userName = "Maria";

        [SerializeField]
        private PlayableDirector playableDirector;

        [SerializeField]
        private OVRCameraRig cameraRig;

        [Header("Handlers")]
        public MenuHandler menuHandler;

        public CompanionHandler companionHandler;

        [Header("Tasks table")]
        public TestTableHandler testTableHandler;

        [Header("exercises")]
        public ExcerciseDelayedRecall excerciseDelayedRecall;

        public ExcercisePathFollowing excercisePathFollowing;

        private UserInfo userInfo = new UserInfo();

        private void Awake()
        {
            // Enforce singleton: allow only one instance and persist it across scenes
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"-- Duplicate {nameof(EchoManager)} on '{gameObject.name}' - destroying duplicate."
                );
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            // Clear the static reference when the singleton is destroyed
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            userInfo = new UserInfo(userName);

            // Initialize Menu
            if (menuHandler != null)
            {
                menuHandler.InitializeMenu();
            }

            testTableHandler.Hide();
            companionHandler.Hide();
        }

        /// <summary>
        /// Starts the delayed recall session, initializing any required components and beginning the associated exercise.
        /// </summary>
        public void StartDelayedRecallTask()
        {
            PlayTimeline();

            RevealCompanion();
            excerciseDelayedRecall.StartExercise();
        }

        internal void StartTask2()
        {
            Debug.Log("-- Starting Task 2 Session...");
            //RevealCompanion();
            companionHandler.Hide();
            excercisePathFollowing.StartExcercise();
        }

        public void RevealCompanion()
        {
            if (companionHandler == null)
                return;

            companionHandler.RevealCompanion();
        }

        internal OVRCameraRig GetCameraRig()
        {
            return cameraRig;
        }

        public void PlayTimeline(PlayableAsset timelineAsset = null)
        {
            if (playableDirector == null)
                return;

            if (timelineAsset)
                playableDirector.playableAsset = timelineAsset;

            playableDirector.Play();
        }
    }
}
