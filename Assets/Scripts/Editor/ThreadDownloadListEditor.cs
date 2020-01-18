using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ThreadDownloadList))]
public class ThreadDownloadListEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); //显示默认所有参数

        ThreadDownloadList demo = (ThreadDownloadList)target;

        if (GUILayout.Button("打开文件夹"))
        {
            //EditorUtility.OpenFolderPanel("zzh", Application.persistentDataPath, "");

            //string path = @"D:\Program Files";
            //string path = @"C:\Users\Administrator\AppData\LocalLow\setsuodu\thread";
            string path = Path.Combine(Application.persistentDataPath, "").Replace("/", @"\");
            Debug.Log(path);
            System.Diagnostics.Process.Start("explorer.exe", path);
        }
    }
}