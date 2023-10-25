using System;
using System.Collections.Generic;
using System.Linq;
using Redactor.Scripts.RedactorUtil.Calc;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Redactor.Scripts.Utilities.SceneUtil
{
    public enum GroupingMode
    {
        ByName,
        ByBoundingBox,
        ByVertexCount,
        ByScale,
        ByDistance,
        ByHeight,
        ByWidth,
        ByDepth,
        ByXPosition,
        ByYPosition,
        ByZPosition,
        ByRotation,
        ByXRotation,
        ByYRotation,
        ByZRotation,
        ByXYDir,
        ByXZDir,
        ByYZDir,
        ByNormalisedCenterPoint,
        ByCenterPoint
    }

    [Serializable]
    public class ReToolRematerialGroup
    {
        public string targetName;
        public Material newMaterial;
        public Material oldMaterial;
        public List<GameObject> targets = new();
    }

    public class ReToolRematerial : MonoBehaviour
    {
        public GroupingMode groupingMode = GroupingMode.ByName;

        [Header("Grouping Settings")] public bool recursiveChildren = true;
        public bool copyAllPropertiesFromBaseMaterial = true;


        [Header("Color Settings")] public bool recolorAssignedMat = true;
        public bool indexColors = true;

        public float hueMaxAngle = 255f;
        public float hueMinAngle = 0f;
        public float mainColorHueOffset = 0f;
        public float secondColorHueOffset = 180f;

        public Material baseMaterial;

        [Header("Sampling Settings")] public bool alwaysUseSampleNames = true;
        [Range(1, 36)] public int sprinkleSampleCount = 10;
        [Range(0, 36)] public int groupCompressionTarget = 10;
        [Range(1, 100)] public int numericNamingCompression = 10;

        public List<ReToolRematerialGroup> groups = new();
        public List<Material> lockedMaterials = new();
        private Dictionary<string, ReToolRematerialGroup> groupDict = new();

        public void GetNames()
        {
            GameObject[] GetChildren(GameObject parent, bool recursive)
            {
                var children = new List<GameObject>();
                foreach (Transform child in parent.transform)
                {
                    children.Add(child.gameObject);
                    if (recursive) children.AddRange(GetChildren(child.gameObject, true));
                }

                return children.ToArray();
            }

            // clear the groups and groupDict
            groups.Clear();
            groupDict.Clear();


            // create a list of all the children, recursively if recursiveChildren is true


            var children = GetChildren(transform.gameObject, recursiveChildren);

            // remove children without renderers
            var childrenWithRenderer = new List<Renderer>();

            foreach (var child in children)
            {
                var targetRenderer = child.GetComponent<Renderer>();
                if (targetRenderer != null) childrenWithRenderer.Add(targetRenderer);
            }

            UpdateLockedMaterials();
            // list of children with locked materials
            var childrenWithLockedMaterials = new List<Renderer>();
            if (lockedMaterials.Count > 0)
                foreach (var child in childrenWithRenderer)
                {
                    var targetRenderer = child;
                    if (lockedMaterials.Contains(targetRenderer.sharedMaterial))
                        childrenWithLockedMaterials.Add(targetRenderer);
                }

            // remove the children with locked materials from the childrenWithRenderer list
            foreach (var child in childrenWithLockedMaterials) childrenWithRenderer.Remove(child);


            // create a list of all the children's names without duplicates
            var uniqueNames = new List<string>();
            foreach (var child in childrenWithRenderer)
            {
                // split names by '(' and '.' to remove the numbers and the (Clone) suffix
                var parsedName = ParseName(child);

                // add the first part of the split name to the list
                if (!uniqueNames.Contains(parsedName)) uniqueNames.Add(parsedName);
            }

            // create a rematerial group for each unique name
            foreach (var name in uniqueNames)
            {
                var group = new ReToolRematerialGroup();
                group.targetName = name;
                groups.Add(group);
            }

            // update the dictionary for the rematerial groups
            foreach (var group in groups) groupDict.Add(group.targetName, group);


            // add the children to the groups if their name is a key in the dictionary
            foreach (var child in childrenWithRenderer)
            {
                var parsedName = ParseName(child);
                if (groupDict.ContainsKey(parsedName)) groupDict[parsedName].targets.Add(child.gameObject);
            }

            // compress the groups to the target size
            CompressGroups(groupCompressionTarget);

            // check if the children in the group have materials
            foreach (var group in groups)
            {
                // skip groups with no targets
                if (group.targets.Count == 0) continue;

                // get the most popular material in the group
                group.oldMaterial = GetMostPopularMaterial(group);
            }
        }

        private Material GetMostPopularMaterial(ReToolRematerialGroup group)
        {
            var materialDict = new Dictionary<Material, int>();
            foreach (var target in group.targets)
            {
                var renderer = target.GetComponent<Renderer>();
                if (renderer == null) continue;
                var material = renderer.sharedMaterial;
                if (materialDict.ContainsKey(material))
                    materialDict[material]++;
                else
                    materialDict.Add(material, 1);
            }

            var mostPopularMaterial = materialDict.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
            return mostPopularMaterial;
        }

        public void UpdateLockedMaterials()
        {
            // remove null materials from the locked materials list
            lockedMaterials.RemoveAll(material => material == null);
            // only keep unique materials in the locked materials list
            lockedMaterials = lockedMaterials.Distinct().ToList();
        }

        private static Vector3 AverageCenterPoint(ReToolRematerialGroup group)
        {
            var averageCenterPoint = Vector3.zero;
            foreach (var target in group.targets)
            {
                var renderer = target.GetComponent<Renderer>();
                if (renderer == null) continue;
                averageCenterPoint += renderer.bounds.center;
            }

            averageCenterPoint /= group.targets.Count;
            return averageCenterPoint;
        }


        private void CompressGroups(int targetCompression)
        {
            if (targetCompression < 1) return;
            if (groups.Count <= targetCompression) return;

            while (groups.Count > targetCompression)
            {
                var groupToMerge = groups.OrderBy(group => group.targets.Count).First();
                var mostSimilarGroup = GetMostSimilarGroup(groupToMerge, out _);

                // merge the smallest group into the most similar group
                foreach (var target in groupToMerge.targets) mostSimilarGroup.targets.Add(target);

                // remove the smallest group
                groups.Remove(groupToMerge);
                // remove the smallest group from the dictionary
                groupDict.Remove(groupToMerge.targetName);

                // add the prefix Comp_ to the most similar group's name
                if (!mostSimilarGroup.targetName.StartsWith("Comp_"))
                    mostSimilarGroup.targetName = "Comp_" + mostSimilarGroup.targetName;
                //todo: create rename rules for each grouping method

                // update the dictionary key for the most similar group
                groupDict.Remove(mostSimilarGroup.targetName);
                groupDict.Add(mostSimilarGroup.targetName, mostSimilarGroup);
            }
        }


        private void GetMostSimilarGroupFloat
        (out ReToolRematerialGroup mostSimilarGroup, out float bestFoundMetric,
            ReToolRematerialGroup comparisonBase,
            Func<ReToolRematerialGroup, float> compute,
            Func<float, float, float> compare)
        {
            mostSimilarGroup = null;
            var selfMetric = compute(comparisonBase);
            bestFoundMetric = float.MaxValue;
            foreach (var group in groups)
            {
                if (group == comparisonBase) continue;

                var foundMetric =
                    compare(selfMetric, compute(group));

                if (foundMetric < bestFoundMetric)
                {
                    bestFoundMetric = foundMetric;
                    mostSimilarGroup = group;
                }
            }
        }


        private void GetMostSimilarGroupVector
        (out ReToolRematerialGroup mostSimilarGroup, out float bestFoundMetric, ReToolRematerialGroup comparisonBase,
            Func<ReToolRematerialGroup, Vector3> compute,
            Func<Vector3, Vector3, float> compare)
        {
            mostSimilarGroup = null;
            var selfMetric = compute(comparisonBase);
            bestFoundMetric = float.MaxValue;
            foreach (var group in groups)
            {
                if (group == comparisonBase) continue;

                var foundMetric =
                    compare(selfMetric, compute(group));

                if (foundMetric < bestFoundMetric)
                {
                    bestFoundMetric = foundMetric;
                    mostSimilarGroup = group;
                }
            }
        }

        private void GetMostSimilarGroupTwo
        (out ReToolRematerialGroup mostSimilarGroup, out float bestFoundMetric, ReToolRematerialGroup comparisonBase,
            Func<ReToolRematerialGroup, ReToolRematerialGroup, float> computeAndCompare)
        {
            mostSimilarGroup = null;
            bestFoundMetric = float.MaxValue;
            foreach (var group in groups)
            {
                if (group == comparisonBase) continue;
                var foundMetric =
                    computeAndCompare(comparisonBase, group);

                if (foundMetric < bestFoundMetric)
                {
                    bestFoundMetric = foundMetric;
                    mostSimilarGroup = group;
                }
            }
        }

        private static Vector3 AverageScale(ReToolRematerialGroup arg)
        {
            var averageScale = arg.targets.Select(target => target.transform.localScale)
                .Aggregate(Vector3.zero, (current, scale) => current + scale);

            averageScale /= arg.targets.Count;
            return averageScale;
        }

        private static Vector3 AverageBounds(ReToolRematerialGroup group)
        {
            var averageBounds = group.targets.Select(target => target.GetComponent<Renderer>())
                .Where(renderer => renderer != null)
                .Aggregate(Vector3.zero, (current, renderer) => current + renderer.localBounds.extents);

            averageBounds /= group.targets.Count;
            return averageBounds;
        }


        public void Rematerialize()
        {
            // apply the material to all the targets in the groups

            foreach (var group in groups)
            {
                var useOldMaterial = false;
                // skip groups with no material
                if (group.newMaterial == null)
                {
                    if (group.oldMaterial == null) continue;
                    useOldMaterial = true;
                }

                foreach (var target in group.targets)
                {
                    // skip targets with no renderer
                    var targetRenderer = target.GetComponent<Renderer>();
                    if (targetRenderer == null) continue;
                    var targetMaterial = useOldMaterial ? group.oldMaterial : group.newMaterial;
                    targetRenderer.sharedMaterial = targetMaterial;
                }
            }
        }

        private Color GetRandColorFromExample(Color exampleColor, int colorIndex, int colorCount, float hueOffset = 0f)
        {
            // generate a indexed value for H, and get the S and V from the base material
            Color.RGBToHSV(exampleColor, out var h, out var s, out var v);
            if (indexColors)
                h = (float)colorIndex / colorCount;
            else
                h = Random.value;

            // add the offset to the H value
            h += hueOffset / 360f;

            // if the H value is outside the range, wrap it around
            h %= 1f;

            // lerp the H value to the range
            h = Mathf.Lerp(hueMinAngle / 360f, hueMaxAngle / 360f, h);


            exampleColor = Color.HSVToRGB(h, s, v);
            return exampleColor;
        }

        public void SetRandColorFromExample(Material materialToEdit, Material exampleMaterial, int colorIndex,
            int colorCount)
        {
            if (copyAllPropertiesFromBaseMaterial)
                materialToEdit.CopyPropertiesFromMaterial(baseMaterial);

            // if _Color does not exist, use Walls_Color and Floor_Color
            if (materialToEdit.HasProperty("_Color"))
            {
                materialToEdit.SetColor("_Color",
                    GetRandColorFromExample(exampleMaterial.GetColor("_Color"), colorIndex, colorCount,
                        mainColorHueOffset));
            }
            else
            {
                materialToEdit.SetColor("Walls_Color",
                    GetRandColorFromExample(exampleMaterial.GetColor("Walls_Color"), colorIndex, colorCount,
                        mainColorHueOffset));
                materialToEdit.SetColor("Floor_Color",
                    GetRandColorFromExample(exampleMaterial.GetColor("Floor_Color"), colorIndex, colorCount,
                        mainColorHueOffset + secondColorHueOffset));
            }
        }

        public void ApplyBaseMaterial()
        {
            if (baseMaterial == null) return;
            if (groups.Count == 0) return;
            // set the new material to the base material
            foreach (var group in groups) group.newMaterial = baseMaterial;

            // rematerialize
            Rematerialize();
        }

        
        private ReToolRematerialGroup GetMostSimilarGroup(ReToolRematerialGroup smallestGroup,
            out float bestFoundMetric)
        {
            
            ReToolRematerialGroup mostSimilarGroup = null;
            switch (groupingMode)
            {
                // find the most similar group to the smallest group
                // find the group with the smallest levenshtein distance
                case GroupingMode.ByName:
                    GetMostSimilarGroupTwo
                    (out mostSimilarGroup, out bestFoundMetric, smallestGroup, (group1, group2) =>
                        LevenshteinDistance.Compute
                        (
                            group1.targetName,
                            group2.targetName
                        ));
                    return mostSimilarGroup;
                case GroupingMode.ByBoundingBox:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup, AverageBounds,
                        Vector3.Distance);
                    return mostSimilarGroup;
                case GroupingMode.ByCenterPoint:
                    GetMostSimilarGroupVector
                    (out mostSimilarGroup, out bestFoundMetric, smallestGroup, AverageCenterPoint,
                        Vector3.Distance);
                    return mostSimilarGroup;
                case GroupingMode.ByYPosition:
                    GetMostSimilarGroupVector
                    (out mostSimilarGroup, out bestFoundMetric, smallestGroup, AverageCenterPoint,
                        (selfMetric, foundMetric) => Mathf.Abs(selfMetric.y - foundMetric.y));
                    return mostSimilarGroup;
                case GroupingMode.ByVertexCount:
                    GetMostSimilarGroupFloat
                    (out mostSimilarGroup, out bestFoundMetric, smallestGroup, group =>
                    {
                        var averageVertexCount = group.targets
                            .Select(target => target.GetComponent<MeshFilter>())
                            .Where(meshFilter => meshFilter != null)
                            .Average(meshFilter => meshFilter.sharedMesh.vertexCount);
                        return (float)averageVertexCount;
                    }, (selfMetric, foundMetric) => Mathf.Abs(selfMetric - foundMetric));
                    return mostSimilarGroup;
                case GroupingMode.ByScale:
                    GetMostSimilarGroupVector
                        (out mostSimilarGroup, out bestFoundMetric, smallestGroup, AverageScale, Vector3.Distance);
                    return mostSimilarGroup;
                case GroupingMode.ByDistance:
                    GetMostSimilarGroupFloat(out mostSimilarGroup, out bestFoundMetric, smallestGroup,
                        group => group.targets[0].GetComponent<Renderer>().bounds.center.sqrMagnitude,
                        (selfMetric, foundMetric) => Mathf.Abs(selfMetric - foundMetric));
                    return mostSimilarGroup;
                case GroupingMode.ByHeight:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup, AverageBounds,
                        (selfMetric, foundMetric) => Mathf.Abs(selfMetric.y - foundMetric.y));
                    return mostSimilarGroup;
                case GroupingMode.ByWidth:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup, AverageBounds,
                        (selfMetric, foundMetric) => Mathf.Abs(selfMetric.x - foundMetric.x));
                    return mostSimilarGroup;
                case GroupingMode.ByDepth:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup, AverageBounds,
                        (selfMetric, foundMetric) => Mathf.Abs(selfMetric.z - foundMetric.z));
                    return mostSimilarGroup;
                case GroupingMode.ByXPosition:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup,
                        AverageCenterPoint,
                        (selfMetric, foundMetric) => Mathf.Abs(selfMetric.x - foundMetric.x));
                    return mostSimilarGroup;
                case GroupingMode.ByZPosition:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup,
                        AverageCenterPoint,
                        (selfMetric, foundMetric) => Mathf.Abs(selfMetric.z - foundMetric.z));
                    return mostSimilarGroup;
                case GroupingMode.ByRotation:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup,
                        AverageRotation,
                        (selfMetric, foundMetric) =>
                            Quaternion.Angle(Quaternion.Euler(selfMetric), Quaternion.Euler(foundMetric)));
                    return mostSimilarGroup;
                case GroupingMode.ByXRotation:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup,
                        AverageRotation,
                        (selfMetric, foundMetric) =>
                            Quaternion.Angle(Quaternion.Euler(selfMetric.x, 0, 0),
                                Quaternion.Euler(foundMetric.x, 0, 0)));
                    return mostSimilarGroup;
                case GroupingMode.ByYRotation:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup,
                        AverageRotation,
                        (selfMetric, foundMetric) =>
                            Quaternion.Angle(Quaternion.Euler(0, selfMetric.y, 0),
                                Quaternion.Euler(0, foundMetric.y, 0)));
                    return mostSimilarGroup;
                case GroupingMode.ByZRotation:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup,
                        AverageRotation,
                        (selfMetric, foundMetric) =>
                            Quaternion.Angle(Quaternion.Euler(0, 0, selfMetric.z),
                                Quaternion.Euler(0, 0, foundMetric.z)));
                    return mostSimilarGroup;
                case GroupingMode.ByXYDir:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup,
                        AverageCenterPoint,
                        (selfMetric, foundMetric) =>
                            selfMetric.x * foundMetric.x + selfMetric.y * foundMetric.y);
                    return mostSimilarGroup;
                case GroupingMode.ByXZDir:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup,
                        AverageCenterPoint,
                        (selfMetric, foundMetric) =>
                            selfMetric.x * foundMetric.x + selfMetric.z * foundMetric.z);

                    return mostSimilarGroup;
                case GroupingMode.ByYZDir:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup,
                        AverageCenterPoint,
                        (selfMetric, foundMetric) =>
                            selfMetric.y * foundMetric.y + selfMetric.z * foundMetric.z);
                    return mostSimilarGroup;
                case GroupingMode.ByNormalisedCenterPoint:
                    GetMostSimilarGroupVector(out mostSimilarGroup, out bestFoundMetric, smallestGroup,
                        AverageCenterPoint,
                        (selfMetric, foundMetric) =>
                            Vector3.Angle(selfMetric.normalized, foundMetric.normalized));
                    return mostSimilarGroup;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static Vector3 AverageRotation(ReToolRematerialGroup arg)
        {
            // get the average rotation of the group
            var average = new Quaternion(0, 0, 0, 0);

            var amount = 0;

            foreach (var target in arg.targets)
            {
                var quaternion = target.transform.rotation;
                amount++;

                average = Quaternion.Slerp(average, quaternion, 1 / amount);
            }

            return average.eulerAngles;
        }

        private string ParseName(Renderer targetRenderer)
        {
            switch (groupingMode)
            {
                case GroupingMode.ByName:
                    return targetRenderer.gameObject.name.Split('(', '.')[0];
                case GroupingMode.ByBoundingBox:
                    return (targetRenderer.localBounds.extents / numericNamingCompression).ToString("F0");
                case GroupingMode.ByCenterPoint:
                    return (targetRenderer.bounds.center / numericNamingCompression).ToString("F0");
                case GroupingMode.ByYPosition:
                    return (targetRenderer.bounds.center.y / numericNamingCompression).ToString("F0");
                case GroupingMode.ByVertexCount:
                    var meshFilter = targetRenderer.GetComponent<MeshFilter>();
                    return
                        meshFilter == null
                            ? targetRenderer.gameObject.name.Split('(', '.')[0]
                            : (meshFilter.sharedMesh.vertexCount / numericNamingCompression).ToString();
                case GroupingMode.ByScale:
                    return (targetRenderer.transform.localScale / numericNamingCompression).ToString("F0");

                case GroupingMode.ByDistance:
                    return (targetRenderer.bounds.center.sqrMagnitude / numericNamingCompression).ToString("F0");

                case GroupingMode.ByHeight:
                    return (targetRenderer.bounds.extents.y / numericNamingCompression).ToString("F0");

                case GroupingMode.ByWidth:
                    return (targetRenderer.bounds.extents.x / numericNamingCompression).ToString("F0");

                case GroupingMode.ByDepth:
                    return (targetRenderer.bounds.extents.z / numericNamingCompression).ToString("F0");

                case GroupingMode.ByXPosition:
                    return (targetRenderer.bounds.center.x / numericNamingCompression).ToString("F0");

                case GroupingMode.ByZPosition:
                    return (targetRenderer.bounds.center.z / numericNamingCompression).ToString("F0");

                case GroupingMode.ByRotation:
                    return (targetRenderer.transform.rotation.eulerAngles / numericNamingCompression).ToString("F0");
                case GroupingMode.ByXRotation:
                    return (targetRenderer.transform.rotation.eulerAngles.x / numericNamingCompression).ToString("F0");
                case GroupingMode.ByYRotation:
                    return (targetRenderer.transform.rotation.eulerAngles.y / numericNamingCompression).ToString("F0");
                case GroupingMode.ByZRotation:
                    return (targetRenderer.transform.rotation.eulerAngles.z / numericNamingCompression).ToString("F0");
                case GroupingMode.ByXYDir:
                    return (targetRenderer.bounds.center.x / numericNamingCompression).ToString("F0") + "_" +
                           (targetRenderer.bounds.center.y / numericNamingCompression).ToString("F0");
                case GroupingMode.ByXZDir:
                    return (targetRenderer.bounds.center.x / numericNamingCompression).ToString("F0") + "_" +
                           (targetRenderer.bounds.center.z / numericNamingCompression).ToString("F0");
                case GroupingMode.ByYZDir:
                    return (targetRenderer.bounds.center.y / numericNamingCompression).ToString("F0") + "_" +
                           (targetRenderer.bounds.center.z / numericNamingCompression).ToString("F0");
                case GroupingMode.ByNormalisedCenterPoint:
                    return targetRenderer.bounds.center.normalized.ToString("F1");
                default:
                    return targetRenderer.gameObject.name.Split('(', '.')[0];
            }
        }

        public void LockMaterial(Material material)
        {
            // add to locked materials
            if (lockedMaterials.Contains(material)) return;
            lockedMaterials.Add(material);
            UpdateLockedMaterials();
        }

        public void UnlockMaterial(Material material)
        {
            // remove from locked materials
            if (!lockedMaterials.Contains(material)) return;
            lockedMaterials.Remove(material);
            UpdateLockedMaterials();
        }
    }
}