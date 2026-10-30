// 구역 데이터 로더 — Assets/Data/*.json → Napolitan.Core.SiteData
// JSON은 camelCase 필드 + 문자열 enum("condition")이라 JsonUtility 대신 Newtonsoft 사용

using System;
using Napolitan.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Napolitan.Game.Data
{
    public static class SiteLoader
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            Converters = { new StringEnumConverter(new CamelCaseNamingStrategy(), allowIntegerValues: false) },
            // 오타 난 필드명이 조용히 무시되지 않도록
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        /// <summary>JSON 문자열 파싱. 게임에서는 TextAsset.text를 넘긴다</summary>
        public static SiteData Parse(string json)
        {
            var site = JsonConvert.DeserializeObject<SiteData>(json, Settings);
            if (site == null) throw new FormatException("구역 데이터가 비어 있음");
            return site;
        }
    }
}
