namespace InventarioTI.ViewModels;

public class PaginacionViewModel
{
    public int PaginaActual  { get; set; } = 1;
    public int TotalPaginas  { get; set; }
    public int TotalRegistros { get; set; }
    public int TamañoPagina  { get; set; } = 10;

    public bool TienePaginaAnterior => PaginaActual > 1;
    public bool TienePaginaSiguiente => PaginaActual < TotalPaginas;
    public int RegistroDesde => (PaginaActual - 1) * TamañoPagina + 1;
    public int RegistroHasta => Math.Min(PaginaActual * TamañoPagina, TotalRegistros);

    // Páginas a mostrar en el paginador (máx 5 botones)
    public IEnumerable<int> Paginas()
    {
        int start = Math.Max(1, PaginaActual - 2);
        int end   = Math.Min(TotalPaginas, start + 4);
        start     = Math.Max(1, end - 4);
        return Enumerable.Range(start, end - start + 1);
    }
}
