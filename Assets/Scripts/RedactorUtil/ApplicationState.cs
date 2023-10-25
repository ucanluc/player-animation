using UnityEngine;

namespace Redactor.Scripts.RedactorUtil
{
    public class ApplicationState
    {

        public static void UpdateMouseOnAppFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                LockMouse();
            }
            else
            {
                UnlockMouse();
            }
        }

        public static void UpdateMouseOnAppPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                UnlockMouse();
            }
            else
            {
                LockMouse();
            }
        }
        
        private static void LockMouse()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void UnlockMouse()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}