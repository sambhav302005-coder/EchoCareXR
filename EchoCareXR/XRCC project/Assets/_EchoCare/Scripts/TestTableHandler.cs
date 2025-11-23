using System;
using UnityEngine;

namespace ECHO
{
    public class TestTableHandler : MonoBehaviour
    {
        private const float tableDistance = 0.7f;

        [Header("Prefabs for Table Items")]
        // public GameObject applePrefab;
        // public GameObject glassesPrefab;
        // public GameObject keyPrefab;
        // public GameObject potPrefab;
        // public GameObject cupPrefab;
        public GameObject objectHolder;

        [Header("Path Follow")]
        public GameObject pathFollowPrefab;

        [Header("Table Transform")]
        public Transform tableTransform;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            InitializeTableItems();
        }

        private void InitializeTableItems() { }

        // position table in front of user
        public void PositionInFrontOfUser()
        {
            print("-- Positioning Table in Front of User...");

            var userTransform = Camera.main.transform;
            var forward = new Vector3(
                userTransform.forward.x,
                0,
                userTransform.forward.z
            ).normalized;
            var targetPosition = userTransform.position + (forward * tableDistance); // 1.5 meters in front

            targetPosition.y = userTransform.position.y - 0.5f; // Slightly lower than eye level

            gameObject.transform.position = targetPosition;
            //tableTransform.LookAt(new Vector3(userTransform.position.x, tableTransform.position.y, userTransform.position.z));
        }

        internal void ShowDelayedRecall()
        {
            objectHolder.SetActive(true);
            gameObject.SetActive(true);
            PositionInFrontOfUser();
        }

        internal void Hide()
        {
            gameObject.SetActive(false);
        }

        internal void ShowPathFollowing()
        {
            pathFollowPrefab.SetActive(true);
        }
    }
}
