using UnityEngine;
using UnityEngine.Serialization;

namespace Redactor.Scripts.Movement
{
    public class LimbIKHint : MonoBehaviour
    {
        [FormerlySerializedAs("limbSolver")] public LimbAnimations limb;
        
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, 0.1f);
        }
    }
}