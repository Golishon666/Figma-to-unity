using UnityEngine;

namespace Figunity.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FigunityImportedNode : MonoBehaviour
    {
        [SerializeField] private string figmaId;
        [SerializeField] private string figmaName;
        [SerializeField] private string figmaType;
        [SerializeField] private string renderMode;
        [SerializeField] private string sourcePath;
        [SerializeField] private string componentKey;
        [SerializeField] private string repeatKey;
        [SerializeField] private string controlHint;
        [SerializeField] private bool mask;
        [SerializeField] private bool repeated;

        public string FigmaId
        {
            get => figmaId;
            set => figmaId = value;
        }

        public string FigmaName
        {
            get => figmaName;
            set => figmaName = value;
        }

        public string FigmaType
        {
            get => figmaType;
            set => figmaType = value;
        }

        public string RenderMode
        {
            get => renderMode;
            set => renderMode = value;
        }

        public string SourcePath
        {
            get => sourcePath;
            set => sourcePath = value;
        }

        public string ComponentKey
        {
            get => componentKey;
            set => componentKey = value;
        }

        public string RepeatKey
        {
            get => repeatKey;
            set => repeatKey = value;
        }

        public string ControlHint
        {
            get => controlHint;
            set => controlHint = value;
        }

        public bool IsMask
        {
            get => mask;
            set => mask = value;
        }

        public bool IsRepeated
        {
            get => repeated;
            set => repeated = value;
        }
    }
}
