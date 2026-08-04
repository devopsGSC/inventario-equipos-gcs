namespace InventarioTI.Filters;

// Excluye una acción o controlador de la pila de navegación usada por
// "Volver" (ver NavigationTrackingFilter): páginas de autenticación,
// errores y la propia acción de Volver no deben quedar en la pila.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class NoTrackNavigationAttribute : Attribute
{
}
