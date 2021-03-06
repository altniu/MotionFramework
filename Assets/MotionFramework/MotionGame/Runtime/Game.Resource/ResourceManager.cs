﻿//--------------------------------------------------
// Motion Framework
// Copyright©2018-2020 何冠峰
// Licensed under the MIT license
//--------------------------------------------------
using System;
using UnityEngine;
using MotionFramework.Debug;

namespace MotionFramework.Resource
{
	/// <summary>
	/// 资源管理器
	/// </summary>
	public sealed class ResourceManager : IGameModule
	{
		public static readonly ResourceManager Instance = new ResourceManager();

		private ResourceManager()
		{
		}
		public void Awake()
		{
		}
		public void Start()
		{
		}
		public void Update()
		{
			AssetSystem.UpdatePoll();
		}
		public void LateUpdate()
		{
			AssetSystem.Release();
		}
		public void OnGUI()
		{
			int totalCount = AssetSystem.DebugGetFileLoaderCount();
			int failedCount = AssetSystem.DebugGetFileLoaderFailedCount();
			DebugConsole.GUILable($"[{nameof(ResourceManager)}] AssetSystemMode : {AssetSystem.SystemMode}");
			DebugConsole.GUILable($"[{nameof(ResourceManager)}] Asset loader total count : {totalCount}");
			DebugConsole.GUILable($"[{nameof(ResourceManager)}] Asset loader failed count : {failedCount}");
		}

		/// <summary>
		/// 同步加载接口
		/// 注意：仅支持无依赖关系的资源
		/// </summary>
		public T SyncLoad<T>(string location) where T : UnityEngine.Object
		{
			UnityEngine.Object result = null;

			if (AssetSystem.SystemMode == EAssetSystemMode.EditorMode)
			{
#if UNITY_EDITOR
				string loadPath = AssetSystem.FindDatabaseAssetPath(location);
				result = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(loadPath);
				if (result == null)
					LogSystem.Log(ELogType.Error, $"Failed to load {loadPath}");
#else
				throw new Exception("AssetDatabaseLoader only support unity editor.");
#endif
			}
			else if (AssetSystem.SystemMode == EAssetSystemMode.ResourcesMode)
			{
				result = Resources.Load<T>(location);
				if (result == null)
					LogSystem.Log(ELogType.Error, $"Failed to load {location}");
			}
			else if (AssetSystem.SystemMode == EAssetSystemMode.BundleMode)
			{
				string fileName = System.IO.Path.GetFileNameWithoutExtension(location);
				string manifestPath = AssetPathHelper.ConvertLocationToManifestPath(location);
				string loadPath = AssetSystem.BundleServices.GetAssetBundleLoadPath(manifestPath);
				AssetBundle bundle = AssetBundle.LoadFromFile(loadPath);
				if(bundle != null)
					result = bundle.LoadAsset<T>(fileName);
				if (result == null)
					LogSystem.Log(ELogType.Error, $"Failed to load {loadPath}");
				if(bundle != null)
					bundle.Unload(false);
			}
			else
			{
				throw new NotImplementedException($"{AssetSystem.SystemMode}");
			}

			return result as T;
		}
	}
}