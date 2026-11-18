using UnityEngine;
using VibeRun.Core;

namespace VibeRun.Gameplay
{
    /// <summary>
    /// Keyboard shortcuts for desktop/web testing: ← / A = left door, → / D = right door, ↑ / W = middle door,
    /// or 1 / 2 / 3. On phones players tap the door directly (see <see cref="DoorView"/>).
    /// </summary>
    public sealed class SwipeInput : MonoBehaviour
    {
        public Swipe Last { get; private set; } = Swipe.None;
        public float LastTime { get; private set; } = -99f;
        public bool Enabled = true;
        public int DoorCount = 2;

        public void Clear() { Last = Swipe.None; LastTime = -99f; }

        void Update()
        {
            if (!Enabled) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Alpha1)) Emit(Swipe.Left);
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Emit(Swipe.Up);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) Emit(DoorCount == 3 ? Swipe.Up : Swipe.Right);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.Alpha3)) Emit(Swipe.Right);
        }

        void Emit(Swipe s)
        {
            Last = s;
            LastTime = Time.unscaledTime;
        }
    }
}
