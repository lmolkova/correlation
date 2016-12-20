using System.Net.Http;

namespace Microsoft.Extensions.Correlation
{
    public interface IOutgoingRequestNotifier
    {
        void OnBeforeRequest(HttpRequestMessage request);
        void OnAfterResponse(HttpResponseMessage response);
    }
}
