using UnityEngine;

namespace _Project.Mechanics.Camera
{
    public static class CameraExtensions
    {
        public static Bounds OrthographicBounds(this UnityEngine.Camera camera)
        {
            float screenAspect = Screen.width / (float)Screen.height;
            float cameraHeight = camera.orthographicSize * 2f;
            Bounds bounds = new(
                camera.transform.position,
                new Vector3(cameraHeight * screenAspect, cameraHeight, 0f));
            return bounds;
        }
    }
}
