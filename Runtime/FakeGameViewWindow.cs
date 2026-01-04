#if UNITY_EDITOR
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public class FakeGameViewWindow : OdinEditorWindow
{
    [MenuItem("UGToolkit/Scrcpy")]
    public static void OpenWindow()
    {
        var window = GetWindow<FakeGameViewWindow>();
        window.Show();
    }
    [InlineEditor(Expanded = true)]
    public FakeGameViewData fakeGameViewData;


}
#endif