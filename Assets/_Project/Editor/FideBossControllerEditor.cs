using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FideBossController))]
public sealed class FideBossControllerEditor : Editor
{
    private SerializedProperty kamehamehaChargeOffsetPixels;
    private SerializedProperty kamehamehaBeamOffset;

    private void OnEnable()
    {
        kamehamehaChargeOffsetPixels = serializedObject.FindProperty("kamehamehaChargeOffsetPixels");
        kamehamehaBeamOffset = serializedObject.FindProperty("kamehamehaBeamOffset");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Select the boss and drag the Kame Charge / Kame Beam handles in Scene View to place the Kamehameha skill.", MessageType.Info);
    }

    private void OnSceneGUI()
    {
        if (kamehamehaChargeOffsetPixels == null || kamehamehaBeamOffset == null) return;

        serializedObject.Update();
        var controller = (FideBossController)target;
        Vector3 origin = controller.transform.position;

        DrawChargeHandle(controller, origin);
        DrawBeamHandle(controller, origin);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawChargeHandle(FideBossController controller, Vector3 origin)
    {
        Vector2 chargeOffsetUnits = kamehamehaChargeOffsetPixels.vector2Value / 32f;
        Vector3 chargeWorld = origin + new Vector3(chargeOffsetUnits.x, chargeOffsetUnits.y, 0f);

        Handles.color = new Color(.35f, 1f, 1f, 1f);
        Handles.Label(chargeWorld + Vector3.up * .18f, "Kame Charge");
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.PositionHandle(chargeWorld, Quaternion.identity);
        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(controller, "Move Kamehameha Charge");
        Vector3 local = moved - origin;
        local.z = 0f;
        kamehamehaChargeOffsetPixels.vector2Value = new Vector2(local.x * 32f, local.y * 32f);
        EditorUtility.SetDirty(controller);
    }

    private void DrawBeamHandle(FideBossController controller, Vector3 origin)
    {
        Vector2 beamOffset = kamehamehaBeamOffset.vector2Value;
        Vector3 beamWorld = origin + new Vector3(beamOffset.x, beamOffset.y, 0f);

        Handles.color = new Color(.1f, .65f, 1f, 1f);
        Handles.Label(beamWorld + Vector3.up * .18f, "Kame Beam");
        Handles.DrawLine(beamWorld, beamWorld + Vector3.right * 1.2f);
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.PositionHandle(beamWorld, Quaternion.identity);
        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(controller, "Move Kamehameha Beam");
        Vector3 local = moved - origin;
        local.z = 0f;
        kamehamehaBeamOffset.vector2Value = new Vector2(local.x, local.y);
        EditorUtility.SetDirty(controller);
    }
}
