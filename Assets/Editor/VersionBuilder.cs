using UnityEngine;
using System.Collections;
using UnityEditor;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class VersionBuilder : Editor 
{
	public static string AssetBundle_Path = Application.dataPath + "/StreamingAssets/AssetBundle";
	public static string Root_Path = Application.dataPath + "/Examples";
	public static string ABResource_Path = Root_Path+ "/ABResources";

	private static string _versionMd5FilesPath = Application.dataPath + "/VersionFiles/";
	private static string _versionMd5FileName = "AssetbudleMd5File.txt";
	private static string _updateFileName = "";

	static VersionBuilder()
	{
		#if UNITY_IOS
			_versionMd5FilesPath += "iOS/";
		#elif UNITY_ANDROID
			_versionMd5FilesPath += "Android/";
		#endif
		_versionMd5FileName = _versionMd5FilesPath + _versionMd5FileName;
		_updateFileName = _versionMd5FilesPath + VersionConfig.updateFileName;
	}

	[MenuItem("Version/CleanBuildApp(we need a new app)")]
	static void BuildGameApp()
	{
		CleanBuildAllBundle();
		CleanAndWriteNewVersion();
	}

	[MenuItem("Version/FastBuildApp(we need a new app)")]
	static void FastBuildGameApp()
	{
		BuildAllBundle();
		CleanAndWriteNewVersion();
	}

	static void CleanAndWriteNewVersion()
	{
		CleanVersionFiles();
		if(GenerateAllBundleMd5Now())
		{
			WriteVersionMd5Files();
		}
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
	}


	[MenuItem("Version/CleanBuildPatch(we need update assetbundles)")]
	static void BuildAssetbundle()
	{
		if(CheckCanBuildPatch())
		{
			CleanBuildAllBundle();
			GenerateUpdateFiles();
		}
	}


	[MenuItem("Version/FastBuildPatch(we need update assetbundles)")]
	static void FastBuildAssetbundle()
	{
		if(CheckCanBuildPatch())
		{
			BuildAllBundle();
			GenerateUpdateFiles();
		}
	}

	static bool CheckCanBuildPatch()
	{
		if(!File.Exists(_versionMd5FileName))
		{
			Debug.LogError("Can not find last version,you may execute \"CleanBuildApp\" first if you want to update assetbundles based on last version");
			return false;
		}
		if(LoadLastUpdateFile() == null)
		{
			Debug.LogError("Load last update file failed!");
			return false;
		}
		if(_updateFiles.resVersion == VersionConfig.res_version)
		{
			Debug.LogError("You must change your res_verion in versionconfig if you want to publish a patch");
			return false;
		}
		return true;
	}


	static void GenerateUpdateFiles()
	{
		if(!LoadLastVersionMd5())
		{
			Debug.LogError("Load last version md5 file failed!");
			return;
		}
		if(!GenerateAllBundleMd5Now())
		{
			Debug.LogError("Generate new version md5 file failed!");
			return;
		}

		List<string> needUpdateFileList = GenerateDifferentFilesList();
		List<string> needDeleteFileList = GenerateNeedDeleteFilesList();
		if(needUpdateFileList.Count == 0 && needDeleteFileList.Count == 0)
		{
			Debug.LogError("nothing need update");
			return;
		}
		//set update files version
		string newVersion = GetNewResVersion();
		_updateFiles.resVersion = newVersion;
		for(int i = 0; i < needUpdateFileList.Count;i++)
		{
			_updateFiles.files[needUpdateFileList[i]] = new UpdateFileInfo(_allBundleMd5Now[needUpdateFileList[i]],newVersion);
		}
		//delete unused assetbundles
		for(int i = 0; i < needDeleteFileList.Count; i++)
		{
			if(_updateFiles.files.ContainsKey(needDeleteFileList[i]))
			{
				_updateFiles.files.Remove(needDeleteFileList[i]);
			}
		}
		WriteUpdateFiles();
		WriteVersionMd5Files();

		//export update assetbundles
		ExportUpdateAssetbundle(needUpdateFileList);

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
	}

	static void CleanVersionFiles()
	{
		if(Directory.Exists(_versionMd5FilesPath))
		{
			Directory.Delete(_versionMd5FilesPath,true);
		}
	}

	static void WriteVersionMd5Files()
	{
		if(!Directory.Exists(_versionMd5FilesPath))
		{
			Directory.CreateDirectory(_versionMd5FilesPath);
		}
		StringBuilder sb = new StringBuilder();
		foreach(KeyValuePair<string,string> kvp in _allBundleMd5Now)
		{
			string content = kvp.Key + "," + kvp.Value + "\n";
			sb.Append(content);
		}
		File.WriteAllText(_versionMd5FileName,sb.ToString(),System.Text.Encoding.UTF8);	
	}


	private static Dictionary<string,string> _allBundleMd5LastVersion = new Dictionary<string, string>();

	static bool LoadLastVersionMd5()
	{
		try
		{
			_allBundleMd5LastVersion.Clear();
			string[] content = File.ReadAllLines(_versionMd5FileName);
			if(content != null)
			{
				for(int i = 0; i < content.Length; i++)
				{
					if(!string.IsNullOrEmpty(content[i]))
					{
						string []kvp = content[i].Split(',');
						if(kvp == null || kvp.Length < 2)
						{
							Debug.LogError("Can not parse last md5 file with content " + content[i]);
							return false;
						}
						_allBundleMd5LastVersion[kvp[0]] = kvp[1];
					}
				}
			}
		}
		catch (Exception ex)
		{
			throw new Exception("LoadLastVersionMd5 Error:" + ex.Message);
			return false;
		}
		return true;
	}

	private static Dictionary<string,string> _allBundleMd5Now = new Dictionary<string, string>();
	static bool GenerateAllBundleMd5Now()
	{
		_allBundleMd5Now.Clear();
		if(!Directory.Exists(AssetBundle_Path))
		{
			Debug.LogError(string.Format("assetbundle path {0} not exist",AssetBundle_Path));
			return false;
		}
		DirectoryInfo dir = new DirectoryInfo(AssetBundle_Path);
		var files = dir.GetFiles("*", SearchOption.AllDirectories);
		for (var i = 0; i < files.Length; ++i)
		{
			try
			{
				if(files[i].Name.EndsWith(".meta") || files[i].Name.EndsWith(".manifest") )
					continue;
				string md5 = VersionHelper.GetMd5Val(files[i].FullName);
				string fileRelativePath = files[i].FullName.Substring(AssetBundle_Path.Length+1);
				_allBundleMd5Now[fileRelativePath] = md5;
			}
			catch (Exception ex)
			{
				throw new Exception("GetMD5HashFromFile fail,error:" + ex.Message);
				return false;
			}
		}
		return true;
	}

	private static UpdateFile _updateFiles = new UpdateFile();
	static List<string> GenerateDifferentFilesList()
	{ 
		List<string> _needUpdateFileList = new List<string>();
		foreach(KeyValuePair<string,string> kvp in _allBundleMd5Now)
		{
			if(_allBundleMd5LastVersion.ContainsKey(kvp.Key))
			{
				if(_allBundleMd5LastVersion[kvp.Key] == kvp.Value)
				{
					continue;
				}
				else
				{
					_needUpdateFileList.Add(kvp.Key);
				}
			}
			else
			{
				_needUpdateFileList.Add(kvp.Key);
			}
		}
		return _needUpdateFileList;
	}

	static List<string> GenerateNeedDeleteFilesList()
	{ 
		List<string> _needDeleteFileList = new List<string>();
		foreach(KeyValuePair<string,string> kvp in _allBundleMd5LastVersion)
		{
			if(_allBundleMd5Now.ContainsKey(kvp.Key))
			{
				continue;
			}
			else
			{
				_needDeleteFileList.Add(kvp.Key);
			}
		}
		return _needDeleteFileList;
	}

	static UpdateFile LoadLastUpdateFile()
	{
		_updateFiles = new UpdateFile();
		if(File.Exists(_updateFileName))
		{
			try
			{		
				string content = File.ReadAllText(_updateFileName);
				VersionHelper.ParseUpdateFile(content,ref _updateFiles);
			}
			catch (Exception ex)
			{
				throw new Exception("Load UpdateFile Error:" + ex.Message);
				return null;
			}
		}
		return _updateFiles;
	}

	static string GetNewResVersion()
	{
		return VersionConfig.res_version;
	}
		
	private static string _exportPath = "";
	static void ExportUpdateAssetbundle(List<string> updatedAssetbundles)
	{		
		if(updatedAssetbundles.Count > 0)
		{
			_exportPath = Application.dataPath.Substring(0,Application.dataPath.LastIndexOf("/")+1) + "NewAssetbundles";
			#if UNITY_IOS
			_exportPath += "/iOS";
			#elif UNITY_ANDROID
			_exportPath += "/Android";
			#endif
			_exportPath = string.Format("{0}/{1}/{2}",_exportPath,VersionConfig.app_version,GetNewResVersion());
			for(int i = 0; i < updatedAssetbundles.Count;i++)
			{
				string assetbundleDestPath = _exportPath + "/" + updatedAssetbundles[i];
				string assetbundleSrcPath = AssetBundle_Path + "/" + updatedAssetbundles[i];
				string destDir = Path.GetDirectoryName(assetbundleDestPath);
				if(!Directory.Exists(destDir))
				{
					Directory.CreateDirectory(destDir);
				}
				File.Copy(assetbundleSrcPath,assetbundleDestPath,true);
			}
		}
		string updateFileSrcPath = _updateFileName;
		string updateFileDestPath = _exportPath + "/" +Path.GetFileName(updateFileSrcPath);
		string updateFileDestDir = Path.GetDirectoryName(updateFileDestPath);
		if(!Directory.Exists(updateFileDestDir))
		{
			Directory.CreateDirectory(updateFileDestDir);
		}
		File.Copy(updateFileSrcPath,updateFileDestPath,true);
	}

	static void WriteUpdateFiles()
	{			
		string str = VersionHelper.ConvertUpdateFileToString(_updateFiles);
		File.WriteAllText(_updateFileName,str,System.Text.Encoding.UTF8);	

	}

	static void CleanBuildAllBundle()
	{
		if(Directory.Exists(AssetBundle_Path))
		{
			Directory.Delete(AssetBundle_Path,true);
		}
		BuildAllBundle();
	}

	static void BuildAllBundle()
	{		
		AssetBundleBuilder.BuildAssetBundle();
	}


}
