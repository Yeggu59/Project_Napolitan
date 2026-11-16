// 서림역 B2 환승통로 화이트박스 + 플레이어 배치 (11/5). 다시 실행하면 Whitebox·Player를 새로 만든다
// 메뉴: Napolitan > Setup > 환승통로 화이트박스
// 배치모드: Unity -batchmode -projectPath Unity -executeMethod WhiteboxBuilder.Run -quit
// 손으로 다듬은 뒤에는 다시 실행하지 말 것 (덮어씀) — 이후 수정은 ProBuilder로

using Napolitan.Game.Anomaly;
using Napolitan.Game.Dispatch;
using Napolitan.Game.Interaction;
using Napolitan.Game.Player;
using Napolitan.Game.Presentation;
using Napolitan.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

public static class WhiteboxBuilder
{
    const string ScenePath = "Assets/Scenes/SeorimB2.unity";
    const string SitePath = "Assets/Data/subway_transfer.json";
    const string PanelMaterialPath = "Assets/Materials/VendingPanel.mat";
    const string PanelSettingsPath = "Assets/UI/GamePanelSettings.asset";
    const string ThemePath = "Assets/UI/GameTheme.tss";
    const string JournalStylePath = "Assets/UI/Journal.uss";
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

        // 붉은 자판기: 상태머신·행동 판정·연출은 WireDispatch에서 연결
        var vending = Box("VendingMachine", whitebox, new Vector3(Width / 2f - 0.45f, 1f, 20f), new Vector3(0.8f, 2f, 0.9f));
        AddClueSource(vending, "자판기", "clue.vending.1");

        // 공지문 (노이즈 단서)
        var notice = Box("NoticeBoard", whitebox, new Vector3(-Width / 2f + 0.03f, 1.5f, 10f), new Vector3(0.05f, 0.8f, 1.2f));
        AddClueSource(notice, "근무 일지", "noise.2");

        for (int i = 0; i < 4; i++)
            CeilingLight(whitebox, new Vector3(0f, Height - 0.2f, 5f + i * 10f), i);

        var vanSpawn = new GameObject("VanSpawn").transform;
        vanSpawn.position = new Vector3(0f, 0f, 2f);

        var player = CreatePlayer(vanSpawn.position);
        WireDispatch(whitebox, vending, vanSpawn, player);

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

    static GameObject CreatePlayer(Vector3 position)
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
        player.AddComponent<Eyes>();
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true; // 붉은 시야 비네팅
        return player;
    }

    /// <summary>파견 매니저 + 자판기 상태머신·행동 판정·연출 연결</summary>
    static void WireDispatch(Transform whitebox, GameObject vending, Transform vanSpawn, GameObject player)
    {
        var vendingPos = vending.transform.position;
        var cameraRoot = player.transform.Find("CameraRoot");

        // 전면 패널 (통로 쪽 = -x). 자판기는 비균등 스케일이라 자식으로 두지 않음
        // 발광 머티리얼은 에셋으로 둬야 빌드에서 _EMISSION 변형이 남는다
        var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = "VendingPanel";
        Object.DestroyImmediate(panel.GetComponent<Collider>());
        panel.transform.SetParent(whitebox);
        panel.transform.SetPositionAndRotation(new Vector3(vendingPos.x - 0.41f, 1.2f, vendingPos.z), Quaternion.Euler(0f, 90f, 0f));
        panel.transform.localScale = new Vector3(0.7f, 1.2f, 1f);
        panel.GetComponent<MeshRenderer>().sharedMaterial = PanelMaterial();

        var glowGo = new GameObject("VendingGlow");
        glowGo.transform.SetParent(whitebox);
        glowGo.transform.position = new Vector3(vendingPos.x - 0.7f, 1.4f, vendingPos.z);
        var glow = glowGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = 4f;
        glow.intensity = 1.5f;

        var coin = vending.AddComponent<AudioSource>();
        coin.playOnAwake = false;
        coin.spatialBlend = 1f;
        coin.minDistance = 1f;
        coin.maxDistance = 25f;

        var dispatchGo = new GameObject("Dispatch");
        var dispatch = dispatchGo.AddComponent<DispatchManager>();
        var hud = dispatchGo.AddComponent<DispatchDebugHud>();
        var harm = player.AddComponent<HarmPresenter>();
        var detector = vending.AddComponent<ResponseDetector>();
        var anomaly = vending.AddComponent<AnomalyController>();

        Set(dispatch, so =>
        {
            so.FindProperty("siteJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SitePath);
            so.FindProperty("vanSpawn").objectReferenceValue = vanSpawn;
            so.FindProperty("player").objectReferenceValue = player.GetComponent<FirstPersonController>();
            so.FindProperty("inspector").objectReferenceValue = player.GetComponent<Inspector>();
            so.FindProperty("harm").objectReferenceValue = harm;
        });
        Set(harm, so =>
        {
            so.FindProperty("dispatch").objectReferenceValue = dispatch;
            so.FindProperty("eyes").objectReferenceValue = player.GetComponent<Eyes>();
            so.FindProperty("viewCamera").objectReferenceValue = cameraRoot.GetComponent<Camera>();
        });
        Set(detector, so =>
        {
            so.FindProperty("dispatch").objectReferenceValue = dispatch;
            so.FindProperty("player").objectReferenceValue = player.GetComponent<FirstPersonController>();
            so.FindProperty("eyes").objectReferenceValue = player.GetComponent<Eyes>();
            so.FindProperty("viewCamera").objectReferenceValue = cameraRoot;
            so.FindProperty("gazeTarget").objectReferenceValue = vending.transform;
        });
        Set(anomaly, so =>
        {
            so.FindProperty("dispatch").objectReferenceValue = dispatch;
            so.FindProperty("player").objectReferenceValue = player.transform;
            so.FindProperty("detector").objectReferenceValue = detector;
            // 자판기 앞뒤 6m: 재발동하려면 6m 밖으로 나갔다 와야 함 (12m는 통로 시작까지 돌아가야 해서 너무 멂 — 2026-11-17 유저 플레이)
            so.FindProperty("zoneCenter").vector3Value = new Vector3(0f, Height / 2f, vendingPos.z);
            so.FindProperty("zoneSize").vector3Value = new Vector3(Width, Height, 12f);
            so.FindProperty("omenSeconds").floatValue = 2f;
            so.FindProperty("glow").objectReferenceValue = glow;
            so.FindProperty("panel").objectReferenceValue = panel.GetComponent<MeshRenderer>();
            so.FindProperty("coinSource").objectReferenceValue = coin;
        });
        Set(hud, so =>
        {
            so.FindProperty("dispatch").objectReferenceValue = dispatch;
            so.FindProperty("anomaly").objectReferenceValue = anomaly;
            so.FindProperty("detector").objectReferenceValue = detector;
        });

        CreateUi(dispatch);
    }

    /// <summary>UI Toolkit 화면 (저널). 패널 설정은 에셋으로 (1920×1080 기준 비율 맞춤)</summary>
    static void CreateUi(DispatchManager dispatch)
    {
        var ui = new GameObject("UI");
        var document = ui.AddComponent<UIDocument>();
        document.panelSettings = PanelSettingsAsset();
        var journal = ui.AddComponent<JournalView>();
        Set(journal, so =>
        {
            so.FindProperty("dispatch").objectReferenceValue = dispatch;
            so.FindProperty("styleSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>(JournalStylePath);
        });
    }

    static PanelSettings PanelSettingsAsset()
    {
        var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (settings != null) return settings;
        settings = ScriptableObject.CreateInstance<PanelSettings>();
        settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1920, 1080);
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.match = 0.5f;
        AssetDatabase.CreateAsset(settings, PanelSettingsPath);
        return settings;
    }

    static void Set(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Material PanelMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(PanelMaterialPath);
        if (material != null) return material;
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", new Color(0.5f, 0.55f, 0.6f));
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        material.SetColor("_EmissionColor", new Color(1.2f, 1.3f, 1.5f));
        AssetDatabase.CreateAsset(material, PanelMaterialPath);
        return material;
    }
}
