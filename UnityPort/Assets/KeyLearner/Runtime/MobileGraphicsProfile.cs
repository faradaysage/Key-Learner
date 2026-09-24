using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KeyLearner.Unity
{
    public static class MobileGraphicsProfile
    {
        public static void Apply(GameServices services)
        {
            if (!services.TouchPlay) return;
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            QualitySettings.globalTextureMipmapLimit = 1;
            services.Camera.allowHDR = false;
            services.Camera.allowMSAA = false;
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline)
            {
                pipeline.renderScale = Mathf.Clamp((float)services.Settings.RenderScale, .65f, .85f);
                pipeline.msaaSampleCount = 1;
                pipeline.supportsHDR = false;
                pipeline.shadowDistance = Mathf.Min(pipeline.shadowDistance, 45);
                pipeline.shadowCascadeCount = 1;
            }
            var cameraData = services.Camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData) cameraData.antialiasing = AntialiasingMode.None;
        }
    }
}
