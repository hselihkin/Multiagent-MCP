// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.SemanticKernel;

namespace TimeseriesMCPServer.Tools;

/// <summary>
/// A collection of utility methods for working with the stream data.
/// </summary>
internal sealed class TimeseriesUtils
{
    /// <summary>
    /// Get the timeseries values for a stream within a specific timerange.
    /// </summary>
    /// <param name="streamName">The name of the Stream.</param>
    /// <param name="startDateTime">The start datetime time in UTC.</param>
    /// <param name="endDateTime">The end datetime time in UTC.</param>
    /// <returns>A JSON structured result object containing the stream of data.</returns>
    [KernelFunction("values_by_stream_name")]
    [Description("Get the timeseries values for a Stream within a specific timerange - identified by a start date/time and an end date/time.")]
    public static async Task<string> GetTimeseriesValuesByStreamName(
        [Description("The name of the Stream or Tag.")] string streamName,
        [Description("The start datetime time in UTC.")] string startDateTime, 
        [Description("The end datetime time in UTC.")] string endDateTime
        )
    {
        string payLoad = """
            {"endDateTime":"2025-06-18T06:10:48.089+00:00","startDateTime":"2025-06-17T06:10:48.089+00:00","fqn":[],"expression":"UOM([YRK1.PI_10.116.20.151_1716],[None]) as [YRK1.PI_10.116.20.151_1716];UOM([YRK1.PI_10.116.20.151_1715],[None]) as [YRK1.PI_10.116.20.151_1715]"}
            """;

        //return await GetStreamProcessValues(payLoad);

        return """
            {"@odata.context":"https://dev.visualization.capdev-connect.aveva.com/apis/Historian/v2/$metadata#AnalogSummary(FQN,Last,Value,Maximum,Minimum,Unit,Average,Count)","value":[{"FQN":"GE07.Bearing.001","Unit":"degree Celsius","Last":24.996780395507812,"Minimum":22.444080352783203,"Maximum":45.43645095825195,"Average":29.77566732441307,"Count":39}]}
            """;
    }

    /// <summary>
    /// Get the timeseries values for a stream for the current datetime in UTC.
    /// </summary>
    /// <param name="streamName">The name of the Stream.</param>
    /// <param name="currentDateTimeInUtc">The current date time in UTC.</param>
    /// <returns>A JSON structured result object containing the stream of data.</returns>
    [KernelFunction("current_value_by_stream_name")]
    [Description("Get the current timeseries values for a Stream.")]
    public static string GetCurrentTimeseriesValuesByStreamName(
        [Description("The name of the Stream or Tag.")] string streamName,
        [Description("The start datetime time in UTC.")] string startDateTime,
        [Description("The end datetime time in UTC.")] string endDateTime
        )
    {
        Console.WriteLine($"GetCurrentTimeseriesValuesByStreamName called with streamName: {streamName}, startDateTime: {startDateTime}, endDateTime: {endDateTime}");
        return """
            {"@odata.context":"https://dev.visualization.capdev-connect.aveva.com/apis/Historian/v2/$metadata#AnalogSummary(FQN,Last,Value,Maximum,Minimum,Unit,Average,Count)","value":[{"FQN":"GE07.Bearing.001","Unit":"degree Celsius","Current":42.34}]}
            """;
    }

    public static async Task<string> GetStreamProcessValues(string strPayload, CancellationToken? cancellationToken = null)
    {


        var client_ = new HttpClient();
        client_.BaseAddress = new Uri($"https://dev.visualization.capdev-connect.aveva.com/apis/Historian/v2/ProcessValues");

        var disposeClient_ = false;

        using var requestcontent = new StringContent(strPayload, Encoding.UTF8, "application/json");

        var otherHeaders = new Dictionary<string, string>();
        try
        {
            var token = "eyJ";
            var solutionId = "329b7204-3f7c-4b94-84da-45de7f579ffb";
            using var request_ = new HttpRequestMessage();
            request_.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request_.Headers.Add("X-WWTenantId", solutionId);
            request_.Content = requestcontent;
            request_.Method = new HttpMethod("POST");
            request_.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
            var response_ = cancellationToken.HasValue ?
                await client_.SendAsync(request_, cancellationToken.Value).ConfigureAwait(continueOnCapturedContext: false) :
                await client_.SendAsync(request_).ConfigureAwait(continueOnCapturedContext: false);

            var disposeResponse_ = true;
            try
            {
                var headers_ = response_.Headers.ToDictionary((KeyValuePair<string, IEnumerable<string>> h_) => h_.Key, (KeyValuePair<string, IEnumerable<string>> h_) => h_.Value);
                if (response_.Content is { Headers: { } })
                {
                    foreach (var header in response_.Content.Headers)
                    {
                        headers_[header.Key] = header.Value;
                    }
                }

                var status_ = (int)response_.StatusCode;
                switch (response_.StatusCode)
                {
                    case HttpStatusCode.OK:
                        {
                            var textResponse = cancellationToken.HasValue ?
                                await response_.Content.ReadAsStringAsync(cancellationToken.Value).ConfigureAwait(continueOnCapturedContext: false) :
                                await response_.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
                            return textResponse;
                        }

                    default:
                        {
                            var text = (response_.Content != null) ? (cancellationToken.HasValue ?
                                await response_.Content.ReadAsStringAsync(cancellationToken.Value).ConfigureAwait(continueOnCapturedContext: false) :
                                await response_.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false)) : null;
                            var response = text;
                            throw new Exception();
                        }
                }
            }
            finally
            {
                if (disposeResponse_)
                {
                    response_.Dispose();
                }
            }
        }
        catch (ArgumentNullException)
        {
            return string.Empty;
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
        catch (TaskCanceledException)
        {
            return string.Empty;
        }
        finally
        {
            if (disposeClient_)
            {
                client_.Dispose();
            }
        }
    }
}
