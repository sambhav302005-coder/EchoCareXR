using DG.Tweening;
using System;
using UnityEngine;

namespace ECHO
{
	public class MenuHandler : MonoBehaviour
	{
		public GameObject welcomeMenu;
		public GameObject dateMenu;
		public GameObject doNextMenu;

		[Header("Audio Clips")]
		public AudioClip welcomeAudioClip;

		public AudioClip morningClip;

		[Header("Menu Settings")]
		public float welcomeMenuDuration = 5f;

		private AudioSource audioSource;

		private OVRCameraRig cameraRig;
		private EchoManager echoManager;

		private void Awake()
		{
			audioSource = gameObject.GetComponent<AudioSource>();
			echoManager = EchoManager.Instance;
			cameraRig = echoManager.GetCameraRig();
		}

		private void Play(AudioClip clip)
		{
			if (clip != null && audioSource != null)
			{
				audioSource.clip = clip;
				audioSource.Play();
			}
		}

		internal void InitializeMenu()
		{
			// Initialize menu items here
			if (welcomeMenu) welcomeMenu.SetActive(false);

			if (dateMenu) dateMenu.SetActive(false);
			if (doNextMenu) doNextMenu.SetActive(false);

			// hide welcome menu after 5 seconds and show date menu
			Invoke(nameof(ShowWelcomeMenu), welcomeMenuDuration);
		}

		public void PostitionMenuInFront()
		{
			//position menu in front of user
			var pos = cameraRig.centerEyeAnchor.position;

			const float dist = 0.5f;
			var userTransform = Camera.main.transform;
			var forward = new Vector3(userTransform.forward.x, 0, userTransform.forward.z).normalized;
			var targetPosition = userTransform.position + forward * dist; // 2 meters in front

			targetPosition.y = userTransform.position.y;

			transform.position = targetPosition;
			//transform.LookAt(new Vector3(userTransform.position.x, transform.position.y, userTransform.position.z));
		}

		private void ShowWelcomeMenu()
		{
			PostitionMenuInFront();
			welcomeMenu.SetActive(true);
			welcomeMenu.transform.DOScale(Vector3.one, 0.5f).From(Vector3.zero).SetEase(Ease.OutBack);

			Play(welcomeAudioClip);

			Invoke(nameof(ShowDateMenu), welcomeMenuDuration);
		}

		private void ShowDateMenu()
		{
			Play(morningClip);
			welcomeMenu.SetActive(false);
			dateMenu.SetActive(true);
			dateMenu.transform.DOScale(Vector3.one, 0.5f).From(Vector3.zero).SetEase(Ease.OutBack);
		}
	}
}