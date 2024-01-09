using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(AnimatorController))]
    public class AnimatorControllerEditor : UnityEditor.Editor
    {
        private AnimatorController _animatorController;
        private readonly Color _layerBackgroundColor = new Color(0.1f, 0.1f, 0.1f);
        private readonly Color _subStateBackgroundColor = new Color(0.15f, 0.15f, 0.15f);
    
        public override void OnInspectorGUI()
        {
            _animatorController = (AnimatorController)target;
            GUILayout.Space(10);

            DrawStatesTable();

            GUILayout.Space(10);
            DrawDragAndDropArea();
        }

        private void DrawStatesTable()
        {
            for (int i = 0; i < _animatorController.layers.Length; i++)
            {
                var layer = _animatorController.layers[i];
                Rect layerRect = EditorGUILayout.BeginVertical();
                EditorGUI.DrawRect(layerRect, i % 2 == 0 ? _layerBackgroundColor : _subStateBackgroundColor);
                GUILayout.Label("Layer: " + layer.name, EditorStyles.boldLabel);
                GUILayout.Space(10);
                DrawStateMachine(layer.stateMachine, 0);
                GUILayout.Space(10);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawStateMachine(AnimatorStateMachine stateMachine, int depth)
        {
            DrawElements(stateMachine.states.Select(s => s.state).ToArray(), stateMachine.stateMachines.Select(sm => sm.stateMachine).ToArray(), depth);
        }

        private void DrawElements(AnimatorState[] states, AnimatorStateMachine[] stateMachines, int depth)
        {
            string indent = new string(' ', depth * 4);
            foreach (var stateMachine in stateMachines)
            {
                DrawElementBackground(indent + "Sub-State: " + stateMachine.name, _subStateBackgroundColor);
                DrawStateMachine(stateMachine, depth + 1);
            }
            foreach (var state in states)
            {
                if (state.motion is BlendTree blendTree)
                {
                    DrawElementBackground(indent + $"BlendTree State: {state.name}", _layerBackgroundColor);
                    DrawBlendTree(blendTree, depth + 1);
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    DrawElementBackground(indent + "State: " + state.name, _layerBackgroundColor);
                    EditorGUI.BeginChangeCheck();
                    var newMotion = (Motion)EditorGUILayout.ObjectField(
                        state.motion,
                        typeof(Motion),
                        false,
                        GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth / 2.2f)
                    );
                    if (EditorGUI.EndChangeCheck())
                    {
                        state.motion = newMotion;
                        EditorUtility.SetDirty(state);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void DrawElementBackground(string label, Color backgroundColor)
        {
            var content = new GUIContent(label);
            var height = EditorStyles.label.CalcHeight(content, EditorGUIUtility.currentViewWidth);
            Rect rect = GUILayoutUtility.GetRect(content, EditorStyles.label, GUILayout.Height(height));
            EditorGUI.DrawRect(rect, backgroundColor);
            EditorGUI.LabelField(rect, content, EditorStyles.boldLabel);
        }

        private void DrawBlendTree(BlendTree blendTree, int depth)
        {
            GUILayout.BeginVertical();
    
            var children = blendTree.children;
            var newChildren = new ChildMotion[children.Length];
    
            for (int i = 0; i < children.Length; ++i)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUIUtility.currentViewWidth / 2.2f);
                Motion childMotion = children[i].motion;
                Motion newMotion = (Motion)EditorGUILayout.ObjectField(
                    childMotion,
                    typeof(Motion),
                    false,
                    GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth / 2.2f)
                );
                if (newMotion != childMotion)
                {
                    newChildren[i] = new ChildMotion
                    {
                        motion = newMotion,
                        threshold = children[i].threshold,
                        timeScale = children[i].timeScale,
                        cycleOffset = children[i].cycleOffset,
                        directBlendParameter = children[i].directBlendParameter,
                        mirror = children[i].mirror
                    };
                }
                else
                {
                    newChildren[i] = children[i];
                }
                GUILayout.EndHorizontal();
            }

            blendTree.children = newChildren;
    
            GUILayout.EndVertical();
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
                    foreach (var draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject is AnimationClip clip)
                        {
                            ReplaceAnimationClipsWithNameMatch(clip);
                        }
                    }
                }
            }
        }

        private void ReplaceAnimationClipsWithNameMatch(AnimationClip newClip)
        {
            foreach (var layer in _animatorController.layers)
            {
                ReplaceInStateMachineWithNameMatch(layer.stateMachine, newClip);
            }
        }

        private void ReplaceInStateMachineWithNameMatch(AnimatorStateMachine stateMachine, AnimationClip newClip)
        {
            foreach (var state in stateMachine.states)
            {
                if (state.state.motion is AnimationClip currentClip && currentClip.name == newClip.name)
                {
                    string oldClipPath = AssetDatabase.GetAssetPath(currentClip);
                    string newClipPath = AssetDatabase.GetAssetPath(newClip);

                    string[] oldPathComponents = oldClipPath.Split('/');
                    string[] newPathComponents = newClipPath.Split('/');

                    if (oldPathComponents.Length >= 3 && newPathComponents.Length >= 3)
                    {
                        string oldFolder = "/" + oldPathComponents[^3] + "/" + oldPathComponents[^2];
                        string newFolder = "/" + newPathComponents[^3] + "/" + newPathComponents[^2];
                        Debug.Log($"Replacing clip '{oldFolder}/{currentClip.name}' with '{newFolder}/{newClip.name}'");
                    }
                    
                    state.state.motion = newClip;
                    EditorUtility.SetDirty(state.state);
                }
                else if (state.state.motion is BlendTree tree)
                {
                    ReplaceInBlendTreeWithNameMatch(tree, newClip);
                }
            }

            foreach (var subStateMachine in stateMachine.stateMachines)
            {
                ReplaceInStateMachineWithNameMatch(subStateMachine.stateMachine, newClip);
            }
        }

        private void ReplaceInBlendTreeWithNameMatch(BlendTree blendTree, AnimationClip newClip)
        {
            var children = blendTree.children;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].motion is AnimationClip childClip && childClip.name == newClip.name)
                {
                    string oldClipPath = AssetDatabase.GetAssetPath(childClip);
                    string newClipPath = AssetDatabase.GetAssetPath(newClip);

                    string[] oldPathComponents = oldClipPath.Split('/');
                    string[] newPathComponents = newClipPath.Split('/');

                    if (oldPathComponents.Length >= 3 && newPathComponents.Length >= 3)
                    {
                        string oldFolder = "/" + oldPathComponents[^3] + "/" + oldPathComponents[^2];
                        string newFolder = "/" + newPathComponents[^3] + "/" + newPathComponents[^2];
                        Debug.Log($"Replacing clip '{oldFolder}/{childClip.name}' with '{newFolder}/{newClip.name}'");
                    }

                    children[i].motion = newClip;
                }
                else if (children[i].motion is BlendTree subTree)
                {
                    ReplaceInBlendTreeWithNameMatch(subTree, newClip);
                }
            }
            blendTree.children = children;
            EditorUtility.SetDirty(blendTree);
        }
    }
}
