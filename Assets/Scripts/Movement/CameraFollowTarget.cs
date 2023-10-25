using UnityEngine;

namespace Redactor.Scripts.Movement
{
    public class CameraFollowTarget : MonoBehaviour
    {
        public Transform targetTransform;
        public Vector3 offset;
        public Vector3 velocity;
        public float smoothTime;

        public bool updateOffsetOnStart = true;

        private void Start()
        {
            if (updateOffsetOnStart) UpdateOffset();
        }
        
        private void UpdateOffset()
        {
            offset = Quaternion.Inverse(targetTransform.rotation) * (targetTransform.position - transform.position);
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            var currentPosition = transform.position;
            var targetPosition = (targetTransform.position - targetTransform.rotation * offset);
            transform.position = Vector3.SmoothDamp(currentPosition, targetPosition, ref velocity, smoothTime);
            transform.rotation = targetTransform.rotation;
        }
    }
}