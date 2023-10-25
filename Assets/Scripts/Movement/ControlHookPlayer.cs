using UnityEngine;

namespace Redactor.Scripts.Movement
{
    public class PlayerCarm : MonoBehaviour
    {


        [Header("Car Internal Data")]
        public LimbedBody limbedBody;
        private PrometCarController _carController;

        public Vector3 inputMoveDir;
        public float inputRotationAngular;
        public bool initialized;
        
        public bool controlEnabled = true;
        public bool parkingBrakeEnabled = true;
        public bool paused = false;

        public float savedTimeScale = 1f;

        private void OnApplicationFocus(bool hasFocus)
        {
            RedactorUtil.ApplicationState.UpdateMouseOnAppFocus(hasFocus);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            RedactorUtil.ApplicationState.UpdateMouseOnAppPause(pauseStatus);
        }

        // Start is called before the first frame update
        private void Start()
        {
            Initialize();
        }


        // Update is called once per frame
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                // force actual application state pause
                
                if (Time.timeScale > 0f)
                {
                    savedTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }
                else
                {
                    Time.timeScale = savedTimeScale;
                }
                
                Debug.Log($"new timeScale: {Time.timeScale}");
                
            }
            
            
            if (Input.GetKeyDown(KeyCode.Z))
            {
                // force pause
                paused = !paused;
                
                limbedBody.UpdatePause(paused);
                Debug.Log($"new body pause state: {paused}");
            }
            
            if (!initialized || !controlEnabled || paused) return;

            limbedBody.bodyIsTheOnlyController = !_carController.WheelsAreGrounded();

            inputMoveDir = Vector3.zero;
            inputRotationAngular = 0f;

            // set input right/left strafe direction
            if (Input.GetAxis("Horizontal") != 0) inputMoveDir += Vector3.right * Input.GetAxis("Horizontal");

            // set input right/left rotation direction, as angle.
            if (Input.GetAxis("HorizontalRotation") != 0) inputRotationAngular += Input.GetAxis("HorizontalRotation");

            // set input forward/backward direction
            if (Input.GetAxis("Vertical") != 0) inputMoveDir += Vector3.forward * Input.GetAxis("Vertical");

            // set input forward/down direction from VerticalWorld
            if (Input.GetAxis("VerticalWorld") != 0) inputMoveDir += Vector3.up * Input.GetAxis("VerticalWorld");

            if (Input.GetKeyDown(KeyCode.R)) limbedBody.ToggleLimbsEnabled();
            if (Input.GetKey(KeyCode.Space)) limbedBody.TriggerJump();

            
            if (Input.GetKeyDown(KeyCode.F))
            {
                limbedBody.TriggerDash();    
            }

            var doHandbrake = Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.Space) || (!controlEnabled && parkingBrakeEnabled);
            // float -1 to 1
            var carForwardControl = Input.GetAxis("Vertical");
            var carSteerControl = Input.GetAxis("HorizontalRotation");
            
            // update car and limbed body with input
            _carController.UpdateDesiredMovement(carForwardControl, carSteerControl, doHandbrake);
            limbedBody.UpdateDesiredMovement(inputMoveDir, inputRotationAngular);
        }

        public void Initialize()
        {
            // find CarController and LimbedBody
            _carController = GetComponent<PrometCarController>();
            limbedBody = GetComponent<LimbedBody>();

            limbedBody.Initialize();
            _carController.Initialize();

            initialized = true;
        }


    }
}