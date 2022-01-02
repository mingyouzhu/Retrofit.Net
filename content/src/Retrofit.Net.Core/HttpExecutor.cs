using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Retrofit.Net.Core.Builder;
using Retrofit.Net.Core.Extensions;
using Retrofit.Net.Core.Interceptors;
using Retrofit.Net.Core.Models;
using Retrofit.Net.Core.Params;
using System.Net.Http.Headers;

namespace Retrofit.Net.Core
{
    public class HttpExecutor : IChain
    {
        Request _request;
        MethodBuilder _method;
        RetrofitClient _retrofitClient;
        public HttpExecutor(MethodBuilder method,RetrofitClient client)
        {
            _method = method;
            _retrofitClient = client;
        }

        public Response<dynamic> Execute()
        {
            _request = new Request().NewBuilder()
                .AddMethod(_method.Method)
                .AddRequestUrl(_method.Path!)
                .Build();
            var interceptor = _retrofitClient.Interceptors.FirstOrDefault();
            return interceptor!.Intercept(this);
        }

        public Response<dynamic> Proceed(Request request)
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage? requestMessage = null;
            if (request.Method == Method.GET)
            {
                var requestUrl = GetParams(_request.RequestUrl,_method.Parameters);
                requestMessage = new HttpRequestMessage(HttpMethod.Get,requestUrl);
            }
            else if (request.Method == Method.POST)
            {
                requestMessage = new HttpRequestMessage(HttpMethod.Post, request.RequestUrl);
                HttpContent? content = GetParams(_method.Parameters);
                requestMessage.Content = content;
            }else if (request.Method == Method.PUT)
            {
                var requestUrl = GetParams(request.RequestUrl, _method.Parameters);
                HttpContent? content = GetParams(_method.Parameters);
                requestMessage = new HttpRequestMessage(HttpMethod.Put, requestUrl);
                requestMessage.Content = content;
            }else if (request.Method == Method.DELETE)
            {
                var requestUrl = GetParams(request.RequestUrl, _method.Parameters);
                HttpContent? content = GetParams(_method.Parameters);
                requestMessage = new HttpRequestMessage(HttpMethod.Delete, requestUrl);
                requestMessage.Content = content;
            }
            
            foreach (var item in request.Headers)
            {
                requestMessage?.Headers.Add(item.Key, item.Value);
            }
            
            HttpResponseMessage responseMessage = client.Send(requestMessage!);
            Response<dynamic> response = new Response<dynamic>();
            string json = JsonConvert.SerializeObject(responseMessage.Headers);
            response.Message = responseMessage.ReasonPhrase;
            response.StatusCode = Convert.ToInt32(responseMessage.StatusCode);
            response.Body = responseMessage.Content.ReadAsStringAsync().Result;
            response.Headers = JsonConvert.DeserializeObject<IEnumerable<KeyValuePair<string, object>>>(json);
            return response;
        }

        public Request Request() => _request;

        string GetParams(string baseUrl,IList<Param>? _params)
        {
            if (baseUrl.Contains("{"))baseUrl = baseUrl[0..baseUrl.LastIndexOf("{")];
            for (int i = 0; i < _params?.Count; i++)
            {
                var param = _params[i];
                if(param.Kind == ParamKind.Query)
                {
                    if(baseUrl.Contains('?') is false)baseUrl += "?";
                    baseUrl += $"{param.Name}={param.Value}";
                    if(i < (_params!.Count - 1))baseUrl += "&";
                }
                else if(param.Kind == ParamKind.Path)baseUrl += param.Value;
            }
            return baseUrl;
        }

        HttpContent? GetParams(IList<Param>? _params)
        {
            HttpContent? response = null;
            if (_params is null || (_params?.Any() ?? false) is false)return null;
            IList<Param> collection = _params.Where(param => param.Kind != ParamKind.Path && param.Kind != ParamKind.Query).ToList();
            if(collection.Count < 1)return null;
            Param first = collection.First();
            Type valueType = first.GetType();
            IList<KeyValuePair<string, dynamic>>? fields = null;
            if(valueType.IsClass)fields = ConvertExtensions.GetProperties(first.Value);
            if(first.Kind == ParamKind.Body)
            {
                JObject obj = new JObject();
                if(fields is not null)
                {
                    foreach (var item in fields)
                    {
                        obj.Add(item.Key, item.Value);
                    }
                }
                else
                {
                    obj.Add(first.Name, first.Value);
                }
                if(collection.Count > 1)
                {
                    foreach(var item in collection.Skip(1))
                    {
                        obj.Add(item.Name, item.Value);
                    }
                }
                response = new StringContent(obj.ToString());
                response.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }
            else if(first.Kind == ParamKind.Form)
            {
                MultipartFormDataContent content = new MultipartFormDataContent();
                if(fields is not null)
                {
                    foreach (var item in fields)
                    {
                        if(item.Value?.GetType() != typeof(FieldFile))
                        {
                            content.Add(new StringContent(item.Value),item.Key);
                        }
                        else
                        {
                            FieldFile? file = (item.Value as FieldFile);
                            string? path = file?.FilePath;
                            content.Add(new ByteArrayContent(File.ReadAllBytes(path ?? "")),item.Key, file?.FileName ?? "");
                        }
                    }
                }
                else
                {
                    content.Add(new StringContent(first.Name,first.Value));
                }
                if(collection.Count > 1)
                {
                    foreach(var item in collection.Skip(1))
                    {
                        content.Add(new StringContent(item.Name, item.Value));
                    }
                }
                response = content;
            }
            return response;
        }
    }
}