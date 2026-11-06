using UnityEditor;
using UnityEngine;

namespace Chuangyou.Narrative.Editor
{
    [CustomEditor(typeof(NarrativeLogTerminal))]
    public sealed class NarrativeLogTerminalEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty logAsset = serializedObject.FindProperty("logAsset");
            EditorGUILayout.PropertyField(logAsset, new GUIContent("日志资产", logAsset.tooltip));

            if (logAsset.objectReferenceValue != null)
            {
                EditorGUILayout.HelpBox(
                    "当前终端使用上面的日志资产。请选中该资产，在它的 Inspector 里编辑标题和条目。",
                    MessageType.Info);

                if (GUILayout.Button("选中日志资产"))
                {
                    Selection.activeObject = logAsset.objectReferenceValue;
                    EditorGUIUtility.PingObject(logAsset.objectReferenceValue);
                }
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("terminalTitle"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("subtitle"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("entries"), true);
            }

            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "logAsset",
                "terminalTitle",
                "subtitle",
                "entries");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
