using UnityEngine;

namespace Redactor.Scripts.Utilities.SceneUtil
{
    public class RaToolForceRemat : MonoBehaviour
    {
        public Material material;
        
        void ForceApply()
        {
            var raToolForceRemat = this.gameObject;

            forceRemat(raToolForceRemat);
        }

        private void forceRemat(GameObject raToolForceRemat)
        {
            var meshRenderers = raToolForceRemat.GetComponentsInChildren<MeshRenderer>();

            foreach (var meshRenderer in meshRenderers)
            {
                // force apply the material
                meshRenderer.sharedMaterial = material;
                // force apply the material to the shared material list
            
                var sharedMaterials = meshRenderer.sharedMaterials;
                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    sharedMaterials[i] = material;
                }
                meshRenderer.sharedMaterials = sharedMaterials;
            }
        
            var skinnedMeshRenderers = raToolForceRemat.GetComponentsInChildren<SkinnedMeshRenderer>();
        
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                // force apply the material
                skinnedMeshRenderer.sharedMaterial = material;
                // force apply the material to the shared material list
            
                var sharedMaterials = skinnedMeshRenderer.sharedMaterials;
                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    sharedMaterials[i] = material;
                }
                skinnedMeshRenderer.sharedMaterials = sharedMaterials;
            
            }
        
            var renderers = raToolForceRemat.GetComponentsInChildren<Renderer>();
        
            foreach (var renderer in renderers)
            {
                // force apply the material
                renderer.sharedMaterial = material;
                // force apply the material to the shared material list
            
                var sharedMaterials = renderer.sharedMaterials;
                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    sharedMaterials[i] = material;
                }
                renderer.sharedMaterials = sharedMaterials;
            }
        
            // go to the children
            foreach (Transform child in raToolForceRemat.transform)
            {
                forceRemat(child.gameObject);
            }
        }
    }
}
