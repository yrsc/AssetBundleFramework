using UnityEngine;
using System.Collections;
using UnityEditor;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace AssetBundleFramework
{
public class VersionBuilder : Editor 
{
	public static string AssetsPath = Application.streamingAssetsPath;
	private static string _versionFilesPath = Application.dataPath + "/VersionFiles/";
	private static string _md5FileName = "Md5File.txt";
	private static string _verionFileName = "";

	static VersionBuilder()
	{
		#if UNITY_IOS
			_versionFilesPath += "iOS/";
		#elif UNITY_ANDROID
			_versionFilesPath += "Android/";
		#endif
		_md5FileName = _versionFilesPath + _md5FileName;
		_verionFileName = _versionFilesPath + VersionConfig.VersionFileName;
	}

	[MenuItem("Version/CleanBuildApp(we need a new app)")]
	static void BuildGameApp()
	{
		CleanBuildAllBundle();
		BuildText();
		CleanAndWriteNewVersion();
	}

	[MenuItem("Version/FastBuildApp(we need a new app)")]
	static void FastBuildGameApp()
	{
		BuildAllBundle();
		BuildText();
		CleanAndWriteNewVersion();
	}

	static void CleanAndWriteNewVersion()
	{
		CleanVersionFiles();
		if(GenerateAllFilesMd5Now())
		{
			WriteMd5Files();
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
			BuildText();
			GenerateUpdateFiles();
		}
	}


	[MenuItem("Version/FastBuildPatch(we need update assetbundles)")]
	static void FastBuildAssetbundle()
	{
		if(CheckCanBuildPatch())
		{
			BuildAllBundle();
			BuildText();
			GenerateUpdateFiles();
		}
	}

	static bool CheckCanBuildPatch()
	{
		if(!File.Exists(_md5FileName))
		{
			Debug.LogError("Can not find last version,you may execute \"CleanBuildApp\" first if you want to update assetbundles based on last version");
			return false;
		}
		if(LoadVersionFileOnDisk() == null)
		{
			Debug.LogError("Load last update file failed!");
			return false;
		}
		if(_versionFile.resVersion == VersionConfig.res_version)
		{
			Debug.LogError("You must change your res_verion in versionconfig if you want to publish a patch");
			return false;
		}
		return true;
	}


	static void GenerateUpdateFiles()
	{
		if(!LoadMd5OnDisk())
		{
			Debug.LogError("Load last version md5 file failed!");
			return;
		}
		if(!GenerateAllFilesMd5Now())
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
		_versionFile.resVersion = newVersion;
		for(int i = 0; i < needUpdateFileList.Count;i++)
		{
			_versionFile.files[needUpdateFileList[i]] = new VersionFileInfo(_allFilesMd5Now[needUpdateFileList[i]],newVersion);
		}
		//delete unused assetbundles
		for(int i = 0; i < needDeleteFileList.Count; i++)
		{
			if(_versionFile.files.ContainsKey(needDeleteFileList[i]))
			{
				_versionFile.files.Remove(needDeleteFileList[i]);
			}
		}
		WriteVersionFiles();
		WriteMd5Files();

		//export update assetbundles
		ExportUpdateFiles(needUpdateFileList);

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
	}

	static void CleanVersionFiles()
	{
		if(Directory.Exists(_versionFilesPath))
		{
			Directory.Delete(_versionFilesPath,true);
		}
	}

	static void WriteMd5Files()
	{
		if(!Directory.Exists(_versionFilesPath))
		{
			Directory.CreateDirectory(_versionFilesPath);
		}
		StringBuilder sb = new StringBuilder();
		foreach(KeyValuePair<string,string> kvp in _allFilesMd5Now)
		{
			string content = kvp.Key + "," + kvp.Value + "\n";
			sb.Append(content);
		}
		File.WriteAllText(_md5FileName,sb.ToString());	
	}


	private static Dictionary<string,string> _allFilesMd5LastVersion = new Dictionary<string, string>();

	static bool LoadMd5OnDisk()
	{
		try
		{
			_allFilesMd5LastVersion.Clear();
			string[] content = File.ReadAllLines(_md5FileName);
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
						_allFilesMd5LastVersion[kvp[0]] = kvp[1];
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

	private static Dictionary<string,string> _allFilesMd5Now = new Dictionary<string, string>();
	static bool GenerateAllFilesMd5Now()
	{
		_allFilesMd5Now.Clear();
		if(!Directory.Exists(AssetsPath))
		{
			Debug.LogError(string.Format("AssetsPath path {0} not exist",AssetsPath));
			return false;
		}
		DirectoryInfo dir = new DirectoryInfo(AssetsPath);
		var files = dir.GetFiles("*", SearchOption.AllDirectories);
		for (var i = 0; i < files.Length; ++i)
		{
			try
			{
				if(files[i].Name.EndsWith(".meta") || files[i].Name.EndsWith(".manifest") )
					continue;
				string md5 = VersionHelper.GetMd5Val(files[i].FullName);
				string fileRelativePath = files[i].FullName.Substring(AssetsPath.Length+1);
				_allFilesMd5Now[fileRelativePath] = md5;
			}
			catch (Exception ex)
			{
				throw new Exception("GetMD5HashFromFile fail,error:" + ex.Message);
				return false;
			}
		}
		return true;
	}

	static List<string> GenerateDifferentFilesList()
	{ 
		List<string> _needUpdateFileList = new List<string>();
		foreach(KeyValuePair<string,string> kvp in _allFilesMd5Now)
		{
			if(_allFilesMd5LastVersion.ContainsKey(kvp.Key))
			{
				if(_allFilesMd5LastVersion[kvp.Key] == kvp.Value)
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
		foreach(KeyValuePair<string,string> kvp in _allFilesMd5LastVersion)
		{
			if(_allFilesMd5Now.ContainsKey(kvp.Key))
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

	private static VersionFileModel _versionFile = new VersionFileModel();
	static VersionFileModel LoadVersionFileOnDisk()
	{
		_versionFile = new VersionFileModel();
		if(File.Exists(_verionFileName))
		{
			try
			{		
				string content = File.ReadAllText(_verionFileName);
				VersionHelper.ParseVersionFile(content,ref _versionFile);
			}
			catch (Exception ex)
			{
				throw new Exception("Load UpdateFile Error:" + ex.Message);
				return null;
			}
		}
		return _versionFile;
	}

	static string GetNewResVersion()
	{
		return VersionConfig.res_version;
	}
		
	private static string _exportPath = "";
	static void ExportUpdateFiles(List<string> updatedFiles)
	{		
		if(updatedFiles.Count > 0)
		{
			_exportPath = Application.dataPath.Substring(0,Application.dataPath.LastIndexOf("/")+1) + "UpdateFiles";
			#if UNITY_IOS
			_exportPath += "/iOS";
			#elif UNITY_ANDROID
			_exportPath += "/Android";
			#endif
			_exportPath = string.Format("{0}/{1}/{2}",_exportPath,VersionConfig.app_version,GetNewResVersion());
			for(int i = 0; i < updatedFiles.Count;i++)
			{
				string assetbundleDestPath = _exportPath + "/" + updatedFiles[i];
				string assetbundleSrcPath = AssetsPath + "/" + updatedFiles[i];
				string destDir = Path.GetDirectoryName(assetbundleDestPath);
				if(!Directory.Exists(destDir))
				{
					Directory.CreateDirectory(destDir);
				}
				File.Copy(assetbundleSrcPath,assetbundleDestPath,true);
			}
		}
		string versionFileSrcPath = _verionFileName;
		string versionFileDestPath = _exportPath + "/" +Path.GetFileName(versionFileSrcPath);
		string updateFileDestDir = Path.GetDirectoryName(versionFileDestPath);
		if(!Directory.Exists(updateFileDestDir))
		{
			Directory.CreateDirectory(updateFileDestDir);
		}
		File.Copy(versionFileSrcPath,versionFileDestPath,true);
	}

	static void WriteVersionFiles()
	{			
		string str = VersionHelper.ConvertVersionFileToString(_versionFile);
		File.WriteAllText(_verionFileName,str);	

	}

	static void CleanBuildAllBundle()
	{
		if(Directory.Exists(AssetsPath))
		{
			Directory.Delete(AssetsPath,true);
		}
		BuildAllBundle();
	}

	static void BuildAllBundle()
	{		
		AssetBundleBuilder.BuildAssetBundle();
	}

	static void BuildText()
	{
		TextBuilder.BuildText();
	}
}
}