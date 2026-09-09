#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UI;

namespace FoodIsekaiZ.Display.Editor
{
    [CustomEditor(typeof(BankPurseImage)), CanEditMultipleObjects]
    public sealed class BankPurseImageEditor : ImageEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            serializedObject.Update();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bank Balance Stages", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("moneyStages"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("stageThresholds"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pulseDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pulseScale"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shakeDegrees"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
