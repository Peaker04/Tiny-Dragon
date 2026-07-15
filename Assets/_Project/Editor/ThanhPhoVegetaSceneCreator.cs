using System.IO;
using TinyDragon.Camera;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ThanhPhoVegetaSceneCreator
{
    private const string ScenePath = "Assets/_Project/Scenes/ThanhPhoVegeta.unity";
    private const string ImportedRoot = "Assets/_Project/ImportedDragonBall";

    [MenuItem("Tiny Dragon/Maps/Create Thanh Pho Vegeta")]
    public static void CreateThanhPhoVegetaScene()
    {
        CreateScene();
    }

    public static void CreateSceneBatch()
    {
        CreateScene();
    }

    private static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 3.55f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.19215687f, 0.3019608f, 0.4745098f, 0f);
        camera.fieldOfView = 34f;
        camera.depth = -1f;
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        camera.allowMSAA = false;
        camera.useOcclusionCulling = false;
        cameraObject.transform.position = new Vector3(34f, -8f, -10f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<CameraFollow>();

        GameObject previewObject = new GameObject("Thanh_Pho_Vegeta");
        CompleteMapPreview preview = previewObject.AddComponent<CompleteMapPreview>();
        SerializedObject serializedPreview = new SerializedObject(preview);
        serializedPreview.FindProperty("mapId").intValue = 19;
        serializedPreview.FindProperty("tileId").intValue = 11;
        serializedPreview.FindProperty("zoomLevel").intValue = 1;
        serializedPreview.FindProperty("pixelsPerUnit").floatValue = 24f;
        serializedPreview.FindProperty("clientResourceRoot").stringValue =
            Path.Combine(ImportedRoot, "res").Replace('\\', '/');
        serializedPreview.FindProperty("serverResourceRoot").stringValue =
            Path.Combine(ImportedRoot, "server").Replace('\\', '/');
        serializedPreview.FindProperty("sqlPath").stringValue =
            Path.Combine(ImportedRoot, "hashirama.sql").Replace('\\', '/');
        serializedPreview.FindProperty("targetCamera").objectReferenceValue = camera;
        serializedPreview.FindProperty("showLabels").boolValue = false;
        serializedPreview.FindProperty("showSqlObjects").boolValue = true;
        serializedPreview.FindProperty("showSourceUi").boolValue = false;
        serializedPreview.ApplyModifiedPropertiesWithoutUndo();

        preview.Rebuild();
        Object.DestroyImmediate(preview);
        AddMapBounds(previewObject);
        FrameCamera(camera, previewObject);

        EditorSceneManager.SaveScene(scene, ScenePath, true);
        EditorSceneManager.OpenScene(ScenePath);
        Selection.activeObject = previewObject;
    }

    private static void AddMapBounds(GameObject root)
    {
        Bounds bounds = CalculateRendererBounds(root);
        GameObject boundsObject = new GameObject("MapBounds_Auto");
        BoxCollider2D collider = boundsObject.AddComponent<BoxCollider2D>();
        boundsObject.AddComponent<MapBounds2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(Mathf.Max(1f, bounds.size.x), Mathf.Max(1f, bounds.size.y));
        collider.offset = bounds.center;
    }

    private static void FrameCamera(Camera camera, GameObject root)
    {
        Bounds bounds = CalculateRendererBounds(root);
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y + 0.35f, -10f);
        camera.orthographicSize = 3.55f;
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, new Vector3(32f, 18f, 1f));
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        bounds.Expand(new Vector3(1f, 1f, 0f));
        return bounds;
    }
}
