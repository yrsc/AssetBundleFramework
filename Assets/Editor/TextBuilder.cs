using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.IO;
namespace AssetBundleFramework
{ 
public class TextBuilder : Editor  
{
	//存放文本文件的文件夹
	public static List<string> textFolder = new List<string>()
	{
		"StaticData",
	};

	[MenuItem("AssetBundle/BuildText")]
	public static void BuildText()
	{
		for(int i = 0; i < textFolder.Count;i++)
		{
			string path = Application.dataPath + "/" + textFolder[i];
			if(!Directory.Exists(path))
			{
				Debug.LogError(string.Format("textFolder {0} not exist",textFolder[i]));
			}
			else
			{							
				DirectoryInfo dir = new DirectoryInfo(path);
				FileInfo []files = dir.GetFiles("*", SearchOption.AllDirectories);			
				for (int j = 0; j < files.Length; j++)
				{		
					if(CheckFileSuffixNeedIgnore(files[j].Name))
						continue;		
					string relativePath = GetRelativePathToAssets(files[j].FullName);
					string exportPath = string.Format("{0}/{1}",Application.streamingAssetsPath,relativePath);				
					string exportPathDir =  exportPath.Substring(0,exportPath.LastIndexOf("/"));
					if(!Directory.Exists(exportPathDir))
					{
						Directory.CreateDirectory(exportPathDir);
					}
					files[j].CopyTo(exportPath,true);
				}	
			}
		}
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
	}

	static string GetRelativePathToAssets(string path)
	{
		return path.Substring(Application.dataPath.Length);
	}

	static bool CheckFileSuffixNeedIgnore(string fileName)
	{
		if(fileName.EndsWith(".meta") || fileName.EndsWith(".DS_Store"))
			return true;
		return false;
	}
}
}