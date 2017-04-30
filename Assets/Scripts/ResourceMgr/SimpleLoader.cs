using UnityEngine;
using System.Collections;
using System.IO;

public class SimpleLoader
{
	static string RES_ROOT_PATH = Application.dataPath;
	public static T Load<T>(string path) where T : Object
	{
		#if UNITY_EDITOR && !LOAD_ASSETBUNDLE_INEDITOR
		path = string.Format("{0}/{1}","Assets",path);
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
		#if UNITY_EDITOR && !LOAD_ASSETBUNDLE_INEDITOR
		path = string.Format("{0}/{1}",RES_ROOT_PATH,path);
		#else		
		if (VersionMgr.instance.CheckFileIsInVersionFile(path)) 
		{
			path = string.Format("{0}/{1}",AssetbundleLoader.Download_Path,path) ;
		}
		else 
		{
			path = string.Format("{0}/{1}",AssetbundleLoader.ROOT_PATH,path);
		}
		#endif

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