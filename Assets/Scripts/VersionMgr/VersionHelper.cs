using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class VersionConfig
{
	public static string app_version = "1.0";
	public static string res_version = "170423.0";
	public static string updateFileName = "UpdateFile.txt";

}

public class UpdateFileInfo
{
	private string _md5;
	public string md5
	{
		get{return _md5;}
	}

	private string _version;
	public string version
	{
		get{return _version;}
	}

	public UpdateFileInfo(string md5Val,string versionVal)
	{
		_md5 = md5Val;
		_version = versionVal;
	}
}

public class UpdateFile
{
	public Dictionary<string,UpdateFileInfo> files = new Dictionary<string, UpdateFileInfo>();
	public string resVersion = "0.0";
}

public class VersionHelper
{
	public static void ParseUpdateFile(string txt,ref UpdateFile updateFile)
	{
		string[] content = txt.Split('\n');
		if(content != null)
		{
			if(updateFile == null)
				updateFile = new UpdateFile();
			for(int i = 0; i < content.Length - 1; i++)
			{
				if(!string.IsNullOrEmpty(content[i]))
				{
					string []kvp = content[i].Split(',');
					if(kvp == null || kvp.Length < 3)
					{
						Debug.LogError("Can not parse last update file with content " + content[i]);
						return;
					}
					updateFile.files[kvp[0]] = new UpdateFileInfo(kvp[1],kvp[2]);
				}
			}
			updateFile.resVersion = content[content.Length - 1];
		}
	}

	public static string ConvertUpdateFileToString(UpdateFile updateFile)
	{
		StringBuilder sb = new StringBuilder();		
		foreach(KeyValuePair<string,UpdateFileInfo> kvp in updateFile.files)
		{
			string content = string.Format("{0},{1},{2}\n",kvp.Key,kvp.Value.md5,kvp.Value.version);
			sb.Append(content);
		}
		sb.Append(updateFile.resVersion);
		return sb.ToString();
	}

	public static string GetMd5Val(string path)
	{
		FileStream file = new FileStream(path, FileMode.Open);
		System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
		byte[] retVal = md5.ComputeHash(file);
		file.Close();
		StringBuilder sb = new StringBuilder();
		for (int j = 0; j < retVal.Length; j++)
		{
			sb.Append(retVal[j].ToString("x2"));
		}
		return sb.ToString();
	}
}
