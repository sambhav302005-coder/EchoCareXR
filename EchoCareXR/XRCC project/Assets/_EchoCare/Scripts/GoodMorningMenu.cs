using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace ECHO
{
    public class GoodMorningMenu : MonoBehaviour
    {
        public TextMeshProUGUI welcomeText;
        public TextMeshProUGUI dateText;

        // define action for public trigger
        [Header("Menu Actions")]
        public UnityEvent menuAction;

        private MenuHandler menuHandler;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            UpdateText();
        }

        private void UpdateText()
        {
            //update date text
            if (dateText == null)
                return;

            dateText.text = $"Today is {DateTime.Now:MMMM dd}";
        }

        public void OnStartDayButtonPressed()
        {
            // Logic to start the day
            menuAction?.Invoke();
        }
    }
}
