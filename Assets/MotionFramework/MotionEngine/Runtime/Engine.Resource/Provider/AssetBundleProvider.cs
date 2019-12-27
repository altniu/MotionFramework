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
		private AssetBundleLoader _owner;
		private AssetBundleRequest _cacheRequest;

		public string AssetName { private set; get; }
		public System.Type AssetType { private set; get; }
		public System.Object AssetObject { private set; get; }
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
		public bool IsValid
		{
			get
			{
				return _owner.IsDestroy == false;
			}
		}

		public AssetBundleProvider(AssetFileLoader owner, string assetName, System.Type assetType)
		{
			_owner = owner as AssetBundleLoader;
			AssetName = assetName;
			AssetType = assetType;
			States = EAssetProviderStates.None;
			Handle = new AssetOperationHandle(this);
		}
		public void Update()
		{
			if (IsDone)
				return;

			if (_owner.CacheBundle == null)
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
					_cacheRequest = _owner.CacheBundle.LoadAssetAsync(AssetName);
				else
					_cacheRequest = _owner.CacheBundle.LoadAssetAsync(AssetName, AssetType);
				States = EAssetProviderStates.Checking;
			}

			// 2. 检测加载结果
			if (States == EAssetProviderStates.Checking)
			{
				if (_cacheRequest.isDone == false)
					return;
				AssetObject = _cacheRequest.asset;
				States = AssetObject == null ? EAssetProviderStates.Failed : EAssetProviderStates.Succeed;
				if (States == EAssetProviderStates.Failed)
					LogSystem.Log(ELogType.Warning, $"Failed to load asset object : {_owner.LoadPath} : {AssetName}");
				Callback?.Invoke(Handle);
			}
		}
	}
}