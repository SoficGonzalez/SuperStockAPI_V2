namespace SuperStock.Domain.Entities
{
    /// <summary>
    /// Resultado paginado generico. Encapsula los datos de una pagina
    /// junto con la metadata de paginacion para los controllers.
    /// </summary>
    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
    }
}
