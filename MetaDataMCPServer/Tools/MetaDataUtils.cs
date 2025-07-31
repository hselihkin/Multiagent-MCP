// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace MetaDataMCPServer.Tools;

/// <summary>
/// A collection of utility methods for working with the meta data of asset or tags or streams.
/// </summary>
internal sealed class MetaDataUtils
{
    /// <summary>
    /// Get the list of tags or streams queried by a named entity.
    /// </summary>
    /// <param name="namedEntitySearchString">The search string.</param>
    /// <returns>A JSON structured result object containing list of meta data related to the search string.</returns>
    [KernelFunction("entities_by_name")]
    [Description("Get the list of tags or streams queried by a named entity search string.")]
    public static async Task<string> GetEntitiesByName(string namedEntitySearchString)
    {
        // Mock the response for now
        return """
            {"result":[{"name":"YRK1.PI_10.116.20.140_1961","entity":"Stream","id":"03c52dc0-e67a-b3fa-812d-3a0ccdf5dd76","displayName":"GE07.Bearing Shaft Temperature","score":2.351715564727783,"document":{"id":"03c52dc0-e67a-b3fa-812d-3a0ccdf5dd76","name":"PI_10.116.20.140_1961","description":"","disabled":false,"type":"Stream","location":null,"keywords":[],"ww_nametype":"PI_10.116.20.140_1961;<ww_type>Stream</ww_type>","dimension":"Unknown","fqn":"YRK1.PI_10.116.20.140_1961","source":"YRK1","streamtype":null,"engunit":"degree Celsius","alias":"GE07.Bearing Shaft Temperature","pointid":"1961","pointsource":"PI-SIM","pointtype":"Float32","step":"0","engunits":"°C","stream":"GE07.Bearing Shaft Temperature","datahubrole_allow_id":["1d322a2b-2c12-5d73-b818-ae34c1f66572","2f1d21b0-9547-5c45-97be-14bc01a7bea5","6c6b9965-8678-5582-8dfb-7011507bb44b"],"exdesc":null,"test3":null,"test2":null,"t1":null,"datahubrole_deny_id":[],"assettypes":[],"data5":null,"test2search":null,"contenttype":null,"ww_userid":null,"ww_contentsharemode":null,"contenttypes":[],"test5":null,"test4":null},"matchingFields":[]},{"name":"YRK1.PI_10.116.20.140_1962","entity":"Stream","id":"03c52dc0-e67a-b3fa-812d-3a0ccdf5dd77","displayName":"GE07.Bearing Shaft Temperature Text","score":2.351715564727783,"document":{"id":"03c52dc0-e67a-b3fa-812d-3a0ccdf5dd76","name":"PI_10.116.20.140_1961","description":"","disabled":false,"type":"Stream","location":null,"keywords":[],"ww_nametype":"PI_10.116.20.140_1961;<ww_type>Stream</ww_type>","dimension":"Unknown","fqn":"YRK1.PI_10.116.20.140_1962","source":"YRK1","streamtype":"String","engunit":"","alias":"GE07.Bearing Shaft Temperature Text","pointid":"1962","pointsource":"PI-SIM","pointtype":"Float32","step":"0","engunits":"°C","stream":"GE07.Bearing Shaft Temperature","datahubrole_allow_id":["1d322a2b-2c12-5d73-b818-ae34c1f66572","2f1d21b0-9547-5c45-97be-14bc01a7bea5","6c6b9965-8678-5582-8dfb-7011507bb44b"],"exdesc":null,"test3":null,"test2":null,"t1":null,"datahubrole_deny_id":[],"assettypes":[],"data5":null,"test2search":null,"contenttype":null,"ww_userid":null,"ww_contentsharemode":null,"contenttypes":[],"test5":null,"test4":null},"matchingFields":[]}]}
            """;
    }

}
