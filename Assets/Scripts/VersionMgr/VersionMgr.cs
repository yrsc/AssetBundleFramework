using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

namespace AssetBundleFramework
{
public enum VersionType
{
	App,
	Res,
	None,
}

public class VersionMgr 
{
	private string _resVersion = "0.0";
	private string _appVersion = "1.0";

	class VersionResultFromServer
	{
		public string downloadBaseUrl = null;
		public string serverResVersion;
		public VersionType versionType = VersionType.None; 
	}
	private VersionResultFromServer _serverResult = null;

	public static VersionMgr instance = null;

	public VersionMgr()
	{
		instance = this;
	}

	private VersionFileModel _lastVersionFile = null;

	void GetClientVersion()
	{
		_appVersion = VersionConfig.app_version;
		if(_lastVersionFile != null)
		{
			_resVersion = _lastVersionFile.resVersion;
			return;
		}
		string verionFilePath = string.Format("{0}/{1}",DownloadConfig.downLoadPath,VersionConfig.VersionFileName);
		//获得上一次客户端更新到的版本
		string content = SimpleLoader.LoadText(verionFilePath);
		if(string.IsNullOrEmpty(content))
		{
			_resVersion = VersionConfig.res_version;
		}
		else
		{
			VersionHelper.ParseVersionFile(content,ref _lastVersionFile);
			_resVersion = _lastVersionFile.resVersion;
		}
	}

	public void StartCheckVersion()
	{
		GetClientVersion();
		_serverResult = CheckVersionFromServer(_appVersion,_resVersion);
		if(_serverResult.versionType == VersionType.App)
		{
			//提示下载新的APP，新的下载地址result.downloadUrl
			return;
		}
		else if(_serverResult.versionType == VersionType.Res)
		{
			DownloadVersionFile();
		}
		else if(_serverResult.versionType == VersionType.None)
		{
			//验证通过，直接进入游戏
		}
	}

	VersionResultFromServer CheckVersionFromServer(string appVersion,string resVersion)
	{
		VersionResultFromServer result = new VersionResultFromServer();
		//result.downloadBaseUrl = "file:///Users/yr/GamePatch";
		result.downloadBaseUrl = "http://192.168.10.180:9001";
		//测试的时候改为VersionType.Res,代表资源更新
		result.versionType = VersionType.None;
		result.serverResVersion = "170423.2";
		//服务器传回来的下载地址假设是xx.xx.xxx.xx,我们需要根据平台重新生成一下，最终为xx.xx.xxx.xx/iOS 或者xx.xx.xxx.xx/Android
		result.downloadBaseUrl  = ConnectDownloadUrlWithPlatform(result.downloadBaseUrl);
		return result;
	}

	string ConnectDownloadUrlWithPlatform(string url)
	{
		#if UNITY_IOS
		return string.Format("{0}/{1}/{2}",url,"iOS",_appVersion);
		#elif UNITY_ANDROID
		return string.Format("{0}/{1}/{2}",url,"Android",_appVersion);
		#endif
	}

	void DownloadVersionFile(float delay = -1)
	{
		//首先下载updateFile文件，然后从updateFile文件里面下载需要更新的资源
		Debug.Log("Begin download version files");
		string versionFileDownUrl = string.Format("{0}/{1}/{2}",_serverResult.downloadBaseUrl,_serverResult.serverResVersion,VersionConfig.VersionFileName);
		DownloadMgr.instance.Download(versionFileDownUrl,OnVersionFileDownload,delay);
	}

	private VersionFileModel _versionFileOnServer = null;
	private int _downloadIndex = 0;
	void OnVersionFileDownload(WWW www)
	{		
		if(www == null)
		{
			//如果下载失败，等待1s后重新下载，可以是个逐渐增长的等待时间
			DownloadVersionFile(1.0f);
			return;
		}
		string content = www.text;
		_downloadIndex = 0;
		VersionHelper.ParseVersionFile(content,ref _versionFileOnServer);
		GenerateUpdateFilesList();
		DownloadUpdateFiles(_downloadIndex);
	}

	private List<string> _needUpdateFileList = new List<string>();
	void GenerateUpdateFilesList()
	{
		_needUpdateFileList.Clear();
		if(_versionFileOnServer == null)
		{
			return;
		}
		Dictionary<string,VersionFileInfo> _allFileDic = new Dictionary<string, VersionFileInfo>();
		if(_lastVersionFile == null)
		{
			_allFileDic = _versionFileOnServer.files;
		}
		else
		{
			//通过比较2次的updateFile生成需要更新的文件
			foreach(KeyValuePair<string,VersionFileInfo> kvp in _versionFileOnServer.files)
			{
				VersionFileInfo lastFileInfo = null;
				_lastVersionFile.files.TryGetValue(kvp.Key,out lastFileInfo);
				if(lastFileInfo == null || lastFileInfo.md5 != kvp.Value.md5)
				{
					_allFileDic[kvp.Key] = kvp.Value;
				}
			}
		}
		foreach(KeyValuePair<string,VersionFileInfo> kvp in _allFileDic)
		{
			string filesDownloadPath = string.Format("{0}/{1}",DownloadConfig.downLoadPath,kvp.Key);
			if(File.Exists(filesDownloadPath))
			{
				string md5 = VersionHelper.GetMd5Val(filesDownloadPath);
				if(!string.Equals(md5,kvp.Value.md5))
				{
					_needUpdateFileList.Add(kvp.Key);
				}		
			}
			else
			{
				_needUpdateFileList.Add(kvp.Key);
			}
		}
	}

	void DownloadUpdateFiles(int index,float delay = -1)
	{
		if(index >= _needUpdateFileList.Count)
		{
			//下载完所有更新文件之后，写入新的updateFile
			_lastVersionFile = _versionFileOnServer;
			WriteVersionFileToDisk();
			return;
		}
		Debug.Log("Begin download update files "+_needUpdateFileList[index]);
		string version = _versionFileOnServer.files[_needUpdateFileList[index]].version;
		string downloadUrl = string.Format("{0}/{1}/{2}",_serverResult.downloadBaseUrl,version,_needUpdateFileList[index]);
		DownloadMgr.instance.Download(downloadUrl,OnUpdateFileDownloadFinised);
	}

	void OnUpdateFileDownloadFinised(WWW www)
	{
		if(www == null)
		{
			//如果下载失败，等待1s后重新下载，可以是个逐渐增长的等待时间
			DownloadUpdateFiles(_downloadIndex,1.0f);
			return;
		}
		byte[] content = www.bytes;
		string writePath = string.Format("{0}/{1}",DownloadConfig.downLoadPath,_needUpdateFileList[_downloadIndex]);
		if(WriteUpdateFileToDisk(writePath,content))
		{
			_downloadIndex += 1;
			DownloadUpdateFiles(_downloadIndex);
		}
	}

	bool WriteUpdateFileToDisk(string path,byte[] content)
	{
		try
		{
			string dir = path.Substring(0,path.LastIndexOf("/"));
			if(!Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}
			FileStream fs =new FileStream(path,FileMode.OpenOrCreate);
			fs.Write(content,0,content.Length);
			fs.Flush();
			fs.Close();
			fs.Dispose();
			return true;
		}
		catch(Exception e)
		{
			Debug.Log("write download file to disk exception " + e.ToString());
		}
		return false;
	}

	void WriteVersionFileToDisk()
	{
		string str = VersionHelper.ConvertVersionFileToString(_versionFileOnServer);
		string versionFilePath = string.Format("{0}/{1}",DownloadConfig.downLoadPath,VersionConfig.VersionFileName);
		File.WriteAllText(versionFilePath,str,System.Text.Encoding.UTF8);	
	}

	public bool CheckFileIsInVersionFile(string path)
	{
		if(_lastVersionFile != null && _lastVersionFile.files.ContainsKey(path))
		{
			return true;
		}
		return false;
	}
}
}