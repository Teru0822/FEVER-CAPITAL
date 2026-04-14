using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.IO;

public class AddStretchRopeTask
{
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.delayCall += Execute;
    }

    public static void Execute()
    {
        if (SessionState.GetBool("StretchRopeAdded", false)) return;
        SessionState.SetBool("StretchRopeAdded", true);

        string scenePath = "Assets/Scenes/UFOCATCHER.unity";
        UnityEngine.SceneManagement.Scene currentScene = EditorSceneManager.GetActiveScene();
        string previousScenePath = currentScene.path;

        bool changedScene = false;
        if (currentScene.path != scenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath);
                changedScene = true;
            }
            else
            {
                return;
            }
        }
        
        GameObject target = GameObject.Find("6");
        if(target != null)
        {
            StretchRope sr = target.GetComponent<StretchRope>();
            if(sr == null)
            {
                sr = target.AddComponent<StretchRope>();
                Debug.Log("Added StretchRope to " + target.name);
            }
            
            sr.autoStretch = true;
            sr.stretchLength = 2.0f;
            sr.stretchSpeed = 4.0f;
            
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("UFOCATCHER scene saved.");
        }

        if (changedScene && !string.IsNullOrEmpty(previousScenePath))
        {
            EditorSceneManager.OpenScene(previousScenePath);
        }

        // 実行後に自身を削除する（1回限りのタスク）
        string scriptPath = "Assets/Editor/AddStretchRopeTask.cs";
        if (File.Exists(scriptPath))
        {
            AssetDatabase.DeleteAsset(scriptPath);
        }
    }
}
