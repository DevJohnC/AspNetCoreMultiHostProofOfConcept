using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProofOfConcept;

var builder = WebApplication.CreateBuilder();
// replace Kestrel with a server that doesn't listen for any connections
builder.Services.RemoveAll<IServer>();
builder.Services.AddSingleton<IServer, NullServer>();
var app1 = builder.Build();
// routing middleware needs to be manually added - assuming Microsoft did something custom with endpoint matching in the regular stack
app1.UseRouting();
app1.MapGet("/", () => "Hello App1!");
// application needs to be started to hydrate the app and run hosted services
await app1.StartAsync();

builder = WebApplication.CreateBuilder();
// replace Kestrel with a server that doesn't listen for any connections
builder.Services.RemoveAll<IServer>();
builder.Services.AddSingleton<IServer, NullServer>();
var app2 = builder.Build();
// routing middleware needs to be manually added - assuming Microsoft did something custom with endpoint matching in the regular stack
app2.UseRouting();
app2.MapGet("/", () => "Hello App2!");
// application needs to be started to hydrate the app and run hosted services
await app2.StartAsync();

var hostBuilder = WebApplication.CreateSlimBuilder(args);
hostBuilder.Services.AddSingleton(new RoutingMiddleware(
    RouteTableEntry.Create("/app1", app1),
    RouteTableEntry.Create("/app2", app2)));

var hostApp = hostBuilder.Build();

hostApp.UseMiddleware<RoutingMiddleware>();

hostApp.MapGet("/", () => "Hello World!");

await hostApp.RunAsync();

class RoutingMiddleware : IMiddleware
{
    private readonly RouteTableEntry _pipeline1;
    private readonly RouteTableEntry _pipeline2;

    public RoutingMiddleware(RouteTableEntry pipeline1, RouteTableEntry pipeline2)
    {
        _pipeline1 = pipeline1;
        _pipeline2 = pipeline2;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments(_pipeline1.PathBase))
            await RouteToApp(context, _pipeline1);
        else if (context.Request.Path.StartsWithSegments(_pipeline2.PathBase))
            await RouteToApp(context, _pipeline2);
        else
            await next(context);
    }

    private static async Task RouteToApp(HttpContext callingContext, RouteTableEntry routeTableEntry)
    {
        var routedContextFactory = routeTableEntry.ServiceProvider.GetRequiredService<IHttpContextFactory>();

        var httpContextFeatures = new HttpContextFeatures();
        
        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(httpContextFeatures);
        features.Set<IQueryFeature>(httpContextFeatures);
        features.Set<IFormFeature>(httpContextFeatures);
        features.Set<IRequestCookiesFeature>(httpContextFeatures);
        features.Set<IRouteValuesFeature>(httpContextFeatures);
        features.Set<IRequestBodyPipeFeature>(httpContextFeatures);
        var routedContext = routedContextFactory.Create(features);
        try
        {
            CopyRequestProperties(callingContext.Request, routedContext.Request);
            
            await using var scope = routeTableEntry.ServiceProvider.CreateAsyncScope();
            
            callingContext.RequestServices = scope.ServiceProvider;
            callingContext.Request.PathBase = routeTableEntry.PathBase;
            callingContext.Request.Path = callingContext.Request.Path.Value?.Substring(routeTableEntry.PathBase.Length);

            await routeTableEntry.RequestDelegate(callingContext);
        }
        finally
        {
            routedContextFactory.Dispose(routedContext);
        }
    }

    private static void CopyRequestProperties(HttpRequest from, HttpRequest into)
    {
        into.Method = from.Method;
        into.Scheme = from.Scheme;
        into.IsHttps = from.IsHttps;
        into.Host = from.Host;
        into.PathBase = from.PathBase;
        into.Path = from.Path;
        into.QueryString = from.QueryString;
        into.Query = from.Query;
        into.Protocol = from.Protocol;
        into.Cookies = from.Cookies;
        into.ContentLength = from.ContentLength;
        into.ContentType = from.ContentType;
        into.Body = from.Body;
        into.RouteValues = from.RouteValues;
        
        // this will cause the form to be read, that should be made lazy
        if (from.HasFormContentType)
            into.Form = from.Form;

        CopyHeaderValues(from.Headers, into.Headers);
    }

    private static void CopyHeaderValues(IHeaderDictionary from, IHeaderDictionary into)
    {
        foreach (var (key, values) in from)
        {
            into.Append(key, values);
        }
    }
}

class RouteTableEntry
{
    public required RequestDelegate RequestDelegate { get; init; }
    
    public required string PathBase { get; init; }
    
    public required IServiceProvider ServiceProvider { get; init; }
    
    public required IFeatureCollection FeatureCollection { get; init; }

    public static RouteTableEntry Create(string pathBase, IApplicationBuilder applicationBuilder)
    {
        return new RouteTableEntry
        {
            PathBase = pathBase,
            RequestDelegate = applicationBuilder.Build(),
            ServiceProvider = applicationBuilder.ApplicationServices,
            FeatureCollection = applicationBuilder.ServerFeatures
        };
    }
}