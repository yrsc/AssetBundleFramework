using UnityEngine;
using System.Collections;
using System;

public static class DownloadConfig
{
	public static string downLoadPath = "";

	static DownloadConfig()
	{
		downLoadPath = Application.persistentDataPath;
	}
}

public class DownloadMgr : MonoBehaviour {

	public static DownloadMgr instance = null;

	void Awake()
	{
		instance = this;
	}
		
	public void Download<T>(string url,Action<T> downloadFinishedCallback)
	{
		StartCoroutine(DownloadCoroutine<T>(url,downloadFinishedCallback));
	}

	IEnumerator DownloadCoroutine<T>(string url,Action<T> downloadFinishedCallback)
	{
		WWW www = new WWW(url);
		yield return www;
		if(!string.IsNullOrEmpty(www.error) && www.isDone)
		{
			if(downloadFinishedCallback != null)
			{
//				if(typeof(T) == typeof(string))
//				{
//					downloadFinishedCallback(www.text);
//				}
//				else if(typeof(T) == typeof(byte[]))
//				{
//					downloadFinishedCallback(www.bytes);
//				}
//				else
//				{
//					Debug.LogError("undefined type " + typeof(T));
//				}
			}
		}
	}
}
