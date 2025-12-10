using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;

namespace ProofOfConcept;

public sealed class HttpContextFeatures :
    IHttpRequestFeature,
    IQueryFeature,
    IFormFeature,
    IRequestCookiesFeature,
    IRouteValuesFeature,
    IRequestBodyPipeFeature
{
    public string Protocol { get; set; }
    public string Scheme { get; set; }
    public string Method { get; set; }
    public string PathBase { get; set; }
    public string Path { get; set; }
    public string QueryString { get; set; }
    public string RawTarget { get; set; }
    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
    public Stream Body { get; set; }
    public IQueryCollection Query { get; set; }
    public IFormCollection ReadForm()
    {
        throw new NotImplementedException();
    }

    public async Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public bool HasFormContentType { get; }
    public IFormCollection? Form { get; set; }
    public IRequestCookieCollection Cookies { get; set; }
    public RouteValueDictionary RouteValues { get; set; }
    public PipeReader Reader { get; }
}