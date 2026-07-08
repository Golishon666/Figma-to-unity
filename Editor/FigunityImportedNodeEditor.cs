using System;
using Figunity.Runtime;
using UnityEditor;
using UnityEngine;

namespace Figunity.Editor
{
    [CustomEditor(typeof(FigunityImportedNode))]
    [CanEditMultipleObjects]
    public sealed class FigunityImportedNodeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Update", GUILayout.Height(28f)))
                {
                    UpdateTargets();
                }
            }
        }

        private void UpdateTargets()
        {
            var updated = 0;
            for (var i = 0; i < targets.Length; i++)
            {
                var imported = targets[i] as FigunityImportedNode;
                if (imported == null)
                {
                    continue;
                }

                try
                {
                    FigunityElementUpdater.UpdateElementFromPayload(imported.gameObject);
                    updated++;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("FIGUNITY", exception.Message, "OK");
                    break;
                }
            }

            if (updated > 0)
            {
                Debug.Log("FIGUNITY updated " + updated + " selected imported element(s).");
            }
        }
    }
}
