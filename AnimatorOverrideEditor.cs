using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(AnimatorOverrideController))]
    public class AnimatorOverrideEditor : UnityEditor.Editor
    {
        private AnimatorOverrideController _overrideController;
        private SerializedProperty _runtimeAnimatorControllerProperty;

        private const float HALF_WIDTH_MODIFIER = 2.2f;

        private void OnEnable()
        {
            _overrideController = (AnimatorOverrideController)target;
            _runtimeAnimatorControllerProperty = serializedObject.FindProperty("m_Controller");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawAnimatorControllerField();
            DrawOverridesTable();

            GUILayout.Space(10);
            DrawDragAndDropArea();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAnimatorControllerField()
        {
            EditorGUILayout.ObjectField(_runtimeAnimatorControllerProperty, new GUIContent("Runtime Animator Controller"));
            GUILayout.Space(20);
        }

        private void DrawOverridesTable()
        {
            var overridesList = GetSortedOverridesList();
            DrawTableHeaders();
            DrawTableRows(overridesList);
        }

        private List<KeyValuePair<AnimationClip, AnimationClip>> GetSortedOverridesList()
        {
            var overridesList = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            _overrideController.GetOverrides(overridesList);
            overridesList.Sort((x, y) => string.Compare(x.Key.name, y.Key.name, StringComparison.Ordinal));
            return overridesList;
        }

        private void DrawTableHeaders()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Original Clip", EditorStyles.boldLabel, GUILayout.Width(EditorGUIUtility.currentViewWidth / HALF_WIDTH_MODIFIER));
            GUILayout.Label("Override Clip", EditorStyles.boldLabel, GUILayout.Width(EditorGUIUtility.currentViewWidth / HALF_WIDTH_MODIFIER));
            GUILayout.EndHorizontal();
        }

        private void DrawTableRows(IEnumerable<KeyValuePair<AnimationClip, AnimationClip>> overridesList)
        {
            foreach (var pair in overridesList)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(pair.Key.name, GUILayout.Width(EditorGUIUtility.currentViewWidth / HALF_WIDTH_MODIFIER)); 
                var newOverrideClip = (AnimationClip)EditorGUILayout.ObjectField(
                    pair.Value, 
                    typeof(AnimationClip), 
                    false, 
                    GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth / HALF_WIDTH_MODIFIER)
                );
                if (newOverrideClip != pair.Value)
                {
                    _overrideController[pair.Key.name] = newOverrideClip;
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawDragAndDropArea()
        {
            GUILayout.Label("Drag and Drop Animation Clips here");
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drop Clips Here");
            HandleDragAndDrop(dropArea);
        }

        private void HandleDragAndDrop(Rect dropArea)
        {
            var currentEvent = Event.current;
            if (!dropArea.Contains(currentEvent.mousePosition)) return;

            if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (currentEvent.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    var overridesList = GetSortedOverridesList();
                    foreach (var draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject is AnimationClip clip)
                        {
                            AddClipToOverrideController(clip, overridesList);
                        }
                    }
                }
            }
        }

        private void AddClipToOverrideController(AnimationClip clip, List<KeyValuePair<AnimationClip, AnimationClip>> overridesList)
        {
            bool hasChanged = false;
            for (int i = 0; i < overridesList.Count; i++)
            {
                if (overridesList[i].Key.name == clip.name)
                {
                    if (overridesList[i].Value != clip)
                    {
                        overridesList[i] = new KeyValuePair<AnimationClip, AnimationClip>(overridesList[i].Key, clip);
                        hasChanged = true;
                    }
                }
            }

            if (hasChanged)
            {
                _overrideController.ApplyOverrides(overridesList);
            }
        }
    }
}
