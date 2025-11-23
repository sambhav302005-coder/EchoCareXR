using TMPro;
using UnityEngine;

namespace ECHO
{
    public class WelcomeMenu : MonoBehaviour
    {
        public TextMeshProUGUI helloText;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            UpdateText();
        }

        private void UpdateText()
        {
            if (helloText != null)
            {
                helloText.text = "Hello, " + PlayerPrefs.GetString("UserName", "Maria") + "!";
            }
        }
    }
}
