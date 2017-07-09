using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.IO;

namespace AssetBundleFramework
{
public class AssetNode
{
	public List<AssetNode> parents = new List<AssetNode>();
	public string path;
	public int depth = 0;
}

public class AssetBundleBuilder : Editor 
{	
	public static string AssetBundle_Path = Application.streamingAssetsPath;

	//需要打包的资源路径（相对于Assets目录），通常是prefab,lua,及其他数据。（贴图，动画，模型，材质等可以通过依赖自己关联上，不需要添加在该路径里，除非是特殊需要）
	//注意这里是目录，单独零散的文件，可以新建一个目录，都放在里面打包
	public static List<string> abResourcePath = new List<string>()
	{
		//"Examples/Prefab",
		"Prefabs",
	};
		
		
	[MenuItem("AssetBundle/BuildScene")]
	static void BuildScenesAssetBundles()
	{
		string outPath = AssetBundle_Path + "/Scenes/";
		if (Directory.Exists(outPath))
		{
		}
		else
		{
			DirectoryInfo newDirect = Directory.CreateDirectory(outPath);

		}
		//获取buildsetting里面enable的场景
		foreach (EditorBuildSettingsScene e in EditorBuildSettings.scenes)
		{
			if(e==null)
				continue;				
			if (e.enabled)
			{
				string levelName = e.path.Substring (e.path.LastIndexOf ("/") + 1);
				BuildPipeline.BuildPlayer(new string[1]{e.path}, outPath + levelName, EditorUserBuildSettings.activeBuildTarget, BuildOptions.BuildAdditionalStreamedScenes);			
			}
		}
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
	}

	[MenuItem("AssetBundle/BuildAssetbundle")]
	public static void BuildAssetBundle()
	{
		Init();
		CollectDependcy();
		BuildResourceBuildMap();
		BuildAssetBundleWithBuildMap();
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();	
	}

	static void Init()
	{
		_buildMap.Clear();
		_leafNodes.Clear();
		_allAssetNodes.Clear();
	}

	private static List<AssetNode> _leafNodes = new List<AssetNode>();
	private static Dictionary<string,AssetNode> _allAssetNodes = new Dictionary<string, AssetNode>();
	private static List<string> _buildMap = new List<string>();

	static void CollectDependcy()
	{		
		for(int i = 0; i < abResourcePath.Count;i++)
		{
			string path = Application.dataPath + "/" + abResourcePath[i];
			if(!Directory.Exists(path))
			{
				Debug.LogError(string.Format("abResourcePath {0} not exist",abResourcePath[i]));
			}
			else
			{
				DirectoryInfo dir = new DirectoryInfo(path);
				FileInfo []files = dir.GetFiles("*", SearchOption.AllDirectories);			
				for (int j = 0; j < files.Length; j++)
				{		
					if(CheckFileSuffixNeedIgnore(files[j].Name))
						continue;	
					//获得在文件相对于Assets下的目录，类似于Assets/Prefab/UI/xx.prefab
					string fileRelativePath = GetReleativeToAssets(files[j].FullName);
					AssetNode root = null;
					//文件可能在上一个文件的依赖关系中被处理了
					_allAssetNodes.TryGetValue(fileRelativePath,out root);
					if(root == null)
					{
						root = new AssetNode();
						root.path = fileRelativePath;
						_allAssetNodes[root.path] = root;
						GetDependcyRecursive(fileRelativePath,root);
					}
				}	
			}
		}
		//PrintDependcy();
	}

	static void GetDependcyRecursive(string path,AssetNode parentNode)
	{
		string []dependcy = AssetDatabase.GetDependencies(path,false);
		for(int i = 0; i < dependcy.Length; i++)
		{
			AssetNode node = null;
			_allAssetNodes.TryGetValue(dependcy[i],out node);
			if(node == null)
			{
				node = new AssetNode();
				node.path = dependcy[i];
				node.depth = parentNode.depth + 1;
				node.parents.Add(parentNode);
				_allAssetNodes[node.path] = node;
				GetDependcyRecursive(dependcy[i],node);
			}
			else
			{
				if(!node.parents.Contains(parentNode))
				{
					node.parents.Add(parentNode);
				}
				if(node.depth < parentNode.depth + 1)
				{
					node.depth = parentNode.depth + 1;
					GetDependcyRecursive(dependcy[i],node);
				}
				
			}
			//Debug.Log("dependcy path is " +dependcy[i] + " parent is " + parentNode.path);
		}
		if(dependcy.Length == 0)
		{			
			if(!_leafNodes.Contains(parentNode))
			{
				_leafNodes.Add(parentNode);
			}
		}
	}
		
	//按照依赖关系的深度，从最底层往上遍历打包，如果一个叶子节点有多个父节点，则该叶子节点被多个资源依赖，该叶子节点需要打包，如果一个节点没有
	//父节点，则该叶子节点是最顶层的文件（prefab,lua...），需要打包。
	static void BuildResourceBuildMap()
	{
		int maxDepth = GetMaxDepthOfLeafNodes();
		while(_leafNodes.Count > 0)
		{
			List<AssetNode> _curDepthNodesList = new List<AssetNode>();
			for(int i = 0; i < _leafNodes.Count;i++)
			{
				if(_leafNodes[i].depth == maxDepth)
				{
					//如果叶子节点有多个父节点或者没有父节点,打包该叶子节点
					if(_leafNodes[i].parents.Count != 1)
					{
						if(!ShouldIgnoreFile(_leafNodes[i].path))
						{
							_buildMap.Add(_leafNodes[i].path);
						}
					}
					_curDepthNodesList.Add(_leafNodes[i]);
				}
			}
			//删除已经遍历过的叶子节点，并把这些叶子节点的父节点添加到新一轮的叶子节点中
			for(int i = 0; i < _curDepthNodesList.Count;i++)
			{
				_leafNodes.Remove(_curDepthNodesList[i]);
				foreach(AssetNode node in _curDepthNodesList[i].parents)
				{
					if(!_leafNodes.Contains(node))
					{
						_leafNodes.Add(node);
					}
				}
			}
			maxDepth -= 1;
		}
	}

	static bool ShouldIgnoreFile(string path)
	{
		if(path.EndsWith(".cs"))
			return true;
		return false;
	}

	static int GetMaxDepthOfLeafNodes()
	{
		if(_leafNodes.Count == 0)
			return 0;
		_leafNodes.Sort((x,y)=> 
			{
				return y.depth - x.depth;
			});
		return _leafNodes[0].depth;
	}

	static void BuildAssetBundleWithBuildMap()
	{
		string prefix = "Assets";
		AssetBundleBuild[] buildMapArray = new AssetBundleBuild[_buildMap.Count];
		for(int i = 0;i < _buildMap.Count;i++)
		{
			buildMapArray[i].assetBundleName = _buildMap[i].Substring(prefix.Length+1);
			buildMapArray[i].assetNames = new string[]{_buildMap[i]};
			Debug.Log(_buildMap[i]);
		}
		if(!Directory.Exists(AssetBundle_Path))
			Directory.CreateDirectory(AssetBundle_Path);
		BuildPipeline.BuildAssetBundles(AssetBundle_Path, buildMapArray, BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.DeterministicAssetBundle, EditorUserBuildSettings.activeBuildTarget);	
	}		

	static string GetReleativeToAssets(string fullName)
	{
		//获得在文件在Assets下的目录，类似于Assets/path/of/yourfile
		string fileRelativePath = fullName.Substring(Application.dataPath.Length-6);
		//如果在windows平台下运行，需要替换路径中的\为/;
		fileRelativePath = 	fileRelativePath.Replace("\\","/");
		return fileRelativePath;
	}

	static bool CheckFileSuffixNeedIgnore(string fileName)
	{
		if(fileName.EndsWith(".meta") || fileName.EndsWith(".DS_Store") || fileName.EndsWith(".cs"))
			return true;
		return false;
	}
	}
}