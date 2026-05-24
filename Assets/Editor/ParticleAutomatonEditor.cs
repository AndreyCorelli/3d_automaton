using UnityEngine;
using UnityEditor;

namespace ParticleAutomaton.Editor
{
    [CustomEditor(typeof(ParticleAutomatonController))]
    public class ParticleAutomatonEditor : UnityEditor.Editor
    {
        string[] _names   = System.Array.Empty<string>();
        int      _index;
        string   _saveAs  = "";
        bool     _expanded = true;

        void OnEnable() => Refresh();

        void Refresh()
        {
            _names = ParticlePresetManager.ListAll();
            _index = Mathf.Clamp(_index, 0, Mathf.Max(0, _names.Length - 1));
        }

        public override void OnInspectorGUI()
        {
            var ctrl = (ParticleAutomatonController)target;

            // ── Presets ───────────────────────────────────────────────────────
            _expanded = EditorGUILayout.BeginFoldoutHeaderGroup(_expanded, "Presets");
            if (_expanded)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (_names.Length == 0)
                {
                    EditorGUILayout.LabelField("No presets found.", EditorStyles.miniLabel);
                }
                else
                {
                    _index = Mathf.Clamp(
                        EditorGUILayout.Popup("Preset", _index, _names),
                        0, _names.Length - 1);
                }

                // ── Row 1: Load / Delete / Reset Built-in ─────────────────────
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Load") && _names.Length > 0)
                {
                    if (ParticlePresetManager.Load(_names[_index], out var cfg))
                    {
                        Undo.RecordObject(ctrl, "Load Preset");
                        ctrl.ApplyPreset(cfg);
                        EditorUtility.SetDirty(ctrl);
                    }
                    else
                    {
                        Debug.LogWarning($"[Presets] Could not load '{_names[_index]}'.");
                    }
                }

                bool isDefault = _names.Length > 0 && _names[_index] == "default";
                bool isBuiltin = _names.Length > 0 && ParticlePresetManager.IsBuiltin(_names[_index]);

                GUI.enabled = !isDefault;
                if (GUILayout.Button("Delete") && _names.Length > 0)
                {
                    if (EditorUtility.DisplayDialog("Delete Preset",
                        $"Delete \"{_names[_index]}\"?", "Delete", "Cancel"))
                    {
                        ParticlePresetManager.Delete(_names[_index]);
                        Refresh();
                    }
                }
                GUI.enabled = true;

                if (isBuiltin && GUILayout.Button("Reset Built-in"))
                {
                    if (EditorUtility.DisplayDialog("Reset Built-in Preset",
                        $"Overwrite \"{_names[_index]}\" with its original values?", "Reset", "Cancel"))
                    {
                        ParticlePresetManager.ResetBuiltin(_names[_index]);
                    }
                }

                EditorGUILayout.EndHorizontal();

                // ── Row 2: Save As ────────────────────────────────────────────
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Save current settings as:", EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();

                _saveAs = EditorGUILayout.TextField(_saveAs);

                GUI.enabled = !string.IsNullOrWhiteSpace(_saveAs);
                if (GUILayout.Button("Save", GUILayout.Width(50)))
                {
                    string name = _saveAs.Trim();
                    ParticlePresetManager.Save(name, ctrl.GetConfig());
                    _saveAs = "";
                    Refresh();
                    int idx = System.Array.IndexOf(_names, name);
                    if (idx >= 0) _index = idx;
                }
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);
                if (GUILayout.Button("Refresh List", EditorStyles.miniButton))
                    Refresh();

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4);

            // ── Default inspector ─────────────────────────────────────────────
            DrawDefaultInspector();
        }
    }
}
