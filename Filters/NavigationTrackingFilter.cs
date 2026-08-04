using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using InventarioTI.Services;

namespace InventarioTI.Filters;

// Registra en la pila de navegación (sesión) cada pantalla completa que el
// usuario visita, para que el link "Volver" pueda regresar a la pantalla
// anterior exacta. Solo pantallas de lectura vía GET; formularios excluidos
// explícitamente con [NoTrackNavigation] (auth, errores, la acción Volver).
public class NavigationTrackingFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.HttpContext.Request.Method != HttpMethods.Get) return;
        if (context.Result is not ViewResult) return;

        if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            if (descriptor.MethodInfo.GetCustomAttributes(typeof(NoTrackNavigationAttribute), true).Length > 0) return;
            if (descriptor.ControllerTypeInfo.GetCustomAttributes(typeof(NoTrackNavigationAttribute), true).Length > 0) return;
        }

        var request = context.HttpContext.Request;
        var url = request.Path + request.QueryString;
        NavegacionHistorial.Registrar(context.HttpContext.Session, url);
    }
}
