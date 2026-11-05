using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NumpadController : MonoBehaviour
{
    [Header("Input Fields")]
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TMP_InputField portInputField;

    private TMP_InputField _activeField;

    private void Start()
    {
        gameObject.SetActive(false);

        foreach (Button btn in GetComponentsInChildren<Button>())
        {
            string label = btn.GetComponentInChildren<TextMeshProUGUI>()?.text.Trim();
            if (string.IsNullOrEmpty(label)) continue;

            string captured = label;
            btn.onClick.AddListener(() => OnButtonPressed(captured));
        }

        if (ipInputField != null)
            ipInputField.onSelect.AddListener((_) => OpenNumpadFor(ipInputField));

        if (portInputField != null)
            portInputField.onSelect.AddListener((_) => OpenNumpadFor(portInputField));
    }

    private void OpenNumpadFor(TMP_InputField targetField)
    {
        _activeField = targetField;
        gameObject.SetActive(true);
    }

    private void OnButtonPressed(string value)
    {
        if (_activeField == null) return;

        switch (value)
        {
            case "<":
                if (_activeField.text.Length > 0)
                    _activeField.text = _activeField.text[..^1];
                break;

            case "enter":
                _activeField = null;
                gameObject.SetActive(false);
                break;

            default:
                _activeField.text += value;
                break;
        }
    }
}