﻿//--------------------------------------------------
// Motion Framework
// Copyright©2019-2020 何冠峰
// Licensed under the MIT license
//--------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using MotionFramework.Resource;
using MotionFramework.AI;
using MotionFramework.Event;

namespace MotionFramework.Patch
{
	public class PatchManager : IGameModule, IBundleServices
	{
		public static readonly PatchManager Instance = new PatchManager();

		private readonly ProcedureSystem _system = new ProcedureSystem();
		private const string StrStaticFileName = "static.bytes";
		private string _strCDNServerIP;
		private string _strWebServerIP;

		/// <summary>
		/// CDN地址
		/// </summary>
		public string StrCDNServerIP
		{
			get
			{
				if (string.IsNullOrEmpty(_strCDNServerIP))
					throw new Exception("Web server ip is null or empty");
				return _strCDNServerIP;
			}
			set
			{
				_strCDNServerIP = value;
			}
		}

		/// <summary>
		/// WEB地址
		/// </summary>
		public string StrWebServerIP
		{
			get
			{
				if (string.IsNullOrEmpty(_strWebServerIP))
					throw new Exception("Web server ip is null or empty");
				return _strWebServerIP;
			}
			set
			{
				_strWebServerIP = value;
			}
		}

		/// <summary>
		/// 是否跳过CDN服务器
		/// </summary>
		public bool SkipCDN = false;
		
		/// <summary>
		/// 下载列表
		/// </summary>
		public readonly List<PatchElement> DownloadList = new List<PatchElement>(1000);
		
		// 版本号和补丁文件
		public Version AppVersion { private set; get; }
		public Version GameVersion { private set; get; }
		public PatchFile AppPatchFile { private set; get; }
		public PatchFile SandboxPatchFile { private set; get; }
		public PatchFile WebPatchFile { private set; get; }


		private PatchManager()
		{
		}
		public void Awake()
		{
			string appVersion = GetAPPVersion();
			AppVersion = new Version(appVersion);
		}
		public void Start()
		{
			// 注意：按照先后顺序添加流程节点
			_system.AddProcedure(new FsmPatchPrepare(_system));
			_system.AddProcedure(new FsmCheckSandboxDirty(_system));
			_system.AddProcedure(new FsmParseAppPatchFile(_system));
			_system.AddProcedure(new FsmParseSandboxPatchFile(_system));
			_system.AddProcedure(new FsmRequestGameVersion(_system));
			_system.AddProcedure(new FsmParseWebPatchFile(_system));
			_system.AddProcedure(new FsmGetDonwloadList(_system));
			_system.AddProcedure(new FsmDownloadWebFiles(_system));
			_system.AddProcedure(new FsmDownloadWebFilesFinish(_system));
			_system.AddProcedure(new FsmPatchOver(_system));
			_system.AddProcedure(new FsmPatchError(_system));
			_system.Run();
		}
		public void Update()
		{
			_system.Update();
		}
		public void LateUpdate()
		{
		}
		public void OnGUI()
		{
		}


		/// <summary>
		/// 获取APP版本号
		/// </summary>
		public string GetAPPVersion()
		{
#if UNITY_EDITOR
			return Application.version;
#elif UNITY_IPHONE
		return Application.version;
#elif UNITY_ANDROID
		return Application.version;
#elif UNITY_STANDALONE
		return Application.version;
#endif
		}

		/// <summary>
		/// 获取游戏版本号
		/// </summary>
		public string GetGameVersion()
		{
			if (GameVersion == null)
				return string.Empty;
			return GameVersion.ToString();
		}

		/// <summary>
		/// 初始化游戏版本号
		/// </summary>
		public void InitGameVesion(string version)
		{
			if (GameVersion != null)
				throw new Exception("Should never get here.");
			GameVersion = new Version(version);
		}

		/// <summary>
		/// 修复客户端
		/// </summary>
		public void FixClient()
		{
			// 清空缓存
			ClearSandbox();

			// 重启游戏
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}

		public void ParseAppPatchFile(string fileContent)
		{
			if (AppPatchFile != null)
				throw new Exception("Should never get here.");
			AppPatchFile = new PatchFile();
			AppPatchFile.Parse(fileContent);
		}
		public void ParseSandboxPatchFile(string fileContent)
		{
			if (SandboxPatchFile != null)
				throw new Exception("Should never get here.");
			SandboxPatchFile = new PatchFile();
			SandboxPatchFile.Parse(fileContent);
		}
		public void ParseSandboxPatchFile(PatchFile patchFile)
		{
			if (SandboxPatchFile != null)
				throw new Exception("Should never get here.");
			SandboxPatchFile = patchFile;
		}
		public void ParseWebPatchFile(string fileContent)
		{
			if (WebPatchFile != null)
				throw new Exception("Should never get here.");
			WebPatchFile = new PatchFile();
			WebPatchFile.Parse(fileContent);
		}

		#region IBundleServices接口
		private AssetBundleManifest _manifest;
		private AssetBundleManifest LoadManifest()
		{
			string loadPath = GetAssetBundleLoadPath(PatchDefine.StrManifestFileName);
			AssetBundle bundle = AssetBundle.LoadFromFile(loadPath);
			if (bundle == null)
				return null;

			AssetBundleManifest result = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
			bundle.Unload(false);
			return result;
		}
		public string GetAssetBundleLoadPath(string manifestPath)
		{
			PatchFile patchFile;
			if (WebPatchFile != null)
				patchFile = WebPatchFile;
			else
				patchFile = SandboxPatchFile;

			// 注意：可能从APP内加载，也可能从沙盒内加载
			PatchElement element;
			if (patchFile.Elements.TryGetValue(manifestPath, out element))
			{
				// 先查询APP内的资源
				PatchElement appElement;
				if (AppPatchFile.Elements.TryGetValue(manifestPath, out appElement))
				{
					if (appElement.MD5 == element.MD5)
						return AssetPathHelper.MakeStreamingLoadPath(manifestPath);
				}

				// 如果APP里不存在或者MD5不匹配，则从沙盒里加载
				return AssetPathHelper.MakePersistentLoadPath(manifestPath);
			}
			else
			{
				PatchManager.Log(ELogType.Warning, $"Not found bundle in package : {manifestPath}");
				return AssetPathHelper.MakeStreamingLoadPath(manifestPath);
			}
		}
		public string[] GetDirectDependencies(string assetBundleName)
		{
			if (_manifest == null)
				_manifest = LoadManifest();
			return _manifest.GetDirectDependencies(assetBundleName);
		}
		public string[] GetAllDependencies(string assetBundleName)
		{
			if (_manifest == null)
				_manifest = LoadManifest();
			return _manifest.GetAllDependencies(assetBundleName);
		}
		#endregion

		#region 静态公共方法
		// 文件操作相关
		public static string ReadFile(string filePath)
		{
			if (File.Exists(filePath) == false)
				return string.Empty;
			return File.ReadAllText(filePath, Encoding.UTF8);
		}
		public static void CreateFile(string filePath, string content)
		{
			// 删除旧文件
			if (File.Exists(filePath))
				File.Delete(filePath);

			// 创建文件夹路径
			CreateFileDirectory(filePath);

			// 创建新文件
			byte[] bytes = Encoding.UTF8.GetBytes(content);
			using (FileStream fs = File.Create(filePath))
			{
				fs.Write(bytes, 0, bytes.Length);
				fs.Flush();
				fs.Close();
			}
		}
		public static void CreateFileDirectory(string filePath)
		{
			// If the destination directory doesn't exist, create it.
			string destDirectory = Path.GetDirectoryName(filePath);
			if (Directory.Exists(destDirectory) == false)
				Directory.CreateDirectory(destDirectory);
		}

		/// <summary>
		/// 输出日志
		/// </summary>
		public static void Log(ELogType logType, string log)
		{
			LogSystem.Log(logType, log);
		}

		/// <summary>
		/// 清空沙盒目录
		/// </summary>
		public static void ClearSandbox()
		{
			string directoryPath = AssetPathHelper.MakePersistentLoadPath(string.Empty);
			Directory.Delete(directoryPath, true);
		}

		/// <summary>
		/// 获取沙盒内静态文件的路径
		/// </summary>
		public static string GetSandboxStaticFilePath()
		{
			return AssetPathHelper.MakePersistentLoadPath(StrStaticFileName);
		}

		/// <summary>
		/// 检测沙盒内静态文件是否存在
		/// </summary>
		public static bool CheckSandboxStaticFileExist()
		{
			string filePath = GetSandboxStaticFilePath();
			return File.Exists(filePath);
		}

		/// <summary>
		/// 检测沙盒内补丁文件是否存在
		/// </summary>
		public static bool CheckSandboxPatchFileExist()
		{
			string filePath = AssetPathHelper.MakePersistentLoadPath(PatchDefine.StrPatchFileName);
			return File.Exists(filePath);
		}

		/// <summary>
		/// 检测沙盒内清单文件是否存在
		/// </summary>
		public static bool CheckSandboxManifestFileExist()
		{
			string filePath = AssetPathHelper.MakePersistentLoadPath(PatchDefine.StrManifestFileName);
			return File.Exists(filePath);
		}

		/// <summary>
		/// 获取网络文件下载地址
		/// </summary>
		public static string MakeWebDownloadURL(string version, string fileName)
		{
			if (Application.platform == RuntimePlatform.Android)
				return $"{PatchManager.Instance.StrCDNServerIP}/Android/{version}/{fileName}";
			else if (Application.platform == RuntimePlatform.IPhonePlayer)
				return $"{PatchManager.Instance.StrCDNServerIP}/IPhone/{version}/{fileName}";
			else
				return $"{PatchManager.Instance.StrCDNServerIP}/Standalone/{version}/{fileName}";
		}
		#endregion

		#region 补丁事件相关
		public static void SendPatchStatesChangeMsg(EPatchStates currentStates)
		{
			PatchEventMessageDefine.PatchStatesChange msg = new PatchEventMessageDefine.PatchStatesChange();
			msg.CurrentStates = currentStates;
			EventManager.Instance.SendMessage(EPatchEventMessageTag.PatchManagerEvent.ToString(), msg);
		}
		public static void SendFoundNewAPPMsg(string newVersion)
		{
			PatchEventMessageDefine.FoundNewAPP msg = new PatchEventMessageDefine.FoundNewAPP();
			msg.NewVersion = newVersion;
			EventManager.Instance.SendMessage(EPatchEventMessageTag.PatchManagerEvent.ToString(), msg);
		}
		public static void SendDownloadFilesProgressMsg(int totalDownloadCount, int currentDownloadCount, long totalDownloadSizeKB, long currentDownloadSizeKB)
		{
			PatchEventMessageDefine.DownloadFilesProgress msg = new PatchEventMessageDefine.DownloadFilesProgress();
			msg.TotalDownloadCount = totalDownloadCount;
			msg.CurrentDownloadCount = currentDownloadCount;			
			msg.TotalDownloadSizeKB = totalDownloadSizeKB;
			msg.CurrentDownloadSizeKB = currentDownloadSizeKB;
			EventManager.Instance.SendMessage(EPatchEventMessageTag.PatchManagerEvent.ToString(), msg);
		}
		public static void SendWebFileDownloadFailedMsg(string filePath)
		{
			PatchEventMessageDefine.WebFileDownloadFailed msg = new PatchEventMessageDefine.WebFileDownloadFailed();
			msg.FilePath = filePath;
			EventManager.Instance.SendMessage(EPatchEventMessageTag.PatchManagerEvent.ToString(), msg);
		}
		public static void SendWebFileMD5VerifyFailedMsg(string filePath)
		{
			PatchEventMessageDefine.WebFileMD5VerifyFailed msg = new PatchEventMessageDefine.WebFileMD5VerifyFailed();
			msg.FilePath = filePath;
			EventManager.Instance.SendMessage(EPatchEventMessageTag.PatchManagerEvent.ToString(), msg);
		}
		public static void SendPatchOverMsg()
		{
			PatchEventMessageDefine.PatchOver msg = new PatchEventMessageDefine.PatchOver();
			EventManager.Instance.SendMessage(EPatchEventMessageTag.PatchManagerEvent.ToString(), msg);
		}
		#endregion
	}
}