#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    public static class VFXSimpleSplineMenu
    {
        [MenuItem("GameObject/特效工具/VFX Simple Spline Path", false, 10)]
        public static void CreateSplinePath(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("VFX Simple Spline Path");
            VFXSimpleSpline spline = go.AddComponent<VFXSimpleSpline>();
            spline.ResetPath();

            if (menuCommand.context is GameObject parent)
                GameObjectUtility.SetParentAndAlign(go, parent);

            Undo.RegisterCreatedObjectUndo(go, "Create VFX Simple Spline Path");
            Selection.activeGameObject = go;
        }

        [MenuItem("GameObject/特效工具/VFX Spline Anchor", false, 11)]
        public static void CreateSplineAnchor(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("VFX Spline Anchor");
            VFXSplineAnchor anchor = go.AddComponent<VFXSplineAnchor>();
            anchor.spline = FindBestSpline(menuCommand);
            anchor.label = go.name;
            anchor.progress = 0.5f;
            anchor.useDistanceBasedProgress = true;

            if (menuCommand.context is GameObject parent && parent.GetComponent<VFXSimpleSpline>() == null)
                GameObjectUtility.SetParentAndAlign(go, parent);

            anchor.ApplyAnchor();
            Undo.RegisterCreatedObjectUndo(go, "Create VFX Spline Anchor");
            Selection.activeGameObject = go;
        }

        private static VFXSimpleSpline FindBestSpline(MenuCommand menuCommand)
        {
            if (menuCommand.context is GameObject context)
            {
                VFXSimpleSpline direct = context.GetComponent<VFXSimpleSpline>();
                if (direct != null) return direct;

                VFXSimpleSpline parent = context.GetComponentInParent<VFXSimpleSpline>();
                if (parent != null) return parent;
            }

            if (Selection.activeGameObject != null)
            {
                VFXSimpleSpline direct = Selection.activeGameObject.GetComponent<VFXSimpleSpline>();
                if (direct != null) return direct;

                VFXSimpleSpline parent = Selection.activeGameObject.GetComponentInParent<VFXSimpleSpline>();
                if (parent != null) return parent;
            }

            return Object.FindObjectOfType<VFXSimpleSpline>();
        }
    }
}
#endif
