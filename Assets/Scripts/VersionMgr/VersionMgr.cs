using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

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

	public VersionMgr()
	{
		GetClientVersion();
	}

	private UpdateFile _lastUpdateFile = null;
	void GetClientVersion()
	{
		_appVersion = VersionConfig.app_version;

		string updateFile = string.Format("{0}/{1}",DownloadConfig.downLoadPath,VersionConfig.updateFileName);
		//获得上一次客户端更新到的版本
		string content = SimpleLoader.LoadText(updateFile);
		if(string.IsNullOrEmpty(content))
		{
			_resVersion = VersionConfig.res_version;
		}
		else
		{
			VersionHelper.ParseUpdateFile(content,ref _lastUpdateFile);
			_resVersion = _lastUpdateFile.resVersion;
		}
	}

	public void StartCheckVersion()
	{
		_serverResult = CheckVersionFromServer(_appVersion,_resVersion);
		if(_serverResult.versionType == VersionType.App)
		{
			//提示下载新的APP，新的下载地址result.downloadUrl
			return;
		}
		else if(_serverResult.versionType == VersionType.Res)
		{
			DownloadUpdateFile();
		}
		else if(_serverResult.versionType == VersionType.None)
		{
			//验证通过，直接进入游戏
		}
	}

	VersionResultFromServer CheckVersionFromServer(string appVersion,string resVersion)
	{
		VersionResultFromServer result = new VersionResultFromServer();
		result.downloadBaseUrl = "";
		result.versionType = VersionType.None;
		result.serverResVersion = "";
		//服务器传回来的下载地址假设是xx.xx.xxx.xx,我们需要根据平台重新生成一下，最终为xx.xx.xxx.xx/iOS 或者xx.xx.xxx.xx/Android
		result.downloadBaseUrl  = ConnectDownloadUrlWithPlatform(result.downloadBaseUrl);
		return result;
	}

	string ConnectDownloadUrlWithPlatform(string url)
	{
		#if UNITY_IOS
		return url + "/iOS";
		#elif UNITY_ANDROID
		return url + "/Android";
		#endif
	}

	void DownloadUpdateFile()
	{
		//首先下载updateFile文件，然后从updateFile文件里面下载需要更新的资源
		string updateFileDownUrl = string.Format("{0}/{1}/{2}",_serverResult.downloadBaseUrl,_serverResult.serverResVersion,VersionConfig.updateFileName);
		DownloadMgr.instance.Download<string>(updateFileDownUrl,OnUpdateFileDownload);
	}

	private UpdateFile _downloadUpdateFile = null;
	private List<string> _needUpdateFileList = new List<string>();
	private int _downloadIndex = 0;
	void OnUpdateFileDownload(string txt)
	{		
		VersionHelper.ParseUpdateFile(txt,ref _downloadUpdateFile);
		GenerateNeedUpdateFile();
		DownloadUpdateFiles(_downloadIndex);
	}

	void GenerateNeedUpdateFile()
	{
		_needUpdateFileList.Clear();
		if(_downloadUpdateFile == null)
		{
			return;
		}
		Dictionary<string,UpdateFileInfo> _allFileDic = new Dictionary<string, UpdateFileInfo>();
		if(_lastUpdateFile == null)
		{
			_allFileDic = _downloadUpdateFile.files;
		}
		else
		{
			//通过比较2次的updateFile生成需要更新的文件
			foreach(KeyValuePair<string,UpdateFileInfo> kvp in _downloadUpdateFile.files)
			{
				UpdateFileInfo lastFileInfo = null;
				_lastUpdateFile.files.TryGetValue(kvp.Key,out lastFileInfo);
				if(lastFileInfo == null || lastFileInfo.md5 != kvp.Value.md5)
				{
					_allFileDic[kvp.Key] = kvp.Value;
				}
			}
		}
		foreach(KeyValuePair<string,UpdateFileInfo> kvp in _allFileDic)
		{
			string downloadPath = string.Format("{0}/{1}",DownloadConfig.downLoadPath,kvp.Key);
			if(File.Exists(downloadPath))
			{
				string md5 = VersionHelper.GetMd5Val(downloadPath);
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

	void DownloadUpdateFiles(int index)
	{
		if(index >= _needUpdateFileList.Count)
		{
			//下载完所有更新文件之后，写入新的updateFile
			WriteUpdateFileTxt();
			return;
		}
		string version = _downloadUpdateFile.files[_needUpdateFileList[index]].version;
		string downloadUrl = string.Format("{0}/{1}/{2}",_serverResult.downloadBaseUrl,version,_needUpdateFileList[index]);
		DownloadMgr.instance.Download<byte[]>(downloadUrl,OnFileDownloadFinised);
	}

	void OnFileDownloadFinised(byte[] content)
	{
		string writePath = string.Format("{0}/{1}",DownloadConfig.downLoadPath,_needUpdateFileList[_downloadIndex]);
		if(WriteFileToDisk(writePath,content))
		{
			_downloadIndex += 1;
			DownloadUpdateFiles(_downloadIndex);
		}
	}

	bool WriteFileToDisk(string path,byte[] content)
	{
		try
		{
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

	void WriteUpdateFileTxt()
	{
		string str = VersionHelper.ConvertUpdateFileToString(_downloadUpdateFile);
		string updateFilePath = string.Format("{0}/{1}",DownloadConfig.downLoadPath,VersionConfig.updateFileName);
		File.WriteAllText(updateFilePath,str,System.Text.Encoding.UTF8);	

	}
}
