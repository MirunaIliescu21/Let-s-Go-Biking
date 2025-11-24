using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;


namespace ProxyCacheService.Caching
{
    /// <summary>
    /// Represents the textual content and metadata of a single HTTP GET request.
    /// Used by the URL-based cache inside "ProxyService".
    /// 
    /// This class encapsulates both successful and failed HTTP responses.
    /// For OpenRouteService (ORS) endpoints, it automatically adds the Bearer token
    /// from App.config. For JCDecaux endpoints, it relies on the proxy layer
    /// to append the apiKey query parameter.
    /// </summary>
    public class HttpGetResource
    {
        /// <summary>
        /// The full URL that was fetched.
        /// </summary>
        public string Url { get; private set; }
        /// <summary>
        /// The raw textual content returned by the server (JSON, XML, or plain text).
        /// In case of failure, this property contains a diagnostic string.
        /// </summary>
        public string Content { get; private set; }

        /// <summary>
        /// The UTC timestamp indicating when the content was retrieved or created.
        /// </summary>
        public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
        /// <summary>
        /// The HTTP status code of the response.
        /// 200 for success, other values for server responses, or -1 for local/network errors.
        /// </summary>
        public int StatusCode { get; private set; } = 200;

        /// <summary>
        /// Parameterless constructor required by <see cref="GenericProxyCache{T}"/>.
        /// </summary>
        public HttpGetResource() { }

        /// <summary>
        /// Initializes a new instance from pre-existing data (used for error or cached content).
        /// </summary>
        /// <param name="url">The request URL.</param>
        /// <param name="content">The content string (may be partial or error message).</param>
        public HttpGetResource(string url, string content)
        {
            Url = url ?? "";
            Content = content ?? "";
            CreatedUtc = DateTime.UtcNow;
            StatusCode = 200;
        }

        /// <summary>
        /// Initializes and immediately performs a synchronous HTTP GET request
        /// to retrieve the content from the specified URL.
        /// Automatically adds an ORS Bearer token if the URL targets
        /// <c>https://api.openrouteservice.org/</c>.
        /// </summary>
        /// <param name="url">The full HTTP URL to fetch.</param>
        public HttpGetResource(string url)
        {
            Url = url;

            // Modern TLS for ORS/JCDecaux APIs
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using (var http = new HttpClient())
            {
                try
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("ProxyCacheService/1.0 (+local)");

                    // Add Bearer only for ORS
                    if (url.StartsWith("https://api.openrouteservice.org/", StringComparison.OrdinalIgnoreCase))
                    {
                        var bearer = ConfigurationManager.AppSettings["ORS_BEARER"];
                        if (string.IsNullOrWhiteSpace(bearer))
                            throw new Exception("ORS_BEARER missing in ProxyCacheService App.config");

                        http.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", bearer);
                    }

                    // GetAsync + ReadAsStringAsync for full control over response.
                    // (with GetStringAsync we can't see non-200 status codes)
                    var resp = http.GetAsync(url).Result;
                    var body = resp.Content.ReadAsStringAsync().Result;

                    StatusCode = (int)resp.StatusCode; // Real status
                    CreatedUtc = DateTime.UtcNow;

                    if (!resp.IsSuccessStatusCode)
                    {
                        Content = $"(HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}) {body}";
                    }
                    else
                    {
                        Content = body;
                    }
                }
                catch (Exception ex)
                {
                    StatusCode = -1; // Local error (timeout, DNS), not server
                    Content = $"(Error (download error): {ex.Message})";
                    CreatedUtc = DateTime.UtcNow;
                }
            }
        }
    }
}