using UnityEngine;

namespace Redactor.Scripts.Utilities.SceneUtil
{
    public class MiniGizmo : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position, 0.1f);
        }
    }
}