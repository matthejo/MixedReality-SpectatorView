﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Microsoft.MixedReality.SpectatorView.Editor
{
    public static class StateSynchronizationMenuItems
    {
        private const string ResourcesDirectoryName = "Resources";
        private static IEqualityComparer<IAssetCacheUpdater> assetTypeComparer = new AssetCacheTypeEqualityComparer();

        private class AssetCacheTypeEqualityComparer : IEqualityComparer<IAssetCacheUpdater>
        {
            public bool Equals(IAssetCacheUpdater x, IAssetCacheUpdater y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }
                if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
                {
                    return false;
                }

                return x.GetType().Equals(y.GetType());
            }

            public int GetHashCode(IAssetCacheUpdater obj)
            {
                return obj.GetType().GetHashCode();
            }
        }

        private static IEnumerable<IAssetCacheUpdater> GetAllAssetCaches()
        {
            var assetCaches = AssetCache.EnumerateAllComponentsInScenesAndPrefabs<IAssetCacheUpdater>();
            return assetCaches.Distinct(assetTypeComparer);
        }

        [MenuItem("Spectator View/Update All Asset Caches", priority = 100)]
        public static void UpdateAllAssetCaches()
        {
            bool assetCacheFound = false;

            foreach (IAssetCacheUpdater assetCache in GetAllAssetCaches())
            {
                Debug.Log($"Updating asset cache {assetCache.GetType().Name}...");
                assetCache.UpdateAssetCache();
                assetCache.SaveAssets();
                assetCacheFound = true;
            }

            if (!assetCacheFound)
            {
                Debug.LogWarning("No asset caches were found in the project. Unable to update asset caches.");
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Asset caches updated.");
        }

        [MenuItem("Spectator View/Clear All Asset Caches", priority = 101)]
        public static void ClearAllAssetCaches()
        {
            bool assetCacheFound = false;

            foreach (IAssetCacheUpdater assetCache in GetAllAssetCaches())
            {
                Debug.Log($"Clearing asset cache {assetCache.GetType().Name}...");
                assetCache.ClearAssetCache();
                assetCacheFound = true;
            }

            if (!assetCacheFound)
            {
                Debug.LogWarning("No asset caches were found in the project. Unable to clear asset caches.");
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Asset caches cleared.");
        }

        private struct AssetBundleInfo
        {
            public AssetBundlePlatform AssetBundlePlatform;
            public BuildTargetGroup BuildTargetGroup;
            public BuildTarget BuildTarget;
        }

        [MenuItem("Spectator View/Generate iOS Asset Bundles", validate = true)]
        public static bool ValidateIOSAssetBundles()
        {
#if UNITY_EDITOR_OSX
            return true;
#else
            return false;
#endif
        }

        [MenuItem("Spectator View/Generate Android Asset Bundles", validate = true)]
        public static bool ValidateAndroidAssetBundles()
        {
#if UNITY_EDITOR_OSX
            return false;
#else
            return true;
#endif
        }

        [MenuItem("Spectator View/Generate HoloLens Asset Bundles", validate = true)]
        public static bool ValidateHoloLensAssetBundles()
        {
#if UNITY_EDITOR_OSX
            return false;
#else
            return true;
#endif
        }

        [MenuItem("Spectator View/Generate iOS Asset Bundles", priority = 202)]
        public static void GenerateIOSAssetBundles()
        {
            BuildAssetBundleForPlatform(new AssetBundleInfo
            {
                AssetBundlePlatform = AssetBundlePlatform.iOS,
                BuildTargetGroup = BuildTargetGroup.iOS,
                BuildTarget = BuildTarget.iOS,
            });
        }

        [MenuItem("Spectator View/Generate Android Asset Bundles", priority = 203)]
        public static void GenerateAndroidAssetBundles()
        {
            BuildAssetBundleForPlatform(new AssetBundleInfo
            {
                AssetBundlePlatform = AssetBundlePlatform.Android,
                BuildTargetGroup = BuildTargetGroup.Android,
                BuildTarget = BuildTarget.Android,
            });
        }

        [MenuItem("Spectator View/Generate HoloLens Asset Bundles", priority = 204)]
        public static void GenerateHoloLensAssetBundles()
        {
            BuildAssetBundleForPlatform(new AssetBundleInfo
            {
                AssetBundlePlatform = AssetBundlePlatform.WSA,
                BuildTargetGroup = BuildTargetGroup.WSA,
                BuildTarget = BuildTarget.WSAPlayer,
            });
        }

        private static void BuildAssetBundleForPlatform(AssetBundleInfo bundleInfo)
        {
            var currentBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var currentBuildTarget = EditorUserBuildSettings.activeBuildTarget;

            BuildAssetBundleOptions bundleOptions = BuildAssetBundleOptions.None
                | BuildAssetBundleOptions.DeterministicAssetBundle
                | BuildAssetBundleOptions.ForceRebuildAssetBundle;

            string directoryPath = Path.Combine(
                                Application.dataPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar),
                                AssetCache.AssetCacheDirectory,
                                ResourcesDirectoryName,
                                bundleInfo.AssetBundlePlatform.ToString()
                                );

            string assetIntermediatePath = Path.Combine(directoryPath, StateSynchronizationObserver.AssetBundleName);
            string assetFinalPath = $"{assetIntermediatePath}.bytes";

            string manifestIntermediatePath = $"{assetIntermediatePath}.manifest";
            string manifestFinalPath = $"{manifestIntermediatePath}.bytes";

            string versionFinalPath = $"{assetIntermediatePath}.version.asset";
            string versionProjectRelativeFinalPath = "Assets" + versionFinalPath.Substring(Application.dataPath.Length);

            string resourcePath = Path.Combine(directoryPath, bundleInfo.AssetBundlePlatform.ToString());

            Directory.CreateDirectory(directoryPath);

            EditorUserBuildSettings.SwitchActiveBuildTarget(bundleInfo.BuildTargetGroup, bundleInfo.BuildTarget);
            var bundleManifest = BuildPipeline.BuildAssetBundles(directoryPath, bundleOptions, bundleInfo.BuildTarget);

            var bundleVersion = ScriptableObject.CreateInstance<AssetBundleVersion>();
            bundleVersion.Identity = bundleManifest.GetAssetBundleHash(StateSynchronizationObserver.AssetBundleName).ToString().ToLowerInvariant();
            bundleVersion.DisplayName = $"{PlayerSettings.productName} - {System.DateTime.Now:g}";

            File.Delete(resourcePath);
            File.Delete($"{resourcePath}.manifest");

            File.Delete(assetFinalPath);
            File.Delete(manifestFinalPath);
            File.Delete(versionFinalPath);

            if (File.Exists(assetIntermediatePath))
            {
                File.Move(assetIntermediatePath, assetFinalPath);
                File.Move(manifestIntermediatePath, manifestFinalPath);
                AssetDatabase.CreateAsset(bundleVersion, versionProjectRelativeFinalPath);
                Debug.Log($"Completed generating asset bundle for platform {bundleInfo.AssetBundlePlatform}");
            }
            else
            {
                Debug.LogError($"Expected that asset bundle {assetIntermediatePath} was generated, but it does not exist");
            }

            // Restore the previously-used build target after building the asset bundle
            EditorUserBuildSettings.SwitchActiveBuildTarget(currentBuildTargetGroup, currentBuildTarget);
        }

        [MenuItem("Spectator View/Edit Global Performance Parameters", priority = 300)]
        internal static void EditGlobalPerformanceParameters()
        {
            GameObject prefab = Resources.Load<GameObject>(StateSynchronizationSceneManager.DefaultStateSynchronizationPerformanceParametersPrefabName);
            if (prefab == null)
            {
                GameObject hierarchyPrefab = new GameObject(StateSynchronizationSceneManager.DefaultStateSynchronizationPerformanceParametersPrefabName);
                hierarchyPrefab.AddComponent<DefaultStateSynchronizationPerformanceParameters>();

                AssetCache.EnsureAssetDirectoryExists();
                prefab = PrefabUtility.SaveAsPrefabAsset(hierarchyPrefab, AssetCache.GetAssetCachePath(StateSynchronizationSceneManager.DefaultStateSynchronizationPerformanceParametersPrefabName, ".prefab"));
                Object.DestroyImmediate(hierarchyPrefab);
            }

            AssetDatabase.OpenAsset(prefab);
        }

        [MenuItem("Spectator View/Edit Custom Network Services", priority = 301)]
        private static void EditCustomShaderProperties()
        {
            GameObject prefab = Resources.Load<GameObject>(StateSynchronizationSceneManager.CustomBroadcasterServicesPrefabName);
            if (prefab == null)
            {
                GameObject hierarchyPrefab = new GameObject(StateSynchronizationSceneManager.CustomBroadcasterServicesPrefabName);

                AssetCache.EnsureAssetDirectoryExists();
                prefab = PrefabUtility.SaveAsPrefabAsset(hierarchyPrefab, AssetCache.GetAssetCachePath(StateSynchronizationSceneManager.CustomBroadcasterServicesPrefabName, ".prefab"));
                Object.DestroyImmediate(hierarchyPrefab);
            }

            AssetDatabase.OpenAsset(prefab);
        }

        [MenuItem("Spectator View/Edit Settings", priority = 302)]
        private static void EditCustomSettingsProperties()
        {
            GameObject prefab = Resources.Load<GameObject>(SpectatorView.SettingsPrefabName);
            GameObject hierarchyPrefab = null;
            if (prefab == null)
            {
                hierarchyPrefab = new GameObject(SpectatorView.SettingsPrefabName);
                hierarchyPrefab.AddComponent<BroadcasterSettings>();
                hierarchyPrefab.AddComponent<SpatialLocalizationInitializationSettings>();
                hierarchyPrefab.AddComponent<MobileRecordingSettings>();
                hierarchyPrefab.AddComponent<NetworkConfigurationSettings>();

                AssetCache.EnsureAssetDirectoryExists();
                prefab = PrefabUtility.SaveAsPrefabAsset(hierarchyPrefab, AssetCache.GetAssetCachePath(SpectatorView.SettingsPrefabName, ".prefab"));
                Object.DestroyImmediate(hierarchyPrefab);
            }
            else
            {
                GameObject editablePrefab = PrefabUtility.LoadPrefabContents(AssetCache.GetAssetCachePath(SpectatorView.SettingsPrefabName, ".prefab"));
                EnsureComponent<BroadcasterSettings>(editablePrefab);
                EnsureComponent<SpatialLocalizationInitializationSettings>(editablePrefab);
                EnsureComponent<MobileRecordingSettings>(editablePrefab);
                EnsureComponent<NetworkConfigurationSettings>(editablePrefab);
                PrefabUtility.SaveAsPrefabAsset(editablePrefab, AssetCache.GetAssetCachePath(SpectatorView.SettingsPrefabName, ".prefab"));
                PrefabUtility.UnloadPrefabContents(editablePrefab);
            }

            AssetDatabase.OpenAsset(prefab);
        }

        private static void EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null)
            {
                component = go.AddComponent<T>();
            }
        }
    }
}
