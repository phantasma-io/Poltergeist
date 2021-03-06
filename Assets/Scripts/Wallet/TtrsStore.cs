using UnityEngine;
using UnityEngine.Networking;
using Phantasma.SDK;
using LunarLabs.Parser;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// Parsing and storing data received from TTRS store.
public static class TtrsStore
{
    public static void Clear()
    {
        StoreNft.Clear();
    }

    public struct Nft
    {
        public string Id;
        public UInt64 Item; // "item": "371"
        public string Url; // "url": "http://www.22series.com/api/store/part_info?id=371"
        public string Img; // "img": "http://www.22series.com/api/store/part_img?id=371"
        public string NftType; // "type": "Item"
        public UInt64 Source; // "source": 0
        public UInt64 SourceData; // "source_data": 2
        public DateTime Timestamp; // "timestamp": 1581797657
        public UInt64 Mint; // "mint": 3

        public string NameEnglish; // "name_english": "Akuna Front Spoiler (Carbon Fibre)"
        public string Make; // "make": "Kaya"
        public string Model; // "model": "Akuna"
        public string Part; // "part": "Front Spoiler"
        public string Material; // "material": "Aluminium"
        public string ImageUrl; // "image_url": "http://www.22series.com/api/store/part_img?id=371"
        public string DescriptionEnglish; // "description_english": "Make: Kaya<br/>Model: Akuna<br/>Part: Aluminium Front Spoiler<br/>Aerodynamic Adjustable<br/>Finish: Clear (High Gloss)<br/>Part No: KA-3301-AERO-SP-FR-Carbon-Fibre"
        public string DisplayTypeEnglish; // "display_type_english": "Part"
        public UInt64 ItemDefId; // "itemdefid": 371
        public UInt64 Season; // "season": 1
        public UInt64 Rarity; // "rarity": 3
        public string BodyPart; // "body_part": "AeroSpoilerFront"
        public string ModelAsset; // "model_asset": "ka-3301-aero-sp-fr-carbon-fibre"
        public string Type; // "type": "kaya akuna"
        public string ParentTypes; // "parent_types": "kaya akuna"
        public string Series; // "series": ""
        public string Extra; // "extra": "Aerodynamic Adjustable"
        public string Color; // "color": "Clear"
        public string Finish; // "finish": "High Gloss"
        public UInt64 MintLimit; // "mint_limit": 0
    }

    private static Hashtable StoreNft = new Hashtable();


    public static bool CheckIfNftLoaded(string id)
    {
        return StoreNft.Contains(id);
    }

    public static Nft GetNft(string id)
    {
        return StoreNft.Contains(id) ? (Nft)StoreNft[id] : new Nft();
    }

    private static void LoadStoreNftFromDataNode(DataNode storeNft, Action<Nft> callback)
    {
        if (storeNft == null)
        {
            return;
        }

        foreach (DataNode item in storeNft.Children)
        {
            var currentId = item.Name;

            var nft = GetNft(currentId);
            var newNft = String.IsNullOrEmpty(nft.Id); // There's no such NFT in StoreNft yet.

            nft.Id = currentId;
            nft.Item = item.GetUInt32("item");
            nft.Url = item.GetString("url");
            nft.Img = item.GetString("img");
            nft.NftType = item.GetString("type");
            nft.Source = item.GetUInt32("source");
            nft.SourceData = item.GetUInt32("source_data");
            nft.Timestamp = item.GetDateTime("timestamp");
            nft.Mint = item.GetUInt32("mint");

            var itemInfo = item.GetNode("item_info");
            
            nft.NameEnglish = itemInfo.GetString("name_english");
            nft.Make = itemInfo.GetString("make");
            nft.Model = itemInfo.GetString("model");
            nft.Part = itemInfo.GetString("part");
            nft.Material = itemInfo.GetString("material");
            nft.ImageUrl = itemInfo.GetString("image_url");
            nft.DescriptionEnglish = itemInfo.GetString("description_english");
            nft.DisplayTypeEnglish = itemInfo.GetString("display_type_english");
            nft.ItemDefId = itemInfo.GetUInt32("itemdefid");
            nft.Season = itemInfo.GetUInt32("season");
            nft.Rarity = itemInfo.GetUInt32("rarity");
            if (nft.Rarity == 5) // Fixing ttrs rarity gap.
                nft.Rarity = 4;
            nft.BodyPart = itemInfo.GetString("body_part");
            nft.ModelAsset = itemInfo.GetString("model_asset");
            nft.Type = itemInfo.GetString("type");
            nft.ParentTypes = itemInfo.GetString("parent_types");
            nft.Series = itemInfo.GetString("series");
            nft.Extra = itemInfo.GetString("extra");
            nft.Color = itemInfo.GetString("color");
            nft.Finish = itemInfo.GetString("finish");
            nft.MintLimit = itemInfo.GetUInt32("mint_limit");

            if(newNft)
                StoreNft.Add(currentId, nft);

            callback(nft);
        }
    }

    public static IEnumerator LoadStoreNft(string[] ids, Action<Nft> onItemLoadedCallback, Action onAllItemsLoadedCallback)
    {
        var url = "https://www.22series.com/api/store/nft";
        var storeNft = Cache.GetDataNode("ttrs-store-nft", Cache.FileType.JSON, 60 * 24);
        if (storeNft != null)
        {
            LoadStoreNftFromDataNode(storeNft, onItemLoadedCallback);

            // Checking, that cache contains all needed NFTs.
            string[] missingIds = ids;
            for (int i = 0; i < ids.Length; i++)
            {
                if (CheckIfNftLoaded(ids[i]))
                {
                    missingIds = missingIds.Where(x => x != ids[i]).ToArray();
                }
            }
            ids = missingIds;

            if (ids.Length == 0)
            {
                onAllItemsLoadedCallback();
                yield break;
            }
        }

        var idList = "";
        for (int i = 0; i < ids.Length; i++)
        {
            if (String.IsNullOrEmpty(idList))
                idList += "\"" + ids[i] + "\"";
            else
                idList += ",\"" + ids[i] + "\"";
        }

        yield return WebClient.RESTRequest(url, "{\"ids\":[" + idList + "]}", (error, msg) =>
        {
            Log.Write("LoadStoreNft() error: " + error);
        },
        (response) =>
        {
            if (response != null)
            {
                LoadStoreNftFromDataNode(response, onItemLoadedCallback);

                if (storeNft != null)
                {
                    // Cache already exists, need to add new nfts to existing cache.
                    foreach (var node in response.Children)
                    {
                        storeNft.AddNode(node);
                    }
                }
                else
                {
                    storeNft = response;
                }
                if (storeNft != null)
                    Cache.Add("ttrs-store-nft", Cache.FileType.JSON, DataFormats.SaveToString(DataFormat.JSON, storeNft));
            }
            onAllItemsLoadedCallback();
        });
    }

    private static string NftToString(Nft nft)
    {
        return "Item #: " + nft.Item + "\n" +
            "URL: " + nft.Url + "\n" +
            "Image: " + nft.Img + "\n" +
            "Type: " + nft.Type + "\n" +
            "Source: " + nft.Source + "\n" +
            "Source Data: " + nft.SourceData + "\n" +
            "Timestamp: " + nft.Timestamp + "\n" +
            "Mint: " + nft.Mint + "\n" +
            "Name (English): " + nft.NameEnglish + "\n" +
            "Make: " + nft.Make + "\n" +
            "Model: " + nft.Model + "\n" +
            "Part: " + nft.Part + "\n" +
            "Material: " + nft.Material + "\n" +
            "ImageUrl: " + nft.ImageUrl + "\n" +
            "Description (English): " + nft.DescriptionEnglish + "\n" +
            "Display Type (English): " + nft.DisplayTypeEnglish + "\n" +
            "ItemDefId: " + nft.ItemDefId + "\n" +
            "Season: " + nft.Season + "\n" +
            "Rarity: " + nft.Rarity + "\n" +
            "BodyPart: " + nft.BodyPart + "\n" +
            "ModelAsset: " + nft.ModelAsset + "\n" +
            "Type: " + nft.Type + "\n" +
            "Parent Types: " + nft.ParentTypes + "\n" +
            "Series: " + nft.Series + "\n" +
            "Extra: " + nft.Extra + "\n" +
            "Color: " + nft.Color + "\n" +
            "Finish: " + nft.Finish + "\n" +
            "MintLimit: " + nft.MintLimit;
    }

    public static void LogStoreNft()
    {
        for (int i = 0; i < StoreNft.Count; i++)
        {
            Log.Write(NftToString((Nft)StoreNft[i]));
        }
    }
}
