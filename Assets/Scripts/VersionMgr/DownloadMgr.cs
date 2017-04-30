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
		
	public void Download(string url,Action<WWW> downloadFinishedCallback,float delay = -1)
	{
		StartCoroutine(DownloadCoroutine(url,downloadFinishedCallback,delay));
	}

	IEnumerator DownloadCoroutine(string url,Action<WWW> downloadFinishedCallback,float delay)
	{
		if(delay > 0)
			yield return new WaitForSeconds(delay);
		WWW www = new WWW(url);
		yield return www;
		if(string.IsNullOrEmpty(www.error) && www.isDone)
		{
			if(downloadFinishedCallback != null)
			{
				downloadFinishedCallback(www);
			}
		}
		else
		{
			Debug.Log("Download failed "+www.error);
			downloadFinishedCallback(null);
		}
		www.Dispose();
	}
}
