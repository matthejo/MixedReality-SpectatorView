﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Microsoft.MixedReality.SpectatorView
{
    internal class AssetService : Singleton<AssetService>, IAssetCacheUpdater
    {
        private readonly Dictionary<ShortID, IAssetSerializer<Texture>> textureSerializers = new Dictionary<ShortID, IAssetSerializer<Texture>>();

        public void RegisterTextureSerializer(IAssetSerializer<Texture> textureSerializer)
        {
            textureSerializers.Add(textureSerializer.GetID(), textureSerializer);
        }

        public bool TrySerializeTexture(BinaryWriter writer, Texture texture)
        {
            foreach (KeyValuePair<ShortID, IAssetSerializer<Texture>> serializerPair in textureSerializers)
            {
                if (serializerPair.Value.CanSerialize(texture))
                {
                    writer.Write(serializerPair.Key.Value);
                    serializerPair.Value.Serialize(writer, texture);
                    return true;
                }
            }

            var textureAssets = TextureAssetCache.Instance;
            if (textureAssets != null)
            {
                if (textureAssets.CanSerialize(texture))
                {
                    writer.Write(textureAssets.GetID().Value);
                    textureAssets.Serialize(writer, texture);
                    return true;
                }
            }

            writer.Write((ushort)0);
            return false;
        }

        public bool TryDeserializeTexture(BinaryReader reader, out Texture texture)
        {
            ShortID shortID = new ShortID(reader.ReadUInt16());

            IAssetSerializer<Texture> textureSerializer;
            if (textureSerializers.TryGetValue(shortID, out textureSerializer))
            {
                texture = textureSerializer.Deserialize(reader);
                return true;
            }
            else
            {
                var textureAssets = TextureAssetCache.Instance;
                if (textureAssets != null && textureAssets.GetID() == shortID)
                {
                    texture = textureAssets.Deserialize(reader);
                    return true;
                }
                else
                {
                    texture = null;
                    return false;
                }
            }
        }

        public AssetId GetMeshId(Mesh mesh)
        {
            var meshAssets = MeshAssetCache.Instance;
            if (meshAssets == null)
            {
                return AssetId.Empty;
            }
            else
            {
                return meshAssets.GetAssetId(mesh);
            }
        }

        public bool AttachMeshFilter(GameObject gameObject, AssetId assetId)
        {
            var meshAssets = MeshAssetCache.Instance;

            if (meshAssets == null)
            {
                return false;
            }

            ComponentExtensions.EnsureComponent<MeshRenderer>(gameObject);

            Mesh mesh = meshAssets.GetAsset(assetId);
            if (mesh != null)
            {
                MeshFilter filter = ComponentExtensions.EnsureComponent<MeshFilter>(gameObject);
                filter.sharedMesh = mesh;
                return true;
            }

            return false;
        }

        public bool AttachSkinnedMeshRenderer(GameObject gameObject, AssetId assetId)
        {
            var meshAssets = MeshAssetCache.Instance;

            if (meshAssets == null)
            {
                return false;
            }

            Mesh mesh = meshAssets.GetAsset(assetId);
            if (mesh != null)
            {
                SkinnedMeshRenderer renderer = ComponentExtensions.EnsureComponent<SkinnedMeshRenderer>(gameObject);
                renderer.sharedMesh = mesh;
                return true;
            }

            return false;
        }

        public void UpdateAssetCache()
        {
            TextureAssetCache.GetOrCreateAssetCache<TextureAssetCache>().UpdateAssetCache();
            MeshAssetCache.GetOrCreateAssetCache<MeshAssetCache>().UpdateAssetCache();
            MaterialPropertyAssetCache.GetOrCreateAssetCache<MaterialPropertyAssetCache>().UpdateAssetCache();
            CustomShaderPropertyAssetCache.GetOrCreateAssetCache<CustomShaderPropertyAssetCache>().UpdateAssetCache();
        }

        public void ClearAssetCache()
        {
            TextureAssetCache.GetOrCreateAssetCache<TextureAssetCache>().ClearAssetCache();
            MeshAssetCache.GetOrCreateAssetCache<MeshAssetCache>().ClearAssetCache();
            MaterialPropertyAssetCache.GetOrCreateAssetCache<MaterialPropertyAssetCache>().ClearAssetCache();
            CustomShaderPropertyAssetCache.GetOrCreateAssetCache<CustomShaderPropertyAssetCache>().ClearAssetCache();
        }

        public void SaveAssets()
        {
            AssetCache.GetOrCreateAssetCache<TextureAssetCache>().SaveAssets();
            AssetCache.GetOrCreateAssetCache<MeshAssetCache>().SaveAssets();
            AssetCache.GetOrCreateAssetCache<MaterialPropertyAssetCache>().SaveAssets();
            AssetCache.GetOrCreateAssetCache<CustomShaderPropertyAssetCache>().SaveAssets();
        }
    }
}