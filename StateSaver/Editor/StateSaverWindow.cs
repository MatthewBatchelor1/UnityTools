using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Plastic.Newtonsoft.Json;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

public class StateSaverWindow : EditorWindow
{
    private const string FILE_PATH = "Assets/StateData.json";
    private object targetObject;
    UnityEngine.Object targetObjectUnity;
    private string stateName = "NewState";
    private List<string> loadOptions = new List<string>();
    private List<string> stateNames = new List<string>();
    private int selectedOption = 0;

    [MenuItem("Window/State Saver")]
    public static void ShowWindow(object target)
    {
        StateSaverWindow window = GetWindow<StateSaverWindow>("State Saver");
        window.targetObject = target;
        window.targetObjectUnity = target as UnityEngine.Object;
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

        Type targetType = targetObject.GetType();
        Debug.Log("Target Type: " + targetType.ToString());

        //Get Fields 

        FieldInfo[] fields = targetType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        foreach (FieldInfo field in fields)
        {
            try
            {
                object value = field.GetValue(targetObject);
                if (value == null || IsDefaultValue(value))
                    continue;
                variableData[field.Name] = ConvertToSerializable(value);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Could not read field: {field.Name}. Exception: {ex.Message}");
            }
        }

        SerializedObject serializedComponent = new SerializedObject(targetObjectUnity);
        serializedComponent.Update();

        SerializedProperty serProperty = serializedComponent.GetIterator();
        serProperty.Next(true);

        while (serProperty.NextVisible(enterChildren: false))
        {
            Debug.Log("Serialized Property: " + serProperty.name);
            Debug.Log("Value = " + GetPropValue(serProperty));

            variableData[serProperty.name + "_" + serProperty.propertyType.ToString()] = ConvertToSerializable(GetPropValue(serProperty));
        }


        //Get Properties

        //This gets the properties of the target object by type, it is not guarenteed that they will exist on it
        // PropertyInfo[] properties = targetType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        PropertyInfo[] properties = targetType.GetProperties();
        foreach (PropertyInfo property in properties)
        {
            if (!property.CanRead)
                continue;

            // Debug.Log("Property: " + property.Name);

            // try
            // {
            //     object value = property.GetValue(targetObject);
            //     Debug.Log("Property: " + property.Name + " Value: " + value.ToString());
            // }
            // catch (Exception e)
            // {
            //     Debug.LogWarning($"Could not get value of property: {property.Name}. Exception: {e.Message}");
            //     continue;
            // }
        }

        Dictionary<string, StateData> allStateData = new Dictionary<string, StateData>();
        if (System.IO.File.Exists(FILE_PATH))
        {
            string existingJson = System.IO.File.ReadAllText(FILE_PATH);
            allStateData = JsonConvert.DeserializeObject<Dictionary<string, StateData>>(existingJson) ?? new Dictionary<string, StateData>();
        }

        if (!allStateData.ContainsKey(targetId))
        {
            allStateData[targetId] = new StateData
            {
                targetId = targetId,
                states = new List<StateEntry>()
            };
        }

        StateData stateData = allStateData[targetId];
        StateEntry existingState = stateData.states.Find(state => state.stateName == stateName);
        if (existingState != null)
        {
            existingState.variables = variableData;
        }
        else
        {
            stateData.states.Add(new StateEntry
            {
                stateName = stateName,
                variables = variableData
            });
        }

        string json = JsonConvert.SerializeObject(allStateData, Formatting.Indented);

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

        if (System.IO.File.Exists(FILE_PATH))
        {
            string existingJson = System.IO.File.ReadAllText(FILE_PATH);
            Dictionary<string, StateData> allStateData = JsonConvert.DeserializeObject<Dictionary<string, StateData>>(existingJson);

            if (allStateData != null && allStateData.ContainsKey(GetTargetId(targetObject)))
            {
                StateData targetStateData = allStateData[GetTargetId(targetObject)];

                StateEntry selectedState = targetStateData.states.Find(state => $"{state.stateName}${targetStateData.targetId}" == selectedStateName);

                if (selectedState != null)
                {
                    Type targetType = targetObject.GetType();
                    foreach (KeyValuePair<string, object> variable in selectedState.variables)
                    {
                        FieldInfo field = targetType.GetField(variable.Key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
            else
            {
                Debug.LogError("No data found for the current target object.");
            }
        }
        else
        {
            Debug.LogError("State data file not found.");
        }
    }

    private void PopulateLoadOptions()
    {
        loadOptions.Clear();
        stateNames.Clear();

        if (System.IO.File.Exists(FILE_PATH))
        {
            string existingJson = System.IO.File.ReadAllText(FILE_PATH);
            Dictionary<string, StateData> allStateData = JsonConvert.DeserializeObject<Dictionary<string, StateData>>(existingJson);

            if (allStateData != null && allStateData.ContainsKey(GetTargetId(targetObject)))
            {
                StateData targetStateData = allStateData[GetTargetId(targetObject)];
                foreach (StateEntry state in targetStateData.states)
                {
                    string fullStateName = $"{state.stateName}${targetStateData.targetId}";
                    loadOptions.Add(fullStateName);
                    stateNames.Add(GetShortName(fullStateName));
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

        Type type = value.GetType();
        return value.Equals(type.IsValueType ? Activator.CreateInstance(type) : null);
    }

    private object ConvertToSerializable(object value)
    {
        if (value is Vector3 vector3)
        {
            return new { x = vector3.x, y = vector3.y, z = vector3.z };
        }
        else if (value is Color color)
        {
            return new { r = color.r, g = color.g, b = color.b, a = color.a };
        }
        else if (value is Quaternion quaternion)
        {
            return new { x = quaternion.x, y = quaternion.y, z = quaternion.z, w = quaternion.w };
        }
        else if (value is Enum enumValue)
        {
            return enumValue.ToString();
        }
        else if (value is Transform transform)
        {
            // Serialize Transform properties
            return new
            {
                position = new { x = transform.position.x, y = transform.position.y, z = transform.position.z },
                rotation = new { x = transform.rotation.x, y = transform.rotation.y, z = transform.rotation.z, w = transform.rotation.w },
                scale = new { x = transform.localScale.x, y = transform.localScale.y, z = transform.localScale.z }
            };
        }

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

    private string GetPropValue(SerializedProperty prop)
    {
        SerializedPropertyType type = prop.propertyType;
        switch (type)
        {
            case SerializedPropertyType.Integer:
                return prop.intValue.ToString();
            case SerializedPropertyType.Boolean:
                return prop.boolValue.ToString();
            case SerializedPropertyType.Float:
                return prop.floatValue.ToString();
            case SerializedPropertyType.String:
                return prop.stringValue;
            case SerializedPropertyType.Color:
                return prop.colorValue.ToString();
            case SerializedPropertyType.Vector2:
                return prop.vector2Value.ToString();
            case SerializedPropertyType.Vector3:
                return prop.vector3Value.ToString();
            case SerializedPropertyType.Vector4:
                return prop.vector4Value.ToString();
            case SerializedPropertyType.Quaternion:
                return prop.quaternionValue.ToString();
            default:
                return "Unsupported type";
        }
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
        public Dictionary<string, object> variables = new Dictionary<string, object>();
    }
}