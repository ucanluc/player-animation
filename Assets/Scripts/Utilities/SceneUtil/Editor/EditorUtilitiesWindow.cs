using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Redactor.Scripts.Utilities.SceneUtil.Editor
{
    public class EditorUtilitiesWindow : EditorWindow
    {
        // public static float offsetDistance = 0.5f;

        public bool drawNames = false;
        public bool handlesAreClickable = false;
        public int recurseDepth = 1;
        public float handleSize = 0.5f;
        public float handleColorTransparency = 0.5f;
        public Transform[] namingRoots = Array.Empty<Transform>();

        public void OnEnable()
        {
            titleContent = new GUIContent("RA Editor Utilities");
        }

        public void OnDestroy()
        {
            drawNames = false;
        }

        public void OnDisable()
        {
            drawNames = false;
        }

        private void OnGUI()
        {
            if (namingRoots == null) namingRoots = Array.Empty<Transform>();
            if (GUILayout.Button("Add selected objects")) AddSelectedAsRoot();
            if (GUILayout.Button("Remove selected objects")) RemoveSelectedAsRoot();


            // Automatic display of serializables
            var obj = new SerializedObject(this);

            EditorGUILayout.PropertyField(obj.FindProperty("namingRoots"));


            if (namingRoots.Length <= 0)
            {
                EditorGUILayout.HelpBox("Root transform must be selected. Please assign a root transform.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.BeginVertical("box");
                DrawButtons();
                EditorGUILayout.EndVertical();
            }

            obj.ApplyModifiedProperties();
        }

        private void SelectMissingMatObjects()
        {
            var missingMatObjects = new List<GameObject>();
            foreach (var root in namingRoots)
            {
                var children = root.GetComponentsInChildren<Transform>();
                foreach (var child in children)
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    if (renderer.sharedMaterial == null) missingMatObjects.Add(child.gameObject);
                }
            }

            Selection.objects = missingMatObjects.ToArray();
        }


        private void AddSelectedAsRoot()
        {
            // add selected objects to the namingRoots array
            var current = new List<GameObject>(namingRoots.Select(x => x.gameObject));
            foreach (var gameObject in Selection.gameObjects)
                // add undo support
                if (!current.Contains(gameObject))
                    current.Add(gameObject);

            Undo.RecordObject(this, "Add selected objects to naming roots");
            namingRoots = current.Select(x => x.transform).ToArray();
        }

        private void RemoveSelectedAsRoot()
        {
            // remove selected objects from the namingRoots array
            var selected = Selection.gameObjects;
            var newRoots = new List<GameObject>(namingRoots.Select(x => x.gameObject));
            foreach (var obj in selected)
            {
                var t = obj.transform;
                if (newRoots.Contains(t.gameObject)) newRoots.Remove(t.gameObject);
            }

            // add undo support
            Undo.RecordObject(this, "Remove selected objects from naming roots");
            namingRoots = newRoots.Select(x => x.transform).ToArray();
        }

        [MenuItem("Tools/Redactor/UtilitesWindow")]
        public static void Open()
        {
            GetWindow<EditorUtilitiesWindow>();
            var window = (EditorUtilitiesWindow)GetWindow(typeof(EditorUtilitiesWindow));
            SceneView.duringSceneGui += window.OnSceneGUI;
        }

        public void Reset()
        {
            Open();
        }

        public void OnSceneGUI(SceneView view)
        {
            // create a label for all children of the root transforms, recursively
            if (drawNames)
                foreach (var root in namingRoots)
                    DrawLabels(root, recurseDepth);
        }

        private void DrawLabels(Transform transform, int recurseDepth = 0)
        {
            // check if the transform is a part of currently selected objects

            var objectIsInSelection = Selection.Contains(transform) || Selection.Contains(transform.gameObject);
            // recursively draw labels for all children of the root transform
            if (handlesAreClickable)
            {
                // select the object when the label is clicked
                // selected objects are written in bold


                Handles.Label(transform.position, transform.name, objectIsInSelection
                    ? EditorStyles.boldLabel
                    : EditorStyles.label);


                Handles.color = objectIsInSelection
                    ? Color.yellow * handleColorTransparency
                    : Color.blue * handleColorTransparency;
                if (Handles.Button(transform.position, Quaternion.identity, handleSize, handleSize,
                        Handles.SphereHandleCap))
                {
                    // use shift to select multiple objects
                    if (Event.current.shift)
                    {
                        var current = new List<GameObject>(Selection.gameObjects);
                        if (objectIsInSelection) current.Add(transform.gameObject);
                        Selection.objects = current.ToArray();
                    }
                    else if (Event.current.control)
                    {
                        // remove object from selection if it is already selected, otherwise add it
                        var current = new List<GameObject>(Selection.gameObjects);
                        if (objectIsInSelection)
                            current.Remove(transform.gameObject);
                        else
                            current.Add(transform.gameObject);
                        // apply the new selection
                        Selection.objects = current.ToArray();
                    }
                    else
                    {
                        Selection.activeTransform = transform;
                    }
                }
            }
            else
            {
                Handles.Label(transform.position, transform.name, objectIsInSelection
                    ? EditorStyles.boldLabel
                    : EditorStyles.label);
            }

            // recurse with recursion depth
            if (recurseDepth > 0)
                foreach (Transform child in transform)
                    DrawLabels(child, recurseDepth - 1);
        }

        private void DrawButtons()
        {
            // if (GUILayout.Button("Create Waypoint")) CreateWaypoint();

            // if (Selection.activeGameObject != null)
            // todo: add a way to automatically close off loops
            // todo: add a way to automatically add branches.
            // for both of these, in-scene clickable 'nodes' could be used.
            // refer to sebastian lague's tool gui tutorial to implement this.
            drawNames = GUILayout.Toggle(drawNames, "Draw Object Names");
            handlesAreClickable = GUILayout.Toggle(handlesAreClickable, "Names are Selectable");
            recurseDepth = EditorGUILayout.IntSlider("Naming Depth", recurseDepth, 0, 10);
            handleSize = EditorGUILayout.Slider("Handle Size", handleSize, 0.1f, 1f);
            handleColorTransparency = EditorGUILayout.Slider("Handle Transparency", handleColorTransparency, 0.1f, 1f);
            if (GUILayout.Button("Select Children with Missing Materials")) SelectMissingMatObjects();
            if (GUILayout.Button("Select all objects in the scene with the same Probuilder Shape"))
                SelectProbuilderShapeWithSameSize();
            if (GUILayout.Button("Select all in list")) SelectAllInList();
            if (GUILayout.Button("Remove Null Roots")) RemoveNullRoots();
        }

        private void RemoveNullRoots()
        {
            var newRoots = new List<Transform>();
            foreach (var root in namingRoots)
            {
                if (root == null) continue;
                newRoots.Add(root);
            }

            namingRoots = newRoots.ToArray();
        }


        private void SelectAllInList()
        {
            Selection.objects = namingRoots.Select(x => x.gameObject).ToArray();
        }


        private void SelectProbuilderShapeWithSameSize()
        {
            var selected = Selection.activeGameObject;
            if (selected == null) return;
            var pb = selected.GetComponent<MeshFilter>();
            if (pb == null) return;
            var selectedSize = pb.sharedMesh.bounds;
            var allObjects = FindObjectsOfType<MeshFilter>();
            var selectedObjects = new List<GameObject>();
            foreach (var pbMesh in allObjects)
            {
                var size = pbMesh.sharedMesh.bounds;
                if (size.size == selectedSize.size) selectedObjects.Add(pbMesh.gameObject);
            }

            Selection.objects = selectedObjects.ToArray();
        }
        
    }
}