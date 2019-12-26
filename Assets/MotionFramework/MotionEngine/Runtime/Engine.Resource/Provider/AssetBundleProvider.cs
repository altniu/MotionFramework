﻿//--------------------------------------------------
// Motion Framework
// Copyright©2018-2020 何冠峰
// Licensed under the MIT license
//--------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MotionFramework.Resource
{
	internal class AssetBundleProvider : IAssetProvider
	{
		private string _loadPath;
		private AssetBundleRequest _cacheRequest;

		public string AssetName { private set; get; }
		public System.Type AssetType { private set; get; }
		public System.Object Result { private set; get; }
		public EAssetProviderStates States { private set; get; }
		public AssetOperationHandle Handle { private set; get; }
		public System.Action<AssetOperationHandle> Callback { set; get; }
		public float Progress
		{
			get
			{
				if (_cacheRequest == null)
					return 0;
				return _cacheRequest.progress;
			}
		}
		public bool IsDone
		{
			get
			{
				return States == EAssetProviderStates.Succeed || States == EAssetProviderStates.Failed;
			}
		}

		/// <summary>
		/// 缓存的AssetBundle对象
		/// 注意：需要在Update调用之前赋值
		/// </summary>
		internal AssetBundle CacheBundle { set; get; }

		public AssetBundleProvider(string loadPath, string assetName, System.Type assetType)
		{
			_loadPath = loadPath;
			AssetName = assetName;
			AssetType = assetType;
			States = EAssetProviderStates.None;
			Handle = new AssetOperationHandle(this);
		}
		public void Update()
		{
			if (IsDone)
				return;

			if (CacheBundle == null)
			{
				States = EAssetProviderStates.Failed;
				Callback?.Invoke(Handle);
			}

			if (States == EAssetProviderStates.None)
			{
				States = EAssetProviderStates.Loading;
			}

			// 1. 加载资源对象
			if (States == EAssetProviderStates.Loading)
			{
				if (AssetType == null)
					_cacheRequest = CacheBundle.LoadAssetAsync(AssetName);
				else
					_cacheRequest = CacheBundle.LoadAssetAsync(AssetName, AssetType);
				States = EAssetProviderStates.Checking;
			}

			// 2. 检测加载结果
			if (States == EAssetProviderStates.Checking)
			{
				if (_cacheRequest.isDone == false)
					return;
				Result = _cacheRequest.asset;
				States = Result == null ? EAssetProviderStates.Failed : EAssetProviderStates.Succeed;
				if (States == EAssetProviderStates.Failed)
					LogSystem.Log(ELogType.Warning, $"Failed to load asset object : {_loadPath} : {AssetName}");
				Callback?.Invoke(Handle);
			}
		}
	}
}