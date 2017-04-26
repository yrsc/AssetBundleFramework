using UnityEngine;
using System.Collections;
using System.IO;

public class SimpleLoader
{
	public static string RES_ROOT_PATH = "Assets/";
	public static T Load<T>(string path) where T : Object
	{
		#if UNITY_EDITOR && !LOAD_ASSETBUNDLE_INEDITOR
		path = RES_ROOT_PATH + path;
		return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
		#else				
		return AssetbundleLoader.LoadRes<T>(path);
		#endif
	}

	public static GameObject InstantiateGameObject(string path,string suffix = ".prefab")
	{
		GameObject go = Load<GameObject>(path+suffix);
		if(go != null)
			return GameObject.Instantiate(go);
		return null;
	}	

	public static string LoadText(string path)
	{
		if(File.Exists(path))
		{
			return File.ReadAllText(path);
		}
		else
		{
			Debug.Log(string.Format("{0} not exist",path));
			return null;
		}
	}
}