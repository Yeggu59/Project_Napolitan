// 서림역 B2 환승통로 화이트박스 + 플레이어 배치 (11/5). 다시 실행하면 Whitebox·Player를 새로 만든다
// 메뉴: Napolitan > Setup > 환승통로 화이트박스
// 배치모드: Unity -batchmode -projectPath Unity -executeMethod WhiteboxBuilder.Run -quit
// 손으로 다듬은 뒤에는 다시 실행하지 말 것 (덮어씀) — 이후 수정은 ProBuilder로

using Napolitan.Game.Interaction;
using Napolitan.Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class WhiteboxBuilder
{
    const string ScenePath = "Assets/Scenes/SeorimB2.unity";
    const float Width = 4f, Height = 3f, Length = 40f, Wall = 0.2f;

    [MenuItem("Napolitan/Setup/환승통로 화이트박스")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
            Object.DestroyImmediate(root); // 기본 카메라·태양광 포함 — 지하라서 자연광 없음

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.06f);

        var whitebox = new GameObject("Whitebox").transform;
        Box("Floor", whitebox, new Vector3(0f, -Wall / 2f, Length / 2f), new Vector3(Width, Wall, Length));
        Box("Ceiling", whitebox, new Vector3(0f, Height + Wall / 2f, Length / 2f), new Vector3(Width, Wall, Length));
        Box("Wall_L", whitebox, new Vector3(-(Width + Wall) / 2f, Height / 2f, Length / 2f), new Vector3(Wall, Height, Length));
        Box("Wall_R", whitebox, new Vector3((Width + Wall) / 2f, Height / 2f, Length / 2f), new Vector3(Wall, Height, Length));
        Box("Wall_Start", whitebox, new Vector3(0f, Height / 2f, -Wall / 2f), new Vector3(Width + Wall * 2f, Height, Wall));
        Box("Wall_End", whitebox, new Vector3(0f, Height / 2f, Length + Wall / 2f), new Vector3(Width + Wall * 2f, Height, Wall));

        // 자판기 자리 (나중에 상태머신 연결). 지금은 조사 테스트용 단서만
        var vending = Box("VendingMachine", whitebox, new Vector3(Width / 2f - 0.45f, 1f, 20f), new Vector3(0.8f, 2f, 0.9f));
        AddClueSource(vending, "자판기", "clue.vending.1");

        // 공지문 (노이즈 단서)
        var notice = Box("NoticeBoard", whitebox, new Vector3(-Width / 2f + 0.03f, 1.5f, 10f), new Vector3(0.05f, 0.8f, 1.2f));
        AddClueSource(notice, "근무 일지", "noise.2");

        for (int i = 0; i < 4; i++)
            CeilingLight(whitebox, new Vector3(0f, Height - 0.2f, 5f + i * 10f), i);

        var vanSpawn = new GameObject("VanSpawn").transform;
        vanSpawn.position = new Vector3(0f, 0f, 2f);

        CreatePlayer(vanSpawn.position);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[WhiteboxBuilder] 환승통로 화이트박스 생성 완료");
    }

    static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = size;
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline != null) go.GetComponent<MeshRenderer>().sharedMaterial = pipeline.defaultMaterial;
        return go;
    }

    static void AddClueSource(GameObject go, string prompt, string clueId)
    {
        var source = go.AddComponent<ClueSource>();
        var so = new SerializedObject(source);
        so.FindProperty("prompt").stringValue = prompt;
        var ids = so.FindProperty("clueIds");
        ids.arraySize = 1;
        ids.GetArrayElementAtIndex(0).stringValue = clueId;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CeilingLight(Transform parent, Vector3 position, int index)
    {
        var go = new GameObject($"CeilingLight_{index}");
        go.transform.SetParent(parent);
        go.transform.position = position;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 9f;
        light.intensity = 3f;
        light.color = new Color(0.92f, 0.96f, 1f); // 형광등 색
        light.shadows = LightShadows.Soft;
    }

    static void CreatePlayer(Vector3 position)
    {
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = position;

        var body = player.AddComponent<CharacterController>();
        body.height = 1.7f;
        body.radius = 0.3f;
        body.center = new Vector3(0f, 0.85f, 0f);

        var cameraRoot = new GameObject("CameraRoot");
        cameraRoot.tag = "MainCamera";
        cameraRoot.transform.SetParent(player.transform);
        cameraRoot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        var camera = cameraRoot.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        cameraRoot.AddComponent<AudioListener>();

        var controller = player.AddComponent<FirstPersonController>();
        var controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("cameraRoot").objectReferenceValue = cameraRoot.transform;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        var inspector = player.AddComponent<Inspector>();
        var inspectorSo = new SerializedObject(inspector);
        inspectorSo.FindProperty("viewCamera").objectReferenceValue = camera;
        inspectorSo.ApplyModifiedPropertiesWithoutUndo();

        player.AddComponent<InspectHud>();
    }
}
