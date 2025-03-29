using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Component), true)]
public class EditorStateSaverInspector : Editor
{
    [MenuItem("CONTEXT/Component/State Saver")]
    private static void OpenStateSaverWindow(MenuCommand command)
    {
        StateSaverWindow.ShowWindow(command.context);
    }
}
