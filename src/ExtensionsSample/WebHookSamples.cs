using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.WebHooks;
using Microsoft.Azure.WebJobs.Host;

namespace ExtensionsSample
{
    public static class WebHookSamples
    {
        public static void HookA([WebHookTrigger("sample/hookb")] string body, TraceWriter trace)
        {
            trace.Info(string.Format("HookA invoked! Body: {0}", body));
        }

        public static async Task HookB([WebHookTrigger("sample/hookb")] HttpRequestMessage request, TraceWriter trace)
        {
            string body = await request.Content.ReadAsStringAsync();
            trace.Info(string.Format("HookB invoked! Body: {0}", body));
        }
    }
}
