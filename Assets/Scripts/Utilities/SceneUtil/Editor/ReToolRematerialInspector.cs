using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Redactor.Scripts.Utilities.SceneUtil.Editor
{
    [CustomEditor(typeof(ReToolRematerial))]
    public class ReToolRematerialInspector : UnityEditor.Editor
    {
        private bool showLock, showApplication, showGeneration, showForceWrite, showPurging;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();


            var rematerial = (ReToolRematerial)target;
            var groupsCount = rematerial.groups.Count;

            showLock = EditorGUILayout.BeginFoldoutHeaderGroup(showLock, " --- Locking --- ");
            if (showLock)
            {
                if (GUILayout.Button("Update Locked Materials"))
                    CheckLockedMaterials(rematerial);
                if (GUILayout.Button("Lock Selected Object Materials"))
                    LockSelectedMaterials(rematerial);

                if (GUILayout.Button("Unlock Selected Object Materials"))
                    UnlockSelectedMaterials(rematerial);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();


            showApplication = EditorGUILayout.BeginFoldoutHeaderGroup(showApplication, " --- Application --- ");
            if (showApplication)
            {
                if (rematerial.groupCompressionTarget == 0)
                    EditorGUILayout.HelpBox("Group Compression Target is 0. This might create too many object groups.",
                        MessageType.Info);


                if (GUILayout.Button("Update Object Groups")) GetNames(rematerial);
                if (GUILayout.Button("Apply Materials")) Rematerialize(rematerial);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            showGeneration = EditorGUILayout.BeginFoldoutHeaderGroup(showGeneration, " --- Generation --- ");
            if (showGeneration)
            {
                if (groupsCount >= 50)
                    EditorGUILayout.HelpBox($"Group count is {groupsCount}. Button disabled.",
                        MessageType.Error);
                if (groupsCount >= 20)
                    EditorGUILayout.HelpBox($"This will create one material for each group.",
                        MessageType.Warning);

                if (groupsCount <= 50 && GUILayout.Button("Update & Select Group Materials"))
                    GenerateFromBaseMaterial(rematerial);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            showForceWrite = EditorGUILayout.BeginFoldoutHeaderGroup(showForceWrite, " --- Force Write --- ");
            if (showForceWrite)
            {
                if (GUILayout.Button("Force Sprinkle Materials"))
                    SprinkleMaterials(rematerial);
                if (GUILayout.Button("Force Base Material Everywhere"))
                    ApplyBaseMaterial(rematerial);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            showPurging = EditorGUILayout.BeginFoldoutHeaderGroup(showPurging, " --- Purging --- ");
            if (showPurging)
            {
                if (GUILayout.Button("Purge Unused Materials"))
                    PurgeAllUnusedMaterials(rematerial);
                if (GUILayout.Button("Purge Active Group Materials"))
                    PurgeGroupMaterials(rematerial);
                if (GUILayout.Button("Purge Sprinkle-Sample Materials"))
                    PurgeSampleMaterials(rematerial);
                if (GUILayout.Button("Purge All Generated Materials"))
                    PurgeMiscGeneratedMaterials(rematerial);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void UnlockSelectedMaterials(ReToolRematerial rematerial)
        {
            // get all selected objects
            var selectedObjects = Selection.gameObjects;
            var selectedAssetGUIDs = Selection.assetGUIDs;

            // get all materials from the selected objects
            var materials = new List<Material>();
            foreach (var selectedObject in selectedObjects)
            {
                var renderers = selectedObject.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers) materials.AddRange(renderer.sharedMaterials);
            }

            // get all materials from the selected assets
            foreach (var selectedAssetGUID in selectedAssetGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(selectedAssetGUID);
                var asset = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                materials.Add(asset);
            }

            // remove nulls and duplicates
            materials = materials.Where(x => x != null).Distinct().ToList();

            // unlock all selected materials
            foreach (var material in materials) rematerial.UnlockMaterial(material);

            CheckLockedMaterials(rematerial);
        }

        private void LockSelectedMaterials(ReToolRematerial rematerial)
        {
            // get all selected objects
            var selectedObjects = Selection.gameObjects;
            var selectedAssetGUIDs = Selection.assetGUIDs;

            // get all materials from the selected objects
            var materials = new List<Material>();
            foreach (var selectedObject in selectedObjects)
            {
                var renderers = selectedObject.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers) materials.AddRange(renderer.sharedMaterials);
            }

            // get all materials from the selected assets
            foreach (var selectedAssetGUID in selectedAssetGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(selectedAssetGUID);
                var asset = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                materials.Add(asset);
            }

            // remove nulls and duplicates
            materials = materials.Where(x => x != null).Distinct().ToList();

            // lock all selected materials
            foreach (var material in materials) rematerial.LockMaterial(material);

            CheckLockedMaterials(rematerial);
        }

        private static void ApplyBaseMaterial(ReToolRematerial rematerial)
        {
            rematerial.ApplyBaseMaterial();
        }

        private static void Rematerialize(ReToolRematerial rematerial)
        {
            rematerial.Rematerialize();
        }

        private static void GetNames(ReToolRematerial reToolRematerial)
        {
            reToolRematerial.GetNames();
        }

        private void PurgeAllUnusedMaterials(ReToolRematerial reToolRematerial)
        {
            var sceneName = reToolRematerial.gameObject.scene.name;
            var folderPath = $"Assets/Materials/M_Gen_{sceneName}";
            // find all materials in the folder with the name M_Gen_
            var materials = AssetDatabase.FindAssets("M_Gen_", new[] { folderPath });

            // find all known materials from the targets in the scene
            var knownMaterials = new List<Material>();
            foreach (var group in reToolRematerial.groups)
            foreach (var target in group.targets)
            {
                var renderer = target.GetComponent<Renderer>();
                if (renderer == null) continue;
                var material = renderer.sharedMaterial;
                if (material == null) continue;
                if (knownMaterials.Contains(material)) continue;
                knownMaterials.Add(material);
            }

            // find unknown materials
            var unknownMaterials = new List<Material>();
            foreach (var material in materials)
            {
                var path = AssetDatabase.GUIDToAssetPath(material);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (knownMaterials.Contains(mat)) continue;
                unknownMaterials.Add(mat);
            }

            // delete unknown materials
            foreach (var material in unknownMaterials) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(material));


            AssetDatabase.Refresh();
        }

        private static void CheckLockedMaterials(ReToolRematerial reToolRematerial)
        {
            var dirty = false;
            reToolRematerial.UpdateLockedMaterials();

            var desiredLock = reToolRematerial.lockedMaterials;

            // move generated and locked materials to the locked folder
            var sceneName = reToolRematerial.gameObject.scene.name;
            var folderPath = $"Assets/Materials/M_Gen_{sceneName}";
            var lockedFolderPath = $"Assets/Materials/M_Gen_{sceneName}/Locked";

            // check if the locked folder exists
            if (!AssetDatabase.IsValidFolder(lockedFolderPath))
                AssetDatabase.CreateFolder(folderPath, "Locked");

            // check all generated materials in the locked folder to see if they are locked
            var filesInLockedFolder = AssetDatabase.FindAssets("M_LockGen_", new[] { folderPath });
            var filesToUnlock = new List<string>();
            foreach (var file in filesInLockedFolder)
            {
                var path = AssetDatabase.GUIDToAssetPath(file);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                if (desiredLock.Contains(mat)) continue;
                filesToUnlock.Add(path);
            }

            // check all generated materials in the unlocked folder to see if they are unlocked
            var filesInUnlockedFolder = AssetDatabase.FindAssets("M_Gen_", new[] { folderPath });

            var filesToLock = new List<string>();
            foreach (var file in filesInUnlockedFolder)
            {
                var path = AssetDatabase.GUIDToAssetPath(file);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                if (!desiredLock.Contains(mat)) continue;
                filesToLock.Add(path);
            }

            if (filesToLock.Count > 0) dirty = true;
            if (filesToUnlock.Count > 0) dirty = true;

            // move locked materials to the locked folder
            foreach (var file in filesToLock)
            {
                var fileName = file.Split('/').Last();
                var newPath = $"{lockedFolderPath}/{fileName}";
                AssetDatabase.MoveAsset(file, newPath);
                var newName = fileName.Replace("M_Gen_", "M_LockGen_");
                AssetDatabase.RenameAsset(newPath, newName);
            }

            // move unlocked materials to the unlocked folder
            foreach (var file in filesToUnlock)
            {
                var fileName = file.Split('/').Last();
                var newPath = $"{folderPath}/{fileName}";
                AssetDatabase.MoveAsset(file, newPath);
                var newName = fileName.Replace("M_LockGen_", "M_Gen_");
                AssetDatabase.RenameAsset(newPath, newName);
            }

            if (dirty) AssetDatabase.Refresh();
        }


        public static void GenerateFromBaseMaterial(ReToolRematerial reToolRematerial)
        {
            // create a copy of the base material for each group
            if (reToolRematerial.baseMaterial == null) return;
            if (reToolRematerial.groups.Count == 0) return;

            var colorCount = reToolRematerial.groups.Count;
            var colorIndex = -1;
            foreach (var group in reToolRematerial.groups)
            {
                colorIndex++;

                // skip groups with no targets
                if (group.targets.Count == 0) continue;

                var sceneName = reToolRematerial.gameObject.scene.name;


                var targetMaterialName = "";
                targetMaterialName =
                    reToolRematerial.alwaysUseSampleNames
                        ? $"M_Gen_Sample_{colorIndex}"
                        : $"M_Gen_{group.targetName.Replace(" ", "_").Replace("(", "").Replace(")", "")} ";

                var folderPath = $"Assets/Materials/M_Gen_{sceneName}";
                var path = $"Assets/Materials/M_Gen_{sceneName}/{targetMaterialName}.mat";

                // create the folder if it doesn't exist
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    AssetDatabase.CreateFolder("Assets/Materials", $"M_Gen_{sceneName}");
                    AssetDatabase.Refresh();
                }

                // check if the material already exists
                var existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existingMaterial != null)
                {
                    // get the material from the asset database 
                    group.newMaterial = existingMaterial;
                    // if recolor is set, set the color from the existing material
                    if (reToolRematerial.recolorAssignedMat)
                        reToolRematerial.SetRandColorFromExample(existingMaterial, reToolRematerial.baseMaterial,
                            colorIndex, colorCount);

                    continue;
                }


                // create a copy of the base material
                var newMaterial = new Material(reToolRematerial.baseMaterial);

                // set the new material's name to the group's target name formatted for file name compatibility

                newMaterial.name = targetMaterialName;

                // if recolor is set, generate a random color from HSV and set it as the new material's color
                if (reToolRematerial.recolorAssignedMat)
                    reToolRematerial.SetRandColorFromExample(newMaterial, newMaterial, colorIndex, colorCount);

                // save the new material with a unique name under the folder Assets/Materials/{Scene name}
                AssetDatabase.CreateAsset(newMaterial, path);

                // refresh the asset database
                AssetDatabase.Refresh();

                // get the new material from the asset database
                group.newMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
        }

        public static void PurgeGroupMaterials(ReToolRematerial reToolRematerial)
        {
            var sceneName = reToolRematerial.gameObject.scene.name;
            var folderPath = $"Assets/Materials/M_Gen_{sceneName}";
            // remove group materials
            foreach (var group in reToolRematerial.groups)
            {
                // skip groups with no targets
                if (group.targets.Count == 0) continue;

                var targetMaterialName = group.targetName.Replace(" ", "_").Replace("(", "").Replace(")", "");
                var path = $"Assets/Materials/M_Gen_{sceneName}/M_Gen_{targetMaterialName}.mat";

                // remove the  target material
                if (!AssetDatabase.IsValidFolder(folderPath)) continue;
                if (!AssetDatabase.LoadAssetAtPath<Material>(path)) continue;
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.Refresh();
        }

        public static void PurgeSampleMaterials(ReToolRematerial reToolRematerial)
        {
            var sceneName = reToolRematerial.gameObject.scene.name;
            var folderPath = $"Assets/Materials/M_Gen_{sceneName}";


            // remove sample materials
            for (var i = 0; i < 36; i++)
            {
                var targetMaterialName = $"M_Gen_Sample_{i}";
                var path = $"Assets/Materials/M_Gen_{sceneName}/{targetMaterialName}.mat";

                // remove the  target material
                if (!AssetDatabase.IsValidFolder(folderPath)) continue;
                if (!AssetDatabase.LoadAssetAtPath<Material>(path)) continue;
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.Refresh();
        }


        public static void PurgeMiscGeneratedMaterials(ReToolRematerial reToolRematerial)
        {
            var sceneName = reToolRematerial.gameObject.scene.name;
            var folderPath = $"Assets/Materials/M_Gen_{sceneName}";
            // find all materials in the folder with the name M_Gen_
            var materials = AssetDatabase.FindAssets("M_Gen_", new[] { folderPath });
            foreach (var material in materials)
            {
                var path = AssetDatabase.GUIDToAssetPath(material);
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.Refresh();
        }


        public static void SprinkleMaterials(ReToolRematerial reToolRematerial)
        {
            // create a copy of the base material for each sample
            if (reToolRematerial.baseMaterial == null) return;

            var sceneName = reToolRematerial.gameObject.scene.name;
            var folderPath = $"Assets/Materials/M_Gen_{sceneName}";

            var sampleList = new List<Material>();

            // create sampleCount materials

            for (var i = 0; i < reToolRematerial.sprinkleSampleCount; i++)
            {
                var targetMaterialName = $"M_Gen_Sample_{i}";
                var path = $"Assets/Materials/M_Gen_{sceneName}/{targetMaterialName}.mat";

                // create the folder if it doesn't exist
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    AssetDatabase.CreateFolder("Assets/Materials", $"M_Gen_{sceneName}");
                    AssetDatabase.Refresh();
                }

                // check if the material already exists
                var existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existingMaterial != null)
                {
                    // get the material from the asset database 
                    // if recolor is set, set the color from the existing material
                    if (reToolRematerial.recolorAssignedMat)
                        reToolRematerial.SetRandColorFromExample(existingMaterial, reToolRematerial.baseMaterial, i,
                            reToolRematerial.sprinkleSampleCount);
                    sampleList.Add(existingMaterial);
                    continue;
                }

                // create a copy of the base material
                var newMaterial = new Material(reToolRematerial.baseMaterial);

                // set the new material's name to the group's target name formatted for file name compatibility

                newMaterial.name = targetMaterialName;

                // if recolor is set, generate a random color from HSV and set it as the new material's color
                if (reToolRematerial.recolorAssignedMat)
                    reToolRematerial.SetRandColorFromExample(newMaterial, newMaterial, i,
                        reToolRematerial.sprinkleSampleCount);

                // save the new material with a unique name under the folder Assets/Materials/{Scene name}
                AssetDatabase.CreateAsset(newMaterial, path);

                // refresh the asset database
                AssetDatabase.Refresh();

                // get the new material from the asset database
                sampleList.Add(AssetDatabase.LoadAssetAtPath<Material>(path));
            }


            // list all targets in all groups
            var targetList = reToolRematerial.groups.SelectMany(group => group.targets).ToList();

            if (!reToolRematerial.indexColors)
                // sort the target array randomly
                targetList = targetList.OrderBy(x => Random.value).ToList();
            else
                // sort the target array by name
                targetList = targetList.OrderBy(x => x.name).ToList();


            // assign the materials to the targets
            for (var i = 0; i < targetList.Count; i++)
            {
                var target = targetList[i];
                var sampleIndex = i % reToolRematerial.sprinkleSampleCount;
                target.GetComponent<Renderer>().sharedMaterial = sampleList[sampleIndex];
            }
        }
    }
}