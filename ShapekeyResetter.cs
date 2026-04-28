using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

#if UNITY_EDITOR
using UnityEditor;
using VRC.SDKBase.Editor.BuildPipeline;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("SuiSite/Shapekey ReSetter")]
public sealed class ShapekeyReSetter : MonoBehaviour, IEditorOnly
{
    [Tooltip("対象の Skinned Mesh Renderer。ここで選んだRendererのShapekeyを下で選択します。")]
    public SkinnedMeshRenderer renderer;

    [Serializable]
    public sealed class ResetTarget
    {
        [Tooltip("ビルド時に0へ戻したいShapekey")]
        public string shapekeyName = "";

        [Tooltip("ビルド時に設定する値。通常は0")]
        public float valueOnBuild = 0f;
    }

    [Tooltip("ビルド時に値を戻すShapekey一覧")]
    public List<ResetTarget> targets = new List<ResetTarget>
    {
        new ResetTarget
        {
            shapekeyName = "",
            valueOnBuild = 0f
        }
    };
}

#if UNITY_EDITOR
[CustomEditor(typeof(ShapekeyReSetter))]
public sealed class ShapekeyReSetterEditor : Editor
{
    private SerializedProperty rendererProperty;
    private SerializedProperty targetsProperty;

    private static readonly Dictionary<string, string> SearchTexts = new Dictionary<string, string>();

    private void OnEnable()
    {
        rendererProperty = serializedObject.FindProperty(nameof(ShapekeyReSetter.renderer));
        targetsProperty = serializedObject.FindProperty(nameof(ShapekeyReSetter.targets));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Shapekey ReSetter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Unityシーン上ではShapekeyを好きな値にしておき、VRChatアップロード時だけ指定したShapekeyを0などに戻します。",
            MessageType.Info
        );

        EditorGUILayout.Space(6);

        EditorGUILayout.PropertyField(rendererProperty, new GUIContent("Renderer"));

        var renderer = rendererProperty.objectReferenceValue as SkinnedMeshRenderer;
        string[] shapekeyNames = GetShapekeyNames(renderer);

        if (renderer == null)
        {
            EditorGUILayout.HelpBox("まず対象の Skinned Mesh Renderer を選択してください。", MessageType.Warning);
        }
        else if (renderer.sharedMesh == null)
        {
            EditorGUILayout.HelpBox("Renderer に Mesh が設定されていません。", MessageType.Warning);
        }
        else if (shapekeyNames.Length == 0)
        {
            EditorGUILayout.HelpBox("この Renderer の Mesh には Shapekey がありません。", MessageType.Warning);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Reset Targets", EditorStyles.boldLabel);

        // Foldoutではなく、最初から全部表示する
        for (int i = 0; i < targetsProperty.arraySize; i++)
        {
            SerializedProperty targetProperty = targetsProperty.GetArrayElementAtIndex(i);
            SerializedProperty shapekeyNameProperty = targetProperty.FindPropertyRelative(nameof(ShapekeyReSetter.ResetTarget.shapekeyName));
            SerializedProperty valueOnBuildProperty = targetProperty.FindPropertyRelative(nameof(ShapekeyReSetter.ResetTarget.valueOnBuild));

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Target {i + 1}", EditorStyles.boldLabel);

            if (GUILayout.Button("Remove", GUILayout.Width(72)))
            {
                targetsProperty.DeleteArrayElementAtIndex(i);
                break;
            }

            EditorGUILayout.EndHorizontal();

            DrawShapekeySelector(
                target,
                i,
                shapekeyNameProperty,
                shapekeyNames
            );

            EditorGUILayout.PropertyField(
                valueOnBuildProperty,
                new GUIContent("Value On Build")
            );

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Add Target"))
        {
            AddTarget();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawShapekeySelector(
        UnityEngine.Object owner,
        int index,
        SerializedProperty shapekeyNameProperty,
        string[] shapekeyNames
    )
    {
        string searchKey = $"{owner.GetInstanceID()}_{index}";

        if (!SearchTexts.TryGetValue(searchKey, out string searchText))
        {
            searchText = "";
        }

        EditorGUILayout.Space(2);

        SearchTexts[searchKey] = EditorGUILayout.TextField(
            new GUIContent("Search"),
            searchText
        );

        string currentName = shapekeyNameProperty.stringValue;

        if (shapekeyNames == null || shapekeyNames.Length == 0)
        {
            shapekeyNameProperty.stringValue = EditorGUILayout.TextField(
                new GUIContent("Shapekey Name"),
                currentName
            );
            return;
        }

        string[] filteredNames = FilterShapekeys(shapekeyNames, SearchTexts[searchKey]);

        if (filteredNames.Length == 0)
        {
            EditorGUILayout.HelpBox("検索に一致するShapekeyがありません。", MessageType.None);

            EditorGUILayout.LabelField(
                "Current Shapekey",
                string.IsNullOrEmpty(currentName) ? "(未選択)" : currentName
            );

            return;
        }

        List<string> popupNames = new List<string>();

        // 空欄を選べるようにする
        popupNames.Add("(未選択)");

        popupNames.AddRange(filteredNames);

        int currentIndex = string.IsNullOrEmpty(currentName)
            ? 0
            : popupNames.IndexOf(currentName);

        if (currentIndex < 0)
        {
            popupNames.Insert(1, $"現在: {currentName}");
            currentIndex = 1;
        }

        int selectedIndex = EditorGUILayout.Popup(
            "Shapekey Name",
            currentIndex,
            popupNames.ToArray()
        );

        string selectedName = popupNames[selectedIndex];

        if (selectedName == "(未選択)")
        {
            shapekeyNameProperty.stringValue = "";
        }
        else if (!selectedName.StartsWith("現在: ", StringComparison.Ordinal))
        {
            shapekeyNameProperty.stringValue = selectedName;
        }
    }

    private void AddTarget()
    {
        targetsProperty.arraySize++;

        SerializedProperty newTarget = targetsProperty.GetArrayElementAtIndex(targetsProperty.arraySize - 1);
        SerializedProperty shapekeyNameProperty = newTarget.FindPropertyRelative(nameof(ShapekeyReSetter.ResetTarget.shapekeyName));
        SerializedProperty valueOnBuildProperty = newTarget.FindPropertyRelative(nameof(ShapekeyReSetter.ResetTarget.valueOnBuild));

        shapekeyNameProperty.stringValue = "";
        valueOnBuildProperty.floatValue = 0f;
    }

    private static string[] GetShapekeyNames(SkinnedMeshRenderer renderer)
    {
        if (renderer == null || renderer.sharedMesh == null)
        {
            return Array.Empty<string>();
        }

        Mesh mesh = renderer.sharedMesh;
        string[] names = new string[mesh.blendShapeCount];

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            names[i] = mesh.GetBlendShapeName(i);
        }

        return names;
    }

    private static string[] FilterShapekeys(string[] source, string searchText)
    {
        if (source == null || source.Length == 0)
        {
            return Array.Empty<string>();
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return source;
        }

        List<string> results = new List<string>();

        foreach (string name in source)
        {
            if (name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                results.Add(name);
            }
        }

        return results.ToArray();
    }
}
#endif

#if UNITY_EDITOR
public sealed class ShapekeyReSetterProcessor : IVRCSDKPreprocessAvatarCallback
{
    public int callbackOrder => -10000;

    public bool OnPreprocessAvatar(GameObject avatarGameObject)
    {
        var resetters = avatarGameObject.GetComponentsInChildren<ShapekeyReSetter>(true);

        foreach (var resetter in resetters)
        {
            if (resetter == null)
                continue;

            var renderer = resetter.renderer;

            if (renderer == null)
            {
                Debug.LogWarning(
                    $"[ShapekeyReSetter] Renderer が未設定です: {GetPath(resetter.transform)}",
                    resetter
                );
                continue;
            }

            Mesh mesh = renderer.sharedMesh;

            if (mesh == null)
            {
                Debug.LogWarning(
                    $"[ShapekeyReSetter] Mesh がありません: {GetPath(renderer.transform)}",
                    renderer
                );
                continue;
            }

            if (resetter.targets == null)
                continue;

            foreach (var target in resetter.targets)
            {
                if (target == null)
                    continue;

                // 空欄のTargetは無視する
                if (string.IsNullOrWhiteSpace(target.shapekeyName))
                    continue;

                int index = mesh.GetBlendShapeIndex(target.shapekeyName);

                if (index < 0)
                {
                    Debug.LogWarning(
                        $"[ShapekeyReSetter] Shapekey '{target.shapekeyName}' が見つかりません: {GetPath(renderer.transform)} / Mesh: {mesh.name}",
                        renderer
                    );
                    continue;
                }

                renderer.SetBlendShapeWeight(index, target.valueOnBuild);

                Debug.Log(
                    $"[ShapekeyReSetter] {GetPath(renderer.transform)} の '{target.shapekeyName}' を {target.valueOnBuild} に戻しました。",
                    renderer
                );
            }

            // アップロードされるアバター側に補助コンポーネントを残さない
            UnityEngine.Object.DestroyImmediate(resetter);
        }

        return true;
    }

    private static string GetPath(Transform transform)
    {
        if (transform == null)
            return "(null)";

        var names = new Stack<string>();

        while (transform != null)
        {
            names.Push(transform.name);
            transform = transform.parent;
        }

        return string.Join("/", names.ToArray());
    }
}
#endif
