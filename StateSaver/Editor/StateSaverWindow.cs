using System;
using System.Collections.Generic;
using Unity.Plastic.Newtonsoft.Json;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

public class StateSaverWindow : EditorWindow
{
    private const string FILE_PATH = "Assets/StateData.json";
    private object targetObject;
    private string stateName = "NewState";
    private List<string> loadOptions = new List<string>();
    private List<string> stateNames = new List<string>();
    private int selectedOption = 0;

    [MenuItem("Window/State Saver")]
    public static void ShowWindow(object target)
    {
        StateSaverWindow window = GetWindow<StateSaverWindow>("State Saver");
        window.targetObject = target;
        window.PopulateLoadOptions();
    }

    private void OnGUI()
    {
        GUILayout.Label("State Saver", EditorStyles.boldLabel);

        if (targetObject != null)
        {
            GUILayout.Label("Target Object: " + targetObject.ToString(), EditorStyles.label);
        }
        else
        {
            GUILayout.Label("No target object selected.", EditorStyles.label);
        }

        GUILayout.Label("Enter your state name:", EditorStyles.label);
        stateName = GUILayout.TextField(stateName, EditorStyles.textField);
        if (GUILayout.Button("Save State"))
        {
            SaveState(stateName);
            PopulateLoadOptions();
        }

        GUILayout.Space(10);

        GUILayout.Label("Load State Options:");
        selectedOption = EditorGUILayout.Popup("Select Option", selectedOption, stateNames.ToArray());

        if (GUILayout.Button("Load State"))
        {
            LoadState(selectedOption);
        }
    }

    private void PopulateLoadOptions()
    {
        loadOptions.Clear();
        stateNames.Clear();


        if (System.IO.File.Exists(FILE_PATH))
        {
            string existingJson = System.IO.File.ReadAllText(FILE_PATH);
            StateData existingData = JsonConvert.DeserializeObject<StateData>(existingJson);

            if (existingData != null && existingData.targetId == GetTargetId(targetObject))
            {
                foreach (var state in existingData.states)
                {
                    string fullStateName = $"{state.stateName}${existingData.targetId}";
                    loadOptions.Add(fullStateName); // Full name with UUID
                    stateNames.Add(GetShortName(fullStateName)); // Short name for display
                }
            }
        }
    }

    private void SaveState(string stateName)
    {
        if (targetObject == null)
        {
            Debug.LogError("No target object selected for saving state.");
            return;
        }
        if (stateName == "")
        {
            stateName = "NewState";
        }

        string targetId = GetTargetId(targetObject);

        Dictionary<string, object> variableData = new Dictionary<string, object>();

        var targetType = targetObject.GetType();

        var fields = targetType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        foreach (var field in fields)
        {
            try
            {
                object value = field.GetValue(targetObject);


                // Skip null or default values
                if (value == null || IsDefaultValue(value))
                    continue;
                variableData[field.Name] = ConvertToSerializable(value);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Could not read field: {field.Name}. Exception: {ex.Message}");
            }
        }

        StateData stateData = new StateData
        {
            targetId = targetId,
            states = new List<StateEntry>()
        };

        stateData.states.Add(new StateEntry
        {
            stateName = stateName,
            variables = variableData
        });

        // string FILE_PATH = $"Assets/StateData.json";

        if (System.IO.File.Exists(FILE_PATH))
        {
            string existingJson = System.IO.File.ReadAllText(FILE_PATH);
            StateData existingData = JsonConvert.DeserializeObject<StateData>(existingJson);
            if (existingData != null && existingData.targetId == targetId)
            {
                stateData.states.AddRange(existingData.states);
            }
        }

        string json = JsonConvert.SerializeObject(stateData, Formatting.Indented);

        System.IO.File.WriteAllText(FILE_PATH, json);
        AssetDatabase.Refresh();
    }


    private void LoadState(int option)
    {
        if (targetObject == null)
        {
            Debug.LogError("No target object selected for loading state.");
            return;
        }

        if (option < 0 || option >= loadOptions.Count)
        {
            Debug.LogError("Invalid state selection.");
            return;
        }

        string selectedStateName = loadOptions[option]; // Full state name with UUID
        string filePath = $"Assets/StateData.json";

        if (System.IO.File.Exists(filePath))
        {
            string existingJson = System.IO.File.ReadAllText(filePath);
            StateData existingData = JsonConvert.DeserializeObject<StateData>(existingJson);

            if (existingData != null && existingData.targetId == GetTargetId(targetObject))
            {
                // Find the selected state
                var selectedState = existingData.states.Find(state => $"{state.stateName}${existingData.targetId}" == selectedStateName);

                if (selectedState != null)
                {
                    // Assign variables to the target object
                    var targetType = targetObject.GetType();
                    foreach (var variable in selectedState.variables)
                    {
                        var field = targetType.GetField(variable.Key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            object value = ConvertFromSerializable(variable.Value, field.FieldType);
                            field.SetValue(targetObject, value);
                        }
                    }
                }
                else
                {
                    Debug.LogError("Selected state not found in JSON.");
                }
            }
        }
    }

    string GetShortName(string stateName)
    {
        return stateName.Split("$")[0];
    }

    static string GetTargetId(object target)
    {
        return target.GetType().ToString() + target.GetHashCode().ToString();
    }
    private bool IsDefaultValue(object value)
    {
        if (value == null)
            return true;

        var type = value.GetType();
        return value.Equals(type.IsValueType ? Activator.CreateInstance(type) : null);
    }

    private object ConvertToSerializable(object value)
    {
        if (value is Vector3 vector3)
        {
            // Convert Vector3 to a serializable format
            return new { x = vector3.x, y = vector3.y, z = vector3.z };
        }
        else if (value is Color color)
        {
            // Convert Color to a serializable format
            return new { r = color.r, g = color.g, b = color.b, a = color.a };
        }
        else if (value is Quaternion quaternion)
        {
            // Convert Quaternion to a serializable format
            return new { x = quaternion.x, y = quaternion.y, z = quaternion.z, w = quaternion.w };
        }
        else if (value is Enum enumValue)
        {
            // Convert Enum to its string representation
            return enumValue.ToString();
        }

        // Return the value as-is for other types
        return value;
    }
    private object ConvertFromSerializable(object value, Type targetType)
    {
        if (value == null)
            return null;

        if (targetType == typeof(Vector3))
        {
            var dict = value as Unity.Plastic.Newtonsoft.Json.Linq.JObject;
            return new Vector3((float)dict["x"], (float)dict["y"], (float)dict["z"]);
        }
        else if (targetType == typeof(Color))
        {
            var dict = value as Unity.Plastic.Newtonsoft.Json.Linq.JObject;
            return new Color((float)dict["r"], (float)dict["g"], (float)dict["b"], (float)dict["a"]);
        }
        else if (targetType == typeof(Quaternion))
        {
            var dict = value as Unity.Plastic.Newtonsoft.Json.Linq.JObject;
            return new Quaternion((float)dict["x"], (float)dict["y"], (float)dict["z"], (float)dict["w"]);
        }
        else if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value.ToString());
        }

        // For other types, return the value as-is
        return Convert.ChangeType(value, targetType);
    }

    [System.Serializable]
    public class StateData
    {
        public string targetId;
        public List<StateEntry> states;
    }

    [System.Serializable]
    public class StateEntry
    {
        public string stateName;
        // public List<VariableEntry> variables;
        public Dictionary<string, object> variables = new Dictionary<string, object>();
    }

    [System.Serializable]
    public class VariableEntry
    {
        public string key;
        public object value;
    }
}