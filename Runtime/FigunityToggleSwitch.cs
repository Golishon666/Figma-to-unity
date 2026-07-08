using UnityEngine;
using UnityEngine.UI;

namespace Figunity.Runtime
{
    [AddComponentMenu("UI/FIGUNITY/Toggle Switch")]
    [DisallowMultipleComponent]
    public sealed class FigunityToggleSwitch : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private RectTransform handle;
        [SerializeField] private Vector2 offPosition;
        [SerializeField] private Vector2 onPosition;

        public void Configure(Toggle sourceToggle, RectTransform handleRect, Vector2 off, Vector2 on)
        {
            toggle = sourceToggle;
            handle = handleRect;
            offPosition = off;
            onPosition = on;
            Apply(toggle == null || toggle.isOn);
        }

        private void OnEnable()
        {
            if (toggle == null)
            {
                toggle = GetComponent<Toggle>();
            }

            if (toggle != null)
            {
                toggle.onValueChanged.AddListener(Apply);
                Apply(toggle.isOn);
            }
        }

        private void OnDisable()
        {
            if (toggle != null)
            {
                toggle.onValueChanged.RemoveListener(Apply);
            }
        }

        private void OnValidate()
        {
            Apply(toggle == null || toggle.isOn);
        }

        private void Apply(bool isOn)
        {
            if (handle != null)
            {
                handle.anchoredPosition = isOn ? onPosition : offPosition;
            }
        }
    }
}
