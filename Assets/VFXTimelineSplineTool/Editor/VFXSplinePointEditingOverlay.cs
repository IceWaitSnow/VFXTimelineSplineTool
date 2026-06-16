
// compatibility layer for old windows
namespace VFXTimelineSplineTool.EditorTools
{
    public static class VFXSplinePointEditingOverlay
    {
        public static bool Enabled
        {
            get => VFXSplinePointAPI.Enabled;
            set => VFXSplinePointAPI.Enabled = value;
        }

        public static float OffsetScale
        {
            get => VFXSplinePointAPI.OffsetScale;
            set => VFXSplinePointAPI.OffsetScale = value;
        }

        public static bool LargerFirstPoint
        {
            get => VFXSplinePointAPI.LargerFirstPoint;
            set => VFXSplinePointAPI.LargerFirstPoint = value;
        }

        public static void AddPointAtEnd() => VFXSplinePointAPI.AddPointAtEnd();
        public static void InsertPointAfter() => VFXSplinePointAPI.InsertPointAfter();
        public static void DeletePoint() => VFXSplinePointAPI.DeletePoint();
        public static void FramePoint() => VFXSplinePointAPI.FramePoint();
    }
}
