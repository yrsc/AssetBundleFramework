using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class AssetbundleLoader
{
	public static string ROOT_PATH = "";

	private const string MANIFEST_SUFFIX = ".manifest";
	public static string Download_Path = DownloadConfig.downLoadPath;
	private static AssetBundleManifest _manifest;

	static AssetbundleLoader ()
	{		
		#if UNITY_EDITOR
		ROOT_PATH = Application.streamingAssetsPath;
		#elif UNITY_IPHONE
		ROOT_PATH = string.Format("{0}/{1}",Application.dataPath,"Raw");
		#elif UNITY_ANDROID
		ROOT_PATH = Application.streamingAssetsPath;
		#endif
	}

	private static Dictionary<string,AssetBundle> _assetbundleDic = new Dictionary<string, AssetBundle> ();

	public static AssetBundle LoadAssetBundleDependcy (string path)
	{        
		if (_manifest == null) {
			LoadManifest ();
		}
		if (_manifest != null) {
			string[] dependencies = _manifest.GetAllDependencies (path);
			if (dependencies.Length > 0) {
				//load all dependencies
				for (int i = 0; i < dependencies.Length; i++) {
					//Debug.Log(" load dependencies " + dependencies[i]);
					LoadAssetBundle (dependencies [i]);
				}

			}
			//load self
			return LoadAssetBundle (path);
		}
		return null;
	}

	static AssetBundle LoadAssetBundle (string path)
	{
		//all characters in assetbundle are lower characters
		path = path.ToLower ();
		AssetBundle bundle = null;
		//cache bundles to ignore load the same bundle
		_assetbundleDic.TryGetValue (path, out bundle);
		if (bundle != null) {
			return bundle;
		}
		if (VersionMgr.instance.CheckFileIsInVersionFile(path)) 
		{
			string bundlePath = string.Format("{0}/{1}",Download_Path,path) ;
			bundle = AssetBundle.LoadFromFile (bundlePath);
		}
		else 
		{
			string bundlePath = string.Format("{0}/{1}",ROOT_PATH,path);
			bundle = AssetBundle.LoadFromFile (bundlePath);
		}
		_assetbundleDic [path] = bundle;
		return bundle;
	}

	public static T LoadRes<T> (string path) where T : Object
	{			
		AssetBundle bundle = LoadAssetBundleDependcy (path);
		if (bundle != null) {
			int assetNameStart = path.LastIndexOf ("/") + 1;
			int assetNameEnd = path.LastIndexOf (".");
			string assetName = path.Substring (assetNameStart, assetNameEnd - assetNameStart);
			T obj = bundle.LoadAsset (assetName) as T;
			return obj;
		}
		return null;
	}

	static void LoadManifest ()
	{
		string assetbundleFile = string.Format("{0}/{1}",ROOT_PATH,"StreamingAssets");
		if (VersionMgr.instance.CheckFileIsInVersionFile("StreamingAssets")) 
		{
			assetbundleFile = string.Format("{0}/{1}",Download_Path,"StreamingAssets");
		}
		AssetBundle bundle = AssetBundle.LoadFromFile (assetbundleFile);
		UnityEngine.Object obj = bundle.LoadAsset ("AssetBundleManifest");
		bundle.Unload (false);
		_manifest = obj as AssetBundleManifest;
		Debug.Log ("Load Manifest Finished");
	}
}