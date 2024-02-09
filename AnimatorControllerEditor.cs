/*
 * AnimatorControllerEditor
 * Date: 09-02-2024
 */

using System.Collections.Generic;
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
        private GameObject _rigGameObject;
        
        private bool _replaceClip;
        private bool _copyEvents;
        private bool _transformRenderer;
        
        private readonly Color _layerBackgroundColor = new Color(0.1f, 0.1f, 0.1f);
        private readonly Color _subStateBackgroundColor = new Color(0.2f, 0.2f, 0.2f);
        
        private readonly Color _baseLayerBackgroundColor = new Color(0.5f, 0.5f, 0.5f);
        private readonly Color _baseSubStateBackgroundColor = new Color(0.7f, 0.7f, 0.7f);
        private readonly List<string> _propertyPrefixes = new List<string> { "Fx", "Geometry" };

        private Color BackgroundColor => EditorGUIUtility.isProSkin ? _layerBackgroundColor : _baseLayerBackgroundColor;
        private Color SubStateBackgroundColor => EditorGUIUtility.isProSkin ? _subStateBackgroundColor : _baseSubStateBackgroundColor;
        private float ViewWidth => EditorGUIUtility.currentViewWidth / 2.2f;
        public static Rect DropArea => GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
    
        public override void OnInspectorGUI()
        {
            _animatorController = (AnimatorController)target;
            GUILayout.Space(10);

            DrawStatesTable();

            GUILayout.Space(10);

            GUILayout.Label("Replace Clips/ Tranfer Events & Properties / Change Renderer Keys", EditorStyles.boldLabel);

            GUILayout.Space(10);

            _replaceClip = EditorGUILayout.Toggle("Replace Animation Clips", _replaceClip);
            
            _copyEvents = EditorGUILayout.Toggle("Copy Animation Events", _copyEvents);
            if (_copyEvents)
            {
                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Custom Property Prefixes");
                if (GUILayout.Button("+", GUILayout.Width(20)))
                {
                    _propertyPrefixes.Add("");
                }
                GUILayout.EndHorizontal();
            
                for (int i = 0; i < _propertyPrefixes.Count; i++)
                {
                    GUILayout.BeginHorizontal();
                    _propertyPrefixes[i] = EditorGUILayout.TextField(_propertyPrefixes[i]);
                    if (GUILayout.Button("-", GUILayout.Width(20)))
                    {
                        _propertyPrefixes.RemoveAt(i);
                        GUILayout.EndHorizontal();
                        break;
                    }
                    GUILayout.EndHorizontal();
                }
                
                GUILayout.Space(10);
            }

            _transformRenderer = EditorGUILayout.Toggle("Renderer Keys to IsActive", _transformRenderer);

            if (_transformRenderer)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Rig GameObject", GUILayout.Width(EditorGUIUtility.labelWidth));
                _rigGameObject = (GameObject)EditorGUILayout.ObjectField(_rigGameObject, typeof(GameObject), true);
                EditorGUILayout.EndHorizontal();
            }


            GUILayout.Space(10);

            DrawDragAndDropArea();

            GUILayout.Space(10);

            if (GUILayout.Button("Rename States To\nMatch Clip Names", GUILayout.Width(120), GUILayout.Height(50)))
            {
                RenameStatesToMatchClipNames();
            }
        }
        
        /// <summary>
        /// Drag & Drop
        /// </summary>
        
        private void HandleDragAndDrop(Rect dropArea)
        {
            var currentEvent = Event.current;
            if (!dropArea.Contains(currentEvent.mousePosition)) return;
            if (currentEvent.type != EventType.DragUpdated && currentEvent.type != EventType.DragPerform) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var draggedObject in DragAndDrop.objectReferences)
                {
                    if (draggedObject is AnimationClip clip)
                    {
                        ProcessDraggedClip(clip);
                    }
                }
            }
        }
        
        private void ProcessDraggedClip(AnimationClip newClip)
        {
            if (_transformRenderer)
            {
                if (!_rigGameObject)
                {
                    Debug.LogError("Rig hasn't been attached!");

                    return;
                }
                ReplaceRendererWithGameObjectActive(newClip);
            }
            
            if (_copyEvents)
            {
                TransferEventsFromMatchingClips(newClip);
            }
            
            if (_replaceClip)
            {
                ReplaceAnimationClipsWithNameMatch(newClip); 
            }
        }

        /// <summary>
        /// Change Clips in Animator
        /// </summary>

        private bool ReplaceClipInStateMachine(AnimatorStateMachine stateMachine, AnimationClip newClip)
        {
            bool clipReplaced = false;
            for (int i = 0; i < stateMachine.states.Length; i++)
            {
                var state = stateMachine.states[i].state;
                if (state.motion is AnimationClip clip && clip.name == newClip.name)
                {
                    Undo.RecordObject(state, $"Replace Animation Clip {clip.name}");
                    state.motion = newClip;
                    LogReplacement(clip, newClip);
                    clipReplaced = true;
                }
                else if (state.motion is BlendTree tree)
                {
                    clipReplaced = ReplaceClipInBlendTree(tree, newClip, clipReplaced); 
                }
            }

            foreach (var childStateMachine in stateMachine.stateMachines)
            {
                clipReplaced = ReplaceClipInStateMachine(childStateMachine.stateMachine, newClip) || clipReplaced;
            }

            return clipReplaced;
        }

        private bool ReplaceClipInBlendTree(BlendTree blendTree, AnimationClip newClip, bool logReplacement)
        {
            bool clipReplaced = false;
            var children = blendTree.children;
            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child.motion is AnimationClip clip && clip.name == newClip.name)
                {
                    children[i].motion = newClip;
                    clipReplaced = true;
                    if (logReplacement) LogReplacement(clip, newClip);
                }
                else if (child.motion is BlendTree subTree)
                {
                    clipReplaced = ReplaceClipInBlendTree(subTree, newClip, clipReplaced) || clipReplaced;
                }
            }

            if (clipReplaced)
            {
                Undo.RecordObject(blendTree, "Replace Animation Clip in BlendTree");
                blendTree.children = children;
            }

            return clipReplaced;
        }

        private void ReplaceAnimationClipsWithNameMatch(AnimationClip newClip)
        {
            if (_animatorController == null) return;

            AnimationClip foundClip = GetClip(newClip);
            if (foundClip == newClip)
            {
                Debug.Log("Attempted to replace a clip with itself. Operation cancelled.");
                return;
            }

            bool clipReplaced = false;
            for (int i = 0; i < _animatorController.layers.Length; i++)
            {
                AnimatorControllerLayer layer = _animatorController.layers[i];
                clipReplaced = ReplaceClipInStateMachine(layer.stateMachine, newClip) || clipReplaced;
            }

            if (clipReplaced)
            {
                EditorUtility.SetDirty(_animatorController);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }


        /// <summary>
        /// Find Clip with Name Match
        /// </summary>
        
        private AnimationClip GetClip(AnimationClip newClip)
        {
            foreach (var layer in _animatorController.layers)
            {
                var clip = FindClipWithNameMatchInLayer(layer, newClip.name);
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        private AnimationClip FindClipWithNameMatchInLayer(AnimatorControllerLayer layer, string clipName)
        {
            return FindClipWithNameMatchInStateMachine(layer.stateMachine, clipName);
        }

        private AnimationClip FindClipWithNameMatchInStateMachine(AnimatorStateMachine stateMachine, string clipName)
        {
            foreach (var state in stateMachine.states)
            {
                if (state.state.motion is AnimationClip clip && clip.name == clipName)
                {
                    return clip;
                }
                else if (state.state.motion is BlendTree tree)
                {
                    var foundClip = FindClipWithNameMatchInBlendTree(tree, clipName);
                    if (foundClip != null)
                    {
                        return foundClip;
                    }
                }
            }

            foreach (var subStateMachine in stateMachine.stateMachines)
            {
                var foundClip = FindClipWithNameMatchInStateMachine(subStateMachine.stateMachine, clipName);
                if (foundClip != null)
                {
                    return foundClip;
                }
            }

            return null;
        }

        private AnimationClip FindClipWithNameMatchInBlendTree(BlendTree blendTree, string clipName)
        {
            foreach (var child in blendTree.children)
            {
                if (child.motion is AnimationClip clip && clip.name == clipName)
                {
                    return clip;
                }
                else if (child.motion is BlendTree subTree)
                {
                    var foundClip = FindClipWithNameMatchInBlendTree(subTree, clipName);
                    if (foundClip != null)
                    {
                        return foundClip;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Transfer Events
        /// </summary>
        
        private void TransferEventsFromMatchingClips(AnimationClip newClip)
        {
            AnimationClip foundClip = GetClip(newClip);
            if (foundClip == newClip)
            {
                Debug.Log("Attempted to transfer events and properties between identical clips. Operation cancelled.");
                return;
            }

            if (foundClip != null)
            {
                TransferCustomPropertiesAndEvents(foundClip, newClip, _propertyPrefixes);
            }
        }

        
        private void TransferCustomPropertiesAndEvents(AnimationClip oldClip, AnimationClip newClip, List<string> propertyPrefixes)
        {
            float durationRatio = newClip.length / oldClip.length;

            AnimationEvent[] eventsFrom = AnimationUtility.GetAnimationEvents(oldClip);
            for (int i = 0; i < eventsFrom.Length; i++)
            {
                eventsFrom[i].time *= durationRatio;
                LogEventTransfer(eventsFrom[i], oldClip, newClip);
            }
            AnimationUtility.SetAnimationEvents(newClip, eventsFrom);

            var oldClipBindings = AnimationUtility.GetCurveBindings(oldClip);
            var newClipBindings = AnimationUtility.GetCurveBindings(newClip).ToDictionary(GetPropertyPath, b => b);

            foreach (string propertyPrefix in propertyPrefixes)
            {
                foreach (var binding in oldClipBindings)
                {
                    if (binding.path.StartsWith(propertyPrefix))
                    {
                        string propertyPath = GetPropertyPath(binding);
                        if (newClipBindings.ContainsKey(propertyPath)) continue;

                        AnimationCurve oldCurve = AnimationUtility.GetEditorCurve(oldClip, binding);
                        if (oldCurve == null) continue;

                        AnimationCurve newCurve = new AnimationCurve();
                        foreach (Keyframe oldKey in oldCurve.keys)
                        {
                            Keyframe newKey = new Keyframe(oldKey.time * durationRatio, oldKey.value,
                                ResetInfinity(oldKey.inTangent), ResetInfinity(oldKey.outTangent));
                            newCurve.AddKey(newKey);
                        }
                        
                        AnimationUtility.SetEditorCurve(newClip, binding, newCurve);
                    }
                }
            }
        }

        private static string GetPropertyPath(EditorCurveBinding binding)
        {
            return $"{binding.path}/{binding.propertyName}";
        }

        private static float ResetInfinity(float value)
        {
            return float.IsInfinity(value) ? 0 : value;
        }
        
        /// <summary>
        /// Rename Block
        /// </summary>
        private void RenameStatesToMatchClipNames()
        {
            if (_animatorController == null)
            {
                Debug.LogError("Animator Controller is null.");
                return;
            }

            Undo.RecordObject(_animatorController, "Rename Animator States");

            foreach (var layer in _animatorController.layers)
            {
                RenameStatesInStateMachine(layer.stateMachine, new HashSet<string>());
            }
    
            AssetDatabase.SaveAssets();
        }

        private void RenameStatesInStateMachine(AnimatorStateMachine stateMachine, HashSet<string> existingStateNames)
        {
            foreach (var childState in stateMachine.states)
            {
                if (childState.state.motion is not AnimationClip clip) continue;
                if (existingStateNames.Contains(clip.name))
                {
                    Debug.Log($"Skipping renaming of state {childState.state.name} as the name {clip.name} already exists.");
                    continue;
                }

                Undo.RecordObject(childState.state, $"Rename State {childState.state.name}");
                Debug.Log($"Renaming state {childState.state.name} to match clip name {clip.name}");
                childState.state.name = clip.name;
                EditorUtility.SetDirty(childState.state);
                existingStateNames.Add(clip.name);
            }

            foreach (var childStateMachine in stateMachine.stateMachines)
            {
                RenameStatesInStateMachine(childStateMachine.stateMachine, existingStateNames);
            }
        }
        
        /// <summary>
        /// Replace Renderer by IsActive Keys
        /// </summary>
        
        private void ReplaceRendererWithGameObjectActive(AnimationClip clip)
        {
            var allBindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in allBindings)
            {
                if (binding.type == typeof(Renderer) && binding.propertyName == "m_Enabled")
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    EditorCurveBinding gameObjectBinding = new EditorCurveBinding
                    {
                        type = typeof(GameObject),
                        path = binding.path,
                        propertyName = "m_IsActive"
                    };

                    AnimationUtility.SetEditorCurve(clip, gameObjectBinding, curve);
                    AnimationUtility.SetEditorCurve(clip, binding, null);

                    LogRendererChange(binding.path);
                }
            }
        }
        
        /// <summary>
        /// Log
        /// </summary>
        
        private void LogReplacement(AnimationClip oldClip, AnimationClip newClip)
        {
            string oldClipPath = AssetDatabase.GetAssetPath(oldClip);
            string newClipPath = AssetDatabase.GetAssetPath(newClip);

            string[] oldPathComponents = oldClipPath.Split('/');
            string[] newPathComponents = newClipPath.Split('/');

            if (oldPathComponents.Length >= 3 && newPathComponents.Length >= 3)
            {
                string oldFolder = $"/{oldPathComponents[^3]}/{oldPathComponents[^2]}";
                string newFolder = $"/{newPathComponents[^3]}/{newPathComponents[^2]}";
                Debug.Log($"Replacing clip '{oldFolder}/{oldClip.name}' with '{newFolder}/{newClip.name}'");
            }
        }

        private void LogEventTransfer(AnimationEvent animEvent, AnimationClip oldClip, AnimationClip newClip)
        {
            Debug.Log($"Event '{animEvent.functionName}' transferred from '{oldClip.name}' to '{newClip.name}' at {animEvent.time} seconds.");
        }
        
        private void LogRendererChange(string path)
        {
            Debug.Log($"Renderer with path '{path}' changed to GameObject IsActive property.");
        }
        
        /// <summary>
        /// Additional UI
        /// </summary>
        
        private void DrawStatesTable()
        {
            for (int i = 0; i < _animatorController.layers.Length; i++)
            {
                var layer = _animatorController.layers[i];
                Rect layerRect = EditorGUILayout.BeginVertical();
                EditorGUI.DrawRect(layerRect, i % 2 == 0 ? BackgroundColor : SubStateBackgroundColor);
                GUILayout.Label($"Layer: {layer.name}", EditorStyles.boldLabel);
                GUILayout.Space(10);
                DrawStateMachine(layer.stateMachine, 0);
                GUILayout.Space(10);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawStateMachine(AnimatorStateMachine stateMachine, int depth)
        {
            DrawElements(stateMachine.states.Select(s => s.state), 
                stateMachine.stateMachines.Select(sm => sm.stateMachine), depth);
        }

        private void DrawElements(IEnumerable<AnimatorState> states, IEnumerable<AnimatorStateMachine> stateMachines, int depth)
        {
            string indent = new string(' ', depth * 4);
            foreach (var stateMachine in stateMachines)
            {
                DrawElementBackground($"{indent}Sub-State: {stateMachine.name}", SubStateBackgroundColor);
                DrawStateMachine(stateMachine, depth + 1);
            }
            foreach (var state in states)
            {
                if (state.motion is BlendTree blendTree)
                {
                    DrawElementBackground($"{indent}BlendTree State: {state.name}", BackgroundColor);
                    DrawBlendTree(blendTree);
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    DrawElementBackground($"{indent}State: {state.name}", BackgroundColor);
                    EditorGUI.BeginChangeCheck();
                    var newMotion = (Motion)EditorGUILayout.ObjectField(
                        state.motion,
                        typeof(Motion),
                        false,
                        GUILayout.MaxWidth(ViewWidth)
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

        private void DrawBlendTree(BlendTree blendTree)
        {
            GUILayout.BeginVertical();
    
            var children = blendTree.children;
            var newChildren = new ChildMotion[children.Length];
    
            for (int i = 0; i < children.Length; ++i)
            {
                var child = children[i];
                GUILayout.BeginHorizontal();
                GUILayout.Space(ViewWidth);
                Motion childMotion = child.motion;
                Motion newMotion = (Motion)EditorGUILayout.ObjectField(
                    childMotion,
                    typeof(Motion),
                    false,
                    GUILayout.MaxWidth(ViewWidth)
                );
                if (newMotion != childMotion)
                {
                    newChildren[i] = new ChildMotion
                    {
                        motion = newMotion,
                        threshold = child.threshold,
                        timeScale = child.timeScale,
                        cycleOffset = child.cycleOffset,
                        directBlendParameter = child.directBlendParameter,
                        mirror = child.mirror
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
            GUILayout.Label("Drag and Drop Clips");
            Rect dropArea = DropArea;
            GUI.Box(dropArea, "Drop Clips Here");
            HandleDragAndDrop(dropArea);
        }
        
    }
}
