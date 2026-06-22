using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
 
namespace NoteVault.Services;
 
public class RazorViewRenderer
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
 
    public RazorViewRenderer(
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IServiceProvider serviceProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
    }
 
    public async Task<string> RenderAsync<TModel>(string viewName, TModel model)
    {
        var actionContext = new ActionContext(
            _httpContextAccessor.HttpContext!,
            new RouteData(),
            new ActionDescriptor());
 
        var viewResult = _viewEngine.GetView( null,  viewName,  false);
        if (!viewResult.Success)
        {
            throw new InvalidOperationException(
                $"Could not find view '{viewName}'. Searched: {string.Join(", ", viewResult.SearchedLocations)}");
        }
 
        var viewData = new ViewDataDictionary<TModel>(
            metadataProvider: new EmptyModelMetadataProvider(),
            modelState: new ModelStateDictionary())
        {
            Model = model
        };
 
        var tempData = new TempDataDictionary(actionContext.HttpContext, _tempDataProvider);
 
        await using var writer = new StringWriter();
        var viewContext = new ViewContext(
            actionContext, viewResult.View, viewData, tempData, writer, new HtmlHelperOptions());
 
        await viewResult.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}